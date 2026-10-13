using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Win32;

namespace BenMcLean.Wolf3D.Shared;

/// <summary>
/// Finds read-only copies of game data installed by GOG, Steam and itch.io, driven by the
/// game XML's Gog/GogDir, Steam/SteamDir and Itch/ItchDir attributes. The ID attributes
/// (comma-separated GOG product IDs, Steam app IDs, one itch.io slug) identify the install, in
/// priority order. The Dir attributes (folders separated by |, matched to the IDs by position, default:
/// the install root) give the folder inside that install which holds the game data.
/// Directories that don't exist are skipped.
/// Candidates are tried in the fixed order Steam, GOG, itch.io; the games folder is always
/// checked by the caller before any of these.
/// Only reads ever happen from these locations. Config, high scores and save games always
/// stay in the games folder. Supports Windows and Linux (including SteamOS and Flatpak apps).
/// </summary>
public static partial class InstalledGameLocator
{
	private static readonly Lazy<IReadOnlyList<string>> SteamLibraries = new(FindSteamLibraries);
	private static readonly Lazy<IReadOnlyList<string>> ItchAppsFolders = new(FindItchAppsFolders);
	/// <summary>
	/// Returns the first existing directory installed by Steam, then GOG, then itch.io for which
	/// <paramref name="hasGameData"/> is true, or null if there is none.
	/// </summary>
	public static string FindGameDataDirectory(XElement game, Func<string, bool> hasGameData) =>
		game is null || OperatingSystem.IsAndroid()
			? null
			: Attempt(() => Candidates(game).Where(Directory.Exists).Where(hasGameData).Take(1)).FirstOrDefault();
	/// <summary>
	/// Reading the registry, installed-game records and library files can fail in many ways
	/// (missing, locked, malformed, access denied), and any failure just means "not found".
	/// LINQ is lazy, so the query is enumerated inside the try to keep failures from escaping.
	/// </summary>
	private static T[] Attempt<T>(Func<IEnumerable<T>> read)
	{
		try
		{
			return [.. read()];
		}
		catch
		{
			return [];
		}
	}
	private static IEnumerable<string> Candidates(XElement game)
	{
		string[] steamDirs = DirList(game, "SteamDir"),
			itchDirs = DirList(game, "ItchDir");
		// Steam officially supports Linux and GOG does not, so Steam goes first on every platform.
		return List(game, "Steam")
			.SelectMany((appId, index) => SteamRoots(appId).Select(root => Path.Combine(root, DirAt(steamDirs, index))))
			.Concat(List(game, "Gog")
				.SelectMany((productId, index) => GogRoots(productId).Select(root => Path.Combine(root, DirAt(DirList(game, "GogDir"), index)))))
			.Concat(ItchRoots(game.Attribute("Itch")?.Value?.Trim()).Select(root => Path.Combine(root, DirAt(itchDirs, 0))));
	}
	// Dir lists are separated by '|' because, unlike ',', it can't appear in a Windows path.
	// Entries pair with the ID at the same position; empty entries are kept so positions line up.
	private static string[] DirList(XElement game, string attribute) =>
		game.Attribute(attribute)?.Value.Split('|', StringSplitOptions.TrimEntries) ?? [];
	// An ID without a matching Dir entry uses the install folder itself.
	private static string DirAt(string[] dirs, int index) => index < dirs.Length ? dirs[index] : string.Empty;
	private static string[] List(XElement game, string attribute) =>
		game.Attribute(attribute)?.Value
			.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
		?? [];
	private static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
	private static bool IsFilled(string value) => !string.IsNullOrWhiteSpace(value);
	[SupportedOSPlatform("windows")]
	private static string RegistryString(RegistryKey hive, string subKey, string name)
	{
		try
		{
			using RegistryKey key = hive.OpenSubKey(subKey);
			return key?.GetValue(name) as string;
		}
		catch
		{
			return null;
		}
	}
	#region GOG
	// Windows: the registry names each install exactly by product ID.
	// Linux: GOG has no official client, so only Heroic's record of its installs is checked.
	private static IEnumerable<string> GogRoots(string productId) =>
		OperatingSystem.IsWindows() ? GogRegistryPaths(productId)
		: OperatingSystem.IsLinux() ? HeroicInstallPaths(productId)
		: [];
	[SupportedOSPlatform("windows")]
	private static IEnumerable<string> GogRegistryPaths(string productId) =>
		new[] { @"SOFTWARE\WOW6432Node\GOG.com\Games\", @"SOFTWARE\GOG.com\Games\" }
			.Select(key => RegistryString(Registry.LocalMachine, key + productId, "path"))
			.Where(IsFilled);
	private static IEnumerable<string> HeroicInstallPaths(string productId) =>
		string.IsNullOrEmpty(Home) ? [] : new[]
		{
			Path.Combine(Home, ".config", "heroic", "gog_store", "installed.json"),
			Path.Combine(Home, ".var", "app", "com.heroicgameslauncher.hgl", "config", "heroic", "gog_store", "installed.json"),
		}
			.Where(File.Exists)
			.SelectMany(file => Attempt(() => ReadHeroicInstallPaths(file, productId)));
	// Materialized before the document is disposed.
	private static string[] ReadHeroicInstallPaths(string file, string productId)
	{
		using JsonDocument document = JsonDocument.Parse(File.ReadAllText(file));
		JsonElement root = document.RootElement,
			entries = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("installed", out JsonElement list) ? list : root;
		return entries.ValueKind != JsonValueKind.Array ? [] :
			[.. entries.EnumerateArray()
				.Where(entry => entry.ValueKind == JsonValueKind.Object &&
					entry.TryGetProperty("appName", out JsonElement appName) &&
					appName.ToString() == productId)
				.Select(entry => entry.TryGetProperty("install_path", out JsonElement installPath) ? installPath.GetString() : null)
				.Where(IsFilled)];
	}
	#endregion GOG
	#region Steam
	private static IEnumerable<string> SteamRoots(string appId) =>
		!appId.All(char.IsAsciiDigit) ? [] :
			SteamLibraries.Value
				.Select(library => Path.Combine(library, "steamapps"))
				.Select(steamApps => (steamApps, installDir: ReadSteamInstallDir(Path.Combine(steamApps, $"appmanifest_{appId}.acf"))))
				.Where(app => app.installDir is not null)
				.Select(app => Path.Combine(app.steamApps, "common", app.installDir));
	private static string ReadSteamInstallDir(string manifest) =>
		Attempt(() => File.Exists(manifest)
			? VdfInstallDirRegex().Matches(File.ReadAllText(manifest)).Select(match => match.Groups[1].Value)
			: [])
		.FirstOrDefault();
	private static IReadOnlyList<string> FindSteamLibraries() =>
		// Resolve symlinks like ~/.steam/steam so the same library isn't visited twice. Additional
		// libraries (other drives, SteamOS SD card) are listed in each Steam folder's libraryfolders.vdf.
		[.. SteamRootCandidates()
			.Where(Directory.Exists)
			.Select(ResolveLink)
			.SelectMany(steamRoot => SteamLibraryFolders(steamRoot)
				.Where(Directory.Exists)
				.Select(ResolveLink)
				.Prepend(steamRoot))
			.Distinct(StringComparer.OrdinalIgnoreCase)];
	private static IEnumerable<string> SteamRootCandidates() =>
		OperatingSystem.IsWindows() ? SteamRegistryPaths().Concat(
		[
			Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"),
			Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam"),
		])
		// SteamOS and most distros use ~/.local/share/Steam; ~/.steam/* are symlinks to it.
		: OperatingSystem.IsLinux() && !string.IsNullOrEmpty(Home) ?
		[
			Path.Combine(Home, ".local", "share", "Steam"),
			Path.Combine(Home, ".steam", "steam"),
			Path.Combine(Home, ".steam", "root"),
			Path.Combine(Home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam"),
			Path.Combine(Home, "snap", "steam", "common", ".local", "share", "steam"),
		]
		: [];
	[SupportedOSPlatform("windows")]
	private static IEnumerable<string> SteamRegistryPaths() =>
		new[]
		{
			RegistryString(Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath"),
			RegistryString(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"),
			RegistryString(Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath"),
		}.Where(IsFilled);
	private static string ResolveLink(string path)
	{
		try
		{
			return new DirectoryInfo(path).ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? Path.GetFullPath(path);
		}
		catch
		{
			return path;
		}
	}
	private static IEnumerable<string> SteamLibraryFolders(string steamRoot) =>
		Attempt(() =>
		{
			string vdf = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
			return File.Exists(vdf)
				? VdfPathRegex().Matches(File.ReadAllText(vdf))
					.Select(match => match.Groups[1].Value.Replace(@"\\", @"\"))
					.Where(IsFilled)
				: [];
		});
	[GeneratedRegex("\"path\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase)]
	private static partial Regex VdfPathRegex();
	[GeneratedRegex("\"installdir\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase)]
	private static partial Regex VdfInstallDirRegex();
	#endregion Steam
	#region itch.io
	private static IEnumerable<string> ItchRoots(string slug) =>
		string.IsNullOrEmpty(slug)
			? []
			: ItchAppsFolders.Value.Select(apps => Path.Combine(apps, slug));
	private static IReadOnlyList<string> FindItchAppsFolders() =>
		[.. (OperatingSystem.IsWindows() ?
			[Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "itch", "apps")]
		: OperatingSystem.IsLinux() && !string.IsNullOrEmpty(Home) ?
			[
				Path.Combine(Home, ".config", "itch", "apps"),
				Path.Combine(Home, ".var", "app", "io.itch.itch", "config", "itch", "apps"),
			]
		: Array.Empty<string>())
			.Where(Directory.Exists)];
	#endregion itch.io
}

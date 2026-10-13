using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace BenMcLean.Wolf3D.Shared;

/// <summary>
/// Resolves the effective set of game XML definitions by combining embedded official XML
/// with optional user XML overrides/additions from the writable games directory.
/// </summary>
public static class GameCatalog
{
	private const string EmbeddedResourcePrefix = "BenMcLean.Wolf3D.Shared.Resources.",
		SharewareFileName = "WL1.xml",
		SharewareZipResource = "BenMcLean.Wolf3D.Shared.Resources.Wolfenstein3dV14sw.ZIP";
	/// <param name="InstalledDataDirectory">
	/// Read-only GOG/Steam/itch.io install directory used when the games folder lacks the game data; null otherwise.
	/// </param>
	public sealed record GameDefinition(string XmlPath, XElement Xml, bool IsEmbedded, string InstalledDataDirectory = null)
	{
		public string FileName => Path.GetFileName(XmlPath);
		public string DisplayName => Xml.Attribute("Name")?.Value ?? Path.GetFileNameWithoutExtension(XmlPath);
		/// <summary>
		/// The games folder location for this game's data. Config, high scores and save games
		/// are always read from and written to here, never to an install.
		/// </summary>
		public string LocalDataDirectory => Path.Combine(
			Path.GetDirectoryName(XmlPath) ?? string.Empty,
			Xml.Attribute("Path")?.Value ?? string.Empty);
		/// <summary>
		/// Where read-only game data is loaded from: the games folder if it has the data,
		/// otherwise an installed copy found through the Install element.
		/// </summary>
		public string GameDataDirectory => InstalledDataDirectory ?? LocalDataDirectory;
	}
	private static readonly Lazy<IReadOnlyDictionary<string, string>> EmbeddedXmlResources = new(() =>
		Assembly.GetExecutingAssembly()
			.GetManifestResourceNames()
			.Where(name =>
				name.StartsWith(EmbeddedResourcePrefix, StringComparison.OrdinalIgnoreCase) &&
				name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
			.ToDictionary(
				keySelector: name => name[EmbeddedResourcePrefix.Length..],
				elementSelector: name => name,
				comparer: StringComparer.OrdinalIgnoreCase));
	private static readonly Lazy<HashSet<string>> EmbeddedSharewareFiles = new(LoadEmbeddedSharewareFiles);
	/// <summary>
	/// Returns the merged, playable game list for the selection menu.
	/// Embedded official XML is considered first, then user XML files override/add by filename.
	/// </summary>
	public static IReadOnlyList<GameDefinition> GetAvailableGames(string gamesDirectory)
	{
		string fullGamesDirectory = Path.GetFullPath(gamesDirectory);
		Dictionary<string, GameDefinition> merged = new(StringComparer.OrdinalIgnoreCase);
		foreach ((string fileName, string resourceName) in EmbeddedXmlResources.Value.OrderBy(kvp => kvp.Key))
			if (TryLoadEmbeddedGameDefinition(fullGamesDirectory, fileName, resourceName, out GameDefinition definition) &&
				IsPlayable(definition))
				merged[fileName] = definition;
		foreach (string xmlPath in EnumerateUserXmlFiles(fullGamesDirectory))
		{
			string fileName = Path.GetFileName(xmlPath);
			if (TryLoadFileGameDefinition(xmlPath, out GameDefinition definition) &&
				IsPlayable(definition))
				merged[fileName] = definition;
			else
				merged.Remove(fileName);
		}
		return [.. merged.Values
			.OrderBy(def => !Path.GetFileNameWithoutExtension(def.XmlPath).Equals("WL1", StringComparison.OrdinalIgnoreCase))
			.ThenBy(def => Path.GetFileNameWithoutExtension(def.XmlPath), StringComparer.OrdinalIgnoreCase)];
	}
	/// <summary>
	/// Resolves the effective XML definition for a logical path inside the games directory.
	/// User XML files take precedence; otherwise embedded official XML is used when available.
	/// </summary>
	public static GameDefinition Resolve(string xmlPath, bool preferEmbeddedOfficial = false)
	{
		string fullPath = Path.GetFullPath(xmlPath),
			fileName = Path.GetFileName(fullPath);
		if (preferEmbeddedOfficial &&
			EmbeddedXmlResources.Value.TryGetValue(fileName, out string preferredResourceName) &&
			TryLoadEmbeddedGameDefinition(Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory(), fileName, preferredResourceName, out GameDefinition preferredDefinition))
			return preferredDefinition;
		if (File.Exists(fullPath))
		{
			if (TryLoadFileGameDefinition(fullPath, out GameDefinition fileDefinition))
				return fileDefinition;
			throw new FileNotFoundException($"Unable to parse game XML: {fullPath}", fullPath);
		}
		if (EmbeddedXmlResources.Value.TryGetValue(fileName, out string resourceName) &&
			TryLoadEmbeddedGameDefinition(Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory(), fileName, resourceName, out GameDefinition embeddedDefinition))
			return embeddedDefinition;
		throw new FileNotFoundException($"Unable to locate game XML: {fullPath}", fullPath);
	}
	private static IEnumerable<string> EnumerateUserXmlFiles(string gamesDirectory)
	{
		try
		{
			return [.. Directory.EnumerateFiles(gamesDirectory, "*.xml")
				.Where(path => !Path.GetFileName(path).Equals("WOLF3D.xsd", StringComparison.OrdinalIgnoreCase))
				.Select(Path.GetFullPath)];
		}
		catch
		{
			return [];
		}
	}
	private static bool TryLoadFileGameDefinition(string xmlPath, out GameDefinition definition)
	{
		try
		{
			XElement xml = Assets.GameXmlResolver.Load(
				xmlPath,
				baseReference =>
				{
					string baseFileName = Path.GetFileName(baseReference);
					if (string.IsNullOrWhiteSpace(baseFileName) ||
						!EmbeddedXmlResources.Value.TryGetValue(baseFileName, out string baseResourceName))
						throw new FileNotFoundException(
							$"Unable to resolve embedded base game XML '{baseReference}' for '{xmlPath}'.",
							baseReference);
					return LoadEmbeddedGameDefinitionXml(
						fileName: baseFileName,
						resourceName: baseResourceName,
						visited: new HashSet<string>(StringComparer.OrdinalIgnoreCase));
				});
			definition = WithInstalledFallback(new(xmlPath, xml, IsEmbedded: false));
			return true;
		}
		catch
		{
			definition = default!;
			return false;
		}
	}
	private static bool TryLoadEmbeddedGameDefinition(string gamesDirectory, string fileName, string resourceName, out GameDefinition definition)
	{
		try
		{
			XElement xml = LoadEmbeddedGameDefinitionXml(fileName, resourceName, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
			definition = WithInstalledFallback(new(Path.Combine(gamesDirectory, fileName), xml, IsEmbedded: true));
			return true;
		}
		catch
		{
			definition = default!;
			return false;
		}
	}
	private static XElement LoadEmbeddedGameDefinitionXml(string fileName, string resourceName, HashSet<string> visited)
	{
		if (!visited.Add(fileName))
			throw new InvalidDataException($"Circular embedded game XML inheritance detected involving '{fileName}'.");
		try
		{
			using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
				?? throw new InvalidDataException($"Missing embedded resource {resourceName}.");
			XElement xml = XDocument.Load(stream).Root
				?? throw new InvalidDataException($"Missing root element in embedded resource {resourceName}.");
			return Assets.GameXmlResolver.Resolve(xml, baseReference =>
			{
				string baseFileName = Path.GetFileName(baseReference);
				if (string.IsNullOrWhiteSpace(baseFileName) ||
					!EmbeddedXmlResources.Value.TryGetValue(baseFileName, out string baseResourceName))
					throw new FileNotFoundException(
						$"Unable to resolve embedded base game XML '{baseReference}' for '{fileName}'.",
						baseReference);
				return LoadEmbeddedGameDefinitionXml(baseFileName, baseResourceName, visited);
			});
		}
		finally
		{
			visited.Remove(fileName);
		}
	}
	private static bool IsPlayable(GameDefinition definition) =>
		CanUseEmbeddedSharewareData(definition)
		|| HasExternalGameData(definition);
	/// <summary>
	/// Priority: the games folder overrides everything; otherwise the first Steam, then GOG,
	/// then itch.io install named by the game's Gog, Steam and Itch attributes that has the data. Read-only fallback:
	/// config, high scores and save games still go to the games folder.
	/// </summary>
	private static GameDefinition WithInstalledFallback(GameDefinition definition)
	{
		if (HasGameDataIn(definition.Xml, definition.LocalDataDirectory))
			return definition;
		string installed = InstalledGameLocator.FindGameDataDirectory(
			definition.Xml,
			directory => HasGameDataIn(definition.Xml, directory));
		return installed is null ? definition : definition with { InstalledDataDirectory = installed };
	}
	public static bool HasExternalGameData(GameDefinition definition) =>
		HasGameDataIn(definition.Xml, definition.GameDataDirectory);
	// A file counts if it exists under any of the game's Extension variants, the same rule the
	// asset loader uses, so a folder the loader could read is never rejected here.
	private static bool HasGameDataIn(XElement xml, string directory) =>
		Directory.Exists(directory) &&
		RequiredFileNames(xml).All(fileName => Assets.GameFiles.Resolve(
			Path.Combine(directory, fileName),
			Assets.GameFiles.ParseExtensions(xml),
			File.Exists) is not null);
	// Only files the XML lists are required: a game in development omits the ones it doesn't have yet.
	// GameXmlResolver rejects XML without a VgaGraph, so this is never empty for a loaded game.
	private static string[] RequiredFileNames(XElement xml) =>
		[.. new (string Element, string[] Attributes)[]
			{
				("VgaGraph", ["VgaDict", "VgaGraph", "VgaHead"]),
				("Maps", ["MapHead", "GameMaps"]),
				("Audio", ["AudioHead", "AudioT"]),
				("VSwap", ["Name"]),
			}
			.SelectMany(section => section.Attributes
				.Select(attribute => xml.Element(section.Element)?.Attribute(attribute)?.Value))
			.Where(fileName => !string.IsNullOrWhiteSpace(fileName))];
	public static bool CanUseEmbeddedSharewareData(GameDefinition definition) =>
		definition.FileName.Equals(SharewareFileName, StringComparison.OrdinalIgnoreCase) &&
		RequiredFileNames(definition.Xml).All(EmbeddedSharewareFileExists);
	private static bool EmbeddedSharewareFileExists(string path) => EmbeddedSharewareFiles.Value.Contains(Path.GetFileName(path));
	private static HashSet<string> LoadEmbeddedSharewareFiles()
	{
		HashSet<string> files = new(StringComparer.OrdinalIgnoreCase);
		using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(SharewareZipResource);
		if (stream is null)
			return files;
		using System.IO.Compression.ZipArchive archive = new(stream, System.IO.Compression.ZipArchiveMode.Read);
		foreach (System.IO.Compression.ZipArchiveEntry entry in archive.Entries)
			if (!string.IsNullOrEmpty(entry.Name))
				files.Add(entry.Name);
		return files;
	}
}

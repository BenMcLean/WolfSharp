using System;
using System.IO;
using System.Linq;
using Godot;

namespace BenMcLean.Wolf3D.VR;

/// <summary>
/// Centralized runtime option parsing for command-line flags and environment overrides.
/// </summary>
public static class RuntimeOptions
{
	public static readonly Vector2I SpectatorResolution = new(1920, 1080);
	/// <summary>
	/// Returns command-line arguments from both Godot's normal argument list and the
	/// user-argument list that appears after `--`.
	/// This allows project-specific options to coexist with engine options such as
	/// MovieWriter flags.
	/// </summary>
	public static string[] GetAllCommandLineArgs() => [.. OS.GetCmdlineArgs(), .. OS.GetCmdlineUserArgs()];
	/// <summary>
	/// Returns true when the desktop spectator view should replace the default VR mirror.
	/// Disabled by default because it adds an extra 3D render pass.
	/// Enable with --spectator or WOLF3D_VR_SPECTATOR=1/true/yes/on.
	/// </summary>
	public static bool SpectatorViewEnabled
	{
		get
		{
			string[] args = GetAllCommandLineArgs();
			if (args.Contains("--spectator"))
				return true;
			if (args.Contains("--no-spectator"))
				return false;
			return IsTruthy(System.Environment.GetEnvironmentVariable("WOLF3D_VR_SPECTATOR"));
		}
	}
	/// <summary>
	/// Returns the directory containing game XML definition files and game data subdirectories.
	/// Override with --path &lt;path&gt; or just a bare positional argument (absolute or relative
	/// to the executable directory). --path takes priority over a bare argument.
	/// Defaults:
	///   Android (Quest): /sdcard/WOLF3D
	///   Editor: ../../games relative to CWD (resolves to repo games/ folder)
	///   Linux AppImage: games/ subfolder next to the .AppImage file itself, not the temporary
	///     FUSE mount OS.GetExecutablePath() would otherwise resolve to
	///   PC export: games/ subfolder next to the executable
	/// </summary>
	public static string Path
	{
		get
		{
			string[] args = GetAllCommandLineArgs();
			string positional = null;
			for (int i = 0; i < args.Length; i++)
			{
				if (args[i] == "--path" && i < args.Length - 1)
					return ResolveGamesPath(args[++i]);
				if (!args[i].StartsWith("--") && !args[i].StartsWith("uid:"))
					positional ??= args[i];
			}
			return positional != null ? ResolveGamesPath(positional) : DefaultGamesDir();
		}
	}
	private static string ResolveGamesPath(string path) =>
		System.IO.Path.IsPathRooted(path)
			? path
			: System.IO.Path.GetFullPath(path, BaseDir());
	private static string DefaultGamesDir() =>
		OS.HasFeature("android") ? "/sdcard/WOLF3D"
		: OS.HasFeature("editor") ? System.IO.Path.GetFullPath(System.IO.Path.Combine("..", "..", "games"))
		: System.IO.Path.Combine(BaseDir(), "games");
	/// <summary>
	/// Directory used as the base for relative --path arguments and the default games folder.
	/// When running from an AppImage, OS.GetExecutablePath() resolves to a temporary, read-only
	/// FUSE mount rather than the .AppImage file's actual location, so the AppImage runtime's
	/// APPIMAGE environment variable (absolute path to the .AppImage file) is used instead.
	/// </summary>
	private static string BaseDir() =>
		System.Environment.GetEnvironmentVariable("APPIMAGE") is string appImagePath && appImagePath.Length > 0
			? System.IO.Path.GetDirectoryName(appImagePath)
			: System.IO.Path.GetDirectoryName(OS.GetExecutablePath());
	private static bool IsTruthy(string value) =>
		!string.IsNullOrWhiteSpace(value) &&
		value.Trim().ToLowerInvariant() is "1" or "true" or "yes" or "on";
}

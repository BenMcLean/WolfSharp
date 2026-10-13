using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace BenMcLean.Wolf3D.Assets;

/// <summary>
/// Resolves a game data file name from the game XML to the file that actually exists, using the
/// game's Extension attribute. When several extensions are listed (e.g. Extension="SD2,SOD"),
/// they are tried in the order listed, so the most specific one goes first, and the file name as
/// written in the XML is the last resort.
/// This lets one XML read both layouts of Spear of Destiny Mission 2: GOG keeps its files in a
/// folder of their own as *.SOD, while Steam keeps all the missions side by side with the shared
/// files as *.SOD and the mission-specific ones as *.SD1, *.SD2 and *.SD3. Steam's *.SOD
/// mission-specific files are a copy of whichever mission was last selected, so they must lose to
/// the specific extension, which is why it is listed first.
/// </summary>
public static class GameFiles
{
	public static string[] ParseExtensions(XElement xml) =>
		(xml.Attribute("Extension")?.Value ?? "")
			.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
	/// <summary>
	/// The names to try for a file, in order of preference. A game with a single extension has no
	/// alternatives, so the file name is used exactly as written.
	/// </summary>
	public static IEnumerable<string> Candidates(string path, string[] extensions) =>
		extensions.Length < 2
			? [path]
			: [.. extensions.Select(extension => Path.Combine(
					Path.GetDirectoryName(path) ?? "",
					Path.GetFileNameWithoutExtension(path) + "." + extension)),
				path];
	/// <summary>
	/// The preferred existing file for <paramref name="path"/>, or null if none of the candidates exist.
	/// </summary>
	public static string Resolve(string path, string[] extensions, Func<string, bool> fileExists) =>
		Candidates(path, extensions).FirstOrDefault(fileExists);
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace BenMcLean.Wolf3D.Assets;

/// <summary>
/// Resolves game XML inheritance chains declared with a Base attribute.
/// When Base is present, Name, Path, Extension and the install attributes (Gog, GogDir, Steam,
/// SteamDir, Itch, ItchDir) override the base definition and all other local content is ignored.
/// </summary>
public static class GameXmlResolver
{
	public const string BaseAttributeName = "Base";
	public static XElement Load(string xmlPath, Func<string, XElement> fallbackBaseXml = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(xmlPath);
		return LoadFromFile(
			Path.GetFullPath(xmlPath),
			new HashSet<string>(StringComparer.OrdinalIgnoreCase),
			fallbackBaseXml);
	}
	public static XElement Resolve(XElement xml, Func<string, XElement> loadBaseXml)
	{
		ArgumentNullException.ThrowIfNull(xml);
		ArgumentNullException.ThrowIfNull(loadBaseXml);
		string baseReference = xml.Attribute(BaseAttributeName)?.Value;
		if (string.IsNullOrWhiteSpace(baseReference))
			return RequireVgaGraph(new XElement(xml));
		ValidateDerivedStub(xml);
		XElement merged = new(loadBaseXml(baseReference.Trim()));
		CopyAttributeIfPresent(xml, merged, "Name");
		CopyAttributeIfPresent(xml, merged, "Path");
		CopyAttributeIfPresent(xml, merged, "Extension");
		// A derived game's install attributes replace the base's as a set, because the base's
		// directories (e.g. GogDir) describe the base game's data, not the derived game's.
		if (InstallAttributeNames.Any(name => xml.Attribute(name) is not null))
		{
			foreach (string name in InstallAttributeNames)
				merged.Attribute(name)?.Remove();
			foreach (string name in InstallAttributeNames)
				CopyAttributeIfPresent(xml, merged, name);
		}
		return merged;
	}
	// Every game draws its menus from VGAGRAPH, so a definition without it is invalid. Maps, Audio and
	// VSwap are optional. A derived game inherits a base that was already checked when it was resolved.
	private static XElement RequireVgaGraph(XElement xml) =>
		new[] { "VgaDict", "VgaGraph", "VgaHead" }
			.All(name => !string.IsNullOrWhiteSpace(xml.Element("VgaGraph")?.Attribute(name)?.Value))
			? xml
			: throw new InvalidDataException(
				"Game XML must define a VgaGraph element with VgaDict, VgaGraph and VgaHead attributes.");
	private static readonly string[] InstallAttributeNames =
		["Gog", "GogDir", "Steam", "SteamDir", "Itch", "ItchDir"];
	private static XElement LoadFromFile(
		string xmlPath,
		HashSet<string> visited,
		Func<string, XElement> fallbackBaseXml)
	{
		if (!visited.Add(xmlPath))
			throw new InvalidDataException($"Circular game XML inheritance detected involving '{xmlPath}'.");
		try
		{
			XElement xml = XDocument.Load(xmlPath).Root
				?? throw new InvalidDataException($"Missing root element in {xmlPath}.");
			string directory = Path.GetDirectoryName(xmlPath) ?? Directory.GetCurrentDirectory();
			return Resolve(xml, baseReference =>
			{
				string basePath = Path.GetFullPath(Path.Combine(directory, baseReference));
				if (File.Exists(basePath))
					return LoadFromFile(basePath, visited, fallbackBaseXml);
				if (fallbackBaseXml is not null)
					return fallbackBaseXml(baseReference);
				throw new FileNotFoundException($"Unable to resolve base game XML '{baseReference}'.", baseReference);
			});
		}
		finally
		{
			visited.Remove(xmlPath);
		}
	}
	private static void ValidateDerivedStub(XElement xml)
	{
		if (string.IsNullOrWhiteSpace(xml.Attribute("Name")?.Value) ||
			string.IsNullOrWhiteSpace(xml.Attribute("Path")?.Value))
			throw new InvalidDataException(
				$"Game XML '{xml.Attribute(BaseAttributeName)?.Value ?? xml.Name.LocalName}' uses Base and must also specify both Name and Path.");
	}
	private static void CopyAttributeIfPresent(XElement source, XElement target, string attributeName)
	{
		XAttribute attribute = source.Attribute(attributeName);
		if (attribute is not null)
			target.SetAttributeValue(attribute.Name, attribute.Value);
	}
}

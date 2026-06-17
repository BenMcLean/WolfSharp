using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using BenMcLean.Wolf3D.Assets;
using BenMcLean.Wolf3D.Assets.Graphics;

if (args.Length < 1)
{
	Console.Error.WriteLine("Usage: FontExporter <game-xml> [output-folder]");
	return 1;
}

string xmlPath = args[0];

if (!File.Exists(xmlPath))
{
	Console.Error.WriteLine($"ERROR: File not found: {xmlPath}");
	return 1;
}

string fullXmlPath = Path.GetFullPath(xmlPath);
XElement xml = GameXmlResolver.Load(fullXmlPath);
string xmlDirectory = Path.GetDirectoryName(fullXmlPath) ?? Directory.GetCurrentDirectory();
string gameDataDirectory = Path.Combine(xmlDirectory, xml.Attribute("Path")?.Value ?? string.Empty);

// Default output: promo/fonts alongside the games/ folder (i.e. one level up from games/)
string outputFolder = args.Length > 1
	? args[1]
	: Path.GetFullPath(Path.Combine(xmlDirectory, "..", "promo", "fonts"));

AssetManager assetManager;
if (HasRequiredVgaGraphFiles(xml, gameDataDirectory))
{
	assetManager = new AssetManager(xml, gameDataDirectory);
}
else
{
	string sharewareZip = Path.GetFullPath(
		Path.Combine(xmlDirectory, "..", "godot", "BenMcLean.Wolf3D.Shared", "Resources", "Wolfenstein3dV14sw.ZIP"));
	string extension = (xml.Attribute("Extension")?.Value ?? "")
		.Split(',', 2, StringSplitOptions.TrimEntries)[0];
	if (File.Exists(sharewareZip) && extension.Equals("WL1", StringComparison.OrdinalIgnoreCase))
	{
		Dictionary<string, byte[]> files = LoadZipEntries(sharewareZip);
		assetManager = AssetManager.Load(
			xml,
			gameDataDirectory,
			path => OpenZipEntry(files, path),
			path => files.ContainsKey(Path.GetFileName(path)));
	}
	else
	{
		assetManager = new AssetManager(xml, gameDataDirectory);
	}
}

VgaGraph? vgaGraph = assetManager.VgaGraph;
if (vgaGraph is null)
{
	Console.Error.WriteLine("ERROR: No VgaGraph element found in XML or required attributes missing.");
	return 1;
}

Directory.CreateDirectory(outputFolder);

List<string> exportedJsonPaths = [];
foreach (KeyValuePair<string, int> kvp in vgaGraph.ChunkFontsByName)
{
	Font font = vgaGraph.Fonts[kvp.Value];
	List<GlyphData> glyphs = [];
	for (int cp = 32; cp < 256; cp++)
	{
		if (font.Widths[cp] == 0)
			continue;
		byte width = font.Widths[cp];
		byte[] rgba = font.Glyphs[cp] ?? [];
		int[] pixels = new int[width * font.Height];
		for (int i = 0; i < pixels.Length; i++)
			pixels[i] = i * 4 < rgba.Length && rgba[i * 4] != 0 ? 1 : 0;
		glyphs.Add(new GlyphData(cp, width, pixels));
	}
	FontData fontData = new(kvp.Key, font.Height, [.. glyphs]);
	string json = JsonSerializer.Serialize(fontData, new JsonSerializerOptions { WriteIndented = true });
	string jsonPath = Path.Combine(outputFolder, kvp.Key + ".json");
	File.WriteAllText(jsonPath, json);
	exportedJsonPaths.Add(jsonPath);
	Console.WriteLine($"Exported {kvp.Key}: {glyphs.Count} glyphs, height {font.Height}px");
}

string scriptPath = Path.Combine(AppContext.BaseDirectory, "wolf3d_font_to_woff2.py");
if (!File.Exists(scriptPath))
{
	Console.Error.WriteLine($"ERROR: {scriptPath} not found. JSON files left in {outputFolder}.");
	return 1;
}

string python = FindPython();
bool anyFailed = false;
foreach (string jsonPath in exportedJsonPaths)
{
	string woff2Path = Path.ChangeExtension(jsonPath, ".woff2");
	try
	{
		using Process process = new();
		process.StartInfo.FileName = python;
		process.StartInfo.UseShellExecute = false;
		process.StartInfo.ArgumentList.Add(scriptPath);
		process.StartInfo.ArgumentList.Add(jsonPath);
		process.StartInfo.ArgumentList.Add(woff2Path);
		process.Start();
		process.WaitForExit();
		if (process.ExitCode == 0)
			File.Delete(jsonPath);
		else
		{
			Console.Error.WriteLine($"ERROR: Python conversion failed for {Path.GetFileName(jsonPath)}. JSON kept.");
			anyFailed = true;
		}
	}
	catch (Exception ex)
	{
		Console.Error.WriteLine($"ERROR: Could not launch '{python}': {ex.Message}");
		Console.Error.WriteLine("Ensure Python 3 is installed and on your PATH, and that you have run:");
		Console.Error.WriteLine("  pip install fonttools brotli");
		anyFailed = true;
		break;
	}
}

return anyFailed ? 1 : 0;

// On Linux/macOS the Python 3 binary is typically "python3"; "python" may be Python 2 or absent.
// On Windows it is typically "python". Try both and use whichever responds successfully.
static string FindPython()
{
	foreach (string candidate in (string[])["python3", "python"])
	{
		try
		{
			using Process probe = new();
			probe.StartInfo.FileName = candidate;
			probe.StartInfo.UseShellExecute = false;
			probe.StartInfo.RedirectStandardOutput = true;
			probe.StartInfo.RedirectStandardError = true;
			probe.StartInfo.ArgumentList.Add("--version");
			probe.Start();
			probe.WaitForExit();
			if (probe.ExitCode == 0)
				return candidate;
		}
		catch (Exception) { }
	}
	return "python3";
}

static bool HasRequiredVgaGraphFiles(XElement xml, string folder)
{
	if (!Directory.Exists(folder))
		return false;
	XElement? el = xml.Element("VgaGraph");
	if (el is null)
		return false;
	foreach (string attr in new[] { "VgaDict", "VgaGraph", "VgaHead" })
	{
		string? fileName = el.Attribute(attr)?.Value;
		if (!string.IsNullOrEmpty(fileName) && !File.Exists(Path.Combine(folder, fileName)))
			return false;
	}
	return true;
}

static Dictionary<string, byte[]> LoadZipEntries(string zipPath)
{
	using ZipArchive archive = ZipFile.OpenRead(zipPath);
	Dictionary<string, byte[]> files = new(StringComparer.OrdinalIgnoreCase);
	foreach (ZipArchiveEntry entry in archive.Entries)
	{
		if (string.IsNullOrEmpty(entry.Name))
			continue;
		using Stream entryStream = entry.Open();
		using MemoryStream memoryStream = new();
		entryStream.CopyTo(memoryStream);
		files[entry.Name] = memoryStream.ToArray();
	}
	return files;
}

static Stream OpenZipEntry(Dictionary<string, byte[]> files, string path)
{
	string name = Path.GetFileName(path);
	if (!files.TryGetValue(name, out byte[]? bytes))
		throw new FileNotFoundException($"Embedded shareware file not found: {name}", path);
	return new MemoryStream(bytes, writable: false);
}

record GlyphData(int Codepoint, int Width, int[] Pixels);
record FontData(string Name, int Height, GlyphData[] Glyphs);

using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace BenMcLean.Wolf3D.GameChecker;

public static class GameCheckerLogic
{
	public record Issue(int Line, int Column, string Context, string Message);

	public static IEnumerable<Issue> Check(string xmlPath)
	{
		using FileStream stream = File.OpenRead(xmlPath);
		string fullPath = Path.GetFullPath(xmlPath);
		string baseDirectory = Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory();
		foreach (Issue issue in Check(stream, fullPath, baseDirectory))
			yield return issue;
	}

	public static IEnumerable<Issue> Check(Stream xmlStream, string context = "<stream>", string? baseDirectory = null)
	{
		#region Schema
		string? schemaPath = FindSchemaPath(baseDirectory);
		if (schemaPath is null)
		{
			yield return new Issue(0, 0, context, "Unable to locate XSD schema for XML validation.");
			yield break;
		}
		XmlSchemaSet schemaSet = new();
		Issue? schemaLoadIssue = null;
		try
		{
			schemaSet.Add(null, schemaPath);
		}
		catch (XmlSchemaException ex)
		{
			schemaLoadIssue = new Issue(ex.LineNumber, ex.LinePosition, schemaPath, $"Failed to parse XSD: {ex.Message}");
		}
		if (schemaLoadIssue is not null)
		{
			yield return schemaLoadIssue;
			yield break;
		}
		List<Issue> schemaIssues = [];
		XmlReaderSettings settings = new()
		{
			Schemas = schemaSet,
			ValidationType = ValidationType.Schema
		};
		settings.ValidationEventHandler += (sender, args) =>
		{
			XmlSchemaException? validationException = args.Exception;
			schemaIssues.Add(new Issue(
				Line: validationException?.LineNumber ?? 0,
				Column: validationException?.LinePosition ?? 0,
				Context: DescribeValidationContext(sender, context),
				Message: args.Message));
		};
		#endregion Schema
		#region XDocument
		XDocument? tryDoc = null;
		XmlException? xmlException = null;
		Exception? loadException = null;
		try
		{
			using XmlReader reader = XmlReader.Create(xmlStream, settings);
			tryDoc = XDocument.Load(reader, LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace);
		}
		catch (XmlException ex)
		{
			xmlException = ex;
		}
		catch (Exception ex)
		{
			loadException = ex;
		}
		// Validation events fire while the document loads, so schema issues are only complete now.
		foreach (Issue issue in schemaIssues)
			yield return issue;
		if (xmlException is not null)
		{
			yield return new Issue(xmlException.LineNumber, xmlException.LinePosition, context, $"Failed to parse XML: {xmlException.Message}");
			yield break;
		}
		if (loadException is not null)
		{
			yield return new Issue(0, 0, context, $"Failed to load XML: {loadException.Message}");
			yield break;
		}
		XDocument doc = tryDoc ?? throw new NullReferenceException();
		#endregion XDocument
		#region Game
		foreach (Issue issue in CheckGame(doc.Root ?? throw new NullReferenceException(), baseDirectory))
			yield return issue;
		#endregion Game
		#region Assets
		HashSet<string> sprites = [.. doc.Names("Sprite")],
			pics = [.. doc.Names("Pic")],
			fonts = [.. doc.Names("Font")],
			sounds = [.. doc.Names("Sound")],
			digis = [.. doc.Names("DigiSound")],
			imfs = [.. doc.Names("Imf").Concat(doc.Names("Midi"))],
			textChunks = [.. doc.Names("TextChunk")],
			states = [.. doc.Names("State")],
			actors = [.. doc.Names("Actor")],
			functions = [.. doc.Names("Function")],
			bonusScripts = [.. doc.Names("BonusScript")];

		if (FindDefaultScriptsDir(baseDirectory) is string scriptsDir)
			foreach (string f in Directory.GetFiles(scriptsDir, "*.lua", SearchOption.AllDirectories))
			{
				string name = Path.GetFileNameWithoutExtension(f);
				functions.Add(name);
				bonusScripts.Add(name);
			}

		static Issue? CheckAttr(XElement el, string attrName, HashSet<string> valid, string typeDesc)
		{
			XAttribute? attr = el.Attribute(attrName);
			if (attr is null || string.IsNullOrEmpty(attr.Value)) return null;
			if (valid.Contains(attr.Value)) return null;
			IXmlLineInfo li = attr;
			return new Issue(
				Line: li.LineNumber,
				Column: li.LinePosition,
				Context: $"<{el.Name.LocalName} {attrName}=\"{attr.Value}\">",
				Message: $"Unknown {typeDesc}: '{attr.Value}'");
		}

		foreach (XElement el in doc.Descendants("State"))
		{
			if (CheckAttr(el, "Shape", sprites, "sprite") is { } i1) yield return i1;
			if (CheckAttr(el, "Next", states, "state") is { } i2) yield return i2;
			if (CheckAttr(el, "Think", functions, "function") is { } i3) yield return i3;
			if (CheckAttr(el, "Action", functions, "function") is { } i4) yield return i4;
		}

		foreach (XElement el in doc.Descendants("Actor"))
		{
			if (CheckAttr(el, "Stand", states, "state") is { } i1) yield return i1;
			if (CheckAttr(el, "Chase", states, "state") is { } i2) yield return i2;
			if (CheckAttr(el, "Attack", states, "state") is { } i3) yield return i3;
			if (CheckAttr(el, "Death", states, "state") is { } i4) yield return i4;
			if (CheckAttr(el, "Pain", states, "state") is { } i5) yield return i5;
			if (CheckAttr(el, "Pain1", states, "state") is { } i6) yield return i6;
			if (CheckAttr(el, "OnAlert", functions, "function") is { } i7) yield return i7;
		}

		foreach (XElement el in doc.Descendants("ObjectType"))
			switch ((string?)el.Attribute("ObClass"))
			{
				case "actor":
					if (CheckAttr(el, "Actor", actors, "actor") is { } i1) yield return i1;
					if (CheckAttr(el, "State", states, "state") is { } i2) yield return i2;
					break;
				case "bonus":
					if (CheckAttr(el, "Script", bonusScripts, "bonus script") is { } i3) yield return i3;
					break;
			}

		foreach (XElement el in doc.Descendants("GameplayWeapon"))
		{
			if (CheckAttr(el, "IdleState", states, "state") is { } i1) yield return i1;
			if (CheckAttr(el, "FireState", states, "state") is { } i2) yield return i2;
			if (CheckAttr(el, "StatusBarPic", pics, "pic") is { } i3) yield return i3;
			if (CheckAttr(el, "FireSound", sounds, "sound") is { } i4) yield return i4;
		}

		foreach (XElement el in doc.Descendants("Map"))
			if (CheckAttr(el, "Music", imfs, "imf") is { } i) yield return i;

		foreach (XElement el in doc.Descendants("Article"))
		{
			if (CheckAttr(el, "ChunkName", textChunks, "text chunk") is { } i1) yield return i1;
			if (CheckAttr(el, "Music", imfs, "imf") is { } i2) yield return i2;
			foreach (XElement pic in el.Elements("Picture"))
				if (CheckAttr(pic, "Name", pics, "pic") is { } i) yield return i;
		}

		foreach (XElement el in doc.Descendants("Menus"))
		{
			if (CheckAttr(el, "Font", fonts, "font") is { } i1) yield return i1;
			if (CheckAttr(el, "CursorPic", pics, "pic") is { } i2) yield return i2;
			if (CheckAttr(el, "EscPressedSnd", sounds, "sound") is { } i3) yield return i3;
			if (CheckAttr(el, "SelectSound", sounds, "sound") is { } i4) yield return i4;
			if (CheckAttr(el, "CursorMoveSound", sounds, "sound") is { } i5) yield return i5;
			if (CheckAttr(el, "Music", imfs, "imf") is { } i6) yield return i6;
			if (CheckAttr(el, "WeaponSprite", sprites, "sprite") is { } i7) yield return i7;
		}

		foreach (XElement el in doc.Descendants("Menu"))
		{
			if (CheckAttr(el, "Font", fonts, "font") is { } i1) yield return i1;
			if (CheckAttr(el, "Music", imfs, "imf") is { } i2) yield return i2;
			foreach (XElement pic in el.Elements("Picture"))
				if (CheckAttr(pic, "Name", pics, "pic") is { } i) yield return i;
			foreach (XElement ss in el.Elements("StaticSprite"))
				if (CheckAttr(ss, "Name", sprites, "sprite") is { } i) yield return i;
			foreach (XElement aa in el.Elements("ActorAnimation"))
				if (CheckAttr(aa, "StartState", states, "state") is { } i) yield return i;
			foreach (XElement ticker in el.Elements("Ticker"))
			{
				if (CheckAttr(ticker, "Font", fonts, "font") is { } i3) yield return i3;
				if (CheckAttr(ticker, "TickSound", sounds, "sound") is { } i4) yield return i4;
				if (CheckAttr(ticker, "DoneSound", sounds, "sound") is { } i5) yield return i5;
				if (CheckAttr(ticker, "PerfectSound", sounds, "sound") is { } i6) yield return i6;
				if (CheckAttr(ticker, "NoBonusSound", sounds, "sound") is { } i7) yield return i7;
			}
			foreach (XElement text in el.Elements("Text"))
				if (CheckAttr(text, "Font", fonts, "font") is { } i) yield return i;
			foreach (XElement item in el.Elements("MenuItem"))
				if (CheckAttr(item, "SelectSound", sounds, "sound") is { } i) yield return i;
		}

		foreach (XElement el in doc.Descendants("StatusBar"))
		{
			if (CheckAttr(el, "Pic", pics, "pic") is { } i1) yield return i1;
			if (CheckAttr(el, "Font", fonts, "font") is { } i2) yield return i2;
			if (CheckAttr(el, "OnFace", functions, "function") is { } i3) yield return i3;
			if (CheckAttr(el, "OnDeath", functions, "function") is { } i4) yield return i4;
			if (CheckAttr(el, "OnNewGame", functions, "function") is { } i5) yield return i5;
			if (CheckAttr(el, "OnMapStart", functions, "function") is { } i6) yield return i6;
			if (CheckAttr(el, "OnCheat", functions, "function") is { } i7) yield return i7;
			foreach (XElement pic in el.Elements("Picture"))
				if (CheckAttr(pic, "Name", pics, "pic") is { } i) yield return i;
		}

		foreach (XElement el in doc.Descendants("VgaGraph"))
			if (CheckAttr(el, "LoadingPic", pics, "pic") is { } i) yield return i;

		foreach (XElement el in doc.Descendants("Door"))
		{
			if (CheckAttr(el, "OpenSound", sounds, "sound") is { } i1) yield return i1;
			if (CheckAttr(el, "CloseSound", sounds, "sound") is { } i2) yield return i2;
			if (CheckAttr(el, "LockedSound", sounds, "sound") is { } i3) yield return i3;
		}

		foreach (XElement el in doc.Descendants("Elevator"))
			if (CheckAttr(el, "Sound", sounds, "sound") is { } i) yield return i;

		foreach (XElement el in doc.Descendants("Pushwall"))
		{
			if (CheckAttr(el, "DigiSound", sounds, "sound") is { } i1) yield return i1;
			if (CheckAttr(el, "BlockedSound", sounds, "sound") is { } i2) yield return i2;
		}

		foreach (XElement el in doc.Descendants("Sound"))
			if (CheckAttr(el, "DigiSound", digis, "digi sound") is { } i)
				yield return i;
		#endregion Assets
	}
	#region Game
	private static readonly string[] StoreAttributes = ["Gog", "GogDir", "Steam", "SteamDir", "Itch", "ItchDir"];
	private static Issue AttributeIssue(XAttribute attribute, string message)
	{
		IXmlLineInfo line = attribute;
		return new Issue(line.LineNumber, line.LinePosition, $"<{attribute.Parent?.Name.LocalName} {attribute.Name.LocalName}=\"{attribute.Value}\">", message);
	}
	private static bool IsOutsideInstall(string folder) =>
		Path.IsPathRooted(folder) || folder.Split('/', '\\').Contains("..");
	/// <summary>
	/// Rules the XSD cannot express and that the loader enforces. A game that breaks the VgaGraph
	/// rule is left out of the game list. Store problems never block a game, they only stop it
	/// being found in a store install.
	/// </summary>
	private static IEnumerable<Issue> CheckGame(XElement game, string? baseDirectory)
	{
		IXmlLineInfo gameLine = game;
		string? baseReference = game.Attribute("Base")?.Value;
		// A game that uses Base inherits its VgaGraph from the base game.
		if (baseReference is null)
		{
			XElement? vgaGraph = game.Element("VgaGraph");
			if (new[] { "VgaDict", "VgaGraph", "VgaHead" }.Any(name => string.IsNullOrWhiteSpace(vgaGraph?.Attribute(name)?.Value)))
				yield return new Issue(gameLine.LineNumber, gameLine.LinePosition, "<Game>",
					"VgaGraph is required: a game must define <VgaGraph> with VgaDict, VgaGraph and VgaHead attributes, because every game draws its menus from VGAGRAPH. Without it the game is invalid and will not appear in the game list.");
		}
		foreach (string store in new[] { "Gog", "Steam" })
		{
			XAttribute? ids = game.Attribute(store),
				dirs = game.Attribute(store + "Dir");
			string[] idList = ids?.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
			if (ids is not null)
			{
				if (idList.Length == 0)
					yield return AttributeIssue(ids, $"{store} lists no IDs.");
				foreach (string id in idList.Where(id => !id.All(char.IsAsciiDigit)))
					yield return AttributeIssue(ids, $"'{id}' is not a numeric {store} ID. Separate IDs with commas.");
			}
			if (dirs is null)
				continue;
			string[] entries = dirs.Value.Split('|', StringSplitOptions.TrimEntries);
			if (ids is null)
				yield return AttributeIssue(dirs, $"{dirs.Name} is ignored because {store} is not set.");
			else if (entries.Length > idList.Length)
				yield return AttributeIssue(dirs, $"{dirs.Name} has {entries.Length} entries but {store} lists {idList.Length} ID(s). Entries are matched to IDs by position, so the extra entries are ignored.");
			foreach (string entry in entries.Where(IsOutsideInstall))
				yield return AttributeIssue(dirs, $"'{entry}' must be a folder inside the install: relative, without '..'. Separate multiple folders with '|'.");
		}
		XAttribute? itch = game.Attribute("Itch"),
			itchDir = game.Attribute("ItchDir");
		if (itch is not null && (string.IsNullOrWhiteSpace(itch.Value) || itch.Value.IndexOfAny([',', '|', '/', '\\', ' ']) >= 0))
			yield return AttributeIssue(itch, "Itch must be the single last part of the game's itch.io URL, such as s3dna.");
		if (itchDir is not null)
		{
			if (itch is null)
				yield return AttributeIssue(itchDir, "ItchDir is ignored because Itch is not set.");
			if (itchDir.Value.Contains('|'))
				yield return AttributeIssue(itchDir, "ItchDir takes a single folder because Itch is a single install.");
			else if (IsOutsideInstall(itchDir.Value.Trim()))
				yield return AttributeIssue(itchDir, $"'{itchDir.Value}' must be a folder inside the install: relative, without '..'.");
		}
		// A derived game's store attributes replace the base's as a set, so inheriting them means
		// looking in folders that hold the base game's data, not this game's.
		if (baseReference is not null &&
			!StoreAttributes.Any(name => game.Attribute(name) is not null) &&
			baseDirectory is not null &&
			Path.Combine(baseDirectory, baseReference) is string basePath &&
			File.Exists(basePath) &&
			XDocument.Load(basePath).Root is XElement baseGame &&
			StoreAttributes.Any(name => baseGame.Attribute(name) is not null))
			yield return new Issue(gameLine.LineNumber, gameLine.LinePosition, "<Game>",
				$"Warning: this game inherits the store attributes (Gog, Steam, Itch) of '{baseReference}', but those point at the base game's data. Set this game's own store attributes.");
	}
	#endregion Game
	#region Utilities
	private static IEnumerable<string> Names(this XDocument doc, string descendants) => doc
		.Descendants(descendants)
		.Select(e => (string?)e.Attribute("Name"))
		.OfType<string>();
	private static string DescribeValidationContext(object? sender, string context) =>
		sender switch
		{
			XElement element => $"<{element.Name.LocalName}>",
			XAttribute attribute when attribute.Parent is XElement parent => $"<{parent.Name.LocalName} {attribute.Name.LocalName}=\"{attribute.Value}\">",
			_ => context
		};
	#endregion Utilities
	#region Paths
	private static string? FindSchemaPath(string? baseDirectory)
	{
		string xmlDirectory = baseDirectory is null
			? Directory.GetCurrentDirectory()
			: Path.GetFullPath(baseDirectory);

		string? dir = xmlDirectory;
		while (dir is not null)
		{
			string siblingCandidate = Path.Combine(dir, "WOLF3D.xsd");
			if (File.Exists(siblingCandidate))
				return siblingCandidate;

			string gamesCandidate = Path.Combine(dir, "games", "WOLF3D.xsd");
			if (File.Exists(gamesCandidate))
				return gamesCandidate;

			dir = Path.GetDirectoryName(dir);
		}

		return null;
	}

	private static string? FindDefaultScriptsDir(string? baseDirectory)
	{
		string? dir = baseDirectory is null
			? Directory.GetCurrentDirectory()
			: Path.GetFullPath(baseDirectory);
		while (dir is not null)
		{
			string candidate = Path.Combine(dir, "src", "BenMcLean.Wolf3D.Simulator", "Lua", "DefaultScripts");
			if (Directory.Exists(candidate)) return candidate;
			dir = Path.GetDirectoryName(dir);
		}
		return null;
	}
	#endregion Paths
}

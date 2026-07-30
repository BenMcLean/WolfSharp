using System;
using System.Reflection;
using MoonSharp.Interpreter;
using BenMcLean.Wolf3D.Assets;

namespace BenMcLean.Wolf3D.MapGenerator.Host;

/// <summary>
/// Hosts the vendored Obsidian Lua level-generation algorithm (see Lua/README.md) inside a
/// MoonSharp VM, replacing Obsidian's C++ engine bindings with a small deterministic shim.
/// This is a thin native-function surface, not a re-implementation of the algorithm itself —
/// see IMPLEMENTATION_PLAN.md for the architecture rationale.
/// </summary>
public sealed class WolfLuaHost
{
	private const int GridSize = 64;
	// g_wolf.cc: NO_TILE / NO_OBJ — matches WOLF.FACTORY.NO_TILE/NO_OBJ in wolf/factory.lua.
	private const ushort NoTile = 48;
	private const ushort NoObj = 0;
	private readonly Script script;
	private readonly RNG rng;
	private readonly ushort[] solidPlane = new ushort[GridSize * GridSize];
	private readonly ushort[] thingPlane = new ushort[GridSize * GridSize];
	public WolfLuaHost(RNG rng)
	{
		this.rng = rng ?? throw new ArgumentNullException(nameof(rng));
		script = new Script(CoreModules.Preset_SoftSandbox);
		// gettext alias used for GUI display labels (e.g. `_("Spear of Destiny")`) — cosmetic, identity is fine.
		script.Globals["_"] = (Func<string, string>)(s => s);
		// OB_GAMES/OB_MODULES registration is generic engine machinery we don't drive from Lua
		// (see IMPLEMENTATION_PLAN.md) — predefine as empty tables so game-def files can register
		// into them without erroring, even though we never read them back.
		script.Globals["OB_GAMES"] = new Table(script);
		script.Globals["OB_MODULES"] = new Table(script);
		Table gui = new(script);
		script.Globals["gui"] = gui;
		gui["random"] = (Func<double>)(() => rng.NextDouble());
		gui["wolf_block"] = (Action<int, int, int, int>)WolfBlock;
		gui["wolf_read"] = (Func<int, int, int, int>)WolfRead;
		gui["v094_begin_wolf_level"] = (Action<string>)(_ => BeginLevel());
		gui["v094_end_wolf_level"] = (Action)(() => { });
		// need-to-know logging policy: printf/debugf are no-ops (variadic, so bound via raw callback)
		gui["printf"] = DynValue.NewCallback((ctx, args) => DynValue.Nil);
		gui["debugf"] = DynValue.NewCallback((ctx, args) => DynValue.Nil);
		gui["ticker"] = (Action)(() => { });
		gui["abort"] = (Func<bool>)(() => false);
		gui["gettext"] = (Func<string, string>)(s => s);
		gui["prog_step"] = (Action<string>)(_ => { });
		gui["at_level"] = (Action<string, int, int>)((name, index, total) => { });
		gui["import"] = (Action<string>)(_ => { }); // files are preloaded explicitly by LoadAlgorithm, in dependency order
		gui["minimap_disable"] = (Action)(() => { });
		gui["minimap_enable"] = (Action)(() => { });
		gui["minimap_begin"] = (Action)(() => { });
		gui["minimap_finish"] = (Action)(() => { });
		// Standard Lua print() routed through gui.printf inside util.lua; nothing further needed here.
	}
	private void BeginLevel()
	{
		// g_wolf.cc:wolf_game_interface_c::BeginLevel — clear both planes before stamping.
		Array.Fill(solidPlane, NoTile);
		Array.Fill(thingPlane, NoObj);
	}
	private void WolfBlock(int x, int y, int tile, int obj)
	{
		// g_wolf.cc:WF_wolf_block — x is 1-based; y is 1-based from the bottom (flipped) in Obsidian's coordinate system.
		x -= 1;
		y = GridSize - y;
		if (x < 0 || x >= GridSize || y < 0 || y >= GridSize)
			throw new InvalidOperationException($"wolf_block coordinates out of range: ({x},{y})");
		solidPlane[y * GridSize + x] = (ushort)tile;
		thingPlane[y * GridSize + x] = (ushort)obj;
	}
	private int WolfRead(int x, int y, int plane)
	{
		x -= 1;
		y = GridSize - y;
		if (x < 0 || x >= GridSize || y < 0 || y >= GridSize)
			return 0;
		return plane switch
		{
			1 => solidPlane[y * GridSize + x],
			2 => thingPlane[y * GridSize + x],
			_ => 0,
		};
	}
	/// <summary>
	/// Loads an embedded Lua source file (path relative to the Lua/ folder, e.g. "094/planner.lua")
	/// and executes it once, in the order the real Obsidian dependency chain requires.
	/// </summary>
	public void LoadEmbeddedScript(string relativePath)
	{
		// MSBuild's embedded-resource naming prefixes an underscore onto any path segment
		// that starts with a digit (e.g. "094" -> "_094"), since it isn't a valid identifier.
		string[] segments = relativePath.Split('/');
		for (int i = 0; i < segments.Length; i++)
			if (segments[i].Length > 0 && char.IsDigit(segments[i][0]))
				segments[i] = "_" + segments[i];
		string resourceName = $"BenMcLean.Wolf3D.MapGenerator.Lua.{string.Join('.', segments)}";
		using System.IO.Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
			?? throw new InvalidOperationException($"Embedded Lua resource not found: {resourceName}");
		using System.IO.StreamReader reader = new(stream);
		script.DoString(reader.ReadToEnd(), codeFriendlyName: relativePath);
	}
	public const int MapSize = GridSize;
	public ushort[] SolidPlane => (ushort[])solidPlane.Clone();
	public ushort[] ThingPlane => (ushort[])thingPlane.Clone();
	public Table Globals => script.Globals;
	public Table NewTable() => new(script);
	public void SetGlobal(string name, DynValue value) => script.Globals[name] = value;
	public DynValue Call(string globalFunctionName, params object[] args)
	{
		DynValue function = script.Globals.Get(globalFunctionName);
		if (function.Type != DataType.Function)
			throw new InvalidOperationException($"Lua global '{globalFunctionName}' is not a function.");
		return script.Call(function, args);
	}
	public DynValue Call(DynValue function, params object[] args) => script.Call(function, args);
	public DynValue CallOn(DynValue table, string methodName, params object[] args)
	{
		DynValue function = table.Table.Get(methodName);
		if (function.Type != DataType.Function)
			throw new InvalidOperationException($"Lua field '{methodName}' is not a function.");
		return script.Call(function, args);
	}
}

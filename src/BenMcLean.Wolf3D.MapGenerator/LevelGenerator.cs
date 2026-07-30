using System;
using MoonSharp.Interpreter;
using BenMcLean.Wolf3D.Assets;
using BenMcLean.Wolf3D.Assets.Gameplay;
using BenMcLean.Wolf3D.MapGenerator.Host;

namespace BenMcLean.Wolf3D.MapGenerator;

/// <summary>
/// Public entry point: (seed, GenerationParameters) -> GameMap.
/// Drives the vendored Obsidian Lua algorithm (see Lua/README.md) through the same three
/// calls the original engine's v094_create_LEVEL makes: plan_sp_level -> build_level ->
/// write_wolf_level. See IMPLEMENTATION_PLAN.md for the traced call graph this replicates.
/// </summary>
public static class LevelGenerator
{
	// Load order matters: each file's top-level code runs immediately and may reference
	// globals/functions defined by files loaded before it (WOLF must exist before factory.lua;
	// util.lua's sel/int/rand/table helpers must exist before anything that uses them at load time).
	private static readonly string[] ScriptLoadOrder =
	[
		"util.lua",
		"094/defs.lua",
		"wolf/factory.lua",
		"wolf/x_spear.lua",
		"094/a_star.lua",
		"094/theme.lua",
		"094/planner.lua",
		"094/monster.lua",
		"094/builder.lua",
		"094/writer.lua",
	];
	/// <summary>
	/// Generates a single map by generating the whole episode it belongs to and discarding the
	/// rest. This is NOT a shortcut over <see cref="GenerateEpisode"/> — it costs the same, because
	/// the real Obsidian engine builds all 10 maps of an episode sequentially in one continuous RNG
	/// stream (v094_build_wolf3d_shit), so map 5's build consumes RNG state left over from maps 1-4's
	/// builds. Building "just map 5" by seeking straight to it would silently produce a DIFFERENT map
	/// 5 than a full episode build does, breaking the determinism contract. Prefer
	/// <see cref="GenerateEpisode"/> directly when you need more than one map from the same episode.
	/// </summary>
	public static GameMap Generate(GenerationParameters parameters)
	{
		ArgumentNullException.ThrowIfNull(parameters);
		GameMap[] episode = GenerateEpisode(parameters);
		if (parameters.Map < 1 || parameters.Map > episode.Length)
			throw new ArgumentOutOfRangeException(nameof(parameters), parameters.Map, $"Map must be between 1 and {episode.Length} for this game/episode.");
		return episode[parameters.Map - 1];
	}
	/// <summary>
	/// Generates every map of one episode (GenerationParameters.Episode), in the same order and the
	/// same single continuous RNG stream the real Obsidian engine uses (v094_build_wolf3d_shit ->
	/// v094_create_LEVEL per map), so results match what a full episode build in the original engine
	/// would produce. GenerationParameters.Map is ignored here.
	/// How many maps an episode has is NOT a constant — it's decided by whichever
	/// GAME.FACTORY.level_func the loaded game's factory_setup() installed: base Wolf3D's
	/// WOLF.get_factory_levels() always returns 10 (wolf/factory.lua: "for map = 1,10 do"), but Spear
	/// of Destiny's SPEAR.get_factory_levels() (x_spear.lua:258) returns however many
	/// SPEAR.FACTORY.EPISODE_INFO[episode].len says — a variable, per-episode, data-driven length.
	/// This method reads however many entries level_func actually returned rather than assuming any
	/// fixed count, so it stays correct once GameGenerationProfile selection (TODO(data-driven-factory)
	/// below) lets it drive something other than WOLF.factory_setup().
	/// Intended to feed BenMcLean.Wolf3D.Assets.AssetManager's Maps/MapAnalyses arrays wholesale —
	/// see IMPLEMENTATION_PLAN.md §10 for the still-open tile/actor-data-convention caveat before
	/// wiring generated output into MapAnalyzer/Simulator against a real loaded game.
	/// </summary>
	public static GameMap[] GenerateEpisode(GenerationParameters parameters)
	{
		ArgumentNullException.ThrowIfNull(parameters);
		RNG rng = new(parameters.SeedA, parameters.SeedB);
		WolfLuaHost host = new(rng);
		// WOLF must exist as an empty table before wolf/factory.lua runs (it does `WOLF.FACTORY = {}`).
		host.SetGlobal("WOLF", DynValue.NewTable(host.NewTable()));
		foreach (string relativePath in ScriptLoadOrder)
			host.LoadEmbeddedScript(relativePath);
		Table obConfig = host.NewTable();
		obConfig["engine"] = "idtech_0";
		obConfig["game"] = "wolf";
		obConfig["length"] = "single";
		obConfig["mode"] = "sp";
		obConfig["health"] = "normal";
		obConfig["ammo"] = "normal";
		obConfig["traps"] = "some";
		host.SetGlobal("OB_CONFIG", DynValue.NewTable(obConfig));
		Table param = host.NewTable();
		// planner.lua's plan_sp_level reads the plain name; factory.lua's get_factory_levels
		// (and its own plan-size table for the wolf-specific 10-level episode layout) reads the
		// _wolf_3d-suffixed name — both must carry the same value.
		param["float_size"] = (double)parameters.LevelSize;
		param["float_size_wolf_3d"] = (double)parameters.LevelSize;
		param["room_size_multiplier_wolf_3d"] = parameters.RoomSizeMultiplier;
		// Monster-quantity multiplier read by planner.lua's toughen_it_up(); 1.0 == Obsidian's "Normal".
		param["float_mons"] = 1.0;
		param["bool_historical_oblige_v2_dm_mode"] = 0;
		host.SetGlobal("PARAM", DynValue.NewTable(param));
		// WOLF.factory_setup() (wolf/factory.lua) builds the entire GAME.FACTORY table from WOLF.FACTORY.*,
		// which is Obsidian's own hardcoded Wolf3D data (tile numbers, combos, actor/thing codes, etc.) —
		// NOT this project's WL1.xml/WL6.xml asset data (see GameXmlResolver, ObjectType/Actor/Door/
		// GameplayWeapon in BenMcLean.Wolf3D.Assets). That means a generated GameMap's tile/object values
		// are Obsidian's own convention and are not guaranteed to match what this project's renderer and
		// Simulator expect from a loaded WL1.xml/WL6.xml-described game.
		// TODO(data-driven-factory): after WOLF.factory_setup() runs, overwrite the relevant
		// GAME.FACTORY sub-tables (TILE_NUMS, combos, things, monsters, weapons, key_doors, exits) with
		// values built from this project's own game XML (via GameXmlResolver / WOLF3D.xsd-described
		// elements) instead of leaving Obsidian's hardcoded wolf/factory.lua data in place. That is what
		// makes MapGenerator work across whichever game XML (WL1/WL6/SOD/N3D) is loaded, rather than only
		// ever generating Obsidian's own idea of Wolf3D. Left as Obsidian's hardcoded data for now to get
		// an end-to-end generator working first; ToughnessFactor below is the only field currently
		// overridden from C#, as a small proof that overriding GAME.FACTORY post-setup is viable.
		DynValue wolfTable = host.Globals.Get("WOLF");
		host.CallOn(wolfTable, "factory_setup");
		Table gameTable = host.Globals.Get("GAME").Table;
		Table gameFactory = gameTable.Get("FACTORY").Table;
		gameFactory["toughness_factor"] = parameters.ToughnessFactor;
		// GAME.FACTORY.level_func builds the level list for one episode (length varies by game — see
		// this method's doc comment), including decide_quests() across the whole list (§7).
		DynValue levelFunc = gameFactory.Get("level_func");
		DynValue levelList = host.Call(levelFunc, parameters.Episode);
		int levelCount = levelList.Table.Length;
		if (levelCount == 0)
			throw new InvalidOperationException($"GAME.FACTORY.level_func returned no levels for episode {parameters.Episode}.");
		GameMap[] maps = new GameMap[levelCount];
		// Sequential, in order, sharing one RNG stream — matches v094_build_wolf3d_shit's per-level loop.
		for (int mapNumber = 1; mapNumber <= levelCount; mapNumber++)
		{
			DynValue level = levelList.Table.Get(mapNumber);
			if (level.Type == DataType.Nil)
				throw new InvalidOperationException($"GAME.FACTORY.level_func returned no entry for map {mapNumber} of {levelCount}.");
			host.Call("plan_sp_level", level, false);
			host.Call("build_level");
			host.Call("write_wolf_level"); // resets and refills the solid/thing planes for this one map
			string name = level.Table.Get("name").String;
			maps[mapNumber - 1] = GameMap.FromGenerated((ushort)(mapNumber - 1), WolfLuaHost.MapSize, WolfLuaHost.MapSize, name, host.SolidPlane, host.ThingPlane, new ushort[WolfLuaHost.MapSize * WolfLuaHost.MapSize]);
		}
		return maps;
	}
}

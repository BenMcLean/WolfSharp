# BenMcLean.Wolf3D.MapGenerator — Implementation Plan

## 1. Purpose

Run the level-generation *algorithm* of the Obsidian / OBLIGE Level Maker (the "id Tech 0" engine target, covering Wolfenstein 3-D, Spear of Destiny, and Super 3D Noah's Ark) inside `BenMcLean.Wolf3D.MapGenerator`, so that VR players can generate and play an infinite stream of procedurally-generated levels without leaving the headset.

**Architecture note (supersedes the original plan):** the first draft of this document called for hand-porting the Lua algorithm to C#. That was abandoned in favor of embedding the actual Obsidian Lua source and executing it via MoonSharp, behind a small native-function shim. See §3 for why, and §4 for what's actually vendored. The rest of this document describes the implementation as built, not as originally proposed.

## 2. Source material (read-only references)

* `Obsidian/scripts/094/planner.lua`, `builder.lua`, `monster.lua`, `writer.lua`, `theme.lua`, `a_star.lua`, `oblige_v094.lua`, `defs.lua` — the v094 engine's quest/room-graph, prefab-stamping, monster-placement, and grid-writing algorithm.
* `Obsidian/scripts/util.lua` — `rand.*`, `table.*`, `geom.*`, `sel`, `int` helpers used throughout.
* `Obsidian/games/wolf/factory.lua`, `x_spear.lua` — Wolf3D/Spear-of-Destiny game data: `TILE_NUMS`, `COMBOS`, `THINGS`, `MONSTERS`, `PREFABS`, `QUESTS`, `ROOMS`, `THEMES`, and `WOLF.factory_setup()`/`WOLF.get_factory_levels()`/`WOLF.decide_quests()`.
* `Obsidian/scripts/obsidian.lua` (`ob_build_cool_shit`, `ob_build_setup`) and `Obsidian/games/wolf/base.lua` — the generic engine bootstrap. **Not vendored** — see §5 for what of this we replicate directly in C# instead.
* `Obsidian/source_files/obsidian_main/m_lua.cc` (`gui_script_funcs[]`) and `g_wolf.cc` — the native C++ functions Lua calls; `g_wolf.cc` is the reference for the two functions that matter (`WF_wolf_block`, `wolf_game_interface_c::BeginLevel`) — see §5.

## 3. Why embed the Lua instead of porting it

Tracing the actual call graph (`Obsidian/scripts/094/oblige_v094.lua:v094_create_LEVEL`) showed the Wolf3D output path is simple: `plan_sp_level()` → `build_level()` → `write_wolf_level()`, where `write_wolf_level()` just walks a 64×64 `PLAN.blocks[x][y]` grid and calls the trivial native function `gui.wolf_block(x,y,tile,obj)` (`g_wolf.cc:WF_wolf_block` — a direct array write, no CSG/brush geometry involved for this game). The `rand.*` calls used throughout planner/builder/monster (`rand.odds`, `rand.irange`, `rand.pick`, etc., all in `scripts/util.lua`) are pure Lua wrapping one native call, `gui.random()`.

Given that, hand-porting ~12,000 lines of intricate quest-graph and prefab-stamping logic to C# carried a much higher bug-risk than running the original algorithm and binding a handful of native functions (`gui.random`, `gui.wolf_block`, `gui.wolf_read`, plus logging/lifecycle no-ops). Binding `gui.random` once makes every `rand.*` call site correct by construction — a manual port would have to get each call site's semantics right individually.

**Licensing (explicit user decision):** Obsidian is GPLv2 (`Obsidian/LICENSE.txt`). `BenMcLean.Wolf3D` is already GPLv3 (`BenMcLean.Wolf3D/LICENSE`), so embedding GPLv2 Lua source is compatible and was explicitly approved. The vendored files keep their original license headers intact (see `Lua/README.md`).

## 4. What's vendored, and what's not

`src/BenMcLean.Wolf3D.MapGenerator/Lua/` contains a verbatim copy (license headers intact) of:

```
Lua/
  util.lua                  <- Obsidian/scripts/util.lua
  094/defs.lua               <- Obsidian/scripts/094/defs.lua
  094/a_star.lua              <- Obsidian/scripts/094/a_star.lua (currently unused by the wolf path; kept for parity)
  094/theme.lua                <- Obsidian/scripts/094/theme.lua
  094/planner.lua                <- Obsidian/scripts/094/planner.lua
  094/monster.lua                  <- Obsidian/scripts/094/monster.lua
  094/builder.lua                    <- Obsidian/scripts/094/builder.lua
  094/writer.lua                       <- Obsidian/scripts/094/writer.lua (write_wolf_level)
  wolf/factory.lua                       <- Obsidian/games/wolf/factory.lua
  wolf/x_spear.lua                         <- Obsidian/games/wolf/x_spear.lua
```

`games/wolf/base.lua` is deliberately **not** vendored: its only load-bearing content for us is `WOLF = {}` (a one-line table we create directly from C#) and `OB_GAMES["wolf"] = {...}` registration, which is part of the generic engine bootstrap we bypass (see §5). Everything else in `base.lua` (the `_()`-labelled GUI registration) is cosmetic.

`Lua/README.md` documents provenance and the "do not hand-edit game-logic behavior here; re-copy from `Obsidian/` if a fix is needed" policy.

## 5. The native shim and the C# driver

`Host/WolfLuaHost.cs` hosts a MoonSharp `Script` (`CoreModules.Preset_SoftSandbox`) and binds:

* `gui.random()` → `RNG.NextDouble()` (the project's existing deterministic PRNG, moved to `BenMcLean.Wolf3D.Assets/RNG.cs` — see §6)
* `gui.wolf_block(x,y,tile,obj)` / `gui.wolf_read(x,y,plane)` → direct reads/writes into two `ushort[4096]` arrays (`g_wolf.cc:WF_wolf_block`'s exact coordinate transform: 1-based x, y flipped from the bottom)
* `gui.v094_begin_wolf_level(name)` → resets both arrays to `NO_TILE`(48)/`NO_OBJ`(0), matching `g_wolf.cc:wolf_game_interface_c::BeginLevel` — **this one bit us during testing**: without it, unbuilt/void cells default to C#'s `0` instead of Obsidian's `NO_TILE`, silently corrupting the output. Confirmed by the "how much of the map got touched" smoke check in §10.
* `gui.printf`/`gui.debugf`/`gui.ticker`/`gui.abort`/`gui.prog_step`/`gui.at_level`/`gui.import`/`gui.minimap_*`/`gui.v094_end_wolf_level` → no-ops (need-to-know logging policy; `abort` always returns false; `import` is a no-op because `LevelGenerator` preloads every file explicitly in dependency order instead of relying on Lua-side `gui.import`)
* `gui.gettext(s)` and global `_(s)` → identity passthrough (only ever compared against sentinel UI strings like `"Mix It Up"`/`"Episodic"` that our numeric `PARAM` values never equal, so these branches are always skipped — by design, see §6)
* `OB_GAMES`, `OB_MODULES` → predefined empty tables so `x_spear.lua`'s `OB_GAMES["spear"] = {...}` registration doesn't error; never read back

**What we deliberately do NOT replicate from `ob_build_setup()`:** the generic module-registration/table-merge machinery (`OB_MODULES`, `ob_merge_table_list`, `ob_sort_modules`) and `Fab_load_all_definitions()`. Tracing `wolf/factory.lua:WOLF.factory_setup()` showed it single-handedly builds the *entire* `GAME.FACTORY` table from `WOLF.FACTORY.*` (tile numbers, combos, things, monsters, prefabs — including prefab size/scale computation and monster power-factor precomputation) with no dependency on that generic machinery. `LevelGenerator.GenerateEpisode()` therefore just calls `WOLF.factory_setup()` directly. This was the single biggest risk called out when this architecture was proposed, and it turned out to be a non-issue — confirmed by the working smoke test in §10.

`LevelGenerator.GenerateEpisode(GenerationParameters)` — the real entry point, `LevelGenerator.cs` — does, in order:
1. Load the ten files in `Lua/` in dependency order (`util.lua` and `094/defs.lua` first; `WOLF = {}` created directly before `wolf/factory.lua` runs, since it does `WOLF.FACTORY = {}`).
2. Set `OB_CONFIG` (`engine="idtech_0"`, `game="wolf"`, `length="single"`, `mode="sp"`, `health="normal"`, `ammo="normal"`, `traps="some"`) and `PARAM` (`float_size` **and** `float_size_wolf_3d` — `planner.lua` reads the plain name, `factory.lua` reads the suffixed one, both must carry the same value; `room_size_multiplier_wolf_3d`; `float_mons=1.0` for `planner.lua`'s monster-quantity multiplier; `bool_historical_oblige_v2_dm_mode=0`).
3. Call `WOLF.factory_setup()` → populates `GAME.FACTORY`; override `GAME.FACTORY.toughness_factor` from `GenerationParameters.ToughnessFactor`.
4. Call `GAME.FACTORY.level_func(episode)` → the episode's level list, complete with `decide_quests()` already applied across the whole episode (see §7 — this is where campaign-level context like boss placement and weapon/key pacing comes from). **The number of levels this returns is read dynamically (`Table.Length`), never assumed** — see the correctness bug this fixes, below.
5. For **every** level in that list, in order, in the same MoonSharp `Script`/`RNG` stream: `plan_sp_level(level, false)` → `build_level()` → `write_wolf_level()` (which itself resets the two planes via `v094_begin_wolf_level` before refilling them), then immediately read back the planes into a `GameMap.FromGenerated(...)` before the next level's `write_wolf_level()` overwrites them. The third plane is always blank, matching `g_wolf.cc:WF_WriteBlankPlane`, which the real engine never fills for Wolf3D either.

`LevelGenerator.Generate(GenerationParameters)` is a convenience wrapper: `GenerateEpisode(parameters)[parameters.Map - 1]`. **It is not a cheaper shortcut** — see the correctness bug below for why it can't be.

**Two correctness bugs found and fixed after the first working version (both by user pushback, not by planning ahead — worth reading if touching this code):**

* **RNG-sequencing bug.** The real engine (`v094_build_wolf3d_shit`) builds every level of an episode sequentially in one continuous RNG stream — level 5's `build_level()` consumes RNG state left over from levels 1-4's builds. The first working version of `LevelGenerator.Generate()` created a fresh `RNG`/`WolfLuaHost` per single-map request and jumped straight to building only the requested map, skipping the RNG draws the preceding levels' builds would have consumed. That silently produced a *different* map N than a real full-episode build would — a determinism-contract violation, not just a missing feature. Fixed by making `GenerateEpisode` the real entry point (always builds every level of the episode, in order, in one stream) and `Generate` a thin slice of its result. Confirmed via the harness in §9: map 5's generated content measurably changed between the old per-map version and the fixed sequential version, for the same seed.
* **Hardcoded episode length.** The first working version hardcoded `for (mapNumber = 1; mapNumber <= 10; ...)` because base Wolf3D's `WOLF.get_factory_levels()` (`wolf/factory.lua`) does exactly that (`for map = 1,10 do`). But Spear of Destiny's `SPEAR.get_factory_levels()` (`x_spear.lua:258`) loops `for map = 1,ep_length do`, where `ep_length` comes from `SPEAR.FACTORY.EPISODE_INFO[episode].len` — a **variable, per-episode, data-driven** length, not a constant. `GenerateEpisode` now reads `levelList.Table.Length` instead of assuming any fixed count, so it stays correct once something other than `WOLF.factory_setup()` can be selected (§11).

## 6. GenerationParameters (`GenerationParameters.cs`)

```csharp
public sealed record GenerationParameters
{
    public ulong SeedA { get; init; }
    public ulong SeedB { get; init; }
    public int LevelSize { get; init; } = 36;              // PARAM.float_size / float_size_wolf_3d ("ob_size")
    public double RoomSizeMultiplier { get; init; } = 1.0;  // PARAM.room_size_multiplier_wolf_3d
    public int Episode { get; init; } = 1;                  // 1-based
    public int Map { get; init; } = 1;                      // 1-10; 9 = boss map, 10 = Pacman bonus map
    public double ToughnessFactor { get; init; } = 0.40;    // GAME.FACTORY.toughness_factor override
}
```

Deliberately kept numeric-only: Obsidian's Lua compares these `PARAM` fields against sentinel strings (`"Mix It Up"`, `"Episodic"`, `"Progressive"`) to pick alternate sizing modes in its GUI-driven flow. A plain number never equals those strings, so passing numbers here always takes the direct/explicit-size code path — this is intentional, not a gap, and avoids needing to model Obsidian's GUI-oriented sentinel system in C# at all.

GUI controls for these values still belong in `godot/BenMcLean.Wolf3D.Shared` per the original plan — out of scope here.

## 7. Episode-level context (why a single map is never generated in isolation)

Boss placement (`WOLF.FACTORY.EPISODE_BOSSES`, fixed to map 9 in base Wolf3D's 10-map episodes), bonus/secret maps (map 10, "Pacman"), the per-map toughness ramp (`1 + (map-1)/5`, capped at 1.1 for the last map), and quest/key/weapon distribution (`decide_quests(level_list)`) are all decided across an entire episode at once, inside `level_func` (`WOLF.get_factory_levels` / `SPEAR.get_factory_levels`) — not independently per map, and the RNG draws for building each map's blocks happen in one continuous sequence across the whole episode (see §5's RNG-sequencing bug). `GenerateEpisode` reflects this correctly by construction: everything in §5 happens for the whole episode in one pass. There is no cross-episode continuity (e.g. carried-over inventory) in Obsidian's model, and none is added here — that belongs in `Simulator` if wanted.

## 8. Feeding a generated episode into `AssetManager` (deferred loading, `LoadOriginalMaps`/`LoadGeneratedMaps`)

`BenMcLean.Wolf3D.Assets.AssetManager` (`AssetManager.cs`) loads `Maps`/`MapAnalyses` from a real game's `GAMEMAPS`/`MAPHEAD` files, alongside `AudioT`/`VgaGraph`/`VSwap`/`StateCollection`/`WeaponCollection`/`MenuCollection`, all read from the same game XML. `MapGenerator` only ever produces the tile/object grid — it has no textures, sprites, audio, actor states, or weapon data of its own, and isn't meant to. So "play a generated episode" doesn't mean constructing an `AssetManager` from scratch; it means loading a real game normally for everything *except* the maps.

**This section originally described an eager `AssetManager.LoadWithGeneratedMaps(xmlPath, generatedMaps)` factory that loaded generated maps at construction time. That design was superseded** by a deferred model, driven by the observation that eagerly decompressing `GAMEMAPS` (or running the level generator) at "load this game" time is wasted work before the player has even chosen whether they want the original campaign or a generated one — and a generated episode has no fixed file to eagerly load in the first place. `Maps`/`MapAnalyses` are now `null` until one of:
* `AssetManager.LoadOriginalMaps()` — idempotent (no-op if already loaded); loads the *entire* `GAMEMAPS`/`MAPHEAD` file, all episodes, exactly as before this change, and sets `GeneratedFrom = null`.
* `AssetManager.LoadGeneratedMaps(GameMap[] generatedMaps, GenerationParameters parameters)` — always overwrites (a fresh "start new game" with generation always means a fresh seed); never touches `GAMEMAPS`/`MAPHEAD` at all; sets `GeneratedFrom = parameters`.

`GeneratedFrom` (`AssetManager.GeneratedFrom`, and the parallel `Simulator.GeneratedFrom`/`SimulatorSnapshot.GeneratedFrom` for save-file round-tripping) is the provenance field: null means "loaded from files, `MapAnalyzer.MapNumber(episode, level)`'s XML `<Map>` lookup applies", non-null means "generated, always treated as episode 1 locally (`MapGenerator` never generates more than one episode per call, so no other episode number would mean anything), `MapNumber()` is never called on this path."

`godot/BenMcLean.Wolf3D.Shared/SharedAssetManager.cs` exposes the call points as `EnsureOriginalMapsLoaded()` and `LoadGeneratedEpisode(GenerationParameters)`, wired into `godot/BenMcLean.Wolf3D.VR/Root.cs`'s three places that can begin a fresh play session (`ShouldStartGame`, `StartLevelAction`, `LoadSavedGame`) — traced exhaustively against the actual code before implementing; `ResumeGame()` and the end-of-level "push to next level" transition need no changes, since both reuse an already-loaded session.

**`GenerationParameters` moved from `MapGenerator` to `Assets`** (same shape, new location) so `AssetManager`/`Simulator`/`SimulatorSnapshot` can reference it without a circular project reference (`MapGenerator` already depends on `Assets`). `LevelGenerator.cs` needed no code changes — it already had `using BenMcLean.Wolf3D.Assets;` for `RNG` (moved there in an earlier pass for the identical reason).

**This does not resolve §11.** `MapAnalyzer` interprets tiles using whichever game's XML it was built from; the `GameMap`s handed to `LoadGeneratedMaps` contain Obsidian's own hardcoded tile/actor numbers (from `WOLF.FACTORY`, not that XML). The plumbing above is now mechanically complete and reachable from real gameplay entry points, but nothing currently *calls* `LoadGeneratedEpisode` from any player-facing UI (no such UI exists yet — deliberately out of scope), and even once something does, results should not be trusted to have correct player spawns, doors, or exits until §11's data-driven `GAME.FACTORY` work happens — `MapAnalyzer` will run without erroring, but may be reading numbers that mean something else entirely to it. End-of-generated-episode behavior (what happens after the last generated map) was deliberately deferred to a future Lua script hook in the game XML, matching how other level-completion behavior already works, rather than being hardcoded here.

## 9. Determinism and RNG

`RNG.cs` (Tommy Ettinger's TangleRNG, public domain) was **moved** from `BenMcLean.Wolf3D.Simulator` to `BenMcLean.Wolf3D.Assets` (namespace `BenMcLean.Wolf3D.Assets`) rather than duplicated, so `MapGenerator` (which depends on `Assets` but must not depend on `Simulator`, per the project-layout rule) and `Simulator` share one implementation. Eleven call sites across `Simulator` and `godot/BenMcLean.Wolf3D.VR` picked up a `using BenMcLean.Wolf3D.Assets;` to keep resolving the type; `Simulator` builds clean after the move.

Every `gui.random()` call — and therefore every `rand.*` call anywhere in the vendored Lua — draws from the single `RNG` instance created from `GenerationParameters.SeedA`/`SeedB` in `LevelGenerator.GenerateEpisode()`. Verified empirically (see §10): identical `GenerationParameters` produce byte-identical `GameMap.MapData` across a whole regenerated episode; different seeds/episodes produce different output.

## 10. Verification performed

A throwaway console harness (not part of the repo — built in the scratch directory, referencing the built `BenMcLean.Wolf3D.MapGenerator.csproj`) exercised both `LevelGenerator.Generate()` and `GenerateEpisode()` across several seeds, episodes, and map indices (including the boss map and the Pacman bonus map), confirming:
* End-to-end generation succeeds with no unhandled Lua errors.
* Identical `GenerationParameters` → byte-identical `MapData`, both for a single `Generate()` call and across a full regenerated `GenerateEpisode()` (determinism holds at both granularities).
* Different seeds/episodes/maps → different, plausibly-sized `MapData`/`ObjectData` (roughly half to three-quarters of the 4096 cells non-void, hundreds of placed things — sane orders of magnitude, not all-empty or all-solid).
* `GenerateEpisode()` returns the dynamically-read level count (10 for base Wolf3D) rather than a hardcoded value.
* After fixing the RNG-sequencing bug (§5), the same seed's map 5 measurably changed output versus the pre-fix per-map version — direct evidence the bug was real and the fix took effect, not just "it still runs."
* Full solution (`dotnet build BenMcLean.Wolf3D.sln`) still builds clean after the `RNG.cs` move, the `AssetManager.LoadWithGeneratedMaps` addition, and the `GameMap.FromGenerated` signature change (added a `number` parameter).

This was iterative and not all of it was caught by planning ahead: the driver initially failed on a mis-derived embedded-resource name (MSBuild prefixes `_` onto path segments starting with a digit, e.g. `094` → `_094`), then on missing `PARAM`/`OB_GAMES`/`_()` globals, then on the `v094_begin_wolf_level` plane-reset gap described in §5 — all caught by the harness. The RNG-sequencing bug and the hardcoded-episode-length bug (§5) were **not** caught by the harness; both were only found because the user pushed back on the initial single-map design ("this generates a complete episode, doesn't it?" and "is 10 actually right for every game?"). Worth remembering: a green smoke test only proves the code runs and looks plausible, not that its design matches the reference engine's actual semantics.

**Not yet done:** comparing generated output against a real Obsidian binary run (no build of Obsidian's C++ engine was attempted — out of scope, and the whole point of embedding the Lua is that it can't diverge from the reference algorithm). No automated test project exists yet for `MapGenerator` — the verification above was manual/exploratory. A real test project (xunit or similar, matching whatever the rest of the solution uses) covering determinism and a range of seeds/episodes/maps would be a reasonable next step.

## 11. TODO: make GAME.FACTORY data-driven from this project's own game XML

**Current state:** `WOLF.factory_setup()` (`wolf/factory.lua`) populates `GAME.FACTORY` entirely from Obsidian's own hardcoded `WOLF.FACTORY.*` tables — tile numbers, actor/thing codes, combos, monster stats, weapon/key/exit definitions. This is **not** the same data as this project's `WL1.xml`/`WL6.xml` (loaded via `GameXmlResolver` into `ObjectType`/`Actor`/`Door`/`GameplayWeapon` etc. in `BenMcLean.Wolf3D.Assets`). Nobody has verified the two agree, and there's no reason they would — Obsidian's numbers reflect whichever Wolf3D data files *Obsidian* was built against.

**Why this matters:** a `GameMap` produced today by `LevelGenerator.Generate()` contains tile/object values in Obsidian's convention. If those don't match the tile numbers and actor codes this project's renderer/`Simulator` expect from the currently-loaded game XML, the generated map either renders wrong or references actors/weapons that don't exist in that game's data. It also means `MapGenerator` currently only ever generates "Obsidian's idea of Wolf3D" — not whichever specific game (`WL1`, `WL6`, mission packs, `N3D`) is actually loaded.

**The fix, when it's time:** after `WOLF.factory_setup()` runs (see the `TODO(data-driven-factory)` comment at that call site in `LevelGenerator.cs`), overwrite the relevant `GAME.FACTORY` sub-tables from C# using this project's own parsed game XML instead of leaving Obsidian's data in place:
* `GAME.FACTORY.TILE_NUMS` (area min/max, door tile pairs, elevator/secret tiles) ← from the game's `WOLF3D.xsd`-described tile/door data
* `GAME.FACTORY.combos` ← from whatever this project's equivalent of Obsidian's `COMBOS` turns out to be (may not exist yet — this project doesn't currently have a "symbolic material role → tile number" concept; would need design work, not just data-copying)
* `GAME.FACTORY.things`/`.monsters`/`.bosses`/`.weapons`/`.pickups` ← from `ObjectType`/`Actor`/`GameplayWeapon` (`WeaponCollection.GetInventoryKey`, per `MEMORY.md`'s note that weapon identity is already XML-driven, not hardcoded)
* `GAME.FACTORY.key_doors`/`.exits` ← from `Door` elements

This is a real design task, not a mechanical find-replace: it means deciding how each XML-described concept maps onto the shape `builder.lua`/`planner.lua`/`monster.lua` expect a Lua table to have (same category of shape-decision work called out in the original plan's Phase-ordering rationale, just against Lua tables instead of C# POCOs). **Prefabs stay baked** regardless (per the original plan's §3 rationale about save-game determinism — that reasoning is unaffected by this pivot). Only the tile-number/actor/combo *data* tables are candidates for this, not the algorithm or the prefab library.

**Until this is done:** treat `LevelGenerator.Generate()` as producing Obsidian-convention output only. Do not wire it into actual gameplay (`Simulator`, VR rendering) against a specific loaded game XML without either doing this work first or explicitly confirming Obsidian's `WOLF.FACTORY.TILE_NUMS`/`THINGS` numbers happen to already match that game's XML (unverified, and unlikely to be a safe assumption to skip).

## 12. Explicit non-goals

* No VR GUI work. `GenerationParameters` ships with hardcoded defaults; the Godot-side settings screen is a separate, later effort.
* `094/a_star.lua` is vendored (cheap to include) but not proven load-bearing for the wolf path — no call site was found feeding into `plan_sp_level`/`build_level`/`write_wolf_level`. Left in for parity in case a future prefab or quest feature turns out to need it.
* No support for other Obsidian-supported engines (id Tech 1/Doom, id Tech 2/Quake) — this project is id Tech 0 only.
* No WAD/GAMEMAPS binary writer — `GameMap` is the terminal output type.
* Spear of Destiny (`x_spear.lua`) is loaded (its top-level code runs and registers `OB_GAMES["spear"]`, harmlessly) but `GenerationParameters` has no way to select it yet — `WOLF.factory_setup()` always builds the base Wolf3D `GAME.FACTORY`. Selecting SoD vs. WL1/WL6 vs. Noah's Ark data would be a `GameId`-style addition to `GenerationParameters` plus whatever `x_spear.lua`'s actual override mechanism turns out to be (not traced in this pass).

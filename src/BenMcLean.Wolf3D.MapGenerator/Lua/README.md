# Vendored Obsidian Lua sources

These files are copied verbatim (license headers intact) from the [OBSIDIAN Level Maker](https://github.com/dashodanger/OBSIDIAN) project, GPLv2-or-later, Copyright (C) 2006-2017 Andrew Apted, Copyright (C) 2021-2022 The OBSIDIAN Team. They are executed at runtime via MoonSharp rather than manually re-implemented, per the design decision in `../IMPLEMENTATION_PLAN.md`.

Do not hand-edit game-logic behavior in these files. If a fix or upstream update is needed, re-copy from `Obsidian/` at the repo root (read-only reference checkout) and note the change here.

| File | Source | Purpose |
|---|---|---|
| `094/defs.lua` | `Obsidian/scripts/094/defs.lua` | v094 engine constants |
| `094/a_star.lua` | `Obsidian/scripts/094/a_star.lua` | Generic grid A* (currently unused by the wolf path; kept for parity/future use) |
| `094/theme.lua` | `Obsidian/scripts/094/theme.lua` | Theme/material resolution |
| `094/planner.lua` | `Obsidian/scripts/094/planner.lua` | Quest/room graph construction |
| `094/monster.lua` | `Obsidian/scripts/094/monster.lua` | Toughness-budget monster placement |
| `094/builder.lua` | `Obsidian/scripts/094/builder.lua` | Prefab stamping |
| `094/writer.lua` | `Obsidian/scripts/094/writer.lua` | `write_wolf_level()` — the PLAN.blocks -> gui.wolf_block bridge |
| `util.lua` | `Obsidian/scripts/util.lua` | `rand.*`, `table.*`, `geom.*`, `sel`, `int` helpers |
| `wolf/factory.lua` | `Obsidian/games/wolf/factory.lua` | `WOLF.FACTORY`: tile numbers, combos, things, monsters, prefabs, quests, rooms, themes; `WOLF.factory_setup()`/`WOLF.get_factory_levels()` |
| `wolf/x_spear.lua` | `Obsidian/games/wolf/x_spear.lua` | Spear of Destiny overrides |

`games/wolf/base.lua` is intentionally **not** vendored — its only load-bearing content (`WOLF = {}`) is created directly by `LevelGenerator.cs` before `wolf/factory.lua` loads; the rest of `base.lua` is generic engine/GUI registration we bypass (see `../IMPLEMENTATION_PLAN.md` §5).

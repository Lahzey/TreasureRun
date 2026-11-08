# Treasure Run (Rogue Tower mod)

BepInEx 5 + Harmony mod for Rogue Tower. The plan and roadmap are in `PLAN.md`; the game internals are in `docs/game-architecture.md`.

## Rules that must always hold
- **Content rules:** follow `docs/content-rules.md` for every card and treasure, including:
  - max-health costs are declared and paid via `HealthCost`, and unaffordable options are never offered
  - content tags drive castle bans
  - castle-exclusive content
- Balance concerns go into `docs/balance-todo.md`, not into ad-hoc tweaks.
- Castles work in every mode (normal and Treasure Run). Treasure Run–only logic checks `TreasureRunMode.Active`.
- Castle models are generated from code (`tools/VoxTool/Recipes`, `dotnet run -- build ../../models`). They must be grounded (`VoxCanvas.Grounded()`), because the grass layer hides the lowest ~4 voxels.
- Commit only when asked.

## Driving the game (dev console)
- With `Debug.DebugTools = true`, `UI/DevConsole.cs` runs commands from:
  - the in-game console (`§`, `` ` `` or F10)
  - the file `BepInEx/plugins/TreasureRun/console/in.txt`, with output appended to `console/out.txt` and each command ending with `#done <command>`
- `help` lists commands. Useful ones:
  - `start <lanes> <castle> tr|normal`, `state`, `wave`, `god`, `gold`, `treasure`, `card`
  - `build <name> <x> <z>`, `shot <name>` (screenshots go to `plugins/TreasureRun/screens`)
  - `active UniverseLibCanvas 0` hides the UnityExplorer bar for clean screenshots
  - `zoom <size> [x z]` for close-ups, `speed 0` to freeze a moment, `closeui`, `bounds <name>` to check overlaps
  - `treasuredoc` writes `console/treasures.md`; copy it to `docs/treasures.md` after treasure changes
- Test castle mechanics under realistic conditions (e.g. a few ballistas on an early wave) and use `god` so the run survives the test.

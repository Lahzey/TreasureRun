# Treasure Run (Rogue Tower mod)

BepInEx 5 + Harmony mod for Rogue Tower. The plan and roadmap are in `PLAN.md`; the game internals are in `docs/game-architecture.md`.
Building, regenerating assets (models, the accelerator transformation, sounds) and deploying them to the game: `README.md`. Only the mod build deploys: after regenerating anything, build the mod again.

## Rules that must always hold
- **Content rules:** follow `docs/content-rules.md` for every card and treasure, including:
  - max-health costs are declared and paid via `HealthCost`, and unaffordable options are never offered
  - content tags drive castle bans
  - castle-exclusive content
- Balance concerns go into `docs/balance-todo.md`, not into ad-hoc tweaks. Visual improvements for later go into `docs/visual-todo.md`.
- Every new treasure and custom card gets a balance spec in `src/TreasureRun/Balance/Specs/ContentSpecs.cs` (one line, `docs/balance-framework.md` §7); `dotnet test` and `scenario balance` check coverage. After content changes, regenerate the report (`balance report`, then copy `console/balance.md` and `.csv` to `docs/balance-report.*`).
- Castles work in every mode (normal and Treasure Run). Treasure Run–only logic checks `TreasureRunMode.Active`.
- Castle models are generated from code (`tools/VoxTool/Recipes`, `dotnet run -- build ../../models`). They must be grounded (`VoxCanvas.Grounded()`), because the grass layer hides the lowest ~4 voxels.
- Ascensions follow `docs/ascension-plan.md`: register through `AscensionRegistry` (exclusion group per tower), behaviour in an `AscendedTower` component, mechanics in a pure `*Logic.cs` file (only `System`, `Vector3`/`Mathf`, `IRng`) with offline tests.
- Sounds are generated from code (`tools/SfxTool/Recipes`, see `docs/sound-workflow.md`); check the analysis `.png`/`.txt` against `SoundReference/reference.md` before asking the user to listen.
- Icons are SVG written as text (`tools/IconTool/Icons`), rendered to `icons/` by IconTool (README); check them with `IconTool sheet`.
- Treasures that change the vanilla state when taken declare how they are sold (`Kit.OnSell` undo, or `Kit.NoSale` with a reason for gold, health or other one-time effects); see `docs/poi-plan.md` §5.
- Points of Interest follow `docs/poi-plan.md`: register in `PoiRegistry`, a `SectionStyle` that works for **every** exit combination (the generator and `Validate` must pass for all seven), an icon (`icons/poi/<id>.png`), a spec in `ContentSpecs.Pois` (budget 15), and a scenario. Treasure Run only. No purely negative POIs.
- Commit only when asked.

## Testing without the game
- `dotnet test tests/TreasureRun.Tests` runs in seconds: `SimDamage` (vanilla damage formula port), `SimWorld` (path, enemies, towers) and the mod's linked `*Logic.cs` files. Unity engine calls (Random, Time, Physics, GameObject) throw there; `Vector3`/`Mathf` work.
- In game, `scenario all` runs every in-game scenario in one launch (`Debugging/Scenarios.cs`); results end with `#scenarios finished: …` in `console/out.txt`.

## Driving the game (dev console)
- With `Debug.DebugTools = true`, `UI/DevConsole.cs` runs commands from:
  - the in-game console (`§`, `` ` `` or F10)
  - the file `BepInEx/plugins/TreasureRun/console/in.txt`, with output appended to `console/out.txt` and each command ending with `#done <command>`
- `help` lists commands. Useful ones:
  - `start <lanes> <castle> tr|normal`, `restart`, `state`, `wave`, `god`, `gold`, `treasure`, `card`
  - `offer [size | id id ...]` opens a treasure offer (exactly the given treasures with ids); `pick <slot>`
  - `towerui <x> <z>` opens a tower's popup; `scenario [name|all]` (scenarios grant ascensions temporarily via `ScenarioContext.Ascend`); `sound <name>`
  - right after a launch, wait a few seconds before the first command: the console clears `in.txt` while starting
  - `config [key] [value]` lists or sets mod settings (bools accept 1/0/on/off)
  - `build <name> <x> <z>`, `shot <name>` (screenshots go to `plugins/TreasureRun/screens`)
  - `active UniverseLibCanvas 0` hides the UnityExplorer bar for clean screenshots
  - `zoom <size> [x z]` for close-ups, `speed 0` to freeze a moment, `closeui`, `bounds <name>` to check overlaps
  - `planes` lists Radar planes (ascension, target, bomber phase, blimp deck); `deckbuild <name>` builds on a free War Blimp deck
  - `treasuredoc` writes `console/treasures.md`; copy it to `docs/treasures.md` after treasure changes
  - `balance <id|title>` prints an item's points, curve and breakdown; `balance report` writes the balance report; `balance pathstats` measures the map's path statistics
  - `poi list | omen <id> [all] | next <id> | exits <L|T|R|LT|LR|TR|LTR> | rate <0-15> | shop | buy <slot> | sell <id> | press <buy n|sell id> | close | souls [n] | fill [chest|gold|rune] | cast [spell]` for Points of Interest (merchant ids: `merchant`, `legendary_merchant`, `ascension_merchant`; the Soul Urn is `soul_urn`, the Wizard's Tower `wizard_tower`); `cells <x> <z>` prints a tile's height map; `dump <object>` its hierarchy
- Test castle mechanics under realistic conditions (e.g. a few ballistas on an early wave) and use `god` so the run survives the test.

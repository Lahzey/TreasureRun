# Visual TODO

Visual improvements noticed in playtests, collected for a later polish pass (like `balance-todo.md` for numbers).

## Ascensions

| Ascension | Change | Where |
|---|---|---|
| **The Saw Dimension** | Visual overhaul of the Saw Engines: make them look like a connection of gears and shafts, maybe with some brass colours. | `tools/VoxTool/Recipes/AscensionRecipes3.cs` (`SawEngineBase`, `SawEngineTurret`) |
| **The Saw Dimension** | Saws currently appear a brief moment after new track is added (the layout is re-checked every 2 s). Either make it immediate (e.g. react to the new tile being placed) or add a spawn animation (saws rising out of new slits) so it is less jarring. | `SawField.RefreshLayout`, `LayoutCheck` in `SawDimension.cs` |
| **Mega Mortar** | Overhaul the steam effect: replace the round steam-cloud particles with small blocky steam particles. | `SteamFx` in `MegaMortar.cs` |
| **Mega Mortar** | The corner vents (3x3 and up) should no longer release steam; instead they briefly glow deep blue when the mortar fires. | `SteamFx.Blast` vents, `MortarModule.VentPositions`, vent grilles in `MortarBody` |
| **Ritual of the Sun** | Ideas not built yet: heat shimmer round the sun column (the Particle Accelerator's `SpaceWarp` camera could bend the scene behind it slightly); a small rainbow fan where a beam leaves a prism (prism dispersion); the prism's crystal is hard to make out under its glint at close zoom; the powered crystal's gold overlay is unlit (flat gold). | `SunRing.cs`, `SunFx`, `RitualGrid.Glint`, `CrystalPower` |

## Points of Interest

| POI | Change | Where |
|---|---|---|
| **Soul Urn** | The soul fire in the urn's mouth is small at low fill and easy to miss; a ring of runes on the plinth that lights up with the fill would show the progress at a glance. The souls read as comets (bright trail, small head); a wispier, flickering head would look more like a soul. | `SoulFx.SoulFire`, `SoulWisp`, `PoiRecipes.SoulUrn` |

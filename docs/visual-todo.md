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
| **Wizard's Tower** | The tornado is a column of particles (it reads as a whirl of smoke); a twisted funnel mesh with a dusty base would look more like a tornado. The gold statue is a gilded palette: a shine sweeping over it would sell the metal. The land's blue shine is very subtle on light grass. The rod's lightning spiral is thin against the copper coil; the lantern is a dark cage whose green glow is faint by day. | `WizardTornado`, `GoldStatue`, `WizardLand.Dress`, `WizardGadgets.Spiral`, `WizardRecipes.Lantern` |
| **Lizard Nest** | A lizard vanishes the moment it dies (the vanilla death); a body that bursts open for a moment, its skin deflating while the brood pours out, would sell the burst. The eggs break by a model swap; a few frames of cracking would read better. The lizard's legs are rigid pieces turning at the hip (gaps show at the full swing); an elbow joint would make the gait smoother. Idle details: a tongue flick, a blink. The hatchlings are small at the default zoom: a lighter outline or a little wake of dust would help them read in a swarm. | `NestCreatures.cs` (`LizardRig`, `HatchlingRig`), `LizardNest.SpawnBrood`, `LizardRecipes` |
| **Soul Urn** | The soul fire in the urn's mouth is small at low fill and easy to miss; a ring of runes on the plinth that lights up with the fill would show the progress at a glance. The souls read as comets (bright trail, small head); a wispier, flickering head would look more like a soul. | `SoulFx.SoulFire`, `SoulWisp`, `PoiRecipes.SoulUrn` |

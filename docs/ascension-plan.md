# Ascension plan (M7)

The design intent for every ascension is in [ascensions.txt](ascensions.txt). This document covers how they get built: shared infrastructure first, then each ascension with its approach, models, sounds, cards, tests and open questions. Vanilla mechanics referenced here are in [reference/vanilla-towers.md](reference/vanilla-towers.md).

**Status:** Radar is done (Strike Fighter, Carpet Bomber, War Blimp; see `PLAN.md` §6b). The Radar tower itself gets its own base and dish per ascension (`AscendedRadar`: Air Base with runway and control tower, Bomb Depot with bomb stacks, Airship Mast with a turning mooring mast). **Phase 0 (infrastructure), phase 1 (Repeater, Springald, Lone Scout, Chain Lightning, Supercharge, Sealed Fate, Nest of Bats) and phase 2 (Icicle Cannon, Cluster Munitions, Firing Line, Hwacha, Magma Lobber, Bladestorm, Beam Cannon, Dead Man's Land) are done** (see §4). Phase 3 is planned.

---

## 1. Shared infrastructure (phase 0)

Everything here comes before the first new ascension. Each piece is used by most ascensions. **All of it is built**; the bullets describe the code as it is (summary in §4 "Phase 0, as built").

### 1.1 Ascension framework

Code: `src/TreasureRun/Ascensions`.

- **`AscensionDef`** (`AscensionRegistry.cs`), registered with `AscensionRegistry.Register`, which creates the treasure:
  - `Target` (`AscensionTarget`): the tower or building it ascends. An own enum, because vanilla buildings share one `TowerType`. `AscensionTargets` (`AscensionTarget.cs`) has display names, banner colours, the vanilla type/component, `BuildButton(target)` and `InPlay(target)`.
  - Exclusion group `ascension:<target>` (e.g. `ascension:tesla_coil`), so a run gets at most one ascension per tower.
  - Tags: `ascension`, `ascension:<target>`, `<target>`, plus `Tags` (content tags such as `castle-health`).
  - Availability: `AscensionTargets.InPlay`: towers once built in this run (same rule as other tower treasures), buildings once unlocked.
  - `Kind`: **Replace** ("replaces attack"), **Augment** ("adds to attack") or **Upgrade** ("upgrades attack"), shown on the card.
  - `Component`: the `AscendedTower` subclass attached to every tower of the target (null when the ascension handles itself, like the Radar planes).
  - `CardModel`: voxel model used as card art. `OnAcquire`: extra one-time effect.
  - `TowerName`: name of the ascended tower, shown as the popup title and on the build button (`BuiltName`; null = the ascension's name). Set where the ascension's name doesn't work as a tower name (Chain Lightning → Storm Coil). Every ascension's tower name is listed in §3 and in the table below.
- **`AscensionRegistry`**: `ForTreasure(id)`, `Owned(target)`, `Siblings(def)`, `PlacedObjects(target)`, `DisplayNameOf(go)`. Register new families in `AscensionCatalog.RegisterAll` (and hooks in `HookAll`).
- **Applying to towers:** an ascension applies to **every** tower of its type, already built and future ones:
  - On acquire, the component is added to all placed towers of the target (`AttachAll`).
  - Postfixes on `Tower.Start` and the building `Start`s (Mana Siphon, Mana Bank, Mine, University, Haunted House) add it to new ones.
  - The component owns the swapped model and the replaced/augmented behaviour; vanilla methods it replaces are skipped with prefixes that check for the component (the `CustomFlight` pattern of the planes).
- **`AscendedTower`** (base component): `OnAscend()` runs once after attaching (the tower's own `Start` has already run), then `Tower.SetStats()` is re-run. Model swap helpers: `HideVanillaModel()` (hides every MeshRenderer whose mesh is a MagicaVoxel export, mesh name `default`; effect renderers stay), `ShowModel(name, parent, pos, rot, scale)` (e.g. under `Tower.turret` so it turns with it), `RestoreVanillaModel()`.
- **Tower UI** (`AscensionUi.cs`): a title panel above the popup's level panel (a clone of it) with the ascended tower's name (`Storm Coil`, no vanilla name in brackets), shrunk to one line; the build button label is renamed to the tower name too. Also for the Radar ascensions (Air Base, Bomb Depot, Airship Mast), which have no component. Ascension-specific buttons (Firing Line's aim button) will be added by the component.
- **Follow-up cards** (More Bomblets, More Chains, Swift Scouts, More Bats): `CustomCards.AscensionChain(ascensionId, id, title, tiers, image, description, onPick)`, only drawable while the ascension is owned (`CustomCardSpec.Available`). They stay tame (content rules §3).
- **Stats:** per-tower stat changes go through `StatLayer.Register(name, (tower, stats) => ...)`, checking `tower.GetComponent<MyAscension>()`. Ascensions that set a new base range (Icicle Cannon 6, Magma Lobber 8, Bladestorm 2) still need a `Tower.SetStats` prefix that swaps the prefab base value, so height, levels and cards keep stacking on top.
- **Test ascension:** "Proving Ground" (`TestAscension.cs`, Ballista, only with `Debug.DebugTools`, never offered): Siege Workshop cannon on the turret and +5 base damage. Use it as the template for new ascensions.

**Tower names** (ascension → ascended tower; implemented ones are set in code):

| Tower | Ascension | Tower name |
|---|---|---|
| Ballista | Repeater | Repeater |
| Ballista | Springald | Springald |
| Mortar | Cluster Munitions | Cluster Mortar |
| Mortar | Mega Mortar | Mortar Module |
| Tesla Coil | Chain Lightning | Storm Coil |
| Tesla Coil | Supercharge | Capacitor Coil |
| Frost Keep | Icicle Cannon | Icicle Cannon |
| Flame Thrower | Magma Lobber | Magma Lobber |
| Shredder | The Saw Dimension | Saw Engine |
| Shredder | Bladestorm | Bladestorm |
| Encampment | Dead Man's Land | Permacamp |
| Lookout | Lone Scout | Scout's Perch |
| Vampire Lair | Nest of Bats | Nest of Bats |
| Cannon | Firing Line | Line Cannon |
| Cannon | Hwacha | Hwacha |
| Monument | Sealed Fate | Sealed Monument |
| Obelisk | Ritual of the Sun | Sun Obelisk |
| Radar | Strike Fighter | Air Base |
| Radar | Carpet Bomber | Bomb Depot |
| Radar | War Blimp | Airship Mast |
| Particle Cannon | Beam Cannon | Beam Cannon |
| Particle Cannon | Particle Accelerator | Particle Accelerator |
| Mana Siphon | Ley Line Network | Ley Siphon |

### 1.2 Ascension card presentation

Ascension treasures look different from normal treasures in the offer panel, the level-up slots and the inventory bar. The idea is borrowed from the **type line** of card games (Magic: "Creature — Elf Warrior"; Hearthstone's tribe tag at the bottom of the card; Slay the Spire's type banner).

```
┌───────────────────────────┐
│        Repeater           │  title, rarity colour
│ [⚑]━ BALLISTA ASCENSION ━ │  type line: banner in the tower's colour, tower crest on the left
│   [ascended model render] │  instead of the gem icon
│  Common · upgrades attack │  first description line: rarity + kind
│  Fires 3-round bursts...  │  description (shrinks to fit)
└───────────────────────────┘
   (double frame behind the card: rarity colour outside, gold inside, shimmering)
```

As built (`UI/AscensionCard.cs`, called from `CardPanel.StyleAsTreasure`; verified with screenshots):
- **Type line:** a banner below the title reading **"BALLISTA ASCENSION"** (building: "MANA SIPHON ASCENSION"), in the target's fixed banner colour (`AscensionTargets.BannerColor`).
- **Tower crest:** the vanilla build-button icon of the target on the left end of the banner, tinted dark.
- **Card art:** a render of `AscensionDef.CardModel`, made at runtime by `UI/ModelIcons.cs` (temporary camera and light on an unused layer, fitted to the model's bounds, cached). No icon files.
- **Frame:** a double border behind the card (outer: rarity colour, inner: gold); the outer one pulses brighter (unscaled time).
- **Kind and rarity:** first line of the description ("Rare · upgrades attack"). A separate footer was tried and dropped: long descriptions overlapped it. The description uses best fit (down to 60% font size).
- **No "Excludes" line** (removed after review): players discover the other ascensions of a tower themselves.
- **Card size:** in a Treasure Run every card panel (level-up cards, treasure offers, monster cards) shows the cards 1.25× larger and spread out by the same factor (`UI/CardSize.cs`).
- **Pick feedback:** the sound `ui/ascension_pick` plays when an ascension is gained (§1.5). The planned particle burst was skipped, because the panel closes on pick.
- **Inventory bar:** ascensions get a gold rim and the crest as a badge (banner colour), are sorted first, and the tooltip shows "Rare · Ballista Ascension".
- The extra objects are created once per card slot, only shown for ascensions, and hidden again by `CardPanel.Restore`.

### 1.3 Monument (resolved)

The Monument works. It looked broken because it had no mana: it needs `0.25 × damage` mana per shot and does nothing without it.

### 1.4 Models

All ascension models are code recipes in `tools/VoxTool/Recipes/AscensionRecipes*.cs`, built with `dotnet run -- build ../../models` into `models/ascensions/<tower>/…`. The vanilla tower is the starting point (`Voxels/<Tower>/…` from `extract`), so ascended towers keep the vanilla footprint (16×16 voxels, 0.1 units per voxel) and read as "the same tower, evolved".

- Split moving parts into their own models (turrets, barrels, rotating blades, launch racks), animated from the component, like the castle parts.
- Projectiles and effects are small models at a finer voxel size (0.02–0.04, like vanilla projectiles).
- Every model gets a preview PNG reviewed by me. Card art is rendered from the model at runtime (§1.2), so just set `AscensionDef.CardModel`.
- **Tower models built on the vanilla model** (`AscensionRecipes.cs`, phase 1): the recipe loads the extracted vanilla `.vox` (`FromVanilla`), keeps its x/y size and only adds or changes voxels; `build` writes a sidecar with the vanilla origin (`VanillaSidecars`). The component then calls `SwapVanillaMesh("TurretArt" | "BaseArt", model)`, which swaps the mesh on the vanilla renderer in place, so turrets keep turning and nothing needs positioning. `VoxTool slices <file.vox> [z0 z1]` prints a model layer by layer to find where parts are.
- Swap models in the component's `OnAscend` with `SwapVanillaMesh` (above), or `HideVanillaModel()` + `ShowModel(...)` for new geometry (§1.1). VoxelLibrary places models centred on x/z and standing on y = 0; vanilla towers are split into `BaseArt` and `Turret/TurretArt`.
- Glow and beams use `Fx.GlowMaterial` + `LineRenderer`, lights and `Fx.Burst`, as evaluated in M6.

### 1.5 Sound effects

**Can I make sounds?** Yes, but I can't listen. I need a workflow where the sound is described by code and I check it through numbers and pictures, exactly like the voxel previews.

**Options evaluated:**

| Option | What it is | Verdict |
|---|---|---|
| **Own SfxTool (recommended)** | A small C# synth like VoxTool: sounds are code recipes, rendered to WAV | Deterministic, versioned in git, no installs, no costs. I see the result as a spectrogram and numbers |
| Vanilla clips, re-pitched | Play existing clips (Ballista, Cannon, Explosion, TeslaZap…) with pitch/volume changes at runtime | Free and always matches the game's style. Good for variants (Repeater, Icicle Cannon), not for new sounds |
| [SuperCollider MCP](https://glama.ai/mcp/servers/@BradA1878/mcp-wave/blob/517c5633b06d974a3e9720dfa9814a0a19c17230/README.md) | A full synthesis language driven over MCP | Powerful, but needs SuperCollider installed and its audio server running; designed for real-time play rather than rendering files |
| [bfxr2 MCP](https://glama.ai/mcp/servers/avi892nash/bfxr2-mcp) / [jsfxr](https://github.com/chr15m/jsfxr) | sfxr-family parameter synths (laser, explosion, pickup presets) | Easy, but chiptune. Rogue Tower's sounds are realistic samples (`.ogg`), so these would clash |
| [ElevenLabs sound effects](https://glama.ai/mcp/servers/elevenlabs/elevenlabs-mcp/tools/text_to_sound_effects) (MCP) | Text prompt → realistic sound effect | Best realism, but a paid cloud API, not reproducible, and I can only judge results by analysis. Optional for a few hard sounds (black hole, sun beam), only with your account and approval |

**SfxTool** (`tools/SfxTool`, net8 console, same layout as VoxTool). **Built**; how to use it: [sound-workflow.md](sound-workflow.md).

- **Recipes in C#** (`Recipes/*.cs`), built from a small set of blocks:
  - sources: sine, triangle, saw, square/pulse, white/pink/brown noise, FM pair
  - envelopes: ADSR, exponential decay, pitch sweeps
  - filters: biquad low/high/band pass with sweeps, comb
  - shaping: distortion/waveshaper, bit crush, ring modulation
  - space: delay, small reverb (Freeverb-style)
  - structure: layer, sequence (bursts, rattles), seeded random variants for round-robin (a rocket swarm shouldn't sound like one clip 20 times)
- **Output:** 44.1 kHz mono 16-bit WAV in `sounds/` (in git, copied into the plugin like `models/`).
- **How I check a sound** (`SfxTool analyze`):
  - a PNG with waveform, log spectrogram and loudness envelope, which I can look at
  - a text report: duration, peak and RMS level, attack time, spectral centroid over time ("brightness"), dominant pitch track
- **Calibration against vanilla:** `SfxTool reference` decodes the vanilla `.ogg` clips (NVorbis NuGet) from the ripped project and analyzes them the same way. New sounds are matched to the vanilla loudness and brightness ranges, so they sit in the mix.
- **You** do the final listening check (`SfxTool play <name>` or just open the WAV) and tell me what to change ("more thump, shorter tail").
- **Runtime (`Audio/SoundLibrary.cs`):**
  - Loads WAVs on first use (cached; F6 and the `sound` command reload): a RIFF PCM parser feeding `AudioClip.Create` + `SetData`. No AssetBundles. Variants `name_1`, `name_2`, … are picked at random for `name`.
  - One-shots play through the vanilla `SFXManager` pool (`AudioPoolSource.PlayClip`), so the SFX volume option applies.
  - Loops (beams, saw hum, charging): `SoundLoop.Start(name, transform, volume)`, an `AudioSource` copying the vanilla source's 3D settings, volume following the SFX option.
  - A per-sound voice limit, so 20 rockets or a field of saws don't stack into noise.

**Sounds needed** (✚ = new sound, ♻ = re-pitched vanilla clip):

| Ascension | Sounds |
|---|---|
| Repeater | ♻ Ballista shot ×3, slightly higher pitch per round. **Done** (`SoundLibrary.PlayVanilla`) |
| Springald | ✚ heavy bolt launch; ♻ Ballista hit per pierce, falling pitch. **Done** (`ascensions/springald_launch`) |
| Cluster Munitions | ✚ shell split "pop"; ♻ small explosions. **Done** (`ascensions/cluster_pop`; bomblets use the vanilla explosion at half size) |
| Mega Mortar | ✚ deep launch boom; ✚ huge explosion |
| Chain Lightning | ✚ crackling arc that jumps. **Done** (`ascensions/chain_crackle`, 3 variants) |
| Supercharge | ✚ rising charge hum on buffed towers. **Done** (`ascensions/supercharge`) |
| Icicle Cannon | ✚ ice shard launch; ✚ shatter. **Done** (`ascensions/icicle_launch`, `ascensions/icicle_shatter` with 3 variants) |
| Magma Lobber | ✚ heavy lob; ✚ sizzling impact. **Done** (`ascensions/magma_lob`, `ascensions/magma_impact`) |
| Saw Dimension | ✚ low saw-hum loop (one global loop, louder with more saws) |
| Bladestorm | ✚ whirring loop per tower. **Done** (`ascensions/blade_whirr`, seamless 2 s loop; hits re-pitch the vanilla ShredderHit) |
| Dead Man's Land | ♻ explosion. **Done** (the mine's own vanilla explosion, once per charge) |
| Lone Scout | ✚ soft tick (quiet; it fires often). **Done** (`ascensions/scout_tick`, 3 variants) |
| Nest of Bats | ✚ flutter loop; ✚ bite. **Bite done** (`ascensions/bat_bite`, 3 variants); the flutter loop was skipped |
| Firing Line | ♻ Cannon. **Done** (vanilla cannonballs) |
| Hwacha | ✚ rocket whoosh (variants); ✚ small pops. **Done** (`ascensions/rocket_whoosh` with 4 variants, `ascensions/rocket_pop` with 3) |
| Sealed Fate | ✚ stab, rising pitch with stacks. Not done: uses the vanilla sword hit sound |
| Ritual of the Sun | ✚ choir-like swell when the ring closes; ✚ beam roar loop |
| Beam Cannon | ✚ continuous energy beam loop. **Done** (`ascensions/beam_hum`, seamless 2 s loop) |
| Particle Accelerator | ✚ charging whine (rising); ✚ black hole rumble; ✚ collapse |
| Ley Line Network | ✚ quiet arcane flow loop |
| Ascension pick | ✚ special pick chime. **Done:** `ui/ascension_pick` (rising bell arpeggio + shimmer, 1.6 s, loudness like the vanilla card sound); not yet listened to |

---

## 2. Testing without launching the game (evaluated)

### 2.1 What runs outside Unity (spike result)

I built a small .NET Framework console app that references the game's `UnityEngine.CoreModule.dll` and `Assembly-CSharp.dll` and ran it outside the game:

| Works outside the game | Fails outside the game |
|---|---|
| `Vector3`/`Vector2` math, `Mathf`, `Color` | `Random.Range`, `Time.deltaTime`, `Debug.Log` |
| Game enums (`TowerType`, `Tower.Priority`) | `Quaternion.Euler`/`LookRotation` |
| | `new GameObject`, `Physics.OverlapSphere`, anything on a `MonoBehaviour` |

Everything in the right column calls into Unity's native engine and throws `SecurityException: ECall methods must be packaged into a system module`. So **vanilla code (e.g. `Enemy.TakeDamage`) can't run in a test**, but pure math and logic written against `Vector3`/`Mathf` can.

### 2.2 Tier 1: unit tests on pure ascension logic

- **Rule:** every ascension splits into a **logic** part and a **Unity** part.
  - The logic part (`Ascensions/<Name>/<Name>Logic.cs`) only uses `System`, `Vector3`/`Vector2`/`Mathf` and plain data in/out. Randomness comes from `Core/IRng.cs` (`GameRng.Instance` in game, `SystemRng(seed)` in tests).
  - The Unity part (the component) gathers positions and stats from the scene, calls the logic, and applies the result (damage, spawns, visuals).
- **Test project (built):** `tests/TreasureRun.Tests` (net472, NUnit), referencing only `UnityEngine.CoreModule` from the game folder. `Core/IRng.cs` and every `src/TreasureRun/**/*Logic.cs` are linked into it as source (just name the file `…Logic.cs`), so any accidental use of a native Unity API fails the tests immediately.
- `dotnet test tests/TreasureRun.Tests` runs in under a second (16 tests at the end of phase 0).

### 2.3 Tier 2: a small combat simulation (`SimWorld`)

For checks of the form "set up a situation, expect an outcome", a deterministic mini world in the test project (**built**: `tests/TreasureRun.Tests/Sim/` with `SimDamage`, `SimEnemy`, `SimWorld`, `SimPath`, `SimTower`; tests in `SimDamageTests.cs` and `SimWorldTests.cs`):

- A path (polyline; branches not modelled yet), enemies with health/armor/shield, speed, regen, slow, freeze, fortify, mark and damage-over-time stacks, moving at a fixed 0.02 s step. `SimWorld.SpawnAt`, `Hit`, `InRange` and a `Stepped` event for custom effects.
- **`SimDamage`**: a pure-C# port of the vanilla damage formula (layers, overflow, crit tiers, mark, damage over time), documented in [vanilla-towers.md](reference/vanilla-towers.md). It is checked against hand-computed vanilla numbers, e.g. a Ballista hit on health = 100.
- Sim towers using vanilla base stats (`SimTower.Vanilla("Ballista", pos)`), with a pluggable `Attack` and `SelectTarget` for the ascension logic under test. Not simulated: tower XP/levels and projectile travel (unless an attack models it); targets are re-picked every step.

**Example expected outcomes** (each one is a test):

| Ascension | Test |
|---|---|
| Repeater | DPS against a dummy is 1.5× (±5%) the vanilla Ballista; a burst whose target dies loses the remaining bolts |
| Springald | Damage sequence 100/80/60/40/20 %; despawns after the 5th hit |
| Cluster Munitions | Over 1,000 seeded shots at a still target, ≥ 80% of the time all 5 bomblets hit the centre enemy |
| Mega Mortar | Fusion finds the largest squares: an L of 5 mortars → one 2×2 + 1 single; 3×3 beats 2×2; XP sums |
| Chain Lightning | 30 enemies in a line 1.5 apart → exactly 20 unique hits; dead ends restart from earlier victims; no enemy hit twice |
| Supercharge | 1 coil firing twice a second = +30% (refresh only); 2 coils = +60% |
| Saw Dimension | Damage per second = Σ(Shredder damage)/10 (0 without Shredders); XP split adds up to the total |
| Dead Man's Land | Mines thrown within merge distance stack; an enemy on a 3-charge mine takes 3 explosions on 3 frames |
| Lone Scout | Marked enemy takes 3 ticks per second (+1 per Swift Scouts) with the mark bonus applied |
| Nest of Bats | Bats retarget when their target dies or leaves range; nearer targets are picked more often |
| Ritual of the Sun | Ring detection: triangle, square and 6-ring found; a chain without a cycle isn't; 2-cycles prevented |
| Particle Accelerator | Charges in 30 s with enough mana; stops below 25%; Ley Line Network shortens it |
| Ley Line Network | Flood fill networks; income = siphons × crystals touched |

The sim also writes a **balance report** (`tests/out/balance.md`): DPS of each ascension vs. its vanilla tower at levels 1/10/30, which feeds `docs/balance-todo.md`.

### 2.4 Tier 3: in-game scenarios, many per launch

Visuals, physics and integration still need the real game. Make each launch count:

- **`scenario <name|all>`** (dev console, **built**: `Debugging/Scenarios.cs`): sets up a scripted situation in the running game, measures, and prints `PASS`/`FAIL` with numbers to the console and `console/out.txt`; the run ends with `#scenarios finished: N passed, M failed, K crashed`. `scenario all` runs every scenario back to back in **one** launch, at 4× game speed, permanent ones (that grant treasures) last.
  - A scenario is a class deriving from `Scenario` (`Name`, `Description`, `Permanent`, `IEnumerator Run(ScenarioContext c)`), added to `Scenarios.All`.
  - `ScenarioContext`: `TrackPoint(distance)` (a waypoint about that far from the castle), `BuildNear(prefab, point, min, max)` (free grass cell), `Dummy(at, health, armor, shield, speed)` (a stationary tier-1 monster with the given layers), `Meter()` (damage per tower type from `Combat.EnemyDamaged`), `Wait(seconds)`, `Expect`/`ExpectNear`, `Info`, `Shot`. Everything built or spawned is removed afterwards.
  - Phase 0 scenarios: `ballista-dps` (measured 36.8 vs. 33.3 expected, within 20%) and `ascension-attach` (Proving Ground on existing and new Ballistas: component, +5 damage, model swap, button label, display name).
- **Faster loading (for everyone, not dev only):** `LevelLoader.LoadAsyncronously` adds about **6 s of artificial delay per scene load**: a 1.5 s wipe in, at least 3 s of "Loading…" whatever the real load time, and a 1.5 s wipe out.
  - **Done** (`Patches/LoadingPatches.cs`, `UI/OptionsMenuExtras.cs`): the mod always removes the 3 s minimum; `General.LoadingAnimation` (BepInEx config, and a "Loading animation" toggle in the main-menu and pause Options panels) turns the wipes off.
  - Measured (menu → game / game → menu, until the new scene is active): with the animation about 5.0 s / 2.2 s, without it about 3.5 s / 0.7 s. The game scene itself takes about 3 s to load, so the removed minimum mostly helps the way back to the menu.
  - `restart` (dev console) reloads `GameScene` with the current castle, lanes and mode, without the menu.
- **Hot reload, evaluated and rejected:** BepInEx's ScriptEngine can reload plugin DLLs, but our mod relies on static state, attribute-based Harmony patches and components from the old assembly that would stay alive. It would be fragile and not worth it.

**Workflow per ascension:** logic + unit/sim tests first (fast loop), then one game launch running its scenarios and a screenshot (`shot`) for the visuals.

---

## 3. The ascensions

Each entry: **effect**, **approach**, **models**, **cards**, **open questions**. Sounds are in §1.5, tests in §2.3.

### Ballista

#### Repeater (Common, Replace)
- **Tower name:** Repeater (same as the ascension)
- **Effect:** 3-round bursts on one target; effective DPS about 1.5×.
- **Approach:** a `Fire` prefix starts a burst coroutine (3 bolts, about 0.15 s apart) and stretches the reload so DPS = 1.5× vanilla. Bolts carry a burst id; when the target dies, the remaining bolts fly to its last position and deal no damage (a non-homing "dud" projectile).
- **Models:** ballista with a 3-bolt magazine on top of the turret.

#### Springald (Rare, Replace)
- **Tower name:** Springald (same as the ascension)
- **Effect:** piercing bolts; 100% damage on the first hit, then −20% of the original per hit; gone after 5 hits.
- **Approach:** own projectile (sphere cast like the vanilla `Cannonball`, but its own class, avoiding the vanilla Cannon's prefab-mutation bug). Flies straight after the first hit.
- **Models:** heavier frame with torsion bundles; a longer bolt.

### Mortar

#### Cluster Munitions (Rare, Augment)
- **Tower name:** Cluster Mortar
- **Effect:** at 2/3 of the flight a shell splits into 5 bomblets with 50% damage and 50% area each; they spread so that the centre is usually hit by all of them.
- **Approach:** a `MorterShell` postfix tracks flight time; at 2/3 it spawns bomblets with 50% damage and **50% blast radius**, whose landing points form a ring at radius ≈ 0.6 × bomblet blast radius around the target point, so the centre overlaps all of them. Each bomblet damages separately.
- **Cards:** More Bomblets I–V (+1 bomblet each).
- **Models:** mortar with a ribbed shell rack; small bomblet model.

#### Mega Mortar (Legendary, Replace)
- **Tower name:** Mortar Module
- **Effect:** mortars in a full square (2×2, 3×3, …) fuse into one big mortar; the terrain is raised to the highest of the cells.
  - **Blast radius** × side length: 2×2 → 2×, 3×3 → 3×.
  - **Damage:** the sum of all fused mortars' base damage, including their levels and upgrades. The same for health, armor and shield damage.
  - **Fire rate:** unchanged (vanilla mortar rate).
- **Approach:**
  - `MegaMortarLogic` finds maximal squares on the build grid (largest first, no overlaps) whenever a mortar is built or demolished.
  - The anchor tower stays a real `Morter` whose `SetStats` result is the sum of the group's stats; the others are hidden and stop firing, but keep existing so demolish/refund still works per mortar. Demolishing one breaks the fusion back into singles.
  - XP: the anchor gains it while firing and spreads it over the members, so each member keeps its own level when the fusion breaks.
  - A plinth model under the fused mortar at the highest cell's height; its `transform.y` sets the height bonus.
- **Models:** scalable mega mortar per size (2×2, 3×3, 4×4 recipes), plinth.

### Tesla Coil

#### Chain Lightning (Uncommon, Replace)
- **Tower name:** Storm Coil
- **Effect:** single-target attack that chains 20 times to the nearest enemy within the coil's range (from the last hit enemy), may leave the coil's range, restarts from earlier victims when stuck, never hits an enemy twice.
- **Approach:** `ChainLightningLogic.Plan(start, enemies, range, chains)` → hit list; the component deals vanilla `TakeDamage` per hit and draws the bolts with `LineRenderer` segments.
- **Cards:** More Chains I–IV (+5 each).
- **Models:** coil with a single focus tip instead of the ring.

#### Supercharge (Rare, Augment)
- **Tower name:** Capacitor Coil
- **Effect:** each attack also gives towers in range +30% attack speed for 1 s; a coil only refreshes its own buff, separate coils stack.
- **Approach:** a buff registry `towerId → {coilId: expiry}`; the stat layer multiplies `rps` (towers), drop rate (Frost Keep, Encampment) and plane fire interval.
- **Models:** coil with conductor rods; a short arc to each buffed tower.
- **Open:** does a coil buff other Tesla Coils, and itself? Default: other coils yes, itself no.

### Frost Keep

#### Icicle Cannon (Uncommon, Replace)
- **Tower name:** Icicle Cannon (same as the ascension)
- **Effect:** a cannon-like straight, piercing icicle shot with the Frost Keep's slow; base range 6.
- **Approach:** turns the keep into a targeting tower (it is a `Dropper` without a turret): skip `Dropper.Update`, run its own fire loop with the cannon's straight-line piercing (own projectile, see Springald). Keeps the mana-per-second drain.
- **Models:** keep with an ice ballista turret; icicle projectile; frost shards on impact.

### Flame Thrower

#### Magma Lobber (Epic, Replace)
- **Tower name:** Magma Lobber (same as the ascension)
- **Effect:** slow, big lobbed balls of magma with a large blast; sets enemies on fire; base range 8.
- **Approach:** an arcing projectile reusing the Siege Workshop Catapult's lob code, exploding with a big radius and high burn %. Uses mana like the Flame Thrower.
- **Models:** squat furnace catapult; glowing magma ball (emissive look via `Fx.GlowMaterial` overlay); scorch decal.

### Shredder

#### The Saw Dimension (Legendary, Replace)
- **Tower name:** Saw Engine
- **Effect:** the whole path is covered in saws: 5 ticks per second, damage per second = Σ(Shredder damage)/10. There is **no base damage**: with no Shredder built, the saws deal nothing. Shredders no longer attack. All Shredders gain XP centrally, split by which layer (shield/armor/health) the monsters on the map currently have.
- **Approach:**
  - One global `SawField` ticks 5×/s over `SpawnManager.currentEnemies` (cheap; no physics).
  - Damage uses `TakeDamage(TowerType.Shredder, …)` with the Shredder type's multipliers and bleed, so Shredder cards keep working. Fractional damage per tick is accumulated, so small totals still land.
  - **No Shredder built:** the saws **retract into the floor** (slots stay visible as dark slits) and stop. That must read clearly as "off", not just "slow". Building the first Shredder makes them rise and spin up again.
  - XP: per tick, XP per layer = share of enemies on that layer × number of Shredders, split evenly over the Shredders (as in your formula).
  - Visuals: saw blades along the path waypoints drawn with `Graphics.DrawMeshInstanced` (one draw call per 1,023 saws), spinning in the shader-free way (rotation per instance per frame).
- **Models:** small floor saw (plus its retracted slot); Shredder variant with a saw "generator" on top.

#### Bladestorm (Epic, Replace)
- **Tower name:** Bladestorm (same as the ascension)
- **Effect:** blades orbit the Shredder flat side up and damage whatever they pass through; attack speed → rotation speed, range → number of blades (on more and wider rings); base range 2.
- **Approach:** blades as transforms on rings; each frame, an overlap test per blade (`OverlapSphere` of blade size) with a per-blade/enemy hit cooldown so an enemy is hit once per pass.
- **Models:** low Shredder hub with a spindle; flat blade.

### Encampment

#### Dead Man's Land (Epic, Augment)
- **Tower name:** Permacamp
- **Effect:** mines persist after the wave; mines landing near an existing mine merge into it as charges; a mine with N charges explodes N times (one per frame while an enemy is on it).
- **Approach:** remove mines from `SpawnManager.destroyOnNewLevel`; a merge radius (≈ 0.3) checked on landing; charge counter on the mine; a charge label or stacked look.
- **Finding:** vanilla has **no cap** on landmines. They are only removed at wave end. With merging, the count stays bounded by the path cells in range.
- **Models:** mine variants with 1/2–3/4+ charge looks.

### Lookout

#### Lone Scout (Uncommon, Augment)
- **Tower name:** Scout's Perch
- **Effect:** the marked enemy takes 1 damage 3 times per second; Swift Scouts add a tick each.
- **Approach:** a tick of `TakeDamage(TowerType.Lookout, 1, 1, 1, 1, …)` on the marked enemy. The vanilla mark bonus applies automatically (it adds the Lookout's damage and multipliers to every hit), which is exactly "triggers the Lookout damage".
- **Cards:** Swift Scouts I–III.
- **Models:** Lookout with a crossbowman in the nest; small bolt.

### Vampire Lair

#### Nest of Bats (Rare, Augment)
- **Tower name:** Nest of Bats (same as the ascension)
- **Effect:** in addition to the pulse, 10 bats fly to enemies in range and deal the Lair's damage once per second each; they retarget (preferring nearby enemies) when the target dies or leaves range.
- **Approach:** bats are light objects driven by the Lair component (no physics; steering with a little noise); weighted random target pick (`weight = 1/distance²`).
- **Cards:** More Bats I–III (+3 each).
- **Models:** Lair with a bat roost; bat with 2 wing frames (frame animation).
- **Open:** do bats cost mana per bite? Default: no.

### Cannon

#### Firing Line (Uncommon, Augment)
- **Tower name:** Line Cannon
- **Effect:** set a fixed firing direction; the cannon fires that way non-stop during waves.
- **Approach:** a "Set target" button in the tower popup; then the cursor becomes a big target marker (world-space voxel decal following the ground hit), click to confirm (like BTD6). The component overrides targeting: always "has a target" during combat, fires along the stored direction. Own cannonball (not the buggy vanilla one).
- **Models:** cannon on a fixed emplacement with sandbags; target marker.

#### Hwacha (Epic, Replace)
- **Tower name:** Hwacha (same as the ascension)
- **Effect:** a swarm of small rockets (20 by default, `ModConfig`) with a small blast, landing roughly around the target.
- **Approach:** rockets launched over about 1 s from the rack slots (staggered), slight homing toward a random point within ~1.5 of the target, small blast each. Rack recoil and smoke animation; reload shows empty slots refilling.
- **Models:** Hwacha cart with a launch rack (slot positions as model metadata), rocket.

### Monument

#### Sealed Fate (Epic, Replace)
- **Tower name:** Sealed Monument
- **Effect:** each stab does +1 damage instead of −1, up to 3× the original; the sword only leaves when the target dies.
- **Approach:** a `SpiritSword.DealDamage` prefix counts up and clamps; disable the "damage ≤ 0" exit.
- **Models:** Monument with a chained sword; glowing sword.

### Obelisk

#### Ritual of the Sun (Mythical, Augment)
- **Tower name:** Sun Obelisk
- **Effect:** each obelisk links to its nearest obelisk in range (not to one that already links to it); closed loops of 3+ obelisks call a huge sun beam at the loop's centre that deals the combined DPS of the loop's obelisks to monsters inside.
- **Approach:**
  - `RitualLogic` builds the "nearest" graph and finds cycles ≥ 3 whenever an obelisk is built or demolished.
  - The beam is a sky-to-ground column with radius = loop size, ticking 6×/s with the sum of the obelisks' per-hit damage.
  - Do the obelisks keep their normal beams? Open, default yes.
- **Visuals (invest heavily, as asked):** golden link beams between obelisks (pulsing `LineRenderer`s); a rune circle traced along the loop when it closes; a sun disc descending above the centre with rotating rays; a layered light column (inner white core, outer gold, soft fade); light motes rising; a point light; sun-themed obelisk model with a gold capstone that glows while linked.
- **Models:** sun obelisk, sun disc, rune segments.

### Particle Cannon

#### Beam Cannon (Epic, Replace)
- **Tower name:** Beam Cannon (same as the ascension)
- **Effect:** a continuous straight beam while there is mana; higher DPS and higher mana per second than vanilla.
- **Approach:** like the Obelisk beam but long-range: ticks 10×/s, about 1.3× vanilla DPS and about 1.6× mana per second (tunable). Hits the first enemy on the line (or all on the line? default: first).
- **Models:** cannon with a focusing lens; beam built from two `LineRenderer`s.

#### Particle Accelerator (Legendary, Replace)
- **Tower name:** Particle Accelerator (same as the ascension)
- **Effect:** drains mana fast while charging (pauses below 25% mana); fully charged after 30 s it fires a black hole at the target that sucks in all enemies for 5 s and deals heavy damage.
- **One core, the rest are capacitors** (only one black hole source, and still only one per 30 s):
  - On pick, the highest-level Particle Cannon becomes the **Accelerator core**. A "Make core" button in the tower menu moves the role to another Particle Cannon (the charge carries over).
  - Every other Particle Cannon, built before or after the pick, becomes a **capacitor**: it stops attacking and adds its damage (base, health/armor/shield, including levels and upgrades) to the black hole's damage. A pulsing energy beam links each capacitor to the core.
  - The charge time stays 30 s however many capacitors exist. The Ley Line Network is the only way to charge faster.
  - If the core is demolished, the strongest capacitor becomes the core and keeps the current charge.
  - Models: the core (ring accelerator) and a capacitor variant (coil/magnet segment with a beam emitter).
- **Approach:**
  - Charge = mana drained; full at a fixed amount, so faster drain (Ley Line Network) means faster charging.
  - Black hole: enemies in radius are pulled toward the centre (moving their transform) and slowed to 0. Damage ticks during the 5 s.
  - **They must not end up stacked on one point.** The pull is a spring toward an orbit around the centre (each enemy gets its own random orbit radius and angle, with a little jitter), not toward the exact centre.
  - **End: a mild supernova.** The black hole implodes (shrinks and brightens), then bursts: a final damage hit and a push that throws every caught enemy a random distance (about 1–3 units) in a random direction over about 0.4 s. Then they walk back on their own: vanilla `Pathfinder` walks from the current position straight to the current waypoint.
- **Models:** ring accelerator around the cannon (spinning ring part); black hole: dark sphere, accretion disc (glow lines), distortion-like particle swirl.
- **Animation:** the collapse (disc spins faster and tightens, sphere shrinks to a bright point, light flash), then the burst (expanding shock ring on the ground, a puff of glowing particles, a short point-light flare, a fading afterglow).

### Mana Siphon (building)

#### Ley Line Network (Legendary, Augment)
- **Tower name:** Ley Siphon
- **Effect:** adjacent siphons form a network; each siphon earns 1 mana/s per crystal touched by the network. Secret: a Particle Accelerator's drain (and so its charge rate) rises by the network's income.
- **Approach:** flood fill over siphons (4-neighbour, same height as the vanilla checks); crystals adjacent to any siphon count; recomputed on build/demolish.
- **Models:** arcane pipes that connect: a hub plus 4 arm parts shown per neighbour (siphon or crystal), and an intake that "pumps" from crystals. Parts instead of 16 full models. Glow flowing through the pipes.
- **Balance:** income = siphons × crystals grows quadratically (5 siphons touching 4 crystals = 20 mana/s). Effectively infinite mana late in a run is intended; if it comes too early, it gets tuned in the balance step (`balance-todo`).

---

## 4. Order of work

| Phase | Content |
|---|---|
| **0 Infrastructure (done)** | Ascension framework (+ Radar moved over), card presentation, model swap, test project + `SimWorld`, `scenario` command, SfxTool + `SoundLibrary` |
| **1 Simple** | Repeater, Springald, Lone Scout, Chain Lightning, Supercharge, Sealed Fate, Nest of Bats |
| **2 Medium** | Icicle Cannon, Cluster Munitions, Firing Line, Hwacha, Magma Lobber, Bladestorm, Beam Cannon, Dead Man's Land |
| **3 Big** | The Saw Dimension, Mega Mortar, Ley Line Network, Particle Accelerator, Ritual of the Sun |

Per ascension: logic + tests → component + models → sounds → one in-game scenario run + screenshot → treasure doc (`treasuredoc`) → balance notes.

**Phase 0, as built:**
- **Framework** (`src/TreasureRun/Ascensions`): `AscensionTarget` (towers and buildings, banner colours, build buttons), `AscensionDef` + `AscensionRegistry` (treasure, exclusion group `ascension:<target>`, tags, attaching the component on acquire and on `Tower.Start`/building `Start`), `AscendedTower` (base component: `OnAscend`, `HideVanillaModel`, `ShowModel`), `AscensionUi` (popup title panel "Repeater (Ballista)", build button label), `AscensionCatalog` (registration). Radar's three ascensions are registered through it.
- **Follow-up cards:** `CustomCards.AscensionChain(ascensionId, ...)` adds a card chain that is only drawable while the ascension is owned.
- **Test ascension:** "Proving Ground" (Ballista, dev only, never offered; `TestAscension.cs`) exercises everything above.
- **Card presentation** (`UI/AscensionCard.cs`, `UI/ModelIcons.cs`): type-line banner with the tower's crest, model render as card art (rendered at runtime from the voxel model, no icon files), rarity + gold double frame with shimmer, first description line "Rare · upgrades attack"; inventory slots get a gold rim, a crest badge, and are sorted first. A separate footer was dropped: long descriptions overlapped it.
- **Tests:** `tests/TreasureRun.Tests` (16 tests, under a second) and in-game `scenario all` (2 scenarios, 12 checks, about 25 s at 4× speed); `restart`, `offer <ids>`, `towerui`, `sound`, `config` console commands.
- **Sound:** `tools/SfxTool`, `Audio/SoundLibrary.cs`, `SoundLoop`; first sound `ui/ascension_pick` (played when an ascension is gained). See `docs/sound-workflow.md`. Particles on the picked card were skipped: the panel closes on pick.
- **Also in this session:** loading speed-up and "Loading animation" setting (§2.4), `docs/reference/vanilla-towers.md` (every vanilla tower and building, with stats and mechanics).

**Open ends after phase 0:**
- The pick sound `ui/ascension_pick` has not been listened to yet.
- Clicking the "Loading animation" toggle in the Options panel is untested (setting it via config updates the toggle; the click path is the standard uGUI listener).
- Nothing of phase 0 is committed yet.

**Phase 1, as built** (`src/TreasureRun/Ascensions/Phase1`):
- **Logic:** `Phase1Logic.cs` (Repeater, Springald, Lone Scout, Chain Lightning, Supercharge buffs, Sealed Fate, Nest of Bats), 13 offline tests in `tests/TreasureRun.Tests/Phase1Tests.cs` (29 tests in total, all passing).
- **Components:** `BallistaAscensions.cs` (Repeater bursts through the vanilla `Fire` with dud bolts; Springald's own piercing bolt), `TeslaAscensions.cs` (Chain Lightning replaces `TeslaCoil.Fire`; Supercharge buffs through the stat layer), `SupportAscensions.cs` (Lone Scout, Sealed Fate via a `SpiritSword.DealDamage` prefix, Nest of Bats). Cards: More Chains I–IV, Swift Scouts I–III, More Bats I–III.
- **Models:** `tools/VoxTool/Recipes/AscensionRecipes.cs`, built on the vanilla models (§1.4). **Sounds:** `tools/SfxTool/Recipes/AscensionSounds.cs`, loudness matched to the vanilla references; not listened to yet.
- **In-game scenarios** (`Debugging/Phase1Scenarios.cs`, one per ascension): `scenario all` runs 9 scenarios, 33 checks, all passing. Measured: Repeater 57 DPS (vanilla 37), Springald 110 through 5 stacked dummies, Chain Lightning exactly 20 of 25 dummies, Supercharge interval 3 → 2.31 s and back, Sealed Fate stabs 15 → 35, Nest of Bats 1270 DPS (balance note).
- **Framework additions:** `AscendedTower.SwapVanillaMesh`, `AscensionRegistry.Revoke` / `TreasureInventory.Revoke` and `ScenarioContext.Ascend` (several ascensions of one tower in one launch), `SoundLibrary.PlayVanilla(sound, pos, pitch)`, `AscensionDef.Hidden`. Scenario dummies are registered in `SpawnManager.currentEnemies`, so effects that scan the wave see them.
- **Chain reach (after playtest):** a jump reaches a monster once its collider touches the circle of the coil's full range (height bonus included) around the last victim, like vanilla range checks (monsters: 0.5 box collider, so range + 0.25). Centre-to-centre distance stopped chains at 2.2-unit gaps with the coil's range of 2. With `Debug.DebugTools`, every attack logs `[chain] hits of monsters, range, nearest unreached gap`.
- **Nest of Bats (after playtest):** each bat idles on its own orbit (radius, speed, direction, height) with a noise wander.
- **Renamed:** the M4 combat treasure "Chain Lightning" (12% arc proc) is now **Static Arc** (`static_arc`), freeing the name for the Tesla ascension.
- **Open questions answered with the defaults:** Supercharge buffs other coils but not itself; Nest of Bats bites cost no mana.

**Phase 2, as built** (`src/TreasureRun/Ascensions/Phase2`):
- **Logic:** `Phase2Logic.cs` covers:
  - Icicle Cannon;
  - the Cluster Munitions landing ring;
  - the Firing Line direction;
  - Hwacha volley timing and scatter;
  - Magma Lobber;
  - Bladestorm rings and blade sweep;
  - Beam Cannon ticks with carried fractions;
  - Dead Man's Land merging and charges.

  It has 10 offline tests in `tests/TreasureRun.Tests/Phase2Tests.cs` (40 tests in total, all passing).
- **`FrostFlameAscensions.cs`:**
  - Icicle Cannon replaces `Dropper.Update`: mana per second, aiming, its own fire loop, and straight piercing `LineShot` icicles. The keep's inactive vanilla turret is switched on, raised onto the tower top and becomes the ice ballista.
  - Magma Lobber replaces `FlameThrower.Update`: catapult-style lobs (`LobShot` with the Siege Workshop's `Ballistics`), a 1.4 blast, extra burn and a scorch mark.
- **`MortarCannonAscensions.cs`:**
  - Cluster Munitions rides on the vanilla shell (`ClusterShell`, tagged in a `Projectile.SetStats` postfix during `Morter.Fire`) and splits it into timed `LobShot` bomblets.
  - Firing Line replaces `Tower.Update` once aimed. It calls the vanilla `Cannon.Fire` with no target, so Ricochet and Collateral still work.
    - It adds a **Set Target** button to the popup (a copy of the demolish button).
    - Aim mode shows a target marker and an aim line. Left click sets it; right click or Esc cancels. `UIManager.Update` is skipped while aiming.
  - Hwacha replaces `Cannon.Fire` with a volley coroutine: rockets on Bézier curves with smoke trails, rack recoil, and an empty-rack model until it is half reloaded. The rocket count is the config value `Ascensions.HwachaRockets` (20).
- **`BladeBeamMineAscensions.cs`:**
  - Bladestorm skips the Shredder's `Fire`, lays out rings of blade models, and hits monsters whose angle a blade swept this frame (no physics).
  - Beam Cannon skips the Particle Cannon's `Fire`. While powered it shows two `LineRenderer`s and a point light and plays a hum loop. It ticks 10 times a second on the first monster on the line.
  - Dead Man's Land tags the camp's mines (`PersistentMine`):
    - they leave `destroyOnNewLevel` and merge on landing;
    - `Landmine.Explode` becomes one blast per charge, with a re-check on the next physics step;
    - mines of a demolished camp are cleared at the next wave end again.
- **`Phase2Kit.cs`:** base range override (`IBaseRangeOverride`, swapped in a `Tower.SetStats` prefix and restored once the component is gone), attack speed factor, blasts, `LineShot`, `LobShot`.
- **Vanilla bug fixed (Treasure Run only):** `Enemy.GetFuturePosition` returns (0, 0, 0) for a monster that stands still (frozen, stunned, speed 0), so Mortar shells flew to the map origin. It now returns the monster's position.
- **Siege Workshop:** ascensions are never attached to the castle's own cannon (`AscensionRegistry.Attach` skips `SiegeCastleTower`).
- **Models:** `tools/VoxTool/Recipes/AscensionRecipes2.cs`:
  - Frost Keep: ice keep, ice ballista, icicle.
  - Mortar: cluster mortar, bomblet.
  - Cannon: line cannon with gun shield, Hwacha cart (loaded and empty), rocket, target marker, aim flag.
  - Flame Thrower: basalt base, furnace catapult, magma ball, scorch mark.
  - Shredder: Bladestorm base, hub and blade.
  - Particle Cannon: violet beam cannon.
  - Encampment: Permacamp with mine stacks, 2- and 4-charge mines.
- **Sounds:** `tools/SfxTool/Recipes/AscensionSounds2.cs`, levels matched to the vanilla references; not listened to yet.
- **Cards:** More Bomblets I–V.
- **In-game scenarios** (`Debugging/Phase2Scenarios.cs`, one per ascension, all passing):
  - Icicle Cannon: 16 shots through 4 stacked dummies, 56% slow.
  - Cluster Munitions: 68 DPS with all bomblets on the centre dummy.
  - Firing Line: hits a dummy at 1.5x its range with no target in range.
  - Hwacha: 20-rocket volleys, an empty rack between them.
  - Magma Lobber: hits and burns 3 dummies.
  - Bladestorm: 2.9 hits/s on the middle ring, nothing inside the inner ring.
  - Beam Cannon: 1.13x the measured vanilla DPS for 1.6x the mana.
  - Dead Man's Land: a 3-charge Permacamp mine explodes 3 times.

  Firing Line and Dead Man's Land switch `SpawnManager.combat` on for the test.
- **Open question answered with the default:** Beam Cannon hits the first monster on the line.

**Phase 2, after the first playtest:**
- **Line Cannon:**
  - the target can be set on the track too;
  - the aim flag only shows while that cannon is selected (or being aimed);
  - its balls fly 1.75x faster and 3x its range (vanilla: 2x).
- **Icicle Cannon:** a round range circle in the popup (the Frost Keep draws a square for its drop area).
- **Magma Lobber:** the scorch mark is now a magma pool that cools from glowing orange to a black crust (three models over about 3.5 s). Monsters standing in it take 20% of the shot's damage per second and get burn worth the same (to their current layer).
- **Bladestorm:** reworked into whole saw blades, 1.5 across.
  - They sit on one ring whose outer edge is the tower's range, at a fixed spacing (base range 2: 3 blades), so more range means more blades.
  - Each blade spins fast on its own axis, the other way round from the ring.
  - A blade hits a monster once per pass, on entry, for 75% damage.
  - Monsters right next to the tower are safe.
- **Beam Cannon:** when its target dies it re-detects at once (every 0.05 s at most) instead of waiting for the vanilla 0.25–1 s target update.
- **Hwacha:**
  - rockets add 30% burn;
  - the blast radius is 0.6 around the landing point, plus the monster's collider;
  - a bigger, more colourful impact burst;
  - **minimum range 2**.
- **Minimum range (new mechanic, `Phase2/MinRange.cs`):** a component implementing `IMinRange` makes its tower skip monsters closer than that (a `Tower.SelectEnemy` prefix filters the candidates). While the tower is selected, a red, semi-transparent circle with diagonal hatching shows the blocked zone. `TowerSelection` tracks the selected tower (the open `TowerUI`).
- **Permacamp white mine (playtest bug):** when a 2-charge mine exploded down to 1 charge, it went back to the vanilla mine mesh but kept the stack model's material, so the vanilla mesh rendered with the wrong palette. The vanilla material is now restored too. This is the most likely cause of the white leftover on the path, but it was not reproduced.
- Dev console: `gold`, `mana` and `health` accept `10k`, `1.5m`.

**Next: phase 3** (The Saw Dimension, Mega Mortar, Ley Line Network, Particle Accelerator, Ritual of the Sun), one ascension at a time with the per-ascension workflow above.

## 5. Open questions (summary)

1. Supercharge: does a coil buff other coils, itself? (default: others yes, itself no)
2. Nest of Bats: do bites cost mana? (default: no)
3. Ritual of the Sun: do linked obelisks keep their normal beams? (default: yes)
4. Beam Cannon: hit the first enemy on the line, or all of them? (default: first, as built)

**Decided:** Particle Accelerator uses one core plus capacitors (only one black hole source, fixed 30 s charge). Cluster Munitions bomblets have 50% radius; Mega Mortar radius × side length, damage = sum of all members' damage incl. levels and upgrades, vanilla fire rate; Magma Lobber lobs like the Catapult; Saw Dimension has no base damage and retracts without Shredders; the Cannon ascension is called Firing Line; Ley Line Network keeps quadratic income for now.

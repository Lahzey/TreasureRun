# Ascension plan (M7)

The design intent for every ascension is in [ascensions.txt](ascensions.txt). This document covers how they get built: shared infrastructure first, then each ascension with its approach, models, sounds, cards, tests and open questions. Vanilla mechanics referenced here are in [reference/vanilla-towers.md](reference/vanilla-towers.md).

**Status:** Radar is done (Strike Fighter, Carpet Bomber, War Blimp; see `PLAN.md` §6b). The Radar tower itself gets its own base and dish per ascension (`AscendedRadar`: Air Base with runway and control tower, Bomb Depot with bomb stacks, Airship Mast with a turning mooring mast). **Phase 0 (infrastructure), phase 1 (Repeater, Springald, Lone Scout, Chain Lightning, Supercharge, Sealed Fate, Nest of Bats) and phase 2 (Icicle Cannon, Cluster Munitions, Firing Line, Hwacha, Magma Lobber, Bladestorm, Beam Cannon, Dead Man's Land) are done** (see §4). **Phase 3 (The Saw Dimension, Mega Mortar, Ley Line Network, Particle Accelerator, Ritual of the Sun) is done** too: every ascension of M7 is built.

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
| Mortar | Mega Mortar | Mortar Module (fused: Mega Mortar) |
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
| Springald | ✚ heavy bolt launch; ♻ Ballista hit per pierce, falling pitch. **Done** (`ascensions/springald/springald_launch`) |
| Cluster Munitions | ✚ shell split "pop"; ♻ small explosions. **Done** (`ascensions/cluster_munitions/cluster_pop`; bomblets use the vanilla explosion at half size) |
| Mega Mortar | ✚ deep launch boom; ✚ huge explosion. **Done** (`ascensions/mega_mortar/mega_launch`, `ascensions/mega_mortar/mega_blast`; the vanilla explosion also plays, scaled up) |
| Chain Lightning | ✚ crackling arc that jumps. **Done** (`ascensions/chain_lightning/chain_crackle`, 3 variants) |
| Supercharge | ✚ rising charge hum on buffed towers. **Done** (`ascensions/supercharge/supercharge`) |
| Icicle Cannon | ✚ ice shard launch; ✚ shatter. **Done** (`ascensions/icicle_cannon/icicle_launch`, `ascensions/icicle_cannon/icicle_shatter` with 3 variants) |
| Magma Lobber | ✚ heavy lob; ✚ sizzling impact. **Done** (`ascensions/magma_lobber/magma_lob`, `ascensions/magma_lobber/magma_impact`) |
| Saw Dimension | ✚ low saw-hum loop (one global loop, louder with more saws). **Done** (`ascensions/saw_dimension/saw_hum`, seamless 3 s loop on the camera; volume follows the number of Shredders and fades out as the saws retract) |
| Bladestorm | ✚ whirring loop per tower. **Done** (`ascensions/bladestorm/blade_whirr`, seamless 2 s loop; hits re-pitch the vanilla ShredderHit) |
| Dead Man's Land | ♻ explosion. **Done** (the mine's own vanilla explosion, once per charge) |
| Lone Scout | ✚ soft tick (quiet; it fires often). **Done** (`ascensions/lone_scout/scout_tick`, 3 variants) |
| Nest of Bats | ✚ flutter loop; ✚ bite. **Bite done** (`ascensions/nest_of_bats/bat_bite`, 3 variants); the flutter loop was skipped |
| Firing Line | ♻ Cannon. **Done** (vanilla cannonballs) |
| Hwacha | ✚ rocket whoosh (variants); ✚ small pops. **Done** (`ascensions/hwacha/rocket_whoosh` with 4 variants, `ascensions/hwacha/rocket_pop` with 3) |
| Sealed Fate | ✚ stab, rising pitch with stacks. Not done: uses the vanilla sword hit sound |
| Ritual of the Sun | ✚ choir-like swell when the ring closes; ✚ beam roar loop. **Done** (`sounds/ascensions/ritual_of_the_sun/`, all on one chord, D major with an added ninth): `sun_link` (3 variants: a quick rising run of soft bells as a beam reaches the next prism), `sun_unlink` (2: two bells falling), `ritual_close` (the pulse round the ring as rising bells, then a choir swelling into the strike: formant-filtered saws singing "aah", opening up from 900 to 5000 Hz, with an upward shimmer and a rush of air), `sun_strike` (a deep boom 90 → 32 Hz, a fiery blast, the chord rung as bells, a long sizzle of embers), `sun_beam` (seamless 4 s loop: furnace roar under 160 Hz, flickering fire, a hum on D, the choir holding the chord), `ritual_break` (the chord sinking a fifth, a falling whoosh, the prisms going dark as falling bells), `crystal_power` (the easter egg: the chord as long glassy bells over a quiet hum on D). Not listened to yet |
| Beam Cannon | ✚ continuous energy beam loop. **Done** (`ascensions/beam_cannon/beam_hum`, seamless 2 s loop) |
| Particle Accelerator | ✚ charging whine (rising); ✚ black hole rumble; ✚ collapse. **Done** (`ascensions/particle_accelerator/accel_charge`, seamless 2 s loop whose pitch rises with the charge; `ascensions/particle_accelerator/accel_fire`; `ascensions/particle_accelerator/blackhole_rumble`, seamless 3 s loop with a deep oscillating layer, pitched down from 2.6x on entry and up again in the collapse; `ascensions/particle_accelerator/blackhole_collapse`, faint reverberations after the burst); reworked after the first listen. Transformation: a hydraulic pump throughout (`accel_pump_start` with the double start-up of the user's hydraulics example, `accel_pump_loop` that strains while a piece moves, `accel_pump_stop` falling into a clunk), `accel_lock` / `accel_lock_heavy` (the end of the key example: tick, snap and scrape, hard clack), `accel_unlock` (its first second: insertion ticks, the pins rattling), `accel_hiss` / `accel_vent` (steam), `accel_gears` (while sliding and tilting: the earthy low rumble of the first version with metal gear teeth and a short echo after the user's gears.mp3; the buzzing whir is gone), `accel_slither` (cables), `accel_click`, `accel_latch`. After the first listen everything in the transformation was lowered (the pump to 135 Hz, its strongest harmonic at 540 Hz; locks, unlock, hiss, vent, clicks, servo, latch about a third lower). Sounds live in one folder per ascension (`sounds/ascensions/<id>/`) |
| Ley Line Network | ✚ quiet arcane flow loop. **Done** (`ascensions/ley_line_network/ley_flow`, seamless 4 s pad + shimmer + soft flow, one global loop on the camera; louder with more income) |
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
| Mega Mortar | Fusion finds the largest squares: an L of 5 mortars → one 2×2 + 1 single; 3×3 beats 2×2; a Mega Mortar only grows and its cells can't be borrowed; levels average by module count |
| Chain Lightning | 30 enemies in a line 1.5 apart → exactly 20 unique hits; dead ends restart from earlier victims; no enemy hit twice |
| Supercharge | 1 coil firing twice a second = +30% (refresh only); 2 coils = +60% |
| Saw Dimension | Damage per second = Σ(Shredder damage)/10 (0 without Shredders); XP split adds up to the total |
| Dead Man's Land | Mines thrown within merge distance stack; an enemy on a 3-charge mine takes 3 explosions on 3 frames |
| Lone Scout | Marked enemy takes 3 ticks per second (+1 per Swift Scouts) with the mark bonus applied |
| Nest of Bats | Bats retarget when their target dies or leaves range; nearer targets are picked more often |
| Ritual of the Sun | Ring detection: triangle, square and 6-ring found; a chain without a cycle isn't; 2-cycles prevented. **Built** (4 tests): also a straight row isn't a ring (the middle obelisk blocks the end-to-end beam), the larger range counts, two beams per obelisk, the same links for any build order, centroid and radius, averaged stats with 100% burn, Focus Fire |
| Particle Accelerator | Charges in 30 s with enough mana and never faster without Ley Lines; stops below 25%; only Ley networks plugged into the core shorten it |
| Ley Line Network | Flood fill networks; income = siphons × crystals touched |

The balance framework ([balance-framework.md](balance-framework.md)) writes the **balance report** ([balance-report.md](balance-report.md), dev command `balance report`). Each ascension has a spec in `Balance/Specs/ContentSpecs.cs`, mostly a proxy marked for the M8 simulation.

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
- **Shift-drag building (for everyone, not dev only):** in build mode, holding Shift and the left mouse button after the first placement paints the same tower or building on every free grass cell the cursor passes (`Patches/PaintBuildPatches.cs`; the cells are a 4-connected line, `PaintBuildLogic.Line`, 2 offline tests). Each placement is vanilla's shift-click step (pay, track the cost, build where the ghost is, raise the price); occupied and non-grass cells are skipped, running out of gold stops the stroke, a War Blimp deck stops it. A stroke that starts near a cell's corner (where vanilla's click builds nothing) fills its first cell itself. Setting `General.DragToBuild`, on by default, with a "Shift-drag to build" toggle in both Options panels under "Loading animation" (the panel grows one row per mod setting). Scenario `paint-build` (10 checks: five Ballistas on five cells at rising prices, none again over them, the Options row); the mouse handling itself is not covered: the console cannot drive the mouse.
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
- **Tower name:** Mortar Module (loose, and the build button); a fused one is called **Mega Mortar**.
- **Effect:** Mortar Modules built in a full square (2×2, 3×3, …) fuse for good into one Mega Mortar, a single tower standing on the highest of its tiles.
  - **Damage:** base damage × number of modules (the modules' sum). Health, armor and shield multipliers and the level are the modules' **average**, weighted by module count (a 2×2 counts 4× when it grows into a 3×3).
  - **Blast radius** × side length; **range** + 2 per side above 1 (2×2 +2, 3×3 +4, 4×4 +6); fire rate unchanged.
  - **Only grows:** a bigger full square around a Mega Mortar (nothing sticking out of it) fuses again; its modules can't be taken for another square.
  - **Selling:** demolish sells the whole Mega Mortar, for what its modules would bring sold one by one. Its modules keep counting for the Mortar price.
- **Models:** square Mortar Module and Mega Mortar per size (2×2 … 8×8: wider, but the same height), yellow connectors in the middle of each side; concrete plinth.

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
- **Effect (as asked when building it):** obelisks link with beams of light; obelisks alone or in an open chain fire as usual, with +25% burn. Every obelisk of a **closed circuit** (triangle, square, any shape of 3+) turns its beam straight up and stops firing (it still gains XP); a huge beam comes down on the circuit's centre: the **average** damage of its obelisks, radius growing with their number, 100% burn. The beams between obelisks are the only hint at what to build.
- **Approach (as built):**
  - **Links** (`RitualLogic.Solve`): each obelisk holds at most **two** beams (one in, one out, like a prism). Pairs within the larger of the two ranges link from the closest up while both still have a free beam; ties in length go by position, so the result never depends on build order; a beam can't pass through another obelisk (closer than 0.45 to its line), so a straight row is a chain, not a flat triangle. So the links always form open chains and closed rings. This replaces the first idea ("each links to its nearest, not back"), whose result depended on the order the obelisks were processed in.
  - `RitualGrid` (one global object) solves again when an obelisk is built or demolished and every second (ranges change with cards), keeps a `LinkBeam` per link and a `SunRing` per ring, and draws the light running along the beams (one `QuadBatch`).
  - **Ring obelisks:** an `Obelisk.Update` prefix skips aiming and firing (and keeps the vanilla beam and sound off) but still calls `GainXP` while the obelisk has a target, so XP is "as normal".
  - **Sun beam** (`SunRing`): centre = the centroid of the ring's polygon (the corners' average if it is flat or crosses itself). Radius 0.25 per obelisk − 0.1 (3: 0.65, 5: 1.15, 7: 1.65, at most 6; after playtest: the first rule, 0.5 per obelisk, hit far wider than the beam looked), and the beam is drawn exactly that wide. Hits every monster whose body reaches into it at the obelisks' average rate (6/s) with their average damage (fractions carried), averaged multipliers and effects, burn 100% + the cards' burn. Focus Fire works per monster: +1 per second it has been in the beam, at most double. Mana: one average obelisk's cost per hit, only on hits with a monster inside; without mana it doesn't hit. Counts as Obelisk damage.
  - Sun Obelisks get +25% burn through the stat layer.
  - **Easter egg (after playtest, the user's idea):** if the ring's centre falls on a mana crystal's cell, the beam comes down on the crystal and powers it: the crystal turns gold (its palette ramped to gold, plus a glowing gold copy drawn over the column so it shows through the light) and a ritual circle 3× the beam's radius turns round it on the ground; the circle is the damage area. Breaking the ring gives the crystal its colour back. Sound `crystal_power`.
- **Visuals (as built):**
  - **Model** (`tools/VoxTool/Recipes/AscensionRecipesSun.cs`): `sun_obelisk`, a stepped sandstone pyramid (5 tiers, block courses in four shades) with a gold ledge round every tier and a darker gold inlay up the middle of each face, a gold sun disc with an amber heart and eight rays on each face of the base, gold-capped obelisks on its four corners and a gold crown with four prongs. Over the prongs floats `sun_prism` (its own model on the turret, half-size voxels): an eight-faceted crystal bipyramid along the turret's forward axis, each facet shaded, the ridges brighter, three facets faintly tinted (violet, mint, rose) like light split by a prism, with a gold halo ring round its middle. It aims like the vanilla beam; in a ring it turns to the sky (the ring becomes a halo). `ritual_card`: the pyramid at twice the detail with the prism standing up. The turret is raised to the prism's centre. After the first screenshot the sandstone and gold were darkened: in the game's sun the first colours looked almost white.
  - **Own beam:** the vanilla red beam art is hidden; while the game fires the obelisk, a golden beam (additive white-gold core and orange glow, flickering) runs from the prism to the target's body, with a glint and sparks at the hit.
  - **Links:** a gold beam between prisms that shoots across when the link forms, with packets of light running along it (chains: from one end to the other; rings: counter-clockwise), wider, brighter and faster in a ring; a glint on every linked prism (bigger in a ring). They fade when the link breaks.
  - **Ring closing** (1.12 s to the strike): a pulse of light runs once round the ring (0.6 s), each prism flashing and turning to the sky as it passes and sending a beam straight up; from the tops of those beams thin beams converge on a **sun** that pops into being over the centre (white-gold disc, two layers of rays turning opposite ways, 5 + 0.3 × radius high); the sun strikes: the column falls to the ground, a flash, a shock ring, sparks and embers, a short screen shake (`ScreenShake.Kick`, new), then it widens to the full radius.
  - **The column** (reworked after playtest: the first was a flat strip turned to the camera, which terrain cut through like a card): a volume of light, drawn as the camera-facing half of nested cylinders (`Shaft`) that fade from their middle towards their silhouette, the usual light-shaft technique. Everything standing inside the beam is behind that surface and lies in the light; things in front still hide it. Layers: a soft orange halo (additive), a golden body (alpha-blended, so it stays gold over the bright path; with a firm edge, so its visible width is its damage radius), streaks of light running down, a white-hot core 0.4 as wide. It keeps its full width up into the sun, and the sun's disc is 1.1× its width. It reaches down to the **lowest ground under it** (path level; a `HeightField` samples the terrain straight down on a grid), so it no longer stops on a crystal or a raised block. Where it meets anything (grass, path, crystals, towers inside it, each at its own height) a glow flickers and sparks splash up, motes rise from there, and the strike's shock wave runs out over the ground at the ground's own height. A warm point light pulses. Between waves it burns a little calmer and quieter. The ground sigil of the first version is gone (it lay at one height, under raised terrain); it now only marks a powered crystal's circle, laid over the terrain sample by sample so it follows every step.
  - **Ring breaking:** the column draws up into the sun, the sun shrinks and fades, the beams up retract, the prisms turn back down.

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
  - Models: the core (ring accelerator and a huge barrel) and the capacitor (the same parts: the barrel stood on end as a battery cell, the base sunk into the ground); a hydraulic transformation between the two.
- **Approach:**
  - Charge = mana drained; a black hole costs 1800 mana, drained at most 60 mana/s, so one shot per 30 s at best. Only a faster drain (Ley Line Network) charges faster. The popup shows the charge in mana (`Charge: 1240/1800`) and the drain with the seconds left.
  - **Attack speed** never adds shots: it makes the black hole tick faster (4 ticks/s × the core's attack speed, over the same 5 s).
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

**Phase 3, as built so far** (`src/TreasureRun/Ascensions/Phase3`):
- **The Saw Dimension** (`SawDimension.cs`, logic in `SawDimensionLogic.cs`, 4 offline tests in `tests/TreasureRun.Tests/Phase3Tests.cs`; 44 tests in total):
  - Every Shredder becomes a **Saw Engine**: a `Tower.Update` prefix skips aiming, attacks and vanilla XP; the turret spins while the saws run.
  - One global `SawField` lays a floor saw every 0.9 along every path segment (waypoint → next and split-next). The layout is rebuilt when the number of waypoints changes, so it follows map expansion. Spawns happen at waypoints, so the saws cover every stretch monsters walk.
  - **Damage:** 5 ticks a second on every monster in `SpawnManager.currentEnemies`. The base damage per tick is Σ(Shredder damage) / 10 / 5, with fractions carried, so there is never a forced minimum-1 hit. At most 5 hits a second; above that the hits get bigger, and the remainder is spread over the second (6 base damage per second hits 1, 1, 1, 1, 2). With few Shredders the hits are rarer instead (1 per second → one hit of 1 per second), so each hit shows the same number and more engines show up as more frequent hits until every tick hits. Multipliers, bleed and the other effects are the Shredders' averages. The hits count as Shredder damage, so Shredder cards keep working.
  - **XP:** each Shredder gains the vanilla per-second XP of a tower with a target (1 / (2 × range) + 1/2), split over the shield, armor and health pools by the share of monsters on each layer. It gains nothing while no monsters are on the map.
  - **No Shredders:** the saws sink into the floor (0.6 s) and stop; the dark slits stay visible. Building one raises them again.
  - **Drawing:** `Graphics.DrawMesh` per saw and slot. The game build strips the Standard shader's instancing variants, so `DrawMeshInstanced` rendered nothing.
  - **Models** (`tools/VoxTool/Recipes/AscensionRecipes3.cs`): floor saw (vertical disc, half of it above the path), slit, Saw Engine base (power cables) and top (generator with an upright saw). **Sound:** `ascensions/saw_dimension/saw_hum` (`tools/SfxTool/Recipes/AscensionSounds3.cs`), not listened to yet.
  - **Scenario** `saw-dimension` (`Debugging/Phase3Scenarios.cs`): two Shredders away from the dummy; the saws deal 42 DPS to it (direct hits 40 = Σ damage 20 / 10 × health multiplier 20); the Shredders gain XP; after demolishing them the saws retract and stop hitting.

- **Mega Mortar** (`MegaMortar.cs`, logic in `MegaMortarLogic.cs`, 4 offline tests; 49 tests in total). Reworked after the first playtest (summed multipliers were far too strong; the fused mortar was still several hidden towers):
  - Every Mortar becomes a **Mortar Module**. Building one (or gaining the ascension) marks `MortarFusionGrid` dirty; it fuses with `NextFusion` until nothing is left. Rule: the largest square (ties: lowest z, then x) that is completely covered by at least two units (loose modules or Mega Mortars), none of which sticks out; up to 8×8.
  - **Fusing:** the biggest unit's tower survives; the others are destroyed without a refund and taken out of the Mortar flyweight. The survivor moves to the middle of the square at the highest tile's height (so the vanilla height bonus and the targeting centre are right), its root `BoxCollider` grows to the square (it blocks building there and opens its popup), its base mesh becomes `mega_mortar_N` and the turret (muzzle) is raised 0.25. Level, damage and multiplier upgrades and XP pools become the module-weighted averages (`MegaMortarLogic.Average`). From then on it is a normal tower: vanilla targeting, XP and level buying.
  - **Stats** (`Tower.SetStats` postfix): damage × Size², range + 2 (Size − 1), blast × Size.
  - **Prices:** modules inside Mega Mortars (`ExtraModules` = Σ Size² − 1) still count. `TowerFlyweight.AddNewTower`/`RemoveTower` patches recompute the build and sell prices and the refund of a loose module with them. Demolishing a Mega Mortar pays `SellValue` (all its modules sold one by one; the popup shows it) and lowers the price by all of them.
  - **Firing:**
    - **Shell:** every module (loose or fused) fires the modern `mortar_shell` in place of the vanilla shell art. Its red tail light (`mortar_shell_light`, same canvas) blinks 3 times a second by switching its renderer on and off; there's no real light source, to keep it cheap. Mega Mortar shells are drawn bigger.
    - **Recoil:** the barrel is a separate model (`mortar_barrel`, `mega_barrel_N`, drawn on the same canvas as the base so they line up). It sinks into its collar in 0.06 s and eases back over 0.45 s.
    - **Steam** (Mega Mortars only): `SteamFx` vents soft white puffs and thin, fast stretched streaks from the barrel rim out to all sides. From 3×3 on, a stream also shoots straight up out of each vent grille: the 4 corners on a 3×3, more on bigger ones (`MegaMortarLogic.VentTiles`, the same rule as the model). Two pooled world-space particle systems (Sprites/Default with a generated soft-circle texture) serve every Mega Mortar.
    - **Sounds and impact:** `mega_launch` plays at the muzzle; on impact, a scaled-up copy of the vanilla explosion, `mega_blast` and debris.
  - **Name:** `AscendedTower.DisplayName` (new, per tower) is "Mega Mortar" for fused ones; the popup title uses it.
  - **Plinth:** with different tile heights, a concrete block (`mega_plinth_N`) stretched from the lowest tile up to the highest. Before, a 2-unit block hung below the map edge.
  - **Models** (`AscensionRecipes3.cs`, `MortarBody`): `mortar_module` (16×16×12, square light steel base with panel seams, a yellow connector socket in the middle of each side, hazard-striped gunmetal deck, cyan corner lights, short wide barrel), `mega_mortar_2…8` (the same design 16N wide but always 16 high, a barrel 4.6N wide, a vent grille on each tile the barrel leaves free), `mega_plinth_2…8`.
  - **Scenarios:** `mega-mortar`:
    - An L of 5 modules → one 2×2 + 1 loose module, with 2 towers left. The price is unchanged by the fusion; the Mega Mortar is named, centred and its tiles are blocked.
    - Damage 80 = 4 × 20, multiplier 10 (averaged), range +2, blast 2×; the biggest hit is 800; it gains XP.
    - Grows to a 3×3 with 5 more modules: level (4 × 5 + 5 × 1) / 9 → 3, price for 9 modules.
    - When it fires: the barrel recoils and returns; the shell is the modern one (screenshots of the steam and the shell).
    - Demolish pays 4500 g and the price returns to 200 g.

    `mega-mortar-hill`: fuses on a slope; it stands on the highest tile, and the plinth spans from the lowest tile to the highest.

- **Ley Line Network** (`LeyLineNetwork.cs`, logic in `LeyLineLogic.cs`, 3 offline tests; 52 tests in total). Income rule reworked after playtest (the first version paid every siphon × every crystal):
  - **Networks:** every Mana Siphon becomes a **Ley Siphon**. `LeyNetwork` (one global object) recomputes on every build or demolish, and when the crystal count changes (checked every 2 s). `LeyLineLogic.Solve` flood-fills siphons over the 4 neighbours at **any** height (after playtest feedback; vanilla only gathers from a crystal at the same height). A crystal counts once per network if it touches any of its siphons.
  - **Income:** only **taps** (siphons next to a crystal themselves) gather: rate × (1 + ½ per further crystal in the network), rounded down, so rate × (C + 1) / 2 (`LeyLineLogic.TapIncome`). They gather once a second during waves (a `ManaSiphon.Update` prefix replaces vanilla gathering). Other siphons are **connectors**: they gather nothing but link crystals into the network. The HUD's mana rate gets the difference to what vanilla already shows.
  - **Popup:** the siphon popup shows the network (siphons, crystals) and the income of the siphon and of the whole network.
  - **Visuals:** hollow glass pipes with gold edges, world-aligned (the vanilla siphon art is hidden because it turns towards its crystal).
    - **Generated in game:** the geometry is `src/Shared/Vox/LeyGeometry.cs`, shared with VoxTool, on one fixed palette. The game meshes each part per port combination the first time it is needed and caches it (`LeyParts`). VoxTool only writes `ley_palette` (whose material all parts use) and the card art.
    - **Glass:** the panes are a second mesh per part, drawn with the unlit alpha-blended ghost material on the same palette. The build strips the Standard shader's transparent variants, so a Fade copy rendered opaque white.
    - **Hubs:** a 6x6 glass junction. Where a pipe joins, the hub's face takes the pipe's own cross-section, so the pipe's gold edge lines and glass panes run straight on into the hub with no break in the border (as in the card edit from playtest). The pipes' bottom gold lines continue across the hub's floor. A tap stands on a wooden plinth with a glowing mark and has purple indicator corners (vivid while the network touches a crystal, pale otherwise). A connector stands on wooden feet and has no indicators.
    - **Corners:** on risers and drops they are as wide as the pipe (4x4); only hubs are wider.
    - **Funnels:** solid stepped rings (each overlaps the one before, and the gold corners are blocks as thick as the wall, so the gold edges step in Ls, never diagonals), with the mouth exactly the crystal's size (8x8 voxels) and flush with its face. Ending exactly on the crystal's face is fine, as the two faces point opposite ways; the earlier dithering came from same-facing faces inside the crystal.
    - The brass first style was dropped after the glass experiment.
  - **Heights** (`LeyLineLogic.Plan`): between nodes of different height the pipe runs at the higher one's level, and the lower siphon carries it.
    - **Riser and tees:** the lower siphon gets a riser (`ley_riser`, stretched) up to its highest neighbour, with one tee fitting (`ley_tee`) per level where arms branch off, so several higher neighbours never get overlapping pipes. Its arms and intakes sit at those levels.
    - **Lower crystal:** it can't carry anything, so the siphon's arm goes to the tile edge, then an elbow (`ley_elbow`), a stretched drop pipe (`ley_drop`), and an elbow with the funnel at the crystal's level (`ley_funnel_low`).
  - **Motes:** all motes are one mesh, built from copies of the `ley_mote` vertices and rewritten every frame. That is one `Graphics.DrawMesh` call, with no GameObjects; the arrays are rebuilt only when the number of motes changes. Motes don't rotate and are 0.06 across, so they fit the tubes' 2x2 hollow (0.125). They move along polylines (up the risers and across), from crystals into the siphons and between siphons away from the crystals; a network without crystals stays still. A fully GPU version would need a custom shader built into an AssetBundle with the game's Unity version, which the mod has no pipeline for yet.
  - **Popup:** short lines (gathering siphons and crystals in the network, this siphon's mana/s or "Connector", the network's mana/s), with best fit so it fits the vanilla box.
  - **Particle Accelerator hook:** networks with a siphon next to the Accelerator's core feed it (`LeyNetwork.AcceleratorIncome`, see below).
  - **Scenario** `ley-line`:
    - Setup: test crystals are placed from the crystal prefab, with the spawn roll forced. 3 siphons in a row, each next to its own crystal (3 crystals).
    - Network: 3 taps × 2 = 6 mana/s; the HUD shows +3 over vanilla's 3. The pipes and intakes are in place, with 3 flowing links.
    - Gathering: 6 mana/s measured during a wave.
    - Demolishing the middle siphon splits the row into 1 + 1 mana/s, removes its pipes and puts the HUD back.

    `ley-line-hill`: a low siphon (a tap) with higher neighbours (connectors). It forms one network with 1 mana/s; the indicators are lit; it has a riser and one tee per level; screenshots of the slope and of the popup.

- **Particle Accelerator** (`ParticleAccelerator.cs`, `BlackHole.cs`, `AcceleratorFx.cs`, logic in `ParticleAcceleratorLogic.cs`; 8 offline tests incl. the Ley feed, 60 in total). Reworked after the first playtest (too small, charged far faster than 30 s, the Ley interaction wasn't physical, sounds):
  - **Roles:** every Particle Cannon gets `AcceleratorCannon`. `AcceleratorGrid` (one global object) makes exactly one the **core**: the highest level (then the most damage) when the ascension is gained or when the core is demolished. Every other one is a **Capacitor** (popup title): it never fires (`Tower.Fire` prefix) and doesn't aim (`Tower.AimTurret` prefix; its coil stands upright) but still gains XP. "Make Core" (a copy of the demolish button, on a capacitor's popup) moves the role. The charge belongs to the grid, so it carries over in both cases.
  - **Transformation** (core ⇄ capacitor; redesigned after the second playtest: logical mechanical steps with temporary hydraulics instead of layer by layer). The capacitor is made of the core's own parts, so the transformation moves them: computed offline by VoxTool (`tools/VoxTool/Recipes/AcceleratorMorph.cs`, `VoxTool morph <models> [previewDir]`, also run by `build`; it also writes the capacitor model), written to `models/ascensions/particle/accelerator_morph.txt`: 11 pieces (feet, base, turntable, column, cell = the barrel, + terminal, cables, two outrigger beams, two rams), their poses (slide, 90° tilt about the cell's middle, ram length, visibility) and voxel changes (appear, vanish, recolour at a time within the step). VoxTool checks that composing the pieces at the last pose gives exactly the core (base + barrel) and that no pieces overlap at rest. Steps, capacitor → core (seconds at the current slow motion, TimeScale 2): **rise** 1.2 (the base comes up 7 voxels out of the ground with everything on it: the Ley sockets appear), **unlock** 0.6 (the + terminal sinks into the cell), **sleeve** 0.9 (the steel sleeve flips to the dark plating, top to bottom), **outriggers** 0.6 (two beams slide out of the turntable), **rams** 1.1 (hydraulic rams extend from their tips to the cell's middle ring, the clamp heads lock on and light up), **lift** 0.7 (the cell comes 2 off the turntable), **tilt** 1.6 (it tips forward 90° on the clamps, muzzle from up to forward, settling), **column** 0.7 (the support column rises out of the turntable), **lower** 0.7 (the barrel sits down on it), **release** 1.1 (clamps open, rams retract, outriggers slide back), **connect** 1.4 (the cables feed out up to the barrel; meanwhile the two gold rails rise out of the barrel's top on its centre line, touching, slide apart to the window's edges, opening the charge window column by column behind them, and settle onto the edges; after playtest: the first version ran them outwards along the barrel): 10.6 s at TimeScale 2, plus 0.5 s for the core to turn its barrel back to straight ahead first. **Now TimeScale 0.28: 1.5 s** (set by the user). The rails are their own pieces (13 pieces in all); a piece can have several tweens in one step (the player takes the last one that has begun). In game (`MorphPlayer`) the real models are hidden and each piece is its own mesh (a growing voxel is a cube stretching from its face); played backwards for core → capacitor. While it transforms the core doesn't fire, shows no charge and takes no Ley pipe (its sockets are still coming up); the charge keeps filling. **Speed:** all step lengths are multiplied by `AcceleratorMorph.TimeScale` (VoxTool; the build prints the sum at TimeScale 1, 5.3 s): for about 1.5 s set it to 1.5 / 5.3 ≈ 0.28 and run `dotnet run -- morph ../../models`; the game reads the lengths (and scales the turn-back) from the file; F6 reloads it. The pump start-up (1.1 s) and stop (1 s) are longer than the whole 1.5 s sequence; the per-step sounds and loops follow the steps. `SfxTool morphpreview` mixes the whole transformation per direction into `sounds/previews/` for listening (README).
  - **Charge:** only during waves, a fixed **60 mana/s** (1800 per black hole). Charge = drained mana / (30 s x 60), so without Ley Lines it can never be faster than one black hole per 30 s; with less mana coming in it simply takes longer. Never below 25% of the maximum mana; at the floor it only drains what comes in. (The first version scaled the drain with the cannon and added *all* Ley income, which let 18 mana/s of Ley income charge it in about 8 s.)
  - **Ley Line easter egg:** a Ley Siphon next to the **core** (never a capacitor) plugs into it: `LeyNeighbourKind.Accelerator`, a feed pipe with a gold coupler flange into a Ley socket on the core's base (sockets on all four sides, the pipe's cross-section on their faces, at the crystal face position, tile y 20), or a drop into a lower core. Every network touching the core adds its income to the drain (`LeyLineLogic.Solve(..., accelerator)` → `LeyNetwork.AcceleratorIncome`), and that mana charges too: 60 mana/s of plugged-in income halves the charge to 15 s. Mana motes flow from the network into the core; the siphon popup says "Feeds the Particle Accelerator"; the core popup's drain shows the sum. Make Core moves the feed; selling the core moves it to the new core or (the last cannon) removes it (the grid compared cores with Unity's `==`, which treats a sold core as null, so selling the last cannon left the pipe in place).
  - **Firing:** when full and the core has a target (range 20), a black bead with a violet trail flies from the barrel's muzzle to where the target will be and opens the black hole. The charge starts over at once.
  - **Black hole** (`BlackHole`): for 5 s it catches every monster within **4** units on the ground plane (10 times a second; one hole per monster), holds their `Pathfinder` and lifts each into its own slot in the disc, 0.85 above the ground (a sunflower layout, radius 1 to 2.3, turning as one so they never stack, pulled by a spring at most 7 units/s). Damage: the core's and every capacitor's base damage summed (layer multipliers weighted by damage, the core's effects), 4 ticks a second of 0.4x, then a 4x burst: 12x per caught monster.
  - **Look** (after classic renderings of a lensed disc, adapted to the game's tilted orthographic camera): an opaque black **shadow** (radius 0.6) with a thin additive **photon ring**. The **near half** of the accretion disc (radius 0.64 to 2.1) lies flat in the world in the monsters' plane and crosses in front of the shadow; the **far half** is drawn where its light arrives, bent up over the shadow into an arc (height max(r·tilt, 1.12 Rs + 0.5 (r − inner)) instead of r·tilt), plus a thin dim secondary image under it. Both halves are dynamic meshes rebuilt every frame: white-yellow inside, orange, red and deep red outside, thin filaments and violet streaks winding with a differential rotation, **Doppler beaming** (the side turning towards the camera hot and white, the other deep red). Each half is drawn twice: a dark smoky underlay (blended) and the same disc as light (additive, `Particles/Standard Unlit` set to One/One, the only additive-capable shader in the build), so it glows on any ground. 150 small additive motes in reds, yellows, violet and white spiral in and follow the same lensing. A faint violet glow and a dark well on the ground (the dashed reach ring was removed after playtest).
  - **Space warp** (`SpaceWarp`, `WarpLens`): a second camera (a copy of the main one, half resolution, no alpha in its texture) renders the scene without the hole's visuals (layer 31) and without world UI; around the hole a camera-facing annulus shows it with each point sampled from β = ρ − E²/ρ (Einstein radius 0.8), so the ground and path bend into a ring around it. It fades out before it would cut into the ground in front. After the burst it becomes a ripple running outwards. Only while a hole exists.
  - **Screen shake** (`ScreenShake`): applied to the main camera only for rendering (onPreCull/onPostRender), stronger the nearer the hole is to the middle of the screen and the more zoomed in: a rumble as it opens (fading over about half a second), a low tremor for the whole 5 s, building in the collapse, and a short hard shake at the burst.
  - **Collapse and burst** (kept from the first version, scaled up): in the last 0.6 s the orbits tighten to 35% and spin up, the disc shrinks and speeds up, the shadow shrinks to a white point. Then the last hit, a shock ring across the reach, three spark bursts, an expanding halo, a light flare, an afterglow; every caught monster is thrown 1 to 3 units (path preferred, never off the map) and walks back to its waypoint.
  - **Core model:** `accelerator_base` (Ley sockets on four sides, pedestal, ring accelerator on four magnets) and `accelerator_barrel`, a free model on the turret instead of the vanilla top: a turntable on the pedestal, four thick cables with violet bands from it to the barrel's flanks, and a huge barrel (radius 4.6 voxels, 18 long): power housing with vents, three copper coil rings, gold-railed **charge window** along the top and a stepped violet muzzle around a black void (VoxTool draws it in layers, `BarrelLayers`, so the transformation can take it apart). The charge window fills with light from the back as it charges (white and pulsing when full); energy runs up the cables while it drains; bunches race round the ring. **Capacitor** (`capacitor_base` only, the vanilla top hidden; redesigned after the second playtest: wider and more like a battery): the barrel stood on end on its turntable as a battery cell in a **steel sleeve** (one colour, `PaDeckLight`, to swap for the dark plating if it doesn't fit) with the copper rings as bands, no charge window, and a gold **+ terminal** with a white-hot tip out of the muzzle; the base sunk 7 voxels so the ring accelerator lies on the floor, only the corner feet stay put (24 voxels tall, the core 26). Each capacitor loops a pulse to the core and back from its + terminal: a bright packet arcs high to the core's ring (flash), a smaller bluish return pulse arcs back low (terminal flash), faster while draining; no circuit while either end transforms.
  - **Sounds** (third rework after playtest): the rumble plays at the hole, full volume out to 30 units, on four identical sources in sync (`SoundLoop` layers; AudioSource volume stops at 1) for +10 dB; saws at 62 Hz with a 31 Hz sub pulsing 6.7 times a second, played at 0.72x (entry from 1.5x). In the collapse it swells in volume and **drops** in pitch (to about 0.4x) and is cut off dead at the burst. `blackhole_collapse` is then a puff falling in pitch with a long, hollow, empty-hall tail (sparse 130 ms echoes into a big, barely damped, dark reverb, about 4 s), well under the implosion. The core's charging whine is 10 dB louder.
  - **Scenarios:** `accelerator-morph` (two cannons swap roles, 24 frames of each direction with game time at 15%), `particle-accelerator` (34 checks: roles and models, 60 mana/s and the 30 s cap with plenty of mana, the 25% floor, firing, shake and warp, 6 dummies lifted into the disc without stacking, ticks and burst, release and landing, Make Core, losing the core), `particle-ley` (siphons next to the core feed it with a feed pipe each, the drain rises by their income, the capacitor gets none, Make Core moves the feed), `black-hole-look` (screenshots only).

- **Ritual of the Sun** (`SunObelisk.cs`: component, `RitualGrid`, `LinkBeam`, the `Obelisk.Update` prefix; `SunRing.cs`: the ring, `LightBeam`, `Shaft`, `HeightField`, `CrystalPower`, `SunFx`; logic in `RitualLogic.cs`, 4 offline tests, 64 in total). Rules and look in §3 (Obelisk). **Scenarios** (`Debugging/RitualScenarios.cs`): `ritual-sun` (26 checks: two obelisks link and fire with 25% burn; the third closes a ring of radius 0.65 centred on the triangle's centroid, reaching down to the lowest ground; the prisms point up and the obelisks stop firing; the beam hits 6 times a second with the average damage, 100% burn, counted as Obelisk damage; the obelisks still gain XP; demolishing one breaks the ring and the others fire again; the triangle is placed on checked free grass by the track), `ritual-crystal` (8 checks: a triangle round a mana crystal powers it, the crystal turns gold, the circle is 3× the beam's radius and hits a monster outside the beam, breaking the ring restores the crystal) and `ritual-look` (five obelisks round the track, ten frames of the ignition at 15% game time, the ring at three zooms, the prism, the break). They need track: in a fresh run, place a few tiles first (`wave`, `killall`). Gotcha found on the way: this compiler treats `field` inside a property accessor as the property's own backing field (C# 14), so `Field => field` returned null; don't name members `field`.

**Phase 3 is done.** Every M7 ascension is built; what is left is listening to the sounds, playtesting and the balance notes in `docs/balance-todo.md`.

## 5. Open questions (summary)

1. Supercharge: does a coil buff other coils, itself? (default: others yes, itself no)
2. Nest of Bats: do bites cost mana? (default: no)
3. Ritual of the Sun: do linked obelisks keep their normal beams? **Decided by the user:** obelisks in a closed ring point their beam at the sky and stop firing (still gaining XP); alone or in a chain they fire as usual.
4. Beam Cannon: hit the first enemy on the line, or all of them? (default: first, as built)

**Decided:** Particle Accelerator uses one core plus capacitors (only one black hole source, fixed 30 s charge). Cluster Munitions bomblets have 50% radius; Mega Mortar radius × side length, base damage × modules with multipliers and level averaged (weighted by module count), +2 range per side above 1, vanilla fire rate, fusing is permanent; Magma Lobber lobs like the Catapult; Saw Dimension has no base damage and retracts without Shredders; the Cannon ascension is called Firing Line; Ley Line Network keeps quadratic income for now.

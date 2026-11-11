# Balance TODO

Things known to need tuning, collected for M9 (content & balance). All numbers are first drafts.

## Castles

| Castle | Concern | Knobs |
|---|---|---|
| **Arcane Spire** | Too tanky: mana is easy to come by, so the 1-health fragility rarely shows. It is meant to collapse once the mana economy falters. | `ArcaneSpireBehaviour.ManaPerDamage` (5), regen 5 mana per 2.5 s pulse, full mana at start, free Mana Siphon + 4 corner crystals. Ideas: 8–10 mana per damage, no passive regen, or a cost that scales with level. |
| Gilded Vault | Flat income `8 × L + L²` and 4% interest (cap `25 × L`) are rough guesses at replacing kill gold. | `GildedVaultBehaviour` constants, vault card bonuses |
| Siege Workshop | The castle cannon is a vanilla Cannon tower (20 damage, 20 rpm) with range 12 that levels like any tower; Cannon cards also buff it. 2× tower prices may be too harsh or too soft. | `SiegeCastleTower` constants, `TowerPriceMultiplier` |
| Necropolis | Rise radius 10.5 and 20% health; minions only rise from kills near the castle, so value depends on the build. | `NecropolisBehaviour` constants |
| Druid Grove | 20 health; regrowth `1 + level/15` trees per wave; +1 health per chop (+1 per Heartwood card, +2 with Golden Sap). Possibly too strong, since health regenerates without limit. | `DruidGroveBehaviour` constants |
| Colosseum | Champion health = replaced wave × 1.2 (strongest monster type); damage to castle `+4 + level/5`. Check early champions (level 5/10) against a starter build. | `ColosseumBehaviour.HealthFactor`, damage formula |
| Crow's Nest | +10 high ground is huge early; the start tile is fully blocked with ore. | `PlatformHeight` |
| Glass Fortress | Free max-health costs are the only upside. Probably needs more, e.g. a damage bonus or extra treasure choices. | — |
| Castle Town | 1.5× spawns at 2/3 gold each; income 1.5 × level per adjacent building. | `CastleTownBehaviour` constants |

## Ascensions

| Ascension | Concern | Knobs |
|---|---|---|
| **Strike Fighter** | A bit too powerful (playtest feedback). It is a pure stat boost on the vanilla AI, so tone down one of speed, fire rate or magazine. | `StrikeFighter.SpeedFactor` (2.2), `TurnTime` (0.55), `FireIntervalFactor` (0.6), `Magazine` (30) |
| Carpet Bomber | Bomb rate (1 per 10 Radar shots) and 3× damage are guesses. Weak on short tracks early, possibly strong on long straights. | `CarpetBomber.BombIntervalFactor`, `BombDamageFactor`, `BlastRadius`, `MaxBombs` |
| Repeater | Measured 57 DPS vs. 37 for a vanilla Ballista on a dummy (≈ 1.5×, as designed). Bursts waste bolts on low-health targets. | `RepeaterLogic.DpsFactor`, `BoltSpacing` |
| Springald | Up to 3× a vanilla shot when 5 monsters line up (100+80+60+40+20%); 1× against single targets. | `SpringaldLogic.FalloffPerHit`, `MaxHits` |
| Lone Scout | 3 ticks/s × (1 + Lookout damage) × (1 + its health multiplier): 18 DPS at level 1, scaling with Lookout levels. Swift Scouts +1 tick each. | `LoneScoutLogic.BaseTicksPerSecond` |
| **Chain Lightning** | 20 full-damage hits per attack (vs. everything within range 2 in vanilla): very strong on dense waves. More Chains adds +5 each. | `ChainLightningLogic.BaseHits`, `HitsPerCard` |
| Supercharge | +30% attack speed per coil for 1 s; several coils around one tower stack without limit. Tesla range is small, so it needs tight clusters. | `SuperchargeBuffs.BonusPerCoil`, `Duration` |
| **Sealed Fate** | Swords never expire before the victim dies, so bosses collect a new sword every 5 s (5 swords after 25 s, each up to 3× damage). | `SealedFateLogic.MaxFactor` |
| **Nest of Bats** | **Far too strong as specified:** 10 bats × the lair's full damage once a second measured 1270 DPS on one dummy, vs. 12 for the vanilla pulse. Probably needs a fraction of the lair's damage per bite (e.g. 10–20%) or longer bite intervals. | `NestOfBatsLogic.BiteInterval`, `BaseBats`, bite damage |
| Icicle Cannon | 3x the keep's damage every 1.2 s, piercing everything on the line, plus the keep's slow (one icicle reaches the 60% slow cap). Measured 610 DPS over 4 stacked dummies at level 1-2. Range 6 instead of 2. | `IcicleCannonLogic.DamageFactor`, `BaseInterval`, `BaseRange` |
| Cluster Munitions | 5 bomblets x 50% = 2.5x a shell's damage on the centre monster (measured 68 DPS vs. 92 expected when all 5 hit; vanilla Mortar about 33). More Bomblets adds +50% each, up to 5 cards. | `ClusterMunitionsLogic.DamageShare`, `RadiusShare`, `BombletsPerCard` |
| Firing Line | Vanilla Cannon damage, but it fires all wave long, target or not, and its balls fly 1.75x faster and 3x its range. Its strength depends entirely on the aim; on a long straight it is very good. | `FiringLineLogic.BallSpeedFactor`, `TravelFactor` |
| Hwacha | 20 rockets x 30% = 6x per volley, every 6 s, +30% burn per rocket; blast 0.6 around the landing point; minimum range 2. Rockets scatter over 1.5, so a single monster takes about a third of them (measured 53 DPS vs. about 67 for a vanilla Cannon); crowds take far more. The rocket count is a config value and the damage per rocket stays, so more rockets = more damage. | `HwachaLogic.DamageFactor`, `Burn`, `Scatter`, `BlastRadius`, `MinRange`, `IntervalFactor`, config `HwachaRockets` |
| Magma Lobber | 4x damage in a 1.4 blast every 4 s, +100% burn on top of the Flame Thrower's 100%; range 8. The magma pool deals 20% of the shot's damage per second for 3.5 s to monsters in it, and adds burn worth the same. Not measured beyond "it hits and burns". | `MagmaLobberLogic` constants (`PoolBurnPerSecond`, pool times) |
| **Bladestorm** | Whole saw blades on one ring at the range edge (3 at base range), 75% damage per pass with the Shredder's 100% bleed. A monster under the ring takes about 1.2 passes/s (about 0.9x the Shredder's damage per second); monsters close to the tower are never hit. Range adds blades at a fixed spacing (2.5), so range cards scale it. | `BladestormLogic.DamageFactor`, `BaseRevsPerSecond`, `BladeSpacing`, `BladeDiameter` |
| **Beam Cannon** | 1.3x vanilla DPS for 1.6x mana (measured 257 vs. 227 for the vanilla cannon on a dummy, 3.3 vs. 2.0 mana/s). **Risk:** the beam hits 10 times a second with a base damage of about 1, so flat per-hit bonuses (+X damage per hit, per-hit procs like Static Arc or Hemorrhage) apply about 50x as often as on a vanilla shot. | `BeamCannonLogic.TicksPerSecond`, `DpsFactor`, `ManaFactor` |
| **Dead Man's Land** | Charges pile up across waves without a cap (merging only bounds the number of mines, not their charges), so a camp next to a busy path builds very deep mines over a long run. | a charge cap per mine, merge radius |
| Ley Line Network (planned) | Income = siphons × crystals touched, quadratic by design (late-game infinite mana is intended). Tune if it arrives too early. | per-crystal rate |
| Mega Mortar (planned) | Fused mortar: blast radius × side length, damage = sum of all members (incl. levels and upgrades), vanilla fire rate. Check whether one big shell outperforms the separate mortars too much. | radius factor |
| Particle Accelerator (planned) | One core plus capacitors: capacitors add their damage to the black hole, charge stays 30 s; the Ley Line Network is the only way to charge faster. | charge time, capacitor share |
| **War Blimp** | **Far too strong** (playtest feedback), even for a Mythical. Continuous gatling (about 3× the biplane's average fire) + bombs + a flying tower. The deck tower's +3 high ground and altitude-extended range are arbitrary. Mythical, so only via Prism/debug for now. | `WarBlimp` constants, `DeckHeightBonus` |

## Treasures and cards

- Starter treasures are M2 placeholders. Review all of them in M9 (see `docs/treasures.md`).
- Siege Workshop treasures (Wall Cannons … Trebuchet Battery) and Druid Grove treasures (Sapling Seeds, Ironbark, Golden Sap) are untested beyond a smoke test.

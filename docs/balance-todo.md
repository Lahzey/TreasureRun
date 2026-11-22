# Balance TODO

Things known to need tuning, collected for M9 (content & balance). All numbers are first drafts.

## Castles

| Castle | Concern | Knobs |
|---|---|---|
| **Arcane Spire** | Too tanky: mana is easy to come by, so the 1-health fragility rarely shows. It is meant to collapse once the mana economy falters. | `ArcaneSpireBehaviour.ManaPerDamage` (5), regen 5 mana per 2.5 s pulse, full mana at start, free Mana Siphon + 4 corner crystals. Ideas: 8–10 mana per damage, no passive regen, or a cost that scales with level. 5 mana per damage is too generous (playtest feedback). |
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
| **Bladestorm** | **Way too powerful** (playtest feedback). Likely causes: every blade hits every monster under it (not one target), so crowds on the ring take full damage each; range cards add blades at a fixed spacing, so damage grows with range on top of coverage; 100% bleed stacks per pass. Ideas: fewer blades or wider spacing, lower damage per pass, a cap on monsters hit per blade per pass, or bleed only on the first pass. Earlier notes: whole saw blades on one ring at the range edge (3 at base range), 75% damage per pass with the Shredder's 100% bleed. A monster under the ring takes about 1.2 passes/s (about 0.9x the Shredder's damage per second); monsters close to the tower are never hit. Range adds blades at a fixed spacing (2.5), so range cards scale it. | `BladestormLogic.DamageFactor`, `BaseRevsPerSecond`, `BladeSpacing`, `BladeDiameter` |
| **Beam Cannon** | 1.3x vanilla DPS for 1.6x mana (measured 257 vs. 227 for the vanilla cannon on a dummy, 3.3 vs. 2.0 mana/s). **Risk:** the beam hits 10 times a second with a base damage of about 1, so flat per-hit bonuses (+X damage per hit, per-hit procs like Static Arc or Hemorrhage) apply about 50x as often as on a vanilla shot. | `BeamCannonLogic.TicksPerSecond`, `DpsFactor`, `ManaFactor` |
| **Dead Man's Land** | Charges pile up across waves without a cap (merging only bounds the number of mines, not their charges), so a camp next to a busy path builds very deep mines over a long run. | a charge cap per mine, merge radius |
| **The Saw Dimension** | Every monster on the map takes Σ(Shredder damage) / 10 per second (× the averaged layer multiplier, with 100% bleed), wherever it is. That scales with the number of monsters and Shredders; with many Shredders late in a run it is a map-wide damage aura. Shredders also level without attacking, as fast as a tower that always has a target. | `SawDimensionLogic.DamageDivisor` (10), `TicksPerSecond` |
| **Ley Line Network** | Reworked after playtest (18 siphons on 4 crystals made 72 mana/s; with the Arcane Spire's 4 corner crystals the Spire became untouchable). Now only siphons next to a crystal ("taps") gather, each rate × (1 + ½ per further crystal in the network), rounded down; other siphons only connect. At most 4 taps per crystal, so with C crystals the cap is about 4C × (C + 1) / 2: 4 crystals with 8 taps = 16 mana/s (vanilla 8). Rounding down means a 2-crystal network gives no bonus (1.5 → 1). Still to check in play: the Arcane Spire with it, and what big late networks unlock (Obelisks, Mana Bombs, the Particle Accelerator's drain). A lucky find, never banned. | `LeyLineLogic.TapIncome` (the ½), carrying fractions instead of rounding down |
| **Mega Mortar** | A fused N×N hits like its N² modules together (base damage × N², multipliers and level averaged) with an N× blast (N² area) and +2 range per extra row. Per shell that is what N² separate mortars deal, but on N² times the area, from further away. It only levels as one tower, and a level-up adds N² damage. Measured: a fresh 2×2 hits for 800 (4 × a module's 200). Mana Bombs cost scales with the multiplied damage. | `MegaMortarLogic.DamageFactor`, `BlastFactor`, `RangePerSide` |
| **Particle Accelerator** | One black hole per 30 s at most (60 mana/s, 1800 mana per charge; slower with less mana), 12x the pooled base damage (all Particle Cannons' damage summed) to every monster within 4 units of the target: 8x over 5 s of ticks plus a 4x burst. A vanilla cannon deals 6x its damage to one target in 30 s, so each capacitor brings 2x its vanilla damage per monster caught, without costing mana. 60 mana/s needs a strong mana economy early on (the user's estimate). Ley Line networks plugged into the core add their income to the drain and charge it faster (the only way past 30 s). Not measured in a real wave yet. | `ParticleAcceleratorLogic.BaseDrain`, `TickFactor`, `BurstFactor`, `CaptureRadius`, `ChargeSeconds` |
| **Ritual of the Sun** | A closed ring gives up its obelisks' single-target beams for one beam with the **average** obelisk damage (not the sum), at the obelisks' rate (6/s), on **every** monster inside, with 100% burn and Focus Fire per monster (up to double). So a ring trades n streams on one target each for one stream on everyone within 0.25 × n − 0.1 of its centre (narrowed after playtest from 0.5 × n to match the beam's look; a powered mana crystal triples it): weaker than its obelisks against a lone monster (a ring of 3 deals a third of their damage to it), stronger in crowds and for burn. Mana is one obelisk's cost per hit. Radius grows without the damage growing, so big rings mostly buy area. Measured: 480 damage in 2 s on a dummy from three level-1 obelisks (8 × 5 × 12 hits). The crystal easter egg's circle (3× radius) makes a ring on a crystal hit 9× the area. Obelisks outside rings only gain +25% burn. | `RitualLogic.RadiusPerObelisk`, `RadiusOffset`, `MaxRadius`, `CrystalCircleFactor`, `BeamBurn`, `ObeliskBurn`, `Average` (average vs. sum) |
| **War Blimp** | **Far too strong** (playtest feedback), even for a Mythical. Continuous gatling (about 3× the biplane's average fire) + bombs + a flying tower. The deck tower's +3 high ground and altitude-extended range are arbitrary. Mythical, so only via Prism/debug for now. | `WarBlimp` constants, `DeckHeightBonus` |

## Treasures and cards

- Starter treasures are M2 placeholders. Review all of them in M9 (see `docs/treasures.md`).
- Siege Workshop treasures (Wall Cannons … Trebuchet Battery) and Druid Grove treasures (Sapling Seeds, Ironbark, Golden Sap) are untested beyond a smoke test.

## Balance framework: first report

From [balance-report.md](balance-report.md) (M7.5). **No balance changes yet:** the treasures are mostly placeholders; the framework is there so the Points of Interest (M8) can be designed somewhat balanced. All constants are uncalibrated until the M8 simulation and the recorded runs of M9. Budgets: Common 12, Uncommon 18, Rare 24, Epic 30, Legendary 36, Mythical 54; ascensions 25% more.

**The model's own doubts (calibrate in M8/M9):**
- **Towers per gold:** with the density calibrated so that Tesla Coil and Mortar match Ballista, Obelisk and Particle Cannon (about 10–23 points per 1000 gold), Flame Thrower, Poison Sprayer, Shredder, Frost Keep and the Radar still come out about 3× higher. That comes from damage over time (the +1 layer multiplier for every tower, burn and poison doubling the damage up to the tick cap), slows, and the plane reaching the whole map, not from density. Encampment (2.5) and Lookout (2–3) come out very low.
- **The safety net** (0.35 weight) and the **mana price** (150 gold per mana/s) are chosen numbers.
- **Ascensions** are proxies ("about x times its tower") until the simulation measures them; they may be 25% above their rarity.

**Leads (likely real, check in play):**
- **Over:** Infinity Engine 88 vs 54 (twice the attack speed is ln 2 = 69 points by definition); Hoarder 39 vs 18 late (+4% per treasure, about 12 treasures by wave 45); Jackpot 42 vs 24 late; First Strike 24 vs 12 early; Trophy Hunter 27 vs 12 (+0.5 chests per mini-boss, about 7 more treasures per run); Monolith on a small board; the treasure cards Lucky Find and Blood Pact against the card budget of 2–6.
- **Under:** Prism of Ascendance 2.9 and Lucky Coin 2.2 (a rarity step is about 4 points per treasure, treasures come about every 4 waves); Quick Study 3.6 (one more card every 6 waves, about 4.5 points each); Thunderclap, Shatter and Permafrost (freezes rarely matter without the others); most economy cards late; Time Dilation 29 vs 54.
- **Mana Bolts** come out slightly negative (−2): the +4 Ballista damage is worth about 5 points, the mana it costs a bit more. Mana generation (Sorcery, siphons) is worth its building price.

## Points of Interest

**Lightning Trap** (docs/poi-plan.md §4): 15.1 points against 15, rated at the best mana rate per wave.
- **The mana is the main cost:** 15 mana/s is about 2,250 gold of mana buildings. So the best rate on the reference boards is low early (1 mana/s up to wave 5) and on boss waves, and the full 15 late.
- **Value by wave** at the best rate: about 20–24 points from wave 4 to 10, about 12 late. It is strongest early and mid game.
- **Check in play:**
  - Does the slider feel useful, or does everyone just set it to 15 or 0?
  - Base regeneration is 2/s, so early high rates run in bursts (out of mana, it waits for a second's worth).
  - On one lane every monster passes through the centre; the model assumes 80% arrive alive (`TrapArrival`).
- **Uncalibrated:** `TrapArrival` 0.8; the best-rate table depends on the reference boards' mana (re-run the probe after mana or board changes).

**Wandering Merchant** (docs/poi-plan.md §5): Normal 15.0, Legendary 15.2, Ascension 14.9 points against 15.
- **Prices** follow the fair price from the framework (`Balance.FairPrice`: the gold whose payment costs as many points as the rarity's budget) within 9% at every rarity and wave; the markups (Normal 75%, Legendary 92%, Ascension 65%) are what brings each merchant to 15 under the assumed purchases.
- **Assumed purchases** (chosen, not measured): Normal and Legendary, the discounted offer and one more; Ascension, one ascension, the discounted one half the time. A player who buys all three at the Normal merchant gets about 19.
- **Paying hurts more than gaining helps:** the board loses towers it relies on, so the fair *price* is about 0.8 of the gold whose *gain* is worth the budget. Selling at 50% of the price therefore returns roughly 40% of a treasure's worth in gold.
- **Check in play:**
  - Early merchants (waves 2–8): a Legendary at about 500–800 gold against an income of 30–170 a wave. Is saving up for it (prices rise with the wave) fun or frustrating?
  - Is selling used? Sell prices differ per merchant (half of *this* merchant's price), so the Legendary Merchant pays more for the same treasure than the Normal one.
  - Ascension Merchant at 65%: is one cheap ascension of your choice too strong compared to finding it in an offer?

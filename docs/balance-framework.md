# Balance framework (proposal)

**Purpose right now:** the treasures are mostly placeholders and get no balance changes yet. The framework exists so that the Points of Interest (M8) can be designed somewhat balanced from the start; the real balance pass is M9.

**Status: steps B0–B3 built (M7.5). §13 says how it is built, what differs from this proposal and what is still uncalibrated. The report is [balance-report.md](balance-report.md). Next: B4 (the simulation) in M8, B5 (recorded boards) in M9.** Decisions are in §11.

The goal is one common currency, **power points**, for everything you can gain in a run:
- base towers and buildings;
- vanilla and custom cards;
- treasures and ascensions;
- castles, and later Points of Interest.

From the points we want:
- **comparability:** is Executioner worth more than Keen Eye? Is a Mortar good value for 200 gold?
- **budgets:** a target number of points per rarity, with outliers flagged automatically;
- **speed:** a new treasure gets a value the moment it is written, not after five playtests;
- **explanations:** *why* something is strong (all its value from AOE, or from a synergy).

---

## 1. The currency: how much tougher can the monsters get?

Every effect in the game ends up in one question: **can you still hold the wave?** Damage, slows, burn, gold, mana, castle health, luck: each matters only because it lets you survive tougher monsters, or the same monsters more safely.

The game has a natural knob for "tougher": the monster upgrade cards that add **% health, % armor, % shield**. So the core measurement is:

> **Toughness headroom (H)** of a board at a stage: the largest factor *k* by which every monster's health, armor and shield can be multiplied while the board still holds the stage's waves (castle health stays above a threshold).

> **Headroom points at a wave = 100 × ln(H with the item / H without it).**

- **1 point ≈ 1% tougher monsters held.** "+10% damage on everything" is about +9.5 points. Halving the monsters' health would be +69.
- **The log makes multiplicative effects add up.** Two +10% damage treasures are worth twice one. Effects that multiply each other (damage × attack speed) simply add their points. Synergies are the explicit excess on top (§8).
- **Every kind of effect lands on the same scale, without hand-made exchange rates:**
  - **Crowd control:** a slow keeps monsters in range longer, so more toughness is held.
  - **Castle health:** more health tolerates more leaks, so a tougher wave can still be survived.
  - **Gold:** gold buys towers and levels, which hold tougher waves.

Headroom points depend on *when* they are measured (§1.2); an item's final points are taken at a good moment for it. H is approximated by formulas (§3–§5). The full simulation (§6, in M8) measures it directly and calibrates the formulas.

### 1.1 Where an item is evaluated: reference boards

An item is never worth points in a vacuum: +1 damage per hit is huge on 3 Ballistas and nothing on 40 Obelisks at level 30. Every item is evaluated as the **marginal change on reference boards**: snapshots of typical runs, every 5 waves (5, 10, …, 50), interpolated in between.

| Checkpoint | Board contents (examples, authored with you) |
|---|---|
| Wave 10 | 6–8 Ballistas, 1 Mortar, a few levels, about 1.5k gold spent, castle 10/10, 4 cards, 1–2 treasures |
| Wave 20 | 15 towers of 3–4 types, first mana towers, monster tier 2 (from wave 16), first boss behind |
| Wave 30 | 25 towers, levels 10–20, mana economy, 3 bosses' worth of monster cards |
| Wave 40 | late board, tier 4 monsters from wave 36 |
| Wave 50 | endgame |

Each board specifies:
- **the towers:** type, count, level, height and placement quality (how much path each one covers);
- **the economy:** cards and treasures held, gold income per wave, mana pool and income;
- **the castle:** its health;
- **the monsters:** the wave of that checkpoint (composition, monster upgrade cards so far).

Each board comes in a few **variants** that a player would steer into: single-target heavy, area heavy, damage-over-time heavy, mana heavy, a Cannon line along a straight. Situational items are judged on the variant that suits them (§1.3).

The boards are **authored** for now (from the game's numbers and your experience of typical runs). A run recorder replaces them with medians of real runs in M9, for the final touches (§9).

### 1.2 Timing: points at a good moment

Headroom points alone change a lot with the moment:
- **Flat effects shrink.** +1 damage per hit is +10% on a wave-5 Ballista board (about 10 points) and nearly nothing at wave 45.
- **Relative effects stay.** +10% damage is about 9.5 points at any wave.
- **Scaling effects grow.** Hoarder (+4% per treasure) and Hivemind (+1 per 3 towers) are small early and large late.
- **Economy compounds.** Gold early becomes towers and levels that keep paying; gold late buys little.

Judging all of them at one fixed wave favours one kind and punishes the others. Judging each at its *perfect* moment overrates niche peaks (+1 damage at wave 1). So every item is valued **as if it is acquired at a good moment for it: the top 20% of its timing, not the very best.**

1. **Value curve.** V(a) is the item's worth if acquired at wave *a*: the average of its headroom points over the next **10 waves**, measured on those waves' boards with the item on them since *a*.
   - Gold and levels gained since *a* are counted as spent at each of those waves.
   - The 10-wave horizon is the stretch a pick has to carry you through. It's tunable; one horizon for everything keeps the curves comparable.
2. **Offer window.** The waves in which the item can actually be offered. That's 1–50 by default; later where an unlock comes first, for example an ascension's tower, or a card behind an earlier card.
3. **Points = the 80th percentile of V(a) over the offer window.** For a curve that falls (flat gold, flat damage, heals) that's its value at the start of the window plus a fifth of it. For one that rises (scaling), near the end. For a flat curve (% effects), its constant value.

This needs no separate formula for "flat" and "scaling" parts: an item is a sum of atoms (§2), its curve is the sum of their curves, and the percentile takes the good moment of the whole. The report still shows the two ends (**early**: V at the window's start, **late**: V at wave 40) and the **peak wave**, so you can see what kind of item it is.

**A peak is itself a condition.** An item that is only good early, or only good late, delivers its value in fewer runs and fewer moments than one that is good all run. So the timing curve also gives a **timing breadth**, which goes into the same penalty as situational effects (§1.3):

> **Timing breadth b_t** = the average over the offer window of min(1, V(a) / P80), where P80 is the 80th percentile from step 3.

| Kind of item | b_t | Factor b_t^0.3 |
|---|---|---|
| % effects: about the same value all run | ≈ 1.0 | 1.00 |
| Flat gold now: falls to about a third of its value by mid-run | ≈ 0.6 | 0.86 |
| Only good in the first 10 waves | ≈ 0.25 | 0.66 |

As a result, for the same points, a peaky item may hit harder at its moment than an all-round item.

Cost curves in the classic sense (Schreiber, §12) put every cost and benefit into one currency; this is the same idea with time as part of the currency: the currency is "headroom over the next 10 waves".

### 1.3 Requirements and situational effects

Two different things look like "conditions":

- **Requirements** decide whether an item can be offered at all: an ascension needs its tower built, a castle treasure its castle, a card its earlier card. **No penalty:** the item is judged as if the requirement is met, since the offer already guarantees it.
- **Situational effects** work only in some situations: for some towers, in some placements, with some builds. They are judged in their situation, then pay a **situational penalty** for how narrow it is:

> **Points = conditional value × (b × b_t)^0.3**

- **Conditional value:** the value on the board variant that suits the item (§1.1), at its good moment (§1.2). A good situation, not the perfect one.
- **Timing breadth b_t:** from the value curve (§1.2); it is 1 for items that are good all run.
- **Breadth b:** the share of typical play in which the item delivers that value, between 0 and 1. It is measured from the plain reference boards where possible: the share of the board's power the effect can act on, times the share of placements that qualify. Otherwise it is set by hand with a reason.
- **The exponent 0.3:** it softens the penalty because players choose situational items when they fit and build around them. The penalty is never a cost, only a smaller benefit. In Schreiber's words: "a limited or restricted benefit is never a cost".

| Effect | Breadth b | Factor b^0.3 |
|---|---|---|
| +% damage for all towers | 1.0 | 1.00 |
| Bigger area of effect (blast radius, pulse range) | ≈ 0.55 (share of damage from area attacks) | 0.84 |
| Stronger damage over time (cap, pop on crit) | ≈ 0.35 | 0.73 |
| Freeze-dependent (Shatter) | ≈ 0.2 | 0.62 |
| Faster projectiles (worth range only for free-flying shots, like the Cannon's, in good positions) | ≈ 0.08 | 0.47 |

**Not situational:** effects that act on a *part of every fight* go into the formula as uptime instead (§4.1). Executioner works on every monster, on its last 20% of health. A crit proc works on every hit, by chance. Drawbacks count in full unless they are explicitly situational themselves.

## 2. Everything that exists in the game: the effect atoms

Every card, treasure, tower and building is described as a list of **atoms**: small, typed effects the model knows how to value. This list is the "thorough" part. If something can't be expressed with atoms, the framework is missing an atom; the list grows instead of getting a fudge factor.

| Group | Atoms (with scope: all towers, a tower type, towers near X, the castle) |
|---|---|
| **Hit** | flat base damage; % damage; layer multipliers (+health / +armor / +shield damage); attack speed; range; height bonus; crit chance (with the ×2/×3/×4 tiers); stun or freeze chance and duration |
| **Area** | blast radius; pierce (Cannon, Springald); line (Flame Thrower, Icicle Cannon); chain (Chain Lightning, Static Arc); ring or pulse (Tesla, Vampire Lair, Bladestorm); global (The Saw Dimension, Retribution); bounces and ricochets; extra projectiles (Repeater, Fragmentation, Hwacha) |
| **Damage over time** | bleed / burn / poison application %; the tick caps (max stacks consumed per second); "+1 layer multiplier while afflicted" (vanilla rule and Trail of Blood / Fire and Flames / Vile Consumption); pops on crit (Ignite, Exsanguinate, Expunge); spread (Contagion) |
| **Conditional damage** | ×N while a condition holds: target below 20% (Executioner), frozen (Shatter, Ice Breaker), all three DOTs (Triple Threat), first hit (First Strike), hexed (Hex of Frailty), mana above 90% (Arcane Overflow), marked (Lookout) |
| **Procs and on-death** | chance-on-hit extra hits (Static Arc, Ricochet Bolts, Hemorrhage, Witch's Brew); on-kill (Overkill, Volatile Corpses, Domino Effect) |
| **Crowd control** | slow % per damage, slow cap, slow decay; freeze; global slows (Time Dilation, Moat); holding and displacing (black hole, knock-back); enemy haste, haste cap; fortify |
| **Scaling with the board** | per tower owned (Hivemind, Siege Engineer, Tithe Box); per treasure (Hoarder); per fewer towers (Monolith); per neighbour (Legion, Lightning Rod); per kill this wave (Bloodthirst) |
| **Growth** | XP rate; starting level (Prodigy); extra levels (Veteran Training); level cost |
| **Economy** | gold now; gold per wave; per kill, with the source tags (Kill, Income, Tree, Other) and their multipliers; interest (Piggy Bank); debt (Loan Shark); tower price (Legion, the price step); refunds; chests and treasure drops (Woodcutter's Luck, Trophy Hunter, Golden Goblin) |
| **Mana** | max mana; regeneration %/s; per kill; per second (siphons, banks, Ley networks); mana cost per shot (−% cost, Infinity Engine) |
| **Castle** | max health; current health (heal now, heal chance per wave); absorb per wave (Bulwark); last stand (Second Wind); damage taken per leak (Destruction, Banditry); damage on being hit (Retribution) |
| **Meta** | card draw frequency (Quick Study); offer size; rarity rolls (Lucky Coin, Prism of Ascendance); luck rolls (Four-Leaf Clover); unlocks |
| **Monster changes** | % health / armor / shield; speed; haste cap; slow cap (Perseverance); fortify (Protection); gold drop (Frugal, Gold Rush); damage dealt (Destruction) |
| **Costs** | max health paid (content rules §1); attack speed or damage lost (Supercapacitor, Legion); price increases; mana drains; placement needs (ore, graves, crystals, shrines, a closed ring) |

Monster-change atoms come up twice:
- **as costs:** Banditry, Gold Rush and Glass Cannon all have a downside;
- **as the yardstick:** a "+5% monster health" card is worth exactly −5 points (−4.9, from ln 1.05). That gives a built-in sanity check, since the vanilla monster cards should come out at the values they state.

**Vanilla cards get their atoms automatically.** `TowerUpgradeCard`, `DOTUpgradeCard`, `MonsterUpgradeCard` and the others are data classes: their serialized fields *are* the effect. A small translator turns each class into atoms, so the ~250 vanilla cards need no hand-written specs. Custom cards and treasures declare theirs next to their definition (§7).

---

## 3. Damage a tower delivers: the core formula

For a tower *t* against the reference wave:

```
Delivered(t) = Hit × Layer × Crit × Rate × Reliability × Targets × Time + DOT
```

| Factor | Meaning | Comes from |
|---|---|---|
| **Hit** | base damage per hit after fortify, plus mark and flat bonuses | base, height, levels, cards (vanilla formula, already ported in `SimDamage`) |
| **Layer** | the layer multiplier, averaged over the wave's effective HP: shares of health, armor and shield, each × that layer's multiplier, +1 while the matching DOT is up | tower stats, the wave's layer mix (§5), DOT uptime (§4.3) |
| **Crit** | expected multiplier: 1 + p₂ + 2p₃ + 3p₄ (each tier capped at 50%) | crit chance, luck rolls |
| **Rate** | hits per second | attack speed, buffs, mana availability |
| **Reliability** | share of hits that count (§4.1) | projectile type, overkill, resources |
| **Targets** | monsters hit per attack (§4.2) | the area atoms, monster density |
| **Time** | seconds per wave the tower has something to hit | path coverage within range, monster speed after crowd control (§4.4) |

A board **holds** a wave if the delivered damage covers the wave's total effective HP. This is checked in order along the path: monsters meet the first towers first, and what survives the whole path leaks. Leaks cost castle health (§4.6). Toughness headroom *H* is the *k* at which the castle just survives.

This is deliberately the vanilla damage formula plus four multipliers. The vanilla formula is already ported (`tests/TreasureRun.Tests/Sim/SimDamage.cs`). The new part is how to get Reliability, Targets and Time without simulating every frame.

---

## 4. The hard parts

### 4.1 Reliability

```
Reliability = P(hit lands) × (1 − overkill waste) × P(resource available) × condition uptime
```

**P(hit lands):**
- 1 for instant and homing attacks (beams, homing bolts, Seeker Rounds);
- for unguided shots it depends on travel time and target speed. Mortar shells use a predicted landing point and a blast radius, so they miss when monsters change speed or turn a corner.
- Measured once per attack type in the simulation, as a function of monster speed; tabulated.

**Overkill waste:**
- A hit bigger than what is left of a monster wastes the rest. The killing hit overshoots by about half a hit on average, so for hits of size *h* against monsters of effective HP *E* (with *h* < *E*) the share wasted is **≈ h / (2E)**. With *h* ≥ *E* every hit kills, and the waste is 1 − E/h.
- Big, slow hits (Mega Mortar, black hole bursts) lose more against small monsters; that's why they need it in the numbers.
- Overkill and Domino Effect turn this waste back into value.

**P(resource available):**
- Mana towers skip shots without mana: P = min(1, mana supply / mana demand) on the board.
- This links mana atoms to damage: +1 mana/s is worth exactly the damage of the shots it pays for, and nothing on a board without mana towers.

**Condition uptime:** the share of the tower's damage dealt while a condition holds. This is for conditions that come up in every fight; effects that only work in some builds or placements are situational instead (§1.3).

| Condition | Uptime |
|---|---|
| Target below 20% (Executioner) | ≈ 20% of each monster's *health layer* |
| Frozen (Shatter) | freeze chance × freeze duration × hit rate on the frozen target |
| First hit (First Strike) | 1 / hits per kill |
| All three DOTs (Triple Threat) | product of the three DOT uptimes |
| Mana above 90% (Arcane Overflow) | from the board's mana surplus |

### 4.2 Area: expected hits per attack

The key quantity is **monster density on the path, ρ** (monsters per tile of path). Every area attack hits **1 + ρ × (path length its area covers)** monsters, or ρ × L for attacks that don't need a target. Per shape:

| Shape | Path length covered (L) | Notes |
|---|---|---|
| Blast of radius r at the target | ≈ 2r on a straight path; up to πr at a bend | Mortar, Hwacha rockets, Magma |
| Pierce or line of length ℓ | ℓ × cos(angle between line and path) | Cannon, Flame Thrower, Icicle Cannon; Firing Line aims along the path |
| Pulse round the tower, radius R | C(R): path length within R of a well-placed tower (map statistic) | Tesla, Vampire Lair, Frost Keep cells, Encampment cells |
| Chain of n jumps, reach d | min(n, ρ × 2d × n) | Chain Lightning, Static Arc; chains stop when density is low |
| Global | total path length on the map | The Saw Dimension, Retribution |

**Density:** with N monsters spawned over T seconds at speed v, ρ₀ = N / (v × T).
- **Slows and freezes raise density:** ρ = ρ₀ / (1 − slow̄). This is how crowd control and area damage reinforce each other, without a hand-made synergy rule.
- **Bunching:** the game spawns in groups, so ρ is not uniform; the simulation provides a bunching factor per wave size.

**C(R), the path within reach**, is a property of real maps.
- **How:** sample it from generated maps, either from SimPath with the game's tile shapes, or from a dev command that dumps the path of real runs.
- **What to store:** a table of "path length within R of the best 10 / 50 / 90% of placements".
- **Why per placement:** placement quality is part of a board.

### 4.3 Delayed damage: bleed, burn, poison

Per afflicted monster:
- **inflow:** stacks added per second = damage dealt × DOT %;
- **outflow:** at most the tick cap per second (24, raised by cards) goes into the matching layer, with burn into armor, poison into shield and bleed into health. Per the vanilla rules (`vanilla-towers.md`) the stacks also act on the layers.

A DOT is worth three things:

1. **Its own damage:**
   - It is capped by the tick rate × the time the monster lives with stacks. More inflow than the cap is wasted, which is exactly why cap cards (Everything Burns, Kill them with Fire) have value.
   - It is also cut by the **delay**: stacks left when the monster dies (killed by others) or leaks are lost.
   - Share delivered ≈ min(1, cap × remaining life / stacks) per monster. The remaining life comes from the board's kill speed.
2. **The +1 layer multiplier while afflicted:**
   - It applies to every hit from every tower, so on a board with health multipliers around 10 it's worth about +10% on that layer.
   - It is often the bigger value of a DOT. Trail of Blood / Fire and Flames / Vile Consumption add to it.
   - Uptime ≈ 1 when DOT is applied steadily; otherwise the share of a monster's life it is afflicted.
3. **Enabling conditions:** Triple Threat, Witch's Brew, Alchemist's Furnace, Slow Cooker, Contagion.

Regeneration matters for the same reason: an afflicted layer can't regenerate, so DOT is also worth the regeneration it blocks (monster regen × time).

### 4.4 Crowd control: time in range

A monster at speed v crossing a coverage of path length C stays **C / v_eff** seconds. Everything that changes speed changes Time for every tower it covers:

```
v_eff = (base speed + speed bonuses) × (1 − slow̄ + haste̅) × (1 − freeze share)
```

- **Slow:** each hit adds damage × slow% / 100, capped at 60% (+ Absolute Zero, − Perseverance), decaying 5 points per second. The average slow is an equilibrium: inflow from the board's slowing hits against decay. That gives exact numbers for Frost Keep, Creeping Cough, Moat and Frost Nova.
- **Freeze:** the share of time frozen = Σ (freeze chance × hits per second × duration, shortened 20% per repeat unless Permafrost).
- **Global slows (Time Dilation):** they apply to the whole path, so they scale every tower's Time and the density ρ, and give more time before leaks.
- **Holding (black hole)** is a freeze of length 5 s on everything caught, with monsters pulled together (density). **Knock-back** adds path length (the monster walks back).
- **Haste and battle cries** (Spawn, Near End, Armor/Shield Break, Death, Periodic) are negative CC on the monster side; the wave model includes them.

Crowd control has a second value: it **reduces leaks** (§4.6) by keeping monsters in the killing zone. That is counted automatically, because the headroom test is "does the castle survive".

### 4.5 Economy: gold and growth

- **Gold → points:**
  - The rate is the board's **marginal value of gold**: the best points-per-gold purchase available at that checkpoint (a new tower, a bought level, the next copy at its stepped price), from a simple greedy purchase policy.
  - It falls over a run as prices step up, which makes early gold worth more than late gold, as it should.
- **Income over time:**
  - A treasure that pays per wave is worth the gold it has paid by each checkpoint, converted at that checkpoint's rate.
  - Interest (Piggy Bank) and debt (Loan Shark) follow from the gold balance over time.
  - Kill gold depends on kill counts; the source tags (Kill, Income, Tree, Other) and castle multipliers (Gilded Vault, Midas Touch) apply per source, as `GoldEconomy` does in game.
- **XP and levels:**
  - A level is +1 base damage and +1 to one layer multiplier.
  - XP comes from time with a target: 1 / (2 × range) + 1/2 per second.
  - So XP atoms (Prodigy, Veteran Training) become levels by checkpoint, and levels become damage.
  - Bought levels connect gold and growth: a level costs (10 × level − XP) × the tower's level-cost multiplier.

### 4.6 Survival: castle health and leaks

- Monsters that get through deal their castle damage: 1 by default, more for bosses, plus Destruction and Banditry.
- **Max health:** the board holds a wave as long as leaks stay below the health left. So more max health → more leaks tolerated → a higher headroom *k*. The exchange rate falls out of the leak curve: how fast leaks grow as *k* rises near the limit, measured on each board.
- **Two ways to lose, both measured:**
  - **attrition:** small leaks every wave. Heals per wave, Bulwark and Golden Sap shine here.
  - **one lethal wave:** a boss or a big jump. Max health, Second Wind and Retribution shine here.
- **Reported apart:** the *survival* points of an item are reported apart from its *offense* points, so "this Common makes you safer" isn't hidden in one number.

### 4.7 Meta: draws, rarity and luck

These are valued from the points of the pool itself:
- **An extra card draw** is worth E[max points of the 3 cards offered], from the card pool's values at that stage.
- **A better rarity roll** (Lucky Coin, Prism of Ascendance) is worth the difference in expected treasure points between the two rarity distributions.
- **A chest** is worth one treasure offer.

Because their value depends on everything else, meta items are computed last, by a few rounds of fixed-point iteration (values → pool expectations → meta values).

---

## 5. The monster model (data to extract first)

The model needs the real monster data, which isn't documented yet:

| Data | Where it comes from |
|---|---|
| Monster roster per tier (1–4): health, armor, shield, speed, regeneration, immunities, battle cries, castle damage, gold | prefabs in the AssetRipper export (`Ripped/ExportedProject/Assets/GameObject`), `Enemy` fields |
| Wave composition | `SpawnManager.Spawn(level²)`: a point budget of level², spent on tiers costing 1–4 (tier 2 from wave 16, tier 3 from 26, tier 4 from 36); spawn timing |
| Bosses and mini-bosses | waves 15/25/35/45, mini-bosses on odd waves after 16 |
| Monster upgrade cards over a run | which monster cards are taken when (health / armor / shield %, speed, haste, fortify, slow cap) |
| Endless scaling | `MonsterManager.EndlessBuff` |

Output:
- **`docs/reference/vanilla-monsters.md`:** the extracted data, in the format of `vanilla-towers.md`;
- **`Balance/Waves`:** a wave generator in pure logic, the same way `SimDamage` ports the damage formula.

The **layer mix** (how much of a wave's effective HP is shield, armor, health) comes from this. It is what values "+2 shield damage" correctly, since that's worthless early and decisive late.

---

## 6. Measurement: formulas now, simulation in M8

| Layer | What it does | Speed | When | Where |
|---|---|---|---|---|
| **Formulas** (§3–§4) | points from atoms on reference boards | instant, every item, every build | before M8 | `src/TreasureRun/Balance/*Logic.cs`, linked into the tests like the ascension logic |
| **Simulation** | headroom by running the waves in `SimWorld` (towers, monsters, effects, leaks), binary search on *k* | seconds per item | M8 | `tests/TreasureRun.Tests/Sim`, extended |
| **In game** | scenarios that measure real numbers (DPS, hits per attack, slow uptime, leaks) | a launch | before M8, more in M8 | `scenario` runner, as now |

**Before M8** the formulas run on their own:
- Constants that the simulation would fit (bunching, miss rates, overkill) start as hand estimates and are marked *uncalibrated* in the report.
- A few in-game measurement scenarios check the most important ones (DPS of each tower type, hits per attack of the area towers), like the existing `ballista-dps`.
- Items the formulas can't capture (chains, the black hole, The Saw Dimension, Ritual of the Sun rings) get a hand estimate marked *estimated*.

**In M8, the full simulation:**
- **The calibration loop:**
  - For each atom type, the formula's prediction is compared with the simulation on benchmark boards: does AOE match measured hits per attack? does slow match measured time in range?
  - The constants are fitted, and the remaining error is reported.
- **Items where formula and simulation disagree** by more than ~15% are marked "simulate"; their points then come from the simulation.
- **Additions to `SimWorld`.** It already has path, enemies with layers, DOT, slow, freeze, fortify and mark. To add:
  - the vanilla tower behaviours not modelled yet, with travel, splash and pierce;
  - waves and spawn timing;
  - leaks and castle health;
  - the economy loop (income, purchases);
  - hooks for treasure effects.

  It can grow tower by tower, alongside the POIs.
- **The synergy table** (§8) needs the simulation, so it comes with it.

Every number in the report says where it comes from: *formula*, *estimated*, *simulated* or *measured in game*.

## 7. How content declares its atoms

Every treasure and custom card has a spec, one line each, in `src/TreasureRun/Balance/Specs/ContentSpecs.cs`, keyed by its id. They live in one pure file rather than next to each definition, so the offline tests can rate them in about a second without the game (§13):

```csharp
["executioner"] = () => S.Conditional(2, Condition.BelowHealth20),

// Glass Cannon: offense and its cost in one line each
["glass_cannon"] = () => S.DamagePercent(0.5).MaxHealthPercent(-0.5),

// Tithe Box: scales with the board
["tithe_box"] = () => S.GoldPerWavePerTower(5),

// a situational treasure: judged in its situation, then the penalty for its narrow breadth
["shatter"] = () => S.Conditional(3, Condition.Frozen).GivenFreeze().Situational(BoardVariant.Mixed, 0.2, "needs a freeze source"),

// an ascension the formulas can't capture: a proxy ("about x times its tower"), marked for the M8 simulation
["bladestorm"] = () => S.DamagePercent(1.5, Shredder).Requires(Shredder).Because("blades circling the Shredder (proxy)").Simulated("bladestorm"),

// or a hand estimate
["second_wind"] = () => S.Estimated(14, "one extra life once per run"),
```

- **Vanilla cards:** translated from their card classes automatically (§2).
- **Towers:** their prefab stats (`vanilla-towers.md`) plus a behaviour tag (single target, blast, pierce, line, pulse, dropper, mark, plane).
- **Coverage is enforced** by the offline test `EveryTreasureHasASpec` (against `docs/treasures.md`) and the in-game scenario `balance` (every treasure, custom card and vanilla card of the running game): every offerable item has a spec, or an explicit `Unrated("reason")`.
- **Requirements** (`Requires`) and **situations** (`Situational`) are declared separately, so the penalty only ever applies to the second (§1.3).

---

## 8. Output: the balance report

The dev command `balance report` writes the report (`console/balance.md` and `.csv`, copied to [balance-report.md](balance-report.md) and `balance-report.csv`). `dotnet test` also writes an offline preview without the vanilla cards (`tests/out/balance-preview.md`). Per item:

| Column | Meaning |
|---|---|
| Points | the final value: 80th percentile of the value curve (§1.2) × (b × b_t)^0.3 (§1.3) |
| Early / late / peak | V at the start of the offer window, V at wave 40, the wave where V is highest, and the timing breadth b_t |
| Situation | the board variant it was judged on, its breadth b and factor b^0.3 (empty when not situational) |
| Split | offense / control / economy / survival / meta |
| Spread | the standard deviation (Golden Goblin, Woodcutter's Luck and crits are lumpy); it needs the simulation, so it comes in M8 |
| Budget | vs. its rarity's target, flagged above +25% or below −25% |
| Source | formula / estimated / simulated / measured in game |

**Rarity budgets** (decided, raised by 50% after the first report): a gentle climb, with Mythical 50% above Legendary.

| Kind | Target points |
|---|---|
| Cards (tame) | 2–6 |
| Common | 12 |
| Uncommon | 18 |
| Rare | 24 |
| Epic | 30 |
| Legendary | 36 |
| Mythical | 54 |

Ascensions are judged against their rarity with their tower's requirement met, but may be **25% stronger** (target = budget × 1.25): they will be rolled rarer within their rarity (a slot first rolls the rarity, then whether it is an ascension).

Also in the report:
- **Base towers:** points per 100 gold at their unlock checkpoint, so prices can be compared. Points per mana as well, for mana towers.
- **Synergy table** (from M8, needs the simulation): V(A + B) − V(A) − V(B) for the pairs with the largest excess. It catches combos before players do (Hex of Frailty with Lookouts, Triple Threat with Master of Elements, Bladestorm with range cards).
- **Sanity checks that must pass:**
  - monster cards come out at their stated % (§2);
  - two copies of a +x% card are worth twice one;
  - removing all towers gives −∞;
  - a flat effect's curve falls and a scaling effect's rises.

**In game (dev only):**
- a console command, `balance <id>`, prints an item's points, curve and breakdown;
- optionally, a debug overlay shows each offered card's points. That makes playtest feedback ("I always pick X over Y") easy to compare with the numbers.

## 9. Reference boards: authored now, recorded in M9

The reference boards (§1.1) are **authored** for the first version: the checkpoints and variants, written as data (towers, levels, heights, placement quality, cards, treasures, gold and mana, castle health), set up with you.

In M9, once everything is implemented, a **run recorder** (dev setting, Treasure Run only) writes a snapshot at the end of every wave:
- the towers: type, level, height, position;
- what was picked: cards and treasures;
- gold and mana;
- the castle's health;
- leaks, kills, and damage per tower type (`DamageTracker` already has it).

Then:
- **median recorded runs** replace the authored boards, for the final touches;
- **the breadth of situational items** is measured from what real runs contain (§1.3);
- **the whole model is checked:** predicted headroom against what actually happened (leaks per wave) in your runs.

## 10. Order of work

| Step | Content | When |
|---|---|---|
| **B0 Data** | monster roster and waves (`vanilla-monsters.md`, wave generator in pure logic), map path statistics C(R) from the game's tile shapes | before M8 |
| **B1 Formulas** | atoms, evaluator, value curves and the 80th percentile, situational factor, authored reference boards and variants, report; base towers and vanilla cards (automatic translation); sanity checks | before M8 |
| **B2 Specs** | specs for all treasures and ascensions (mostly one line each), hand estimates where formulas can't reach; coverage test | before M8 |
| **B3 In game** | `balance <id>` command, offer overlay, measurement scenarios for the key constants | before M8 |
| **B4 Simulation** | `SimWorld` towers, waves, leaks, economy; headroom by binary search; calibration of the formula constants; synergy table | **M8**, alongside the POIs |
| **B5 Recordings** | run recorder, recorded boards replacing the authored ones, measured breadths | **M9**, final touches |

B0–B3 give a usable report before M8, with formulas and hand estimates. B4 makes it trustworthy for the complex items. M9's balance pass then works from the report: fix the flagged items, tune the constants listed in `balance-todo.md`, re-run.

## 11. Decisions

**Decided (after review):**
1. **Unit:** 1 point ≈ 1% tougher monsters held (headroom points).
2. **Timing:** each item is valued at a good moment for it: the 80th percentile of its value curve over its offer window (§1.2).
3. **Rarity budgets:** cards 2–6, Common 12, Uncommon 18, Rare 24, Epic 30, Legendary 36, Mythical 54 (raised by 50% after the first report); ascensions may be 25% above their rarity.
4. **Requirements vs situations:** requirements (an ascension's tower) cost nothing. Situational effects are judged in their situation with the penalty b^0.3 (§1.3).
5. **Timing is a condition too:** a pronounced peak (only good early, or only good late) is penalised through the timing breadth b_t, in the same factor (b × b_t)^0.3 (§1.2).
6. **Reference boards:** authored now, recorded runs in M9.
7. **Simulation:** the full simulation is part of M8.

**Still open (defaults proposed):**
1. The horizon of the value curve: 10 waves.
2. The penalty exponent: 0.3 (b = 0.55 → −16%, b = 0.08 → −53%).
3. The outlier flag: ±25% of the budget.
4. A budget for castles and Points of Interest. **Decided for POIs:** 15 points (`Pools.PoiBudget`, between a Common and an Uncommon treasure); a POI with a risk may pay more when it works out. Castles: still open.

## 12. Inspirations

- **Cost curves** (Ian Schreiber, *Game Balance Concepts*, level 3: [Transitive Mechanics and Cost Curves](https://gamebalanceconcepts.wordpress.com/2010/07/21/level-3-transitive-mechanics-and-cost-curves/); and the book *Game Balance* with Brenda Romero, which has chapters on cost curves and situational balance):
  - every cost and benefit, limitations included, goes into one currency;
  - simple objects are priced first, then the unknown terms solved one by one;
  - a restricted benefit is a smaller benefit, never a cost.

  The atoms (§2) and the situational factor (§1.3) follow this.
- **Draft card ratings** ([17Lands' "games in hand win rate"](https://mtgazone.com/17lands-in-defense-of-the-data/)): cards are rated in the decks that actually play them, not averaged over decks that never would. That's the reason situational items are judged on the variant that suits them.
- **Roguelike item design** (developer and player discussions, e.g. [scaling items](https://itch.io/post/12954599) and [relic evaluation](https://www.gamenguide.com/articles/108599/20260720/slay-spire-relic-guide-how-evaluate-relics-avoid-traps-that-hurt-your-deck)):
  - items that scale without limit eventually become the best thing to have, so they need caps or diminishing returns;
  - relics are judged on baseline power, consistency, synergy and cost.

  The report's late value and spread columns are there to catch exactly that.

No source found a formal method for the timing problem (flat vs scaling value). The value curve with its 80th percentile (§1.2) is this framework's own answer.

## 13. Implementation (M7.5)

**Where things are:**

| Part | File | Notes |
|---|---|---|
| Monster data (B0) | `tools/balance/extract_monsters.py` → [reference/vanilla-monsters.md](reference/vanilla-monsters.md) and `Balance/MonsterRoster.cs` (generated) | every monster prefab's stats and battle cries; the tier lists and bosses from `GameScene` |
| Wave model (B0) | `Balance/WaveModel.cs` | `SpawnManager`'s budget, tiers, mini-bosses and bosses as expected values (48 seeded draws, every tier class equally often), with death and break spawns, periodic cries and heals |
| Path statistics (B0) | `PathStats` in `Balance/ReferenceBoards.cs` | path length per wave; path within range R, a table **measured** on three real maps (the scenario `balance-area` prints it every run; `balance pathstats` measures the current map) |
| Towers | `Balance/TowerData.cs` | the vanilla stats plus an attack shape (single, blast, pulse, line, pierce, dropper, mines, saw, sword, mark, plane) |
| Reference boards (B1) | `Balance/ReferenceBoards.cs` | 7 variants (Mixed, SingleTarget, Area, DamageOverTime, Mana, CannonLine, Tall). Tower mixes are set at waves 1/10/20/30/40 and interpolated. The gold invested follows the wave model's income (kills, tower types, tree chops). Mana buildings cover 90% of the board's mana demand |
| Evaluator (B1) | `Balance/Evaluator.cs` | §3–§4: per tower group, hits × damage per hit (harmonic over the wave's layer mix) × crit × reliability (accuracy, overkill, mana) × targets (density) × active time. Also damage over time with tick caps and pops, slow and freeze equilibria, leaks, and a separate boss check (waves 15/25/35/45) combined by a soft minimum |
| Atoms and specs (B1–B2) | `Balance/Spec.cs`, `Balance/Specs/ContentSpecs.cs`, `Balance/Specs/VanillaCards.cs` | about 70 atom kinds; vanilla cards are translated from their serialized fields, read in game by reflection |
| Valuation (B1) | `Balance/Valuation.cs` | value curve over the offer window (horizon 10 waves), 80th percentile, timing breadth, situational breadth, split by category |
| Report (B1) | `Balance/Report.cs` | markdown and CSV; the console detail per item |
| In game (B3) | `Balance/Game/BalanceGame.cs`, `BalanceOverlay.cs`, `Debugging/BalanceScenarios.cs` | `balance <id or title>`, `balance report`, `balance coverage`, `balance pathstats`; the offer overlay (setting `Debug.BalanceOverlay`: points in the title of offered treasures, `+` over budget and `-` under, and under the text of level-up cards); scenarios `balance` (coverage, sanity) and `balance-area` (Tesla coverage and Mortar hits on dummies, path statistics) |
| Tests | `tests/TreasureRun.Tests/BalanceTests.cs` | the sanity checks of §8, coverage, vanilla translation, the offline preview |

Everything under `Balance/` except `Balance/Game` is pure and compiled into the offline tests. They rate all treasures and custom cards in about a second; the full report in game takes about ten.

**Deviations from the proposal:**
- Specs live in one pure file keyed by id (§7), not on `TreasureDef`, so the tests can rate them.
- The report is produced in game (it needs the vanilla cards' fields) and copied to `docs/`; the offline tests write a preview.
- **Normalisation:** every reference board is scaled so that it just holds its own wave (stream headroom 1.0 before leaks, boss 1.1). Only ratios enter the points, so the model's unknown absolute level cancels. What stays uncalibrated is the *shape* of each effect.
- **Mana is a market** (revised after review): mana is worth what the buildings that make it cost, **150 gold per mana per second** (`Valuation.ManaPrice`). A Mana Siphon is 100 but needs a crystal; a Mana Bank is 500 for 1 mana/s plus 20 max mana, which with Savings Account and Sorcery makes about 4.6 mana/s, so about 110; without those cards 500. Vanilla mana towers are hungry: an Obelisk pays `(int)(damage × 0.25)` mana per hit at 6 hits a second, 12–30 mana/s.
  - Reference boards cover 90% of their mana demand and pay for it from their tower gold.
  - An item's change in mana demand or supply is settled in mana buildings: Mana Bolts buys mana for the Ballistas, Sorcery or a Ley Line Network saves buildings. Gold an item brings buys towers together with the mana they need.
  - Result: +1 mana/s is worth about as much as 150 gold, and Mana Bolts I comes out slightly negative (its +4 Ballista damage alone is worth about 5 points; the mana costs a bit more). Mana Bolts really is weak, it isn't a model artefact.
- **Range** is a strong stat in the model (longer lines, bigger pulses, a longer kill zone for damage over time), which matches play. Long waves keep many towers firing all the time even with little range; the model's active time (the stream length plus the time a monster needs to cross the tower's reach) already gives range less weight late.
- **Requirements set the offer window:** an item that requires a tower is offered from the first wave a reference board has that tower (wave 5 at the earliest). If the board lacks the tower, 12% of its tower gold moves to it. Unlock cards are options, never negative.
- **Pools (§4.7)** use fixed values for now: a card pick is 4.5 points, a treasure pick the rarity weights × budgets × 1.15. The fixed-point iteration over the real pool comes with M8.
- The **Spread** column needs the simulation (M8).

- **Crowd density, calibrated to the spawner and vanilla prices:** the spawner releases one monster per spawn point every 0.5 s, single file, about one per cell at speed 2 before slows, so there is no extra bunching. The share of them alive inside a tower's reach (0.4) is set so that the plain area towers (Tesla Coil, Mortar) come out even with the single-target ones (Ballista, Obelisk, Particle Cannon) per gold. The probe `BalanceProbe.TowerBalanceByDensity` (explicit test, writes `tests/out/tower-balance.txt`) shows the balance at several densities.
- **The castle's safety net** (chosen, not derived): late in a run castle health barely changes the headroom, but it saves runs from one bad wave. So it adds `0.35 × 100 × ln(safety with / without)` points, where safety = max health + 10 waves of heals and absorbs, divided by the leak damage (extra leak damage counted at half weight), times a safety factor (Second Wind ×1.5). +5% max health is about +1.7 points, halving it (Glass Cannon) about −24.

- **Points of Interest** (M8, docs/poi-plan.md): rated like treasures against a budget of 15 (`ContentSpecs.Pois`). A POI that deals damage is a **fixed source** (`Spec.FixedSource`, `TowerGroup.Custom`): never bought or sold, unreachable for tower cards and treasures. The attack shape **Trap** works per monster: each monster walking through takes `rate × cells / speed` strikes, `Evaluator.TrapArrival` (0.8, uncalibrated) of them arrive alive, overkill is wasted per strike as for towers. Its mana is paid in mana buildings like any other demand.

**Constants that are hand estimates (uncalibrated, to fit in M8):**
- the alive share (above), the safety weight 0.35 and leak damage weight 0.5, the mana price 150;
- leak share (8% of the castle per wave), boss attention 0.7, sword survival 0.6;
- accuracies: Mortar 0.8, Cannon 0.85, Radar 0.33 at a quarter of its 11.7 shots/s;
- battle-cry reach, the pool values;
- the reference boards' mixes, levels (1 + 0.45 per wave) and heights.

**Measured so far:**
- path length per wave: 41–49 at waves 4–5, model 45–52;
- path within range: the table above (three maps);
- a Tesla pulse's coverage and a Mortar shell's hits on a dense line of dummies (5.0 per shell, model 4.2).

**What the first report says** is summarised in [balance-todo.md](balance-todo.md) ("Balance framework: first report"). Proxies and estimates are the least reliable rows; the M8 simulation replaces them.


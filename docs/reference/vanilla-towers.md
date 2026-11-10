# Vanilla towers and buildings

Extracted from `..\Decompiled\Assembly-CSharp` (behaviour) and the AssetRipper export (prefab stats, `GameScene` prices). The game has 20 buildable structures: 15 towers and 5 support buildings.

## Rules every tower follows

- **Damage per hit:** an enemy loses its shield first, then its armor, then its health. Each hit does `max(base damage + mark bonus − fortify, 1) × that layer's damage multiplier`. For example, a Ballista hit on health does 10 × 10 = 100. Damage left over after breaking a layer carries into the next layer as a new hit, which can't crit or stun.
- **Damage over time (bleed, burn, poison):** a tower with bleed X% adds `damage dealt × X` bleed stacks per hit. Burn and poison work the same way.
  - Once a second, up to 24 stacks are used up (`GameManager.dotTick`; cards raise this). Bleed goes mainly into health, burn into armor, poison into shield.
  - While an effect is on an enemy, every hit gets +1 multiplier on its matching layer (bleed → health, burn → armor, poison → shield), and that layer can't regenerate.
- **Slow:** each hit adds `damage dealt × slow% / 100` slow. Slow is capped at 60% and wears off by 5 points per second.
- **Crit:** towers have 0% crit unless cards add it. One crit roll gives ×2; crit chance above 50% also allows ×3, and above 100% also ×4. Each tier is capped at 50%.
- **Freeze/stun:** a per-hit chance, from cards only. It stops the enemy for 1 s, and each later freeze on the same enemy is 20% shorter.
- **XP and levels:** a tower gains XP while it has a target. The XP goes into a health, armor or shield pool, depending on which layer the target is on.
  - Each level costs 10 × current level XP and gives +1 base damage plus +1 to that layer's multiplier.
  - You can also buy a level for `(10 × level − XP) × the tower's level-cost multiplier` gold.
- **Height:** each 1/3 unit of height gives +1 base damage and +0.5 range (`heightBonus = round(3y − 1)`).
- **Targeting:** 3 priority slots from 11 options: Progress, Near Death, Most/Least Health, Most/Least Armor, Most/Least Shield, Fastest, Slowest, Marked. Priorities can be copied and pasted between towers.
- **Price:** the base price goes up by a fixed step for each copy you already own. Demolishing refunds the price of the most recent copy, so you get your money back in full.
- **Mana:** a mana tower pays `damage × its rate` per shot. If you don't have enough mana, it skips the shot.
- **Hamlet houses:** these are map decorations, not buildable. Each tower (any `Tower` subclass) placed directly next to a house at the same height adds to that house's gift of `number of towers × wave number` gold at the start of every wave.

## Towers

| Tower | Cost (+ per copy) | Base dmg | Health/Armor/Shield multiplier | Fires every | Range | Mana per shot | Level-cost multiplier |
|---|---|---|---|---|---|---|---|
| Ballista | 10 (+15) | 10 | 10/5/5 | 3 s | 5 | – | ×1 |
| Mortar | 200 (+75) | 20 | 10/15/5 | 6 s | 10 | – | ×1 |
| Tesla Coil | 200 (+75) | 10 | 6/3/10 | 2 s | 2 | 0.5 × dmg | ×2 |
| Frost Keep | 250 (+100) | 6 | 10/5/5 | 0.33 s, faster with more path in range | 2 | 2 per second | ×2 |
| Flame Thrower | 300 (+75) | 6 | 6/9/3 | 1 s | 4 | 0.25 × dmg | ×3 |
| Poison Sprayer | 300 (+75) | 5 | 6/3/9 | 1 s | 4 | 0.25 × dmg | ×3 |
| Shredder | 500 (+100) | 10 | 20/10/10 | 6 s | 5 | – | ×3 |
| Encampment | 500 (+100) | 20 | 10/15/5 | 12 s, faster with more path in range | 2 | – | ×1 |
| Lookout | 500 (+100) | 1 | 2/1/3 | never fires | 8 | – | ×5 |
| Vampire Lair | 750 (+150) | 12 | 10/5/5 | 10 s | 9 | 1 × dmg | ×3 |
| Cannon | 750 (+150) | 20 | 10/20/5 | 3 s | 6 | – | ×1 |
| Monument | 750 (+150) | 15 | 10/5/15 | 5 s | 7 | 0.25 × dmg | ×2 |
| Obelisk | 1000 (+250) | 8 | 5/10/2 | 6 times a second | 5 | 0.25 × dmg | ×3 |
| Radar | 1000 (+250) | 20 | 20/10/10 | plane: about 11.7 shots a second | 30 | – | ×4 |
| Particle Cannon | 1000 (+250) | 40 | 15/10/20 | 5 s | 20 | 0.25 × dmg | ×3 |

**Ballista** (`Tower`, the starting tower)
- Fires a homing bolt (speed 15) at one target.
- It has no special mechanics; everything it gains comes from cards.

**Mortar** (`Morter`)
- Lobs a shell, with about a 3 s flight, at the spot where the target is predicted to be.
- The shell damages every enemy within blast radius 1. Cards add +50% blast radius each.
- "Mana Bombs" cards make it use mana in exchange for +4 base damage.

**Tesla Coil** (`TeslaCoil`)
- Every 2 s it zaps every enemy within range 2 at once. No projectile, no aiming.

**Frost Keep** (`Dropper`)
- Uses 2 mana per second during waves, and stops working if you can't pay.
- It doesn't need a target. It drops slowly falling snowflakes on random path cells in a 5×5 square around itself.
- Each snowflake hits the first enemy it touches and slows by 0.5% per point of damage. A health hit for 60 means 30% slow.
- Its fire rate is multiplied by (10 + number of path cells in range) / 10. The path cells are recounted every wave.

**Flame Thrower** (`FlameThrower`)
- Every second it hits every enemy in a line 6 units long in front of it (sphere cast, radius range/6, length 1.5 × range).
- Every hit adds burn equal to 100% of the damage dealt.

**Poison Sprayer** (`FlameThrower`)
- Works like the Flame Thrower, but adds 100% poison instead of burn.

**Shredder** (`Tower` + `Sawblade`)
- Launches a homing saw blade at the target.
- After reaching the target, the blade travels back up the path toward the enemy spawn, picking a random branch at forks.
- It hits each enemy once and adds 100% bleed. Its damage goes down by 1 per enemy hit.
- The blade disappears when its damage reaches 0, when it reaches the start of the path, or after 60 s.

**Encampment** (`Encampment` : `Dropper`)
- During waves it throws landmines onto random path cells in a 5×5 square. It doesn't need a target.
- Its fire rate gets the same path-cell bonus as the Frost Keep.
- A mine arms after 1 s and explodes when an enemy touches it, damaging every enemy within radius 1.
- Mines left over are removed when the wave ends.

**Lookout** (`Lookout`)
- Never attacks. It marks one enemy in range, and won't pick an enemy another Lookout has already marked.
- Every hit on a marked enemy, damage-over-time ticks included, gets the Lookout's damage added to its base damage and the Lookout's health/armor/shield multipliers (+2/+1/+3, more with cards) added to its multipliers.
- The Lookout's crit chance is added to hits on the marked enemy, but only while that enemy is bleeding.
- The extra damage counts toward the Lookout in the damage stats. It levels up from marking, which makes its mark stronger.
- Other towers can use the "Marked" priority to prefer the enemy with the strongest mark.

**Vampire Lair** (`VampireLair`)
- Every 10 s it hits every enemy within range 9.
- With the "Tower of Blood" cards (special1), each lair has a 6% chance per card at the start of each wave to repair 1 castle HP.

**Cannon** (`Cannon` + `Cannonball`)
- Fires a cannonball that pierces through enemies, hitting each one once.
- After each hit it bounces off in a slightly random direction. It travels up to twice the Cannon's range.
- "Ricochet" (special1): the first hit always splits off a second ball. Each split halves the split chance, and both balls drop to 75% damage. A split needs at least 1 s of flight left.
- "Collateral" (special2): the ball gains +1 damage for each enemy it hits.

**Monument** (`Monument` + `SpiritSword`)
- Fires a homing spirit sword. The sword sticks into the enemy and stabs it once a second.
- Each stab does 1 less damage than the last, so 15 base damage gives about 15 stabs. The sword disappears when its damage reaches 0 or the enemy dies.
- Cards add +1 stab damage against bleeding enemies (Slaughter, special1), burning enemies (Dismantle, special2) and poisoned enemies (Desecrate, special3).

**Obelisk** (`Obelisk`)
- A beam that hits 6 times a second.
- "Focus Fire" (`obeliskTimeOnTargetMultiplier`): +1 base damage for each second it stays on the same target. The bonus stops growing once it has doubled the Obelisk's damage.

**Radar** (`RadarTower` + `BiPlane`)
- Spawns its own biplane. Every 3 s, if the plane has no target, the Radar gives it one from its range of 30.
- The plane keeps chasing that target anywhere on the map until the target dies.
- It makes diving passes and fires up to 20 inaccurate bullets per pass. It reloads when it comes within 5 units of the target.
- The plane uses the Radar's stats and levels. Demolishing the Radar also removes the plane.

**Particle Cannon** (`Tower` + `ParticleBeam`)
- Fires a fast homing beam (speed 60) at one target over a very long range.

## Support buildings

All five implement `IBuildable`, have a fixed price and refund that price when demolished. Each one only works when placed directly next to the matching map object at the same height.

- **Mana Siphon** (100, needs a mana crystal): +1 mana per second during waves.
- **Mana Bank** (500, works anywhere): +1 mana per second during waves and +20 maximum mana. "Savings Account" cards raise the maximum mana on every bank.
- **Mine** (150, needs an iron vein): +1 castle max HP, and a 10% chance at the start of each wave to repair 1 HP.
- **University** (500, needs a shrine): at the start of each wave it makes three rolls, using its health %, armor % and shield %.
  - Each successful roll gives **every tower** +1 to that damage multiplier, permanently.
  - Raising one % by 1 costs (sum of the three % + 1) × 20 gold. Cards (`universityBonus`) add to every roll.
- **Haunted House** (100, needs graves): at the start of each wave it pays `floor(castle max HP × 40%) × number of adjacent graves` gold. The "Seance" cards raise the 40%.

## Unlocking

The Ballista is available from the start. The Mortar, Tesla Coil and Mana Siphon unlock cards are in the card pool by default. The other 16 unlock cards first have to be unlocked through meta-progression.

## Bugs and quirks in the game code

- **Cannon:** `Cannon.Fire` writes range, Ricochet and Collateral to the cannonball prefab instead of the ball it just fired. They take effect from the next shot, and all Cannons share whichever values were written last.
- **Mana-cost cards:** the Tesla Coil, Obelisk, Flame Thrower, Poison Sprayer, Vampire Lair, Frost Keep and Encampment use `manaConsumptionRate` instead of `finalManaConsumption`, so they always pay their base mana rate. As a result, "Mana Cycling" (−20% Obelisk mana cost) has no effect. Mortar "Mana Bombs" does work.
- **Lookout crit:** the Lookout's crit chance only counts against bleeding targets.

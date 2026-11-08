# M3 Card Design: level-up pool for Treasure Run (decided)

Data source: the `GameScene` card objects, extracted including unlock chains. Monster Mode keeps its own pool (99 cards) and isn't touched.

---

## 1. How the vanilla pool works (relevant facts)

- **Level-ups are frequent.** One happens every 3 levels with 3 cards each. The skill tree is ignored, so there are no `CardDraw`/`CardFreq` meta bonuses. That gives about 15 level-ups × 3 slots ≈ 45 card slots per 45-level run, and every picked card leaves the pool.
- **Chains:** picking a card adds its `unlocks` to the pool, so "II" is only available after "I".
- **Interesting cards sit deep in chains** (e.g. Ricochet ← Round Shot II), hence the re-rooting rule in section 2.
- **Selling already refunds full price** (`TowerFlyweight.RemoveTower`).
- **Tree chests are already treasure-only** in Treasure Run (same `TreasureChest` prefab, patched in M2).
- **Attack speed can be granted.**
  - `Tower.SetStats` computes `rps` (seconds per shot) from `rpm`.
  - `Dropper` (Shredder) recomputes `rps` in its own `SetStats` and `SetDropPoints`, so it needs its own postfix.
  - `RadarTower` copies `rps` to its biplane after the base call, so the plane follows automatically.
  - `TowerUI` displays the raw `rpm` and will need a display patch.

## 2. Whitelist mechanics (decided)

1. **Pool = whitelist.** `UpgradeCard.unlocked` and the skill tree are ignored.
2. **Re-rooting ("de-chaining"):** a whitelisted card's effective parent is its nearest whitelisted ancestor in the vanilla chain.
   - If no ancestor is whitelisted, the card is in the pool from the start.
   - If the nearest whitelisted ancestor is a `TowerUnlockCard`, the card enters the pool when that tower or building is unlocked. This keeps tower-specific cards gated on their tower.
3. **Duplicates:** the whitelist names a count per title. Cards that exist several times get **×2** (Absolute Zero, Watch your step!), and a single copy otherwise (Everything Bleeds, Everything Burns, Pandemic, Make them Suffer).
4. **Custom cards** are `UpgradeCard` subclasses created at runtime. They use the vanilla UI, chains and queueing, with vanilla card sprites as images.

## 3. Whitelisted vanilla cards

**All of these categories**

| Category | Cards | Count |
|---|---|---|
| TowerUnlockCard | 14 towers + 5 buildings | 19 |
| ManaGenUpgradeCard | Sorcery I–III, Savings Account I–III (III chained after II; vanilla leaves it unlinked) | 6 |
| MaxHealthUpgradeCard | Fortification I–IV | 4 |
| UniversityUpgrade | Scholarships I–III | 3 |
| HauntedHouseUpgrade | Seance I–III | 3 |
| TreeUpgradeCard | Golden Forest I–II | 2 |
| MonsterUpgradeCard (normal pool) | Banditry I–III, Gold Rush!, Life Steal I–III, Absolute Zero ×2, Watch your step! ×2 | 11 |
| **Subtotal** | | **48** |

**Elements**

| Card | Count |
|---|---|
| Everything Bleeds, Everything Burns, Pandemic, Make them Suffer | 1 each = 4 |
| Creeping Cough | 1 |
| Slow Cooker I–III | 3 |
| Eviscerate | 1 |
| Exsanguinate I–II, Ignite I–II, Expunge I–II | 6 |
| Trail of Blood I, Fire and Flames I, Vile Consumption I, Ice Breaker I | 4 |
| **Subtotal** | **19** |

**Tower-specific** (gated on the tower via re-rooting)

| Card | Tower | Count |
|---|---|---|
| Ricochet | Cannon | 1 |
| Collateral I–III | Cannon | 3 |
| Focus Fire | Obelisk | 1 |
| Mana Cycling I | Obelisk | 1 |
| Longbow I–III | Ballista | 3 |
| Mana Bolts I–II | Ballista | 2 |
| Freezing Bolts | Ballista | 1 |
| Mana Bombs I | Mortar | 1 |
| Area of Effect I | Mortar | 1 |
| Deep Freeze I | Frost Keep | 1 |
| Tower of Blood | Vampire Lair | 1 |
| Slaughter I, Dismantle I, Desecrate I | Monument | 3 |
| **Subtotal** | | **19** |

**Global:** Quality over Quantity ×1 and Research Breakthrough! ×1 (after Scholarships) = **2**

**Vanilla total: about 88**

## 4. Custom cards

**Treasure-related**

| Card | Chain | Effect |
|---|---|---|
| Lucky Find | I–III | Gain a random treasure (current rarity odds, luck applies) |
| Treasure Sense | I–V | +5% per card that each level-up card slot rolls a treasure instead |
| Cartographer | I–II | Treasure offers show +1 option (4, then 5) |
| Blood Pact | single | Gain a treasure of Rare or better; −5 max castle HP |

**Map spawns** (tiles placed after picking; patch on `SpawnableObject.Start`)

| Card | Chain | Spawn | Gate |
|---|---|---|---|
| Old Growth | I–II | Trees +50% / +50% | none |
| Restless Dead | I–II | Graves | Haunted House unlock |
| Forgotten Shrines | I–II | Occult shrines | University unlock |
| Rich Veins | I–II | Iron veins | Mine unlock |
| Ley Lines | I–II | Mana crystals | Mana Siphon (available by default) |

**Economy and build style** (numbers are first drafts)

| Card | Effect |
|---|---|
| Royal Grant | Gain a flat 500 gold |
| Trade Route I–III | +3 × level gold at the start of each wave |
| Compound Interest I–II | 4% / +4% interest on unspent gold at wave end. Cap: 300 + 15 × level; doubled with both I and II |
| Investment | Pay 50% of your gold now, get 2× back after 5 waves |
| Bounty Hunters | Bosses and mini-bosses drop 10 × level bonus gold |
| Lumber Mill | Chopping trees gives +50% gold |
| War Bonds | Whenever the castle takes damage, gain 3 × level gold per HP lost |
| Bulk Discount | Price increase per extra tower of the same type −30% (wide builds) |
| Master Mason | Buying tower levels costs −20% (tall builds) |
| Undivided Attention | +100% attack speed shared across all towers: each tower gets +100% / tower count (tall builds) |

**Skill-tree replacements**

| Card | Chain | Effect |
|---|---|---|
| Wider Hand | I–II | +1 card per level-up draw |
| Field Repairs | I–II | Castle heals 1 HP at the start of each wave |

**Moved to treasures:** Quick Study (level-up draws every 2 levels instead of 3) becomes an **Epic treasure**.

**Custom total: about 38. Overall pool: about 126 cards.**

## 5. Treasure slots in level-ups (decided)

- **Each card slot rolls independently** whether it becomes a treasure slot. Several, or even all, slots of one level-up can become treasures.
- **Chance per slot:** `base(level) + 5% × Treasure Sense cards`, with `base` = 3% + 0.6% per level (≈ 30% at level 45). **No cap**: high rolls are intended, and very late levels should often show at least one treasure.
- The roll goes through `Luck` (`LuckCategory.TreasureSlot`).
- **A treasure slot shows one fixed treasure** in place of the card that was drawn for that slot. Example with hand size 5, slots 2 and 5 hit: card, treasure, card, card, treasure.
  - The treasure is rolled exactly like a slot in a chest offer: rarity roll with luck, pity, uniqueness, exclusion groups. No nested choice.
  - **The player picks exactly one slot**, as in vanilla. Picking a treasure ends the level-up, so the replaced card can't also be taken.
  - Cards that were replaced or not picked stay in the card pool for later draws, just like unpicked cards in vanilla.
  - Treasures within the same level-up are distinct.

## 6. Treasures
- The M2 starter treasures stay as **placeholders**. They're not representative of the intended treasure design and will be replaced over time.
- New treasure from this round: **Quick Study** (Epic).

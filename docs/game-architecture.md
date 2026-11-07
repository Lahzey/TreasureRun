# Rogue Tower: Game Architecture Notes

Sources:
- `..\Decompiled\Assembly-CSharp` (ilspycmd)
- `..\Ripped\ExportedProject` (AssetRipper Unity project export)

`Assembly-CSharp-firstpass` contains only Steamworks.NET and is irrelevant for modding.

**General style:**
- Every manager is a singleton MonoBehaviour with a `public static X instance` field, set in `Awake`.
- There is no data layer: numbers live in serialized fields on prefabs and scene objects. Read them from the AssetRipper export, not from the code.
- All persistent data is stored in `PlayerPrefs`.

---

## 1. Scenes and run start
- **Scenes:** `MainMenu` and `GameScene`. `LevelLoader.instance.LoadLevel(name)` switches between them asynchronously.
- **Starting a run:**
  - `PlayMenu.StartGame(int mode)` sets `PlayerPrefs["GameMode"]` = 1–4. **GameMode is the number of starting enemy paths**, i.e. Single, Double, Triple, Quadruple.
  - It then loads `GameScene`.
- **Existing run modifiers:** `MonsterMode`, `SplitMode` and `FirstPersonMode` are toggles in `PlayMenu`, each stored as a PlayerPrefs int.
  - Treasure Run fits in as **one more toggle** on that menu.
- **No mid-run save exists.** A run lives entirely in the scene. Treasure Run state can stay in memory and is reset when `GameScene` loads.

## 2. Game flow (`SpawnManager`, `GameManager`)
- `SpawnManager.level`: current wave/level. `lastLevel` = 45; after that comes Victory, then optional endless mode.
- `StartNextWave()`:
  - repair chance
  - `level++`
  - bosses at 15/25/35/45 (and every 5 levels in endless)
  - a **mini-boss on every odd level above 16**
  - spawns `level²` "spawn points worth" of enemies
  - triggers income: `House`, `IncomeGenerator`, `University`, `Mine`, `VampireLair`
- `Update()` → `EndWave()` once no enemies remain. `EndWave()`:
  - XP/score
  - **card draw if `level % cardDrawFrequency == 0`**. The frequency is 3 by default and goes down to 1 with meta upgrades.
- Enemy tiers: `SetTiers()` picks one of 3 enemy "classes" each for tier 2, 3 and 4, at random per run.
- **Castle HP:** `GameManager.health` / `maxHealth`.
  - Internally ×10; the display divides by 10. The base is 100 + meta upgrades.
  - `Enemy.AtEnd()` → `GameManager.TakeDamage(damageToTower + extraTowerDamage)`.

## 3. Rewards: the vanilla terms
- The game calls the rewards **cards** (`UpgradeCard`).
- Chests are **treasure chests** (`TreasureChest` + `TreasureUI`). Vanilla already uses the word "treasure" for the chest; the rewards inside are still cards.

**Where card draws come from (all end in `CardManager.DrawCards(n)`):**

| Source | Code | Draw |
|---|---|---|
| Level-up | `SpawnManager.EndWave`, every `cardDrawFrequency` levels | `baseCardDraw` = 3 + meta (up to 6). Twice in Monster Mode |
| Boss chest | `Enemy.Die()` → `treasureObject` with `spawnRange` (Boss15/25/35 and the endless "Ooogie" bosses: 1..2+GameMode drops) | per drop: `TreasureUI.cardDraw` = 2 + meta |
| Mini-boss chest | `SpawnManager.SpawnMiniBoss`, 50% chance via `deathObjectSpawn` | same as boss chest, 1 drop |
| Tree chop | `TreeUI.Chop()`, chance `level × treeCardChanceMultiplier` % | spawns a chest |

**Monster cards:**
- Drawn by `DrawMonsterCards()` only in Monster Mode.
- They are enemy buffs you must pick (`MonsterUpgradeCard`).
- They're a separate pool, `availableMonsterCards`.

**How `CardManager` works:**
- `availableCards`: the current pool. It starts with the unlocked entries of `startCardsCheck`.
- `DrawCards(n)` picks n unique random cards into `cards[]`, then calls `DisplayCards` (title, image, description on up to 6 card holders).
- `ActivateCard(i)`:
  - calls `card.Upgrade()`
  - adds `card.unlocks` (follow-up tiers I→II→III) to the pool
  - removes the card from the pool
  - processes queued draws
- Draws are queued while another draw is open (`queuedDraws`).
- `UpgradeCard.unlocked` comes from meta-progression: `PlayerPrefs[unlockName]` or `unlockedByDefault`.

**Card classes:**

| Class | Count | Effect |
|---|---|---|
| `TowerUpgradeCard` | 361 | per-TowerType stat add via `TowerManager.AddXxx` |
| `MonsterUpgradeCard` | 115 | Monster Mode enemy buffs → `MonsterManager` fields |
| `DOTUpgradeCard` | 41 | bleed/burn/poison mechanics → `GameManager.dotTick`, `MonsterManager` |
| `TowerUnlockCard` | 19 | unlocks a tower/building build button |
| `ManaGenUpgradeCard` | 6 | mana |
| `MaxHealthUpgradeCard` | 4 | castle HP |
| `UniversityUpgrade` | 3 | |
| `HauntedHouseUpgrade` | 3 | |
| `TreeUpgradeCard` | 2 | tree card chance |

The full list (554 cards) is in [reference/vanilla-cards.md](reference/vanilla-cards.md).

## 4. Towers
- **`TowerType` enum:** Crossbow (Ballista), Morter, TeslaCoil, Frost, Obelisk, FlameThrower, PoisonSprayer, Siphon, Radar, ParticleCannon, Shredder, Encampment, Lookout, VampireLair, Cannon, Monument.
  - `DOT` and `Global` are pseudo-types used for global bonuses.
- **`Tower` (base MonoBehaviour)** has:
  - base stats in serialized fields
  - `SetStats()`: recomputes final stats from base + height bonus + per-tower level upgrades + `TowerManager` per-type bonuses
  - `Fire()`: instantiates the projectile and calls `Projectile.SetStats(...)`
  - `DetectEnemies()`: `OverlapSphere` targeting with priorities
- **Tower XP:** towers gain XP while targeting. Each level adds +1 damage and +1 to one of health/armor/shield damage. Levels can also be bought: `BuyHealthLevel` etc., cost `10*level - xp`.
- **`TowerManager`** (58 KB, mostly boilerplate): holds one `TowerFlyweight` per type, with bonuses, `currentPrice`, `priceIncrease`, `sellPrice` and the set of towers. `UpdateAllTowers()` re-runs `SetStats`.
- **Subclasses override `SetStats`/`Fire`/`Update`:**
  - `Dropper` (Shredder?), `RadarTower`, `Monument`, `Obelisk`, `TeslaCoil`, `FlameThrower`, `Lookout`, `Encampment`, `ManaSiphon`, `VampireLair`, `Cannon`, `Morter`
  - `SpiritSword`, `Sawblade`, `ParticleBeam`, `Landmine`
- **The plane:**
  - `RadarTower` spawns a **`BiPlane`**: free-flying AI with ammo and reload, and its own projectile.
  - The radar passes its target and stats to the plane.
  - Plane ascensions (fighter jet / bomber / blimp) are a different prefab plus behaviour for `RadarTower.biPlanePrefab`.
- **Building:** `BuildButtonUI` → `BuildingManager.EnterBuildMode(prefab, cost, priceIncrease, type)`. Placement snaps to integer x/z, and y in steps of 1/3. Height gives +damage and +range.
- **Non-tower buildings** implement `IBuildable`: `House`, `ManaBank`, `Mine`, `University`, `HauntedHouse`.

## 5. Enemies, damage and RNG
- **`Enemy`:**
  - stats: health, armor, shield, regen, speed, immunities
  - DOTs: bleed, burn, poison
  - slow/haste, freeze/stun, fortify, battle cries, mark (`Lookout`)
- `Enemy.TakeDamage(TowerType whoHitMe, ...)` is **the single damage entry point** for all projectiles.
- `MonsterManager` holds global enemy modifiers plus `EndlessBuff`.
- **RNG calls that a Luck treasure would hook:**
  - `Enemy.TakeDamage`: stun roll (line ~549) and three crit rolls (lines ~567–569)
  - `Enemy.Die`: chest drop count
  - `SpawnManager.SpawnMiniBoss`: 50% chest
  - `TreeUI.Chop`: chest chance
  - `University.Research`: research rolls
  - `VampireLair` repair, `Cannonball` split chance
  - `CardManager.DrawCards`: card choice
- All of these use `UnityEngine.Random`. A Harmony transpiler can swap the specific `Random.Range` calls for our `Luck.Roll(...)`.

## 6. Map
- **The map is player-built.** After each wave, `TileSpawnLocation` markers appear at open path ends. The player clicks one, and `TileManager.SpawnNewTile(x, y, angle)` picks a random fitting tile prefab:
  - L/T/R straight, LT/LR/TR/LTR splits, dead ends
  - forced dead ends near the end
- **Grid:** 99×99, tiles are 7×7 units, start at [50, 50]. Waypoints link tiles; `Pathfinder` moves enemies along them.
- **Start tile:** one of four start tiles (1–4 paths) placed in the scene. **The castle is part of the start tile.**
- **Random tile decorations (`SpawnableObject`):** placed on tile prefabs with `spawnChance` and `spawnLevels`. Examples: Shrine, ManaCrystal, IronVein, Grave, House (hamlet), TreeSpawn.
  - A **POI fits this exact pattern**: either inject spawn slots into tile prefabs, or postfix `SpawnNewTile` and place a POI on a free grass cell of the new tile.

## 7. UI
- **Unity uGUI** with `UnityEngine.UI.Text`. TextMeshPro is shipped but barely used.
- `UIManager.SetNewUI/CloseUI` manages world-space popups (tower UI, chest UI, tree UI).
- Card UI: a panel with 6 card holders (title / image / description). Treasures can **reuse this panel** at first, with a rarity-colored frame added in code.

## 8. Meta-progression (PlayerPrefs)
Keys include:
- `XP`, `Prestige`, `Record1..4`, `Class11..43`, `UnlockedCardCount`, `Development`
- `CardDraw1..3`, `CardFreq1..2`, `TreasureDraw1`, `TowerHealth1..10`, `TowerRepair1..3`
- per-tower `XxxPHealth/PArmor/PShield`
- card unlock names

`GameManager.GameOver/Victory` write `Record{GameMode}` and XP. Treasure Run must decide whether to keep doing that (see PLAN open questions).

## 9. Models (from the AssetRipper export)
- **Plain static meshes, no procedural generation.** Every model is a voxel mesh exported from **MagicaVoxel**. The signatures:
  - meshes named `default` (MagicaVoxel's OBJ export name)
  - each material uses a **256×1 palette texture** (101 of these)
  - the standard shader
- **Scale:** buildings use roughly **0.1 world units per voxel** (a tower footprint is about 16×16 voxels = 1.6 units). Small objects and enemies use smaller voxel sizes, between 0.02 and 0.04.
- Prefabs: `Ripped/ExportedProject/Assets/GameObject/*.prefab`, e.g. `CrossbowTower`, `RadarTower`, `BiPlane`, `TreasureChest`, tile prefabs `L1`, `T3`, `LTR1`, `TCastle`. Textures are named after the model: `MainTower.png`, `BiPlane.png`, etc.

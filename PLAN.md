# Treasure Run: Mod Plan (v1)

How the game itself works is in [docs/game-architecture.md](docs/game-architecture.md).

---

## 1. Identity
- **Mode name:** **Treasure Run**
- **Listing title:** **Rogue Tower: Treasure Run**
- **Plugin GUID:** `arno.roguetower.treasurerun`
- **What players pick up:** **Treasures**.
  - Vanilla rewards are called **cards** (`UpgradeCard`), and the chest is a **treasure chest**.
  - In code, our type is `Treasure`, so it doesn't collide with the vanilla `TreasureChest`/`TreasureUI`.

## 2. Technical base (decided)
- **BepInEx 5.4.x, x86** (the game is 32-bit Mono, Unity 2021.1.11) with **HarmonyX**.
- **The plugin project:**
  - C# class library targeting `net472`
  - game DLLs referenced through a `GamePath` property
  - compiled against a publicized `Assembly-CSharp`, so private fields are accessible
  - a post-build step copies the DLL to `BepInEx/plugins/TreasureRun/`
- **Gating:** every patch checks `TreasureRun.Active`. When the mode is off, vanilla behaviour is unchanged.
- **Run lifetime:** `RunState` is created when `GameScene` loads.
- **Save/resume (wanted later, maybe for vanilla mode too):** vanilla has no mid-run save. Keep everything we add serializable from day one:
  - `RunState` holds plain data only (ids, numbers), never Unity object references.
  - The RNG seed is stored.
  - Every treasure effect can rebuild itself from its saved state.
- **How saving would work:** a full save would snapshot, between waves only:
  - placed tiles (grid position + prefab name + rotation; the tile picks are random, so they must be stored, not re-rolled)
  - towers (type, position, level/XP, priorities)
  - buildings
  - gold, mana, HP, level, enemy tier classes
  - card pool state and the `TowerManager`/`MonsterManager` bonuses
  - our `RunState`

  Loading rebuilds the scene from that snapshot. This is a sizeable but self-contained milestone, and saving only between waves avoids having to serialize enemies or projectiles.

## 3. Reward flow (decided)

| Source | Vanilla | Treasure Run |
|---|---|---|
| Level-up draw (`SpawnManager.EndWave`) | n random cards | n **whitelisted** cards. **Each slot independently** has a chance to roll a treasure instead |
| Boss / mini-boss / tree chest (`TreasureUI.Open`) | n cards per drop | **pick 1 of 3 treasures** per drop |
| Monster cards (Monster Mode) | unchanged | unchanged (still allowed in combination) |

**Card whitelist:**
- Keep only interesting cards. Candidates are listed in [docs/reference/vanilla-cards.md](docs/reference/vanilla-cards.md) (554 cards, 361 of them flat `+1 damage to armor` tower stat cards).
- The whitelist is a list of card titles in a config file. Card chains still work: unlocks of a whitelisted card are only added if they are themselves whitelisted.
- **Exception:** `TowerUnlockCard`s (19) must always stay, or towers can never be unlocked.
- **TODO (you + me):** go through the list and mark keepers.

**Treasure-per-slot chance:**
- `chance(level) = clamp(base + perLevel * level, 0, cap)`, for example 3% + 0.6%/level, capped at 35%. All values are configurable.
- The roll goes through the luck system (§5), so a Luck treasure affects it automatically.

## 4. Treasure system
```csharp
class TreasureDef {
  string Id; string Name; string Description; Rarity Rarity; Sprite Icon;
  string[] Tags;               // "economy", "projectile", "plane", "big-tower"...
  string ExclusionGroup;       // owning one member removes the rest of the group from the pool
  Func<RunState,bool> IsAvailable; // e.g. requires Radar unlocked
  float Weight = 1;
  bool Unique = true;
  Func<TreasureEffect> Create;
}
enum Rarity { Common, Uncommon, Rare, Epic, Legendary, Mythical }
```

**Draft steps:**
1. Roll a rarity using a weight table that shifts with `SpawnManager.level`. The roll goes through the luck system.
2. Filter out owned unique treasures, blocked exclusion groups, and treasures whose `IsAvailable` fails.
3. Pick n distinct treasures.
4. Pity timer.

**Mythical:**
- Treated as a normal tier everywhere in the code (weights, colors, UI).
- Its base weight is 0, so it never rolls naturally.
- Debug options to get one anyway:
  - a config flag `Debug.MythicalWeight`
  - a debug hotkey that grants or offers any treasure by id
- The Prism of Ascendance comes later. It will be a simple "rarity +1 on roll" hook in step 1, so no architecture change is needed for it.

**Effects:**
- Treasures don't patch the game themselves. They subscribe to the mod's `GameEvents`:
  - `WaveStart`/`WaveEnd`
  - `EnemyDamaged` (from `Enemy.TakeDamage`), `EnemyKilled`
  - `ProjectileFired` (from `Tower.Fire`/`BiPlane.LaunchProjectile`)
  - `TowerBuilt`/`TowerLeveled`
  - `GoldGained`, `CastleDamaged`
- Stat changes go through a modifier layer that hooks into `Tower.SetStats` (postfix). Modifier kinds: flat / additive% / multiplicative. Scopes: global / tag / TowerType / single tower.
- Simple per-type flat bonuses can still use the vanilla `TowerManager.AddXxx` methods directly.

## 5. Luck system (designed in now, treasure later)
- A central `Luck` service. Every random check the mod cares about goes through it:
  ```csharp
  bool Luck.Check(LuckCategory cat, float chance);   // success if roll < chance
  float Luck.Roll01(LuckCategory cat, bool lowIsGood);
  ```
- `LuckCategory` values: `Crit`, `Stun`, `TreasureSlot`, `TreasureRarity`, `ChestDrops`, `MiniBossChest`, `TreeChest`, `University`, …
- **Advantage:** each category has a number of extra rolls (default 0). The best result is kept, Isaac/D&D style. A Luck treasure just adds +1 extra roll to many categories. Negative values (curses) give disadvantage.
- **Vanilla RNG checks** (crit/stun in `Enemy.TakeDamage`, chest checks, university) are rerouted to `Luck` with Harmony transpilers that replace those exact `Random.Range` calls.

## 6. Main tower (decided: replaces the castle)
- The castle is part of the start tile prefab, and castle HP is `GameManager.health`.
- **At run start:** the player picks a Main Tower. The mod then:
  - hides the vanilla castle mesh on the start tile
  - spawns the chosen main tower model in its place
  - applies its trait: HP modifier, its own attack (a `Tower` component), income, pool bias, and so on
- Example archetypes:
  - **Arcane Spire:** attacks; works well with mana.
  - **Merchant Keep:** no attack, +gold per wave; economy treasures weighted higher.
  - **Iron Citadel:** much more HP, slow regeneration.
  - **War Forge:** the first bought level on every tower is free.
- The picker UI goes into the PlayMenu flow, or appears as a modal at the start of `GameScene`.

## 7. Points of interest
- Tiles are placed by the player during the run, so POIs come along with new tiles:
  - postfix on `TileManager.SpawnNewTile`
  - roll a chance via `Luck` (category `POI`)
  - place the POI on a free grass cell of the new tile
- This copies the existing `SpawnableObject` pattern (Shrine, ManaCrystal, …).
- POIs:
  - **Mage Tower**: a neutral tower that attacks and can be upgraded
  - Merchant
  - Cursed Altar
  - Treasure Vault
  - Shrine of Choice

## 8. Models and custom assets
- **Vanilla models are plain MagicaVoxel meshes** with a 256×1 palette texture each, about 0.1 world units per voxel for buildings. There's no procedural generation.
- **You don't need a custom model editor.** Use **MagicaVoxel** (free), the same tool the original developer used.
- **Main workflow: models generated from code.**
  - Claude writes a small voxel builder: Python that emits `.vox` files from primitives (boxes, cylinders, cones, mirrors, palette ramps).
  - Claude renders preview PNGs from several angles, so it can check and iterate on the look itself.
  - You make final touch-ups in MagicaVoxel.
- **Planned tooling (me):**
  1. **Ripped mesh → `.vox` converter.** It turns vanilla models back into editable `.vox` files, so you can start from the real ballista, castle or biplane.
  2. **Runtime `.vox` loader in the mod.** It builds meshes from `.vox` files in `BepInEx/plugins/TreasureRun/models/` at startup.
     - No Unity Editor and no AssetBundles are needed.
     - You can change a model and see it after restarting the game.
     - It uses the same voxel scale as vanilla, so the look matches.
- AssetBundles (with Unity 2021.1.x) are only needed if we later want particle systems or animations made in the editor.

## 9. Roadmap

| # | Milestone |
|---|---|
| **M0 Setup** | BepInEx + UnityExplorer installed, git repo, plugin skeleton that logs on load |
| **M1 Mode toggle** | "Treasure Run" toggle in PlayMenu; `RunState` lifecycle |
| **M2 First playable** | Chest → 1-of-3 treasure choice (reusing the card panel), ~10 simple treasures, rarity colors, `Luck` service, debug grant hotkey |
| **M3 Level-up rework** | Card whitelist + treasure-per-slot chance |
| **M4 Framework** | `GameEvents`, stat modifier layer, exclusion groups, pity, inventory bar with tooltips, ~50 treasures |
| **M5 Voxel pipeline** | Mesh→vox converter, runtime vox loader |
| **M6 Main tower** | Picker + castle replacement + 4 archetypes |
| **M7 Ascensions** | Radar/BiPlane → Strike Fighter / Carpet Bomber / War Blimp, then other towers |
| **M8 POIs** | Mage Tower first |
| **M9 Content & balance** | 150+ treasures, Prism of Ascendance, Mythicals, release |
| **M10 Save/resume** | Between-wave save and load for Treasure Run, then optionally for vanilla mode |

## 10. Decisions on former open questions
1. **Records/XP:** Treasure Run grants vanilla XP. Records go to separate `TR_Record{n}` keys.
   - Later: possibly a separate Treasure Run progression system based on achievements (e.g. "get a ballista to level 100"), not XP.
2. **Skill tree is ignored in Treasure Run.**
   - The skill tree (518 buttons, `UpgradeButton`) only writes `PlayerPrefs[unlockString] = 1`. GameScene code reads those keys through `PlayerPrefs.GetInt`, e.g.:
     - `StartGold1..10`, `TowerHealth1..10`, `CardDraw1..3`, `CardFreq1..2`, `TreasureDraw1`
     - `XxxPHealth/PArmor/PShield`, `ManaGen`, `MaxMana`, `GoldDrop`
     - card `unlockName`s, which are the same keys
   - **Implementation:** while Treasure Run is active, a Harmony transpiler redirects the `PlayerPrefs.GetInt` calls in GameScene classes to `MetaKeys.GetInt`. That returns the default (0) for every skill-tree key.
     - Exceptions: tower/building unlock keys (`Ballista`, `Cannon`, `Obelisk`, …) always return 1, so all towers can be unlocked.
     - Non-meta keys such as `GameMode`, `MonsterMode` and the display options pass through unchanged.
   - The card pool in Treasure Run is defined only by our whitelist and ignores `UpgradeCard.unlocked`. That also lets us add cards back that are normally locked behind the tree.
   - This is a single, contained patch, so it won't spread mess through the code.
3. **Monster Mode + Treasure Run:** allowed while the integration stays free. If it gets messy, the two become mutually exclusive.
4. **Whitelist:** reviewed later. Expect most cards to be excluded, so we'll probably add custom "normal" cards to keep the level-up pool large and unpredictable.

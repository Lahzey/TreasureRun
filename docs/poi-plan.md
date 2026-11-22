# Points of Interest (M8): design and system

**Status:** the system is built; the **Lightning Trap** (§4) and the **Wandering Merchant** (§5, three kinds) are in the game. Decisions are in §2. Code is in `src/TreasureRun/Pois`, and PLAN.md §7 links here.

Points of Interest (POIs) are special map sections that turn up while the map grows tile by tile. Each one has its own function. Their randomness and rarity make every map feel like a place with a story.

---

## 1. How the map grows (what POIs build on)

From the vanilla code (`TileManager`, `TileSpawnLocation`, `SpawnManager`):
- **One tile per wave.** After a wave, an **Expand button** (`TileSpawnLocation`) shows at every open path end. Clicking one calls `TileManager.SpawnNewTile`, which picks a random fitting tile prefab, and then starts the next wave right away. A run places about 45 tiles.
- **The player chooses where, not what.** The tile shape is random among the shapes that fit:
  - straight or turn (one exit);
  - splits (two exits);
  - four-way (three exits);
  - dead end.

  Near the end of a run, dead ends are forced, so the map closes.
- **Monsters spawn at the newest path ends.** Spawn points move outward through each waypoint's `previous` links. A new tile is the frontier for its first wave, and lies inside the map once the path grows past it.
- **A tile is 7×7 cells of 1 unit.**
  - Heights are in thirds: path 0, grass 1, hills 2–4.
  - The sand-coloured **Ground** cube (layer Path) shows wherever the green **Terrain** mesh (layer Grass, mesh collider) has a hole.
  - Waypoints sit at cell centres: the entry `last` at local (0, 0, −3), exits `left` (−3, 0, 0), `top` (0, 0, 3) and `right` (3, 0, 0).

---

## 2. Decisions

1. **Omens:**
   - Every POI has an **icon**, shown on its Expand button between waves (the omen), with the POI's name underneath.
   - An omen **can't be removed**. If a risky POI lands on your only path end and you are not up to it, tough luck.
2. **Spawning:** after every expansion, **each new Expand button rolls** for an omen.
   - There is **no pity**, and **no protection** against several omens at once (a four-way split can show three).
   - The roll goes through `Luck` (category `POI`). The chance is a setting (`PointsOfInterest.OmenChance`, default 10%).
   - The start tile's buttons don't roll.
3. **The whole section is predefined:**
   - A POI is the entire 7×7 tile, generated procedurally for **any exit combination**: one, two or three exits, in any direction.
   - So a POI never fails to fit. The only exception: when vanilla must place a **dead end** (an end tile with a portal), the omen is lost and the vanilla dead end stays.
   - No transition border was needed. The edges are plain grass at height 1 like vanilla, so the whole tile is free for the POI.
4. **No purely negative POIs.** Risky but rewarding ones are fine, for example a monster nest that guards a treasure chest.
5. **Strength:** a POI is worth about **15 points** (`Pools.PoiBudget`), between a Common and an Uncommon treasure. One with a downside or risk may pay more when it works out (the treasure chest example should net more than 15 on average).
6. **Modes:** Treasure Run only for now (`TreasureRunMode.Active`, setting `PointsOfInterest.Enabled`).

---

## 3. The system (as built)

### 3.1 Flow
1. **A new Expand button appears** (`TileSpawnLocation.Start` postfix). `PoiDirector.OnMarker` rolls against `OmenChance` through `Luck`. On success it picks a POI by weight among those whose `MinWave` has passed, stores the omen in `RunState.PoiOmens` (grid position → id), and shows the badge (`OmenBadge`).
   - The badge is the icon to the left of the button, with the name below.
   - It is a child of the button, so it pops in and out with it.
2. **The button is used** (`TileSpawnLocation.SpawnTile` prefix). The omen becomes pending.
3. **Vanilla places the tile** (`TileManager.SpawnNewTile` postfix, before the wave starts). Vanilla has already chosen the tile, so its exits follow the vanilla rules. It has also linked the tile to the previous one and created the next Expand buttons. `SectionBuilder` then:
   - reads the exits from the tile (`TerrainTile.left/top/right`); with none, the omen is lost;
   - generates the layout (`PoiSectionLogic.Generate(exits, def.Style(), seed)`) and checks it (`Validate`);
   - removes everything except the root, the Ground cube and the entry and exit waypoints (the old terrain, decorations, inner waypoints);
   - builds the terrain mesh (`SectionMeshLogic`) with the vanilla grass material, a mesh collider and the Grass layer;
   - rebuilds the inner waypoints along the generated paths and relinks `next` and `previous`, so spawn points move outward as in vanilla;
   - attaches the POI's `PoiSite` component, which builds its structure and behaviour, and stores a `PoiRecord`.

### 3.2 Procedural sections (`Pois/PoiSectionLogic.cs`, pure, offline-tested)
- **Cells:**
  - `Grass` at a height of 1–4 thirds (buildable);
  - `Path` (height 0);
  - `Covered` (path under a POI roof: a tunnel; no POI uses it yet);
  - `Reserved` (a structure: ground at its height, blocked by the structure; neither path nor buildable).
- **Routing:**
  - entry → **hub** → every exit, shortest 4-connected routes with a cost for each turn;
  - routes avoid reserved cells and stay off the edge rows, except at their openings;
  - waypoints are the corners of the routes and point towards the entry.
- **Style** (`SectionStyle`), one per POI:
  - the hub (default: the centre);
  - `Reserve` (cells to keep before routing);
  - `Shape` (its own terrain after the paths are laid);
  - random vanilla-like hill blobs on the remaining grass.
- **Mesh:** a top quad per grass cell, and side quads wherever a neighbour is lower. The path cells are holes, so the Ground cube shows through.
- **Tests:**
  - every exit combination over 25 seeds is valid;
  - all path cells are connected;
  - the four-way waypoints merge in the centre;
  - the mesh winding is correct.

### 3.3 Code
| File | Role |
|---|---|
| `PoiDef.cs` | `PoiDef` (id, name, description, icon, weight, `MinWave`, `IsAvailable`, `Style`, `Attach`) and `PoiRegistry` |
| `PoiDirector.cs` | omen rolls, the pending omen, placement; debug hooks `ForceExits`, `ForceNextOmen` |
| `PoiPatches.cs` | Harmony hooks (above), plus the debug filter on the tile choice |
| `PoiSectionLogic.cs` | layout, routing, validation, mesh (pure) |
| `SectionBuilder.cs` | turns the vanilla tile into the section |
| `OmenBadge.cs` | icon and name on the Expand button |
| `PoiSite.cs` | base of a placed POI (`Def`, `Record`, `Layout`, `Center`, `CellWorld`) |
| `PoiPopup.cs` | click target and popup (the vanilla building popup with a title, a text and one action button) |
| `LightningTrap.cs`, `LightningTrapLogic.cs` | the first POI |
| `Merchant.cs`, `MerchantLogic.cs`, `MerchantShopUI.cs` | the Wandering Merchant: site and stock, prices and section style (pure), the shop window |
| `Treasures/TreasureSale.cs` | selling treasures (used by the merchant) |
| `Core/RunState.cs` | `PoiOmens`, `Pois` (plain records, for M10 save/resume) |
| `UI/IconLibrary.cs` | loads `plugins/TreasureRun/icons/**.png` as sprites |

### 3.4 Assets
- **Icons:** SVG written as text (`tools/IconTool/Icons/<path>.svg`), rendered by IconTool to `icons/<path>.png` (128 px). Commands are in the README.
  - The mod build deploys them to `plugins/TreasureRun/icons`.
  - `IconTool sheet` renders every icon on a light and a dark background for review.
- **Models:** VoxTool recipes (`tools/VoxTool/Recipes/PoiRecipes.cs`, `pois/<id>`).
- **Sounds:** SfxTool recipes (`tools/SfxTool/Recipes/PoiSounds.cs`, `pois/<id>/…`).

### 3.5 Dev tools
- **Console:**
  - `poi list` shows omens and sites with their state.
  - `poi omen <id> [all]` puts an omen on the first open Expand button, or on all of them.
  - `poi next <id>` makes the next new button get that omen.
  - `poi exits <L|T|R|LT|LR|TR|LTR>` forces the shape of the next tile.
  - `poi rate <0-15>` sets the mana per second of the nearest Lightning Trap.
  - `poi shop` opens the nearest open merchant; `poi buy <slot>` and `poi sell <id>` trade there directly; `poi press <buy n|sell id>` presses a button of the open shop (confirmations need two presses); `poi close` closes it.
  - `cells <x> <z> [step]` prints a tile's height map.
  - `dump <object>` prints its hierarchy.
- **Settings:** `PointsOfInterest.Enabled`, `PointsOfInterest.OmenChance`, `Debug.PoiForce` (every new button gets an omen).
- **Scenarios:**
  - `poi-sections`: an omen, then each exit combination as a section, checked by raycast against the layout, with its waypoint links.
  - `poi-lightning-trap`: the pole cells are blocked; strikes per second, damage per strike and mana per second at 15 and 5 mana/s; at 0; out of mana; close-up and popup screenshots.
  - `poi-merchant`: all three merchants; wagon cells blocked; stock by kind; prices by the formula; selling (with the confirmation, unsellable treasures refused); buying all three at the Normal merchant (the last asks first, then it packs up, the closed wagon still blocks); one purchase at the Legendary (it stays) and at the Ascension Merchant (it leaves); wagon and shop screenshots per skin.

### 3.6 Balance
- POIs have specs in `ContentSpecs.Pois` and their own section in the balance report.
  - Coverage is checked: `balance coverage`, the `balance` scenario and an offline test.
  - The budget is 15 ±25%.
- A POI that deals damage is a **fixed source** on the board (`Spec.FixedSource`): a tower group that is never bought or sold and that tower cards don't reach.
- The **Trap** attack shape works per monster:
  - Each monster that walks through takes `rate × cells / speed` strikes.
  - `TrapArrival` (0.8) of them reach the trap alive.
  - Overkill is wasted per strike, as for towers.

---

## 4. Lightning Trap (built)

- **Section:** flat, like a vanilla tile, apart from a few random hills.
  - The paths from the entry and every exit meet in the **centre cell**.
  - The four cells diagonal to it hold **poles** (model `pois/lightning_pole`): a stone footing, an iron mast with three ceramic insulators, and a copper cap with a crystal.
  - The pole cells are **blocked**: each pole has a solid box over its whole cell on the Building layer, which vanilla building placement hits before the grass.
  - Nothing hangs over the path, so bosses and big or flying monsters can't clip through anything.
- **Arcs:** electric arcs run **diagonally between opposite poles** at three heights (0.45, 0.85 and 1.25 above the grass), so they cross over the centre cell.
- **Use:**
  - clicking **any pole** opens the popup: its mana rate, its strike damage, its status and its kills;
  - a **slider** sets the mana per second, **0–15** (0 = off, the default).
- **During waves**, at a rate above 0:
  - it pays that much mana per second, and strikes the centre cell (a 1×1 square) **4 times a second**;
  - every monster inside takes the strike: base damage × 6 health / × 3 armor / × 10 shield (the vanilla Tesla Coil's multipliers);
  - the hits count as tower type Global, so one more tower type for kill gold;
  - out of mana it stays silent until the bank holds a full second's worth again, so on a trickle of mana it strikes in short bursts.
- **Damage scales with the slider:** `Damage(wave, rate) = DamagePerMana(wave) × rate`, with `DamagePerMana(w) = 2.5 + 0.3·w + 0.008·w²`. At 15 mana/s that is 63 at wave 5, 176 at wave 20 and 410 at wave 40.
- **Effects and sounds, scaled with the rate:**
  - the arcs: their width, brightness and how many of the three levels show (the top arcs come on first and flicker at low rates); they dim between waves;
  - bolts from the crossing point (and at high rates from the poles) into every monster hit, with impact sparks;
  - an **X-ray flash** on every monster hit: its own palette with the brightness inverted into electric blue, drawn unlit and see-through for about 0.1 s (`XRayFlash`, the Necropolis ghost technique; no extra models);
  - sounds `pois/lightning_trap/trap_strike` (3 variants, louder at higher rates), and `trap_on` / `trap_off` when the slider leaves or reaches 0.
- **Damage from the budget:**
  - The mana counts as a cost at the market price (150 gold per mana/s).
  - For each wave, the probe `BalanceProbe.LightningTrapDamage` solves the damage per mana at which the trap, **at its best rate**, is worth 15 points, and records that best rate.
  - The rating assumes the best rate per wave (`LightningTrapLogic.PlannedRate`, a table from the probe): low early (1 mana/s up to wave 5), the full 15 late, low on boss waves. That is how a player would tune the slider.
  - Rating: 15.1 points. It can appear from wave 1.
- **Camera note:** with the default camera, one pole stands in front of the centre cell, so a monster in the middle is partly hidden for a moment.

---

## 5. Wandering Merchant (built)

Three POIs share the code, each with its own omen icon, wagon and shop skin:

| Kind | Offers | Leaves | Omen weight | From wave |
|---|---|---|---|---|
| **Wandering Merchant** (`merchant`) | 3 treasures, no ascensions (rarity rolled like an offer, luck counts; no pity, no Prism) | once all 3 are bought | 1 | 2 |
| **Legendary Merchant** (`legendary_merchant`) | 3 Legendary treasures | once all 3 are bought | 0.25 | 6 |
| **Ascension Merchant** (`ascension_merchant`) | 3 ascensions of towers in play (built towers, unlocked buildings); several may be for the same tower, since only one can be bought | after the first purchase | 0.3 | 6 |

- An omen only rolls when the kind has enough to offer (3 treasures, or 2 Legendaries or ascensions).
- **One random offer is 40% off.** An offer that becomes unavailable (you got the treasure elsewhere, its tower was ascended) shows why and no longer counts towards "all bought".
- **Selling:** every owned treasure is listed. Selling pays **half of what this merchant would ask** for it (undiscounted). These can't be sold, and the shop shows why:
  - one-time effects: Treasure Hoard and Loan Shark (gold), Sturdy Walls (health), Glass Cannon (its health cost);
  - Legion (towers bought at its prices would refund more than they cost);
  - ascensions (bound to the towers that carry them: their models, planes, decks and grids).
- A sold copy is removed (`TreasureInventory.RemoveOne`). Treasures that changed the vanilla state undo it (`TreasureDef.OnSell`): Whetstone, Long Sight, Keen Eye, Thunderclap, Bounty Board, Lucky Coin, Four-Leaf Clover, Quick Study. Piggy Bank now pays per copy owned at the wave end, and the Siege Workshop takes a sold treasure's weapon off the castle.
- Selling ends with the merchant, so buying the last offer is a trade-off.

### 5.1 Prices (`MerchantLogic`, checked against the framework)
- **Price = markup × rarity factor × BaseGold(wave)**, × 1.2 for an ascension and × 0.6 for the discounted offer, rounded to 5 gold.
  - Prices are taken **at the current wave**, so they rise with every wave and waiting is no bargain.
  - `BaseGold(w) = 160 + 22.5·w − 1.3·w² + 0.071·w³` (a Common: 182 at wave 1, 327 at 10, 659 at 20, 1576 at 30, 3487 at 40).
  - Rarity factors 1 / 1.46 / 1.9 / 2.3 / 2.7 / 3.75 (Common … Mythical).
  - Markups: Normal 75%, Legendary 92%, Ascension 65%.
- **Fair price** (`Balance.FairPrice`, probe `BalanceProbe.MerchantFairPrices`): the gold whose payment at a wave costs as many points as the rarity's budget.
  - Paying costs more points than gaining the same gold brings (the board gives up towers it relies on), so the price is solved from the buyer's side.
  - Rarity factor × BaseGold matches it within 9% for every rarity and wave (`PoiTests.MerchantPricesFollowTheFairValue`).
- **Ratings** (`ContentSpecs.Pois`): the assumed purchases at their budgets, minus the gold.
  - Normal and Legendary buy the discounted offer and one more. The Ascension Merchant buys one ascension, the discounted one half the time.
  - Normal 15.0, Legendary 15.2, Ascension 14.9; the markups were set for that. Notes are in balance-todo.md.

### 5.2 Section and wagon
- **The wagon parks on a levelled 2×2 patch beside the path** (`MerchantStyle`).
  - Its opening always faces **south-west on screen** (world +z; the camera looks along world (+x, −z)), whatever the tile's rotation.
  - The patch is chosen so the opening faces a path segment where possible: beside the trunk when the opening points across it, below or above the left/right exit route otherwise, else beside the trunk.
- **Model** (`tools/VoxTool/Recipes/MerchantRecipes.cs`, `pois/merchant/<normal|legendary|ascension>_wagon`, 20×20 voxels = the patch): a horseless market wagon with:
  - a barrel roof and spoked wheels;
  - a serving window with goods on a counter and a shelf, and a fold-out ledge;
  - a lantern, and a drawbar resting on a block;
  - crates, a barrel and a sign on the opening's side;
  - a small window with two shutters on the front end (towards south-east).
- Each kind looks different:
  - Normal: wood, a green canvas roof, a red and cream awning, a stovepipe.
  - Legendary: mahogany with gold trim, a purple roof with a gold ridge and a crown, a purple and gold awning.
  - Ascension: indigo with silver trim, a starry roof with a cut crystal spire, an indigo and pale cyan awning.
- **Moving parts** are their own models on hinges:
  - the awning: open, it is propped up 35° like a hatch so the window shows; closed, it hangs down over the window;
  - the two shutter leaves: open, they fold against the wall.
- **Packing up:**
  - the shutters fold in and the awning swings down (1.1 s, eased, with a small bounce), with the sound `pois/merchant/shutter` (played as a UI sound, like the shop's other sounds, so it is equally loud wherever the camera is; at volume 1.75 on two sources and soft-limited, about 10 dB louder than it was at 0.9) and a floating "The merchant packs up";
  - the closed wagon stays and keeps blocking its cells; a click shows "Closed".
- A solid box over the patch (Building layer) blocks building; a click opens the shop.

### 5.3 Shop window (`MerchantShopUI`)
- Screen-space uGUI built in code, with its own look per kind (`MerchantSkin`):
  - a striped awning valance with scalloped edges on a rod over the panel (procedural texture);
  - rounded frames, the kind's colours and a tagline;
  - the Ascension skin adds a star field.
- **Header:** the merchant's name, its tagline and "Prices rise with every wave", and your gold (live).
- **The rule banner** says when the merchant leaves. With one offer left it changes to "This is the last offer…".
- **Three offer cards**, each with:
  - an icon: the gem in the rarity colour, or the ascension's model;
  - the name, the rarity (and the tower, for an ascension) and the description;
  - a buy button with the price. The discounted offer adds "(was …)" and a "-40%" ribbon; otherwise the button shows "need … more" or why the offer can't be bought;
  - a SOLD stamp once bought.
- **The sell counter:** a scrolling grid of owned treasures with their sell prices; the unsellable ones are dimmed with "can't sell".
- Hovering a card or row shows its description at the bottom, or why it can't be sold.
- **Confirmations** need a second click within 4 s:
  - the purchase that closes the shop ("Last one! Buy and close shop?", or "Buy? The merchant leaves" at the Ascension Merchant);
  - every sale ("Sure?").
- While the shop is open, map clicks and the camera are blocked (vanilla `UIManager` raycasts through screen UI). Escape, a right click, the X or a click beside the panel close it.
- **Sounds** (`tools/SfxTool/Recipes/PoiSounds.cs`), with loudness matched to the vanilla card and coin clips:
  - `shop_open`: a shop-door bell on its spring, the clapper bouncing five times at uneven intervals, each a hard strike with a shimmering ring that carries on under the next;
  - `register` (buying and selling): a cash register as an event model fitted to a recording (`SfxTool fit register`, `tools/SfxTool/Recipes/RegisterModel.cs`). The key's ratchet clicks, the release, the till bell's modes, the drawer's impacts (crack, metal ring, delayed body thump) and rattle, the settling knocks ringing the counter, and room tone;
  - `shutter`: two models fitted to recordings (`fit cloth`, `fit door`) and mixed: the awning's cloth rustling as it swings (low, soft swells with crackles), then a door closing as the awning hits the wagon (the latch touching, then the slam: cracks and a heavy low thump).
  - They are built from the foley blocks in `tools/SfxTool/Recipes/Foley.cs`, modelled on recorded examples (docs/sound-workflow.md, "Building blocks").

---

## 6. POI ideas (for later batches)

Rule check: none is purely negative. Each risky one pays more when handled. The size is now the whole section; "structure" says what the POI places in it.

### Boons and towers
| POI | What it does | Agency |
|---|---|---|
| **Mage Tower** | A neutral tower on a central rock that fires arcane bolts on its own. Its popup has upgrades for **mana**, not gold, and it gains levels from kills. It isn't a built tower and can't be demolished. | route, decision (mana into it or into your towers) |
| **Ancient Forge** | An anvil cell: the tower on it gains +1 level at every wave end. | placement (which tower deserves it) |
| **Wellspring** | Three mana crystals in a ring; siphons on them give +50%. | placement |
| **Lookout Crag** | A tall rock with one build cell counting +3 high ground. | placement |
| **Fallen Star** | A crater: a tower built on its core gets an **ascension offer** for its type. Rare. | route, decision |

### Encounters (choices)
| POI | What it does |
|---|---|
| **Shrine of Choice** | Pick one: a treasure offer +1 rarity, two levels on every tower of one type, or max health. |
| **Cursed Altar** | Pay max health (declared, `HealthCost`) for a higher-rarity treasure offer, or trade a treasure up. |
| **Wishing Well** | Once per wave, throw in 10% of your gold: a luck roll returns 0×, 1×, 2× or a Common treasure. |
| **Crossroads Shrine** (split sections only) | At each wave start, choose the branch the wave takes through this section. |

### Quests (special buildings to place, objectives over waves)
| POI | What it does |
|---|---|
| **Signal Beacon** | Build any tower on the beacon cell within 2 waves, then don't leak for 3 waves: treasure offer. |
| **Ley Anchors** | You get two Anchor stones to place anywhere. Towers on the line between them get +20% damage, and the reward grows with the path cells the line crosses. |
| **Sapling of the Old Wood** | Plant it at least 3 tiles from the castle. It grows over 5 waves (a leak resets a stage); grown, it is a Golden Tree you chop for a chest. |
| **Royal Courier** | A courier runs to the castle ahead of the wave; monsters that catch it hurt it. If it arrives: treasure offer +2 rarity. |

### Risky (dangerous, pays well)
| POI | Risk | Reward |
|---|---|---|
| **Monster Nest** | A targetable nest that releases extra monsters mid-path each wave, more the longer it lives. | Destroy it: the **treasure chest** it guards. |
| **Bandit Camp** | Bandit waves come out of the camp and steal gold if they reach the castle. | Clear the camp: its loot (twice what it took) and a treasure. |
| **Dark Totem** | Monsters passing it gain a shield. | Destroy it with towers: Rare treasure offer. |
| **Dragon's Lair** | Every 5 waves the dragon flies the path as a boss. | Kill it: Legendary treasure offer. |

### Terrain sections
| POI | What it does |
|---|---|
| **Bog** | Monsters on its path move 30% slower; half its grass is marsh (not buildable). |
| **Old Road** | Monsters are 25% faster on it, and kills on it give +50% gold. |
| **Plateau** | All grass +1 height. |
| **Ruins** | Rubble cells you clear for gold or a Common treasure. |

---

## 7. Next
- Play the Lightning Trap: drag its slider with the mouse, check the arcs and the X-ray in play, the strike sound, and its damage against the real waves (balance-todo.md).
- Play the merchants: the prices against the real gold (early Legendary Merchants especially), whether selling gets used, the shop window with the mouse.
- The next POIs, one at a time; each needs an icon, a spec and a scenario.
- The balance simulation (framework B4) alongside, as planned for M8.

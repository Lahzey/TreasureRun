# Points of Interest (M8): design and system

**Status:** the system is built; the **Lightning Trap** (§4), the **Wandering Merchant** (§5, three kinds), the **Soul Urn** (§6), the **Wizard's Tower** (§7) and the **Lizard Nest** (§8) are in the game. Decisions are in §2. Code is in `src/TreasureRun/Pois`, and PLAN.md §7 links here.

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
| `SoulUrn.cs`, `SoulUrnLogic.cs`, `SoulRune.cs`, `SoulFx.cs` | the Soul Urn: site, charging and rewards; size, rewards, soul flight and section style (pure); the soulfire rune (attack speed, War Blimp deck); effects |
| `WizardTower.cs`, `WizardTowerLogic.cs`, `WizardSpells.cs`, `WizardLand.cs` | the Wizard's Tower: site, casting and the ley link; spells, weights, targeting, damage, section style and balance inputs (pure); the spells and their effects; his land (blocked building, the shine) |
| `LizardNest.cs`, `LizardNestLogic.cs`, `NestCreatures.cs` | the Lizard Nest: site, its wave and the chest; counts, stats by wave, the nest's cell and where its lizards join the path (pure); the lizards and hatchlings (vanilla monsters with our animated models) |
| `PoiReach.cs` | a POI's reach drawn like a tower's range (Soul Urn, Wizard's Tower) |
| `Core/FixedDamageLogic.cs`, `Combat.FixedHit` | fixed POI damage: exact, split over the layers like vanilla, untouched by treasures and cards |
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
  - `poi souls [n]` sends n souls into the nearest charging Soul Urn (from random points around it); `poi fill [chest|gold|rune]` fills it at once, with that reward or a random one.
  - `poi cast [fireball|lightning|ice|tornado|necromancy|goldify]` makes the nearest Wizard's Tower cast now (that spell, if it has a target; else a random one).
  - `poi hatch` hatches the nearest waiting Lizard Nest now (during a wave).
  - `cells <x> <z> [step]` prints a tile's height map.
  - `dump <object>` prints its hierarchy.
- **Settings:** `PointsOfInterest.Enabled`, `PointsOfInterest.OmenChance`, `Debug.PoiForce` (every new button gets an omen).
- **Scenarios:**
  - `poi-sections`: an omen, then each exit combination as a section, checked by raycast against the layout, with its waypoint links.
  - `poi-lightning-trap`: the pole cells are blocked; strikes per second, damage per strike and mana per second at 15 and 5 mana/s; at 0; out of mana; close-up and popup screenshots.
  - `poi-soul-urn`: the urn's cell blocked; its size for the wave and lane; souls from kills in reach (dummies on its path); the popup and its reach circle (radius 7, gone with the popup); gold (the formula at the urn's first wave, the cell free afterwards), a chest, a rune (+100%: a Ballista on it shoots twice as fast and wears the shell, its neighbour doesn't); a War Blimp's Radar on the rune (the deck shows the rune, the deck's Ballista gets the bonus, both wear the shell); screenshots of souls, reach, burst, chest, rune, shell and deck.
  - `poi-wizard-tower`: the tower's cell (off the edge and the path); its gadgets (16 chimney segments, the anemometer turning by itself); a spell winding up first (no second one meanwhile), the bulge travelling up the chimney, the fireball flying as the Magma Lobber's ball, lightning spiralling up the rod; every grass cell of his land refused; the shine, the motes, the popup and its reach; each spell on dummies: the fireball on the group (blast and burn exact), lightning on the shield (exact), the same with four damage treasures owned (still exact; a Lightning Trap strike too), three ice spikes on three dummies (exact, slowed), the tornado dragging a dummy back up the path but not a boss, a slain dummy rising in the aura, goldify on the toughest (not the boss; a statue, then 25 times its kill gold); an ascension that can't be sold or removed; the ley link (a siphon on his land next to the tower, a feed pipe; between waves idle without mana and ready with it; the popup naming its 10 mana per second; 10 mana/s drained in a wave, a spell every 4 s); screenshots of the tower, land, popup, the chimney's bulge, the rod's spiral, the crystal winding up, each spell and the link.
  - `poi-lizard-nest`: the map grown away from the castle first, so the fast hatchlings have a long way to go; the nest's cell beside the path and blocked; its way onto the path; it hatches in the wave it was found; the lizards out within their gaps, their health, armor, speed and castle damage for the wave, our model in place of the vanilla art, walking onto the path and on, legs moving; a lizard slain: its brood (count, health, speed, starting where it fell and on its way, the hatchling model, wriggling off); a slain hatchling spawns nothing; the popup; after the wave a chest in the nest, the nest still blocking; no lizards in the next wave; screenshots of the nest hatching, a lizard (and a close-up), the burst, a hatchling close-up, the brood, the popup and the chest.
  - `poi-merchant`: all three merchants; wagon cells blocked; stock by kind; prices by the formula; selling (with the confirmation, unsellable treasures refused); buying all three at the Normal merchant (the last asks first, then it packs up, the closed wagon still blocks); one purchase at the Legendary (it stays) and at the Ascension Merchant (it leaves); wagon and shop screenshots per skin.

### 3.6 Balance
- POIs have specs in `ContentSpecs.Pois` and their own section in the balance report.
  - Coverage is checked: `balance coverage`, the `balance` scenario and an offline test.
  - The budget is 15 ±25%.
- A POI that deals damage is a **fixed source** on the board (`Spec.FixedSource`): a tower group that is never bought or sold and that tower cards don't reach.
- **In game its damage is fixed too** (`Combat.FixedHit`, §7): no treasure, card or crit changes a POI's hits, and nothing procs on them. Hit points a POI takes by other means (risen minions, monsters turned to gold) go into `BoardMods.ExtraRemoved`.
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
  - ascensions (bound to the towers that carry them: their models, planes, decks and grids). `TreasureInventory.RemoveOne` refuses them too, so no other path can take one away.
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

## 6. Soul Urn (built)

- **Section:** generic (paths from every exit meet in the centre, random hills). The urn stands on **one random grass cell that no path uses** (`SoulUrnStyle`, chosen after the paths are laid, so always at plain height 1). A solid box over the cell (Building layer) blocks building there until the urn is gone; a click opens the popup.
- **Model** (`pois/soul_urn`, 10 x 10 x 13 voxels, one cell): a funerary urn in a dark blue glaze on a square stone plinth, with a bronze foot, a bronze band on the shoulder, a band of glowing runes round the belly and a flared bronze lip round a pool of soul light. Omen icon `poi/soul_urn`: the urn with a soul diving into it.
- **Charging:** every monster that dies within **7 cells** (horizontal distance) of the urn gives it its soul (`Combat.EnemyKilled`, so every kill counts, whatever killed it).
  - The soul flies from the body into the urn's mouth (`SoulUrnLogic.SoulPoint`: it rises, arcs over and dives in, weaving a little; 0.75 s + 0.09 s per cell): a glowing head with a bright core and a fading trail, a small puff where it leaves the body and a flash where it enters.
  - Sound `pois/soul_urn/soul_absorb` (3 variants, quiet, at most 3 at once): a breath of air rising as it swoops in, then a soft glassy plip on a note of A minor and a faint low "whum".
  - The soul fire in the urn's mouth (rising soft flames and a glow) grows with the fill; every soul arriving flashes it; above 85% the urn trembles.
- **Size** (`SoulUrnLogic.Capacity`), set when its first wave starts: all the monsters its lane sends that wave and the next two (the balance wave model), times the lane's share of the spawn points (the spawn points whose path runs through the section, at least a quarter), rounded to 5, at least 30. About 30 at wave 3, 120 at wave 10, 210 at wave 15, 280 at wave 20, 450 at wave 30 on one lane. A kill zone in its reach that takes three quarters of its lane's monsters fills it in about four waves, one that takes half in about six; towers elsewhere barely charge it.
- **Popup:** what it does, `Souls: n / size (p%)` and a bar of 20 blocks (cyan for the filled part); refreshed while open. While it is open, the urn's **reach** shows as a circle of 7 round it, drawn like a tower's range (the vanilla TowerUI line's material, width and colour).
- **Full:** it bursts and leaves one of three rewards at random (a third each, `Luck.Raw01`):
  - the fill sound `pois/soul_urn/urn_fill` (2.8 s): a ghostly choir on A minor and a rush of air swell for 0.9 s while the souls whirl into the mouth and the urn shakes and glows; then the burst, a deep boom, the glaze cracking, and the chord turns major (bells A, C#, E, A ringing out with the choir). Played loud (minimum distance 12), so it carries at any zoom;
  - at the burst: glaze and bronze shards fly, soul light bursts upwards, a shock ring runs over the ground and a beam of light shoots up; the urn is gone and its cell is free;
  - **a treasure chest** (the vanilla chest, a treasure offer in Treasure Run) on the cell;
  - **gold** (`SoulUrnLogic.Gold` at the urn's **first wave**, so stalling the fill pays nothing; paid at once, with a burst of gold voxels and the vanilla coin sound);
  - **a soulfire rune** on the cell (`SoulRune`): the tower built on it attacks **+100% faster** (`SoulUrnLogic.RuneBonus`, fixed whatever the wave; StatLayer, percent attack speed), with the sound `pois/soul_urn/rune_ignite` (a flame catching, a rising sparkle, a hum). The cell stays buildable; the rune can't be removed.
  - A floating text names the reward ("A treasure chest!", "+1200 gold", "Soulfire rune: +100% attack speed").
- **The rune's look:** a circle of light just above the ground (a soft halo, an outer ring of twelve runes, a three-bladed sigil turning in the middle) with motes of soulfire rising. It is a little wider than the cell, so it shows round a tower's base. With a tower on it, the sigil turns faster, the circle brightens and more motes rise.
- **The soulfire shell:** every tower the rune powers (the one on it, a Radar on it and the tower on that Radar's blimp deck) wears a ghostly shell (`RuneShell`): each of its meshes drawn again 0.045 larger on every side, unlit and translucent in the soul colours, flickering like plasma. The copies are children of the tower's parts, so they turn with its turret; a model change (an ascension) rebuilds them; the shell goes out when the tower no longer draws on a rune. Cost: one extra draw per tower part, for the one or two towers on runes.
- **War Blimp:** a Radar (the War Blimp's air mast) on a rune gets the bonus itself (its planes fire faster, `owner.rps`), and **the tower on the blimp's deck gets it too** (`SoulRune.For`: a deck passenger takes the rune under its blimp's Radar, never one the blimp happens to hover over). The deck then carries the same circle, sized to the deck, flying with the blimp (`RuneDeckGlow`); it goes out when the Radar no longer stands on a rune.
- **Balance:** the chest is a treasure pick (18.5-23 points); the gold is 1.5 times the gold worth a chest at the urn's first wave (`FairPrice.GainOf`, fitted within 8%; `GoldFactor` makes up for the late payout); the rune's +100% is rated with its tower counted as three average towers (`RuneTowerShare`: the strongest tower, kept busy with slows). Rated from four waves after the omen (`FillDelay`): **12.8 points** against 15, inside the budget. Probes `BalanceProbe.SoulUrnRewards` and `SoulUrnLevers` (rating by fill delay, gold factor and tower share). Omens from wave 3 to 40 (later urns would have no time to fill). Notes in balance-todo.md.

## 7. Wizard's Tower (built)

An erratic wizard's crooked tower. Named with the possessive: someone lives there, and the land is his.

- **Section:** generic (paths from every exit meet in the centre, random hills).
  - The tower stands on a random grass cell that is **neither on the section's edge nor next to a path** (8 neighbours; `WizardTowerStyle`, chosen after the paths are laid, so at plain height 1).
  - If no cell qualifies: one with a path only diagonally next to it, then any inner grass cell. A four-way section leaves the four inner corner cells.
- **The wizard's land** is the whole section (`WizardLand`):
  - its grass shimmers blue (a translucent copy of the section's terrain mesh, breathing slowly) and motes of blue light rise from it;
  - **nothing can be built on it**: the build ghost doesn't show there, and a click says "This land belongs to the wizard" (a red floating text, like a closed shop's "Closed"); drag-building skips its cells;
  - exception: with the Ley Line Network, mana siphons may be built there (the ley link below).
- **Model:** a plain wizard's tower with one gadget per spell (`tools/VoxTool/Recipes/WizardRecipes.cs`, the layout and animation in `WizardGadgets`).
  - **The tower** (`pois/wizard_tower`, 14 x 14 x 42 voxels): a round fieldstone tower on a plinth, a stone band halfway up and a corbelled ring under the roof, an arched oak door facing the camera, three glowing arched windows, and a tall indigo wizard's hat with a gold band and a few stars, its tip bending a little.
  - **The gadgets** (`pois/wizard/…`) are each neat and symmetric. Their placement round the tower, at different sides and heights, is what makes it look erratic:

| Spell | Gadget | Where | Idle | Wind-up before the spell leaves it |
|---|---|---|---|---|
| Fireball | an arcane chimney: a stack of 16 round segments of dark arcane stone (every third with glowing runes), the same from the ground up, under a flared brazier of embers | on the camera's left, a third of it sunk into the tower's side like a round stair turret set into a keep (it cuts through the brim, its brazier against the hat) | embers rise | a **bulge travels up the chimney** (each segment widens as it passes, up to 1.7x, with a glow inside), as if the fireball, a little wider than the chimney, pressed its walls apart (0.75 s) |
| Lightning | an iron rod with a copper coil and a glass orb | behind the hat, strapped under the brim, the orb above the tip | the orb glows | **two strands of lightning spiral up the rod** into the orb, which swells (0.6 s) |
| Ice Spikes | a small round balcony with a railing and a pedestal, an ice crystal floating over it | under the window on the front right | the crystal turns and bobs | the crystal **spins up** in a swirl of frost, glowing (0.6 s) |
| Tornado | an anemometer: an iron arm with a mast, three white cups on a brass hub | high on the right | the cups turn | the cups **whirl** to a blur in a swirl of wind (0.6 s) |
| Necromancy | a skull lantern hanging from a bracket | by the window on the front left | it sways | it **swings harder and harder**, its green glow flaring (0.6 s) |
| Goldify | an alembic: a gold flask on an iron tripod, two gold rings crossing round it | on the ground at the front right | the rings turn | the rings **spin up** in a swirl of gold (0.5 s) |

  - Every part is its own model on the tower, world-aligned (not turned with the tile), so the door and gadgets always face the camera. Some gadgets reach onto neighbouring cells, which are always his land.
  - A spell picks its targets when it leaves the gadget, after the wind-up; with nobody left in range it fizzles. No second spell starts while one winds up.
  - `VoxTool preview <file.vox> --game` renders a model as the in-game camera sees it.
  - Omen icon `poi/wizard_tower`: the tower under its hat with all six gadgets as the game places them, simplified (the chimney behind the brim on the left with the lantern in front of it, the rod and orb behind the hat, the anemometer high on the right, the crystal on its balcony, the alembic on the ground); nothing the tower doesn't have, every part touching the next, the hat's tip exactly at the centre line. Colours: stone, the hat's violet, a darker violet for the chimney, gold, one warm orange (fire, runes, the window), cyan (ice, the orb), green (the lantern), iron, wood and the cups' white.
- **Spells:** during waves he casts every **5 s** (`WizardTowerLogic.Interval`) at monsters within **10 cells** of the tower. With nobody in range the spell waits until someone comes. He picks at random by weight among the spells that have a target:

| Spell | Weight | Target | Effect | Base damage (x Power), multipliers |
|---|---|---|---|---|
| **Fireball** | 3 (26%) | the largest group (monsters within 1.5 of each one; ties: furthest along), aimed where it will be when the ball lands | blast of radius 1.5, then a burn: half the blast again in 4 ticks over 2 s (his own burn, not vanilla's), flames on the burning | x1, 5/8/3 (fire bites armor) |
| **Lightning** | 3 (26%) | the most shield; with no shields in range the most health and armor | one big hit: a bolt with forks from the orb, an X-ray flash | x4, 6/3/12 |
| **Ice Spikes** | 3 (26%) | the fastest (ties: furthest along), three different monsters while there are enough | three spikes, each a hit and +25% slow (vanilla slow) | x1.2 each, 6/6/6 |
| **Tornado** | 1 (9%) | the furthest along that isn't a boss | a funnel that drifts back up the path at 1 cell a second for 3.5 s; monsters it touches (not bosses) whirl inside it, are carried back, and are ground 4 times a second; released, they walk on from there | x0.5 per second, 5/5/5 |
| **Necromancy** | 1 (9%) | none (his whole reach) | an aura for 4 s: monsters slain inside rise as the Necropolis's minions (`RisenMinion`: a fifth of their max health, walk back up the path, blow up on the first monster); a green circle at the reach, a sigil round the tower, wisps | none (the minions' blasts) |
| **Goldify** | 0.4 (4%) | the toughest (shield + armor + health) that isn't a boss | the monster dies at once and stands for 1 s as a gold statue (a gilded copy), then bursts into coins: 25 times its kill gold in all | none |

  - Mini-bosses count as bosses.
  - Goldify: a monster turned to gold doesn't split or spawn anything, and the wizard counts as a tower type for its kill gold. The 24 extra kill golds are paid outside the kill, so gold multipliers count once (they are in the kill's own gold).
  - Spells in flight: glowing heads with trails. The fireball is the Magma Lobber's ball (`ascensions/flame/magma_ball` at 0.85, 0.6 across, a little wider than the chimney), tumbling and trailing embers and smoke; it lands as the wizard's blast, with no lava pool. An ice spike is a shard. Every cast flashes its gadget; impacts bloom (`Bloom`, `GroundFlash`).
  - **Sounds:** the fireball and the ice spikes reuse the Magma Lobber's lob and impact and the Icicle Cannon's launch and shatter. New (`tools/SfxTool/Recipes/WizardSounds.cs`, `pois/wizard_tower/…`):
    - `thunder`: a sizzle, the crack with crackles, a boom and rolling thunder (1.9 s, as loud as an explosion);
    - `tornado`: wind whirling 2.2 times a second over a low roar, a whistle, grit (3.6 s);
    - `necro_aura`: a beating low drone, a ghostly choir on a diminished chord sinking, whispers (4.2 s);
    - `goldify`: a rising run of bells, a metallic clink as it sets, glitter (1.5 s); the vanilla coin sound at the coins.
- **Fixed damage:** every hit goes through `Combat.FixedHit` (`Core/FixedDamageLogic.cs`):
  - base damage x the layer's multiplier, split over shield, armor and health like vanilla (the excess of a broken layer carries on as base damage);
  - **exact**: no crits, no treasure or card modifiers (First Strike, Executioner, Hex of Frailty, Master of Elements…), no damage-over-time or mark bonuses, and nothing procs on it;
  - the burn is his own and the minions' blasts are fixed too. Only fortify (a monster's own trait) lowers it, as for every hit.
  - Treasures that act on a monster's *death* (Volatile Corpses, Overkill) still fire on the monsters he kills.
  - **The Lightning Trap now uses it too:** its strikes used to take treasure modifiers (First Strike, Executioner, Static Arc procs).
- **Damage by wave:** `WizardTowerLogic.Power(w)`, about 6 at wave 5, 35 at 10, 190 at 20, 450 at 30 and 795 at 40; a lightning bolt is four times that.
- **Texts keep the details hidden.** The omen: an erratic wizard lives here and lends you a hand against nearby monsters with whatever spell comes to his mind, from the everyday to the very rare; his land can't be built on. The popup says the same, then the ley link (below), then his spells cast, monsters slain and gold. While it is open his reach shows as a circle of 10, like a tower's range (`PoiReach`, shared with the Soul Urn).
- **Ley link:** with the Ley Line Network, siphons may be built on his land.
  - A siphon next to the tower (4 neighbours, any height) **links** it. The siphon grows a feed pipe into the tower, like the Particle Accelerator's core, and mana motes flow in. It needs no crystal in its network.
  - During waves the tower then draws **10 mana a second** from your mana (metered like the Lightning Trap: out of mana it waits until a second's worth is there again) and casts **every 4 s** while it is paid.
  - The popup says what the link costs: "he draws 10 mana per second during waves and casts a little faster", or that he would but your mana has run dry. With the ascension and no link it says a siphon next to the tower would feed him. The siphon's popup shows the link too.
  - Between waves the link shows as it will start: ready if the bank holds a second's worth (10). Before, it kept the last wave's state, so after a wave that ran the mana dry it showed 5 s until the next wave had paid again.
- **Balance** (`ContentSpecs.WizardTower`): 17.6 points (P80 19.3, early 19.3, late 7.1) against 15. Omens from wave 3. Notes in balance-todo.md.
  - Each damaging spell is a fixed source with its share of the casts. The fireball counts 30% more targets for aiming at the largest group, and all his spells count 85% (`Reach`: he stands where his section landed, not where the defence is).
  - The tornado's drag counts as a slow. The minions and goldify's kills count as hit points taken (`BoardMods.ExtraRemoved`), goldify's gold as gold.
  - The power is solved per wave for 15 points (probe `BalanceProbe.WizardTowerPower`) up to about wave 15.
  - Later no damage reaches 15: a spell every 5 s lands too few hits against the late boards, even with a reach of 10. The ceiling is about 15 points at wave 18, 9 at 30 and 6 at 40. There the power is the one that reaches 90% of the ceiling: his spells kill what they hit.
  - The ley link needs an ascension and isn't rated.

## 8. Lizard Nest (built)

A nest of giant lizards beside the path: a risk in the wave it is found, a treasure chest afterwards.

- **Section:** generic (paths from every exit meet in the centre, random hills).
  - The nest stands on a random grass cell **next to the path** (4 neighbours), off the section's edge where possible (`LizardNestStyle`, chosen after the paths are laid, so at plain height 1).
  - Its cell stays blocked for good: a low solid box on the Building layer; a click opens the popup.
- **Where the lizards join the path:** the path cell beside the nest (`LizardNestLogic.PathCell`), then the waypoint ahead towards the entry (`TargetWaypoint`: the cell's own waypoint, or the end of the route segment through it).
  - The nest adds a waypoint of its own on that path cell (`TR_NestExit`, linked to it).
  - So the lizards climb down off the nest onto the path, then follow the route like any monster.
- **Its wave** is the one that starts when its tile is placed.
  - The first lizard crawls out at once, the others 1.4 s apart (`SpawnGap`). A lizard is about 1.35 cells long and walks a cell a second, so they don't walk into each other.
  - As the first one comes out the eggs break (the model swaps to the hatched nest). Each time, the nest shakes, twigs fly, the rustle sound plays, and the lizard rises out of the bowl.
  - If the wave ends before all are out, the rest stay in.
- **Lizards** (`LizardNestLogic`):
  - 1 at wave 1, rising to 5 at wave 40 (1 + 4 x (wave - 1) / 39, rounded): 2 from wave 6, 3 from 16, 4 from 26, 5 from 36.
  - Each has **12 times the wave's average monster** as health (health, armor and shield together, from the wave model) and 1.2 times as armor, no shield. So a lizard is equally strong for its wave whenever the nest turns up: about 2,000 health at wave 3, 5,000 at 10, 18,500 at 20, 43,000 at 30 and 76,000 at 40.
  - Speed 1 (a Goblin walks at 2), 3 castle damage, kill gold 4 times the wave's average kill.
  - Monster cards (+% health and so on) apply as to every monster.
- **The brood:** when a lizard dies it bursts (a splat of goo, the burst sound). **5 to 15 hatchlings** (5 at wave 1, 15 at wave 40) wriggle out where it fell and on along its way.
  - A hatchling has 0.12 times the wave's average monster as health, no armor or shield: one hit for most towers. That is about 20 at wave 3, 50 at 10, 185 at 20, 430 at 30 and 760 at 40.
  - Speed 3.4 (each one within 8%; a Bat flies at 3.6), 1 castle damage, kill gold 1 (plus the tower types).
  - A lizard that the Wizard's Tower turns to gold is pure gold: no brood.
- **The monsters** are vanilla Goblins underneath (`NestCreatures.cs`): pathfinder, health bar, collider, death and gold.
  - Their art is hidden, and our animated voxel model sits in its place under the walk animator's direction transform, so they turn the way they walk.
  - They count as the wave's monsters (`currentEnemies`, `Combat.EnemySpawned`). The freeze tint shows on the lizard's body.
  - **Lizard** (`pois/lizard/…`, 0.027 per voxel, about 1.35 long with its tail): a low body, dark green with blotches, a cream belly and an orange crest of spikes; a head with yellow slit eyes; a tail of three pieces; four sprawling legs.
    - **Animation:** a diagonal gait (front right with back left, then the other pair), each leg swinging forward and lifting. The spine sways with the steps, the head steadies against it, and the tail swings behind in a travelling wave.
    - The pace follows the distance actually walked. Slowed, it walks slower; frozen, it stands still, breathing, its tail curling a little.
  - **Hatchling** (`pois/hatchling/…`, 0.024 per voxel, about half a cell long): a big round green head (a dark back, a pale belly, big black eyes with a glint), three body pieces getting smaller, and a fin. **No legs.**
    - It wriggles like a tadpole: a wave runs down its body, growing towards the fin, faster the faster it goes. The whole body weaves a little from side to side.
    - It pops out with a hop.
  - **Nest** (`pois/lizard_nest`, `pois/lizard_nest_hatched`, 12 x 12 x 6 at 0.1): a ring of woven twigs with sticks along the rim and poking out, dry grass in the bowl, four speckled leathery eggs. Hatched, it holds broken shells with pale insides, shell bits and slime.
  - Omen icon `poi/lizard_nest`: a lizard rearing out of its nest, a foreleg with pale claws gripping the rim, an egg on either side.
- **After the wave:** a treasure chest stands in the nest (the vanilla chest, a treasure offer in Treasure Run), with a rustle and "The lizards' treasure!".
  - The nest stays and keeps its cell blocked, but never hatches again.
- **Popup:** before its wave, what will happen. During it, how many lizards are out of how many, the hatchlings, and how many are still out there. Afterwards, an empty nest and how many of its brood were slain.
- **Sounds** (`tools/SfxTool/Recipes/LizardSounds.cs`, `pois/lizard_nest/…`; the egg crack and the splat are modelled on recorded examples):
  - `nest_rustle` (3 variants, 1.1 s): an egg cracking at the start (shell ticks, the crack with its knock, a duller second crack, fragments crackling), and twigs and dry grass rustling in one swelling gesture throughout.
  - `lizard_burst` (2 variants, 0.9 s): a wet splat. A heavy low impact under a fleshy wet splash, sharp splatter, then sparse bright drips and two soft gloops.
  - `hatchling_squish` (3 variants, 0.25 s, played quietly, at most 3 at once): the same splat in miniature.
- **Balance** (`ContentSpecs.LizardNest`): **18.1 points** (P80 18.9, early 10.9, late 17.9) against 15: a risk that pays a little more when it works out. Omens from wave 3. Notes in balance-todo.md.
  - The lizards and all their hatchlings count as more health and armor on the monsters of its one wave. The evaluator then sees the tougher wave, leaks included.
  - The chest counts from the next wave (a treasure pick), and their kill gold as gold.
  - The chest dominates (the risk is one wave of the ten-wave horizon), so the lever was the lizards' toughness: at 6 times the wave's average monster the nest rated 19.6 (over budget), at 12 times 18.1 (probe `BalanceProbe.LizardNestLevers`, tests/out/lizard-nest.txt).

## 9. POI ideas (for later batches)

Rule check: none is purely negative. Each risky one pays more when handled. The size is now the whole section; "structure" says what the POI places in it.

### Boons and towers
| POI | What it does | Agency |
|---|---|---|
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

## 10. Next
- Play the Lightning Trap: drag its slider with the mouse, check the arcs and the X-ray in play, the strike sound, and its damage against the real waves (balance-todo.md).
- Play the merchants: the prices against the real gold (early Legendary Merchants especially), whether selling gets used, the shop window with the mouse.
- Play the Soul Urn: how long it takes to fill with a real kill zone, the rune on strong towers and on a War Blimp's Radar.
- Play the Lizard Nest: how risky its wave feels early (a 2,000-health lizard at wave 3) and late (five lizards and 75 hatchlings), whether the hatchlings leak, the chest's worth against that risk.
- Play the Wizard's Tower: his strength early (about 15 points from wave 3) and late (below the budget), whether the tornado and goldify feel special, the ley link's 10 mana/s against its 25% more spells, building round his land.
- The next POIs, one at a time; each needs an icon, a spec and a scenario.
- The balance simulation (framework B4) alongside, as planned for M8.

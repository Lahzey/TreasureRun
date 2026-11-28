using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TreasureRun.Core;
using TreasureRun.Pois;
using TreasureRun.UI;
using UnityEngine;

namespace TreasureRun.Debugging
{
    /// <summary>Points of Interest (docs/poi-plan.md): omens, generated sections for every exit combination, the Lightning Trap.</summary>
    internal static class PoiScenarios
    {
        public static IEnumerable<Scenario> All => new Scenario[] { new PoiSectionsScenario(), new LightningTrapScenario(), new MerchantScenario(), new SoulUrnScenario(), new WizardTowerScenario(), new LizardNestScenario(), new LaboratoryScenario() };

        /// <summary>Lets the running wave end (killing everything) so the Expand buttons are open again.</summary>
        public static IEnumerator OpenButtons(ScenarioContext c)
        {
            for (int i = 0; i < 16 && SpawnManager.instance.combat; i++)
            {
                DevConsole.Execute("killall");
                yield return c.Wait(1.5f);
            }
            // A level-up card draw or a treasure offer can come up a moment after the wave ends: pick until none is left.
            for (int i = 0; i < 8; i++)
            {
                yield return c.Wait(0.5f);
                if (CardManager.instance == null || !CardManager.instance.drawingCards) { if (OpenMarker() != null) break; continue; }
                DevConsole.Execute("pick 0");
            }
        }

        public static TileSpawnLocation OpenMarker() =>
            Object.FindObjectsOfType<TileSpawnLocation>().Where(m => m.gameObject.activeInHierarchy)
                .OrderBy(m => m.transform.position.sqrMagnitude).FirstOrDefault();

        /// <summary>The open Expand button farthest from the castle (to grow the map away from it).</summary>
        public static TileSpawnLocation FarMarker() =>
            Object.FindObjectsOfType<TileSpawnLocation>().Where(m => m.gameObject.activeInHierarchy)
                .OrderByDescending(m => m.transform.position.sqrMagnitude).FirstOrDefault();

        /// <summary>Puts a Lightning Trap omen on an open Expand button and uses it; returns the new site (null if none).</summary>
        public static IEnumerator Expand(ScenarioContext c, SectionExits? exits, List<LightningTrap> result) =>
            Expand(c, exits, PoiRegistry.Get(LightningTrapLogic.Id), result);

        /// <summary>Puts an omen of <paramref name="def"/> on an open Expand button and uses it; adds the new site to the list.</summary>
        public static IEnumerator Expand<T>(ScenarioContext c, SectionExits? exits, PoiDef def, List<T> result, bool far = false) where T : PoiSite
        {
            yield return OpenButtons(c);
            TileSpawnLocation marker = far ? FarMarker() : OpenMarker();
            c.Expect(marker != null, "an open Expand button");
            if (marker == null) yield break;
            c.Expect(PoiDirector.PlaceOmen(marker, def), $"omen {def.Id} placed");
            c.Expect(marker.GetComponentsInChildren<Transform>(true).Any(t => t.name == OmenBadge.Name), "the Expand button shows the omen icon");
            int before = TreasureRunMode.Run.Pois.Count;
            int x = marker.posX, y = marker.posY;
            PoiDirector.ForceExits = exits;
            marker.SpawnTile();
            yield return c.Wait(0.3f);
            bool placed = TreasureRunMode.Run.Pois.Count == before + 1;
            TerrainTile tile = TileManager.instance.tileArray[x, y];
            if (!placed && tile != null && SectionBuilder.ExitsOf(tile) == SectionExits.None)
            {
                c.Info($"the tile at {x},{y} became a dead end: the omen is lost (expected rule)");
                yield break;
            }
            c.Expect(placed, $"a POI section was placed at {x},{y}");
            T site = tile != null ? tile.GetComponent<T>() : null;
            c.Expect(site != null, $"the section carries the {def.Name}");
            if (site != null) result.Add(site);
        }

        /// <summary>
        /// Checks a built section against its layout: heights and path cells by raycast, waypoints linked. An
        /// <paramref name="open"/> reserved cell is one the structure leaves buildable (the Laboratory's chamber): grass.
        /// </summary>
        public static void CheckSection(ScenarioContext c, PoiSite site, (int x, int z)? open = null)
        {
            SectionLayout l = site.Layout;
            int wrong = 0;
            for (int x = 0; x < SectionLayout.Size; x++)
                for (int z = 0; z < SectionLayout.Size; z++)
                {
                    Vector3 p = site.CellWorld(x, z);
                    if (!Physics.Raycast(new Vector3(p.x, 40f, p.z), Vector3.down, out RaycastHit hit, 80f, ~0, QueryTriggerInteraction.Ignore)) { wrong++; continue; }
                    string layer = LayerMask.LayerToName(hit.collider.gameObject.layer);
                    if (l.IsPath(x, z)) { if (layer != "Path") wrong++; }
                    else if (l.Cells[x, z] == SectionCell.Reserved && open != (x, z)) { if (layer == "Grass" || layer == "Path") wrong++; }
                    else if (layer != "Grass" || Mathf.Abs(hit.point.y - l.Height[x, z] / 3f) > 0.05f) wrong++;
                }
            c.Expect(wrong == 0, $"terrain matches the layout ({wrong} cells wrong)");
            TerrainTile tile = site.GetComponent<TerrainTile>();
            var exits = new[] { tile.left, tile.top, tile.right }.Where(w => w != null).ToList();
            bool linked = exits.All(w =>
            {
                Waypoint cur = w;
                for (int i = 0; i < 10 && cur != null && cur != tile.last; i++) cur = cur.next;
                return cur == tile.last;
            });
            c.Expect(linked, $"every exit ({exits.Count}) leads to the entry");
            c.Expect(tile.last.previous.Count > 0 && tile.last.next != null, "the entry is linked to the inside and to the previous tile");
        }
    }

    /// <summary>Every exit combination vanilla offers becomes a valid POI section (forced tile shapes).</summary>
    internal sealed class PoiSectionsScenario : Scenario
    {
        public override string Name => "poi-sections";
        public override string Description => "POI sections: omen on an Expand button, the generated section for each exit combination.";
        public override bool Permanent => true;

        public override IEnumerator Run(ScenarioContext c)
        {
            c.Expect(PoiDirector.Enabled, "Points of Interest are on (Treasure Run)");
            if (!PoiDirector.Enabled) yield break;
            if (GameHelpers.Level < LightningTrapLogic.MinWave) DevConsole.Execute($"level {LightningTrapLogic.MinWave}");
            var shapes = new SectionExits?[]
            {
                SectionExits.All, SectionExits.Left | SectionExits.Top, SectionExits.Top | SectionExits.Right,
                SectionExits.Left | SectionExits.Right, SectionExits.Left, SectionExits.Top, SectionExits.Right,
            };
            var seen = new HashSet<SectionExits>();
            foreach (SectionExits? shape in shapes)
            {
                var sites = new List<LightningTrap>();
                yield return PoiScenarios.Expand(c, shape, sites);
                foreach (LightningTrap s in sites)
                {
                    var e = (SectionExits)s.Record.Exits;
                    seen.Add(e);
                    c.Info($"wanted {shape}, got {e} (rotation {s.Record.Rotation})");
                    PoiScenarios.CheckSection(c, s);
                }
            }
            c.Info($"exit combinations built: {string.Join(", ", seen)}");
            c.Expect(seen.Count >= 4, "at least four different exit combinations");
            yield return PoiScenarios.OpenButtons(c);
            DevConsole.Execute("zoom 14");
            yield return c.Wait(0.5f);
            c.Shot("sections");
        }
    }

    /// <summary>The Lightning Trap: switched on it strikes the tunnel cell 4 times a second for 15 mana per second.</summary>
    internal sealed class LightningTrapScenario : Scenario
    {
        public override string Name => "poi-lightning-trap";
        public override string Description => "Lightning Trap: strikes, damage per strike, mana per second, off and out of mana.";
        public override bool Permanent => true;

        public override IEnumerator Run(ScenarioContext c)
        {
            c.Expect(PoiDirector.Enabled, "Points of Interest are on (Treasure Run)");
            if (!PoiDirector.Enabled) yield break;
            if (GameHelpers.Level < LightningTrapLogic.MinWave) DevConsole.Execute($"level {LightningTrapLogic.MinWave}");
            var sites = new List<LightningTrap>();
            for (int i = 0; i < 3 && sites.Count == 0; i++) yield return PoiScenarios.Expand(c, SectionExits.Top, sites);
            LightningTrap trap = sites.FirstOrDefault();
            if (trap == null) yield break;
            PoiScenarios.CheckSection(c, trap);

            // The pole cells are blocked for building.
            int blocked = 0;
            foreach (var (x, z) in LightningTrapLogic.PoleCells)
            {
                Vector3 p = trap.CellWorld(x, z);
                if (c.BuildAt("CrossbowTower", Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.z)) == null) blocked++;
            }
            c.Expect(blocked == 4, $"the four pole cells can't be built on ({blocked}/4)");

            ResourceManager rm = ResourceManager.instance;
            rm.maxMana = 100000;
            rm.AddMana(100000);
            Enemy dummy = c.Dummy(trap.Center + new Vector3(0.35f, 0f, -0.35f), 10000000); // inside the zone, beside the near pole
            c.Expect(dummy != null, "a dummy stands in the centre cell");
            if (!SpawnManager.instance.combat) { c.Expect(false, "a wave is running"); yield break; }
            int wave = Mathf.Max(1, GameHelpers.Level);

            foreach (int rate in new[] { 15, 5 })
            {
                int mana0 = rm.mana;
                float s0 = trap.Record.Get("strikes"), h0 = trap.Record.Get("hits"), d0 = trap.Record.Get("damage");
                trap.SetRate(rate);
                yield return c.Wait(2f);
                float strikes = trap.Record.Get("strikes") - s0, hits = trap.Record.Get("hits") - h0, damage = trap.Record.Get("damage") - d0;
                int spent = mana0 - rm.mana;
                c.Info($"{rate} mana/s for 2 s: {strikes} strikes, {hits} hits, {damage} damage, {spent} mana (incl. regeneration)");
                c.ExpectNear(strikes, 8f, 2f, $"{rate} mana/s: about 4 strikes a second");
                c.Expect(hits >= strikes, "every strike hits the dummy in the centre");
                c.ExpectNear(spent, 2f * rate - 4f, 6f, $"{rate} mana per second (minus 2/s regeneration)");
                c.ExpectNear(damage / Mathf.Max(1f, hits), LightningTrapLogic.Damage(wave, rate), 1f, $"base damage per strike = Damage({wave}, {rate})");
                if (rate == 15)
                {
                    // Close-up, slowed down: arcs, bolts, sparks and the X-ray flash on the dummy.
                    DevConsole.Execute($"zoom 2.5 {trap.Center.x:0} {trap.Center.z:0}");
                    yield return c.Wait(0.5f);
                    Time.timeScale = 0.08f;
                    c.Shot("rate15_wide");
                    yield return new WaitForSecondsRealtime(0.2f);
                    Time.timeScale = Scenarios.SpeedUp;
                    Vector3 d = dummy.transform.position;
                    DevConsole.Execute($"zoom 1 {d.x.ToString(System.Globalization.CultureInfo.InvariantCulture)} {d.z.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                    yield return c.Wait(0.5f);
                    Time.timeScale = 0.08f;
                    for (int i = 0; i < 3; i++)
                    {
                        c.Shot("rate15_" + i);
                        yield return new WaitForSecondsRealtime(0.35f);
                    }
                    Time.timeScale = Scenarios.SpeedUp;
                    // The popup with its slider.
                    DevConsole.Execute($"zoom 4 {trap.Center.x:0} {trap.Center.z:0}");
                    DevConsole.Execute($"towerui {Mathf.RoundToInt(trap.CellWorld(4, 4).x)} {Mathf.RoundToInt(trap.CellWorld(4, 4).z)}");
                    yield return c.Wait(0.4f);
                    c.Shot("popup");
                    yield return c.Wait(0.4f);
                    DevConsole.Execute("closeui");
                }
            }

            trap.SetRate(0);
            int mana1 = rm.mana;
            float s1 = trap.Record.Get("strikes");
            yield return c.Wait(1f);
            c.Expect(trap.Record.Get("strikes") == s1 && rm.mana >= mana1, "at 0 mana per second: no strikes, no mana");

            rm.SpendMana(rm.mana);
            trap.SetRate(15);
            float s2 = trap.Record.Get("strikes");
            yield return c.Wait(1f);
            c.Expect(trap.Record.Get("strikes") - s2 <= 1 && !trap.Powered, "without mana it stays silent");
            c.Info(trap.Status);
            trap.SetRate(0);
        }
    }


    /// <summary>
    /// The three Wandering Merchants: their sections (wagon cells blocked), stock and prices, the shop window per skin,
    /// selling (with the confirmation), buying (the final purchase asks first), packing up and the closed wagon.
    /// </summary>
    internal sealed class MerchantScenario : Scenario
    {
        public override string Name => "poi-merchant";
        public override string Description => "Wandering Merchants: stock, prices, buying, selling, closing, the shop skins and wagons.";
        public override bool Permanent => true;

        private static string F(float v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>Zooms onto a merchant's wagon (its front column, nearer the camera).</summary>
        private static void ZoomOn(Merchant m, float size)
        {
            Vector3 p = m.FootprintCentre;
            DevConsole.Execute($"zoom {F(size)} {F(p.x)} {F(p.z)}");
        }

        public override IEnumerator Run(ScenarioContext c)
        {
            c.Expect(PoiDirector.Enabled, "Points of Interest are on (Treasure Run)");
            if (!PoiDirector.Enabled) yield break;
            if (GameHelpers.Level < 8) DevConsole.Execute("level 8");
            // Towers in play, so several towers have ascensions to offer.
            foreach (TowerType t in new[] { TowerType.Crossbow, TowerType.Morter, TowerType.TeslaCoil, TowerType.Frost, TowerType.Obelisk })
                TowerManager.instance.towersUsed.Add(t);

            var merchants = new Dictionary<MerchantKind, Merchant>();
            var shapes = new SectionExits?[] { SectionExits.Top, SectionExits.Left | SectionExits.Right, SectionExits.All };
            int k = 0;
            foreach (MerchantKind kind in new[] { MerchantKind.Normal, MerchantKind.Legendary, MerchantKind.Ascension })
            {
                var sites = new List<Merchant>();
                PoiDef def = PoiRegistry.Get(Merchant.IdOf(kind));
                for (int i = 0; i < 3 && sites.Count == 0; i++) yield return PoiScenarios.Expand(c, shapes[k % shapes.Length], def, sites);
                k++;
                Merchant m = sites.FirstOrDefault();
                if (m == null) continue;
                merchants[kind] = m;
                PoiScenarios.CheckSection(c, m);
                c.Info($"{def.Name}: {m.Status}");

                // The wagon's cells are blocked for building.
                int blocked = 0;
                foreach (var (x, z) in MerchantStyle.Footprint(MerchantStyle.CornerOf(m.Layout)))
                {
                    Vector3 p = m.CellWorld(x, z);
                    if (c.BuildAt("CrossbowTower", Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.z)) == null) blocked++;
                }
                c.Expect(blocked == 4, $"{def.Name}: the four wagon cells can't be built on ({blocked}/4)");

                // Stock by kind, prices by the formula.
                List<Treasures.TreasureDef> stock = m.Stock;
                c.Expect(stock.Count == MerchantLogic.Offers, $"{def.Name}: {stock.Count} offers");
                c.Expect(stock.Distinct().Count() == stock.Count, "offers are distinct");
                if (kind == MerchantKind.Legendary) c.Expect(stock.All(t => t.Rarity == Treasures.Rarity.Legendary && !Merchant.IsAscension(t)), "only Legendary treasures");
                else if (kind == MerchantKind.Ascension)
                {
                    c.Expect(stock.All(Merchant.IsAscension), "only ascensions");
                    c.Expect(stock.All(t => Ascensions.AscensionTargets.InPlay(Ascensions.AscensionRegistry.ForTreasure(t).Target)),
                             "only ascensions of towers in play: " + string.Join(", ", stock.Select(t => Ascensions.AscensionRegistry.ForTreasure(t).TargetName)));
                    c.Info($"Mana Siphon in play: {Ascensions.AscensionTargets.InPlay(Ascensions.AscensionTarget.ManaSiphon)} (not unlocked in this run)");
                }
                else c.Expect(stock.All(t => !Merchant.IsAscension(t)), "no ascensions at the Normal merchant");
                int wave = Mathf.Max(1, GameHelpers.Level);
                for (int i = 0; i < stock.Count; i++)
                {
                    int expected = MerchantLogic.Price(kind, (int)stock[i].Rarity, wave, Merchant.IsAscension(stock[i]), i == m.DiscountSlot);
                    c.Expect(m.PriceOf(i) == expected, $"slot {i} ({stock[i].Name}, {stock[i].Rarity}{(i == m.DiscountSlot ? ", discounted" : "")}): {m.PriceOf(i)}g = {expected}g");
                }
                ZoomOn(m, 2.6f);
                yield return c.Wait(0.4f);
                c.Shot($"wagon_{Merchant.SkinOf(kind)}_open");
                yield return c.Wait(0.3f);
            }
            yield return PoiScenarios.OpenButtons(c);
            DevConsole.Execute("zoom 10");
            ResourceManager.instance.AddMoney(200000);

            // ---- Normal: sell, buy all three (the last one asks first), the merchant packs up.
            if (merchants.TryGetValue(MerchantKind.Normal, out Merchant normal))
            {
                Treasures.TreasureDef whetstone = Treasures.TreasureRegistry.Get("whetstone");
                Treasures.TreasureInventory.Grant(whetstone);
                c.Expect(Treasures.TreasureSale.WhyNot(Treasures.TreasureRegistry.Get("loan_shark")) != null, "Loan Shark (gold on pickup) can't be sold");
                c.Expect(Treasures.TreasureSale.WhyNot(Treasures.TreasureRegistry.Get("treasure_hoard")) != null, "Treasure Hoard can't be sold");
                MerchantShopUI.Open(normal);
                yield return c.Wait(0.4f);
                c.Expect(MerchantShopUI.IsOpen, "the shop window opens");
                string hit = MerchantShopUI.TopHitAt("Close");
                c.Expect(hit == "Close", $"a click on the X reaches its button (hits '{hit}')");
                c.Shot("shop_normal");
                yield return c.Wait(0.3f);

                int count = Treasures.TreasureInventory.CountOf("whetstone"), gold = GameHelpers.Gold, sellPrice = normal.SellPriceOf(whetstone);
                c.Expect(sellPrice == MerchantLogic.SellPrice(MerchantKind.Normal, 0, Mathf.Max(1, GameHelpers.Level), false), $"Whetstone sells for {sellPrice}g");
                MerchantShopUI.Press("sell whetstone");
                yield return c.Wait(0.1f);
                c.Expect(Treasures.TreasureInventory.CountOf("whetstone") == count, "the first press only asks");
                MerchantShopUI.Press("sell whetstone");
                yield return c.Wait(0.2f);
                c.Expect(Treasures.TreasureInventory.CountOf("whetstone") == count - 1, "the second press sells");
                c.Expect(GameHelpers.Gold - gold >= sellPrice, $"gold +{GameHelpers.Gold - gold} (sale {sellPrice})");

                int d = normal.DiscountSlot;
                var order = new List<int> { d };
                order.AddRange(Enumerable.Range(0, normal.Stock.Count).Where(i => i != d));
                for (int n = 0; n < order.Count; n++)
                {
                    int slot = order[n], price = normal.PriceOf(slot);
                    float spent = normal.Record.Get("spent"); // gold can't tell: some treasures pay out on pickup
                    bool last = n == order.Count - 1;
                    MerchantShopUI.Press("buy " + slot);
                    yield return c.Wait(0.15f);
                    if (last)
                    {
                        c.Expect(!normal.Bought(slot) && !normal.Closed, "the last offer asks first (the shop would close)");
                        c.Shot("shop_normal_last");
                        yield return c.Wait(0.3f);
                        MerchantShopUI.Press("buy " + slot);
                        yield return c.Wait(0.15f);
                    }
                    c.Expect(normal.Bought(slot), $"bought slot {slot} ({normal.Stock[slot].Name})");
                    c.Expect(Mathf.RoundToInt(normal.Record.Get("spent") - spent) == price, $"paid {normal.Record.Get("spent") - spent}g = {price}g");
                    c.Expect(Treasures.TreasureInventory.Owns(normal.Stock[slot].Id), "the treasure is owned");
                    c.Expect(normal.Closed == last, last ? "the merchant packs up after the last offer" : "the merchant stays");
                }
                yield return c.Wait(0.3f);
                c.Expect(!MerchantShopUI.IsOpen, "the shop window closed with the merchant");
                MerchantShopUI.Open(normal);
                c.Expect(!MerchantShopUI.IsOpen, "a closed merchant's shop doesn't open");
                var corner = MerchantStyle.CornerOf(normal.Layout);
                Vector3 p = normal.CellWorld(corner.x, corner.z);
                c.Expect(c.BuildAt("CrossbowTower", Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.z)) == null, "the closed wagon still blocks its cells");
                ZoomOn(normal, 2.6f);
                yield return c.Wait(1.4f);
                c.Shot("wagon_normal_closed");
                yield return c.Wait(0.3f);
                DevConsole.Execute("zoom 10");
            }

            // ---- Legendary: one purchase, the merchant stays.
            if (merchants.TryGetValue(MerchantKind.Legendary, out Merchant legendary))
            {
                MerchantShopUI.Open(legendary);
                yield return c.Wait(0.4f);
                c.Shot("shop_legendary");
                yield return c.Wait(0.3f);
                MerchantShopUI.Press("buy 0");
                yield return c.Wait(0.2f);
                c.Expect(legendary.Bought(0) && !legendary.Closed, "Legendary: bought one, the merchant stays");
                MerchantShopUI.Close();
            }

            // ---- Ascension: the first purchase asks, then the merchant leaves.
            if (merchants.TryGetValue(MerchantKind.Ascension, out Merchant ascension))
            {
                MerchantShopUI.Open(ascension);
                yield return c.Wait(0.4f);
                c.Shot("shop_ascension");
                yield return c.Wait(0.3f);
                MerchantShopUI.Press("buy 1");
                yield return c.Wait(0.15f);
                c.Expect(!ascension.Bought(1), "Ascension: the first press asks (the merchant leaves after one purchase)");
                MerchantShopUI.Press("buy 1");
                yield return c.Wait(0.3f);
                c.Expect(ascension.Bought(1) && ascension.Closed, "Ascension: bought one, the merchant packs up");
                Ascensions.AscensionDef bought = Ascensions.AscensionRegistry.ForTreasure(ascension.Stock[1]);
                if (bought != null) Ascensions.AscensionRegistry.Revoke(bought);
                ZoomOn(ascension, 2.6f);
                yield return c.Wait(0.25f);
                Time.timeScale = 0.1f;
                c.Shot("wagon_ascension_closing");
                yield return c.Wait(0.3f);
                Time.timeScale = Scenarios.SpeedUp;
                yield return c.Wait(1.5f);
                c.Shot("wagon_ascension_closed");
                yield return c.Wait(0.3f);
            }
            MerchantShopUI.Close();
        }
    }

    /// <summary>
    /// The Soul Urn: its cell is blocked, its size follows the wave, kills in its reach charge it (and only those), the
    /// popup shows the fill, and each reward works: gold, a chest, and a soulfire rune that speeds up the tower on it and,
    /// through a War Blimp's Radar standing on it, the tower on the blimp's deck.
    /// </summary>
    internal sealed class SoulUrnScenario : Scenario
    {
        public override string Name => "poi-soul-urn";
        public override string Description => "Soul Urn: blocked cell, capacity, souls from kills in reach, popup, gold / chest / rune rewards, the rune on a War Blimp deck.";
        public override bool Permanent => true;

        private static string F(float v) => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

        private static IEnumerator NewUrn(ScenarioContext c, List<SoulUrn> into)
        {
            // A level-up draw after the last wave can hold the Expand buttons back: wait until one is open.
            for (int i = 0; i < 4 && PoiScenarios.OpenMarker() == null; i++) yield return PoiScenarios.OpenButtons(c);
            for (int i = 0; i < 3 && into.Count == 0; i++) yield return PoiScenarios.Expand(c, SectionExits.Top, PoiRegistry.Get(SoulUrnLogic.Id), into);
            yield return c.Wait(0.5f);
        }

        /// <summary>The build prefab of a built tower.</summary>
        private static GameObject PrefabOf(GameObject built)
        {
            BuildButtonUI button = Resources.FindObjectsOfTypeAll<BuildButtonUI>()
                .FirstOrDefault(btn => btn.gameObject.scene.IsValid() && btn.tower != null && built != null && built.name.StartsWith(btn.tower.name));
            return button != null ? button.tower : null;
        }

        public override IEnumerator Run(ScenarioContext c)
        {
            c.Expect(PoiDirector.Enabled, "Points of Interest are on (Treasure Run)");
            if (!PoiDirector.Enabled) yield break;
            DevConsole.Execute("god");
            if (GameHelpers.Level < 12) DevConsole.Execute("level 12");

            // ----- the first urn: charging, popup, gold
            var urns = new List<SoulUrn>();
            yield return NewUrn(c, urns);
            SoulUrn urn = urns.FirstOrDefault();
            if (urn == null) yield break;
            PoiScenarios.CheckSection(c, urn);
            Vector3 g = urn.Ground;
            int gx = Mathf.RoundToInt(g.x), gz = Mathf.RoundToInt(g.z);
            var cell = SoulUrnStyle.CellOf(urn.Layout);
            c.Expect(!urn.Layout.IsPath(cell.x, cell.z), "the urn stands beside the path");
            c.Expect(c.BuildAt("CrossbowTower", gx, gz) == null, "the urn's cell can't be built on");
            int wave = Mathf.Max(1, GameHelpers.Level);
            c.Expect(urn.Capacity > 0, $"its size is set when the wave starts ({urn.Capacity} souls at wave {wave}, lane share {F(urn.Record.Get("share"))})");
            c.Expect(urn.Capacity == SoulUrnLogic.Capacity(wave, urn.Record.Get("share")), "size = Capacity(wave, lane share)");

            // Kills: four dummies on the section's path (in reach), one by the castle (out of reach).
            int before = urn.Souls;
            var inReach = new List<Enemy>();
            SectionLayout l = urn.Layout;
            for (int x = 0; x < SectionLayout.Size && inReach.Count < 4; x++)
                for (int z = 0; z < SectionLayout.Size && inReach.Count < 4; z++)
                    if (l.IsPath(x, z) && (x + z) % 2 == 0) inReach.Add(c.Dummy(urn.CellWorld(x, z), 5));
            Vector3 castle = GameHelpers.CastlePosition;
            Enemy far = SoulUrnLogic.InReach(castle - g) ? null : c.Dummy(castle + new Vector3(0f, 0f, -1f), 5);
            var victims = new List<Enemy>(inReach);
            if (far != null) victims.Add(far);
            foreach (Enemy e in victims.Where(e => e != null))
                e.TakeDamage(TowerType.Global, 1000000, 1, 1, 1, 0f, 0f, 0f, 0f, 0f, 0f);
            yield return c.Wait(0.3f);
            DevConsole.Execute($"zoom 3.5 {F(g.x)} {F(g.z)}");
            Time.timeScale = 0.15f;
            yield return new WaitForSecondsRealtime(0.25f);
            c.Shot("souls_flying");
            Time.timeScale = Scenarios.SpeedUp;
            yield return c.Wait(2.5f);
            int gained = urn.Souls - before;
            c.Info($"souls {before} -> {urn.Souls} ({inReach.Count} dummies in reach{(far != null ? ", one out of reach" : "")}; wave monsters may add more)");
            c.Expect(gained >= inReach.Count, "every kill in reach sent a soul");
            c.Expect(urn.InFlight == 0, "the souls have arrived");

            // Many souls at once, close up, then the popup.
            urn.AddSouls(Mathf.Min(12, urn.Capacity - urn.Souls - 1));
            yield return c.Wait(0.4f);
            Time.timeScale = 0.15f;
            yield return new WaitForSecondsRealtime(0.25f);
            c.Shot("souls_many");
            Time.timeScale = Scenarios.SpeedUp;
            yield return c.Wait(2f);
            DevConsole.Execute($"towerui {gx} {gz}");
            yield return c.Wait(1.2f);
            LineRenderer reach = urn.GetComponentsInChildren<LineRenderer>(true).FirstOrDefault(lr => lr.name == "TR_UrnReach");
            c.Expect(reach != null && reach.enabled && reach.positionCount == 61, "the popup shows the urn's reach as a circle");
            if (reach != null) c.ExpectNear(new Vector2(reach.GetPosition(0).x - g.x, reach.GetPosition(0).z - g.z).magnitude, SoulUrnLogic.Radius, 0.01f, "the circle's radius");
            DevConsole.Execute("zoom 7.5");
            yield return c.Wait(0.4f);
            c.Shot("popup");
            DevConsole.Execute("closeui");
            yield return c.Wait(0.5f);
            c.Expect(reach == null || !reach.enabled, "the circle goes away with the popup");

            int gold0 = GameHelpers.Gold;
            int urnWave = Mathf.RoundToInt(urn.Record.Get("wave"));
            c.Expect(urnWave == wave, $"the urn remembers its first wave ({urnWave})");
            int expectedGold = SoulUrnLogic.Gold(urnWave);
            urn.ForceFill(UrnReward.Gold);
            yield return new WaitForSeconds(0.97f);
            Time.timeScale = 0.1f;
            yield return new WaitForSecondsRealtime(0.15f);
            c.Shot("burst");
            Time.timeScale = Scenarios.SpeedUp;
            yield return c.Wait(1.5f);
            c.Expect(urn.Full && urn.Reward == UrnReward.Gold, $"full, reward gold: {urn.Status}");
            c.Expect(GameHelpers.Gold - gold0 >= expectedGold, $"paid {expectedGold} gold (gold {gold0} -> {GameHelpers.Gold})");
            GameObject free = c.BuildAt("CrossbowTower", gx, gz);
            c.Expect(free != null, "the cell is free once the urn is gone");
            if (free != null) Object.Destroy(free);

            // ----- the second urn: a chest
            urns.Clear();
            yield return NewUrn(c, urns);
            SoulUrn chestUrn = urns.FirstOrDefault();
            if (chestUrn != null)
            {
                int chests = Object.FindObjectsOfType<TreasureChest>().Length;
                chestUrn.ForceFill(UrnReward.Chest);
                yield return c.Wait(1.5f);
                TreasureChest chest = Object.FindObjectsOfType<TreasureChest>().OrderBy(t => (t.transform.position - chestUrn.Ground).sqrMagnitude).FirstOrDefault();
                c.Expect(Object.FindObjectsOfType<TreasureChest>().Length == chests + 1 && chest != null &&
                         (chest.transform.position - chestUrn.Ground).magnitude < 1f, "a treasure chest stands where the urn was");
                DevConsole.Execute($"zoom 3 {F(chestUrn.Ground.x)} {F(chestUrn.Ground.z)}");
                yield return c.Wait(0.4f);
                c.Shot("chest");
                yield return c.Wait(0.2f);
                if (chest != null) Object.Destroy(chest.gameObject);
            }

            // ----- the third urn: a rune
            urns.Clear();
            yield return NewUrn(c, urns);
            SoulUrn runeUrn = urns.FirstOrDefault();
            if (runeUrn == null) yield break;
            runeUrn.ForceFill(UrnReward.Rune);
            yield return c.Wait(1.5f);
            Vector3 rg = runeUrn.Ground;
            int rx = Mathf.RoundToInt(rg.x), rz = Mathf.RoundToInt(rg.z);
            SoulRune rune = SoulRune.At(rg);
            c.Expect(rune != null, $"a soulfire rune lies on the cell: {runeUrn.Status}");
            if (rune == null) yield break;
            float bonus = rune.Bonus;
            c.ExpectNear(bonus, (float)SoulUrnLogic.RuneBonus, 0.001f, "its bonus is the fixed +100%");
            DevConsole.Execute($"zoom 2.5 {F(rg.x)} {F(rg.z)}");
            yield return c.Wait(0.6f);
            c.Shot("rune_empty");

            GameObject onRune = c.BuildAt("CrossbowTower", rx, rz);
            GameObject plain = c.BuildNear("CrossbowTower", rg, 2f, 6f);
            c.Expect(onRune != null && plain != null, "a Ballista on the rune and one elsewhere");
            if (onRune == null || plain == null) yield break;
            Tower a = onRune.GetComponent<Tower>(), b = plain.GetComponent<Tower>();
            TowerManager.instance.UpdateAllTowers();
            c.Info($"rune Ballista: {F(a.rps)} s per shot, plain: {F(b.rps)} s (heights {a.heightBonus}/{b.heightBonus})");
            // Percent attack speed adds up: the rune adds +100% of the base rate whatever other bonuses the run has.
            float baseRate = 1f / (float)Balance.TowerData.Get(Balance.TowerKind.Crossbow).Interval;
            c.ExpectNear((1f / a.rps - 1f / b.rps) / baseRate, bonus, 0.05f, $"the tower on the rune gains +{bonus:0%} of the base attack rate (x{b.rps / a.rps:0.00} here)");
            yield return c.Wait(0.8f);
            c.Expect(onRune.GetComponent<RuneShell>() != null && onRune.GetComponentsInChildren<MeshRenderer>().Any(r => r.name == "TR_RuneShell"),
                     "the tower on the rune wears the soulfire shell");
            c.Expect(plain.GetComponent<RuneShell>() == null, "the tower beside it doesn't");
            DevConsole.Execute($"zoom 1.4 {F(rg.x)} {F(rg.z)}");
            yield return c.Wait(0.3f);
            c.Shot("rune_tower");
            yield return c.Wait(0.2f); // the screenshot is taken at the end of the frame
            GameObject ballista = PrefabOf(onRune);
            Object.Destroy(onRune);
            yield return c.Wait(0.2f);

            // ----- a War Blimp's Radar on the rune: the deck's tower gets the bonus too
            c.Ascend(Ascensions.PlaneAscensions.WarBlimpId);
            GameObject radar = c.BuildAt("RadarTower", rx, rz);
            c.Expect(radar != null, "a Radar on the rune");
            if (radar == null) yield break;
            Ascensions.BlimpDeck deck = null;
            for (int i = 0; i < 40 && deck == null; i++)
            {
                yield return c.Wait(0.25f);
                deck = Ascensions.BlimpDeck.All.FirstOrDefault(dk => dk != null && dk.Blimp != null && dk.Blimp.Owner != null && dk.Blimp.Owner.gameObject == radar);
            }
            c.Expect(deck != null, "the Radar's War Blimp has a deck");
            if (deck == null) yield break;
            yield return c.Wait(1.2f);
            c.Expect(RuneDeckGlow.On(deck), "the deck shows the rune");
            c.Expect(ballista != null, "the Ballista prefab");
            if (ballista == null) yield break;
            GameObject passenger = deck.Build(ballista);
            c.Track(passenger);
            yield return c.Wait(0.3f);
            Tower p = passenger != null ? passenger.GetComponent<Tower>() : null;
            if (p != null)
            {
                c.Info($"deck Ballista: {F(p.rps)} s per shot, plain: {F(b.rps)} s");
                c.ExpectNear((1f / p.rps - 1f / b.rps) / baseRate, bonus, 0.05f, "the deck's tower gets the rune's attack speed");
                yield return c.Wait(1f);
                c.Expect(p.GetComponent<RuneShell>() != null, "the deck's tower wears the soulfire shell");
                c.Expect(radar.GetComponent<RuneShell>() != null, "so does the Radar on the rune");
            }
            Time.timeScale = 0.1f;
            Vector3 d = deck.transform.position;
            DevConsole.Execute($"zoom 4 {F((d.x + rg.x) / 2f)} {F((d.z + rg.z) / 2f)}");
            yield return new WaitForSecondsRealtime(0.3f);
            c.Shot("blimp_rune");
            yield return new WaitForSecondsRealtime(0.1f);
            // The deck flies 4.4 up: aim at the ground point behind it on the camera's line of sight.
            d = deck.transform.position;
            Vector3 f = Camera.main != null ? Camera.main.transform.forward : Vector3.down;
            if (f.y < -0.1f) d += f * (d.y / -f.y);
            DevConsole.Execute($"zoom 1.8 {F(d.x)} {F(d.z)}");
            yield return new WaitForSecondsRealtime(0.3f);
            c.Shot("blimp_deck");
            yield return new WaitForSecondsRealtime(0.1f);
            Time.timeScale = Scenarios.SpeedUp;
            c.Info(runeUrn.Status);
        }
    }
}

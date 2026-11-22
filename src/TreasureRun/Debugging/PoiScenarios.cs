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
        public static IEnumerable<Scenario> All => new Scenario[] { new PoiSectionsScenario(), new LightningTrapScenario(), new MerchantScenario() };

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

        /// <summary>Puts a Lightning Trap omen on an open Expand button and uses it; returns the new site (null if none).</summary>
        public static IEnumerator Expand(ScenarioContext c, SectionExits? exits, List<LightningTrap> result) =>
            Expand(c, exits, PoiRegistry.Get(LightningTrapLogic.Id), result);

        /// <summary>Puts an omen of <paramref name="def"/> on an open Expand button and uses it; adds the new site to the list.</summary>
        public static IEnumerator Expand<T>(ScenarioContext c, SectionExits? exits, PoiDef def, List<T> result) where T : PoiSite
        {
            yield return OpenButtons(c);
            TileSpawnLocation marker = OpenMarker();
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

        /// <summary>Checks a built section against its layout: heights and path cells by raycast, waypoints linked.</summary>
        public static void CheckSection(ScenarioContext c, PoiSite site)
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
                    else if (l.Cells[x, z] == SectionCell.Reserved) { if (layer == "Grass" || layer == "Path") wrong++; }
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
                if (c.BuildAt("Ballista", Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.z)) == null) blocked++;
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
                    if (c.BuildAt("Ballista", Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.z)) == null) blocked++;
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
                c.Expect(c.BuildAt("Ballista", Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.z)) == null, "the closed wagon still blocks its cells");
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
}

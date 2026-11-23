using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TreasureRun.Ascensions;
using TreasureRun.Ascensions.Phase3;
using TreasureRun.Castles;
using TreasureRun.Core;
using TreasureRun.Pois;
using TreasureRun.Treasures;
using TreasureRun.UI;
using UnityEngine;

namespace TreasureRun.Debugging
{
    /// <summary>
    /// Wizard's Tower (docs/poi-plan.md §7): the tower's cell, the blocked land, every spell on dummies (exact fixed
    /// damage, targets, slow, drag, minions, gold), damage unchanged by damage treasures, the ley link (a siphon on his
    /// land, mana drained, faster casts) and an ascension that can't be sold.
    /// </summary>
    internal sealed class WizardTowerScenario : Scenario
    {
        public override string Name => "poi-wizard-tower";
        public override string Description => "Wizard's Tower: placement, blocked land, every spell, fixed damage, the ley link, unsellable ascensions.";
        public override bool Permanent => true;

        private static string F(float v) => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

        private readonly List<Enemy> dummies = new List<Enemy>();

        private Enemy Dummy(ScenarioContext c, Vector3 at, int health, int armor = 0, int shield = 0)
        {
            Enemy e = c.Dummy(at, health, armor, shield);
            if (e != null) dummies.Add(e);
            return e;
        }

        /// <summary>Removes the dummies of the last test (and anything else left on the map).</summary>
        private IEnumerator Clear(ScenarioContext c)
        {
            foreach (Enemy e in dummies)
                if (e != null)
                {
                    SpawnManager.instance.currentEnemies.Remove(e);
                    Object.Destroy(e.gameObject);
                }
            dummies.Clear();
            yield return c.Wait(0.3f);
        }

        /// <summary>Centres the camera on a point above the ground (along the camera's line of sight, as zoom aims at the ground).</summary>
        private static void Aim(Vector3 p, float size)
        {
            Vector3 f = Camera.main != null ? Camera.main.transform.forward : Vector3.down;
            if (f.y < -0.1f) p += f * (p.y / -f.y);
            DevConsole.Execute($"zoom {F(size)} {F(p.x)} {F(p.z)}");
        }

        private static IEnumerator Slow(ScenarioContext c, string shot, float delay)
        {
            yield return c.Wait(delay);
            Time.timeScale = 0.12f;
            yield return new WaitForSecondsRealtime(0.25f);
            c.Shot(shot);
            yield return new WaitForSecondsRealtime(0.1f);
            Time.timeScale = Scenarios.SpeedUp;
        }

        public override IEnumerator Run(ScenarioContext c)
        {
            c.Expect(PoiDirector.Enabled, "Points of Interest are on (Treasure Run)");
            if (!PoiDirector.Enabled) yield break;
            DevConsole.Execute("god");
            if (GameHelpers.Level < 10) DevConsole.Execute("level 10");

            // ----- placement
            var towers = new List<WizardTower>();
            for (int i = 0; i < 4 && PoiScenarios.OpenMarker() == null; i++) yield return PoiScenarios.OpenButtons(c);
            for (int i = 0; i < 3 && towers.Count == 0; i++) yield return PoiScenarios.Expand(c, SectionExits.Top, PoiRegistry.Get(WizardTowerLogic.Id), towers);
            WizardTower t = towers.FirstOrDefault();
            c.Expect(t != null, "a Wizard's Tower was placed");
            if (t == null) yield break;
            yield return PoiScenarios.OpenButtons(c); // the wave its tile started ends: the spells are tested in peace
            PoiScenarios.CheckSection(c, t);
            var (cx, cz) = WizardTowerStyle.CellOf(t.Layout);
            bool nearPath = false;
            for (int dx = -1; dx <= 1; dx++) for (int dz = -1; dz <= 1; dz++) if (t.Layout.IsPath(cx + dx, cz + dz)) nearPath = true;
            c.Expect(cx >= 1 && cx <= 5 && cz >= 1 && cz <= 5 && !nearPath, $"the tower stands off the edge and away from the path (cell {cx},{cz})");
            Vector2Int g = t.Grid;
            c.Expect(c.BuildAt("CrossbowTower", g.x, g.y) == null, "the tower's cell can't be built on");

            // ----- the land
            int refused = 0, landCells = 0;
            for (int x = 0; x < SectionLayout.Size; x++)
                for (int z = 0; z < SectionLayout.Size; z++)
                {
                    if (!t.Layout.IsGrass(x, z)) continue;
                    Vector3 w = t.CellWorld(x, z);
                    landCells++;
                    if (c.BuildAt("CrossbowTower", Mathf.RoundToInt(w.x), Mathf.RoundToInt(w.z)) == null) refused++;
                }
            c.Expect(landCells > 10 && refused == landCells, $"nothing can be built on his land ({refused} of {landCells} grass cells refused)");
            Vector3 outside = t.Center + new Vector3(4.5f, 0f, 0f);
            c.Expect(WizardLand.Refuses(t.Center + new Vector3(3.4f, 0.4f, 0f), null) && !WizardLand.Refuses(outside, null), "the land ends at the section's edge");
            c.Expect(t.GetComponentsInChildren<MeshRenderer>(true).Any(r => r.name == "TR_WizardShine"), "the grass shimmers blue");
            c.Expect(t.GetComponentsInChildren<ParticleSystem>(true).Any(p => p.name == "TR_WizardMotes" && p.isPlaying), "blue motes rise from it");
            DevConsole.Execute("active UniverseLibCanvas 0");
            c.Expect(t.Gadgets != null && t.Gadgets.ChimneySegments.Count == WizardGadgets.Segments && t.Gadgets.Rotor != null, "the tower has its gadgets");
            float rot0 = t.Gadgets != null && t.Gadgets.Rotor != null ? t.Gadgets.Rotor.localEulerAngles.y : 0f;
            Aim(t.TowerBase + Vector3.up * 2.1f, 2.8f);
            yield return c.Wait(0.6f);
            c.Expect(t.Gadgets != null && t.Gadgets.Rotor != null && Mathf.Abs(Mathf.DeltaAngle(rot0, t.Gadgets.Rotor.localEulerAngles.y)) > 5f, "the anemometer turns by itself");
            c.Shot("tower");
            yield return c.Wait(0.2f); // the screenshot is taken at the end of the frame
            DevConsole.Execute($"zoom 6 {F(t.Center.x)} {F(t.Center.z)}");
            yield return c.Wait(0.5f);
            c.Shot("land");
            yield return c.Wait(0.2f);

            // ----- popup and reach
            DevConsole.Execute($"towerui {g.x} {g.y}");
            yield return c.Wait(1.2f);
            LineRenderer reach = t.GetComponentsInChildren<LineRenderer>(true).FirstOrDefault(lr => lr.name == "TR_WizardReach");
            c.Expect(reach != null && reach.enabled, "the popup shows his reach");
            if (reach != null) c.ExpectNear(new Vector2(reach.GetPosition(0).x - t.TowerBase.x, reach.GetPosition(0).z - t.TowerBase.z).magnitude, WizardTowerLogic.Range, 0.01f, "the reach's radius");
            DevConsole.Execute("zoom 9");
            yield return c.Wait(0.4f);
            c.Shot("popup");
            yield return c.Wait(0.2f);
            DevConsole.Execute("closeui");
            yield return c.Wait(0.4f);

            int wave = Mathf.Max(1, GameHelpers.Level);
            var path = new List<Vector3>();
            for (int x = 0; x < SectionLayout.Size; x++)
                for (int z = 0; z < SectionLayout.Size; z++)
                    if (t.Layout.IsPath(x, z) && WizardTowerLogic.InRange(t.CellWorld(x, z) - t.TowerBase)) path.Add(t.CellWorld(x, z));
            path = path.OrderBy(p => (p - t.Center).sqrMagnitude).ToList();
            c.Expect(path.Count >= 5, $"{path.Count} path cells in his reach");
            if (path.Count < 5) yield break;
            Vector3 mid = (t.TowerBase + path[2]) / 2f;
            DevConsole.Execute($"zoom 3.2 {F(mid.x)} {F(mid.z)}");

            // ----- fireball: the largest group, exact blast and burn
            const int big = 1000000;
            Vector3 p0 = path[0];
            var group = new[] { Dummy(c, p0, big), Dummy(c, p0 + new Vector3(0.3f, 0f, 0f), big), Dummy(c, p0 + new Vector3(0f, 0f, 0.3f), big) };
            Enemy lone = Dummy(c, path[path.Count - 1], big);
            yield return c.Wait(0.2f);
            t.Forced = WizardSpell.Fireball;
            c.Expect(t.TryCast(), "fireball cast");
            c.Expect(t.Charging && !t.TryCast(), "it winds up first (no second spell meanwhile)");
            float charge = WizardGadgets.ChargeTime(WizardSpell.Fireball);
            float bulge = 0f;
            bool shot = false;
            Aim(t.Emitter(WizardSpell.Fireball) - Vector3.up * 1.6f, 2.2f);
            for (float w = 0f; w < charge; w += Time.deltaTime)
            {
                foreach (Transform seg in t.Gadgets.ChimneySegments) bulge = Mathf.Max(bulge, seg.localScale.x);
                if (!shot && w > charge * 0.45f)
                {
                    shot = true;
                    Time.timeScale = 0.05f;
                    yield return new WaitForSecondsRealtime(0.2f);
                    c.Shot("fireball_charge");
                    yield return new WaitForSecondsRealtime(0.1f);
                    Time.timeScale = Scenarios.SpeedUp;
                }
                yield return null;
            }
            DevConsole.Execute($"zoom 3.2 {F(mid.x)} {F(mid.z)}");
            c.Expect(bulge > 1.4f, $"a bulge travelled up the chimney (widest {F(bulge)}x)");
            yield return null;
            c.Expect(Object.FindObjectsOfType<Transform>().Any(x => x.name == "TR_" + WizardSpells.FireballModel), "the fireball is the Magma Lobber's ball");
            float flight = 0.55f + 0.05f * Vector3.Distance(t.Emitter(WizardSpell.Fireball), p0);
            yield return Slow(c, "fireball_flying", flight * 0.5f);
            yield return Slow(c, "fireball_blast", flight * 0.5f + 0.08f);
            yield return c.Wait(2.5f);
            int fireExpected = WizardTowerLogic.FireballHealth * (WizardTowerLogic.FireballDamage(wave) + WizardTowerLogic.BurnTicks * WizardTowerLogic.BurnTick(wave));
            foreach (Enemy e in group)
                if (e != null) c.Expect(big - e.health == fireExpected, $"the group took the blast and the burn: {big - e.health} (expected {fireExpected})");
            c.Expect(lone != null && lone.health == big, "the lone dummy away from the group wasn't hit");
            yield return Clear(c);

            // ----- lightning: the most shield, exact
            Enemy plain = Dummy(c, path[0], big);
            Enemy shielded = Dummy(c, path[2], big, 0, big);
            yield return c.Wait(0.2f);
            t.Forced = WizardSpell.Lightning;
            c.Expect(t.TryCast(), "lightning cast");
            Aim(t.Emitter(WizardSpell.Lightning) - Vector3.up * 0.9f, 1.8f);
            yield return Slow(c, "lightning_charge", 0.35f);
            c.Expect(Object.FindObjectsOfType<LineRenderer>().Any(l => l.name == "TR_RodSpiral"), "lightning spirals up the rod first");
            DevConsole.Execute($"zoom 3.2 {F(mid.x)} {F(mid.z)}");
            yield return Slow(c, "lightning", 0.27f);
            yield return c.Wait(0.3f);
            int boltShield = WizardTowerLogic.LightningDamage(wave) * WizardTowerLogic.LightningShield;
            c.Expect(shielded != null && big - shielded.shield == boltShield, $"it struck the shielded dummy for {boltShield} shield damage ({(shielded != null ? big - shielded.shield : -1)})");
            c.Expect(plain != null && plain.health == big, "not the one without a shield");
            yield return Clear(c);

            // ----- damage treasures don't change it
            string[] boosts = { "first_strike", "executioner", "hex_of_frailty", "master_of_elements" };
            var granted = new List<string>();
            foreach (string id in boosts)
            {
                TreasureDef def = TreasureRegistry.Get(id);
                if (def == null || TreasureInventory.Owns(id)) continue;
                TreasureInventory.Grant(def);
                granted.Add(id);
            }
            Enemy boosted = Dummy(c, path[1], big);
            yield return c.Wait(0.2f);
            t.Forced = WizardSpell.Lightning;
            t.TryCast();
            yield return c.Wait(0.9f);
            int boltHealth = WizardTowerLogic.LightningDamage(wave) * WizardTowerLogic.LightningHealth;
            c.Expect(boosted != null && big - boosted.health == boltHealth && boosted.burn == 0,
                     $"with {string.Join(", ", granted)}: still exactly {boltHealth} ({(boosted != null ? big - boosted.health : -1)}), no burn added");
            Enemy trapTarget = Dummy(c, path[3], big);
            Combat.FixedHit(trapTarget, 10, LightningTrapLogic.HealthMult, LightningTrapLogic.ArmorMult, LightningTrapLogic.ShieldMult);
            c.Expect(trapTarget != null && big - trapTarget.health == 10 * LightningTrapLogic.HealthMult, "a Lightning Trap strike is exact too");
            foreach (string id in granted) TreasureInventory.Revoke(id);
            StatLayer.Refresh();
            yield return Clear(c);

            // ----- ice spikes: three different targets, damage and a slow
            var iced = new List<Enemy> { Dummy(c, path[0], big), Dummy(c, path[2], big), Dummy(c, path[4], big) };
            yield return c.Wait(0.2f);
            t.Forced = WizardSpell.IceSpikes;
            c.Expect(t.TryCast(), "ice spikes cast");
            Aim(t.Emitter(WizardSpell.IceSpikes), 1.8f);
            yield return Slow(c, "ice_charge", 0.4f);
            DevConsole.Execute($"zoom 3.2 {F(mid.x)} {F(mid.z)}");
            yield return Slow(c, "ice_spikes", 0.45f);
            yield return c.Wait(1f);
            int iceExpected = WizardTowerLogic.IceDamage(wave) * WizardTowerLogic.IceMult;
            c.Expect(iced.All(e => e != null && big - e.health == iceExpected), $"each of the three took one spike ({iceExpected}): " +
                     string.Join(", ", iced.Select(e => e != null ? (big - e.health).ToString() : "-")));
            c.Expect(iced.All(e => e != null && e.currentSlowPercentage > 0.15f), $"and is slowed ({string.Join(", ", iced.Select(e => e != null ? F(e.currentSlowPercentage) : "-"))})");
            yield return Clear(c);

            // ----- tornado: drags a monster back up the path, not a boss
            Enemy dragged = Dummy(c, path[0], big);
            Enemy boss = Dummy(c, path[0] + new Vector3(0.25f, 0f, 0.25f), big);
            if (boss != null) Combat.MarkBoss(boss, false);
            yield return c.Wait(0.2f);
            // A still dummy's own distance is never set: measure it from the waypoint ahead of it.
            float PathDistance(Enemy e) => e == null || e.pathfinder == null || e.pathfinder.currentWaypoint == null ? 0f
                : e.pathfinder.currentWaypoint.distanceFromEnd + Vector3.Distance(e.transform.position, e.pathfinder.currentWaypoint.transform.position);
            float d0 = PathDistance(dragged);
            Vector3 b0 = boss != null ? boss.transform.position : Vector3.zero;
            float caught0 = t.Record.Get("caught");
            t.Forced = WizardSpell.Tornado;
            c.Expect(t.TryCast(), "tornado cast");
            yield return Slow(c, "tornado", 1.8f);
            yield return c.Wait(2.6f);
            float d1 = PathDistance(dragged);
            c.Expect(t.Record.Get("caught") > caught0, "the tornado caught the monster");
            c.Expect(d1 - d0 > 1f, $"and carried it back up the path ({F(d0)} -> {F(d1)} from the castle)");
            c.Expect(boss != null && (boss.transform.position - b0).magnitude < 0.05f, "the boss stayed put");
            c.Expect(dragged != null && big - dragged.health > 0, $"it was ground ({big - (dragged != null ? dragged.health : big)} damage)");
            yield return Clear(c);

            // ----- necromancy: monsters slain in the aura rise
            Enemy slain = Dummy(c, path[1], 200);
            Dummy(c, path[path.Count - 1], big); // something for the minion to blow up on
            yield return c.Wait(0.2f);
            float raised0 = t.Record.Get("raised");
            int minions0 = RisenMinion.Active.Count;
            t.Forced = WizardSpell.Necromancy;
            c.Expect(t.TryCast(), "necromancy cast");
            yield return c.Wait(WizardGadgets.ChargeTime(WizardSpell.Necromancy) + 0.1f);
            c.Expect(t.AuraActive, "the aura is up");
            yield return c.Wait(0.5f);
            if (slain != null) slain.TakeDamage(TowerType.Crossbow, big, 1, 1, 1, 0f, 0f, 0f, 0f, 0f, 0f);
            DevConsole.Execute($"zoom 7.5 {F(t.Center.x)} {F(t.Center.z)}");
            yield return Slow(c, "necromancy", 0.3f);
            DevConsole.Execute($"zoom 3.2 {F(mid.x)} {F(mid.z)}");
            c.Expect(t.Record.Get("raised") == raised0 + 1 && RisenMinion.Active.Count > minions0, "the slain monster rose as a minion");
            yield return c.Wait(4f);
            c.Expect(!t.AuraActive, "the aura is gone after 4 seconds");
            yield return Clear(c);

            // ----- goldify: the toughest (not the boss) turns to gold, 25 times its kill gold
            Enemy tough = Dummy(c, path[1], 300, 300, 300);
            Enemy weak = Dummy(c, path[3], 100);
            Enemy goldBoss = Dummy(c, path[2], big, big, big);
            if (goldBoss != null) Combat.MarkBoss(goldBoss, false);
            yield return c.Wait(0.2f);
            int gold0 = GameHelpers.Gold;
            float recorded0 = t.Record.Get("gold");
            t.Forced = WizardSpell.Goldify;
            c.Expect(t.TryCast(), "goldify cast");
            yield return c.Wait(1f);
            c.Expect(tough == null || tough.dead, "the toughest monster is gone");
            c.Expect(weak != null && !weak.dead && goldBoss != null && !goldBoss.dead, "the others (and the boss) are not");
            c.Expect(Object.FindObjectsOfType<GoldStatue>().Length > 0, "a gold statue stands where it was");
            DevConsole.Execute($"zoom 2 {F(path[1].x)} {F(path[1].z)}");
            yield return c.Wait(0.15f);
            c.Shot("goldify_statue");
            yield return c.Wait(1.4f);
            DevConsole.Execute($"zoom 3.2 {F(mid.x)} {F(mid.z)}");
            float paid = t.Record.Get("gold") - recorded0;
            c.Expect(paid >= WizardTowerLogic.GoldifyFactor && Mathf.RoundToInt(paid) % WizardTowerLogic.GoldifyFactor == 0,
                     $"it paid {paid:0} gold: 25 times its kill gold");
            c.Expect(GameHelpers.Gold - gold0 == Mathf.RoundToInt(paid), $"gold {gold0} -> {GameHelpers.Gold}");
            c.Expect(Object.FindObjectsOfType<GoldStatue>().Length == 0, "the statue has turned into coins");
            yield return Clear(c);

            // ----- ascensions can't be sold
            AscensionDef ley = c.Ascend(LeySiphon.Id);
            c.Expect(ley != null && TreasureInventory.Owns(LeySiphon.Id), "Ley Line Network owned");
            if (ley == null) yield break;
            c.Expect(TreasureSale.WhyNot(ley.Treasure) == TreasureSale.AscensionReason && !TreasureSale.Sell(ley.Treasure), "an ascension can't be sold");
            c.Expect(!TreasureInventory.RemoveOne(ley.Treasure) && TreasureInventory.Owns(LeySiphon.Id), "nor removed by any other way");

            // ----- the ley link: siphons on his land, mana drained, faster spells
            // The camera's side first (it looks along world +x, -z).
            Vector2Int[] around = { new Vector2Int(g.x - 1, g.y), new Vector2Int(g.x, g.y + 1), new Vector2Int(g.x + 1, g.y), new Vector2Int(g.x, g.y - 1) };
            GameObject siphon = null;
            foreach (Vector2Int a in around)
                if ((siphon = c.BuildAt("ManaSiphon", a.x, a.y)) != null) break;
            c.Expect(siphon != null, "a ley siphon can be built on his land, next to the tower");
            c.Expect(around.All(a => c.BuildAt("CrossbowTower", a.x, a.y) == null), "a tower still can't");
            yield return c.Wait(1.2f);
            c.Expect(t.Linked && t.LinkedSiphons() == 1, "the tower is linked");
            LeySiphon ls = siphon != null ? siphon.GetComponent<LeySiphon>() : null;
            c.Expect(ls != null && ls.FeedsWizard && ls.Parts.Any(p => p != null && p.name.StartsWith("TR_Ley_feed")), "the siphon has a feed pipe into the tower");
            DevConsole.Execute($"zoom 1.8 {F(t.TowerBase.x)} {F(t.TowerBase.z)}");
            yield return c.Wait(0.4f);
            c.Shot("ley_link");
            yield return c.Wait(0.2f);
            // Between waves the link shows as it will work: powered while the bank holds a second's worth.
            DevConsole.Execute("mana 0");
            yield return c.Wait(0.3f);
            c.Expect(!t.LeyPowered && Mathf.Approximately(t.Interval, WizardTowerLogic.Interval), "between waves with no mana: the link is idle (5 s)");
            DevConsole.Execute("mana 100");
            yield return c.Wait(0.3f);
            c.Expect(t.LeyPowered && Mathf.Approximately(t.Interval, WizardTowerLogic.LeyInterval), "with mana in the bank it is ready (4 s)");
            DevConsole.Execute($"towerui {g.x} {g.y}");
            yield return c.Wait(1f);
            string text = Object.FindObjectsOfType<SimpleUI>().Select(u => u.discriptionText != null ? u.discriptionText.text : "").FirstOrDefault(x => x.Contains("erratic wizard")) ?? "";
            c.Expect(text.Contains(WizardTowerLogic.LeyManaPerSecond + " mana per second"), "the popup says what the link costs: " + text.Replace('\n', ' '));
            DevConsole.Execute("closeui");
            yield return c.Wait(0.3f);
            DevConsole.Execute("wave");
            DevConsole.Execute("mana 500");
            float mana0 = t.Record.Get("mana"), casts0 = t.Record.Get("casts");
            yield return c.Wait(3f);
            float drained = t.Record.Get("mana") - mana0;
            c.Info($"during the wave: {drained:0} mana drained in 3 s, interval {F(t.Interval)} s, {t.Record.Get("casts") - casts0:0} spells");
            c.Expect(drained >= 2 * WizardTowerLogic.LeyManaPerSecond, $"it drains {WizardTowerLogic.LeyManaPerSecond} mana a second during the wave");
            c.Expect(t.LeyPowered && Mathf.Approximately(t.Interval, WizardTowerLogic.LeyInterval), "and casts every 4 seconds");
            yield return PoiScenarios.OpenButtons(c);
            c.Info(t.Status);
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TreasureRun.Ascensions;
using TreasureRun.Core;
using TreasureRun.Pois;
using TreasureRun.UI;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TreasureRun.Debugging
{
    /// <summary>
    /// Laboratory (docs/poi-plan.md §9): the 3x3 structure in the middle with the path round it (its eight cells blocked,
    /// the chamber free); a Ballista built into the chamber through the vanilla build path: counted for the price, inert
    /// (no shots, no XP, no tower count, its colliders off), the lab lit; research at a wave start (+5% attack speed for
    /// every Ballista, the specimen untouched), kept after selling; the sale refunding like any copy; a Radar in the
    /// chamber spawning no plane; a Mana Siphon studied: siphons gather more mana; Haunted Houses tax more; the Lookout's
    /// mark; the popup.
    /// </summary>
    internal sealed class LaboratoryScenario : Scenario
    {
        public override string Name => "poi-laboratory";
        public override string Description => "Laboratory: the structure and chamber, an inert specimen counted for the price, research per wave, buildings yielding more, selling.";
        public override bool Permanent => true;

        private static string F(float v) => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

        private static void Aim(Vector3 p, float size)
        {
            Vector3 f = Camera.main != null ? Camera.main.transform.forward : Vector3.down;
            if (f.y < -0.1f) p += f * (p.y / -f.y);
            DevConsole.Execute($"zoom {F(size)} {F(p.x)} {F(p.z)}");
        }

        private static BuildButtonUI Button(Func<GameObject, bool> match) =>
            Resources.FindObjectsOfTypeAll<BuildButtonUI>().FirstOrDefault(b => b != null && b.gameObject.scene.IsValid() && b.tower != null && match(b.tower));

        private static BuildButtonUI TowerButton(TowerType type) =>
            Button(p => p.GetComponent<Tower>() != null && p.GetComponent<Tower>().towerType == type);

        /// <summary>One placement through the vanilla build path (as a click does it): build mode, the ghost on the cell, Build.</summary>
        private static void Place(BuildButtonUI b, Vector3 at, int price)
        {
            BuildingManager bm = BuildingManager.instance;
            bm.EnterBuildMode(b.tower, price, b.towerFlyweight.priceIncrease, b.myTowerType);
            bm.DisplayGhost(at, "");
            bm.Build();
            bm.ExitBuildMode();
        }

        public override IEnumerator Run(ScenarioContext c)
        {
            c.Expect(PoiDirector.Enabled, "Points of Interest are on (Treasure Run)");
            if (!PoiDirector.Enabled) yield break;
            DevConsole.Execute("god");
            DevConsole.Execute("gold 100000");
            if (GameHelpers.Level < 8) DevConsole.Execute("level 8");
            DevConsole.Execute("active UniverseLibCanvas 0");
            foreach (AscensionTarget t in AscensionTargets.All) LabResearch.Set(t, 0);

            // ----- placement: the lab in the middle, the path round it
            var labs = new List<Laboratory>();
            for (int i = 0; i < 3 && labs.Count == 0; i++) yield return PoiScenarios.Expand(c, SectionExits.All, PoiRegistry.Get(LaboratoryLogic.Id), labs);
            Laboratory lab = labs.FirstOrDefault();
            c.Expect(lab != null, "a Laboratory was placed");
            if (lab == null) yield break;
            Time.timeScale = 1f;
            PoiScenarios.CheckSection(c, lab, LaboratoryLogic.Slot);
            Vector2Int g = lab.Grid;
            int refused = 0;
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                    if ((dx != 0 || dz != 0) && c.BuildAt("CrossbowTower", g.x + dx, g.y + dz) == null) refused++;
            c.Expect(refused == 8, $"the eight cells round the chamber can't be built on ({refused} of 8 refused)");
            c.Expect(lab.Empty && lab.Power == 0f, "the lab stands dark with its chamber empty");
            Aim(lab.Slot + Vector3.up * 0.6f, 3.2f);
            yield return c.Wait(0.3f);
            c.Shot("idle");
            yield return c.Wait(0.3f);

            // ----- a Ballista in the chamber, through the vanilla build path
            BuildButtonUI ballistaButton = TowerButton(TowerType.Crossbow);
            c.Expect(ballistaButton != null, "the Ballista's build button");
            if (ballistaButton == null) yield break;
            TowerFlyweight fly = ballistaButton.towerFlyweight;
            int priceBefore = fly.currentPrice, countBefore = fly.towers.Count, towersBefore = GameEvents.TowerCount;
            // Aimed the way the mouse does: camera rays to points of the chamber's floor. They meet the lab's near boxes
            // first (vanilla showed no ghost there: the bug); the ghost must show in the chamber all the same.
            BuildingManager bm = BuildingManager.instance;
            Camera cam = Camera.main;
            bm.EnterBuildMode(ballistaButton.tower, priceBefore, fly.priceIncrease, ballistaButton.myTowerType);
            int blocked = 0, shown = 0;
            foreach (Vector3 o in new[] { Vector3.zero, new Vector3(0.3f, 0f, 0.3f), new Vector3(-0.3f, 0f, -0.3f), new Vector3(0.3f, 0f, -0.3f), new Vector3(-0.3f, 0f, 0.3f) })
            {
                Ray aim = cam.ScreenPointToRay(cam.WorldToScreenPoint(lab.Slot + o));
                bool onLab = Physics.Raycast(aim, out RaycastHit first, 2000f, bm.buildableMask, QueryTriggerInteraction.Ignore)
                             && first.collider.GetComponentInParent<Laboratory>() == lab;
                if (!onLab) continue;
                blocked++;
                if (LabPatches.SampleChamber(bm, aim) && bm.currentGhost != null && bm.currentGhost.activeSelf
                    && (bm.currentGhost.transform.position - lab.Slot).sqrMagnitude < 0.01f) shown++;
            }
            c.Info($"{blocked} of 5 camera rays to the chamber floor hit the lab's boxes first");
            c.Expect(shown == blocked, $"aimed at the chamber past the lab's boxes, the build ghost shows in it ({shown} of {blocked})");
            if (bm.currentGhost == null || !bm.currentGhost.activeSelf) bm.DisplayGhost(lab.Slot, "");
            bm.Build();
            bm.ExitBuildMode();
            LabSpecimen spec = lab.Specimen;
            Tower studied = spec != null ? spec.GetComponent<Tower>() : null;
            c.Expect(studied != null && studied.towerType == TowerType.Crossbow, "the Ballista went into the chamber as a specimen");
            if (studied == null) yield break;
            c.Expect(fly.towers.Contains(studied) && fly.towers.Count == countBefore + 1 && fly.currentPrice == priceBefore + fly.priceIncrease,
                     $"it counts for the price like any copy (next Ballista {priceBefore}g -> {fly.currentPrice}g)");
            c.Expect(GameEvents.TowerCount == towersBefore && !GameEvents.Towers.Contains(studied), "but not as a tower of the mod (tower counts, neighbours)");
            c.Expect(!studied.enabled && studied.GetComponentsInChildren<Collider>(true).All(x => !x.enabled), "its behaviour and colliders are off");
            c.Expect(c.BuildAt("CrossbowTower", g.x, g.y) == null, "the full chamber can't be built on");
            float specimenRps = studied.rps;

            // It does nothing: a monster right in front of it stays untouched.
            Vector3 front = lab.CellWorld(LaboratoryLogic.Slot.x, LaboratoryLogic.Slot.z - 2);
            Enemy dummy = c.Dummy(front, 5000);
            if (!SpawnManager.instance.combat) DevConsole.Execute("wave");
            yield return c.Wait(4f);
            c.Expect(dummy != null && dummy.health == 5000 && studied.level == 1 && studied.currentTarget == null,
                     $"in 4 s it neither aims nor shoots nor learns (dummy {(dummy != null ? dummy.health : -1)} / 5000, level {studied.level})");
            c.ExpectNear(lab.Power, 1f, 0.01f, "the lab is lit while it studies");
            Aim(lab.Slot + Vector3.up * 0.6f, 3.2f);
            yield return c.Wait(0.3f);
            c.Shot("active");
            yield return c.Wait(0.3f);
            Aim(lab.Slot + Vector3.up * 0.7f, 1.8f);
            yield return c.Wait(0.2f);
            c.Shot("active_close");
            yield return c.Wait(0.3f);

            // ----- research at the start of a wave
            yield return PoiScenarios.OpenButtons(c);
            Tower outside = c.BuildNear("CrossbowTower", lab.Slot, 2.5f, 6f)?.GetComponent<Tower>();
            yield return c.Wait(0.2f);
            c.Expect(outside != null, "a Ballista outside the lab");
            if (outside == null) yield break;
            // Other treasures of earlier scenarios may speed it up too (percent bonuses add up, some follow the tower count):
            // measure the attack speed factor (base interval / interval) and what research adds to it.
            float baseInterval = 60f / outside.rpm;
            float Factor() => baseInterval / outside.rps;
            float rpsBefore = outside.rps, f0 = Factor();
            DevConsole.Execute("wave");
            yield return c.Wait(0.3f);
            c.Expect(LabResearch.Waves(AscensionTarget.Ballista) == 1 && lab.WavesStudied == 1, "a wave start brings a wave of research");
            float d1 = Factor() - f0;
            c.Expect(d1 > 0.0495f && d1 < 0.15f, $"every Ballista attacks 5% faster: +{d1:0.###} on its attack speed factor ({F(rpsBefore)} s -> {F(outside.rps)} s between shots; exactly 0.05 without other multipliers)");
            c.Shot("research");
            yield return c.Wait(0.3f);
            for (int i = 0; i < 4; i++) lab.ForceResearch();
            float d5 = Factor() - f0;
            c.ExpectNear(d5, 5f * d1, 0.01f, $"five waves: five times as much (+{d5:0.###}, {F(outside.rps)} s)");
            c.Expect(studied.rps == specimenRps, "the specimen itself is never updated (its stats aren't computed)");

            // The popup.
            lab.GetComponentsInChildren<MonoBehaviour>(true).OfType<IBuildable>().FirstOrDefault()?.SpawnUI();
            yield return c.Wait(0.3f);
            SimpleUI ui = Object.FindObjectsOfType<SimpleUI>().FirstOrDefault();
            string text = ui != null && ui.discriptionText != null ? ui.discriptionText.text : "";
            c.Expect(text.Contains("Studying the <b>Ballista</b>") && text.Contains("+25% attack speed"), "the popup names the specimen and its research");
            c.Expect(ui != null && ui.demolishButton != null && ui.demolishButton.activeSelf, "with a sell button");
            Aim(lab.Slot + Vector3.up * 1.2f, 3.4f);
            yield return c.Wait(0.2f);
            c.Shot("popup");
            yield return c.Wait(0.3f);

            // ----- selling: the copy's refund, the chamber free, the research kept
            int sell = TowerManager.instance.GetSellPrice(TowerType.Crossbow), gold = ResourceManager.instance.gold;
            lab.Sell();
            yield return c.Wait(1.2f);
            c.Expect(ResourceManager.instance.gold == gold + sell, $"selling refunds {sell}g like any Ballista");
            c.Expect(lab.Empty && studied == null && !fly.towers.Contains(studied), "the specimen is gone");
            c.Expect(fly.currentPrice == priceBefore + fly.priceIncrease * (fly.towers.Count - countBefore), $"the price follows ({fly.currentPrice}g)");
            c.ExpectNear(lab.Power, 0f, 0.01f, "the lab goes dark again");
            c.Expect(LabResearch.Waves(AscensionTarget.Ballista) == 5 && Factor() > f0 + 4f * d1, $"the research stays (+{Factor() - f0:0.###})");

            // ----- a Radar in the chamber: no plane
            BuildButtonUI radarButton = TowerButton(TowerType.Radar);
            if (radarButton != null)
            {
                int planes = Object.FindObjectsOfType<BiPlane>().Length;
                Place(radarButton, lab.Slot, 0);
                yield return c.Wait(0.5f);
                StatLayer.Refresh();
                yield return c.Wait(0.3f);
                c.Expect(lab.Specimen != null && lab.Specimen.Target == AscensionTarget.Radar, "a Radar in the chamber");
                c.Expect(Object.FindObjectsOfType<BiPlane>().Length == planes, "it sends out no plane (also after the stats refresh)");
                lab.Sell();
                yield return c.Wait(0.3f);
            }
            else c.Info("no Radar build button (locked): skipped");

            // ----- a Mana Siphon studied: siphons gather more mana; Haunted Houses researched tax more
            BuildButtonUI siphonButton = Button(p => p.GetComponent<ManaSiphon>() != null);
            c.Expect(siphonButton != null, "the Mana Siphon's build button");
            if (siphonButton != null)
            {
                int vanilla = siphonButton.towerFlyweight.currentPrice;
                Place(siphonButton, lab.Slot, vanilla);
                c.Expect(lab.Specimen != null && lab.Specimen.Target == AscensionTarget.ManaSiphon && !lab.Specimen.GetComponent<ManaSiphon>().enabled,
                         "a Mana Siphon in the chamber, switched off");
                ManaSiphon outsideSiphon = c.BuildNear(siphonButton.tower.name, lab.Slot, 2.5f, 7f)?.GetComponent<ManaSiphon>();
                if (outsideSiphon != null)
                {
                    ResourceManager rm = ResourceManager.instance;
                    rm.maxMana = Mathf.Max(rm.maxMana, 100000);
                    int Gathered()
                    {
                        rm.mana = 0;
                        for (int i = 0; i < 20; i++) outsideSiphon.Gather();
                        return rm.mana;
                    }
                    int plain = Gathered();
                    for (int i = 0; i < 4; i++) lab.ForceResearch();
                    int boosted = Gathered();
                    c.Expect(LabResearch.Waves(AscensionTarget.ManaSiphon) == 4 && Mathf.Abs(boosted - plain * 1.2f) <= 1f,
                             $"4 waves (+20%): a siphon's 20 gathers bring {boosted} mana instead of {plain}");
                }
                else c.Info("no free cell for a second siphon: skipped");
                gold = ResourceManager.instance.gold;
                lab.Sell();
                c.Expect(ResourceManager.instance.gold == gold + vanilla, $"the studied siphon sells for what it cost ({vanilla}g)");
            }
            BuildButtonUI houseButton = Button(p => p.GetComponent<HauntedHouse>() != null);
            HauntedHouse house = houseButton != null ? c.BuildNear(houseButton.tower.name, lab.Slot, 2.5f, 7f)?.GetComponent<HauntedHouse>() : null;
            if (house != null)
            {
                house.isGathering = true; // as if it stood by a grave
                house.graveCount = 1;
                int Tax()
                {
                    int before = ResourceManager.instance.gold;
                    house.GenerateIncome();
                    return ResourceManager.instance.gold - before;
                }
                float rate = GameManager.instance.hauntedHouseTaxRate;
                int plain = Tax();
                LabResearch.Set(AscensionTarget.HauntedHouse, 20);
                int taxed = Tax();
                c.Expect(taxed > plain && Mathf.Abs(taxed - plain * 2f) <= 1f && Mathf.Approximately(GameManager.instance.hauntedHouseTaxRate, rate),
                         $"20 waves of Haunted House research (+100%): its tax is {taxed}g instead of {plain}g (the tax rate itself unchanged)");
                LabResearch.Set(AscensionTarget.HauntedHouse, 0);
            }
            else c.Info("no Haunted House to build: skipped");

            // ----- the Lookout's mark
            BuildButtonUI lookoutButton = TowerButton(TowerType.Lookout);
            Tower lookout = lookoutButton != null ? c.BuildNear(lookoutButton.tower.name, lab.Slot, 2.5f, 7f)?.GetComponent<Tower>() : null;
            if (lookout != null)
            {
                yield return c.Wait(0.2f);
                int health = lookout.healthDamage, shield = lookout.shieldDamage;
                LabResearch.Set(AscensionTarget.Lookout, 50);
                c.Expect(lookout.healthDamage > health && lookout.shieldDamage > shield,
                         $"research strengthens the Lookout's mark (50 waves: health x{health} -> x{lookout.healthDamage}, shield x{shield} -> x{lookout.shieldDamage})");
                LabResearch.Set(AscensionTarget.Lookout, 0);
            }
            else c.Info("no Lookout to build: skipped");

            yield return PoiScenarios.OpenButtons(c);
            foreach (AscensionTarget t in AscensionTargets.All) LabResearch.Set(t, 0);
            c.Expect(AscensionTargets.All.All(t => LabResearch.Waves(t) == 0), "research reset for the next scenarios");
            Time.timeScale = Scenarios.SpeedUp;
        }
    }
}

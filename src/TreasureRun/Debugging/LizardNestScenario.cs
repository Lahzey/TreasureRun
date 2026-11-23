using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TreasureRun.Core;
using TreasureRun.Pois;
using TreasureRun.UI;
using UnityEngine;

namespace TreasureRun.Debugging
{
    /// <summary>
    /// Lizard Nest (docs/poi-plan.md §8): the nest's cell beside the path (blocked for good), the lizards crawling out in
    /// the wave it is found (count and stats for the wave, our model in place of the vanilla art, walking onto the path
    /// and on towards the castle), a lizard's brood when it dies (count, stats, wriggling on from where it fell), the popup,
    /// the chest after the wave, and no more lizards in the next wave.
    /// </summary>
    internal sealed class LizardNestScenario : Scenario
    {
        public override string Name => "poi-lizard-nest";
        public override string Description => "Lizard Nest: placement, lizards in its wave, their brood, the chest afterwards, quiet after.";
        public override bool Permanent => true;

        private static string F(float v) => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

        private static void Aim(Vector3 p, float size)
        {
            Vector3 f = Camera.main != null ? Camera.main.transform.forward : Vector3.down;
            if (f.y < -0.1f) p += f * (p.y / -f.y);
            DevConsole.Execute($"zoom {F(size)} {F(p.x)} {F(p.z)}");
        }

        private static List<NestCreature> Of(LizardNest n, NestCreatureKind kind) =>
            NestCreature.Active.Where(x => x != null && x.Nest == n && x.Kind == kind && Combat.IsAlive(x.Enemy)).ToList();

        public override IEnumerator Run(ScenarioContext c)
        {
            c.Expect(PoiDirector.Enabled, "Points of Interest are on (Treasure Run)");
            if (!PoiDirector.Enabled) yield break;
            DevConsole.Execute("god");
            if (GameHelpers.Level < 10) DevConsole.Execute("level 10");
            DevConsole.Execute("active UniverseLibCanvas 0");

            // ----- placement: the wave starts as the tile is placed, and the lizards come out at once
            var nests = new List<LizardNest>();
            for (int i = 0; i < 4 && PoiScenarios.OpenMarker() == null; i++) yield return PoiScenarios.OpenButtons(c);
            // Away from the castle first, so the fast hatchlings have a long way to go.
            for (int i = 0; i < 3; i++)
            {
                TileSpawnLocation far = PoiScenarios.FarMarker();
                if (far == null) break;
                PoiDirector.ForceExits = SectionExits.Top;
                far.SpawnTile();
                yield return c.Wait(0.3f);
                yield return PoiScenarios.OpenButtons(c);
            }
            PoiDirector.ForceExits = null;
            if (GameHelpers.Level < 10) DevConsole.Execute("level 10");
            for (int i = 0; i < 3 && nests.Count == 0; i++) yield return PoiScenarios.Expand(c, SectionExits.Top, PoiRegistry.Get(LizardNestLogic.Id), nests, far: true);
            LizardNest n = nests.FirstOrDefault();
            c.Expect(n != null, "a Lizard Nest was placed");
            if (n == null) yield break;
            Time.timeScale = 1f;
            PoiScenarios.CheckSection(c, n);
            var cell = LizardNestStyle.CellOf(n.Layout);
            var pc = LizardNestLogic.PathCell(n.Layout, cell);
            c.Expect(pc.x >= 0 && Mathf.Abs(pc.x - cell.x) + Mathf.Abs(pc.z - cell.z) == 1, $"the nest (cell {cell.x},{cell.z}) is beside the path ({pc.x},{pc.z})");
            c.Expect(n.Exit != null && n.Target != null && n.Exit.GetNextWaypoint(false) == n.Target, "its lizards have a way onto the path");
            Vector2Int g = n.Grid;
            c.Expect(c.BuildAt("CrossbowTower", g.x, g.y) == null, "the nest's cell can't be built on");
            int wave = GameHelpers.Level;
            c.Expect(n.State == LizardNest.NestState.Hatching && n.HatchWave == wave, $"it hatches in the wave it was found ({n.State}, wave {n.HatchWave} of {wave})");
            int lizards = LizardNestLogic.Lizards(wave), brood = LizardNestLogic.Brood(wave);
            c.Info($"wave {wave}: {lizards} lizards of {LizardNestLogic.LizardHealthAt(wave)} health and {LizardNestLogic.LizardArmorAt(wave)} armor, " +
                   $"{brood} hatchlings each of {LizardNestLogic.HatchlingHealthAt(wave)}");

            Aim(n.Ground + Vector3.up * 0.3f, 2.4f);
            yield return c.Wait(0.25f);
            c.Shot("emerge");
            yield return c.Wait(LizardNestLogic.SpawnSeconds(lizards) + 0.4f);
            List<NestCreature> out_ = NestCreature.Active.Where(x => x != null && x.Nest == n && x.Kind == NestCreatureKind.Lizard).ToList();
            c.Expect(n.LizardsOut == lizards && out_.Count == lizards, $"{lizards} lizards came out within {LizardNestLogic.SpawnSeconds(lizards):0.#} s ({n.LizardsOut}, {out_.Count} alive)");
            float m = 1f + MonsterManager.instance.extraHealthPercentage;
            bool stats = out_.All(x => x.Enemy.health == (int)(LizardNestLogic.LizardHealthAt(wave) * m)
                                       && x.Enemy.armor == (int)(LizardNestLogic.LizardArmorAt(wave) * (1f + MonsterManager.instance.extraArmorPercentage))
                                       && x.Enemy.shield == 0 && Mathf.Approximately(x.Enemy.baseSpeed, LizardNestLogic.LizardSpeed)
                                       && x.Enemy.damageToTower == LizardNestLogic.LizardCastleDamage);
            c.Expect(stats, "the lizards' health, armor (no shield), speed and castle damage follow the wave");
            NestCreature first = out_.FirstOrDefault();
            if (first == null) yield break;
            bool art = first.GetComponentsInChildren<MeshRenderer>(true).Count(r => r.enabled && r.name == "Art") >= 9
                       && first.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled).All(r => r.name == "Art" || r is ParticleSystemRenderer);
            c.Expect(art, "a lizard wears our model (body, head, tail, four legs), the vanilla art is hidden");
            c.Expect(SpawnManager.instance.currentEnemies.Contains(first.Enemy), "it is one of the wave's monsters");

            // ----- walking: onto the path and on along it, legs moving
            LizardRig rig = first.GetComponent<LizardRig>();
            Transform leg = rig != null ? rig.transform.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "leg_right") : null;
            Quaternion leg0 = leg != null ? leg.localRotation : Quaternion.identity;
            Vector3 start = first.transform.position;
            Aim(first.transform.position + Vector3.up * 0.2f, 1.6f);
            yield return c.Wait(0.5f);
            c.Shot("lizard");
            yield return c.Wait(0.1f);
            Aim(first.transform.position + Vector3.up * 0.15f, 0.8f);
            yield return c.Wait(0.1f);
            c.Shot("lizard_close");
            yield return c.Wait(0.1f);
            yield return c.Wait(1.2f);
            float walked = Vector3.Distance(first.transform.position, start);
            c.Expect(walked > 0.5f, $"the lizard walks ({F(walked)} in 1.9 s at speed {F(first.Enemy.pathfinder.speed)})");
            c.Expect(leg != null && Quaternion.Angle(leg0, leg.localRotation) > 3f, "its legs move");
            c.Expect(first.Enemy.pathfinder.currentWaypoint != n.Exit, "it left the nest and follows the path");

            // ----- a lizard dies: its brood swarms out
            NestCreature victim = Of(n, NestCreatureKind.Lizard).FirstOrDefault();
            if (victim == null) yield break;
            int before = Of(n, NestCreatureKind.Hatchling).Count;
            Aim(victim.transform.position + Vector3.up * 0.2f, 1.8f);
            yield return c.Wait(0.4f);
            if (!Combat.IsAlive(victim.Enemy)) yield break;
            Waypoint way = victim.Enemy.pathfinder.currentWaypoint;
            Vector3 at = victim.transform.position;
            Combat.FixedTrue(victim.Enemy, 100000000);
            yield return null;
            Time.timeScale = 0.15f;
            yield return new WaitForSecondsRealtime(0.3f);
            c.Shot("burst");
            yield return new WaitForSecondsRealtime(0.1f);
            Time.timeScale = 1f;
            List<NestCreature> hatch = Of(n, NestCreatureKind.Hatchling);
            c.Expect(hatch.Count - before == brood && n.BroodOut >= brood, $"{brood} hatchlings swarm out ({hatch.Count - before})");
            bool hstats = hatch.All(x => x.Enemy.health == (int)(LizardNestLogic.HatchlingHealthAt(wave) * m) && x.Enemy.armor == 0 && x.Enemy.shield == 0
                                         && x.Enemy.baseSpeed > LizardNestLogic.HatchlingSpeed * 0.9f && x.Enemy.baseSpeed < LizardNestLogic.HatchlingSpeed * 1.1f);
            c.Expect(hstats, "the hatchlings are small, fast and squishy (health for the wave, no armor or shield)");
            float farthest = hatch.Select(x => Vector3.Distance(x.transform.position, at)).DefaultIfEmpty(0f).Max();
            int onWay = hatch.Count(x => x.Enemy.pathfinder.currentWaypoint == way || x.Enemy.pathfinder.currentWaypoint == way.GetNextWaypoint(x.Enemy.pathfinder.takesSplitPaths));
            c.Expect(onWay == hatch.Count && farthest < 0.7f, $"they start where it fell, on its way ({onWay} of {hatch.Count} on its way, at most {F(farthest)} from it)");
            c.Expect(hatch.All(x => x.GetComponent<HatchlingRig>() != null), "they wear the hatchling model (no legs)");
            NestCreature near = hatch.FirstOrDefault(x => x != null);
            if (near != null)
            {
                yield return c.Wait(0.25f);
                if (near != null) Aim(near.transform.position + Vector3.up * 0.05f, 0.7f);
                yield return c.Wait(0.15f);
                Time.timeScale = 0.1f;
                yield return new WaitForSecondsRealtime(0.25f);
                c.Shot("hatchling_close");
                yield return new WaitForSecondsRealtime(0.1f);
                Time.timeScale = 1f;
                Aim(at + Vector3.up * 0.2f, 1.8f);
            }
            yield return c.Wait(0.2f);
            c.Shot("hatchlings");
            yield return c.Wait(0.6f);
            float spread = hatch.Where(x => x != null).Select(x => Vector3.Distance(x.transform.position, at)).DefaultIfEmpty(0f).Max();
            c.Expect(spread > 1f, $"they wriggle off fast ({F(spread)} in 1.2 s)");
            int squish = hatch.Count(x => x != null);
            NestCreature h0 = hatch.FirstOrDefault(x => x != null && Combat.IsAlive(x.Enemy));
            if (h0 != null) Combat.FixedTrue(h0.Enemy, 100000);
            yield return null;
            c.Expect(Of(n, NestCreatureKind.Hatchling).Count == squish - 1 && n.BroodOut == brood, "a dead hatchling spawns nothing");

            // ----- popup
            DevConsole.Execute($"towerui {g.x} {g.y}");
            yield return c.Wait(0.8f);
            DevConsole.Execute("zoom 7");
            yield return c.Wait(0.3f);
            c.Shot("popup");
            yield return c.Wait(0.2f);
            DevConsole.Execute("closeui");
            yield return c.Wait(0.3f);

            // ----- the wave ends: a chest in the nest; the nest stays
            Time.timeScale = Scenarios.SpeedUp;
            yield return PoiScenarios.OpenButtons(c);
            c.Expect(n.State == LizardNest.NestState.Done, $"done after its wave ({n.State})");
            TreasureChest chest = Object.FindObjectsOfType<TreasureChest>()
                .FirstOrDefault(t => new Vector2(t.transform.position.x - n.Ground.x, t.transform.position.z - n.Ground.z).magnitude < 0.3f);
            c.Expect(chest != null, "a treasure chest stands in the nest");
            c.Expect(n.Kills >= lizards + lizards * brood - 1, $"its lizards and brood were slain ({n.Kills})");
            c.Expect(c.BuildAt("CrossbowTower", g.x, g.y) == null, "the nest still blocks its cell");
            Aim(n.Ground + Vector3.up * 0.3f, 2.2f);
            yield return c.Wait(0.5f);
            c.Shot("chest");
            yield return c.Wait(0.2f);

            // ----- the next wave: no more lizards
            TileSpawnLocation marker = PoiScenarios.OpenMarker();
            c.Expect(marker != null, "an open Expand button for the next wave");
            if (marker == null) yield break;
            int count = NestCreature.Active.Count(x => x != null && x.Nest == n);
            marker.SpawnTile();
            yield return c.Wait(LizardNestLogic.SpawnSeconds(LizardNestLogic.MaxLizards) + 1f);
            c.Expect(NestCreature.Active.Count(x => x != null && x.Nest == n) == count && n.LizardsOut == lizards && n.State == LizardNest.NestState.Done,
                     "no more lizards in the next wave");
            yield return PoiScenarios.OpenButtons(c);
            if (chest != null) Object.Destroy(chest.gameObject);
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TreasureRun.Ascensions.Phase3;
using UnityEngine;

namespace TreasureRun.Debugging
{
    /// <summary>In-game checks of the phase 3 ascensions (granted for the scenario and revoked afterwards).</summary>
    internal static class Phase3Scenarios
    {
        public static IEnumerable<Scenario> All => new Scenario[]
        {
            new SawDimensionScenario(), new SawScalingScenario(),
        };
    }

    internal sealed class SawDimensionScenario : Scenario
    {
        public override string Name => "saw-dimension";
        public override string Description => "The Saw Dimension: saws on the whole path deal Σ(Shredder damage)/10 per second, Shredders level up, saws retract without Shredders.";

        public override IEnumerator Run(ScenarioContext c)
        {
            Vector3 spot = c.TrackPoint(5f) ?? Vector3.zero;
            Vector3 far = c.TrackPoint(12f) ?? spot;
            // Two Shredders anywhere: the saws work on the whole map, not in range.
            GameObject a = c.BuildNear("Shredder", far, 1f, 4f);
            GameObject b = c.BuildNear("Shredder", far, 1f, 4f);
            if (a == null || b == null) yield break;
            var towers = new[] { a.GetComponent<Tower>(), b.GetComponent<Tower>() };
            c.Ascend(SawEngine.Id);
            yield return null;
            c.Expect(Phase1Scenarios.MeshSwapped(a, "ascensions/shredder/saw_engine_turret"), "Saw Engine model swapped");
            SawField field = SawField.Instance;
            c.Expect(field != null, "saw field exists");
            if (field == null) yield break;
            yield return c.Wait(3f); // layout + saws rising
            c.Expect(field.SawCount >= 1, $"{field.SawCount} saws along the path");
            c.ExpectNear(field.Extension, 1f, 0.01f, "saws fully out");

            Enemy dummy = c.Dummy(spot, 10_000_000);
            int levels0 = towers.Sum(t => t.level);
            float xp0 = towers.Sum(t => t.healthXP);
            yield return c.Wait(1f);
            DamageMeter m = c.Meter();
            yield return c.Wait(15f);
            int sum = towers.Sum(t => t.damage);
            float h = (float)towers.Average(t => t.healthDamage);
            float expected = sum / SawDimensionLogic.DamageDivisor * h;
            // The meter also counts bleed ticks (100% bleed), so allow up to twice the direct damage.
            float dps = m.PerSecond(TowerType.Shredder);
            c.Expect(dps >= expected * 0.8f && dps <= expected * 2.2f,
                     $"saw damage per second {dps:0.#} (direct hits {expected:0.#} = Σ damage {sum} / 10 × health multiplier {h:0.#}, plus bleed)");
            c.Expect(towers.Sum(t => t.level) > levels0 || towers.Sum(t => t.healthXP) > xp0, "Shredders gained XP without attacking");
            yield return Phase1Scenarios.Picture(c, dummy.gameObject, "saws", 4f);

            // Without Shredders the saws sink into the floor and stop.
            foreach (Tower t in towers) t.Demolish();
            yield return c.Wait(2f);
            c.ExpectNear(field.Extension, 0f, 0.01f, "saws retracted without Shredders");
            long hits0 = field.Hits;
            yield return c.Wait(3f);
            c.Expect(field.Hits == hits0, "no saw hits without Shredders");
            yield return Phase1Scenarios.Picture(c, dummy.gameObject, "retracted", 4f);
        }
    }

    internal sealed class SawScalingScenario : Scenario
    {
        public override string Name => "saw-scaling";
        public override string Description => "The Saw Dimension: saw damage grows with every Saw Engine built (1 vs. 4).";

        public override IEnumerator Run(ScenarioContext c)
        {
            Vector3 spot = c.TrackPoint(5f) ?? Vector3.zero;
            Vector3 far = c.TrackPoint(12f) ?? spot;
            var towers = new List<Tower>();
            GameObject first = c.BuildNear("Shredder", far, 1f, 4f);
            if (first == null) yield break;
            towers.Add(first.GetComponent<Tower>());
            c.Ascend(SawEngine.Id);
            c.Dummy(spot, 100_000_000);
            yield return c.Wait(3f);
            DamageMeter one = c.Meter();
            long hits0 = SawField.Instance != null ? SawField.Instance.Hits : 0;
            yield return c.Wait(12f);
            float dpsOne = one.PerSecond(TowerType.Shredder);
            one.Dispose();
            c.Info($"1 engine: Σ damage {towers.Sum(t => t.damage)}, {dpsOne:0.#} DPS, {(SawField.Instance != null ? SawField.Instance.Hits - hits0 : 0)} saw hits");

            for (int i = 0; i < 3; i++)
            {
                GameObject go = c.BuildNear("Shredder", far, 1f, 5f);
                if (go != null) towers.Add(go.GetComponent<Tower>());
            }
            yield return c.Wait(1f);
            c.Expect(SawEngine.All.Count == towers.Count, $"{SawEngine.All.Count} Saw Engines registered for {towers.Count} Shredders");
            DamageMeter four = c.Meter();
            hits0 = SawField.Instance != null ? SawField.Instance.Hits : 0;
            yield return c.Wait(12f);
            float dpsFour = four.PerSecond(TowerType.Shredder);
            c.Info($"{towers.Count} engines: Σ damage {towers.Sum(t => t.damage)}, {dpsFour:0.#} DPS, {(SawField.Instance != null ? SawField.Instance.Hits - hits0 : 0)} saw hits");
            c.Expect(dpsFour > dpsOne * 2.5f, $"damage grows with engines: {dpsOne:0.#} → {dpsFour:0.#} DPS");
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TreasureRun.Balance;
using TreasureRun.Balance.Game;
using TreasureRun.Core;
using TreasureRun.UI;
using UnityEngine;

namespace TreasureRun.Debugging
{
    /// <summary>Balance framework checks (docs/balance-framework.md, step B3).</summary>
    internal static class BalanceScenarios
    {
        public static IEnumerable<Scenario> All => new Scenario[] { new BalanceCoverageScenario(), new BalanceAreaScenario() };
    }

    /// <summary>Every offerable treasure and card has a spec, and a few ratings come out finite and on the expected side.</summary>
    internal sealed class BalanceCoverageScenario : Scenario
    {
        public override string Name => "balance";
        public override string Description => "Balance specs: coverage of all treasures and cards, a few ratings.";

        public override IEnumerator Run(ScenarioContext c)
        {
            List<ReportItem> items = BalanceGame.Items();
            List<ReportItem> missing = items.Where(i => i.Spec == null).ToList();
            c.Expect(missing.Count == 0, "every treasure and card has a spec" + (missing.Count > 0 ? ": missing " + string.Join(", ", missing.Select(m => m.Name)) : ""));
            c.Expect(items.Count(i => i.Kind != "treasure" && i.Kind != "card" && i.Kind != "tower") > 100, "vanilla cards found in the scene");
            yield return null;

            Rating Of(string q) => BalanceGame.Rate(items.First(i => i.Id == q || i.Name == q));
            Rating whetstone = Of("whetstone"), keen = Of("keen_eye");
            c.Expect(whetstone.Points > 0 && whetstone.Early > whetstone.Late, $"Whetstone {whetstone.Points:0.0}: positive, falls over the run");
            c.Expect(keen.Points > 0, $"Keen Eye {keen.Points:0.0}");
            ReportItem monster = items.FirstOrDefault(i => i.Kind == "MonsterUpgradeCard" && i.Name.StartsWith("Monster Health"));
            if (monster != null)
            {
                Rating m = BalanceGame.Rate(monster);
                c.Expect(m.Points < 0, $"{monster.Name}: {m.Points:0.0} (a cost)");
            }
            ReportItem longbow = items.FirstOrDefault(i => i.Name == "Longbow I");
            if (longbow != null) c.Expect(BalanceGame.Rate(longbow).Points > 0, $"Longbow I: {BalanceGame.Rate(longbow).Points:0.0}");
        }
    }

    /// <summary>
    /// Measures the area model's geometry on the real map: a Tesla Coil's pulse against dummies on every half cell of
    /// the path (its path coverage, PathStats.Coverage) and a Mortar shell's hits (1 + density x 1.6 x radius).
    /// </summary>
    internal sealed class BalanceAreaScenario : Scenario
    {
        public override string Name => "balance-area";
        public override string Description => "Area model: Tesla pulse coverage and Mortar blast hits against a dense line of dummies.";

        public override IEnumerator Run(ScenarioContext c)
        {
            // The starting map is too small: grow it by a few waves (killing everything in between).
            for (int i = 0; i < 10 && BalanceGame.PathPoints(0.5f).Count < 140; i++)
            {
                DevConsole.Execute("killall");
                yield return c.Wait(3f); // the wave ends 2 s after its last monster
                DevConsole.Execute("wave");
                yield return c.Wait(1f);
            }
            DevConsole.Execute("killall");
            yield return c.Wait(1f);
            c.Info(BalanceGame.PathStatsText().Replace("\n", "\n    "));

            Vector3? spot = c.TrackPoint(12f);
            c.Expect(spot.HasValue, "found a track point");
            if (!spot.HasValue) yield break;
            List<Vector3> path = BalanceGame.PathPoints(0.5f);

            // Tesla: every dummy within range is hit by one pulse.
            GameObject tesla = c.BuildNear("Tesla Coil", spot.Value, 1f, 4f);
            c.Expect(tesla != null, "built a Tesla Coil");
            if (tesla == null) yield break;
            Tower t = tesla.GetComponent<Tower>();
            float range = t.range;
            var dummies = path.Where(p => Flat(p, tesla.transform.position) <= range + 1f).Select(p => c.Dummy(p, 10_000_000)).Where(e => e != null).ToList();
            var hit = new HashSet<Enemy>();
            var dummySet = new HashSet<Enemy>(dummies);
            void OnHit(HitResult r) { if (r.Source == TowerType.TeslaCoil && dummySet.Contains(r.Enemy)) hit.Add(r.Enemy); }
            Combat.EnemyDamaged += OnHit;
            ResourceManager.instance.AddMana(1000);
            yield return c.Wait(2.5f);
            Combat.EnemyDamaged -= OnHit;
            float measured = hit.Count * 0.5f; // dummies every half cell
            double model = PathStats.Coverage(range, 0.5, 1e9);
            c.Info($"Tesla range {range:0.0}: {hit.Count} of {dummies.Count} dummies hit = {measured:0.0} cells of path; model (median placement) {model:0.0}");
            c.Expect(hit.Count > 0, "the pulse hit dummies");
            c.Expect(measured > model * 0.4 && measured < model * 2.5, $"coverage within a factor 2.5 of the model ({measured / model:0.00})");
            foreach (Enemy e in dummies) if (e != null) Object.Destroy(e.gameObject);
            Object.Destroy(tesla);
            yield return null;

            // Mortar: dummies every half cell (2 per cell); the model says 1 + 2 x 1.6 x radius per shell.
            GameObject mortar = c.BuildNear("Morter", spot.Value, 5f, 8f); // not too close: shells need a few cells of flight
            c.Expect(mortar != null, "built a Mortar");
            if (mortar == null) yield break;
            Vector3 target = spot.Value;
            var line = path.Where(p => Flat(p, target) <= 3f).Select(p => c.Dummy(p, 10_000_000)).Where(e => e != null).ToList();
            var hits = new List<int>();
            var current = new HashSet<Enemy>();
            var lineSet = new HashSet<Enemy>(line);
            void OnShell(HitResult r) { if (r.Source == TowerType.Morter && lineSet.Contains(r.Enemy)) current.Add(r.Enemy); }
            Combat.EnemyDamaged += OnShell;
            for (int shot = 0; shot < 5; shot++)
            {
                current.Clear();
                yield return c.Wait(6f);
                if (current.Count > 0) hits.Add(current.Count);
            }
            Combat.EnemyDamaged -= OnShell;
            float radius = mortar.GetComponent<Tower>().blastRadius;
            double modelHits = 1 + 2 * 1.6 * radius;
            float avg = hits.Count > 0 ? (float)hits.Average() : 0;
            c.Info($"Mortar blast {radius:0.0}: {avg:0.0} dummies per shell over {hits.Count} shells; model {modelHits:0.0}");
            c.Expect(hits.Count > 0, "shells landed");
            c.Expect(avg > modelHits * 0.4 && avg < modelHits * 2.5, $"hits per shell within a factor 2.5 of the model ({avg / modelHits:0.00})");
        }

        private static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    }
}


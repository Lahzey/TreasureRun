using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using TreasureRun.Balance;

namespace TreasureRun.Tests
{
    /// <summary>Calibration probe (docs/balance-framework.md section 13): points per 1000 gold of each tower at several crowd densities.</summary>
    public class BalanceProbe
    {
        [Test, Explicit]
        public void TowerBalanceByDensity()
        {
            var sb = new StringBuilder();
            var area = new[] { TowerKind.TeslaCoil, TowerKind.Morter };
            var single = new[] { TowerKind.Crossbow, TowerKind.Obelisk, TowerKind.ParticleCannon };
            double keep = Evaluator.AliveShare;
            foreach (double alive in new[] { 0.5, 0.35, 0.25, 0.18 })
            {
                Evaluator.AliveShare = alive; Evaluator.ClearCache();
                var pts = TowerData.All.ToDictionary(t => t.Kind, t => Valuation.Rate(t.Name, t.Name, "tower", null, false, Report.TowerSpec(t.Kind)).Points);
                double a = area.Average(k => pts[k]), s = single.Average(k => pts[k]);
                sb.AppendLine($"alive {alive}: Tesla+Mortar {a:0.0}, Ballista+Obelisk+Particle {s:0.0}, ratio {a / s:0.00} | " + string.Join(" ", pts.Select(p => $"{p.Key}:{p.Value:0.0}")));
            }
            Evaluator.AliveShare = keep; Evaluator.ClearCache();
            Directory.CreateDirectory(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "out"));
            File.WriteAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "out", "tower-balance.txt"), sb.ToString());
        }
    }
}

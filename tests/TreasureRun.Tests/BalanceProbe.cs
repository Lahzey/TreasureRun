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

        /// <summary>
        /// Wandering Merchant: per wave and rarity, the gold that is worth as many points as a treasure of that rarity
        /// (its budget), i.e. the fair price, next to the income of the wave. Writes tests/out/merchant-fair.txt.
        /// </summary>
        [Test, Explicit]
        public void MerchantFairPrices()
        {
            var sb = new StringBuilder();
            sb.AppendLine("wave | income | gold before | bank | fair price (paid) Common Uncommon Rare Epic Legendary Mythical | gold gained worth Common Legendary");
            for (int w = 1; w <= WaveModel.LastWave; w++)
            {
                sb.Append($"{w,4} | {ReferenceBoards.Income(w),7:0} | {ReferenceBoards.GoldBefore(w),8:0} | {50 + 0.15 * ReferenceBoards.Income(w),6:0} |");
                foreach (Rarity r in new[] { Rarity.Common, Rarity.Uncommon, Rarity.Rare, Rarity.Epic, Rarity.Legendary, Rarity.Mythical })
                    sb.Append($" {FairPrice.Of(Pools.Budget(r), w),7:0}");
                sb.AppendLine($" | {FairPrice.GainOf(Pools.Budget(Rarity.Common), w),7:0} {FairPrice.GainOf(Pools.Budget(Rarity.Legendary), w),7:0}");
            }
            string dir = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "out");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "merchant-fair.txt"), sb.ToString());
        }

        /// <summary>Wandering Merchant: the ratings of the three merchants and the formula price against the fair price. Writes tests/out/merchant.txt.</summary>
        [Test, Explicit]
        public void MerchantRatings()
        {
            var sb = new StringBuilder();
            foreach (string id in new[] { "merchant", "legendary_merchant", "ascension_merchant" })
            {
                Rating r = Valuation.Rate(id, id, "poi", null, false, ContentSpecs.Poi(id));
                sb.AppendLine($"{id}: {r.Points:0.0} points (P80 {r.P80:0.0}, early {r.Early:0.0}, late {r.Late:0.0}, peak {r.PeakWave}) curve " +
                              string.Join(" ", r.Curve.Select(v => v.ToString("0"))));
            }
            sb.AppendLine("wave | price / fair: Common Uncommon Rare Epic Legendary Mythical | ascension Rare Epic");
            for (int w = 1; w <= WaveModel.LastWave; w++)
            {
                sb.Append($"{w,4} |");
                for (int r = 0; r < 6; r++)
                    sb.Append($" {Pois.MerchantLogic.FairValue(r, w, false) / FairPrice.Of(Pools.Budget((Rarity)r), w),5:0.00}");
                sb.Append(" |");
                for (int r = 2; r < 4; r++)
                    sb.Append($" {Pois.MerchantLogic.FairValue(r, w, true) / FairPrice.Of(Pools.Budget((Rarity)r) * Pools.AscensionAllowance, w),5:0.00}");
                sb.AppendLine();
            }
            string dir = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "out");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "merchant.txt"), sb.ToString());
        }

        /// <summary>
        /// Lightning Trap: per wave, the damage per strike for each mana per second (k) that makes the trap worth the POI
        /// budget (15 points) at its best mana rate (1-15 mana/s, paid in mana buildings), that best rate, and the points
        /// of the fitted formula (LightningTrapLogic.DamagePerMana). Writes tests/out/lightning-trap.txt.
        /// </summary>
        [Test, Explicit]
        public void LightningTrapDamage()
        {
            var sb = new StringBuilder();
            sb.AppendLine("wave | k for 15 pts | best rate | formula k | formula: best rate, pts | pts at 15 mana/s | V(a) rated");
            var bestRates = new System.Collections.Generic.List<int>();
            for (int w = 1; w <= WaveModel.LastWave; w++)
            {
                double Points(double k, int rate) =>
                    Valuation.PointsAt(new Spec().FixedSource("trap", _ => Pois.LightningTrapLogic.InfoWith(k * rate, rate)), w, w, BoardVariant.Mixed);
                (double pts, int rate) Best(double k)
                {
                    double best = double.NegativeInfinity; int at = 0;
                    for (int r = 1; r <= Pois.LightningTrapLogic.MaxManaPerSecond; r++) { double p = Points(k, r); if (p > best) { best = p; at = r; } }
                    return (best, at);
                }
                double lo = 0, hi = 5000;
                if (Best(hi).pts < Pools.PoiBudget) lo = hi;
                else
                    for (int i = 0; i < 30; i++)
                    {
                        double mid = (lo + hi) / 2;
                        if (Best(mid).pts < Pools.PoiBudget) lo = mid; else hi = mid;
                    }
                int bestRate = Best(hi).rate;
                double fk = Pois.LightningTrapLogic.DamagePerMana(w);
                var f = Best(fk);
                bestRates.Add(f.rate);
                double at15 = Points(fk, 15);
                double v = Valuation.Value(ContentSpecs.Poi("lightning_trap"), w, BoardVariant.Mixed);
                sb.AppendLine($"{w,4} | {lo,8:0.00} | {bestRate,3} | {fk,7:0.00} | {f.rate,3} {f.pts,7:0.0} | {at15,7:0.0} | {v,6:0.0}");
            }
            sb.AppendLine("Best rates at the formula k (LightningTrapLogic.BestRates): { " + string.Join(", ", bestRates) + " }");
            Rating r = Valuation.Rate("lightning_trap", "Lightning Trap", "poi", null, false, ContentSpecs.Poi("lightning_trap"));
            sb.AppendLine($"Rating: {r.Points:0.0} points (P80 {r.P80:0.0}, early {r.Early:0.0}, late {r.Late:0.0}, budget {r.Budget}).");
            string dir = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "out");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "lightning-trap.txt"), sb.ToString());
        }
    }
}

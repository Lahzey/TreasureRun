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
        /// <summary>
        /// Wizard's Tower: the power (base damage) at which the tower, acquired at a wave, is worth the POI budget there;
        /// a cubic fit over waves 3-45 without boss waves; each part's share at the fitted power; the rating. Writes
        /// tests/out/wizard-tower.txt.
        /// </summary>
        [Test, Explicit]
        public void WizardTowerPower()
        {
            var sb = new StringBuilder();
            sb.AppendLine("wave | power for 15 | ceiling pts | power for 90% of it | target | fitted | pts at fit | casts/wave | fire | bolt | ice | tornado dmg | drag slow | necro | goldify kill | gold");
            var xs = new System.Collections.Generic.List<double>();
            var ys = new System.Collections.Generic.List<double>();
            double Pts(Spec spec, int w) => Valuation.PointsAt(spec, w, w, BoardVariant.Mixed);
            for (int w = 1; w <= WaveModel.LastWave; w++)
            {
                double lo = 0, hi = 1e5;
                if (Pts(ContentSpecs.WizardTower(_ => hi), w) < Pools.PoiBudget) lo = hi;
                else
                    for (int i = 0; i < 40; i++)
                    {
                        double mid = (lo + hi) / 2;
                        if (Pts(ContentSpecs.WizardTower(_ => mid), w) < Pools.PoiBudget) lo = mid; else hi = mid;
                    }
                // Past some wave no damage reaches the budget (a spell every 5 s lands few hits): the ceiling, and the
                // power that gets 90% of it.
                double ceiling = Pts(ContentSpecs.WizardTower(_ => 1e5), w);
                double clo = 0, chi = 1e5;
                for (int i = 0; i < 40; i++)
                {
                    double mid = (clo + chi) / 2;
                    if (Pts(ContentSpecs.WizardTower(_ => mid), w) < 0.9 * ceiling) clo = mid; else chi = mid;
                }
                double target = System.Math.Min(lo, chi);
                double fit = Pois.WizardTowerLogic.Power(w);
                Spec full = ContentSpecs.WizardTower(Pois.WizardTowerLogic.Power);
                sb.Append($"{w,4} | {lo,8:0.00} | {ceiling,5:0.0} | {chi,8:0.0} | {target,8:0.0} | {fit,7:0.00} | {Pts(full, w),6:0.0} | {Pois.WizardTowerLogic.CastsPerWave(ReferenceBoards.Get(w, BoardVariant.Mixed)),5:0.0} |");
                // Each part alone at the fitted power.
                foreach (Atom a in full.Atoms)
                {
                    var one = new Spec();
                    one.Atoms.Add(a);
                    sb.Append($" {Pts(one, w),5:0.0}");
                }
                sb.AppendLine();
                if (w >= 3 && w <= 45 && !WaveModel.Get(w).HasBoss) { xs.Add(w); ys.Add(target); }
            }
            double[] c = CubicFit(xs, ys);
            sb.AppendLine($"Cubic fit of the target (waves 3-45, no boss waves): {c[0]:0.###} + {c[1]:0.####} w + {c[2]:0.#####} w^2 + {c[3]:0.######} w^3");
            Rating r = Valuation.Rate("wizard_tower", "Wizard's Tower", "poi", null, false, ContentSpecs.Poi("wizard_tower"));
            sb.AppendLine($"Rating: {r.Points:0.0} points (P80 {r.P80:0.0}, early {r.Early:0.0}, late {r.Late:0.0}, budget {r.Budget}).");
            string dir = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "out");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "wizard-tower.txt"), sb.ToString());
        }

        /// <summary>Least-squares cubic through the points (normal equations).</summary>
        private static double[] CubicFit(System.Collections.Generic.List<double> xs, System.Collections.Generic.List<double> ys)
        {
            var m = new double[4, 5];
            for (int i = 0; i < xs.Count; i++)
            {
                double[] p = { 1, xs[i], xs[i] * xs[i], xs[i] * xs[i] * xs[i] };
                for (int r = 0; r < 4; r++)
                {
                    for (int k = 0; k < 4; k++) m[r, k] += p[r] * p[k];
                    m[r, 4] += p[r] * ys[i];
                }
            }
            for (int col = 0; col < 4; col++)
            {
                int piv = col;
                for (int r = col + 1; r < 4; r++) if (System.Math.Abs(m[r, col]) > System.Math.Abs(m[piv, col])) piv = r;
                for (int k = 0; k < 5; k++) { double t = m[col, k]; m[col, k] = m[piv, k]; m[piv, k] = t; }
                for (int r = 0; r < 4; r++)
                {
                    if (r == col) continue;
                    double f = m[r, col] / m[col, col];
                    for (int k = col; k < 5; k++) m[r, k] -= f * m[col, k];
                }
            }
            return new[] { m[0, 4] / m[0, 0], m[1, 4] / m[1, 1], m[2, 4] / m[2, 2], m[3, 4] / m[3, 3] };
        }

            /// <summary>Soul Urn: monsters per wave (with death spawns), for the charge it needs. Writes tests/out/soul-urn.txt.</summary>
        [Test, Explicit]
        public void SoulUrnCounts()
        {
            var sb = new StringBuilder();
            sb.AppendLine("wave | monsters | next 2 | next 3 | income");
            for (int w = 1; w <= WaveModel.LastWave; w++)
            {
                double n = WaveModel.Get(w).Count, n2 = n + (w < WaveModel.LastWave ? WaveModel.Get(w + 1).Count : 0);
                double n3 = n2 + (w + 1 < WaveModel.LastWave ? WaveModel.Get(w + 2).Count : 0);
                sb.AppendLine($"{w,4} | {n,7:0} | {n2,7:0} | {n3,7:0} | {ReferenceBoards.Income(w),7:0}");
            }
            string dir = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "out");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "soul-urn.txt"), sb.ToString());
        }
            /// <summary>Soul Urn rewards: a chest's points, the gold worth as much, and one tower's attack speed. Writes tests/out/soul-urn-rewards.txt.</summary>
        [Test, Explicit]
        public void SoulUrnRewards()
        {
            var sb = new StringBuilder();
            double[] xs = { 0.25, 0.5, 0.75, 1.0, 1.5 };
            Rating rating = Valuation.Rate("soul_urn", "Soul Urn", "poi", null, false, ContentSpecs.Poi("soul_urn"));
            sb.AppendLine($"Soul Urn rating: {rating.Points:0.0} points (P80 {rating.P80:0.0}, early {rating.Early:0.0}, late {rating.Late:0.0}, peak {rating.PeakWave})");
            sb.AppendLine("wave | towers | chest pts | gold worth it | one tower +25% +50% +75% +100% +150% (x1 share) | best x for chest");
            for (int w = 1; w <= WaveModel.LastWave; w++)
            {
                double chest = Pools.TreasurePick(w);
                Board b = ReferenceBoards.Get(w, BoardVariant.Mixed);
                sb.Append($"{w,4} | {b.TowerCount,5:0.0} | {chest,5:0.0} | {FairPrice.GainOf(chest, w),7:0} |");
                double Pts(double x) => Valuation.Value(new Spec().Custom(Category.Offense, "one tower", c => c.Board.All.RateFactor *= 1 + x / System.Math.Max(1, c.Board.TowerCount)), w, BoardVariant.Mixed);
                foreach (double x in xs) sb.Append($" {Pts(x),5:0.0}");
                double lo = 0, hi = 20;
                for (int i = 0; i < 30; i++) { double m = (lo + hi) / 2; if (Pts(m) < chest) lo = m; else hi = m; }
                sb.AppendLine($" | {lo,5:0.00}");
            }
            string dir = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "out");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "soul-urn-rewards.txt"), sb.ToString());
        }
            /// <summary>Soul Urn rating by fill delay, gold multiplier and rune tower share. Writes tests/out/soul-urn-levers.txt.</summary>
        [Test, Explicit]
        public void SoulUrnLevers()
        {
            var sb = new StringBuilder();
            foreach (int d in new[] { 3, 4, 5, 6 })
                foreach (double g in new[] { 1.0, 1.5, 2.0 })
                    foreach (double share in new[] { 2.0, 3.0 })
                    {
                        Spec s = new Spec()
                            .Custom(Category.Meta, "chest", c => { if (c.Wave >= c.Acquired + d) c.Mods.DirectPoints += Pools.TreasurePick(c.Wave) / 3; })
                            .Custom(Category.Economy, "gold", c => { if (c.Wave >= c.Acquired + d) c.Mods.ExtraGold += g * Pois.SoulUrnLogic.Gold(c.Acquired) / 3.0; })
                            .Custom(Category.Offense, "rune", c => { if (c.Wave >= c.Acquired + d) c.Board.All.RateFactor *= 1 + share / System.Math.Max(1, c.Board.TowerCount) / 3; })
                            .Offered(3, 40);
                        Rating r = Valuation.Rate("u", "u", "poi", null, false, s);
                        sb.AppendLine($"delay {d} gold x{g} share {share}: {r.Points:0.0} (split {string.Join("/", r.Split.Select(x => x.ToString("0")))})");
                    }
            string dir = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "out");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "soul-urn-levers.txt"), sb.ToString());
        }

        /// <summary>Lizard Nest: rating by toughness (x the lizards' and hatchlings' health), and the value curve. Writes tests/out/lizard-nest.txt.</summary>
        [Test, Explicit]
        public void LizardNestLevers()
        {
            var sb = new StringBuilder();
            foreach (double k in new[] { 1.0, 1.25, 1.5, 2.0, 2.5, 3.0 })
            {
                Spec s = ContentSpecs.LizardNest(k);
                Rating r = Valuation.Rate("lizard_nest", "Lizard Nest", "poi", null, false, s);
                sb.Append($"toughness {k:0.00}: {r.Points:0.0} (P80 {r.P80:0.0}, early {r.Early:0.0}, late {r.Late:0.0}, peak {r.PeakWave}) |");
                for (int w = 3; w <= 45; w += 3) sb.Append($" {w}:{Valuation.Value(s, w, BoardVariant.Mixed):0.0}");
                sb.AppendLine();
            }
            sb.AppendLine("wave | lizards | brood | lizard hp | armor | hatchling hp | extra share of the wave | gold");
            for (int w = 1; w <= 45; w++)
            {
                var (h, a) = Pois.LizardNestLogic.ExtraLayers(w);
                WaveStats ws = WaveModel.Get(w);
                sb.AppendLine($"{w,3} | {Pois.LizardNestLogic.Lizards(w)} | {Pois.LizardNestLogic.Brood(w),2} | {Pois.LizardNestLogic.LizardHealthAt(w),6} | {Pois.LizardNestLogic.LizardArmorAt(w),5} | {Pois.LizardNestLogic.HatchlingHealthAt(w),5} | {(h + a) / ws.Total:0%} | {Pois.LizardNestLogic.KillGold(w):0}");
            }
            string dir = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "out");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "lizard-nest.txt"), sb.ToString());
        }

        /// <summary>Laboratory: rating by research per wave, and the value curve by wave. Writes tests/out/laboratory.txt.</summary>
        [Test, Explicit]
        public void LaboratoryLevers()
        {
            var sb = new StringBuilder();
            foreach (bool idle in new[] { true })
                foreach (double p in new[] { 0.01, 0.03, 0.05, 0.08 })
                {
                    Spec s = ContentSpecs.Laboratory(p, idle);
                    Rating r = Valuation.Rate("laboratory", "Laboratory", "poi", null, false, s);
                    sb.Append($"{p:0%} per wave{(idle ? ", idle copy" : ", no cost  ")}: {r.Points:0.0} (P80 {r.P80:0.0}, early {r.Early:0.0}, late {r.Late:0.0}, peak {r.PeakWave}) {r.Flag} |");
                    for (int w = 2; w <= 45; w += 3) sb.Append($" {w}:{Valuation.Value(s, w, s.Variant):0.0}");
                    sb.AppendLine();
                }
            // Where the value comes from at 5%: the main kind (no mana) and its share of the tower gold, research and the idle copy apart.
            Spec five = ContentSpecs.Laboratory(0.05);
            sb.AppendLine("wave | main kind | its gold share | research | idle copy | research at +10 waves");
            for (int a = 2; a <= 44; a += 6)
            {
                Board b = ReferenceBoards.Get(a, BoardVariant.CannonLine);
                TowerGroup main = null;
                double total = 0;
                foreach (TowerGroup g in b.Scope(null))
                {
                    total += b.GroupCost(g);
                    TowerInfo t = TowerData.Get(g.Kind);
                    if (g.Count > 0 && t.ManaRate <= 0 && t.ManaPerSecond <= 0 && (main == null || b.GroupCost(g) > b.GroupCost(main))) main = g;
                }
                double research = Valuation.Value(five, a, BoardVariant.CannonLine, x => x.Category == Category.Offense);
                double idle = Valuation.Value(five, a, BoardVariant.CannonLine, x => x.Category == Category.Economy);
                double later = Valuation.PointsAt(five, a, System.Math.Min(45, a + 10), BoardVariant.CannonLine, x => x.Category == Category.Offense);
                sb.AppendLine($"{a,3} | {main?.Kind} x{main?.Count:0.#} | {(main != null ? b.GroupCost(main) / total : 0):0%} | {research:0.0} | {idle:0.0} | {later:0.0}");
            }
            string dir = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "out");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "laboratory.txt"), sb.ToString());
        }
    }
}

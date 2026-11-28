using System;
using System.Collections.Generic;
using System.Linq;

namespace TreasureRun.Balance
{
    /// <summary>The rating of one item (a report row, section 8).</summary>
    public sealed class Rating
    {
        public string Id, Name, Kind, Group, Note, Source, Requirement;
        public Rarity? Rarity;
        public bool IsCard, IsAscension;
        /// <summary>Final points: P80 x (breadth x timing breadth)^0.3.</summary>
        public double Points;
        public double P80, Early, Late, TimingBreadth, Breadth = 1, Factor = 1;
        public int PeakWave, OfferFrom, OfferTo;
        public BoardVariant Variant;
        public double Budget, BudgetMin, BudgetMax;
        public double[] Split = new double[5];
        /// <summary>V(a) for a = OfferFrom..OfferTo.</summary>
        public double[] Curve = new double[0];
        public bool Unrated;

        public bool OverBudget => !Unrated && Points > BudgetMax;
        public bool UnderBudget => !Unrated && Points < BudgetMin;
        public string Flag => Unrated ? "unrated" : OverBudget ? "over" : UnderBudget ? "under" : "";
    }

    /// <summary>Value curves, the good moment (80th percentile) and the breadth penalties (sections 1.2 and 1.3).</summary>
    public static class Valuation
    {
        /// <summary>
        /// Weight of each later wave in the value curve: wave a + k counts Decay^k. The weights add up to about 10 waves,
        /// as the old flat 10-wave horizon did, so a steady effect rates about the same; an effect that grows over the run
        /// (the Laboratory's research) is seen beyond the tenth wave, at less weight.
        /// </summary>
        public const double Decay = 0.9;
        /// <summary>Waves past this weight are left out (Decay^28 is about 0.05).</summary>
        public const double MinWeight = 0.05;
        /// <summary>The good moment: this percentile of the value curve.</summary>
        public const double Percentile = 0.8;
        /// <summary>Exponent of the situational and timing penalty.</summary>
        public const double PenaltyExponent = 0.3;
        /// <summary>Flag items outside +-25% of their budget.</summary>
        public const double BudgetTolerance = 0.25;

        /// <summary>
        /// The castle's safety net (section 4.6): health barely changes the headroom late in a run, but it is what saves a
        /// run from one bad wave. Chosen, not derived: points = SafetyWeight x 100 x ln(safety with / without), where
        /// safety = max health + 10 waves of heals and absorbs, divided by the leak damage, times the safety factor.
        /// +5% max health is about 1.7 points; halving it (Glass Cannon) about -24.
        /// </summary>
        public const double SafetyWeight = 0.35;
        /// <summary>Weight of extra leak damage on the safety net (Banditry: +1 per leak halves the margin only in part).</summary>
        public const double LeakDamageWeight = 0.5;

        /// <summary>
        /// Gold per mana per second, the one price of the mana market. A Mana Siphon is 100 per mana/s but needs a
        /// crystal; a Mana Bank is 500 for 1 mana/s plus 20 max mana, which with Savings Account (+40 max) and Sorcery
        /// (6% of max per second) makes about 4.6 mana/s, so 110 gold each. Without those cards a bank is 500. Chosen: 150.
        /// Vanilla mana towers are hungry: an Obelisk pays (int)(damage x 0.25) per hit at 6 hits a second, 12-30 mana/s.
        /// </summary>
        public const double ManaPrice = 150;

        /// <summary>Headroom points at <paramref name="wave"/> of an item acquired at <paramref name="acquired"/>.</summary>
        public static double PointsAt(Spec s, int acquired, int wave, BoardVariant variant, Func<Atom, bool> only = null)
        {
            Board baseBoard = ReferenceBoards.Get(wave, variant);
            if (s.RequiredTower != null) EnsureTower(baseBoard, s.RequiredTower.Value);
            foreach (var setup in s.Setups) setup.Apply(baseBoard);
            Board withItem = baseBoard.Clone();
            var ctx = new AtomContext { Board = withItem, Acquired = acquired, Wave = wave };
            foreach (Atom a in s.Atoms) if (only == null || only(a)) a.Apply(ctx);

            // The mana market: the player settles the item's change in mana demand or supply in mana buildings, so mana
            // is worth its gold price. The gold left buys towers together with the mana they need.
            double gap0 = ManaGap(baseBoard);
            withItem.Mods.ExtraGold -= TradeMana(withItem, ManaGap(withItem) - gap0);
            SpendWithMana(withItem, withItem.Mods.ExtraGold, gap0);

            double h0 = Evaluator.Evaluate(baseBoard).Combined, h1 = Evaluator.Evaluate(withItem).Combined;
            if (h1 <= 0) return -100;
            return 100 * Math.Log(h1 / h0) + withItem.Mods.DirectPoints
                   + SafetyWeight * 100 * Math.Log(Safety(withItem) / Safety(baseBoard));
        }

        /// <summary>The castle's safety margin (see <see cref="SafetyWeight"/>).</summary>
        public static double Safety(Board b)
        {
            BoardMods m = b.Mods;
            double health = Math.Max(1, m.MaxHealth * m.MaxHealthFactor) + 10 * (m.HealPerWave + m.AbsorbPerWave);
            return health * m.SafetyFactor / (1 + LeakDamageWeight * Math.Max(-0.9, m.Monster.ExtraCastleDamage));
        }

        /// <summary>Mana per second the board lacks to cover its towers (negative: a surplus).</summary>
        public static double ManaGap(Board b)
        {
            Headroom h = Evaluator.Raw(b);
            double need = ReferenceBoards.ManaCoverage * h.ManaDemand / Math.Max(1, h.WaveTime);
            return need - (b.Mods.ManaPerSecond + b.Mods.ManaRegen * b.Mods.MaxMana);
        }

        /// <summary>
        /// Buys (delta &gt; 0) or sells (delta &lt; 0, only buildings the board has) mana buildings for <paramref name="delta"/>
        /// mana per second; returns the gold it costs (negative: gold freed).
        /// </summary>
        public static double TradeMana(Board b, double delta)
        {
            if (Math.Abs(delta) < 1e-6) return 0;
            double after = Math.Max(0, b.Siphons + delta), traded = after - b.Siphons;
            b.Siphons = after;
            b.Mods.ManaPerSecond += traded;
            return traded * ManaPrice;
        }

        /// <summary>
        /// Spends gold (negative: sells) on the board's mix of towers together with the mana they need: a copy of a mana
        /// tower costs its price plus its mana buildings. The mana share is measured on a 1000-gold probe.
        /// </summary>
        public static void SpendWithMana(Board b, double gold, double gap0)
        {
            if (Math.Abs(gold) < 1) return;
            double probeGold = Math.Sign(gold) * 1000;
            Board probe = b.Clone();
            probe.Spend(probeGold);
            double manaShare = Math.Max(0, (ManaGap(probe) - ManaGap(b)) * ManaPrice / probeGold);
            b.Spend(gold / (1 + manaShare));
            TradeMana(b, ManaGap(b) - gap0);
        }

        /// <summary>
        /// V(a): the weighted average of the points over the rest of the run of an item acquired at a, wave a + k weighing
        /// <see cref="Decay"/>^k (down to <see cref="MinWeight"/>).
        /// </summary>
        public static double Value(Spec s, int a, BoardVariant variant, Func<Atom, bool> only = null)
        {
            double sum = 0, weights = 0, weight = 1;
            for (int w = a; w <= WaveModel.LastWave && weight >= MinWeight; w++, weight *= Decay)
            {
                sum += weight * PointsAt(s, a, w, variant, only);
                weights += weight;
            }
            return weights > 0 ? sum / weights : 0;
        }

        /// <summary>Gives the board a tower it must have for the item (its requirement): 12% of the tower gold moves to it.</summary>
        public static void EnsureTower(Board b, TowerKind kind)
        {
            TowerGroup g = b.Group(kind);
            if (g != null && g.Count >= 1) return;
            double gold = 0;
            foreach (TowerGroup x in b.Groups) gold += b.GroupCost(x);
            double moved = Math.Max(TowerData.Get(kind).Price, 0.12 * gold);
            foreach (TowerGroup x in b.Groups) x.Count *= Math.Max(0.5, 1 - moved / Math.Max(1, gold));
            if (g == null)
            {
                TowerGroup any = b.Groups.Count > 0 ? b.Groups[0] : null;
                g = new TowerGroup { Kind = kind, Level = any?.Level ?? 1, Height = any?.Height ?? 1 };
                b.Groups.Add(g);
            }
            g.Count = Math.Max(1, b.Copies(kind, 0, moved));
            FeedMana(b, g, moved);
        }

        /// <summary>
        /// A player who adds a mana tower also builds siphons for it: 90% of its mana demand is covered, paid with
        /// part of its gold (<see cref="ManaPrice"/> per mana per second).
        /// </summary>
        /// <summary>Spends gold on more of one tower (feeding its mana like a player would).</summary>
        public static void AddTowerGold(Board b, TowerKind kind, double gold)
        {
            TowerGroup g = b.Group(kind);
            if (g == null)
            {
                TowerGroup any = b.Groups.Count > 0 ? b.Groups[0] : null;
                g = new TowerGroup { Kind = kind, Level = any?.Level ?? 1, Height = any?.Height ?? 1 };
                g.Count = b.Copies(kind, 0, gold);
                b.Groups.Add(g);
                FeedMana(b, g, gold);
                return;
            }
            double before = g.Count;
            g.Count += b.Copies(kind, g.Count, gold);
            var added = new TowerGroup { Kind = kind, Count = g.Count - before, Level = g.Level, Height = g.Height };
            if (added.Info.ManaRate <= 0 && added.Info.ManaPerSecond <= 0) return;
            // feed the new copies: measure their demand as a group of their own
            b.Groups.Add(added);
            g.Count = before;
            FeedMana(b, added, gold);
            b.Groups.Remove(added);
            g.Count = before + added.Count;
        }

        private static void FeedMana(Board b, TowerGroup g, double gold)
        {
            TowerInfo t = g.Info;
            if (t.ManaRate <= 0 && t.ManaPerSecond <= 0) return;
            Headroom h = Evaluator.Raw(b);
            double demand = 0;
            foreach (GroupResult r in h.Groups) if (r.Kind == g.Kind) demand += r.Mana;
            double perSecond = ReferenceBoards.ManaCoverage * demand / Math.Max(1, h.WaveTime);
            double cost = perSecond * ManaPrice;
            double keep = gold / (gold + cost);
            g.Count = Math.Max(1, g.Count * keep);
            b.Mods.ManaPerSecond += perSecond * keep;
            b.Siphons += perSecond * keep;
        }

        /// <summary>Removes a tower from the board; its gold buys more of the other towers.</summary>
        public static void RemoveTower(Board b, TowerKind kind)
        {
            TowerGroup g = b.Group(kind);
            if (g == null) return;
            double gold = b.GroupCost(g);
            b.Groups.Remove(g);
            b.Spend(gold);
        }

        public static Rating Rate(string id, string name, string kind, Rarity? rarity, bool isCard, Spec s, bool ascension = false)
        {
            var r = new Rating
            {
                Id = id, Name = name, Kind = kind, Rarity = rarity, IsCard = isCard, IsAscension = ascension,
                Note = s.Note ?? s.EstimateReason ?? s.UnratedReason,
                Requirement = s.Requirement, Variant = s.Variant, Breadth = s.Breadth, OfferFrom = s.OfferFrom, OfferTo = s.OfferTo,
            };
            if (isCard) { r.Budget = Pools.CardBudget; r.BudgetMin = Pools.CardBudgetMin; r.BudgetMax = Pools.CardBudgetMax; }
            else if (kind == "poi")
            {
                r.Budget = Pools.PoiBudget;
                r.BudgetMin = r.Budget * (1 - BudgetTolerance); r.BudgetMax = r.Budget * (1 + BudgetTolerance);
            }
            else if (rarity != null)
            {
                r.Budget = Pools.Budget(rarity.Value) * (ascension ? Pools.AscensionAllowance : 1);
                r.BudgetMin = r.Budget * (1 - BudgetTolerance); r.BudgetMax = r.Budget * (1 + BudgetTolerance);
            }
            else { r.BudgetMin = double.NegativeInfinity; r.BudgetMax = double.PositiveInfinity; }

            if (s.IsUnrated) { r.Unrated = true; r.Source = "unrated"; return r; }
            if (s.Estimate != null)
            {
                r.Points = r.P80 = r.Early = r.Late = s.Estimate.Value;
                r.TimingBreadth = 1;
                r.Factor = Math.Pow(s.Breadth, PenaltyExponent);
                r.Points *= r.Factor;
                r.Source = s.SimulatedBy != null ? "estimated (simulate: " + s.SimulatedBy + ")" : "estimated";
                r.PeakWave = s.OfferFrom;
                r.Split[(int)(s.Atoms.Count > 0 ? s.Atoms[0].Category : Category.Offense)] = 1;
                return r;
            }

            var curve = new List<double>();
            for (int a = s.OfferFrom; a <= s.OfferTo; a++) curve.Add(Value(s, a, s.Variant));
            r.Curve = curve.ToArray();
            r.Early = curve[0];
            r.Late = curve[Math.Min(curve.Count - 1, Math.Max(0, 40 - s.OfferFrom))];
            int peak = 0;
            for (int i = 1; i < curve.Count; i++) if (curve[i] > curve[peak]) peak = i;
            r.PeakWave = s.OfferFrom + peak;

            if (curve.Max() <= 0)
            {
                // A pure cost (monster cards): its average, no penalty.
                r.P80 = r.Points = curve.Average();
                r.TimingBreadth = 1;
            }
            else
            {
                r.P80 = PercentileOf(curve, Percentile);
                r.TimingBreadth = r.P80 > 0 ? curve.Average(v => Math.Max(0, Math.Min(1, v / r.P80))) : 1;
                r.Factor = Math.Pow(Math.Max(1e-6, s.Breadth * r.TimingBreadth), PenaltyExponent);
                r.Points = r.P80 * r.Factor;
            }
            r.Source = "formula" + (s.SimulatedBy != null ? " (simulate: " + s.SimulatedBy + ")" : "");

            // Split by category at the good moment: each category alone, scaled to the total.
            int at = s.OfferFrom + ClosestIndex(curve, r.P80);
            double sum = 0;
            for (int c = 0; c < 5; c++)
            {
                Category cat = (Category)c;
                if (!s.Atoms.Any(x => x.Category == cat)) continue;
                r.Split[c] = Value(s, at, s.Variant, x => x.Category == cat);
                sum += Math.Abs(r.Split[c]);
            }
            if (sum > 0) for (int c = 0; c < 5; c++) r.Split[c] /= sum;
            return r;
        }

        public static double PercentileOf(IList<double> values, double p)
        {
            var sorted = values.OrderBy(v => v).ToList();
            double pos = p * (sorted.Count - 1);
            int lo = (int)Math.Floor(pos), hi = (int)Math.Ceiling(pos);
            return sorted[lo] + (sorted[hi] - sorted[lo]) * (pos - lo);
        }

        private static int ClosestIndex(List<double> curve, double v)
        {
            int best = 0;
            for (int i = 1; i < curve.Count; i++) if (Math.Abs(curve[i] - v) < Math.Abs(curve[best] - v)) best = i;
            return best;
        }
    }
}

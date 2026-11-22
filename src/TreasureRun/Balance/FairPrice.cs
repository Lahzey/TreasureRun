using System;
using System.Collections.Generic;

namespace TreasureRun.Balance
{
    /// <summary>
    /// The gold that is worth a number of points at a wave (Wandering Merchant, docs/poi-plan.md), seen from a buyer:
    /// the price whose payment costs as many points as the item brings, with gold valued as V(a) of "gold now" (section
    /// 4.5: gold buys the board's towers over the next waves). Paying is dearer than gaining (the board loses towers it
    /// relies on), so this is lower than the gold whose gain is worth the points (<see cref="GainOf"/>, used for selling).
    /// Used to check shop prices against the treasure budgets.
    /// </summary>
    public static class FairPrice
    {
        private static readonly Dictionary<(double, int, BoardVariant), double> cache = new Dictionary<(double, int, BoardVariant), double>();

        /// <summary>Points of <paramref name="gold"/> gold gained at <paramref name="wave"/>.</summary>
        public static double PointsOf(double gold, int wave, BoardVariant variant = BoardVariant.Mixed) =>
            Valuation.Value(new Spec().GoldNow(gold), wave, variant);

        /// <summary>The price whose payment at <paramref name="wave"/> costs <paramref name="points"/> (bisection on a log scale).</summary>
        public static double Of(double points, int wave, BoardVariant variant = BoardVariant.Mixed) => Solve(points, wave, variant, -1);

        /// <summary>The gold whose gain at <paramref name="wave"/> is worth <paramref name="points"/>.</summary>
        public static double GainOf(double points, int wave, BoardVariant variant = BoardVariant.Mixed) => Solve(points, wave, variant, +1);

        private static double Solve(double points, int wave, BoardVariant variant, int sign)
        {
            var key = (points * sign, wave, variant);
            lock (cache)
                if (cache.TryGetValue(key, out double hit)) return hit;
            double lo = 1, hi = 1e7;
            for (int i = 0; i < 40 && hi / lo > 1.002; i++)
            {
                double mid = Math.Sqrt(lo * hi);
                if (sign * PointsOf(sign * mid, wave, variant) < points) lo = mid; else hi = mid;
            }
            double result = Math.Sqrt(lo * hi);
            lock (cache) cache[key] = result;
            return result;
        }
    }
}

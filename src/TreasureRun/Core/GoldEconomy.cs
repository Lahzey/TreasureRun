using System;
using System.Collections.Generic;

namespace TreasureRun.Core
{
    public enum GoldSource
    {
        /// <summary>Refunds, card rewards, treasure payouts outside the contexts below, ...</summary>
        Other,
        /// <summary>Gold gained while an enemy dies (vanilla kill gold and on-kill effects).</summary>
        Kill,
        /// <summary>Income buildings (houses, haunted houses, castle income).</summary>
        Income,
        /// <summary>Chopping trees.</summary>
        Tree,
    }

    /// <summary>
    /// Tracks where gold comes from (see Patches/GoldPatches.cs) and applies multipliers to it. Works in every mode;
    /// each multiplier decides itself whether it applies (e.g. a treasure only during a Treasure Run).
    /// Fractions are carried over per source, so e.g. x2/3 on 1-gold kills averages out exactly.
    /// </summary>
    public static class GoldEconomy
    {
        public static readonly List<Func<GoldSource, float>> Multipliers = new List<Func<GoldSource, float>>();

        private static readonly Stack<GoldSource> sources = new Stack<GoldSource>();
        private static readonly float[] carry = new float[4];

        public static GoldSource Current => sources.Count > 0 ? sources.Peek() : GoldSource.Other;

        internal static void Push(GoldSource s) => sources.Push(s);
        internal static void Pop() { if (sources.Count > 0) sources.Pop(); }

        internal static void Reset()
        {
            sources.Clear();
            Array.Clear(carry, 0, carry.Length);
        }

        internal static int Apply(int amount)
        {
            GoldSource src = Current;
            if (amount <= 0 || src == GoldSource.Other || Multipliers.Count == 0) return amount;

            float mult = 1f;
            foreach (Func<GoldSource, float> m in Multipliers)
            {
                try { mult *= m(src); }
                catch (Exception e) { Plugin.Log.LogError($"Gold multiplier failed: {e}"); }
            }
            if (mult == 1f) return amount;

            float exact = amount * mult + carry[(int)src];
            int result = (int)Math.Floor(exact);
            carry[(int)src] = exact - result;
            return Math.Max(0, result);
        }
    }
}

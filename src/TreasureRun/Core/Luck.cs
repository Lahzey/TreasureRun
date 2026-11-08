using System;

namespace TreasureRun.Core
{
    /// <summary>Every random check that luck can influence has a category.</summary>
    public enum LuckCategory
    {
        Crit,
        Stun,
        TreasureSlot,
        TreasureRarity,
        ChestDrops,
        MiniBossChest,
        TreeChest,
        University,
        POI,
        /// <summary>Chance-based treasure effects (chain lightning, extra levels, ...).</summary>
        Proc,
        GoldenGoblin,
    }

    /// <summary>
    /// Central RNG for Treasure Run. Each category can have extra rolls:
    /// +n = roll n+1 times and keep the best result (advantage), -n = keep the worst (disadvantage).
    /// A future "luck" treasure only has to raise <see cref="RunState.LuckBonus"/>.
    /// </summary>
    public static class Luck
    {
        /// <summary>Extra rolls for a category: its own bonus plus the "All" bonus.</summary>
        public static int ExtraRolls(LuckCategory category)
        {
            RunState run = TreasureRunMode.Run;
            if (run == null) return 0;
            run.LuckBonus.TryGetValue(category.ToString(), out int own);
            run.LuckBonus.TryGetValue("All", out int all);
            return own + all;
        }

        public static void AddExtraRolls(LuckCategory category, int amount) => AddExtraRolls(category.ToString(), amount);

        /// <summary>Use key "All" to affect every category.</summary>
        public static void AddExtraRolls(string key, int amount)
        {
            RunState run = TreasureRunMode.Run;
            if (run == null) return;
            run.LuckBonus.TryGetValue(key, out int current);
            run.LuckBonus[key] = current + amount;
        }

        /// <summary>Raw uniform roll in [0,1) from the run RNG (falls back to UnityEngine.Random outside a run).</summary>
        public static float Raw01()
        {
            RunState run = TreasureRunMode.Run;
            return run != null ? run.Rng.Next01() : UnityEngine.Random.value;
        }

        public static int RawRange(int minInclusive, int maxExclusive)
        {
            RunState run = TreasureRunMode.Run;
            return run != null ? run.Rng.Range(minInclusive, maxExclusive) : UnityEngine.Random.Range(minInclusive, maxExclusive);
        }

        /// <summary>Succeeds with probability <paramref name="chance"/> (0..1), modified by luck.</summary>
        public static bool Check(LuckCategory category, float chance)
        {
            return Roll01(category, lowIsGood: true) < chance;
        }

        /// <summary>Uniform roll in [0,1); with luck, the best of several rolls.</summary>
        public static float Roll01(LuckCategory category, bool lowIsGood)
        {
            return Best(category, Raw01, (a, b) => lowIsGood ? b.CompareTo(a) : a.CompareTo(b));
        }

        /// <summary>
        /// Generic best-of-n roll. <paramref name="compare"/> returns &gt; 0 when the first argument is better.
        /// </summary>
        public static T Best<T>(LuckCategory category, Func<T> roll, Comparison<T> compare)
        {
            int extra = ExtraRolls(category);
            T result = roll();
            for (int i = 0; i < Math.Abs(extra); i++)
            {
                T next = roll();
                int c = compare(next, result);
                if (extra > 0 ? c > 0 : c < 0) result = next;
            }
            return result;
        }
    }
}

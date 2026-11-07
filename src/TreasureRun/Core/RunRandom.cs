using System;

namespace TreasureRun.Core
{
    /// <summary>
    /// Mod-owned RNG (xorshift64*). Unlike System.Random its whole state is one ulong,
    /// so it can be saved and restored exactly (needed for save/resume later).
    /// </summary>
    [Serializable]
    public sealed class RunRandom
    {
        public ulong State;

        public RunRandom(ulong seed)
        {
            State = seed != 0 ? seed : 0x9E3779B97F4A7C15UL;
        }

        public ulong NextULong()
        {
            State ^= State >> 12;
            State ^= State << 25;
            State ^= State >> 27;
            return State * 0x2545F4914F6CDD1DUL;
        }

        /// <summary>Uniform float in [0, 1).</summary>
        public float Next01() => (NextULong() >> 40) / (float)(1UL << 24);

        /// <summary>Uniform int in [minInclusive, maxExclusive).</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return minInclusive + (int)(NextULong() % (ulong)(maxExclusive - minInclusive));
        }
    }
}

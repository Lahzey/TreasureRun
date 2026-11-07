using System;
using System.Collections.Generic;

namespace TreasureRun.Core
{
    /// <summary>
    /// All Treasure Run state for one run. Plain data only (no Unity object references),
    /// so it stays serializable for the planned save/resume feature.
    /// </summary>
    [Serializable]
    public sealed class RunState
    {
        public int FormatVersion = 1;
        public ulong Seed;
        public int GameMode;
        public bool MonsterMode;
        public string StartedUtc;

        public RunRandom Rng;

        /// <summary>Treasure ids in acquisition order.</summary>
        public List<string> OwnedTreasures = new List<string>();

        /// <summary>Extra luck rolls per <see cref="LuckCategory"/> name (positive = advantage, negative = disadvantage).</summary>
        public Dictionary<string, int> LuckBonus = new Dictionary<string, int>();

        /// <summary>Treasure offers shown since the last one containing Epic or better (pity timer).</summary>
        public int OffersSinceEpicOrBetter;

        /// <summary>Generic per-treasure state, e.g. "second_wind.used" = 1.</summary>
        public Dictionary<string, int> Counters = new Dictionary<string, int>();

        public int GetCounter(string key) => Counters.TryGetValue(key, out int v) ? v : 0;
        public void SetCounter(string key, int value) => Counters[key] = value;

        public static RunState CreateNew(int gameMode, bool monsterMode)
        {
            ulong seed = (ulong)DateTime.UtcNow.Ticks ^ ((ulong)Guid.NewGuid().GetHashCode() << 32);
            return new RunState
            {
                Seed = seed,
                GameMode = gameMode,
                MonsterMode = monsterMode,
                StartedUtc = DateTime.UtcNow.ToString("o"),
                Rng = new RunRandom(seed),
            };
        }
    }
}

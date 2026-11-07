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
        public List<string> OwnedTreasures = new List<string>();

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

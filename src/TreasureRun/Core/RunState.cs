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

        /// <summary>Omens on Expand buttons (marker grid position -> POI id), until the button is used.</summary>
        public List<PoiOmenRecord> PoiOmens = new List<PoiOmenRecord>();

        /// <summary>Placed Points of Interest, in placement order.</summary>
        public List<PoiRecord> Pois = new List<PoiRecord>();

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

    /// <summary>An omen: the POI that the Expand button at a grid position will place.</summary>
    [Serializable]
    public sealed class PoiOmenRecord
    {
        public string Id;
        public int X, Y;
        public int Wave;
    }

    /// <summary>A placed Point of Interest (plain data, for the planned save/resume).</summary>
    [Serializable]
    public sealed class PoiRecord
    {
        public string Id;
        /// <summary>Tile grid position (TileManager, 99x99) and rotation of the section.</summary>
        public int X, Y, Rotation;
        /// <summary>Exits of the generated section (SectionExits) and the seed of its terrain.</summary>
        public int Exits, Seed;
        public int Wave;
        /// <summary>POI state (e.g. "active" = 1, "kills").</summary>
        public Dictionary<string, float> Data = new Dictionary<string, float>();

        public float Get(string key) => Data.TryGetValue(key, out float v) ? v : 0f;
        public void Set(string key, float value) => Data[key] = value;
    }
}

using System;

namespace TreasureRun.Core
{
    /// <summary>
    /// Game moments raised by thin Harmony patches (see Patches/GameEventPatches.cs).
    /// Treasure effects subscribe here instead of patching the game themselves.
    /// All handlers are cleared when a run ends.
    /// </summary>
    public static class GameEvents
    {
        /// <summary>Raised after SpawnManager.StartNextWave (arg: new level).</summary>
        public static event Action<int> WaveStarted;

        /// <summary>Raised after SpawnManager.EndWave (arg: level that just ended).</summary>
        public static event Action<int> WaveEnded;

        /// <summary>Raised after a tower was added to or removed from TowerManager (arg: current tower count).</summary>
        public static event Action<int> TowersChanged;

        /// <summary>Number of towers currently registered with TowerManager during this run.</summary>
        public static int TowerCount { get; private set; }

        internal static void RaiseWaveStarted(int level) => Raise(WaveStarted, level);
        internal static void RaiseWaveEnded(int level) => Raise(WaveEnded, level);

        internal static void ChangeTowerCount(int delta)
        {
            TowerCount = Math.Max(0, TowerCount + delta);
            Raise(TowersChanged, TowerCount);
        }

        internal static void Reset()
        {
            WaveStarted = null;
            WaveEnded = null;
            TowersChanged = null;
            TowerCount = 0;
        }

        private static void Raise(Action<int> evt, int arg)
        {
            if (evt == null) return;
            foreach (Action<int> handler in evt.GetInvocationList())
            {
                try { handler(arg); }
                catch (Exception e) { Plugin.Log.LogError($"GameEvents handler failed: {e}"); }
            }
        }
    }
}

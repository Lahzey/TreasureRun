using System;

namespace TreasureRun.Core
{
    /// <summary>
    /// Run-level game moments raised by thin Harmony patches (see Patches/GameEventPatches.cs).
    /// Handlers are cleared at every run start/end; permanent modules re-attach in <see cref="Resubscribe"/>.
    /// Combat moments (hits, kills, spawns) live in <see cref="Combat"/>.
    /// </summary>
    public static class GameEvents
    {
        /// <summary>Raised after SpawnManager.StartNextWave (arg: new level).</summary>
        public static event Action<int> WaveStarted;

        /// <summary>Raised after SpawnManager.EndWave (arg: level that just ended).</summary>
        public static event Action<int> WaveEnded;

        /// <summary>Raised after a tower was added to or removed from TowerManager (arg: current tower count).</summary>
        public static event Action<int> TowersChanged;

        /// <summary>Raised when a tower is placed (after its first SetStats).</summary>
        public static event Action<Tower> TowerBuilt;

        /// <summary>Raised whenever a tower gains a level (natural XP or bought).</summary>
        public static event Action<Tower> TowerLeveled;

        /// <summary>Raised after the castle took damage (arg: damage in displayed health points).</summary>
        public static event Action<int> CastleDamaged;

        /// <summary>
        /// Permanent: invoked right after the per-run handlers were cleared. Modules that need run events
        /// subscribe here once at plugin start.
        /// </summary>
        public static event Action Resubscribe;

        /// <summary>Number of towers currently registered with TowerManager during this run.</summary>
        public static int TowerCount { get; private set; }

        private static readonly System.Collections.Generic.HashSet<Tower> towers = new System.Collections.Generic.HashSet<Tower>();

        /// <summary>All towers currently registered with TowerManager during this run.</summary>
        public static System.Collections.Generic.IReadOnlyCollection<Tower> Towers => towers;

        internal static void TrackTower(Tower t, bool added)
        {
            if (t == null) return;
            if (added) towers.Add(t); else towers.Remove(t);
        }

        internal static void RaiseWaveStarted(int level) => Raise(WaveStarted, level);
        internal static void RaiseWaveEnded(int level) => Raise(WaveEnded, level);
        internal static void RaiseTowerBuilt(Tower t) => Raise(TowerBuilt, t);
        internal static void RaiseTowerLeveled(Tower t) => Raise(TowerLeveled, t);
        internal static void RaiseCastleDamaged(int damage) => Raise(CastleDamaged, damage);

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
            TowerBuilt = null;
            TowerLeveled = null;
            CastleDamaged = null;
            TowerCount = 0;
            towers.Clear();

            if (Resubscribe == null) return;
            foreach (Action handler in Resubscribe.GetInvocationList())
            {
                try { handler(); }
                catch (Exception e) { Plugin.Log.LogError($"GameEvents resubscribe failed: {e}"); }
            }
        }

        private static void Raise<T>(Action<T> evt, T arg)
        {
            if (evt == null) return;
            foreach (Action<T> handler in evt.GetInvocationList())
            {
                try { handler(arg); }
                catch (Exception e) { Plugin.Log.LogError($"GameEvents handler failed: {e}"); }
            }
        }
    }
}

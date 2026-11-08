using System;
using System.Collections.Generic;
using TreasureRun.Core;

namespace TreasureRun.Treasures
{
    /// <summary>Owned treasures of the current run and their live effects.</summary>
    public static class TreasureInventory
    {
        private static readonly List<TreasureEffect> active = new List<TreasureEffect>();
        private static readonly Dictionary<string, int> counts = new Dictionary<string, int>();

        public static IReadOnlyList<TreasureEffect> Active => active;

        public static event Action<TreasureDef> TreasureGained;

        /// <summary>Fast ownership check (called from hot combat paths). False outside a Treasure Run.</summary>
        public static bool Owns(string id) => TreasureRunMode.Active && counts.ContainsKey(id);

        public static int CountOf(string id) => TreasureRunMode.Active && counts.TryGetValue(id, out int n) ? n : 0;

        public static int TotalOwned => TreasureRunMode.Run?.OwnedTreasures.Count ?? 0;

        internal static void Initialize()
        {
            TreasureRunMode.RunStarted += _ => Reset();
            TreasureRunMode.RunEnded += _ => Reset();
        }

        private static void Reset()
        {
            active.Clear();
            counts.Clear();
            GameEvents.Reset();
        }

        public static void Grant(TreasureDef def)
        {
            RunState run = TreasureRunMode.Run;
            if (run == null || def == null) return;

            run.OwnedTreasures.Add(def.Id);
            counts[def.Id] = CountOf(def.Id) + 1;

            TreasureEffect effect = def.CreateEffect?.Invoke();
            if (effect != null)
            {
                effect.Def = def;
                active.Add(effect);
                try { effect.OnAcquire(run, restoring: false); }
                catch (Exception e) { Plugin.Log.LogError($"Treasure '{def.Id}' OnAcquire failed: {e}"); }
            }

            Plugin.Log.LogInfo($"Treasure gained: {def.Name} ({def.Rarity})");
            try { TreasureGained?.Invoke(def); }
            catch (Exception e) { Plugin.Log.LogError($"TreasureGained handler failed: {e}"); }
        }
    }
}

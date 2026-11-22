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
        /// <summary>A copy was removed (sold at a merchant).</summary>
        public static event Action<TreasureDef> TreasureRemoved;

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

        /// <summary>
        /// Removes every copy of a treasure (dev tools and scenarios only). Passive effects that check ownership stop at
        /// once; one-time effects (gold, health) are not undone.
        /// </summary>
        public static void Revoke(string id)
        {
            RunState run = TreasureRunMode.Run;
            if (run == null || !counts.Remove(id)) return;
            run.OwnedTreasures.RemoveAll(x => x == id);
            active.RemoveAll(e => e.Def != null && e.Def.Id == id);
            Plugin.Log.LogInfo($"Treasure revoked: {id}");
        }

        /// <summary>Removes one copy of a treasure (selling): ownership checks see one copy fewer at once.</summary>
        public static bool RemoveOne(TreasureDef def)
        {
            RunState run = TreasureRunMode.Run;
            if (run == null || def == null || !counts.TryGetValue(def.Id, out int n)) return false;
            if (n <= 1) counts.Remove(def.Id); else counts[def.Id] = n - 1;
            run.OwnedTreasures.Remove(def.Id);
            int i = active.FindLastIndex(e => e.Def != null && e.Def.Id == def.Id);
            if (i >= 0) active.RemoveAt(i);
            Plugin.Log.LogInfo($"Treasure removed: {def.Name} ({def.Rarity})");
            try { TreasureRemoved?.Invoke(def); }
            catch (Exception e) { Plugin.Log.LogError($"TreasureRemoved handler failed: {e}"); }
            return true;
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

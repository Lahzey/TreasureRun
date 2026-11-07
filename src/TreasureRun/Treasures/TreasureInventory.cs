using System;
using System.Collections.Generic;
using System.Linq;
using TreasureRun.Core;

namespace TreasureRun.Treasures
{
    /// <summary>Owned treasures of the current run and their live effects.</summary>
    public static class TreasureInventory
    {
        private static readonly List<TreasureEffect> active = new List<TreasureEffect>();

        public static IReadOnlyList<TreasureEffect> Active => active;

        public static event Action<TreasureDef> TreasureGained;

        public static bool Owns(string id) => TreasureRunMode.Run != null && TreasureRunMode.Run.OwnedTreasures.Contains(id);

        public static int CountOf(string id) => TreasureRunMode.Run?.OwnedTreasures.Count(x => x == id) ?? 0;

        internal static void Initialize()
        {
            TreasureRunMode.RunStarted += _ => { active.Clear(); GameEvents.Reset(); };
            TreasureRunMode.RunEnded += _ => { active.Clear(); GameEvents.Reset(); };
        }

        public static void Grant(TreasureDef def)
        {
            RunState run = TreasureRunMode.Run;
            if (run == null || def == null) return;

            run.OwnedTreasures.Add(def.Id);
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

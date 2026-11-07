using System.Collections.Generic;
using System.Linq;
using TreasureRun.Treasures.Effects;

namespace TreasureRun.Treasures
{
    /// <summary>All treasure definitions. Add new treasures in <see cref="RegisterAll"/>.</summary>
    public static class TreasureRegistry
    {
        private static readonly Dictionary<string, TreasureDef> byId = new Dictionary<string, TreasureDef>();
        private static readonly List<TreasureDef> all = new List<TreasureDef>();

        public static IReadOnlyList<TreasureDef> All => all;

        public static TreasureDef Get(string id) => id != null && byId.TryGetValue(id, out var d) ? d : null;

        public static void Register(TreasureDef def)
        {
            if (byId.ContainsKey(def.Id))
            {
                Plugin.Log.LogError($"Duplicate treasure id '{def.Id}' ignored.");
                return;
            }
            byId.Add(def.Id, def);
            all.Add(def);
        }

        internal static void RegisterAll()
        {
            StarterTreasures.Register();

            string counts = string.Join(", ", all.GroupBy(d => d.Rarity).OrderBy(g => g.Key).Select(g => $"{g.Key} {g.Count()}"));
            Plugin.Log.LogInfo($"Registered {all.Count} treasures ({counts}).");
        }
    }
}

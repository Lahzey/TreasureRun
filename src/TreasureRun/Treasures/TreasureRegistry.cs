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
            def.Module = def.Module ?? currentModule;
            all.Add(def);
        }

        internal static void RegisterAll()
        {
            Module("Starter", StarterTreasures.Register); // M2 placeholders, to be reviewed in M9
            Module("Economy", EconomyTreasures.Register);
            Module("Combat", CombatTreasures.Register);
            Module("Build", BuildTreasures.Register);
            Module("Meta", MetaTreasures.Register);
            Module("Castle", CastleTreasures.Register);

            // Content tags used by castle bans (docs/content-rules.md).
            AddTags(Cards.ContentTags.KillGold, "scavenger", "bounty_board", "diverse_portfolio", "alchemists_furnace", "jackpot");
            AddTags(Cards.ContentTags.CastleHealth, "sturdy_walls", "bulwark", "second_wind", "retribution");

            string counts = string.Join(", ", all.GroupBy(d => d.Rarity).OrderBy(g => g.Key).Select(g => $"{g.Key} {g.Count()}"));
            Plugin.Log.LogInfo($"Registered {all.Count} treasures ({counts}).");
        }

        private static string currentModule;

        private static void Module(string name, System.Action register)
        {
            currentModule = name;
            register();
            currentModule = null;
        }

        private static void AddTags(string tag, params string[] ids)
        {
            foreach (string id in ids)
            {
                TreasureDef d = Get(id);
                if (d == null) { Plugin.Log.LogWarning($"AddTags: unknown treasure '{id}'."); continue; }
                if (!d.Tags.Contains(tag)) d.Tags = d.Tags.Concat(new[] { tag }).ToArray();
            }
        }

        /// <summary>Attaches the permanent event handlers / stat providers of all treasure modules.</summary>
        internal static void HookAll()
        {
            StarterTreasurePatches.Hook();
            EconomyTreasures.Hook();
            CombatTreasures.Hook();
            BuildTreasures.Hook();
            MetaTreasures.Hook();
            CastleTreasures.Hook();
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using TreasureRun.Core;
using UnityEngine;

namespace TreasureRun.Castles
{
    /// <summary>All selectable castles, in carousel order.</summary>
    public static class CastleRegistry
    {
        public const string DefaultId = "rogue_tower";

        private static readonly List<CastleDef> all = new List<CastleDef>();
        public static IReadOnlyList<CastleDef> All => all;

        public static CastleDef Get(string id) => all.FirstOrDefault(c => c.Id == id);
        public static CastleDef Default => Get(DefaultId);

        public static void Register(CastleDef def)
        {
            if (Get(def.Id) != null) { Plugin.Log.LogError($"Duplicate castle id '{def.Id}'."); return; }
            all.Add(def);
        }
    }

    /// <summary>
    /// Castle selection (persisted) and the castle active in the current game scene. Unlike Treasure Run state,
    /// castles work in every mode. The active castle is snapshotted when GameScene starts loading.
    /// </summary>
    public static class CastleRuntime
    {
        public const string SelectedPrefKey = "TR_Castle";

        /// <summary>Castle chosen in the menu (may be unavailable for the current settings).</summary>
        public static CastleDef Selected
        {
            get => CastleRegistry.Get(PlayerPrefs.GetString(SelectedPrefKey, CastleRegistry.DefaultId)) ?? CastleRegistry.Default;
            set => PlayerPrefs.SetString(SelectedPrefKey, value?.Id ?? CastleRegistry.DefaultId);
        }

        /// <summary>Castle of the running game scene (default castle outside a game).</summary>
        public static CastleDef Active { get; private set; }
        public static CastleBehaviour Behaviour { get; private set; }

        /// <summary>Called from the LevelLoader.LoadLevel prefix (after Treasure Run state is set).</summary>
        internal static void OnSceneLoadRequested(string sceneName)
        {
            Behaviour = null;
            Active = null;
            if (sceneName != TreasureRunMode.GameSceneName) return;

            CastleDef def = Selected;
            if (!def.AvailableIn(CastleContext.FromPrefs()))
            {
                Plugin.Log.LogWarning($"Castle '{def.Name}' is not available with the current settings; using {CastleRegistry.Default.Name}.");
                def = CastleRegistry.Default;
            }
            Active = def;
            Behaviour = def.CreateBehaviour?.Invoke();
            if (Behaviour != null) Behaviour.Def = def;
            Plugin.Log.LogInfo($"Castle: {def.Name}");
        }
    }
}

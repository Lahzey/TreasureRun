using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace TreasureRun.Pois
{
    /// <summary>Hooks of the Points of Interest into the vanilla map growth (TileSpawnLocation / TileManager).</summary>
    [HarmonyPatch]
    internal static class PoiPatches
    {
        /// <summary>A new Expand button: roll its omen (or show the one it has).</summary>
        [HarmonyPostfix, HarmonyPatch(typeof(TileSpawnLocation), "Start")]
        private static void MarkerStarted(TileSpawnLocation __instance)
        {
            try { PoiDirector.OnMarker(__instance); }
            catch (System.Exception e) { Plugin.Log.LogError($"POI omen failed: {e}"); }
        }

        /// <summary>An Expand button is used: remember its omen for the tile vanilla is about to place.</summary>
        [HarmonyPrefix, HarmonyPatch(typeof(TileSpawnLocation), nameof(TileSpawnLocation.SpawnTile))]
        private static void Expanding(TileSpawnLocation __instance)
        {
            try { PoiDirector.OnExpand(__instance); }
            catch (System.Exception e) { Plugin.Log.LogError($"POI expand failed: {e}"); }
        }

        /// <summary>The tile is placed (before the wave starts): build the POI section on it.</summary>
        [HarmonyPostfix, HarmonyPatch(typeof(TileManager), nameof(TileManager.SpawnNewTile))]
        private static void TileSpawned(int posX, int posY, int eulerAngle)
        {
            try { PoiDirector.OnTileSpawned(posX, posY, eulerAngle); }
            catch (System.Exception e) { Plugin.Log.LogError($"POI section failed: {e}"); }
        }

        /// <summary>Debug (console <c>poi exits</c>): restricts the tile choice to one exit combination.</summary>
        [HarmonyPostfix, HarmonyPatch(typeof(TileManager), "SpawnSelectionNormal")]
        private static void FilterNormal(ref List<GameObject> spawnList) => Filter(spawnList);

        [HarmonyPostfix, HarmonyPatch(typeof(TileManager), "SpawnSelectionSplitMode")]
        private static void FilterSplit(ref List<GameObject> spawnList) => Filter(spawnList);

        private static void Filter(List<GameObject> list)
        {
            if (PoiDirector.ForceExits == null || list == null) return;
            SectionExits want = PoiDirector.ForceExits.Value;
            List<GameObject> match = list.FindAll(go => go != null && go.GetComponent<TerrainTile>() is TerrainTile t && SectionBuilder.ExitsOf(t) == want);
            if (match.Count == 0) return;
            list.Clear();
            list.AddRange(match);
        }
    }
}

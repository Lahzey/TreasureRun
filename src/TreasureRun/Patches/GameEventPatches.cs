using HarmonyLib;
using TreasureRun.Core;

namespace TreasureRun.Patches
{
    /// <summary>Thin patches that turn game moments into <see cref="GameEvents"/>. Only active during a Treasure Run.</summary>
    [HarmonyPatch]
    internal static class GameEventPatches
    {
        [HarmonyPostfix, HarmonyPatch(typeof(SpawnManager), nameof(SpawnManager.StartNextWave))]
        private static void StartNextWavePostfix(SpawnManager __instance)
        {
            if (TreasureRunMode.Active) GameEvents.RaiseWaveStarted(__instance.level);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(SpawnManager), nameof(SpawnManager.EndWave))]
        private static void EndWavePostfix(SpawnManager __instance)
        {
            if (TreasureRunMode.Active) GameEvents.RaiseWaveEnded(__instance.level);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(TowerManager), nameof(TowerManager.AddNewTower))]
        private static void AddNewTowerPostfix()
        {
            if (TreasureRunMode.Active) GameEvents.ChangeTowerCount(+1);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(TowerManager), nameof(TowerManager.RemoveTower))]
        private static void RemoveTowerPostfix()
        {
            if (TreasureRunMode.Active) GameEvents.ChangeTowerCount(-1);
        }
    }
}

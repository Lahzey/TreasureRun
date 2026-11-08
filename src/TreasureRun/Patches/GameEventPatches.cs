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
        private static void AddNewTowerPostfix(Tower t)
        {
            if (!TreasureRunMode.Active) return;
            GameEvents.TrackTower(t, added: true);
            GameEvents.ChangeTowerCount(+1);
            GameEvents.RaiseTowerBuilt(t);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(TowerManager), nameof(TowerManager.RemoveTower))]
        private static void RemoveTowerPostfix(Tower t)
        {
            if (!TreasureRunMode.Active) return;
            GameEvents.TrackTower(t, added: false);
            GameEvents.ChangeTowerCount(-1);
        }

        private static int levelUpDepth;

        /// <summary>Every level-up (XP or bought) ends in Tower.LevelUpText.</summary>
        [HarmonyPostfix, HarmonyPatch(typeof(Tower), nameof(Tower.LevelUpText))]
        private static void LevelUpTextPostfix(Tower __instance)
        {
            if (!TreasureRunMode.Active || levelUpDepth > 3) return;
            levelUpDepth++;
            try { GameEvents.RaiseTowerLeveled(__instance); }
            finally { levelUpDepth--; }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(GameManager), nameof(GameManager.TakeDamage))]
        private static void CastleTakeDamagePostfix(int damage)
        {
            if (TreasureRunMode.Active && damage > 0) GameEvents.RaiseCastleDamaged(damage);
        }
    }
}

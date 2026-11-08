using HarmonyLib;
using TreasureRun.Cards;
using TreasureRun.Core;
using TreasureRun.Treasures;

namespace TreasureRun.Patches
{
    /// <summary>
    /// Stat modifiers applied on top of the vanilla tower stat calculation (first step towards the M4 stat layer).
    /// Tower.SetStats is the base every tower override calls first; Dropper (Shredder) recomputes its
    /// fire rate afterwards, so attack speed is re-applied there.
    /// </summary>
    [HarmonyPatch]
    internal static class TowerStatPatches
    {
        /// <summary>Attack speed multiplier from Undivided Attention: +100% shared across all towers.</summary>
        public static float AttackSpeedMultiplier
        {
            get
            {
                if (CardCounters.Get(CustomCardIds.UndividedAttention) <= 0) return 1f;
                int towers = System.Math.Max(1, GameEvents.TowerCount);
                return 1f + 1f / towers;
            }
        }

        /// <summary>True while any modifier depends on the number of towers.</summary>
        private static bool DependsOnTowerCount =>
            TreasureInventory.Owns("hivemind") || CardCounters.Get(CustomCardIds.UndividedAttention) > 0;

        [HarmonyPostfix, HarmonyPatch(typeof(Tower), nameof(Tower.SetStats))]
        private static void TowerSetStatsPostfix(Tower __instance)
        {
            if (!TreasureRunMode.Active) return;

            int hivemind = TreasureInventory.CountOf("hivemind");
            if (hivemind > 0) __instance.damage += hivemind * (GameEvents.TowerCount / 3);

            ApplyAttackSpeed(__instance);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Dropper), nameof(Dropper.SetStats))]
        private static void DropperSetStatsPostfix(Dropper __instance)
        {
            if (TreasureRunMode.Active) ApplyAttackSpeed(__instance);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Dropper), nameof(Dropper.SetDropPoints))]
        private static void DropperSetDropPointsPostfix(Dropper __instance)
        {
            if (TreasureRunMode.Active) ApplyAttackSpeed(__instance);
        }

        /// <summary>rps is "seconds per shot", freshly computed by the patched method; divide to speed up.</summary>
        private static void ApplyAttackSpeed(Tower tower)
        {
            float mult = AttackSpeedMultiplier;
            if (mult != 1f && tower.rps > 0f) tower.rps /= mult;
        }

        // Tower count changed: refresh stats that depend on it. (GameEventPatches updates the count first.)
        [HarmonyPostfix, HarmonyPatch(typeof(TowerManager), nameof(TowerManager.AddNewTower)), HarmonyPriority(Priority.Low)]
        private static void AddNewTowerPostfix(TowerManager __instance)
        {
            if (TreasureRunMode.Active && DependsOnTowerCount) __instance.UpdateAllTowers();
        }

        [HarmonyPostfix, HarmonyPatch(typeof(TowerManager), nameof(TowerManager.RemoveTower)), HarmonyPriority(Priority.Low)]
        private static void RemoveTowerPostfix(TowerManager __instance)
        {
            if (TreasureRunMode.Active && DependsOnTowerCount) __instance.UpdateAllTowers();
        }
    }
}

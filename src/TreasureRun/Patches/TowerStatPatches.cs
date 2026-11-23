using HarmonyLib;
using TreasureRun.Cards;
using TreasureRun.Core;
using TreasureRun.Treasures;

namespace TreasureRun.Patches
{
    /// <summary>
    /// Hooks the <see cref="StatLayer"/> into the vanilla stat calculation. Tower.SetStats is the base every tower
    /// override calls first; Dropper (Shredder) recomputes its fire rate afterwards, so attack speed is re-applied there.
    /// </summary>
    [HarmonyPatch]
    internal static class TowerStatPatches
    {
        /// <summary>
        /// Drops towers that were destroyed without being demolished (scenarios, dev tools) before vanilla recomputes the
        /// stats of every tower of a type: a destroyed tower left in the set makes every later stat refresh throw.
        /// </summary>
        [HarmonyPrefix, HarmonyPatch(typeof(TowerFlyweight), nameof(TowerFlyweight.UpdateTowerStats))]
        private static void UpdateTowerStatsPrefix(TowerFlyweight __instance) => __instance.towers?.RemoveWhere(t => t == null);

        internal static void Initialize()
        {
            // Hivemind (placeholder treasure): +1 damage per 3 towers.
            StatLayer.Register("hivemind", (t, b) =>
            {
                int n = TreasureInventory.CountOf("hivemind");
                if (n > 0) b.Flat(TowerStat.Damage, n * (GameEvents.TowerCount / 3));
            });

            // Undivided Attention (card): +100% attack speed shared across all towers.
            StatLayer.Register("undivided_attention", (t, b) =>
            {
                if (CardCounters.Get(CustomCardIds.UndividedAttention) > 0)
                    b.Percent(TowerStat.AttackSpeed, 1f / System.Math.Max(1, GameEvents.TowerCount));
            });

            // Treasures may change stat providers' output: refresh after each gain.
            TreasureInventory.TreasureGained += _ => StatLayer.Refresh();
            TreasureInventory.TreasureRemoved += _ => StatLayer.Refresh();
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Tower), nameof(Tower.SetStats))]
        private static void TowerSetStatsPostfix(Tower __instance)
        {
            if (TreasureRunMode.Active) StatLayer.ApplyAll(__instance);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Dropper), nameof(Dropper.SetStats))]
        private static void DropperSetStatsPostfix(Dropper __instance)
        {
            if (TreasureRunMode.Active) StatLayer.ApplyAttackSpeedOnly(__instance);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Dropper), nameof(Dropper.SetDropPoints))]
        private static void DropperSetDropPointsPostfix(Dropper __instance)
        {
            if (TreasureRunMode.Active) StatLayer.ApplyAttackSpeedOnly(__instance);
        }

        // Tower count changed: many modifiers depend on it. (GameEventPatches updates the count first.)
        [HarmonyPostfix, HarmonyPatch(typeof(TowerManager), nameof(TowerManager.AddNewTower)), HarmonyPriority(Priority.Low)]
        private static void AddNewTowerPostfix() => StatLayer.Refresh();

        [HarmonyPostfix, HarmonyPatch(typeof(TowerManager), nameof(TowerManager.RemoveTower)), HarmonyPriority(Priority.Low)]
        private static void RemoveTowerPostfix() => StatLayer.Refresh();
    }
}

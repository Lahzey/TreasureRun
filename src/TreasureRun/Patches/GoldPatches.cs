using HarmonyLib;
using TreasureRun.Core;

namespace TreasureRun.Patches
{
    /// <summary>Marks gold sources and applies <see cref="GoldEconomy"/> multipliers in ResourceManager.AddMoney. Active in every mode.</summary>
    [HarmonyPatch]
    internal static class GoldPatches
    {
        [HarmonyPrefix, HarmonyPatch(typeof(Enemy), nameof(Enemy.Die)), HarmonyPriority(Priority.First)]
        private static void DiePrefix() => GoldEconomy.Push(GoldSource.Kill);

        [HarmonyFinalizer, HarmonyPatch(typeof(Enemy), nameof(Enemy.Die))]
        private static void DieFinalizer() => GoldEconomy.Pop();

        [HarmonyPrefix, HarmonyPatch(typeof(IncomeGenerator), nameof(IncomeGenerator.GenerateIncome))]
        private static void IncomePrefix() => GoldEconomy.Push(GoldSource.Income);

        [HarmonyFinalizer, HarmonyPatch(typeof(IncomeGenerator), nameof(IncomeGenerator.GenerateIncome))]
        private static void IncomeFinalizer() => GoldEconomy.Pop();

        [HarmonyPrefix, HarmonyPatch(typeof(TreeUI), nameof(TreeUI.Chop))]
        private static void ChopPrefix() => GoldEconomy.Push(GoldSource.Tree);

        [HarmonyFinalizer, HarmonyPatch(typeof(TreeUI), nameof(TreeUI.Chop))]
        private static void ChopFinalizer() => GoldEconomy.Pop();

        [HarmonyPrefix, HarmonyPatch(typeof(ResourceManager), nameof(ResourceManager.AddMoney))]
        private static void AddMoneyPrefix(ref int addedGold) => addedGold = GoldEconomy.Apply(addedGold);

        [HarmonyPostfix, HarmonyPatch(typeof(LevelLoader), nameof(LevelLoader.LoadLevel))]
        private static void LoadLevelPostfix() => GoldEconomy.Reset();
    }
}

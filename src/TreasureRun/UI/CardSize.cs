using HarmonyLib;
using TreasureRun.Core;
using UnityEngine;

namespace TreasureRun.UI
{
    /// <summary>
    /// Bigger cards in every card panel of a Treasure Run (level-up cards, treasure offers, monster cards): vanilla
    /// places the holders 200 units apart at scale 1 (<c>CardManager.ArrangeCards</c>); they are scaled up and spread
    /// out by the same factor, so descriptions get more room on screen.
    /// </summary>
    [HarmonyPatch]
    internal static class CardSize
    {
        public const float Scale = 1.25f;
        private const float VanillaSpacing = 200f;

        [HarmonyPostfix, HarmonyPatch(typeof(CardManager), nameof(CardManager.ArrangeCards))]
        private static void ArrangeCardsPostfix(CardManager __instance, int count) => Arrange(__instance.cardHolders, count);

        [HarmonyPostfix, HarmonyPatch(typeof(CardManager), nameof(CardManager.ArrangeMonsterCards))]
        private static void ArrangeMonsterCardsPostfix(CardManager __instance, int count) => Arrange(__instance.monsterCardHolders, count);

        private static void Arrange(GameObject[] holders, int count)
        {
            if (!TreasureRunMode.Active || holders == null) return;
            for (int k = 0; k < count && k < holders.Length; k++)
            {
                if (holders[k] == null) continue;
                Transform t = holders[k].transform;
                t.localScale = Vector3.one * Scale;
                t.localPosition = new Vector3((VanillaSpacing * k - VanillaSpacing / 2f * count + VanillaSpacing / 2f) * Scale, 0f, 0f);
            }
        }
    }
}

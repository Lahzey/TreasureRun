using HarmonyLib;
using TreasureRun.Cards;
using TreasureRun.Core;
using TreasureRun.UI;

namespace TreasureRun.Patches
{
    /// <summary>
    /// Card panel integration for Treasure Run:
    ///  - CardManager.Start: rebuild the card pool (whitelist + custom cards),
    ///  - chests offer treasures instead of cards,
    ///  - level-up draws can contain treasure slots,
    ///  - card picks are routed to treasure offers / treasure slots / vanilla.
    /// </summary>
    [HarmonyPatch]
    internal static class CardAndChestPatches
    {
        [HarmonyPostfix, HarmonyPatch(typeof(CardManager), nameof(CardManager.Start))]
        private static void CardManagerStartPostfix(CardManager __instance)
        {
            if (!TreasureRunMode.Active) return;
            CardPanel.Capture(__instance);
            CardPool.Build(__instance);
            InventoryBar.Create(__instance);
            Voxels.ModelOverrides.ApplyAll();
        }

        /// <summary>Opening a chest: one treasure offer per drop instead of one card draw per drop.</summary>
        [HarmonyPrefix, HarmonyPatch(typeof(TreasureUI), nameof(TreasureUI.Open))]
        private static bool TreasureUIOpenPrefix(TreasureUI __instance)
        {
            if (!TreasureRunMode.Active) return true;

            SFXManager.instance.PlaySound(Sound.CritBig, __instance.myChest.transform.position);
            int drops = __instance.myChest.GetComponent<TreasureChest>().numberOfDrops;
            for (int i = 0; i < drops; i++) TreasureOfferUI.RequestOffer();
            __instance.Die();
            return false;
        }

        /// <summary>A card select button was pressed. Routes to treasure offer, treasure slot, or vanilla card pick.</summary>
        [HarmonyPrefix, HarmonyPatch(typeof(CardManager), nameof(CardManager.ActivateCard))]
        private static bool ActivateCardPrefix(CardManager __instance, int index, out bool __state)
        {
            __state = false;
            if (!TreasureRunMode.Active) return true;

            if (TreasureOfferUI.IsShowing)
            {
                TreasureOfferUI.Choose(index);
                __state = true;
                return false;
            }
            if (LevelUpTreasureSlots.TryChoose(__instance, index))
            {
                __state = true;
                return false;
            }

            // Vanilla card pick.
            CardPool.OnCardPicked(__instance.cards[index]);
            LevelUpTreasureSlots.Clear();
            CardPanel.Restore(__instance);
            return true;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(CardManager), nameof(CardManager.ActivateCard))]
        private static void ActivateCardPostfix(CardManager __instance, bool __state)
        {
            if (__state || !TreasureRunMode.Active) return;
            CardPool.CleanUpPool(__instance);
            TreasureOfferUI.TryShowNext();
        }

        [HarmonyPostfix, HarmonyPatch(typeof(CardManager), nameof(CardManager.ActivateMonsterCard))]
        private static void ActivateMonsterCardPostfix()
        {
            if (TreasureRunMode.Active) TreasureOfferUI.TryShowNext();
        }

        /// <summary>Vanilla card draws start from the vanilla look...</summary>
        [HarmonyPrefix, HarmonyPatch(typeof(CardManager), nameof(CardManager.DisplayCards))]
        private static void DisplayCardsPrefix(CardManager __instance)
        {
            if (TreasureRunMode.Active) CardPanel.Restore(__instance);
        }

        /// <summary>...then each slot rolls whether it becomes a treasure.</summary>
        [HarmonyPostfix, HarmonyPatch(typeof(CardManager), nameof(CardManager.DisplayCards))]
        private static void DisplayCardsPostfix(CardManager __instance, int count)
        {
            if (TreasureRunMode.Active) LevelUpTreasureSlots.Roll(__instance, count);
        }
    }
}

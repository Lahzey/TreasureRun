using HarmonyLib;
using TreasureRun.Core;
using TreasureRun.UI;

namespace TreasureRun.Patches
{
    /// <summary>
    /// Treasure chests offer treasures instead of cards, and the card panel is shared between
    /// vanilla card draws and treasure offers.
    /// </summary>
    [HarmonyPatch]
    internal static class CardAndChestPatches
    {
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

        /// <summary>A card select button was pressed: if a treasure offer is showing, it is a treasure pick.</summary>
        [HarmonyPrefix, HarmonyPatch(typeof(CardManager), nameof(CardManager.ActivateCard))]
        private static bool ActivateCardPrefix(int index, out bool __state)
        {
            __state = TreasureRunMode.Active && TreasureOfferUI.IsShowing;
            if (!__state) return true;
            TreasureOfferUI.Choose(index);
            return false;
        }

        /// <summary>After a vanilla card pick, show a queued treasure offer if the panel became free.</summary>
        [HarmonyPostfix, HarmonyPatch(typeof(CardManager), nameof(CardManager.ActivateCard))]
        private static void ActivateCardPostfix(bool __state)
        {
            if (!__state && TreasureRunMode.Active) TreasureOfferUI.TryShowNext();
        }

        [HarmonyPostfix, HarmonyPatch(typeof(CardManager), nameof(CardManager.ActivateMonsterCard))]
        private static void ActivateMonsterCardPostfix()
        {
            if (TreasureRunMode.Active) TreasureOfferUI.TryShowNext();
        }

        /// <summary>Vanilla card draws must look vanilla even after a treasure offer recolored the cards.</summary>
        [HarmonyPrefix, HarmonyPatch(typeof(CardManager), nameof(CardManager.DisplayCards))]
        private static void DisplayCardsPrefix(CardManager __instance)
        {
            if (TreasureRunMode.Active) TreasureOfferUI.RestoreOriginalStyles(__instance);
        }
    }
}

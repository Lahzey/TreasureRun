using System.Collections.Generic;
using TreasureRun.Cards;
using TreasureRun.Core;
using TreasureRun.Treasures;
using UnityEngine;

namespace TreasureRun.UI
{
    /// <summary>
    /// Level-up draws: after vanilla filled the hand, each card slot independently rolls whether it is
    /// replaced by a treasure. Treasures are rolled exactly like a chest offer (rarity, luck, pity, exclusions).
    /// The player still picks exactly one slot; replaced cards stay in the card pool.
    /// </summary>
    internal static class LevelUpTreasureSlots
    {
        public const float BaseChance = 0.03f;
        public const float ChancePerLevel = 0.006f;
        public const float ChancePerTreasureSense = 0.05f;

        private static readonly TreasureDef[] slots = new TreasureDef[6];

        /// <summary>Per-slot chance to become a treasure at the given level (no cap).</summary>
        public static float Chance(int level) =>
            BaseChance + ChancePerLevel * level + ChancePerTreasureSense * CardCounters.Get(CustomCardIds.TreasureSense);

        public static TreasureDef Get(int index) => index >= 0 && index < slots.Length ? slots[index] : null;

        public static void Clear()
        {
            for (int i = 0; i < slots.Length; i++) slots[i] = null;
        }

        /// <summary>Called after vanilla CardManager.DisplayCards(count) in a Treasure Run.</summary>
        public static void Roll(CardManager cm, int count)
        {
            Clear();
            int level = GameHelpers.Level;
            float chance = Chance(level);

            var hitSlots = new List<int>();
            for (int i = 0; i < count && i < slots.Length; i++)
                if (Luck.Check(LuckCategory.TreasureSlot, chance)) hitSlots.Add(i);
            if (hitSlots.Count == 0) return;

            // One draft for all hit slots: distinct treasures, pity counted once per level-up.
            List<TreasureDef> treasures = TreasureDraft.RollOffer(hitSlots.Count, level);
            for (int k = 0; k < treasures.Count; k++)
            {
                int slot = hitSlots[k];
                slots[slot] = treasures[k];
                CardPanel.StyleAsTreasure(cm, slot, treasures[k]);
            }
            Plugin.Log.LogInfo($"Level-up: {treasures.Count} of {count} slots became treasures (chance {chance:P0}).");
        }

        /// <summary>Called from the ActivateCard prefix. Returns true if the pick was a treasure slot (handled).</summary>
        public static bool TryChoose(CardManager cm, int index)
        {
            TreasureDef treasure = Get(index);
            if (treasure == null) return false;

            Clear();
            TreasureInventory.Grant(treasure);
            CardPanel.FinishPick(cm);
            return true;
        }
    }
}

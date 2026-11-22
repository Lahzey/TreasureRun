using System;
using TreasureRun.Ascensions;
using TreasureRun.Core;

namespace TreasureRun.Treasures
{
    /// <summary>
    /// Selling owned treasures (Wandering Merchant, docs/poi-plan.md §5). A sold copy is removed: effects that check
    /// ownership stop at once, <see cref="TreasureDef.OnSell"/> undoes lasting vanilla changes, and every tower recomputes
    /// its stats. Not sellable: treasures with <see cref="TreasureDef.NoSale"/> (gold, health, a paid cost, locked prices)
    /// and ascensions (bound to the towers that carry them).
    /// </summary>
    public static class TreasureSale
    {
        public const string AscensionReason = "Ascensions are bound to your towers.";

        /// <summary>Null if the treasure can be sold, else why not.</summary>
        public static string WhyNot(TreasureDef def)
        {
            if (def == null) return "Unknown treasure.";
            if (AscensionRegistry.ForTreasure(def) != null) return AscensionReason;
            return def.NoSale;
        }

        public static bool CanSell(TreasureDef def) => WhyNot(def) == null && TreasureInventory.CountOf(def.Id) > 0;

        /// <summary>Removes one copy (no gold: the merchant pays). Returns false if it can't be sold.</summary>
        public static bool Sell(TreasureDef def)
        {
            if (!CanSell(def)) return false;
            try { def.OnSell?.Invoke(); }
            catch (Exception e) { Plugin.Log.LogError($"Treasure '{def.Id}' OnSell failed: {e}"); }
            if (!TreasureInventory.RemoveOne(def)) return false;
            StatLayer.Refresh();
            return true;
        }
    }
}

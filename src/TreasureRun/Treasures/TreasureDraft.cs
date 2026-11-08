using System.Collections.Generic;
using System.Linq;
using TreasureRun.Core;

namespace TreasureRun.Treasures
{
    /// <summary>Rolls treasure offers: rarity roll (luck-aware), pool filtering, pity timer.</summary>
    public static class TreasureDraft
    {
        /// <summary>After this many offers without Epic+, the next offer guarantees one Epic+ slot.</summary>
        public const int PityThreshold = 4;

        /// <summary>
        /// Rolls <paramref name="count"/> distinct treasures. Used for chest offers, level-up treasure slots
        /// and cards that grant treasures. <paramref name="minimumRarity"/> raises the floor for every slot.
        /// </summary>
        public static List<TreasureDef> RollOffer(int count, int level, Rarity minimumRarity = Rarity.Common)
        {
            RunState run = TreasureRunMode.Run;
            var offer = new List<TreasureDef>();
            if (run == null) return offer;

            bool pity = run.OffersSinceEpicOrBetter >= PityThreshold;

            for (int i = 0; i < count; i++)
            {
                Rarity minimum = pity && i == 0 && minimumRarity < Rarity.Epic ? Rarity.Epic : minimumRarity;
                Rarity rolled = Luck.Best(LuckCategory.TreasureRarity, () => RollRarity(level, minimum), (a, b) => a.CompareTo(b));
                TreasureDef pick = PickFromRarityOrNearest(rolled, run, offer);
                if (pick == null) break;
                offer.Add(pick);
            }

            ApplyPrism(offer, run);

            if (offer.Any(t => t.Rarity >= Rarity.Epic)) run.OffersSinceEpicOrBetter = 0;
            else run.OffersSinceEpicOrBetter++;

            // The pity pick is always rolled first; shuffle so it lands in a random slot.
            Shuffle(offer);
            return offer;
        }

        /// <summary>
        /// Prism of Ascendance: one random slot (per Prism owned) is replaced by a treasure one rarity higher.
        /// This is the only natural source of Mythical treasures.
        /// </summary>
        private static void ApplyPrism(List<TreasureDef> offer, RunState run)
        {
            int prisms = TreasureInventory.CountOf(Effects.MetaTreasures.PrismId);
            var upgraded = new HashSet<int>();
            for (int p = 0; p < prisms && upgraded.Count < offer.Count; p++)
            {
                int idx;
                do idx = Luck.RawRange(0, offer.Count); while (upgraded.Contains(idx));
                upgraded.Add(idx);

                TreasureDef current = offer[idx];
                if (current.Rarity >= Rarity.Mythical) continue;
                var others = offer.Where((t, i) => i != idx).ToList();
                TreasureDef better = PickWeighted(Available(current.Rarity + 1, run, others));
                if (better != null) offer[idx] = better;
            }
        }

        public static Rarity RollRarity(int level, Rarity minimum)
        {
            float total = 0f;
            for (int r = (int)minimum; r < RarityInfo.Count; r++) total += RarityInfo.Weight((Rarity)r, level);
            if (total <= 0f) return minimum;

            float roll = Luck.Raw01() * total;
            for (int r = (int)minimum; r < RarityInfo.Count; r++)
            {
                roll -= RarityInfo.Weight((Rarity)r, level);
                if (roll < 0f) return (Rarity)r;
            }
            return minimum;
        }

        /// <summary>Picks from the rolled rarity; if empty, searches downward first, then upward.</summary>
        private static TreasureDef PickFromRarityOrNearest(Rarity rarity, RunState run, List<TreasureDef> alreadyOffered)
        {
            for (int r = (int)rarity; r >= 0; r--)
            {
                var pick = PickWeighted(Available((Rarity)r, run, alreadyOffered));
                if (pick != null) return pick;
            }
            for (int r = (int)rarity + 1; r < RarityInfo.Count; r++)
            {
                if ((Rarity)r == Rarity.Mythical && RarityInfo.Weight(Rarity.Mythical, 1) <= 0f) break;
                var pick = PickWeighted(Available((Rarity)r, run, alreadyOffered));
                if (pick != null) return pick;
            }
            return null;
        }

        public static List<TreasureDef> Available(Rarity rarity, RunState run, List<TreasureDef> alreadyOffered)
        {
            var ownedGroups = new HashSet<string>(run.OwnedTreasures
                .Select(TreasureRegistry.Get)
                .Where(d => d?.ExclusionGroup != null)
                .Select(d => d.ExclusionGroup));
            var offeredGroups = new HashSet<string>(alreadyOffered.Where(d => d.ExclusionGroup != null).Select(d => d.ExclusionGroup));

            return TreasureRegistry.All.Where(d =>
                d.Rarity == rarity
                && !alreadyOffered.Contains(d)
                && !(d.Unique && run.OwnedTreasures.Contains(d.Id))
                && (d.ExclusionGroup == null || (!ownedGroups.Contains(d.ExclusionGroup) && !offeredGroups.Contains(d.ExclusionGroup)))
                && (d.IsAvailable == null || d.IsAvailable(run))
                && IsOfferable(d)
            ).ToList();
        }

        /// <summary>Global rules: castle-exclusive treasures, castle bans by tag, affordable max-health cost.</summary>
        public static bool IsOfferable(TreasureDef d)
        {
            Castles.CastleBehaviour castle = Castles.CastleRuntime.Behaviour;
            if (d.Castle != null && d.Castle != Castles.CastleRuntime.Active?.Id) return false;
            if (castle != null && castle.BannedTagSet.Count > 0 && d.Tags.Any(castle.BannedTagSet.Contains)) return false;
            return Cards.HealthCost.CanPay(Cards.HealthCost.Of(d));
        }

        private static TreasureDef PickWeighted(List<TreasureDef> candidates)
        {
            if (candidates.Count == 0) return null;
            float total = candidates.Sum(c => c.Weight);
            float roll = Luck.Raw01() * total;
            foreach (var c in candidates)
            {
                roll -= c.Weight;
                if (roll < 0f) return c;
            }
            return candidates[candidates.Count - 1];
        }

        private static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Luck.RawRange(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}

using System;
using System.Collections.Generic;

namespace TreasureRun.Pois
{
    /// <summary>The three Wandering Merchants (docs/poi-plan.md §5).</summary>
    public enum MerchantKind
    {
        /// <summary>Any treasure (no ascensions); leaves once every offer is bought.</summary>
        Normal,
        /// <summary>Legendary treasures only; leaves once every offer is bought.</summary>
        Legendary,
        /// <summary>Ascensions only; leaves after the first purchase.</summary>
        Ascension,
    }

    /// <summary>
    /// Prices and rules of the Wandering Merchant (pure, offline-tested). Rarities are the Treasures.Rarity order as ints
    /// (0 Common … 5 Mythical).
    ///
    /// Price = markup of the kind × fair value, fair value = rarity factor × <see cref="BaseGold"/>(wave), × 1.2 for an
    /// ascension. The fair value is fitted to the balance framework (<c>Balance.FairPrice</c>): the price whose payment costs as
    /// many points as a treasure of the rarity brings (its budget) at that wave; checked by
    /// <c>PoiTests.MerchantPricesFollowTheFairValue</c> (within 10% for every rarity and wave).
    /// </summary>
    public static class MerchantLogic
    {
        public const int Offers = 3;
        /// <summary>One random offer is this much cheaper.</summary>
        public const double Discount = 0.4;
        /// <summary>Selling pays this share of the merchant's own (undiscounted) price for the treasure.</summary>
        public const double SellShare = 0.5;
        /// <summary>Gold of an ascension relative to a treasure of its rarity (its budget is 25% higher; gold is convex in points).</summary>
        public const double AscensionFactor = 1.2;

        /// <summary>Fair price of each rarity relative to a Common (fitted: 1 / 1.46 / 1.90 / 2.31 / 2.70 / 3.75).</summary>
        private static readonly double[] RarityFactors = { 1.0, 1.46, 1.9, 2.3, 2.7, 3.75 };

        /// <summary>Ascensions per rarity in the catalog (C/U/R/E/L), for the balance spec of the Ascension Merchant.</summary>
        public static readonly int[] AscensionsPerRarity = { 1, 4, 6, 8, 4 };

        public static double RarityFactor(int rarity) => RarityFactors[Math.Max(0, Math.Min(RarityFactors.Length - 1, rarity))];

        /// <summary>The share of the fair value a merchant asks (balanced so each merchant is worth about 15 points, the POI budget).</summary>
        public static double Markup(MerchantKind kind)
        {
            switch (kind)
            {
                case MerchantKind.Legendary: return 0.92;
                case MerchantKind.Ascension: return 0.65;
                default: return 0.75;
            }
        }

        public static int MinWave(MerchantKind kind) => kind == MerchantKind.Normal ? 2 : 6;

        /// <summary>
        /// Fair price of a Common treasure at a wave (fitted to the framework: 194 at wave 1, 339 at 10, 644 at 20, 1571 at 30,
        /// 3513 at 40, 4747 at 45).
        /// </summary>
        public static double BaseGold(int wave)
        {
            double w = Math.Max(1, wave);
            return 160 + 22.5 * w - 1.3 * w * w + 0.071 * w * w * w;
        }

        public static double FairValue(int rarity, int wave, bool ascension) =>
            RarityFactor(rarity) * BaseGold(wave) * (ascension ? AscensionFactor : 1);

        public static int Price(MerchantKind kind, int rarity, int wave, bool ascension, bool discounted) =>
            Round5(Markup(kind) * FairValue(rarity, wave, ascension) * (discounted ? 1 - Discount : 1));

        /// <summary>Half of what this merchant would ask for the treasure (undiscounted).</summary>
        public static int SellPrice(MerchantKind kind, int rarity, int wave, bool ascension) =>
            Round5(SellShare * Price(kind, rarity, wave, ascension, false));

        public static int Round5(double gold) => Math.Max(5, (int)Math.Round(gold / 5.0) * 5);

        /// <summary>Does the merchant pack up after a purchase? <paramref name="openAfter"/>: offers still for sale after it.</summary>
        public static bool LeavesAfterPurchase(MerchantKind kind, int openAfter) => kind == MerchantKind.Ascension || openAfter <= 0;

        /// <summary>The rule shown in the shop.</summary>
        public static string Rule(MerchantKind kind, int open)
        {
            if (kind == MerchantKind.Ascension)
                return "Only one ascension can be bought here: the merchant packs up right after your purchase, and selling ends with it.";
            if (open <= 1)
                return "This is the last offer: buying it closes the shop for good, and you can no longer sell here.";
            return $"The merchant stays until all {open} offers are bought. The last purchase closes the shop, and selling ends with it.";
        }
    }

    /// <summary>
    /// The merchant's section: the wagon parks on a levelled 2 x 2 patch beside the path. Its opening always faces south-west
    /// on screen, which is world +z (the camera looks along world (+x, -z)), whatever the tile's rotation; the patch is
    /// chosen so the opening faces a path segment where the exits allow it:
    ///  - opening towards local -x / +x: right / left of the trunk (entry → centre), rows 1-2;
    ///  - towards local +z: below the left or right exit route (rows 1-2), else beside the trunk;
    ///  - towards local -z: above the left or right exit route (rows 4-5), else beside the trunk.
    /// The cells of the 2 x 2 patch on the opening's side hold the awning, crates and sign; the others hold the wagon.
    /// </summary>
    public sealed class MerchantStyle : SectionStyle
    {
        /// <summary>The opening's direction in the tile's local cells (dx, dz) for a tile turned by <paramref name="rotation"/> degrees.</summary>
        public static (int dx, int dz) OpeningFor(int rotation)
        {
            // Unity turns local (x, z) by r around y to (x cos r + z sin r, -x sin r + z cos r); find the local axis that lands on world +z.
            switch ((((int)Math.Round(rotation / 90.0)) % 4 + 4) % 4)
            {
                case 1: return (-1, 0);
                case 2: return (0, -1);
                case 3: return (1, 0);
                default: return (0, 1);
            }
        }

        /// <summary>The lower-left cell of the 2 x 2 patch for an opening direction and the section's exits.</summary>
        public static (int x, int z) Corner((int dx, int dz) opening, SectionExits exits)
        {
            bool right = (exits & SectionExits.Right) != 0, left = (exits & SectionExits.Left) != 0;
            if (opening.dx < 0) return (4, 1);
            if (opening.dx > 0) return (1, 1);
            int x = right || !left ? 4 : 1;
            if (opening.dz > 0) return (x, 1);
            return right || left ? (x, 4) : (4, 1);
        }

        /// <summary>The four cells of the patch.</summary>
        public static IEnumerable<(int x, int z)> Footprint((int x, int z) corner)
        {
            for (int x = corner.x; x <= corner.x + 1; x++)
                for (int z = corner.z; z <= corner.z + 1; z++) yield return (x, z);
        }

        /// <summary>The patch's lower-left cell in a generated layout (from its reserved cells).</summary>
        public static (int x, int z) CornerOf(SectionLayout l)
        {
            for (int x = 0; x < SectionLayout.Size; x++)
                for (int z = 0; z < SectionLayout.Size; z++)
                    if (l.Cells[x, z] == SectionCell.Reserved) return (x, z);
            return (4, 1);
        }

        public override void Reserve(SectionLayout layout)
        {
            foreach (var (x, z) in Footprint(Corner(OpeningFor(Rotation), layout.Exits)))
            {
                layout.Cells[x, z] = SectionCell.Reserved;
                layout.Height[x, z] = 1; // level ground under the wagon
            }
        }
    }
}

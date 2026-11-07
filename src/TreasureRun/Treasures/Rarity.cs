using UnityEngine;

namespace TreasureRun.Treasures
{
    public enum Rarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary,
        /// <summary>Never rolled naturally (weight 0); only via rarity upgrades or debug config.</summary>
        Mythical,
    }

    public static class RarityInfo
    {
        public const int Count = 6;

        /// <summary>Text/accent color, dark enough to read on the cream card background.</summary>
        public static Color TextColor(Rarity r)
        {
            switch (r)
            {
                case Rarity.Common: return new Color(0.40f, 0.40f, 0.40f);
                case Rarity.Uncommon: return new Color(0.13f, 0.55f, 0.18f);
                case Rarity.Rare: return new Color(0.12f, 0.38f, 0.85f);
                case Rarity.Epic: return new Color(0.55f, 0.18f, 0.75f);
                case Rarity.Legendary: return new Color(0.90f, 0.48f, 0.02f);
                case Rarity.Mythical: return new Color(0.85f, 0.08f, 0.15f);
                default: return Color.black;
            }
        }

        /// <summary>Brighter color for icons and highlights.</summary>
        public static Color IconColor(Rarity r)
        {
            switch (r)
            {
                case Rarity.Common: return new Color(0.75f, 0.75f, 0.75f);
                case Rarity.Uncommon: return new Color(0.30f, 0.85f, 0.35f);
                case Rarity.Rare: return new Color(0.30f, 0.60f, 1.00f);
                case Rarity.Epic: return new Color(0.75f, 0.35f, 1.00f);
                case Rarity.Legendary: return new Color(1.00f, 0.65f, 0.10f);
                case Rarity.Mythical: return new Color(1.00f, 0.20f, 0.25f);
                default: return Color.white;
            }
        }

        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        /// <summary>
        /// Draft weight for a rarity at a given game level, interpolated from early (level 1) to late (level 45).
        /// Mythical uses the debug config value (default 0).
        /// </summary>
        public static float Weight(Rarity r, int level)
        {
            float t = Mathf.InverseLerp(1f, 45f, level);
            switch (r)
            {
                case Rarity.Common: return Mathf.Lerp(55f, 30f, t);
                case Rarity.Uncommon: return Mathf.Lerp(28f, 30f, t);
                case Rarity.Rare: return Mathf.Lerp(12f, 22f, t);
                case Rarity.Epic: return Mathf.Lerp(4f, 13f, t);
                case Rarity.Legendary: return Mathf.Lerp(1f, 5f, t);
                case Rarity.Mythical: return Mathf.Max(0f, Core.ModConfig.DebugMythicalWeight.Value);
                default: return 0f;
            }
        }
    }
}

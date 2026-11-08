using TreasureRun.Core;
using TreasureRun.Patches;
using UnityEngine;
using static TreasureRun.Treasures.Effects.Kit;

namespace TreasureRun.Treasures.Effects
{
    /// <summary>Luck, treasure-offer manipulation and Mythical treasures (M4).</summary>
    internal static class MetaTreasures
    {
        public const string PrismId = "prism_of_ascendance";

        public static void Register()
        {
            Add("lucky_coin", "Lucky Coin", Rarity.Common,
                "Treasure rarity is rolled one extra time, keeping the best result.",
                (run, restoring) => { if (!restoring) Luck.AddExtraRolls(LuckCategory.TreasureRarity, 1); },
                new[] { "luck", "treasures" });

            Add("four_leaf_clover", "Four-Leaf Clover", Rarity.Epic,
                "Every luck roll is made one extra time, keeping the best result: crits, stuns, treasure slots and rarities, chests and more.",
                (run, restoring) => { if (!restoring) Luck.AddExtraRolls("All", 1); },
                new[] { "luck" });

            Add(PrismId, "Prism of Ascendance", Rarity.Legendary,
                "In every treasure offer, one treasure is upgraded by one rarity. Legendaries become Mythical.",
                tags: new[] { "treasures" });

            // ----- Mythical (only via Prism of Ascendance or debug) -----
            Add("infinity_engine", "Infinity Engine", Rarity.Mythical,
                "All towers attack twice as fast and use half as much mana.", tags: new[] { "attack-speed", "mana" });

            Add("time_dilation", "Time Dilation", Rarity.Mythical,
                "All enemies move 30% slower.", tags: new[] { "slow" });
        }

        public static void Hook()
        {
            StatLayer.Register("infinity_engine", (t, b) =>
            {
                if (!Has("infinity_engine")) return;
                b.Multiply(TowerStat.AttackSpeed, 2f);
                b.Multiply(TowerStat.ManaCost, 0.5f);
            });

            CombatPatches.EnemySpeedMultiplier = () => Has("time_dilation") ? Mathf.Pow(0.7f, Count("time_dilation")) : 1f;
        }
    }

    /// <summary>Per-frame treasure effects (hosted on the plugin's persistent object).</summary>
    internal sealed class TreasureTicker : MonoBehaviour
    {
        private float timer;

        private void Update()
        {
            if (!TreasureRunMode.Active) return;
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = 0.1f;
            BuildTreasures.TickMoat();
        }
    }
}

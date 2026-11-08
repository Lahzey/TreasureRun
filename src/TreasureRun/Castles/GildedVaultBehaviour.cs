using System.Collections.Generic;
using TreasureRun.Cards;
using TreasureRun.Core;
using UnityEngine;

namespace TreasureRun.Castles
{
    /// <summary>
    /// Gilded Vault (bank castle): monsters drop no gold at all. Instead the vault pays out at the start of every
    /// wave: a flat income (<see cref="BaseIncome"/>) plus interest on the gold you saved. The payout is regular
    /// income (vanilla IncomeGenerator, GoldSource.Income), so income multipliers such as Midas Touch apply.
    /// Content that raises kill gold is banned; vault cards (CastleCards) raise the payout instead.
    /// </summary>
    internal sealed class GildedVaultBehaviour : CastleBehaviour
    {
        public const float BaseInterest = 0.04f;
        public const float InterestPerCard = 0.01f;
        public const int InterestCapPerLevel = 25;
        public const float CapBonusPerCard = 0.5f;
        public const float CharterBonusPerCard = 0.15f;

        private IncomeGenerator income;

        public override IEnumerable<string> BannedTags => new[] { ContentTags.KillGold };

        public override float GoldMultiplier(GoldSource source) => source == GoldSource.Kill ? 0f : 1f;

        /// <summary>Flat part of the payout for a level: 8 x level + level², raised by Royal Charter.</summary>
        public static int BaseIncome(int level) =>
            Mathf.RoundToInt((8 * level + level * level) * (1f + CharterBonusPerCard * CardCounters.Get(CastleCards.RoyalCharter)));

        public static float InterestRate => BaseInterest + InterestPerCard * CardCounters.Get(CastleCards.VaultInterest);

        public static int InterestCap(int level) =>
            Mathf.RoundToInt(InterestCapPerLevel * level * (1f + CapBonusPerCard * CardCounters.Get(CastleCards.DeeperVaults)));

        public static int Interest(int level, int gold) => Mathf.Clamp(Mathf.FloorToInt(gold * InterestRate), 0, InterestCap(level));

        public override void OnGameStart()
        {
            income = CastleObject.AddComponent<IncomeGenerator>();
            income.incomePerRound = 0;
            income.incomeTimesLevel = 0f;
        }

        public override void OnBeforeWaveStart(int nextLevel)
        {
            if (income != null) income.incomePerRound = BaseIncome(nextLevel) + Interest(nextLevel, GameHelpers.Gold);
        }

        public override string InfoText()
        {
            int next = GameHelpers.Level + 1;
            int interest = Interest(next, GameHelpers.Gold);
            return $"Next payout (level {next}): {BaseIncome(next) + interest}g\n" +
                   $"  income {BaseIncome(next)}g + interest {interest}g\n" +
                   $"Interest: {InterestRate:P0} of saved gold, max {InterestCap(next)}g\n" +
                   $"Paid out so far: {(income != null ? income.netGold : 0)}g";
        }
    }
}

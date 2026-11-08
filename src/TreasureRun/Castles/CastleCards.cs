using TreasureRun.Cards;

namespace TreasureRun.Castles
{
    /// <summary>
    /// Castle-exclusive level-up cards. They only appear with their castle, in normal mode and in Treasure Run.
    /// Cards stay tame; the wild upgrades are treasures (CastleTreasures).
    /// </summary>
    internal static class CastleCards
    {
        // Gilded Vault
        public const string VaultInterest = "vault_interest";
        public const string DeeperVaults = "deeper_vaults";
        public const string RoyalCharter = "royal_charter";

        // Siege Workshop
        public const string HeavierShot = "heavier_shot";
        public const string QuickReload = "quick_reload";
        public const string LongBarrel = "long_barrel";

        // Druid Grove
        public const string Heartwood = "heartwood";

        public static void RegisterAll()
        {
            CustomCards.CastleChain("druid_grove", Heartwood, "Heartwood", 3, "Golden Forest I",
                _ => $"Chopping a tree heals +{DruidGroveBehaviour.HealPerHeartwood} more health.", () => { });

            const string vault = "gilded_vault";
            CustomCards.CastleChain(vault, VaultInterest, "Vault Interest", 3, "Gold Rush!",
                _ => $"The vault pays +{GildedVaultBehaviour.InterestPerCard:P0} more interest on saved gold.", () => { });
            CustomCards.CastleChain(vault, DeeperVaults, "Deeper Vaults", 2, "Gold Rush!",
                _ => $"The vault's interest cap is raised by {GildedVaultBehaviour.CapBonusPerCard:P0} of its base.", () => { });
            CustomCards.CastleChain(vault, RoyalCharter, "Royal Charter", 3, "Gold Rush!",
                _ => $"The vault's flat income is {GildedVaultBehaviour.CharterBonusPerCard:P0} higher.", () => { });

            const string siege = "siege_workshop";
            CustomCards.CastleChain(siege, HeavierShot, "Heavier Shot", 3, "Tower - Cannon",
                _ => $"The castle cannon deals +{SiegeCastleTower.DamagePerHeavierShot} damage.", () => { });
            CustomCards.CastleChain(siege, QuickReload, "Quick Reload", 3, "Tower - Cannon",
                _ => $"The castle cannon fires {SiegeCastleTower.FireRatePerQuickReload:P0} faster.", () => { });
            CustomCards.CastleChain(siege, LongBarrel, "Long Barrel", 2, "Tower - Cannon",
                _ => $"The castle cannon gains +{SiegeCastleTower.RangePerLongBarrel} range.", () => { });
        }
    }
}

namespace TreasureRun.Castles
{
    /// <summary>Castle definitions (carousel order). Add new castles here.</summary>
    internal static class CastleCatalog
    {
        public static void RegisterAll()
        {
            CastleRegistry.Register(new CastleDef
            {
                Id = CastleRegistry.DefaultId,
                Name = "Rogue Tower",
                Description = "The tower you know. Nothing special about it, nothing missing either.",
            });

            CastleRegistry.Register(new CastleDef
            {
                Id = "castle_town",
                Name = "Castle Town",
                Model = "castles/castle_town",
                Description =
                    "A bustling town has grown around the keep.\n\n" +
                    $"• Every wave, earns {CastleTownBehaviour.IncomePerBuilding:0.#} × level gold for each building or tower " +
                    "directly next to the castle, like a house.\n" +
                    $"• Draws attention: {CastleTownBehaviour.SpawnMultiplier:0.#}× as many monsters per wave, " +
                    "but each drops a third less gold.",
                CreateBehaviour = () => new CastleTownBehaviour(),
            });

            CastleRegistry.Register(new CastleDef
            {
                Id = "arcane_spire",
                Name = "Arcane Spire",
                Model = "castles/arcane_spire",
                StartingHealth = 1,
                Parts =
                {
                    new CastlePart { Name = "crystal", Model = "castles/arcane_spire_crystal",
                                     VoxelPosition = new UnityEngine.Vector3(0f, ArcaneSpireFx.CrystalBaseVoxel, 0f), Animation = PartAnimation.Float },
                },
                Description =
                    "Mages who burn mana instead of stone.\n\n" +
                    $"• Monsters reaching the spire are <b>obliterated</b> for {ArcaneSpireBehaviour.ManaPerDamage} mana per damage and still drop gold.\n" +
                    $"• Starts with full mana, +{ArcaneSpireBehaviour.ManaPerPulse} mana every {ArcaneSpireBehaviour.PulseInterval:0.#} s in waves. " +
                    "Corner mana crystals and the Mana Siphon included.\n" +
                    "• Only <b>1 health</b>: once the mana runs dry, the next monster ends the run.",
                CreateBehaviour = () => new ArcaneSpireBehaviour(),
            });

            CastleRegistry.Register(new CastleDef
            {
                Id = "gilded_vault",
                Name = "Gilded Vault",
                Model = "castles/gilded_vault",
                Description =
                    "The richest bank in the realm, and it does not deal in loot.\n\n" +
                    "• Monsters drop <b>no gold</b>.\n" +
                    "• At the start of every wave the vault pays out 8 × level + level² gold, plus " +
                    $"{GildedVaultBehaviour.BaseInterest:P0} interest on your saved gold (max {GildedVaultBehaviour.InterestCapPerLevel} × level).\n" +
                    "• Cards that raise monster gold are replaced by vault cards.",
                CreateBehaviour = () => new GildedVaultBehaviour(),
            });

            CastleRegistry.Register(new CastleDef
            {
                Id = "siege_workshop",
                Name = "Siege Workshop",
                Model = "castles/siege_workshop",
                Parts =
                {
                    new CastlePart { Name = "cannon", Model = "castles/siege_cannon", MenuOnly = true, Scale = SiegeWorkshopBehaviour.CannonScale,
                                     VoxelPosition = new UnityEngine.Vector3(0f, SiegeWorkshopBehaviour.CannonVoxelHeight, 0f) },
                },
                Description =
                    "Engineers turned the keep into a war machine.\n\n" +
                    "• The castle has its own <b>cannon</b>: a tower that levels up, can be upgraded and fires from the roof.\n" +
                    $"• All that machinery is expensive: towers cost <b>{SiegeWorkshopBehaviour.TowerPriceMultiplier:0}×</b> as much.",
                CreateBehaviour = () => new SiegeWorkshopBehaviour(),
            });

            CastleRegistry.Register(new CastleDef
            {
                Id = "necropolis",
                Name = "Necropolis",
                Model = "castles/necropolis",
                StartingHealth = 6,
                Description =
                    "The dead do not rest here. They serve.\n\n" +
                    $"• Monsters slain near the castle <b>rise again</b> with {NecropolisBehaviour.RiseHealthFraction:P0} of their health, " +
                    "walk back up the path and explode on the first monster they touch, dealing their health as damage.\n" +
                    "• Starts with graves on the starting tile and the Haunted House unlocked.\n" +
                    "• Only 6 health, and houses pay nothing: the villagers shun necromancers.",
                CreateBehaviour = () => new NecropolisBehaviour(),
            });

            CastleRegistry.Register(new CastleDef
            {
                Id = "druid_grove",
                Name = "Druid Grove",
                Model = "castles/druid_grove",
                StartingHealth = 20,
                Description =
                    "An ancient grove, wrapped in roots and bark.\n\n" +
                    $"• Trees grow {DruidGroveBehaviour.TreeChanceMultiplier - 1f:P0} more often, some surround the grove from the start " +
                    "and new ones sprout after every wave.\n" +
                    $"• Chopping a tree heals {DruidGroveBehaviour.HealPerChop} health. <b>20 health</b>.\n" +
                    "• No houses, graves or iron veins ever appear.",
                CreateBehaviour = () => new DruidGroveBehaviour(),
            });

            CastleRegistry.Register(new CastleDef
            {
                Id = "colosseum",
                Name = "Colosseum",
                Model = "castles/colosseum",
                Description =
                    "The crowd wants a show.\n\n" +
                    $"• Every {ColosseumBehaviour.Interval}th wave is a <b>champion wave</b>: instead of the regular monsters, " +
                    "a single giant champion enters, with the health of the whole wave and then some. It hits the castle hard.\n" +
                    "• Every champion drops a treasure chest.",
                CreateBehaviour = () => new ColosseumBehaviour(),
            });

            CastleRegistry.Register(new CastleDef
            {
                Id = "crows_nest",
                Name = "Crow's Nest",
                Model = "castles/crows_nest",
                Description =
                    "A towering lookout held up by iron and stubbornness.\n\n" +
                    $"• You can build one tower <b>on top</b> of the castle, with <b>+{CrowsNestBehaviour.PlatformHeight} high ground</b>.\n" +
                    "• Every free spot of the starting tile is filled with iron veins: at first, the top is the only place to build.",
                CreateBehaviour = () => new CrowsNestBehaviour(),
            });

            CastleRegistry.Register(new CastleDef
            {
                Id = "glass_fortress",
                Name = "Glass Fortress",
                Model = "castles/glass_fortress",
                ModelAlpha = 0.72f,
                HealthText = "none",
                Description =
                    "Beautiful, flawless and terribly fragile.\n\n" +
                    "• <b>No health</b>: the first monster that reaches the fortress ends the run. Nothing can change that.\n" +
                    "• Cards and treasures that add or restore health never appear.\n" +
                    "• Upside: cards and treasures that cost max health are <b>free</b>.",
                CreateBehaviour = () => new GlassFortressBehaviour(),
            });
        }
    }
}

using TreasureRun.Castles;
using static TreasureRun.Treasures.Effects.Kit;

namespace TreasureRun.Castles
{
    /// <summary>Ids of castle-exclusive treasures (registered in Treasures.Effects.CastleTreasures).</summary>
    internal static class CastleTreasures
    {
        // Siege Workshop
        public const string WallCannons = "wall_cannons";
        public const string Grapeshot = "grapeshot";
        public const string Catapult = "catapult";
        public const string IncendiaryShells = "incendiary_shells";
        public const string SiegeEngineer = "siege_engineer";
        public const string GrandBombard = "grand_bombard";
        public const string TrebuchetBattery = "trebuchet_battery";

        // Druid Grove
        public const string SaplingSeeds = "sapling_seeds";
        public const string Ironbark = "ironbark";
        public const string GoldenSap = "golden_sap";
    }
}

namespace TreasureRun.Treasures.Effects
{
    /// <summary>
    /// Treasures that only appear with a specific castle (TreasureDef.Castle). Effects live in the castle's code
    /// (e.g. SiegeCannon), which checks ownership.
    /// </summary>
    internal static class CastleTreasures
    {
        private const string Siege = "siege_workshop";

        public static void Register()
        {
            AddFor(Siege, Castles.CastleTreasures.WallCannons, "Wall Cannons", Rarity.Uncommon,
                $"Small cannons on the four corner towers take turns firing at monsters in range, " +
                $"dealing {SiegeCastleTower.WallCannonDamage:P0} of the castle cannon's damage.");
            AddFor(Siege, Castles.CastleTreasures.Grapeshot, "Grapeshot", Rarity.Rare,
                $"Every castle cannon shot also fires {SiegeCastleTower.GrapeshotPellets} pellets at nearby monsters " +
                $"for {SiegeCastleTower.GrapeshotDamage:P0} damage each.");
            AddFor(Siege, Castles.CastleTreasures.Catapult, "Catapult", Rarity.Rare,
                $"A catapult on the castle hurls a boulder every {SiegeCastleTower.CatapultInterval:0} seconds at the monster " +
                $"farthest from the castle (range {SiegeCastleTower.CatapultRange:0}), dealing {SiegeCastleTower.CatapultDamage:0}x " +
                "the cannon's damage in a large area.");
            AddFor(Siege, Castles.CastleTreasures.IncendiaryShells, "Incendiary Shells", Rarity.Epic,
                "Castle cannon shots set monsters on fire, applying burn equal to their damage.");
            AddFor(Siege, Castles.CastleTreasures.SiegeEngineer, "Siege Engineer", Rarity.Epic,
                "The castle cannon deals +1 damage for every tower you own.");
            AddFor(Siege, Castles.CastleTreasures.GrandBombard, "Grand Bombard", Rarity.Legendary,
                "The castle cannon deals triple damage, but fires 30% slower.");
            AddFor(Siege, Castles.CastleTreasures.TrebuchetBattery, "Trebuchet Battery", Rarity.Legendary,
                "The catapult throws three boulders at once, each at a different monster.",
                _ => Has(Castles.CastleTreasures.Catapult));

            RegisterGrove();
        }

        public static void Hook() { }

        private const string Grove = "druid_grove";

        private static void RegisterGrove()
        {
            AddFor(Grove, Castles.CastleTreasures.SaplingSeeds, "Sapling Seeds", Rarity.Uncommon,
                $"{DruidGroveBehaviour.SaplingSeedsTrees} more trees sprout after every wave.");
            AddFor(Grove, Castles.CastleTreasures.Ironbark, "Ironbark", Rarity.Rare,
                $"Chopping a tree also raises the grove's maximum health by 1 (up to +{DruidGroveBehaviour.IronbarkCap}).");
            AddFor(Grove, Castles.CastleTreasures.GoldenSap, "Golden Sap", Rarity.Epic,
                $"Chopping a tree gives double gold and heals {DruidGroveBehaviour.GoldenSapHeal} more health.");
        }

        private static void AddFor(string castle, string id, string name, Rarity rarity, string description,
                                   System.Func<Core.RunState, bool> available = null)
        {
            Add(id, name, rarity, description, tags: new[] { "castle", castle }, available: available);
            TreasureRegistry.Get(id).Castle = castle;
        }
    }
}

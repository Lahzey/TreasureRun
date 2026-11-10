using TreasureRun.Core;
using TreasureRun.Treasures;
using UnityEngine;

namespace TreasureRun.Ascensions
{
    /// <summary>
    /// Development-only Ballista ascension ("Proving Ground") that exercises the framework: model swap on the turret,
    /// popup title, build button label, card presentation. Never offered (IsAvailable = false); grant it with the dev
    /// console (<c>treasure test_ascension</c>, <c>offer test_ascension ...</c>). Registered only with Debug.DebugTools.
    /// </summary>
    internal sealed class TestAscension : AscendedTower
    {
        public const string Id = "test_ascension";

        public static void Register()
        {
            if (ModConfig.DebugTools == null || !ModConfig.DebugTools.Value) return;
            var def = new AscensionDef
            {
                Id = Id, Name = "Proving Ground", Rarity = Rarity.Rare, Kind = AscensionKind.Upgrade,
                Target = AscensionTarget.Ballista, Component = typeof(TestAscension), CardModel = "castles/siege_cannon",
                Description = "Development test ascension: Ballistas wear the Siege Workshop cannon and gain +5 base damage.",
            };
            AscensionRegistry.Register(def);
            def.Treasure.IsAvailable = _ => false;
            StatLayer.Register("TestAscension", (tower, stats) =>
            {
                if (tower.GetComponent<TestAscension>() != null) stats.Flat(TowerStat.Damage, 5f);
            });
        }

        protected override void OnAscend()
        {
            HideVanillaModel();
            Transform mount = Tower != null && Tower.turret != null ? Tower.turret.transform : transform;
            ShowModel("castles/siege_cannon", mount, new Vector3(0f, 0f, 0f), Quaternion.Euler(0f, -90f, 0f), 0.6f);
        }
    }
}

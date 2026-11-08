using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TreasureRun.Cards;
using TreasureRun.Core;
using UnityEngine;

namespace TreasureRun.Castles
{
    /// <summary>
    /// Druid Grove: 20 health. Trees grow 50% more often on new tiles, a few grow around the grove at the start and
    /// new ones sprout on free cells after every wave. Chopping a tree heals the castle. Houses, graves and iron veins
    /// never appear (content that needs them is banned).
    /// </summary>
    [HarmonyPatch]
    internal sealed class DruidGroveBehaviour : CastleBehaviour
    {
        public const float TreeChanceMultiplier = 1.5f;
        public const int StartingTrees = 6;
        public const int BaseHealPerChop = 1;
        // Castle cards and treasures
        public const int HealPerHeartwood = 1;
        public const int SaplingSeedsTrees = 2;
        public const int IronbarkCap = 10;
        public const int GoldenSapHeal = 2;

        private int treesGrown;
        private int ironbarkGained;

        public static int HealPerChop =>
            BaseHealPerChop + HealPerHeartwood * Cards.CardCounters.Get(CastleCards.Heartwood)
            + (Treasures.TreasureInventory.Owns(CastleTreasures.GoldenSap) ? GoldenSapHeal : 0);

        public static int RegrowthPerWave(int level) =>
            1 + level / 15 + (Treasures.TreasureInventory.Owns(CastleTreasures.SaplingSeeds) ? SaplingSeedsTrees : 0);

        public override float GoldMultiplier(GoldSource source) =>
            source == GoldSource.Tree && Treasures.TreasureInventory.Owns(CastleTreasures.GoldenSap) ? 2f : 1f;

        public override IEnumerable<string> BannedTags => new[] { ContentTags.NeedsOre, ContentTags.NeedsGraves };

        public override DevelopmentFate StartingDevelopment(GameObject development) =>
            development.name.StartsWith("StartingHouse") ? DevelopmentFate.Remove : DevelopmentFate.Vanilla;

        public override float DecorationChance(SpawnableObject decoration, float chance)
        {
            if (decoration is House || decoration is Grave || decoration is IronVein) return 0f;
            if (decoration is TreeSpawn) return Mathf.Min(1f, chance * TreeChanceMultiplier);
            return chance;
        }

        public override void OnGameStart() => CastleObject.AddComponent<DelayedAction>().Run(1, () => Plant(MapDecorations.FreeStartTileCells(), StartingTrees));

        public override void OnWaveEnded(int level) => Plant(MapDecorations.FreeCellsOnMap(), RegrowthPerWave(level));

        private void Plant(List<Vector3> cells, int count)
        {
            MapDecorations.Shuffle(cells);
            foreach (Vector3 cell in cells.Take(count))
                if (MapDecorations.Spawn<TreeSpawn>(cell) != null) treesGrown++;
        }

        public override string InfoText() =>
            $"Trees on the map: {Object.FindObjectsOfType<TreeSpawn>().Length}\n" +
            $"Regrowth: {RegrowthPerWave(GameHelpers.Level)} tree(s) after each wave ({treesGrown} grown so far)\n" +
            $"Chopping a tree heals {HealPerChop} health.";

        [HarmonyPostfix, HarmonyPatch(typeof(TreeUI), nameof(TreeUI.Chop))]
        private static void ChopPostfix()
        {
            if (!(CastleRuntime.Behaviour is DruidGroveBehaviour grove)) return;
            if (Treasures.TreasureInventory.Owns(CastleTreasures.Ironbark) && grove.ironbarkGained < IronbarkCap)
            {
                grove.ironbarkGained++;
                GameManager.instance.IncreaseTowerHealth(1);
            }
            int heal = HealPerChop;
            GameManager.instance.RepairTower(heal * 10);
            GameHelpers.FloatingText(GameHelpers.CastlePosition + Vector3.up * 2.5f, $"+{heal} health", "Green");
        }
    }

    /// <summary>Runs an action after N frames (lets vanilla Start/Destroy calls settle first).</summary>
    internal sealed class DelayedAction : MonoBehaviour
    {
        private int frames;
        private System.Action action;

        public void Run(int afterFrames, System.Action a)
        {
            frames = afterFrames;
            action = a;
        }

        private void Update()
        {
            if (frames-- > 0) return;
            try { action?.Invoke(); }
            catch (System.Exception e) { Plugin.Log.LogError($"Delayed castle action failed: {e}"); }
            Destroy(this);
        }
    }
}

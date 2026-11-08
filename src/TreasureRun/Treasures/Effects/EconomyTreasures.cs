using TreasureRun.Core;
using UnityEngine;
using static TreasureRun.Treasures.Effects.Kit;

namespace TreasureRun.Treasures.Effects
{
    /// <summary>Gold, chests and income treasures (M4).</summary>
    internal static class EconomyTreasures
    {
        private const string LoanLeftKey = "loan_shark.left";

        public static void Register()
        {
            Add("tithe_box", "Tithe Box", Rarity.Common,
                "At the start of each wave, gain 5 gold for every tower you own.", tags: new[] { "economy", "many-towers" });

            Add("scavenger", "Scavenger", Rarity.Common,
                "Enemies killed by a critical hit drop +3 gold.", tags: new[] { "economy", "crit" });

            Add("woodcutters_luck", "Woodcutter's Luck", Rarity.Common,
                "Chopping a tree has a 15% chance to drop a treasure chest.", tags: new[] { "economy", "chests" });

            Add("trophy_hunter", "Trophy Hunter", Rarity.Common,
                "Mini-bosses always drop a treasure chest.", tags: new[] { "chests" });

            Add("diverse_portfolio", "Diverse Portfolio", Rarity.Uncommon,
                "Enemies drop +1 extra gold for every different tower type that damaged them.", tags: new[] { "economy", "variety" });

            Add("loan_shark", "Loan Shark", Rarity.Uncommon,
                "Gain 1000 gold now. Repay 150 gold at the start of each of the next 10 waves (you can go into debt).",
                (run, restoring) =>
                {
                    if (restoring) return;
                    run.SetCounter(LoanLeftKey, run.GetCounter(LoanLeftKey) + 10);
                    Gold(1000, CastleTop, "loan");
                },
                new[] { "economy", "risk" });

            Add("alchemists_furnace", "Alchemist's Furnace", Rarity.Rare,
                "Burning enemies drop +4 gold on death. Poisoned enemies restore 2 mana on death.", tags: new[] { "economy", "burn", "poison" });

            Add("golden_goblin", "Golden Goblin", Rarity.Rare,
                "Each wave has a 35% chance to contain a golden goblin: a fast enemy that drops a treasure chest when killed.",
                tags: new[] { "chests" });

            Add("jackpot", "Jackpot", Rarity.Rare,
                "Every 40th enemy killed drops 15 × level gold.", tags: new[] { "economy" });
        }

        public static void Hook()
        {
            GameEvents.Resubscribe += () =>
            {
                GameEvents.WaveStarted += level =>
                {
                    if (Has("tithe_box")) Gold(5 * GameEvents.TowerCount * Count("tithe_box"), CastleTop, "tithe");

                    RunState run = TreasureRunMode.Run;
                    int left = run?.GetCounter(LoanLeftKey) ?? 0;
                    if (left > 0)
                    {
                        run.SetCounter(LoanLeftKey, left - 1);
                        ResourceManager.instance.Spend(150);
                        GameHelpers.FloatingText(CastleTop + Vector3.up, "-150g loan", "Red");
                    }

                    goblinPending = Has("golden_goblin") && Luck.Check(LuckCategory.GoldenGoblin, 0.35f);
                    goblinCountdown = Random.Range(3, 12);
                };
            };

            Combat.EnemyKilled += k =>
            {
                if (!TreasureRunMode.Active) return;
                Enemy e = k.Enemy;

                if (k.WasCrit && Has("scavenger") && e.dropsGold) Gold(3 * Count("scavenger"), k.Position);

                if (Has("diverse_portfolio") && e.dropsGold)
                {
                    int types = e.typesOfTowers.Count - (e.typesOfTowers.Contains(TowerType.DOT) ? 1 : 0);
                    if (types > 0) ResourceManager.instance.AddMoney(types * Count("diverse_portfolio"));
                }

                if (Has("alchemists_furnace"))
                {
                    if (e.burning) Gold(4, k.Position);
                    if (e.poisoned) ResourceManager.instance.AddMana(2);
                }

                if (Has("jackpot") && Combat.KillsThisRun % 40 == 0) Gold(15 * Level, k.Position, "JACKPOT");
            };

            Combat.EnemySpawned += e =>
            {
                if (!TreasureRunMode.Active) return;

                if (Has("trophy_hunter") && Combat.IsMiniBoss(e)) e.deathObjectSpawn = SpawnManager.instance.treasureChest;

                if (goblinPending && !Combat.IsBoss(e) && !Combat.IsMiniBoss(e) && --goblinCountdown <= 0)
                {
                    goblinPending = false;
                    MakeGoblin(e);
                }
            };
        }

        private static bool goblinPending;
        private static int goblinCountdown;

        private static void MakeGoblin(Enemy e)
        {
            e.deathObjectSpawn = SpawnManager.instance.treasureChest;
            e.baseSpeed *= 1.6f;
            e.pathfinder.speed *= 1.6f;
            e.transform.localScale *= 1.2f;
            if (e.renderer != null) e.renderer.material.color = new Color(1f, 0.82f, 0.2f);
            e.SetName("Golden Goblin");
            GameHelpers.FloatingText(e.transform.position + Vector3.up, "GOLDEN GOBLIN!", "Yellow", 1.5f, 1.5f);
        }

        /// <summary>Woodcutter's Luck: called from the TreeUI.Chop postfix.</summary>
        internal static void OnTreeChopped(TreeUI ui)
        {
            if (!Has("woodcutters_luck") || ui.treasureObject == null) return;
            if (Luck.Check(LuckCategory.TreeChest, 0.15f * Count("woodcutters_luck")))
                Object.Instantiate(ui.treasureObject, ui.transform.position, Quaternion.identity);
        }
    }
}

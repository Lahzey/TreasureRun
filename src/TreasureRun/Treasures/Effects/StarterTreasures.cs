using HarmonyLib;
using TreasureRun.Core;
using UnityEngine;

namespace TreasureRun.Treasures.Effects
{
    /// <summary>
    /// First batch of treasures (M2): simple, testable effects across all rarities.
    /// Convention: with restoring == true, only re-attach hooks; vanilla state (stats, gold, HP)
    /// will be restored by the save system.
    /// </summary>
    internal static class StarterTreasures
    {
        public static void Register()
        {
            // ---------- Common ----------
            Add("whetstone", "Whetstone", Rarity.Common,
                "All towers deal +1 damage.",
                (run, restoring) => { if (!restoring) TowerManager.instance.AddBonusBaseDamage(TowerType.Global, 1); },
                "power");

            Add("sturdy_walls", "Sturdy Walls", Rarity.Common,
                "+5 maximum castle health. Also heals 5.",
                (run, restoring) => { if (!restoring) GameManager.instance.IncreaseTowerHealth(5); },
                "defense");

            Add("piggy_bank", "Piggy Bank", Rarity.Common,
                "At the end of each wave, gain 5% of your unspent gold (max 10 × level).",
                (run, restoring) => GameEvents.WaveEnded += PiggyBankInterest,
                "economy");

            // ---------- Uncommon ----------
            Add("long_sight", "Long Sight", Rarity.Uncommon,
                "All towers gain +1 range.",
                (run, restoring) => { if (!restoring) TowerManager.instance.AddBonusRange(TowerType.Global, 1f); },
                "power");

            Add("bounty_board", "Bounty Board", Rarity.Uncommon,
                "Monsters drop +1 gold.",
                (run, restoring) => { if (!restoring) MonsterManager.instance.extraGoldDrop += 1; },
                "economy");

            Add("keen_eye", "Keen Eye", Rarity.Uncommon,
                "All towers gain +5% crit chance.",
                (run, restoring) => { if (!restoring) TowerManager.instance.AddCritChance(TowerType.Global, 0.05f); },
                "power", "crit");

            // ---------- Rare ----------
            Add("thunderclap", "Thunderclap", Rarity.Rare,
                "All towers gain +6% stun chance.",
                (run, restoring) => { if (!restoring) TowerManager.instance.AddStunChance(TowerType.Global, 0.06f); },
                "power", "stun");

            Add("treasure_hoard", "Treasure Hoard", Rarity.Rare,
                "Gain gold worth three tree chops (3 × level²).",
                (run, restoring) =>
                {
                    if (restoring) return;
                    int level = GameHelpers.Level;
                    int gold = 3 * level * level;
                    ResourceManager.instance.AddMoney(gold);
                    GameHelpers.FloatingText(GameHelpers.CastlePosition + Vector3.up * 2f, $"+{gold}g", "Yellow", 1.5f);
                },
                "economy");

            // ---------- Epic ----------
            Add("hivemind", "Hivemind", Rarity.Epic,
                "Every tower deals +1 damage for every 3 towers you own.",
                (run, restoring) => TowerManager.instance.UpdateAllTowers(), // bonus applied in TowerStatPatches
                "power", "many-towers");

            Add("quick_study", "Quick Study", Rarity.Epic,
                "Level-up card draws happen every 2 levels instead of every 3.",
                (run, restoring) =>
                {
                    SpawnManager sm = SpawnManager.instance;
                    sm.cardDrawFrequency = Mathf.Max(1, Mathf.Min(sm.cardDrawFrequency, 2));
                },
                "cards");

            // ---------- Legendary ----------
            Add("second_wind", "Second Wind", Rarity.Legendary,
                "Once per run, when your castle would be destroyed, it survives with 50% health instead.",
                (run, restoring) => { },
                "defense");

            // ---------- Mythical ----------
            Add("midas_touch", "Midas Touch", Rarity.Mythical,
                "Gold from monsters, income buildings and trees is doubled.",
                (run, restoring) => { },
                "economy");
        }

        private static void Add(string id, string name, Rarity rarity, string description,
                                System.Action<RunState, bool> onAcquire, params string[] tags)
        {
            TreasureRegistry.Register(new TreasureDef
            {
                Id = id,
                Name = name,
                Rarity = rarity,
                Description = description,
                Tags = tags,
                CreateEffect = () => new LambdaEffect(onAcquire),
            });
        }

        private static void PiggyBankInterest(int level)
        {
            int interest = Mathf.Min(GameHelpers.Gold * 5 / 100, 10 * level);
            if (interest <= 0) return;
            ResourceManager.instance.AddMoney(interest);
            GameHelpers.FloatingText(GameHelpers.CastlePosition + Vector3.up * 2f, $"+{interest}g interest", "Yellow");
        }
    }

    /// <summary>Patches backing treasures that need a hook into game logic.</summary>
    [HarmonyPatch]
    internal static class StarterTreasurePatches
    {
        // ----- Second Wind: survive one lethal hit -----
        [HarmonyPrefix, HarmonyPatch(typeof(GameManager), nameof(GameManager.TakeDamage))]
        private static bool TakeDamagePrefix(GameManager __instance, int damage)
        {
            if (!TreasureRunMode.Active || __instance.gameOver) return true;
            if (!TreasureInventory.Owns("second_wind")) return true;

            RunState run = TreasureRunMode.Run;
            if (run.GetCounter("second_wind.used") > 0) return true;
            if (__instance.health - 10 * damage > 0) return true;

            run.SetCounter("second_wind.used", 1);
            __instance.health = __instance.maxHealth / 2;
            __instance.healthLoss = __instance.health;
            __instance.SetHealthBar();
            GameHelpers.FloatingText(GameHelpers.CastlePosition + Vector3.up * 2f, "SECOND WIND!", "Green", 2f, 2f);
            Plugin.Log.LogInfo("Second Wind triggered.");
            return false;
        }

        // ----- Midas Touch: double "earned" gold (kills, income, trees; not refunds) -----
        internal static void Hook()
        {
            GoldEconomy.Multipliers.Add(src =>
            {
                int stacks = TreasureInventory.CountOf("midas_touch");
                return stacks > 0 && src != GoldSource.Other ? 1 << Mathf.Min(stacks, 4) : 1f;
            });
        }
    }
}

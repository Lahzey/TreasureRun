using System.Collections.Generic;
using HarmonyLib;
using TreasureRun.Core;
using UnityEngine;
using Ids = TreasureRun.Cards.CustomCardIds;

namespace TreasureRun.Cards
{
    /// <summary>Game hooks backing custom cards. Every patch is a no-op outside a Treasure Run.</summary>
    [HarmonyPatch]
    internal static class CardEffectPatches
    {
        // ----- Map spawns: Old Growth, Restless Dead, Forgotten Shrines, Rich Veins, Ley Lines -----
        [HarmonyPrefix, HarmonyPatch(typeof(SpawnableObject), nameof(SpawnableObject.Start))]
        private static void SpawnableStartPrefix(SpawnableObject __instance)
        {
            if (!TreasureRunMode.Active) return;
            float mult = CustomCards.SpawnMultiplier(__instance);
            if (mult != 1f) __instance.spawnChance = Mathf.Min(1f, __instance.spawnChance * mult);
        }

        // ----- Bounty Hunters (boss / mini-boss flags come from CombatPatches) -----
        internal static void Initialize()
        {
            Combat.EnemyKilled += k =>
            {
                if (!TreasureRunMode.Active || !(k.IsBoss || k.IsMiniBoss)) return;
                int n = CardCounters.Get(Ids.BountyHunters);
                if (n <= 0) return;
                int gold = 10 * GameHelpers.Level * n;
                ResourceManager.instance.AddMoney(gold);
                GameHelpers.FloatingText(k.Position + Vector3.up, $"+{gold}g bounty", "Yellow", 1.5f);
            };
        }

        // ----- Lumber Mill -----
        [HarmonyPostfix, HarmonyPatch(typeof(TreeUI), nameof(TreeUI.Chop))]
        private static void ChopPostfix()
        {
            if (!TreasureRunMode.Active || CardCounters.Get(Ids.LumberMill) <= 0) return;
            int level = GameHelpers.Level;
            ResourceManager.instance.AddMoney(level * level / 2);
        }

        // ----- War Bonds -----
        [HarmonyPostfix, HarmonyPatch(typeof(GameManager), nameof(GameManager.TakeDamage))]
        private static void TakeDamagePostfix(GameManager __instance, int damage)
        {
            if (!TreasureRunMode.Active || damage <= 0 || __instance.gameOver) return;
            int n = CardCounters.Get(Ids.WarBonds);
            if (n <= 0) return;
            int gold = 3 * GameHelpers.Level * damage * n;
            ResourceManager.instance.AddMoney(gold);
            GameHelpers.FloatingText(GameHelpers.CastlePosition + Vector3.up * 2f, $"+{gold}g war bonds", "Yellow");
        }

        // ----- Master Mason: buying tower levels costs 20% less -----
        internal const float MasonDiscount = 0.8f;

        internal static int LevelPrice(Tower t, float xp)
        {
            int full = (10 * t.level - (int)xp) * t.upgradeCostMultiplier;
            return CardCounters.Get(Ids.MasterMason) > 0 ? Mathf.CeilToInt(full * MasonDiscount) : full;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Tower), nameof(Tower.BuyHealthLevel))]
        private static bool BuyHealthPrefix(Tower __instance) => !MasonBuy(__instance, 0);

        [HarmonyPrefix, HarmonyPatch(typeof(Tower), nameof(Tower.BuyArmorLevel))]
        private static bool BuyArmorPrefix(Tower __instance) => !MasonBuy(__instance, 1);

        [HarmonyPrefix, HarmonyPatch(typeof(Tower), nameof(Tower.BuyShieldLevel))]
        private static bool BuyShieldPrefix(Tower __instance) => !MasonBuy(__instance, 2);

        /// <summary>Discounted re-implementation of Tower.BuyXxxLevel. Returns true if it handled the call.</summary>
        private static bool MasonBuy(Tower t, int kind)
        {
            if (!TreasureRunMode.Active || CardCounters.Get(Ids.MasterMason) <= 0) return false;

            float xp = kind == 0 ? t.healthXP : kind == 1 ? t.armorXP : t.shieldXP;
            int full = (10 * t.level - (int)xp) * t.upgradeCostMultiplier;
            int price = Mathf.CeilToInt(full * MasonDiscount);
            if (!ResourceManager.instance.CheckMoney(price)) return true;

            ResourceManager.instance.Spend(price);
            DamageTracker.instance.AddCost(t.towerType, price);
            xp -= 10 * t.level - full / t.upgradeCostMultiplier;
            switch (kind)
            {
                case 0: t.healthXP = xp; t.healthDamageUpgrade++; break;
                case 1: t.armorXP = xp; t.armorDamageUpgrade++; break;
                default: t.shieldXP = xp; t.shieldDamageUpgrade++; break;
            }
            t.level++;
            t.damageUpgrade++;
            t.SetStats();
            t.LevelUpText();
            return true;
        }

        // ----- Tower info panel: effective fire rate and discounted level prices -----
        [HarmonyPostfix, HarmonyPatch(typeof(TowerUI), nameof(TowerUI.UpdateText))]
        private static void TowerUIUpdateTextPostfix(TowerUI __instance)
        {
            if (!TreasureRunMode.Active) return;
            Tower t = __instance.myTower;
            if (t == null) return;

            if (t.rps > 0f) __instance.rpmText.text = "Fire Rate: " + Mathf.RoundToInt(60f / t.rps) + " RPM";

            if (CardCounters.Get(Ids.MasterMason) > 0)
            {
                __instance.healthXPText.text = LevelPrice(t, t.healthXP).ToString();
                __instance.armorXPText.text = LevelPrice(t, t.armorXP).ToString();
                __instance.shieldXPText.text = LevelPrice(t, t.shieldXP).ToString();
            }
        }
    }
}

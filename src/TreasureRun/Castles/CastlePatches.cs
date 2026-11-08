using System.Collections.Generic;
using HarmonyLib;
using TreasureRun.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TreasureRun.Castles
{
    /// <summary>Game hooks for the active castle. Active in every game mode.</summary>
    [HarmonyPatch]
    internal static class CastlePatches
    {
        internal static void Initialize()
        {
            GoldEconomy.Multipliers.Add(src => CastleRuntime.Behaviour?.GoldMultiplier(src) ?? 1f);

            // Main menu background shows the selected castle.
            SceneManager.sceneLoaded += (scene, mode) =>
            {
                if (scene.name == "MainMenu") CastleVisuals.Apply(CastleRuntime.Selected);
            };
        }

        private static CastleBehaviour B => CastleRuntime.Behaviour;

        private static void Safe(string what, System.Action action)
        {
            try { action(); }
            catch (System.Exception e) { Plugin.Log.LogError($"Castle '{CastleRuntime.Active?.Id}' {what} failed: {e}"); }
        }

        // ------------------------------------------------------------------ game start

        [HarmonyPostfix, HarmonyPatch(typeof(GameManager), nameof(GameManager.Start))]
        private static void GameManagerStartPostfix(GameManager __instance)
        {
            CastleDef def = CastleRuntime.Active;
            if (def == null) return;

            if (def.StartingHealth.HasValue)
            {
                __instance.maxHealth = Mathf.Max(1, def.StartingHealth.Value) * 10; // internal units are x10
                __instance.health = __instance.maxHealth;
                __instance.healthLoss = __instance.maxHealth;
                __instance.SetHealthBar();
            }

            CastleVisuals.Apply(def);
            GameObject castle = CastleVisuals.FindCastle();
            CastleInfo.Attach(castle);

            CastleBehaviour b = B;
            if (b == null) return;
            b.CastleObject = castle;
            if (b.TowerCostMultiplier != 1f) Safe("tower prices", () => ApplyTowerCostMultiplier(b.TowerCostMultiplier));
            Safe("OnGameStart", b.OnGameStart);
        }

        /// <summary>Scales every tower flyweight's prices (build buttons, increments and refunds follow).</summary>
        private static void ApplyTowerCostMultiplier(float m)
        {
            TowerManager tm = TowerManager.instance;
            var flyweights = new List<TowerFlyweight>
            {
                tm.crossbow, tm.morter, tm.teslaCoil, tm.flameThrower, tm.poisonSprayer, tm.frostKeep, tm.radar, tm.obelisk,
                tm.particleCannon, tm.shredder, tm.encampment, tm.lookout, tm.vampireLair, tm.cannon, tm.monument,
            };
            foreach (TowerFlyweight f in flyweights)
            {
                if (f == null) continue;
                f.basePrice = Mathf.RoundToInt(f.basePrice * m);
                f.priceIncrease = Mathf.RoundToInt(f.priceIncrease * m);
                f.currentPrice = f.basePrice + f.towers.Count * f.priceIncrease;
                f.sellPrice = f.basePrice + (f.towers.Count - 1) * f.priceIncrease;
                if (f.myBuildButton != null) f.myBuildButton.UpdatePriceText();
            }
        }

        // ------------------------------------------------------------------ waves and enemies

        [HarmonyPrefix, HarmonyPatch(typeof(SpawnManager), nameof(SpawnManager.StartNextWave))]
        private static void StartNextWavePrefix(SpawnManager __instance)
        {
            if (B != null) Safe("OnBeforeWaveStart", () => B.OnBeforeWaveStart(__instance.level + 1));
        }

        [HarmonyPostfix, HarmonyPatch(typeof(SpawnManager), nameof(SpawnManager.StartNextWave))]
        private static void StartNextWavePostfix(SpawnManager __instance)
        {
            if (B != null) Safe("OnWaveStarted", () => B.OnWaveStarted(__instance.level));
        }

        [HarmonyPostfix, HarmonyPatch(typeof(SpawnManager), nameof(SpawnManager.EndWave))]
        private static void EndWavePostfix(SpawnManager __instance)
        {
            if (B != null) Safe("OnWaveEnded", () => B.OnWaveEnded(__instance.level));
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Enemy), nameof(Enemy.AtEnd))]
        private static bool EnemyAtEndPrefix(Enemy __instance)
        {
            if (B == null || __instance.dead) return true;
            bool handled = false;
            Safe("OnEnemyReachedCastle", () => handled = B.OnEnemyReachedCastle(__instance));
            return !handled;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Enemy), nameof(Enemy.Die))]
        private static void EnemyDiePrefix(Enemy __instance, out bool __state) => __state = __instance.dead;

        [HarmonyPostfix, HarmonyPatch(typeof(Enemy), nameof(Enemy.Die))]
        private static void EnemyDiePostfix(Enemy __instance, bool __state)
        {
            if (B == null || __state || __instance == null) return; // already dead before: vanilla returned early
            Safe("OnEnemyKilled", () => B.OnEnemyKilled(__instance));
        }

        /// <summary>SpawnManager.Spawn(int num) starts the spawn coroutine for a wave's enemy budget.</summary>
        [HarmonyPrefix, HarmonyPatch(typeof(SpawnManager), nameof(SpawnManager.Spawn))]
        private static void SpawnPrefix(ref int num)
        {
            float m = B?.SpawnCountMultiplier ?? 1f;
            if (m != 1f) num = Mathf.Max(0, Mathf.RoundToInt(num * m));
        }

        // ------------------------------------------------------------------ health

        [HarmonyPrefix, HarmonyPatch(typeof(GameManager), nameof(GameManager.TakeDamage)), HarmonyPriority(Priority.First)]
        private static bool TakeDamagePrefix(GameManager __instance, ref int damage)
        {
            if (UI.DevConsole.GodMode) return false;
            if (B != null && B.IgnoresHealth && damage > 0) damage = __instance.health / 10 + 1; // any leak is lethal
            return true;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(GameManager), nameof(GameManager.IncreaseTowerHealth))]
        private static bool IncreaseTowerHealthPrefix() => B == null || !B.IgnoresHealth;

        [HarmonyPrefix, HarmonyPatch(typeof(GameManager), nameof(GameManager.RepairTower))]
        private static bool RepairTowerPrefix() => B == null || !B.IgnoresHealth;

        // ------------------------------------------------------------------ map

        /// <summary>Starting developments (houses, corner mana crystals) of the start tile.</summary>
        [HarmonyPrefix, HarmonyPatch(typeof(DevelopmentManager), nameof(DevelopmentManager.Start))]
        private static void DevelopmentManagerStartPrefix(DevelopmentManager __instance)
        {
            CastleBehaviour b = B;
            if (b == null || __instance.startingDevelopments == null) return;

            var remaining = new List<GameObject>();
            foreach (GameObject dev in __instance.startingDevelopments)
            {
                if (dev == null) continue;
                DevelopmentFate fate = DevelopmentFate.Vanilla;
                Safe("StartingDevelopment", () => fate = b.StartingDevelopment(dev));
                switch (fate)
                {
                    case DevelopmentFate.Keep:
                        // Kept for good: make sure its own spawn roll (SpawnableObject) also passes.
                        foreach (SpawnableObject so in dev.GetComponentsInChildren<SpawnableObject>(true))
                        {
                            so.spawnChance = 2f;
                            so.spawnLevels = new Vector2(-1f, 99999f);
                        }
                        break;
                    case DevelopmentFate.Remove:
                        Object.Destroy(dev);
                        break;
                    default:
                        remaining.Add(dev);
                        break;
                }
            }
            __instance.startingDevelopments = remaining.ToArray();
        }

        /// <summary>Decoration spawn chance on new tiles (after the Treasure Run card multipliers).</summary>
        [HarmonyPrefix, HarmonyPatch(typeof(SpawnableObject), nameof(SpawnableObject.Start)), HarmonyPriority(Priority.Low)]
        private static void SpawnableStartPrefix(SpawnableObject __instance)
        {
            CastleBehaviour b = B;
            if (b == null || MapDecorations.IsForced(__instance)) return;
            float chance = __instance.spawnChance;
            Safe("DecorationChance", () => chance = b.DecorationChance(__instance, chance));
            __instance.spawnChance = chance;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(House), nameof(House.CheckTowers))]
        private static void HouseCheckTowersPostfix(House __instance)
        {
            if (B != null && !B.HousesGiveIncome && __instance.myIncomeGenerator != null)
                __instance.myIncomeGenerator.incomeTimesLevel = 0f;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(House), nameof(House.SpawnUI))]
        private static bool HouseSpawnUIPrefix(House __instance)
        {
            if (B == null || B.HousesGiveIncome || __instance.UIObject == null) return true;
            SimpleUI ui = Object.Instantiate(__instance.UIObject, __instance.transform.position, Quaternion.identity).GetComponent<SimpleUI>();
            ui?.SetDiscriptionText("The villagers want nothing to do with a necromancer. This house pays no gold.");
            return false;
        }
    }
}

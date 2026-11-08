using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TreasureRun.Core;
using UnityEngine;

namespace TreasureRun.Patches
{
    /// <summary>
    /// Wraps Enemy.TakeDamage (the single damage entry point of the game):
    ///  - prefix: runs <see cref="Combat.HitModifiers"/> on the call's parameters,
    ///  - transpiler: routes the stun and crit rolls through <see cref="Luck"/> and records crits,
    ///  - postfix: raises <see cref="Combat.EnemyDamaged"/>; Enemy.Die raises <see cref="Combat.EnemyKilled"/>.
    /// Also: enemy spawn (incl. boss / mini-boss flags), freeze and enemy speed hooks.
    /// </summary>
    [HarmonyPatch]
    internal static class CombatPatches
    {
        private sealed class Frame
        {
            public HitResult Result;
            public int HpBefore;
            public int ChainDepth;
        }

        private static readonly Stack<Frame> frames = new Stack<Frame>();
        private static readonly HitContext ctx = new HitContext();

        internal static void Initialize()
        {
            TreasureRunMode.RunStarted += _ => { frames.Clear(); Combat.ResetRun(); };
            TreasureRunMode.RunEnded += _ => { frames.Clear(); Combat.ResetRun(); };
        }

        // ---------------- TakeDamage ----------------

        [HarmonyPrefix, HarmonyPatch(typeof(Enemy), nameof(Enemy.TakeDamage))]
        private static void TakeDamagePrefix(Enemy __instance, TowerType whoHitMe, ref int _baseDmg,
            ref int healthDmg, ref int armorDmg, ref int shieldDmg, ref float slowPercentage, ref float bleedPercentage,
            ref float burnPercentage, ref float poisonPercentage, ref float critChance, ref float stunChance)
        {
            if (!TreasureRunMode.Active || __instance.dead) { frames.Push(null); return; }

            bool nested = false;
            foreach (Frame f in frames)
                if (f != null && f.Result.Enemy == __instance) { nested = true; break; }

            ctx.Enemy = __instance;
            ctx.Source = whoHitMe;
            ctx.BaseDamage = _baseDmg;
            ctx.HealthMult = healthDmg; ctx.ArmorMult = armorDmg; ctx.ShieldMult = shieldDmg;
            ctx.Slow = slowPercentage; ctx.Bleed = bleedPercentage; ctx.Burn = burnPercentage; ctx.Poison = poisonPercentage;
            ctx.Crit = critChance; ctx.Stun = stunChance;
            ctx.IsDirect = _baseDmg > -1;
            ctx.IsSecondary = Combat.SecondaryDepth > 0;
            ctx.IsNested = nested;

            foreach (Action<HitContext> mod in Combat.HitModifiers)
            {
                try { mod(ctx); }
                catch (Exception e) { Plugin.Log.LogError($"Hit modifier failed: {e}"); }
            }

            _baseDmg = ctx.IsDirect ? Mathf.Max(0, ctx.BaseDamage) : ctx.BaseDamage;
            healthDmg = ctx.HealthMult; armorDmg = ctx.ArmorMult; shieldDmg = ctx.ShieldMult;
            slowPercentage = ctx.Slow; bleedPercentage = ctx.Bleed; burnPercentage = ctx.Burn; poisonPercentage = ctx.Poison;
            critChance = ctx.Crit; stunChance = ctx.Stun;

            frames.Push(new Frame
            {
                HpBefore = Combat.CurrentHp(__instance),
                ChainDepth = Combat.SecondaryDepth,
                Result = new HitResult
                {
                    Enemy = __instance, Source = whoHitMe,
                    BaseDamage = _baseDmg, HealthMult = healthDmg, ArmorMult = armorDmg, ShieldMult = shieldDmg,
                    IsDirect = ctx.IsDirect, IsSecondary = ctx.IsSecondary, IsNested = nested,
                },
            });
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Enemy), nameof(Enemy.TakeDamage))]
        private static void TakeDamagePostfix(Enemy __instance)
        {
            if (frames.Count == 0) return;
            Frame f = frames.Peek();
            if (f == null) return;
            f.Result.DamageDealt = Mathf.Max(0, f.HpBefore - Combat.CurrentHp(__instance));
            f.Result.Killed = __instance.dead;
            Combat.RaiseDamaged(f.Result);
        }

        [HarmonyFinalizer, HarmonyPatch(typeof(Enemy), nameof(Enemy.TakeDamage))]
        private static void TakeDamageFinalizer()
        {
            if (frames.Count > 0) frames.Pop();
        }

        private static readonly MethodInfo RandomRange = AccessTools.Method(typeof(UnityEngine.Random), nameof(UnityEngine.Random.Range), new[] { typeof(float), typeof(float) });
        private static readonly MethodInfo StunRollMethod = AccessTools.Method(typeof(CombatPatches), nameof(StunRoll));
        private static readonly MethodInfo CritRollMethod = AccessTools.Method(typeof(CombatPatches), nameof(CritRoll));
        private static readonly MethodInfo NoteCritMethod = AccessTools.Method(typeof(CombatPatches), nameof(NoteCrit));

        [HarmonyTranspiler, HarmonyPatch(typeof(Enemy), nameof(Enemy.TakeDamage))]
        private static IEnumerable<CodeInstruction> TakeDamageTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var list = new List<CodeInstruction>(instructions);
            int randomCalls = 0, critMarks = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Calls(RandomRange))
                {
                    // Vanilla order: 1st roll = stun, next three = crit tiers.
                    list[i].operand = randomCalls == 0 ? StunRollMethod : CritRollMethod;
                    randomCalls++;
                    continue;
                }
                // Crit tiers store the number-popup scale 1.5 / 2 / 2.5 into a local: record the crit right after.
                if (list[i].opcode == OpCodes.Ldc_R4 && i + 1 < list.Count && list[i + 1].IsStloc())
                {
                    float v = (float)list[i].operand;
                    if (v == 1.5f || v == 2f || v == 2.5f)
                    {
                        list.Insert(i + 2, new CodeInstruction(OpCodes.Ldc_R4, v));
                        list.Insert(i + 3, new CodeInstruction(OpCodes.Call, NoteCritMethod));
                        critMarks++;
                        i += 3;
                    }
                }
            }
            if (randomCalls != 4 || critMarks != 3)
                Plugin.Log.LogWarning($"CombatPatches transpiler: expected 4 rolls / 3 crit marks, found {randomCalls} / {critMarks}.");
            return list;
        }

        private static float StunRoll(float min, float max) =>
            TreasureRunMode.Active ? min + (max - min) * Luck.Roll01(LuckCategory.Stun, lowIsGood: true) : UnityEngine.Random.Range(min, max);

        private static float CritRoll(float min, float max) =>
            TreasureRunMode.Active ? min + (max - min) * Luck.Roll01(LuckCategory.Crit, lowIsGood: true) : UnityEngine.Random.Range(min, max);

        private static void NoteCrit(float popupScale)
        {
            if (frames.Count == 0) return;
            Frame f = frames.Peek();
            if (f == null) return;
            f.Result.WasCrit = true;
            f.Result.CritMultiplier = popupScale >= 2.5f ? 4f : popupScale >= 2f ? 3f : 2f;
        }

        // ---------------- Death ----------------

        [HarmonyPrefix, HarmonyPatch(typeof(Enemy), nameof(Enemy.Die)), HarmonyPriority(Priority.High)]
        private static void DiePrefix(Enemy __instance)
        {
            if (!TreasureRunMode.Active || __instance.dead) return;

            Combat.KillsThisWave++;
            Combat.KillsThisRun++;

            Frame killer = null;
            foreach (Frame f in frames)
                if (f != null && f.Result.Enemy == __instance) { killer = f; break; }

            var info = new KillInfo
            {
                Enemy = __instance,
                Killer = killer?.Result.Source ?? TowerType.DOT,
                WasCrit = killer?.Result.WasCrit ?? false,
                KilledBySecondary = killer?.Result.IsSecondary ?? false,
                ChainDepth = killer?.ChainDepth ?? 0,
                Overkill = Mathf.Max(0, -__instance.health),
                MaxHp = Combat.MaxHp(__instance),
                Position = __instance.transform.position,
                IsBoss = Combat.IsBoss(__instance),
                IsMiniBoss = Combat.IsMiniBoss(__instance),
            };
            Combat.RaiseKilled(info);
        }

        // ---------------- Spawning ----------------

        private static int bossDepth, miniBossDepth;

        [HarmonyPrefix, HarmonyPatch(typeof(SpawnManager), nameof(SpawnManager.SpawnBoss))]
        private static void SpawnBossPrefix() => bossDepth++;

        [HarmonyFinalizer, HarmonyPatch(typeof(SpawnManager), nameof(SpawnManager.SpawnBoss))]
        private static void SpawnBossFinalizer() => bossDepth--;

        [HarmonyPrefix, HarmonyPatch(typeof(SpawnManager), nameof(SpawnManager.SpawnMiniBoss))]
        private static void SpawnMiniBossPrefix() => miniBossDepth++;

        [HarmonyFinalizer, HarmonyPatch(typeof(SpawnManager), nameof(SpawnManager.SpawnMiniBoss))]
        private static void SpawnMiniBossFinalizer() => miniBossDepth--;

        [HarmonyPostfix, HarmonyPatch(typeof(SpawnManager), nameof(SpawnManager.SpawnEnemy))]
        private static void SpawnEnemyPostfix(Enemy __result)
        {
            if (!TreasureRunMode.Active || __result == null) return;
            if (bossDepth > 0) Combat.MarkBoss(__result, mini: false);
            else if (miniBossDepth > 0) Combat.MarkBoss(__result, mini: true);
            Combat.RaiseSpawned(__result);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(SpawnManager), nameof(SpawnManager.StartNextWave))]
        private static void StartNextWavePostfix()
        {
            if (TreasureRunMode.Active) Combat.KillsThisWave = 0;
        }

        // ---------------- Freeze & speed ----------------

        [HarmonyPostfix, HarmonyPatch(typeof(Enemy), nameof(Enemy.Freeze))]
        private static void FreezePostfix(Enemy __instance)
        {
            if (TreasureRunMode.Active) Combat.RaiseFrozen(__instance);
        }

        /// <summary>Global enemy speed factor (e.g. Time Dilation). Applied whenever vanilla sets the path speed.</summary>
        public static Func<float> EnemySpeedMultiplier = () => 1f;

        [HarmonyPostfix, HarmonyPatch(typeof(Enemy), nameof(Enemy.SetStats), new[] { typeof(int) })]
        private static void EnemySetStatsPostfix(Enemy __instance) => ApplySpeed(__instance);

        [HarmonyPostfix, HarmonyPatch(typeof(Enemy), nameof(Enemy.SpeedUpdate))]
        private static void SpeedUpdatePostfix(Enemy __instance) => ApplySpeed(__instance);

        private static void ApplySpeed(Enemy e)
        {
            if (!TreasureRunMode.Active) return;
            float m = EnemySpeedMultiplier();
            if (m != 1f) e.pathfinder.speed *= m;
        }
    }
}

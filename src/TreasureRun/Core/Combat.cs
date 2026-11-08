using System;
using System.Collections.Generic;
using UnityEngine;

namespace TreasureRun.Core
{
    /// <summary>
    /// Mutable view of one Enemy.TakeDamage call, passed to hit modifiers before vanilla runs.
    /// Damage model (vanilla): dealt = max(BaseDamage - fortify + mark + freeze, 1) * (LayerMultiplier + status bonus),
    /// against the current layer (shield, then armor, then health). DoT ticks and true damage use BaseDamage = -999
    /// and carry their amount in the multipliers.
    /// </summary>
    public sealed class HitContext
    {
        public Enemy Enemy;
        public TowerType Source;
        public int BaseDamage;
        public int HealthMult, ArmorMult, ShieldMult;
        public float Slow, Bleed, Burn, Poison, Crit, Stun;

        /// <summary>A normal attack (BaseDamage &gt; -1), not a DoT tick or true damage.</summary>
        public bool IsDirect;
        /// <summary>Damage caused by a mod effect (chain lightning, explosions, ...). On-hit procs should not trigger.</summary>
        public bool IsSecondary;
        /// <summary>Re-entrant call on the same enemy (vanilla overflow damage or crit DoT pops).</summary>
        public bool IsNested;

        public bool CanProc => IsDirect && !IsSecondary && !IsNested;

        /// <summary>Multiply the damage of this hit: base damage for direct hits, multipliers for DoT/true damage.</summary>
        public void MultiplyDamage(float factor)
        {
            if (IsDirect) BaseDamage = Mathf.Max(0, Mathf.RoundToInt(BaseDamage * factor));
            else
            {
                HealthMult = Mathf.RoundToInt(HealthMult * factor);
                ArmorMult = Mathf.RoundToInt(ArmorMult * factor);
                ShieldMult = Mathf.RoundToInt(ShieldMult * factor);
            }
        }
    }

    /// <summary>Result of one Enemy.TakeDamage call.</summary>
    public sealed class HitResult
    {
        public Enemy Enemy;
        public TowerType Source;
        public int BaseDamage, HealthMult, ArmorMult, ShieldMult;
        public int DamageDealt;
        public bool WasCrit;
        public float CritMultiplier = 1f;
        public bool IsDirect, IsSecondary, IsNested;
        public bool Killed;
        public bool CanProc => IsDirect && !IsSecondary && !IsNested;
    }

    public sealed class KillInfo
    {
        public Enemy Enemy;
        public TowerType Killer;
        public bool WasCrit;
        public bool KilledBySecondary;
        public int Overkill;
        public int MaxHp;
        public Vector3 Position;
        public bool IsBoss, IsMiniBoss;
        /// <summary>Chain depth of mod effects that led to this kill (0 = killed by a normal attack).</summary>
        public int ChainDepth;
    }

    /// <summary>
    /// Combat events and helpers (raised by Patches/CombatPatches.cs). Handlers are permanent (registered once at
    /// plugin start) and must check <see cref="TreasureRunMode.Active"/> / treasure ownership themselves.
    /// </summary>
    public static class Combat
    {
        /// <summary>Modifiers run in order before every TakeDamage call.</summary>
        public static readonly List<Action<HitContext>> HitModifiers = new List<Action<HitContext>>();

        public static event Action<HitResult> EnemyDamaged;
        public static event Action<KillInfo> EnemyKilled;
        public static event Action<Enemy> EnemySpawned;
        public static event Action<Enemy> EnemyFrozen;

        /// <summary>Max chain depth for mod-caused damage (explosions triggering explosions...).</summary>
        public const int MaxChainDepth = 25;

        internal static int SecondaryDepth;
        public static int KillsThisWave { get; internal set; }
        public static int KillsThisRun { get; internal set; }

        private static readonly HashSet<Enemy> bosses = new HashSet<Enemy>();
        private static readonly HashSet<Enemy> miniBosses = new HashSet<Enemy>();

        public static bool IsBoss(Enemy e) => bosses.Contains(e);
        public static bool IsMiniBoss(Enemy e) => miniBosses.Contains(e);
        internal static void MarkBoss(Enemy e, bool mini) { if (mini) miniBosses.Add(e); else bosses.Add(e); }

        internal static void ResetRun()
        {
            SecondaryDepth = 0;
            KillsThisWave = 0;
            KillsThisRun = 0;
            bosses.Clear();
            miniBosses.Clear();
        }

        public static int MaxHp(Enemy e) => Mathf.Max(1, e.baseHealth + e.baseArmor + e.baseShield);
        public static int CurrentHp(Enemy e) => Mathf.Max(0, e.health) + e.armor + e.shield;

        public static bool IsAlive(Enemy e) => e != null && !e.dead && e.health > 0;

        /// <summary>
        /// Deals exact damage to the enemy's current layer, ignoring multipliers, fortify and crits.
        /// Counts as secondary damage (no on-hit procs) and is attributed to DoT (does not affect kill gold).
        /// </summary>
        public static void TrueDamage(Enemy e, int amount)
        {
            if (!IsAlive(e) || amount <= 0 || SecondaryDepth >= MaxChainDepth) return;
            SecondaryDepth++;
            try { e.TakeDamage(TowerType.DOT, -999, amount, amount, amount, 0f, 0f, 0f, 0f, 0f, 0f); }
            finally { SecondaryDepth--; }
        }

        /// <summary>Repeats a hit on another enemy as secondary damage (no crits, no stun, no new procs).</summary>
        public static void SecondaryHit(Enemy e, TowerType source, int baseDamage, int h, int a, int s,
                                        float slow = 0f, float bleed = 0f, float burn = 0f, float poison = 0f)
        {
            if (!IsAlive(e) || SecondaryDepth >= MaxChainDepth) return;
            SecondaryDepth++;
            try { e.TakeDamage(source, Mathf.Max(0, baseDamage), h, a, s, slow, bleed, burn, poison, 0f, 0f); }
            finally { SecondaryDepth--; }
        }

        public static List<Enemy> EnemiesInRadius(Vector3 center, float radius, Enemy exclude = null)
        {
            var result = new List<Enemy>();
            if (SpawnManager.instance == null) return result;
            float r2 = radius * radius;
            foreach (Enemy e in SpawnManager.instance.currentEnemies)
            {
                if (e == exclude || !IsAlive(e)) continue;
                Vector3 d = e.transform.position - center;
                d.y = 0f;
                if (d.sqrMagnitude <= r2) result.Add(e);
            }
            return result;
        }

        public static List<Enemy> NearestEnemies(Vector3 center, float radius, int count, Enemy exclude = null)
        {
            var list = EnemiesInRadius(center, radius, exclude);
            list.Sort((a, b) => (a.transform.position - center).sqrMagnitude.CompareTo((b.transform.position - center).sqrMagnitude));
            if (list.Count > count) list.RemoveRange(count, list.Count - count);
            return list;
        }

        internal static void RaiseDamaged(HitResult r) => Raise(EnemyDamaged, r);
        internal static void RaiseKilled(KillInfo k) => Raise(EnemyKilled, k);
        internal static void RaiseSpawned(Enemy e) => Raise(EnemySpawned, e);
        internal static void RaiseFrozen(Enemy e) => Raise(EnemyFrozen, e);

        private static void Raise<T>(Action<T> evt, T arg)
        {
            if (evt == null) return;
            foreach (Action<T> handler in evt.GetInvocationList())
            {
                try { handler(arg); }
                catch (Exception ex) { Plugin.Log.LogError($"Combat handler failed: {ex}"); }
            }
        }
    }
}

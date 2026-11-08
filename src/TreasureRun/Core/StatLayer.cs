using System;
using System.Collections.Generic;
using UnityEngine;

namespace TreasureRun.Core
{
    public enum TowerStat
    {
        Damage,
        HealthMult,
        ArmorMult,
        ShieldMult,
        Range,
        /// <summary>Only Percent/Multiply apply: shots per second are scaled by (1 + pct) * mult.</summary>
        AttackSpeed,
        CritChance,
        StunChance,
        Slow,
        Bleed,
        Burn,
        Poison,
        BlastRadius,
        /// <summary>Only Percent/Multiply apply: scales mana cost per shot.</summary>
        ManaCost,
    }

    /// <summary>Collects modifiers for one tower. Final = (vanilla + flat) * (1 + percent) * multiplier.</summary>
    public sealed class TowerStatBuilder
    {
        private const int N = 14;
        internal readonly float[] FlatValues = new float[N];
        internal readonly float[] PercentValues = new float[N];
        internal readonly float[] MultValues = new float[N];

        internal void Clear()
        {
            for (int i = 0; i < N; i++) { FlatValues[i] = 0f; PercentValues[i] = 0f; MultValues[i] = 1f; }
        }

        public void Flat(TowerStat stat, float value) => FlatValues[(int)stat] += value;
        /// <summary>Additive percent: 0.25 = +25%. All percent bonuses on a stat are summed.</summary>
        public void Percent(TowerStat stat, float value) => PercentValues[(int)stat] += value;
        public void Multiply(TowerStat stat, float value) => MultValues[(int)stat] *= value;

        internal float Apply(TowerStat stat, float vanilla)
        {
            int i = (int)stat;
            return (vanilla + FlatValues[i]) * Mathf.Max(0f, 1f + PercentValues[i]) * MultValues[i];
        }

        internal float Factor(TowerStat stat)
        {
            int i = (int)stat;
            return Mathf.Max(0.05f, (1f + PercentValues[i]) * MultValues[i]);
        }
    }

    /// <summary>
    /// Tower stat modifier layer, applied after vanilla Tower.SetStats (see Patches/TowerStatPatches.cs).
    /// Providers are permanent (registered at plugin start) and check run/treasure state themselves.
    /// Call <see cref="Refresh"/> when a provider's output changes.
    /// </summary>
    public static class StatLayer
    {
        private static readonly List<(string name, Action<Tower, TowerStatBuilder> apply)> providers =
            new List<(string, Action<Tower, TowerStatBuilder>)>();

        private static readonly TowerStatBuilder builder = new TowerStatBuilder();

        public static void Register(string name, Action<Tower, TowerStatBuilder> apply) => providers.Add((name, apply));

        public static void Refresh()
        {
            if (TreasureRunMode.Active && TowerManager.instance != null) TowerManager.instance.UpdateAllTowers();
        }

        private static TowerStatBuilder Collect(Tower t)
        {
            builder.Clear();
            foreach (var (name, apply) in providers)
            {
                try { apply(t, builder); }
                catch (Exception e) { Plugin.Log.LogError($"Stat provider '{name}' failed: {e}"); }
            }
            return builder;
        }

        /// <summary>Applies all modifiers to a tower whose stats vanilla just recomputed.</summary>
        internal static void ApplyAll(Tower t)
        {
            TowerStatBuilder b = Collect(t);
            t.damage = Mathf.Max(0, Mathf.RoundToInt(b.Apply(TowerStat.Damage, t.damage)));
            t.healthDamage = Mathf.Max(0, Mathf.RoundToInt(b.Apply(TowerStat.HealthMult, t.healthDamage)));
            t.armorDamage = Mathf.Max(0, Mathf.RoundToInt(b.Apply(TowerStat.ArmorMult, t.armorDamage)));
            t.shieldDamage = Mathf.Max(0, Mathf.RoundToInt(b.Apply(TowerStat.ShieldMult, t.shieldDamage)));
            t.range = Mathf.Max(0.5f, b.Apply(TowerStat.Range, t.range));
            t.critChance = Mathf.Max(0f, b.Apply(TowerStat.CritChance, t.critChance));
            t.stunChance = Mathf.Max(0f, b.Apply(TowerStat.StunChance, t.stunChance));
            t.slowPercent = Mathf.Max(0f, b.Apply(TowerStat.Slow, t.slowPercent));
            t.bleedPercent = Mathf.Max(0f, b.Apply(TowerStat.Bleed, t.bleedPercent));
            t.burnPercent = Mathf.Max(0f, b.Apply(TowerStat.Burn, t.burnPercent));
            t.poisonPercent = Mathf.Max(0f, b.Apply(TowerStat.Poison, t.poisonPercent));
            t.blastRadius = Mathf.Max(0f, b.Apply(TowerStat.BlastRadius, t.blastRadius));
            t.finalManaConsumption *= b.Factor(TowerStat.ManaCost);
            if (t.rps > 0f) t.rps /= b.Factor(TowerStat.AttackSpeed);
        }

        /// <summary>For towers that recompute their fire rate after the base SetStats (Dropper/Shredder).</summary>
        internal static void ApplyAttackSpeedOnly(Tower t)
        {
            TowerStatBuilder b = Collect(t);
            if (t.rps > 0f) t.rps /= b.Factor(TowerStat.AttackSpeed);
        }
    }
}

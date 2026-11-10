using UnityEngine;

namespace TreasureRun.Tests.Sim
{
    /// <summary>Global enemy modifiers (vanilla MonsterManager fields that the damage formula reads).</summary>
    public sealed class SimMonsterMods
    {
        public int FortificationBonus;
        public int BonusDamageOnBleed, BonusDamageOnBurn, BonusDamageOnPoison, BonusDamageOnStun;
        public float PoisonSlowPercent, BurnSpeedDamagePercentBonus, BleedingCritChance;
        public float BleedPop, BurnPop, PoisonPop;
        public float SlowCapModifier;
        /// <summary>Damage-over-time stacks used up per tick (vanilla GameManager.dotTick: bleed, burn, poison).</summary>
        public Vector3 DotTick = new Vector3(24f, 24f, 24f);
    }

    /// <summary>A Lookout mark: the Lookout's stats added to every hit on the marked enemy.</summary>
    public sealed class SimMark
    {
        public int Damage, HealthDamage, ArmorDamage, ShieldDamage;
        public float CritChance;
    }

    /// <summary>Enemy state for the simulation (mirrors the fields of vanilla Enemy/Pathfinder that matter for combat).</summary>
    public sealed class SimEnemy
    {
        private static int nextId;
        public readonly int Id = ++nextId;
        public string Name = "enemy";

        public int Health, Armor, Shield;
        public int BaseHealth, BaseArmor, BaseShield;
        public int HealthRegen, ArmorRegen, ShieldRegen;
        public bool BleedImmune, BurnImmune, PoisonImmune, SlowImmune;

        public int Bleed, Burn, Poison;
        public float Slow;
        public float FreezeTime;
        public float MaxFreeze = 1f;
        public float FortifiedTime;
        public SimMark Mark;
        public bool Dead;

        /// <summary>Walking speed (units per second) before slow.</summary>
        public float BaseSpeed = 1f;
        /// <summary>Distance walked along the path.</summary>
        public float Distance;
        public Vector3 Position;

        // DOT timers (vanilla: timeSinceBleed/Burn/Poison and the shared dotTick spacing).
        internal float TimeSinceBleed, TimeSinceBurn, TimeSincePoison, DotSpacing;
        internal float RegenTimer;

        public bool Bleeding => Bleed > 0;
        public bool Burning => Burn > 0;
        public bool Poisoned => Poison > 0;
        public bool Fortified => FortifiedTime > 0f;
        public int TotalHp => Mathf.Max(0, Health) + Armor + Shield;

        public float CurrentSpeed => FreezeTime > 0f ? 0f : BaseSpeed * (1f - Slow);

        public static SimEnemy Create(int health, int armor = 0, int shield = 0, float speed = 1f, string name = "enemy") =>
            new SimEnemy
            {
                Name = name,
                Health = health, Armor = armor, Shield = shield,
                BaseHealth = health, BaseArmor = armor, BaseShield = shield,
                BaseSpeed = speed,
            };

        /// <summary>A target that never dies (for DPS measurements).</summary>
        public static SimEnemy Dummy(int layerHp = 100_000_000, string layer = "health") =>
            layer == "shield" ? Create(layerHp, 0, layerHp, 0f, "dummy")
            : layer == "armor" ? Create(layerHp, layerHp, 0, 0f, "dummy")
            : Create(layerHp, 0, 0, 0f, "dummy");

        public override string ToString() => $"{Name}#{Id} hp {Health}/{Armor}/{Shield}";
    }
}

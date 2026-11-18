using System;
using System.Collections.Generic;

namespace TreasureRun.Balance
{
    [Flags]
    public enum Immunity { None = 0, Bleed = 1, Burn = 2, Poison = 4, Slow = 8 }

    public enum CryTrigger { Death, Spawn, NearEnd, ArmorBreak, ShieldBreak, Periodic }

    /// <summary>A battle cry of a monster prefab (vanilla BattleCry).</summary>
    public sealed class Cry
    {
        public readonly CryTrigger Trigger;
        public readonly float Radius, Haste, Fortify;
        public readonly int HealHealth, HealArmor, HealShield;
        public readonly string[] Spawns;

        public Cry(CryTrigger trigger, float radius, float haste, float fortify, int healHealth, int healArmor, int healShield, string[] spawns)
        {
            Trigger = trigger; Radius = radius; Haste = haste; Fortify = fortify;
            HealHealth = healHealth; HealArmor = healArmor; HealShield = healShield;
            Spawns = spawns ?? Array.Empty<string>();
        }
    }

    /// <summary>A monster prefab's stats (vanilla Enemy fields), see docs/reference/vanilla-monsters.md.</summary>
    public sealed class Monster
    {
        public readonly string Name;
        public readonly int Level, Health, Armor, Shield;
        public readonly float Speed;
        public readonly int HealthRegen, ArmorRegen, ShieldRegen;
        public readonly Immunity Immune;
        public readonly int CastleDamage, BonusGold;
        public readonly bool DropsGold;
        public readonly string DeathSpawn;
        public readonly Cry[] Cries;

        public Monster(string name, int level, int health, int armor, int shield, float speed, int healthRegen, int armorRegen,
                       int shieldRegen, Immunity immune, int castleDamage, int bonusGold, bool dropsGold, string deathSpawn, Cry[] cries)
        {
            Name = name; Level = level; Health = health; Armor = armor; Shield = shield; Speed = speed;
            HealthRegen = healthRegen; ArmorRegen = armorRegen; ShieldRegen = shieldRegen; Immune = immune;
            CastleDamage = castleDamage; BonusGold = bonusGold; DropsGold = dropsGold; DeathSpawn = deathSpawn; Cries = cries;
        }

        public int Total => Health + Armor + Shield;

        /// <summary>Monsters spawned when this one dies (death object and Death battle cries).</summary>
        public IEnumerable<string> Children()
        {
            if (DeathSpawn != null) yield return DeathSpawn;
            foreach (Cry c in Cries)
                if (c.Trigger == CryTrigger.Death || c.Trigger == CryTrigger.ArmorBreak || c.Trigger == CryTrigger.ShieldBreak)
                    foreach (string s in c.Spawns) yield return s;
        }
    }

    public static partial class MonsterRoster
    {
        private static readonly string[] NoSpawns = Array.Empty<string>();
        private static Dictionary<string, Monster> byName;

        public static Monster Get(string name)
        {
            if (byName == null)
            {
                var d = new Dictionary<string, Monster>();
                foreach (Monster m in All) d[m.Name] = m;
                byName = d;
            }
            return name != null && byName.TryGetValue(name, out Monster r) ? r : null;
        }
    }
}

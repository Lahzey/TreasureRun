using System;
using System.Collections.Generic;

namespace TreasureRun.Balance
{
    /// <summary>The buildable towers (names as in vanilla TowerType, plus the support buildings).</summary>
    public enum TowerKind
    {
        Crossbow, Morter, TeslaCoil, Frost, Obelisk, FlameThrower, PoisonSprayer, Radar, ParticleCannon,
        Shredder, Encampment, Lookout, VampireLair, Cannon, Monument,
    }

    /// <summary>How a tower's attack reaches monsters, for the area model (docs/balance-framework.md section 4.2).</summary>
    public enum AttackShape
    {
        /// <summary>One homing projectile or beam, one monster.</summary>
        Single,
        /// <summary>A blast of radius Area at the target (Mortar).</summary>
        Blast,
        /// <summary>Hits everything within its range around the tower (Tesla Coil, Vampire Lair).</summary>
        Pulse,
        /// <summary>A line of length Area x range in front of the tower (Flame Thrower, Poison Sprayer).</summary>
        Line,
        /// <summary>A projectile that pierces everything along Area x range (Cannon).</summary>
        Pierce,
        /// <summary>Drops on random path cells near the tower; no target needed (Frost Keep snowflakes).</summary>
        Dropper,
        /// <summary>Mines: dropped on path cells, blast of radius Area (Encampment).</summary>
        Mines,
        /// <summary>A saw that runs back up the path, hitting each monster once with falling damage (Shredder).</summary>
        Saw,
        /// <summary>A sword that stabs once a second with falling damage (Monument).</summary>
        Sword,
        /// <summary>Never attacks; marks a monster (Lookout).</summary>
        Mark,
        /// <summary>A plane that reaches the whole map (Radar).</summary>
        Plane,
    }

    /// <summary>Vanilla tower stats (docs/reference/vanilla-towers.md).</summary>
    public sealed class TowerInfo
    {
        public TowerKind Kind;
        public string Name;
        public int Price, PriceStep;
        public int Damage, HealthMult, ArmorMult, ShieldMult;
        /// <summary>Seconds between attacks.</summary>
        public double Interval;
        public double Range;
        /// <summary>Mana per shot as a multiple of the damage per hit (0 = no mana); Frost Keep pays per second instead.</summary>
        public double ManaRate, ManaPerSecond;
        public int LevelCostMult;
        public AttackShape Shape;
        /// <summary>Blast radius, or line length as a multiple of the range.</summary>
        public double Area;
        /// <summary>Damage-over-time it applies by itself (Flame Thrower 100% burn, ...).</summary>
        public double Bleed, Burn, Poison, Slow;
        /// <summary>Share of its shots that land (unguided projectiles miss some).</summary>
        public double Accuracy = 1;
        /// <summary>Unlock card title (null = available from the start).</summary>
        public string Unlock;
    }

    public static class TowerData
    {
        public static readonly TowerInfo[] All =
        {
            T(TowerKind.Crossbow, "Ballista", 10, 15, 10, 10, 5, 5, 3, 5, 0, 1, AttackShape.Single, 0, unlock: null),
            T(TowerKind.Morter, "Mortar", 200, 75, 20, 10, 15, 5, 6, 10, 0, 1, AttackShape.Blast, 1, accuracy: 0.8, unlock: "Tower - Mortar"),
            T(TowerKind.TeslaCoil, "Tesla Coil", 200, 75, 10, 6, 3, 10, 2, 2, 0.5, 2, AttackShape.Pulse, 0, unlock: "Tower - Tesla Coil"),
            T(TowerKind.Frost, "Frost Keep", 250, 100, 6, 10, 5, 5, 0.33, 2, 0, 2, AttackShape.Dropper, 0, slow: 0.5, unlock: "Tower - Frost Keep", manaPerSecond: 2),
            T(TowerKind.FlameThrower, "Flame Thrower", 300, 75, 6, 6, 9, 3, 1, 4, 0.25, 3, AttackShape.Line, 1.5, burn: 1, unlock: "Tower - Flame Thrower"),
            T(TowerKind.PoisonSprayer, "Poison Sprayer", 300, 75, 5, 6, 3, 9, 1, 4, 0.25, 3, AttackShape.Line, 1.5, poison: 1, unlock: "Tower - Poison Sprayer"),
            T(TowerKind.Shredder, "Shredder", 500, 100, 10, 20, 10, 10, 6, 5, 0, 3, AttackShape.Saw, 0, bleed: 1, unlock: "Tower - Shredder"),
            T(TowerKind.Encampment, "Encampment", 500, 100, 20, 10, 15, 5, 12, 2, 0, 1, AttackShape.Mines, 1, unlock: "Tower - Encampment"),
            T(TowerKind.Lookout, "Lookout", 500, 100, 1, 2, 1, 3, 0, 8, 0, 5, AttackShape.Mark, 0, unlock: "Tower - Lookout"),
            T(TowerKind.VampireLair, "Vampire Lair", 750, 150, 12, 10, 5, 5, 10, 9, 1, 3, AttackShape.Pulse, 0, unlock: "Tower - Vampire Lair"),
            T(TowerKind.Cannon, "Cannon", 750, 150, 20, 10, 20, 5, 3, 6, 0, 1, AttackShape.Pierce, 2, accuracy: 0.85, unlock: "Tower - Cannon"),
            T(TowerKind.Monument, "Monument", 750, 150, 15, 10, 5, 15, 5, 7, 0.25, 2, AttackShape.Sword, 0, unlock: "Tower - Monument"),
            T(TowerKind.Obelisk, "Obelisk", 1000, 250, 8, 5, 10, 2, 1 / 6.0, 5, 0.25, 3, AttackShape.Single, 0, unlock: "Tower - Obelisk"),
            // The plane fires 11.7 shots/s in passes of up to 20 inaccurate bullets, then turns, climbs and reloads: about a
            // quarter of the time firing, a third of the bullets hitting (estimated; it rated far above every other tower otherwise).
            T(TowerKind.Radar, "Radar", 1000, 250, 20, 20, 10, 10, 1 / 3.0, 30, 0, 4, AttackShape.Plane, 0, accuracy: 0.33, unlock: "Tower - Radar"),
            T(TowerKind.ParticleCannon, "Particle Cannon", 1000, 250, 40, 15, 10, 20, 5, 20, 0.25, 3, AttackShape.Single, 0, unlock: "Tower - Particle Cannon"),
        };

        private static Dictionary<TowerKind, TowerInfo> byKind;

        public static TowerInfo Get(TowerKind k)
        {
            if (byKind == null)
            {
                var d = new Dictionary<TowerKind, TowerInfo>();
                foreach (TowerInfo t in All) d[t.Kind] = t;
                byKind = d;
            }
            return byKind[k];
        }

        public static bool TryParse(string name, out TowerKind kind)
        {
            foreach (TowerInfo t in All)
                if (string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase) || string.Equals(t.Kind.ToString(), name, StringComparison.OrdinalIgnoreCase))
                { kind = t.Kind; return true; }
            kind = TowerKind.Crossbow;
            return false;
        }

        private static TowerInfo T(TowerKind k, string name, int price, int step, int dmg, int h, int a, int s, double interval, double range,
                                   double manaRate, int levelCost, AttackShape shape, double area, double accuracy = 1, double bleed = 0,
                                   double burn = 0, double poison = 0, double slow = 0, string unlock = null, double manaPerSecond = 0) =>
            new TowerInfo
            {
                Kind = k, Name = name, Price = price, PriceStep = step, Damage = dmg, HealthMult = h, ArmorMult = a, ShieldMult = s,
                Interval = interval, Range = range, ManaRate = manaRate, LevelCostMult = levelCost, Shape = shape, Area = area,
                Accuracy = accuracy, Bleed = bleed, Burn = burn, Poison = poison, Slow = slow, Unlock = unlock, ManaPerSecond = manaPerSecond,
            };
    }

    /// <summary>Support buildings that matter to the model.</summary>
    public static class Buildings
    {
        public const int ManaSiphonPrice = 100, ManaBankPrice = 500, MinePrice = 150, UniversityPrice = 500, HauntedHousePrice = 100;
        public const double ManaPerBuilding = 1, BankMaxMana = 20;
    }
}

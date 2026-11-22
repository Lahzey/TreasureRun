using System;
using System.Linq;
using UnityEngine;

namespace TreasureRun.Ascensions
{
    /// <summary>
    /// What an ascension evolves: a tower or a building. An own enum because vanilla buildings share
    /// <see cref="TowerType"/> values (all of them use the Siphon/Global flyweight type).
    /// </summary>
    public enum AscensionTarget
    {
        Ballista, Mortar, TeslaCoil, FrostKeep, FlameThrower, PoisonSprayer, Shredder, Encampment, Lookout,
        VampireLair, Cannon, Monument, Obelisk, Radar, ParticleCannon,
        ManaSiphon, ManaBank, Mine, University, HauntedHouse,
    }

    /// <summary>Static facts per target: display name, banner colour, vanilla type and how to find its objects.</summary>
    public static class AscensionTargets
    {
        public static readonly AscensionTarget[] All = (AscensionTarget[])Enum.GetValues(typeof(AscensionTarget));

        public static bool IsBuilding(AscensionTarget t) => t >= AscensionTarget.ManaSiphon;

        /// <summary>Vanilla tower type (null for buildings).</summary>
        public static TowerType? TowerTypeOf(AscensionTarget t)
        {
            switch (t)
            {
                case AscensionTarget.Ballista: return TowerType.Crossbow;
                case AscensionTarget.Mortar: return TowerType.Morter;
                case AscensionTarget.TeslaCoil: return TowerType.TeslaCoil;
                case AscensionTarget.FrostKeep: return TowerType.Frost;
                case AscensionTarget.FlameThrower: return TowerType.FlameThrower;
                case AscensionTarget.PoisonSprayer: return TowerType.PoisonSprayer;
                case AscensionTarget.Shredder: return TowerType.Shredder;
                case AscensionTarget.Encampment: return TowerType.Encampment;
                case AscensionTarget.Lookout: return TowerType.Lookout;
                case AscensionTarget.VampireLair: return TowerType.VampireLair;
                case AscensionTarget.Cannon: return TowerType.Cannon;
                case AscensionTarget.Monument: return TowerType.Monument;
                case AscensionTarget.Obelisk: return TowerType.Obelisk;
                case AscensionTarget.Radar: return TowerType.Radar;
                case AscensionTarget.ParticleCannon: return TowerType.ParticleCannon;
                default: return null;
            }
        }

        public static AscensionTarget? FromTowerType(TowerType type)
        {
            foreach (AscensionTarget t in All)
                if (TowerTypeOf(t) == type) return t;
            return null;
        }

        /// <summary>The vanilla component that identifies a building of this target (null for towers).</summary>
        public static Type BuildingComponent(AscensionTarget t)
        {
            switch (t)
            {
                case AscensionTarget.ManaSiphon: return typeof(ManaSiphon);
                case AscensionTarget.ManaBank: return typeof(ManaBank);
                case AscensionTarget.Mine: return typeof(Mine);
                case AscensionTarget.University: return typeof(University);
                case AscensionTarget.HauntedHouse: return typeof(HauntedHouse);
                default: return null;
            }
        }

        /// <summary>Target of a placed tower or building (null for other objects).</summary>
        public static AscensionTarget? Of(GameObject go)
        {
            if (go == null) return null;
            Tower tower = go.GetComponent<Tower>();
            if (tower != null) return FromTowerType(tower.towerType);
            foreach (AscensionTarget t in All)
            {
                Type c = BuildingComponent(t);
                if (c != null && go.GetComponent(c) != null) return t;
            }
            return null;
        }

        public static string DisplayName(AscensionTarget t)
        {
            switch (t)
            {
                case AscensionTarget.TeslaCoil: return "Tesla Coil";
                case AscensionTarget.FrostKeep: return "Frost Keep";
                case AscensionTarget.FlameThrower: return "Flame Thrower";
                case AscensionTarget.PoisonSprayer: return "Poison Sprayer";
                case AscensionTarget.VampireLair: return "Vampire Lair";
                case AscensionTarget.ParticleCannon: return "Particle Cannon";
                case AscensionTarget.ManaSiphon: return "Mana Siphon";
                case AscensionTarget.ManaBank: return "Mana Bank";
                case AscensionTarget.HauntedHouse: return "Haunted House";
                default: return t.ToString();
            }
        }

        /// <summary>Id fragment used in exclusion groups and tags ("ascension:tesla_coil").</summary>
        public static string Key(AscensionTarget t) =>
            string.Concat(t.ToString().Select((c, i) => i > 0 && char.IsUpper(c) ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));

        /// <summary>Fixed banner colour per target, so the type line is recognizable at a glance.</summary>
        public static Color BannerColor(AscensionTarget t)
        {
            switch (t)
            {
                case AscensionTarget.Ballista: return Rgb(139, 94, 52);
                case AscensionTarget.Mortar: return Rgb(96, 104, 112);
                case AscensionTarget.TeslaCoil: return Rgb(58, 110, 196);
                case AscensionTarget.FrostKeep: return Rgb(86, 170, 205);
                case AscensionTarget.FlameThrower: return Rgb(206, 92, 36);
                case AscensionTarget.PoisonSprayer: return Rgb(98, 160, 52);
                case AscensionTarget.Shredder: return Rgb(150, 40, 40);
                case AscensionTarget.Encampment: return Rgb(110, 116, 62);
                case AscensionTarget.Lookout: return Rgb(170, 140, 70);
                case AscensionTarget.VampireLair: return Rgb(110, 22, 48);
                case AscensionTarget.Cannon: return Rgb(60, 60, 68);
                case AscensionTarget.Monument: return Rgb(120, 96, 170);
                case AscensionTarget.Obelisk: return Rgb(190, 150, 40);
                case AscensionTarget.Radar: return Rgb(70, 120, 90);
                case AscensionTarget.ParticleCannon: return Rgb(120, 60, 200);
                case AscensionTarget.ManaSiphon: return Rgb(60, 140, 200);
                case AscensionTarget.ManaBank: return Rgb(40, 90, 170);
                case AscensionTarget.Mine: return Rgb(110, 90, 70);
                case AscensionTarget.University: return Rgb(150, 120, 60);
                case AscensionTarget.HauntedHouse: return Rgb(80, 70, 110);
                default: return Color.grey;
            }
        }

        private static Color Rgb(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f);

        /// <summary>The build button that places this target (null if not in the scene).</summary>
        public static BuildButtonUI BuildButton(AscensionTarget t)
        {
            foreach (BuildButtonUI b in Resources.FindObjectsOfTypeAll<BuildButtonUI>())
            {
                if (b == null || !b.gameObject.scene.IsValid() || b.tower == null) continue;
                if (Of(b.tower) == t) return b;
            }
            return null;
        }

        /// <summary>
        /// True if this target is relevant in the current run: towers once built (same rule as other tower treasures),
        /// buildings once unlocked (their build button is in TowerUnlockManager's unlocked list; an active button alone
        /// doesn't mean unlocked, e.g. the Mana Siphon's).
        /// </summary>
        public static bool InPlay(AscensionTarget t)
        {
            TowerType? type = TowerTypeOf(t);
            if (type.HasValue) return Treasures.Effects.Kit.TowerUsed(type.Value);
            BuildButtonUI button = BuildButton(t);
            return button != null && Unlocked(button.gameObject);
        }

        /// <summary>Is this build button unlocked in the current run?</summary>
        public static bool Unlocked(GameObject button)
        {
            TowerUnlockManager m = TowerUnlockManager.instance;
            if (m == null) return button.activeInHierarchy;
            foreach (GameObject u in m.unlockedBuildings.Concat(m.unlockedTowers))
                if (u != null && (u == button || button.transform.IsChildOf(u.transform))) return true;
            return false;
        }
    }
}

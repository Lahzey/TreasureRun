using System;
using System.Collections.Generic;

namespace TreasureRun.Balance
{
    /// <summary>Board variants a player steers into; situational items are judged on the one that suits them (section 1.1).</summary>
    public enum BoardVariant { Mixed, SingleTarget, Area, DamageOverTime, Mana, CannonLine, Tall }

    public enum Layer { Health, Armor, Shield }

    /// <summary>Per-tower stat changes from cards and treasures (all zero / one by default).</summary>
    public sealed class TowerBonus
    {
        public double BaseDamage, HealthMult, ArmorMult, ShieldMult, Range, Crit, Stun, Bleed, Burn, Poison, Slow, Levels, ManaRate;
        public double DamageFactor = 1, RateFactor = 1, AreaFactor = 1, ManaCostFactor = 1, XpFactor = 1, TargetFactor = 1, HeightFactor = 1;
        /// <summary>Overrides the tower's accuracy (Seeker Rounds: 1).</summary>
        public double? Accuracy;
        /// <summary>Extra hits per direct hit on another monster nearby: chance x count x damage share (Static Arc, Ricochet).</summary>
        public double ProcHits;
        /// <summary>Conditional damage: multiplier while a condition holds.</summary>
        public List<(double Mult, Condition When)> Conditional = new List<(double, Condition)>();

        public TowerBonus Clone()
        {
            var c = (TowerBonus)MemberwiseClone();
            c.Conditional = new List<(double, Condition)>(Conditional);
            return c;
        }
    }

    public enum Condition { BelowHealth20, Frozen, AllThreeDots, FirstHit, ManaAbove90, Slowed, Poisoned, Burning, Bleeding, Hexed }

    /// <summary>A group of identical towers on a board.</summary>
    public sealed class TowerGroup
    {
        public TowerKind Kind;
        public double Count, Level, Height;
        public TowerBonus Bonus = new TowerBonus();
        public TowerInfo Info => TowerData.Get(Kind);

        public TowerGroup Clone() => new TowerGroup { Kind = Kind, Count = Count, Level = Level, Height = Height, Bonus = Bonus.Clone() };
    }

    /// <summary>Global modifiers (MonsterManager, GameManager, ResourceManager and the mod's own rules).</summary>
    public sealed class BoardMods
    {
        public MonsterMods Monster;
        public double[] DotCap = { 24, 24, 24 };
        /// <summary>Extra multiplier on the matching layer while afflicted (vanilla bonusDamageOnBleed/Burn/Poison).</summary>
        public double[] DotLayerBonus = new double[3];
        /// <summary>Share of the current stacks a crit pops as extra damage (Exsanguinate, Ignite, Expunge).</summary>
        public double[] Pop = new double[3];
        public double BleedingCrit, BonusOnStun, PoisonSlow, BurnSlowBonus;
        public double SlowCap, HasteCap, Fortify;
        public double GlobalSlow, ZoneSlow;
        public double FreezeDuration = 1;
        public bool FreezeNoDecay;
        /// <summary>Luck rolls made one extra time (Four-Leaf Clover).</summary>
        public bool LuckReroll;
        /// <summary>Extra damage multiplier on every layer for hexed monsters, and the hex uptime.</summary>
        public double Hex;
        /// <summary>On-death damage: share of max hit points to monsters within the radius (Volatile Corpses).</summary>
        public double DeathBlast, DeathBlastRadius;
        /// <summary>Share of overkill damage that is passed on (Overkill).</summary>
        public double OverkillCarry;
        /// <summary>Damage-over-time spreads on death to this many monsters (Contagion).</summary>
        public double DotSpread;
        public double OnDeathFactor = 1;

        public double MaxHealth = 100, MaxHealthFactor = 1, HealPerWave, AbsorbPerWave;
        /// <summary>Extra safety of the castle beyond its health (Second Wind: x1.5).</summary>
        public double SafetyFactor = 1;
        public double MaxMana, ManaRegen, ManaPerSecond, ManaPerKill;
        public double KillGold, KillGoldFactor = 1, IncomeFactor = 1, PriceStepFactor = 1, LevelCostFactor = 1;
        /// <summary>Extra gold received from the acquisition until the evaluated wave (spent on towers).</summary>
        public double ExtraGold;
        /// <summary>Points that don't go through headroom (meta: cards, treasures), already in points.</summary>
        public double DirectPoints;

        public BoardMods Clone()
        {
            var c = (BoardMods)MemberwiseClone();
            c.DotCap = (double[])DotCap.Clone();
            c.DotLayerBonus = (double[])DotLayerBonus.Clone();
            c.Pop = (double[])Pop.Clone();
            return c;
        }
    }

    /// <summary>A reference board: a snapshot of a typical run at one wave (section 1.1).</summary>
    public sealed class Board
    {
        public int Wave;
        public BoardVariant Variant;
        public int Lanes = 1;
        public List<TowerGroup> Groups = new List<TowerGroup>();
        public BoardMods Mods = new BoardMods();
        /// <summary>Tower bonus for every tower (applied on top of each group's own).</summary>
        public TowerBonus All = new TowerBonus();
        public double Invest, Bank, Income, PathLength, Placement;
        public double Siphons, Banks, Universities, Houses;
        public double CardsOwned, TreasuresOwned;

        public double TowerCount { get { double n = 0; foreach (TowerGroup g in Groups) n += g.Count; return n; } }

        public TowerGroup Group(TowerKind k)
        {
            foreach (TowerGroup g in Groups) if (g.Kind == k) return g;
            return null;
        }

        public IEnumerable<TowerGroup> Scope(TowerKind? k)
        {
            foreach (TowerGroup g in Groups) if (k == null || g.Kind == k) yield return g;
        }

        public Board Clone()
        {
            var b = (Board)MemberwiseClone();
            b.Groups = new List<TowerGroup>();
            foreach (TowerGroup g in Groups) b.Groups.Add(g.Clone());
            b.Mods = Mods.Clone();
            b.All = All.Clone();
            return b;
        }

        /// <summary>Price of the next copy of a tower kind, given how many are owned.</summary>
        public double Price(TowerKind k, double owned)
        {
            TowerInfo t = TowerData.Get(k);
            return t.Price + t.PriceStep * Mods.PriceStepFactor * owned;
        }

        /// <summary>Spends gold on more copies, split over the groups by their share of the investment.</summary>
        /// <summary>Spends gold on more copies (or, with negative gold, gives up copies), split by the groups' investment.</summary>
        public void Spend(double gold, Func<TowerGroup, double> weight = null)
        {
            if (Math.Abs(gold) < 1e-9 || Groups.Count == 0) return;
            double total = 0;
            foreach (TowerGroup g in Groups) total += weight?.Invoke(g) ?? GroupCost(g);
            if (total <= 0) return;
            foreach (TowerGroup g in Groups)
            {
                double share = gold * (weight?.Invoke(g) ?? GroupCost(g)) / total;
                if (share >= 0) g.Count += Copies(g.Kind, g.Count, share);
                else g.Count = Math.Max(0, g.Count + share / Price(g.Kind, Math.Max(0, g.Count - 1)));
            }
        }

        public double GroupCost(TowerGroup g)
        {
            TowerInfo t = TowerData.Get(g.Kind);
            return g.Count * t.Price + t.PriceStep * Mods.PriceStepFactor * g.Count * (g.Count - 1) / 2;
        }

        /// <summary>How many more copies <paramref name="gold"/> buys at the stepped price (fractional).</summary>
        public double Copies(TowerKind k, double owned, double gold)
        {
            TowerInfo t = TowerData.Get(k);
            double step = t.PriceStep * Mods.PriceStepFactor, p0 = t.Price + step * owned;
            if (step <= 1e-9) return gold / p0;
            // n p0 + step n (n - 1) / 2 = gold
            double a = step / 2, b = p0 - step / 2;
            return (-b + Math.Sqrt(b * b + 4 * a * gold)) / (2 * a);
        }
    }
}

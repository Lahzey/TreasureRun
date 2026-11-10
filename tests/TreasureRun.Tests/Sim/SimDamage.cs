using System.Collections.Generic;
using TreasureRun.Core;
using UnityEngine;

namespace TreasureRun.Tests.Sim
{
    /// <summary>One hit, as passed to vanilla Enemy.TakeDamage.</summary>
    public struct SimHit
    {
        public string Source;
        public int BaseDamage, HealthDamage, ArmorDamage, ShieldDamage;
        public float Slow, Bleed, Burn, Poison, Crit, Stun;

        public SimHit(string source, int baseDamage, int health, int armor, int shield)
        {
            Source = source;
            BaseDamage = baseDamage;
            HealthDamage = health; ArmorDamage = armor; ShieldDamage = shield;
            Slow = Bleed = Burn = Poison = Crit = Stun = 0f;
        }

        public override string ToString() => $"{Source} {BaseDamage} x {HealthDamage}/{ArmorDamage}/{ShieldDamage}";
    }

    /// <summary>Damage dealt, per source (vanilla DamageTracker).</summary>
    public sealed class SimDamageLog
    {
        public readonly Dictionary<string, long> BySource = new Dictionary<string, long>();
        public readonly List<string> Kills = new List<string>();
        public long Total;

        public void Add(string source, int amount)
        {
            if (amount <= 0) return;
            BySource.TryGetValue(source, out long v);
            BySource[source] = v + amount;
            Total += amount;
        }

        public long Of(string source) => BySource.TryGetValue(source, out long v) ? v : 0;
    }

    /// <summary>
    /// Pure-C# port of the vanilla damage formula: Enemy.TakeDamage, the damage-over-time ticks (DOTUpdate), slow,
    /// freeze, fortify and regen (Assembly-CSharp Enemy.cs, game version 1.3.2; see docs/reference/vanilla-towers.md).
    /// Kept as close to the original as possible, including its quirks; the tests in SimDamageTests pin it down.
    /// </summary>
    public static class SimDamage
    {
        public const string Dot = "DOT";
        public const string Lookout = "Lookout";

        public static void TakeDamage(SimEnemy e, SimHit hit, IRng rng, SimMonsterMods mods, SimDamageLog log)
        {
            if (e.Dead) return;
            int baseDmg = hit.BaseDamage;

            if (rng.Value() < hit.Stun && e.FreezeTime <= 0f) Freeze(e);

            float crit = hit.Crit, critNoMark = hit.Crit;
            if (baseDmg > -1 && BleedCheck(e, mods) > 0)
            {
                crit += mods.BleedingCritChance;
                critNoMark += mods.BleedingCritChance;
                if (e.Mark != null) crit += e.Mark.CritChance;
            }

            // Three crit rolls (x2 / x3 / x4), each tier capped at 50%.
            int dmg = baseDmg;
            float multiplier = 1f;
            float r5 = rng.Value(), r6 = rng.Value(), r7 = rng.Value();
            if (r7 < Mathf.Min(crit - 1f, 0.5f)) { dmg *= 4; multiplier = 2.5f; }
            else if (r6 < Mathf.Min(crit - 0.5f, 0.5f)) { dmg *= 3; multiplier = 2f; }
            else if (r5 < Mathf.Min(crit, 0.5f)) { dmg *= 2; multiplier = 1.5f; }

            int dmgNoMark = baseDmg; // the tower's own share (without the mark's crit), for the damage log
            if (r7 < Mathf.Min(critNoMark - 1f, 0.5f)) dmgNoMark *= 4;
            else if (r6 < Mathf.Min(critNoMark, 0.5f)) dmgNoMark *= 3;
            else if (r5 < Mathf.Min(critNoMark, 0.5f)) dmgNoMark *= 2;

            int fort = e.Fortified ? 5 + mods.FortificationBonus : 0;
            int freeze = e.FreezeTime > 0f ? mods.BonusDamageOnStun : 0;
            SimMark m = e.Mark;
            int markBase = m?.Damage ?? 0;

            int dealt, own, overflow = 0;
            if (e.Shield > 0)
            {
                own = Mathf.Max(dmgNoMark - fort + freeze, 1) * (hit.ShieldDamage + PoisonCheck(e, mods));
                dealt = Mathf.Max(dmg - fort + markBase + freeze, 1) * (hit.ShieldDamage + PoisonCheck(e, mods) + (m?.ShieldDamage ?? 0));
                if (dealt > e.Shield) overflow = (int)((float)(dealt - e.Shield) / hit.ShieldDamage);
                e.Shield = Mathf.Max(e.Shield - dealt, 0);
                Log(log, hit.Source, dealt, own);
            }
            else if (e.Armor > 0)
            {
                own = Mathf.Max(dmgNoMark - fort + freeze, 1) * (hit.ArmorDamage + BurnCheck(e, mods));
                dealt = Mathf.Max(dmg - fort + markBase + freeze, 1) * (hit.ArmorDamage + BurnCheck(e, mods) + (m?.ArmorDamage ?? 0));
                if (dealt > e.Armor) overflow = (int)((float)(dealt - e.Armor) / hit.ArmorDamage);
                e.Armor = Mathf.Max(e.Armor - dealt, 0);
                Log(log, hit.Source, dealt, own);
            }
            else
            {
                own = Mathf.Max(dmgNoMark - fort + freeze, 1) * (hit.HealthDamage + BleedCheck(e, mods));
                dealt = Mathf.Max(dmg - fort + markBase + freeze, 1) * (hit.HealthDamage + BleedCheck(e, mods) + (m?.HealthDamage ?? 0));
                Log(log, hit.Source, Mathf.Min(dealt, Mathf.Max(e.Health, 0)), Mathf.Min(own, Mathf.Max(e.Health, 0)));
                e.Health -= dealt;
            }

            if (e.Health <= 0)
            {
                e.Dead = true;
                log?.Kills.Add(hit.Source);
                return;
            }

            e.Bleed += (int)(dealt * hit.Bleed);
            e.Burn += (int)(dealt * hit.Burn);
            e.Poison += (int)(dealt * hit.Poison);
            if (hit.Slow > 0f) AddSlow(e, dealt * hit.Slow / 100f, mods);
            if (hit.Poison > 0f) AddSlow(e, mods.PoisonSlowPercent * hit.Poison * dealt / 100f, mods);

            if (multiplier > 1f && baseDmg > -1 && mods.BleedPop > 0f && !e.Dead)
            {
                int pop = (int)(e.Bleed * mods.BleedPop);
                e.Bleed -= pop;
                TakeDamage(e, new SimHit(Dot, -999, pop, pop / 2, pop / 2), rng, mods, log);
            }
            if (multiplier > 1f && baseDmg > -1 && mods.BurnPop > 0f && !e.Dead)
            {
                int pop = (int)(e.Burn * mods.BurnPop);
                e.Burn -= pop;
                TakeDamage(e, new SimHit(Dot, -999, pop / 2, pop, pop / 2), rng, mods, log);
            }
            if (multiplier > 1f && baseDmg > -1 && mods.PoisonPop > 0f && !e.Dead)
            {
                int pop = (int)(e.Poison * mods.PoisonPop);
                e.Poison -= pop;
                TakeDamage(e, new SimHit(Dot, -999, pop / 2, pop / 2, pop), rng, mods, log);
            }

            if (overflow > 0 && !e.Dead)
            {
                var carry = hit;
                carry.BaseDamage = overflow;
                carry.Crit = carry.Stun = 0f;
                TakeDamage(e, carry, rng, mods, log);
            }
        }

        /// <summary>Advances timers by <paramref name="dt"/>: damage-over-time ticks, slow decay, freeze, fortify, regen.</summary>
        public static void Update(SimEnemy e, float dt, IRng rng, SimMonsterMods mods, SimDamageLog log)
        {
            if (e.Dead) return;
            e.FreezeTime = Mathf.Max(0f, e.FreezeTime - dt);
            e.Slow = Mathf.Max(0f, e.Slow - dt * 0.05f);
            e.FortifiedTime = Mathf.Max(0f, e.FortifiedTime - dt);
            DotUpdate(e, dt, rng, mods, log);
            if (e.Dead) return;

            e.RegenTimer += dt;
            if (e.RegenTimer >= 1f)
            {
                e.RegenTimer = 0f;
                if (e.HealthRegen > 0 && e.Health > 0 && !e.Bleeding) e.Health = Mathf.Min(e.BaseHealth, e.Health + e.HealthRegen);
                if (e.ArmorRegen > 0 && e.Armor > 0 && !e.Burning) e.Armor = Mathf.Min(e.BaseArmor, e.Armor + e.ArmorRegen);
                if (e.ShieldRegen > 0 && e.Shield > 0 && !e.Poisoned) e.Shield = Mathf.Min(e.BaseShield, e.Shield + e.ShieldRegen);
            }
        }

        private static void DotUpdate(SimEnemy e, float dt, IRng rng, SimMonsterMods mods, SimDamageLog log)
        {
            if (e.Poisoned && e.TimeSincePoison <= 0f && e.DotSpacing <= 0f)
            {
                int before = e.Poison;
                e.Poison = Mathf.Max(e.Poison - (int)mods.DotTick.z, 0);
                int used = before - e.Poison;
                if (!e.PoisonImmune) { int half = Mathf.Max(used / 2, 1); TakeDamage(e, new SimHit(Dot, -999, half, half, used), rng, mods, log); }
                e.TimeSincePoison = 1f;
                e.DotSpacing = 0.1f;
            }
            if (!e.Dead && e.Burning && e.TimeSinceBurn <= 0f && e.DotSpacing <= 0f)
            {
                int before = e.Burn;
                e.Burn = Mathf.Max(e.Burn - (int)(mods.DotTick.y * Mathf.Max(1f + mods.BurnSpeedDamagePercentBonus * e.Slow, 1f)), 0);
                int used = before - e.Burn;
                if (!e.BurnImmune) { int half = used / 2; TakeDamage(e, new SimHit(Dot, -999, half, used, half), rng, mods, log); }
                e.TimeSinceBurn = 1f;
                e.DotSpacing = 0.1f;
            }
            if (!e.Dead && e.Bleeding && e.TimeSinceBleed <= 0f && e.DotSpacing <= 0f)
            {
                int before = e.Bleed;
                e.Bleed = Mathf.Max(e.Bleed - (int)mods.DotTick.x, 0);
                int used = before - e.Bleed;
                if (!e.BleedImmune) { int half = used / 2; TakeDamage(e, new SimHit(Dot, -999, used, half, half), rng, mods, log); }
                e.TimeSinceBleed = 1f;
                e.DotSpacing = 0.1f;
            }
            e.TimeSinceBleed -= dt;
            e.TimeSinceBurn -= dt;
            e.TimeSincePoison -= dt;
            e.DotSpacing -= dt;
        }

        public static void Freeze(SimEnemy e)
        {
            e.FreezeTime = e.MaxFreeze;
            e.MaxFreeze *= 0.8f;
        }

        public static void AddSlow(SimEnemy e, float amount, SimMonsterMods mods) =>
            e.Slow = e.SlowImmune ? 0f : Mathf.Clamp(e.Slow + amount, 0f, 0.6f + mods.SlowCapModifier);

        private static int BleedCheck(SimEnemy e, SimMonsterMods m) => e.Bleed > 0 && !e.BleedImmune ? 1 + m.BonusDamageOnBleed : 0;
        private static int BurnCheck(SimEnemy e, SimMonsterMods m) => e.Burn > 0 && !e.BurnImmune ? 1 + m.BonusDamageOnBurn : 0;
        private static int PoisonCheck(SimEnemy e, SimMonsterMods m) => e.Poison > 0 && !e.PoisonImmune ? 1 + m.BonusDamageOnPoison : 0;

        /// <summary>Vanilla credits the mark's extra damage to the Lookout and the rest to the hitting tower.</summary>
        private static void Log(SimDamageLog log, string source, int dealt, int own)
        {
            if (log == null) return;
            if (dealt - own > 0) { log.Add(Lookout, dealt - own); log.Add(source, own); }
            else log.Add(source, dealt);
        }
    }
}

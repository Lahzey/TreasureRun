using System;
using System.Collections.Generic;

namespace TreasureRun.Balance
{
    /// <summary>Monster-side modifiers (vanilla MonsterManager fields that cards and treasures change).</summary>
    public struct MonsterMods
    {
        public float ExtraHealth, ExtraArmor, ExtraShield, SpeedBonus;
        public int ExtraCastleDamage, ExtraGold;

        public bool IsZero => ExtraHealth == 0 && ExtraArmor == 0 && ExtraShield == 0 && SpeedBonus == 0 && ExtraCastleDamage == 0 && ExtraGold == 0;
    }

    /// <summary>Expected contents of one wave (averaged over the random draws and the tier classes).</summary>
    public sealed class WaveStats
    {
        public int Wave;
        /// <summary>Monsters of the stream (everything but the boss), children included.</summary>
        public double Count;
        /// <summary>Spawn events of the spawner (two per second): the stream's length in seconds is SpawnEvents / 2.</summary>
        public double SpawnEvents;
        public double Health, Armor, Shield;
        /// <summary>Count-weighted mean base speed of the stream.</summary>
        public double Speed;
        /// <summary>Castle damage if the whole stream leaked, and per leak on average.</summary>
        public double CastleDamage;
        /// <summary>Gold of all kills, without the tower-type part (vanilla: level + bonus).</summary>
        public double KillGold;
        /// <summary>Regeneration per second of an average monster, per layer.</summary>
        public double RegenHealth, RegenArmor, RegenShield;
        /// <summary>Share of the stream's effective hit points on monsters immune to bleed / burn / poison / slow.</summary>
        public double ImmuneBleed, ImmuneBurn, ImmunePoison, ImmuneSlow;
        /// <summary>Average share of a monster's passage it is fortified, and its average haste.</summary>
        public double FortifyShare, Haste;
        /// <summary>Boss of the wave (waves 15/25/35/45), children included; zero otherwise.</summary>
        public double BossHealth, BossArmor, BossShield, BossSpeed;
        public bool HasBoss => BossHealth + BossArmor + BossShield > 0;

        public double Total => Health + Armor + Shield;
        public double BossTotal => BossHealth + BossArmor + BossShield;
        public double StreamSeconds => SpawnEvents / 2.0;
        public double MeanTotal => Count > 0 ? Total / Count : 0;
    }

    /// <summary>
    /// The vanilla wave generator in pure C# (SpawnManager.Spawn, SpawnSelection, SpawnFromArray, SpawnBoss and
    /// SpawnMiniBoss), evaluated as expected values: many seeded draws, every tier class equally often.
    /// </summary>
    public static class WaveModel
    {
        public const int LastWave = 45;
        private const int Samples = 48;
        /// <summary>Assumed seconds a monster is on the path, for periodic battle cries and fortify uptime.</summary>
        public const double PassageSeconds = 30;

        private static readonly Dictionary<int, WaveStats> cache = new Dictionary<int, WaveStats>();

        /// <summary>Expected wave <paramref name="wave"/> without monster modifiers (cached).</summary>
        public static WaveStats Get(int wave)
        {
            lock (cache)
            {
                if (!cache.TryGetValue(wave, out WaveStats s)) cache[wave] = s = Compute(wave);
                return s;
            }
        }

        /// <summary>Wave with monster modifiers applied (health/armor/shield %, speed, castle damage, gold).</summary>
        public static WaveStats Get(int wave, MonsterMods mods)
        {
            WaveStats b = Get(wave);
            if (mods.IsZero) return b;
            var s = b.Copy();
            s.Health *= 1 + mods.ExtraHealth; s.Armor *= 1 + mods.ExtraArmor; s.Shield *= 1 + mods.ExtraShield;
            s.BossHealth *= 1 + mods.ExtraHealth; s.BossArmor *= 1 + mods.ExtraArmor; s.BossShield *= 1 + mods.ExtraShield;
            s.Speed += mods.SpeedBonus; s.BossSpeed += mods.SpeedBonus;
            s.CastleDamage += mods.ExtraCastleDamage * s.Count;
            s.KillGold = Math.Max(0, s.KillGold + mods.ExtraGold * s.Count);
            return s;
        }

        private static WaveStats Copy(this WaveStats s) => new WaveStats
        {
            Wave = s.Wave, Count = s.Count, SpawnEvents = s.SpawnEvents, Health = s.Health, Armor = s.Armor, Shield = s.Shield,
            Speed = s.Speed, CastleDamage = s.CastleDamage, KillGold = s.KillGold, RegenHealth = s.RegenHealth,
            RegenArmor = s.RegenArmor, RegenShield = s.RegenShield, ImmuneBleed = s.ImmuneBleed, ImmuneBurn = s.ImmuneBurn,
            ImmunePoison = s.ImmunePoison, ImmuneSlow = s.ImmuneSlow, FortifyShare = s.FortifyShare, Haste = s.Haste,
            BossHealth = s.BossHealth, BossArmor = s.BossArmor, BossShield = s.BossShield, BossSpeed = s.BossSpeed,
        };

        private sealed class Acc
        {
            public double Count, Events, H, A, S, Speed, Castle, Gold, RH, RA, RS, IB, IU, IP, IS, Fort, Haste;
            public double BH, BA, BS, BSpeed, BCount;
        }

        private static WaveStats Compute(int wave)
        {
            var acc = new Acc();
            var rng = new Random(9173 + wave);
            for (int i = 0; i < Samples; i++)
            {
                int c2 = 1 + i % 3, c3 = 1 + (i / 3) % 3, c4 = 1 + (i / 9) % 3; // every class combination equally often
                Sample(wave, c2, c3, c4, rng, acc);
            }
            double n = Samples;
            var s = new WaveStats
            {
                Wave = wave,
                Count = acc.Count / n, SpawnEvents = acc.Events / n,
                Health = acc.H / n, Armor = acc.A / n, Shield = acc.S / n,
                Speed = acc.Count > 0 ? acc.Speed / acc.Count : 2,
                CastleDamage = acc.Castle / n, KillGold = acc.Gold / n,
                RegenHealth = acc.Count > 0 ? acc.RH / acc.Count : 0,
                RegenArmor = acc.Count > 0 ? acc.RA / acc.Count : 0,
                RegenShield = acc.Count > 0 ? acc.RS / acc.Count : 0,
                FortifyShare = acc.Count > 0 ? acc.Fort / acc.Count : 0,
                Haste = acc.Count > 0 ? acc.Haste / acc.Count : 0,
                BossHealth = acc.BH / n, BossArmor = acc.BA / n, BossShield = acc.BS / n,
                BossSpeed = acc.BCount > 0 ? acc.BSpeed / acc.BCount : 1,
            };
            double tot = acc.H + acc.A + acc.S;
            if (tot > 0) { s.ImmuneBleed = acc.IB / tot; s.ImmuneBurn = acc.IU / tot; s.ImmunePoison = acc.IP / tot; s.ImmuneSlow = acc.IS / tot; }
            return s;
        }

        private static string[] List(int tier, int cls)
        {
            string key = tier == 1 ? "1" : tier + "." + cls;
            foreach (var l in MonsterRoster.Lists) if (l.Key == key) return l.Monsters;
            throw new InvalidOperationException("No monster list " + key);
        }

        private static void Sample(int wave, int c2, int c3, int c4, Random rng, Acc acc)
        {
            string[] t1 = List(1, 1), t2 = List(2, c2), t3 = List(3, c3), t4 = List(4, c4);

            // Boss (spawned first, at the farthest spawn point).
            string boss = null;
            if (wave == 15 || wave == 25 || wave == 35 || wave == 45)
            {
                int cls = wave == 25 ? c2 : wave == 35 ? c3 : wave == 45 ? c4 : 0;
                foreach (var b in MonsterRoster.Bosses) if (b.Wave == wave && (b.Class == 0 || b.Class == cls)) boss = b.Monster;
                if (boss != null) AddBoss(MonsterRoster.Get(boss), acc, 0);
            }

            // Mini-boss on odd waves after 16: tier[(wave - 17) / 2] with the tier as stat multiplier.
            if (wave > 16 && wave % 2 == 1 && wave <= LastWave)
            {
                int idx = (wave - 1 - 16) / 2;
                string[] list = idx < 5 ? t2 : idx < 10 ? t3 : t4;
                int mult = idx < 5 ? 2 : idx < 10 ? 3 : 4;
                int k = idx % 5;
                if (k < list.Length) AddStream(MonsterRoster.Get(list[k]), mult, 4, acc, 0);
            }

            // Stream: budget wave², spent by SpawnSelection.
            int budget = wave * wave;
            int safety = budget + 100;
            while (budget > 0 && safety-- > 0)
            {
                int cost = 0;
                if (wave >= 36) cost = FromArray(t4, 4, wave, rng, acc);
                if (cost <= 0 && wave >= 26) cost = FromArray(t3, 3, wave, rng, acc);
                if (cost <= 0 && wave >= 16) cost = FromArray(t2, 2, wave, rng, acc);
                if (cost <= 0) cost = FromArray(t1, 1, wave, rng, acc);
                budget -= Math.Max(cost, 1);
                acc.Events += 1;
            }
        }

        private static int FromArray(string[] list, float tierMult, int wave, Random rng, Acc acc)
        {
            for (int i = list.Length - 1; i >= 0; i--)
            {
                Monster m = MonsterRoster.Get(list[i]);
                if (m.Level <= wave && rng.NextDouble() < tierMult / m.Level)
                {
                    AddStream(m, 1, 0, acc, 0);
                    return Math.Max((int)Math.Round(m.Level / tierMult, MidpointRounding.ToEven), 1);
                }
            }
            return 0;
        }

        private static void AddStream(Monster m, int mult, int extraCastle, Acc acc, int depth)
        {
            if (m == null || depth > 6) return;
            acc.Count += 1;
            acc.H += m.Health * mult; acc.A += m.Armor * mult; acc.S += m.Shield * mult;
            acc.Speed += m.Speed;
            acc.Castle += m.CastleDamage + extraCastle;
            if (m.DropsGold) acc.Gold += Math.Max(0, m.Level + m.BonusGold);
            acc.RH += m.HealthRegen; acc.RA += m.ArmorRegen; acc.RS += m.ShieldRegen;
            double tot = m.Total * mult;
            if ((m.Immune & Immunity.Bleed) != 0) acc.IB += tot;
            if ((m.Immune & Immunity.Burn) != 0) acc.IU += tot;
            if ((m.Immune & Immunity.Poison) != 0) acc.IP += tot;
            if ((m.Immune & Immunity.Slow) != 0) acc.IS += tot;
            double fort = 0, haste = 0;
            foreach (Cry c in m.Cries)
            {
                // A cry with a radius also reaches about one neighbour per unit of radius in a dense stream.
                double reach = 1 + c.Radius;
                fort += c.Fortify * reach;
                haste += 10 * c.Haste * c.Haste * reach / PassageSeconds; // haste decays 5 points/s: area h^2/(2*0.05)
                if (c.Trigger != CryTrigger.Periodic) continue;
                double times = PassageSeconds / 7.0; // periodic cries fire every 2-12 s (7 on average)
                foreach (string child in c.Spawns)
                    for (int t = 0; t < (int)Math.Round(times); t++) AddStream(MonsterRoster.Get(child), 1, 0, acc, depth + 1);
                // periodic heals on itself and its neighbours count as extra hit points (half of them land on damaged layers)
                acc.H += c.HealHealth * times * reach * 0.5; acc.A += c.HealArmor * times * reach * 0.5; acc.S += c.HealShield * times * reach * 0.5;
            }
            acc.Fort += Math.Min(1, fort / PassageSeconds);
            acc.Haste += Math.Min(0.6, haste);
            foreach (string child in m.Children()) AddStream(MonsterRoster.Get(child), 1, 0, acc, depth + 1);
        }

        private static void AddBoss(Monster m, Acc acc, int depth)
        {
            if (m == null || depth > 6) return;
            acc.BH += m.Health; acc.BA += m.Armor; acc.BS += m.Shield;
            if (depth == 0) { acc.BSpeed += m.Speed; acc.BCount += 1; }
            foreach (Cry c in m.Cries)
            {
                if (c.Trigger == CryTrigger.Periodic)
                {
                    double times = PassageSeconds * 2 / 7.0; // bosses are slow: about twice as long on the path
                    foreach (string child in c.Spawns) for (int t = 0; t < (int)Math.Round(times); t++) AddStream(MonsterRoster.Get(child), 1, 0, acc, depth + 1);
                    acc.BH += c.HealHealth * times; acc.BA += c.HealArmor * times; acc.BS += c.HealShield * times;
                }
                else if (c.Trigger == CryTrigger.ArmorBreak || c.Trigger == CryTrigger.ShieldBreak)
                    foreach (string child in c.Spawns) AddStream(MonsterRoster.Get(child), 1, 0, acc, depth + 1);
            }
            if (m.DeathSpawn != null) AddBoss(MonsterRoster.Get(m.DeathSpawn), acc, depth + 1);
            foreach (Cry c in m.Cries)
                if (c.Trigger == CryTrigger.Death) foreach (string child in c.Spawns) AddBoss(MonsterRoster.Get(child), acc, depth + 1);
        }
    }
}

using System;
using System.Collections.Generic;

namespace TreasureRun.Balance
{
    /// <summary>What one tower group does in a wave (for the breakdown).</summary>
    public sealed class GroupResult
    {
        public TowerKind Kind;
        /// <summary>Display name (a fixed source's own name).</summary>
        public string Name;
        public double Count, Coverage, ActiveSeconds, Targets, Shots, Hits, DamagePerHit, Waste, Mana, Removed, BossDamage;
    }

    /// <summary>Toughness headroom of a board against its wave (docs/balance-framework.md sections 1 and 3-4).</summary>
    public sealed class Headroom
    {
        /// <summary>Raw headroom before the board's normalisation: damage capacity / hit points, stream and boss.</summary>
        public double RawStream, RawBoss;
        /// <summary>Normalised headroom: the stream with leak tolerance, the boss, and both combined.</summary>
        public double Stream, Boss, Combined;
        public double LeakFactor, Slow, Freeze, ManaFactor, ManaDemand, WaveTime, Speed, Density, DotRemoved;
        public double[] DotUptime = new double[3];
        public List<GroupResult> Groups = new List<GroupResult>();
    }

    /// <summary>
    /// The formula model: per tower group Hit x Layer x Crit x Rate x Reliability x Targets x Time, plus damage over time,
    /// against the wave model's hit points. Constants marked "uncalibrated" are hand estimates until the M8 simulation.
    /// </summary>
    public static class Evaluator
    {
        /// <summary>
        /// Crowd density. The spawner releases one monster per spawn point every 0.5 s (SpawnManager.Spawn), so a lane
        /// carries them single file, about one per cell at speed 2 before slows: no extra bunching. Of those, about 40%
        /// are still alive inside a tower's reach on a board that holds its wave. Calibrated so that the plain
        /// area towers (Tesla Coil, Mortar) come out even with the single-target ones per gold, as vanilla's prices
        /// suggest (probe BalanceProbe.TowerBalanceByDensity, docs/balance-framework.md section 13); recorded runs will give
        /// the real numbers.
        /// </summary>
        public static double Bunching = 1.0;
        /// <summary>Share of the monsters in a stretch of path that are still alive when an attack lands (see <see cref="Bunching"/>).</summary>
        public static double AliveShare = 0.4;

        /// <summary>Forgets the boards' normalisation (after changing a constant, for calibration probes).</summary>
        public static void ClearCache() { lock (norms) norms.Clear(); }
        /// <summary>Share of the castle's health a typical wave may cost (attrition budget, uncalibrated).</summary>
        public const double LeakShare = 0.08;
        /// <summary>Castle health is on average this share of its maximum.</summary>
        public const double AverageHealth = 0.7;
        /// <summary>Soft minimum of stream and boss headroom.</summary>
        public const double SoftMinPower = 4;
        /// <summary>Share of the towers' attention a boss gets while it passes (uncalibrated).</summary>
        public const double BossAttention = 0.7;
        /// <summary>A Monument sword's stabs survive this share of their count on average (uncalibrated).</summary>
        public const double SwordSurvival = 0.6;
        /// <summary>
        /// Share of the monsters that reach a trap (a fixed POI damage source) alive: traps sit wherever their section was
        /// placed, usually ahead of the main defence (uncalibrated).
        /// </summary>
        public const double TrapArrival = 0.8;
        /// <summary>Normalised baseline headroom of every reference board (stream before leaks, and boss).</summary>
        public const double BaselineStream = 1.0, BaselineBoss = 1.1;

        private static readonly Dictionary<(int, BoardVariant), (double, double)> norms = new Dictionary<(int, BoardVariant), (double, double)>();

        /// <summary>Headroom of a board, normalised so that the plain reference board of its wave and variant sits at the baseline.</summary>
        public static Headroom Evaluate(Board b)
        {
            (double ks, double kb) = Norm(b.Wave, b.Variant);
            Headroom h = Raw(b);
            Finish(h, b, ks, kb);
            return h;
        }

        private static (double, double) Norm(int wave, BoardVariant v)
        {
            lock (norms)
            {
                if (norms.TryGetValue((wave, v), out var n)) return n;
                Headroom h = Raw(ReferenceBoards.Get(wave, v));
                n = (BaselineStream / Math.Max(1e-9, h.RawStream), h.RawBoss > 0 ? BaselineBoss / h.RawBoss : 1);
                norms[(wave, v)] = n;
                return n;
            }
        }

        private static void Finish(Headroom h, Board b, double ks, double kb)
        {
            WaveStats w = WaveModel.Get(b.Wave, b.Mods.Monster);
            double maxHp = b.Mods.MaxHealth * b.Mods.MaxHealthFactor;
            double allowed = maxHp * AverageHealth * LeakShare + b.Mods.HealPerWave + b.Mods.AbsorbPerWave;
            double share = w.CastleDamage > 0 ? Math.Min(0.8, allowed / w.CastleDamage) : 0;
            h.LeakFactor = 1 / (1 - share);
            h.Stream = h.RawStream * ks * h.LeakFactor;
            h.Boss = h.RawBoss * kb;
            h.Combined = w.HasBoss
                ? Math.Pow(Math.Pow(h.Stream, -SoftMinPower) + Math.Pow(h.Boss, -SoftMinPower), -1 / SoftMinPower)
                : h.Stream;
        }

        private struct Stats
        {
            public TowerGroup G;
            public TowerInfo I;
            public double Base, Range, Rate, Crit, Stun, Area, Accuracy, TargetFactor, Proc, ManaCost, ManaRate, DamageFactor;
            public double[] Mult, Dot;
            public double Slow;
            public List<(double, Condition)> Cond;
        }

        /// <summary>Raw headroom (capacity / hit points) without normalisation.</summary>
        public static Headroom Raw(Board b)
        {
            BoardMods m = b.Mods;
            WaveStats w = WaveModel.Get(b.Wave, m.Monster);
            var h = new Headroom();
            double total = Math.Max(1, w.Total);
            double[] share = { w.Health / total, w.Armor / total, w.Shield / total };
            double[] immune = { w.ImmuneBleed, w.ImmuneBurn, w.ImmunePoison };
            double hasteScale = Math.Max(0, Math.Min(1.5, (0.6 + m.HasteCap) / 0.6));
            double v0 = Math.Max(0.2, w.Speed * (1 + w.Haste * hasteScale) * (1 - m.GlobalSlow));

            var stats = new List<Stats>();
            foreach (TowerGroup g in b.Groups) if (g.Count > 1e-6) stats.Add(StatsOf(b, g));

            double lookoutDmg = 0, lookoutCount = 0;
            double[] lookoutMult = new double[3];
            foreach (Stats s in stats)
                if (s.I.Shape == AttackShape.Mark)
                {
                    lookoutCount += s.G.Count;
                    lookoutDmg = s.Base; lookoutMult = s.Mult;
                }

            double slow = 0, freeze = 0, manaFactor = 1;
            double[] uptime = new double[3];
            double hexUptime = m.Hex > 0 ? 0.9 : 0;
            var results = new List<GroupResult>();
            double removedTotal = 0, bossDamage = 0, dotRemoved = 0;
            double coverSum = 0;
            foreach (Stats s in stats) coverSum += Coverage(b, s) * s.G.Count;
            double zone = b.PathLength * (1 - Math.Exp(-coverSum / Math.Max(1, b.PathLength)));

            // Two passes: speed, density, damage-over-time uptime and mana depend on the hits of the first pass.
            for (int pass = 0; pass < 3; pass++)
            {
                double v = Math.Max(0.15, v0 * (1 - slow * (1 - w.ImmuneSlow)) * (1 - freeze));
                double stream = Math.Max(1, w.StreamSeconds);
                double lambda = w.Count / stream / Math.Max(1, b.Lanes);
                double rho = lambda / v * Bunching * AliveShare;
                double lifetime = zone / v;
                double inZone = Math.Max(1, rho * zone);
                double markShare = lookoutCount > 0 ? Math.Min(1, lookoutCount * 1.5 / inZone) : 0;

                results.Clear();
                removedTotal = 0; bossDamage = 0;
                double slowIn = 0, freezeEvents = 0, manaDemand = 0;
                double[] dotIn = new double[3], dotHits = new double[3];
                double critHits = 0, allHits = 0;

                foreach (Stats s in stats)
                {
                    var r = new GroupResult { Kind = s.G.Kind, Name = s.I.Name, Count = s.G.Count };
                    results.Add(r);
                    if (s.I.Shape == AttackShape.Mark) continue;
                    double c = Coverage(b, s);
                    r.Coverage = c;
                    r.ActiveSeconds = s.I.Shape == AttackShape.Plane ? stream + b.PathLength / v
                        : s.I.Shape == AttackShape.Trap ? stream + c / v
                        : (stream + c / v) * (1 - Math.Exp(-rho * c));
                    double rate = s.Rate;
                    if (s.I.Shape == AttackShape.Dropper) rate *= (10 + PathStats.DropCells(b.Placement)) / 10;
                    r.Targets = Targets(s, rho, b.Variant) * s.TargetFactor * (1 + s.Proc * Math.Min(1, rho * 3));
                    r.Shots = s.G.Count * rate * r.ActiveSeconds * s.Accuracy;

                    // Damage per hit on each layer.
                    double crit = s.Crit + m.BleedingCrit * uptime[0];
                    double critFactor = CritFactor(crit, m.LuckReroll);
                    double baseDmg = s.Base + markShare * lookoutDmg + m.BonusOnStun * freeze;
                    foreach (var (mult, when) in s.Cond)
                        if (when == Condition.Slowed) baseDmg += mult * slow / 0.15;
                    double fort = 5 + m.Fortify;
                    double baseEff = (1 - w.FortifyShare) * Math.Max(baseDmg, 1) + w.FortifyShare * Math.Max(baseDmg - fort, 1);
                    if (s.I.Shape == AttackShape.Sword) baseEff = SwordSurvival * baseEff * (baseEff + 1) / 2;
                    double sawFactor = 1;
                    if (s.I.Shape == AttackShape.Saw)
                    {
                        double hitsPerBlade = Math.Min(Math.Max(1, baseDmg), r.Targets);
                        sawFactor = 1 - (hitsPerBlade - 1) / (2 * Math.Max(1, baseDmg));
                    }
                    double mean = Math.Max(1, w.MeanTotal);
                    double plain = Math.Max(1, baseEff * (share[0] * s.Mult[0] + share[1] * s.Mult[1] + share[2] * s.Mult[2]) * critFactor * s.DamageFactor);
                    double hitsPerKill = Math.Max(1, mean / plain);
                    double cond = 1;
                    foreach (var (mult, cd) in s.Cond)
                        if (cd != Condition.Slowed) cond *= 1 + (mult - 1) * Uptime(cd, share, uptime, freeze, slow, hitsPerKill, manaFactor, b);
                    double inv = 0;
                    double[] dealt = new double[3];
                    for (int l = 0; l < 3; l++)
                    {
                        double mult = s.Mult[l] + (1 + m.DotLayerBonus[l]) * uptime[l] * (1 - immune[l])
                                      + markShare * lookoutMult[l] + m.Hex * hexUptime;
                        dealt[l] = Math.Max(1, baseEff * mult * sawFactor * critFactor * cond * s.DamageFactor);
                        if (share[l] > 0) inv += share[l] / dealt[l];
                    }
                    double perHit = inv > 0 ? 1 / inv : dealt[0];
                    r.DamagePerHit = perHit;
                    double waste = perHit >= mean ? 1 - mean / perHit : perHit / (2 * mean);
                    r.Waste = waste * (1 - m.OverkillCarry);

                    double shotsPaid = s.G.Count * rate * r.ActiveSeconds; // mana is paid per shot fired
                    if (s.ManaRate > 0) r.Mana = shotsPaid * Math.Max(1, baseDmg) * s.ManaRate * s.ManaCost;
                    if (s.I.ManaPerSecond > 0) r.Mana += s.G.Count * s.I.ManaPerSecond * s.ManaCost * (stream + b.PathLength / v);
                    manaDemand += r.Mana;
                    double mf = r.Mana > 0 ? manaFactor : 1;

                    r.Hits = r.Shots * r.Targets * mf;
                    r.Removed = r.Hits * perHit * (1 - r.Waste);
                    if (s.I.Shape == AttackShape.Trap)
                    {
                        // A trap works per monster, not per crowd: each one that walks through takes rate x time-in-zone
                        // strikes (capacity, with the towers' overkill waste per strike).
                        double passes = w.Count / Math.Max(1, b.Lanes) * TrapArrival * s.G.Count;
                        double strikes = s.Rate * c / v;
                        r.Hits = passes * strikes * mf;
                        r.Removed = r.Hits * perHit * (1 - r.Waste);
                    }
                    removedTotal += r.Removed;
                    allHits += r.Hits;
                    critHits += r.Hits * Math.Min(1, crit);

                    // Boss: every tower that reaches it fires at it while it passes (single-target damage).
                    if (w.HasBoss)
                    {
                        double bossTotal = Math.Max(1, w.BossTotal);
                        double binv = (w.BossHealth / bossTotal) / dealt[0] + (w.BossArmor / bossTotal) / dealt[1] + (w.BossShield / bossTotal) / dealt[2];
                        double bossPerHit = binv > 0 ? 1 / binv : dealt[0];
                        double bossSpeed = Math.Max(0.15, w.BossSpeed * (1 - m.GlobalSlow) * (1 - slow) * (1 - freeze));
                        double bossTime = s.I.Shape == AttackShape.Plane ? b.PathLength / bossSpeed : c / bossSpeed;
                        r.BossDamage = s.G.Count * rate * s.Accuracy * bossTime * bossPerHit * BossAttention * mf * (s.I.Shape == AttackShape.Saw ? 0.5 : 1);
                        bossDamage += r.BossDamage;
                    }

                    // Inflows for the next pass.
                    for (int l = 0; l < 3; l++)
                        if (s.Dot[l] > 0) { dotIn[l] += r.Removed * s.Dot[l]; dotHits[l] += r.Hits; }
                    if (s.Slow > 0) slowIn += r.Removed * s.Slow / 100;
                    if (s.Stun > 0) freezeEvents += r.Hits * Luck(s.Stun, m.LuckReroll);
                }

                // Damage over time: capped per monster per second; the excess can be popped by crits.
                double count = Math.Max(1, w.Count);
                double critsPerMonster = critHits / count;
                dotRemoved = 0;
                for (int l = 0; l < 3; l++)
                {
                    double cap = m.DotCap[l] * (l == 1 ? 1 + m.BurnSlowBonus * slow : 1);
                    double inflow = dotIn[l] * (1 - immune[l]);
                    double capacity = cap * count * lifetime * (1 - immune[l]);
                    double delivered = Math.Min(inflow, capacity);
                    double excess = Math.Max(0, inflow - capacity);
                    delivered += excess * (1 - Math.Pow(1 - Math.Min(1, m.Pop[l]), critsPerMonster)) * Math.Min(1, m.Pop[l] > 0 ? 1 : 0);
                    delivered *= 1 + 0.3 * m.DotSpread;
                    double eff = share[l] + (1 - share[l]) * 0.5;
                    dotRemoved += delivered * eff;
                    uptime[l] = (1 - Math.Exp(-dotHits[l] * (1 + m.DotSpread) / count / 2)) * (1 - immune[l]);
                }
                removedTotal += dotRemoved;

                // Hit points taken by other means (a Wizard's Tower's minions and goldify).
                removedTotal += w.Total * m.ExtraRemoved;

                // On-death blasts (Volatile Corpses) hit the neighbours of each dying monster.
                if (m.DeathBlast > 0)
                    removedTotal += w.Total * m.DeathBlast * m.OnDeathFactor * Math.Min(4, rho * 2 * m.DeathBlastRadius);

                // Crowd control for the next pass.
                double slowRate = (slowIn + m.PoisonSlow * dotIn[2] / 100) / (count * Math.Max(1, lifetime));
                double slowCap = Math.Max(0, 0.6 + m.SlowCap);
                slow = slowCap * (1 - Math.Exp(-slowRate / (0.05 * Math.Max(0.05, slowCap))));
                if (m.ZoneSlow > 0)
                {
                    double near = Math.Min(1, PathStats.Coverage(6, 0.5, b.PathLength) / Math.Max(1, zone));
                    slow += (Math.Max(slow, m.ZoneSlow) - slow) * near;
                }
                double events = freezeEvents / count;
                double dur = m.FreezeDuration * (m.FreezeNoDecay || events < 1e-6 ? 1 : (1 - Math.Pow(0.8, events)) / (0.2 * events));
                freeze = Math.Min(0.8, events * dur / Math.Max(1, lifetime));

                // Mana: income over the wave, the carried-over pool and kill refunds.
                double waveTime = stream + b.PathLength / v;
                double supply = (m.ManaPerSecond + m.ManaRegen * m.MaxMana) * waveTime + 0.3 * m.MaxMana + m.ManaPerKill * m.MaxMana * count;
                manaFactor = manaDemand > 0 ? Math.Min(1, supply / manaDemand) : 1;
                h.ManaDemand = manaDemand; h.WaveTime = waveTime;

                h.Slow = slow; h.Freeze = freeze; h.ManaFactor = manaFactor; h.Speed = v; h.Density = rho;
            }

            h.DotRemoved = dotRemoved;
            h.DotUptime = uptime;
            h.Groups = results;
            h.RawStream = removedTotal / total;
            h.RawBoss = w.HasBoss ? bossDamage / Math.Max(1, w.BossTotal) : 0;
            return h;
        }

        private static double Coverage(Board b, Stats s)
        {
            switch (s.I.Shape)
            {
                case AttackShape.Plane: return b.PathLength;
                case AttackShape.Trap: return s.Area;
                case AttackShape.Dropper:
                case AttackShape.Mines: return PathStats.DropCells(b.Placement);
                default: return PathStats.Coverage(s.Range, b.Placement, b.PathLength);
            }
        }

        private static double Targets(Stats s, double rho, BoardVariant variant)
        {
            switch (s.I.Shape)
            {
                case AttackShape.Blast:
                case AttackShape.Mines: return 1 + rho * 1.6 * s.Area;
                case AttackShape.Pulse: return Math.Max(1, rho * PathStats.Coverage(s.Range, 0.5, 1e9));
                case AttackShape.Line: return 1 + rho * s.Area * s.Range * 0.6;
                case AttackShape.Pierce: return 1 + rho * s.Area * s.Range * (variant == BoardVariant.CannonLine ? 0.9 : 0.5);
                case AttackShape.Saw: return 1 + rho * 10;
                case AttackShape.Trap: return rho * s.Area;
                default: return 1;
            }
        }

        private static double Uptime(Condition c, double[] share, double[] dot, double freeze, double slow, double hitsPerKill, double mana, Board b)
        {
            switch (c)
            {
                case Condition.BelowHealth20: return 0.2 * share[0];
                case Condition.Frozen: return freeze;
                case Condition.AllThreeDots: return dot[0] * dot[1] * dot[2];
                case Condition.FirstHit: return 1 / Math.Max(1, hitsPerKill);
                case Condition.ManaAbove90: return Math.Max(0, Math.Min(1, (mana >= 1 ? 0.5 : 0.1)));
                case Condition.Bleeding: return dot[0];
                case Condition.Burning: return dot[1];
                case Condition.Poisoned: return dot[2];
                case Condition.Hexed: return 0.9;
                default: return 0;
            }
        }

        /// <summary>Expected damage factor of vanilla's three crit rolls (x2, x3, x4; each tier capped at 50%).</summary>
        public static double CritFactor(double crit, bool reroll)
        {
            double p2 = Luck(Clamp(crit, 0, 0.5), reroll), p3 = Luck(Clamp(crit - 0.5, 0, 0.5), reroll), p4 = Luck(Clamp(crit - 1, 0, 0.5), reroll);
            double x4 = p4, x3 = (1 - p4) * p3, x2 = (1 - p4) * (1 - p3) * p2;
            return 4 * x4 + 3 * x3 + 2 * x2 + (1 - x4 - x3 - x2);
        }

        private static double Luck(double p, bool reroll) => reroll ? 1 - (1 - p) * (1 - p) : p;
        private static double Clamp(double x, double a, double b) => Math.Max(a, Math.Min(b, x));

        private static Stats StatsOf(Board b, TowerGroup g)
        {
            TowerInfo t = g.Info;
            TowerBonus x = g.Bonus, a = g.Fixed ? new TowerBonus() : b.All;
            double level = g.Level + x.Levels + a.Levels;
            double xp = x.XpFactor * a.XpFactor;
            if (Math.Abs(xp - 1) > 1e-9) level = LevelWithXp(level, xp);
            double height = g.Height * x.HeightFactor * a.HeightFactor;
            var s = new Stats
            {
                G = g, I = t,
                Base = t.Damage + level + height + x.BaseDamage + a.BaseDamage,
                Range = t.Range + 0.5 * height + x.Range + a.Range,
                Rate = t.Interval > 0 ? x.RateFactor * a.RateFactor / t.Interval : 0,
                Crit = x.Crit + a.Crit,
                Stun = x.Stun + a.Stun,
                Area = t.Area * x.AreaFactor * a.AreaFactor,
                Accuracy = x.Accuracy ?? a.Accuracy ?? t.Accuracy,
                TargetFactor = x.TargetFactor * a.TargetFactor,
                Proc = x.ProcHits + a.ProcHits,
                ManaCost = x.ManaCostFactor * a.ManaCostFactor,
                ManaRate = t.ManaRate + x.ManaRate + a.ManaRate,
                DamageFactor = x.DamageFactor * a.DamageFactor,
                Mult = new[]
                {
                    t.HealthMult + level / 3 + x.HealthMult + a.HealthMult,
                    t.ArmorMult + level / 3 + x.ArmorMult + a.ArmorMult,
                    t.ShieldMult + level / 3 + x.ShieldMult + a.ShieldMult,
                },
                Dot = new[] { t.Bleed + x.Bleed + a.Bleed, t.Burn + x.Burn + a.Burn, t.Poison + x.Poison + a.Poison },
                Slow = t.Slow + x.Slow + a.Slow,
                Cond = new List<(double, Condition)>(x.Conditional),
            };
            s.Cond.AddRange(a.Conditional);
            if (t.Shape == AttackShape.Mark) s.Base = t.Damage + level + x.BaseDamage; // a Lookout's mark damage
            return s;
        }

        /// <summary>Level reached with <paramref name="xpFactor"/> times the XP (a level costs 10 x level XP).</summary>
        public static double LevelWithXp(double level, double xpFactor)
        {
            double xp = 5 * level * (level - 1) * xpFactor;
            return (1 + Math.Sqrt(1 + 4 * xp / 5)) / 2;
        }
    }
}

using System;
using System.Collections.Generic;
using TreasureRun.Balance;
using TreasureRun.Core;
using UnityEngine;

namespace TreasureRun.Pois
{
    /// <summary>The spells of the Wizard's Tower (docs/poi-plan.md §7).</summary>
    public enum WizardSpell
    {
        /// <summary>A fireball at the largest group: a blast and a burn.</summary>
        Fireball = 0,
        /// <summary>A lightning strike: high single-target damage, strong against shields.</summary>
        Lightning = 1,
        /// <summary>Three ice spikes at different monsters (the fastest first): damage and a slow.</summary>
        IceSpikes = 2,
        /// <summary>A tornado that drifts back up the path, dragging monsters (not bosses) with it.</summary>
        Tornado = 3,
        /// <summary>A necromantic aura over the whole range: monsters slain inside rise as the Necropolis's minions.</summary>
        Necromancy = 4,
        /// <summary>Turns a monster (not a boss) into gold: it dies at once and pays 25 times its kill gold.</summary>
        Goldify = 5,
    }

    /// <summary>A monster as the wizard's targeting sees it.</summary>
    public struct WizardTarget
    {
        /// <summary>Path distance to the castle (vanilla Pathfinder.distanceFromEnd): lower is further along.</summary>
        public float Progress;
        public float Speed;
        public int Health, Armor, Shield;
        public bool Boss;
        /// <summary>Horizontal position (world x, z).</summary>
        public float X, Z;
        public int Total => Math.Max(0, Health) + Math.Max(0, Armor) + Math.Max(0, Shield);
    }

    /// <summary>
    /// The Wizard's Tower POI (docs/poi-plan.md §7): an erratic wizard's crooked tower on a cell of its section that is
    /// neither on the edge nor next to the path. Every <see cref="Interval"/> seconds during waves he casts a random spell
    /// (weighted, <see cref="Weight"/>) at monsters within <see cref="Range"/>. The whole section is his land: nothing can
    /// be built on it (a Ley Line Network's siphons excepted). Siphons next to the tower feed it <see cref="LeyManaPerSecond"/>
    /// mana per second, and he casts every <see cref="LeyInterval"/> seconds. No treasure or card changes his spells.
    /// Pure, offline-tested.
    /// </summary>
    public static class WizardTowerLogic
    {
        public const string Id = "wizard_tower";
        public const int MinWave = 3;
        /// <summary>Reach of his spells (world units = cells), horizontal distance from the tower.</summary>
        public const float Range = 10f;
        public const float Interval = 5f, LeyInterval = 4f;
        public const int LeyManaPerSecond = 10;

        public static readonly WizardSpell[] Spells =
            { WizardSpell.Fireball, WizardSpell.Lightning, WizardSpell.IceSpikes, WizardSpell.Tornado, WizardSpell.Necromancy, WizardSpell.Goldify };

        /// <summary>Common 3, rare 1, very rare 0.4.</summary>
        public static double Weight(WizardSpell s)
        {
            switch (s)
            {
                case WizardSpell.Fireball:
                case WizardSpell.Lightning:
                case WizardSpell.IceSpikes: return 3;
                case WizardSpell.Tornado:
                case WizardSpell.Necromancy: return 1;
                default: return 0.4;
            }
        }

        public static double TotalWeight
        {
            get { double t = 0; foreach (WizardSpell s in Spells) t += Weight(s); return t; }
        }

        /// <summary>Share of the casts that are this spell (when every spell has a target).</summary>
        public static double Chance(WizardSpell s) => Weight(s) / TotalWeight;

        public static string Rarity(WizardSpell s) => Weight(s) >= 3 ? "common" : Weight(s) >= 1 ? "rare" : "very rare";

        public static string Name(WizardSpell s)
        {
            switch (s)
            {
                case WizardSpell.Fireball: return "Fireball";
                case WizardSpell.Lightning: return "Lightning";
                case WizardSpell.IceSpikes: return "Ice Spikes";
                case WizardSpell.Tornado: return "Tornado";
                case WizardSpell.Necromancy: return "Necromancy";
                default: return "Goldify";
            }
        }

        /// <summary>A spell by weight among those that have a target (<paramref name="valid"/>), from a roll in [0, 1).</summary>
        public static WizardSpell? Pick(double roll01, Func<WizardSpell, bool> valid)
        {
            double total = 0;
            foreach (WizardSpell s in Spells) if (valid(s)) total += Weight(s);
            if (total <= 0) return null;
            double r = Math.Max(0, Math.Min(0.999999, roll01)) * total;
            foreach (WizardSpell s in Spells)
            {
                if (!valid(s)) continue;
                r -= Weight(s);
                if (r < 0) return s;
            }
            for (int i = Spells.Length - 1; i >= 0; i--) if (valid(Spells[i])) return Spells[i];
            return null;
        }

        // ------------------------------------------------------------------ damage (fixed: nothing scales it)

        /// <summary>
        /// The wizard's base damage at a wave (probe BalanceProbe.WizardTowerPower, fitted): until about wave 15 the power
        /// at which the tower is worth the POI budget (15 points) when it appears; later no damage gets there (a spell every
        /// 5 s lands too few hits against the late boards), so the power that reaches 90% of what his spells can be worth:
        /// they kill what they hit. About 6 at wave 5, 35 at 10, 190 at 20, 450 at 30, 795 at 40. Every damaging spell is
        /// a multiple of it.
        /// </summary>
        public static double Power(int wave)
        {
            double w = Math.Max(MinWave, wave);
            return PowerBase + PowerLinear * w + PowerSquare * w * w + PowerCube * w * w * w;
        }

        public const double PowerBase = 11.37, PowerLinear = -4.818, PowerSquare = 0.7603, PowerCube = -0.00376;

        /// <summary>Fireball: base damage (× Power), multipliers (fire bites armor) and blast radius.</summary>
        public const double FireballFactor = 1;
        public const int FireballHealth = 5, FireballArmor = 8, FireballShield = 3;
        public const float FireballRadius = 1.5f;
        /// <summary>The burn after the blast: this share of the blast's base damage again, in <see cref="BurnTicks"/> ticks over <see cref="BurnSeconds"/>.</summary>
        public const double BurnShare = 0.5;
        public const int BurnTicks = 4;
        public const float BurnSeconds = 2f;

        /// <summary>Lightning: a big single hit (Tesla-like multipliers, shields most).</summary>
        public const double LightningFactor = 4;
        public const int LightningHealth = 6, LightningArmor = 3, LightningShield = 12;

        /// <summary>Ice spikes: three at once, each a hit and a slow.</summary>
        public const int IceSpikeCount = 3;
        public const double IceFactor = 1.2;
        public const int IceMult = 6;
        /// <summary>Slow added per spike (vanilla slow: decays by 5% a second, capped at 60%).</summary>
        public const float IceSlow = 0.25f;

        /// <summary>Tornado: drifts back up the path, dragging and grinding what it catches.</summary>
        public const float TornadoSeconds = 3.5f, TornadoSpeed = 1f, TornadoRadius = 0.65f;
        /// <summary>Damage per second to every monster it holds (× Power, all layers × <see cref="TornadoMult"/>).</summary>
        public const double TornadoFactor = 0.5;
        public const int TornadoMult = 5;

        /// <summary>Necromancy: the aura lasts this long (until a second before the next cast).</summary>
        public const float AuraSeconds = 4f;
        /// <summary>Minion health (= its blast) as a share of the slain monster's: the Necropolis's.</summary>
        public const double MinionShare = 0.2;

        /// <summary>Goldify: kill gold times this; the statue stands this long before it turns into coins.</summary>
        public const int GoldifyFactor = 25;
        public const float StatueSeconds = 1f;

        public static int Base(double factor, int wave) => Math.Max(1, (int)Math.Round(factor * Power(wave)));
        public static int FireballDamage(int wave) => Base(FireballFactor, wave);
        /// <summary>Base damage of one burn tick.</summary>
        public static int BurnTick(int wave) => Math.Max(1, (int)Math.Round(FireballFactor * BurnShare * Power(wave) / BurnTicks));
        public static int LightningDamage(int wave) => Base(LightningFactor, wave);
        public static int IceDamage(int wave) => Base(IceFactor, wave);
        /// <summary>Base damage of one tornado tick (4 a second).</summary>
        public static int TornadoTick(int wave) => Math.Max(1, (int)Math.Round(TornadoFactor * Power(wave) / 4));

        // ------------------------------------------------------------------ targeting

        public static bool InRange(Vector3 offset) => offset.x * offset.x + offset.z * offset.z <= Range * Range;

        /// <summary>Does the spell have something to cast at?</summary>
        public static bool HasTarget(WizardSpell s, IList<WizardTarget> inRange)
        {
            if (inRange.Count == 0) return false;
            if (s != WizardSpell.Tornado && s != WizardSpell.Goldify) return true;
            foreach (WizardTarget t in inRange) if (!t.Boss) return true;
            return false;
        }

        /// <summary>The better of two candidates by <paramref name="key"/> (by index); ties go to the one furthest along.</summary>
        private static int Better(IList<WizardTarget> l, int best, int i, Func<int, double> key)
        {
            if (best < 0) return i;
            double a = key(i), b = key(best);
            if (a > b + 1e-6) return i;
            if (a < b - 1e-6) return best;
            return l[i].Progress < l[best].Progress ? i : best;
        }

        /// <summary>Monsters within the blast radius of each one; the fireball goes to the largest group (ties: furthest along).</summary>
        public static int FireballTarget(IList<WizardTarget> l)
        {
            int best = -1;
            float r2 = FireballRadius * FireballRadius;
            var group = new double[l.Count];
            for (int i = 0; i < l.Count; i++)
                for (int j = 0; j < l.Count; j++)
                {
                    float dx = l[i].X - l[j].X, dz = l[i].Z - l[j].Z;
                    if (dx * dx + dz * dz <= r2) group[i]++;
                }
            for (int i = 0; i < l.Count; i++) best = Better(l, best, i, k => group[k]);
            return best;
        }

        /// <summary>The most shield; with no shields in range the most health (and armor); ties: furthest along.</summary>
        public static int LightningTarget(IList<WizardTarget> l)
        {
            bool shields = false;
            foreach (WizardTarget t in l) if (t.Shield > 0) shields = true;
            int best = -1;
            for (int i = 0; i < l.Count; i++)
                best = Better(l, best, i, k => shields ? l[k].Shield : Math.Max(0, l[k].Health) + l[k].Armor);
            return best;
        }

        /// <summary>
        /// <paramref name="count"/> targets: the fastest first (ties: furthest along), each a different monster while there
        /// are enough; with fewer monsters the spikes go round them again.
        /// </summary>
        public static List<int> IceTargets(IList<WizardTarget> l, int count)
        {
            var order = new List<int>();
            for (int i = 0; i < l.Count; i++) order.Add(i);
            order.Sort((a, b) =>
            {
                int c = l[b].Speed.CompareTo(l[a].Speed);
                return c != 0 ? c : l[a].Progress.CompareTo(l[b].Progress);
            });
            var result = new List<int>();
            for (int k = 0; k < count && order.Count > 0; k++) result.Add(order[k % order.Count]);
            return result;
        }

        /// <summary>The monster furthest along that isn't a boss (-1 if none).</summary>
        public static int TornadoTarget(IList<WizardTarget> l)
        {
            int best = -1;
            for (int i = 0; i < l.Count; i++)
                if (!l[i].Boss && (best < 0 || l[i].Progress < l[best].Progress)) best = i;
            return best;
        }

        /// <summary>The toughest monster that isn't a boss: shield + armor + health (ties: furthest along; -1 if none).</summary>
        public static int GoldifyTarget(IList<WizardTarget> l)
        {
            int best = -1;
            for (int i = 0; i < l.Count; i++)
                if (!l[i].Boss) best = Better(l, best, i, k => l[k].Total);
            return best;
        }

        // ------------------------------------------------------------------ the land and the mana link

        /// <summary>Is a point (offset from the section centre, world axes) on the wizard's land (the whole section)?</summary>
        public static bool OnLand(Vector3 offset) => Mathf.Abs(offset.x) < 3.5f && Mathf.Abs(offset.z) < 3.5f;

        /// <summary>Seconds between casts: <see cref="LeyInterval"/> while the ley link pays, else <see cref="Interval"/>.</summary>
        public static float CastInterval(bool leyPowered) => leyPowered ? LeyInterval : Interval;

        // ------------------------------------------------------------------ balance (fixed sources, docs/poi-plan.md §7)

        /// <summary>
        /// Share of his casts that land on something worth it: the tower stands wherever its section was placed, not
        /// where the player builds the defence (like the trap's arrival share).
        /// </summary>
        public const double Reach = 0.85;
        /// <summary>The fireball picks the largest group: this many more monsters than a blast at a random one.</summary>
        public const double GroupBonus = 1.3;

        private static TowerInfo Source(string name, double damage, int h, int a, int s, double castsShare, AttackShape shape, double area = 0) =>
            new TowerInfo
            {
                Kind = TowerKind.Poi, Name = name, Damage = (int)Math.Round(damage), HealthMult = h, ArmorMult = a, ShieldMult = s,
                Interval = Interval / Math.Max(1e-6, castsShare), Range = Range, Shape = shape, Area = area, Accuracy = Reach,
            };

        /// <summary>The fireball as a fixed source (the burn counted into its hit) at a power.</summary>
        public static TowerInfo FireballInfo(double power) =>
            Source("Wizard: Fireball", FireballFactor * (1 + BurnShare) * power, FireballHealth, FireballArmor, FireballShield,
                   Chance(WizardSpell.Fireball), AttackShape.Blast, FireballRadius);

        public static TowerInfo LightningInfo(double power) =>
            Source("Wizard: Lightning", LightningFactor * power, LightningHealth, LightningArmor, LightningShield, Chance(WizardSpell.Lightning), AttackShape.Single);

        /// <summary>Ice spikes: three single hits per cast; the slow as vanilla "percent per damage" for the evaluator.</summary>
        public static TowerInfo IceInfo(double power)
        {
            TowerInfo t = Source("Wizard: Ice Spikes", IceFactor * power, IceMult, IceMult, IceMult, Chance(WizardSpell.IceSpikes) * IceSpikeCount, AttackShape.Single);
            t.Slow = 100.0 * IceSlow / Math.Max(1, IceFactor * power * IceMult);
            return t;
        }

        /// <summary>The tornado's grinding: per cast a blast of its catch radius, the damage of its whole life.</summary>
        public static TowerInfo TornadoInfo(double power) =>
            Source("Wizard: Tornado", TornadoFactor * power * TornadoSeconds, TornadoMult, TornadoMult, TornadoMult, Chance(WizardSpell.Tornado),
                   AttackShape.Blast, TornadoRadius * 2);

        /// <summary>Seconds of a wave with monsters in his range (the evaluator's active time for a single target).</summary>
        public static double ActiveSeconds(Board b)
        {
            WaveStats w = WaveModel.Get(b.Wave);
            double v = Math.Max(0.2, w.Speed);
            double c = PathStats.Coverage(Range, b.Placement, b.PathLength);
            return (Math.Max(1, w.StreamSeconds) + c / v) * Reach;
        }

        public static double CastsPerWave(Board b) => ActiveSeconds(b) / Interval;

        /// <summary>
        /// The tornado as a slow over the whole wave: each cast catches the monsters it meets while drifting back (it and
        /// they close in at 1 + v cells a second) and holds each for half its life on average, carrying them back too.
        /// </summary>
        public static double TornadoSlow(Board b)
        {
            WaveStats w = WaveModel.Get(b.Wave);
            double v = Math.Max(0.2, w.Speed);
            double perCell = w.Count / Math.Max(1, w.StreamSeconds) / Math.Max(1, b.Lanes) / v * (1 - w.ImmuneSlow);
            double caught = perCell * (1 + v) * TornadoSeconds * 0.6; // some are bosses, some were already dying
            double held = TornadoSeconds / 2;
            double delay = held + held * TornadoSpeed / v;
            double extra = CastsPerWave(b) * Chance(WizardSpell.Tornado) * caught * delay;
            double zone = PathStats.Coverage(Range, b.Placement, b.PathLength);
            double baseTime = w.Count / Math.Max(1, b.Lanes) * zone / v;
            return extra / (extra + Math.Max(1e-6, baseTime)) * 0.5; // only inside his reach and the zone near it
        }

        /// <summary>
        /// Share of the wave's hit points the risen minions take: monsters slain in his range while the aura is up give a
        /// minion worth a fifth of their own.
        /// </summary>
        public static double NecromancyShare(Board b)
        {
            double uptime = Math.Min(1, CastsPerWave(b) * Chance(WizardSpell.Necromancy) * AuraSeconds / Math.Max(1, ActiveSeconds(b) / Reach));
            double c = PathStats.Coverage(Range, b.Placement, b.PathLength);
            double inRange = Math.Min(1, c / Math.Max(1, b.PathLength)) * 2; // kills crowd round the defence, often near him
            return MinionShare * uptime * Math.Min(1, inRange) * Reach;
        }

        /// <summary>Goldify: the casts of a wave that turn a monster to gold.</summary>
        public static double GoldifiesPerWave(Board b) => CastsPerWave(b) * Chance(WizardSpell.Goldify);

        /// <summary>Kill gold of one monster at a wave: its own gold plus one per tower type that hit it (about four).</summary>
        public static double KillGold(int wave)
        {
            WaveStats w = WaveModel.Get(Math.Max(1, Math.Min(WaveModel.LastWave, wave)));
            return w.KillGold / Math.Max(1, w.Count) + 4;
        }
    }

    /// <summary>
    /// The Wizard's Tower section: a generic section (paths from every exit to the centre, random hills); the tower stands
    /// on a random grass cell that is neither on the section's edge nor next to a path (8 neighbours), chosen after the
    /// paths are laid (height 1). If no cell qualifies, one off the edge that only touches a path diagonally, then any
    /// inner grass cell.
    /// </summary>
    public sealed class WizardTowerStyle : SectionStyle
    {
        public override void Shape(SectionLayout l, IRng rng)
        {
            List<(int x, int z)> free = Candidates(l, 8);
            if (free.Count == 0) free = Candidates(l, 4);
            if (free.Count == 0) free = Candidates(l, 0);
            if (free.Count == 0) return;
            var (cx, cz) = free[rng.Range(0, free.Count)];
            l.Cells[cx, cz] = SectionCell.Reserved;
        }

        /// <summary>Inner grass cells with no path among their <paramref name="neighbours"/> (8, 4 or 0) neighbours.</summary>
        public static List<(int x, int z)> Candidates(SectionLayout l, int neighbours)
        {
            var free = new List<(int x, int z)>();
            for (int x = 1; x < SectionLayout.Size - 1; x++)
                for (int z = 1; z < SectionLayout.Size - 1; z++)
                {
                    if (!l.IsGrass(x, z)) continue;
                    bool near = false;
                    for (int dx = -1; dx <= 1 && !near; dx++)
                        for (int dz = -1; dz <= 1 && !near; dz++)
                        {
                            if (dx == 0 && dz == 0) continue;
                            bool diagonal = dx != 0 && dz != 0;
                            if (neighbours == 0 || neighbours == 4 && diagonal) continue;
                            if (l.IsPath(x + dx, z + dz)) near = true;
                        }
                    if (!near) free.Add((x, z));
                }
            return free;
        }

        /// <summary>The tower's cell in a generated layout (its one reserved cell).</summary>
        public static (int x, int z) CellOf(SectionLayout l)
        {
            for (int x = 0; x < SectionLayout.Size; x++)
                for (int z = 0; z < SectionLayout.Size; z++)
                    if (l.Cells[x, z] == SectionCell.Reserved) return (x, z);
            return (1, 1);
        }
    }
}

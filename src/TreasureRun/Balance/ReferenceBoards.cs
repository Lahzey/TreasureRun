using System;
using System.Collections.Generic;

namespace TreasureRun.Balance
{
    /// <summary>
    /// The authored reference boards (docs/balance-framework.md sections 1.1 and 9): what a typical Treasure Run board
    /// holds at each wave, per variant. Investment follows from the wave model's gold; composition, levels, heights and
    /// buildings are authored at a few checkpoints and interpolated. Replaced by recorded runs in M9.
    /// </summary>
    public static class ReferenceBoards
    {
        public const double StartGold = 1000;
        /// <summary>Share of income spent on towers (the rest is levels, buildings, savings).</summary>
        public const double SpendShare = 0.85;
        /// <summary>Tree chops per wave (a chop pays wave² gold).</summary>
        public const double ChopsPerWave = 0.5;
        /// <summary>Different tower types that hit a monster on average (vanilla adds one gold each).</summary>
        public const double TypesPerKill = 2;
        /// <summary>Share of their mana demand the board's mana buildings cover.</summary>
        public const double ManaCoverage = 0.9;

        private static readonly Dictionary<BoardVariant, (int Wave, (TowerKind Kind, double Share)[] Mix)[]> mixes =
            new Dictionary<BoardVariant, (int, (TowerKind, double)[])[]>
            {
                [BoardVariant.Mixed] = new[]
                {
                    (1, M((TowerKind.Crossbow, 1))),
                    (10, M((TowerKind.Crossbow, .55), (TowerKind.Morter, .3), (TowerKind.TeslaCoil, .15))),
                    (20, M((TowerKind.Crossbow, .25), (TowerKind.Morter, .25), (TowerKind.TeslaCoil, .1), (TowerKind.FlameThrower, .15),
                           (TowerKind.Frost, .1), (TowerKind.Obelisk, .15))),
                    (30, M((TowerKind.Crossbow, .12), (TowerKind.Morter, .18), (TowerKind.TeslaCoil, .08), (TowerKind.FlameThrower, .1),
                           (TowerKind.PoisonSprayer, .1), (TowerKind.Frost, .07), (TowerKind.Obelisk, .2), (TowerKind.Cannon, .1), (TowerKind.Lookout, .05))),
                    (40, M((TowerKind.Crossbow, .07), (TowerKind.Morter, .14), (TowerKind.TeslaCoil, .05), (TowerKind.FlameThrower, .08),
                           (TowerKind.PoisonSprayer, .08), (TowerKind.Frost, .05), (TowerKind.Obelisk, .2), (TowerKind.Cannon, .1),
                           (TowerKind.ParticleCannon, .15), (TowerKind.Shredder, .05), (TowerKind.Lookout, .03))),
                },
                [BoardVariant.SingleTarget] = new[]
                {
                    (1, M((TowerKind.Crossbow, 1))),
                    (10, M((TowerKind.Crossbow, .85), (TowerKind.Morter, .15))),
                    (20, M((TowerKind.Crossbow, .5), (TowerKind.Obelisk, .4), (TowerKind.Morter, .1))),
                    (30, M((TowerKind.Crossbow, .27), (TowerKind.Obelisk, .4), (TowerKind.ParticleCannon, .2), (TowerKind.Morter, .08), (TowerKind.Lookout, .05))),
                    (40, M((TowerKind.Crossbow, .17), (TowerKind.Obelisk, .4), (TowerKind.ParticleCannon, .3), (TowerKind.Morter, .08), (TowerKind.Lookout, .05))),
                },
                [BoardVariant.Area] = new[]
                {
                    (1, M((TowerKind.Crossbow, 1))),
                    (10, M((TowerKind.Crossbow, .35), (TowerKind.Morter, .45), (TowerKind.TeslaCoil, .2))),
                    (20, M((TowerKind.Crossbow, .15), (TowerKind.Morter, .4), (TowerKind.TeslaCoil, .2), (TowerKind.FlameThrower, .15), (TowerKind.Encampment, .1))),
                    (30, M((TowerKind.Crossbow, .05), (TowerKind.Morter, .4), (TowerKind.TeslaCoil, .15), (TowerKind.FlameThrower, .15),
                           (TowerKind.Cannon, .15), (TowerKind.Encampment, .1))),
                    (40, M((TowerKind.Morter, .35), (TowerKind.TeslaCoil, .1), (TowerKind.FlameThrower, .15), (TowerKind.Cannon, .2),
                           (TowerKind.Encampment, .1), (TowerKind.VampireLair, .1))),
                },
                [BoardVariant.DamageOverTime] = new[]
                {
                    (1, M((TowerKind.Crossbow, 1))),
                    (10, M((TowerKind.Crossbow, .5), (TowerKind.FlameThrower, .3), (TowerKind.Morter, .2))),
                    (20, M((TowerKind.Crossbow, .2), (TowerKind.FlameThrower, .3), (TowerKind.PoisonSprayer, .25), (TowerKind.Shredder, .15), (TowerKind.Morter, .1))),
                    (30, M((TowerKind.Crossbow, .1), (TowerKind.FlameThrower, .25), (TowerKind.PoisonSprayer, .25), (TowerKind.Shredder, .2),
                           (TowerKind.VampireLair, .1), (TowerKind.Monument, .1))),
                    (40, M((TowerKind.Crossbow, .05), (TowerKind.FlameThrower, .25), (TowerKind.PoisonSprayer, .25), (TowerKind.Shredder, .2),
                           (TowerKind.VampireLair, .1), (TowerKind.Monument, .15))),
                },
                [BoardVariant.Mana] = new[]
                {
                    (1, M((TowerKind.Crossbow, 1))),
                    (10, M((TowerKind.Crossbow, .5), (TowerKind.TeslaCoil, .5))),
                    (20, M((TowerKind.Obelisk, .4), (TowerKind.TeslaCoil, .3), (TowerKind.Crossbow, .2), (TowerKind.Frost, .1))),
                    (30, M((TowerKind.Obelisk, .4), (TowerKind.ParticleCannon, .25), (TowerKind.TeslaCoil, .2), (TowerKind.Frost, .15))),
                    (40, M((TowerKind.Obelisk, .4), (TowerKind.ParticleCannon, .35), (TowerKind.TeslaCoil, .15), (TowerKind.Frost, .1))),
                },
                [BoardVariant.CannonLine] = new[]
                {
                    (1, M((TowerKind.Crossbow, 1))),
                    (10, M((TowerKind.Crossbow, .4), (TowerKind.Cannon, .4), (TowerKind.Morter, .2))),
                    (20, M((TowerKind.Cannon, .5), (TowerKind.Crossbow, .2), (TowerKind.Morter, .3))),
                    (30, M((TowerKind.Cannon, .55), (TowerKind.Morter, .25), (TowerKind.Obelisk, .2))),
                    (40, M((TowerKind.Cannon, .55), (TowerKind.Morter, .25), (TowerKind.Obelisk, .2))),
                },
                [BoardVariant.Tall] = new[]
                {
                    (1, M((TowerKind.Crossbow, 1))),
                    (10, M((TowerKind.Crossbow, 1))),
                    (20, M((TowerKind.Crossbow, .5), (TowerKind.Obelisk, .5))),
                    (30, M((TowerKind.Obelisk, .5), (TowerKind.ParticleCannon, .5))),
                    (40, M((TowerKind.Obelisk, .5), (TowerKind.ParticleCannon, .5))),
                },
            };

        private static (TowerKind, double)[] M(params (TowerKind, double)[] m) => m;

        private static readonly Dictionary<(int, BoardVariant), Board> cache = new Dictionary<(int, BoardVariant), Board>();

        /// <summary>A fresh copy of the reference board at a wave (1..45).</summary>
        public static Board Get(int wave, BoardVariant variant = BoardVariant.Mixed)
        {
            wave = Math.Max(1, Math.Min(WaveModel.LastWave, wave));
            Board b;
            lock (cache)
                if (!cache.TryGetValue((wave, variant), out b)) cache[(wave, variant)] = b = Build(wave, variant);
            return b.Clone();
        }

        /// <summary>Gold a typical run earns during wave <paramref name="wave"/>.</summary>
        public static double Income(int wave)
        {
            WaveStats s = WaveModel.Get(wave);
            return s.KillGold + s.Count * TypesPerKill + ChopsPerWave * wave * wave;
        }

        /// <summary>Total gold earned before wave <paramref name="wave"/> starts, start gold included.</summary>
        public static double GoldBefore(int wave)
        {
            double g = StartGold;
            for (int j = 1; j < wave; j++) g += Income(j);
            return g;
        }

        /// <summary>
        /// The wave a tower typically appears: the first wave any variant board holds at least one, never before 5
        /// (an early tower is built from wave 5 at the earliest, when its unlock card has come up).
        /// </summary>
        public static int FirstWave(TowerKind kind)
        {
            if (kind == TowerKind.Crossbow) return 1;
            for (int w = 5; w <= WaveModel.LastWave; w++)
                foreach (BoardVariant v in Enum.GetValues(typeof(BoardVariant)))
                    if ((Get(w, v).Group(kind)?.Count ?? 0) >= 1) return w;
            return 25;
        }

        public static double Level(int wave) => 1 + 0.45 * (wave - 1);
        public static double HeightBonus(int wave) => 1 + wave / 20.0;
        public static double CardsOwned(int wave) => wave / 3.0;
        public static double TreasuresOwned(int wave) => wave / 4.0;

        private static Board Build(int wave, BoardVariant variant)
        {
            var b = new Board
            {
                Wave = wave, Variant = variant, Lanes = 1, Placement = 0.5,
                Invest = GoldBefore(wave) * SpendShare,
                Income = Income(wave),
                PathLength = PathStats.PathLength(wave),
                CardsOwned = CardsOwned(wave), TreasuresOwned = TreasuresOwned(wave),
            };
            b.Bank = 50 + 0.15 * b.Income;
            double mana = variant == BoardVariant.Mana ? 2.5 : 1;
            b.Siphons = wave < 8 ? 0 : (1 + (wave - 8) / 6.0) * mana;
            b.Banks = wave < 20 ? 0 : (1 + (wave - 20) / 12.0) * mana;
            b.Universities = wave < 18 ? 0 : 1 + (wave - 18) / 14.0;
            double buildings = b.Siphons * Buildings.ManaSiphonPrice + b.Banks * Buildings.ManaBankPrice + b.Universities * Buildings.UniversityPrice;
            double towers = Math.Max(b.Invest - buildings, b.Invest * 0.6);

            // The cards a typical run holds: modest generic bonuses (absorbed into the board's normalisation anyway).
            b.All.BaseDamage = wave / 10.0;
            b.All.Crit = Math.Min(0.25, 0.005 * wave);
            b.Mods.MaxMana = 100 + b.Banks * Buildings.BankMaxMana;
            b.Mods.ManaPerSecond = (b.Siphons + b.Banks) * Buildings.ManaPerBuilding;

            double level = Level(wave), height = HeightBonus(wave);
            if (variant == BoardVariant.Tall) level *= 1.6;
            foreach ((TowerKind kind, double share) in Mix(wave, variant))
            {
                var g = new TowerGroup { Kind = kind, Level = level, Height = height };
                g.Count = b.Copies(kind, 0, towers * share);
                b.Groups.Add(g);
            }
            if (variant == BoardVariant.Tall)
            {
                double cap = Math.Min(9, 4 + wave / 8.0), n = b.TowerCount;
                if (n > cap) foreach (TowerGroup g in b.Groups) g.Count *= cap / n;
            }

            // A typical player feeds the mana towers they build: mana buildings cover most of the demand, paid from the
            // tower gold at the market price (Valuation.ManaPrice). Two rounds: fewer towers need a little less mana.
            for (int round = 0; round < 2; round++)
            {
                Headroom h = Evaluator.Raw(b);
                double need = ManaCoverage * h.ManaDemand / Math.Max(1, h.WaveTime) - b.Mods.ManaRegen * b.Mods.MaxMana;
                double extra = need - b.Mods.ManaPerSecond;
                if (extra <= 0.01) break;
                b.Siphons += extra;
                b.Mods.ManaPerSecond = need;
                double cost = extra * Valuation.ManaPrice;
                double gold = 0;
                foreach (TowerGroup g in b.Groups) gold += b.GroupCost(g);
                foreach (TowerGroup g in b.Groups) g.Count *= Math.Max(0.3, 1 - cost / Math.Max(1, gold));
            }
            return b;
        }

        /// <summary>The variant's tower mix at a wave, interpolated between its checkpoints.</summary>
        public static List<(TowerKind Kind, double Share)> Mix(int wave, BoardVariant variant)
        {
            var points = mixes[variant];
            int i = 0;
            while (i < points.Length - 1 && points[i + 1].Wave <= wave) i++;
            var a = points[i];
            var bpt = i + 1 < points.Length ? points[i + 1] : points[i];
            double t = bpt.Wave == a.Wave ? 0 : (wave - a.Wave) / (double)(bpt.Wave - a.Wave);
            var shares = new Dictionary<TowerKind, double>();
            foreach (var (k, s) in a.Mix) shares[k] = (shares.TryGetValue(k, out double v) ? v : 0) + s * (1 - t);
            foreach (var (k, s) in bpt.Mix) shares[k] = (shares.TryGetValue(k, out double v) ? v : 0) + s * t;
            var list = new List<(TowerKind, double)>();
            foreach (TowerKind k in Enum.GetValues(typeof(TowerKind)))
                if (shares.TryGetValue(k, out double s) && s > 1e-6) list.Add((k, s));
            return list;
        }
    }

    /// <summary>
    /// Map path statistics (section 4.2): how much path a tower covers. Defaults are estimates; the dev command
    /// <c>balance pathstats</c> measures them on real maps (docs/balance-framework.md, step B0).
    /// </summary>
    public static class PathStats
    {
        /// <summary>Path cells from the spawn to the castle at a wave (the map grows by one tile per wave).</summary>
        public static double PathLength(int wave) => 15 + 7.5 * wave;

        /// <summary>
        /// Measured path length within range R of the cells next to the path (scenario balance-area, a wave-4 map with
        /// 46 path cells): R, then the 10th, 50th and 90th percentile over the cells. Re-measure on bigger maps in M8.
        /// </summary>
        private static readonly double[,] Measured =
        {
            { 0, 0, 0, 0 },
            { 2, 1.5, 3.3, 5.5 },
            { 3, 4.8, 6.3, 11.0 },
            { 4, 7.8, 11.5, 16.3 },
            { 5, 10.3, 15.3, 20.8 },
            { 7, 14.8, 24.3, 30.3 },
            { 10, 21.8, 32.5, 39.3 },
        };

        /// <summary>Path length within <paramref name="range"/> of a tower at placement percentile <paramref name="placement"/>.</summary>
        public static double Coverage(double range, double placement, double pathLength)
        {
            int col = 0;
            double t;
            if (placement <= 0.5) { col = 1; t = Math.Max(0, (placement - 0.1) / 0.4); }
            else { col = 2; t = Math.Min(1, (placement - 0.5) / 0.4); }
            double At(int c)
            {
                int n = Measured.GetLength(0);
                if (range >= Measured[n - 1, 0])
                {
                    // beyond the table: the last slope
                    double slope = (Measured[n - 1, c] - Measured[n - 2, c]) / (Measured[n - 1, 0] - Measured[n - 2, 0]);
                    return Measured[n - 1, c] + slope * (range - Measured[n - 1, 0]);
                }
                int k = 1;
                while (Measured[k, 0] < range) k++;
                double f = (range - Measured[k - 1, 0]) / (Measured[k, 0] - Measured[k - 1, 0]);
                return Measured[k - 1, c] + f * (Measured[k, c] - Measured[k - 1, c]);
            }
            double v = At(col) + t * (At(col + 1) - At(col));
            return Math.Min(pathLength, Math.Max(0.5, v));
        }

        /// <summary>Path cells in the 5x5 square a Frost Keep or Encampment drops on.</summary>
        public static double DropCells(double placement) => Coverage(2.8, placement, 1e9);

    }
}

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TreasureRun.Ascensions.Phase3;
using UnityEngine;

namespace TreasureRun.Tests
{
    /// <summary>Phase 3 ascensions (docs/ascension-plan.md §2.3, expected outcomes).</summary>
    public class Phase3Tests
    {
        // ------------------------------------------------------------------ The Saw Dimension

        /// <summary>Base damage per second the saws deal to one monster for the given Shredder damages.</summary>
        private static float SawBasePerSecond(params int[] shredderDamage)
        {
            float carry = 0f;
            int total = 0;
            const int seconds = 100;
            for (int tick = 0; tick < SawDimensionLogic.TicksPerSecond * seconds; tick++)
                total += SawDimensionLogic.Take(ref carry, SawDimensionLogic.BaseDamagePerTick(shredderDamage.Sum()));
            return total / (float)seconds;
        }

        [Test]
        public void SawDamageIsTheSumOfShredderDamageOverTen()
        {
            Assert.That(SawBasePerSecond(), Is.EqualTo(0f), "no Shredders, no damage");
            Assert.That(SawBasePerSecond(10), Is.EqualTo(1f).Within(0.01f));
            Assert.That(SawBasePerSecond(10, 14, 23), Is.EqualTo(4.7f).Within(0.02f));
            Assert.That(SawBasePerSecond(Enumerable.Repeat(30, 20).ToArray()), Is.EqualTo(60f).Within(0.01f));
            // Small totals still land (carried fractions), never as a minimum-1 hit every tick.
            Assert.That(SawDimensionLogic.BaseDamagePerTick(10), Is.EqualTo(0.2f).Within(1e-6));
        }

        [Test]
        public void SawHitsAtMostFiveTimesASecondAndSpreadsTheRemainder()
        {
            // Σ damage 60 → 6 base damage per second → 1, 1, 1, 1, 2 every second.
            float carry = 0f;
            var perTick = new List<int>();
            for (int tick = 0; tick < 5 * 3; tick++) perTick.Add(SawDimensionLogic.Take(ref carry, SawDimensionLogic.BaseDamagePerTick(60)));
            Assert.That(perTick, Is.EqualTo(new[] { 1, 1, 1, 1, 2, 1, 1, 1, 1, 2, 1, 1, 1, 1, 2 }));

            // Σ damage 10 → 1 per second: a single hit of 1 per second, not one per tick.
            carry = 0f;
            int hits = 0, total = 0;
            for (int tick = 0; tick < 5 * 10; tick++)
            {
                int d = SawDimensionLogic.Take(ref carry, SawDimensionLogic.BaseDamagePerTick(10));
                if (d > 0) hits++;
                total += d;
            }
            Assert.That(hits, Is.EqualTo(10));
            Assert.That(total, Is.EqualTo(10));

            // Σ damage 500 → 50 per second: 5 hits of 10 (more engines = bigger hits, never more hits).
            carry = 0f;
            for (int tick = 0; tick < 5; tick++) Assert.That(SawDimensionLogic.Take(ref carry, SawDimensionLogic.BaseDamagePerTick(500)), Is.EqualTo(10));
        }

        [Test]
        public void SawXpSplitAddsUpToTheTotal()
        {
            Vector3 s = SawDimensionLogic.LayerShares(2, 3, 5);
            Assert.That(s.x + s.y + s.z, Is.EqualTo(1f).Within(1e-5));
            Assert.That(s.x, Is.EqualTo(0.2f).Within(1e-5));
            Assert.That(s.z, Is.EqualTo(0.5f).Within(1e-5));
            Assert.That(SawDimensionLogic.LayerShares(0, 0, 0), Is.EqualTo(Vector3.zero));
            // Each Shredder gains as if it always had a target: vanilla rate, split over the layers.
            float rate = SawDimensionLogic.XpPerSecond(5f);
            Assert.That(rate, Is.EqualTo(0.6f).Within(1e-5));
            Vector3 xp = s * rate;
            Assert.That(xp.x + xp.y + xp.z, Is.EqualTo(rate).Within(1e-5));
            Assert.That(SawDimensionLogic.Average(new List<int> { 20, 21, 25 }), Is.EqualTo(22));
        }

        [Test]
        public void SawsAreSpreadEvenlyAlongThePath()
        {
            // An L-shaped path: 6 units east, then 4 units north (sharing the corner).
            var segments = new List<(Vector3, Vector3)>
            {
                (new Vector3(0, 0, 0), new Vector3(6, 0, 0)),
                (new Vector3(6, 0, 0), new Vector3(6, 0, 4)),
            };
            List<SawSpot> spots = SawDimensionLogic.Layout(segments);
            Assert.That(spots.Count, Is.EqualTo(Mathf.RoundToInt(6f / 0.9f) + Mathf.RoundToInt(4f / 0.9f)));
            for (int i = 0; i < spots.Count; i++)
                for (int j = i + 1; j < spots.Count; j++)
                    Assert.That((spots[i].Position - spots[j].Position).magnitude, Is.GreaterThan(SawDimensionLogic.SawSpacing * 0.5f));
            Assert.That(spots.First().Direction, Is.EqualTo(Vector3.right));
            Assert.That(spots.Last().Direction, Is.EqualTo(Vector3.forward));
            // A segment going back over the same stretch (split paths re-joining) adds no duplicates.
            segments.Add((new Vector3(6, 0, 0), new Vector3(0, 0, 0)));
            Assert.That(SawDimensionLogic.Layout(segments).Count, Is.EqualTo(spots.Count));
        }

        [Test]
        public void SawsRetractWithoutShredders()
        {
            float e = 1f;
            for (int i = 0; i < 60; i++) e = SawDimensionLogic.Extension(e, false, 1f / 60f);
            Assert.That(e, Is.EqualTo(0f));
            for (int i = 0; i < 20; i++) e = SawDimensionLogic.Extension(e, true, 1f / 60f);
            Assert.That(e, Is.GreaterThan(0f).And.LessThan(1f));
        }

        // ------------------------------------------------------------------ Mega Mortar

        private static List<MortarSquare> Singles(params (int x, int z)[] cells) => cells.Select(c => new MortarSquare(c.x, c.z, 1)).ToList();

        private static List<MortarSquare> Block(int x0, int z0, int size)
        {
            var cells = new List<MortarSquare>();
            for (int x = 0; x < size; x++)
                for (int z = 0; z < size; z++)
                    cells.Add(new MortarSquare(x0 + x, z0 + z, 1));
            return cells;
        }

        [Test]
        public void MegaMortarFusesTheLargestSquares()
        {
            // An L of 5 modules: one 2x2, the fifth stays loose.
            List<MortarSquare> l = MegaMortarLogic.Resolve(Singles((0, 0), (1, 0), (0, 1), (1, 1), (0, 2)));
            Assert.That(l.Count, Is.EqualTo(2));
            Assert.That(l.Single(u => u.Size == 2).Contains(0, 2), Is.False);

            // 3x3 beats the 2x2s inside it; a 2x3 rectangle gives one 2x2 (lowest first) and two loose modules.
            Assert.That(MegaMortarLogic.Resolve(Block(4, 7, 3)).Single(), Is.EqualTo(new MortarSquare(4, 7, 3)));
            List<MortarSquare> rect = MegaMortarLogic.Resolve(Block(0, 0, 2).Concat(Singles((0, 2), (1, 2))));
            Assert.That(rect.Single(u => u.Size == 2), Is.EqualTo(new MortarSquare(0, 0, 2)));
            Assert.That(rect.Count, Is.EqualTo(3));

            // A line or a diagonal fuses nothing.
            Assert.That(MegaMortarLogic.Resolve(Singles((0, 0), (1, 0), (2, 0), (3, 0))).Count, Is.EqualTo(4));
            Assert.That(MegaMortarLogic.Resolve(Singles((0, 0), (1, 1), (2, 2))).Count, Is.EqualTo(3));
        }

        [Test]
        public void MegaMortarOnlyGrowsAndCannotBeTakenApart()
        {
            // A 2x2 Mega Mortar plus 5 modules around it becomes a 3x3.
            var mega = new MortarSquare(0, 0, 2);
            var grow = new List<MortarSquare> { mega };
            grow.AddRange(Singles((2, 0), (2, 1), (2, 2), (0, 2), (1, 2)));
            var contained = new List<int>();
            Assert.That(MegaMortarLogic.NextFusion(grow, contained), Is.EqualTo(new MortarSquare(0, 0, 3)));
            Assert.That(contained.Count, Is.EqualTo(6));

            // Modules next to a Mega Mortar can't borrow its cells for a square of their own: the 2x2 at (1,1) would
            // take one cell of the mega, so nothing fuses.
            var steal = new List<MortarSquare> { mega };
            steal.AddRange(Singles((2, 1), (1, 2), (2, 2)));
            Assert.That(MegaMortarLogic.NextFusion(steal, contained), Is.Null);

            // A 3x3 next to the mega that would cut through it doesn't form either; separate squares beside it do.
            var beside = new List<MortarSquare> { mega };
            beside.AddRange(Block(2, 0, 2));
            List<MortarSquare> after = MegaMortarLogic.Resolve(beside);
            Assert.That(after, Is.EquivalentTo(new[] { mega, new MortarSquare(2, 0, 2) }));

            // Two 2x2 megas side by side never fuse into a rectangle; with a full 4x4 of megas they become one 4x4.
            var four = new List<MortarSquare> { new MortarSquare(0, 0, 2), new MortarSquare(2, 0, 2), new MortarSquare(0, 2, 2), new MortarSquare(2, 2, 2) };
            Assert.That(MegaMortarLogic.Resolve(four).Single(), Is.EqualTo(new MortarSquare(0, 0, 4)));
        }

        [Test]
        public void MegaMortarAveragesLevelsByModuleCount()
        {
            // A level-5 2x2 (weight 4) with 5 fresh modules: (4 x 5 + 5 x 1) / 9 = 2.8 -> level 3.
            var parts = new List<MortarProgress> { new MortarProgress { Weight = 4, Level = 5, DamageUp = 4, HealthUp = 4, HealthXp = 9f } };
            for (int i = 0; i < 5; i++) parts.Add(new MortarProgress { Weight = 1, Level = 1 });
            MortarProgress p = MegaMortarLogic.Average(parts);
            Assert.That(p.Weight, Is.EqualTo(9));
            Assert.That(p.Level, Is.EqualTo(3));
            Assert.That(p.DamageUp, Is.EqualTo(2)); // 16 / 9 = 1.8
            Assert.That(p.HealthUp, Is.EqualTo(2));
            Assert.That(p.HealthXp, Is.EqualTo(4f).Within(1e-4));

            // Stats: damage x modules, blast x side, +2 range per side above 1.
            Assert.That(MegaMortarLogic.DamageFactor(3), Is.EqualTo(9));
            Assert.That(MegaMortarLogic.BlastFactor(2), Is.EqualTo(2f));
            Assert.That(MegaMortarLogic.RangeBonus(1), Is.EqualTo(0f));
            Assert.That(MegaMortarLogic.RangeBonus(2), Is.EqualTo(2f));
            Assert.That(MegaMortarLogic.RangeBonus(4), Is.EqualTo(6f));

            // Vents: a 3x3 has exactly its 4 corners (steam shoots up from each).
            Assert.That(MegaMortarLogic.VentTiles(3), Is.EquivalentTo(new[] { (0, 0), (2, 0), (0, 2), (2, 2) }));
        }

        [Test]
        public void MegaMortarSellsLikeItsModulesOneByOne()
        {
            // Mortar 200 + 75 per mortar owned. 6 owned, selling a 2x2: 4 sales at 575, 500, 425, 350.
            Assert.That(MegaMortarLogic.SellValue(200, 75, 6, 4), Is.EqualTo(575 + 500 + 425 + 350));
            Assert.That(MegaMortarLogic.SellValue(200, 75, 1, 1), Is.EqualTo(200));
        }
    }
}

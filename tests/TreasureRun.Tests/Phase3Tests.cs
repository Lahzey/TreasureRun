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
    }
}

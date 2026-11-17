using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TreasureRun.Ascensions.Phase3;
using TreasureRun.Core;
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

        // ------------------------------------------------------------------ Ley Line Network

        private static List<LeyCell> Cells(params (int x, int z)[] cells) => cells.Select(c => new LeyCell(c.x, c.z, 1)).ToList();

        [Test]
        public void LeyNetworksFloodFillAndPayTapsPerCrystal()
        {
            // 5 siphons in a row touching 4 crystals: taps are siphons 0, 2 and 4; each gathers 1 + 3 x 1/2 = 2.5 -> 2.
            var siphons = Cells((0, 0), (1, 0), (2, 0), (3, 0), (4, 0));
            var crystals = Cells((-1, 0), (0, 1), (2, -1), (5, 0));
            LeyLineLogic.Result r = LeyLineLogic.Solve(siphons, crystals);
            Assert.That(r.Networks.Count, Is.EqualTo(1));
            Assert.That(r.IsTap, Is.EqualTo(new[] { true, false, true, false, true }));
            Assert.That(r.Income, Is.EqualTo(new[] { 2, 0, 2, 0, 2 }), "connectors gather nothing");
            Assert.That(r.Networks[0].Income, Is.EqualTo(6));

            // Per tap: 1 crystal 1, 2 -> 1.5 -> 1, 3 -> 2, 4 -> 2.5 -> 2, 5 -> 3, 9 -> 5. The rate (cards) multiplies first.
            Assert.That(new[] { 0, 1, 2, 3, 4, 5, 9 }.Select(c => LeyLineLogic.TapIncome(c)), Is.EqualTo(new[] { 0, 1, 1, 2, 2, 3, 5 }));
            Assert.That(LeyLineLogic.TapIncome(4, 2), Is.EqualTo(5));

            // A crystal next to two siphons of the same network counts once (both are taps); a diagonal doesn't connect.
            r = LeyLineLogic.Solve(Cells((0, 0), (1, 0), (2, 1)), Cells((0, 1)));
            Assert.That(r.Networks.Count, Is.EqualTo(2));
            Assert.That(r.Income, Is.EqualTo(new[] { 1, 0, 0 }));
            r = LeyLineLogic.Solve(Cells((0, 0), (1, 0), (2, 0)), Cells((0, 1), (1, 1), (2, 1)));
            Assert.That(r.Income, Is.EqualTo(new[] { 2, 2, 2 }));

            // Different heights link too (siphons and crystals), unlike vanilla.
            var mixed = new List<LeyCell> { new LeyCell(0, 0, 1), new LeyCell(1, 0, 2) };
            r = LeyLineLogic.Solve(mixed, new List<LeyCell> { new LeyCell(2, 0, 4) });
            Assert.That(r.Networks.Count, Is.EqualTo(1));
            Assert.That(r.Income, Is.EqualTo(new[] { 0, 1 }));

            // Two separate networks share a crystal: it counts for both. No crystal: no income.
            r = LeyLineLogic.Solve(Cells((0, 0), (2, 0), (9, 9)), Cells((1, 0)));
            Assert.That(r.Income, Is.EqualTo(new[] { 1, 1, 0 }));
        }

        [Test]
        public void LeyPipesMeetAcrossHeights()
        {
            var self = new LeyCell(0, 0, 3);
            var none = new LeyNeighbour(LeyNeighbourKind.None, 0);
            // Neighbours one and two steps up, one level with it, and a crystal one step down.
            LeyPlan plan = LeyLineLogic.Plan(self, new[]
            {
                new LeyNeighbour(LeyNeighbourKind.Siphon, 4), new LeyNeighbour(LeyNeighbourKind.Siphon, 5),
                new LeyNeighbour(LeyNeighbourKind.Siphon, 3), new LeyNeighbour(LeyNeighbourKind.Crystal, 2),
            });
            Assert.That(plan.Arms.Select(a => a.Rise), Is.EqualTo(new[] { 1, 2, 0, 0 }));
            Assert.That(plan.Arms[3].Drop, Is.EqualTo(1));
            Assert.That(plan.Tees, Is.EqualTo(new[] { 1, 2 }), "one tee per level the riser passes");
            Assert.That(plan.Riser, Is.EqualTo(2), "the riser reaches the highest neighbour");

            // Two higher neighbours on the same level share one tee; a node above all its neighbours has no riser
            // (they carry the pipes up to it), and a higher crystal is reached on the riser like a siphon.
            plan = LeyLineLogic.Plan(self, new[] { new LeyNeighbour(LeyNeighbourKind.Siphon, 5), new LeyNeighbour(LeyNeighbourKind.Crystal, 5), none, none });
            Assert.That(plan.Tees, Is.EqualTo(new[] { 2 }));
            Assert.That(plan.Arms[1].Rise, Is.EqualTo(2));
            plan = LeyLineLogic.Plan(self, new[] { new LeyNeighbour(LeyNeighbourKind.Siphon, 1), new LeyNeighbour(LeyNeighbourKind.Siphon, 2), none, none });
            Assert.That(plan.Riser, Is.EqualTo(0));
            Assert.That(plan.Tees, Is.Empty);
            Assert.That(plan.Arms.All(a => a.Rise == 0 && a.Drop == 0), Is.True);
        }

        [Test]
        public void LeyNetworksPluggedIntoTheAcceleratorFeedIt()
        {
            // Two networks: a row of 3 (2 taps, 2 crystals: 2 x 1 = ... see TapIncome) next to the core, and one far away.
            LeyLineLogic.Result r = LeyLineLogic.Solve(Cells((0, 0), (1, 0), (2, 0), (9, 9)), Cells((-1, 0), (3, 0), (9, 10)),
                                                       accelerator: new LeyCell(1, 1, 0));
            int near = r.NetworkOf[0], far = r.NetworkOf[3];
            Assert.That(r.Networks[near].FeedsAccelerator, Is.True, "the middle siphon touches the core");
            Assert.That(r.Networks[far].FeedsAccelerator, Is.False);
            Assert.That(r.AcceleratorIncome, Is.EqualTo(r.Networks[near].Income));
            Assert.That(r.AcceleratorIncome, Is.GreaterThan(0));
            // No core, or not touching: nothing.
            Assert.That(LeyLineLogic.Solve(Cells((0, 0)), Cells((-1, 0))).AcceleratorIncome, Is.EqualTo(0));
            Assert.That(LeyLineLogic.Solve(Cells((0, 0)), Cells((-1, 0)), accelerator: new LeyCell(1, 1, 0)).AcceleratorIncome, Is.EqualTo(0), "diagonal");
            // A lower core is fed through a drop, a higher one from the riser, like a crystal.
            LeyPlan plan = LeyLineLogic.Plan(new LeyCell(0, 0, 3), new[] { new LeyNeighbour(LeyNeighbourKind.Accelerator, 1),
                new LeyNeighbour(LeyNeighbourKind.Accelerator, 5), new LeyNeighbour(), new LeyNeighbour() });
            Assert.That(plan.Arms[0].Drop, Is.EqualTo(2));
            Assert.That(plan.Arms[1].Rise, Is.EqualTo(2));
        }

        [Test]
        public void LeyManaFlowsAwayFromTheCrystals()
        {
            // Crystal at the left end: distances 0, 1, 2, 3 along the row; a network without crystals has none.
            LeyLineLogic.Result r = LeyLineLogic.Solve(Cells((0, 0), (1, 0), (2, 0), (3, 0), (8, 8)), Cells((-1, 0)));
            Assert.That(r.Distance, Is.EqualTo(new[] { 0, 1, 2, 3, -1 }));
            // Crystals at both ends: the middle is furthest.
            r = LeyLineLogic.Solve(Cells((0, 0), (1, 0), (2, 0), (3, 0), (4, 0)), Cells((-1, 0), (5, 0)));
            Assert.That(r.Distance, Is.EqualTo(new[] { 0, 1, 2, 1, 0 }));
        }
            // ------------------------------------------------------------------ Particle Accelerator

        /// <summary>
        /// Seconds until the charge is full, draining from a pool of mana (spent as it drains) that gains
        /// <paramref name="income"/> mana per second; <paramref name="ley"/> is the income of networks plugged into the core.
        /// </summary>
        private static float ChargeTime(int ley, ref int mana, int maxMana, float income = 0f, float limit = 300f)
        {
            var charge = new AcceleratorCharge();
            const float dt = 0.02f;
            float t = 0f, carry = 0f;
            while (!charge.Full && t < limit)
            {
                carry += income * dt;
                int gain = (int)carry;
                carry -= gain;
                mana = System.Math.Min(maxMana, mana + gain);
                mana -= charge.Step(dt, mana, maxMana, ley);
                t += dt;
            }
            return t;
        }

        [Test]
        public void AcceleratorChargesInThirtySecondsWithEnoughMana()
        {
            Assert.That(ParticleAcceleratorLogic.Drain(0), Is.EqualTo(60f), "60 mana per second");
            int mana = 10_000;
            Assert.That(ChargeTime(0, ref mana, 10_000), Is.EqualTo(30f).Within(0.2f));
            Assert.That(mana, Is.EqualTo(8200).Within(1), "30 s x 60 mana/s drained");
        }

        [Test]
        public void AcceleratorNeverChargesFasterThanThirtySecondsWithoutLeyLines()
        {
            // Lots of income (from anything but a plugged-in Ley Line network) can't speed it up: the drain is capped.
            int mana = 1000;
            Assert.That(ChargeTime(0, ref mana, 1000, income: 300f), Is.EqualTo(30f).Within(0.2f));
            // Less income than the drain: it takes as long as the income needs (18 mana/s from the floor: 100 s).
            mana = 250;
            Assert.That(ChargeTime(0, ref mana, 1000, income: 18f), Is.EqualTo(100f).Within(1.5f));
        }

        [Test]
        public void AcceleratorStopsDrainingBelowAQuarterOfTheMana()
        {
            var charge = new AcceleratorCharge();
            int mana = 400, max = 1000;
            for (int i = 0; i < 1000; i++) mana -= charge.Step(0.02f, mana, max, 0);
            Assert.That(mana, Is.GreaterThanOrEqualTo(250), "never below 25% of the maximum");
            Assert.That(mana, Is.LessThan(252), "but drained down to it");
            Assert.That(charge.Starved, Is.True);
            float held = charge.Progress;
            Assert.That(held, Is.EqualTo(150f / 1800f).Within(0.01f), "150 of the 1800 mana a charge needs");
            for (int i = 0; i < 500; i++) mana -= charge.Step(0.02f, mana, max, 0);
            Assert.That(charge.Progress, Is.EqualTo(held), "no charge while held at the floor");
            // More mana comes in: it drains again.
            mana += 30;
            for (int i = 0; i < 100; i++) mana -= charge.Step(0.02f, mana, max, 0);
            Assert.That(charge.Progress, Is.GreaterThan(held));
        }

        [Test]
        public void PluggedInLeyLineIncomeShortensTheCharge()
        {
            int mana = 100_000;
            Assert.That(ChargeTime(60, ref mana, 100_000), Is.EqualTo(15f).Within(0.2f), "drain doubled by 60 mana/s of Ley income");
            mana = 100_000;
            Assert.That(ChargeTime(18, ref mana, 100_000), Is.EqualTo(30f / 1.3f).Within(0.2f));
            Assert.That(ParticleAcceleratorLogic.SecondsLeft(0.5f, 60), Is.EqualTo(7.5f).Within(1e-3));
        }

        [Test]
        public void AcceleratorPoolsTheCapacitorsAndPicksTheStrongestCore()
        {
            var cannons = new List<CannonStats>
            {
                new CannonStats(3, 43, 15, 10, 20),
                new CannonStats(5, 45, 16, 10, 20),
                new CannonStats(5, 50, 15, 11, 20),
            };
            Assert.That(ParticleAcceleratorLogic.PickCore(cannons), Is.EqualTo(2), "highest level, then most damage");
            Assert.That(ParticleAcceleratorLogic.PickCore(new List<CannonStats>()), Is.EqualTo(-1));
            PooledDamage p = ParticleAcceleratorLogic.Pool(cannons);
            Assert.That(p.Base, Is.EqualTo(138));
            Assert.That(p.Cannons, Is.EqualTo(3));
            float exact = 43 * 15 + 45 * 16 + 50 * 15;
            Assert.That(p.Base * p.Health, Is.EqualTo(exact).Within(exact * 0.04f), "base x multiplier is the cannons' sum");
            Assert.That(p.Armor, Is.EqualTo(10));
            Assert.That(p.Shield, Is.EqualTo(20));
            Assert.That(ParticleAcceleratorLogic.TickDamage(138), Is.EqualTo(55));
            Assert.That(ParticleAcceleratorLogic.BurstDamage(138), Is.EqualTo(552));
        }

        [Test]
        public void BlackHoleKeepsCaughtMonstersApart()
        {
            var rng = new SystemRng(7);
            Vector3 centre = new Vector3(4f, 0.33f, -2f);
            const int n = 30;
            var slots = new OrbitSlot[n];
            var pos = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                slots[i] = ParticleAcceleratorLogic.Slot(i, rng);
                float a = rng.Range(0f, 6.28f), d = rng.Range(0f, ParticleAcceleratorLogic.CaptureRadius);
                pos[i] = centre + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                Assert.That(ParticleAcceleratorLogic.InReach(centre, pos[i]), Is.True);
            }
            const float dt = 0.02f;
            float minDist = float.MaxValue;
            for (float age = 0f; age < ParticleAcceleratorLogic.HoleSeconds; age += dt)
            {
                for (int i = 0; i < n; i++)
                    pos[i] = ParticleAcceleratorLogic.Pull(pos[i], ParticleAcceleratorLogic.OrbitPoint(centre, slots[i], age), dt);
                if (age < 1.5f) continue; // pulled in by then
                for (int i = 0; i < n; i++)
                {
                    float r = Vector3.Distance(pos[i], centre);
                    if (age < 4f) Assert.That(r, Is.LessThan(ParticleAcceleratorLogic.OrbitMax + 0.15f), "in the disc");
                    // (In the collapse the disc shrinks to a third: closer, but still apart.)
                    float k = ParticleAcceleratorLogic.Collapse(age) > 0f ? 0.4f : 1f;
                    for (int j = i + 1; j < n; j++) minDist = Mathf.Min(minDist, Vector3.Distance(pos[i], pos[j]) / k);
                }
            }
            Assert.That(minDist, Is.GreaterThan(0.08f), "no two monsters on one point");
            float spread = pos.Average(p => Vector3.Distance(p, centre));
            Assert.That(spread, Is.LessThan(ParticleAcceleratorLogic.OrbitMax * 0.6f), "the collapse pulled them in");
            Assert.That(spread, Is.GreaterThan(0.1f), "but not onto the centre");
            // A monster at the rim is not snapped in: at most 6 units a second.
            Vector3 rim = centre + new Vector3(ParticleAcceleratorLogic.CaptureRadius, 0f, 0f);
            Assert.That(Vector3.Distance(rim, ParticleAcceleratorLogic.Pull(rim, centre, 0.1f)), Is.LessThanOrEqualTo(ParticleAcceleratorLogic.PullMaxSpeed * 0.1f + 1e-4f));
        }

        [Test]
        public void SupernovaThrowsOneToThreeUnits()
        {
            var rng = new SystemRng(3);
            for (int i = 0; i < 200; i++)
            {
                Vector3 o = ParticleAcceleratorLogic.ThrowOffset(rng);
                Assert.That(o.y, Is.EqualTo(0f));
                Assert.That(o.magnitude, Is.InRange(ParticleAcceleratorLogic.ThrowMin - 1e-4f, ParticleAcceleratorLogic.ThrowMax + 1e-4f));
                Vector3 end = ParticleAcceleratorLogic.ThrowPoint(Vector3.zero, o, 1f);
                Assert.That(Vector3.Distance(end, o), Is.LessThan(1e-4f), "lands where it was thrown");
                Assert.That(ParticleAcceleratorLogic.ThrowPoint(Vector3.zero, o, 0.5f).y, Is.GreaterThan(0.3f), "with a hop");
            }
        }
    }
}

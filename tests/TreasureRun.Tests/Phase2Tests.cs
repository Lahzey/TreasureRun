using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TreasureRun.Ascensions.Phase2;
using TreasureRun.Core;
using UnityEngine;

namespace TreasureRun.Tests
{
    /// <summary>Phase 2 ascensions (docs/ascension-plan.md §2.3, expected outcomes).</summary>
    public class Phase2Tests
    {
        // ------------------------------------------------------------------ Cluster Munitions

        [Test]
        public void ClusterBombletsAllReachTheCentreMostOfTheTime()
        {
            var rng = new SystemRng(7);
            float radius = ClusterMunitionsLogic.BombletRadius(1f);
            int all = 0;
            const int shots = 1000;
            for (int i = 0; i < shots; i++)
            {
                var landings = ClusterMunitionsLogic.LandingPoints(Vector3.zero, ClusterMunitionsLogic.BaseBomblets, radius, rng);
                Assert.That(landings.Count, Is.EqualTo(5));
                // A monster standing on the target point (0.25 collider radius) is hit by all of them.
                if (ClusterMunitionsLogic.BlastsReaching(landings, Vector3.zero, radius, 0.25f) == landings.Count) all++;
            }
            Assert.That(all, Is.GreaterThanOrEqualTo(shots * 8 / 10));
        }

        [Test]
        public void ClusterBombletsSpreadOut()
        {
            var landings = ClusterMunitionsLogic.LandingPoints(Vector3.zero, 5, 0.5f, new SystemRng(3));
            // Not stacked on one point: every pair at least a bit apart.
            for (int i = 0; i < landings.Count; i++)
                for (int j = i + 1; j < landings.Count; j++)
                    Assert.That((landings[i] - landings[j]).magnitude, Is.GreaterThan(0.05f));
            Assert.That(ClusterMunitionsLogic.Count(3), Is.EqualTo(8));
            Assert.That(ClusterMunitionsLogic.BombletDamage(21), Is.EqualTo(10).Or.EqualTo(11));
            Assert.That(ClusterMunitionsLogic.BombletDamage(1), Is.EqualTo(1));
        }

        // ------------------------------------------------------------------ Firing Line

        [Test]
        public void FiringLineDirectionIsFlatAndRejectsCloseAims()
        {
            Vector3? d = FiringLineLogic.Direction(new Vector3(1f, 0.6f, 1f), new Vector3(4f, 0f, 5f));
            Assert.That(d.HasValue);
            Assert.That(d.Value.y, Is.EqualTo(0f));
            Assert.That(d.Value.magnitude, Is.EqualTo(1f).Within(1e-4));
            Assert.That(d.Value.x, Is.EqualTo(0.6f).Within(1e-4));
            Assert.That(FiringLineLogic.Direction(Vector3.zero, new Vector3(0.3f, 5f, 0.2f)), Is.Null);
            Assert.That(FiringLineLogic.Fires(true, true));
            Assert.That(FiringLineLogic.Fires(false, true), Is.False);
            Assert.That(FiringLineLogic.Fires(true, false), Is.False);
        }

        // ------------------------------------------------------------------ Hwacha

        [Test]
        public void HwachaVolleySpreadsOverOneSecondAndAroundTheTarget()
        {
            int n = HwachaLogic.DefaultRockets;
            Assert.That(HwachaLogic.LaunchDelay(0, n), Is.EqualTo(0f));
            Assert.That(HwachaLogic.LaunchDelay(n - 1, n), Is.EqualTo(HwachaLogic.LaunchWindow).Within(1e-5));
            for (int i = 1; i < n; i++) Assert.That(HwachaLogic.LaunchDelay(i, n), Is.GreaterThan(HwachaLogic.LaunchDelay(i - 1, n)));

            var rng = new SystemRng(11);
            var offsets = Enumerable.Range(0, 2000).Select(_ => HwachaLogic.ScatterOffset(rng)).ToList();
            Assert.That(offsets.All(o => o.magnitude <= HwachaLogic.Scatter + 1e-4 && o.y == 0f));
            // Uniform over the disc: about a quarter land within half the radius.
            float inner = offsets.Count(o => o.magnitude < HwachaLogic.Scatter / 2f) / (float)offsets.Count;
            Assert.That(inner, Is.EqualTo(0.25f).Within(0.04f));
            Assert.That(HwachaLogic.RocketDamage(20), Is.EqualTo(6));
        }

        // ------------------------------------------------------------------ Bladestorm

        [Test]
        public void BladestormBladesSitOnTheRangeEdgeAndMultiplyWithRange()
        {
            float r = BladestormLogic.OrbitRadius(BladestormLogic.BaseRange);
            Assert.That(r + BladestormLogic.BladeRadius, Is.EqualTo(BladestormLogic.BaseRange).Within(1e-5));
            Assert.That(BladestormLogic.Count(2f), Is.EqualTo(3));
            Assert.That(BladestormLogic.Count(4f), Is.GreaterThan(BladestormLogic.Count(2f)));
            Assert.That(BladestormLogic.Count(6f), Is.GreaterThan(BladestormLogic.Count(4f)));
            // The spacing along the ring stays about the same.
            foreach (float range in new[] { 3f, 5f, 8f })
            {
                float spacing = 2f * Mathf.PI * BladestormLogic.OrbitRadius(range) / BladestormLogic.Count(range);
                Assert.That(spacing, Is.InRange(BladestormLogic.BladeSpacing, BladestormLogic.BladeSpacing * 1.5f));
            }
            Assert.That(BladestormLogic.Count(100f), Is.EqualTo(BladestormLogic.MaxBlades));
        }

        [Test]
        public void BladestormHitsOncePerPassAndSparesTheCentre()
        {
            const float range = 2f, dt = 1f / 60f, radius = 0.25f;
            int count = BladestormLogic.Count(range);
            float revs = BladestormLogic.RevsPerSecond(1f);
            Vector3 onRing = new Vector3(0f, 0f, BladestormLogic.OrbitRadius(range)), centre = new Vector3(0.15f, 0f, 0f);
            var inside = new bool[count];
            int hits = 0, centreHits = 0;
            float turns = 0f;
            for (int frame = 0; frame < 600; frame++)
            {
                turns += revs * dt;
                for (int i = 0; i < count; i++)
                {
                    Vector3 blade = BladestormLogic.BladeOffset(i, count, turns, range);
                    bool now = BladestormLogic.Touches(blade, onRing, radius);
                    if (now && !inside[i]) hits++;
                    inside[i] = now;
                    if (BladestormLogic.Touches(blade, centre, radius)) centreHits++;
                }
            }
            Assert.That(hits, Is.EqualTo(count * revs * 10f).Within(count));
            Assert.That(centreHits, Is.EqualTo(0));
        }

        // ------------------------------------------------------------------ Beam Cannon

        [Test]
        public void BeamCannonDealsMoreDamageForMoreMana()
        {
            // Vanilla Particle Cannon: 40 damage every 5 s, 0.25 × damage mana per shot.
            float carry = 0f, manaCarry = 0f;
            int damage = 0, mana = 0;
            for (int tick = 0; tick < BeamCannonLogic.TicksPerSecond * 100; tick++)
            {
                damage += BeamCannonLogic.Take(ref carry, BeamCannonLogic.DamagePerTick(40, 5f));
                mana += BeamCannonLogic.Take(ref manaCarry, BeamCannonLogic.ManaPerTick(40, 0.25f, 5f));
            }
            Assert.That(damage / 100f, Is.EqualTo(40f / 5f * 1.3f).Within(0.1f));
            Assert.That(mana / 100f, Is.EqualTo(10f / 5f * 1.6f).Within(0.1f));
        }

        // ------------------------------------------------------------------ Dead Man's Land

        [Test]
        public void MinesThrownCloseTogetherMerge()
        {
            var mines = new List<Vector3> { new Vector3(3f, 0f, 3f), new Vector3(5f, 0f, 3f) };
            Assert.That(DeadMansLandLogic.FindMerge(mines, new Vector3(3.2f, 0f, 2.9f)), Is.EqualTo(0));
            Assert.That(DeadMansLandLogic.FindMerge(mines, new Vector3(4f, 0f, 3f)), Is.EqualTo(-1));
            // Two throws at one path cell (centre ± 0.125 each) always merge.
            Assert.That(DeadMansLandLogic.FindMerge(new[] { new Vector3(0.125f, 0f, 0.125f) }, new Vector3(-0.125f, 0f, -0.125f)), Is.EqualTo(0));

            var m = new MineCharges();
            m.Merge(1);
            m.Merge(1);
            Assert.That(m.Charges, Is.EqualTo(3));
            Assert.That(DeadMansLandLogic.Look(1), Is.EqualTo(0));
            Assert.That(DeadMansLandLogic.Look(3), Is.EqualTo(1));
            Assert.That(DeadMansLandLogic.Look(4), Is.EqualTo(2));
        }

        [Test]
        public void ThreeChargeMineExplodesOnThreeFramesWhileAMonsterStands()
        {
            var m = new MineCharges(3);
            var frames = new List<int>();
            bool[] monster = { false, true, true, false, true, true, true };
            for (int f = 0; f < monster.Length; f++) if (m.Step(monster[f])) frames.Add(f);
            Assert.That(frames, Is.EqualTo(new[] { 1, 2, 4 }));
            Assert.That(m.Spent);
        }

        // ------------------------------------------------------------------ timing

        [Test]
        public void IntervalsFollowAttackSpeed()
        {
            Assert.That(IcicleCannonLogic.Interval(1f), Is.EqualTo(IcicleCannonLogic.BaseInterval));
            Assert.That(IcicleCannonLogic.Interval(2f), Is.EqualTo(IcicleCannonLogic.BaseInterval / 2f));
            Assert.That(MagmaLobberLogic.Interval(1.25f), Is.EqualTo(MagmaLobberLogic.BaseInterval / 1.25f).Within(1e-5));
            Assert.That(MagmaLobberLogic.ManaPerLob(6, 0.25f), Is.EqualTo(6));
            // Pool: 20% of a 24 × 6 shot per second = 28.8/s, in half-second ticks.
            Assert.That(MagmaLobberLogic.PoolBurnPerTick(24, 6), Is.EqualTo(14));
            Assert.That(MagmaLobberLogic.PoolDamagePerTick(24), Is.EqualTo(2)); // × the layer multiplier on the hit
            Assert.That(MagmaLobberLogic.PoolDamagePerTick(1), Is.EqualTo(1));
            Assert.That(MagmaLobberLogic.PoolStage(0.5f), Is.EqualTo(0));
            Assert.That(MagmaLobberLogic.PoolStage(2f), Is.EqualTo(1));
            Assert.That(MagmaLobberLogic.PoolStage(3f), Is.EqualTo(2));
            float timer = 0f;
            int shots = 0;
            for (int i = 0; i < 600; i++) shots += FireClock.Due(ref timer, 1f / 60f, 1.2f);
            Assert.That(shots, Is.EqualTo(8));
        }
    }
}

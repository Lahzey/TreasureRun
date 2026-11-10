using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TreasureRun.Ascensions.Phase1;
using TreasureRun.Core;
using TreasureRun.Tests.Sim;
using UnityEngine;

namespace TreasureRun.Tests
{
    /// <summary>Phase 1 ascensions (docs/ascension-plan.md §2.3, expected outcomes).</summary>
    public class Phase1Tests
    {
        // ------------------------------------------------------------------ Repeater

        /// <summary>A Ballista whose shots are Repeater bursts: 3 bolts BoltSpacing apart, reload × IntervalFactor.</summary>
        private static SimTower RepeaterBallista(SimWorld w, Vector3 pos, List<(float time, SimEnemy target)> pending)
        {
            SimTower t = SimTower.Vanilla("Ballista", pos);
            t.Name = "Repeater";
            t.FireInterval *= RepeaterLogic.IntervalFactor;
            w.Add(t);
            t.Attack = (world, tower, target) =>
            {
                for (int i = 0; i < RepeaterLogic.Burst; i++) pending.Add((world.Time + RepeaterLogic.BoltDelay(i), target));
            };
            w.Stepped += (world, dt) =>
            {
                for (int i = pending.Count - 1; i >= 0; i--)
                {
                    if (pending[i].time > world.Time) continue;
                    SimEnemy e = pending[i].target;
                    if (!e.Dead) world.Hit(e, t.Hit()); // a bolt whose target died is a dud
                    pending.RemoveAt(i);
                }
            };
            return t;
        }

        [Test]
        public void RepeaterDealsOneAndAHalfTimesBallistaDps()
        {
            var w = new SimWorld(SimPath.Straight(-10f, 10f));
            w.Add(SimEnemy.Dummy(), 10f);
            RepeaterBallista(w, new Vector3(0f, 0f, 2f), new List<(float, SimEnemy)>());
            w.Run(120f);
            double vanillaDps = 100.0 / 3.0;
            Assert.That(w.Log.Of("Repeater") / 120.0, Is.EqualTo(vanillaDps * 1.5).Within(vanillaDps * 1.5 * 0.05));
        }

        [Test]
        public void RepeaterBoltsAfterTheTargetDiesAreLost()
        {
            var w = new SimWorld(SimPath.Straight(-10f, 10f));
            SimEnemy weak = w.Add(SimEnemy.Create(100, speed: 0f), 10f);     // dies to the first bolt (100 damage)
            SimEnemy other = w.Add(SimEnemy.Create(100_000, speed: 0f), 10.2f);
            var pending = new List<(float, SimEnemy)>();
            SimTower t = RepeaterBallista(w, new Vector3(0f, 0f, 2f), pending);
            t.SelectTarget = list => list.FirstOrDefault(e => e == weak) ?? list.First();
            w.Run(6.6f); // first burst only (the first shot comes after one reload of 6 s)
            Assert.That(weak.Dead, Is.True);
            Assert.That(other.Health, Is.EqualTo(100_000), "the 2 remaining bolts must not hit anything");
            Assert.That(w.Log.Of("Repeater"), Is.EqualTo(100));
        }

        // ------------------------------------------------------------------ Springald

        [Test]
        public void SpringaldDamageFallsByFifthsAndStopsAfterFiveHits()
        {
            int[] dmg = Enumerable.Range(0, 7).Select(k => SpringaldLogic.DamageForHit(10, k)).ToArray();
            Assert.That(dmg, Is.EqualTo(new[] { 10, 8, 6, 4, 2, 0, 0 }));
            Assert.That(SpringaldLogic.Spent(4), Is.False);
            Assert.That(SpringaldLogic.Spent(5), Is.True);
        }

        // ------------------------------------------------------------------ Chain Lightning

        private static List<Vector3> Line(int n, float spacing) =>
            Enumerable.Range(0, n).Select(i => new Vector3(i * spacing, 0f, 0f)).ToList();

        [Test]
        public void ChainLightningHitsTwentyDistinctEnemiesInALine()
        {
            List<ChainHop> hops = ChainLightningLogic.Plan(Line(30, 1.5f), 0, 2f, ChainLightningLogic.MaxHits(0));
            Assert.That(hops.Count, Is.EqualTo(20));
            Assert.That(hops.Select(h => h.To).Distinct().Count(), Is.EqualTo(20));
            Assert.That(hops[0].From, Is.EqualTo(-1));
            for (int i = 1; i < hops.Count; i++) Assert.That(hops[i].From, Is.EqualTo(hops[i - 1].To), "each jump starts at the last victim");
        }

        [Test]
        public void ChainLightningRestartsFromEarlierVictimsAtDeadEnds()
        {
            // Start in the middle of a line: the chain runs to one end, then continues on the other side from the start.
            List<Vector3> enemies = Line(9, 1f);
            List<ChainHop> hops = ChainLightningLogic.Plan(enemies, 4, 1.2f, 20);
            Assert.That(hops.Count, Is.EqualTo(9), "every enemy is reached exactly once");
            Assert.That(hops.Select(h => h.To).Distinct().Count(), Is.EqualTo(9));
            Assert.That(hops.Any(h => h.From >= 0 && hops.FindIndex(x => x.To == h.From) < hops.IndexOf(h) - 1), Is.True,
                        "some jump starts from an earlier victim");
            foreach (ChainHop h in hops.Skip(1))
                Assert.That(Vector3.Distance(enemies[h.From], enemies[h.To]), Is.LessThanOrEqualTo(1.2f));
        }

        [Test]
        public void ChainLightningReachCountsTheMonstersCollider()
        {
            // Gaps of 2.2 with range 2: out of reach centre to centre, in reach once the 0.25 radius counts (like vanilla).
            List<Vector3> enemies = Line(10, 2.2f);
            Assert.That(ChainLightningLogic.Plan(enemies, 0, 2f, 20).Count, Is.EqualTo(1));
            Assert.That(ChainLightningLogic.Plan(enemies, 0, 2f, 20, Enumerable.Repeat(0.25f, 10).ToList()).Count, Is.EqualTo(10));
        }

        [Test]
        public void ChainLightningStopsWhenNothingIsInReach()
        {
            var enemies = new List<Vector3> { Vector3.zero, new Vector3(1f, 0f, 0f), new Vector3(10f, 0f, 0f) };
            Assert.That(ChainLightningLogic.Plan(enemies, 0, 2f, 20).Count, Is.EqualTo(2));
            Assert.That(ChainLightningLogic.MaxHits(2), Is.EqualTo(30));
        }

        // ------------------------------------------------------------------ Supercharge

        [Test]
        public void SuperchargeOneCoilRefreshesTwoCoilsStack()
        {
            var buffs = new SuperchargeBuffs();
            const int tower = 1, coilA = 10, coilB = 11;
            // Coil A attacks twice a second for 3 s: always exactly one stack.
            for (float t = 0f; t < 3f; t += 0.5f)
            {
                buffs.Apply(coilA, new[] { tower }, t);
                buffs.Expire(t + 0.25f);
                Assert.That(buffs.Bonus(tower, t + 0.25f), Is.EqualTo(0.3f).Within(1e-5f));
            }
            buffs.Apply(coilB, new[] { tower }, 3f);
            buffs.Apply(coilA, new[] { tower }, 3f);
            Assert.That(buffs.Bonus(tower, 3.5f), Is.EqualTo(0.6f).Within(1e-5f));
            Assert.That(buffs.Expire(4.1f), Is.EquivalentTo(new[] { tower }));
            Assert.That(buffs.Stacks(tower, 4.1f), Is.EqualTo(0));
        }

        [Test]
        public void SuperchargeCoilDoesNotBuffItselfButOtherCoils()
        {
            var buffs = new SuperchargeBuffs();
            buffs.Apply(10, new[] { 10, 11 }, 0f);
            Assert.That(buffs.Stacks(10, 0.5f), Is.EqualTo(0));
            Assert.That(buffs.Stacks(11, 0.5f), Is.EqualTo(1));
        }

        // ------------------------------------------------------------------ Lone Scout

        [Test]
        public void LoneScoutTicksThreeTimesASecondPlusOnePerCard()
        {
            foreach (int cards in new[] { 0, 1, 3 })
            {
                float acc = 0f;
                int ticks = 0;
                for (int i = 0; i < 500; i++) ticks += LoneScoutLogic.TicksDue(ref acc, 0.02f, cards); // 10 s
                Assert.That(ticks, Is.EqualTo(10 * (3 + cards)).Within(1));
            }
        }

        [Test]
        public void LoneScoutTickGetsTheMarkBonus()
        {
            var w = new SimWorld(SimPath.Straight(-10f, 10f));
            SimEnemy e = w.Add(SimEnemy.Dummy(), 10f);
            e.Mark = new SimMark { Damage = 1, HealthDamage = 2, ArmorDamage = 1, ShieldDamage = 3 };
            float acc = 0f;
            w.Stepped += (world, dt) =>
            {
                int n = LoneScoutLogic.TicksDue(ref acc, dt, 0);
                for (int i = 0; i < n; i++) world.Hit(e, new SimHit("LoneScout", 1, 1, 1, 1));
            };
            w.Run(10f);
            // Vanilla mark: base 1 + mark 1 = 2, health multiplier 1 + mark 2 = 3 → 6 per tick, 30 ticks.
            Assert.That(w.Log.Total, Is.EqualTo(30 * 6).Within(6), "own share + the mark share credited to the Lookout");
        }

        // ------------------------------------------------------------------ Sealed Fate

        [Test]
        public void SealedFateGrowsByOneUpToThreeTimes()
        {
            int d = 15;
            var seq = new List<int>();
            for (int i = 0; i < 40; i++) { seq.Add(d); d = SealedFateLogic.NextDamage(d, 15); }
            Assert.That(seq.Take(3), Is.EqualTo(new[] { 15, 16, 17 }));
            Assert.That(seq.Max(), Is.EqualTo(45));
            Assert.That(seq.Last(), Is.EqualTo(45));
        }

        // ------------------------------------------------------------------ Nest of Bats

        [Test]
        public void BatsPreferNearbyTargets()
        {
            var rng = new SystemRng(3);
            var targets = new List<Vector3> { new Vector3(1f, 0f, 0f), new Vector3(4f, 0f, 0f) }; // weights 1 vs 1/16
            int near = 0;
            for (int i = 0; i < 2000; i++) if (NestOfBatsLogic.PickTarget(Vector3.zero, targets, rng) == 0) near++;
            Assert.That(near / 2000f, Is.EqualTo(16f / 17f).Within(0.03f));
            Assert.That(NestOfBatsLogic.PickTarget(Vector3.zero, new List<Vector3>(), rng), Is.EqualTo(-1));
        }

        [Test]
        public void BatsRetargetWhenTheTargetDiesOrLeavesRange()
        {
            Assert.That(NestOfBatsLogic.NeedsNewTarget(false, Vector3.zero, Vector3.zero, 9f), Is.True);
            Assert.That(NestOfBatsLogic.NeedsNewTarget(true, new Vector3(5f, 3f, 0f), Vector3.zero, 9f), Is.False);
            Assert.That(NestOfBatsLogic.NeedsNewTarget(true, new Vector3(9.5f, 0f, 0f), Vector3.zero, 9f), Is.True);
            Assert.That(NestOfBatsLogic.Count(3), Is.EqualTo(19));
        }
    }
}

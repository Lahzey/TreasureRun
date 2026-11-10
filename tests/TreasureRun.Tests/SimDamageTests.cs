using NUnit.Framework;
using TreasureRun.Core;
using TreasureRun.Tests.Sim;

namespace TreasureRun.Tests
{
    /// <summary>Pins SimDamage to hand-computed vanilla numbers (docs/reference/vanilla-towers.md).</summary>
    public class SimDamageTests
    {
        private static readonly SimMonsterMods Mods = new SimMonsterMods();

        /// <summary>RNG returning a fixed value (crit and stun rolls become deterministic).</summary>
        private sealed class FixedRng : IRng
        {
            private readonly float value;
            public FixedRng(float value) { this.value = value; }
            public float Value() => value;
            public int Range(int min, int max) => min;
        }

        private static readonly IRng NoCrit = new FixedRng(0.99f);

        [Test]
        public void BallistaHitOnHealthIsBaseTimesMultiplier()
        {
            var e = SimEnemy.Create(1000);
            SimDamage.TakeDamage(e, new SimHit("Ballista", 10, 10, 5, 5), NoCrit, Mods, null);
            Assert.That(e.Health, Is.EqualTo(900));
        }

        [Test]
        public void LayersGoShieldThenArmorThenHealth()
        {
            var e = SimEnemy.Create(1000, armor: 1000, shield: 1000);
            SimDamage.TakeDamage(e, new SimHit("Ballista", 10, 10, 5, 5), NoCrit, Mods, null);
            Assert.That((e.Shield, e.Armor, e.Health), Is.EqualTo((950, 1000, 1000)));
        }

        [Test]
        public void OverflowCarriesIntoTheNextLayerAsBaseDamage()
        {
            // 10 x 5 = 50 on a 20-point shield: 30 left over = 30 / 5 = 6 base damage into armor: 6 x 5 = 30.
            var e = SimEnemy.Create(1000, armor: 1000, shield: 20);
            SimDamage.TakeDamage(e, new SimHit("Ballista", 10, 10, 5, 5), NoCrit, Mods, null);
            Assert.That((e.Shield, e.Armor), Is.EqualTo((0, 970)));
        }

        [TestCase(0.5f, 0.4f, 200)] // roll 0.4 < 0.5: x2
        [TestCase(0.9f, 0.3f, 300)] // roll 0.3 < 0.9 - 0.5: x3
        [TestCase(1.6f, 0.1f, 400)] // roll 0.1 < 0.5 (the x4 tier is capped at 50%)
        [TestCase(0.3f, 0.4f, 100)] // no crit
        public void CritTiers(float crit, float roll, int expected)
        {
            var e = SimEnemy.Create(10_000);
            SimDamage.TakeDamage(e, new SimHit("x", 10, 10, 0, 0) { Crit = crit }, new FixedRng(roll), Mods, null);
            Assert.That(10_000 - e.Health, Is.EqualTo(expected));
        }

        [Test]
        public void MarkAddsLookoutDamageAndMultipliersAndCreditsTheLookout()
        {
            var e = SimEnemy.Create(10_000);
            e.Mark = new SimMark { Damage = 1, HealthDamage = 2, ArmorDamage = 1, ShieldDamage = 3 };
            var log = new SimDamageLog();
            SimDamage.TakeDamage(e, new SimHit("Ballista", 10, 10, 5, 5), NoCrit, Mods, log);
            // (10 + 1) x (10 + 2) = 132; the Ballista's own share is 10 x 10 = 100.
            Assert.That(10_000 - e.Health, Is.EqualTo(132));
            Assert.That(log.Of("Ballista"), Is.EqualTo(100));
            Assert.That(log.Of(SimDamage.Lookout), Is.EqualTo(32));
        }

        [Test]
        public void BleedStacksTicksAndRaisesTheHealthMultiplier()
        {
            var e = SimEnemy.Create(10_000);
            SimDamage.TakeDamage(e, new SimHit("Shredder", 10, 20, 10, 10) { Bleed = 1f }, NoCrit, Mods, null);
            Assert.That(e.Health, Is.EqualTo(9800));
            Assert.That(e.Bleed, Is.EqualTo(200));

            // First tick right away: 24 stacks used; 1 base x (24 + 1 for bleeding) = 25 health damage.
            SimDamage.Update(e, 0.02f, NoCrit, Mods, null);
            Assert.That(e.Bleed, Is.EqualTo(176));
            Assert.That(e.Health, Is.EqualTo(9800 - 25));

            // A bleeding target takes +1 health multiplier from every hit: 10 x 11.
            SimDamage.TakeDamage(e, new SimHit("Ballista", 10, 10, 5, 5), NoCrit, Mods, null);
            Assert.That(e.Health, Is.EqualTo(9775 - 110));
        }

        [Test]
        public void SlowIsDamageTimesPercentDecaysAndCapsAt60()
        {
            var e = SimEnemy.Create(100_000);
            SimDamage.TakeDamage(e, new SimHit("Frost", 6, 10, 5, 5) { Slow = 0.5f }, NoCrit, Mods, null);
            Assert.That(e.Slow, Is.EqualTo(0.3f).Within(1e-5)); // 60 damage x 0.5% = 30%
            SimDamage.TakeDamage(e, new SimHit("Frost", 6, 10, 5, 5) { Slow = 0.5f }, NoCrit, Mods, null);
            Assert.That(e.Slow, Is.EqualTo(0.6f).Within(1e-5));
            SimDamage.Update(e, 1f, NoCrit, Mods, null);
            Assert.That(e.Slow, Is.EqualTo(0.55f).Within(1e-5)); // decays 5 points per second
        }

        [Test]
        public void FreezeGetsShorterEachTime()
        {
            var e = SimEnemy.Create(100_000);
            SimDamage.TakeDamage(e, new SimHit("x", 1, 1, 1, 1) { Stun = 1f }, new FixedRng(0.5f), Mods, null);
            Assert.That(e.FreezeTime, Is.EqualTo(1f));
            SimDamage.Update(e, 1.1f, NoCrit, Mods, null);
            SimDamage.TakeDamage(e, new SimHit("x", 1, 1, 1, 1) { Stun = 1f }, new FixedRng(0.5f), Mods, null);
            Assert.That(e.FreezeTime, Is.EqualTo(0.8f).Within(1e-5));
        }

        [Test]
        public void KillIsLogged()
        {
            var e = SimEnemy.Create(50);
            var log = new SimDamageLog();
            SimDamage.TakeDamage(e, new SimHit("Ballista", 10, 10, 5, 5), NoCrit, Mods, log);
            Assert.That(e.Dead, Is.True);
            Assert.That(log.Kills, Has.Member("Ballista"));
            Assert.That(log.Of("Ballista"), Is.EqualTo(50));
        }
    }
}

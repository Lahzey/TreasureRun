using NUnit.Framework;
using TreasureRun.Tests.Sim;
using UnityEngine;

namespace TreasureRun.Tests
{
    public class SimWorldTests
    {
        [Test]
        public void BallistaDpsAgainstADummyMatchesVanilla()
        {
            // 100 damage per shot, one shot every 3 s.
            var w = new SimWorld(SimPath.Straight(-10f, 10f));
            w.Add(SimEnemy.Dummy(), 10f);
            w.Add(SimTower.Vanilla("Ballista", new Vector3(0f, 0f, 2f)));
            w.Run(60f);
            Assert.That(w.Log.Of("Ballista") / 60.0, Is.EqualTo(100.0 / 3.0).Within(2.0));
        }

        [Test]
        public void EnemiesWalkThePathAndLeakAtTheEnd()
        {
            var w = new SimWorld(SimPath.Straight(0f, 10f));
            SimEnemy e = w.Add(SimEnemy.Create(100, speed: 2f));
            w.Run(4f);
            Assert.That(e.Position.x, Is.EqualTo(8f).Within(0.1f));
            w.Run(1.1f);
            Assert.That(w.Leaked, Has.Member(e));
        }

        [Test]
        public void TowersOnlyShootInRange()
        {
            var w = new SimWorld(SimPath.Straight(-30f, 30f));
            w.Add(SimEnemy.Dummy(), 30f);
            w.Add(SimTower.Vanilla("Ballista", new Vector3(0f, 0f, 6f))); // range 5
            w.Run(10f);
            Assert.That(w.Log.Total, Is.EqualTo(0));
        }

        [Test]
        public void SameSeedSameResult()
        {
            long Run(int seed)
            {
                var w = new SimWorld(SimPath.Straight(-10f, 10f), seed);
                w.Add(SimEnemy.Dummy(), 10f);
                SimTower t = w.Add(SimTower.Vanilla("Cannon", Vector3.zero));
                t.Crit = 0.4f;
                w.Run(30f);
                return w.Log.Total;
            }
            Assert.That(Run(7), Is.EqualTo(Run(7)));
            Assert.That(Run(7), Is.Not.EqualTo(Run(8)));
        }
    }
}

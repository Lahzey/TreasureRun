using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TreasureRun.Balance;
using TreasureRun.Core;
using TreasureRun.Pois;
using UnityEngine;

namespace TreasureRun.Tests
{
    /// <summary>Points of Interest: the section generator (every exit combination), the Lightning Trap logic and its balance.</summary>
    public class PoiTests
    {
        private static readonly SectionExits[] AllExits =
        {
            SectionExits.Left, SectionExits.Top, SectionExits.Right,
            SectionExits.Left | SectionExits.Top, SectionExits.Left | SectionExits.Right, SectionExits.Top | SectionExits.Right,
            SectionExits.All,
        };

        [Test]
        public void EveryExitCombinationMakesAValidSection([ValueSource(nameof(AllExits))] SectionExits exits)
        {
            for (int seed = 0; seed < 25; seed++)
            {
                foreach (SectionStyle style in new[] { new SectionStyle(), new LightningTrapStyle() })
                {
                    SectionLayout l = PoiSectionLogic.Generate(exits, style, new SystemRng(seed));
                    Assert.That(PoiSectionLogic.Validate(l), Is.Null, $"{exits} seed {seed} {style.GetType().Name}");
                    // Every path cell is reachable from the entry along path cells (no stray path).
                    Assert.That(PathCellsReachable(l), Is.True, $"{exits} seed {seed}");
                    // Grass only at heights 1-4, path at 0.
                    for (int x = 0; x < SectionLayout.Size; x++)
                        for (int z = 0; z < SectionLayout.Size; z++)
                            if (l.IsPath(x, z)) Assert.That(l.Height[x, z], Is.EqualTo(0));
                            else Assert.That(l.Height[x, z], Is.InRange(1, 4));
                }
            }
        }

        [Test]
        public void EmptyExitsAreRejected() =>
            Assert.Throws<System.ArgumentException>(() => PoiSectionLogic.Generate(SectionExits.None, null, new SystemRng(1)));

        [Test]
        public void FourWayWaypointsMergeInTheCentreAndRunToTheEntry()
        {
            SectionLayout l = PoiSectionLogic.Generate(SectionExits.All, new LightningTrapStyle(), new SystemRng(3));
            SectionWaypoint last = l.Last;
            Assert.That((last.X, last.Z), Is.EqualTo((3, 0)));
            Assert.That(last.Local, Is.EqualTo(new Vector3(0, 0, -3)));
            foreach (string role in new[] { "left", "top", "right" })
            {
                SectionWaypoint w = l.Waypoints.Single(p => p.Role == role);
                SectionWaypoint hub = l.Waypoints[w.Next];
                Assert.That((hub.X, hub.Z), Is.EqualTo((3, 3)), role);
                Assert.That(l.Waypoints[hub.Next], Is.SameAs(last));
            }
            Assert.That(l.Waypoints.Single(p => p.Role == "left").Local, Is.EqualTo(new Vector3(-3, 0, 0)));
            Assert.That(l.Waypoints.Single(p => p.Role == "right").Local, Is.EqualTo(new Vector3(3, 0, 0)));
            Assert.That(l.Waypoints.Single(p => p.Role == "top").Local, Is.EqualTo(new Vector3(0, 0, 3)));
        }

        [Test]
        public void LightningTrapSectionHasPolesOnTheDiagonals([ValueSource(nameof(AllExits))] SectionExits exits)
        {
            SectionLayout l = PoiSectionLogic.Generate(exits, new LightningTrapStyle(), new SystemRng(7));
            foreach (var (x, z) in LightningTrapLogic.PoleCells)
            {
                Assert.That(l.Cells[x, z], Is.EqualTo(SectionCell.Reserved), $"pole ({x},{z})");
                Assert.That(l.HasGround(x, z), Is.True);
            }
            Assert.That(l.Cells[3, 3], Is.EqualTo(SectionCell.Path), "the paths meet in the centre cell");
            Assert.That(l.IsPath(3, 2), Is.True);
            if ((exits & SectionExits.Left) != 0) Assert.That(l.IsPath(2, 3), Is.True);
            if ((exits & SectionExits.Top) != 0) Assert.That(l.IsPath(3, 4), Is.True);
            if ((exits & SectionExits.Right) != 0) Assert.That(l.IsPath(4, 3), Is.True);
        }

        [Test]
        public void TerrainMeshCoversTheGrassAndStaysInsideTheTile()
        {
            SectionLayout l = PoiSectionLogic.Generate(SectionExits.Left | SectionExits.Right, new LightningTrapStyle(), new SystemRng(2));
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            SectionMeshLogic.Build(l, verts, normals, tris);
            int grass = 0;
            for (int x = 0; x < 7; x++) for (int z = 0; z < 7; z++) if (l.HasGround(x, z)) grass++;
            int tops = 0;
            for (int i = 0; i < verts.Count; i += 4) if (normals[i] == Vector3.up) tops++;
            Assert.That(tops, Is.EqualTo(grass));
            Assert.That(verts.All(v => Mathf.Abs(v.x) <= 3.5f + 1e-4f && Mathf.Abs(v.z) <= 3.5f + 1e-4f), Is.True);
            Assert.That(verts.Max(v => v.y), Is.LessThanOrEqualTo(4 / 3f + 1e-4f));
            Assert.That(verts.Min(v => v.y), Is.EqualTo(SectionMeshLogic.Floor).Within(1e-4));
            Assert.That(tris.Count % 6, Is.EqualTo(0));
            // Front faces are clockwise seen along their normal (Unity): the triangle normal matches the face normal.
            for (int t = 0; t < tris.Count; t += 3)
            {
                Vector3 a = verts[tris[t]], b = verts[tris[t + 1]], c = verts[tris[t + 2]];
                Assert.That(Vector3.Dot(Vector3.Cross(b - a, c - a), normals[tris[t]]), Is.GreaterThan(0f));
            }
        }

        [Test]
        public void TrapMetersItsManaRateAndStopsWithoutMana()
        {
            foreach (int rate in new[] { 15, 6, 1 })
            {
                float owed = 0f;
                int spent = 0;
                bool powered = false;
                for (int i = 0; i < 120; i++) spent += LightningTrapLogic.Meter(ref owed, ref powered, 1f / 60f, 1000, rate);
                Assert.That(spent, Is.InRange(2 * rate - 1, 2 * rate), $"rate {rate}");
                Assert.That(powered, Is.True);
            }
            float o = 0f;
            bool on = true;
            Assert.That(LightningTrapLogic.Meter(ref o, ref on, 1f, 1000, 0), Is.EqualTo(0));
            Assert.That(on, Is.False, "at 0 mana per second it is off");

            // Out of mana: silent, and it stays silent on a trickle until a full second's worth is back.
            bool p = true;
            int mana = 0, paid = 0;
            for (int i = 0; i < 60; i++)
            {
                if (i % 30 == 15) mana += 1; // 2 mana a second of regeneration
                int d = LightningTrapLogic.Meter(ref o, ref p, 1f / 60f, mana, 15);
                mana -= d; paid += d;
            }
            Assert.That(p, Is.False);
            Assert.That(paid, Is.EqualTo(0));
            mana = 15;
            LightningTrapLogic.Meter(ref o, ref p, 1f / 60f, mana, 15);
            Assert.That(p, Is.True, "back on with a second's worth of mana");
        }

        [Test]
        public void TrapZoneIsTheCentreCell()
        {
            Assert.That(LightningTrapLogic.InZone(new Vector3(0.45f, 0.3f, -0.45f)), Is.True);
            Assert.That(LightningTrapLogic.InZone(new Vector3(0.55f, 0f, 0f)), Is.False);
            Assert.That(LightningTrapLogic.InZone(new Vector3(0f, 0f, -0.6f)), Is.False);
        }

        [Test]
        public void TrapDamageGrowsWithTheWavesAndFollowsTheMana()
        {
            for (int w = 2; w <= 45; w++)
                Assert.That(LightningTrapLogic.Damage(w, 15), Is.GreaterThan(LightningTrapLogic.Damage(w - 1, 15)));
            Assert.That(LightningTrapLogic.Damage(20, 0), Is.EqualTo(0));
            Assert.That(LightningTrapLogic.Damage(20, 10), Is.EqualTo(LightningTrapLogic.Damage(20, 15) * 2 / 3.0).Within(1));
            Assert.That(LightningTrapLogic.Damage(20, 99), Is.EqualTo(LightningTrapLogic.Damage(20, 15)), "capped at 15 mana/s");
            Assert.That(LightningTrapLogic.HealthMult, Is.EqualTo(TowerData.Get(TowerKind.TeslaCoil).HealthMult));
            Assert.That(LightningTrapLogic.ShieldMult, Is.EqualTo(TowerData.Get(TowerKind.TeslaCoil).ShieldMult));
        }

        [Test]
        public void TrapIsRatedAtThePoiBudget()
        {
            Spec s = ContentSpecs.Poi(LightningTrapLogic.Id);
            Assert.That(s, Is.Not.Null, "every POI needs a spec in ContentSpecs.Pois");
            Rating r = Valuation.Rate(LightningTrapLogic.Id, "Lightning Trap", "poi", null, false, s);
            Assert.That(r.Budget, Is.EqualTo(Pools.PoiBudget));
            Assert.That(r.Flag, Is.EqualTo(""), $"{r.Points:0.0} points");
        }

        [Test]
        public void FixedSourcesAreNeverBoughtOrBuffed()
        {
            Board b = ReferenceBoards.Get(20, BoardVariant.Mixed);
            new Spec().FixedSource("trap", LightningTrapLogic.Info).Atoms[0].Apply(new AtomContext { Board = b, Acquired = 20, Wave = 20 });
            TowerGroup trap = b.Groups.Single(g => g.Fixed);
            b.Spend(5000);
            Assert.That(trap.Count, Is.EqualTo(1));
            Assert.That(b.GroupCost(trap), Is.EqualTo(0));
            Assert.That(b.Scope(null).Contains(trap), Is.False);
        }

        private static bool PathCellsReachable(SectionLayout l)
        {
            var seen = new HashSet<(int, int)>();
            var stack = new Stack<(int x, int z)>();
            stack.Push(SectionLayout.Entry);
            while (stack.Count > 0)
            {
                var c = stack.Pop();
                if (!l.IsPath(c.x, c.z) || !seen.Add(c)) continue;
                stack.Push((c.x + 1, c.z)); stack.Push((c.x - 1, c.z)); stack.Push((c.x, c.z + 1)); stack.Push((c.x, c.z - 1));
            }
            for (int x = 0; x < 7; x++) for (int z = 0; z < 7; z++) if (l.IsPath(x, z) && !seen.Contains((x, z))) return false;
            return true;
        }
    }
}

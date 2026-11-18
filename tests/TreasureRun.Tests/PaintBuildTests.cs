using System;
using System.Linq;
using NUnit.Framework;
using TreasureRun.Patches;
using UnityEngine;

namespace TreasureRun.Tests
{
    /// <summary>Shift-drag building: the cells a drag passes.</summary>
    public class PaintBuildTests
    {
        [Test]
        public void StraightDragsCoverEveryCellOnce()
        {
            Assert.That(PaintBuildLogic.Line(new Vector2Int(2, 5), new Vector2Int(6, 5)),
                        Is.EqualTo(Enumerable.Range(2, 5).Select(x => new Vector2Int(x, 5))));
            Assert.That(PaintBuildLogic.Line(new Vector2Int(0, 3), new Vector2Int(0, -1)),
                        Is.EqualTo(Enumerable.Range(0, 5).Select(i => new Vector2Int(0, 3 - i))));
            Assert.That(PaintBuildLogic.Line(new Vector2Int(4, 4), new Vector2Int(4, 4)), Is.EqualTo(new[] { new Vector2Int(4, 4) }));
        }

        [Test]
        public void DiagonalDragsAreStaircasesOfNeighbours()
        {
            var rng = new System.Random(7);
            for (int trial = 0; trial < 200; trial++)
            {
                var a = new Vector2Int(rng.Next(-10, 10), rng.Next(-10, 10));
                var b = new Vector2Int(rng.Next(-10, 10), rng.Next(-10, 10));
                var line = PaintBuildLogic.Line(a, b);
                Assert.That(line.First(), Is.EqualTo(a));
                Assert.That(line.Last(), Is.EqualTo(b));
                Assert.That(line.Count, Is.EqualTo(Math.Abs(b.x - a.x) + Math.Abs(b.y - a.y) + 1), "no cell twice, none skipped");
                for (int i = 1; i < line.Count; i++)
                    Assert.That(Math.Abs(line[i].x - line[i - 1].x) + Math.Abs(line[i].y - line[i - 1].y), Is.EqualTo(1), "shares a side with the one before");
                // It stays close to the straight line (within a cell).
                Vector2 d = b - a;
                foreach (Vector2Int c in line)
                {
                    Vector2 r = c - a;
                    float off = d.sqrMagnitude < 1e-6f ? 0f : Mathf.Abs(r.x * d.y - r.y * d.x) / d.magnitude;
                    Assert.That(off, Is.LessThanOrEqualTo(1f));
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

// Pure part of shift-drag building (PaintBuildPatches): which grid cells a drag passes. No Unity engine calls, so the
// offline tests run it directly.
namespace TreasureRun.Patches
{
    public static class PaintBuildLogic
    {
        /// <summary>
        /// The cells from <paramref name="a"/> to <paramref name="b"/> (both included), each next to the one before
        /// (sharing a side, never only a corner), as close to the straight line as possible. A diagonal drag becomes a
        /// staircase, so buildings that work with their four neighbours (Ley Siphons) stay connected.
        /// </summary>
        public static List<Vector2Int> Line(Vector2Int a, Vector2Int b)
        {
            int dx = Math.Abs(b.x - a.x), dy = Math.Abs(b.y - a.y), sx = Math.Sign(b.x - a.x), sy = Math.Sign(b.y - a.y);
            var cells = new List<Vector2Int>(dx + dy + 1) { a };
            int x = a.x, y = a.y, ix = 0, iy = 0;
            while (ix < dx || iy < dy)
            {
                // Step along x while the next x boundary comes before the next y boundary on the straight line.
                if ((1 + 2 * ix) * dy < (1 + 2 * iy) * dx) { x += sx; ix++; }
                else { y += sy; iy++; }
                cells.Add(new Vector2Int(x, y));
            }
            return cells;
        }
    }
}

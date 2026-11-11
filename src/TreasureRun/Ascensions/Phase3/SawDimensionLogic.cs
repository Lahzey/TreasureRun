using System.Collections.Generic;
using UnityEngine;

// Pure mechanics of The Saw Dimension (docs/ascension-plan.md §3, phase 3). No Unity engine calls: only Vector3/Mathf,
// so the offline tests (tests/TreasureRun.Tests) run them directly.
namespace TreasureRun.Ascensions.Phase3
{
    /// <summary>One floor saw: where it sits and which way the path runs there.</summary>
    public struct SawSpot
    {
        public Vector3 Position;
        public Vector3 Direction;
        public SawSpot(Vector3 position, Vector3 direction) { Position = position; Direction = direction; }
    }

    /// <summary>
    /// The Saw Dimension (Shredder): the whole path is covered in saws that tick 5 times a second on every monster on the
    /// map. Damage per second = Σ(Shredder damage) / 10, with no base damage: without Shredders the saws do nothing
    /// (and retract). Shredders stop attacking and gain XP centrally, split by the layer (shield, armor, health) the
    /// monsters on the map are on.
    /// </summary>
    public static class SawDimensionLogic
    {
        public const int TicksPerSecond = 5;
        public const float DamageDivisor = 10f;
        /// <summary>Distance between floor saws along the path.</summary>
        public const float SawSpacing = 0.9f;

        /// <summary>Base damage per tick (fractional) for the summed damage of all Shredders.</summary>
        public static float BaseDamagePerTick(int summedDamage) => Mathf.Max(0, summedDamage) / DamageDivisor / TicksPerSecond;

        /// <summary>
        /// Adds <paramref name="amount"/> to the carry and takes out the whole part (fractions accumulate). The game only
        /// deals whole base damage, so 6 per second at 5 ticks comes out as 1, 1, 1, 1, 2; 0.6 per second as one hit of
        /// 1 every 1.67 s. Hits never happen more than <see cref="TicksPerSecond"/> times a second; above that the base
        /// damage per hit grows. (A small epsilon keeps float rounding from turning a due 2 into a 1.)
        /// </summary>
        public static int Take(ref float carry, float amount)
        {
            carry += amount;
            int whole = Mathf.FloorToInt(carry + 1e-4f);
            carry -= whole;
            return whole;
        }

        /// <summary>Average of the Shredders' values, rounded (multipliers of the shared saw hit).</summary>
        public static int Average(IReadOnlyList<int> values)
        {
            if (values.Count == 0) return 0;
            long sum = 0;
            foreach (int v in values) sum += v;
            return Mathf.RoundToInt(sum / (float)values.Count);
        }

        /// <summary>XP a tower with a target gains per second in vanilla (Tower.GainXP): 1 / (2 range) + 1/2.</summary>
        public static float XpPerSecond(float range) => 1f / (2f * Mathf.Max(0.1f, range)) + 0.5f;

        /// <summary>
        /// Share of the XP that goes into each pool: the fraction of monsters on the map whose top layer is shield,
        /// armor or health. All zero when there are no monsters.
        /// </summary>
        public static Vector3 LayerShares(int shieldMonsters, int armorMonsters, int healthMonsters)
        {
            int all = shieldMonsters + armorMonsters + healthMonsters;
            if (all <= 0) return Vector3.zero;
            return new Vector3(shieldMonsters / (float)all, armorMonsters / (float)all, healthMonsters / (float)all);
        }

        /// <summary>
        /// Saws along path segments (from → to pairs, e.g. waypoint → next waypoint): evenly spaced along each segment
        /// (at least one per segment), never two closer than half the spacing (segments share their end points).
        /// </summary>
        public static List<SawSpot> Layout(IReadOnlyList<(Vector3 from, Vector3 to)> segments, float spacing = SawSpacing)
        {
            var spots = new List<SawSpot>();
            float minGap2 = spacing * spacing * 0.25f;
            foreach (var (from, to) in segments)
            {
                Vector3 d = to - from;
                d.y = 0f;
                float len = d.magnitude;
                if (len < 0.01f) continue;
                Vector3 dir = d / len;
                int n = Mathf.Max(1, Mathf.RoundToInt(len / spacing));
                for (int i = 0; i < n; i++)
                {
                    Vector3 p = Vector3.Lerp(from, to, (i + 0.5f) / n);
                    bool close = false;
                    foreach (SawSpot s in spots)
                    {
                        Vector3 g = s.Position - p;
                        g.y = 0f;
                        if (g.sqrMagnitude < minGap2) { close = true; break; }
                    }
                    if (!close) spots.Add(new SawSpot(p, dir));
                }
            }
            return spots;
        }

        /// <summary>
        /// How far the saws are out of the floor (0 = retracted, 1 = fully out), moving towards the target state at
        /// <paramref name="speed"/> per second.
        /// </summary>
        public static float Extension(float current, bool shreddersBuilt, float dt, float speed = 1.6f) =>
            Mathf.MoveTowards(current, shreddersBuilt ? 1f : 0f, speed * dt);
    }
}

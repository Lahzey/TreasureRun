using System;
using System.Collections.Generic;
using UnityEngine;

// Pure mechanics of Ritual of the Sun (docs/ascension-plan.md §3, phase 3). No Unity engine calls: only Vector2/Mathf,
// so the offline tests (tests/TreasureRun.Tests) run them directly.
namespace TreasureRun.Ascensions.Phase3
{
    /// <summary>A Sun Obelisk for the link solver: its position on the ground plane (x, z) and its range.</summary>
    public struct RitualNode
    {
        public Vector2 Pos;
        public float Range;
        public RitualNode(float x, float z, float range) { Pos = new Vector2(x, z); Range = range; }
    }

    /// <summary>The links between Sun Obelisks and what they form.</summary>
    public sealed class RitualSolution
    {
        /// <summary>Every link as a pair of node indices (A &lt; B).</summary>
        public readonly List<(int A, int B)> Links = new List<(int, int)>();
        /// <summary>Closed circuits: the nodes in order round the ring, counter-clockwise seen from above (x right, z up).</summary>
        public readonly List<List<int>> Rings = new List<List<int>>();
        /// <summary>Open chains (2 or more nodes) from one end to the other; the end with the smaller (x, z) comes first.</summary>
        public readonly List<List<int>> Chains = new List<List<int>>();
        /// <summary>Per node: the ring it belongs to (index into <see cref="Rings"/>), or -1.</summary>
        public int[] RingOf = new int[0];

        public bool Linked(int a, int b)
        {
            if (a > b) { int t = a; a = b; b = t; }
            return Links.Contains((a, b));
        }
    }

    /// <summary>The big beam's stats, averaged over the ring's obelisks.</summary>
    public struct RitualBeamStats
    {
        public float Damage;
        public int HealthMult, ArmorMult, ShieldMult;
        public float Slow, Bleed, Burn, Poison, Crit, Stun;
        /// <summary>Hits per second (the obelisks' average).</summary>
        public float Rate;
        /// <summary>Mana per hit (0 when no obelisk uses mana).</summary>
        public float Mana;
    }

    /// <summary>One obelisk's stats as the tower has them (vanilla values plus cards and ascension bonuses).</summary>
    public struct RitualMemberStats
    {
        public int Damage, HealthMult, ArmorMult, ShieldMult;
        public float Slow, Bleed, Burn, Poison, Crit, Stun;
        /// <summary>Seconds between hits (vanilla <c>rps</c>).</summary>
        public float Interval;
        /// <summary>Mana per point of damage (vanilla <c>manaConsumptionRate</c>; 0 if the tower uses none).</summary>
        public float ManaRate;
    }

    /// <summary>
    /// Ritual of the Sun (Obelisk, Mythical). Every Obelisk becomes a <b>Sun Obelisk</b> that holds up to two beams of
    /// light. The closest pairs within range link first (ties: by position, so the result never depends on build order);
    /// a beam can't pass through another obelisk. So obelisks always form open chains or closed rings. A Sun Obelisk on
    /// its own or in a chain fires as usual with <see cref="ObeliskBurn"/> extra burn. Every obelisk of a closed ring
    /// turns its light to the sky and stops firing itself (it still gains XP); the sun answers with a beam at the ring's
    /// centre that hits every monster within <see cref="Radius"/> (growing with the obelisks in the ring) with their
    /// average damage, at their average rate, with <see cref="BeamBurn"/> burn.
    /// </summary>
    public static class RitualLogic
    {
        /// <summary>Extra burn of every Sun Obelisk's own beam.</summary>
        public const float ObeliskBurn = 0.25f;
        /// <summary>Burn of the sun beam (cards' burn bonuses still add on top).</summary>
        public const float BeamBurn = 1f;
        /// <summary>
        /// Sun beam radius: <see cref="RadiusPerObelisk"/> per obelisk in the ring less <see cref="RadiusOffset"/>, at most
        /// <see cref="MaxRadius"/> (3 obelisks: 0.65, 5: 1.15, 7: 1.65). The beam is drawn exactly this wide.
        /// </summary>
        public const float RadiusPerObelisk = 0.25f, RadiusOffset = 0.1f, MaxRadius = 6f;
        /// <summary>A beam whose centre falls on a mana crystal powers it: a ritual circle this many times the beam's radius
        /// round the crystal becomes the damage area.</summary>
        public const float CrystalCircleFactor = 3f;
        /// <summary>A beam is blocked by another obelisk whose centre is this close to it (half a tile, a little less).</summary>
        public const float BlockDistance = 0.45f;
        /// <summary>Most beams per obelisk (one in, one out, like a prism).</summary>
        public const int MaxLinks = 2;
        /// <summary>The smallest ring.</summary>
        public const int MinRing = 3;

        public static float Radius(int obelisks) => Mathf.Clamp(RadiusPerObelisk * obelisks - RadiusOffset, 0f, MaxRadius);

        /// <summary>The beam's centre lies on the crystal's cell (map cells are one unit, crystals sit in their middle).</summary>
        public static bool OnCell(Vector2 centre, Vector2 crystal) =>
            Mathf.Abs(centre.x - crystal.x) <= 0.5f && Mathf.Abs(centre.y - crystal.y) <= 0.5f;

        /// <summary>
        /// Links the obelisks. A pair can link when it is within the larger of the two ranges and no other obelisk
        /// stands on the line between them. Pairs are taken from the closest up; a pair links when both still have a
        /// free beam (<see cref="MaxLinks"/>). Ties in length are taken in order of position, never of build order.
        /// </summary>
        public static RitualSolution Solve(IList<RitualNode> nodes)
        {
            int n = nodes.Count;
            var pairs = new List<(int a, int b, float d)>();
            for (int a = 0; a < n; a++)
                for (int b = a + 1; b < n; b++)
                {
                    float d = Vector2.Distance(nodes[a].Pos, nodes[b].Pos);
                    if (d < 0.01f || d > Mathf.Max(nodes[a].Range, nodes[b].Range) + 1e-4f) continue;
                    if (Blocked(nodes, a, b)) continue;
                    pairs.Add((a, b, d));
                }
            pairs.Sort((p, q) =>
            {
                if (Mathf.Abs(p.d - q.d) > 1e-4f) return p.d.CompareTo(q.d);
                return ComparePairs(nodes, p.a, p.b, q.a, q.b);
            });

            var degree = new int[n];
            var adj = new List<int>[n];
            for (int i = 0; i < n; i++) adj[i] = new List<int>(MaxLinks);
            var result = new RitualSolution { RingOf = new int[n] };
            foreach (var p in pairs)
            {
                if (degree[p.a] >= MaxLinks || degree[p.b] >= MaxLinks) continue;
                degree[p.a]++; degree[p.b]++;
                adj[p.a].Add(p.b); adj[p.b].Add(p.a);
                result.Links.Add((p.a, p.b));
            }

            // Every node has at most two links, so each connected group is a path or a cycle.
            for (int i = 0; i < n; i++) result.RingOf[i] = -1;
            var seen = new bool[n];
            for (int i = 0; i < n; i++)
            {
                if (seen[i] || adj[i].Count == 0) continue;
                List<int> group = Walk(adj, i, seen);
                bool cycle = group.Count >= MinRing && group.TrueForAll(k => adj[k].Count == 2) &&
                             adj[group[0]].Contains(group[group.Count - 1]);
                if (cycle)
                {
                    List<int> ring = OrientRing(nodes, group);
                    foreach (int k in ring) result.RingOf[k] = result.Rings.Count;
                    result.Rings.Add(ring);
                }
                else result.Chains.Add(OrientChain(nodes, adj, group));
            }
            return result;
        }

        /// <summary>Another obelisk stands on the line between a and b (closer than <see cref="BlockDistance"/>, between the two).</summary>
        public static bool Blocked(IList<RitualNode> nodes, int a, int b)
        {
            Vector2 p = nodes[a].Pos, q = nodes[b].Pos, d = q - p;
            float len2 = d.sqrMagnitude;
            for (int k = 0; k < nodes.Count; k++)
            {
                if (k == a || k == b) continue;
                Vector2 r = nodes[k].Pos;
                float t = Vector2.Dot(r - p, d) / len2;
                if (t <= 0f || t >= 1f) continue;
                if (Vector2.Distance(p + d * t, r) < BlockDistance) return true;
            }
            return false;
        }

        private static int ComparePos(Vector2 p, Vector2 q)
        {
            int c = Math.Round(p.x, 3).CompareTo(Math.Round(q.x, 3));
            return c != 0 ? c : Math.Round(p.y, 3).CompareTo(Math.Round(q.y, 3));
        }

        /// <summary>Orders two equally long pairs by their positions (each pair's smaller end first).</summary>
        private static int ComparePairs(IList<RitualNode> nodes, int a1, int b1, int a2, int b2)
        {
            Vector2 p1 = nodes[a1].Pos, q1 = nodes[b1].Pos, p2 = nodes[a2].Pos, q2 = nodes[b2].Pos;
            if (ComparePos(q1, p1) < 0) { Vector2 t = p1; p1 = q1; q1 = t; }
            if (ComparePos(q2, p2) < 0) { Vector2 t = p2; p2 = q2; q2 = t; }
            int c = ComparePos(p1, p2);
            return c != 0 ? c : ComparePos(q1, q2);
        }

        /// <summary>The nodes of i's group in link order (from one end for a path; round the cycle otherwise).</summary>
        private static List<int> Walk(List<int>[] adj, int start, bool[] seen)
        {
            // Find an end (a node with one link) to start a path from; a cycle has none and starts anywhere.
            int first = start, prev = -1, cur = start;
            var visited = new HashSet<int> { start };
            while (true)
            {
                int next = -1;
                foreach (int k in adj[cur]) if (k != prev && !visited.Contains(k)) { next = k; break; }
                if (next < 0) break;
                visited.Add(next);
                prev = cur; cur = next;
            }
            if (adj[cur].Count == 1) first = cur;
            var order = new List<int>();
            prev = -1; cur = first;
            while (cur >= 0 && !seen[cur])
            {
                seen[cur] = true;
                order.Add(cur);
                int next = -1;
                foreach (int k in adj[cur]) if (k != prev && !seen[k]) { next = k; break; }
                prev = cur; cur = next;
            }
            return order;
        }

        private static List<int> OrientRing(IList<RitualNode> nodes, List<int> ring)
        {
            var poly = new List<Vector2>(ring.Count);
            foreach (int k in ring) poly.Add(nodes[k].Pos);
            if (SignedArea(poly) < 0f) ring.Reverse();
            // Start at the node with the smallest position, so the order is stable.
            int best = 0;
            for (int i = 1; i < ring.Count; i++) if (ComparePos(nodes[ring[i]].Pos, nodes[ring[best]].Pos) < 0) best = i;
            var result = new List<int>(ring.Count);
            for (int i = 0; i < ring.Count; i++) result.Add(ring[(best + i) % ring.Count]);
            return result;
        }

        private static List<int> OrientChain(IList<RitualNode> nodes, List<int>[] adj, List<int> chain)
        {
            if (chain.Count > 1 && ComparePos(nodes[chain[chain.Count - 1]].Pos, nodes[chain[0]].Pos) < 0) chain.Reverse();
            return chain;
        }

        /// <summary>Signed area of a polygon (positive when counter-clockwise).</summary>
        public static float SignedArea(IList<Vector2> poly)
        {
            float a = 0f;
            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 p = poly[i], q = poly[(i + 1) % poly.Count];
                a += p.x * q.y - q.x * p.y;
            }
            return a * 0.5f;
        }

        /// <summary>
        /// The ring's centre: the centroid of its polygon (the centre of its area), or the average of its corners when
        /// the polygon is (nearly) flat or crosses itself.
        /// </summary>
        public static Vector2 Centre(IList<Vector2> poly)
        {
            Vector2 mean = Vector2.zero;
            foreach (Vector2 p in poly) mean += p;
            mean /= Mathf.Max(1, poly.Count);
            float area = SignedArea(poly);
            float perimeter = 0f;
            for (int i = 0; i < poly.Count; i++) perimeter += Vector2.Distance(poly[i], poly[(i + 1) % poly.Count]);
            // Flat or self-crossing shapes have little area for their outline.
            if (Mathf.Abs(area) < 0.02f * perimeter * perimeter) return mean;
            float cx = 0f, cy = 0f;
            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 p = poly[i], q = poly[(i + 1) % poly.Count];
                float cross = p.x * q.y - q.x * p.y;
                cx += (p.x + q.x) * cross;
                cy += (p.y + q.y) * cross;
            }
            return new Vector2(cx / (6f * area), cy / (6f * area));
        }

        /// <summary>The sun beam's stats: the ring's obelisks averaged; burn is <see cref="BeamBurn"/> plus the cards' burn.</summary>
        public static RitualBeamStats Average(IList<RitualMemberStats> members)
        {
            var s = new RitualBeamStats();
            int n = members.Count;
            if (n == 0) return s;
            float dmg = 0f, hm = 0f, am = 0f, sm = 0f, rate = 0f, mana = 0f;
            foreach (RitualMemberStats m in members)
            {
                dmg += m.Damage; hm += m.HealthMult; am += m.ArmorMult; sm += m.ShieldMult;
                s.Slow += m.Slow; s.Bleed += m.Bleed; s.Burn += m.Burn; s.Poison += m.Poison; s.Crit += m.Crit; s.Stun += m.Stun;
                rate += m.Interval > 0f ? 1f / m.Interval : 0f;
                mana += m.Damage * m.ManaRate;
            }
            s.Damage = dmg / n;
            s.HealthMult = Mathf.RoundToInt(hm / n); s.ArmorMult = Mathf.RoundToInt(am / n); s.ShieldMult = Mathf.RoundToInt(sm / n);
            s.Slow /= n; s.Bleed /= n; s.Poison /= n; s.Crit /= n; s.Stun /= n;
            // The members' burn includes the Sun Obelisk's own bonus; the beam replaces it with its own.
            s.Burn = s.Burn / n - ObeliskBurn + BeamBurn;
            s.Rate = rate / n;
            s.Mana = mana / n;
            return s;
        }

        /// <summary>
        /// Focus Fire on the sun beam: like the vanilla Obelisk's bonus for staying on one target, a monster that has
        /// been in the beam for <paramref name="seconds"/> takes +floor(seconds × multiplier), at most the beam's damage.
        /// </summary>
        public static int FocusBonus(float seconds, float multiplier, int damage) =>
            Mathf.Clamp(Mathf.FloorToInt(seconds * multiplier), 0, Mathf.Max(0, damage));

        /// <summary>Whole units of a fractional amount, carrying the remainder (damage and mana per hit).</summary>
        public static int Take(ref float carry, float amount)
        {
            carry += amount;
            int whole = Mathf.FloorToInt(carry + 1e-4f);
            carry -= whole;
            return whole;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

// Pure mechanics of Ley Line Network (docs/ascension-plan.md §3, phase 3). No Unity engine calls: only Mathf, so the
// offline tests (tests/TreasureRun.Tests) run them directly.
namespace TreasureRun.Ascensions.Phase3
{
    /// <summary>A grid cell with its height step (round(y × 3)). Neighbours link at any height difference.</summary>
    public struct LeyCell
    {
        public int X, Z, Level;
        public LeyCell(int x, int z, int level) { X = x; Z = z; Level = level; }
        public bool Touches(LeyCell o) => Mathf.Abs(X - o.X) + Mathf.Abs(Z - o.Z) == 1;
    }

    public enum LeyNeighbourKind { None, Siphon, Crystal }

    /// <summary>What sits next to a siphon in one direction, and at which height step.</summary>
    public struct LeyNeighbour
    {
        public LeyNeighbourKind Kind;
        public int Level;
        public LeyNeighbour(LeyNeighbourKind kind, int level) { Kind = kind; Level = level; }
    }

    /// <summary>The pipe towards one neighbour: <see cref="Rise"/> steps above the siphon (on its riser), or for a lower
    /// crystal an intake that drops <see cref="Drop"/> steps down to it.</summary>
    public struct LeyArm
    {
        public LeyNeighbourKind Kind;
        public int Rise, Drop;
    }

    /// <summary>The pipes of one siphon: an arm per direction, the tee fitting levels and the riser height (in steps).</summary>
    public sealed class LeyPlan
    {
        public LeyArm[] Arms;
        public List<int> Tees;
        public int Riser;
    }

    /// <summary>One connected group of siphons and the crystals next to any of them.</summary>
    public sealed class LeyNetworkInfo
    {
        public readonly List<int> Siphons = new List<int>();
        public readonly List<int> Crystals = new List<int>();
        /// <summary>Siphons that touch a crystal themselves: only these gather (the others are connectors).</summary>
        public int Taps;
        /// <summary>Mana per second each tap gathers.</summary>
        public int IncomePerTap;
        public int Income => IncomePerTap * Taps;
    }

    /// <summary>
    /// Ley Line Network (Mana Siphon, Legendary): siphons next to each other (4 neighbours, any height) form a network.
    /// Only siphons that touch a crystal themselves ("taps") gather; the others just connect. Each tap gathers its rate ×
    /// (1 + ½ per further crystal in the network), rounded down: with C crystals, rate × (C + 1) / 2. Linking distant
    /// crystals raises every tap; adding siphons doesn't (at most 4 taps per crystal).
    /// </summary>
    public static class LeyLineLogic
    {
        public sealed class Result
        {
            public readonly List<LeyNetworkInfo> Networks = new List<LeyNetworkInfo>();
            /// <summary>Network index per siphon.</summary>
            public int[] NetworkOf;
            /// <summary>Mana per second per siphon (0 for connectors).</summary>
            public int[] Income;
            /// <summary>Whether a siphon touches a crystal itself (a tap) or only connects.</summary>
            public bool[] IsTap;
            /// <summary>
            /// Steps to the nearest siphon that touches a crystal (0 there; −1 in a network without crystals). Mana flows
            /// from lower to higher values.
            /// </summary>
            public int[] Distance;
        }

        public static Result Solve(IReadOnlyList<LeyCell> siphons, IReadOnlyList<LeyCell> crystals, int ratePerCrystal = 1)
        {
            int n = siphons.Count;
            var r = new Result { NetworkOf = new int[n], Income = new int[n], Distance = new int[n], IsTap = new bool[n] };
            for (int i = 0; i < n; i++) { r.NetworkOf[i] = -1; r.Distance[i] = -1; }

            var neighbours = new List<int>[n];
            for (int i = 0; i < n; i++)
            {
                neighbours[i] = new List<int>();
                for (int j = 0; j < n; j++)
                    if (i != j && siphons[i].Touches(siphons[j])) neighbours[i].Add(j);
            }

            var queue = new Queue<int>();
            for (int start = 0; start < n; start++)
            {
                if (r.NetworkOf[start] >= 0) continue;
                var net = new LeyNetworkInfo();
                int id = r.Networks.Count;
                r.Networks.Add(net);
                r.NetworkOf[start] = id;
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    int s = queue.Dequeue();
                    net.Siphons.Add(s);
                    foreach (int t in neighbours[s])
                        if (r.NetworkOf[t] < 0) { r.NetworkOf[t] = id; queue.Enqueue(t); }
                }
                for (int c = 0; c < crystals.Count; c++)
                    foreach (int s in net.Siphons)
                        if (siphons[s].Touches(crystals[c])) { net.Crystals.Add(c); break; }
                net.IncomePerTap = TapIncome(net.Crystals.Count, ratePerCrystal);
                foreach (int s in net.Siphons)
                {
                    foreach (int c in net.Crystals)
                        if (siphons[s].Touches(crystals[c])) { r.IsTap[s] = true; break; }
                    if (!r.IsTap[s]) continue;
                    net.Taps++;
                    r.Income[s] = net.IncomePerTap;
                }

                // Flow: breadth-first from the siphons that touch a crystal.
                foreach (int s in net.Siphons)
                    foreach (int c in net.Crystals)
                        if (siphons[s].Touches(crystals[c])) { r.Distance[s] = 0; queue.Enqueue(s); break; }
                while (queue.Count > 0)
                {
                    int s = queue.Dequeue();
                    foreach (int t in neighbours[s])
                        if (r.Distance[t] < 0) { r.Distance[t] = r.Distance[s] + 1; queue.Enqueue(t); }
                }
            }
            return r;
        }

        /// <summary>
        /// Mana per second of one tap in a network with <paramref name="crystals"/> crystals: rate × (1 + ½ per further
        /// crystal), rounded down (the game only adds whole mana).
        /// </summary>
        public static int TapIncome(int crystals, int rate = 1) =>
            crystals <= 0 ? 0 : Mathf.Max(0, rate) * (crystals + 1) / 2;

        /// <summary>
        /// How one siphon draws its pipe towards each of its 4 neighbours. Between nodes of different height the pipe runs
        /// at the higher one's level: the lower node carries it, the higher one just has its normal arm. A siphon below a
        /// neighbour therefore gets a riser up to its highest such neighbour, with a tee fitting at every level where an
        /// arm branches off (one fitting per level, however many arms leave there). A crystal below a siphon can't carry
        /// anything, so that siphon's intake drops down to the crystal's level instead.
        /// </summary>
        public static LeyPlan Plan(LeyCell self, LeyNeighbour[] neighbours)
        {
            var plan = new LeyPlan { Arms = new LeyArm[4] };
            var tees = new SortedSet<int>();
            for (int d = 0; d < 4 && d < neighbours.Length; d++)
            {
                LeyNeighbour n = neighbours[d];
                if (n.Kind == LeyNeighbourKind.None) continue;
                int diff = n.Level - self.Level;
                var arm = new LeyArm { Kind = n.Kind, Rise = Mathf.Max(0, diff), Drop = n.Kind == LeyNeighbourKind.Crystal ? Mathf.Max(0, -diff) : 0 };
                plan.Arms[d] = arm;
                if (arm.Rise > 0) tees.Add(arm.Rise);
            }
            plan.Tees = new List<int>(tees);
            plan.Riser = plan.Tees.Count > 0 ? plan.Tees[plan.Tees.Count - 1] : 0;
            return plan;
        }

        /// <summary>Directions to the 4 neighbours, in the order the models are rotated: +z, +x, −z, −x (0°, 90°, 180°, 270°).</summary>
        public static readonly (int dx, int dz)[] Directions = { (0, 1), (1, 0), (0, -1), (-1, 0) };
    }
}

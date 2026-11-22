using System;
using System.Collections.Generic;
using TreasureRun.Core;
using UnityEngine;

namespace TreasureRun.Pois
{
    /// <summary>Path exits of a map section, relative to its entry (the side towards the castle).</summary>
    [Flags]
    public enum SectionExits
    {
        None = 0,
        Left = 1,
        Top = 2,
        Right = 4,
        All = Left | Top | Right,
    }

    /// <summary>What a cell of a section is.</summary>
    public enum SectionCell
    {
        /// <summary>Grass at <see cref="SectionLayout.Height"/> (buildable).</summary>
        Grass,
        /// <summary>Open path (height 0).</summary>
        Path,
        /// <summary>Path under a roof the POI builds (a tunnel): monsters walk through, nothing can be built.</summary>
        Covered,
        /// <summary>Reserved for a POI structure: ground at its height, but not path and not buildable (the structure blocks it).</summary>
        Reserved,
    }

    /// <summary>A waypoint of a section in local coordinates; <see cref="Next"/> points towards the entry (-1: the entry).</summary>
    public sealed class SectionWaypoint
    {
        public int X, Z;
        public int Next = -1;
        /// <summary>"last" (the entry), "left", "top", "right" (the exits) or null (inside).</summary>
        public string Role;
        public Vector3 Local => new Vector3(X - SectionLayout.Half, 0f, Z - SectionLayout.Half);
    }

    /// <summary>
    /// A 7x7 map section (one tile). Cell (x, z) has its centre at local position (x - 3, 0, z - 3); local -z is the entry
    /// (vanilla TerrainTile "last"), -x the left exit, +z the top exit, +x the right exit, all at edge midpoints.
    /// Heights are in thirds of a world unit, like vanilla terrain: path 0, plain grass 1, hills 2-4.
    /// </summary>
    public sealed class SectionLayout
    {
        public const int Size = 7, Half = 3;
        public readonly SectionCell[,] Cells = new SectionCell[Size, Size];
        public readonly int[,] Height = new int[Size, Size];
        public readonly List<SectionWaypoint> Waypoints = new List<SectionWaypoint>();
        public SectionExits Exits;

        public static bool In(int x, int z) => x >= 0 && z >= 0 && x < Size && z < Size;
        public bool IsPath(int x, int z) => In(x, z) && (Cells[x, z] == SectionCell.Path || Cells[x, z] == SectionCell.Covered);
        public bool IsGrass(int x, int z) => In(x, z) && Cells[x, z] == SectionCell.Grass;
        /// <summary>Grass or a reserved cell: both have ground at their height (reserved cells are blocked by their structure).</summary>
        public bool HasGround(int x, int z) => In(x, z) && (Cells[x, z] == SectionCell.Grass || Cells[x, z] == SectionCell.Reserved);

        public static (int x, int z) Entry => (Half, 0);

        public static (int x, int z) ExitCell(SectionExits e)
        {
            switch (e)
            {
                case SectionExits.Left: return (0, Half);
                case SectionExits.Top: return (Half, Size - 1);
                case SectionExits.Right: return (Size - 1, Half);
                default: throw new ArgumentException("one exit expected", nameof(e));
            }
        }

        public static IEnumerable<SectionExits> Each(SectionExits exits)
        {
            foreach (SectionExits e in new[] { SectionExits.Left, SectionExits.Top, SectionExits.Right })
                if ((exits & e) != 0) yield return e;
        }

        /// <summary>The entry waypoint (vanilla "last").</summary>
        public SectionWaypoint Last => Waypoints.Find(w => w.Role == "last");
    }

    /// <summary>
    /// How a POI shapes its section on top of the generic generator: the path hub (where all routes meet), cells it
    /// reserves, its own terrain, and how many random hills decorate the rest.
    /// </summary>
    public class SectionStyle
    {
        /// <summary>Cell where every route meets before running to the entry (default: the centre).</summary>
        public (int x, int z) Hub = (SectionLayout.Half, SectionLayout.Half);
        /// <summary>Random hill blobs on free grass (vanilla-like decoration).</summary>
        public int MinHills = 1, MaxHills = 3;

        /// <summary>Cells reserved before routing (routes go around them).</summary>
        public virtual void Reserve(SectionLayout layout) { }
        /// <summary>The POI's own terrain, after the paths are laid.</summary>
        public virtual void Shape(SectionLayout layout, IRng rng) { }
    }

    /// <summary>Builds a section for any combination of exits: paths, waypoints, terrain (pure, offline-tested).</summary>
    public static class PoiSectionLogic
    {
        private static readonly (int dx, int dz)[] Dirs = { (1, 0), (-1, 0), (0, 1), (0, -1) };

        public static SectionLayout Generate(SectionExits exits, SectionStyle style, IRng rng)
        {
            if (exits == SectionExits.None) throw new ArgumentException("a section needs at least one exit (dead ends stay vanilla)");
            style = style ?? new SectionStyle();
            var l = new SectionLayout { Exits = exits };
            for (int x = 0; x < SectionLayout.Size; x++)
                for (int z = 0; z < SectionLayout.Size; z++)
                {
                    l.Cells[x, z] = SectionCell.Grass;
                    l.Height[x, z] = 1;
                }
            style.Reserve(l);

            // Routes: entry -> hub, then hub -> every exit. Each route is a cell list; waypoints are its corners.
            var hub = style.Hub;
            List<(int, int)> trunk = Route(l, SectionLayout.Entry, hub)
                                     ?? throw new InvalidOperationException("no route from the entry to the hub");
            var routes = new List<(SectionExits exit, List<(int, int)> cells)>();
            foreach (SectionExits e in SectionLayout.Each(exits))
                routes.Add((e, Route(l, hub, SectionLayout.ExitCell(e)) ?? throw new InvalidOperationException($"no route to the {e} exit")));

            foreach (var c in trunk) MarkPath(l, c);
            foreach (var r in routes) foreach (var c in r.cells) MarkPath(l, c);

            // Waypoints point towards the entry: exit -> ... -> hub -> ... -> entry.
            var index = new Dictionary<(int, int), int>();
            int Node((int x, int z) c, string role)
            {
                if (index.TryGetValue(c, out int i)) { if (role != null) l.Waypoints[i].Role = role; return i; }
                l.Waypoints.Add(new SectionWaypoint { X = c.x, Z = c.z, Role = role });
                index[c] = l.Waypoints.Count - 1;
                return l.Waypoints.Count - 1;
            }
            List<(int, int)> trunkCorners = Corners(trunk);
            int prev = -1;
            foreach (var c in trunkCorners) // entry first
            {
                int n = Node(c, c.Equals(SectionLayout.Entry) ? "last" : null);
                if (prev >= 0 && l.Waypoints[n].Next < 0 && n != prev) l.Waypoints[n].Next = prev;
                prev = n;
            }
            int hubNode = index[hub];
            foreach (var (exit, cells) in routes)
            {
                List<(int, int)> corners = Corners(cells); // hub first
                int toward = hubNode;
                for (int i = 1; i < corners.Count; i++)
                {
                    int n = Node(corners[i], i == corners.Count - 1 ? exit.ToString().ToLowerInvariant() : null);
                    if (l.Waypoints[n].Next < 0 && n != toward) l.Waypoints[n].Next = toward;
                    toward = n;
                }
            }

            style.Shape(l, rng);
            AddHills(l, rng, rng.Range(style.MinHills, style.MaxHills + 1));
            return l;
        }

        private static void MarkPath(SectionLayout l, (int x, int z) c)
        {
            if (l.Cells[c.x, c.z] != SectionCell.Covered) l.Cells[c.x, c.z] = SectionCell.Path;
            l.Height[c.x, c.z] = 0;
        }

        /// <summary>The cells of a route where it turns (plus both ends), in order.</summary>
        public static List<(int, int)> Corners(List<(int x, int z)> cells)
        {
            var result = new List<(int, int)>();
            for (int i = 0; i < cells.Count; i++)
            {
                if (i == 0 || i == cells.Count - 1) { result.Add(cells[i]); continue; }
                int dx0 = cells[i].x - cells[i - 1].x, dz0 = cells[i].z - cells[i - 1].z;
                int dx1 = cells[i + 1].x - cells[i].x, dz1 = cells[i + 1].z - cells[i].z;
                if (dx0 != dx1 || dz0 != dz1) result.Add(cells[i]);
            }
            return result;
        }

        /// <summary>
        /// Shortest 4-connected route that avoids reserved cells and keeps off the edge rows (except at its ends), with a
        /// cost for every turn so routes stay straight. Joins existing path freely.
        /// </summary>
        public static List<(int x, int z)> Route(SectionLayout l, (int x, int z) from, (int x, int z) to)
        {
            if (from.Equals(to)) return new List<(int, int)> { from };
            const int n = SectionLayout.Size;
            var cost = new float[n, n, 4];
            var back = new (int x, int z, int d)[n, n, 4];
            for (int x = 0; x < n; x++) for (int z = 0; z < n; z++) for (int d = 0; d < 4; d++) cost[x, z, d] = float.MaxValue;
            var open = new List<(float c, int x, int z, int d)>();
            for (int d = 0; d < 4; d++) { cost[from.x, from.z, d] = 0; open.Add((0, from.x, from.z, d)); back[from.x, from.z, d] = (-1, -1, -1); }
            while (open.Count > 0)
            {
                int best = 0;
                for (int i = 1; i < open.Count; i++) if (open[i].c < open[best].c) best = i;
                var cur = open[best];
                open.RemoveAt(best);
                if (cur.c > cost[cur.x, cur.z, cur.d]) continue;
                if (cur.x == to.x && cur.z == to.z)
                {
                    var path = new List<(int, int)>();
                    (int x, int z, int d) s = (cur.x, cur.z, cur.d);
                    while (s.x >= 0) { path.Add((s.x, s.z)); s = back[s.x, s.z, s.d]; }
                    path.Reverse();
                    return path;
                }
                for (int d = 0; d < 4; d++)
                {
                    int nx = cur.x + Dirs[d].dx, nz = cur.z + Dirs[d].dz;
                    if (!SectionLayout.In(nx, nz) || l.Cells[nx, nz] == SectionCell.Reserved) continue;
                    bool edge = nx == 0 || nz == 0 || nx == n - 1 || nz == n - 1;
                    if (edge && !(nx == to.x && nz == to.z) && !(nx == from.x && nz == from.z)) continue;
                    float c = cur.c + (l.IsPath(nx, nz) ? 0.6f : 1f) + (d != cur.d && !(cur.x == from.x && cur.z == from.z) ? 0.75f : 0f);
                    if (c >= cost[nx, nz, d]) continue;
                    cost[nx, nz, d] = c;
                    back[nx, nz, d] = (cur.x, cur.z, cur.d);
                    open.Add((c, nx, nz, d));
                }
            }
            return null;
        }

        /// <summary>Random hill blobs (height 2-3) on plain grass, like vanilla tiles.</summary>
        public static void AddHills(SectionLayout l, IRng rng, int count)
        {
            for (int k = 0; k < count; k++)
            {
                for (int attempt = 0; attempt < 12; attempt++)
                {
                    int x = rng.Range(0, SectionLayout.Size), z = rng.Range(0, SectionLayout.Size);
                    if (!l.IsGrass(x, z) || l.Height[x, z] != 1) continue;
                    int h = rng.Range(2, 4);
                    int w = rng.Range(1, 3), d = rng.Range(1, 3);
                    for (int ix = x; ix < x + w; ix++)
                        for (int iz = z; iz < z + d; iz++)
                            if (l.IsGrass(ix, iz) && l.Height[ix, iz] == 1) l.Height[ix, iz] = h;
                    break;
                }
            }
        }

        /// <summary>Checks a layout: every exit reaches the entry through path cells, waypoints step along path only.</summary>
        public static string Validate(SectionLayout l)
        {
            SectionWaypoint last = l.Last;
            if (last == null || (last.X, last.Z) != SectionLayout.Entry) return "no entry waypoint";
            if (last.Next != -1) return "the entry must be the end of the section";
            foreach (SectionExits e in SectionLayout.Each(l.Exits))
            {
                var cell = SectionLayout.ExitCell(e);
                SectionWaypoint w = l.Waypoints.Find(p => p.Role == e.ToString().ToLowerInvariant());
                if (w == null || (w.X, w.Z) != cell) return $"no waypoint at the {e} exit";
                int steps = 0;
                while (w.Next >= 0)
                {
                    SectionWaypoint n = l.Waypoints[w.Next];
                    if (w.X != n.X && w.Z != n.Z) return "diagonal waypoint step";
                    for (int x = Math.Min(w.X, n.X); x <= Math.Max(w.X, n.X); x++)
                        for (int z = Math.Min(w.Z, n.Z); z <= Math.Max(w.Z, n.Z); z++)
                            if (!l.IsPath(x, z)) return $"waypoint step crosses a non-path cell ({x},{z})";
                    w = n;
                    if (++steps > 50) return "waypoint loop";
                }
                if (w != last) return $"the {e} exit does not reach the entry";
            }
            for (int x = 0; x < SectionLayout.Size; x++)
                for (int z = 0; z < SectionLayout.Size; z++)
                {
                    bool edge = x == 0 || z == 0 || x == SectionLayout.Size - 1 || z == SectionLayout.Size - 1;
                    if (!edge || !l.IsPath(x, z)) continue;
                    bool open = (x, z) == SectionLayout.Entry;
                    foreach (SectionExits e in SectionLayout.Each(l.Exits)) open |= SectionLayout.ExitCell(e) == (x, z);
                    if (!open) return $"path touches the edge outside an opening ({x},{z})";
                }
            return null;
        }
    }

    /// <summary>
    /// The grass surface of a section as quads (vanilla-like stepped blocks): a top per grass or reserved cell at its
    /// height and a side wherever a neighbour is lower. Path and tunnel cells are holes: the tile's sand Ground cube shows
    /// there (its top is at <see cref="Floor"/>).
    /// </summary>
    public static class SectionMeshLogic
    {
        public const float Floor = -0.05f;

        public static float Top(SectionLayout l, int x, int z) =>
            l.HasGround(x, z) ? l.Height[x, z] / 3f : Floor;

        public static void Build(SectionLayout l, List<Vector3> verts, List<Vector3> normals, List<int> tris)
        {
            for (int x = 0; x < SectionLayout.Size; x++)
                for (int z = 0; z < SectionLayout.Size; z++)
                {
                    if (!l.HasGround(x, z)) continue;
                    float y = Top(l, x, z);
                    float x0 = x - SectionLayout.Half - 0.5f, x1 = x0 + 1f, z0 = z - SectionLayout.Half - 0.5f, z1 = z0 + 1f;
                    Quad(verts, normals, tris, new Vector3(x0, y, z0), new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0), Vector3.up);
                    Side(l, verts, normals, tris, x + 1, z, y, new Vector3(x1, 0, z0), new Vector3(x1, 0, z1), Vector3.right);
                    Side(l, verts, normals, tris, x - 1, z, y, new Vector3(x0, 0, z1), new Vector3(x0, 0, z0), Vector3.left);
                    Side(l, verts, normals, tris, x, z + 1, y, new Vector3(x1, 0, z1), new Vector3(x0, 0, z1), Vector3.forward);
                    Side(l, verts, normals, tris, x, z - 1, y, new Vector3(x0, 0, z0), new Vector3(x1, 0, z0), Vector3.back);
                }
        }

        private static void Side(SectionLayout l, List<Vector3> verts, List<Vector3> normals, List<int> tris, int nx, int nz, float top,
                                 Vector3 a, Vector3 b, Vector3 normal)
        {
            float low = SectionLayout.In(nx, nz) ? Top(l, nx, nz) : Floor;
            if (low >= top - 1e-4f) return;
            Quad(verts, normals, tris, new Vector3(a.x, low, a.z), new Vector3(a.x, top, a.z), new Vector3(b.x, top, b.z), new Vector3(b.x, low, b.z), normal);
        }

        /// <summary>A flat quad facing <paramref name="normal"/> (Unity: clockwise seen from the front).</summary>
        public static void Quad(List<Vector3> verts, List<Vector3> normals, List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0f) { Vector3 t = b; b = d; d = t; }
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            for (int k = 0; k < 4; k++) normals.Add(normal);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }
    }
}

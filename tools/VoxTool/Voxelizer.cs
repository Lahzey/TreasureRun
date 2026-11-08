using System;
using System.Collections.Generic;
using System.Linq;
using TreasureRun.Vox;

namespace VoxTool
{
    /// <summary>
    /// Rebuilds a voxel model from a MagicaVoxel-exported mesh: every face is axis-aligned and lies on the voxel grid,
    /// so each face cell marks the voxel behind it. Hollow interiors are filled afterwards.
    /// </summary>
    public static class Voxelizer
    {
        public sealed class Result
        {
            public VoxModel Model;
            public float VoxelSize;
            public float[] Origin; // Unity mesh-local min corner
            public int Conflicts;
        }

        public static Result Convert(MeshData mesh, Image palette, string name)
        {
            float s = InferVoxelSize(mesh.Positions);
            var min = new float[3];
            var max = new float[3];
            for (int a = 0; a < 3; a++)
            {
                min[a] = mesh.Positions.Min(p => p[a]);
                max[a] = mesh.Positions.Max(p => p[a]);
            }
            int nx = Math.Max(1, (int)Math.Round((max[0] - min[0]) / s));
            int ny = Math.Max(1, (int)Math.Round((max[1] - min[1]) / s));
            int nz = Math.Max(1, (int)Math.Round((max[2] - min[2]) / s));
            if (nx > 256 || ny > 256 || nz > 256) throw new InvalidOperationException($"Model too large for .vox: {nx}x{ny}x{nz}");

            var grid = new uint[nx, ny, nz]; // Unity axes, RGBA (0 = empty)
            int conflicts = 0;

            for (int t = 0; t + 2 < mesh.Triangles.Length; t += 3)
            {
                int i0 = mesh.Triangles[t], i1 = mesh.Triangles[t + 1], i2 = mesh.Triangles[t + 2];
                Vec3 a = mesh.Positions[i0], b = mesh.Positions[i1], c = mesh.Positions[i2];

                Vec3 n = mesh.Normals != null
                    ? new Vec3(mesh.Normals[i0].X + mesh.Normals[i1].X + mesh.Normals[i2].X,
                               mesh.Normals[i0].Y + mesh.Normals[i1].Y + mesh.Normals[i2].Y,
                               mesh.Normals[i0].Z + mesh.Normals[i1].Z + mesh.Normals[i2].Z)
                    : Vec3.Cross(b - a, c - a);
                int axis = Math.Abs(n.X) >= Math.Abs(n.Y) && Math.Abs(n.X) >= Math.Abs(n.Z) ? 0 : Math.Abs(n.Y) >= Math.Abs(n.Z) ? 1 : 2;
                int sign = n[axis] >= 0 ? 1 : -1;

                int layer = (int)Math.Round((a[axis] - min[axis]) / s) + (sign > 0 ? -1 : 0);
                int u = (axis + 1) % 3, v = (axis + 2) % 3;

                uint color = 0xFFFF00FFu; // magenta = missing texture
                if (mesh.UVs != null && palette != null)
                {
                    float tu = (mesh.UVs[i0][0] + mesh.UVs[i1][0] + mesh.UVs[i2][0]) / 3f;
                    float tv = (mesh.UVs[i0][1] + mesh.UVs[i1][1] + mesh.UVs[i2][1]) / 3f;
                    int px = (int)Math.Floor(tu * palette.Width);
                    int py = palette.Height - 1 - (int)Math.Floor(tv * palette.Height); // UV v=0 is the bottom row
                    color = palette.Get(px, py) | 0xFF000000u;
                }

                // 2D triangle in grid units on the (u, v) plane.
                float au = (a[u] - min[u]) / s, av = (a[v] - min[v]) / s;
                float bu = (b[u] - min[u]) / s, bv = (b[v] - min[v]) / s;
                float cu = (c[u] - min[u]) / s, cv = (c[v] - min[v]) / s;
                int u0 = (int)Math.Floor(Math.Min(au, Math.Min(bu, cu)) + 1e-3), u1 = (int)Math.Ceiling(Math.Max(au, Math.Max(bu, cu)) - 1e-3);
                int v0 = (int)Math.Floor(Math.Min(av, Math.Min(bv, cv)) + 1e-3), v1 = (int)Math.Ceiling(Math.Max(av, Math.Max(bv, cv)) - 1e-3);

                for (int gu = u0; gu < u1; gu++)
                    for (int gv = v0; gv < v1; gv++)
                    {
                        if (!InTriangle(gu + 0.5f, gv + 0.5f, au, av, bu, bv, cu, cv)) continue;
                        var idx = new int[3];
                        idx[axis] = layer; idx[u] = gu; idx[v] = gv;
                        if (idx[0] < 0 || idx[1] < 0 || idx[2] < 0 || idx[0] >= nx || idx[1] >= ny || idx[2] >= nz) continue;
                        uint existing = grid[idx[0], idx[1], idx[2]];
                        if (existing == 0) grid[idx[0], idx[1], idx[2]] = color;
                        else if (existing != color) conflicts++;
                    }
            }

            FillInterior(grid, nx, ny, nz);

            // Unity (x, y, z) -> vox (x, z, y)
            var model = new VoxModel(nx, nz, ny) { Name = name };
            var pal = new PaletteBuilder();
            for (int x = 0; x < nx; x++)
                for (int y = 0; y < ny; y++)
                    for (int z = 0; z < nz; z++)
                        if (grid[x, y, z] != 0) model.Voxels[x, z, y] = pal.Get(grid[x, y, z]);
            model.Palette = pal.Palette;

            return new Result { Model = model, VoxelSize = s, Origin = new[] { min[0], min[1], min[2] }, Conflicts = conflicts };
        }

        private static bool InTriangle(float px, float py, float ax, float ay, float bx, float by, float cx, float cy)
        {
            float d1 = (px - bx) * (ay - by) - (ax - bx) * (py - by);
            float d2 = (px - cx) * (by - cy) - (bx - cx) * (py - cy);
            float d3 = (px - ax) * (cy - ay) - (cx - ax) * (py - ay);
            bool neg = d1 < -1e-4f || d2 < -1e-4f || d3 < -1e-4f;
            bool pos = d1 > 1e-4f || d2 > 1e-4f || d3 > 1e-4f;
            return !(neg && pos);
        }

        /// <summary>Smallest grid step that all vertex coordinates sit on (e.g. 0.1 for buildings).</summary>
        public static float InferVoxelSize(Vec3[] positions)
        {
            var diffs = new List<float>();
            for (int a = 0; a < 3; a++)
            {
                var vals = positions.Select(p => (float)Math.Round(p[a], 4)).Distinct().OrderBy(x => x).ToArray();
                for (int i = 1; i < vals.Length; i++) if (vals[i] - vals[i - 1] > 1e-3f) diffs.Add(vals[i] - vals[i - 1]);
            }
            if (diffs.Count == 0) return 0.1f;
            float s = diffs.Min();
            // Refine: the step must divide every difference.
            for (int attempt = 0; attempt < 4; attempt++)
            {
                float step = s;
                if (diffs.All(d => Math.Abs(d / step - Math.Round(d / step)) < 0.05f)) return (float)Math.Round(step, 5);
                s /= 2f;
            }
            return (float)Math.Round(diffs.Min(), 5);
        }

        /// <summary>Empty cells not reachable from outside are interior: fill them with a neighbouring colour.</summary>
        private static void FillInterior(uint[,,] g, int nx, int ny, int nz)
        {
            var outside = new bool[nx + 2, ny + 2, nz + 2];
            var q = new Queue<(int, int, int)>();
            q.Enqueue((0, 0, 0));
            outside[0, 0, 0] = true;
            int[] dx = { 1, -1, 0, 0, 0, 0 }, dy = { 0, 0, 1, -1, 0, 0 }, dz = { 0, 0, 0, 0, 1, -1 };
            while (q.Count > 0)
            {
                var (x, y, z) = q.Dequeue();
                for (int k = 0; k < 6; k++)
                {
                    int X = x + dx[k], Y = y + dy[k], Z = z + dz[k];
                    if (X < 0 || Y < 0 || Z < 0 || X > nx + 1 || Y > ny + 1 || Z > nz + 1 || outside[X, Y, Z]) continue;
                    bool inGrid = X >= 1 && Y >= 1 && Z >= 1 && X <= nx && Y <= ny && Z <= nz;
                    if (inGrid && g[X - 1, Y - 1, Z - 1] != 0) continue;
                    outside[X, Y, Z] = true;
                    q.Enqueue((X, Y, Z));
                }
            }

            // Propagate colours from filled cells into interior cells.
            var frontier = new Queue<(int, int, int)>();
            for (int x = 0; x < nx; x++)
                for (int y = 0; y < ny; y++)
                    for (int z = 0; z < nz; z++)
                        if (g[x, y, z] != 0) frontier.Enqueue((x, y, z));
            while (frontier.Count > 0)
            {
                var (x, y, z) = frontier.Dequeue();
                for (int k = 0; k < 6; k++)
                {
                    int X = x + dx[k], Y = y + dy[k], Z = z + dz[k];
                    if (X < 0 || Y < 0 || Z < 0 || X >= nx || Y >= ny || Z >= nz) continue;
                    if (g[X, Y, Z] != 0 || outside[X + 1, Y + 1, Z + 1]) continue;
                    g[X, Y, Z] = g[x, y, z];
                    frontier.Enqueue((X, Y, Z));
                }
            }
        }
    }
}

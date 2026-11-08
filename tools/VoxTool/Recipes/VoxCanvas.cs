using System;
using System.Collections.Generic;
using TreasureRun.Vox;

namespace VoxTool.Recipes
{
    /// <summary>
    /// Drawing surface for procedural voxel models (MagicaVoxel axes: X/Y horizontal, Z up).
    /// Colours are RGB(A) packed with <see cref="C"/>. All coordinate ranges are inclusive.
    /// </summary>
    public sealed class VoxCanvas
    {
        public readonly int SizeX, SizeY, SizeZ;
        private readonly uint[,,] cells;

        public VoxCanvas(int sx, int sy, int sz)
        {
            SizeX = sx; SizeY = sy; SizeZ = sz;
            cells = new uint[sx, sy, sz];
        }

        public static uint C(byte r, byte g, byte b) => VoxModel.Rgba(r, g, b);

        public bool In(int x, int y, int z) => x >= 0 && y >= 0 && z >= 0 && x < SizeX && y < SizeY && z < SizeZ;
        public uint Get(int x, int y, int z) => In(x, y, z) ? cells[x, y, z] : 0;
        public void Set(int x, int y, int z, uint c) { if (In(x, y, z)) cells[x, y, z] = c; }
        public void Clear(int x, int y, int z) => Set(x, y, z, 0);

        public void Box(int x0, int y0, int z0, int x1, int y1, int z1, uint c)
        {
            for (int x = Math.Min(x0, x1); x <= Math.Max(x0, x1); x++)
                for (int y = Math.Min(y0, y1); y <= Math.Max(y0, y1); y++)
                    for (int z = Math.Min(z0, z1); z <= Math.Max(z0, z1); z++) Set(x, y, z, c);
        }

        public void ClearBox(int x0, int y0, int z0, int x1, int y1, int z1) => Box(x0, y0, z0, x1, y1, z1, 0);

        /// <summary>Hollow box walls (no top/bottom).</summary>
        public void Walls(int x0, int y0, int z0, int x1, int y1, int z1, uint c)
        {
            Box(x0, y0, z0, x1, y0, z1, c); Box(x0, y1, z0, x1, y1, z1, c);
            Box(x0, y0, z0, x0, y1, z1, c); Box(x1, y0, z0, x1, y1, z1, c);
        }

        /// <summary>Vertical cylinder centred between voxels when the centre has .5 (e.g. 15.5 for a 32-wide grid).</summary>
        public void Cylinder(float cx, float cy, int z0, int z1, float r, uint c, bool hollow = false, float wall = 1.2f)
        {
            for (int x = 0; x < SizeX; x++)
                for (int y = 0; y < SizeY; y++)
                {
                    float d = (float)Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    if (d > r || (hollow && d < r - wall)) continue;
                    for (int z = z0; z <= z1; z++) Set(x, y, z, c);
                }
        }

        /// <summary>Cone/taper from radius r0 at z0 to r1 at z1.</summary>
        public void Cone(float cx, float cy, int z0, int z1, float r0, float r1, uint c)
        {
            for (int z = z0; z <= z1; z++)
            {
                float t = z1 == z0 ? 0f : (z - z0) / (float)(z1 - z0);
                Cylinder(cx, cy, z, z, r0 + (r1 - r0) * t, c);
            }
        }

        /// <summary>Recolours existing voxels for which the predicate holds.</summary>
        public void Recolor(Func<int, int, int, uint, bool> where, uint c)
        {
            for (int x = 0; x < SizeX; x++)
                for (int y = 0; y < SizeY; y++)
                    for (int z = 0; z < SizeZ; z++)
                        if (cells[x, y, z] != 0 && where(x, y, z, cells[x, y, z])) cells[x, y, z] = c;
        }

        /// <summary>True if the voxel is filled and at least one horizontal neighbour is empty (visible side surface).</summary>
        public bool IsSideSurface(int x, int y, int z) =>
            Get(x, y, z) != 0 && (Get(x + 1, y, z) == 0 || Get(x - 1, y, z) == 0 || Get(x, y + 1, z) == 0 || Get(x, y - 1, z) == 0);

        /// <summary>Filled sphere / ellipsoid with radii rx, ry, rz.</summary>
        public void Ellipsoid(float cx, float cy, float cz, float rx, float ry, float rz, uint c, Func<int, int, int, bool> where = null)
        {
            for (int x = 0; x < SizeX; x++)
                for (int y = 0; y < SizeY; y++)
                    for (int z = 0; z < SizeZ; z++)
                    {
                        float dx = (x - cx) / rx, dy = (y - cy) / ry, dz = (z - cz) / rz;
                        if (dx * dx + dy * dy + dz * dz <= 1f && (where == null || where(x, y, z))) Set(x, y, z, c);
                    }
        }

        /// <summary>Straight 1-voxel line (3D DDA-ish sampling).</summary>
        public void Line(float x0, float y0, float z0, float x1, float y1, float z1, uint c, float thickness = 0f)
        {
            float len = (float)Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0) + (z1 - z0) * (z1 - z0));
            int steps = Math.Max(1, (int)Math.Ceiling(len * 2));
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float x = x0 + (x1 - x0) * t, y = y0 + (y1 - y0) * t, z = z0 + (z1 - z0) * t;
                if (thickness <= 0f) Set((int)Math.Round(x), (int)Math.Round(y), (int)Math.Round(z), c);
                else
                    for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++) for (int dz = -1; dz <= 1; dz++)
                        if (dx * dx + dy * dy + dz * dz <= thickness * thickness)
                            Set((int)Math.Round(x) + dx, (int)Math.Round(y) + dy, (int)Math.Round(z) + dz, c);
            }
        }

        /// <summary>Copies a model in (optionally rotated by 90° steps around Z), skipping empty voxels.</summary>
        public void Paste(VoxModel m, int ox, int oy, int oz, int quarterTurns = 0)
        {
            for (int x = 0; x < m.SizeX; x++)
                for (int y = 0; y < m.SizeY; y++)
                    for (int z = 0; z < m.SizeZ; z++)
                    {
                        byte c = m.Voxels[x, y, z];
                        if (c == 0) continue;
                        int rx = x, ry = y;
                        for (int k = 0; k < (quarterTurns % 4 + 4) % 4; k++) { int t = rx; rx = m.SizeY - 1 - ry; ry = t; }
                        Set(ox + rx, oy + ry, oz + z, m.Palette[c]);
                    }
        }

        /// <summary>
        /// Lifts the model by <paramref name="height"/> voxels and extends every column that touches the ground down
        /// to the new floor in its own colour. In game the tile's grass covers the lowest ~3.5 voxels around the
        /// castle, so models are designed from z = 0 and then grounded.
        /// </summary>
        public VoxCanvas Grounded(int height = GrassHeight)
        {
            var g = new VoxCanvas(SizeX, SizeY, SizeZ + height);
            for (int x = 0; x < SizeX; x++)
                for (int y = 0; y < SizeY; y++)
                {
                    for (int z = 0; z < SizeZ; z++) g.cells[x, y, z + height] = cells[x, y, z];
                    if (cells[x, y, 0] != 0)
                        for (int z = 0; z < height; z++) g.cells[x, y, z] = cells[x, y, 0];
                }
            return g;
        }

        /// <summary>Turns the model by 180° around the vertical axis (front becomes back).</summary>
        public VoxCanvas Rotated180()
        {
            var r = new VoxCanvas(SizeX, SizeY, SizeZ);
            for (int x = 0; x < SizeX; x++)
                for (int y = 0; y < SizeY; y++)
                    for (int z = 0; z < SizeZ; z++)
                        r.cells[SizeX - 1 - x, SizeY - 1 - y, z] = cells[x, y, z];
            return r;
        }

        /// <summary>Voxels hidden by the grass layer at the castle position (grass top ≈ 0.333 world = 3.5 voxels).</summary>
        public const int GrassHeight = 4;

        public VoxModel ToModel(string name)
        {
            var model = new VoxModel(SizeX, SizeY, SizeZ) { Name = name };
            var pal = new PaletteBuilder();
            for (int x = 0; x < SizeX; x++)
                for (int y = 0; y < SizeY; y++)
                    for (int z = 0; z < SizeZ; z++)
                        if (cells[x, y, z] != 0) model.Voxels[x, y, z] = pal.Get(cells[x, y, z] | 0xFF000000u);
            model.Palette = pal.Palette;
            return model;
        }

        /// <summary>Deterministic pseudo-random value in [0,1) for a coordinate (brick patterns etc.).</summary>
        public static float Hash(int x, int y, int z, int seed = 0)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093 ^ y * 19349663 ^ z * 83492791 ^ seed * 2654435761);
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }
}

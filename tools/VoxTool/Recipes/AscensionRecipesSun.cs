using System;
using System.Collections.Generic;
using TreasureRun.Vox;
using static VoxTool.Recipes.VoxCanvas;

namespace VoxTool.Recipes
{
    /// <summary>
    /// Ritual of the Sun (Obelisk) models. The Sun Obelisk is a stepped sandstone pyramid with gold ledges, a gold sun
    /// disc on each face of its base, gold-capped corner obelisks and a gold crown with four prongs. The crystal prism
    /// that floats above the prongs is its own model on the turret (it aims like the vanilla beam and turns to the sky
    /// in a ring): an eight-faceted bipyramid along the turret's forward axis (+y here) with a gold halo ring round its
    /// middle.
    /// </summary>
    public static partial class AscensionRecipes
    {
        private const string ObeliskBase = "Obelisk/Obelisk_BaseArt_Obelisk2_default";

        private static IEnumerable<(string, Func<VoxModel>)> SunAll()
        {
            yield return ("ascensions/obelisk/sun_obelisk", () => SunPyramid().ToModel("sun_obelisk"));
            yield return ("ascensions/obelisk/sun_prism", () => SunPrism().ToModel("sun_prism"));
            yield return ("ascensions/obelisk/ritual_card", RitualCard);
        }

        internal static readonly uint SandLight = C(206, 170, 112), Sand = C(190, 152, 96), SandMid = C(172, 134, 82),
                                      SandDark = C(140, 104, 62);
        internal static readonly uint SunGold = C(226, 160, 30), SunGoldLight = C(246, 200, 70), SunGoldDeep = C(186, 118, 20),
                                      SunGoldDark = C(140, 84, 14), Amber = C(236, 102, 16);

        /// <summary>Crystal colours, darkest to lightest, and the prism tints of some facets.</summary>
        private static readonly uint[] CrystalRamp = { C(84, 132, 206), C(132, 190, 240), C(196, 232, 255), C(246, 251, 255) };
        private static readonly uint[] PrismTints = { C(196, 168, 255), C(160, 246, 224), C(255, 196, 228), C(255, 240, 170) };

        /// <summary>Pyramid tiers: inset from the 16-wide footprint, first and last layer.</summary>
        private static readonly (int Inset, int Z0, int Z1)[] SunTiers = { (0, 0, 5), (2, 6, 8), (3, 9, 11), (4, 12, 13), (5, 14, 15) };

        /// <summary>Height (voxels of the base model) of the prism's centre: the turret goes there.</summary>
        public const float SunPrismCentre = 21.5f;

        /// <summary>The Sun Obelisk's pyramid (16 x 16, 1/16 tile per voxel in game like the vanilla model).</summary>
        public static VoxCanvas SunPyramid()
        {
            var c = new VoxCanvas(16, 16, 20);
            foreach (var (inset, z0, z1) in SunTiers)
            {
                int lo = inset, hi = 15 - inset;
                for (int x = lo; x <= hi; x++)
                    for (int y = lo; y <= hi; y++)
                        for (int z = z0; z <= z1; z++)
                        {
                            bool rim = x == lo || x == hi || y == lo || y == hi;
                            uint col = Brick(x, y, z, lo, hi);
                            // A gold ledge round the top of every tier; a darker gold inlay up the middle of each face.
                            if (z == z1 && rim) col = (x == lo || x == hi) && (y == lo || y == hi) ? SunGoldLight : SunGold;
                            else if (rim && inset > 0 && Mid(x, y, lo, hi)) col = SunGoldDeep;
                            c.Set(x, y, z, col);
                        }
            }
            // A sun disc with rays on each face of the base tier.
            for (int face = 0; face < 4; face++)
                for (int u = 0; u < 16; u++)
                    for (int z = 1; z <= 4; z++)
                    {
                        uint col = SunEmblem(u, z);
                        if (col == 0) continue;
                        switch (face)
                        {
                            case 0: c.Set(u, 0, z, col); break;
                            case 1: c.Set(15 - u, 15, z, col); break;
                            case 2: c.Set(0, 15 - u, z, col); break;
                            default: c.Set(15, u, z, col); break;
                        }
                    }
            // Gold-capped obelisks on the corners of the base tier.
            foreach (int ox in new[] { 0, 14 })
                foreach (int oy in new[] { 0, 14 })
                {
                    for (int z = 6; z <= 11; z++)
                        for (int x = ox; x <= ox + 1; x++)
                            for (int y = oy; y <= oy + 1; y++) c.Set(x, y, z, Hash(x, y, z, 7) < 0.5f ? SandLight : Sand);
                    c.Box(ox, oy, 12, ox + 1, oy + 1, 12, SunGold);
                    c.Set(ox == 0 ? 0 : 15, oy == 0 ? 0 : 15, 13, SunGoldLight);
                }
            // Gold crown (a small pyramidion) and the four prongs the prism floats over.
            c.Box(6, 6, 16, 9, 9, 16, SunGold);
            c.Box(7, 7, 17, 8, 8, 17, SunGoldLight);
            foreach (int px in new[] { 6, 9 })
                foreach (int py in new[] { 6, 9 })
                {
                    c.Set(px, py, 16, SunGoldDeep);
                    c.Set(px, py, 17, SunGold);
                    c.Set(px, py, 18, SunGold);
                    c.Set(px, py, 19, SunGoldLight);
                }
            return c;
        }

        private static bool Mid(int x, int y, int lo, int hi) =>
            ((x == lo || x == hi) && (y == 7 || y == 8)) || ((y == lo || y == hi) && (x == 7 || x == 8));

        /// <summary>Sandstone blocks: courses one voxel high, blocks four long, offset every other course, each a shade.</summary>
        private static uint Brick(int x, int y, int z, int lo, int hi)
        {
            int u = (y == lo || y == hi) ? x : y;
            int block = (u + (z % 2) * 2) / 4;
            float h = Hash(block, z, (y == lo || y == hi) ? 1 : 2, 31);
            if (z == 0) return SandDark;
            return h < 0.25f ? SandLight : h < 0.7f ? Sand : h < 0.92f ? SandMid : SandDark;
        }

        /// <summary>The sun on a base face (u along the face, z up): a gold disc with an amber heart and eight rays.</summary>
        private static uint SunEmblem(int u, int z)
        {
            float du = u - 7.5f, dz = z - 2.5f;
            if (du * du + dz * dz <= 4.4f) return Math.Abs(du) < 1f && Math.Abs(dz) < 1f ? Amber : SunGold;
            if ((u == 4 || u == 11) && (z == 2 || z == 3)) return SunGoldDeep;
            if ((u == 5 || u == 10) && (z == 1 || z == 4)) return SunGoldDeep;
            return 0;
        }

        /// <summary>
        /// The crystal prism (11 x 15 x 11, its long axis along +y = the turret's forward): an eight-faceted bipyramid,
        /// each facet shaded for light from above and a little behind, its ridges brighter, some facets tinted like light
        /// split by a prism; a gold ring round its middle (a halo when it points at the sky).
        /// </summary>
        public static VoxCanvas SunPrism()
        {
            var c = new VoxCanvas(11, 15, 11);
            const int cx = 5, cy = 7, cz = 5;
            const float r0 = 3.4f, half = 7.4f;
            double lx = -0.3, ly = -0.45, lz = 1.0, ll = Math.Sqrt(lx * lx + ly * ly + lz * lz);
            lx /= ll; ly /= ll; lz /= ll;
            for (int x = 0; x < 11; x++)
                for (int y = 0; y < 15; y++)
                    for (int z = 0; z < 11; z++)
                    {
                        int dx = x - cx, dy = y - cy, dz = z - cz;
                        float r = r0 * (1f - Math.Abs(dy) / half);
                        if (Math.Abs(dx) + Math.Abs(dz) > r + 0.01f) continue;
                        int sx = dx == 0 ? (Hash(x, y, z, 3) < 0.5f ? 1 : -1) : Math.Sign(dx);
                        int sz = dz == 0 ? (Hash(x, y, z, 5) < 0.5f ? 1 : -1) : Math.Sign(dz);
                        int sy = dy == 0 ? 1 : Math.Sign(dy);
                        double nx = sx, ny = sy * r0 / half, nz = sz, nl = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                        double b = (nx * lx + ny * ly + nz * lz) / nl;
                        int step = b < -0.45 ? 0 : b < 0.0 ? 1 : b < 0.45 ? 2 : 3;
                        bool ridge = (dx == 0 || dz == 0) && Math.Abs(dx) + Math.Abs(dz) >= (int)r;
                        if (ridge) step = Math.Min(3, step + 1);
                        uint col = CrystalRamp[step];
                        // Prism dispersion: three of the eight facets carry a faint colour.
                        int facet = (sx > 0 ? 1 : 0) + (sz > 0 ? 2 : 0) + (sy > 0 ? 4 : 0);
                        if (!ridge && (facet == 1 || facet == 4 || facet == 6)) col = Mix(col, PrismTints[facet % 4], 0.35f);
                        if (!ridge && facet == 3) col = Mix(col, PrismTints[3], 0.2f);
                        c.Set(x, y, z, col);
                    }
            // The halo ring round the middle (x/z plane at the centre), touching the crystal at four points.
            for (int x = 0; x < 11; x++)
                for (int z = 0; z < 11; z++)
                {
                    float d = (float)Math.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz));
                    if (d < 3.75f || d > 4.6f) continue;
                    c.Set(x, cy, z, z - cz >= 2 ? SunGoldLight : z - cz <= -2 ? SunGoldDeep : SunGold);
                }
            return c;
        }

        private static uint Mix(uint a, uint b, float t)
        {
            byte Ch(int shift) => (byte)Math.Round(((a >> shift) & 0xFF) * (1 - t) + ((b >> shift) & 0xFF) * t);
            return C(Ch(0), Ch(8), Ch(16));
        }

        /// <summary>Card art: the pyramid at twice the detail with the prism standing upright over its prongs.</summary>
        public static VoxModel RitualCard()
        {
            VoxCanvas p = SunPyramid(), prism = SunPrism();
            int top = (int)Math.Round(SunPrismCentre * 2);
            var c = new VoxCanvas(32, 32, top + 8);
            for (int x = 0; x < 32; x++)
                for (int y = 0; y < 32; y++)
                    for (int z = 0; z < p.SizeZ * 2; z++)
                    {
                        uint v = p.Get(x / 2, y / 2, z / 2);
                        if (v != 0) c.Set(x, y, z, v);
                    }
            // Upright: the prism's forward axis (y) becomes up.
            for (int x = 0; x < prism.SizeX; x++)
                for (int y = 0; y < prism.SizeY; y++)
                    for (int z = 0; z < prism.SizeZ; z++)
                    {
                        uint v = prism.Get(x, y, z);
                        if (v != 0) c.Set(16 - 5 + x - 1, 16 - 5 + z - 1, top - 7 + y, v);
                    }
            return c.ToModel("ritual_card");
        }
    }
}

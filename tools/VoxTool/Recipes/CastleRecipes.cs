using System;
using TreasureRun.Vox;
using static VoxTool.Recipes.VoxCanvas;

namespace VoxTool.Recipes
{
    /// <summary>
    /// Castle models. All castles use the vanilla castle footprint (32x32 voxels at 0.1 = 3x3 build cells after the
    /// scene's 0.9375 scale) so the buildable cells around them stay free. Colours come from vanilla palettes.
    /// </summary>
    public static class CastleRecipes
    {
        // Vanilla palettes
        private static readonly uint Stone = C(80, 79, 72), StoneDark = C(66, 64, 58), StoneLight = C(94, 93, 87);
        private static readonly uint Roof = C(128, 92, 21), Beam = C(85, 39, 0), Plaster = C(170, 132, 57);
        private static readonly uint ManaBlue = C(79, 98, 142), ManaDark = C(46, 66, 114), ManaNavy = C(22, 41, 85), ManaPale = C(120, 135, 171);
        private static readonly uint Gold = C(212, 177, 106), Banner = C(150, 40, 40), Glow = C(170, 210, 255);

        // ------------------------------------------------------------------ Castle Town

        public static VoxModel CastleTown()
        {
            var c = new VoxCanvas(32, 32, 38);

            // Low town wall around the edge with merlons.
            c.Walls(0, 0, 0, 31, 31, 3, Stone);
            for (int i = 0; i < 32; i += 2)
            {
                c.Set(i, 0, 4, Stone); c.Set(i, 31, 4, Stone);
                c.Set(0, i, 4, Stone); c.Set(31, i, 4, Stone);
            }

            // Corner towers.
            foreach (var (cx, cy) in new[] { (3.5f, 3.5f), (27.5f, 3.5f), (3.5f, 27.5f), (27.5f, 27.5f) })
            {
                c.Cylinder(cx, cy, 0, 13, 3.6f, Stone);
                c.Cylinder(cx, cy, 14, 14, 3.6f, Stone, hollow: true);
                c.Recolor((x, y, z, col) => z == 14 && (x + y) % 2 == 1 && Math.Abs(x - cx) + Math.Abs(y - cy) > 2.5f, 0);
                c.Cylinder(cx, cy, 13, 13, 3.6f, StoneDark, hollow: true);
            }

            // Central keep.
            c.Box(11, 11, 0, 20, 20, 26, Stone);
            c.ClearBox(12, 12, 26, 19, 19, 26);                       // roof walkway
            for (int i = 11; i <= 20; i += 2)
            {
                c.Set(i, 11, 27, Stone); c.Set(i, 20, 27, Stone);
                c.Set(11, i, 27, Stone); c.Set(20, i, 27, Stone);
            }
            c.Box(11, 11, 25, 20, 20, 25, StoneDark);                  // cornice band
            c.Box(11, 11, 12, 20, 20, 12, StoneDark);
            foreach (int z in new[] { 17, 18, 19 })                    // arrow slits
            {
                c.Set(15, 11, z, StoneDark); c.Set(16, 20, z, StoneDark);
                c.Set(11, 16, z, StoneDark); c.Set(20, 15, z, StoneDark);
            }
            // Brick speckle on visible keep walls.
            c.Recolor((x, y, z, col) => col == Stone && z > 0 && z < 25 && x >= 11 && x <= 20 && y >= 11 && y <= 20
                                         && c.IsSideSurface(x, y, z) && Hash(x, y, z, 7) < 0.1f, StoneDark);

            // Flag on top of the keep.
            c.Box(15, 15, 26, 15, 15, 36, Beam);
            c.Box(16, 15, 32, 19, 15, 35, Banner);
            c.Set(19, 15, 32, 0);

            // Four houses between keep and wall, facing outwards.
            VoxModel house = House();
            c.Paste(house, 10, 2, 0, 0);    // south (front faces -Y)
            c.Paste(house, 10, 21, 0, 2);   // north
            c.Paste(house, 21, 10, 0, 1);   // east
            c.Paste(house, 2, 10, 0, 3);    // west

            return c.Grounded().ToModel("castle_town");
        }

        /// <summary>Timber-frame house in the vanilla style: 12 wide (X), 9 deep (Y), door at the front (low Y).</summary>
        private static VoxModel House()
        {
            var h = new VoxCanvas(12, 9, 12);
            // Walls
            h.Box(1, 1, 0, 10, 7, 6, Plaster);
            h.ClearBox(2, 2, 1, 9, 6, 6);
            // Timber frame: base, top, corners, studs
            h.Walls(1, 1, 0, 10, 7, 0, Beam);
            h.Walls(1, 1, 6, 10, 7, 6, Beam);
            foreach (int x in new[] { 1, 4, 7, 10 }) { h.Box(x, 1, 0, x, 1, 6, Beam); h.Box(x, 7, 0, x, 7, 6, Beam); }
            foreach (int y in new[] { 1, 4, 7 }) { h.Box(1, y, 0, 1, y, 6, Beam); h.Box(10, y, 0, 10, y, 6, Beam); }
            h.Box(1, 1, 3, 10, 1, 3, Beam);
            // Door and windows
            h.Box(5, 1, 0, 6, 1, 2, Beam);
            h.Box(2, 1, 4, 3, 1, 5, Beam); h.Box(8, 1, 4, 9, 1, 5, Beam);
            // Gable roof along X, stepped like vanilla, 1-voxel overhang
            for (int y = 0; y <= 8; y++)
            {
                int z = 7 + Math.Min(y, 8 - y);
                h.Box(0, y, z, 11, y, z, Roof);
                // gable ends under the roof
                for (int zz = 7; zz < z; zz++) { h.Set(1, y, zz, Plaster); h.Set(10, y, zz, Plaster); }
            }
            h.Box(0, 4, 11, 11, 4, 11, Beam); // ridge
            return h.ToModel("house");
        }

        // ------------------------------------------------------------------ Arcane Spire

        public static VoxModel ArcaneSpire()
        {
            var c = new VoxCanvas(32, 32, 43);
            const float cx = 15.5f, cy = 15.5f;

            // Plinth with steps.
            c.Cylinder(cx, cy, 0, 1, 15.6f, StoneLight);
            c.Cylinder(cx, cy, 2, 2, 15.6f, StoneDark, hollow: true, wall: 1.1f);
            c.Cylinder(cx, cy, 2, 2, 14.6f, StoneLight);
            c.Cylinder(cx, cy, 3, 3, 11.5f, StoneLight);

            // Tapered spire body with bands and glowing windows.
            c.Cone(cx, cy, 4, 24, 6.6f, 4.6f, ManaBlue);
            foreach (int z in new[] { 4, 10, 17, 23 }) c.Recolor((x, y, zz, col) => zz == z, ManaDark);
            foreach (int z0 in new[] { 12, 19 })
                c.Recolor((x, y, z, col) => z >= z0 && z <= z0 + 2 && c.IsSideSurface(x, y, z)
                                            && (Math.Abs(x - cx) < 1f || Math.Abs(y - cy) < 1f), Glow);

            // Buttresses at the four diagonals.
            foreach (var (dx, dy) in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) })
            {
                int bx = (int)Math.Round(cx + dx * 5.2f), by = (int)Math.Round(cy + dy * 5.2f);
                c.Box(bx, by, 4, bx, by, 10, StoneLight);
                c.Box(bx + (dx > 0 ? 1 : -1), by + (dy > 0 ? 1 : -1), 4, bx + (dx > 0 ? 1 : -1), by + (dy > 0 ? 1 : -1), 7, StoneLight);
            }

            // Upper chamber with gold trim and balcony.
            c.Cylinder(cx, cy, 25, 25, 7.6f, StoneLight);
            c.Cylinder(cx, cy, 25, 25, 7.6f, Gold, hollow: true, wall: 1.0f);
            c.Cylinder(cx, cy, 26, 29, 5.8f, ManaBlue);
            c.Recolor((x, y, z, col) => z >= 27 && z <= 28 && c.IsSideSurface(x, y, z) && Hash(x, y, 0, 3) < 0.35f, Glow);
            c.Cylinder(cx, cy, 30, 30, 6.6f, ManaDark);

            // Pointed roof (linear taper reads as a spire, not a dome) and gold finial.
            for (int z = 31; z <= 41; z++)
            {
                float t = (z - 31) / 10f;
                c.Cylinder(cx, cy, z, z, 6.4f * (1f - t) + 0.5f * t, ManaNavy);
            }
            c.Box(15, 15, 42, 16, 16, 42, Gold);

            // Crystal clusters on the plinth.
            // Small crystal clusters on the plinth's edge, each leaning towards the spire.
            foreach (var (px, py) in new[] { (5, 5), (26, 5), (5, 26), (26, 26) })
            {
                int dx = px < cx ? 1 : -1, dy = py < cy ? 1 : -1;   // towards the centre
                c.Box(px - dx, py - dy, 3, px - dx, py - dy, 6, ManaPale);  // tall shard at the back
                c.Box(px, py - dy, 3, px, py - dy, 5, ManaBlue);
                c.Box(px - dx, py, 3, px - dx, py, 5, ManaBlue);
                c.Box(px, py, 3, px, py, 4, Glow);                           // small glowing tip in front
            }

            // Rune grooves on the upper plinth step (the glowing runes are drawn in game on top of them).
            c.Recolor((x, y, z, col) => z == 3 && Math.Abs(Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - 10.2) < 0.6, StoneDark);
            return c.Grounded().ToModel("arcane_spire");
        }

        /// <summary>The spire's floating crystal, a separate model so the game can animate it (bob and spin).</summary>
        public static VoxModel ArcaneCrystal()
        {
            var c = new VoxCanvas(8, 8, 7);
            const float cx = 3.5f, cy = 3.5f;
            int[] half = { 0, 1, 2, 3, 2, 1, 0 };
            for (int i = 0; i < half.Length; i++)
            {
                int r = half[i];
                for (int x = 0; x < 8; x++)
                    for (int y = 0; y < 8; y++)
                    {
                        float d = Math.Abs(x - cx) + Math.Abs(y - cy);
                        if (d > r + 1f) continue;
                        c.Set(x, y, i, d > r ? ManaBlue : (d > r - 1f ? Glow : C(225, 240, 255)));
                    }
            }
            return c.ToModel("arcane_spire_crystal");
        }
    }
}

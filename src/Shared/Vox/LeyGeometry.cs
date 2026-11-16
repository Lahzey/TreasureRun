using System.Collections.Generic;

namespace TreasureRun.Vox
{
    /// <summary>
    /// Voxel geometry of the Ley Line Network's glass pipes, shared by VoxTool (card art, palette model) and the game
    /// (fittings for every port combination, meshed on demand and cached).
    /// <para>
    /// Every part is two models on one fixed palette (<see cref="Palette"/>): the opaque frame (gold edges, wood) and the
    /// glass panes (drawn transparent). Canvas: 16 x 32 x <see cref="Height"/>, centred like a tile, so tile y =
    /// canvas y − 8 (tile y 0..15 is the siphon's own tile, 16.. the next one). Pipes run along +y at z 2..5 (4x4 with a
    /// hollow 2x2 inside); the game rotates parts per direction. Directions as a bit mask: 1 = +y (world +z), 2 = +x,
    /// 4 = −y, 8 = −x.
    /// </para>
    /// </summary>
    public static class LeyGeometry
    {
        public const int SizeX = 16, SizeY = 32, Height = 16, TileY = 8;

        public static readonly uint Gold = VoxModel.Rgba(214, 172, 74), GoldDark = VoxModel.Rgba(164, 124, 48),
            Wood = VoxModel.Rgba(128, 88, 50), WoodDark = VoxModel.Rgba(92, 62, 34),
            Glass = VoxModel.Rgba(176, 222, 236), GlassViolet = VoxModel.Rgba(186, 150, 245),
            Glow = VoxModel.Rgba(120, 196, 255), GlowBright = VoxModel.Rgba(214, 240, 255),
            IndicatorOn = VoxModel.Rgba(150, 70, 255), IndicatorOff = VoxModel.Rgba(204, 194, 220);

        private static readonly PaletteBuilder builder = MakePalette();
        /// <summary>The one palette of every Ley model (same indices everywhere, so one material serves all).</summary>
        public static uint[] Palette => builder.Palette;

        private static PaletteBuilder MakePalette()
        {
            var b = new PaletteBuilder();
            foreach (uint c in new[] { Gold, GoldDark, Wood, WoodDark, Glass, GlassViolet, Glow, GlowBright, IndicatorOn, IndicatorOff }) b.Get(c);
            return b;
        }

        public sealed class Part
        {
            public readonly VoxModel Frame = New(), Glass = New();
            public void SetFrame(int x, int y, int z, uint c) => Frame.Set(x, y + TileY, z, builder.Get(c));
            public void SetGlass(int x, int y, int z, uint c) => Glass.Set(x, y + TileY, z, builder.Get(c));
            public void Clear(int x, int y, int z) { Frame.Set(x, y + TileY, z, 0); Glass.Set(x, y + TileY, z, 0); }
        }

        private static VoxModel New() => new VoxModel(SizeX, SizeY, Height) { Palette = builder.Palette };

        public enum HubKind { TapLit, TapUnlit, Connector }

        // ------------------------------------------------------------------ fittings

        private enum Look { Gold, Glass, Hole }

        /// <summary>
        /// The pipe's cross-section seen from its end, on a 4x4 window (u, v from lo to lo + 3): gold at the four edge
        /// lines, glass between them, the 2x2 hollow open.
        /// </summary>
        private static Look PipeSection(int u, int v, int ulo, int vlo)
        {
            bool eu = u == ulo || u == ulo + 3, ev = v == vlo || v == vlo + 3;
            return eu && ev ? Look.Gold : eu || ev ? Look.Glass : Look.Hole;
        }

        /// <summary>
        /// A hollow glass box fitting (tile coordinates, inclusive) with gold edges. Where a pipe joins (side ports by mask,
        /// a riser on top), the face takes the pipe's own cross-section, so the pipe's gold edge lines and glass panes
        /// run on into the fitting without a break in the border. <paramref name="bottomLines"/> (hubs) continues the
        /// pipes' bottom gold lines straight across the floor; <paramref name="bottomHole"/> opens its middle (taps).
        /// </summary>
        private static void Fitting(Part p, int x0, int y0, int z0, int x1, int y1, int z1, uint glass,
                                    int sides, bool top, bool bottomHole, bool bottomLines, uint corner)
        {
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                    for (int z = z0; z <= z1; z++)
                    {
                        int edges = (x == x0 || x == x1 ? 1 : 0) + (y == y0 || y == y1 ? 1 : 0) + (z == z0 || z == z1 ? 1 : 0);
                        if (edges == 0) continue;
                        bool inX = x >= 6 && x <= 9, inY = y >= 6 && y <= 9, inZ = z >= 2 && z <= 5;
                        // The windows this voxel lies in (a pipe's cross-section on the face it joins).
                        var looks = new List<Look>(3);
                        if ((sides & 1) != 0 && y == y1 && inX && inZ) looks.Add(PipeSection(x, z, 6, 2));
                        if ((sides & 4) != 0 && y == y0 && inX && inZ) looks.Add(PipeSection(x, z, 6, 2));
                        if ((sides & 2) != 0 && x == x1 && inY && inZ) looks.Add(PipeSection(y, z, 6, 2));
                        if ((sides & 8) != 0 && x == x0 && inY && inZ) looks.Add(PipeSection(y, z, 6, 2));
                        if (top && z == z1 && inX && inY) looks.Add(PipeSection(x, y, 6, 6));

                        Look look;
                        if (looks.Contains(Look.Hole)) look = Look.Hole;
                        else if (looks.Contains(Look.Glass)) look = Look.Glass;
                        else if (looks.Count > 0) look = Look.Gold;
                        else look = edges >= 2 ? Look.Gold : Look.Glass;

                        if (z == z0 && edges == 1 && look == Look.Glass && inX && inY)
                        {
                            bool middle = x >= 7 && x <= 8 && y >= 7 && y <= 8;
                            if (bottomHole && middle) look = Look.Hole;
                            else if (bottomLines && (((sides & 5) != 0 && (x == 6 || x == 9)) || ((sides & 10) != 0 && (y == 6 || y == 9))))
                                look = Look.Gold;
                        }

                        bool cornerTop = z == z1 && (x == x0 || x == x1) && (y == y0 || y == y1);
                        if (look == Look.Gold) p.SetFrame(x, y, z, cornerTop ? corner : Gold);
                        else if (look == Look.Glass) p.SetGlass(x, y, z, glass);
                    }
        }

        // ------------------------------------------------------------------ parts

        /// <summary>
        /// A siphon's hub: a 6x6x4 glass fitting (x/y 5..10, z 2..5) with ports only where pipes join. A tap stands on a
        /// wooden plinth with a gold rim and a glowing mark under its open floor, and has purple indicator corners on
        /// top; a connector stands on four wooden feet.
        /// </summary>
        public static Part Hub(HubKind kind, int sides, bool top)
        {
            var p = new Part();
            bool tap = kind != HubKind.Connector;
            uint corner = kind == HubKind.TapLit ? IndicatorOn : kind == HubKind.TapUnlit ? IndicatorOff : Gold;
            Fitting(p, 5, 5, 2, 10, 10, 5, Glass, sides, top, tap, true, corner);
            if (tap)
            {
                for (int x = 4; x <= 11; x++)
                    for (int y = 4; y <= 11; y++)
                    {
                        p.SetFrame(x, y, 0, WoodDark);
                        bool rim = x == 4 || y == 4 || x == 11 || y == 11;
                        p.SetFrame(x, y, 1, rim ? GoldDark : Wood);
                    }
                for (int x = 7; x <= 8; x++)
                    for (int y = 7; y <= 8; y++) p.SetFrame(x, y, 1, Glow);
            }
            else
                foreach (int x in new[] { 5, 10 })
                    foreach (int y in new[] { 5, 10 })
                        for (int z = 0; z <= 1; z++) p.SetFrame(x, y, z, Wood);
            return p;
        }

        /// <summary>
        /// A corner/tee on a riser: a fitting as wide as the pipe (4x4x4: x/y 6..9, z 2..5), the pipes' cross-sections
        /// carried into its faces where they join (the riser from below always).
        /// </summary>
        public static Part Corner(int sides, bool top, bool bottom)
        {
            var p = new Part();
            Fitting(p, 6, 6, 2, 9, 9, 5, Glass, sides, top, false, false, Gold);
            if (bottom) OpenBottom(p, 6, 6, 2);
            return p;
        }

        /// <summary>Gives a pipe-wide fitting's floor the riser's cross-section (a riser joins from below).</summary>
        private static void OpenBottom(Part p, int x0, int y0, int z)
        {
            for (int x = x0; x <= x0 + 3; x++)
                for (int y = y0; y <= y0 + 3; y++)
                {
                    Look look = PipeSection(x, y, x0, y0);
                    bool edgeOfBox = (x == x0 || x == x0 + 3) && (y == y0 || y == y0 + 3);
                    if (edgeOfBox) continue; // the fitting's vertical edges stay gold
                    p.Clear(x, y, z);
                    if (look == Look.Glass) p.SetGlass(x, y, z, Glass);
                }
        }

        /// <summary>A glass tube along +y (x 6..9, z 2..5; hollow 2x2) from tile y <paramref name="y0"/> to <paramref name="y1"/>.</summary>
        private static void TubeY(Part p, int y0, int y1, uint glass)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = 6; x <= 9; x++)
                    for (int z = 2; z <= 5; z++)
                    {
                        bool ex = x == 6 || x == 9, ez = z == 2 || z == 5;
                        if (ex && ez) p.SetFrame(x, y, z, Gold);
                        else if (ex || ez) p.SetGlass(x, y, z, glass);
                    }
        }

        /// <summary>
        /// Pipe arm to the tile edge (tile y <paramref name="from"/>..15): from a hub (11) or from a corner (10). The
        /// neighbour's arm continues it.
        /// </summary>
        public static Part Pipe(int from)
        {
            var p = new Part();
            TubeY(p, from, 15, Glass);
            return p;
        }

        /// <summary>
        /// A funnel at tile y 16..19 opening to the crystal on the next tile (the crystal is 8 voxels wide and tall, its
        /// face at tile y 20): the mouth is exactly the crystal's size and flush with it. Rings: 4x4 (the pipe, hollow
        /// 2x2), 6x6 (hollow 2x2), 8x8 (hollow 4x4), 8x8 gold mouth (hollow 6x6). Each ring's walls overlap the one before,
        /// and the gold corners are solid blocks as thick as the wall, so every step of the gold edges touches the next
        /// by a face (an L, never a diagonal).
        /// </summary>
        private static void Funnel(Part p)
        {
            TubeY(p, 16, 16, GlassViolet);
            (int outer, int hollow, bool rim)[] rings = { (6, 2, false), (8, 4, false), (8, 6, true) };
            for (int i = 0; i < rings.Length; i++)
            {
                var (outer, hollow, rim) = rings[i];
                int t = (outer - hollow) / 2;
                int x0 = 8 - outer / 2, x1 = 7 + outer / 2, h0 = 8 - hollow / 2, h1 = 7 + hollow / 2;
                int z0 = 4 - outer / 2, z1 = 3 + outer / 2, hz0 = 4 - hollow / 2, hz1 = 3 + hollow / 2;
                for (int x = x0; x <= x1; x++)
                    for (int z = System.Math.Max(0, z0); z <= z1; z++)
                    {
                        if (x >= h0 && x <= h1 && z >= hz0 && z <= hz1) continue;
                        bool cornerBlock = (x < x0 + t || x > x1 - t) && (z < z0 + t || z > z1 - t);
                        if (rim || cornerBlock) p.SetFrame(x, 17 + i, z, Gold);
                        else p.SetGlass(x, 17 + i, z, GlassViolet);
                    }
            }
        }

        /// <summary>Intake to a crystal on the same level (or a higher one, from a corner): the arm plus the funnel.</summary>
        public static Part Intake(int from)
        {
            Part p = Pipe(from);
            Funnel(p);
            return p;
        }

        /// <summary>
        /// Corner just past the tile edge (tile y 16..19, pipe-wide) for an intake down to a lower crystal. Upper: port
        /// from the arm (−y) and down. Lower (at the crystal's level): port up and an open mouth flush with the crystal
        /// (+y, at its face).
        /// </summary>
        public static Part DropCorner(bool upper)
        {
            var p = new Part();
            // A pipe-wide fitting on tile y 16..19 (canvas-relative ports: −y = 4, +y = 1 in its own frame).
            var shifted = new Part();
            Fitting(shifted, 6, 6, 2, 9, 9, 5, upper ? Glass : GlassViolet, upper ? 4 : 1, !upper, false, false, Gold);
            if (upper) OpenBottom(shifted, 6, 6, 2);
            // Move it from tile y 6..9 to 16..19.
            for (int x = 0; x < SizeX; x++)
                for (int y = 6; y <= 9; y++)
                    for (int z = 0; z < Height; z++)
                    {
                        byte f = shifted.Frame.Get(x, y + TileY, z), g = shifted.Glass.Get(x, y + TileY, z);
                        if (f != 0) p.Frame.Set(x, y + 10 + TileY, z, f);
                        if (g != 0) p.Glass.Set(x, y + 10 + TileY, z, g);
                    }
            return p;
        }

        /// <summary>A vertical glass tube (4x4, hollow 2x2) centred on tile y <paramref name="cy"/>.., z 0..15 (the game stretches it).</summary>
        public static Part Vertical(int y0)
        {
            var p = new Part();
            for (int z = 0; z < Height; z++)
                for (int x = 6; x <= 9; x++)
                    for (int y = y0; y <= y0 + 3; y++)
                    {
                        bool ex = x == 6 || x == 9, ey = y == y0 || y == y0 + 3;
                        if (ex && ey) p.SetFrame(x, y, z, Gold);
                        else if (ex || ey) p.SetGlass(x, y, z, Glass);
                    }
            return p;
        }

        /// <summary>The riser above a hub (tile x/y 6..9).</summary>
        public static Part Riser() => Vertical(6);

        /// <summary>The drop down to a lower crystal (tile y 16..19).</summary>
        public static Part Drop() => Vertical(16);

        /// <summary>A mana mote (2x2x2, bright). The game batches all motes into one mesh.</summary>
        public static VoxModel Mote()
        {
            var m = new VoxModel(2, 2, 2) { Palette = builder.Palette };
            for (int x = 0; x < 2; x++)
                for (int y = 0; y < 2; y++)
                    for (int z = 0; z < 2; z++)
                        m.Set(x, y, z, builder.Get((x + y + z) % 2 == 0 ? GlowBright : Glow));
            return m;
        }

        /// <summary>A model holding every palette colour once (VoxTool writes it; the game takes its material).</summary>
        public static VoxModel PaletteModel()
        {
            var m = new VoxModel(16, 1, 1) { Palette = builder.Palette };
            for (int i = 1; i <= 10; i++) m.Set(i, 0, 0, (byte)i);
            return m;
        }

        /// <summary>Card art: one opaque model of a lit tap hub with pipes to all four sides.</summary>
        public static VoxModel Card()
        {
            var m = new VoxModel(16, 16, 8) { Palette = builder.Palette };
            void Merge(Part part, int turn)
            {
                foreach (VoxModel src in new[] { part.Frame, part.Glass })
                    for (int x = 0; x < 16; x++)
                        for (int y = 0; y < 16; y++)
                            for (int z = 0; z < 8; z++)
                            {
                                byte c = src.Get(x, y + TileY, z);
                                if (c == 0) continue;
                                int rx = x, ry = y;
                                for (int k = 0; k < turn; k++) { int t = rx; rx = 15 - ry; ry = t; }
                                if (m.Get(rx, ry, z) == 0) m.Set(rx, ry, z, c);
                            }
            }
            Merge(Hub(HubKind.TapLit, 15, false), 0);
            for (int turn = 0; turn < 4; turn++) Merge(Pipe(11), turn);
            return m;
        }
    }
}

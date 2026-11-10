using System;
using TreasureRun.Vox;
using static VoxTool.Recipes.VoxCanvas;

namespace VoxTool.Recipes
{
    /// <summary>
    /// Radar plane ascensions (M7). Aircraft face +Y (MagicaVoxel), which is +Z / forward in Unity; the runtime centres
    /// them on their mesh bounds and scales them, so the canvas origin does not matter (except where noted for parts).
    /// Not grounded: these fly.
    /// </summary>
    public static class PlaneRecipes
    {
        private static readonly uint Grey = C(122, 130, 140), GreyDark = C(86, 92, 102), GreyLight = C(162, 168, 176);
        private static readonly uint Canopy = C(70, 150, 200), CanopyLight = C(130, 200, 236);
        private static readonly uint Red = C(150, 28, 28), Cream = C(255, 214, 170), Nozzle = C(46, 46, 52), Flame = C(255, 170, 70);
        private static readonly uint Olive = C(96, 104, 60), OliveDark = C(72, 78, 44), OliveLight = C(122, 130, 80);
        private static readonly uint Metal = C(70, 70, 78), MetalLight = C(110, 110, 120);
        private static readonly uint Canvas = C(150, 148, 134), CanvasDark = C(124, 122, 110), CanvasLight = C(176, 174, 160);
        private static readonly uint Wood = C(110, 70, 34), WoodDark = C(85, 39, 0), Brass = C(196, 160, 80);

        // ------------------------------------------------------------------ Strike Fighter

        /// <summary>Modern delta-wing jet fighter: twin tails, bubble canopy, afterburner nozzle at y = 0.</summary>
        public static VoxModel StrikeFighter()
        {
            var c = new VoxCanvas(13, 22, 7);
            const int cx = 6;
            // Fuselage with a pointed nose.
            c.Box(cx - 1, 1, 1, cx + 1, 17, 3, Grey);
            c.Box(cx - 1, 18, 1, cx + 1, 18, 2, Grey);
            c.Box(cx, 19, 1, cx, 20, 2, GreyLight);
            c.Set(cx, 21, 1, Red);
            c.Recolor((x, y, z, col) => z == 1, GreyDark);                    // darker belly
            // Air intakes along the sides.
            foreach (int x in new[] { cx - 2, cx + 2 }) { c.Box(x, 9, 1, x, 12, 2, Grey); c.Set(x, 12, 1, Nozzle); c.Set(x, 12, 2, Nozzle); }
            // Bubble canopy.
            c.Box(cx, 12, 4, cx, 16, 4, Canopy);
            c.Set(cx, 15, 4, CanopyLight); c.Set(cx, 16, 4, CanopyLight);
            c.Set(cx, 11, 4, GreyDark);
            // Delta wings (thin, swept back).
            for (int y = 3; y <= 12; y++)
            {
                int hs = (int)Math.Round(6 - (y - 3) * 5 / 9.0);
                c.Box(cx - hs, y, 2, cx + hs, y, 2, Grey);
            }
            c.Recolor((x, y, z, col) => z == 2 && (x == 0 || x == 12), Red);   // wingtip markings
            // Wing missiles.
            foreach (int x in new[] { 2, 10 }) { c.Box(x, 4, 1, x, 8, 1, GreyLight); c.Set(x, 9, 1, Red); }
            // Twin canted tails and stabilisers.
            foreach (int x in new[] { cx - 2, cx + 2 })
            {
                c.Box(x, 1, 4, x, 4, 4, Grey); c.Box(x, 1, 5, x, 3, 5, Grey); c.Box(x, 1, 6, x, 2, 6, GreyDark);
                c.Box(x, 4, 3, x, 4, 3, Grey);
            }
            c.Box(1, 0, 2, 11, 1, 2, Grey);
            c.Box(1, 0, 2, 3, 0, 2, GreyDark); c.Box(9, 0, 2, 11, 0, 2, GreyDark);
            // Engine nozzle with afterburner glow.
            c.Box(cx - 1, 0, 1, cx + 1, 0, 3, Nozzle);
            c.Set(cx, 0, 2, Flame);
            return c.ToModel("strike_fighter");
        }

        // ------------------------------------------------------------------ Carpet Bomber

        /// <summary>
        /// Heavy four-engine bomber. Engine fronts (propeller hubs) are at y 19, z 3 and x 6/11/21/26 (the runtime puts
        /// spinning propellers there).
        /// </summary>
        public static VoxModel CarpetBomber()
        {
            var c = new VoxCanvas(33, 26, 10);
            const float cx = 16f;
            // Round fuselage, glazed nose, tail gunner.
            c.Ellipsoid(cx, 12.5f, 3f, 2.4f, 12.5f, 2.4f, Olive);
            c.Recolor((x, y, z, col) => z <= 1, OliveDark);
            c.Ellipsoid(cx, 23f, 3f, 1.8f, 2.6f, 1.8f, Canopy, (x, y, z) => y >= 22);
            c.Set(16, 25, 3, CanopyLight);
            c.Box(15, 18, 5, 17, 20, 5, Canopy);                          // cockpit windows
            c.Box(16, 0, 3, 16, 1, 3, Canopy);                           // tail gunner
            c.Ellipsoid(cx, 15f, 5.6f, 1.3f, 1.3f, 1.2f, Canopy, (x, y, z) => z >= 6); // dorsal turret
            c.Box(15, 7, 0, 17, 12, 0, Metal);                           // bomb bay doors
            // Long straight wings with a slight taper.
            for (int x = 0; x <= 32; x++)
            {
                int d = Math.Abs(x - 16);
                int y0 = 12 + (d > 12 ? 1 : 0), y1 = 16 - (d > 8 ? 1 : 0) - (d > 13 ? 1 : 0);
                c.Box(x, y0, 3, x, y1, 3, Olive);
                if (d <= 6) c.Box(x, y0, 4, x, y1 - 1, 4, Olive);
            }
            c.Recolor((x, y, z, col) => z == 3 && (x == 0 || x == 32), OliveDark);
            // Roundels (vanilla red and cream).
            foreach (int rx in new[] { 3, 29 })
                for (int x = rx - 1; x <= rx + 1; x++)
                    for (int y = 13; y <= 15; y++)
                        c.Set(x, y, 3, x == rx && y == 14 ? Red : Cream);
            // Engine nacelles.
            foreach (int x in new[] { 6, 11, 21, 26 })
            {
                c.Box(x - 1, 13, 2, x + 1, 18, 4, OliveDark);
                c.Box(x - 1, 18, 2, x + 1, 18, 4, Metal);
                c.Set(x, 19, 3, MetalLight);                              // hub
                c.Set(x, 12, 3, Metal);                                   // exhaust
            }
            // Tail: stabiliser and a tall fin.
            c.Box(9, 1, 3, 23, 3, 3, Olive);
            c.Box(9, 1, 3, 10, 1, 3, OliveDark); c.Box(22, 1, 3, 23, 1, 3, OliveDark);
            for (int z = 4; z <= 9; z++) c.Box(16, 0, z, 16, 5 - (z - 4) * 4 / 5, z, Olive);
            c.Box(16, 0, 8, 16, 1, 9, Red);
            return c.ToModel("carpet_bomber");
        }

        /// <summary>Two-blade propeller in the XZ plane (spins around Y), hub at the centre (3, 0, 3).</summary>
        public static VoxModel Propeller()
        {
            var c = new VoxCanvas(7, 1, 7);
            c.Box(3, 0, 0, 3, 0, 6, Metal);
            c.Set(3, 0, 0, Cream); c.Set(3, 0, 6, Cream);
            c.Box(2, 0, 3, 4, 0, 3, MetalLight);
            return c.ToModel("propeller");
        }

        /// <summary>Small bomb, nose towards +Y, with tail fins.</summary>
        public static VoxModel Bomb()
        {
            var c = new VoxCanvas(3, 7, 3);
            c.Box(1, 1, 0, 1, 5, 2, OliveDark);
            c.Box(0, 2, 1, 2, 5, 1, OliveDark);
            c.Set(1, 6, 1, Metal);
            c.Box(0, 0, 1, 2, 1, 1, Metal); c.Box(1, 0, 0, 1, 1, 2, Metal);   // fins
            c.Set(1, 3, 2, Cream);                                           // stripe
            return c.ToModel("bomb");
        }

        // ------------------------------------------------------------------ War Blimp

        /// <summary>Envelope centre (voxel coordinates); the runtime puts the blimp's pivot here.</summary>
        public const float BlimpCx = 15.5f, BlimpCy = 31.5f, BlimpCz = 18f;
        /// <summary>Top of the tower deck (voxel z; the deck spans x 8..23, y 24..39).</summary>
        public const int BlimpDeckTop = 30;

        /// <summary>
        /// Huge armoured airship: a long envelope with a flat tower deck on top (16x16 voxels = one build cell), a gondola
        /// with a gatling mount (front, under the gondola: x 15.5, y 40, z 1) and two pusher engines (propellers at
        /// x 4/27, y 24, z 9).
        /// </summary>
        public static VoxModel WarBlimp()
        {
            var c = new VoxCanvas(32, 64, 36);
            // Envelope with panel rings and a red nose cap.
            c.Ellipsoid(BlimpCx, BlimpCy, BlimpCz, 10.6f, 31.4f, 10.6f, Canvas);
            c.Recolor((x, y, z, col) => y % 7 == 3, CanvasDark);
            c.Recolor((x, y, z, col) => z <= 10 && y % 7 != 3, CanvasDark);
            c.Recolor((x, y, z, col) => z >= 25 && y % 7 != 3, CanvasLight);
            c.Recolor((x, y, z, col) => y >= 59, Red);
            c.Recolor((x, y, z, col) => y >= 50 && y <= 52 && col != Red, Cream);
            // Tail fins (top, bottom and both sides), rooted on the tapering envelope, with red trailing edges.
            for (int y = 1; y <= 13; y++)
            {
                float t = (y - BlimpCy) / 31.4f;
                int r = (int)Math.Round(10.6 * Math.Sqrt(Math.Max(0, 1 - t * t)));
                int reach = y <= 8 ? 6 : 6 - (y - 8);
                uint fin = y <= 2 ? Red : CanvasDark;
                const int cz = (int)BlimpCz, cx = 15;
                c.Box(15, y, cz + r - 1, 16, y, cz + r + reach, fin);            // top
                c.Box(15, y, cz - r - reach, 16, y, cz - r + 1, fin);            // bottom
                c.Box(cx - r - reach, y, 17, cx - r + 1, y, 18, fin);            // left
                c.Box(cx + 1 + r - 1, y, 17, cx + 1 + r + reach, y, 18, fin);    // right
            }
            // Tower deck: a plank deck with an iron rim and brass posts on top of the envelope.
            c.ClearBox(8, 24, BlimpDeckTop - 1, 23, 39, 35);
            c.Box(8, 24, BlimpDeckTop - 1, 23, 39, BlimpDeckTop - 1, WoodDark);
            c.Box(8, 24, BlimpDeckTop, 23, 39, BlimpDeckTop, Wood);
            c.Recolor((x, y, z, col) => z == BlimpDeckTop && (x + y) % 4 == 0, WoodDark);
            c.Walls(8, 24, BlimpDeckTop, 23, 39, BlimpDeckTop, Metal);
            foreach (var (x, y) in new[] { (8, 24), (23, 24), (8, 39), (23, 39), (8, 31), (23, 31), (15, 24), (16, 39) })
                c.Box(x, y, BlimpDeckTop + 1, x, y, BlimpDeckTop + 1, Brass);
            // Deck supports down into the envelope.
            for (int x = 8; x <= 23; x += 15)
                for (int y = 24; y <= 39; y += 15)
                    c.Box(x, y, 22, x, y, BlimpDeckTop - 2, Metal);
            // Gondola under the envelope: wooden cabin with portholes and an iron keel.
            c.Box(12, 20, 2, 19, 42, 7, Wood);
            c.Recolor((x, y, z, col) => col == Wood && z == 5 && (x == 12 || x == 19) && y % 3 == 0, CanopyLight);
            c.Recolor((x, y, z, col) => col == Wood && (y == 20 || y == 42) && z == 5 && (x == 15 || x == 16), CanopyLight);
            c.Box(12, 20, 2, 19, 42, 2, Metal);
            c.Box(13, 22, 1, 18, 40, 1, Metal);
            c.Box(14, 28, 1, 17, 33, 1, Nozzle);                              // bomb bay
            c.Box(12, 20, 7, 19, 42, 7, WoodDark);
            foreach (int y in new[] { 22, 31, 40 }) { c.Box(13, y, 8, 13, y, 9, Metal); c.Box(18, y, 8, 18, y, 9, Metal); }
            // Gatling mount ring at the front of the gondola.
            c.Box(14, 38, 0, 17, 41, 0, MetalLight);
            // Engine pods on struts with pusher propellers at the back.
            foreach (int x in new[] { 4, 27 })
            {
                c.Box(x - 1, 25, 8, x + 1, 32, 10, Metal);
                c.Box(x - 1, 33, 9, x + 1, 33, 9, MetalLight);
                c.Set(x, 24, 9, MetalLight);
                int inner = x < 16 ? 1 : -1;
                c.Line(x + inner * 2, 28, 9, 15.5f - inner * 3.5f, 28, 5, Metal);
                c.Line(x + inner * 2, 29, 9, x + inner * 6, 29, 13, Metal);
            }
            return c.ToModel("war_blimp");
        }

        /// <summary>Gatling housing (yaws/pitches), barrels attach at the front (y 6, z 2, x 2).</summary>
        public static VoxModel GatlingMount()
        {
            var c = new VoxCanvas(5, 7, 4);
            c.Box(0, 0, 0, 4, 4, 3, Metal);
            c.Box(1, 0, 3, 3, 3, 3, MetalLight);
            c.Box(0, 1, 1, 0, 3, 2, Brass); c.Box(4, 1, 1, 4, 3, 2, Brass);   // ammo boxes
            c.Box(1, 5, 1, 3, 6, 3, Metal);
            return c.ToModel("gatling_mount");
        }

        /// <summary>Rotating barrel cluster along +Y, centred on (1, *, 1).</summary>
        public static VoxModel GatlingBarrels()
        {
            var c = new VoxCanvas(3, 9, 3);
            foreach (var (x, z) in new[] { (1, 0), (0, 1), (2, 1), (1, 2) }) c.Box(x, 0, z, x, 8, z, Nozzle);
            c.Box(0, 0, 0, 2, 1, 2, MetalLight);
            c.Box(0, 6, 0, 2, 6, 2, MetalLight);
            c.Set(1, 8, 1, Metal);
            return c.ToModel("gatling_barrels");
        }
    }
}

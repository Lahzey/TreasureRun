using System;
using System.Collections.Generic;
using TreasureRun.Vox;
using static VoxTool.Recipes.VoxCanvas;

namespace VoxTool.Recipes
{
    /// <summary>
    /// Particle Accelerator models (phase 3).
    /// <para>
    /// Core: <c>accelerator_base</c> replaces the vanilla base (16x16, origin in the middle): Ley sockets on all four
    /// sides, a pedestal and a ring accelerator. <c>accelerator_barrel</c> is a free model (centred, standing on y = 0) the
    /// game shows on the turret instead of the vanilla top: a turntable that sits on the pedestal, cables from it up to a
    /// huge barrel with coil rings, a charge window along its top and a singularity muzzle. Its canvas is 14 x 18 x 17
    /// (the barrel points to +y); the pivot is the canvas centre.
    /// </para>
    /// <para>
    /// Capacitor: <c>capacitor_base</c> replaces the vanilla base and shows the whole tower (the game hides the vanilla
    /// top): the core's own barrel stood on end as a big battery cell (steel sleeve, a gold + terminal), on the core's
    /// base sunk 7 voxels into the ground so the ring accelerator lies on the floor. It is built from the core's parts by
    /// <see cref="AcceleratorMorph"/>, which also computes the transformation between the two.
    /// </para>
    /// </summary>
    public static partial class AscensionRecipes
    {
        internal static readonly uint PaSlab = C(58, 60, 70), PaDeck = C(96, 100, 112), PaDeckLight = C(132, 137, 150), PaEdge = C(40, 41, 48);
        internal static readonly uint PaPipe = C(170, 176, 190), PaMagnet = C(74, 52, 120), PaMagnetDark = C(46, 32, 78);
        internal static readonly uint PaViolet = C(156, 84, 240), PaGlow = C(214, 168, 255), PaWhite = C(246, 236, 255), PaVoid = C(14, 10, 22);
        internal static readonly uint PaCopper = C(190, 110, 52), PaCopperDark = C(122, 66, 30), PaCable = C(34, 30, 40), PaCableDark = C(22, 20, 26);
        internal static readonly uint PaGold = C(214, 172, 74), PaGlass = C(186, 150, 245);

        /// <summary>Barrel canvas (see the class summary); the game uses the same numbers to place the charge bar and cables.</summary>
        public const int BarrelX = 14, BarrelY = 18, BarrelZ = 17;

        private static IEnumerable<(string, Func<VoxModel>)> AcceleratorAll()
        {
            yield return ("ascensions/particle/accelerator_base", () => AcceleratorBase().ToModel("accelerator_base"));
            yield return ("ascensions/particle/accelerator_barrel", () => AcceleratorBarrel().ToModel("accelerator_barrel"));
            yield return ("ascensions/particle/capacitor_base", () => AcceleratorMorph.CapacitorModel().ToModel("capacitor_base"));
            yield return ("ascensions/particle/accelerator_card", AcceleratorCard);
        }

        /// <summary>
        /// Sets a voxel given relative to one side of a 16x16 tile: <paramref name="u"/> along the side, <paramref name="d"/>
        /// the depth from the edge (side 0 = −y, 1 = +x, 2 = +y, 3 = −x), so the four sides can be drawn with one loop.
        /// </summary>
        private static void SideSet(VoxCanvas c, int side, int u, int d, int z, uint col)
        {
            switch (side)
            {
                case 0: c.Set(u, d, z, col); break;
                case 1: c.Set(15 - d, u, z, col); break;
                case 2: c.Set(15 - u, 15 - d, z, col); break;
                default: c.Set(d, 15 - u, z, col); break;
            }
        }

        /// <summary>
        /// Core base (16x16x10). Ley sockets in the middle of each side: the Ley pipes (x 6..9, z 2..5, the pipe's own
        /// cross-section) plug into their faces at depth 4 (tile y 20 of a neighbour's pipe canvas), and depth 0..3 stays
        /// free for the pipe's coupler. A pedestal (top z 8) carries the turntable of the barrel. The ring accelerator (mid
        /// radius 6, z 7..8) runs above the sockets on four magnets on pylons in the corners.
        /// </summary>
        public static VoxCanvas AcceleratorBase()
        {
            var c = new VoxCanvas(16, 16, 10);
            const float mid = 7.5f;
            bool Channel(int x, int y) => (x >= 5 && x <= 10 && (y <= 3 || y >= 12)) || (y >= 5 && y <= 10 && (x <= 3 || x >= 12));
            for (int x = 1; x <= 14; x++)
                for (int y = 1; y <= 14; y++)
                    if (!Channel(x, y)) c.Box(x, y, 0, x, y, 1, PaSlab);
            c.Box(4, 4, 2, 11, 11, 2, PaDeck);
            c.Recolor((x, y, z, col) => z == 2 && (x == 4 || x == 11 || y == 4 || y == 11), PaDeckLight);
            foreach (int x in new[] { 0, 13 })
                foreach (int y in new[] { 0, 13 })
                {
                    c.Box(x, y, 0, x + 2, y + 2, 3, PaEdge);
                    c.Set(x + 1, y + 1, 4, PaViolet);
                }

            // Ley sockets: a housing with the pipe's cross-section on its face (gold corners, violet glass, glowing core).
            for (int side = 0; side < 4; side++)
                for (int u = 5; u <= 10; u++)
                    for (int z = 1; z <= 6; z++)
                    {
                        SideSet(c, side, u, 5, z, PaSlab);
                        bool inU = u >= 6 && u <= 9, inZ = z >= 2 && z <= 5;
                        if (!inU || !inZ) { SideSet(c, side, u, 4, z, z == 6 || z == 1 ? PaEdge : PaSlab); continue; }
                        bool eu = u == 6 || u == 9, ez = z == 2 || z == 5;
                        SideSet(c, side, u, 4, z, eu && ez ? PaGold : eu || ez ? PaGlass : PaGlow);
                    }

            // Pedestal under the turntable.
            c.Box(5, 5, 3, 10, 10, 8, PaSlab);
            c.Box(5, 5, 8, 10, 10, 8, PaDeck);
            foreach (int z in new[] { 4, 6 }) { c.Box(5, 7, z, 5, 8, z, PaViolet); c.Box(10, 7, z, 10, 8, z, PaViolet); c.Box(7, 5, z, 8, 5, z, PaViolet); c.Box(7, 10, z, 8, 10, z, PaViolet); }

            // Ring accelerator: beam pipe with four magnets on the diagonals (on pylons in the corners).
            for (int x = 0; x < 16; x++)
                for (int y = 0; y < 16; y++)
                {
                    double dx = x - mid, dy = y - mid, d = Math.Sqrt(dx * dx + dy * dy);
                    double a = (Math.Atan2(dy, dx) * 180 / Math.PI + 360) % 360;
                    double fromDiag = Math.Abs(((a - 45) % 90 + 90) % 90 - 45); // 45 at the side middles, 0 on the diagonals
                    bool magnet = 45 - fromDiag < 14;
                    if (magnet && d >= 4.9 && d <= 7.3)
                    {
                        c.Box(x, y, 6, x, y, 9, d > 5.4 && d < 6.8 ? PaMagnet : PaMagnetDark);
                        if (d > 5.6 && d < 6.5) c.Set(x, y, 9, PaGlow);
                    }
                    else if (d >= 5.3 && d <= 6.8)
                        c.Box(x, y, 7, x, y, 8, PaPipe);
                }
            foreach (var (x, y) in new[] { (3, 3), (12, 3), (3, 12), (12, 12) }) c.Box(x, y, 2, x + (x < 8 ? 1 : -1), y + (y < 8 ? 1 : -1), 5, PaEdge);
            return c;
        }

        /// <summary>
        /// The core's barrel (12 x 18 x 17, pointing to +y, pivot in the middle): a turntable (radius 4.5, z 0..1) on the
        /// pedestal, a column holding a huge barrel (radius 4.6 round the axis x 6.5 / z 10.5): a dark power housing at the
        /// back with vents, three copper coil rings, a charge window along the top (x 5..8, y 3..14, the game fills it with
        /// light as it charges) and a stepped violet muzzle around a black void. Four thick cables run from sockets on the
        /// turntable up to the barrel's flanks.
        /// </summary>
        public static VoxCanvas AcceleratorBarrel() => AcceleratorBarrel(BarrelLayers.All);

        /// <summary>Layers of the barrel model (the transformation moves them separately, <see cref="AcceleratorMorph"/>).</summary>
        [Flags]
        public enum BarrelLayers
        {
            Table = 1,
            Column = 2,
            /// <summary>The barrel itself: power housing, body, coil rings, vents and muzzle.</summary>
            Body = 4,
            /// <summary>The charge window cut into the body's top and its two gold rails (with <see cref="Body"/>).</summary>
            Window = 8,
            Cables = 16,
            All = 31,
        }

        /// <summary>The barrel with only some of its layers (drawn in the usual order, so the full set is the model).</summary>
        public static VoxCanvas AcceleratorBarrel(BarrelLayers layers)
        {
            var c = new VoxCanvas(BarrelX, BarrelY, BarrelZ);
            const float ax = 6.5f, cy = 8.5f;
            bool Has(BarrelLayers l) => (layers & l) != 0;

            // Turntable with violet lights and the column.
            if (Has(BarrelLayers.Table))
            {
                c.Cylinder(ax, cy, 0, 1, 4.6f, PaSlab);
                c.Cylinder(ax, cy, 1, 1, 4.6f, PaDeck);
                for (int i = 0; i < 8; i++)
                {
                    double a = i * Math.PI / 4 + Math.PI / 8;
                    c.Set((int)Math.Round(ax + Math.Cos(a) * 4.1), (int)Math.Round(cy + Math.Sin(a) * 4.1), 1, PaViolet);
                }
            }
            if (Has(BarrelLayers.Column))
            {
                c.Box(5, 6, 2, 8, 11, 6, PaEdge);
                c.Box(4, 7, 4, 9, 10, 6, PaSlab);
            }
            if (Has(BarrelLayers.Body)) BarrelBody(c, Has(BarrelLayers.Window));
            if (Has(BarrelLayers.Cables)) BarrelCables(c);
            return c;
        }

        private static void BarrelBody(VoxCanvas c, bool window)
        {
            const float ax = 6.5f, az = 10.5f;
            bool InBarrel(int x, int z, double r) => (x - ax) * (x - ax) + (z - az) * (z - az) <= r * r;
            // Barrel body.
            for (int x = 0; x < BarrelX; x++)
                for (int z = 5; z < BarrelZ; z++)
                    for (int y = 0; y < BarrelY; y++)
                    {
                        if (y <= 3 && InBarrel(x, z, 5.1)) { c.Set(x, y, z, y == 0 && (z % 2 == 0) && InBarrel(x, z, 3.5) ? PaViolet : PaEdge); continue; }
                        if (y >= 4 && y <= 14 && InBarrel(x, z, 4.6)) c.Set(x, y, z, PaSlab);
                    }
            // Coil rings.
            foreach (int y in new[] { 6, 9, 12 })
                for (int x = 0; x < BarrelX; x++)
                    for (int z = 5; z < BarrelZ; z++)
                        if (InBarrel(x, z, 5.4) && !InBarrel(x, z, 4.4)) c.Set(x, y, z, y == 9 ? PaCopper : PaCopperDark);
            // Panel seams and vents on the power housing.
            for (int z = 8; z <= 13; z += 2) { c.Set(1, 1, z, PaViolet); c.Set(12, 1, z, PaViolet); c.Set(1, 2, z, PaViolet); c.Set(12, 2, z, PaViolet); }

            // Charge window: a dark glass groove along the top; the game lights it from the back as it charges.
            if (window)
            {
                for (int y = 3; y <= 14; y++)
                    for (int x = 5; x <= 8; x++)
                    {
                        int top = 15;
                        while (top > 6 && c.Get(x, y, top) == 0) top--;
                        c.Clear(x, y, top);
                        c.Set(x, y, top - 1, PaVoid);
                    }
                for (int y = 3; y <= 14; y++) { c.Set(4, y, 15, PaGold); c.Set(9, y, 15, PaGold); }
            }

            // Muzzle: stepped violet rings and a black void in a glowing rim.
            for (int x = 0; x < BarrelX; x++)
                for (int z = 5; z < BarrelZ; z++)
                {
                    if (InBarrel(x, z, 5.1)) c.Set(x, 15, z, PaViolet);
                    if (InBarrel(x, z, 4.6)) c.Set(x, 16, z, PaMagnet);
                    if (InBarrel(x, z, 4.1)) c.Set(x, 17, z, InBarrel(x, z, 2.6) ? PaVoid : InBarrel(x, z, 3.3) ? PaWhite : PaGlow);
                }
        }

        /// <summary>Cables: sockets on the turntable up to the barrel's flanks, bulging outwards; copper plugs at both ends.</summary>
        private static void BarrelCables(VoxCanvas c)
        {
            foreach (var (path, _) in CablePaths())
            {
                // A thick rubber cable (radius 0.95) with a glowing violet band every 3 voxels.
                float run = 0f;
                for (int i = 1; i < path.Length; i++)
                {
                    var (x0, y0, z0) = path[i - 1];
                    var (x1, y1, z1) = path[i];
                    float len = (float)Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0) + (z1 - z0) * (z1 - z0));
                    for (float t = 0f; t <= len; t += 0.35f, run += 0.35f)
                    {
                        float px = x0 + (x1 - x0) * t / len, py = y0 + (y1 - y0) * t / len, pz = z0 + (z1 - z0) * t / len;
                        uint col = run % 3f < 0.8f ? PaViolet : PaCable;
                        for (int x = (int)Math.Floor(px - 1); x <= (int)Math.Ceiling(px + 1); x++)
                            for (int y = (int)Math.Floor(py - 1); y <= (int)Math.Ceiling(py + 1); y++)
                                for (int z = (int)Math.Floor(pz - 1); z <= (int)Math.Ceiling(pz + 1); z++)
                                    if ((x - px) * (x - px) + (y - py) * (y - py) + (z - pz) * (z - pz) <= 0.9f && c.Get(x, y, z) != PaViolet)
                                        c.Set(x, y, z, col);
                    }
                }
                var (sx, sy, sz) = path[0];
                var (ex, ey, ez) = path[path.Length - 1];
                c.Set((int)Math.Round(sx), (int)Math.Round(sy), (int)Math.Round(sz), PaCopper);
                c.Set((int)Math.Round(ex), (int)Math.Round(ey), (int)Math.Round(ez), PaCopper);
            }
        }

        /// <summary>
        /// How far along its cable each cable voxel lies (0 at the turntable socket, 1 at the barrel's flank), for the
        /// cables feeding out in the transformation. Same sampling as <see cref="BarrelCables"/>.
        /// </summary>
        public static Dictionary<(int, int, int), float> CableRuns()
        {
            var runs = new Dictionary<(int, int, int), float>();
            foreach (var (path, _) in CablePaths())
            {
                float total = 0f;
                for (int i = 1; i < path.Length; i++)
                {
                    var (x0, y0, z0) = path[i - 1];
                    var (x1, y1, z1) = path[i];
                    total += (float)Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0) + (z1 - z0) * (z1 - z0));
                }
                float run = 0f;
                for (int i = 1; i < path.Length; i++)
                {
                    var (x0, y0, z0) = path[i - 1];
                    var (x1, y1, z1) = path[i];
                    float len = (float)Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0) + (z1 - z0) * (z1 - z0));
                    for (float t = 0f; t <= len; t += 0.35f, run += 0.35f)
                    {
                        float px = x0 + (x1 - x0) * t / len, py = y0 + (y1 - y0) * t / len, pz = z0 + (z1 - z0) * t / len;
                        for (int x = (int)Math.Floor(px - 1); x <= (int)Math.Ceiling(px + 1); x++)
                            for (int y = (int)Math.Floor(py - 1); y <= (int)Math.Ceiling(py + 1); y++)
                                for (int z = (int)Math.Floor(pz - 1); z <= (int)Math.Ceiling(pz + 1); z++)
                                    if ((x - px) * (x - px) + (y - py) * (y - py) + (z - pz) * (z - pz) <= 0.9f && !runs.ContainsKey((x, y, z)))
                                        runs[(x, y, z)] = Math.Min(1f, run / total);
                    }
                }
                var (sx, sy, sz) = path[0];
                var (ex, ey, ez) = path[path.Length - 1];
                runs[((int)Math.Round(sx), (int)Math.Round(sy), (int)Math.Round(sz))] = 0f;
                runs[((int)Math.Round(ex), (int)Math.Round(ey), (int)Math.Round(ez))] = 1f;
            }
            return runs;
        }

        /// <summary>
        /// The barrel's four cables as polylines in canvas voxels (turntable socket → bulge → barrel flank). The game has
        /// the same points (ParticleAccelerator.cs) for the energy running up them.
        /// </summary>
        public static IEnumerable<((float x, float y, float z)[] path, int side)> CablePaths()
        {
            foreach (int side in new[] { 0, 1 })
                foreach (var (y0, y1, y2) in new[] { (5f, 4f, 3f), (12f, 13f, 14f) })
                {
                    float X(float x) => side == 0 ? x : 13 - x;
                    yield return (new[] { (X(2.5f), y0, 1.5f), (X(0.5f), y1, 4.5f), (X(0.5f), y2, 7.5f), (X(1.6f), y2, 10.5f) }, side);
                }
        }

        /// <summary>Card art: the core base with the barrel on its pedestal (turntable at z 9, centres lined up).</summary>
        public static VoxModel AcceleratorCard()
        {
            VoxCanvas b = AcceleratorBase(), t = AcceleratorBarrel();
            var c = new VoxCanvas(16, 18, 9 + BarrelZ);
            for (int x = 0; x < 16; x++)
                for (int y = 0; y < 16; y++)
                    for (int z = 0; z < b.SizeZ; z++)
                        if (b.Get(x, y, z) != 0) c.Set(x, y + 1, z, b.Get(x, y, z));
            for (int x = 0; x < BarrelX; x++)
                for (int y = 0; y < BarrelY; y++)
                    for (int z = 0; z < BarrelZ; z++)
                        if (t.Get(x, y, z) != 0) c.Set(x + 1, y, z + 9, t.Get(x, y, z));
            return c.ToModel("accelerator_card");
        }
    }
}

using System;
using TreasureRun.Vox;
using static VoxTool.Recipes.VoxCanvas;

namespace VoxTool.Recipes
{
    /// <summary>
    /// The Laboratory (docs/poi-plan.md §9): a 3x3-cell lab round a chamber in its centre cell, where a tower stands to be
    /// studied. Idle (dark: nothing in the chamber) and active (lit: fluids, orbs, screens and the chamber's ring glow).
    /// Axes: x across, y forward, z up; at 0.1 per voxel the 30 x 30 footprint is the 3 x 3 cells. The camera looks from
    /// low x and high y, so the tall parts stand at the far corner and side, the near ones stay low, and the middle
    /// (voxels 7-22, where a tower's base sits) holds only the chamber floor.
    /// </summary>
    public static class LabRecipes
    {
        private static readonly uint Tile = C(72, 78, 88), TileAlt = C(84, 90, 100), Grout = C(54, 58, 66);
        private static readonly uint Steel = C(104, 110, 122), SteelDark = C(58, 62, 72), SteelLight = C(136, 144, 156), Bolt = C(140, 146, 154);
        private static readonly uint Pad = C(96, 102, 114), PadLine = C(120, 126, 138);
        private static readonly uint Copper = C(188, 112, 60), CopperDark = C(132, 74, 40), Brass = C(202, 166, 74);
        private static readonly uint Hazard = C(190, 150, 40), HazardDark = C(34, 34, 36);
        private static readonly uint Wood = C(124, 88, 56), WoodDark = C(88, 62, 40);
        private static readonly uint Cable = C(30, 32, 36), Rubber = C(44, 46, 52);
        private static readonly uint Glass = C(168, 198, 206);

        /// <summary>The palette that changes with the chamber's state.</summary>
        private sealed class Lit
        {
            public uint Glow, GlowDim, Fluid, FluidLight, FluidDark, Specimen, Screen, ScreenLine, Red, Green, Amber, Purple, Orange, Blue;
        }

        private static readonly Lit Idle = new Lit
        {
            Glow = C(52, 86, 96), GlowDim = C(44, 66, 74), Fluid = C(44, 92, 64), FluidLight = C(70, 120, 88), FluidDark = C(26, 56, 40),
            Specimen = C(22, 42, 30), Screen = C(26, 38, 42), ScreenLine = C(36, 52, 56),
            Red = C(92, 46, 44), Green = C(52, 86, 58), Amber = C(110, 88, 44), Purple = C(92, 60, 112), Orange = C(132, 92, 48), Blue = C(56, 76, 120),
        };

        private static readonly Lit Active = new Lit
        {
            Glow = C(140, 244, 255), GlowDim = C(84, 196, 220), Fluid = C(96, 232, 120), FluidLight = C(196, 255, 190), FluidDark = C(52, 170, 84),
            Specimen = C(34, 92, 52), Screen = C(80, 214, 196), ScreenLine = C(190, 255, 236),
            Red = C(255, 72, 60), Green = C(96, 255, 120), Amber = C(255, 190, 60), Purple = C(200, 96, 255), Orange = C(255, 150, 40), Blue = C(90, 150, 255),
        };

        /// <summary>Emitter orbs (voxel centres): far corner, right corner, left corner, near corner (also used by the game for the beams).</summary>
        public static readonly (float x, float y, float z)[] Orbs =
        {
            (24.7f, 4.3f, 20f), (4.3f, 4.3f, 14f), (24.7f, 24.7f, 14f), (5.3f, 23.7f, 6f),
        };

        private const float Cx = 14.5f, Cy = 14.5f;

        public static VoxModel Laboratory() => Build(Idle);
        public static VoxModel LaboratoryActive() => Build(Active);

        private static VoxModel Build(Lit lit)
        {
            var c = new VoxCanvas(30, 30, 24);
            Floor(c, lit);
            Pylon(c, lit, 25.5f, 3.5f, 22, Orbs[0]);
            Pylon(c, lit, 3.5f, 3.5f, 16, Orbs[1]);
            Pylon(c, lit, 25.5f, 25.5f, 16, Orbs[2]);
            Pylon(c, lit, 4.5f, 24.5f, 8, Orbs[3]);
            Tank(c, lit);
            Bench(c, lit);
            Console(c, lit);
            Capacitors(c, lit);
            Cables(c);
            return c.ToModel("laboratory");
        }

        // ------------------------------------------------------------------ floor and chamber

        /// <summary>Lab tiles with a steel curb; in the middle the chamber's dark plate with a glowing ring round it.</summary>
        private static void Floor(VoxCanvas c, Lit lit)
        {
            for (int x = 0; x < 30; x++)
                for (int y = 0; y < 30; y++)
                {
                    float d = Dist(x, y, Cx, Cy);
                    uint col;
                    if (d < 8.6f) col = (x + y) % 6 == 0 ? PadLine : Pad;
                    else if (d < 9.6f) col = lit.Glow;
                    else if (d < 10.4f) col = SteelDark;
                    else if (x % 5 == 0 || y % 5 == 0) col = Grout;
                    else col = ((x / 5) + (y / 5)) % 2 == 0 ? Tile : TileAlt;
                    c.Set(x, y, 0, col);
                    bool edge = x == 0 || y == 0 || x == 29 || y == 29;
                    if (edge) c.Set(x, y, 1, (x + y) % 7 == 0 ? Bolt : Steel);
                }
            // Hazard stripes on the curb's corners (the way into the chamber stays clear).
            for (int i = 0; i < 6; i++)
            {
                uint h = (i / 2) % 2 == 0 ? Hazard : HazardDark;
                c.Set(i, 0, 1, h); c.Set(0, i, 1, h); c.Set(29 - i, 29, 1, h); c.Set(29, 29 - i, 1, h);
                c.Set(29 - i, 0, 1, h); c.Set(29, i, 1, h); c.Set(i, 29, 1, h); c.Set(0, 29 - i, 1, h);
            }
            // Four emitter studs on the ring.
            foreach (var (sx, sy) in new[] { (14.5f, 5.4f), (14.5f, 23.6f), (5.4f, 14.5f), (23.6f, 14.5f) })
                for (int x = 0; x < 30; x++)
                    for (int y = 0; y < 30; y++)
                        if (Dist(x, y, sx, sy) < 1.3f) { c.Set(x, y, 0, Brass); c.Set(x, y, 1, lit.GlowDim); }
        }

        // ------------------------------------------------------------------ emitter pylons

        /// <summary>A pylon: a bolted footing, a steel shaft wound with copper, a brass collar and an orb leaning to the chamber.</summary>
        private static void Pylon(VoxCanvas c, Lit lit, float cx, float cy, int h, (float x, float y, float z) orb)
        {
            int x0 = (int)Math.Floor(cx) - 2, y0 = (int)Math.Floor(cy) - 2;
            c.Box(x0, y0, 1, x0 + 4, y0 + 4, 2, SteelDark);
            c.Set(x0, y0, 2, Bolt); c.Set(x0 + 4, y0, 2, Bolt); c.Set(x0, y0 + 4, 2, Bolt); c.Set(x0 + 4, y0 + 4, 2, Bolt);
            int top = (int)orb.z - 3;
            c.Cylinder(cx, cy, 3, top, 1.2f, Steel);
            if (h > 10)
                for (int z = 5; z <= top - 2; z += 2) c.Cylinder(cx, cy, z, z, 2.0f, z % 4 == 1 ? Copper : CopperDark);
            else c.Cylinder(cx, cy, 3, 3, 2.0f, Copper);
            c.Cylinder(cx, cy, top + 1, top + 1, 2.1f, Brass);
            // A short arm from the shaft to the orb, which leans out over the chamber.
            c.Line(cx - 0.5f, cy - 0.5f, top + 1, orb.x, orb.y, orb.z - 1.5f, SteelLight, 0.6f);
            c.Ellipsoid(orb.x, orb.y, orb.z, 1.7f, 1.7f, 1.7f, lit.Glow);
            c.Ellipsoid(orb.x, orb.y, orb.z, 1.0f, 1.0f, 1.0f, lit.GlowDim,
                        (x, y, z) => z > orb.z + 0.6f || (x - orb.x) * (Cx - orb.x) + (y - orb.y) * (Cy - orb.y) < 0);
            // Three brass prongs round the orb.
            for (int k = 0; k < 3; k++)
            {
                double a = 2 * Math.PI * k / 3 + 0.4;
                float px = orb.x + 1.9f * (float)Math.Cos(a), py = orb.y + 1.9f * (float)Math.Sin(a);
                c.Line(px, py, orb.z - 1.2f, px, py, orb.z + 0.6f, Brass);
            }
            // A warning light on the footing.
            c.Set(x0 + 2, y0 + 4, 3, lit.Red);
        }

        // ------------------------------------------------------------------ the specimen tank

        /// <summary>A tall tank of glowing fluid with something curled up inside, on the far left side.</summary>
        private static void Tank(VoxCanvas c, Lit lit)
        {
            const float tx = 25.5f, ty = 14.5f;
            c.Cylinder(tx, ty, 1, 2, 3.6f, SteelDark);
            c.Cylinder(tx, ty, 3, 16, 3.1f, lit.Fluid);
            // Glass shine on the camera's side, bubbles, and the specimen's dark shape pressed near the front.
            for (int x = 0; x < 30; x++)
                for (int y = 0; y < 30; y++)
                    for (int z = 3; z <= 16; z++)
                    {
                        if (c.Get(x, y, z) != lit.Fluid) continue;
                        float d = Dist(x, y, tx, ty);
                        float side = ((x - tx) * -0.7f + (y - ty) * 0.7f) / Math.Max(0.1f, d);
                        if (d > 2.4f && side > 0.55f && side < 0.85f && z > 4 && z < 15) c.Set(x, y, z, Glass);
                        else if (Hash(x, y, z, 31) < 0.06f) c.Set(x, y, z, lit.FluidLight);
                        else if (z <= 4) c.Set(x, y, z, lit.FluidDark);
                    }
            c.Ellipsoid(tx - 0.6f, ty + 0.6f, 9.5f, 2.2f, 2.2f, 3.2f, lit.Specimen, (x, y, z) => c.Get(x, y, z) == lit.Fluid || c.Get(x, y, z) == lit.FluidLight);
            // Steel struts, the cap and a pipe over to the chamber.
            foreach (double a in new[] { 0.8, 2.4, 3.9, 5.5 })
            {
                float sx = tx + 3.3f * (float)Math.Cos(a), sy = ty + 3.3f * (float)Math.Sin(a);
                c.Line(sx, sy, 3, sx, sy, 16, Steel);
            }
            c.Cylinder(tx, ty, 17, 18, 3.5f, Steel);
            c.Cylinder(tx, ty, 19, 19, 1.6f, SteelLight);
            c.Set((int)tx, (int)ty, 20, lit.Green);
            c.Line(tx - 2.5f, ty, 18, 21f, ty, 18, Copper, 0.5f);
            c.Line(21f, ty, 18, 21f, ty, 2, Copper, 0.5f);
        }

        // ------------------------------------------------------------------ the workbench

        /// <summary>A workbench along the far right side: flasks of coloured fluids, a rack of test tubes, an alembic.</summary>
        private static void Bench(VoxCanvas c, Lit lit)
        {
            c.Box(9, 1, 6, 20, 5, 6, Wood);
            c.Box(9, 1, 5, 20, 1, 5, WoodDark);
            foreach (int lx in new[] { 9, 20 }) foreach (int ly in new[] { 1, 5 }) c.Box(lx, ly, 1, lx, ly, 5, WoodDark);
            c.Box(10, 2, 3, 19, 5, 3, WoodDark); // shelf
            c.Box(11, 3, 4, 12, 4, 4, lit.Blue); c.Box(15, 3, 4, 16, 4, 4, lit.Amber); // jars on the shelf
            // An Erlenmeyer flask of purple, a round flask of orange.
            c.Cone(11.5f, 3f, 7, 10, 1.6f, 0.5f, lit.Purple);
            c.Set(11, 3, 11, Glass); c.Set(11, 3, 12, Glass);
            c.Ellipsoid(14.5f, 3f, 8.5f, 1.5f, 1.5f, 1.5f, lit.Orange);
            c.Set(14, 3, 10, Glass); c.Set(14, 3, 11, Glass);
            // The test-tube rack.
            c.Box(16, 2, 7, 19, 4, 7, Wood);
            uint[] tubes = { lit.Green, lit.Blue, lit.Red, lit.Purple };
            for (int i = 0; i < 4; i++) { c.Set(16 + i, 3, 8, tubes[i]); c.Set(16 + i, 3, 9, Glass); }
            // An alembic over a little burner at the end.
            c.Box(18, 2, 7, 19, 3, 7, SteelDark);
            c.Ellipsoid(19f, 2.5f, 9f, 1.1f, 1.1f, 1.2f, Glass);
            c.Line(19f, 2.5f, 10.5f, 21.5f, 1.5f, 9f, CopperDark);
        }

        // ------------------------------------------------------------------ the control console

        /// <summary>A low control desk on the near right side: a sloped top of screens and buttons, two levers.</summary>
        private static void Console(VoxCanvas c, Lit lit)
        {
            c.Box(1, 10, 1, 5, 19, 3, SteelDark);
            c.Box(2, 10, 4, 5, 19, 4, Steel);
            c.Box(4, 10, 5, 5, 19, 5, Steel);
            for (int y = 11; y <= 18; y++)
            {
                c.Set(3, y, 4, y % 3 == 0 ? lit.ScreenLine : lit.Screen);
                c.Set(2, y, 4, y % 4 == 1 ? lit.Green : y % 4 == 3 ? lit.Amber : Steel);
                c.Set(4, y, 5, (y == 12 || y == 16) ? lit.ScreenLine : lit.Screen);
            }
            c.Set(5, 12, 6, lit.Red);
            // Two levers.
            c.Line(2f, 17.5f, 5, 1f, 17.5f, 8, SteelLight); c.Set(1, 17, 8, lit.Red);
            c.Line(2f, 14.5f, 5, 1.5f, 14.5f, 8, SteelLight); c.Set(1, 14, 8, HazardDark);
            // Dials on the front.
            c.Set(1, 12, 2, Brass); c.Set(1, 15, 2, Brass); c.Set(1, 18, 2, Brass);
        }

        // ------------------------------------------------------------------ the capacitor bank

        /// <summary>A low row of capacitors on the near left side, with copper caps and lights.</summary>
        private static void Capacitors(VoxCanvas c, Lit lit)
        {
            c.Box(10, 24, 1, 20, 28, 1, SteelDark);
            float[] xs = { 11.5f, 14.5f, 17.5f };
            for (int i = 0; i < xs.Length; i++)
            {
                c.Cylinder(xs[i], 26f, 2, 5, 1.4f, i % 2 == 0 ? Rubber : SteelDark);
                c.Cylinder(xs[i], 26f, 6, 6, 1.0f, Copper);
                c.Set((int)xs[i], 26, 7, lit.GlowDim);
                c.Set((int)xs[i], 27, 4, i == 1 ? lit.Amber : lit.Green);
            }
            c.Line(11.5f, 26f, 7, 17.5f, 26f, 7, CopperDark);
        }

        /// <summary>Thick cables over the floor from the machines to the chamber's ring.</summary>
        private static void Cables(VoxCanvas c)
        {
            c.Line(21f, 14.5f, 1, 24f, 14.5f, 1, Cable, 0.4f);   // the tank
            c.Line(6f, 14.5f, 1, 5.5f, 14.5f, 1, Cable, 0.4f);   // the console
            c.Line(14.5f, 23.5f, 1, 14.5f, 24f, 1, Cable, 0.4f); // the capacitors
            c.Line(7f, 6f, 1, 8.5f, 7.5f, 1, Cable, 0.4f);        // the right pylon
            c.Line(22f, 7f, 1, 20.5f, 8.5f, 1, Cable, 0.4f);      // the far pylon
            c.Line(22f, 22f, 1, 20.5f, 20.5f, 1, Cable, 0.4f);    // the left pylon
        }

        private static float Dist(float x, float y, float cx, float cy) => (float)Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
    }
}

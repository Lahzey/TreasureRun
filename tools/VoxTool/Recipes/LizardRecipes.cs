using System;
using TreasureRun.Vox;
using static VoxTool.Recipes.VoxCanvas;

namespace VoxTool.Recipes
{
    /// <summary>
    /// The Lizard Nest (docs/poi-plan.md §8): the nest (whole, and hatched), and the two monsters that come out of it as
    /// separate parts, so the game can animate them (<c>NestCreatures</c> in src/TreasureRun/Pois). Axes: x across, y
    /// forward (the way the creature walks), z up. The nest is at voxel size 0.1 like every building; the creatures' parts
    /// are scaled down in the game (the lizard to 0.03 per voxel, the hatchling to 0.02).
    /// </summary>
    public static class LizardRecipes
    {
        private static readonly uint Scale = C(84, 128, 56), ScaleDark = C(52, 86, 42), ScaleLight = C(126, 166, 72), Spot = C(34, 58, 30);
        private static readonly uint Belly = C(214, 198, 136), BellyShade = C(178, 162, 104);
        private static readonly uint Crest = C(212, 92, 38), CrestTip = C(250, 168, 60);
        private static readonly uint Eye = C(250, 214, 60), Pupil = C(22, 18, 14), Mouth = C(132, 44, 40), Claw = C(232, 224, 200);
        private static readonly uint Pale = C(98, 160, 60), PaleLight = C(204, 208, 128), PaleDark = C(56, 104, 40), Fin = C(132, 190, 88), FinVein = C(88, 142, 56), Black = C(16, 20, 16), White = C(244, 250, 240);
        private static readonly uint Twig = C(112, 82, 52), TwigDark = C(74, 54, 34), TwigLight = C(150, 116, 76), TwigBleach = C(178, 158, 120);
        private static readonly uint Straw = C(206, 184, 110), StrawDark = C(166, 144, 82);
        private static readonly uint Egg = C(228, 222, 192), EggShade = C(200, 192, 158), EggSpeck = C(132, 124, 88), ShellInside = C(244, 240, 220), Slime = C(156, 204, 72);

        // ------------------------------------------------------------------ the nest

        /// <summary>The nest with four leathery eggs in its bowl. 12 x 12 x 6 (a little wider than its cell).</summary>
        public static VoxModel Nest() => NestModel(hatched: false);

        /// <summary>The nest after the lizards have hatched: broken shells, shell bits and slime in the bowl.</summary>
        public static VoxModel NestHatched() => NestModel(hatched: true);

        private static readonly (float x, float y)[] Eggs = { (4.3f, 4.4f), (6.8f, 4.8f), (5.0f, 6.9f), (7.1f, 7.1f) };

        private static VoxModel NestModel(bool hatched)
        {
            var c = new VoxCanvas(12, 12, 6);
            const float cx = 5.5f, cy = 5.5f;
            // The rim: a ring of woven twigs, highest along its middle, ragged on top.
            for (int x = 0; x < 12; x++)
                for (int y = 0; y < 12; y++)
                {
                    float d = Dist(x, y, cx, cy);
                    if (d > 5.9f) continue;
                    if (d < 2.7f)
                    {
                        c.Set(x, y, 0, Hash(x, y, 0, 3) < 0.4f ? StrawDark : Straw);
                        if (Hash(x, y, 1, 4) < 0.35f) c.Set(x, y, 1, Straw);
                        continue;
                    }
                    int h = (int)Math.Round(3.4f - Math.Abs(d - 4.3f) * 1.3f + Hash(x, y, 0, 5) * 1.2f);
                    for (int z = 0; z <= Math.Min(4, h); z++)
                    {
                        float k = Hash(x, y, z, 6);
                        c.Set(x, y, z, k < 0.3f ? TwigDark : k < 0.75f ? Twig : k < 0.92f ? TwigLight : TwigBleach);
                    }
                }
            // Sticks laid round the rim (roughly along it), a few poking out.
            for (int i = 0; i < 20; i++)
            {
                float a = (float)(2 * Math.PI * i / 20 + Hash(i, 0, 0, 7) * 0.4);
                float r = 4.1f + Hash(i, 1, 0, 8) * 0.9f, z = 1.5f + Hash(i, 2, 0, 9) * 2.5f;
                float tx = (float)-Math.Sin(a), ty = (float)Math.Cos(a);
                float rx = (float)Math.Cos(a), ry = (float)Math.Sin(a);
                float len = 2.4f + Hash(i, 3, 0, 10) * 1.6f, out_ = i % 4 == 0 ? 1.6f : 0.2f;
                float x0 = cx + rx * r - tx * len, y0 = cy + ry * r - ty * len;
                float x1 = cx + rx * (r + out_) + tx * len, y1 = cy + ry * (r + out_) + ty * len;
                uint col = i % 3 == 0 ? TwigBleach : i % 3 == 1 ? TwigDark : TwigLight;
                c.Line(x0, y0, z, x1, y1, z + (Hash(i, 4, 0, 11) - 0.5f) * 1.5f, col);
            }
            // A few twigs sticking up out of the rim.
            foreach (var (sx, sy) in new[] { (1, 4), (9, 2), (10, 8), (3, 10) })
                c.Line(sx, sy, 3, sx + (sx < 6 ? -0.6f : 0.6f), sy + (sy < 6 ? -0.6f : 0.6f), 5, TwigBleach);

            if (!hatched)
            {
                foreach (var (ex, ey) in Eggs)
                    c.Ellipsoid(ex, ey, 2.2f, 1.25f, 1.25f, 1.9f, Egg);
                c.Recolor((x, y, z, col) => col == Egg && Hash(x, y, z, 12) < 0.22f, EggSpeck);
                c.Recolor((x, y, z, col) => col == Egg && z <= 1, EggShade);
            }
            else
            {
                // Broken shells: the lower part of each egg, open, with a jagged rim and a pale inside.
                foreach (var (ex, ey) in Eggs)
                {
                    c.Ellipsoid(ex, ey, 2.2f, 1.25f, 1.25f, 1.9f, Egg, (x, y, z) => z <= 2 - (Hash(x, y, 0, 13) < 0.4f ? 1 : 0));
                    c.Ellipsoid(ex, ey, 2.2f, 0.7f, 0.7f, 1.4f, ShellInside, (x, y, z) => z >= 1 && z <= 2);
                }
                c.Recolor((x, y, z, col) => col == ShellInside && z == 2, 0);
                foreach (var (ex, ey) in Eggs)
                {
                    int x = (int)Math.Round(ex), y = (int)Math.Round(ey);
                    if (c.Get(x, y, 1) != 0) c.Set(x, y, 1, Slime);
                }
                // Shell bits and slime in the straw.
                foreach (var (x, y, col) in new[] { (3, 6, Egg), (6, 3, Egg), (8, 6, EggShade), (4, 8, Egg), (7, 9, Slime), (2, 5, Slime), (5, 5, Slime) })
                    if (c.Get(x, y, 1) == 0) c.Set(x, y, 1, col);
            }
            return c.ToModel(hatched ? "lizard_nest_hatched" : "lizard_nest");
        }

        // ------------------------------------------------------------------ the lizard (0.03 per voxel in game)

        /// <summary>The lizard's body: a low, broad ellipsoid, dark green on top with blotches, a cream belly, an orange crest of spikes. 11 x 16 x 8.</summary>
        public static VoxModel LizardBody()
        {
            var c = new VoxCanvas(11, 16, 8);
            c.Ellipsoid(5f, 7.5f, 2.4f, 5.3f, 8.3f, 3.3f, Scale);
            Skin(c, 2);
            for (int y = 2; y <= 13; y += 2) Spike(c, 5, y, y % 4 == 0 ? 2 : 1);
            return c.ToModel("lizard_body");
        }

        /// <summary>
        /// The head: a broad skull narrowing to a blunt snout (forward, +y), yellow eyes with a slit pupil on each side
        /// under a dark brow, nostrils, a dark mouth line, two crest spikes behind. 9 x 11 x 7.
        /// </summary>
        public static VoxModel LizardHead()
        {
            var c = new VoxCanvas(9, 11, 7);
            c.Ellipsoid(4f, 3.5f, 2.6f, 4.2f, 3.8f, 2.8f, Scale);
            c.Ellipsoid(4f, 7f, 2.0f, 2.9f, 3.6f, 2.0f, Scale);
            Skin(c, 1);
            for (int y = 4; y <= 9; y++)
                foreach (int x in new[] { Outer(c, y, 1, -1), Outer(c, y, 1, 1) })
                    if (x >= 0) c.Set(x, y, 1, Mouth);
            foreach (int side in new[] { -1, 1 })
            {
                // A yellow eye three voxels long with a black slit in the middle, a dark brow over it.
                for (int y = 3; y <= 5; y++)
                {
                    int x = Outer(c, y, 3, side);
                    if (x < 0) continue;
                    c.Set(x, y, 3, y == 4 ? Pupil : Eye);
                    int xb = Outer(c, y, 4, side);
                    if (xb >= 0) c.Set(xb, y, 4, Spot);
                }
            }
            c.Set(3, 9, 3, Spot); c.Set(5, 9, 3, Spot);
            Spike(c, 4, 1, 1); Spike(c, 4, 3, 1);
            return c.ToModel("lizard_head");
        }

        /// <summary>The tail's three pieces, tapering to a tip; each starts at its front (+y) end. Dark bands every few voxels.</summary>
        public static VoxModel LizardTail1() => Tail("lizard_tail1", 7, 8, 5, 3.2f, 2.4f, 2.2f, 1.8f, spikes: true);
        public static VoxModel LizardTail2() => Tail("lizard_tail2", 5, 8, 4, 2.3f, 1.6f, 1.7f, 1.3f, spikes: true);
        public static VoxModel LizardTail3() => Tail("lizard_tail3", 3, 9, 3, 1.5f, 0.5f, 1.3f, 0.5f, spikes: false);

        private static VoxModel Tail(string name, int sx, int sy, int sz, float r0, float r1, float h0, float h1, bool spikes)
        {
            var c = new VoxCanvas(sx, sy, sz + 1);
            float cx = (sx - 1) / 2f, cz = (sz - 1) / 2f;
            for (int y = 0; y < sy; y++)
            {
                float t = y / (float)(sy - 1); // 0 at the back end, 1 at the front
                float rx = r1 + (r0 - r1) * t, rz = h1 + (h0 - h1) * t;
                for (int x = 0; x < sx; x++)
                    for (int z = 0; z < sz; z++)
                    {
                        float dx = (x - cx) / rx, dz = (z - cz) / rz;
                        if (dx * dx + dz * dz <= 1f) c.Set(x, y, z, Scale);
                    }
            }
            Skin(c, 1);
            c.Recolor((x, y, z, col) => z > 0 && (y % 4 == 1) && col != Belly, Spot);
            if (spikes) for (int y = 1; y < sy; y += 3) Spike(c, (int)cx, y, 1);
            return c.ToModel(name);
        }

        /// <summary>A sprawling right leg: a thigh straight out from the hip, the shin down to a splayed foot with pale claws. Hip at x = 0, top. 6 x 5 x 6.</summary>
        public static VoxModel LizardLegRight() => Leg(mirror: false);
        public static VoxModel LizardLegLeft() => Leg(mirror: true);

        private static VoxModel Leg(bool mirror)
        {
            var c = new VoxCanvas(6, 5, 6);
            c.Line(0, 2, 4.5f, 3.2f, 2, 4.2f, Scale, 1f);
            c.Line(3.4f, 2, 4.2f, 4.0f, 2.2f, 1f, Scale, 1f);
            c.Box(3, 1, 0, 5, 3, 0, ScaleDark);
            c.Set(3, 4, 0, ScaleDark); c.Set(5, 4, 0, ScaleDark); c.Set(4, 4, 0, ScaleDark);
            foreach (int x in new[] { 3, 4, 5 }) c.Set(x, 4, 0, Claw);
            c.Set(5, 0, 0, ScaleDark);
            c.Recolor((x, y, z, col) => col == Scale && z >= 4 && c.Get(x, y, z + 1) == 0 && Hash(x, y, z, 21) < 0.5f, ScaleDark);
            c.Recolor((x, y, z, col) => col == Scale && z <= 3 && x <= 2, Belly);
            if (mirror)
            {
                var m = new VoxCanvas(6, 5, 6);
                for (int x = 0; x < 6; x++) for (int y = 0; y < 5; y++) for (int z = 0; z < 6; z++) m.Set(5 - x, y, z, c.Get(x, y, z));
                c = m;
            }
            return c.ToModel(mirror ? "lizard_leg_left" : "lizard_leg_right");
        }

        /// <summary>Colours a scaly part: a cream belly up to <paramref name="belly"/>, a dark back, mottling and blotches, a pale stripe along the flank.</summary>
        private static void Skin(VoxCanvas c, int belly)
        {
            int top = 0;
            for (int x = 0; x < c.SizeX; x++) for (int y = 0; y < c.SizeY; y++) for (int z = 0; z < c.SizeZ; z++) if (c.Get(x, y, z) != 0) top = Math.Max(top, z);
            c.Recolor((x, y, z, col) => z < belly, Belly);
            c.Recolor((x, y, z, col) => z == belly && Hash(x, y, z, 1) < 0.5f, BellyShade);
            c.Recolor((x, y, z, col) => z > belly && z >= top - 1, ScaleDark);
            c.Recolor((x, y, z, col) => z > belly && z < top - 1 && Hash(x, y, z, 2) < 0.25f, ScaleLight);
            c.Recolor((x, y, z, col) => z > belly && Hash(x / 2, y / 2, 0, 3) < 0.16f, Spot);
        }

        /// <summary>A crest spike on top of the column at (x, y): <paramref name="height"/> voxels, the tip lighter.</summary>
        private static void Spike(VoxCanvas c, int x, int y, int height)
        {
            int top = -1;
            for (int z = 0; z < c.SizeZ; z++) if (c.Get(x, y, z) != 0) top = z;
            if (top < 0) return;
            for (int i = 1; i <= height; i++) c.Set(x, y, top + i, i == height ? CrestTip : Crest);
        }

        /// <summary>The outermost filled x at (y, z) on one side (-1: towards x = 0); -1 if the row is empty.</summary>
        private static int Outer(VoxCanvas c, int y, int z, int side)
        {
            if (side < 0) { for (int x = 0; x < c.SizeX; x++) if (c.Get(x, y, z) != 0) return x; }
            else for (int x = c.SizeX - 1; x >= 0; x--) if (c.Get(x, y, z) != 0) return x;
            return -1;
        }

        // ------------------------------------------------------------------ the hatchling (0.02 per voxel in game)

        /// <summary>The hatchling's head: a big round green blob (a tadpole's), dark on top, a pale belly, big black eyes with a glint in each. 7 x 7 x 5.</summary>
        public static VoxModel HatchlingHead()
        {
            var c = new VoxCanvas(7, 7, 5);
            c.Ellipsoid(3f, 3.1f, 2f, 3.5f, 3.6f, 2.4f, Pale);
            c.Recolor((x, y, z, col) => z <= 0, PaleLight);
            c.Recolor((x, y, z, col) => z >= 3, PaleDark);
            c.Recolor((x, y, z, col) => z == 2 && Hash(x, y, z, 31) < 0.3f, PaleDark);
            foreach (int x in new[] { 0, 6 })
            {
                c.Set(x, 4, 2, Black); c.Set(x, 5, 2, Black); c.Set(x, 4, 3, Black); c.Set(x, 5, 3, White);
            }
            c.Set(2, 6, 1, PaleDark); c.Set(3, 6, 1, PaleDark); c.Set(4, 6, 1, PaleDark);
            return c.ToModel("hatchling_head");
        }

        /// <summary>The hatchling's body pieces behind the head, smaller each time, and its fin.</summary>
        public static VoxModel HatchlingBody1() => Blob("hatchling_body1", 4, 3, 4, Pale);
        public static VoxModel HatchlingBody2() => Blob("hatchling_body2", 3, 3, 3, Pale);
        public static VoxModel HatchlingBody3() => Blob("hatchling_body3", 2, 3, 2, Pale);

        private static VoxModel Blob(string name, int sx, int sy, int sz, uint col)
        {
            var c = new VoxCanvas(sx, sy, sz);
            c.Ellipsoid((sx - 1) / 2f, (sy - 1) / 2f, (sz - 1) / 2f, sx / 2f + 0.1f, sy / 2f + 0.3f, sz / 2f + 0.1f, col);
            c.Recolor((x, y, z, cc) => z == sz - 1, PaleDark);
            c.Recolor((x, y, z, cc) => z == 0 && sz > 2, PaleLight);
            return c.ToModel(name);
        }

        /// <summary>The tail fin: thin and upright, widening and then rounding off towards its tip (the back, y = 0). 1 x 6 x 4.</summary>
        public static VoxModel HatchlingFin()
        {
            var c = new VoxCanvas(1, 6, 4);
            int[] h = { 2, 3, 4, 4, 3, 2 };
            for (int y = 0; y < 6; y++)
            {
                int lo = (4 - h[y]) / 2;
                for (int z = lo; z < lo + h[y]; z++) c.Set(0, y, z, z == lo + h[y] / 2 ? FinVein : Fin);
            }
            return c.ToModel("hatchling_fin");
        }

        private static float Dist(float x, float y, float cx, float cy) => (float)Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
    }
}

using System;
using TreasureRun.Vox;
using static VoxTool.Recipes.VoxCanvas;

namespace VoxTool.Recipes
{
    /// <summary>
    /// The Wizard's Tower (docs/poi-plan.md §7): a plain wizard's tower, and one neat, symmetric gadget per spell as separate
    /// models, so the game can place them round the tower at different sides and heights (the chaos is in where they
    /// are, not in what they look like) and animate them. Voxel size 0.1; every model's origin is the centre of its
    /// bottom. The game's layout of the parts is in <c>WizardGadgets</c> (src/TreasureRun/Pois/WizardGadgets.cs).
    /// </summary>
    public static class WizardRecipes
    {
        private static readonly uint Stone = C(124, 118, 132), StoneDark = C(96, 92, 106), StoneLight = C(152, 146, 158), Mortar = C(80, 76, 88);
        private static readonly uint Wood = C(122, 86, 52), WoodDark = C(84, 58, 36), WoodLight = C(150, 110, 70);
        private static readonly uint Iron = C(58, 60, 68), IronLight = C(96, 100, 112);
        private static readonly uint Hat = C(70, 50, 132), HatLight = C(94, 68, 158), HatDark = C(50, 36, 98);
        private static readonly uint Gold = C(224, 184, 72), GoldBright = C(255, 228, 120), GoldDark = C(170, 128, 40);
        private static readonly uint Amber = C(255, 198, 96), AmberHot = C(255, 234, 168);
        private static readonly uint Ice = C(168, 228, 255), IceWhite = C(232, 250, 255), IceDeep = C(104, 184, 238);
        private static readonly uint Bone = C(232, 226, 204), Ghoul = C(120, 255, 150), GhoulDark = C(46, 128, 76);
        private static readonly uint Copper = C(186, 116, 52), CopperDark = C(136, 80, 34);
        private static readonly uint Arcane = C(74, 52, 104), ArcaneDark = C(52, 36, 76), Rune = C(255, 150, 60), RuneHot = C(255, 214, 120), Ember = C(255, 124, 40);
        private static readonly uint Orb = C(150, 220, 255), OrbWhite = C(236, 252, 255);
        private static readonly uint Cloth = C(226, 222, 210);

        // ------------------------------------------------------------------ the tower

        /// <summary>
        /// The tower itself: a round fieldstone tower on a plinth, a stone band halfway up and a corbelled ring under the
        /// roof, an arched oak door (facing the camera, 135 degrees: world -x, +z), three glowing arched windows, and a
        /// tall indigo wizard's hat with a gold band and a few stars, its tip bending a little. 14 x 14 x 42 voxels.
        /// </summary>
        public static VoxModel Tower()
        {
            var c = new VoxCanvas(14, 14, 42);
            const float cx = 6.5f, cy = 6.5f;
            c.Cylinder(cx, cy, 0, 0, 6.4f, StoneDark);
            c.Cylinder(cx, cy, 1, 1, 5.6f, Stone);
            for (int z = 2; z <= 24; z++)
            {
                float r = Radius(z);
                for (int x = 0; x < 14; x++)
                    for (int y = 0; y < 14; y++)
                    {
                        if (Dist(x, y, cx, cy) > r) continue;
                        int course = z / 2;
                        float a = (float)Math.Atan2(y - cy, x - cx);
                        int block = (int)Math.Floor((a + Math.PI) * 2.6 + (course % 2) * 0.5);
                        float h = Hash(block, course, 0, 11);
                        uint col = h < 0.3f ? StoneDark : h < 0.8f ? Stone : StoneLight;
                        if (z % 2 == 0 && Hash(block, course, 1, 12) < 0.35f) col = Mortar;
                        c.Set(x, y, z, col);
                    }
            }
            Ring(c, 12, 0.8f, StoneLight);
            Ring(c, 23, 0.8f, StoneLight);
            Ring(c, 24, 1.1f, StoneLight);
            // Door (135 degrees, towards the camera): stone arch, oak with a dark seam, iron bands, a brass ring, a step.
            Surface(c, 135f, 2, 8, 1.9f, 1.4f, StoneLight, arch: true);
            Surface(c, 135f, 2, 7, 1.4f, 1.2f, WoodLight, arch: true);
            Surface(c, 135f, 2, 7, 0.35f, 1.3f, WoodDark);
            Surface(c, 135f, 3, 3, 1.4f, 1.3f, Iron);
            Surface(c, 135f, 6, 6, 1.4f, 1.3f, Iron);
            Surface(c, 142f, 4, 4, 0.4f, 1.4f, Gold);
            // Windows: the balcony's (175), one under the lantern (95), one at the back (300).
            Window(c, 175f, 16, 19);
            Window(c, 95f, 7, 10);
            Window(c, 300f, 13, 16);
            // The hat: a brim with a gold band, a cone whose top bends over a little.
            const int brim = 25, tip = 41;
            for (int z = brim; z <= tip; z++)
            {
                float t = (z - brim) / (float)(tip - brim);
                float bend = t > 0.7f ? 2.2f * (t - 0.7f) / 0.3f : 0f;
                float r = z == brim ? 6.4f : 5.0f * (1f - t) + 0.45f;
                float hx = cx + bend * 0.7f, hy = cy - bend * 0.7f;
                for (int x = 0; x < 14; x++)
                    for (int y = 0; y < 14; y++)
                    {
                        float d = Dist(x, y, hx, hy);
                        if (d > r) continue;
                        float h = Hash(x, y, z, 31);
                        uint col = h < 0.25f ? HatDark : h < 0.85f ? Hat : HatLight;
                        if (z == brim + 1 || z == brim + 2) col = Gold;
                        else if (z > brim + 3 && (x * 7 + y * 3 + z * 5) % 23 == 0) col = GoldBright;
                        c.Set(x, y, z, col);
                    }
            }
            return c.ToModel("wizard_tower");
        }

        private static float Radius(float z) => 4.6f - 0.016f * (z - 2);

        // ------------------------------------------------------------------ fire: the arcane chimney

        /// <summary>A plain chimney segment: a round ring of arcane stone, 5 across, 2 high (the game stacks them from the ground up).</summary>
        public static VoxModel ChimneyRing()
        {
            var c = new VoxCanvas(5, 5, 2);
            c.Cylinder(2f, 2f, 0, 1, 2.45f, Arcane);
            c.Recolor((x, y, z, col) => z == 1 && (x + y) % 2 == 0, ArcaneDark);
            return c.ToModel("chimney_ring");
        }

        /// <summary>A rune segment: dark arcane stone with a glowing rune on each of four sides.</summary>
        public static VoxModel ChimneyRune()
        {
            var c = new VoxCanvas(5, 5, 2);
            c.Cylinder(2f, 2f, 0, 1, 2.45f, ArcaneDark);
            foreach (var (x, y) in new[] { (2, 0), (2, 4), (0, 2), (4, 2) }) { c.Set(x, y, 0, Rune); c.Set(x, y, 1, RuneHot); }
            return c.ToModel("chimney_rune");
        }

        /// <summary>The chimney's mouth: a flared brazier with a gold lip round a bed of embers. 7 x 7 x 4.</summary>
        public static VoxModel ChimneyCrown()
        {
            var c = new VoxCanvas(7, 7, 4);
            c.Cylinder(3f, 3f, 0, 0, 2.5f, Arcane);
            c.Cylinder(3f, 3f, 1, 1, 3.0f, Arcane);
            c.Cylinder(3f, 3f, 2, 2, 3.4f, ArcaneDark, hollow: true, wall: 1.1f);
            c.Cylinder(3f, 3f, 3, 3, 3.5f, Gold, hollow: true, wall: 1.0f);
            c.Cylinder(3f, 3f, 2, 2, 2.2f, Ember);
            c.Set(3, 3, 2, RuneHot);
            foreach (var (x, y) in new[] { (3, 0), (3, 6), (0, 3), (6, 3) }) c.Set(x, y, 1, Rune);
            return c.ToModel("chimney_crown");
        }

        /// <summary>An iron strap (from the tower's wall to a gadget), 4 long along x.</summary>
        public static VoxModel Strap()
        {
            var c = new VoxCanvas(4, 1, 1);
            c.Box(0, 0, 0, 3, 0, 0, Iron);
            c.Set(3, 0, 0, IronLight);
            return c.ToModel("strap");
        }

        // ------------------------------------------------------------------ lightning: the rod

        /// <summary>
        /// The lightning rod: a clamp at its foot, an iron rod, a copper coil of four rings and a glass orb on top (its centre
        /// at 2.15 above the foot). 5 x 5 x 24.
        /// </summary>
        public static VoxModel Rod()
        {
            var c = new VoxCanvas(5, 5, 24);
            c.Box(1, 1, 0, 3, 3, 1, Iron);
            c.Box(2, 2, 2, 2, 2, 19, IronLight);
            for (int z = 7; z <= 16; z += 3)
            {
                c.Cylinder(2f, 2f, z, z, 1.9f, z % 2 == 0 ? Copper : CopperDark, hollow: true, wall: 0.9f);
                c.Set(2, 2, z, Copper);
            }
            c.Box(1, 1, 18, 3, 3, 18, Iron);
            c.Ellipsoid(2f, 2f, 21f, 1.9f, 1.9f, 1.9f, Orb);
            c.Set(2, 2, 22, OrbWhite); c.Set(1, 2, 21, OrbWhite);
            return c.ToModel("rod");
        }

        // ------------------------------------------------------------------ ice: the balcony and its crystal

        /// <summary>
        /// A small round balcony on two brackets, its wall side at x = 0, with a railing and a stone pedestal in the middle
        /// (the crystal floats above it). 6 x 10 x 7.
        /// </summary>
        public static VoxModel IceBalcony()
        {
            var c = new VoxCanvas(6, 10, 7);
            for (int x = 0; x < 6; x++)
                for (int y = 0; y < 10; y++)
                    if (Dist(x, y, -0.5f, 4.5f) <= 6.2f) c.Set(x, y, 2, Wood);
            c.Recolor((x, y, z, col) => z == 2 && (y == 0 || y == 9 || Dist(x, y, -0.5f, 4.5f) > 5.4f), WoodDark);
            c.Line(0f, 2f, 0f, 3f, 2f, 1.5f, WoodDark);
            c.Line(0f, 7f, 0f, 3f, 7f, 1.5f, WoodDark);
            for (int x = 0; x < 6; x++)
                for (int y = 0; y < 10; y++)
                {
                    float d = Dist(x, y, -0.5f, 4.5f);
                    if (d > 6.2f || d < 5.3f) continue;
                    if ((x + y) % 3 == 0) c.Box(x, y, 3, x, y, 4, WoodDark);
                    c.Set(x, y, 5, Wood);
                }
            c.Box(2, 4, 3, 3, 5, 3, StoneLight);
            c.Box(2, 4, 4, 3, 5, 4, Stone);
            c.Set(2, 4, 4, IceDeep); c.Set(3, 5, 4, IceDeep);
            return c.ToModel("ice_balcony");
        }

        /// <summary>A floating ice crystal: a tall six-sided point with four smaller ones round its waist. 7 x 7 x 10.</summary>
        public static VoxModel IceCrystal()
        {
            var c = new VoxCanvas(7, 7, 10);
            Point(c, 3f, 3f, 0, 10, 1.8f, Ice, IceWhite);
            foreach (var (x, y) in new[] { (0.6f, 3f), (5.4f, 3f), (3f, 0.6f), (3f, 5.4f) }) Point(c, x, y, 2, 5, 0.9f, IceDeep, Ice);
            return c.ToModel("ice_crystal");
        }

        // ------------------------------------------------------------------ tornado: the anemometer

        /// <summary>
        /// The anemometer's arm: an iron bar out from the wall (x = 0) with a brace under it, and a mast standing at its end
        /// (x 7, rising 6). 9 x 3 x 9.
        /// </summary>
        public static VoxModel AnemometerArm()
        {
            var c = new VoxCanvas(9, 3, 9);
            c.Box(0, 1, 2, 7, 1, 2, Iron);
            c.Line(0f, 1f, 0f, 4f, 1f, 2f, Iron);
            c.Box(0, 0, 0, 0, 2, 3, IronLight);
            c.Box(7, 1, 2, 7, 1, 8, IronLight);
            c.Set(7, 1, 8, Gold);
            return c.ToModel("anemometer_arm");
        }

        /// <summary>The anemometer's rotor: a brass hub and three arms at 120 degrees, each with a white cup. 13 x 13 x 3.</summary>
        public static VoxModel AnemometerRotor()
        {
            var c = new VoxCanvas(13, 13, 3);
            c.Cylinder(6f, 6f, 0, 2, 1.2f, Gold);
            for (int k = 0; k < 3; k++)
            {
                double a = k * 2 * Math.PI / 3;
                float ex = 6f + 5f * (float)Math.Cos(a), ey = 6f + 5f * (float)Math.Sin(a);
                c.Line(6f, 6f, 1f, ex, ey, 1f, Iron);
                c.Ellipsoid(ex, ey, 1f, 1.3f, 1.3f, 1.2f, Cloth);
            }
            return c.ToModel("anemometer_rotor");
        }

        // ------------------------------------------------------------------ necromancy: the skull lantern

        /// <summary>The lantern's bracket: a wall plate (x = 0) and a bar out to a hook at x 6. 7 x 3 x 4.</summary>
        public static VoxModel LanternArm()
        {
            var c = new VoxCanvas(7, 3, 4);
            c.Box(0, 0, 0, 0, 2, 3, IronLight);
            c.Box(0, 1, 2, 6, 1, 2, Iron);
            c.Line(1f, 1f, 0f, 4f, 1f, 2f, Iron);
            c.Box(6, 1, 0, 6, 1, 1, Iron);
            return c.ToModel("lantern_arm");
        }

        /// <summary>The skull lantern: an iron cage with a cap and ring, a skull inside with green eyes, a green glow under it. 5 x 5 x 8.</summary>
        public static VoxModel Lantern()
        {
            var c = new VoxCanvas(5, 5, 8);
            c.Box(1, 1, 0, 3, 3, 0, Iron);
            foreach (var (x, y) in new[] { (0, 0), (4, 0), (0, 4), (4, 4) }) c.Box(x, y, 1, x, y, 5, Iron);
            c.Box(0, 0, 6, 4, 4, 6, IronLight);
            c.Box(1, 1, 7, 3, 3, 7, Iron);
            c.Set(2, 2, 7, IronLight);
            c.Box(1, 1, 2, 3, 3, 4, Bone);
            c.Set(1, 0, 3, Ghoul); c.Set(3, 0, 3, Ghoul);
            c.Set(1, 4, 3, Ghoul); c.Set(3, 4, 3, Ghoul);
            c.Box(1, 1, 1, 3, 3, 1, GhoulDark);
            c.Set(2, 2, 1, Ghoul);
            return c.ToModel("lantern");
        }

        // ------------------------------------------------------------------ goldify: the alembic

        /// <summary>The alembic: a tripod of iron legs carrying a round gold flask with a neck and a copper stopper. 7 x 7 x 9.</summary>
        public static VoxModel Alembic()
        {
            var c = new VoxCanvas(7, 7, 9);
            for (int k = 0; k < 3; k++)
            {
                double a = k * 2 * Math.PI / 3 + Math.PI / 2;
                c.Line(3f + 3f * (float)Math.Cos(a), 3f + 3f * (float)Math.Sin(a), 0f, 3f + 1.5f * (float)Math.Cos(a), 3f + 1.5f * (float)Math.Sin(a), 2.5f, Iron);
            }
            c.Ellipsoid(3f, 3f, 4.3f, 2.4f, 2.4f, 2.3f, Gold);
            c.Recolor((x, y, z, col) => col == Gold && z <= 3, GoldDark);
            c.Set(2, 2, 5, GoldBright);
            c.Box(3, 3, 6, 3, 3, 7, GoldBright);
            c.Set(3, 3, 8, CopperDark);
            return c.ToModel("alembic");
        }

        /// <summary>Two gold rings crossing at right angles (an armillary), spinning round the flask. 11 x 11 x 11.</summary>
        public static VoxModel AlembicRings()
        {
            var c = new VoxCanvas(11, 11, 11);
            for (int x = 0; x < 11; x++)
                for (int y = 0; y < 11; y++)
                    for (int z = 0; z < 11; z++)
                    {
                        float dx = x - 5f, dy = y - 5f, dz = z - 5f;
                        float rxz = (float)Math.Sqrt(dx * dx + dz * dz), ryz = (float)Math.Sqrt(dy * dy + dz * dz);
                        if (y == 5 && rxz <= 5.4f && rxz >= 4.5f) c.Set(x, y, z, (x + z) % 3 == 0 ? GoldBright : Gold);
                        if (x == 5 && ryz <= 5.4f && ryz >= 4.5f) c.Set(x, y, z, (y + z) % 3 == 0 ? GoldBright : Gold);
                    }
            return c.ToModel("alembic_rings");
        }

        // ------------------------------------------------------------------ helpers

        private static float Dist(float x, float y, float cx, float cy) => (float)Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));

        private static float Wrap(float a)
        {
            while (a > Math.PI) a -= (float)(2 * Math.PI);
            while (a < -Math.PI) a += (float)(2 * Math.PI);
            return a;
        }

        /// <summary>Paints (or builds out) a patch of the shaft's surface at an angle (0 = +x, 90 = +y), heights z0..z1.</summary>
        private static void Surface(VoxCanvas c, float deg, int z0, int z1, float halfWidth, float depth, uint col, bool arch = false, int outward = 0)
        {
            float th = deg * (float)Math.PI / 180f;
            for (int z = z0; z <= z1; z++)
            {
                float r = Radius(z);
                float hw = halfWidth;
                if (arch && z >= z1 - 1) hw *= z == z1 ? 0.45f : 0.8f;
                for (int x = 0; x < 14; x++)
                    for (int y = 0; y < 14; y++)
                    {
                        float d = Dist(x, y, 6.5f, 6.5f);
                        float a = Wrap((float)Math.Atan2(y - 6.5f, x - 6.5f) - th);
                        if (Math.Abs(a) * Math.Max(1f, r) > hw + 0.01f) continue;
                        if (outward > 0 ? d > r && d <= r + outward : d <= r && d > r - depth - 0.6f) c.Set(x, y, z, col);
                    }
            }
        }

        /// <summary>An arched window: a stone frame, its warm glow, a sill.</summary>
        private static void Window(VoxCanvas c, float deg, int z0, int z1)
        {
            Surface(c, deg, z0 - 1, z1 + 1, 1.9f, 0.8f, StoneLight, arch: true);
            Surface(c, deg, z0, z1, 1.0f, 1.2f, Amber, arch: true);
            Surface(c, deg, z0 + 1, z0 + 1, 0.4f, 1.3f, AmberHot);
            Surface(c, deg, z0 - 1, z0 - 1, 1.3f, 0.2f, StoneLight, outward: 1);
        }

        /// <summary>A stone ring round the shaft at height z, <paramref name="out"/> voxels proud of it.</summary>
        private static void Ring(VoxCanvas c, int z, float @out, uint col)
        {
            float r = Radius(z);
            for (int x = 0; x < 14; x++)
                for (int y = 0; y < 14; y++)
                {
                    float d = Dist(x, y, 6.5f, 6.5f);
                    if (d > r && d <= r + @out + 0.5f) c.Set(x, y, z, col);
                }
        }

        /// <summary>A six-sided crystal point: a column narrowing to a tip, lighter at the top.</summary>
        private static void Point(VoxCanvas c, float x, float y, int z0, int height, float radius, uint col, uint tip)
        {
            for (int z = z0; z < z0 + height; z++)
            {
                float t = (z - z0) / (float)height;
                float r = t < 0.55f ? radius : radius * (1f - (t - 0.55f) / 0.45f) + 0.2f;
                for (int xx = 0; xx < c.SizeX; xx++)
                    for (int yy = 0; yy < c.SizeY; yy++)
                        if (Dist(xx, yy, x, y) <= r) c.Set(xx, yy, z, t > 0.65f ? tip : col);
            }
        }
    }
}

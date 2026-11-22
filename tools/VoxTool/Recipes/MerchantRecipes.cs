using System;
using TreasureRun.Vox;
using static VoxTool.Recipes.VoxCanvas;

namespace VoxTool.Recipes
{
    /// <summary>
    /// Wandering Merchant wagons (docs/poi-plan.md §5), one look per merchant: a horseless market wagon parked on a 2 x 2
    /// footprint (20 x 20 voxels, voxel size 0.1). Axes: x across (x = 0 is the front, towards the path), y along the wagon,
    /// z up. The wagon body stands on x 6-17 with wheels outside it; the front column (x 0-3) holds crates, a barrel and a
    /// sign. Moving parts are separate models the game hinges: the awning over the serving window (x = 6 face, window
    /// y 4-15, z 8-14, hinge at z = 15) and two shutter leaves over the end window at the front end (y = 3 face, x 9-14,
    /// z 9-13). In game the serving side faces south-west and the front end south-east, both towards the camera.
    /// </summary>
    public static class MerchantRecipes
    {
        private sealed class Look
        {
            public uint Wood, WoodDark, WoodLight, Trim, Roof, RoofDark, StripeA, StripeB, Wheel, Rim, Metal, Glow, Sign;
            public uint[] Goods;
            public string Name;
        }

        private static Look Normal => new Look
        {
            Name = "normal",
            Wood = C(150, 98, 56), WoodDark = C(104, 66, 38), WoodLight = C(178, 126, 78), Trim = C(92, 56, 30),
            Roof = C(70, 122, 78), RoofDark = C(50, 92, 58), StripeA = C(198, 50, 42), StripeB = C(246, 234, 208),
            Wheel = C(110, 72, 40), Rim = C(62, 62, 68), Metal = C(74, 76, 84), Glow = C(255, 214, 120), Sign = C(240, 196, 70),
            Goods = new[] { C(206, 44, 40), C(64, 112, 222), C(74, 192, 84), C(212, 182, 120), C(244, 204, 72) },
        };

        private static Look Legendary => new Look
        {
            Name = "legendary",
            Wood = C(116, 42, 44), WoodDark = C(80, 28, 32), WoodLight = C(146, 60, 56), Trim = C(232, 182, 62),
            Roof = C(98, 42, 124), RoofDark = C(70, 28, 92), StripeA = C(116, 40, 134), StripeB = C(250, 204, 82),
            Wheel = C(84, 30, 34), Rim = C(214, 162, 52), Metal = C(226, 176, 58), Glow = C(255, 226, 130), Sign = C(250, 210, 80),
            Goods = new[] { C(250, 212, 84), C(232, 52, 70), C(70, 120, 240), C(70, 210, 120), C(255, 236, 150) },
        };

        private static Look Ascension => new Look
        {
            Name = "ascension",
            Wood = C(46, 54, 114), WoodDark = C(30, 36, 82), WoodLight = C(66, 78, 144), Trim = C(192, 202, 224),
            Roof = C(26, 32, 74), RoofDark = C(18, 22, 54), StripeA = C(42, 52, 132), StripeB = C(160, 230, 255),
            Wheel = C(30, 36, 82), Rim = C(186, 196, 220), Metal = C(186, 196, 220), Glow = C(140, 236, 255), Sign = C(130, 230, 255),
            Goods = new[] { C(120, 232, 255), C(184, 124, 255), C(232, 250, 255), C(90, 160, 255), C(255, 150, 240) },
        };

        public static VoxModel NormalWagon() => Wagon(Normal);
        public static VoxModel LegendaryWagon() => Wagon(Legendary);
        public static VoxModel AscensionWagon() => Wagon(Ascension);
        public static VoxModel NormalAwning() => Awning(Normal);
        public static VoxModel LegendaryAwning() => Awning(Legendary);
        public static VoxModel AscensionAwning() => Awning(Ascension);
        public static VoxModel NormalShutter() => Shutter(Normal);
        public static VoxModel LegendaryShutter() => Shutter(Legendary);
        public static VoxModel AscensionShutter() => Shutter(Ascension);

        private static VoxModel Wagon(Look k)
        {
            var c = new VoxCanvas(20, 20, 30);

            // Chassis, axles and wheels (outside the body: front x 4-5, back x 18-19).
            c.Box(6, 2, 5, 17, 18, 5, k.WoodDark);
            foreach (int y in new[] { 5, 14 })
            {
                c.Box(4, y, 3, 19, y, 4, k.Metal);
                Wheel(c, k, 4, 5, y, 3.5f);
                Wheel(c, k, 18, 19, y, 3.5f);
            }

            // Body: planked walls between corner posts and trim bands.
            for (int x = 6; x <= 17; x++)
                for (int y = 3; y <= 18; y++)
                    for (int z = 6; z <= 15; z++)
                    {
                        int plank = (x == 6 || x == 17) ? y / 2 : x / 2;
                        uint wood = plank % 2 == 0 ? k.Wood : k.WoodLight;
                        if (Hash(x, y, z, 3) < 0.04f) wood = k.WoodDark;
                        c.Set(x, y, z, wood);
                    }
            foreach (int x in new[] { 6, 17 })
                foreach (int y in new[] { 3, 18 })
                    c.Box(x, y, 5, x, y, 16, k.Trim);
            foreach (int z in new[] { 6, 15 })
            {
                c.Box(6, 3, z, 6, 18, z, k.Trim); c.Box(17, 3, z, 17, 18, z, k.Trim);
                c.Box(6, 3, z, 17, 3, z, k.Trim); c.Box(6, 18, z, 17, 18, z, k.Trim);
            }

            // Serving window (x = 6 face): a recess with goods on the counter and on a shelf; a ledge outside.
            c.ClearBox(6, 4, 8, 8, 15, 14);
            c.Box(9, 4, 8, 9, 15, 14, k.WoodDark);
            c.Box(8, 4, 11, 8, 15, 11, k.Trim);
            for (int y = 4; y <= 15; y++)
            {
                uint g = k.Goods[(y * 7 + 3) % k.Goods.Length];
                if (y % 3 != 0) c.Set(8, y, 12, g);
                if (y % 4 != 1) c.Set(7, y, 8, k.Goods[(y * 3 + 1) % k.Goods.Length]);
                if (y % 4 == 2) c.Set(7, y, 9, k.Goods[(y + 2) % k.Goods.Length]);
            }
            c.Box(6, 3, 7, 6, 16, 7, k.Trim);            // window sill
            c.Box(4, 4, 7, 5, 15, 7, k.WoodLight);        // fold-out ledge
            c.Box(5, 5, 6, 5, 5, 6, k.Trim); c.Box(5, 14, 6, 5, 14, 6, k.Trim);
            c.Box(6, 3, 8, 6, 3, 14, k.Trim); c.Box(6, 16, 8, 6, 16, 14, k.Trim);

            // End window at the front end (y = 3 face, towards the camera) behind the shutters, a lamp inside.
            c.ClearBox(9, 3, 9, 14, 4, 13);
            c.Box(9, 5, 9, 14, 5, 13, k.WoodDark);
            c.Set(11, 5, 11, k.Glow); c.Set(12, 5, 11, k.Glow);
            c.Box(8, 3, 8, 15, 3, 8, k.Trim); c.Box(8, 3, 14, 15, 3, 14, k.Trim);

            // Barrel roof with overhangs.
            for (int x = 5; x <= 18; x++)
            {
                double t = (x - 11.5) / 7.0;
                int top = 16 + (int)Math.Round(3.2 * (1 - t * t));
                for (int y = 2; y <= 19; y++)
                    for (int z = 16; z <= top; z++)
                    {
                        bool edge = x == 5 || x == 18 || y == 2 || y == 19;
                        uint col = edge ? k.RoofDark : (z == top ? k.Roof : k.RoofDark);
                        if (z == top && !edge && (y % 4 == 0)) col = k.RoofDark; // seams
                        c.Set(x, y, z, col);
                    }
            }

            // Drawbar resting on a block at the front end.
            foreach (int x in new[] { 9, 14 })
                c.Line(x, 3, 6, x, 0, 2, k.WoodDark);
            c.Box(9, 0, 1, 14, 0, 2, k.WoodDark);
            c.Box(10, 0, 0, 13, 1, 0, k.WoodLight);

            // Lantern on the front corner.
            c.Box(6, 2, 15, 6, 2, 15, k.Metal);
            c.Box(5, 2, 13, 5, 2, 14, k.Glow);
            c.Set(5, 2, 15, k.Metal);

            // Front column: crates with goods, a barrel, and a sign.
            c.Box(0, 2, 0, 3, 5, 3, k.WoodLight);
            c.Box(0, 2, 0, 3, 2, 3, k.Trim); c.Box(0, 5, 0, 3, 5, 3, k.Trim);
            c.Box(1, 3, 4, 2, 4, 4, k.Goods[0]); c.Set(1, 4, 5, k.Goods[3]);
            c.Cylinder(1.5f, 16.5f, 0, 5, 1.8f, k.Wood);
            c.Cylinder(1.5f, 16.5f, 1, 1, 1.9f, k.Metal);
            c.Cylinder(1.5f, 16.5f, 4, 4, 1.9f, k.Metal);
            c.Box(1, 15, 6, 2, 16, 6, k.Goods[2]);
            // Sign: a post with a board facing the path (x = 0), the shop's mark painted on it.
            c.Box(2, 10, 0, 2, 10, 7, k.Trim);
            c.Box(1, 8, 4, 1, 12, 8, k.Trim);
            c.Box(1, 9, 5, 1, 11, 7, k.Sign);
            c.Set(1, 10, 6, k.Trim);

            Decorate(c, k);
            return c.ToModel(k.Name + "_wagon");
        }

        /// <summary>Kind-specific decoration: a stovepipe, a gold crown, or a glowing crystal and stars.</summary>
        private static void Decorate(VoxCanvas c, Look k)
        {
            switch (k.Name)
            {
                case "normal":
                    c.Box(14, 15, 19, 14, 15, 22, k.Metal);
                    c.Box(13, 14, 22, 15, 16, 22, k.Metal);
                    break;
                case "legendary":
                    // Crown on the roof's front end, gold ridge and a red gem.
                    c.Box(9, 3, 20, 14, 5, 20, k.Trim);
                    foreach (int x in new[] { 9, 11, 12, 14 }) c.Box(x, 4, 21, x, 4, x == 11 || x == 12 ? 23 : 22, k.Trim);
                    c.Set(11, 3, 21, C(220, 40, 60)); c.Set(12, 3, 21, C(220, 40, 60));
                    for (int y = 6; y <= 18; y++) c.Set(11, y, 20, k.Trim);
                    break;
                case "ascension":
                    // Stars on the roof, a crystal spire on its ridge.
                    for (int x = 5; x <= 18; x++)
                        for (int y = 2; y <= 19; y++)
                            for (int z = 23; z >= 16; z--)
                                if (c.Get(x, y, z) != 0)
                                {
                                    if (Hash(x, y, z, 11) < 0.06f) c.Set(x, y, z, C(226, 242, 255));
                                    break;
                                }
                    // A cut crystal on a silver mount: widening, then tapering to a point, lit on the side facing the camera.
                    c.Box(10, 9, 20, 13, 12, 20, k.Trim);
                    c.Cone(11.5f, 10.5f, 21, 22, 1.2f, 2.1f, k.Goods[0]);
                    c.Cone(11.5f, 10.5f, 23, 28, 2.0f, 0.4f, k.Goods[0]);
                    c.Recolor((x, y, z, col) => z >= 21 && col == k.Goods[0] && (x >= 12 && y >= 11), C(214, 248, 255));
                    c.Recolor((x, y, z, col) => z >= 21 && col == k.Goods[0] && (x <= 11 && y <= 10), C(70, 170, 235));
                    break;
            }
        }

        /// <summary>A spoked wheel in the y-z plane, x0..x1 thick, centred at (y, zc).</summary>
        private static void Wheel(VoxCanvas c, Look k, int x0, int x1, int y, float zc)
        {
            float yc = y;
            for (int x = x0; x <= x1; x++)
                for (int yy = y - 4; yy <= y + 4; yy++)
                    for (int z = 0; z <= 8; z++)
                    {
                        float dy = yy - yc, dz = z + 0.5f - zc - 0.5f;
                        float d = (float)Math.Sqrt(dy * dy + dz * dz);
                        if (d > 3.7f) continue;
                        if (d > 2.7f) c.Set(x, yy, z, k.Rim);
                        else if (d < 1.1f) c.Set(x, yy, z, k.Metal);
                        else if (Math.Abs(dy) < 0.6f || Math.Abs(dz) < 0.6f || Math.Abs(Math.Abs(dy) - Math.Abs(dz)) < 0.5f) c.Set(x, yy, z, k.Wheel);
                    }
        }

        /// <summary>
        /// The awning: 7 voxels out from the hinge (x = 6 is the hinge side), 14 wide (y), striped from the hinge to the
        /// edge, with a toothed valance hanging from its outer edge (x = 0).
        /// </summary>
        private static VoxModel Awning(Look k)
        {
            var c = new VoxCanvas(7, 14, 3);
            for (int x = 0; x <= 6; x++)
                for (int y = 0; y < 14; y++)
                    c.Set(x, y, 2, x == 6 ? k.Trim : (y / 2) % 2 == 0 ? k.StripeA : k.StripeB);
            for (int y = 0; y < 14; y++)
            {
                uint col = (y / 2) % 2 == 0 ? k.StripeA : k.StripeB;
                c.Set(0, y, 1, col);
                if (y % 2 == 0) c.Set(0, y, 0, col);
            }
            return c.ToModel(k.Name + "_awning");
        }

        /// <summary>A shutter leaf: 3 wide (x), 1 thick (y), 5 tall (z), planks in a frame with two iron bands.</summary>
        private static VoxModel Shutter(Look k)
        {
            var c = new VoxCanvas(3, 1, 5);
            for (int x = 0; x < 3; x++)
                for (int z = 0; z < 5; z++)
                {
                    bool frame = z == 0 || z == 4;
                    uint col = frame ? k.Trim : x % 2 == 0 ? k.Wood : k.WoodLight;
                    if (z == 1 || z == 3) col = k.Metal;
                    c.Set(x, 0, z, col);
                }
            return c.ToModel(k.Name + "_shutter");
        }
    }
}

using System;
using TreasureRun.Vox;
using static VoxTool.Recipes.VoxCanvas;

namespace VoxTool.Recipes
{
    /// <summary>
    /// Castle models, second batch. Same rules as <see cref="CastleRecipes"/>: 32x32 footprint (3x3 build cells),
    /// designed from z = 0 and then grounded (lifted above the grass layer) unless noted.
    /// </summary>
    public static class CastleRecipes2
    {
        private const float Cx = 15.5f, Cy = 15.5f;

        private static readonly uint Stone = C(80, 79, 72), StoneDark = C(66, 64, 58), StoneLight = C(94, 93, 87), StonePale = C(132, 128, 118);
        private static readonly uint Marble = C(205, 198, 182), MarbleShade = C(170, 163, 148);
        private static readonly uint Gold = C(212, 177, 106), GoldDark = C(168, 128, 56), GoldBright = C(240, 210, 130);
        private static readonly uint Wood = C(110, 70, 34), WoodDark = C(85, 39, 0), Iron = C(58, 58, 64), IronLight = C(92, 92, 100);
        private static readonly uint Ore = C(196, 112, 40), OreDark = C(150, 78, 28);
        private static readonly uint Bark = C(101, 67, 33), BarkDark = C(74, 47, 22), Leaf = C(74, 125, 44), LeafDark = C(52, 95, 32), LeafLight = C(104, 160, 62), Moss = C(88, 120, 52);
        private static readonly uint Shroom = C(170, 50, 40), Spot = C(235, 225, 210);
        private static readonly uint Crypt = C(52, 50, 58), CryptDark = C(36, 34, 42), CryptLight = C(78, 76, 86), Soul = C(120, 240, 160), SoulDark = C(60, 170, 110), Bone = C(214, 206, 186);
        private static readonly uint Sand = C(214, 186, 128), SandDark = C(190, 160, 104), Banner = C(150, 40, 40), BannerGold = C(200, 160, 70);
        private static readonly uint Glass = C(176, 222, 236), GlassDeep = C(118, 182, 210), GlassEdge = C(232, 248, 252), GlassDark = C(80, 140, 176);

        // ------------------------------------------------------------------ Gilded Vault

        public static VoxModel GildedVault()
        {
            var c = new VoxCanvas(32, 32, 40);
            // Marble floor: stepped plinth with a checkered top.
            c.Box(0, 0, 0, 31, 31, 1, MarbleShade);
            c.Box(1, 1, 2, 30, 30, 2, Marble);
            c.Recolor((x, y, z, col) => z == 2 && (x / 2 + y / 2) % 2 == 0, MarbleShade);
            // Square hall: wooden plank walls, marble corner pilasters, gold trim bands.
            c.Box(8, 8, 3, 23, 23, 16, Wood);
            c.Recolor((x, y, z, col) => col == Wood && (x % 3 == 0 || y % 3 == 0) && c.IsSideSurface(x, y, z), WoodDark);
            foreach (var (x, y) in new[] { (8, 8), (23, 8), (8, 23), (23, 23) }) c.Box(x, y, 3, x, y, 16, Marble);
            c.Box(8, 8, 3, 23, 23, 3, GoldDark);
            c.Box(8, 8, 16, 23, 23, 16, Gold);
            // The same entrance on all four sides.
            VoxModel portico = Portico();
            for (int q = 0; q < 4; q++) c.Paste(portico, 0, 0, 0, q);
            // Roof: wooden top with a gold rim, a stepped wooden drum and a large golden dome.
            c.Box(8, 8, 17, 23, 23, 17, WoodDark);
            c.Walls(8, 8, 18, 23, 23, 18, Gold);
            c.Cylinder(Cx, Cy, 18, 20, 7.4f, WoodDark);
            c.Cylinder(Cx, Cy, 20, 20, 7.4f, GoldDark, hollow: true, wall: 1f);
            c.Ellipsoid(Cx, Cy, 20.5f, 6.8f, 6.8f, 9f, Gold, (x, y, z) => z >= 21);
            c.Recolor((x, y, z, col) => col == Gold && z >= 21 && (x + y) % 5 == 0, GoldBright);
            c.Box(15, 15, 30, 16, 16, 32, GoldDark); c.Box(15, 15, 33, 16, 16, 33, GoldBright);
            // Coin stacks in the four plinth corners.
            foreach (var (x, y) in new[] { (2, 2), (28, 2), (2, 28), (28, 28) })
                for (int z = 3; z <= 5; z++) c.Box(x, y, z, x + 1, y + 1, z, z % 2 == 0 ? Gold : GoldDark);
            return c.Grounded().ToModel("gilded_vault");
        }

        /// <summary>One entrance of the vault (front side, low Y) on a 32x32 canvas, pasted rotated 4 times.</summary>
        private static VoxModel Portico()
        {
            var c = new VoxCanvas(32, 32, 26);
            c.Box(8, 3, 3, 23, 7, 3, Marble);                          // portico floor
            c.Recolor((x, y, z, col) => (x / 2 + y / 2) % 2 == 0, MarbleShade);
            c.Box(10, 1, 2, 21, 2, 2, MarbleShade);                    // front step
            foreach (int x in new[] { 9, 13, 18, 22 })                 // marble columns with gold capitals and bases
            {
                c.Box(x, 4, 4, x, 4, 15, Marble);
                c.Set(x, 4, 4, Gold); c.Set(x, 4, 15, Gold);
            }
            c.Box(8, 3, 16, 23, 7, 17, WoodDark);                      // entablature
            c.Box(8, 3, 17, 23, 3, 17, Gold);                          // gold frieze
            for (int z = 18; z <= 22; z++)                             // thin pediment with gold edges
            {
                int inset = (z - 18) * 2;
                if (8 + inset > 23 - inset) break;
                c.Box(8 + inset, 3, z, 23 - inset, 4, z, Wood);
                c.Set(8 + inset, 3, z, Gold); c.Set(23 - inset, 3, z, Gold);
            }
            c.Box(13, 2, 18, 18, 2, 20, Gold); c.Box(14, 2, 19, 17, 2, 19, GoldBright); // coin sign
            c.Box(13, 7, 4, 18, 7, 12, Iron); c.Box(14, 7, 5, 17, 7, 11, IronLight);    // vault door
            c.Box(15, 6, 7, 16, 6, 9, Gold);                                             // door wheel
            return c.ToModel("vault_portico");
        }

        // ------------------------------------------------------------------ Siege Workshop

        public static VoxModel SiegeWorkshop()
        {
            var c = new VoxCanvas(32, 32, 34);
            // Squat stone keep.
            c.Box(4, 4, 0, 27, 27, 18, Stone);
            c.Recolor((x, y, z, col) => c.IsSideSurface(x, y, z) && Hash(x, y, z, 5) < 0.12f, StoneDark);
            c.Box(3, 3, 18, 28, 28, 19, StoneDark);              // roof rim
            for (int i = 3; i <= 28; i += 3) { c.Set(i, 3, 20, Stone); c.Set(i, 28, 20, Stone); c.Set(3, i, 20, Stone); c.Set(28, i, 20, Stone); }
            // Square corner towers, protruding from the walls and a bit taller (the wall cannons sit on top).
            foreach (var (x, y) in new[] { (0, 0), (25, 0), (0, 25), (25, 25) })
            {
                c.Box(x, y, 0, x + 6, y + 6, 22, StoneDark);
                c.Recolor((xx, yy, z, col) => xx >= x && xx <= x + 6 && yy >= y && yy <= y + 6 && z <= 22
                                              && c.IsSideSurface(xx, yy, z) && Hash(xx, yy, z, 9) < 0.15f, Stone);
                c.Box(x, y, 22, x + 6, y + 6, 22, Stone);
                for (int i = 0; i <= 6; i += 2)
                {
                    c.Set(x + i, y, 23, StoneDark); c.Set(x + i, y + 6, 23, StoneDark);
                    c.Set(x, y + i, 23, StoneDark); c.Set(x + 6, y + i, 23, StoneDark);
                }
            }
            // Big wooden gate with iron bands.
            c.Box(12, 3, 0, 19, 3, 9, Wood);
            foreach (int z in new[] { 2, 6 }) c.Box(12, 3, z, 19, 3, z, Iron);
            // Turret ring (the cannon itself is a separate, rotating model: SiegeCannon).
            c.Cylinder(Cx, Cy, 20, 21, 8.5f, Wood);
            c.Cylinder(Cx, Cy, 21, 21, 8.5f, WoodDark, hollow: true, wall: 1f);
            c.Cylinder(Cx, Cy, 21, 21, 3f, Iron);
            // Crane in the middle of the high-X wall, reaching outwards.
            c.Box(26, 15, 20, 26, 15, 31, Wood);
            c.Line(26, 15, 31, 31, 15, 29, Wood);
            c.Box(31, 15, 24, 31, 15, 28, Iron); c.Set(31, 15, 23, IronLight);
            // Cannonball pile.
            foreach (var (x, y, z) in new[] { (22, 9, 20), (23, 9, 20), (22, 10, 20), (23, 10, 20), (22, 9, 21) }) c.Set(x, y, z, Iron);
            return c.Grounded().ToModel("siege_workshop");
        }

        /// <summary>
        /// The Siege Workshop's rotating cannon. Barrel along +Y (= forward in game). The model's centre (pivot) is the
        /// carriage; the muzzle is 14.5 voxels forward and 7.5 up (SiegeCastleTower relies on this).
        /// </summary>
        public static VoxModel SiegeCannon()
        {
            var c = new VoxCanvas(12, 30, 12);
            // Carriage and wheels.
            c.Box(2, 9, 0, 9, 21, 3, Wood);
            c.Box(2, 9, 3, 9, 9, 6, WoodDark); c.Box(2, 21, 3, 9, 21, 5, WoodDark);
            foreach (int x in new[] { 1, 10 })
                for (int y = 10; y <= 20; y++) for (int z = 0; z <= 8; z++)
                    if (Math.Abs(Math.Sqrt((y - 15) * (y - 15) + (z - 4) * (z - 4)) - 3.6) < 0.8) c.Set(x, y, z, WoodDark);
            foreach (int x in new[] { 1, 10 }) c.Set(x, 15, 4, Iron);
            // Barrel with gold bands and a flared muzzle.
            for (int y = 8; y <= 29; y++)
            {
                float r = y >= 26 ? 3f : y <= 10 ? 1.6f : 2.5f;
                for (int x = 0; x < 12; x++)
                    for (int z = 0; z < 12; z++)
                    {
                        double d = Math.Sqrt((x - 5.5) * (x - 5.5) + (z - 7.5) * (z - 7.5));
                        if (d > r) continue;
                        c.Set(x, y, z, y == 16 || y == 22 ? Gold : y >= 27 ? IronLight : Iron);
                    }
            }
            c.Box(4, 29, 6, 7, 29, 9, C(30, 30, 34)); // bore
            return c.ToModel("siege_cannon");
        }

        /// <summary>Blocky cannonball.</summary>
        public static VoxModel SiegeBall()
        {
            var c = new VoxCanvas(5, 5, 5);
            c.Ellipsoid(2f, 2f, 2f, 2.6f, 2.6f, 2.6f, Iron);
            c.Set(3, 1, 3, IronLight); c.Set(3, 1, 4, IronLight); c.Set(4, 2, 3, IronLight);
            return c.ToModel("siege_ball");
        }

        /// <summary>Small wall cannon (treasure), barrel along +Y.</summary>
        public static VoxModel SiegeWallCannon()
        {
            var c = new VoxCanvas(7, 14, 6);
            c.Box(1, 3, 0, 5, 9, 1, Wood);
            for (int y = 2; y <= 13; y++)
                for (int x = 0; x < 7; x++) for (int z = 0; z < 6; z++)
                    if ((x - 3) * (x - 3) + (z - 3) * (z - 3) <= (y >= 12 ? 4 : 2.6)) c.Set(x, y, z, y == 7 ? Gold : Iron);
            c.Set(3, 13, 3, C(30, 30, 34));
            return c.ToModel("siege_wall_cannon");
        }

        /// <summary>Catapult frame (treasure); the throwing arm is a separate, animated model. Throws towards +Y.</summary>
        public static VoxModel SiegeCatapult()
        {
            var c = new VoxCanvas(12, 16, 10);
            c.Box(1, 1, 0, 10, 14, 1, Wood);                       // base frame
            c.ClearBox(3, 3, 1, 8, 12, 1);
            c.Box(1, 1, 0, 10, 14, 0, WoodDark);
            foreach (int x in new[] { 1, 10 })
            {
                c.Box(x, 7, 2, x, 7, 8, Wood);                     // uprights carrying the axle (y 7, z 8)
                c.Line(x, 3, 2, x, 7, 7, WoodDark);                // braces
                c.Line(x, 12, 2, x, 7, 7, WoodDark);
            }
            c.Box(2, 7, 8, 9, 7, 8, Iron);                         // axle
            c.Box(1, 13, 2, 10, 13, 4, WoodDark);                  // front stop beam
            return c.ToModel("siege_catapult");
        }

        /// <summary>
        /// Catapult throwing arm: beam along Y with the bucket (and boulder) at y = 0; the axle is at y 13.5, z 1.5
        /// (SiegeWeapon relies on this).
        /// </summary>
        public static VoxModel SiegeCatapultArm()
        {
            var c = new VoxCanvas(6, 18, 5);
            c.Box(2, 2, 1, 3, 17, 2, Wood);                        // beam
            c.Box(0, 0, 0, 5, 3, 3, WoodDark);                     // bucket
            c.ClearBox(1, 1, 2, 4, 2, 3);
            c.Box(1, 1, 2, 4, 2, 3, StonePale);                    // boulder
            c.Box(2, 13, 0, 3, 14, 3, Iron);                       // axle collar
            return c.ToModel("siege_catapult_arm");
        }

        // ------------------------------------------------------------------ Necropolis

        public static VoxModel Necropolis()
        {
            var c = new VoxCanvas(32, 32, 44);
            // Base platform with gravestones.
            c.Box(1, 1, 0, 30, 30, 1, CryptDark);
            c.Box(2, 2, 2, 29, 29, 2, Crypt);
            foreach (var (x, y) in new[] { (3, 4), (3, 26), (27, 4), (27, 26), (4, 15), (27, 15) })
            {
                c.Box(x, y, 3, x + 1, y, 6, StonePale);
                c.Set(x, y, 7, StonePale); c.Set(x + 1, y, 7, 0);
            }
            // Mausoleum hall.
            c.Box(8, 7, 3, 23, 24, 20, Crypt);
            c.Recolor((x, y, z, col) => c.IsSideSurface(x, y, z) && Hash(x, y, z, 9) < 0.15f, CryptLight);
            c.Box(7, 6, 20, 24, 25, 21, CryptDark);
            // Pointed doorway with green glow.
            for (int z = 3; z <= 12; z++)
            {
                int half = z < 10 ? 2 : 12 - z;
                if (half >= 0) c.Box(16 - half - 1, 7, z, 15 + half + 1, 7, z, Soul);
            }
            // Skull above the door.
            c.Box(14, 6, 14, 17, 6, 17, Bone); c.Set(14, 6, 15, CryptDark); c.Set(17, 6, 15, CryptDark); c.Box(15, 6, 14, 16, 6, 14, CryptDark);
            // Central spire and four corner spires with glowing slits.
            c.Cone(Cx, Cy, 21, 40, 5.5f, 0.6f, CryptDark);
            c.Box(15, 15, 41, 16, 16, 43, Soul);
            foreach (var (x, y) in new[] { (6.5f, 5.5f), (24.5f, 5.5f), (6.5f, 25.5f), (24.5f, 25.5f) })
            {
                c.Cylinder(x, y, 3, 24, 2.6f, Crypt);
                c.Cone(x, y, 25, 32, 2.6f, 0.5f, CryptDark);
                c.Recolor((xx, yy, z, col) => z >= 14 && z <= 18 && c.IsSideSurface(xx, yy, z)
                                              && Math.Abs(xx - x) < 0.8f && Math.Abs(yy - y) < 3f, Soul);
            }
            // Green windows along the hall.
            foreach (int z in new[] { 12, 13, 14 }) { c.Set(8, 12, z, SoulDark); c.Set(8, 19, z, SoulDark); c.Set(23, 12, z, SoulDark); c.Set(23, 19, z, SoulDark); }
            return c.Grounded().ToModel("necropolis");
        }

        // ------------------------------------------------------------------ Druid Grove

        public static VoxModel DruidGrove()
        {
            var c = new VoxCanvas(32, 32, 44);
            // Mossy ground mound.
            c.Ellipsoid(Cx, Cy, -2f, 15.5f, 15.5f, 5f, Moss, (x, y, z) => z >= 0);
            // Root buttresses spreading out.
            for (int i = 0; i < 6; i++)
            {
                double a = i / 6.0 * Math.PI * 2 + 0.3;
                float ex = Cx + (float)Math.Cos(a) * 14f, ey = Cy + (float)Math.Sin(a) * 14f;
                c.Line(Cx + (float)Math.Cos(a) * 4f, Cy + (float)Math.Sin(a) * 4f, 9, ex, ey, 1, BarkDark, 1.2f);
            }
            // Giant trunk, slightly tapering, with bark texture and a door.
            c.Cone(Cx, Cy, 0, 26, 6.2f, 4.2f, Bark);
            c.Recolor((x, y, z, col) => col == Bark && Hash(x, y, z / 3, 4) < 0.3f, BarkDark);
            for (int z = 3; z <= 10; z++) c.Box(14, 9, z, 17, 10, z, z > 8 ? BarkDark : WoodDark);
            c.Set(17, 9, 6, Gold);
            c.Set(10, 13, 15, C(255, 220, 120)); c.Set(21, 18, 18, C(255, 220, 120)); // lit windows
            // Branches.
            foreach (var (dx, dy) in new[] { (1, 0.4f), (-0.8f, 0.9f), (-0.6f, -1f), (0.7f, -0.8f) })
                c.Line(Cx, Cy, 22, Cx + dx * 11f, Cy + dy * 9f, 30, Bark, 1f);
            // Layered canopy.
            c.Ellipsoid(Cx, Cy, 32f, 15f, 15f, 7f, LeafDark);
            c.Ellipsoid(Cx, Cy, 35f, 12.5f, 12.5f, 7f, Leaf);
            c.Ellipsoid(Cx - 1, Cy + 1, 39f, 8f, 8f, 4.5f, LeafLight);
            c.Recolor((x, y, z, col) => (col == Leaf || col == LeafDark) && Hash(x, y, z, 2) < 0.12f, LeafLight);
            // Hollow out the canopy underside a bit so the trunk shows.
            c.Recolor((x, y, z, col) => z < 28 && (col == Leaf || col == LeafDark), 0);
            // Mushrooms.
            foreach (var (x, y) in new[] { (5, 20), (24, 7), (25, 24) })
            {
                c.Box(x, y, 2, x, y, 4, Spot);
                c.Box(x - 1, y - 1, 5, x + 1, y + 1, 5, Shroom); c.Set(x, y, 6, Shroom); c.Set(x + 1, y, 5, Spot);
            }
            return c.Grounded().ToModel("druid_grove");
        }

        // ------------------------------------------------------------------ Colosseum

        public static VoxModel Colosseum()
        {
            var c = new VoxCanvas(32, 32, 26);
            // Arena floor.
            c.Cylinder(Cx, Cy, 0, 0, 15.6f, SandDark);
            c.Cylinder(Cx, Cy, 1, 1, 12f, Sand);
            // Outer ring wall, two tiers of arches.
            c.Cylinder(Cx, Cy, 0, 17, 15.6f, StonePale, hollow: true, wall: 2.6f);
            c.Cylinder(Cx, Cy, 18, 19, 15.6f, MarbleShade, hollow: true, wall: 3f);
            for (int x = 0; x < 32; x++)
                for (int y = 0; y < 32; y++)
                {
                    double d = Math.Sqrt((x - Cx) * (x - Cx) + (y - Cy) * (y - Cy));
                    if (d < 13f || d > 15.7f) continue;
                    double a = Math.Atan2(y - Cy, x - Cx);
                    int sector = (int)Math.Floor((a + Math.PI) / (Math.PI * 2) * 20);
                    double local = (a + Math.PI) / (Math.PI * 2) * 20 - sector;
                    bool archColumn = local > 0.25 && local < 0.75;
                    if (archColumn)
                    {
                        for (int z = 3; z <= 7; z++) c.Set(x, y, z, 0);
                        for (int z = 11; z <= 14; z++) c.Set(x, y, z, 0);
                        if (d > 15f) { c.Set(x, y, 8, StoneLight); c.Set(x, y, 15, StoneLight); }
                    }
                }
            c.Recolor((x, y, z, col) => col == StonePale && (z == 9 || z == 16 || z == 2), Stone); // cornices
            // Inner seating tiers.
            for (int t = 0; t < 4; t++) c.Cylinder(Cx, Cy, 1, 2 + t * 3, 13f - t * 0.1f - 0f, t % 2 == 0 ? Stone : StoneLight, hollow: true, wall: 1.2f + t);
            c.Cylinder(Cx, Cy, 1, 12, 12.2f, 0, hollow: false);   // clear the arena
            c.Cylinder(Cx, Cy, 1, 1, 12f, Sand);
            for (int t = 0; t < 4; t++) c.Cylinder(Cx, Cy, 2 + t * 2, 2 + t * 2, 12.9f - t * 0.1f, t % 2 == 0 ? Stone : StoneLight, hollow: true, wall: 2.6f - t * 0.5f);
            // Banners on four masts.
            foreach (var (x, y) in new[] { (15, 0), (16, 31), (0, 16), (31, 15) })
            {
                c.Box(x, y, 18, x, y, 25, WoodDark);
                c.Box(x, y, 21, x, y, 24, Banner);
                c.Set(x, y, 25, BannerGold);
            }
            // Gate.
            c.Box(14, 0, 1, 17, 2, 7, 0);
            c.Box(14, 1, 1, 17, 1, 7, Iron);
            // Central sand pattern.
            c.Cylinder(Cx, Cy, 1, 1, 3f, SandDark, hollow: true, wall: 1f);
            return c.Grounded().ToModel("colosseum");
        }

        // ------------------------------------------------------------------ Crow's Nest

        /// <summary>
        /// Not grounded: designed in final coordinates. The building platform's surface must be at world height
        /// (7 + 1) / 3 = 2.667 = voxel 28.9 (castle at y -0.042, 0.09375 per voxel), i.e. the top of layer 28.
        /// </summary>
        public static VoxModel CrowsNest()
        {
            var c = new VoxCanvas(32, 32, 54);
            const int platformTop = 49;
            const int deckBottom = platformTop - 2;
            // Core column: almost as wide as the deck (1 voxel overhang per side), with stone texture and iron bands.
            c.Cylinder(Cx, Cy, 0, deckBottom - 1, 7.4f, StoneLight);
            c.Recolor((x, y, z, col) => col == StoneLight && Hash(x, y, z, 21) < 0.14f, Stone);
            foreach (int z in new[] { 6, 16, 26, 36 })
            {
                c.Recolor((x, y, zz, col) => zz == z && c.IsSideSurface(x, y, zz), Ore);
                c.Recolor((x, y, zz, col) => zz == z + 1 && c.IsSideSurface(x, y, zz), OreDark);
            }
            for (int z = 8; z < deckBottom - 2; z += 9)                           // arrow slits
                foreach (var (x, y) in new[] { (15, 8), (16, 23), (8, 16), (23, 15) }) c.Box(x, y, z, x, y, z + 2, StoneDark);
            c.Box(14, 8, 0, 17, 8, 4, WoodDark);                                    // door
            // Four leaning pillars on their own foundations at the very edge of the castle's footprint (the ore on the
            // neighbouring cells starts right behind it), meeting the deck's corners.
            foreach (var (bx, by) in new[] { (2f, 2f), (29f, 2f), (2f, 29f), (29f, 29f) })
            {
                int fx = (int)Math.Round(bx), fy = (int)Math.Round(by);
                c.Box(fx - 2, fy - 2, 0, fx + 2, fy + 2, 3, StoneDark);            // foundation
                c.Box(fx - 1, fy - 1, 4, fx + 1, fy + 1, 4, Stone);
                float tx = Cx + (bx - Cx) * 0.48f, ty = Cy + (by - Cy) * 0.48f;
                for (int z = 4; z <= deckBottom; z++)
                {
                    float t = (z - 4f) / (deckBottom - 4f);
                    float px = bx + (tx - bx) * t, py = by + (ty - by) * t;
                    bool ring = z % 9 == 0 || z == deckBottom - 1;
                    c.Cylinder(px, py, z, z, ring ? 2.2f : 1.7f, ring ? (z % 18 == 0 ? OreDark : Ore) : Stone);
                }
                // Iron strut bracing pillar and core.
                c.Line(bx + (tx - bx) * 0.35f, by + (ty - by) * 0.35f, 20, Cx + (bx - Cx) * 0.3f, Cy + (by - Cy) * 0.3f, 30, Ore, 0.9f);
            }
            // Deck: one build cell (the light 12x12 centre) on a 16x16 stone top with an iron rim.
            c.Box(8, 8, deckBottom, 23, 23, platformTop, Stone);
            c.Walls(8, 8, deckBottom, 23, 23, deckBottom, Ore);
            c.Box(10, 10, platformTop, 21, 21, platformTop, StonePale);
            // Iron fence around the whole top: posts and a top rail.
            for (int i = 8; i <= 23; i++)
                foreach (var (x, y) in new[] { (i, 8), (i, 23), (8, i), (23, i) })
                {
                    if (i % 3 == 2 || i == 23) c.Box(x, y, platformTop + 1, x, y, platformTop + 2, OreDark);
                    c.Set(x, y, platformTop + 3, Ore);
                }
            return c.ToModel("crows_nest");
        }

        // ------------------------------------------------------------------ Glass Fortress

        public static VoxModel GlassFortress()
        {
            var c = new VoxCanvas(32, 32, 46);
            c.Box(1, 1, 0, 30, 30, 1, GlassDark);
            // Faceted outer walls (diamond-shaped footprint corners clipped).
            c.Box(3, 3, 2, 28, 28, 12, Glass);
            c.ClearBox(5, 5, 4, 26, 26, 12);
            c.Recolor((x, y, z, col) => (x + y + z) % 7 == 0, GlassDeep);
            c.Recolor((x, y, z, col) => z == 12 || x == 3 && y == 3 || x == 28 && y == 28 || x == 3 && y == 28 || x == 28 && y == 3, GlassEdge);
            for (int i = 3; i <= 28; i += 3) { c.Set(i, 3, 13, GlassEdge); c.Set(i, 28, 13, GlassEdge); c.Set(3, i, 13, GlassEdge); c.Set(28, i, 13, GlassEdge); }
            // Central crystal keep: tall hexagonal prism tapering into a point.
            for (int z = 2; z <= 44; z++)
            {
                float r = z < 30 ? 6.5f - z * 0.04f : Math.Max(0.5f, 5.3f * (44 - z) / 14f);
                for (int x = 0; x < 32; x++)
                    for (int y = 0; y < 32; y++)
                    {
                        float dx = Math.Abs(x - Cx), dy = Math.Abs(y - Cy);
                        if (Math.Max(dx * 0.866f + dy * 0.5f, dy) > r) continue;
                        bool edge = Math.Max(dx * 0.866f + dy * 0.5f, dy) > r - 1f;
                        c.Set(x, y, z, edge ? (z % 6 == 0 ? GlassEdge : Glass) : GlassDeep);
                    }
            }
            // Corner crystal shards.
            foreach (var (x, y, h) in new[] { (4.5f, 4.5f, 22), (26.5f, 4.5f, 18), (4.5f, 26.5f, 19), (26.5f, 26.5f, 24) })
                for (int z = 2; z <= h; z++)
                    c.Cylinder(x, y, z, z, Math.Max(0.4f, 2.4f * (h - z) / (h - 2f) + 0.3f), z > h - 3 ? GlassEdge : Glass);
            // Door.
            c.Box(14, 3, 2, 17, 3, 8, GlassDark);
            return c.Grounded().ToModel("glass_fortress");
        }
    }
}

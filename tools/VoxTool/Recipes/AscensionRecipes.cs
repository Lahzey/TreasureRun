using System;
using System.Collections.Generic;
using System.IO;
using TreasureRun.Vox;
using static VoxTool.Recipes.VoxCanvas;

namespace VoxTool.Recipes
{
    /// <summary>
    /// Ascension models (docs/ascension-plan.md §1.4), phase 1. Tower models start from the extracted vanilla model
    /// (<c>RogueTower/Voxels/…</c>) and keep its x/y size; their sidecar copies the vanilla origin, so the runtime can
    /// swap the mesh of the vanilla renderer in place (same position, same turret rotation).
    /// </summary>
    public static partial class AscensionRecipes
    {
        static AscensionRecipes()
        {
            foreach (var kv in Phase2Sidecars) VanillaSidecars[kv.Key] = kv.Value;
            foreach (var kv in Phase3Sidecars) VanillaSidecars[kv.Key] = kv.Value;
        }

        /// <summary>Folder of the extracted vanilla models (set by <c>build</c>: models/../../Voxels).</summary>
        public static string VoxelsRoot = Path.Combine("..", "..", "..", "Voxels");

        private const string BallistaTurret = "CrossbowTower/CrossbowTower_Turret_TurretArt_Crossbow Top_default";
        private const string TeslaBase = "Tesla Coil/Tesla Coil_BaseArt_default";
        private const string LookoutBase = "Lookout/Lookout_BaseArt_Lookout_default";
        private const string MonumentBase = "Monument/Monument_BaseArt_SwordTower_default";
        private const string LairBase = "VampireLair/VampireLair_BaseArt_default";
        private const string RadarBase = "RadarTower/RadarTower_BaseArt_RadarBase_default";
        private const string RadarDish = "RadarTower/RadarTower_Turret_TurretArt_RadarDish_default";

        /// <summary>Recipe name → vanilla model whose sidecar (origin) it reuses.</summary>
        public static readonly Dictionary<string, string> VanillaSidecars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "ascensions/ballista/repeater_turret", BallistaTurret },
            { "ascensions/ballista/springald_turret", BallistaTurret },
            { "ascensions/tesla/chain_lightning", TeslaBase },
            { "ascensions/tesla/supercharge", TeslaBase },
            { "ascensions/lookout/lone_scout", LookoutBase },
            { "ascensions/monument/sealed_fate", MonumentBase },
            { "ascensions/vampire_lair/nest_of_bats", LairBase },
            { "ascensions/radar/air_base", RadarBase },
            { "ascensions/radar/air_base_dish", RadarDish },
            { "ascensions/radar/bomb_depot", RadarBase },
            { "ascensions/radar/bomb_depot_dish", RadarDish },
            { "ascensions/radar/airship_mast", RadarBase },
            { "ascensions/radar/airship_mast_dish", RadarDish },
        };

        public static IEnumerable<(string, Func<VoxModel>)> All()
        {
            yield return ("ascensions/ballista/repeater_turret", RepeaterTurret);
            yield return ("ascensions/ballista/springald_turret", SpringaldTurret);
            yield return ("ascensions/ballista/springald_bolt", SpringaldBolt);
            yield return ("ascensions/tesla/chain_lightning", ChainLightning);
            yield return ("ascensions/tesla/supercharge", Supercharge);
            yield return ("ascensions/lookout/lone_scout", LoneScout);
            yield return ("ascensions/lookout/scout_bolt", ScoutBolt);
            yield return ("ascensions/monument/sealed_fate", SealedFate);
            yield return ("ascensions/vampire_lair/nest_of_bats", NestOfBats);
            yield return ("ascensions/vampire_lair/bat_up", () => Bat(true));
            yield return ("ascensions/vampire_lair/bat_down", () => Bat(false));
            yield return ("ascensions/radar/air_base", AirBase);
            yield return ("ascensions/radar/air_base_dish", AirBaseDish);
            yield return ("ascensions/radar/bomb_depot", BombDepot);
            yield return ("ascensions/radar/bomb_depot_dish", BombDepotDish);
            yield return ("ascensions/radar/airship_mast", AirshipMast);
            yield return ("ascensions/radar/airship_mast_dish", AirshipMastDish);
            foreach (var r in Phase2All()) yield return r;
            foreach (var r in Phase3All()) yield return r;
        }

        private static readonly uint Wood = C(128, 92, 21), WoodDark = C(85, 57, 0), Cream = C(255, 227, 170), Steel = C(128, 124, 105);
        private static readonly uint Iron = C(60, 60, 66), IronLight = C(110, 110, 118), Rope = C(168, 136, 82), RopeDark = C(130, 100, 56);
        private static readonly uint Copper = C(184, 115, 51), CopperDark = C(140, 82, 34), Spark = C(120, 220, 255), SparkWhite = C(210, 248, 255);
        private static readonly uint Tunic = C(58, 92, 48), Skin = C(226, 180, 140), Hood = C(46, 70, 38);
        private static readonly uint Blood = C(200, 30, 40), BatBody = C(34, 28, 38), BatWing = C(58, 46, 62), BatEye = C(230, 40, 40);

        private static VoxModel Load(string rel) => VoxFile.Read(Path.Combine(VoxelsRoot, rel + ".vox"))[0];

        /// <summary>Canvas with the vanilla model pasted in, optionally taller (x/y stay the same).</summary>
        private static VoxCanvas FromVanilla(string rel, int extraZ = 0)
        {
            VoxModel m = Load(rel);
            var c = new VoxCanvas(m.SizeX, m.SizeY, m.SizeZ + extraZ);
            c.Paste(m, 0, 0, 0);
            return c;
        }

        /// <summary>Highest filled z of a column (−1 if empty).</summary>
        private static int TopZ(VoxCanvas c, int x, int y)
        {
            for (int z = c.SizeZ - 1; z >= 0; z--) if (c.Get(x, y, z) != 0) return z;
            return -1;
        }

        /// <summary>Line with alternating colours (chain links).</summary>
        private static void Chain(VoxCanvas c, float x0, float y0, float z0, float x1, float y1, float z1)
        {
            float len = (float)Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0) + (z1 - z0) * (z1 - z0));
            int steps = Math.Max(1, (int)Math.Ceiling(len));
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                c.Set((int)Math.Round(x0 + (x1 - x0) * t), (int)Math.Round(y0 + (y1 - y0) * t), (int)Math.Round(z0 + (z1 - z0) * t),
                      i % 2 == 0 ? Iron : IronLight);
            }
        }

        // ------------------------------------------------------------------ Ballista (turret faces +y)

        /// <summary>Vanilla ballista turret with a 3-bolt magazine over the rail.</summary>
        public static VoxModel RepeaterTurret()
        {
            var c = FromVanilla(BallistaTurret, 5);
            c.Box(5, 2, 4, 10, 8, 4, WoodDark);                       // magazine floor
            c.Box(5, 2, 5, 5, 8, 7, Wood); c.Box(10, 2, 5, 10, 8, 7, Wood);
            c.Box(5, 2, 5, 10, 2, 8, WoodDark);                       // back plate with the feed spring
            c.Set(7, 2, 9, Steel); c.Set(8, 2, 9, Steel);
            foreach (int z in new[] { 5, 6, 7 })                      // three stacked bolts with steel tips
            {
                c.Box(7, 3, z, 8, 8, z, Wood);
                c.Set(7, 9, z, Steel); c.Set(8, 9, z, Steel);
                c.Box(6, 3, z, 6, 3, z, Cream); c.Box(9, 3, z, 9, 3, z, Cream); // fletching
            }
            foreach (int y in new[] { 4, 7 }) c.Box(5, y, 8, 10, y, 8, Iron);  // iron straps over the top
            return c.ToModel("repeater_turret");
        }

        /// <summary>Heavier ballista: thick rail, torsion bundles at the arms, iron fittings.</summary>
        public static VoxModel SpringaldTurret()
        {
            var c = FromVanilla(BallistaTurret, 4);
            c.Box(6, 0, 1, 9, 14, 1, WoodDark);                       // thicker rail
            c.Box(7, 0, 2, 8, 14, 2, Wood);
            foreach (int x0 in new[] { 2, 12 })                       // twisted rope bundles (torsion springs)
            {
                for (int z = 0; z <= 6; z++)
                    for (int dx = 0; dx <= 1; dx++)
                        for (int dy = 0; dy <= 1; dy++)
                            c.Set(x0 + dx, 10 + dy, z, (z + dx + dy) % 2 == 0 ? Rope : RopeDark);
                c.Box(x0, 10, 0, x0 + 1, 11, 0, Iron);
                c.Box(x0, 10, 7, x0 + 1, 11, 7, Iron);
            }
            c.Box(4, 10, 6, 11, 10, 6, Iron);                         // iron cross beam joining the bundles
            c.Box(6, 14, 3, 9, 14, 3, Iron);                          // bolt guide at the front
            return c.ToModel("springald_turret");
        }

        /// <summary>Long heavy bolt along +y (rendered small at runtime).</summary>
        public static VoxModel SpringaldBolt()
        {
            var c = new VoxCanvas(3, 16, 3);
            c.Box(1, 1, 1, 1, 12, 1, Wood);
            foreach (var (x, z) in new[] { (0, 1), (2, 1), (1, 0), (1, 2) }) c.Box(x, 0, z, x, 2, z, Cream); // fletching
            c.Box(0, 13, 0, 2, 13, 2, Iron);                         // broad iron head
            c.Box(1, 13, 0, 1, 14, 2, Iron); c.Box(0, 14, 1, 2, 14, 1, Iron);
            c.Set(1, 15, 1, IronLight);
            return c.ToModel("springald_bolt");
        }

        // ------------------------------------------------------------------ Tesla Coil

        /// <summary>Tesla coil without the ring: the top ball becomes one tall focusing spike with a glowing tip.</summary>
        public static VoxModel ChainLightning()
        {
            var c = FromVanilla(TeslaBase, 4);
            uint core = c.Get(7, 7, 16);
            c.ClearBox(0, 0, 15, 15, 15, c.SizeZ - 1);               // ring and ball
            c.Box(7, 7, 15, 8, 8, 18, core);
            c.Box(6, 7, 17, 9, 8, 17, Copper); c.Box(7, 6, 17, 8, 9, 17, Copper);   // copper collar
            c.Box(7, 7, 19, 8, 8, 21, Copper);
            c.Box(7, 7, 22, 8, 8, 23, Spark);                        // focusing crystal
            c.Set(7, 7, 24, SparkWhite); c.Set(8, 8, 24, SparkWhite); c.Set(7, 8, 25, SparkWhite);
            c.Set(8, 7, 26, SparkWhite);
            return c.ToModel("chain_lightning");
        }

        /// <summary>Vanilla coil with four copper conductor rods around it.</summary>
        public static VoxModel Supercharge()
        {
            var c = FromVanilla(TeslaBase, 0);
            foreach (var (x, y) in new[] { (3, 3), (12, 3), (3, 12), (12, 12) })
            {
                int z0 = TopZ(c, x, y) + 1;
                int z1 = Math.Min(c.SizeZ - 3, z0 + 8);
                c.Box(x, y, z0, x, y, z1, Copper);
                for (int z = z0 + 1; z < z1; z += 3) c.Set(x, y, z, CopperDark);
                c.Set(x, y, z1 + 1, Spark); c.Set(x, y, z1 + 2, SparkWhite);
            }
            return c.ToModel("supercharge");
        }

        // ------------------------------------------------------------------ Lookout

        /// <summary>Lookout with a hooded crossbowman kneeling on the roof ridge, aiming over the −y side.</summary>
        public static VoxModel LoneScout()
        {
            var c = FromVanilla(LookoutBase, 4);
            c.Box(3, 3, 20, 4, 4, 21, Tunic);                        // body on the ridge (roof top z 19)
            c.Box(3, 5, 20, 4, 5, 20, Hood);                         // cloak behind
            c.Box(3, 3, 22, 4, 4, 22, Skin);                         // head
            c.Box(3, 4, 23, 4, 4, 23, Hood); c.Box(3, 3, 23, 4, 3, 23, Hood); c.Set(3, 5, 22, Hood); c.Set(4, 5, 22, Hood);
            c.Box(3, 0, 21, 4, 2, 21, Wood);                         // crossbow stock pointing forward
            c.Box(2, 0, 21, 5, 0, 21, Cream);                        // bow
            c.Set(3, 0, 20, Steel); c.Set(4, 0, 20, Steel);
            return c.ToModel("lone_scout");
        }

        /// <summary>Tiny crossbow bolt along +y.</summary>
        public static VoxModel ScoutBolt()
        {
            var c = new VoxCanvas(1, 6, 1);
            c.Box(0, 0, 0, 0, 0, 0, Cream);
            c.Box(0, 1, 0, 0, 4, 0, Wood);
            c.Set(0, 5, 0, Steel);
            return c.ToModel("scout_bolt");
        }

        // ------------------------------------------------------------------ Monument

        /// <summary>Monument whose sword is bound in chains anchored to the four corners; a blood-red pommel.</summary>
        public static VoxModel SealedFate()
        {
            var c = FromVanilla(MonumentBase, 0);
            // Spiral of links around the blade (blade at (7,8)/(8,7), z 6..13).
            for (int z = 6; z <= 13; z++)
            {
                double a = z * 1.3;
                int x = (int)Math.Round(7.5 + Math.Cos(a) * 1.6), y = (int)Math.Round(7.5 + Math.Sin(a) * 1.6);
                c.Set(x, y, z, z % 2 == 0 ? Iron : IronLight);
            }
            foreach (var (x, y) in new[] { (2, 2), (13, 2), (2, 13), (13, 13) })
            {
                int z = TopZ(c, x, y) + 1;
                Chain(c, x, y, z, 7.5f + Math.Sign(x - 7.5f) * 1.2f, 7.5f + Math.Sign(y - 7.5f) * 1.2f, 10f);
                c.Box(x, y, z, x, y, z, Iron);                       // anchor ring
            }
            c.Recolor((x, y, z, col) => z == 20, Blood);             // pommel
            c.Recolor((x, y, z, col) => z == 16, C(120, 20, 28));    // grip wrap
            return c.ToModel("sealed_fate");
        }


        // ------------------------------------------------------------------ Radar (one base + dish per plane ascension)
        // Vanilla base: plate z 4-5 over x/y 3..12, a pyramid of pins z 6-10 rising to a 2x2 column at x/y 7-8 (the dish
        // turret sits on top of it), corner pads x/y 0-2 / 13-15 up to z 2, teal stripes. Vanilla dish: a 14-voxel grid
        // frame at x 1-2 on a short arm (pivot near x 2, y 7).

        private static readonly uint Asphalt = C(58, 58, 62), Paint = C(235, 235, 225), RadarGlass = C(90, 170, 210), RadarSteel = C(150, 156, 165);
        private static readonly uint PanelBlue = C(60, 110, 170), Olive = C(96, 104, 60), OliveDark = C(72, 78, 44), Hazard = C(220, 180, 40);
        private static readonly uint MastRed = C(170, 40, 40), MastWhite = C(225, 222, 210), MooringRope = C(168, 136, 82);

        /// <summary>Clears the pin pyramid of the vanilla base, keeping the central column that carries the dish.</summary>
        private static void ClearPins(VoxCanvas c)
        {
            for (int x = 0; x < 16; x++)
                for (int y = 0; y < 16; y++)
                    if (!(x >= 7 && x <= 8 && y >= 7 && y <= 8)) c.ClearBox(x, y, 6, x, y, 10);
        }

        /// <summary>Strike Fighter: an airfield. Runway with centre dashes across the plate, control tower in a corner.</summary>
        public static VoxModel AirBase()
        {
            var c = FromVanilla(RadarBase, 2);
            ClearPins(c);
            c.Box(3, 3, 5, 12, 5, 5, Asphalt);                         // runway along x, in front of the column
            for (int x = 3; x <= 12; x += 3) c.Box(x, 4, 5, x + 1, 4, 5, Paint);
            c.Set(3, 3, 5, Paint); c.Set(3, 5, 5, Paint); c.Set(12, 3, 5, Paint); c.Set(12, 5, 5, Paint);
            c.Box(10, 10, 6, 12, 12, 8, StoneOf(c));                   // control tower
            c.Box(10, 10, 9, 12, 12, 9, RadarGlass);                   // glass cab
            c.Box(11, 11, 9, 11, 11, 9, StoneOf(c));
            c.Box(10, 10, 10, 12, 12, 10, RadarSteel);                      // roof
            c.Box(11, 11, 11, 11, 11, 12, RadarSteel); c.Set(11, 11, 12, MastRed); // antenna
            c.Box(4, 10, 6, 4, 10, 8, RadarSteel); c.Set(5, 10, 8, Hazard); c.Set(6, 10, 8, Hazard); // windsock
            c.Recolor((x, y, z, col) => col == C(34, 102, 102), PanelBlue);
            return c.ToModel("air_base");
        }

        /// <summary>Modern solid radar panel (same shape as the vanilla grid, filled, steel frame and blue face).</summary>
        public static VoxModel AirBaseDish()
        {
            var c = FromVanilla(RadarDish, 0);
            c.Box(1, 0, 2, 1, 13, 5, PanelBlue);                       // fill the grid
            c.Recolor((x, y, z, col) => col == PanelBlue && (z == 2 || z == 5 || y == 0 || y == 13), RadarSteel);
            c.Recolor((x, y, z, col) => col != PanelBlue, RadarSteel);
            c.Set(1, 7, 6, Hazard); c.Set(1, 6, 6, Hazard);             // top beacon
            return c.ToModel("air_base_dish");
        }

        /// <summary>Carpet Bomber: a bomb depot. Stacked bombs on the corner pads, hazard stripes instead of the teal ones.</summary>
        public static VoxModel BombDepot()
        {
            var c = FromVanilla(RadarBase, 2);
            c.Recolor((x, y, z, col) => col == C(34, 102, 102) && (x + y) % 2 == 0, Hazard);
            c.Recolor((x, y, z, col) => col == C(34, 102, 102), Asphalt);
            foreach (var (x0, y0) in new[] { (0, 0), (13, 0), (0, 13), (13, 13) })
            {
                // Three bombs: two side by side, one on top, lying along y with red tips.
                foreach (var (dx, z) in new[] { (0, 3), (2, 3), (1, 4) })
                {
                    c.Box(x0 + dx, y0, z, x0 + dx, y0 + 1, z, Olive);
                    c.Set(x0 + dx, y0 + 2, z, MastRed);
                }
            }
            c.Box(5, 11, 6, 7, 12, 7, OliveDark); c.Box(5, 11, 7, 7, 11, 7, Olive); // ammo crates by the column
            c.Box(9, 3, 6, 10, 4, 6, OliveDark);
            return c.ToModel("bomb_depot");
        }

        /// <summary>The vanilla dish in olive drab with a yellow-tipped frame.</summary>
        public static VoxModel BombDepotDish()
        {
            var c = FromVanilla(RadarDish, 0);
            c.Recolor((x, y, z, col) => true, Olive);
            c.Recolor((x, y, z, col) => y == 0 || y == 13, Hazard);
            c.Recolor((x, y, z, col) => x >= 3, OliveDark);
            return c.ToModel("bomb_depot_dish");
        }

        /// <summary>War Blimp: mooring field. Anchor posts with rope coils on the corner pads; the dish becomes the mast.</summary>
        public static VoxModel AirshipMast()
        {
            var c = FromVanilla(RadarBase, 2);
            ClearPins(c);
            c.Box(6, 6, 6, 9, 9, 6, RadarSteel);                             // mast foot plate around the column
            c.Box(7, 7, 6, 8, 8, 10, RadarSteel);
            c.Recolor((x, y, z, col) => col == C(34, 102, 102), MastRed);
            foreach (var (x, y) in new[] { (1, 1), (14, 1), (1, 14), (14, 14) })
            {
                c.Box(x, y, 3, x, y, 5, RadarSteel);                         // anchor bollard
                c.Set(x, y, 6, MastRed);
                c.Box(x - 1, y, 3, x + 1, y, 3, MooringRope); c.Box(x, y - 1, 3, x, y + 1, 3, MooringRope);
            }
            return c.ToModel("airship_mast");
        }

        /// <summary>A tall red-and-white lattice mooring mast on the turret (turns like the dish), nose cone and beacon on top.</summary>
        public static VoxModel AirshipMastDish()
        {
            var c = new VoxCanvas(7, 14, 22);                           // same x/y as the vanilla dish (sidecar origin)
            for (int z = 0; z <= 15; z++)
            {
                uint col = (z / 3) % 2 == 0 ? MastRed : MastWhite;
                foreach (var (x, y) in new[] { (1, 6), (2, 6), (1, 7), (2, 7) })
                    if ((x + y + z) % 2 == 0 || z % 3 == 0) c.Set(x, y, z, col);
            }
            c.Box(0, 5, 16, 3, 8, 16, RadarSteel);                           // mooring platform
            c.Box(1, 6, 17, 2, 7, 18, MastWhite);
            c.Box(1, 8, 17, 2, 11, 17, RadarSteel);                          // mooring arm pointing forward (+y)
            c.Box(1, 12, 16, 2, 12, 18, RadarSteel); c.Set(1, 13, 17, Hazard); c.Set(2, 13, 17, Hazard); // coupling cone
            c.Box(1, 6, 19, 2, 7, 19, MastRed);                         // beacon
            c.Set(1, 6, 20, Hazard);
            return c.ToModel("airship_mast_dish");
        }

        /// <summary>The stone colour of the vanilla radar base.</summary>
        private static uint StoneOf(VoxCanvas c) => c.Get(7, 7, 4) != 0 ? c.Get(7, 7, 4) : C(94, 93, 87);

        // ------------------------------------------------------------------ Vampire Lair

        /// <summary>Vampire Lair with bats perched on the four corner posts.</summary>
        public static VoxModel NestOfBats()
        {
            var c = FromVanilla(LairBase, 0);
            foreach (var (x, y, dx) in new[] { (0, 0, 1), (15, 0, -1), (0, 15, 1), (15, 15, -1) })
            {
                int z = TopZ(c, x, y) + 1;
                c.Box(x, y, z, x, y, z + 1, BatBody);
                c.Set(x, y, z + 2, BatBody);
                c.Set(x + dx, y, z + 1, BatWing);                    // folded wing
                c.Set(x + dx, y, z, BatWing);
                c.Set(x, y + (y == 0 ? 1 : -1), z + 2, BatEye);
            }
            return c.ToModel("nest_of_bats");
        }

        /// <summary>Flying bat along +y, two frames (wings up / down).</summary>
        public static VoxModel Bat(bool wingsUp)
        {
            var c = new VoxCanvas(9, 4, 5);
            c.Box(4, 0, 1, 4, 2, 2, BatBody);                        // body
            c.Box(4, 3, 2, 4, 3, 2, BatBody);                        // head
            c.Set(3, 3, 3, BatBody); c.Set(5, 3, 3, BatBody);        // ears
            c.Set(4, 3, 1, BatEye);
            for (int i = 1; i <= 4; i++)
            {
                int z = wingsUp ? 2 + (i + 1) / 2 : 2 - (i + 1) / 2;
                c.Box(4 - i, 1, Math.Max(0, z), 4 - i, 2, Math.Max(0, z), BatWing);
                c.Box(4 + i, 1, Math.Max(0, z), 4 + i, 2, Math.Max(0, z), BatWing);
            }
            return c.ToModel(wingsUp ? "bat_up" : "bat_down");
        }
    }
}

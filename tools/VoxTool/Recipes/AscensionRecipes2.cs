using System;
using System.Collections.Generic;
using TreasureRun.Vox;
using static VoxTool.Recipes.VoxCanvas;

namespace VoxTool.Recipes
{
    /// <summary>
    /// Ascension models, phase 2 (docs/ascension-plan.md §3). Tower parts start from the vanilla model like phase 1.
    /// Orientation: turrets of the Frost Keep, Flame Thrower and Cannon point to −x (their art is turned 90° in the
    /// prefab); the Particle Cannon top points to +y. Free models (projectiles) point along +y.
    /// </summary>
    public static partial class AscensionRecipes
    {
        private const string FrostBase = "FrostTower/FrostTower_BaseArt_FrostTower_default";
        private const string FrostTurret = "FrostTower/FrostTower_Turret_TurretArt_FlameThrowerTurret_default";
        private const string MortarBase = "Morter/Morter_BaseArt_default";
        private const string CannonTurret = "Cannon/Cannon_Turret_TurretArt_Cannon_default";
        private const string FlameBase = "FlameThrower/FlameThrower_BaseArt_FlameThrowerBase_default";
        private const string FlameTurret = "FlameThrower/FlameThrower_Turret_TurretArt_FlameThrowerTurret_default";
        private const string ShredderBase = "Shredder/Shredder_BaseArt_SawbladeTower_default";
        private const string ShredderTurret = "Shredder/Shredder_Turret_TurretArt_SawbladeTurret_default";
        private const string ParticleBase = "ParticleCannon/ParticleCannon_BaseArt_ParticleCannonBase_default";
        private const string ParticleTurret = "ParticleCannon/ParticleCannon_Turret_TurretArt_ParticleCannonTop_default";
        private const string EncampmentArt = "Encampment/Encampment_Encampment_default";
        private const string LandmineArt = "Landmine/Landmine_Landmine_default";

        private static readonly Dictionary<string, string> Phase2Sidecars = new Dictionary<string, string>
        {
            { "ascensions/frost/icicle_keep", FrostBase },
            { "ascensions/frost/icicle_turret", FrostTurret },
            { "ascensions/mortar/cluster_mortar", MortarBase },
            { "ascensions/cannon/line_cannon", CannonTurret },
            { "ascensions/cannon/hwacha_loaded", CannonTurret },
            { "ascensions/cannon/hwacha_empty", CannonTurret },
            { "ascensions/flame/magma_base", FlameBase },
            { "ascensions/flame/magma_turret", FlameTurret },
            { "ascensions/shredder/bladestorm_base", ShredderBase },
            { "ascensions/shredder/bladestorm_hub", ShredderTurret },
            { "ascensions/particle/beam_base", ParticleBase },
            { "ascensions/particle/beam_turret", ParticleTurret },
            { "ascensions/encampment/permacamp", EncampmentArt },
            { "ascensions/encampment/mine_2", LandmineArt },
            { "ascensions/encampment/mine_4", LandmineArt },
        };

        private static IEnumerable<(string, Func<VoxModel>)> Phase2All()
        {
            yield return ("ascensions/frost/icicle_keep", IcicleKeep);
            yield return ("ascensions/frost/icicle_turret", IcicleTurret);
            yield return ("ascensions/frost/icicle", Icicle);
            yield return ("ascensions/mortar/cluster_mortar", ClusterMortar);
            yield return ("ascensions/mortar/bomblet", Bomblet);
            yield return ("ascensions/cannon/line_cannon", LineCannon);
            yield return ("ascensions/cannon/hwacha_loaded", () => HwachaCart(true));
            yield return ("ascensions/cannon/hwacha_empty", () => HwachaCart(false));
            yield return ("ascensions/cannon/rocket", Rocket);
            yield return ("ascensions/cannon/target_marker", TargetMarker);
            yield return ("ascensions/cannon/aim_flag", AimFlag);
            yield return ("ascensions/flame/magma_base", MagmaBase);
            yield return ("ascensions/flame/magma_turret", MagmaTurret);
            yield return ("ascensions/flame/magma_ball", MagmaBall);
            yield return ("ascensions/flame/scorch_hot", () => Scorch(0));
            yield return ("ascensions/flame/scorch_warm", () => Scorch(1));
            yield return ("ascensions/flame/scorch", () => Scorch(2));
            yield return ("ascensions/shredder/bladestorm_base", BladestormBase);
            yield return ("ascensions/shredder/bladestorm_hub", BladestormHub);
            yield return ("ascensions/shredder/blade", Blade);
            yield return ("ascensions/particle/beam_base", BeamBase);
            yield return ("ascensions/particle/beam_turret", BeamTurret);
            yield return ("ascensions/encampment/permacamp", Permacamp);
            yield return ("ascensions/encampment/mine_2", () => MineStack(2));
            yield return ("ascensions/encampment/mine_4", () => MineStack(4));
        }

        private static readonly uint Ice = C(185, 225, 248), IceDeep = C(110, 170, 225), IceWhite = C(238, 250, 255);
        private static readonly uint ShellGrey = C(84, 86, 90), BandRed = C(180, 50, 40), Brass = C(196, 160, 70);
        private static readonly uint Plank = C(140, 100, 52), PlankDark = C(98, 68, 34), RocketRed = C(190, 45, 35), Hole = C(28, 24, 22);
        private static readonly uint Basalt = C(52, 46, 44), BasaltLight = C(78, 70, 66), Lava = C(240, 110, 20), LavaBright = C(255, 196, 60), LavaDark = C(170, 50, 15);
        private static readonly uint Blade1 = C(92, 97, 108), BladeEdge = C(168, 174, 186), Hub = C(70, 72, 78);
        private static readonly uint Violet = C(150, 80, 230), VioletBright = C(220, 170, 255);
        private static readonly uint FlagBlack = C(30, 28, 32), Bone = C(230, 225, 205), MineGreen = C(30, 80, 30), MineRed = C(200, 50, 50);

        /// <summary>Clears every voxel for which the predicate holds.</summary>
        private static void ClearWhere(VoxCanvas c, Func<int, int, int, uint, bool> where)
        {
            for (int x = 0; x < c.SizeX; x++)
                for (int y = 0; y < c.SizeY; y++)
                    for (int z = 0; z < c.SizeZ; z++)
                    {
                        uint col = c.Get(x, y, z);
                        if (col != 0 && where(x, y, z, col)) c.Clear(x, y, z);
                    }
        }

        // ------------------------------------------------------------------ Frost Keep

        /// <summary>Frost Keep under ice: frosted stones, ice spikes on the ring, icicles under the battlements.</summary>
        public static VoxModel IcicleKeep()
        {
            var c = FromVanilla(FrostBase, 0);
            c.Recolor((x, y, z, col) => col == C(79, 98, 142), IceDeep);
            c.Recolor((x, y, z, col) => z >= 12 && Hash(x, y, z, 3) < 0.35f, IceWhite);
            for (int i = 0; i < 8; i++)
            {
                double a = i * Math.PI / 4 + Math.PI / 8;
                int x = (int)Math.Round(7.5 + Math.Cos(a) * 6.5), y = (int)Math.Round(7.5 + Math.Sin(a) * 6.5);
                int top = TopZ(c, x, y);
                if (top < 0) continue;
                c.Box(x, y, top + 1, x, y, top + 3, Ice);
                c.Set(x, y, top + 4, IceWhite);
            }
            foreach (int x in new[] { 6, 8 }) { c.Set(x, 5, 11, Ice); c.Set(x, 10, 11, Ice); c.Set(x, 5, 10, IceWhite); c.Set(x, 10, 10, IceWhite); }
            foreach (int y in new[] { 7, 9 }) { c.Set(5, y, 11, Ice); c.Set(10, y, 11, Ice); c.Set(5, y, 10, IceWhite); c.Set(10, y, 10, IceWhite); }
            return c.ToModel("icicle_keep");
        }

        /// <summary>Ice ballista on the keep's turret (points to −x): frame, bow, a loaded icicle.</summary>
        public static VoxModel IcicleTurret()
        {
            var c = new VoxCanvas(15, 10, 6);
            c.Box(5, 3, 0, 13, 6, 1, IceDeep);                         // stock
            c.Box(11, 2, 0, 14, 7, 2, IceDeep);                        // rear block
            c.Box(12, 3, 3, 13, 6, 3, Ice);
            c.Box(5, 0, 2, 6, 9, 2, Ice);                              // bow arms
            c.Set(5, 0, 3, IceWhite); c.Set(5, 9, 3, IceWhite);
            c.Line(6, 0, 2, 11, 4, 2, IceWhite); c.Line(6, 9, 2, 11, 5, 2, IceWhite); // string
            c.Box(1, 4, 2, 12, 5, 2, Ice);                             // loaded icicle
            c.Box(0, 4, 2, 0, 5, 2, IceWhite);
            c.Box(2, 4, 3, 9, 5, 3, IceWhite);
            return c.ToModel("icicle_turret");
        }

        /// <summary>Icicle projectile along +y: thick at the back, pointed tip.</summary>
        public static VoxModel Icicle()
        {
            var c = new VoxCanvas(3, 12, 3);
            c.Box(0, 0, 0, 2, 4, 2, IceDeep);
            c.Box(1, 0, 1, 1, 9, 1, Ice);
            c.Box(0, 5, 1, 2, 6, 1, Ice); c.Box(1, 5, 0, 1, 7, 2, Ice);
            c.Set(1, 10, 1, IceWhite); c.Set(1, 11, 1, IceWhite);
            return c.ToModel("icicle");
        }

        // ------------------------------------------------------------------ Mortar

        /// <summary>Mortar with racks of bomblets on its rim.</summary>
        public static VoxModel ClusterMortar()
        {
            var c = FromVanilla(MortarBase, 3);
            foreach (var (x, y) in new[] { (5, 1), (10, 1), (5, 14), (10, 14), (1, 5), (1, 10), (14, 5), (14, 10) })
            {
                int top = TopZ(c, x, y);
                if (top < 0) continue;
                c.Set(x, y, top + 1, ShellGrey);
                c.Set(x, y, top + 2, BandRed);
                c.Set(x, y, top + 3, ShellGrey);
            }
            c.Recolor((x, y, z, col) => col == C(170, 57, 57), BandRed);
            return c.ToModel("cluster_mortar");
        }

        /// <summary>Small bomblet along +y: grey body, red band, fins at the back.</summary>
        public static VoxModel Bomblet()
        {
            var c = new VoxCanvas(3, 6, 3);
            c.Box(0, 1, 0, 2, 4, 2, ShellGrey);
            c.Box(0, 3, 0, 2, 3, 2, BandRed);
            c.Set(1, 5, 1, ShellGrey);
            c.Set(0, 0, 1, Steel); c.Set(2, 0, 1, Steel); c.Set(1, 0, 0, Steel); c.Set(1, 0, 2, Steel);
            return c.ToModel("bomblet");
        }

        // ------------------------------------------------------------------ Cannon

        /// <summary>Cannon with a steel gun shield in front of the axle, a sight and a red-banded barrel (barrel points to −x).</summary>
        public static VoxModel LineCannon()
        {
            var c = FromVanilla(CannonTurret, 0);
            for (int y = 2; y <= 9; y++)
                for (int z = 2; z <= 9; z++)
                {
                    bool barrel = y >= 4 && y <= 7 && z >= 4 && z <= 8;
                    if (!barrel) c.Set(5, y, z, (y + z) % 5 == 0 ? IronLight : Iron);
                }
            c.Box(5, 3, 10, 5, 8, 10, IronLight);
            c.Recolor((x, y, z, col) => col == C(170, 132, 57) && (x == 2 || x == 3), BandRed);
            c.Set(8, 5, 9, Brass); c.Set(8, 6, 9, Brass); c.Set(8, 5, 10, Brass);
            return c.ToModel("line_cannon");
        }

        /// <summary>Hwacha cart: the cannon's wheels with a stepped rocket rack (5 × 4 slots) facing −x.</summary>
        public static VoxModel HwachaCart(bool loaded)
        {
            var c = FromVanilla(CannonTurret, 2);
            uint wheel = C(66, 64, 58);
            ClearWhere(c, (x, y, z, col) => !(col == wheel && (y == 0 || y == 11)));
            c.Box(2, 1, 4, 13, 10, 4, PlankDark);                      // cart bed on the axle
            c.Box(12, 4, 5, 15, 7, 5, Plank); c.Box(14, 3, 3, 15, 8, 4, PlankDark); // handles
            for (int r = 0; r < 4; r++)
            {
                int front = 3 + 2 * r, z = 5 + r * 2;
                c.Box(front, 1, z, 11, 10, z + 1, (r & 1) == 0 ? Plank : PlankDark);
                for (int col = 0; col < 5; col++)
                {
                    int y = 1 + 2 * col;
                    if (loaded)
                    {
                        c.Set(front, y, z, Hole);
                        c.Set(front - 1, y, z, RocketRed);
                        c.Set(front - 2, y, z, Iron);
                    }
                    else c.Set(front, y, z, Hole);
                }
            }
            c.Box(11, 1, 5, 11, 10, 12, PlankDark);                    // back board
            return c.ToModel(loaded ? "hwacha_loaded" : "hwacha_empty");
        }

        /// <summary>Small rocket along +y: iron tip, red body, wooden tail with fins.</summary>
        public static VoxModel Rocket()
        {
            var c = new VoxCanvas(3, 10, 3);
            c.Box(1, 0, 1, 1, 4, 1, Plank);
            c.Box(0, 5, 0, 2, 7, 2, RocketRed);
            c.Box(1, 8, 1, 1, 9, 1, Iron);
            c.Set(0, 0, 1, Cream); c.Set(2, 0, 1, Cream); c.Set(1, 0, 0, Cream); c.Set(1, 0, 2, Cream);
            return c.ToModel("rocket");
        }

        /// <summary>Flat red target marker (ring, cross with a gap in the middle).</summary>
        public static VoxModel TargetMarker()
        {
            var c = new VoxCanvas(15, 15, 1);
            for (int x = 0; x < 15; x++)
                for (int y = 0; y < 15; y++)
                {
                    double d = Math.Sqrt((x - 7) * (x - 7) + (y - 7) * (y - 7));
                    if (d >= 6 && d < 7.3) c.Set(x, y, 0, RocketRed);
                    if (d >= 2.5 && d < 3.5) c.Set(x, y, 0, BladeEdge);
                }
            for (int i = 0; i < 15; i++)
                if (Math.Abs(i - 7) >= 2 && Math.Abs(i - 7) <= 5) { c.Set(i, 7, 0, RocketRed); c.Set(7, i, 0, RocketRed); }
            c.Set(7, 7, 0, RocketRed);
            return c.ToModel("target_marker");
        }

        /// <summary>Small aim flag: a pole with a red pennant.</summary>
        public static VoxModel AimFlag()
        {
            var c = new VoxCanvas(5, 3, 9);
            c.Box(0, 1, 0, 0, 1, 8, PlankDark);
            c.Box(1, 1, 5, 3, 1, 8, RocketRed);
            c.Box(4, 1, 6, 4, 1, 7, RocketRed);
            return c.ToModel("aim_flag");
        }

        // ------------------------------------------------------------------ Flame Thrower

        /// <summary>Flame thrower base turned to basalt with glowing lava veins.</summary>
        public static VoxModel MagmaBase()
        {
            var c = FromVanilla(FlameBase, 0);
            c.Recolor((x, y, z, col) => true, Basalt);
            c.Recolor((x, y, z, col) => Hash(x, y, z, 5) < 0.25f, BasaltLight);
            c.Recolor((x, y, z, col) => c.IsSideSurface(x, y, z) && Hash(x, y, z, 9) < 0.18f, Lava);
            c.Recolor((x, y, z, col) => z == TopZ(c, x, y) && Hash(x, y, z, 2) < 0.2f, LavaBright);
            return c.ToModel("magma_base");
        }

        /// <summary>Squat furnace catapult (points to −x): basalt cradle, glowing slits, arm and a bucket with a magma ball.</summary>
        public static VoxModel MagmaTurret()
        {
            var c = new VoxCanvas(15, 10, 8);
            c.Box(5, 2, 0, 14, 7, 2, Basalt);                           // furnace cradle
            for (int x = 6; x <= 13; x += 2) { c.Set(x, 2, 1, Lava); c.Set(x, 7, 1, Lava); }
            c.Box(12, 3, 3, 13, 6, 3, BasaltLight);                     // axle block
            c.Box(4, 4, 3, 12, 5, 3, Wood);                             // arm
            c.Box(1, 3, 3, 4, 6, 3, Iron);                              // bucket
            c.Walls(1, 3, 4, 4, 6, 4, Iron);
            c.Box(2, 4, 4, 3, 5, 5, Lava);                              // magma ball
            c.Set(2, 4, 5, LavaBright); c.Set(3, 5, 6, LavaDark);
            c.Box(14, 3, 3, 14, 6, 5, Basalt);                          // counterweight
            return c.ToModel("magma_turret");
        }

        /// <summary>Magma ball: dark crust with glowing cracks.</summary>
        public static VoxModel MagmaBall()
        {
            var c = new VoxCanvas(7, 7, 7);
            c.Ellipsoid(3, 3, 3, 3.4f, 3.4f, 3.4f, LavaDark);
            c.Recolor((x, y, z, col) => Hash(x, y, z, 1) < 0.45f, Lava);
            c.Recolor((x, y, z, col) => Hash(x, y, z, 4) < 0.15f, LavaBright);
            c.Recolor((x, y, z, col) => Hash(x, y, z, 7) < 0.2f, Basalt);
            return c.ToModel("magma_ball");
        }

        /// <summary>
        /// Magma pool (20 voxels across = 2 units at scale 1), ragged edge, in three cooling stages: 0 = glowing orange
        /// with a few dark spots, 1 = half crusted, 2 = black crust with a few embers.
        /// </summary>
        public static VoxModel Scorch(int stage)
        {
            var c = new VoxCanvas(20, 20, 1);
            float crust = stage == 0 ? 0.12f : stage == 1 ? 0.5f : 0.86f;
            for (int x = 0; x < 20; x++)
                for (int y = 0; y < 20; y++)
                {
                    double d = Math.Sqrt((x - 9.5) * (x - 9.5) + (y - 9.5) * (y - 9.5));
                    double edge = 8.5 + 1.6 * Math.Sin(Math.Atan2(y - 9.5, x - 9.5) * 5) * Hash(x, y, 0, 3);
                    if (d > edge) continue;
                    // Crust grows in from the cooler rim: the rim crusts first, the centre glows longest.
                    float rim = (float)(d / edge);
                    float h = Hash(x, y, 0, 11) * 0.7f + rim * 0.3f;
                    uint col;
                    if (h < 1f - crust) col = Hash(x, y, 0, 17) < 0.3f ? LavaBright : Hash(x, y, 0, 19) < 0.5f ? Lava : LavaDark;
                    else col = d < edge * 0.6 ? C(28, 24, 22) : C(48, 42, 38);
                    c.Set(x, y, 0, col);
                }
            string[] names = { "scorch_hot", "scorch_warm", "scorch" };
            return c.ToModel(names[stage]);
        }

        // ------------------------------------------------------------------ Shredder

        /// <summary>Shredder base with a steel ring of guide rails around it.</summary>
        public static VoxModel BladestormBase()
        {
            var c = FromVanilla(ShredderBase, 1);
            for (int x = 0; x < 14; x++)
                for (int y = 0; y < 14; y++)
                {
                    double d = Math.Sqrt((x - 6.5) * (x - 6.5) + (y - 6.5) * (y - 6.5));
                    if (d >= 5.6 && d < 6.6) c.Set(x, y, 0, (x + y) % 3 == 0 ? BladeEdge : Hub);
                }
            c.Recolor((x, y, z, col) => z == 6, Blade1);
            return c.ToModel("bladestorm_base");
        }

        /// <summary>Spindle hub with four spokes (spins in game).</summary>
        public static VoxModel BladestormHub()
        {
            var c = new VoxCanvas(6, 6, 8);
            c.Box(2, 2, 0, 3, 3, 7, Hub);
            c.Box(0, 2, 4, 5, 3, 4, Blade1); c.Box(2, 0, 4, 3, 5, 4, Blade1);
            c.Set(0, 2, 5, BladeEdge); c.Set(5, 3, 5, BladeEdge); c.Set(3, 0, 5, BladeEdge); c.Set(2, 5, 5, BladeEdge);
            c.Box(2, 2, 7, 3, 3, 7, BladeEdge);
            return c.ToModel("bladestorm_hub");
        }

        /// <summary>Whole circular saw blade lying flat (15 voxels = 1.5 units across): teeth, a slotted disc, a hub.</summary>
        public static VoxModel Blade()
        {
            var c = new VoxCanvas(15, 15, 2);
            const double cx = 7, cy = 7;
            for (int x = 0; x < 15; x++)
                for (int y = 0; y < 15; y++)
                {
                    double dx = x - cx, dy = y - cy, d = Math.Sqrt(dx * dx + dy * dy);
                    double a = Math.Atan2(dy, dx);
                    // 14 teeth: a sawtooth in angle, longer on the leading edge.
                    double tooth = ((a + Math.PI) / (2 * Math.PI) * 14) % 1.0;
                    double outer = 5.6 + 1.6 * tooth;
                    if (d > outer) continue;
                    bool slot = d > 2.6 && d < 4.4 && (((a + Math.PI) / (2 * Math.PI) * 5) % 1.0) < 0.12; // expansion slots
                    if (slot || d < 0.8) continue;
                    uint col = d > 5.4 ? BladeEdge : d < 1.9 ? Hub : Blade1;
                    c.Set(x, y, 0, col);
                    if (d < 1.9) c.Set(x, y, 1, Hub);
                }
            return c.ToModel("blade");
        }

        // ------------------------------------------------------------------ Particle Cannon

        public static VoxModel BeamBase()
        {
            var c = FromVanilla(ParticleBase, 0);
            c.Recolor((x, y, z, col) => col == C(46, 66, 114), Violet);
            return c.ToModel("beam_base");
        }

        /// <summary>Particle cannon top (points to +y) with violet coils and a glowing focusing lens at the front.</summary>
        public static VoxModel BeamTurret()
        {
            var c = FromVanilla(ParticleTurret, 1);
            c.Recolor((x, y, z, col) => col == C(46, 66, 114), Violet);
            c.Box(1, 13, 1, 4, 13, 4, VioletBright);
            c.Box(2, 13, 2, 3, 13, 3, BladeEdge);
            foreach (int y in new[] { 9, 11 }) { c.Set(1, y, 6, VioletBright); c.Set(4, y, 6, VioletBright); }
            return c.ToModel("beam_turret");
        }

        // ------------------------------------------------------------------ Encampment

        /// <summary>Encampment with a black skull banner and stacks of mines.</summary>
        public static VoxModel Permacamp()
        {
            var c = FromVanilla(EncampmentArt, 1);
            c.Recolor((x, y, z, col) => (col & 0xFF0000) >> 16 > (col & 0xFF) + 40, FlagBlack); // blue banner → black
            // A bone-white skull mark on the banner: the highest black voxels.
            for (int x = 0; x < c.SizeX; x++)
                for (int y = 0; y < c.SizeY; y++)
                {
                    int top = TopZ(c, x, y);
                    if (top >= 0 && c.Get(x, y, top) == FlagBlack && Hash(x, y, top, 2) < 0.5f) c.Set(x, y, top - 1, Bone);
                }
            foreach (var (x, y) in new[] { (2, 12), (12, 3) })
            {
                int top = Math.Max(TopZ(c, x, y), 0);
                for (int k = 0; k < 2; k++)
                {
                    c.Box(x - 1, y - 1, top + 1 + k, x + 1, y + 1, top + 1 + k, MineGreen);
                    c.Set(x, y, top + 1 + k, MineRed);
                }
            }
            return c.ToModel("permacamp");
        }

        /// <summary>Stacked mines on the vanilla 5×5 footprint: 2 = two discs, 4 = a spiked pile.</summary>
        public static VoxModel MineStack(int look)
        {
            var c = FromVanilla(LandmineArt, look == 2 ? 2 : 4);
            c.Box(1, 1, 1, 3, 3, 1, MineGreen); c.Set(2, 2, 2, MineRed);
            c.Box(1, 1, 2, 3, 3, 2, MineGreen); c.Set(2, 2, 3, MineRed);
            if (look >= 4)
            {
                c.Box(1, 1, 3, 3, 3, 3, MineGreen); c.Set(2, 2, 4, MineRed);
                c.Set(0, 2, 1, Iron); c.Set(4, 2, 1, Iron); c.Set(2, 0, 1, Iron); c.Set(2, 4, 1, Iron);
                c.Set(1, 1, 4, Iron); c.Set(3, 3, 4, Iron);
            }
            return c.ToModel(look == 2 ? "mine_2" : "mine_4");
        }
    }
}

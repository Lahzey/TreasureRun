using System;
using System.Collections.Generic;
using TreasureRun.Vox;
using static VoxTool.Recipes.VoxCanvas;

namespace VoxTool.Recipes
{
    /// <summary>Ascension models, phase 3 (docs/ascension-plan.md Â§3).</summary>
    public static partial class AscensionRecipes
    {
        private static readonly Dictionary<string, string> Phase3Sidecars = new Dictionary<string, string>
        {
            { "ascensions/shredder/saw_engine_base", ShredderBase },
            { "ascensions/shredder/saw_engine_turret", ShredderTurret },
        };

        private static IEnumerable<(string, Func<VoxModel>)> Phase3All()
        {
            yield return ("ascensions/shredder/floor_saw", FloorSaw);
            yield return ("ascensions/shredder/floor_saw_slot", FloorSawSlot);
            yield return ("ascensions/shredder/saw_engine_base", SawEngineBase);
            yield return ("ascensions/shredder/saw_engine_turret", SawEngineTurret);
        }

        private static readonly uint SawSteel = C(108, 112, 122), SawTooth = C(172, 177, 188), SawHub = C(64, 66, 72);
        private static readonly uint SlotDark = C(26, 24, 24), SlotRim = C(70, 72, 78), EngineRed = C(200, 50, 40), Coil = C(184, 115, 51);

        /// <summary>
        /// Vertical saw blade standing in the floor: a disc in the y/z plane (thin along x), 11 across with 12 teeth and a
        /// hub. In game it turns about its x axis with half of it above the path.
        /// </summary>
        public static VoxModel FloorSaw()
        {
            var c = new VoxCanvas(1, 11, 11);
            const double cy = 5, cz = 5;
            for (int y = 0; y < 11; y++)
                for (int z = 0; z < 11; z++)
                {
                    double dy = y - cy, dz = z - cz, d = Math.Sqrt(dy * dy + dz * dz);
                    double tooth = ((Math.Atan2(dz, dy) + Math.PI) / (2 * Math.PI) * 12) % 1.0;
                    if (d > 4.2 + 1.2 * tooth) continue;
                    c.Set(0, y, z, d > 4.1 ? SawTooth : d < 1.6 ? SawHub : SawSteel);
                }
            return c.ToModel("floor_saw");
        }

        /// <summary>Flat dark slit with a steel rim, where a saw comes out of the path (13 long, along +y).</summary>
        public static VoxModel FloorSawSlot()
        {
            var c = new VoxCanvas(3, 13, 1);
            c.Box(0, 0, 0, 2, 12, 0, SlotRim);
            c.Box(1, 1, 0, 1, 11, 0, SlotDark);
            return c.ToModel("floor_saw_slot");
        }

        /// <summary>Shredder base with power cables running out into the ground and a steel deck.</summary>
        public static VoxModel SawEngineBase()
        {
            var c = FromVanilla(ShredderBase, 0);
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                for (int i = 4; i <= 6; i++)
                {
                    int x = 6 + (dx > 0 ? 1 : 0) + dx * i, y = 6 + (dy > 0 ? 1 : 0) + dy * i;
                    c.Set(x, y, 0, i == 6 ? EngineRed : SlotDark);
                }
            c.Recolor((x, y, z, col) => z == TopZ(c, x, y) && (x + y) % 4 == 0, SawSteel);
            return c.ToModel("saw_engine_base");
        }

        /// <summary>The Saw Engine on top: a generator block with copper coils and a big upright saw (spins in game).</summary>
        public static VoxModel SawEngineTurret()
        {
            var c = new VoxCanvas(6, 6, 13);
            c.Box(0, 0, 0, 5, 5, 3, SawHub);
            c.Box(0, 0, 1, 5, 5, 1, Coil);
            c.Box(0, 0, 3, 5, 5, 3, Coil);
            c.Set(0, 2, 2, EngineRed); c.Set(5, 3, 2, EngineRed);
            const double cy = 2.5, cz = 8.5;
            for (int y = 0; y < 6; y++)
                for (int z = 4; z < 13; z++)
                {
                    double dy = y - cy, dz = z - cz, d = Math.Sqrt(dy * dy + dz * dz);
                    double tooth = ((Math.Atan2(dz, dy) + Math.PI) / (2 * Math.PI) * 10) % 1.0;
                    if (d > 3.3 + 1.1 * tooth) continue;
                    for (int x = 2; x <= 3; x++) c.Set(x, y, z, d > 3.2 ? SawTooth : d < 1.2 ? EngineRed : SawSteel);
                }
            c.Box(2, 2, 4, 3, 3, 5, SawHub);                            // axle post
            return c.ToModel("saw_engine_turret");
        }
    }
}

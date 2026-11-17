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
            { "ascensions/mortar/mortar_module", MortarBase },
            { "ascensions/particle/accelerator_base", ParticleBase },
            { "ascensions/particle/capacitor_base", ParticleBase },
        };

        private static IEnumerable<(string, Func<VoxModel>)> Phase3All()
        {
            yield return ("ascensions/shredder/floor_saw", FloorSaw);
            yield return ("ascensions/shredder/floor_saw_slot", FloorSawSlot);
            yield return ("ascensions/shredder/saw_engine_base", SawEngineBase);
            yield return ("ascensions/shredder/saw_engine_turret", SawEngineTurret);
            foreach (var r in LeyAll()) yield return r;
            foreach (var r in AcceleratorAll()) yield return r;
            yield return ("ascensions/mortar/mortar_module", MortarModule);
            yield return ("ascensions/mortar/mortar_barrel", () => MortarBody(1, 3, 11, true).ToModel("mortar_barrel"));
            yield return ("ascensions/mortar/mortar_shell", () => MortarShell(false));
            yield return ("ascensions/mortar/mortar_shell_light", () => MortarShell(true));
            for (int n = 2; n <= 8; n++)
            {
                int size = n;
                yield return ($"ascensions/mortar/mega_mortar_{size}", () => MegaMortar(size));
                yield return ($"ascensions/mortar/mega_barrel_{size}", () => MortarBody(size, 5, 15, true).ToModel($"mega_barrel_{size}"));
                yield return ($"ascensions/mortar/mega_plinth_{size}", () => MegaPlinth(size));
            }
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

        private static readonly uint MPanel = C(176, 182, 190), MSeam = C(128, 134, 142), MTrim = C(60, 64, 72), MTrimDark = C(36, 38, 44),
            MBore = C(16, 16, 20), MYellow = C(246, 202, 40), MLed = C(90, 230, 255), Concrete = C(150, 148, 142), ConcreteJoint = C(112, 110, 106);

        /// <summary>
        /// Single Mortar Module: a square steel block (1 tile) with a yellow connector in the middle of each side. The barrel
        /// is a separate model on the same canvas (<c>mortar_barrel</c>), so the game can let it recoil.
        /// </summary>
        public static VoxModel MortarModule() => MortarBody(1, 3, 11).ToModel("mortar_module");

        /// <summary>
        /// Mega Mortar of side <paramref name="n"/>: the module design n tiles wide and deep, but only a little taller
        /// (the same height for every size); the barrel (<c>mega_barrel_n</c>, separate for the recoil) widens with n.
        /// </summary>
        public static VoxModel MegaMortar(int n) => MortarBody(n, 5, 15).ToModel($"mega_mortar_{n}");

        /// <summary>
        /// The square mortar body: a light steel base with panel seams and a dark rim, a yellow connector socket in the
        /// middle of each side, a gunmetal deck with a hazard-striped edge and cyan corner lights, and a short wide barrel
        /// with a dark bore. Grilles fill the deck around a big barrel. <paramref name="slabTop"/> and
        /// <paramref name="barrelTop"/> are z levels. With <paramref name="barrelOnly"/> only the barrel is drawn, on the
        /// same canvas size, so both models line up when shown at the same spot; without it, the base gets a dark collar
        /// and bore where the barrel stands.
        /// </summary>
        private static VoxCanvas MortarBody(int n, int slabTop, int barrelTop, bool barrelOnly = false)
        {
            int w = 16 * n;
            var c = new VoxCanvas(w, w, barrelTop + 1);
            float mid = (w - 1) / 2f;
            int deck = slabTop + 1;
            float r = n == 1 ? 4.4f : 4.6f * n, bore = r * 0.58f;
            if (barrelOnly)
            {
                // Barrel: short and wide, gunmetal with a yellow ring, a light band and a dark bore.
                c.Cylinder(mid, mid, deck + 2, barrelTop, r, MTrim);
                c.Cylinder(mid, mid, deck + 3, deck + 3, r, MYellow);
                c.Cylinder(mid, mid, barrelTop - 2, barrelTop - 2, r, MPanel);
                c.Cylinder(mid, mid, barrelTop, barrelTop, r, MSeam);
                for (int x = 0; x < w; x++)
                    for (int y = 0; y < w; y++)
                    {
                        double d = Math.Sqrt((x - mid) * (x - mid) + (y - mid) * (y - mid));
                        if (d > bore) continue;
                        for (int z = deck + 2; z <= barrelTop; z++) c.Clear(x, y, z);
                    }
                return c;
            }

            // Base block.
            c.Box(0, 0, 0, w - 1, w - 1, slabTop, MPanel);
            for (int x = 0; x < w; x++)
                for (int y = 0; y < w; y++)
                    for (int z = 0; z <= slabTop; z++)
                    {
                        bool edgeX = x == 0 || x == w - 1, edgeY = y == 0 || y == w - 1;
                        if (!edgeX && !edgeY) continue;
                        int along = edgeX ? y : x;
                        uint col = MPanel;
                        if (z == 0) col = MTrimDark;
                        else if (z == slabTop || (edgeX && edgeY)) col = MTrim;
                        else if (along % 8 == 0 || along % 8 == 7) col = MSeam;
                        c.Set(x, y, z, col);
                    }

            // Connector sockets: a yellow plug in a dark frame, in the middle of each side.
            int cw = n == 1 ? 2 : 4, ch = n == 1 ? 2 : 3;
            int c0 = w / 2 - cw / 2;
            for (int i = -1; i <= cw; i++)
                for (int z = 0; z <= ch + 1; z++)
                {
                    bool plug = i >= 0 && i < cw && z >= 1 && z <= ch;
                    uint col = plug ? MYellow : MTrimDark;
                    c.Set(0, c0 + i, z, col); c.Set(w - 1, c0 + i, z, col);
                    c.Set(c0 + i, 0, z, col); c.Set(c0 + i, w - 1, z, col);
                }

            // Deck with a hazard-striped edge and cyan corner lights.
            c.Box(1, 1, deck, w - 2, w - 2, deck, MTrim);
            for (int x = 1; x < w - 1; x++)
                for (int y = 1; y < w - 1; y++)
                    if (x == 1 || y == 1 || x == w - 2 || y == w - 2)
                        c.Set(x, y, deck, ((x + y) / 2) % 2 == 0 ? MYellow : MTrimDark);
            foreach (var (x, y) in new[] { (2, 2), (w - 3, 2), (2, w - 3), (w - 3, w - 3) }) c.Set(x, y, deck + 1, MLed);

            // Collar the barrel sits in (and sinks into on recoil), with the dark bore floor.
            c.Cylinder(mid, mid, deck + 1, deck + 1, r + 0.6f, MTrimDark);
            for (int x = 0; x < w; x++)
                for (int y = 0; y < w; y++)
                {
                    double d = Math.Sqrt((x - mid) * (x - mid) + (y - mid) * (y - mid));
                    if (d <= bore) c.Set(x, y, deck + 1, MBore);
                }

            // Big decks: a vent grille on every tile the barrel leaves free.
            if (n > 1)
                for (int tx = 0; tx < n; tx++)
                    for (int ty = 0; ty < n; ty++)
                    {
                        float gx = tx * 16 + 7.5f, gy = ty * 16 + 7.5f;
                        if (Math.Sqrt((gx - mid) * (gx - mid) + (gy - mid) * (gy - mid)) < r + 4) continue;
                        for (int x = (int)gx - 3; x <= (int)gx + 4; x++)
                            for (int y = (int)gy - 3; y <= (int)gy + 4; y++)
                                c.Set(x, y, deck + 1, (x - (int)gx + 3) % 2 == 0 ? MTrimDark : MSeam);
                    }
            return c;
        }

        /// <summary>
        /// Modern mortar shell along +y (12 long): light steel body with a yellow band, gunmetal nose and cross fins, and
        /// a dark red cap at the back. <paramref name="lightOnly"/> gives just the bright red tail light behind the cap,
        /// on the same canvas (the game blinks it by switching its renderer on and off; no real light source).
        /// </summary>
        public static VoxModel MortarShell(bool lightOnly)
        {
            var c = new VoxCanvas(5, 12, 5);
            if (lightOnly)
            {
                c.Box(1, 0, 1, 3, 0, 3, C(255, 40, 30));
                c.Set(2, 0, 2, C(255, 150, 120));
                return c.ToModel("mortar_shell_light");
            }
            c.Box(1, 1, 1, 3, 1, 3, C(90, 18, 16));      // tail cap
            c.Box(2, 1, 0, 2, 3, 4, MTrim);                // fins (cross)
            c.Box(0, 1, 2, 4, 3, 2, MTrim);
            c.Box(1, 2, 1, 3, 8, 3, MPanel);               // body
            c.Box(1, 6, 1, 3, 6, 3, MYellow);              // band
            c.Box(1, 3, 1, 3, 3, 3, MSeam);
            c.Box(1, 9, 1, 3, 9, 3, MTrim);                // nose
            c.Box(2, 10, 1, 2, 10, 3, MTrim);
            c.Box(1, 10, 2, 3, 10, 2, MTrim);
            c.Set(2, 11, 2, MTrimDark);
            return c.ToModel("mortar_shell");
        }

        /// <summary>
        /// Concrete block under a Mega Mortar of side n whose tiles have different heights (n tiles wide at the art scale,
        /// 16 voxels tall; the game stretches it from the lowest to the highest tile).
        /// </summary>
        public static VoxModel MegaPlinth(int n)
        {
            int w = 16 * n;
            var c = new VoxCanvas(w, w, 16);
            c.Box(0, 0, 0, w - 1, w - 1, 15, Concrete);
            c.Recolor((x, y, z, col) => c.IsSideSurface(x, y, z) && (z % 8 == 0 || ((x == 0 || x == w - 1) ? y : x) % 16 == 0), ConcreteJoint);
            c.Recolor((x, y, z, col) => z == 15 && (x == 0 || y == 0 || x == w - 1 || y == w - 1), MTrim);
            return c.ToModel($"mega_plinth_{n}");
        }
    }
}

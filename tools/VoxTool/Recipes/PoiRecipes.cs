using TreasureRun.Vox;
using static VoxTool.Recipes.VoxCanvas;

namespace VoxTool.Recipes
{
    /// <summary>
    /// Point of Interest structures (docs/poi-plan.md). Voxel size 0.1 like buildings; the model's origin is the
    /// centre of its bottom (no sidecar), and the game places that bottom where the POI needs it.
    /// </summary>
    public static class PoiRecipes
    {
        private static readonly uint Stone = C(132, 128, 120), StoneDark = C(98, 95, 90), StoneLight = C(160, 156, 146);
        private static readonly uint Copper = C(184, 115, 51), CopperDark = C(140, 82, 34);
        private static readonly uint Spark = C(120, 220, 255), SparkWhite = C(210, 248, 255), Insulator = C(225, 222, 205);

        private static readonly uint Iron = C(58, 60, 68), IronLight = C(92, 96, 108);

        /// <summary>
        /// Lightning Trap pole (one of four on the cells diagonal to the centre): a stone footing, an iron mast with
        /// ceramic insulators where the arcs attach (0.45, 0.85 and 1.25 above the ground), a copper cap and a crystal.
        /// 6 x 6 x 18 voxels; the game stands its bottom on the grass.
        /// </summary>
        public static VoxModel LightningPole()
        {
            var c = new VoxCanvas(6, 6, 18);
            c.Box(0, 0, 0, 5, 5, 0, StoneDark);
            c.Box(1, 1, 1, 4, 4, 1, Stone);
            c.Box(2, 2, 2, 3, 3, 14, Iron);
            foreach (int z in new[] { 4, 8, 12 })
            {
                c.Box(1, 1, z, 4, 4, z, Insulator);
                c.Set(2, 2, z + 1, IronLight); c.Set(3, 3, z + 1, IronLight);
            }
            c.Box(1, 1, 15, 4, 4, 15, Copper);
            c.Box(2, 2, 16, 3, 3, 16, CopperDark);
            c.Box(2, 2, 17, 3, 3, 17, Spark);
            c.Set(2, 3, 17, SparkWhite);
            return c.ToModel("lightning_pole");
        }
            private static readonly uint Glaze = C(58, 74, 110), GlazeDark = C(38, 48, 74), GlazeLight = C(84, 106, 148);
        private static readonly uint Bronze = C(196, 156, 72), BronzeDark = C(140, 106, 44);
        private static readonly uint Soul = C(140, 236, 222), SoulWhite = C(222, 255, 250);

        /// <summary>
        /// Soul Urn (one cell): a funerary urn in a dark blue glaze with a bronze foot and lip, on a square stone plinth.
        /// A band of glowing runes runs round its belly, a bronze band round its shoulder; its open mouth holds a pool of soul light (the game adds the soul
        /// fire above it, growing as the urn fills). 10 x 10 x 13 voxels; the game stands its bottom on the grass.
        /// </summary>
        public static VoxModel SoulUrn()
        {
            var c = new VoxCanvas(10, 10, 13);
            const float cx = 4.5f, cy = 4.5f;
            c.Box(0, 0, 0, 9, 9, 0, StoneDark);
            c.Box(1, 1, 1, 8, 8, 1, Stone);
            foreach (var (x, y) in new[] { (1, 1), (8, 1), (1, 8), (8, 8) }) c.Set(x, y, 1, StoneLight);
            // The profile, layer by layer: a narrow foot, a round belly, a slim neck.
            float[] radius = { 0, 0, 1.8f, 2.5f, 3.3f, 3.9f, 4.05f, 3.9f, 3.4f, 2.7f, 2.0f, 1.6f };
            for (int z = 2; z < radius.Length; z++) c.Cylinder(cx, cy, z, z, radius[z], z == 2 ? BronzeDark : Glaze);
            // Mottled glaze, darker towards the foot.
            c.Recolor((x, y, z, col) => col == Glaze && Hash(x, y, z, 7) < 0.2f, GlazeLight);
            c.Recolor((x, y, z, col) => z == 3, GlazeDark);
            // A bronze band on the shoulder and runes round the belly.
            c.Recolor((x, y, z, col) => z == 8 && c.IsSideSurface(x, y, z), Bronze);
            c.Recolor((x, y, z, col) => z == 6 && c.IsSideSurface(x, y, z) && (x + y) % 2 == 0, Soul);
            // A flared bronze lip round the pool of soul light.
            c.Cylinder(cx, cy, 12, 12, 2.4f, Bronze, hollow: true, wall: 1.0f);
            c.Cylinder(cx, cy, 11, 11, 1.2f, Soul);
            c.Set(4, 4, 11, SoulWhite); c.Set(5, 5, 11, SoulWhite);
            return c.ToModel("soul_urn");
        }
    }
}

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
    }
}

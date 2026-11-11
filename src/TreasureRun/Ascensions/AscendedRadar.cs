namespace TreasureRun.Ascensions
{
    /// <summary>
    /// The Radar tower of a plane ascension: swaps the vanilla base and dish for the ascension's models
    /// (VoxTool AscensionRecipes: Air Base, Bomb Depot, Airship Mast). The planes themselves are handled by
    /// <see cref="PlaneAscensions"/>.
    /// </summary>
    internal sealed class AscendedRadar : AscendedTower
    {
        protected override void OnAscend()
        {
            string model = ModelFor(Def.Id);
            if (model == null) return;
            SwapVanillaMesh("BaseArt", model);
            SwapVanillaMesh("TurretArt", model + "_dish");
        }

        private static string ModelFor(string ascensionId)
        {
            switch (ascensionId)
            {
                case PlaneAscensions.StrikeFighterId: return "ascensions/radar/air_base";
                case PlaneAscensions.CarpetBomberId: return "ascensions/radar/bomb_depot";
                case PlaneAscensions.WarBlimpId: return "ascensions/radar/airship_mast";
                default: return null;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using TreasureRun.Vox;

namespace VoxTool.Recipes
{
    /// <summary>
    /// Ley Line Network models. The pipe parts are built in the game from <see cref="LeyGeometry"/> (src/Shared), one
    /// mesh per port combination, so only the palette model (whose material the game uses for all of them) and the card
    /// art are files.
    /// </summary>
    public static partial class AscensionRecipes
    {
        private static IEnumerable<(string, Func<VoxModel>)> LeyAll()
        {
            yield return ("ascensions/siphon/ley_palette", LeyGeometry.PaletteModel);
            yield return ("ascensions/siphon/ley_card", LeyGeometry.Card);
        }
    }
}

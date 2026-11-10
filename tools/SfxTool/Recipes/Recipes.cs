using System;
using System.Collections.Generic;

namespace SfxTool
{
    /// <summary>
    /// All sound recipes: name (path inside the sounds folder, without .wav) → generator. Variants of one sound are
    /// registered as name_1, name_2, ...; the game picks one at random when playing "name".
    /// </summary>
    public static partial class Recipes
    {
        public static readonly Dictionary<string, Func<Sound>> All = new Dictionary<string, Func<Sound>>(StringComparer.OrdinalIgnoreCase);

        static Recipes()
        {
            RegisterUi();
            RegisterAscensions();
        }

        private static void Add(string name, Func<Sound> recipe) => All[name] = recipe;

        private static void Variants(string name, int count, Func<int, Sound> recipe)
        {
            for (int i = 1; i <= count; i++)
            {
                int seed = i;
                All[$"{name}_{i}"] = () => recipe(seed);
            }
        }

        // ------------------------------------------------------------------ shared building blocks

        /// <summary>FM bell: bright inharmonic attack that settles into a pure tone.</summary>
        internal static Sound Bell(float freq, float seconds, float brightness = 2.5f, float decay = 5f) =>
            Sound.FM(seconds, _ => freq, _ => freq * 3.5f, t => brightness * (float)Math.Exp(-t * 6f), Env.Exp(decay, 0.003f));
    }
}

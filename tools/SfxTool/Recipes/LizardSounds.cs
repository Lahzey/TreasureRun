using System;

namespace SfxTool
{
    /// <summary>
    /// The Lizard Nest's sounds (docs/poi-plan.md §8): an egg cracking and the nest rustling as a lizard pushes its way
    /// out, a lizard bursting in a wet splat, and a hatchling squished. Built from the brittle and wet blocks in
    /// Foley.cs (<c>EggCrack</c>, <c>TwigRustle</c>, <c>Splat</c>).
    /// </summary>
    public static partial class Recipes
    {
        private static void RegisterLizardNest()
        {
            Variants("pois/lizard_nest/nest_rustle", 3, NestRustle);
            Variants("pois/lizard_nest/lizard_burst", 2, LizardBurst);
            Variants("pois/lizard_nest/hatchling_squish", 3, HatchlingSquish);
        }

        /// <summary>
        /// A lizard pushing its way out of the nest (1.1 s): an egg cracks at the start, and the twigs and dry grass
        /// rustle in one swelling gesture that rises with it and dies away. RMS -20 dBFS (the crack's peaks rounded off).
        /// </summary>
        private static Sound NestRustle(int seed)
        {
            const float length = 1.1f;
            var rng = new Random(900 + seed);
            float peak = 0.28f + 0.1f * (float)rng.NextDouble();
            Func<float, float> swell = Env.Points(0f, 0f, 0.06f, 0.55f, peak, 1f, 0.62f, 0.75f, 0.9f, 0.35f, length, 0f);
            return Sound.Silence(length)
                .Mix(TwigRustle(910 + seed, length, swell), 0f, 0.8f)
                .Mix(EggCrack(rng), 0.015f, 1.5f)
                .Reverb(room: 0.35f, damp: 0.5f, mix: 0.1f, tail: 0.3f).Trim(length).Fade(0.002f, 0.08f)
                .LevelRms(-20f);
        }

        /// <summary>A lizard bursting (0.9 s): a big wet splat. Loudest 50 ms about -9 dBFS, like an explosion.</summary>
        private static Sound LizardBurst(int seed)
        {
            var rng = new Random(930 + seed);
            Sound s = Splat(rng, 1f);
            return s.HighPass(30f).Reverb(room: 0.35f, damp: 0.5f, mix: 0.1f, tail: 0.3f).Trim(s.Duration).Fade(0.001f, 0.1f)
                .Normalize(-1f).Drive(1.8f).Normalize(-1f);
        }

        /// <summary>A hatchling squished (0.2 s): a tiny wet splat, the same parts shortened, higher and lighter.</summary>
        private static Sound HatchlingSquish(int seed)
        {
            var rng = new Random(960 + seed);
            Sound s = Splat(rng, 0.22f + 0.03f * seed);
            return s.Trim(0.25f).Fade(0.001f, 0.04f).Normalize(-1f);
        }
    }
}

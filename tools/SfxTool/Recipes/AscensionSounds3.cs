using System;

namespace SfxTool
{
    /// <summary>Ascension sounds, phase 3 (docs/ascension-plan.md §1.5).</summary>
    public static partial class Recipes
    {
        private static void RegisterPhase3()
        {
            Add("ascensions/saw_hum", SawHum);
        }

        /// <summary>
        /// The Saw Dimension: one global loop for all floor saws: a low grinding motor (saw 55 Hz through a low pass),
        /// a metallic whine (two close sines around 1.3 kHz beating slowly) and teeth chattering (band noise pulsing
        /// 30 times a second). 3 s, seamless; the game sets its volume from the number of Shredders.
        /// </summary>
        private static Sound SawHum()
        {
            const float length = 3f, overlap = 0.3f, total = length + overlap;
            Sound motor = Sound.Layer(Sound.Tone(Wave.Saw, total, 55f), Sound.Tone(Wave.Saw, total, 55.7f)).LowPass(500f).Gain(0.6f);
            Sound whine = Sound.Layer(Sound.Tone(Wave.Sine, total, 1300f), Sound.Tone(Wave.Sine, total, 1303f)).Gain(0.12f);
            Sound teeth = Sound.Noise(NoiseColor.White, total, 61).BandPass(2400f, 1.2f)
                .Map((x, t) => x * (0.35f + 0.65f * (float)Math.Pow(0.5 + 0.5 * Math.Sin(2 * Math.PI * 30f * t), 3))).Gain(0.45f);
            Sound grind = Sound.Noise(NoiseColor.Brown, total, 62).LowPass(900f).Gain(0.35f);
            return Seamless(Sound.Layer(motor, whine, teeth, grind), length, overlap).Normalize(-8f);
        }
    }
}

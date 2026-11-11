using System;

namespace SfxTool
{
    /// <summary>Ascension sounds, phase 3 (docs/ascension-plan.md §1.5).</summary>
    public static partial class Recipes
    {
        private static void RegisterPhase3()
        {
            Add("ascensions/saw_hum", SawHum);
            Add("ascensions/mega_launch", MegaLaunch);
            Add("ascensions/mega_blast", MegaBlast);
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

        /// <summary>
        /// Mega Mortar launch: a deep cannon "whump" (sine sweeping 70 → 28 Hz), a blast of low noise and a short metallic
        /// ring of the huge barrel, 1.2 s. Lower and longer than the vanilla Mortar (85 Hz zero-crossing, −9.6 dB).
        /// </summary>
        private static Sound MegaLaunch()
        {
            Sound whump = Sound.Tone(Wave.Sine, 1.0f, Env.Sweep(70f, 28f, 0.8f), Env.Exp(4.5f, 0.003f));
            Sound blast = Sound.Noise(NoiseColor.Brown, 0.7f, 71).LowPass(Env.Sweep(1800f, 300f, 0.6f)).Envelope(Env.Exp(7f, 0.002f)).Gain(0.9f);
            Sound crack = Sound.Noise(NoiseColor.White, 0.08f, 72).BandPass(2200f, 1f).Envelope(Env.Exp(60f, 0.0005f)).Gain(0.5f);
            Sound ring = Sound.Layer(Sound.Tone(Wave.Sine, 1.2f, 182f, Env.Exp(3.5f, 0.01f)), Sound.Tone(Wave.Sine, 1.2f, 297f, Env.Exp(5f, 0.01f))).Gain(0.12f);
            return Sound.Layer(whump, blast, crack, ring).Drive(1.4f).Reverb(room: 0.6f, mix: 0.18f, tail: 0.4f).Normalize(-1f);
        }

        /// <summary>
        /// Mega Mortar impact: a huge explosion: a sub boom (sine 60 → 22 Hz), a heavy noise body, a crackling debris
        /// tail and a long rumble, 2.6 s (Explosion1 level and up, darker and longer).
        /// </summary>
        private static Sound MegaBlast()
        {
            Sound boom = Sound.Tone(Wave.Sine, 1.4f, Env.Sweep(60f, 22f, 1.2f), Env.Exp(3f, 0.002f));
            Sound body = Sound.Noise(NoiseColor.Brown, 1.2f, 81).LowPass(Env.Sweep(2500f, 400f, 1.0f)).Envelope(Env.Exp(4f, 0.002f));
            Sound crack = Sound.Noise(NoiseColor.White, 0.15f, 82).BandPass(1800f, 0.8f).Envelope(Env.Exp(30f, 0.0005f)).Gain(0.6f);
            Sound debris = Sound.Noise(NoiseColor.White, 2.0f, 83).BandPass(3500f, 0.9f)
                .Map((x, t) => x * (((int)(t * 60f) * 7919 % 5) == 0 ? 1f : 0.08f))
                .Envelope(Env.Points(0f, 0f, 0.15f, 0.7f, 1.0f, 0.3f, 2.0f, 0f)).Gain(0.25f);
            Sound rumble = Sound.Noise(NoiseColor.Brown, 2.6f, 84).LowPass(250f).Envelope(Env.Points(0f, 0f, 0.1f, 1f, 1.2f, 0.5f, 2.6f, 0f)).Gain(0.7f);
            return Sound.Layer(boom, body, crack, debris, rumble).Drive(1.5f).Reverb(room: 0.8f, mix: 0.2f, tail: 0.6f).Normalize(-0.5f);
        }
    }
}

using System;

namespace SfxTool
{
    /// <summary>Point of Interest sounds (docs/poi-plan.md). Loudness is matched to the vanilla clips noted per recipe.</summary>
    public static partial class Recipes
    {
        private static void RegisterPois()
        {
            Variants("pois/lightning_trap/trap_strike", 3, TrapStrike);
            Add("pois/lightning_trap/trap_on", TrapOn);
            Add("pois/lightning_trap/trap_off", TrapOff);
        }

        /// <summary>
        /// Lightning Trap strike: a sharp crack with a short low thump from the tunnel, 0.3 s. It plays up to four times
        /// a second, so it is quieter than Chain Lightning's crackle (loudest 50 ms ≈ −15 dB; TeslaZap −9.6 dB, centroid 7.5 kHz).
        /// </summary>
        private static Sound TrapStrike(int seed)
        {
            var rng = new Random(seed * 17 + 3);
            const float length = 0.3f;
            Sound crack = Sound.Noise(NoiseColor.White, 0.06f, rng.Next()).HighPass(1100f).Envelope(Env.Exp(70f, 0.0004f));
            Sound sizzle = Sound.Silence(length);
            for (float t = 0.01f; t < 0.2f; t += 0.006f + (float)rng.NextDouble() * 0.012f)
            {
                Sound pop = Sound.Noise(NoiseColor.White, 0.015f, rng.Next()).HighPass(1600f).Envelope(Env.Exp(220f, 0.0003f));
                sizzle = sizzle.Mix(pop, t, 0.25f + (float)rng.NextDouble() * 0.35f);
            }
            Sound thump = Sound.Tone(Wave.Sine, 0.22f, Env.Sweep(110f, 48f, 0.2f), Env.Exp(18f, 0.002f)).Gain(0.7f);
            return Sound.Layer(crack, sizzle, thump)
                .Reverb(room: 0.35f, mix: 0.18f, tail: 0.12f)
                .Fade(0f, 0.04f)
                .Normalize(-1f).Gain(0.75f);
        }

        /// <summary>
        /// Lightning Trap switched on: a lever clunk, an electric hum rising (saw 70 → 260 Hz with tremolo) and a crack
        /// at the top, 0.8 s. Like a button click plus a short charge-up; loudest ≈ −6 dB.
        /// </summary>
        private static Sound TrapOn()
        {
            const float length = 0.8f;
            Sound clunk = Sound.Tone(Wave.Sine, 0.12f, Env.Sweep(180f, 90f, 0.1f), Env.Exp(40f, 0.001f))
                .Mix(Sound.Noise(NoiseColor.Pink, 0.05f, 11).BandPass(900f, 1.5f).Envelope(Env.Exp(90f, 0.001f)), 0f, 0.6f);
            Sound hum = Sound.Tone(Wave.Saw, length, Env.Sweep(70f, 260f, 0.6f), Env.Points(0f, 0f, 0.1f, 0.6f, 0.6f, 1f, 0.8f, 0f))
                .LowPass(Env.Sweep(600f, 3000f, 0.6f), 1.1f);
            hum = hum.Map((x, t) => x * (0.7f + 0.3f * (float)Math.Sin(2 * Math.PI * 30f * t)));
            Sound crack = TrapStrike(5).Gain(1f);
            return Sound.Layer(clunk, hum.Gain(0.55f))
                .Mix(crack, 0.55f, 1f)
                .Normalize(-6f);
        }

        /// <summary>Lightning Trap switched off: the clunk and the hum falling away (260 → 50 Hz), 0.55 s, quieter.</summary>
        private static Sound TrapOff()
        {
            const float length = 0.55f;
            Sound clunk = Sound.Tone(Wave.Sine, 0.12f, Env.Sweep(160f, 80f, 0.1f), Env.Exp(40f, 0.001f))
                .Mix(Sound.Noise(NoiseColor.Pink, 0.05f, 13).BandPass(800f, 1.5f).Envelope(Env.Exp(90f, 0.001f)), 0f, 0.6f);
            Sound hum = Sound.Tone(Wave.Saw, length, Env.Sweep(260f, 50f, length), Env.Points(0f, 1f, 0.45f, 0.4f, 0.55f, 0f))
                .LowPass(Env.Sweep(2500f, 400f, length), 1.1f);
            return Sound.Layer(clunk, hum.Gain(0.5f)).Fade(0f, 0.08f).Normalize(-9f);
        }
    }
}

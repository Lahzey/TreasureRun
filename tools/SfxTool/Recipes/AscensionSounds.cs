using System;

namespace SfxTool
{
    /// <summary>Ascension sounds, phase 1 (docs/ascension-plan.md §1.5). Loudness is matched to the vanilla clips noted per recipe.</summary>
    public static partial class Recipes
    {
        private static void RegisterAscensions()
        {
            Variants("ascensions/scout_tick", 3, ScoutTick);
            Variants("ascensions/chain_crackle", 3, ChainCrackle);
            Add("ascensions/supercharge", Supercharge);
            Variants("ascensions/bat_bite", 3, BatBite);
            Add("ascensions/springald_launch", SpringaldLaunch);
            RegisterPhase2();
        }

        /// <summary>
        /// Lone Scout: a soft crossbow "tick" (string snap + tiny wooden click). It plays 3+ times a second, so it is
        /// short (0.12 s) and quieter than BulletHit (loudest 50 ms ≈ −14 dB there; −20 dB here).
        /// </summary>
        private static Sound ScoutTick(int seed)
        {
            var rng = new Random(seed);
            float f = 1300f + (float)rng.NextDouble() * 250f;
            Sound snap = Sound.Tone(Wave.Triangle, 0.1f, Env.Sweep(f, f * 0.7f, 0.08f), Env.Exp(45f, 0.001f));
            Sound click = Sound.Noise(NoiseColor.White, 0.03f, seed).BandPass(3200f, 2f).Envelope(Env.Exp(120f, 0.0005f)).Gain(0.7f);
            return Sound.Layer(snap, click).Fade(0f, 0.02f).Normalize(-6f).Gain(0.7f);
        }

        /// <summary>
        /// Chain Lightning: an electric arc that jumps: a dense train of sharp crackles over a 100 Hz buzz, 0.5 s.
        /// Close to TeslaZap (bright, ≈ 7 kHz centroid, loudest −9.6 dB).
        /// </summary>
        private static Sound ChainCrackle(int seed)
        {
            var rng = new Random(seed * 31 + 5);
            const float length = 0.5f;
            Sound crackles = Sound.Silence(length);
            for (float t = 0f; t < length - 0.03f; t += 0.005f + (float)rng.NextDouble() * 0.015f)
            {
                Sound pop = Sound.Noise(NoiseColor.White, 0.02f, rng.Next()).HighPass(1400f).Envelope(Env.Exp(180f, 0.0003f));
                crackles = crackles.Mix(pop, t, 0.4f + (float)rng.NextDouble() * 0.6f);
            }
            Sound buzz = Sound.Tone(Wave.Saw, length, 100f, Env.Points(0f, 0f, 0.02f, 1f, 0.35f, 0.6f, 0.5f, 0f))
                .RingMod(_ => 330f).LowPass(2500f).Gain(0.6f);
            return Sound.Layer(crackles, buzz)
                .Envelope(Env.Points(0f, 1f, 0.3f, 0.8f, 0.5f, 0f))
                .Reverb(room: 0.4f, mix: 0.12f, tail: 0.2f)
                .Normalize(-1f);
        }

        /// <summary>
        /// Supercharge: towers being charged: a short rising electric hum (saw 160 → 520 Hz, tremolo), 0.45 s, quiet
        /// (it plays on every coil attack; loudest ≈ −16 dB).
        /// </summary>
        private static Sound Supercharge()
        {
            const float length = 0.45f;
            Sound hum = Sound.Tone(Wave.Saw, length, Env.Sweep(160f, 520f, length), Env.Points(0f, 0f, 0.05f, 1f, 0.35f, 0.9f, 0.45f, 0f))
                .LowPass(Env.Sweep(900f, 3500f, length), 1.2f);
            Sound tremolo = hum.Map((x, t) => x * (0.75f + 0.25f * (float)Math.Sin(2 * Math.PI * 28f * t)));
            Sound fizz = Sound.Noise(NoiseColor.White, length, 3).BandPass(Env.Sweep(3000f, 7000f, length), 4f)
                .Envelope(Env.Points(0f, 0f, 0.3f, 1f, 0.45f, 0f)).Gain(0.25f);
            return Sound.Layer(tremolo, fizz).Normalize(-6f).Gain(0.9f);
        }

        /// <summary>Nest of Bats: a tiny squeak with a soft bite thud, 0.15 s, quiet (ten bats bite every second).</summary>
        private static Sound BatBite(int seed)
        {
            var rng = new Random(seed * 7 + 1);
            float f = 3200f + (float)rng.NextDouble() * 900f;
            Sound squeak = Sound.Tone(Wave.Sine, 0.07f, Env.Sweep(f, f * 0.65f, 0.07f), Env.Points(0f, 0f, 0.01f, 1f, 0.07f, 0f));
            Sound thud = Sound.Tone(Wave.Sine, 0.1f, Env.Sweep(260f, 120f, 0.1f), Env.Exp(35f, 0.001f)).Gain(0.8f);
            Sound tear = Sound.Noise(NoiseColor.Pink, 0.06f, seed).BandPass(1800f, 1.5f).Envelope(Env.Exp(60f, 0.001f)).Gain(0.5f);
            return Sound.Layer(squeak.Gain(0.5f), thud.Mix(tear, 0.04f, 1f)).Normalize(-6f).Gain(0.75f);
        }

        /// <summary>
        /// Springald: heavy torsion launch: a deep wooden thump, a rope creak and a whoosh, 0.6 s. Heavier and darker
        /// than the vanilla Ballista (≈ 3.4 kHz centroid, loudest −7.6 dB).
        /// </summary>
        private static Sound SpringaldLaunch()
        {
            Sound thump = Sound.Tone(Wave.Sine, 0.35f, Env.Sweep(140f, 55f, 0.25f), Env.Exp(12f, 0.002f));
            Sound knock = Sound.Noise(NoiseColor.Brown, 0.12f, 4).LowPass(900f).Envelope(Env.Exp(30f, 0.001f)).Gain(0.8f);
            Sound twang = Sound.Tone(Wave.Triangle, 0.3f, Env.Sweep(220f, 180f, 0.3f), Env.Exp(10f, 0.002f)).Drive(2f).Gain(0.35f);
            Sound whoosh = Sound.Noise(NoiseColor.Pink, 0.5f, 9).BandPass(Env.Sweep(2500f, 700f, 0.5f), 1.2f)
                .Envelope(Env.Points(0f, 0f, 0.06f, 1f, 0.5f, 0f)).Gain(0.5f);
            return Sound.Layer(thump, knock, twang, whoosh)
                .Reverb(room: 0.5f, mix: 0.15f, tail: 0.3f)
                .Normalize(-1f);
        }
    }
}

using System;

namespace SfxTool
{
    /// <summary>Ascension sounds, phase 2 (docs/ascension-plan.md §1.5). Loudness is matched to the vanilla clips noted per recipe.</summary>
    public static partial class Recipes
    {
        private static void RegisterPhase2()
        {
            Add("ascensions/icicle_launch", IcicleLaunch);
            Variants("ascensions/icicle_shatter", 3, IcicleShatter);
            Add("ascensions/cluster_pop", ClusterPop);
            Variants("ascensions/rocket_whoosh", 4, RocketWhoosh);
            Variants("ascensions/rocket_pop", 3, RocketPop);
            Add("ascensions/magma_lob", MagmaLob);
            Add("ascensions/magma_impact", MagmaImpact);
            Add("ascensions/blade_whirr", BladeWhirr);
            Add("ascensions/beam_hum", BeamHum);
        }

        /// <summary>
        /// Makes a buffer loop seamlessly: renders <paramref name="length"/> + <paramref name="overlap"/> seconds and
        /// crossfades the extra tail into the start.
        /// </summary>
        private static Sound Seamless(Sound s, float length, float overlap)
        {
            int n = Sound.Samples(length), x = Sound.Samples(overlap);
            var outS = new float[n];
            for (int i = 0; i < n; i++)
            {
                float v = i < s.Length ? s.S[i] : 0f;
                if (i < x && n + i < s.Length)
                {
                    float k = i / (float)x;
                    v = v * k + s.S[n + i] * (1f - k);
                }
                outS[i] = v;
            }
            return new Sound(outS);
        }

        /// <summary>Icicle Cannon launch: a crisp crack plus a glassy chime, 0.4 s (Ballista-like level, brighter).</summary>
        private static Sound IcicleLaunch()
        {
            Sound crack = Sound.Noise(NoiseColor.White, 0.12f, 4).HighPass(2500f).Envelope(Env.Exp(40f, 0.001f));
            Sound twang = Sound.Tone(Wave.Triangle, 0.3f, Env.Sweep(900f, 600f, 0.3f), Env.Exp(14f, 0.002f)).Gain(0.6f);
            Sound chime = Bell(2400f, 0.4f, 3f, 9f).Gain(0.35f).Mix(Bell(3600f, 0.35f, 2f, 11f).Gain(0.2f), 0.01f);
            return Sound.Layer(crack.Gain(0.5f), twang, chime).Reverb(room: 0.4f, mix: 0.12f, tail: 0.15f).Normalize(-1f);
        }

        /// <summary>Icicle hits a monster: glass-like shatter (bright tinkles), 0.35 s, quiet (FrostHit is −16 dB loudest).</summary>
        private static Sound IcicleShatter(int seed)
        {
            var rng = new Random(seed * 13 + 2);
            Sound s = Sound.Noise(NoiseColor.White, 0.08f, seed).HighPass(3000f).Envelope(Env.Exp(50f, 0.001f));
            for (int i = 0; i < 7; i++)
            {
                float f = 3000f + (float)rng.NextDouble() * 4000f;
                s = s.Mix(Bell(f, 0.2f, 1.5f, 18f).Gain(0.25f), (float)rng.NextDouble() * 0.12f);
            }
            return s.Normalize(-1f).Gain(0.9f);
        }

        /// <summary>Cluster shell splits: a hollow "pop" with a short metallic rattle, 0.35 s.</summary>
        private static Sound ClusterPop()
        {
            Sound pop = Sound.Tone(Wave.Sine, 0.2f, Env.Sweep(320f, 90f, 0.2f), Env.Exp(18f, 0.001f));
            Sound burst = Sound.Noise(NoiseColor.Pink, 0.15f, 7).LowPass(2500f).Envelope(Env.Exp(30f, 0.001f)).Gain(0.7f);
            Sound rattle = Sound.Silence(0.35f);
            var rng = new Random(9);
            for (float t = 0.03f; t < 0.25f; t += 0.03f + (float)rng.NextDouble() * 0.02f)
                rattle = rattle.Mix(Sound.Noise(NoiseColor.White, 0.015f, rng.Next()).BandPass(4500f, 3f).Envelope(Env.Exp(200f, 0.0005f)), t, 0.5f);
            return Sound.Layer(pop, burst, rattle).Normalize(-1f).Gain(0.8f);
        }

        /// <summary>Hwacha rocket launch: a fizzing whoosh (band noise sweeping up then fading), 0.5 s, varied per rocket.</summary>
        private static Sound RocketWhoosh(int seed)
        {
            var rng = new Random(seed * 17 + 3);
            float f0 = 900f + (float)rng.NextDouble() * 400f;
            Sound hiss = Sound.Noise(NoiseColor.White, 0.5f, seed)
                .BandPass(Env.Sweep(f0, f0 * 2.5f, 0.5f), 1.5f)
                .Envelope(Env.Points(0f, 0f, 0.02f, 1f, 0.15f, 0.7f, 0.5f, 0f));
            Sound thump = Sound.Tone(Wave.Sine, 0.08f, Env.Sweep(180f, 70f, 0.08f), Env.Exp(40f, 0.001f)).Gain(0.6f);
            Sound crackle = Sound.Noise(NoiseColor.White, 0.4f, seed + 50).HighPass(5000f)
                .Map((x, t) => x * (((int)(t * 140f) * 7919 % 5) == 0 ? 1f : 0.1f)).Gain(0.12f);
            return Sound.Layer(hiss, thump, crackle).Normalize(-1f).Gain(0.9f);
        }

        /// <summary>Small rocket blast: a short dry bang, 0.3 s, quieter than Explosion1 (many land at once).</summary>
        private static Sound RocketPop(int seed)
        {
            var rng = new Random(seed * 5 + 1);
            float f = 140f + (float)rng.NextDouble() * 40f;
            Sound boom = Sound.Tone(Wave.Sine, 0.25f, Env.Sweep(f, 50f, 0.25f), Env.Exp(16f, 0.001f));
            Sound crack = Sound.Noise(NoiseColor.Pink, 0.2f, seed).LowPass(3500f).Envelope(Env.Exp(25f, 0.001f));
            return Sound.Layer(boom, crack).Drive(1.5f).Normalize(-6f).Gain(0.6f);
        }

        /// <summary>Magma Lobber launch: a heavy creaking throw with a roaring fire whoosh, 0.8 s.</summary>
        private static Sound MagmaLob()
        {
            Sound thump = Sound.Tone(Wave.Sine, 0.3f, Env.Sweep(110f, 55f, 0.3f), Env.Exp(10f, 0.002f));
            Sound creak = Sound.Tone(Wave.Saw, 0.2f, Env.Sweep(160f, 120f, 0.2f), Env.Exp(15f, 0.005f)).LowPass(900f).Gain(0.35f);
            Sound roar = Sound.Noise(NoiseColor.Brown, 0.8f, 21).LowPass(Env.Sweep(400f, 1500f, 0.8f))
                .Envelope(Env.Points(0f, 0f, 0.1f, 1f, 0.5f, 0.6f, 0.8f, 0f)).Gain(0.9f);
            Sound crackle = Sound.Noise(NoiseColor.White, 0.7f, 22).HighPass(3000f)
                .Map((x, t) => x * (((int)(t * 90f) * 104729 % 7) == 0 ? 0.9f : 0.05f)).Gain(0.15f);
            return Sound.Layer(thump, creak, roar, crackle).Normalize(-1f);
        }

        /// <summary>Magma impact: a deep boom with a long sizzle, 1.4 s (Explosion1 level, darker).</summary>
        private static Sound MagmaImpact()
        {
            Sound boom = Sound.Tone(Wave.Sine, 0.6f, Env.Sweep(95f, 35f, 0.6f), Env.Exp(6f, 0.002f));
            Sound body = Sound.Noise(NoiseColor.Brown, 0.6f, 31).LowPass(1200f).Envelope(Env.Exp(8f, 0.002f)).Gain(0.8f);
            Sound sizzle = Sound.Noise(NoiseColor.White, 1.4f, 32).BandPass(5500f, 0.8f)
                .Envelope(Env.Points(0f, 0f, 0.1f, 0.8f, 0.6f, 0.4f, 1.4f, 0f))
                .Map((x, t) => x * (0.6f + 0.4f * (float)Math.Sin(t * 70f) * (float)Math.Sin(t * 23f))).Gain(0.35f);
            return Sound.Layer(boom, body, sizzle).Drive(1.3f).Reverb(room: 0.5f, mix: 0.15f, tail: 0.3f).Normalize(-5f);
        }

        /// <summary>
        /// Bladestorm loop: a steady whirr (blades cutting air: noise pulsing 12 times a second) over a low motor hum,
        /// 2 s, seamless. Played quietly per tower.
        /// </summary>
        private static Sound BladeWhirr()
        {
            const float length = 2f, overlap = 0.25f, total = length + overlap;
            Sound air = Sound.Noise(NoiseColor.Pink, total, 41).BandPass(1600f, 0.9f)
                .Map((x, t) => x * (0.45f + 0.55f * (float)Math.Pow(0.5 + 0.5 * Math.Sin(2 * Math.PI * 12f * t), 2)));
            Sound hum = Sound.Tone(Wave.Saw, total, 75f).LowPass(400f).Gain(0.35f);
            Sound ring = Sound.Tone(Wave.Sine, total, 1150f, t => 0.08f).Gain(1f);
            return Seamless(Sound.Layer(air, hum, ring), length, overlap).Normalize(-8f);
        }

        /// <summary>
        /// Beam Cannon loop: a continuous energy beam: detuned saw drone at 110/111.5 Hz with a shimmering band of
        /// noise and a slow wobble, 2 s, seamless.
        /// </summary>
        private static Sound BeamHum()
        {
            const float length = 2f, overlap = 0.25f, total = length + overlap;
            Sound drone = Sound.Layer(Sound.Tone(Wave.Saw, total, 110f), Sound.Tone(Wave.Saw, total, 111.5f)).LowPass(1800f).Gain(0.5f);
            Sound shimmer = Sound.Noise(NoiseColor.White, total, 51).BandPass(4200f, 2f)
                .Map((x, t) => x * (0.6f + 0.4f * (float)Math.Sin(2 * Math.PI * 4f * t))).Gain(0.5f);
            Sound whine = Sound.Tone(Wave.Sine, total, t => 880f + 20f * (float)Math.Sin(2 * Math.PI * 1f * t)).Gain(0.15f);
            return Seamless(Sound.Layer(drone, shimmer, whine), length, overlap).Normalize(-8f);
        }
    }
}

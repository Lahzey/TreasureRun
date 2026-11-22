using System;

namespace SfxTool
{
    /// <summary>
    /// Reusable building blocks for everyday sounds (docs/sound-workflow.md, "Building blocks"): struck metal (bells,
    /// chimes), mechanical clicks and latches, wooden panels and doors slamming, cloth rustling. Each one is modelled on
    /// recorded examples; the comments say what in the recording each part stands for.
    /// </summary>
    public static partial class Recipes
    {
        /// <summary>
        /// A hard strike transient: a very short burst of high-passed noise (2–12 ms). This is the "punch" of anything
        /// struck: clapper on a bell, hammer on metal, key on a register. Without it, metal sounds like a sine pad.
        /// </summary>
        public static Sound Strike(int seed, float highPass = 2500f, float decay = 450f, float length = 0.02f) =>
            Sound.Noise(NoiseColor.White, length, seed).HighPass(highPass).Envelope(Env.Exp(decay, 0.0002f));

        /// <summary>
        /// Struck metal by modal synthesis: one decaying sine per mode (frequency ratio, gain). Higher modes decay faster
        /// (rate × (1 + <paramref name="damping"/> × (ratio − 1))), which is what makes a bell's tail settle on its
        /// fundamental. Each mode has a twin detuned by <paramref name="beat"/> (a fraction, 0.001 = 0.1%) that beats
        /// against it: the slow shimmer of a real bell's ring ("Nachhall").
        /// </summary>
        public static Sound Modal(float f, float length, float decay, float damping, float beat, params (float ratio, float gain)[] modes)
        {
            Sound s = Sound.Silence(length);
            foreach (var (ratio, gain) in modes)
            {
                float rate = decay * (1f + damping * (ratio - 1f));
                s = s.Mix(Sound.Tone(Wave.Sine, length, f * ratio, Env.Exp(rate, 0.0004f)), 0f, gain);
                if (beat > 0f) s = s.Mix(Sound.Tone(Wave.Sine, length, f * ratio * (1f + beat), Env.Exp(rate * 1.1f, 0.0004f)), 0f, gain * 0.6f);
            }
            return s;
        }

        /// <summary>
        /// A small brass bell (shop door, register chime): the strike transient plus modes 1, 2.32, 4.15, 6.4 (a small
        /// open bell / thick plate; church bells use 0.5, 1, 1.2, 1.5, 2), fundamental decaying over about a second.
        /// </summary>
        public static Sound SmallBell(int seed, float f, float length = 1.6f, float decay = 3.2f, float strike = 0.55f) =>
            Modal(f, length, decay, 0.9f, 0.0016f, (1f, 1f), (2.32f, 0.5f), (4.15f, 0.3f), (6.4f, 0.16f))
                .Mix(Strike(seed, 3000f, 380f), 0f, strike);

        /// <summary>
        /// Mechanical clatter (keys, levers, a ratchet): <paramref name="count"/> tiny clicks with irregular spacing, each a
        /// band-passed noise tick (2–6 kHz) of a few milliseconds, plus a soft low thunk of the mechanism under them.
        /// </summary>
        public static Sound Clatter(Random rng, int count, float spacing = 0.025f, float gain = 1f)
        {
            Sound s = Sound.Silence(count * spacing * 1.6f + 0.1f);
            float t = 0f;
            for (int i = 0; i < count; i++)
            {
                float centre = 2000f + (float)rng.NextDouble() * 4000f;
                Sound tick = Sound.Noise(NoiseColor.White, 0.012f, rng.Next()).BandPass(centre, 2.5f).Envelope(Env.Exp(420f, 0.0003f));
                s = s.Mix(tick, t, gain * (0.5f + 0.5f * (float)rng.NextDouble()));
                t += spacing * (0.5f + (float)rng.NextDouble());
            }
            Sound thunk = Sound.Tone(Wave.Sine, 0.1f, Env.Sweep(170f, 110f, 0.06f), Env.Exp(45f, 0.001f));
            return s.Mix(thunk, 0.01f, gain * 0.35f);
        }

        /// <summary>
        /// Mechanical rattle (a register's keys and drawer, a ratchet, loose parts): bright noise (1–12 kHz) chopped into
        /// fast irregular grains of 2–6 ms, scaled by <paramref name="swell"/>. In recordings these read as a dense broadband
        /// smear lasting 0.2–0.3 s, not as separate clicks; layer a few <see cref="Clatter"/> ticks on top for the
        /// individual keys.
        /// </summary>
        public static Sound Rattle(int seed, float length, Func<float, float> swell, float low = 1000f, float high = 12000f)
        {
            var rng = new Random(seed);
            int n = (int)(length / 0.002f) + 2;
            var grain = new float[n];
            float level = 0.5f;
            for (int i = 0; i < n; i++)
            {
                if (i % (1 + rng.Next(3)) == 0) level = (float)Math.Pow(rng.NextDouble(), 1.8);
                grain[i] = level;
            }
            return Sound.Noise(NoiseColor.White, length, seed).HighPass(low).LowPass(high)
                .Map((x, t) => x * grain[Math.Min(n - 1, (int)(t / 0.002f))] * swell(t));
        }

        /// <summary>
        /// A metal latch or catch snapping shut: a bright tick and a short high ring (a small spring or plate).
        /// </summary>
        public static Sound Latch(int seed, float ring = 3200f) =>
            Strike(seed, 2800f, 500f, 0.01f).Mix(Sound.Tone(Wave.Sine, 0.09f, ring, Env.Exp(70f, 0.0004f)), 0f, 0.35f);

        /// <summary>
        /// A door or heavy panel slamming against its frame (from a recorded door close): a bright contact crack whose
        /// highs are gone within about 50 ms, a mid "knock" of the panel (400–600 Hz), and a long low body: low-passed brown
        /// noise and a sine falling to <paramref name="body"/> Hz that rumble on for half a second. Then a little frame rattle.
        /// The low body is what makes it heavy; the crack is what makes it hard.
        /// </summary>
        public static Sound Slam(int seed, float body = 65f, float knock = 480f, float length = 0.8f)
        {
            var rng = new Random(seed);
            Sound crack = Sound.Noise(NoiseColor.White, 0.06f, seed).HighPass(1500f).Envelope(Env.Exp(90f, 0.0004f));
            Sound panel = Sound.Noise(NoiseColor.Pink, 0.18f, seed + 1).BandPass(knock, 1.6f).Envelope(Env.Exp(28f, 0.0008f));
            Sound rumble = Sound.Noise(NoiseColor.Brown, length, seed + 2).LowPass(170f).Envelope(Env.Exp(6.5f, 0.003f));
            Sound thump = Sound.Tone(Wave.Sine, length, Env.Sweep(body * 1.6f, body, 0.07f), Env.Exp(8f, 0.002f));
            Sound s = Sound.Layer(crack.Gain(0.55f), panel.Gain(1.2f), rumble.Gain(1.6f), thump.Gain(0.9f));
            for (int i = 0; i < 3; i++)
                s = s.Mix(Sound.Noise(NoiseColor.White, 0.01f, rng.Next()).BandPass(1500f + 1500f * (float)rng.NextDouble(), 3f).Envelope(Env.Exp(300f, 0.0003f)),
                          0.05f + 0.07f * i + 0.03f * (float)rng.NextDouble(), 0.12f);
            return s;
        }

        /// <summary>
        /// Cloth rustling (from a recorded rustle): dark noise (most energy below 1 kHz, pink low-passed at
        /// <paramref name="brightness"/>, with a brown-noise "whoomp" of the moving fabric under it), chopped into irregular
        /// grains of 8–30 ms whose loudness jumps at random (sparse, mostly quiet, a few loud), scaled by a slow
        /// <paramref name="swell"/> for the gesture, plus a few sharp crackles (fibres snapping, folds catching).
        /// </summary>
        public static Sound ClothRustle(int seed, float length, Func<float, float> swell, float brightness = 1400f, int crackles = 5)
        {
            var rng = new Random(seed);
            int n = (int)(length / 0.004f) + 2;
            var grain = new float[n];
            float level = 0.3f;
            for (int i = 0; i < n; i++)
            {
                if (i % (2 + rng.Next(6)) == 0) level = 0.15f + 0.85f * (float)Math.Pow(rng.NextDouble(), 2.5);
                grain[i] = level;
            }
            Func<float, float> g = t =>
            {
                float p = t / 0.004f;
                int i = Math.Min(n - 2, (int)p);
                return grain[i] + (grain[i + 1] - grain[i]) * (p - i);
            };
            Sound hiss = Sound.Noise(NoiseColor.Pink, length, seed).LowPass(brightness, 0.8f).HighPass(120f).Map((x, t) => x * g(t) * swell(t));
            Sound whoomp = Sound.Noise(NoiseColor.Brown, length, seed + 1).LowPass(260f).Map((x, t) => x * swell(t));
            Sound s = Sound.Layer(hiss.Gain(1.2f), whoomp.Gain(0.9f));
            for (int i = 0; i < crackles; i++)
            {
                float at = (float)rng.NextDouble() * length * 0.9f;
                s = s.Mix(Sound.Noise(NoiseColor.White, 0.006f, rng.Next()).HighPass(2200f).Envelope(Env.Exp(500f, 0.0003f)), at, (0.1f + 0.15f * swell(at)) * (0.4f + 0.6f * (float)rng.NextDouble()));
            }
            return s;
        }
    }
}

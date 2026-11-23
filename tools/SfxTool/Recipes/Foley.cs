using System;

namespace SfxTool
{
    /// <summary>
    /// Reusable building blocks for everyday sounds (docs/sound-workflow.md, "Building blocks"): struck metal (bells,
    /// chimes), mechanical clicks and latches, wooden panels and doors slamming, cloth rustling, and brittle and wet
    /// things (an egg cracking, twigs, a wet splat). Each one is modelled on
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


        // ------------------------------------------------------------------ brittle and wet (the Lizard Nest)

        /// <summary>A click of 1-3 ms: high-passed noise, no ring (anything brittle: shell, splinters, a wet fleck).</summary>
        public static Sound Tick(int seed, float highPass, float length = 0.002f) =>
            Sound.Noise(NoiseColor.White, length + 0.004f, seed).HighPass(highPass).HighPass(highPass).Envelope(Env.Exp(1.5f / length * 3f, 0.0001f));

        /// <summary>
        /// An egg cracking (0.26 s, from a recording of an egg cracked by hand): three tiny bright ticks 8-10 ms apart as
        /// the shell gives (4-16 kHz, 1-2 ms each); the main crack 12 ms later, broadband with a bite round 2.7 kHz and a strong
        /// low-mid knock of the egg's body (100-500 Hz, as loud as the crack itself); a second, duller crack (1-2 kHz) 25 ms after it; the fragments crackling for 90 ms (tiny
        /// grains round 1.5-4 kHz, fading); and, 0.1 s on, two faint low knocks (under 300 Hz, 15 dB under the crack) as
        /// the egg settles. The first ticks are quiet (15-20 dB under the crack). No ring anywhere:
        /// a shell is brittle, every part dies within a few milliseconds.
        /// </summary>
        public static Sound EggCrack(Random rng)
        {
            Sound s = Sound.Silence(0.26f);
            s = s.Mix(Tick(rng.Next(), 4000f), 0f, 0.1f)
                 .Mix(Tick(rng.Next(), 3500f), 0.008f + 0.002f * (float)rng.NextDouble(), 0.15f)
                 .Mix(Tick(rng.Next(), 4500f, 0.0015f), 0.018f + 0.002f * (float)rng.NextDouble(), 0.3f);
            // The main crack: a broadband burst, its knock in the egg's body.
            Sound crack = Sound.Noise(NoiseColor.White, 0.03f, rng.Next()).HighPass(150f).Envelope(Env.Exp(380f, 0.0002f));
            Sound knock = Sound.Noise(NoiseColor.Pink, 0.05f, rng.Next()).BandPass(220f, 0.9f).Envelope(Env.Exp(110f, 0.0004f));
            Sound thud = Sound.Noise(NoiseColor.Brown, 0.08f, rng.Next()).LowPass(180f).Envelope(Env.Exp(55f, 0.0005f));
            Sound bite = Sound.Noise(NoiseColor.White, 0.03f, rng.Next()).BandPass(2700f, 0.8f).Envelope(Env.Exp(260f, 0.0002f));
            s = s.Mix(crack, 0.03f, 1f).Mix(bite, 0.03f, 1.8f).Mix(knock, 0.031f, 2.4f).Mix(thud, 0.031f, 1.6f);
            // The second crack, duller.
            Sound second = Sound.Noise(NoiseColor.White, 0.03f, rng.Next()).BandPass(1500f, 1f).Envelope(Env.Exp(320f, 0.0002f));
            s = s.Mix(second, 0.056f, 0.9f).Mix(Sound.Noise(NoiseColor.Pink, 0.04f, rng.Next()).BandPass(230f, 1f).Envelope(Env.Exp(140f, 0.0004f)), 0.057f, 1.6f);
            // The fragments crackling.
            for (int i = 0; i < 16; i++)
            {
                float t = 0.062f + 0.09f * i / 16f + 0.004f * (float)rng.NextDouble();
                Sound grain = Sound.Noise(NoiseColor.White, 0.006f, rng.Next()).BandPass(1500f + 2500f * (float)rng.NextDouble(), 1.5f).Envelope(Env.Exp(900f, 0.0002f));
                s = s.Mix(grain, t, 0.7f * (1f - i / 17f) * (0.5f + 0.5f * (float)rng.NextDouble()));
            }
            // The egg settling: two faint low knocks.
            foreach (float t in new[] { 0.135f, 0.155f })
                s = s.Mix(Sound.Noise(NoiseColor.Brown, 0.04f, rng.Next()).LowPass(300f).Envelope(Env.Exp(90f, 0.001f)), t + 0.005f * (float)rng.NextDouble(), 0.25f);
            return s;
        }

        /// <summary>
        /// Twigs and dry grass scraping against each other: crisp grains (700 Hz - 4 kHz, a new loudness every 3-12 ms,
        /// mostly quiet, a few loud) for the twigs' bark, a darker interpolated rustle for the dry grass, and the low
        /// "whff" of the nest's mass shifting, all scaled by <paramref name="swell"/>.
        /// </summary>
        public static Sound TwigRustle(int seed, float length, Func<float, float> swell)
        {
            var rng = new Random(seed);
            int n = (int)(length / 0.003f) + 2;
            var grain = new float[n];
            float level = 0.3f;
            for (int i = 0; i < n; i++)
            {
                if (i % (1 + rng.Next(4)) == 0) level = (float)Math.Pow(rng.NextDouble(), 2.2);
                grain[i] = level;
            }
            Sound bark = Sound.Noise(NoiseColor.White, length, seed).HighPass(700f).HighPass(700f).LowPass(4000f)
                .Map((x, t) => x * grain[Math.Min(n - 1, (int)(t / 0.003f))] * swell(t));
            Sound grass = ClothRustle(seed + 1, length, swell, 1600f, 6).HighPass(250f).HighPass(250f);
            Sound mass = Sound.Noise(NoiseColor.Brown, length, seed + 2).LowPass(220f).Map((x, t) => x * swell(t));
            return Sound.Layer(bark.Gain(1.3f), grass.Gain(0.55f), mass.Gain(0.22f));
        }

        /// <summary>
        /// A wet fleck landing: a click band-passed somewhere between <paramref name="low"/> and 4.5 times that, dying in
        /// 2-15 ms (from the splat recording: the bits of goo slapping down are short transients, centroid 4-9 kHz; the
        /// late drips are the brightest).
        /// </summary>
        public static Sound WetClick(Random rng, float gain, float low = 1500f)
        {
            float centre = low * (float)Math.Pow(4.5, rng.NextDouble()), rate = 250f + 1200f * (float)rng.NextDouble();
            return Sound.Noise(NoiseColor.White, 0.03f, rng.Next()).BandPass(centre, 1f).HighPass(700f).Envelope(Env.Exp(rate, 0.0002f)).Gain(gain);
        }

        /// <summary>
        /// A soft "gloop" of goo: pink noise in a narrow band (Q 5) round 1-2.4 kHz whose centre sags a little, swelling in
        /// 10 ms and dying over 0.12-0.18 s (the recording's two late resonant blobs). Noise, not a sine: a sine reads as a
        /// water droplet.
        /// </summary>
        public static Sound Gloop(Random rng, float gain)
        {
            float f = 1000f + 1400f * (float)rng.NextDouble(), len = 0.12f + 0.06f * (float)rng.NextDouble();
            return Sound.Noise(NoiseColor.Pink, len, rng.Next()).BandPass(Env.Sweep(f * 1.25f, f, 0.05f), 5f)
                .Envelope(Env.Points(0f, 0f, 0.01f, 1f, len, 0f)).Gain(gain);
        }

        /// <summary>
        /// A wet splat (from a recording of a wet splat impact). <paramref name="size"/> 1 is a big body (a lizard bursting);
        /// smaller is shorter, higher and lighter. The parts, in the recording's proportions:
        /// <list type="bullet">
        /// <item>the impact: a heavy low body (31-125 Hz): low-passed brown noise swelling in 30 ms and gone by 0.3 s, with a
        /// little of a sine falling from 85 to 40 Hz for weight (more sine sounds like a tonal thump, a drum);</item>
        /// <item>the splash: a dense wet burst over 700 Hz - 12 kHz, fast irregular grains, strongest in the first 60 ms and
        /// gone by 0.3 s; nothing between 250 and 500 Hz (the gap between the body and the splash is what keeps it wet,
        /// not muddy);</item>
        /// <item>the splatter: 12 sharp wet clicks from 0.12 to 0.32 s, nearly as loud as the impact;</item>
        /// <item>the drips: about 16 sparse, quieter and brighter clicks (3-13 kHz) fading out to 0.75 s, and two soft gloops.</item>
        /// </list>
        /// No hiss, no tones.
        /// </summary>
        public static Sound Splat(Random rng, float size)
        {
            float length = 0.3f + 0.6f * size;
            Sound s = Sound.Silence(length);
            Func<float, float> bodyEnv = Env.Points(0f, 0f, 0.025f * size + 0.005f, 1f, 0.08f * size, 0.45f, 0.2f * size, 0.07f, 0.32f * size, 0f);
            Sound body = Sound.Tone(Wave.Sine, 0.4f * size, Env.Sweep(85f / size, 40f / size, 0.12f), bodyEnv).Gain(0.45f)
                .Mix(Sound.Noise(NoiseColor.Brown, 0.4f * size, rng.Next()).LowPass(140f / size).HighPass(30f).Envelope(bodyEnv), 0f, 2.2f);
            s = s.Mix(body, 0f, 0.75f * size);
            Func<float, float> splashEnv = Env.Points(0f, 0f, 0.003f, 1f, 0.06f * size + 0.02f, 0.55f, 0.15f * size + 0.03f, 0.18f, 0.3f * size + 0.05f, 0f);
            Sound splash = Sound.Noise(NoiseColor.White, length, rng.Next()).HighPass(450f).HighPass(450f).LowPass(8000f).Envelope(splashEnv).Gain(0.4f)
                .Mix(Rattle(rng.Next(), length, splashEnv, 500f, 8000f), 0f, 1.1f)
                .Mix(Rattle(rng.Next(), length, splashEnv, 450f, 2500f).BandPass(1200f, 0.6f), 0f, 1.6f);
            s = s.Mix(splash, 0f, 1f);
            int splatter = (int)(12 * size) + 2;
            for (int i = 0; i < splatter; i++)
                s = s.Mix(WetClick(rng, 0.9f + 0.6f * (float)rng.NextDouble()), (0.12f + 0.2f * (float)rng.NextDouble()) * size);
            int drips = (int)(16 * size) + 1;
            for (int i = 0; i < drips; i++)
            {
                float t = (0.3f + 0.45f * (float)rng.NextDouble()) * size;
                s = s.Mix(WetClick(rng, 1.4f * (1.1f - t / Math.Max(0.1f, 0.8f * size)) * (0.4f + 0.6f * (float)rng.NextDouble()), 3000f), t);
            }
            if (size >= 0.5f)
            {
                s = s.Mix(Gloop(rng, 0.7f), 0.33f * size);
                s = s.Mix(Gloop(rng, 0.6f), 0.58f * size);
            }
            return s;
        }
    }
}

using System;
using System.Linq;

namespace SfxTool
{
    /// <summary>
    /// The cash register (buying and selling at the merchant) as an event model fitted to a recording (docs/sound-workflow.md,
    /// "Fitting a model"). The structure is hand-written from the analysis of the recording (<c>inspect</c>, <c>envelope</c>);
    /// the numbers are fitted by <c>SfxTool fit register &lt;recording&gt;</c> and live in Recipes/Fitted/RegisterFit.cs.
    /// Times are seconds from the start of the sound.
    ///
    /// What the recording is made of:
    ///  1. The key stroke (0.02–0.25 s): a sliding noise bed rising over 0.2 s (its lows first, its highs late) and a
    ///     ratchet of clicks: small ones 12 ms apart, bigger ones accelerating (0.116, 0.144, 0.173, 0.191 s), the
    ///     strongest at 0.205 s. Each click is bright (4–16 kHz), a few milliseconds long, and briefly rings the metal of
    ///     the mechanism (581 Hz, 2.2, 2.71, 4.52, 5.08, 7.17, 13.2 kHz, 5–40 ms). At the peak the mechanism releases: a
    ///     bright burst holding for about 25 ms.
    ///  2. The till bell, struck at the peak (~0.214 s): a cluster of modes around 2.1 kHz (2.02, 2.05, 2.09, 2.11 kHz:
    ///     they beat against each other), the loudest mode at 4.8 kHz (with neighbours at 4.75 and 4.87 kHz), 7.73 kHz, and
    ///     short-lived high modes (6.0, 7.46, 10.0, 11.0, 11.7, 14.9, 15.7 kHz) that make the strike bright.
    ///  3. The drawer: a low swell as it starts to slide (0.461 s, mostly below 1 kHz), then impacts: a bright crack (a few
    ///     milliseconds, 4–16 kHz) whose low body thump (below 1 kHz) arrives about 8 ms later; at 0.501 s, a small one at
    ///     0.564 s, the main one at 0.576 s (the loudest moment of the sound) and a bounce at 0.598 s; each crack briefly
    ///     rings the drawer's metal (730 Hz, 1.68, 3.88, 4.57, 5.25, 8.31 kHz); a rattle in between and after (grainy
    ///     noise), fading by 0.66 s.
    ///  4. The tail: soft knocks as the drawer settles (0.703, 0.846, ~0.99, ~1.13 s), each ringing modes of the
    ///     counter and drawer (366, 824, 969 Hz, and quieter 2.86, 5.47, 6.96 kHz), over a faint low rumble (150–600 Hz).
    ///     No hiss.
    /// </summary>
    public static partial class Recipes
    {
        private static readonly float[] EqPoints = { 63, 125, 250, 500, 1000, 2000, 4000, 8000, 16000 };

        /// <summary>Added to every noise seed of fitted models: the fitter's floor check renders a second take with other noise.</summary>
        internal static int SeedOffset;

        /// <summary>
        /// A 9-point EQ (octaves 63 Hz – 16 kHz) whose gains are fitted parameters "name.eq&lt;hz&gt;"; the defaults are
        /// given from 125 Hz up (63 Hz starts 6 dB below 125 Hz).
        /// </summary>
        private static (float hz, float db)[] Eq8(Params p, string name, params float[] defaults) => EqCapped(p, name, 20, 20, defaults);

        /// <summary>An EQ whose points up to 125 Hz may not rise above <paramref name="maxLow"/> and from 2 kHz above <paramref name="maxHigh"/> dB.</summary>
        private static (float hz, float db)[] EqCapped(Params p, string name, float maxLow, float maxHigh, params float[] defaults) =>
            EqPoints.Select((hz, i) => (hz, p.P($"{name}.eq{(int)hz}", Math.Min(i == 0 ? defaults[0] - 6 : defaults[i - 1], hz <= 125 ? maxLow : hz >= 2000 ? maxHigh : 20),
                                                2, -60, hz <= 125 ? maxLow : hz >= 2000 ? maxHigh : 20))).ToArray();

        /// <summary>White noise through an EQ, scaled back to RMS 1 (so a level parameter means the layer's RMS in dB).</summary>
        private static Sound ShapedNoise(int seed, float seconds, (float hz, float db)[] eq)
        {
            Sound n = Sound.Noise(NoiseColor.White, seconds, seed + SeedOffset).Eq(eq);
            float rms = Compare.Rms(n.S, 0, n.Length);
            return rms > 0 ? n.Gain(1f / rms) : n;
        }

        /// <summary>The factor that keeps white noise at the same RMS through an EQ (for sums of bursts EQ'd together).</summary>
        private static float EqNorm((float hz, float db)[] eq)
        {
            double sum = 0; int n = 0;
            for (double f = 20; f < 16000; f += 20) { sum += Math.Pow(10, Dsp.GainAt(eq, f) / 10); n++; }
            return (float)(1 / Math.Sqrt(sum / n));
        }

        /// <summary>An amplitude curve through (time, dB) breakpoints: linear in dB between them, silent outside.</summary>
        private static Func<float, float> Curve(float[] times, float[] db) => t =>
        {
            if (t <= times[0] || t >= times[times.Length - 1]) return 0f;
            for (int i = 1; i < times.Length; i++)
                if (t <= times[i])
                {
                    float d = db[i - 1] + (db[i] - db[i - 1]) * (t - times[i - 1]) / (times[i] - times[i - 1]);
                    return (float)Math.Pow(10, d / 20);
                }
            return 0f;
        };

        private static float[] Levels(Params p, string name, float[] defaults, double step = 2, double min = -90, double max = 0) =>
            defaults.Select((d, i) => p.P($"{name}.l{i}", d, step, min, max)).ToArray();

        /// <summary>Event times as fitted parameters "name.t&lt;i&gt;", each free to move by ±<paramref name="range"/> seconds.</summary>
        private static float[] Times(Params p, string name, float[] measured, double range = 0.004) =>
            measured.Select((t, i) => p.P($"{name}.t{i}", t, 0.0005, t - range, t + range)).ToArray();

        /// <summary>
        /// Bursts (sharp attack, exponential decay) of white noise at the given times and levels, summed and EQ'd together:
        /// clicks, cracks, knocks. <paramref name="tauMs"/> is the decay time constant per burst.
        /// </summary>
        private static Sound Bursts(int seed, float seconds, float[] times, float[] levels, float[] tauMs, (float hz, float db)[] eq, float delay = 0f)
        {
            var raw = new Sound(Sound.Samples(seconds));
            var rng = new Random(seed + SeedOffset);
            for (int i = 0; i < times.Length; i++)
            {
                float tau = tauMs[Math.Min(i, tauMs.Length - 1)] / 1000f, a = (float)Math.Pow(10, levels[i] / 20);
                int start = (int)((times[i] + delay) * Sound.Rate), len = (int)(tau * 7 * Sound.Rate);
                for (int k = 0; k < len && start + k < raw.Length; k++)
                {
                    if (start + k < 0) continue;
                    float t = k / (float)Sound.Rate;
                    float env = (t < 0.0003f ? t / 0.0003f : 1f) * (float)Math.Exp(-t / tau);
                    raw.S[start + k] += a * env * (float)(rng.NextDouble() * 2 - 1) * 1.732f;
                }
            }
            return raw.Eq(eq).Gain(EqNorm(eq));
        }

        /// <summary>Random grain envelope (2–6 ms steps, levels mostly low): the texture of a rattle.</summary>
        private static Func<float, float> Grains(int seed, float seconds)
        {
            var rng = new Random(seed + SeedOffset);
            int n = (int)(seconds / 0.002f) + 2;
            var g = new float[n];
            float level = 0.5f;
            for (int i = 0; i < n; i++) { if (i % (1 + rng.Next(3)) == 0) level = (float)Math.Pow(rng.NextDouble(), 1.6); g[i] = level; }
            return t => g[Math.Min(n - 1, Math.Max(0, (int)(t / 0.002f)))] * 1.6f;
        }

        /// <summary>
        /// The brief metallic ring of a mechanism's parts: the excitation (clicks, cracks) drives a bank of resonators whose
        /// levels and decays are fitted parameters "name.f&lt;hz&gt;.db" / ".decay". Measured modes ring for 5–40 ms;
        /// <paramref name="minDecay"/> (dB/s) keeps a ring from turning into a tone (wood: 250).
        /// </summary>
        private static Sound Clank(Params p, string name, Sound excitation, (float f, float db, float decay)[] modes, double minDecay = 20)
        {
            Sound sum = Sound.Silence(excitation.Duration);
            foreach (var m in modes)
                sum = sum.Mix(excitation.Resonate(m.f, p.P($"{name}.f{(int)m.f}.decay", Math.Max(m.decay, minDecay), m.decay * 0.15, minDecay, 5000)),
                              0f, (float)Math.Pow(10, p.P($"{name}.f{(int)m.f}.db", m.db, 2, -80, 20) / 20));
            return sum;
        }

        public static Sound RegisterModel(Params p, bool final)
        {
            const float length = 1.33f;
            Sound s = Sound.Silence(length);

            // ---- 1. Key stroke: the sliding bed (lows and highs with their own curves) and the ratchet clicks.
            float[] bedT = { 0.02f, 0.045f, 0.07f, 0.095f, 0.115f, 0.14f, 0.17f, 0.195f, 0.21f, 0.23f, 0.25f, 0.28f, 0.32f };
            // The bed is the sliding noise under the clicks: capped, so it can't stand in for a click.
            float[] bedLow = Levels(p, "keys.bedlow", new float[] { -90, -54, -52, -44, -42, -38, -38, -34, -30, -28, -34, -40, -90 }, max: -22);
            float[] bedHigh = Levels(p, "keys.bedhigh", new float[] { -90, -56, -52, -44, -38, -33, -33, -28, -22, -22, -30, -42, -90 }, max: -16);
            Func<float, float> cl = Curve(bedT, bedLow), ch = Curve(bedT, bedHigh);
            s = s.Mix(ShapedNoise(101, 0.33f, EqCapped(p, "keys.bedlow", -24, 20, -30, -10, 0, 0, -6, -24, -40, -50)).Map((x, t) => x * cl(t)), 0f);
            s = s.Mix(ShapedNoise(102, 0.33f, Eq8(p, "keys.bedhigh", -40, -34, -26, -16, -6, 0, 0, -4)).Map((x, t) => x * ch(t)), 0f);

            float[] clickT = Times(p, "keys.click", new[] { 0.028f, 0.068f, 0.080f, 0.092f, 0.104f, 0.115f, 0.144f, 0.173f, 0.191f, 0.205f, 0.225f, 0.240f });
            float[] clickL = Levels(p, "keys.click", new float[] { -46, -46, -42, -36, -35, -24, -22, -22, -19, -14, -16, -18 });
            float clickTau = p.P("keys.click.tau", 5, 1, 0.5, 30);
            Sound clicks = Bursts(103, 0.35f, clickT, clickL, new[] { clickTau }, EqCapped(p, "keys.click", -24, 20, -34, -28, -20, -12, -6, 0, 2, -2));
            // Some clicks carry a low body (the key's stop hitting its frame), like the drawer's impacts; the low end of the
            // stroke lives in these, not in the bed (around 0.12 s, nothing at the peak).
            float[] keyBodyL = Levels(p, "keys.body", new float[] { -80, -80, -80, -80, -80, -40, -60, -60, -70, -70, -80, -80 });
            float keyBodyTau = p.P("keys.body.tau", 15, 3, 2, 80), keyBodyDelay = p.P("keys.body.delay", 0.004, 0.001, 0, 0.02);
            s = s.Mix(Bursts(113, 0.35f, clickT, keyBodyL, new[] { keyBodyTau }, Eq8(p, "keys.body", -4, 0, -4, -14, -26, -40, -50, -60), keyBodyDelay), 0f);
            s = s.Mix(clicks, 0f).Mix(Clank(p, "keys.clank", clicks, new (float, float, float)[]
                { (581, -30, 500), (2200, -30, 750), (2710, -30, 370), (4520, -30, 880), (5080, -30, 300), (7170, -34, 400), (13240, -36, 500) }), 0f);

            // The release at the peak of the stroke: a bright burst that holds for about 25 ms before the bell takes over
            // (a plateau in the recording, not a click).
            float releaseT = p.P("keys.release.t", 0.205, 0.0005, 0.198, 0.212);
            float releaseL = p.P("keys.release.l0", -16, 2, -90, 0), releaseTau = p.P("keys.release.tau", 18, 3, 2, 80);
            s = s.Mix(Bursts(112, 0.2f, new[] { 0.01f }, new[] { releaseL }, new[] { releaseTau }, Eq8(p, "keys.release", -40, -34, -26, -16, -6, 0, 0, -4)), releaseT - 0.01f);

            // ---- 2. The till bell: modes with their own levels and decays (dB per second), struck at bell.t.
            float bellT = p.P("bell.t", 0.214, 0.002, 0.18, 0.25);
            // Decays measured with inspect (dB per second); the fitter may only move them within 0.6-1.6x, so it can't trade
            // one mode for its neighbour in the same band (it swapped 7.73 and 7.46 kHz without this).
            (float f, float db, float decay)[] modes =
            {
                (2020, -36, 90), (2050, -34, 90), (2090, -27, 50), (2110, -27, 50),
                (4750, -36, 55), (4800, -23, 58), (4870, -36, 60),
                (6010, -36, 620), (7460, -38, 400), (7730, -30, 74),
                (10030, -40, 700), (11030, -40, 400), (11740, -42, 600), (14880, -38, 490), (15680, -42, 650),
            };
            var phase = new Random(7 + SeedOffset);
            for (int i = 0; i < modes.Length; i++)
            {
                float db = p.P($"bell.m{(int)modes[i].f}.db", modes[i].db, 1.5, -90, 0);
                float decay = p.P($"bell.m{(int)modes[i].f}.decay", modes[i].decay, modes[i].decay * 0.1, modes[i].decay * 0.6, modes[i].decay * 1.6);
                Sound tone = Sound.Tone(Wave.Sine, length - bellT, _ => modes[i].f, Env.Exp(decay / 8.686f, 0.0006f), (float)phase.NextDouble());
                s = s.Mix(tone, bellT, (float)Math.Pow(10, db / 20) * 1.414f);
            }

            // ---- 3. The drawer: the slide swell, impacts (crack + delayed body), the rattle.
            float[] swellT = { 0.455f, 0.47f, 0.485f, 0.498f, 0.515f, 0.54f };
            Func<float, float> sw = Curve(swellT, Levels(p, "drawer.swell", new float[] { -90, -46, -40, -34, -36, -90 }));
            s = s.Mix(ShapedNoise(104, 0.1f, Eq8(p, "drawer.swell", -8, -2, 0, -4, -10, -8, -6, -10)).Map((x, t) => x * sw(t + 0.45f)), 0.45f);

            float[] impactT = Times(p, "drawer.impact", new[] { 0.501f, 0.564f, 0.576f, 0.598f });
            float[] crackL = Levels(p, "drawer.crack", new float[] { -15, -26, -8, -20 });
            float[] crackTau = impactT.Select((_, i) => p.P($"drawer.crack.tau{i}", new[] { 6f, 4f, 4f, 6f }[i], 1, 0.5, 40)).ToArray();
            Sound cracks = Bursts(105, 0.25f, impactT.Select(t => t - 0.45f).ToArray(), crackL, crackTau, Eq8(p, "drawer.crack", -35, -30, -24, -16, -9, -2, 0, -4));
            // The spike at the start of each impact: an impulse through the crack's EQ (metal on metal is near-instant).
            var snapEq = Eq8(p, "drawer.crack", -35, -30, -24, -16, -9, -2, 0, -4);
            float[] snapL = Levels(p, "drawer.snap", new float[] { -30, -40, -24, -36 }, 2, -90, 10);
            var snaps = new Sound(Sound.Samples(0.25f));
            for (int i = 0; i < impactT.Length; i++)
            {
                int at = (int)((impactT[i] - 0.45f) * Sound.Rate);
                if (at >= 0 && at < snaps.Length) snaps.S[at] += (float)Math.Pow(10, snapL[i] / 20) * 20f;
            }
            s = s.Mix(snaps.Eq(snapEq).Gain(EqNorm(snapEq)), 0.45f);
            s = s.Mix(cracks, 0.45f).Mix(Clank(p, "drawer.clank", cracks, new (float, float, float)[]
                { (730, -30, 300), (1680, -30, 510), (3880, -30, 440), (4570, -30, 900), (5250, -30, 770), (8310, -34, 1300) }), 0.45f);
            float[] bodyL = Levels(p, "drawer.body", new float[] { -30, -40, -26, -33 });
            float bodyTau = p.P("drawer.body.tau", 25, 3, 2, 120), bodyDelay = p.P("drawer.body.delay", 0.008, 0.002, 0, 0.03);
            s = s.Mix(Bursts(106, 0.3f, impactT.Select(t => t - 0.45f).ToArray(), bodyL, new[] { bodyTau }, Eq8(p, "drawer.body", -12, -3, 0, -2, -10, -20, -30, -40), bodyDelay), 0.45f);

            float[] rattleT = { 0.505f, 0.52f, 0.54f, 0.56f, 0.59f, 0.61f, 0.63f, 0.66f, 0.70f };
            Func<float, float> rc = Curve(rattleT, Levels(p, "drawer.rattle", new float[] { -90, -27, -28, -27, -24, -26, -30, -33, -90 }));
            float grain = p.P("drawer.rattle.grain", 0.6, 0.15, 0, 1);
            Func<float, float> gr = Grains(107, 0.25f);
            s = s.Mix(ShapedNoise(108, 0.2f, Eq8(p, "drawer.rattle", -20, -10, -5, -4, -2, 0, -2, -8))
                .Map((x, t) => x * rc(t + 0.5f) * (1 - grain + grain * gr(t))), 0.5f);

            // ---- 4. The tail: knocks ringing low modes of the counter, over a faint low rumble.
            float[] knockT = Times(p, "tail.knock", new[] { 0.703f, 0.846f, 0.99f, 1.13f });
            float[] knockL = Levels(p, "tail.knock", new float[] { -24, -31, -42, -44 });
            float knockTau = p.P("tail.knock.tau", 10, 2, 1, 60);
            // The first knock (the drawer's last real stop) is brighter than the later settling ones: its own EQ.
            Sound knocks = Bursts(109, 0.66f, new[] { knockT[0] - 0.67f }, new[] { knockL[0] }, new[] { knockTau }, Eq8(p, "tail.knock0", -12, -6, -2, 0, 0, -6, -14, -26))
                .Mix(Bursts(111, 0.66f, knockT.Skip(1).Select(t => t - 0.67f).ToArray(), knockL.Skip(1).ToArray(), new[] { knockTau }, Eq8(p, "tail.knock", -12, -6, -2, 0, -3, -10, -18, -28)), 0f);
            Sound ring = Sound.Silence(0.66f);
            foreach ((float f, float db, float decay) m in new[] { (366f, -18f, 130f), (824f, -18f, 100f), (969f, -14f, 350f), (2860f, -30f, 60f), (5470f, -34f, 120f), (6960f, -34f, 60f) })
                ring = ring.Mix(knocks.Resonate(m.f, p.P($"tail.mode{(int)m.f}.decay", m.decay, m.decay * 0.15, 10, 3000)),
                                0f, (float)Math.Pow(10, p.P($"tail.mode{(int)m.f}.db", m.db, 1.5, -80, 20) / 20));
            s = s.Mix(knocks, 0.67f).Mix(ring, 0.67f);

            // Room tone: a faint low rumble under the whole recording (it is there under the bell, too). Its EQ can't rise
            // above -35 dB from 2 kHz up, so it never turns into hiss (the recording is silent above 2 kHz between events).
            float[] floorT = { 0.0f, 0.03f, 0.12f, 0.25f, 0.35f, 0.45f, 0.6f, 0.72f, 0.9f, 1.1f, 1.25f, 1.33f };
            Func<float, float> fc = Curve(floorT, Levels(p, "room.floor", new float[] { -90, -60, -56, -56, -56, -56, -54, -48, -50, -52, -55, -90 }));
            s = s.Mix(ShapedNoise(110, length, EqCapped(p, "room.floor", 20, -35, -6, 0, 0, -8, -20, -28, -38, -48)).Map((x, t) => x * fc(t)), 0f);

            // ---- The room, then (for the game) the recording's 16 kHz edge and the level.
            s = s.Reverb(room: 0.35f, damp: 0.4f, mix: p.P("room.mix", 0.04, 0.01, 0, 0.3), tail: 0.2f).Trim(length);
            if (!final) return s;
            return s.BrickWall(15300f, 511).Fade(0.002f, 0.06f).Normalize(-3f);
        }
    }
}

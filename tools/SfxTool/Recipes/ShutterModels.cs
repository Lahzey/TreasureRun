using System;
using System.Linq;

namespace SfxTool
{
    /// <summary>
    /// The two halves of the merchant's shutter sound as event models fitted to recordings (docs/sound-workflow.md,
    /// "Fitting a model"): a door closing (<c>fit door</c>) and cloth rustling (<c>fit cloth</c>). The values live in
    /// Recipes/Fitted/DoorFit.cs and ClothFit.cs; <see cref="ShopShutter"/> mixes the two. Times are seconds from the start.
    ///
    /// The door recording:
    ///  1. The latch touches the frame (0.024 s): a dull crack (250 Hz – 1 kHz), its low body thump (31–125 Hz) about
    ///     15 ms later, and a little bright tick (0.046 s).
    ///  2. The door swings the last bit (0.05–0.22 s): quiet creaks and ticks (0.106, 0.118, 0.151, 0.175 s).
    ///  3. The slam: a cluster of cracks (0.223–0.262 s, the brightest at 0.252 s) ringing the frame's wood and fittings
    ///     (947 Hz and 1.43 kHz for up to half a second, short metal rings at 9–13 kHz), and the door's body: a heavy
    ///     thump below 125 Hz arriving 20–40 ms after the first crack and holding until about 0.32 s (the loudest
    ///     moment; most of a slam's energy is under 125 Hz).
    ///  4. Settling knocks (0.367, 0.49, 0.50 s) and a low rumble (31–125 Hz) dying away until about 1 s.
    ///
    /// The cloth recording (fabric handled and shaken): dense noise whose energy is flat from 31 to 500 Hz and falls
    /// steeply above 1 kHz: the mass of the fabric moving air, not a bright hiss. It comes in gestures (0.05–0.25 s,
    /// 0.35–0.95 s, a softer one to 1.63 s) made of 10–30 ms swells, with crisp crackles (1–4 kHz) where the fabric
    /// folds or snaps (crest factors 6–11 above 2 kHz).
    /// </summary>
    public static partial class Recipes
    {
        /// <summary>A random step envelope with steps of <paramref name="stepMs"/> (levels mostly low, a few high): grains.</summary>
        private static Func<float, float> GrainSteps(int seed, float seconds, float stepMs, double shape)
        {
            var rng = new Random(seed + SeedOffset);
            float step = stepMs / 1000f;
            int n = (int)(seconds / step) + 2;
            var g = new float[n];
            for (int i = 0; i < n; i++) g[i] = (float)Math.Pow(rng.NextDouble(), shape);
            float norm = (float)Math.Sqrt(1.0 / g.Average(v => v * v));
            // Linear between steps: swells, not clicks at every step boundary.
            return t =>
            {
                float x = t / step;
                int i = Math.Min(n - 2, Math.Max(0, (int)x));
                float f = Math.Clamp(x - i, 0f, 1f);
                return (g[i] + (g[i + 1] - g[i]) * f) * norm;
            };
        }

        public static Sound DoorModel(Params p, bool final)
        {
            const float length = 1.0f;
            Sound s = Sound.Silence(length);

            // ---- 1. The latch: crack, delayed body, a bright tick.
            float[] latchT = Times(p, "latch", new[] { 0.024f, 0.046f });
            Sound latch = Bursts(201, length, latchT, Levels(p, "latch.crack", new float[] { -14, -30 }),
                                 latchT.Select((_, i) => p.P($"latch.crack.tau{i}", new[] { 6f, 3f }[i], 1, 0.5, 40)).ToArray(),
                                 Eq8(p, "latch.crack", -6, 0, 0, -4, -8, -14, -22, -30));
            s = s.Mix(latch, 0f);
            s = s.Mix(Bursts(202, length, latchT, Levels(p, "latch.body", new float[] { -16, -60 }), new[] { p.P("latch.body.tau", 18, 3, 2, 120) },
                             EqCapped(p, "latch.body", 20, -20, 0, -6, -16, -26, -36, -46, -56, -60), p.P("latch.body.delay", 0.015, 0.002, 0, 0.04)), 0f);

            // ---- 2. The swing: creak noise and small ticks.
            float[] creakT = { 0.05f, 0.08f, 0.11f, 0.14f, 0.17f, 0.2f, 0.222f };
            Func<float, float> cc = Curve(creakT, Levels(p, "swing.creak", new float[] { -90, -34, -34, -38, -38, -36, -90 }));
            Func<float, float> cg = GrainSteps(203, length, 6, 1.5);
            s = s.Mix(ShapedNoise(204, length, Eq8(p, "swing.creak", -2, 0, -6, -8, -8, -10, -14, -20)).Map((x, t) => x * cc(t) * cg(t)), 0f);
            float[] tickT = Times(p, "swing.tick", new[] { 0.106f, 0.118f, 0.151f, 0.175f });
            Sound ticks = Bursts(205, length, tickT, Levels(p, "swing.tick", new float[] { -30, -28, -24, -28 }), new[] { p.P("swing.tick.tau", 4, 1, 0.5, 30) },
                                 Eq8(p, "swing.tick", -20, -14, -8, -4, 0, 0, 0, -6));
            s = s.Mix(ticks, 0f);

            // ---- 3. The slam: cracks ringing the frame, and the heavy body.
            float[] slamT = Times(p, "slam", new[] { 0.223f, 0.232f, 0.244f, 0.252f, 0.262f }, 0.005);
            float[] slamTau = slamT.Select((_, i) => p.P($"slam.crack.tau{i}", 5, 1, 0.5, 40)).ToArray();
            Sound cracks = Bursts(206, length, slamT, Levels(p, "slam.crack", new float[] { -16, -18, -14, -10, -14 }), slamTau,
                                  Eq8(p, "slam.crack", -8, -4, -2, 0, 0, -2, -2, -8));
            s = s.Mix(cracks, 0f).Mix(Clank(p, "slam.clank", cracks, new (float, float, float)[]
                { (947, -40, 46), (1430, -40, 125), (2100, -40, 300), (9730, -36, 770), (10250, -36, 1100), (12810, -38, 300) }, 250), 0f);
            Sound body = Bursts(207, length, slamT, Levels(p, "slam.body", new float[] { -20, -20, -14, -12, -14 }), new[] { p.P("slam.body.tau", 30, 4, 2, 150) },
                                EqCapped(p, "slam.body", 20, -16, 0, -8, -16, -24, -34, -44, -54, -60), p.P("slam.body.delay", 0.025, 0.003, 0, 0.06));
            s = s.Mix(body, 0f);
            // The door's mass rings at a low pitch for a moment (a thump with a tone, not just low noise).
            Sound sub = Sound.Silence(length);
            foreach ((float f, float db, float decay) m in new[] { (45f, -40f, 60f), (62f, -40f, 70f), (90f, -42f, 90f), (140f, -46f, 120f) })
                sub = sub.Mix(body.Resonate(m.f, p.P($"slam.mode{(int)m.f}.decay", m.decay, m.decay * 0.15, 10, 1000)), 0f,
                              (float)Math.Pow(10, p.P($"slam.mode{(int)m.f}.db", m.db, 1.5, -80, 20) / 20));
            s = s.Mix(sub, 0f);

            // ---- 4. Settling: knocks, rattle, the low rumble.
            float[] knockT = Times(p, "settle.knock", new[] { 0.367f, 0.49f, 0.502f });
            s = s.Mix(Bursts(208, length, knockT, Levels(p, "settle.knock", new float[] { -24, -36, -34 }), new[] { p.P("settle.knock.tau", 8, 2, 0.5, 60) },
                             Eq8(p, "settle.knock", -6, -2, 0, 0, 0, -6, -16, -26)), 0f);
            float[] rattleT = { 0.26f, 0.3f, 0.34f, 0.38f, 0.42f, 0.48f, 0.56f };
            Func<float, float> rc = Curve(rattleT, Levels(p, "settle.rattle", new float[] { -90, -32, -36, -38, -44, -52, -90 }));
            Func<float, float> rg = GrainSteps(209, length, 4, 1.6);
            s = s.Mix(ShapedNoise(210, length, Eq8(p, "settle.rattle", -10, -6, -4, -2, 0, -4, -10, -20)).Map((x, t) => x * rc(t) * rg(t)), 0f);
            float[] rumbleT = { 0.02f, 0.06f, 0.12f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.75f, 0.9f, 1.0f };
            Func<float, float> rr = Curve(rumbleT, Levels(p, "rumble", new float[] { -90, -30, -34, -38, -24, -34, -40, -44, -48, -52, -60 }));
            s = s.Mix(ShapedNoise(211, length, EqCapped(p, "rumble", 20, -40, 0, -10, -24, -36, -46, -56, -60, -60)).Map((x, t) => x * rr(t)), 0f);


            // The recording's own noise floor (about 60 dB under the slam): inaudible, but without it the quiet parts of the
            // score are dominated by silence against noise.
            s = s.Mix(ShapedNoise(212, length, Eq8(p, "room.hiss", 0, 0, 0, 0, 0, 0, 0, 0)), 0f, (float)Math.Pow(10, p.P("room.hiss.l0", -70, 2, -100, -50) / 20));
            s = s.Reverb(room: 0.3f, damp: 0.5f, mix: p.P("room.mix", 0.05, 0.01, 0, 0.3), tail: 0.2f).Trim(length);
            if (!final) return s;
            return s.BrickWall(15300f, 511).Fade(0.001f, 0.15f);
        }

        public static Sound ClothModel(Params p, bool final)
        {
            const float length = 1.65f;
            Sound s = Sound.Silence(length);

            // ---- The body of the rustle: low-heavy noise in 10-30 ms swells, its level every 25 ms.
            float[] bodyT = Enumerable.Range(0, 67).Select(i => i * 0.025f).ToArray();
            float[] bodyDefaults =
            {
                -90, -46, -30, -22, -18, -15, -22, -24, -28, -36, -40, -38, -42, -40, -32, -28, -33, -32, -28, -28, -24, -24, -24,
                -22, -24, -22, -20, -22, -26, -26, -24, -25, -28, -28, -20, -26, -28, -30, -36, -28, -30, -28, -30, -30, -34, -36,
                -42, -42, -42, -40, -42, -36, -40, -40, -42, -42, -40, -42, -42, -44, -44, -40, -38, -40, -40, -38, -90,
            };
            Func<float, float> bc = Curve(bodyT, Levels(p, "body", bodyDefaults));
            float bodyGrain = p.P("body.grain", 0.7, 0.1, 0, 1), bodyStep = p.P("body.step", 14, 3, 4, 40);
            Func<float, float> bg = GrainSteps(301, length, bodyStep, 1.6);
            s = s.Mix(ShapedNoise(302, length, EqCapped(p, "body", 20, 20, 0, 0, 0, -4, -16, -26, -34, -42))
                .Map((x, t) => x * bc(t) * (1 - bodyGrain + bodyGrain * bg(t))), 0f);

            // ---- The crisp layer: the fabric's surface (1-8 kHz), grainier, its level every 50 ms.
            float[] crispT = Enumerable.Range(0, 34).Select(i => i * 0.05f).ToArray();
            Func<float, float> kc = Curve(crispT, Levels(p, "crisp", Enumerable.Range(0, 34).Select(i => i == 0 || i == 33 ? -90f : -58f).ToArray()));
            float crispGrain = p.P("crisp.grain", 0.8, 0.1, 0, 1), crispStep = p.P("crisp.step", 5, 1, 1, 20);
            Func<float, float> kg = GrainSteps(303, length, crispStep, 2.2);
            s = s.Mix(ShapedNoise(304, length, Eq8(p, "crisp", -30, -20, -10, -4, 0, 0, -4, -12))
                .Map((x, t) => x * kc(t) * (1 - crispGrain + crispGrain * kg(t))), 0f);

            // ---- Crackles where the fabric folds or snaps.
            float[] crackT = Times(p, "crackle", new[] { 0.167f, 0.185f, 0.226f, 0.46f, 0.514f, 0.559f, 0.587f, 0.592f, 0.668f, 0.678f, 0.803f, 0.838f, 0.854f, 0.901f, 1.094f, 1.216f, 1.467f, 1.618f });
            float[] crackL = Levels(p, "crackle", crackT.Select(_ => -40f).ToArray());
            // Snaps: the fabric pulled taut, a short crack with weight, mostly below 1 kHz (the loudest moment of the
            // recording is the one at 0.668 s).
            float[] snapT = Times(p, "snap", new[] { 0.559f, 0.592f, 0.668f, 0.678f, 0.854f });
            s = s.Mix(Bursts(307, length, snapT, Levels(p, "snap", new float[] { -30, -30, -14, -30, -30 }, 2, -90, 12),
                             snapT.Select((_, i) => p.P($"snap.tau{i}", 3, 1, 0.3, 30)).ToArray(),
                             Eq8(p, "snap", -2, 0, 0, -2, -4, -8, -14, -20)), 0f);
            s = s.Mix(Bursts(305, length, crackT, crackL, new[] { p.P("crackle.tau", 3, 1, 0.3, 30) }, Eq8(p, "crackle", -24, -14, -6, 0, 0, -2, -8, -16)), 0f);


            // The recording's own noise floor (about 60 dB under the slam): inaudible, but without it the quiet parts of the
            // score are dominated by silence against noise.
            s = s.Mix(ShapedNoise(306, length, Eq8(p, "room.hiss", 0, 0, 0, 0, 0, 0, 0, 0)), 0f, (float)Math.Pow(10, p.P("room.hiss.l0", -70, 2, -100, -50) / 20));
            s = s.Reverb(room: 0.25f, damp: 0.5f, mix: p.P("room.mix", 0.03, 0.01, 0, 0.3), tail: 0.15f).Trim(length);
            if (!final) return s;
            return s.BrickWall(15300f, 511);
        }
    }
}

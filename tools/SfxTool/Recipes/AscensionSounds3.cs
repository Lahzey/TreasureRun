using System;

namespace SfxTool
{
    /// <summary>Ascension sounds, phase 3 (docs/ascension-plan.md Â§1.5).</summary>
    public static partial class Recipes
    {
        private static void RegisterPhase3()
        {
            Add("ascensions/saw_dimension/saw_hum", SawHum);
            Add("ascensions/mega_mortar/mega_launch", MegaLaunch);
            Add("ascensions/mega_mortar/mega_blast", MegaBlast);
            Add("ascensions/ley_line_network/ley_flow", LeyFlow);
            Add("ascensions/particle_accelerator/accel_charge", AccelCharge);
            Add("ascensions/particle_accelerator/accel_fire", AccelFire);
            Add("ascensions/particle_accelerator/blackhole_rumble", BlackHoleRumble);
            Add("ascensions/particle_accelerator/blackhole_collapse", BlackHoleCollapse);
            Variants("ascensions/particle_accelerator/accel_click", 6, AccelClick);
            Add("ascensions/particle_accelerator/accel_servo", AccelServo);
            RegisterMorph();
            Add("ascensions/particle_accelerator/accel_latch", AccelLatch);
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
        /// Mega Mortar launch: a deep cannon "whump" (sine sweeping 70 â†’ 28 Hz), a blast of low noise and a short metallic
        /// ring of the huge barrel, 1.2 s. Lower and longer than the vanilla Mortar (85 Hz zero-crossing, âˆ’9.6 dB).
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
        /// Mega Mortar impact: a huge explosion: a sub boom (sine 60 â†’ 22 Hz), a heavy noise body, a crackling debris
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

        /// <summary>
        /// Ley Line Network: a quiet arcane flow, one global loop. A soft pad (A minor triad, sines with slow tremolo), a
        /// shimmer of high partials that drift in and out and a gentle liquid whoosh (band noise sweeping slowly), 4 s,
        /// seamless. Played quietly; the game sets its volume from the network's income.
        /// </summary>
        private static Sound LeyFlow()
        {
            const float length = 4f, overlap = 0.4f, total = length + overlap;
            Func<float, float> trem(float rate, float depth, float phase) => t => 1f - depth + depth * (0.5f + 0.5f * (float)Math.Sin(2 * Math.PI * (rate * t + phase)));
            Sound pad = Sound.Layer(
                Sound.Tone(Wave.Sine, total, 220f, trem(0.5f, 0.3f, 0f)),
                Sound.Tone(Wave.Sine, total, 261.6f, trem(0.5f, 0.3f, 0.33f)).Gain(0.8f),
                Sound.Tone(Wave.Sine, total, 329.6f, trem(0.5f, 0.3f, 0.66f)).Gain(0.7f)).Gain(0.5f);
            Sound shimmer = Sound.Layer(
                Sound.Tone(Wave.Sine, total, 1760f, trem(0.25f, 0.9f, 0f)),
                Sound.Tone(Wave.Sine, total, 2217f, trem(0.25f, 0.9f, 0.5f)),
                Sound.Tone(Wave.Sine, total, 2637f, trem(0.5f, 0.9f, 0.25f))).Gain(0.05f);
            Sound flow = Sound.Noise(NoiseColor.Pink, total, 91).BandPass(900f, 1.5f)
                .Map((x, t) => x * (0.5f + 0.5f * (float)Math.Sin(2 * Math.PI * 0.75f * t))).Gain(0.25f);
            return Seamless(Sound.Layer(pad, shimmer, flow).Reverb(room: 0.7f, mix: 0.25f, tail: 0.5f), length, overlap).Normalize(-12f);
        }
            /// <summary>
        /// Particle Accelerator charging: an electric whine (a saw at 220 Hz and a sine an octave up, low passed) with a
        /// fast flutter of the particles going round the ring (amplitude 14 times a second) and a faint high crackle, 2 s,
        /// seamless. The game raises its pitch with the charge (about 0.7x to 1.6x), so it rises as it fills.
        /// </summary>
        private static Sound AccelCharge()
        {
            const float length = 2f, overlap = 0.25f, total = length + overlap;
            Sound whine = Sound.Layer(Sound.Tone(Wave.Saw, total, 220f), Sound.Tone(Wave.Saw, total, 221.3f)).LowPass(1400f).Gain(0.35f);
            Sound tone = Sound.Tone(Wave.Sine, total, 440f).Gain(0.3f);
            Sound core = Sound.Layer(whine, tone).Map((x, t) => x * (0.7f + 0.3f * (float)Math.Sin(2 * Math.PI * 14f * t)));
            Sound crackle = Sound.Noise(NoiseColor.White, total, 101).HighPass(5000f)
                .Map((x, t) => x * (((int)(t * 90f) * 7919 % 7) == 0 ? 1f : 0.05f)).Gain(0.15f);
            return Seamless(Sound.Layer(core, crackle), length, overlap).Normalize(-10f);
        }

        /// <summary>
        /// Firing the black hole: a deep falling "thoom" (sine 140 â†’ 35 Hz), a downward electric zap (saw 1600 â†’ 120 Hz
        /// through a falling low pass) and a soft air rush, 1.2 s.
        /// </summary>
        private static Sound AccelFire()
        {
            Sound thoom = Sound.Tone(Wave.Sine, 1.1f, Env.Sweep(140f, 35f, 0.9f), Env.Exp(3.5f, 0.004f));
            Sound zap = Sound.Tone(Wave.Saw, 0.6f, Env.Sweep(1600f, 120f, 0.5f), Env.Exp(6f, 0.002f)).LowPass(Env.Sweep(5000f, 400f, 0.5f)).Gain(0.35f);
            Sound rush = Sound.Noise(NoiseColor.Pink, 0.9f, 102).BandPass(Env.Sweep(3000f, 500f, 0.8f), 0.8f).Envelope(Env.Exp(5f, 0.01f)).Gain(0.3f);
            return Sound.Layer(thoom, zap, rush).Drive(1.3f).Reverb(room: 0.6f, mix: 0.2f, tail: 0.4f).Normalize(-2f);
        }

        /// <summary>
        /// The black hole: a deep sub rumble (brown noise under 120 Hz) swelling and ebbing as it swirls (0.67 Hz), a low
        /// sine drone at 41 Hz with a slow beat, a hollow wind circling at the swirl rate, and a deep oscillating tone: saws
        /// at 62 Hz with a sub octave, low passed, pulsing 6.7 times a second ("wub-wub", after the example the user gave),
        /// 3 s, seamless (20 pulses, 2 swirls). In game it starts high and loud and falls to this, and rises again in the
        /// collapse (the loop's pitch and volume).
        /// </summary>
        private static Sound BlackHoleRumble()
        {
            const float length = 3f, overlap = 0.3f, total = length + overlap;
            Func<float, float> swirl(float depth, float phase) => t => 1f - depth + depth * (0.5f + 0.5f * (float)Math.Sin(2 * Math.PI * (0.667f * t + phase)));
            Sound rumble = Sound.Noise(NoiseColor.Brown, total, 103).LowPass(120f).Envelope(swirl(0.4f, 0f)).Gain(1.2f);
            Sound drone = Sound.Layer(Sound.Tone(Wave.Sine, total, 41f), Sound.Tone(Wave.Sine, total, 41.67f)).Gain(0.5f);
            Sound wind = Sound.Noise(NoiseColor.Pink, total, 104).BandPass(400f, 2f).Envelope(swirl(0.8f, 0.5f)).Gain(0.25f);
            Sound osc = Sound.Layer(Sound.Tone(Wave.Saw, total, 62f), Sound.Tone(Wave.Saw, total, 62.67f).Gain(0.8f), Sound.Tone(Wave.Sine, total, 31f).Gain(0.9f))
                .LowPass(360f, 1.2f)
                .Map((x, t) => x * (0.12f + 0.88f * (float)Math.Pow(0.5 + 0.5 * Math.Sin(2 * Math.PI * 6.6667f * t), 2.2)))
                .Gain(0.75f);
            return Seamless(Sound.Layer(rumble, drone, wind, osc).Drive(1.8f), length, overlap).Normalize(-1f);
        }

        /// <summary>
        /// After the burst (the rumble is cut off dead at its loudest): a smoky puff falling in pitch (low passed pink noise
        /// 1.4 kHz → 180 Hz, a sub thump 60 → 28 Hz), then a long, hollow, empty-hall tail: sparse echoes (130 ms) feeding a
        /// big, barely damped reverb, dark (low passed at 1.3 kHz), dying away over about 4 s. Much quieter than the
        /// implosion: the game plays it well under the rumble's peak.
        /// </summary>
        private static Sound BlackHoleCollapse()
        {
            Sound puff = Sound.Noise(NoiseColor.Pink, 1f, 107).LowPass(Env.Sweep(1400f, 180f, 0.6f))
                .Envelope(Env.Points(0f, 0f, 0.012f, 1f, 0.12f, 0.45f, 0.6f, 0.1f, 0.9f, 0f));
            Sound thump = Sound.Tone(Wave.Sine, 0.45f, Env.Sweep(60f, 28f, 0.4f), Env.Exp(7f, 0.003f)).Gain(0.8f);
            Sound dry = Sound.Layer(puff, thump);
            Sound wet = dry.Delay(0.13f, 0.4f, 0.6f, 5).Reverb(room: 0.96f, damp: 0.25f, mix: 1f, tail: 3.6f)
                .LowPass(1300f).HighPass(55f);
            Sound tail = Sound.Silence(0.06f).Then(wet);
            return Sound.Layer(dry.Gain(0.6f), tail.Gain(1.1f)).Trim(4.4f).Fade(0f, 0.6f).Normalize(-6f);
        }

        /// <summary>
        /// One click of the capacitor ⇄ core transformation (a mechanical lock engaging, like a key turning or a weapon
        /// mount switching): a latch transient and the part seating a few ms later (high passed noise, very short), a small
        /// metal ping (1.4–2.4 kHz, varies) and a dull thunk (85–120 Hz). 0.15 s; 6 variants.
        /// </summary>
        private static Sound AccelClick(int seed)
        {
            var r = new Random(seed * 7919);
            float gap = 0.006f + 0.012f * (float)r.NextDouble(), ping = 1400f + 1000f * (float)r.NextDouble(), thunk = 85f + 35f * (float)r.NextDouble();
            Sound tick = Sound.Noise(NoiseColor.White, 0.03f, 200 + seed).HighPass(1000f).Envelope(Env.Exp(160f, 0.0005f));
            Sound seat = Sound.Noise(NoiseColor.White, 0.04f, 300 + seed).BandPass(2100f, 1.2f).Envelope(Env.Exp(110f, 0.0005f)).Gain(0.7f);
            Sound ring = Sound.Layer(Sound.Tone(Wave.Sine, 0.15f, ping, Env.Exp(45f, 0.001f)), Sound.Tone(Wave.Sine, 0.15f, ping * 1.52f, Env.Exp(60f, 0.001f)).Gain(0.5f)).Gain(0.25f);
            Sound low = Sound.Tone(Wave.Sine, 0.12f, thunk, Env.Exp(35f, 0.002f)).Gain(0.6f);
            return Sound.Silence(0.15f).Mix(tick, 0f).Mix(low, 0f).Mix(seat, gap).Mix(ring, gap).Normalize(-6f);
        }

        /// <summary>
        /// A larger part moving (the coil lifting, the plinth and cells rising): a servo whir sweeping up (saw 80 → 180 Hz,
        /// band passed) under dense gear chatter (noise pulsed 70 times a second), like the transformation example. 0.4 s.
        /// </summary>
        private static Sound AccelServo()
        {
            Sound whir = Sound.Tone(Wave.Saw, 0.4f, Env.Sweep(80f, 180f, 0.4f), Env.Points(0f, 0f, 0.04f, 1f, 0.32f, 0.9f, 0.4f, 0f))
                .BandPass(480f, 1f).Gain(0.6f);
            Sound chatter = Sound.Noise(NoiseColor.White, 0.4f, 401).BandPass(1700f, 0.9f)
                .Map((x, t) => x * (float)Math.Pow(0.5 + 0.5 * Math.Sin(2 * Math.PI * 70f * t), 6))
                .Envelope(Env.Points(0f, 0f, 0.05f, 1f, 0.35f, 0.8f, 0.4f, 0f)).Gain(0.35f);
            Sound body = Sound.Noise(NoiseColor.Brown, 0.4f, 402).LowPass(500f).Envelope(Env.Points(0f, 0f, 0.05f, 1f, 0.4f, 0f)).Gain(0.4f);
            return Sound.Layer(whir, chatter, body).Normalize(-8f);
        }

        /// <summary>A moved part locking home: a heavy thump (60 → 38 Hz), a metal clank (620 / 1000 Hz ringing) and a click. 0.35 s.</summary>
        private static Sound AccelClunk()
        {
            Sound thump = Sound.Tone(Wave.Sine, 0.3f, Env.Sweep(60f, 38f, 0.25f), Env.Exp(16f, 0.002f));
            Sound clank = Sound.Layer(Sound.Tone(Wave.Sine, 0.35f, 620f, Env.Exp(14f, 0.001f)), Sound.Tone(Wave.Sine, 0.35f, 1000f, Env.Exp(20f, 0.001f)).Gain(0.7f)).Gain(0.3f);
            Sound hit = Sound.Noise(NoiseColor.White, 0.08f, 403).LowPass(1800f).Envelope(Env.Exp(70f, 0.0005f)).Gain(0.8f);
            return Sound.Layer(thump, clank, hit, AccelClick(9).Gain(0.6f)).Reverb(room: 0.3f, mix: 0.1f, tail: 0.15f).Normalize(-3f);
        }

        /// <summary>The transformation finished: two clunks in quick succession (lock, then latch) and a short spring ring. 0.6 s.</summary>
        private static Sound AccelLatch()
        {
            Sound spring = Sound.Tone(Wave.Triangle, 0.5f, 360f, Env.Exp(9f, 0.002f)).Map((x, t) => x * (0.7f + 0.3f * (float)Math.Sin(2 * Math.PI * 31f * t))).Gain(0.15f);
            return Sound.Silence(0.6f).Mix(AccelClunk().Gain(0.8f), 0f).Mix(AccelClunk(), 0.09f).Mix(spring, 0.1f).Normalize(-2f);
        }
    }
}

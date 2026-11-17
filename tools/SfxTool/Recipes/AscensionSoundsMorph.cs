using System;
using System.Collections.Generic;

namespace SfxTool
{
    /// <summary>
    /// The Particle Accelerator's transformation (capacitor ⇄ core): a hydraulic pump running throughout, locks snapping
    /// into place, keys unlocking, steam hisses, gear chatter and cables feeding. Modelled on three examples: a hydraulic
    /// machine (the start-up of its first version: the whine rising, a jolt that drops it back, rising again; its stop:
    /// the pitch falling into a clunk), a key in a lock (the first second: insertion ticks and the pins rattling as it
    /// turns; the very end: a tick, a snap with a short scrape and the hard clack of the bolt) and a transformer (dense
    /// mechanical chatter). The game plays them per step (VoxTool AcceleratorMorph lists which).
    /// </summary>
    public static partial class Recipes
    {
        private static void RegisterMorph()
        {
            Add("ascensions/particle_accelerator/accel_pump_start", AccelPumpStart);
            Add("ascensions/particle_accelerator/accel_pump_loop", AccelPumpLoop);
            Add("ascensions/particle_accelerator/accel_pump_stop", AccelPumpStop);
            Variants("ascensions/particle_accelerator/accel_hiss", 3, AccelHiss);
            Add("ascensions/particle_accelerator/accel_vent", AccelVent);
            Variants("ascensions/particle_accelerator/accel_lock", 4, AccelLock);
            Variants("ascensions/particle_accelerator/accel_lock_heavy", 2, AccelLockHeavy);
            Variants("ascensions/particle_accelerator/accel_unlock", 3, AccelUnlock);
            Add("ascensions/particle_accelerator/accel_gears", AccelGears);
            Add("ascensions/particle_accelerator/accel_slither", AccelSlither);
        }

        /// <summary>
        /// The pump's speed at full: 135 Hz, its strongest harmonic (the 4th) at 540 Hz. The example sits at 780 Hz; lowered
        /// after the first listen (the transformation was a bit high).
        /// </summary>
        private const float PumpHz = 135f;

        /// <summary>
        /// The pump motor: harmonics of <paramref name="f0"/>, strongest the 4th, then the 6th and 8th (like the example's
        /// lines), a little of the low ones.
        /// </summary>
        private static Sound Motor(float seconds, Func<float, float> f0, Func<float, float> amp)
        {
            float[] a = { 0.22f, 0.18f, 0.3f, 1f, 0.3f, 0.55f, 0.18f, 0.4f, 0.1f, 0.16f, 0.06f, 0.1f };
            var parts = new List<Sound>();
            for (int k = 1; k <= a.Length; k++)
            {
                int kk = k;
                parts.Add(Sound.Tone(Wave.Sine, seconds, t => kk * f0(t), amp).Gain(a[k - 1]));
            }
            return Sound.Layer(parts.ToArray()).Gain(0.35f);
        }

        /// <summary>The pump's body: a broad low rumble (most of the example's energy is under 300 Hz), pulsing with the pistons.</summary>
        private static Sound PumpRumble(float seconds, int seed, Func<float, float> amp, Func<float, float> pulseHz)
        {
            Sound low = Sound.Noise(NoiseColor.Brown, seconds, seed).LowPass(320f).Gain(0.9f);
            Sound mid = Sound.Noise(NoiseColor.Pink, seconds, seed + 1).BandPass(170f, 0.8f).Gain(0.7f);
            Sound flow = Sound.Noise(NoiseColor.White, seconds, seed + 2).HighPass(3500f).Gain(0.04f);
            double ph = 0;
            float last = 0f;
            return Sound.Layer(low, mid, flow).Map((x, t) =>
            {
                ph += pulseHz(t) * (t - last);
                last = t;
                return x * amp(t) * (0.78f + 0.22f * (float)Math.Sin(2 * Math.PI * ph));
            });
        }

        /// <summary>
        /// The pump starting (the example's first version): a relay click, the whine rising from 45% to 85% speed in 0.3 s,
        /// a jolt (a clunk) drops it back to 62%, and it rises again to full by 0.75 s; the rumble grows with it. Fades out
        /// from 0.7 s as the game fades the loop in. 1.1 s.
        /// </summary>
        private static Sound AccelPumpStart()
        {
            const float len = 1.1f;
            Func<float, float> speed = t =>
                t < 0.32f ? 0.45f + 0.4f * (1f - (1f - t / 0.32f) * (1f - t / 0.32f))
                : t < 0.36f ? 0.85f - 0.23f * ((t - 0.32f) / 0.04f)
                : t < 0.75f ? 0.62f + 0.38f * (1f - (1f - (t - 0.36f) / 0.39f) * (1f - (t - 0.36f) / 0.39f))
                : 1f;
            Func<float, float> amp = t => Math.Min(1f, t / 0.03f) * (t < 0.7f ? 1f : Math.Max(0f, 1f - (t - 0.7f) / 0.4f));
            Sound motor = Motor(len, t => PumpHz * speed(t), t => amp(t) * (0.55f + 0.45f * speed(t)));
            Sound rumble = PumpRumble(len, 501, t => amp(t) * speed(t), t => 11f * speed(t));
            Sound jolt = Sound.Layer(
                Sound.Noise(NoiseColor.White, 0.06f, 502).LowPass(1800f).Envelope(Env.Exp(60f, 0.001f)),
                Sound.Tone(Wave.Sine, 0.12f, Env.Sweep(110f, 70f, 0.1f), Env.Exp(30f, 0.002f)).Gain(0.8f)).Gain(0.5f);
            return Sound.Layer(motor, rumble).Mix(AccelClick(3).Gain(0.5f), 0f).Mix(jolt, 0.32f).Normalize(-9f);
        }

        /// <summary>
        /// The pump running: the whine at full speed (a slight wander), the rumble with the pistons pulsing 11 times a
        /// second and a faint flow hiss. 2 s, seamless. The game raises its pitch and volume while a part moves.
        /// </summary>
        private static Sound AccelPumpLoop()
        {
            const float length = 2f, overlap = 0.3f, total = length + overlap;
            Sound motor = Motor(total, t => PumpHz * (1f + 0.004f * (float)Math.Sin(2 * Math.PI * 1.5f * t)), null);
            Sound rumble = PumpRumble(total, 511, _ => 1f, _ => 11f);
            // The example's peak level is about the same for start and loop: normalised like the start.
            return Seamless(Sound.Layer(motor, rumble), length, overlap).Normalize(-10f);
        }

        /// <summary>
        /// The pump stopping: the whine falls to 35% speed over 0.55 s, a valve clunk, then a short pressure release. 1 s.
        /// </summary>
        private static Sound AccelPumpStop()
        {
            const float len = 1f;
            Func<float, float> speed = t => t < 0.55f ? 1f - 0.65f * (t / 0.55f) * (t / 0.55f) : 0.35f;
            Func<float, float> amp = t => t < 0.5f ? 1f - 0.3f * t / 0.5f : Math.Max(0f, 0.7f - (t - 0.5f) / 0.12f);
            Sound motor = Motor(len, t => PumpHz * speed(t), t => amp(t) * (0.5f + 0.5f * speed(t)));
            Sound rumble = PumpRumble(len, 521, t => amp(t) * speed(t), t => 11f * speed(t));
            Sound clunk = Sound.Layer(
                Sound.Noise(NoiseColor.White, 0.1f, 522).LowPass(2200f).Envelope(Env.Exp(45f, 0.001f)),
                Sound.Tone(Wave.Sine, 0.25f, Env.Sweep(90f, 45f, 0.2f), Env.Exp(18f, 0.002f))).Gain(0.7f);
            return Sound.Layer(motor, rumble).Mix(clunk, 0.52f).Mix(AccelHiss(2).Gain(0.35f), 0.58f).Trim(len).Normalize(-8f);
        }

        /// <summary>
        /// A burst of steam or air out of a valve: sudden broadband noise (high passed at 600 Hz, low passed at 6 kHz, the
        /// band around 3.6 kHz strongest and sliding down to 2.4 kHz), dropping fast and fading away. Variants 0.45, 0.6
        /// and 0.75 s.
        /// </summary>
        private static Sound AccelHiss(int seed)
        {
            float len = 0.3f + 0.15f * seed;
            Func<float, float> env = t => Math.Min(1f, t / 0.003f) * (0.55f * (float)Math.Exp(-t * 28f) + 0.45f * (float)Math.Exp(-t * 4.5f / len)) * Math.Max(0f, 1f - t / len);
            Sound air = Sound.Noise(NoiseColor.White, len, 530 + seed).HighPass(600f).Gain(0.5f);
            Sound band = Sound.Noise(NoiseColor.White, len, 540 + seed).BandPass(t => 3600f - 1200f * Math.Min(1f, t / len), 1.4f);
            return Sound.Layer(air, band).Envelope(env).LowPass(6000f).Normalize(-6f);
        }

        /// <summary>The end of the transformation: a long pressure release (1.6 s), fluttering a little as it empties.</summary>
        private static Sound AccelVent()
        {
            const float len = 1.6f;
            Func<float, float> env = t => Math.Min(1f, t / 0.004f) * (0.4f * (float)Math.Exp(-t * 20f) + 0.6f * Math.Max(0f, 1f - t / len) * (1f - 0.12f * (float)Math.Sin(2 * Math.PI * 23f * t)));
            Sound air = Sound.Noise(NoiseColor.White, len, 551).HighPass(500f).Gain(0.5f);
            Sound band = Sound.Noise(NoiseColor.White, len, 552).BandPass(t => 3200f - 1200f * t / len, 1.2f);
            return Sound.Layer(air, band).Envelope(env).LowPass(5500f).Normalize(-7f);
        }

        /// <summary>A short metal transient: a noise click through a band, ringing at two frequencies.</summary>
        private static Sound Strike(int seed, float band, float ring1, float ring2, float decay, float length)
        {
            Sound hit = Sound.Noise(NoiseColor.White, 0.02f, seed).BandPass(band, 0.9f).Envelope(Env.Exp(260f, 0.0003f));
            Sound ring = Sound.Layer(Sound.Tone(Wave.Sine, length, ring1, Env.Exp(decay, 0.0005f)),
                                     Sound.Tone(Wave.Sine, length, ring2, Env.Exp(decay * 1.4f, 0.0005f)).Gain(0.6f)).Gain(0.35f);
            return Sound.Silence(length).Mix(hit, 0f).Mix(ring, 0f);
        }

        /// <summary>Dense little clicks (a scrape, pins rattling, gears): random short bursts in a frequency range.</summary>
        private static Sound Clicks(float seconds, float perSecond, int seed, float lo, float hi, Func<float, float> density = null)
        {
            var r = new Random(seed);
            Sound s = Sound.Silence(seconds + 0.02f);
            int n = (int)(seconds * perSecond);
            for (int i = 0; i < n; i++)
            {
                float t = (float)r.NextDouble() * seconds;
                if (density != null && r.NextDouble() > density(t)) continue;
                float f = lo + (hi - lo) * (float)r.NextDouble();
                Sound c = Sound.Noise(NoiseColor.White, 0.012f, seed * 1000 + i).BandPass(f, 2.5f).Envelope(Env.Exp(500f + 300f * (float)r.NextDouble(), 0.0002f));
                s = s.Mix(c, t, 0.4f + 0.6f * (float)r.NextDouble());
            }
            return s.Trim(seconds);
        }

        /// <summary>
        /// Something snapping into place (the very end of the key example): a faint tick, a snap with a short scrape of
        /// the bolt sliding, then the hard clack (the loudest: a ring around 1–1.9 kHz and a low body thump), a short decay.
        /// 0.4 s; 4 variants.
        /// </summary>
        private static Sound AccelLock(int seed)
        {
            var r = new Random(seed * 131);
            float snapAt = 0.04f + 0.03f * (float)r.NextDouble(), clackAt = snapAt + 0.07f + 0.03f * (float)r.NextDouble();
            float ring = 1050f + 400f * (float)r.NextDouble();
            Sound tick = Strike(600 + seed, 4000f, 2800f, 4100f, 220f, 0.05f).Gain(0.18f);
            Sound snap = Strike(610 + seed, 2000f, ring * 1.3f, ring * 1.9f, 90f, 0.12f).Gain(0.55f);
            Sound scrape = Clicks(clackAt - snapAt, 900f, 620 + seed, 1300f, 3200f).Gain(0.35f);
            Sound clack = Strike(630 + seed, 1600f, ring, ring * 1.62f, 45f, 0.25f);
            Sound crack = Sound.Noise(NoiseColor.White, 0.04f, 640 + seed).LowPass(6000f).HighPass(400f).Envelope(Env.Exp(110f, 0.0003f)).Gain(0.9f);
            Sound body = Sound.Tone(Wave.Sine, 0.15f, Env.Sweep(110f, 80f, 0.12f), Env.Exp(45f, 0.001f)).Gain(0.45f);
            Sound s = Sound.Silence(0.4f).Mix(tick, 0f).Mix(snap, snapAt).Mix(scrape, snapAt + 0.004f).Mix(clack, clackAt, 1.2f).Mix(crack, clackAt).Mix(body, clackAt);
            return s.Reverb(room: 0.25f, damp: 0.5f, mix: 0.08f, tail: 0.12f).Trim(0.4f).Normalize(-1f);
        }

        /// <summary>
        /// A heavy part landing and locking (the base, the cell, the barrel on its column): the same snap-and-clack, lower
        /// (a 420 / 660 Hz clank), with a deep thump (70 → 35 Hz) and a little room. 0.6 s; 2 variants.
        /// </summary>
        private static Sound AccelLockHeavy(int seed)
        {
            float snapAt = 0.03f, clackAt = 0.09f + 0.02f * seed;
            Sound snap = Strike(700 + seed, 1500f, 1000f, 1550f, 70f, 0.12f).Gain(0.5f);
            Sound scrape = Clicks(clackAt - snapAt, 700f, 710 + seed, 800f, 2300f).Gain(0.3f);
            Sound clank = Strike(720 + seed, 950f, 420f + 30f * seed, 660f + 40f * seed, 16f, 0.5f);
            Sound thump = Sound.Tone(Wave.Sine, 0.45f, Env.Sweep(70f, 35f, 0.3f), Env.Exp(10f, 0.002f)).Gain(1.1f);
            Sound hit = Sound.Noise(NoiseColor.White, 0.1f, 730 + seed).LowPass(1700f).Envelope(Env.Exp(55f, 0.0005f)).Gain(0.6f);
            Sound s = Sound.Silence(0.6f).Mix(snap, snapAt).Mix(scrape, snapAt + 0.004f).Mix(clank, clackAt).Mix(thump, clackAt).Mix(hit, clackAt);
            return s.Reverb(room: 0.4f, damp: 0.45f, mix: 0.14f, tail: 0.25f).Trim(0.6f).Normalize(-2f);
        }

        /// <summary>
        /// Unlocking (the first second of the key example): two or three faint ticks (the key going in), then the turn: the
        /// pins rattling (dense clicks around 450 Hz–1.3 kHz over a low thud) for 0.15 s, and a soft release click. 0.55 s;
        /// 3 variants.
        /// </summary>
        private static Sound AccelUnlock(int seed)
        {
            var r = new Random(seed * 977);
            Sound s = Sound.Silence(0.55f);
            int ticks = 2 + seed % 2;
            for (int i = 0; i < ticks; i++)
                s = s.Mix(Strike(800 + seed * 10 + i, 3600f, 2500f + 600f * (float)r.NextDouble(), 3700f, 240f, 0.04f), 0.03f * i + 0.02f * (float)r.NextDouble(), 0.16f);
            float turn = 0.16f + 0.04f * (float)r.NextDouble();
            Sound rattle = Clicks(0.16f, 700f, 820 + seed, 450f, 1300f, t => (float)Math.Sin(Math.PI * t / 0.16f)).Gain(0.7f);
            Sound thud = Sound.Tone(Wave.Sine, 0.2f, 110f, Env.Points(0f, 0f, 0.03f, 1f, 0.12f, 0.6f, 0.2f, 0f)).Gain(0.4f);
            Sound release = Strike(830 + seed, 2100f, 1400f, 2200f, 80f, 0.1f).Gain(0.5f);
            s = s.Mix(rattle, turn).Mix(thud, turn).Mix(release, turn + 0.19f);
            return s.Reverb(room: 0.2f, damp: 0.5f, mix: 0.06f, tail: 0.1f).Trim(0.55f).Normalize(-5f);
        }

        /// <summary>
        /// Gears turning, after the user's two examples: the low earthy rumble of the first version (brown noise under
        /// 400 Hz, which the user liked; its buzzing whir is gone) under metal gear teeth like gears.mp3: the teeth mesh in
        /// bursts (about three a second, 26 teeth a second inside one), each tooth a short metal ping (damped modes from
        /// 380 Hz to 2.2 kHz), now and then a heavier clank (250 Hz–1.1 kHz, longer), all with a short metal echo.
        /// 1.5 s, seamless; played while a part slides or tilts.
        /// </summary>
        private static Sound AccelGears()
        {
            const float length = 1.5f, overlap = 0.25f, total = length + overlap;
            var r = new Random(907);
            Sound teeth = Sound.Silence(total + 0.4f);
            for (float b0 = 0.02f; b0 < total; b0 += 0.3f + 0.12f * (float)r.NextDouble())
            {
                float dur = 0.18f + 0.1f * (float)r.NextDouble();
                for (float t = b0; t < b0 + dur && t < total; t += (0.8f + 0.4f * (float)r.NextDouble()) / 26f)
                {
                    float env = (float)Math.Sin(Math.PI * (t - b0) / dur);
                    teeth = teeth.Mix(GearPing(r, new[] { 380f, 610f, 1020f, 1480f, 2150f }, 35f, 0.12f), t, (0.4f + 0.6f * env) * (0.6f + 0.4f * (float)r.NextDouble()));
                }
            }
            foreach (float at in new[] { 0.55f, 1.25f }) teeth = teeth.Mix(GearPing(r, new[] { 250f, 410f, 690f, 1130f }, 12f, 0.4f), at, 1.2f);
            Sound metal = teeth.Reverb(room: 0.55f, damp: 0.35f, mix: 0.25f, tail: 0.4f).Trim(total).Normalize(-6f);
            Sound rumble = Sound.Noise(NoiseColor.Brown, total, 903).LowPass(400f).Normalize(-9f);
            return Seamless(Sound.Layer(metal, rumble), length, overlap).Normalize(-6f);
        }

        /// <summary>One metal impact: a short noise click and damped modes (each detuned a little, random strength).</summary>
        private static Sound GearPing(Random r, float[] modes, float decay, float length)
        {
            var parts = new List<Sound> { Sound.Noise(NoiseColor.White, 0.015f, r.Next()).LowPass(3000f).Envelope(Env.Exp(300f, 0.0003f)).Gain(0.5f) };
            for (int i = 0; i < modes.Length; i++)
            {
                // Detuned up to 15% per impact, so the repeated teeth don't add up to a steady tone.
                float f = modes[i] * (0.85f + 0.3f * (float)r.NextDouble());
                parts.Add(Sound.Tone(Wave.Sine, length, f, Env.Exp(decay * (1f + 0.6f * (float)r.NextDouble()), 0.0005f))
                    .Gain((0.3f + 0.7f * (float)r.NextDouble()) / (1f + 0.7f * i)));
            }
            return Sound.Layer(parts.ToArray());
        }

        /// <summary>
        /// Cables feeding out: a rubbery slither (pink noise around 280–560 Hz, scraping unevenly 15–25 times a second)
        /// and a faint high friction. 1.2 s, seamless.
        /// </summary>
        private static Sound AccelSlither()
        {
            const float length = 1.2f, overlap = 0.2f, total = length + overlap;
            Sound rub = Sound.Noise(NoiseColor.Pink, total, 911).BandPass(t => 420f + 140f * (float)Math.Sin(2 * Math.PI * 1.7f * t), 1.3f)
                .Map((x, t) => x * (0.5f + 0.5f * (float)Math.Abs(Math.Sin(2 * Math.PI * (15f + 5f * Math.Sin(2 * Math.PI * 0.9 * t)) * t))));
            Sound friction = Sound.Noise(NoiseColor.White, total, 912).HighPass(3000f)
                .LowPass(7000f).Map((x, t) => x * (0.3f + 0.7f * (float)Math.Pow(Math.Abs(Math.Sin(2 * Math.PI * 7f * t)), 3))).Gain(0.025f);
            return Seamless(Sound.Layer(rub, friction), length, overlap).Normalize(-10f);
        }
    }
}

using System;

namespace SfxTool
{
    /// <summary>
    /// The Wizard's Tower's sounds (docs/poi-plan.md §7). Fire and ice reuse the Magma Lobber's lob and impact and the
    /// Icicle Cannon's launch and shatter; these are the spells nothing else sounds like.
    /// </summary>
    public static partial class Recipes
    {
        private static void RegisterWizard()
        {
            Add("pois/wizard_tower/thunder", WizardThunder);
            Add("pois/wizard_tower/tornado", WizardTornado);
            Add("pois/wizard_tower/necro_aura", WizardNecroAura);
            Add("pois/wizard_tower/goldify", WizardGoldify);
        }

        /// <summary>
        /// The lightning strike: a split-second electric sizzle (band-passed noise round 5 kHz), the crack (a hard white
        /// transient with a burst of crackles over 0.12 s), a deep boom under it, then thunder rolling away (brown noise
        /// under 180 Hz, its loudness swelling and sagging irregularly) for 1.6 s. 1.9 s; loudest 50 ms like an explosion's
        /// (about -9 dBFS).
        /// </summary>
        private static Sound WizardThunder()
        {
            const float length = 1.9f;
            var rng = new Random(701);
            Sound sizzle = Sound.Noise(NoiseColor.White, 0.1f, 702).BandPass(5200f, 1.6f).Envelope(Env.Points(0f, 0f, 0.07f, 1f, 0.1f, 0f)).Gain(0.5f);
            Sound crack = Strike(703, 1200f, 160f, 0.05f).Mix(Clatter(rng, 10, 0.012f), 0.004f, 0.8f);
            Sound boom = Sound.Noise(NoiseColor.Brown, 0.7f, 704).LowPass(320f).Envelope(Env.Exp(5.5f, 0.006f))
                .Mix(Sound.Tone(Wave.Sine, 0.6f, Env.Sweep(90f, 48f, 0.4f), Env.Exp(6f, 0.004f)), 0f, 0.7f);
            float[] rolls = { 0.18f, 0.42f, 0.7f, 1.05f };
            Sound roll = Sound.Noise(NoiseColor.Brown, length, 705).LowPass(180f)
                .Envelope(t =>
                {
                    float env = Math.Max(0f, 1f - t / (length - 0.05f));
                    float swell = 0.45f;
                    foreach (float r in rolls) swell += 0.55f * (float)Math.Exp(-Math.Pow((t - r) / 0.09f, 2));
                    return env * env * swell * Math.Min(1f, t / 0.06f);
                });
            return Sound.Silence(length)
                .Mix(sizzle, 0f)
                .Mix(crack, 0.08f, 1.2f)
                .Mix(boom, 0.08f, 1.1f)
                .Mix(roll, 0.12f, 1.6f)
                .Reverb(room: 0.75f, damp: 0.5f, mix: 0.22f, tail: 0.6f).Trim(length).Fade(0f, 0.3f)
                .Normalize(-1f).Drive(1.4f).Normalize(-1f);
        }

        /// <summary>
        /// The tornado: wind whirling round (pink noise through a band whose centre circles 2.2 times a second, rising from
        /// 600 Hz as it spins up), a low roar under it, a faint whistle riding the whirl, and grit rattling now and then.
        /// Spins up over 0.4 s, holds, and dies away over the last 0.8 s. 3.6 s; loudest 50 ms about -13 dBFS.
        /// </summary>
        private static Sound WizardTornado()
        {
            const float length = 3.6f;
            Func<float, float> env = Env.Points(0f, 0f, 0.4f, 1f, length - 0.8f, 0.9f, length, 0f);
            Sound wind = Sound.Noise(NoiseColor.Pink, length, 711)
                .BandPass(t => 600f + 220f * t + 380f * (float)Math.Sin(2 * Math.PI * 2.2f * t), 1.6f)
                .Envelope(t => env(t) * (0.75f + 0.25f * (float)Math.Sin(2 * Math.PI * 2.2f * t + 1.2f)));
            Sound roar = Sound.Noise(NoiseColor.Brown, length, 712).LowPass(260f).Envelope(env).Gain(0.8f);
            Sound whistle = Sound.Tone(Wave.Sine, length, t => 1100f + 160f * (float)Math.Sin(2 * Math.PI * 2.2f * t) + 90f * t, env).Gain(0.04f);
            var rng = new Random(713);
            Sound grit = Sound.Silence(length);
            for (float t = 0.5f; t < length - 0.9f; t += 0.18f + (float)rng.NextDouble() * 0.3f)
                grit = grit.Mix(Clatter(rng, 3, 0.02f, 0.5f).HighPass(2500f), t, 0.25f);
            return Sound.Silence(length)
                .Mix(wind, 0f, 1.4f)
                .Mix(roar, 0f)
                .Mix(whistle, 0f)
                .Mix(grit, 0f)
                .Reverb(room: 0.6f, damp: 0.5f, mix: 0.18f, tail: 0.4f).Trim(length).Fade(0.05f, 0.3f)
                .Normalize(-4.5f);
        }

        /// <summary>
        /// The necromantic aura: a low drone of two notes a semitone apart (A1 and B-flat 1) beating slowly, a ghostly choir
        /// on a diminished chord (A3, C4, E-flat 4, A4) sinking a quarter tone as the aura holds, and whispers (band-passed
        /// noise flickering round 3 kHz). Swells in over 0.6 s, fades over the last second. 4.2 s; loudest 50 ms about -14 dBFS.
        /// </summary>
        private static Sound WizardNecroAura()
        {
            const float length = 4.2f;
            Func<float, float> env = Env.Points(0f, 0f, 0.6f, 1f, length - 1f, 0.8f, length, 0f);
            Sound drone = Sound.Layer(Sound.Tone(Wave.Saw, length, 55f), Sound.Tone(Wave.Saw, length, 58.27f).Gain(0.8f))
                .LowPass(240f).Envelope(env).Gain(0.6f);
            Sound choir = Choir(length, new[] { 220f, 261.63f, 311.13f, 440f }, 721, t => 1f - 0.03f * Math.Min(1f, t / length), 0.012f)
                .LowPass(1400f).Envelope(env).Gain(2.2f);
            Sound whisper = Sound.Noise(NoiseColor.Pink, length, 723).BandPass(3000f, 2f)
                .Envelope(t => env(t) * (0.3f + 0.7f * (float)Math.Abs(Math.Sin(2 * Math.PI * (1.3f + 0.4f * Math.Sin(t)) * t)))).Gain(0.35f);
            return Sound.Silence(length)
                .Mix(drone, 0f)
                .Mix(choir, 0f)
                .Mix(whisper, 0f)
                .Reverb(room: 0.85f, damp: 0.45f, mix: 0.35f, tail: 0.9f).Trim(length).Fade(0f, 0.4f)
                .Normalize(-7f);
        }

        /// <summary>
        /// Goldify: a quick rising run of eight bright bells (C major pentatonic from C6) as the spell takes hold, then the
        /// monster sets into gold: a hard metallic clink (strike plus inharmonic partials) and a glittering tail of small
        /// random pings. 1.5 s; loudest 50 ms about -12 dBFS.
        /// </summary>
        private static Sound WizardGoldify()
        {
            const float length = 1.5f, set = 0.34f;
            float[] run = { 1046.5f, 1174.7f, 1318.5f, 1568f, 1760f, 2093f, 2349.3f, 2637f };
            Sound rise = Sound.Silence(0.6f);
            for (int i = 0; i < run.Length; i++) rise = rise.Mix(Bell(run[i], 0.5f, 1.6f, 9f), 0.04f * i, 0.16f + 0.02f * i);
            Sound clink = Modal(1480f, 1.1f, 3.6f, 0.8f, 0.002f, (1f, 1f), (2.76f, 0.55f), (5.4f, 0.3f), (8.9f, 0.15f))
                .Mix(Strike(731, 2800f, 500f), 0f, 0.9f);
            var rng = new Random(732);
            Sound glitter = Sound.Silence(length);
            for (int i = 0; i < 14; i++)
                glitter = glitter.Mix(Bell(3000f + (float)rng.NextDouble() * 3500f, 0.25f, 1f, 18f), set + 0.05f + (float)rng.NextDouble() * 0.8f,
                                      0.05f + 0.05f * (float)rng.NextDouble());
            return Sound.Silence(length)
                .Mix(rise, 0f)
                .Mix(clink, set, 0.8f)
                .Mix(glitter, 0f)
                .Reverb(room: 0.55f, damp: 0.35f, mix: 0.22f, tail: 0.45f).Trim(length).Fade(0f, 0.25f)
                .Normalize(-3f);
        }
    }
}

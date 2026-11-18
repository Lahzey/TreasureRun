using System;

namespace SfxTool
{
    /// <summary>
    /// Ritual of the Sun (docs/ascension-plan.md §1.5). One chord runs through all of it, D major with an added ninth
    /// (bright, open, "holy"): the links chime its notes, the ring closing swells it as a choir, the strike rings it as
    /// bells, the beam holds it under its roar, and the break lets it sink.
    /// </summary>
    public static partial class Recipes
    {
        private const string Sun = "ascensions/ritual_of_the_sun/";
        private const float D3 = 146.83f, A3 = 220f, D4 = 293.66f, Fs4 = 369.99f, A4 = 440f, E5 = 659.26f,
                            D5 = 587.33f, Fs5 = 739.99f, A5 = 880f, B5 = 987.77f, D6 = 1174.66f;

        private static void RegisterSun()
        {
            Variants(Sun + "sun_link", 3, SunLink);
            Variants(Sun + "sun_unlink", 2, SunUnlink);
            Add(Sun + "ritual_close", RitualClose);
            Add(Sun + "sun_strike", SunStrike);
            Add(Sun + "sun_beam", SunBeam);
            Add(Sun + "ritual_break", RitualBreak);
            Add(Sun + "crystal_power", CrystalPower);
        }

        /// <summary>
        /// The easter egg: the beam powers a mana crystal. The crystal rings: the chord as long glassy bells (A4, D5, F#5,
        /// A5, struck one after another within a tenth of a second), a slow shimmer of two close partials beating, a deep
        /// hum swelling under it and a soft rising breath. 3 s.
        /// </summary>
        private static Sound CrystalPower()
        {
            Sound bells = Sound.Silence(0.1f);
            float[] chord = { A4, D5, Fs5, A5 };
            for (int i = 0; i < chord.Length; i++) bells = bells.Mix(Bell(chord[i], 3f, 0.9f, 1.4f).Gain(0.32f), 0.03f * i);
            Sound shimmer = Sound.Layer(Sound.Tone(Wave.Sine, 3f, 1174.66f), Sound.Tone(Wave.Sine, 3f, 1178.2f))
                .Envelope(Env.Points(0f, 0f, 0.4f, 1f, 3f, 0f)).Gain(0.05f);
            Sound hum = Sound.Layer(Sound.Tone(Wave.Sine, 3f, 73.42f), Sound.Tone(Wave.Sine, 3f, 146.83f).Gain(0.5f))
                .Envelope(Env.Points(0f, 0f, 0.8f, 1f, 3f, 0f)).Gain(0.18f);
            Sound breath = Sound.Noise(NoiseColor.Pink, 1.2f, 360).BandPass(Env.Sweep(500f, 2200f, 1f), 1.2f)
                .Envelope(Env.Points(0f, 0f, 0.6f, 1f, 1.2f, 0f)).Gain(0.12f);
            return Sound.Layer(bells, shimmer, hum, breath).Reverb(room: 0.8f, damp: 0.45f, mix: 0.3f, tail: 0.8f).Trim(3f).Fade(0f, 0.5f).Normalize(-4f);
        }

        /// <summary>
        /// A choir singing "aah": each note three slightly detuned saws with their own vibrato, through the formants of the
        /// vowel (800, 1150 and 2900 Hz) over a little of the raw body, with a breath of noise. <paramref name="bend"/>
        /// multiplies every pitch over time (1 = as written).
        /// </summary>
        private static Sound Choir(float seconds, float[] notes, int seed, Func<float, float> bend = null, float vibrato = 0.006f)
        {
            var rng = new Random(seed);
            bend ??= _ => 1f;
            Sound voices = Sound.Silence(seconds);
            foreach (float note in notes)
                for (int k = 0; k < 3; k++)
                {
                    float f = note * (1f + (k - 1) * 0.0045f + (float)(rng.NextDouble() - 0.5) * 0.002f);
                    float rate = 4.6f + (float)rng.NextDouble() * 1.6f, ph = (float)(rng.NextDouble() * Math.PI * 2);
                    voices = voices.Add(Sound.Tone(Wave.Saw, seconds, t => f * bend(t) * (1f + vibrato * (float)Math.Sin(2 * Math.PI * rate * t + ph)))
                        .Gain(1f / (notes.Length * 3)));
                }
            Sound vowel = Sound.Layer(voices.BandPass(800f, 5f), voices.BandPass(1150f, 6f).Gain(0.7f), voices.BandPass(2900f, 8f).Gain(0.3f),
                                      voices.LowPass(520f).Gain(0.3f));
            Sound breath = Sound.Noise(NoiseColor.Pink, seconds, seed + 7).BandPass(1200f, 1.2f).Gain(0.03f);
            return Sound.Layer(vowel, breath);
        }

        /// <summary>
        /// A beam of light reaching the next prism: a quick rising run of three soft bells from the chord (a different
        /// one per variant), a breath of air sweeping up and a faint shimmer, about 1 s with its reverb.
        /// </summary>
        private static Sound SunLink(int seed)
        {
            float[][] runs = { new[] { D5, Fs5, A5 }, new[] { E5, A5, B5 }, new[] { Fs5, A5, D6 } };
            float[] run = runs[(seed - 1) % runs.Length];
            Sound s = Sound.Silence(0.05f);
            for (int i = 0; i < run.Length; i++) s = s.Mix(Bell(run[i], 0.7f, 1.1f, 6f).Gain(0.5f - 0.08f * i), 0.045f * i);
            Sound air = Sound.Noise(NoiseColor.Pink, 0.35f, 300 + seed).BandPass(Env.Sweep(700f, 3200f, 0.25f), 1.4f)
                .Envelope(Env.Points(0f, 0f, 0.12f, 1f, 0.35f, 0f)).Gain(0.18f);
            Sound shimmer = Sound.Layer(Sound.Tone(Wave.Sine, 0.6f, run[2] * 2f), Sound.Tone(Wave.Sine, 0.6f, run[2] * 2.004f))
                .Envelope(Env.Points(0f, 0f, 0.1f, 1f, 0.6f, 0f)).Gain(0.04f);
            return Sound.Layer(s, air, shimmer).Reverb(room: 0.6f, damp: 0.5f, mix: 0.3f, tail: 0.5f).Normalize(-5f);
        }

        /// <summary>A beam going out: two soft bells falling a fourth and a sigh of air, 0.7 s, quiet.</summary>
        private static Sound SunUnlink(int seed)
        {
            float a = seed == 1 ? A5 : Fs5, b = seed == 1 ? E5 : D5;
            Sound s = Bell(a, 0.5f, 0.9f, 7f).Gain(0.45f).Mix(Bell(b, 0.55f, 0.8f, 6f).Gain(0.35f), 0.09f);
            Sound sigh = Sound.Noise(NoiseColor.Pink, 0.4f, 310 + seed).BandPass(Env.Sweep(2200f, 600f, 0.35f), 1.2f)
                .Envelope(Env.Points(0f, 0f, 0.05f, 1f, 0.4f, 0f)).Gain(0.15f);
            return Sound.Layer(s, sigh).Reverb(room: 0.5f, damp: 0.5f, mix: 0.25f, tail: 0.4f).Normalize(-9f);
        }

        /// <summary>
        /// The ring closes: the pulse runs round it (a rising run of bells over 0.6 s), then a choir swells on the chord
        /// with its light opening up (low pass 900 → 5000 Hz), a shimmer of high partials gliding upward and a rush of air
        /// growing into the strike at 1.12 s (where sun_strike takes over); the choir lingers into the reverb. 3.2 s.
        /// </summary>
        private static Sound RitualClose()
        {
            const float len = 3.2f, peak = 1.12f;
            Sound run = Sound.Silence(0.1f);
            float[] notes = { D5, Fs5, A5, D6 };
            for (int i = 0; i < notes.Length; i++) run = run.Mix(Bell(notes[i], 1.2f, 1.1f, 3.5f).Gain(0.12f + 0.03f * i), 0.15f * i);
            Sound choir = Choir(len, new[] { D3, A3, D4, Fs4, A4, E5 }, 41)
                .LowPass(t => 900f * (float)Math.Pow(5000f / 900f, Math.Min(1.0, t / peak)), 0.8f)
                .Envelope(t => t < peak ? (float)Math.Pow(t / peak, 1.8) : (float)Math.Exp(-(t - peak) * 1.6f))
                .Gain(3.2f);
            Sound shimmer = Sound.Silence(len);
            for (int i = 0; i < 4; i++)
            {
                float f0 = 900f + 260f * i;
                shimmer = shimmer.Add(Sound.Tone(Wave.Sine, len, t => f0 * (float)Math.Pow(2.2, Math.Min(1.0, t / peak)))
                    .Envelope(t => t < peak ? (float)Math.Pow(t / peak, 3) : (float)Math.Exp(-(t - peak) * 3f)).Gain(0.035f));
            }
            Sound rush = Sound.Noise(NoiseColor.Pink, peak + 0.15f, 320).HighPass(Env.Sweep(300f, 2500f, peak))
                .LowPass(6000f).Envelope(t => t < peak ? (float)Math.Pow(t / peak, 3) : Math.Max(0f, 1f - (t - peak) / 0.15f)).Gain(0.22f);
            return Sound.Layer(run, choir, shimmer, rush).Reverb(room: 0.85f, damp: 0.45f, mix: 0.3f, tail: 0.9f).Trim(len).Fade(0f, 0.4f).Normalize(-3f);
        }

        /// <summary>
        /// The sun strikes: a deep boom (sine 90 → 32 Hz) with a sub thump, a fiery blast (pink noise, low pass sweeping
        /// down from 6 kHz), a sharp crack at the front, the chord rung as soft bells that hang in the air, and a long
        /// sizzle of embers; a big reverb. 3.4 s.
        /// </summary>
        private static Sound SunStrike()
        {
            Sound boom = Sound.Tone(Wave.Sine, 2.2f, Env.Sweep(90f, 32f, 1.2f), Env.Exp(2.4f, 0.004f));
            Sound thump = Sound.Tone(Wave.Sine, 0.4f, Env.Sweep(160f, 50f, 0.2f), Env.Exp(12f, 0.001f)).Gain(0.8f);
            Sound blast = Sound.Noise(NoiseColor.Pink, 1.6f, 330).LowPass(Env.Sweep(6000f, 450f, 0.9f)).Envelope(Env.Exp(3.2f, 0.003f)).Gain(0.9f);
            Sound crack = Sound.Noise(NoiseColor.White, 0.12f, 331).HighPass(2000f).LowPass(7000f).Envelope(Env.Exp(35f, 0.0005f)).Gain(0.45f);
            Sound bells = Sound.Silence(0.1f);
            float[] chord = { D4, A4, D5, Fs5 };
            for (int i = 0; i < chord.Length; i++) bells = bells.Mix(Bell(chord[i], 3f, 1.6f, 1.3f).Gain(0.16f), 0.02f * i);
            Sound sizzle = Sound.Noise(NoiseColor.White, 2.8f, 332).BandPass(2600f, 0.8f)
                .Map((x, t) => x * (((int)(t * 140f) * 7919 % 9) == 0 ? 1f : 0.03f))
                .Envelope(Env.Points(0f, 0f, 0.08f, 1f, 2.8f, 0f)).Gain(0.07f);
            Sound rumble = Sound.Noise(NoiseColor.Brown, 3f, 333).LowPass(200f).Envelope(Env.Points(0f, 0f, 0.05f, 1f, 1.4f, 0.4f, 3f, 0f)).Gain(0.6f);
            return Sound.Layer(boom, thump, blast, crack, bells, sizzle, rumble).Drive(1.4f)
                .Reverb(room: 0.85f, damp: 0.4f, mix: 0.25f, tail: 0.8f).Trim(3.4f).Fade(0f, 0.5f).Normalize(-0.5f);
        }

        /// <summary>
        /// The sun's beam, a loop: a furnace roar (brown noise under 160 Hz swelling every 4 s), fire licking (band noise
        /// round 600 Hz flickering), a deep hum on D (73 and 147 Hz), the chord held by the choir under it all with a slow
        /// breath, and sparse sizzling embers. 4 s, seamless. The game plays it on two layers, quieter between waves.
        /// </summary>
        private static Sound SunBeam()
        {
            const float length = 4f, overlap = 0.4f, total = length + overlap;
            Sound roar = Sound.Noise(NoiseColor.Brown, total, 340).LowPass(160f)
                .Map((x, t) => x * (0.75f + 0.25f * (float)Math.Sin(2 * Math.PI * 0.25f * t))).Gain(1.3f);
            Sound fire = Sound.Noise(NoiseColor.Pink, total, 341).BandPass(450f, 0.8f)
                .Map((x, t) => x * (0.55f + 0.15f * (float)Math.Sin(2 * Math.PI * 7.3f * t) + 0.15f * (float)Math.Sin(2 * Math.PI * 11.1f * t + 1f)
                                    + 0.15f * (float)Math.Sin(2 * Math.PI * 17.9f * t + 2f))).Gain(0.32f);
            Sound hum = Sound.Layer(Sound.Tone(Wave.Sine, total, 73.42f), Sound.Tone(Wave.Sine, total, 146.83f).Gain(0.5f),
                                    Sound.Tone(Wave.Sine, total, 147.3f).Gain(0.3f)).Gain(0.45f);
            Sound choir = Choir(total, new[] { D3, A3, D4, Fs4, A4 }, 342, null, 0.005f).LowPass(1900f)
                .Map((x, t) => x * (0.8f + 0.2f * (float)Math.Sin(2 * Math.PI * 0.5f * t))).Gain(0.55f);
            Sound sizzle = Sound.Noise(NoiseColor.White, total, 343).BandPass(3000f, 0.9f)
                .Map((x, t) => x * (((int)(t * 120f) * 7919 % 11) == 0 ? 1f : 0.03f)).Gain(0.05f);
            return Seamless(Sound.Layer(roar, fire, hum, choir, sizzle).Drive(1.4f), length, overlap).Normalize(-8f);
        }

        /// <summary>
        /// The ring breaks: the choir's chord sinks a fifth and fades, a whoosh of air falls (band noise 2.5 kHz → 300 Hz),
        /// a low thump as the column lifts, and the prisms go dark one after another (falling soft bells). 1.8 s.
        /// </summary>
        private static Sound RitualBreak()
        {
            const float len = 1.8f;
            Sound choir = Choir(len, new[] { D3, A3, D4, Fs4, A4 }, 350, t => 1f - 0.33f * (float)Math.Pow(Math.Min(1.0, t / 1.3), 1.5))
                .LowPass(Env.Sweep(4000f, 700f, 1.3f)).Envelope(Env.Points(0f, 0f, 0.04f, 1f, 1.5f, 0f)).Gain(1.2f);
            Sound whoosh = Sound.Noise(NoiseColor.Pink, 1.2f, 351).BandPass(Env.Sweep(2500f, 300f, 1f), 1.1f)
                .Envelope(Env.Points(0f, 0f, 0.08f, 1f, 1.2f, 0f)).Gain(0.4f);
            Sound thump = Sound.Tone(Wave.Sine, 0.6f, Env.Sweep(110f, 45f, 0.4f), Env.Exp(7f, 0.003f)).Gain(0.7f);
            Sound dark = Sound.Silence(0.1f);
            float[] down = { A5, Fs5, D5, A4 };
            for (int i = 0; i < down.Length; i++) dark = dark.Mix(Bell(down[i], 0.6f, 0.8f, 7f).Gain(0.2f), 0.12f + 0.13f * i);
            return Sound.Layer(choir, whoosh, thump, dark).Reverb(room: 0.75f, damp: 0.5f, mix: 0.25f, tail: 0.6f).Trim(len).Fade(0f, 0.3f).Normalize(-3f);
        }
    }
}

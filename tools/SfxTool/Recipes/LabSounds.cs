using System;

namespace SfxTool
{
    /// <summary>
    /// The Laboratory's sounds (docs/poi-plan.md §9): the lab powering up when something is put in its chamber, powering
    /// down when it is sold, and a short "eureka" when a wave of research is done.
    /// </summary>
    public static partial class Recipes
    {
        private static void RegisterLaboratory()
        {
            Add("pois/laboratory/power_up", LabPowerUp);
            Add("pois/laboratory/power_down", LabPowerDown);
            Add("pois/laboratory/research", LabResearch);
        }

        /// <summary>The lab's mains hum: two detuned saws round 100 Hz, darkened, with a 25 Hz flutter.</summary>
        private static Sound LabHum(float length, Func<float, float> pitch, Func<float, float> cutoff, Func<float, float> level)
        {
            Sound a = Sound.Tone(Wave.Saw, length, pitch, level);
            Sound b = Sound.Tone(Wave.Saw, length, t => pitch(t) * 1.013f, level);
            return Sound.Layer(a, b).LowPass(cutoff, 1.1f).Map((x, t) => x * (0.8f + 0.2f * (float)Math.Sin(2 * Math.PI * 25f * t)));
        }

        /// <summary>
        /// Something put in the chamber (1.4 s): a relay clicks, the hum swells in as the filters open, a charging whine
        /// glides up (320 Hz to 1.5 kHz), a crackle as the emitters take, and a glassy ping when the field locks on.
        /// Like the Lightning Trap's switch-on; peak -5 dBFS, loudest 50 ms about -14.6.
        /// </summary>
        private static Sound LabPowerUp()
        {
            const float length = 1.4f;
            Sound hum = LabHum(length, t => 98f, Env.Sweep(260f, 1700f, 0.9f), Env.Points(0f, 0f, 0.08f, 0.5f, 0.9f, 1f, 1.15f, 0.7f, length, 0f));
            Sound whine = Sound.Tone(Wave.Sine, 1.2f, t => 320f * (float)Math.Pow(1500f / 320f, Math.Min(1f, t / 1.0f)) * (1f + 0.006f * (float)Math.Sin(2 * Math.PI * 7f * t)),
                                     Env.Points(0f, 0f, 0.15f, 0.35f, 0.95f, 0.6f, 1.2f, 0f));
            return Sound.Silence(length)
                .Mix(Latch(301, 2800f), 0f, 0.8f)
                .Mix(hum, 0.02f, 0.5f)
                .Mix(whine, 0.05f, 0.22f)
                .Mix(ChainCrackle(7).HighPass(900f), 0.62f, 0.35f)
                .Mix(SmallBell(303, 1760f, 0.6f, 7f, 0.4f), 0.98f, 0.32f)
                .Reverb(room: 0.35f, damp: 0.5f, mix: 0.12f, tail: 0.3f).Trim(length).Fade(0.002f, 0.12f)
                .Normalize(-5f);
        }

        /// <summary>The specimen sold (1 s): the relay, the hum sagging and closing down, the whine falling away. Quieter.</summary>
        private static Sound LabPowerDown()
        {
            const float length = 1.0f;
            Sound hum = LabHum(length, Env.Sweep(98f, 52f, 0.9f), Env.Sweep(1700f, 220f, 0.85f), Env.Points(0f, 1f, 0.5f, 0.6f, length, 0f));
            Sound whine = Sound.Tone(Wave.Sine, 0.8f, t => 1500f * (float)Math.Pow(250f / 1500f, Math.Min(1f, t / 0.8f)), Env.Points(0f, 0.6f, 0.8f, 0f));
            return Sound.Silence(length)
                .Mix(Latch(311, 2600f), 0f, 0.8f)
                .Mix(hum, 0.01f, 0.5f)
                .Mix(whine, 0.02f, 0.18f)
                .Reverb(room: 0.35f, damp: 0.5f, mix: 0.12f, tail: 0.25f).Trim(length).Fade(0.002f, 0.1f)
                .Normalize(-10f);
        }

        /// <summary>
        /// A wave of research done (1.2 s): the tank bubbles, and a little rising run of glassy pings (C, E, G, C an octave
        /// up) like a discovery. Loudest about -9 dBFS: it plays once per wave per lab.
        /// </summary>
        private static Sound LabResearch()
        {
            const float length = 1.2f;
            var rng = new Random(320);
            Sound s = Sound.Silence(length);
            for (int i = 0; i < 6; i++) s = s.Mix(Gloop(rng, 0.5f + 0.3f * (float)rng.NextDouble()), 0.02f + 0.3f * (float)rng.NextDouble(), 1f);
            float[] notes = { 1046.5f, 1318.5f, 1568f, 2093f };
            for (int i = 0; i < notes.Length; i++) s = s.Mix(SmallBell(330 + i, notes[i], 0.75f, 5.5f, 0.45f), 0.06f + 0.11f * i, 0.42f - 0.05f * i);
            return s.Reverb(room: 0.4f, damp: 0.45f, mix: 0.15f, tail: 0.3f).Trim(length).Fade(0.002f, 0.15f)
                .Normalize(-9f);
        }
    }
}

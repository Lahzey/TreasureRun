namespace SfxTool
{
    public static partial class Recipes
    {
        private static void RegisterUi()
        {
            Add("ui/ascension_pick", AscensionPick);
        }

        /// <summary>
        /// Played when an ascension treasure is picked: a quick rising arpeggio of bells (C6 E6 G6 C7) over a soft
        /// swelling shimmer, with a short reverb tail. Brighter and longer than the vanilla card sound, similar loudness.
        /// </summary>
        private static Sound AscensionPick()
        {
            float[] notes = { 1046.5f, 1318.5f, 1568f, 2093f };
            Sound bells = Sound.Silence(0.01f);
            for (int i = 0; i < notes.Length; i++)
                bells = bells.Mix(Bell(notes[i], 0.9f, 2.2f, 4.5f), i * 0.07f, i == notes.Length - 1 ? 0.9f : 0.6f);

            Sound shimmer = Sound.Noise(NoiseColor.White, 0.9f, 7)
                .BandPass(Env.Sweep(2500f, 7000f, 0.6f), 3f)
                .Envelope(Env.Points(0f, 0f, 0.25f, 1f, 0.9f, 0f))
                .Gain(0.25f);

            Sound low = Sound.Tone(Wave.Sine, 0.6f, Env.Sweep(260f, 523f, 0.3f), Env.Exp(6f, 0.01f)).Gain(0.2f);

            return Sound.Layer(bells, shimmer, low)
                .Reverb(room: 0.75f, damp: 0.35f, mix: 0.3f, tail: 0.5f)
                .Fade(0f, 0.15f)
                .Normalize(-5f);
        }
    }
}

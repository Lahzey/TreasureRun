using System;

namespace SfxTool
{
    /// <summary>Point of Interest sounds (docs/poi-plan.md). Loudness is matched to the vanilla clips noted per recipe.</summary>
    public static partial class Recipes
    {
        private static void RegisterPois()
        {
            Variants("pois/lightning_trap/trap_strike", 3, TrapStrike);
            Add("pois/lightning_trap/trap_on", TrapOn);
            Add("pois/lightning_trap/trap_off", TrapOff);
            Add("pois/merchant/shop_open", ShopOpen);
            Add("pois/merchant/register", ShopRegister);
            Add("pois/merchant/shutter", ShopShutter);
        }

        /// <summary>One coin: a short metallic tick (two high partials) with a noise click.</summary>
        private static Sound Coin(Random rng, float pitch = 1f)
        {
            float f = (2400f + (float)rng.NextDouble() * 1800f) * pitch;
            Sound ring = Sound.Tone(Wave.Sine, 0.18f, f, Env.Exp(28f, 0.0005f))
                .Mix(Sound.Tone(Wave.Sine, 0.18f, f * 1.47f, Env.Exp(40f, 0.0005f)), 0f, 0.5f)
                .Mix(Sound.Tone(Wave.Sine, 0.18f, f * 2.31f, Env.Exp(60f, 0.0005f)), 0f, 0.3f);
            Sound click = Sound.Noise(NoiseColor.White, 0.02f, rng.Next()).HighPass(3500f).Envelope(Env.Exp(250f, 0.0003f));
            return ring.Mix(click, 0f, 0.6f);
        }

        /// <summary>
        /// Opening the merchant's shop: a shop-door bell on its spring, its clapper bouncing: five strikes at uneven
        /// intervals (0, ~0.1, ~0.17, ~0.3, ~0.47 s), each weaker and slightly off in pitch as the bell swings, each a hard
        /// strike with a ring that keeps going over the next ones (SmallBell, 2.25 kHz), then a long settling tail in a
        /// small room, 1.9 s. UI loudness like CardLoud (loudest 50 ms ≈ −15 dB).
        /// </summary>
        private static Sound ShopOpen()
        {
            var rng = new Random(23);
            const float length = 1.9f;
            Sound s = Sound.Silence(length);
            float t = 0f, gain = 1f;
            for (int i = 0; i < 5; i++)
            {
                float f = 2250f * (1f + ((float)rng.NextDouble() - 0.5f) * 0.012f);
                s = s.Mix(SmallBell(rng.Next(), f, length - t, 3.0f, 0.7f), t, gain);
                t += 0.07f + (float)rng.NextDouble() * 0.09f + i * 0.02f;
                gain *= 0.62f + 0.12f * (float)rng.NextDouble();
            }
            return s.Reverb(room: 0.5f, damp: 0.35f, mix: 0.18f, tail: 0.45f)
                .Fade(0f, 0.15f).Normalize(-3f).Gain(0.6f);
        }

        /// <summary>
        /// Buying and selling: a cash register, an event model fitted to a recording (Recipes/RegisterModel.cs, values in
        /// Recipes/Fitted/RegisterFit.cs, refit with <c>SfxTool fit register &lt;recording&gt;</c>). Loudest 50 ms ≈ −17 dB, like
        /// the vanilla CoinLong clips.
        /// </summary>
        private static Sound ShopRegister() => RegisterModel(new Params(Fitted.Register), true);

        /// <summary>
        /// The merchant packs up (synced to the 1.1 s shutter animation): two fitted models (Recipes/ShutterModels.cs) mixed.
        /// The awning's cloth rustles as it swings (the first 0.95 s of the cloth model, fading from 0.5 s), and the
        /// door model closes the wagon: its latch touches at ~0.58 s and its slam lands at ~0.83 s, as the awning hits the
        /// frame. Played as a UI sound, soft-limited for loudness.
        /// </summary>
        private static Sound ShopShutter()
        {
            const float length = 1.7f, slamAt = 0.83f, slamInDoor = 0.26f;
            // The cloth gives way to the door: it fades from 0.5 s, so the latch (0.58 s) and the slam stand out.
            Sound cloth = Similarity.Level(ClothModel(new Params(Fitted.Cloth), true)).Trim(0.95f)
                .Map((x, t) => x * (t < 0.5f ? 1f : Math.Max(0f, 1f - (t - 0.5f) / 0.45f)));
            Sound door = Similarity.Level(DoorModel(new Params(Fitted.Door), true));
            return Sound.Silence(length)
                .Mix(cloth, 0f, ClothShare)
                .Mix(door, slamAt - slamInDoor)
                .Fade(0f, 0.12f).Normalize(-1f).Drive(2.5f).Normalize(-1f).Gain(ShutterGain);
        }

        /// <summary>The cloth's loudest 50 ms relative to the door's, and the final gain (loudest 50 ms ≈ −7 dBFS after the soft limiter, Drive, which lifts it by 4 dB at the same peak; played at volume 1.75).</summary>
        private const float ClothShare = 0.42f, ShutterGain = 0.9f;

        /// <summary>
        /// Lightning Trap strike: a sharp crack with a short low thump from the tunnel, 0.3 s. It plays up to four times
        /// a second, so it is quieter than Chain Lightning's crackle (loudest 50 ms ≈ −15 dB; TeslaZap −9.6 dB, centroid 7.5 kHz).
        /// </summary>
        private static Sound TrapStrike(int seed)
        {
            var rng = new Random(seed * 17 + 3);
            const float length = 0.3f;
            Sound crack = Sound.Noise(NoiseColor.White, 0.06f, rng.Next()).HighPass(1100f).Envelope(Env.Exp(70f, 0.0004f));
            Sound sizzle = Sound.Silence(length);
            for (float t = 0.01f; t < 0.2f; t += 0.006f + (float)rng.NextDouble() * 0.012f)
            {
                Sound pop = Sound.Noise(NoiseColor.White, 0.015f, rng.Next()).HighPass(1600f).Envelope(Env.Exp(220f, 0.0003f));
                sizzle = sizzle.Mix(pop, t, 0.25f + (float)rng.NextDouble() * 0.35f);
            }
            Sound thump = Sound.Tone(Wave.Sine, 0.22f, Env.Sweep(110f, 48f, 0.2f), Env.Exp(18f, 0.002f)).Gain(0.7f);
            return Sound.Layer(crack, sizzle, thump)
                .Reverb(room: 0.35f, mix: 0.18f, tail: 0.12f)
                .Fade(0f, 0.04f)
                .Normalize(-1f).Gain(0.75f);
        }

        /// <summary>
        /// Lightning Trap switched on: a lever clunk, an electric hum rising (saw 70 → 260 Hz with tremolo) and a crack
        /// at the top, 0.8 s. Like a button click plus a short charge-up; loudest ≈ −6 dB.
        /// </summary>
        private static Sound TrapOn()
        {
            const float length = 0.8f;
            Sound clunk = Sound.Tone(Wave.Sine, 0.12f, Env.Sweep(180f, 90f, 0.1f), Env.Exp(40f, 0.001f))
                .Mix(Sound.Noise(NoiseColor.Pink, 0.05f, 11).BandPass(900f, 1.5f).Envelope(Env.Exp(90f, 0.001f)), 0f, 0.6f);
            Sound hum = Sound.Tone(Wave.Saw, length, Env.Sweep(70f, 260f, 0.6f), Env.Points(0f, 0f, 0.1f, 0.6f, 0.6f, 1f, 0.8f, 0f))
                .LowPass(Env.Sweep(600f, 3000f, 0.6f), 1.1f);
            hum = hum.Map((x, t) => x * (0.7f + 0.3f * (float)Math.Sin(2 * Math.PI * 30f * t)));
            Sound crack = TrapStrike(5).Gain(1f);
            return Sound.Layer(clunk, hum.Gain(0.55f))
                .Mix(crack, 0.55f, 1f)
                .Normalize(-6f);
        }

        /// <summary>Lightning Trap switched off: the clunk and the hum falling away (260 → 50 Hz), 0.55 s, quieter.</summary>
        private static Sound TrapOff()
        {
            const float length = 0.55f;
            Sound clunk = Sound.Tone(Wave.Sine, 0.12f, Env.Sweep(160f, 80f, 0.1f), Env.Exp(40f, 0.001f))
                .Mix(Sound.Noise(NoiseColor.Pink, 0.05f, 13).BandPass(800f, 1.5f).Envelope(Env.Exp(90f, 0.001f)), 0f, 0.6f);
            Sound hum = Sound.Tone(Wave.Saw, length, Env.Sweep(260f, 50f, length), Env.Points(0f, 1f, 0.45f, 0.4f, 0.55f, 0f))
                .LowPass(Env.Sweep(2500f, 400f, length), 1.1f);
            return Sound.Layer(clunk, hum.Gain(0.5f)).Fade(0f, 0.08f).Normalize(-9f);
        }
    }
}

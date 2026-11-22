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
            Variants("pois/soul_urn/soul_absorb", 3, SoulAbsorb);
            Add("pois/soul_urn/urn_fill", UrnFill);
            Add("pois/soul_urn/rune_ignite", RuneIgnite);
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
            // ------------------------------------------------------------------ Soul Urn (A minor, resolving to A major when it fills)

        private const float UrnA3 = 220f, UrnE4 = 329.63f, UrnA4 = 440f, UrnC5 = 523.25f, UrnCs5 = 554.37f, UrnE5 = 659.26f,
                            UrnA5 = 880f, UrnC6 = 1046.5f, UrnCs6 = 1108.73f, UrnE6 = 1318.51f, UrnA6 = 1760f;

        /// <summary>
        /// A soul flying into the urn (subtle; it plays for every kill nearby): a breath of air rising as the soul swoops in
        /// (band noise 500 Hz → 2.2 kHz over 0.25 s), then at the mouth a soft glassy plip (a short bell on a note of A minor,
        /// one per variant, with its fifth an octave up very quietly) and a faint low "whum". 0.7 s with a small room.
        /// Quiet: loudest 50 ms ≈ −18 dBFS, the breath about 8 dB under the plip; played at a low volume besides.
        /// </summary>
        private static Sound SoulAbsorb(int seed)
        {
            float[] notes = { UrnA5, UrnC6, UrnE6 };
            float note = notes[(seed - 1) % notes.Length];
            const float entry = 0.24f;
            Sound breath = Sound.Noise(NoiseColor.Pink, 0.34f, 410 + seed).BandPass(Env.Sweep(500f, 2200f, entry), 1.4f)
                .Envelope(Env.Points(0f, 0f, entry * 0.8f, 1f, entry + 0.03f, 0.25f, 0.34f, 0f)).Gain(2.2f);
            Sound plip = Bell(note, 0.45f, 1.2f, 9f).Mix(Bell(note * 1.5f * 2f, 0.3f, 0.8f, 14f), 0f, 0.12f);
            Sound whum = Sound.Tone(Wave.Sine, 0.18f, Env.Sweep(260f, 140f, 0.12f), Env.Exp(22f, 0.001f));
            return Sound.Silence(0.7f)
                .Mix(breath, 0f)
                .Mix(plip, entry, 0.5f)
                .Mix(whum, entry, 0.25f)
                .Reverb(room: 0.45f, damp: 0.4f, mix: 0.2f, tail: 0.3f).Trim(0.7f).Fade(0f, 0.12f).Normalize(-10f);
        }

        /// <summary>
        /// The urn is full and bursts. A swell (0–0.9 s): a ghostly choir on A minor (A3, E4, A4, C5, darkened to an "ooh")
        /// rising in pitch by a quarter tone, and a rush of air sweeping up (300 Hz → 3 kHz). At 0.9 s the burst: a deep boom
        /// (70 Hz → 45 Hz with a low noise thump), the urn's glaze cracking (a sparse clatter of bright ticks over 0.25 s), and
        /// the chord turns major: bells A5, C#6, E6, A6 struck one after another, ringing out with the choir on A major.
        /// 2.8 s. Loudness of a reward (loudest 50 ms ≈ −10 dBFS, like an explosion's).
        /// </summary>
        private static Sound UrnFill()
        {
            const float length = 2.8f, burst = 0.9f;
            Sound swell = Choir(burst + 0.15f, new[] { UrnA3, UrnE4, UrnA4, UrnC5 }, 511, t => 1f + 0.03f * Math.Min(1f, t / burst), 0.01f)
                .LowPass(1600f).Envelope(Env.Points(0f, 0f, burst * 0.85f, 1f, burst + 0.15f, 0f));
            Sound rush = Sound.Noise(NoiseColor.Pink, burst + 0.1f, 512).BandPass(Env.Sweep(300f, 3000f, burst), 1.2f)
                .Envelope(Env.Points(0f, 0f, burst * 0.9f, 1f, burst + 0.1f, 0f)).Gain(0.45f);
            Sound boom = Sound.Tone(Wave.Sine, 1.1f, Env.Sweep(70f, 45f, 0.6f), Env.Exp(4.5f, 0.002f))
                .Mix(Sound.Noise(NoiseColor.Brown, 0.4f, 513).LowPass(220f).Envelope(Env.Exp(12f, 0.002f)), 0f, 0.8f);
            var rng = new Random(514);
            Sound crack = Strike(515, 2500f, 300f, 0.03f).Mix(Clatter(rng, 9, 0.028f), 0.01f, 0.6f).HighPass(1500f);
            Sound bells = Sound.Silence(0.2f);
            float[] chord = { UrnA5, UrnCs6, UrnE6, UrnA6 };
            for (int i = 0; i < chord.Length; i++) bells = bells.Mix(Bell(chord[i], 1.8f, 1.6f, 2.2f), 0.05f * i, 0.3f);
            Sound major = Choir(length - burst, new[] { UrnA3, UrnE4, UrnA4, UrnCs5, UrnE5 }, 516, t => 1.03f - 0.03f * Math.Min(1f, t / 0.15f), 0.008f)
                .LowPass(2200f).Envelope(Env.Points(0f, 0f, 0.05f, 1f, 0.5f, 0.6f, length - burst, 0f)).Gain(0.7f);
            return Sound.Silence(length)
                .Mix(swell, 0f, 4f)
                .Mix(rush, 0f, 4f)
                .Mix(boom, burst, 0.75f)
                .Mix(crack, burst, 0.55f)
                .Mix(bells, burst + 0.02f)
                .Mix(major, burst)
                .Reverb(room: 0.7f, damp: 0.45f, mix: 0.25f, tail: 0.6f).Trim(length).Fade(0f, 0.4f)
                .Normalize(-1.5f).Drive(1.6f).Normalize(-2f);
        }

        /// <summary>
        /// The soulfire rune lights up where the urn stood: a soft "whoomp" of a flame catching (low noise swelling and
        /// falling, 150 → 600 Hz), a quick rising sparkle of three bells (E6, A6, C#7) and a hum on A that fades. 1.4 s.
        /// Loudest 50 ms ≈ −14 dBFS.
        /// </summary>
        private static Sound RuneIgnite()
        {
            const float length = 1.4f;
            Sound whoomp = Sound.Noise(NoiseColor.Pink, 0.5f, 520).BandPass(Env.Sweep(150f, 600f, 0.3f), 1.0f)
                .Envelope(Env.Points(0f, 0f, 0.07f, 1f, 0.5f, 0f));
            Sound sparkle = Sound.Silence(0.2f);
            float[] run = { UrnE6, UrnA6, UrnCs6 * 2f };
            for (int i = 0; i < run.Length; i++) sparkle = sparkle.Mix(Bell(run[i], 0.9f, 1.4f, 4.5f), 0.07f * i, 0.28f - 0.05f * i);
            Sound hum = Sound.Layer(Sound.Tone(Wave.Sine, length, UrnA3), Sound.Tone(Wave.Sine, length, UrnA4 * 1.003f).Gain(0.4f))
                .Envelope(Env.Points(0f, 0f, 0.15f, 1f, length, 0f)).Gain(0.25f);
            return Sound.Silence(length)
                .Mix(whoomp, 0f, 0.9f)
                .Mix(sparkle, 0.08f)
                .Mix(hum, 0.03f)
                .Reverb(room: 0.55f, damp: 0.4f, mix: 0.22f, tail: 0.45f).Trim(length).Fade(0f, 0.3f).Normalize(-6f);
        }
    }
}

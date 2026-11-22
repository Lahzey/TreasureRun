# Sound workflow

Sound effects are code, like the voxel models: recipes in `tools/SfxTool/Recipes` render to WAV files in `sounds/`. Claude can't listen, so every sound also gets an analysis image and a text report. The final judgement is always a listening check by you.

## Folders

| Folder | Content | In git? |
|---|---|---|
| `TreasureRun/sounds` | Our sounds (`.wav`) with their analysis (`.png`, `.txt`): `ascensions/<ascension id>/` (one folder per ascension, e.g. `ascensions/particle_accelerator/accel_charge`), `ui/`; `previews/` holds review mixes (not deployed) | yes |
| `RogueTower/SoundReference` | Analysis of every vanilla clip (`reference.md` table, `.png`, `.txt`) | no (game assets) |
| `BepInEx/plugins/TreasureRun/sounds` | What the game loads (only `.wav`; copied on every plugin build, stale ones removed) | – |

## SfxTool

```bash
dotnet build tools/SfxTool -c Release
```

Run from `tools/SfxTool`:

```bash
bin/Release/net8.0/SfxTool.exe build ../../sounds
```
```bash
bin/Release/net8.0/SfxTool.exe build ../../sounds ui/ascension_pick
```
```bash
bin/Release/net8.0/SfxTool.exe analyze path/to/file.wav
```
```bash
bin/Release/net8.0/SfxTool.exe reference ../../../Ripped/ExportedProject/Assets/AudioClip ../../../SoundReference
```
```bash
bin/Release/net8.0/SfxTool.exe play ../../sounds/ui/ascension_pick.wav
```

- **`build`** renders recipes (all, or names starting with a prefix) into `.wav` + `.png` + `.txt`.
- **`analyze`** writes the `.png` for any `.wav`/`.ogg`/`.mp3` and prints the report.
- **`compare <reference> <candidate> [out.png] [segments]`** lines a recipe up against an example recording (see "Matching an example").
- **`inspect <file> [out-prefix] [t0 t1]`** and **`envelope <file> t0 t1 [step ms]`** show a sound's fine structure (transients, impulsiveness, periodicity, resonances, 2 ms envelopes).
- **`levels <file> t0 t1 [step ms]`** prints octave-band levels over time.
- **`fit <model> <reference> [rounds] [prefix]`** fits a model's parameters to a recording; `fit <model> <reference> floor | diff t0 t1 | solo <prefix> t0 t1 | render <out.wav>` diagnose a fit (see "Fitting a model").
- **`morphpreview <modelsDir> <soundsDir> <outDir>`** mixes the Particle Accelerator's transformation the way the game plays it (timing and sounds from `models/ascensions/particle/accelerator_morph.txt`): `accelerator_to_core.wav`, `accelerator_to_capacitor.wav` and `*_slow.wav` (TimeScale 2), for listening to the whole sequence at once:

  ```bash
  bin/Release/net8.0/SfxTool.exe morphpreview ../../models ../../sounds ../../sounds/previews
  ```
- **`reference`** analyzes a folder of clips (the vanilla ones) and writes a summary table.
- **`play`** opens the file in the default player.
- **`list`** lists the recipes.

SfxTool runs as a built program: after editing a recipe, `dotnet build tools/SfxTool -c Release` first. Rendering only writes into `sounds/`; the game gets the new files with the next mod build (see the README), then F6 reloads them.

## Writing a recipe

Recipes are C# in `Recipes/*.cs` (partial class `Recipes`), registered with `Add(name, recipe)` or `Variants(name, count, seed => ...)`. A `Sound` is a mono 44.1 kHz buffer; each operation returns a new one, so recipes read like a signal chain:

```csharp
Sound.Noise(NoiseColor.White, 0.4f, seed)
     .LowPass(Env.Sweep(6000f, 400f, 0.4f))   // cutoff falls over time
     .Envelope(Env.Exp(9f))                   // fast exponential decay
     .Drive(2f)                               // a little saturation
     .Reverb(mix: 0.2f)
     .Normalize(-4f);
```

- Sources: `Tone` (sine/triangle/saw/square/pulse, with frequency as a function of time), `FM`, `Noise` (white/pink/brown), `Silence`.
- Shaping: `Envelope`, `Gain`, `Fade`, `Normalize`, `LowPass`/`HighPass`/`BandPass` (fixed or swept, 12 dB per octave), `BrickWall` (a steep FIR low-pass), `Drive`, `Crush`, `RingMod`.
- Time: `Delay`, `Reverb`, `Pitch` (resample), `Reverse`, `Trim`.
- Combining: `Layer`, `Mix(other, at, gain)`, `Then`.
- Envelopes: `Env.Exp`, `Env.Adsr`, `Env.Points`, `Env.Sweep`.
- Shared parts: `Recipes.Bell` (FM bell); the foley blocks in `Recipes/Foley.cs` (below).

Variants (`name_1`, `name_2`, …) are picked at random by the game when it plays `name`, so repeated sounds (rockets, saws) don't sound identical.

## Building blocks: how everyday sounds are made

Learned from recorded examples (each block in `Recipes/Foley.cs` names its source). Start from these before inventing; the
general rules come first.

### General rules
- **Structure first.** Analyse the example (`SfxTool analyze file.mp3` writes the report and the picture) and write down its *events* with times before choosing any waveform: "a key rattle 0–0.3 s, a bell from 0.2 s, the drawer at 0.45–0.7 s, a click at 0.85 s". Most failed attempts had the right parts in the wrong proportions or order.
- **Mechanical sounds are events, not swells.** What looks like a broadband smear in a 50 ms picture is, at 2 ms, a sequence of impacts. Each is a bright crack of a few milliseconds that briefly rings the metal (modes lasting 5–40 ms), often with a low body thump arriving about 8 ms later, and rattle excited by the impacts. Filtered noise with a smooth crescendo matches the band energies and still sounds like hiss: that was the register's second attempt. Look at `envelope … 2` before modelling.
- **Matching energy is not matching sound.** Two sounds can have the same energy in every band and differ completely in texture: smooth hiss, sparse crackle, or a low rumble. Check the fine structure (`inspect`: transients, crest factor, 2 ms envelopes), not just band levels. A quiet tail is usually a faint low rumble plus a few knocks, never a noise bed across the highs.
- **Grains make texture.** Noise multiplied by a random step envelope (a new level every few milliseconds, levels mostly low, a few high) turns into rattle (2–6 ms grains, bright), rustle (8–30 ms grains, dark) or crackle (rare loud grains). Smooth envelopes on noise sound like wind or steam instead.
- **The strike is the punch.** Anything struck starts with a 2–12 ms burst of high-passed noise (`Strike`). Without it, metal sounds like a synth pad and wood like a thump.
- **Higher modes die faster.** In struck metal and wood, the decay rate rises with frequency (`Modal`'s damping). The tail then settles on the fundamental, the way real objects ring out.
- **Weight is in the low body, hardness in the highs.** A door slam's highs are gone within about 50 ms, while its 40–150 Hz body rumbles for about 0.5 s. To make something heavier, lengthen and raise the low body; to make it harder, sharpen the crack, rather than making everything louder.
- **Each band has its own envelope.** In real events the mids start first and linger, and the highs arrive late and die fast (a key stroke, a drawer, a door). One envelope over all frequencies sounds synthetic, so split a burst into a low-mid and a high layer with separate rise and decay.
- **Check every layer for low leakage.** Band-passed and high-passed noise (12 dB per octave) still leaves energy at 40–200 Hz. Stack two high-passes, or compare against the example's low bands, before adding any thump. The first register attempt failed partly on a 100 Hz thump the recording doesn't have.
- **Don't add what the example lacks.** Check for every part ("coins", "a thump") before including it. Ears expect coins in a register; this recording has none.
- **Rooms:** a short reverb (room 0.3–0.5, mix 0.1–0.2, tail 0.2–0.45) puts dry layers into one space; more washes out the transients.

### Struck metal: bells, chimes, gongs (`Modal`, `SmallBell`, `Strike`)
- One decaying sine per mode. Mode ratios: a small open bell or thick plate 1, 2.32, 4.15, 6.4; a register chime 1, 2.24, 3.5, 5.2 (measured: 2.1, 4.7, 7.4 kHz); a church bell 0.5, 1, 1.2, 1.5, 2 (hum, prime, minor-third tierce, quint, nominal); a bar or tuning fork 1, 2.76, 5.4; a gong is a low fundamental (100–300 Hz) with many dense, close modes and slow decay.
- **Ring ("Nachhall"):** each mode gets a twin detuned by 0.1–0.2%. The two beat slowly (a few Hz), the shimmer of a real bell. Fundamental decay rate 3–5 means about 0.7–1.1 s to −30 dB.
- **A hand or door bell is several strikes:** the clapper bounces at uneven intervals (0, ~0.1, ~0.17, ~0.3, ~0.47 s), each weaker (×0.6–0.75) and slightly off in pitch (±0.6%) as the bell swings. The rings overlap and keep going under later strikes. Evenly spaced, equally loud strikes sound like an electronic doorbell.
- Small bells live at 1.5–3.5 kHz; lower reads as a gong or a big bell.

### Mechanisms: registers, keys, latches (`Rattle`, `Clatter`, `Latch`)
- **Cash register** (a fitted event model, `Recipes/RegisterModel.cs`; measured with `inspect` and `envelope`):
  - **Key stroke, a ratchet:** a sliding noise bed rising over 0.2 s (its lows first, its highs late) under a series of clicks.
    - Small clicks come 12 ms apart, then bigger ones accelerate (28, 30, 18, 14 ms apart), up to the strongest at the peak.
    - Each click is a bright burst of a few milliseconds that briefly rings the mechanism (581 Hz, 2.2, 2.71, 4.52, 5.08, 7.17, 13.2 kHz).
    - This "k-k-k-KAA" is the mechanical part; a smooth swell loses it.
  - **Till bell,** struck at the peak:
    - a cluster of modes around 2.1 kHz (2.02, 2.05, 2.09, 2.11 kHz) that beat against each other;
    - the *loudest* mode at 4.8 kHz (neighbours 4.75 and 4.87 kHz), and 7.73 kHz;
    - short-lived high modes (6.0–15.7 kHz) that make the strike bright.
  - **Drawer:**
    - a low swell as it starts to slide (below 1 kHz);
    - then impacts: at 0.50 s, a small one, the main one (the loudest moment), and a bounce. Each is a very bright crack (4–16 kHz about 10 dB above 1–4 kHz) that rings the drawer's metal (730 Hz, 1.68, 3.88, 4.57, 5.25, 8.31 kHz), with its low body thump arriving about 8 ms later;
    - grainy rattle between and after.
  - **Tail:** soft knocks as the drawer settles, each ringing low modes of the counter (366, 824, 969 Hz), over a faint low rumble. No hiss.
  - **No coins.** Almost nothing below 120 Hz.
- **Individual clicks** are band-passed noise ticks (2–6 kHz, 3–8 ms, irregular spacing); a soft low thunk (110–170 Hz) under a burst of them gives the mechanism body.
- **A latch or catch** is a bright tick plus a short high ring (2.5–3.5 kHz, decay rate 60–80).
- **Coins** are short high rings (2.4–4.2 kHz with modes 1, 1.47, 2.31) plus a click. Many in quick, falling succession make a pour.

### Wood: doors, panels, shutters (fitted `DoorModel`; `Slam` for quick ones)
- **A door closing** (measured from a recording, fitted in `Recipes/ShutterModels.cs`):
  - **The latch touches the frame:** a dull crack (250 Hz – 1 kHz, a few ms), its low thump (31–125 Hz) about 15 ms later, a little bright tick.
  - **The last bit of the swing** (0.2 s): quiet creaks and ticks.
  - **The slam:** a cluster of cracks 10 ms apart, then the door's body.
    - The cracks are broadband, the brightest in the middle of the cluster.
    - They ring the frame's wood briefly (around 950 Hz and 1.4 kHz) and the fittings at 9–13 kHz.
    - The body is a heavy thump below 125 Hz, arriving 20–40 ms after the first crack and holding for about 60 ms. It is the loudest moment, and most of a slam's energy is under 125 Hz.
    - A low resonance (45–90 Hz) gives the thump its pitch.
  - **Settling:** a few knocks and a rattle, and a low rumble (31–125 Hz) dying away over about 0.7 s.
- **Wood rings short.** A wood mode that rings longer than about 0.1 s (decay below about 250 dB/s) becomes a tone and sounds like metal; `Clank(..., minDecay: 250)` prevents it.
- Smaller or lighter panels: raise the knock and body frequencies, shorten the body.

### Cloth: rustle, flap (fitted `ClothModel`; `ClothRustle` for quick ones)
- **Rustle** (fabric handled and shaken, measured from a recording):
  - **The energy is low:** flat from 31 to 500 Hz, falling steeply above 1 kHz (−12 dB at 2 kHz, −25 dB at 8 kHz). It is the mass of the fabric moving air: a soft "whff", not a bright hiss. A rustle band-passed around 1.5 kHz sounds thin and like paper; that was the shutter's first attempt.
  - **It comes in gestures** (0.2–0.6 s each), made of swells of 10–20 ms: noise multiplied by a step envelope that is interpolated, not stepped.
  - **A crisp layer** of grains (1–8 kHz, 2–5 ms) sits about 20 dB under the body: the fabric's surface.
  - **Crackles and snaps** where it folds or pulls taut. Crackles are bright (1–4 kHz); snaps are short cracks with weight, around 500 Hz – 1 kHz and below, the loudest moments of the rustle.
- **A flap** (cloth snapping taut) is a snap followed by a short swell of the body.

## Matching an example (`compare`)

When an example recording is given, iterate until it matches; don't stop at "has the same parts":

```
bin/Release/net8.0/SfxTool.exe compare <example.mp3> ../../sounds/<name>.wav <out.png> [t0 t1 t0 t1 ...]
```

- It trims both sounds to their onset and levels them to the same loudest 50 ms, then prints:
  - energy in 8 bands every 50 ms, reference | candidate;
  - for each given segment (t0–t1), the 1/3-octave spectrum difference (the candidate's colour relative to the reference, both levelled to the segment);
  - the tonal peaks of each sound (frequency, prominence, when, how long it rings);
  - a score (mean absolute band difference).
- The picture stacks both waveforms and fine spectrograms (4096-point FFT, 40 Hz–20 kHz) above a difference map: red where the candidate is louder, blue where it is quieter.
- **Workflow:** one change per finding, a re-run after every change, and a look at the picture every few rounds (some differences, such as missing small events, show better there than in the numbers). Segments: one per event (stroke, gap, drawer, tail).
- **Reading the score:** 13–14 dB means the parts are wrong; about 7 dB, the parts are right but the balance isn't; 3–4 dB is about the floor for noise-based sounds (two takes of the same noise differ by that much).
- **MP3 examples** have nothing above about 16 kHz. Match that with `BrickWall(15300, 511)` (a windowed-sinc FIR); biquad low-passes can't cut that steeply.

## Looking closer (`inspect`, `envelope`)

```
bin/Release/net8.0/SfxTool.exe inspect <file> <out-prefix> [t0 t1]
bin/Release/net8.0/SfxTool.exe envelope <file> t0 t1 [step ms]
```

- `inspect` lists:
  - every **transient** (time, level, how sudden, how fast it decays, brightness);
  - the **impulsiveness** every 25 ms (crest factor: noise about 3–4, clicks and knocks 6–20);
  - the **periodicity** of the high-band envelope (ratchets and buzzes show strengths of 0.4 and more);
  - the **resonances** with their decay rates, of the whole sound and of the window t0–t1 alone.

  Its pictures are a high-zoom waveform of the window and a short-window spectrogram (256-point FFT) where single clicks show as vertical lines.
- `levels <file> t0 t1 [step ms]` prints the octave-band levels (31 Hz – 16 kHz) per step: where the energy of a sound sits and how it moves. The cloth's low body and the door's sub-125 Hz thump showed up here first.
- `envelope` prints the level per step (2 ms is good) in four bands: full, below 1 kHz, 1–4 kHz, 4–16 kHz. Attacks, decays, the order in which bands arrive, and series of clicks can be read straight off it. The register's ratchet showed up here and nowhere else.
- Periods longer than the analysis window hide: the ratchet's 28 ms steps were missed by a periodicity search that only looked up to 25 ms, and found in the 2 ms envelope.

## Fitting a model (`fit`)

When an example has many measured details, write the sound as a **model** and let the computer fit its numbers:

1. **Write the structure by hand,** one part per measured event (`Recipes/RegisterModel.cs`).
   - Each part reads named parameters through `p.P(name, default, step, min, max)`: levels at breakpoints, decay times, onsets, 9-point EQs (`Eq8`, octaves 63 Hz–16 kHz), resonator levels and decays.
   - Start the defaults from the measurements.
2. **Register the model** in `Fitter.Models` (name, duration, segments, render function, generated table), and add an empty `Recipes/Fitted/<Name>Fit.cs`.
3. **Fit:** `bin/Release/net8.0/SfxTool.exe fit register <example.mp3> 10` (about 30–40 s per round for 200 parameters).
   - Every parameter is nudged by its step; improvements are kept, and steps halve when a round finds nothing.
   - The table is rewritten after every round.
4. **Rebuild SfxTool and render.** Check with `compare`, `inspect` and your ears. Then improve the *structure* (missing events, missing resonances, an EQ that can't reach low enough) and fit again; the fitter continues from the table.

- **The score** (`Similarity`) adds up three terms, each the RMS of the dB differences:
  - 1/3-octave levels every 10 ms;
  - 0.5 × the 2 ms envelope in three bands (the sharpness of transients);
  - 0.5 × the fine spectrum (11 Hz bins) averaged per segment. Without this term the fitter can't tell two partials in one 1/3-octave band apart: it swapped the register's 7.73 kHz partial for 7.46 kHz.

  RMS rather than mean: a short, large error at an important moment (a missing click) must outweigh many small texture differences. With a mean, the fitter switched off the register's loudest click and faked it with low bed noise.
- **Diagnosing a fit:**
  - `floor` renders the fitted model twice with different noise and scores the takes against each other. That is the texture floor: as close as two takes of the same event get. It then prints the error per 50 ms, against the recording and against the floor; the *excess* shows where the model is still wrong. The register ended at 8.46 against a floor of 7.12, with the remaining excess spread thinly instead of in one place.
  - `diff t0 t1` prints candidate minus reference per 1/3-octave band every 10 ms. Large positive numbers are energy the recording doesn't have.
  - `render <out.wav>` writes the fitted model alone, for listening, `compare` and `inspect`.
  - `solo <prefix> t0 t1` renders one part alone (all other levels muted) and prints its band levels, to find which part causes an excess.
- **Lessons from fitting the register:**
  - **Let event times move** (`Times`, ±4 ms). A click 1–2 ms off is punished by the 2 ms envelope, and the fitter removes it instead of moving it.
  - **Give each part one job.** A static EQ on a long layer forces compromises: the bed's lows were needed at 0.12 s and wrong at the peak. Low thumps belong to the events that make them (`keys.body`), the room tone to a layer of its own.
  - **Cap what must not happen.** The bed may not be as loud as a click; bright layers may not reach into the bass; the room tone may not rise above −35 dB from 2 kHz up (it hissed at −20).
  - **Constrain with measurements.** Decays measured with `inspect` are allowed to move only within 0.6–1.6×, so neighbouring partials can't swap roles.
  - **A fit at the floor can still be wrong.** The door reached its floor while one wood ring had become a pure 1.43 kHz tone: the score allows a tone at that level, but the ear hears it at once. Always run `compare` on the render as well; its "tonal peaks" line catches rings the recording doesn't have. Then constrain the structure (`minDecay`) and refit.
  - **Model the recording's noise floor** (a constant layer, capped about 60 dB under the loudest moment). Otherwise the quiet parts score silence against noise, and the fitter fills them with something audible.
  - **Several sounds in one effect:** fit each recording with its own model (`fit door`, `fit cloth`), then mix the renders in a recipe (`ShopShutter`), each levelled with `Similarity.Level`. Two fits ran side by side in two processes.
  - **Reset values that drifted** into a wrong role (delete them from the table) before refitting: coordinate descent doesn't climb back out of such local minima.
- **What fitting can't do:** add parts the structure lacks. Each round of improvement came from reading the residual (`compare`'s difference map, segment spectra, `floor`, `diff`, `solo`) and adding the missing part: deep-low EQ points, tail modes, metal rings at the clicks, the release burst at the peak, click bodies, room tone.
- **New synth tools behind it:**
  - `Sound.Eq(points)`: a zero-phase FFT EQ of any shape;
  - `Sound.Resonate(hz, dB/s)`: a two-pole resonator. An impulse rings as a decaying sine, and noise bursts through a bank of them make struck metal;
  - `Sound.Impulse`.

## Reading the analysis

- **Image:** waveform with the loudness envelope (orange) on top; spectrogram below (40 Hz to 16 kHz, log scale, dotted lines at 100 Hz, 1 kHz and 10 kHz; vertical lines every 100 ms, brighter every second). Bright = loud.
- **Report:** duration, peak, RMS, loudest 50 ms, attack time, time until −30 dB, brightness (spectral centroid), and per-100 ms brightness and dominant pitch.
- **Matching the game:** compare with `SoundReference/reference.md`. Most vanilla one-shots sit at a loudest-50 ms level of about −8 to −16 dBFS and an RMS of about −15 to −25 dBFS. Examples: `CardLoud` −15.5 / −20.0, `Explosion1` −9.0 / −16.3, `TeslaZap` −9.6 / −14.3.
- **Louder than normalising allows:** a sound with sharp peaks (a slam) hits 0 dBFS long before it sounds loud. `.Normalize(-1).Drive(2.5).Normalize(-1)` soft-limits it: the quiet parts rise by up to 8 dB, the peaks stay (the shutter: loudest 50 ms −11.0 → −6.8 dBFS, RMS −21.1 → −15.8; lower the quiet layers a little afterwards, the limiter lifts them more than the peak). World sounds fade with the camera's distance (the pool rolls off from 5 units); `SoundLibrary.PlayLoud` with a larger minimum distance (×5.62 = +15 dB for a far camera) keeps a loud event loud, and `PlayUi` plays without position (the same everywhere). A volume above 1 in `PlayUi` plays the clip on several sources started on the same sample, which add up coherently (2 = +6 dB); keep the file's peak below about −2 dBFS, since the sum can clip at full game volume.

## In the game

- `Audio.SoundLibrary.Play(name, position, volume, pitchVariance, maxVoices)` plays a one-shot through the vanilla `SFXManager` pool (so the SFX volume option applies). `maxVoices` caps how many copies play at once.
- `SoundLibrary.PlayUi(name)` for interface sounds.
- `SoundLoop.Start(name, transform, volume)` for loops (beams, hums); destroy it to stop.
- Dev console: `sound <name> [volume]` plays a sound (re-reading the file). F6 also reloads sounds.

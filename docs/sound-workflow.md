# Sound workflow

Sound effects are code, like the voxel models: recipes in `tools/SfxTool/Recipes` render to WAV files in `sounds/`. Claude can't listen, so every sound also gets an analysis image and a text report. The final judgement is always a listening check by you.

## Folders

| Folder | Content | In git? |
|---|---|---|
| `TreasureRun/sounds` | Our sounds (`.wav`) with their analysis (`.png`, `.txt`) | yes |
| `RogueTower/SoundReference` | Analysis of every vanilla clip (`reference.md` table, `.png`, `.txt`) | no (game assets) |
| `BepInEx/plugins/TreasureRun/sounds` | What the game loads (only `.wav`; copied on every plugin build) | – |

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
- **`analyze`** writes the `.png` for any `.wav`/`.ogg` and prints the report.
- **`reference`** analyzes a folder of clips (the vanilla ones) and writes a summary table.
- **`play`** opens the file in the default player.
- **`list`** lists the recipes.

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
- Shaping: `Envelope`, `Gain`, `Fade`, `Normalize`, `LowPass`/`HighPass`/`BandPass` (fixed or swept), `Drive`, `Crush`, `RingMod`.
- Time: `Delay`, `Reverb`, `Pitch` (resample), `Reverse`, `Trim`.
- Combining: `Layer`, `Mix(other, at, gain)`, `Then`.
- Envelopes: `Env.Exp`, `Env.Adsr`, `Env.Points`, `Env.Sweep`.
- Shared parts: `Recipes.Bell` (FM bell).

Variants (`name_1`, `name_2`, …) are picked at random by the game when it plays `name`, so repeated sounds (rockets, saws) don't sound identical.

## Reading the analysis

- **Image:** waveform with the loudness envelope (orange) on top; spectrogram below (40 Hz to 16 kHz, log scale, dotted lines at 100 Hz, 1 kHz and 10 kHz; vertical lines every 100 ms, brighter every second). Bright = loud.
- **Report:** duration, peak, RMS, loudest 50 ms, attack time, time until −30 dB, brightness (spectral centroid), and per-100 ms brightness and dominant pitch.
- **Matching the game:** compare with `SoundReference/reference.md`. Most vanilla one-shots sit at a loudest-50 ms level of about −8 to −16 dBFS and an RMS of about −15 to −25 dBFS. Examples: `CardLoud` −15.5 / −20.0, `Explosion1` −9.0 / −16.3, `TeslaZap` −9.6 / −14.3.

## In the game

- `Audio.SoundLibrary.Play(name, position, volume, pitchVariance, maxVoices)` plays a one-shot through the vanilla `SFXManager` pool (so the SFX volume option applies). `maxVoices` caps how many copies play at once.
- `SoundLibrary.PlayUi(name)` for interface sounds.
- `SoundLoop.Start(name, transform, volume)` for loops (beams, hums); destroy it to stop.
- Dev console: `sound <name> [volume]` plays a sound (re-reading the file). F6 also reloads sounds.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace TreasureRun.Audio
{
    /// <summary>
    /// Mod sound effects (docs/ascension-plan.md §1.5): 16-bit PCM WAVs from <c>BepInEx/plugins/TreasureRun/sounds</c>
    /// (made by tools/SfxTool), loaded on first use. Names are paths without extension ("ui/ascension_pick").
    /// A name with variants on disk (name_1.wav, name_2.wav, ...) plays a random one.
    /// One-shots go through the vanilla SFXManager pool, so the game's SFX volume applies.
    /// </summary>
    public static class SoundLibrary
    {
        private static readonly Dictionary<string, AudioClip[]> cache = new Dictionary<string, AudioClip[]>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, List<float>> playing = new Dictionary<string, List<float>>(StringComparer.OrdinalIgnoreCase);
        private static readonly System.Random random = new System.Random();

        public static string SoundsDirectory =>
            Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location) ?? ".", "sounds");

        /// <summary>The clip (a random variant if there are several); null if missing.</summary>
        public static AudioClip Get(string name)
        {
            AudioClip[] clips = Variants(name);
            return clips.Length == 0 ? null : clips[random.Next(clips.Length)];
        }

        /// <summary>
        /// Plays a one-shot at a world position through the vanilla pool. <paramref name="maxVoices"/> limits how many
        /// copies of this sound play at once (a swarm of rockets should not stack into noise).
        /// </summary>
        public static void Play(string name, Vector3 position, float volume = 1f, float pitchVariance = 0.05f, int maxVoices = 6)
        {
            SFXManager sfx = SFXManager.instance;
            if (sfx == null || sfx.volume <= 0f) return;
            AudioClip clip = Get(name);
            if (clip == null) return;
            if (!TakeVoice(name, clip.length, maxVoices)) return;

            AudioPoolSource source;
            if (sfx.sources.Count < 1) source = UnityEngine.Object.Instantiate(sfx.sourceObject).GetComponent<AudioPoolSource>();
            else
            {
                source = sfx.sources[0];
                sfx.sources.Remove(source);
            }
            source.transform.position = position;
            source.PlayClip(clip, sfx.volume * volume, pitchVariance);
        }

        /// <summary>
        /// Plays a vanilla game sound at a fixed pitch (re-pitched variants: Repeater bursts, Springald pierces).
        /// Goes through the same pool and volume as SFXManager.PlaySound.
        /// </summary>
        public static void PlayVanilla(Sound sound, Vector3 position, float pitch = 1f, float volume = 1f)
        {
            SFXManager sfx = SFXManager.instance;
            if (sfx == null || sfx.volume <= 0f) return;
            AudioClip clip = VanillaClip(sfx, sound);
            if (clip == null) return;
            AudioPoolSource source;
            if (sfx.sources.Count < 1) source = UnityEngine.Object.Instantiate(sfx.sourceObject).GetComponent<AudioPoolSource>();
            else
            {
                source = sfx.sources[0];
                sfx.sources.Remove(source);
            }
            source.transform.position = position;
            source.PlayClip(clip, sfx.volume * volume, 0f);
            source.audioS.pitch = pitch;
        }

        private static AudioClip VanillaClip(SFXManager s, Sound sound)
        {
            AudioClip Pick(AudioClip[] a) => a != null && a.Length > 0 ? a[random.Next(a.Length)] : null;
            switch (sound)
            {
                case Sound.Ballista: return s.ballistaClip;
                case Sound.Mortar: return s.mortarClip;
                case Sound.TeslaZap: return s.teslaClip;
                case Sound.Explosion: return Pick(s.explosions);
                case Sound.BiPlaneGun: return s.biPlaneGunClip;
                case Sound.ParticleCannon: return s.particleCannonClip;
                case Sound.Shredder: return s.shredderClip;
                case Sound.BallistaHit: return Pick(s.ballistaHits);
                case Sound.FrostHit: return s.frostHitClip;
                case Sound.ShredderHit: return s.shredderHitClip;
                case Sound.BulletHit: return Pick(s.bulletHits);
                case Sound.ParticleHit: return s.particleHitClip;
                case Sound.Cannon: return s.cannon;
                default: return null;
            }
        }

        /// <summary>Plays a one-shot without position (UI): at the music manager, like vanilla button clicks.</summary>
        public static void PlayUi(string name, float volume = 1f)
        {
            Vector3 at = MusicManager.instance != null ? MusicManager.instance.transform.position : Vector3.zero;
            Play(name, at, volume, 0f, 2);
        }

        /// <summary>Drops loaded clips; the next use reads the files again.</summary>
        public static void ClearCache()
        {
            foreach (AudioClip[] clips in cache.Values)
                foreach (AudioClip c in clips) if (c != null) UnityEngine.Object.Destroy(c);
            cache.Clear();
        }

        private static bool TakeVoice(string name, float length, int max)
        {
            if (!playing.TryGetValue(name, out List<float> ends)) playing[name] = ends = new List<float>();
            float now = Time.unscaledTime;
            ends.RemoveAll(t => t <= now);
            if (ends.Count >= max) return false;
            ends.Add(now + length);
            return true;
        }

        private static AudioClip[] Variants(string name)
        {
            if (string.IsNullOrEmpty(name)) return Array.Empty<AudioClip>();
            if (cache.TryGetValue(name, out AudioClip[] clips)) return clips;

            string basePath = Path.Combine(SoundsDirectory, name.Replace('/', Path.DirectorySeparatorChar));
            var files = new List<string>();
            if (File.Exists(basePath + ".wav")) files.Add(basePath + ".wav");
            else
            {
                string dir = Path.GetDirectoryName(basePath);
                string stem = Path.GetFileName(basePath) + "_";
                if (Directory.Exists(dir))
                    files.AddRange(Directory.GetFiles(dir, stem + "*.wav")
                        .Where(f => int.TryParse(Path.GetFileNameWithoutExtension(f).Substring(stem.Length), out _)).OrderBy(f => f));
            }

            clips = files.Select(Load).Where(c => c != null).ToArray();
            if (clips.Length == 0) Plugin.Log.LogWarning($"Sound not found: {name} ({basePath}.wav)");
            cache[name] = clips;
            return clips;
        }

        /// <summary>Reads a 16-bit PCM WAV into an AudioClip.</summary>
        private static AudioClip Load(string path)
        {
            try
            {
                using (var r = new BinaryReader(File.OpenRead(path)))
                {
                    if (new string(r.ReadChars(4)) != "RIFF") throw new InvalidDataException("not RIFF");
                    r.ReadInt32();
                    if (new string(r.ReadChars(4)) != "WAVE") throw new InvalidDataException("not WAVE");
                    int channels = 1, rate = 44100, bits = 16;
                    while (r.BaseStream.Position < r.BaseStream.Length)
                    {
                        string id = new string(r.ReadChars(4));
                        int size = r.ReadInt32();
                        if (id == "fmt ")
                        {
                            r.ReadInt16(); channels = r.ReadInt16(); rate = r.ReadInt32(); r.ReadInt32(); r.ReadInt16(); bits = r.ReadInt16();
                            r.BaseStream.Seek(size - 16, SeekOrigin.Current);
                        }
                        else if (id == "data")
                        {
                            if (bits != 16) throw new InvalidDataException("only 16-bit PCM");
                            int count = size / 2;
                            var data = new float[count];
                            for (int i = 0; i < count; i++) data[i] = r.ReadInt16() / 32768f;
                            AudioClip clip = AudioClip.Create("TR_" + Path.GetFileNameWithoutExtension(path), count / channels, channels, rate, false);
                            clip.SetData(data, 0);
                            return clip;
                        }
                        else r.BaseStream.Seek(size + (size & 1), SeekOrigin.Current);
                    }
                    throw new InvalidDataException("no data chunk");
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Failed to load sound {path}: {e.Message}");
                return null;
            }
        }
    }

    /// <summary>
    /// A looping sound attached to an object (beams, saw hum, charging). Volume follows the game's SFX volume.
    /// Destroy the component (or its object) to stop it.
    /// </summary>
    public sealed class SoundLoop : MonoBehaviour
    {
        private AudioSource source;
        public float Volume = 1f;

        public static SoundLoop Start(string name, Transform parent, float volume = 1f)
        {
            AudioClip clip = SoundLibrary.Get(name);
            if (clip == null || parent == null) return null;
            var go = new GameObject("TR_SoundLoop_" + name);
            go.transform.SetParent(parent, false);
            var loop = go.AddComponent<SoundLoop>();
            loop.Volume = volume;
            loop.source = go.AddComponent<AudioSource>();
            AudioSource template = SFXManager.instance != null && SFXManager.instance.sourceObject != null
                ? SFXManager.instance.sourceObject.GetComponentInChildren<AudioSource>() : null;
            if (template != null)
            {
                loop.source.spatialBlend = template.spatialBlend;
                loop.source.rolloffMode = template.rolloffMode;
                loop.source.minDistance = template.minDistance;
                loop.source.maxDistance = template.maxDistance;
                loop.source.outputAudioMixerGroup = template.outputAudioMixerGroup;
            }
            loop.source.clip = clip;
            loop.source.loop = true;
            loop.source.playOnAwake = false;
            loop.Update();
            loop.source.Play();
            return loop;
        }

        private void Update()
        {
            if (source != null) source.volume = (SFXManager.instance != null ? SFXManager.instance.volume : 0.5f) * Volume;
        }
    }
}

using System;
using System.IO;

namespace SfxTool
{
    /// <summary>WAV read/write (16-bit PCM, mono out), OGG read (NVorbis) and MP3 read (NLayer), mixed down to mono at 44.1 kHz.</summary>
    public static class AudioIO
    {
        public static void WriteWav(string path, Sound s)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            using var w = new BinaryWriter(File.Create(path));
            int bytes = s.Length * 2;
            w.Write("RIFF"u8.ToArray()); w.Write(36 + bytes); w.Write("WAVE"u8.ToArray());
            w.Write("fmt "u8.ToArray()); w.Write(16); w.Write((short)1); w.Write((short)1);
            w.Write(Sound.Rate); w.Write(Sound.Rate * 2); w.Write((short)2); w.Write((short)16);
            w.Write("data"u8.ToArray()); w.Write(bytes);
            foreach (float x in s.S) w.Write((short)Math.Round(Math.Clamp(x, -1f, 1f) * 32767));
        }

        public static Sound Read(string path) =>
            path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) ? ReadOgg(path)
            : path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ? ReadMp3(path) : ReadWav(path);

        public static Sound ReadWav(string path)
        {
            using var r = new BinaryReader(File.OpenRead(path));
            if (new string(r.ReadChars(4)) != "RIFF") throw new InvalidDataException("Not a RIFF file");
            r.ReadInt32();
            if (new string(r.ReadChars(4)) != "WAVE") throw new InvalidDataException("Not a WAVE file");
            int channels = 1, rate = Sound.Rate, bits = 16;
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
                    if (bits != 16) throw new InvalidDataException("Only 16-bit PCM is supported");
                    int frames = size / 2 / channels;
                    var mono = new float[frames];
                    for (int i = 0; i < frames; i++)
                    {
                        float sum = 0;
                        for (int c = 0; c < channels; c++) sum += r.ReadInt16() / 32768f;
                        mono[i] = sum / channels;
                    }
                    return Resample(mono, rate);
                }
                else r.BaseStream.Seek(size + (size & 1), SeekOrigin.Current);
            }
            throw new InvalidDataException("No data chunk");
        }

        public static Sound ReadOgg(string path)
        {
            using var v = new NVorbis.VorbisReader(path);
            int channels = v.Channels;
            var all = new float[(int)v.TotalSamples * channels];
            int read = v.ReadSamples(all, 0, all.Length);
            int frames = read / channels;
            var mono = new float[frames];
            for (int i = 0; i < frames; i++)
            {
                float sum = 0;
                for (int c = 0; c < channels; c++) sum += all[i * channels + c];
                mono[i] = sum / channels;
            }
            return Resample(mono, v.SampleRate);
        }

        /// <summary>MP3 (NLayer, a managed decoder), for reference examples.</summary>
        public static Sound ReadMp3(string path)
        {
            using var f = new NLayer.MpegFile(path);
            int channels = f.Channels;
            var buf = new float[8192 * channels];
            var mono = new System.Collections.Generic.List<float>();
            int n;
            while ((n = f.ReadSamples(buf, 0, buf.Length)) > 0)
                for (int i = 0; i + channels <= n; i += channels)
                {
                    float sum = 0;
                    for (int c = 0; c < channels; c++) sum += buf[i + c];
                    mono.Add(sum / channels);
                }
            return Resample(mono.ToArray(), f.SampleRate);
        }

        private static Sound Resample(float[] mono, int rate) =>
            rate == Sound.Rate ? new Sound(mono) : new Sound(mono).Pitch(rate / (float)Sound.Rate);
    }
}

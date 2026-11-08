using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace VoxTool
{
    /// <summary>RGBA8 image with a minimal PNG decoder (8-bit RGB/RGBA/palette/grey) and encoder.</summary>
    public sealed class Image
    {
        public readonly int Width, Height;
        public readonly uint[] Pixels; // RGBA packed like VoxModel.Rgba, row 0 = top

        public Image(int w, int h, uint fill = 0)
        {
            Width = w; Height = h;
            Pixels = new uint[w * h];
            if (fill != 0) Array.Fill(Pixels, fill);
        }

        public uint Get(int x, int y) => Pixels[Math.Clamp(y, 0, Height - 1) * Width + Math.Clamp(x, 0, Width - 1)];
        public void Set(int x, int y, uint c) { if (x >= 0 && y >= 0 && x < Width && y < Height) Pixels[y * Width + x] = c; }

        public static Image LoadPng(string path)
        {
            byte[] data = File.ReadAllBytes(path);
            int pos = 8;
            int w = 0, h = 0, depth = 0, type = 0;
            byte[] plte = null, trns = null;
            var idat = new MemoryStream();
            while (pos < data.Length)
            {
                int len = BigEndian(data, pos);
                string id = Encoding.ASCII.GetString(data, pos + 4, 4);
                int start = pos + 8;
                switch (id)
                {
                    case "IHDR":
                        w = BigEndian(data, start); h = BigEndian(data, start + 4);
                        depth = data[start + 8]; type = data[start + 9];
                        if (data[start + 12] != 0) throw new NotSupportedException("Interlaced PNG");
                        break;
                    case "PLTE": plte = data.AsSpan(start, len).ToArray(); break;
                    case "tRNS": trns = data.AsSpan(start, len).ToArray(); break;
                    case "IDAT": idat.Write(data, start, len); break;
                }
                pos = start + len + 4;
            }
            if (depth != 8) throw new NotSupportedException($"PNG bit depth {depth}");
            int channels = type switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, 6 => 4, _ => throw new NotSupportedException($"PNG colour type {type}") };

            idat.Position = 0;
            byte[] raw;
            using (var z = new ZLibStream(idat, CompressionMode.Decompress))
            using (var outMs = new MemoryStream()) { z.CopyTo(outMs); raw = outMs.ToArray(); }

            int stride = w * channels;
            var cur = new byte[stride];
            var prev = new byte[stride];
            var img = new Image(w, h);
            int rp = 0;
            for (int y = 0; y < h; y++)
            {
                byte filter = raw[rp++];
                for (int i = 0; i < stride; i++)
                {
                    byte x = raw[rp++];
                    int a = i >= channels ? cur[i - channels] : 0;
                    int b = prev[i];
                    int c = i >= channels ? prev[i - channels] : 0;
                    cur[i] = filter switch
                    {
                        0 => x,
                        1 => (byte)(x + a),
                        2 => (byte)(x + b),
                        3 => (byte)(x + (a + b) / 2),
                        4 => (byte)(x + Paeth(a, b, c)),
                        _ => throw new InvalidDataException("Bad PNG filter"),
                    };
                }
                for (int px = 0; px < w; px++)
                {
                    int o = px * channels;
                    uint col = type switch
                    {
                        0 => Rgba(cur[o], cur[o], cur[o], 255),
                        4 => Rgba(cur[o], cur[o], cur[o], cur[o + 1]),
                        2 => Rgba(cur[o], cur[o + 1], cur[o + 2], 255),
                        6 => Rgba(cur[o], cur[o + 1], cur[o + 2], cur[o + 3]),
                        _ => Rgba(plte[cur[o] * 3], plte[cur[o] * 3 + 1], plte[cur[o] * 3 + 2],
                                  trns != null && cur[o] < trns.Length ? trns[cur[o]] : (byte)255),
                    };
                    img.Pixels[y * w + px] = col;
                }
                (prev, cur) = (cur, prev);
            }
            return img;
        }

        public void SavePng(string path)
        {
            var raw = new MemoryStream();
            for (int y = 0; y < Height; y++)
            {
                raw.WriteByte(0);
                for (int x = 0; x < Width; x++)
                {
                    uint c = Pixels[y * Width + x];
                    raw.WriteByte((byte)c); raw.WriteByte((byte)(c >> 8)); raw.WriteByte((byte)(c >> 16)); raw.WriteByte((byte)(c >> 24));
                }
            }
            var comp = new MemoryStream();
            using (var z = new ZLibStream(comp, CompressionLevel.Optimal, leaveOpen: true)) { raw.Position = 0; raw.CopyTo(z); }

            using var fs = File.Create(path);
            fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            var ihdr = new byte[13];
            WriteBE(ihdr, 0, Width); WriteBE(ihdr, 4, Height);
            ihdr[8] = 8; ihdr[9] = 6;
            WriteChunk(fs, "IHDR", ihdr);
            WriteChunk(fs, "IDAT", comp.ToArray());
            WriteChunk(fs, "IEND", Array.Empty<byte>());
        }

        private static uint Rgba(byte r, byte g, byte b, byte a) => (uint)(r | g << 8 | b << 16 | a << 24);

        private static int Paeth(int a, int b, int c)
        {
            int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
            return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
        }

        private static int BigEndian(byte[] d, int o) => d[o] << 24 | d[o + 1] << 16 | d[o + 2] << 8 | d[o + 3];
        private static void WriteBE(byte[] d, int o, int v) { d[o] = (byte)(v >> 24); d[o + 1] = (byte)(v >> 16); d[o + 2] = (byte)(v >> 8); d[o + 3] = (byte)v; }

        private static void WriteChunk(Stream s, string id, byte[] content)
        {
            var len = new byte[4]; WriteBE(len, 0, content.Length);
            s.Write(len);
            byte[] idBytes = Encoding.ASCII.GetBytes(id);
            s.Write(idBytes);
            s.Write(content);
            uint crc = Crc32(idBytes, 0xFFFFFFFFu);
            crc = Crc32(content, crc) ^ 0xFFFFFFFFu;
            var c = new byte[4]; WriteBE(c, 0, (int)crc);
            s.Write(c);
        }

        private static uint[] crcTable;
        private static uint Crc32(byte[] data, uint crc)
        {
            if (crcTable == null)
            {
                crcTable = new uint[256];
                for (uint n = 0; n < 256; n++)
                {
                    uint c = n;
                    for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                    crcTable[n] = c;
                }
            }
            foreach (byte b in data) crc = crcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
            return crc;
        }
    }
}

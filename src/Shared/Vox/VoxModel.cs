using System;
using System.Collections.Generic;

namespace TreasureRun.Vox
{
    /// <summary>
    /// A single voxel model in MagicaVoxel conventions: Z is up, X/Y horizontal, colour index 1..255 into
    /// <see cref="Palette"/> (index 0 = empty). Pure .NET, shared by the plugin and VoxTool.
    ///
    /// Mapping to Unity (left-handed, Y up): unity.x = vox.x, unity.y = vox.z, unity.z = vox.y.
    /// This keeps the model visually un-mirrored in MagicaVoxel.
    /// </summary>
    public sealed class VoxModel
    {
        public string Name = "";
        public int SizeX, SizeY, SizeZ;

        /// <summary>Voxel colour indices [x, y, z]; 0 = empty.</summary>
        public byte[,,] Voxels;

        /// <summary>256 RGBA colours. Entry i is used by colour index i (entry 0 unused).</summary>
        public uint[] Palette = DefaultPalette();

        public VoxModel(int sx, int sy, int sz)
        {
            if (sx < 1 || sy < 1 || sz < 1 || sx > 256 || sy > 256 || sz > 256)
                throw new ArgumentException($"Invalid vox size {sx}x{sy}x{sz} (1..256 per axis).");
            SizeX = sx; SizeY = sy; SizeZ = sz;
            Voxels = new byte[sx, sy, sz];
        }

        public bool InBounds(int x, int y, int z) => x >= 0 && y >= 0 && z >= 0 && x < SizeX && y < SizeY && z < SizeZ;

        public byte Get(int x, int y, int z) => InBounds(x, y, z) ? Voxels[x, y, z] : (byte)0;

        public void Set(int x, int y, int z, byte color)
        {
            if (InBounds(x, y, z)) Voxels[x, y, z] = color;
        }

        public int CountFilled()
        {
            int n = 0;
            foreach (byte b in Voxels) if (b != 0) n++;
            return n;
        }

        public static uint Rgba(byte r, byte g, byte b, byte a = 255) => (uint)(r | g << 8 | b << 16 | a << 24);

        public static void Unpack(uint c, out byte r, out byte g, out byte b, out byte a)
        {
            r = (byte)(c & 0xFF); g = (byte)(c >> 8 & 0xFF); b = (byte)(c >> 16 & 0xFF); a = (byte)(c >> 24 & 0xFF);
        }

        /// <summary>Neutral grey ramp; real models always carry their own palette.</summary>
        public static uint[] DefaultPalette()
        {
            var p = new uint[256];
            for (int i = 0; i < 256; i++) p[i] = Rgba((byte)i, (byte)i, (byte)i);
            return p;
        }
    }

    /// <summary>Builds a palette from arbitrary RGBA colours (max 255 distinct).</summary>
    public sealed class PaletteBuilder
    {
        private readonly Dictionary<uint, byte> index = new Dictionary<uint, byte>();
        public readonly uint[] Palette = VoxModel.DefaultPalette();

        public byte Get(uint rgba)
        {
            if (index.TryGetValue(rgba, out byte i)) return i;
            if (index.Count >= 255) return Nearest(rgba);
            i = (byte)(index.Count + 1);
            index[rgba] = i;
            Palette[i] = rgba;
            return i;
        }

        private byte Nearest(uint rgba)
        {
            VoxModel.Unpack(rgba, out byte r, out byte g, out byte b, out _);
            int best = int.MaxValue; byte bi = 1;
            foreach (var kv in index)
            {
                VoxModel.Unpack(kv.Key, out byte r2, out byte g2, out byte b2, out _);
                int d = (r - r2) * (r - r2) + (g - g2) * (g - g2) + (b - b2) * (b - b2);
                if (d < best) { best = d; bi = kv.Value; }
            }
            return bi;
        }
    }
}

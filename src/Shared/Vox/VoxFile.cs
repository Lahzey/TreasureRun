using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TreasureRun.Vox
{
    /// <summary>
    /// Reader/writer for MagicaVoxel .vox files (version 150/200).
    /// Reads every model in the file (SIZE/XYZI pairs) and names them from the scene graph (nTRN "_name")
    /// when present. Scene transforms are ignored: each model is used in its own local space.
    /// Writes a single model with its palette.
    /// </summary>
    public static class VoxFile
    {
        public static List<VoxModel> Read(string path)
        {
            using (var fs = File.OpenRead(path)) return Read(fs, Path.GetFileNameWithoutExtension(path));
        }

        public static List<VoxModel> Read(Stream stream, string baseName)
        {
            var r = new BinaryReader(stream);
            if (Encoding.ASCII.GetString(r.ReadBytes(4)) != "VOX ") throw new InvalidDataException("Not a .vox file");
            r.ReadInt32(); // version

            var models = new List<VoxModel>();
            uint[] palette = null;
            var shapeNames = new Dictionary<int, string>(); // model id -> name (via nTRN -> nSHP)
            var nodeNames = new Dictionary<int, string>();  // nTRN node id -> name
            var nodeChild = new Dictionary<int, int>();     // nTRN node id -> child node id
            var shapeModel = new Dictionary<int, int>();    // nSHP node id -> model id
            int sx = 0, sy = 0, sz = 0;

            ReadChunkHeader(r, out string mainId, out int mainContent, out int mainChildren);
            if (mainId != "MAIN") throw new InvalidDataException("Missing MAIN chunk");
            r.ReadBytes(mainContent);
            long end = stream.Position + mainChildren;

            while (stream.Position < end)
            {
                ReadChunkHeader(r, out string id, out int content, out int children);
                long next = stream.Position + content + children;
                switch (id)
                {
                    case "SIZE":
                        sx = r.ReadInt32(); sy = r.ReadInt32(); sz = r.ReadInt32();
                        break;
                    case "XYZI":
                    {
                        var m = new VoxModel(sx, sy, sz) { Name = baseName + (models.Count > 0 ? "_" + models.Count : "") };
                        int n = r.ReadInt32();
                        for (int i = 0; i < n; i++)
                        {
                            byte x = r.ReadByte(), y = r.ReadByte(), z = r.ReadByte(), c = r.ReadByte();
                            m.Set(x, y, z, c);
                        }
                        models.Add(m);
                        break;
                    }
                    case "RGBA":
                    {
                        palette = new uint[256];
                        // File entry i (0..254) is colour index i+1.
                        for (int i = 0; i < 256; i++)
                        {
                            uint c = r.ReadUInt32();
                            if (i < 255) palette[i + 1] = c;
                        }
                        break;
                    }
                    case "nTRN":
                    {
                        int nodeId = r.ReadInt32();
                        var attrs = ReadDict(r);
                        int child = r.ReadInt32();
                        nodeChild[nodeId] = child;
                        if (attrs.TryGetValue("_name", out string name)) nodeNames[nodeId] = name;
                        break;
                    }
                    case "nSHP":
                    {
                        int nodeId = r.ReadInt32();
                        ReadDict(r);
                        int count = r.ReadInt32();
                        if (count > 0) shapeModel[nodeId] = r.ReadInt32();
                        break;
                    }
                }
                stream.Position = next;
            }

            if (palette == null) palette = DefaultMagicaPalette();
            foreach (var kv in nodeNames)
                if (nodeChild.TryGetValue(kv.Key, out int child) && shapeModel.TryGetValue(child, out int modelId))
                    shapeNames[modelId] = kv.Value;

            for (int i = 0; i < models.Count; i++)
            {
                models[i].Palette = (uint[])palette.Clone();
                if (shapeNames.TryGetValue(i, out string n) && !string.IsNullOrWhiteSpace(n)) models[i].Name = n;
            }
            return models;
        }

        public static void Write(string path, VoxModel m)
        {
            using (var fs = File.Create(path)) Write(fs, m);
        }

        public static void Write(Stream stream, VoxModel m)
        {
            var size = new MemoryStream();
            var sw = new BinaryWriter(size);
            sw.Write(m.SizeX); sw.Write(m.SizeY); sw.Write(m.SizeZ);

            var xyzi = new MemoryStream();
            var xw = new BinaryWriter(xyzi);
            xw.Write(m.CountFilled());
            for (int z = 0; z < m.SizeZ; z++)
                for (int y = 0; y < m.SizeY; y++)
                    for (int x = 0; x < m.SizeX; x++)
                    {
                        byte c = m.Voxels[x, y, z];
                        if (c == 0) continue;
                        xw.Write((byte)x); xw.Write((byte)y); xw.Write((byte)z); xw.Write(c);
                    }

            var rgba = new MemoryStream();
            var rw = new BinaryWriter(rgba);
            for (int i = 0; i < 256; i++) rw.Write(i < 255 ? m.Palette[i + 1] : 0u);

            byte[] children = Concat(Chunk("SIZE", size.ToArray()), Chunk("XYZI", xyzi.ToArray()), Chunk("RGBA", rgba.ToArray()));

            var w = new BinaryWriter(stream);
            w.Write(Encoding.ASCII.GetBytes("VOX "));
            w.Write(150);
            w.Write(Encoding.ASCII.GetBytes("MAIN"));
            w.Write(0);
            w.Write(children.Length);
            w.Write(children);
        }

        private static byte[] Chunk(string id, byte[] content)
        {
            var ms = new MemoryStream();
            var w = new BinaryWriter(ms);
            w.Write(Encoding.ASCII.GetBytes(id));
            w.Write(content.Length);
            w.Write(0);
            w.Write(content);
            return ms.ToArray();
        }

        private static byte[] Concat(params byte[][] parts)
        {
            var ms = new MemoryStream();
            foreach (byte[] p in parts) ms.Write(p, 0, p.Length);
            return ms.ToArray();
        }

        private static void ReadChunkHeader(BinaryReader r, out string id, out int content, out int children)
        {
            id = Encoding.ASCII.GetString(r.ReadBytes(4));
            content = r.ReadInt32();
            children = r.ReadInt32();
        }

        private static Dictionary<string, string> ReadDict(BinaryReader r)
        {
            var d = new Dictionary<string, string>();
            int n = r.ReadInt32();
            for (int i = 0; i < n; i++) d[ReadString(r)] = ReadString(r);
            return d;
        }

        private static string ReadString(BinaryReader r) => Encoding.UTF8.GetString(r.ReadBytes(r.ReadInt32()));

        /// <summary>Fallback when a file has no RGBA chunk (rare): a simple hue ramp.</summary>
        private static uint[] DefaultMagicaPalette()
        {
            var p = new uint[256];
            for (int i = 1; i < 256; i++)
            {
                float h = (i * 7 % 255) / 255f;
                HsvToRgb(h, 0.6f, 0.9f, out byte rr, out byte gg, out byte bb);
                p[i] = VoxModel.Rgba(rr, gg, bb);
            }
            return p;
        }

        private static void HsvToRgb(float h, float s, float v, out byte r, out byte g, out byte b)
        {
            float c = v * s, x = c * (1 - Math.Abs(h * 6 % 2 - 1)), m = v - c;
            float rf, gf, bf;
            int seg = (int)(h * 6) % 6;
            switch (seg)
            {
                case 0: rf = c; gf = x; bf = 0; break;
                case 1: rf = x; gf = c; bf = 0; break;
                case 2: rf = 0; gf = c; bf = x; break;
                case 3: rf = 0; gf = x; bf = c; break;
                case 4: rf = x; gf = 0; bf = c; break;
                default: rf = c; gf = 0; bf = x; break;
            }
            r = (byte)((rf + m) * 255); g = (byte)((gf + m) * 255); b = (byte)((bf + m) * 255);
        }
    }

    /// <summary>Placement info written next to a .vox file (same name, .json). Optional for new models.</summary>
    public sealed class VoxSidecar
    {
        /// <summary>World units per voxel (vanilla buildings: 0.1).</summary>
        public float VoxelSize = 0.1f;
        /// <summary>Unity-space position (mesh-local) of the voxel grid's minimum corner. Null = centre X/Z, bottom at 0.</summary>
        public float[] Origin;
        /// <summary>Vanilla source of an extracted model: "Prefab/Child/Path" (informational, used for overrides).</summary>
        public string Source;
    }
}

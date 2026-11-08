using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace VoxTool
{
    public struct Vec3
    {
        public float X, Y, Z;
        public Vec3(float x, float y, float z) { X = x; Y = y; Z = z; }
        public float this[int i] => i == 0 ? X : i == 1 ? Y : Z;
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 Cross(Vec3 a, Vec3 b) => new Vec3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    }

    /// <summary>Vertex/index data of one Unity mesh asset (AssetRipper YAML export).</summary>
    public sealed class MeshData
    {
        public string Name;
        public Vec3[] Positions;
        public Vec3[] Normals;   // may be null
        public float[][] UVs;    // uv0, may be null
        public int[] Triangles;
    }

    /// <summary>One renderer inside a prefab or scene.</summary>
    public sealed class RendererRef
    {
        public string Path;        // "Root/Child/Grandchild"
        public string MeshGuid;
        public long MeshFileId;
        public string MaterialGuid;
    }

    /// <summary>Minimal reader for an AssetRipper "ExportedProject/Assets" folder.</summary>
    public sealed class UnityAssets
    {
        public readonly string Root;
        private readonly Dictionary<string, string> guidToPath = new Dictionary<string, string>();

        public UnityAssets(string assetsDir)
        {
            Root = assetsDir;
            foreach (string meta in Directory.EnumerateFiles(assetsDir, "*.meta", SearchOption.AllDirectories))
            {
                string line = File.ReadLines(meta).FirstOrDefault(l => l.StartsWith("guid: "));
                if (line != null) guidToPath[line.Substring(6).Trim()] = meta.Substring(0, meta.Length - 5);
            }
        }

        public string PathOf(string guid) => guid != null && guidToPath.TryGetValue(guid, out string p) ? p : null;

        // ---------------- YAML documents ----------------

        public sealed class Doc
        {
            public int ClassId;
            public long FileId;
            public string Body;
        }

        public static List<Doc> ReadDocs(string path)
        {
            string text = File.ReadAllText(path);
            var docs = new List<Doc>();
            foreach (Match m in Regex.Matches(text, @"--- !u!(\d+) &(-?\d+)[^\n]*\n(.*?)(?=\n--- !u!|\z)", RegexOptions.Singleline))
                docs.Add(new Doc { ClassId = int.Parse(m.Groups[1].Value), FileId = long.Parse(m.Groups[2].Value), Body = m.Groups[3].Value });
            return docs;
        }

        private static string Field(string body, string name)
        {
            var m = Regex.Match(body, @"\n\s*" + Regex.Escape(name) + @":[ ]?(.*)");
            return m.Success ? m.Groups[1].Value.Trim() : null;
        }

        private static (long fileId, string guid) Ref(string value)
        {
            if (value == null) return (0, null);
            var m = Regex.Match(value, @"fileID: (-?\d+)(?:, guid: (\w+))?");
            return m.Success ? (long.Parse(m.Groups[1].Value), m.Groups[2].Success ? m.Groups[2].Value : null) : (0, null);
        }

        // ---------------- Prefabs / scenes ----------------

        /// <summary>All mesh renderers (MeshFilter + MeshRenderer, or SkinnedMeshRenderer) with their hierarchy path.</summary>
        public List<RendererRef> ReadRenderers(string prefabOrScenePath)
        {
            List<Doc> docs = ReadDocs(prefabOrScenePath);
            var byId = docs.ToDictionary(d => d.FileId);
            var goName = new Dictionary<long, string>();
            var goTransform = new Dictionary<long, long>();
            var transformGo = new Dictionary<long, long>();
            var transformFather = new Dictionary<long, long>();

            foreach (Doc d in docs)
            {
                if (d.ClassId == 1) goName[d.FileId] = Field(d.Body, "m_Name") ?? "";
                if (d.ClassId == 4 || d.ClassId == 224)
                {
                    long go = Ref(Field(d.Body, "m_GameObject")).fileId;
                    transformGo[d.FileId] = go;
                    goTransform[go] = d.FileId;
                    transformFather[d.FileId] = Ref(Field(d.Body, "m_Father")).fileId;
                }
            }

            string PathOfGo(long go)
            {
                var parts = new List<string>();
                long t = goTransform.TryGetValue(go, out long tt) ? tt : 0;
                int guard = 0;
                while (t != 0 && guard++ < 64)
                {
                    parts.Add(transformGo.TryGetValue(t, out long g) && goName.TryGetValue(g, out string n) ? n : "?");
                    t = transformFather.TryGetValue(t, out long f) ? f : 0;
                }
                parts.Reverse();
                return string.Join("/", parts);
            }

            var meshByGo = new Dictionary<long, (long, string)>();
            var matByGo = new Dictionary<long, string>();
            foreach (Doc d in docs)
            {
                long go = Ref(Field(d.Body, "m_GameObject")).fileId;
                if (d.ClassId == 33 || d.ClassId == 137) meshByGo[go] = Ref(Field(d.Body, "m_Mesh"));
                if (d.ClassId == 23 || d.ClassId == 137)
                {
                    var m = Regex.Match(d.Body, @"m_Materials:\s*\n\s*- (\{[^}]*\})");
                    if (m.Success) matByGo[go] = Ref(m.Groups[1].Value).guid;
                }
            }

            var result = new List<RendererRef>();
            foreach (var kv in meshByGo)
            {
                if (kv.Value.Item2 == null) continue;
                result.Add(new RendererRef
                {
                    Path = PathOfGo(kv.Key),
                    MeshFileId = kv.Value.Item1,
                    MeshGuid = kv.Value.Item2,
                    MaterialGuid = matByGo.TryGetValue(kv.Key, out string mat) ? mat : null,
                });
            }
            return result;
        }

        // ---------------- Materials ----------------

        /// <summary>Path of the material's _MainTex texture file, or null.</summary>
        public string MainTexturePath(string materialGuid)
        {
            string matPath = PathOf(materialGuid);
            if (matPath == null || !File.Exists(matPath)) return null;
            string text = File.ReadAllText(matPath);
            var m = Regex.Match(text, @"(?:- )?_MainTex:\s*\n\s*m_Texture: (\{[^}]*\})");
            return m.Success ? PathOf(Ref(m.Groups[1].Value).guid) : null;
        }

        // ---------------- Meshes ----------------

        public static MeshData ReadMesh(string meshAssetPath)
        {
            Doc doc = ReadDocs(meshAssetPath).First(d => d.ClassId == 43);
            string body = doc.Body;

            int indexFormat = int.Parse(Field(body, "m_IndexFormat") ?? "0");
            byte[] indexBytes = Hex(Field(body, "m_IndexBuffer"));
            int vertexCount = int.Parse(Regex.Match(body, @"m_VertexData:\s*\n\s*serializedVersion: \d+\s*\n\s*m_VertexCount: (\d+)").Groups[1].Value);

            var channels = new List<(int stream, int offset, int format, int dim)>();
            foreach (Match m in Regex.Matches(body, @"- stream: (\d+)\s*\n\s*offset: (\d+)\s*\n\s*format: (\d+)\s*\n\s*dimension: (\d+)"))
                channels.Add((int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value), int.Parse(m.Groups[4].Value) & 0xF));
            byte[] data = Hex(Field(body, "_typelessdata"));

            // Stream layout: streams are stored one after another, each 16-byte aligned; vertices interleaved per stream.
            int streams = channels.Count == 0 ? 0 : channels.Max(c => c.stream) + 1;
            var strideOf = new int[streams];
            foreach (var c in channels)
                if (c.dim > 0) strideOf[c.stream] = Math.Max(strideOf[c.stream], c.offset + c.dim * FormatSize(c.format));
            var streamStart = new int[streams];
            int pos = 0;
            for (int s = 0; s < streams; s++)
            {
                streamStart[s] = pos;
                pos += strideOf[s] * vertexCount;
                pos = (pos + 15) & ~15;
            }

            float Read(int channel, int vertex, int component)
            {
                var c = channels[channel];
                int o = streamStart[c.stream] + vertex * strideOf[c.stream] + c.offset + component * FormatSize(c.format);
                return c.format switch
                {
                    0 => BitConverter.ToSingle(data, o),
                    1 => (float)BitConverter.ToHalf(data, o),
                    2 => data[o] / 255f,
                    3 => Math.Max((sbyte)data[o] / 127f, -1f),
                    4 => BitConverter.ToUInt16(data, o) / 65535f,
                    5 => Math.Max(BitConverter.ToInt16(data, o) / 32767f, -1f),
                    _ => throw new NotSupportedException($"Vertex format {c.format}"),
                };
            }

            var mesh = new MeshData { Name = Field(body, "m_Name"), Positions = new Vec3[vertexCount] };
            for (int v = 0; v < vertexCount; v++) mesh.Positions[v] = new Vec3(Read(0, v, 0), Read(0, v, 1), Read(0, v, 2));
            if (channels.Count > 1 && channels[1].dim >= 3)
            {
                mesh.Normals = new Vec3[vertexCount];
                for (int v = 0; v < vertexCount; v++) mesh.Normals[v] = new Vec3(Read(1, v, 0), Read(1, v, 1), Read(1, v, 2));
            }
            if (channels.Count > 4 && channels[4].dim >= 2)
            {
                mesh.UVs = new float[vertexCount][];
                for (int v = 0; v < vertexCount; v++) mesh.UVs[v] = new[] { Read(4, v, 0), Read(4, v, 1) };
            }

            int isz = indexFormat == 0 ? 2 : 4;
            mesh.Triangles = new int[indexBytes.Length / isz];
            for (int i = 0; i < mesh.Triangles.Length; i++)
                mesh.Triangles[i] = isz == 2 ? BitConverter.ToUInt16(indexBytes, i * 2) : (int)BitConverter.ToUInt32(indexBytes, i * 4);
            return mesh;
        }

        private static int FormatSize(int format) => format switch
        {
            0 => 4, 1 => 2, 2 => 1, 3 => 1, 4 => 2, 5 => 2, 6 => 1, 7 => 1, 8 => 2, 9 => 2, 10 => 4, 11 => 4, _ => 4,
        };

        private static byte[] Hex(string s)
        {
            if (string.IsNullOrEmpty(s)) return Array.Empty<byte>();
            var b = new byte[s.Length / 2];
            for (int i = 0; i < b.Length; i++) b[i] = byte.Parse(s.AsSpan(i * 2, 2), NumberStyles.HexNumber);
            return b;
        }
    }
}

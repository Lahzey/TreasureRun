using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TreasureRun.Audio;
using TreasureRun.Vox;
using TreasureRun.Voxels;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TreasureRun.Ascensions.Phase3
{
    /// <summary>
    /// The capacitor → core transformation as computed by VoxTool (<c>AcceleratorMorph</c>, written to
    /// <c>models/ascensions/particle/accelerator_morph.txt</c>): pieces (voxel sets in the tower's frame, turret facing
    /// forward), their poses at the start (translation, a tilt about the x axis, a length for the rams, visibility) and
    /// the steps: pose tweens, voxel changes (waves: a voxel appears, vanishes or changes colour at its time) and the
    /// sounds. Played backwards for core → capacitor.
    /// </summary>
    internal sealed class MorphData
    {
        public struct Pose
        {
            public Vector3 T; // voxels, grid axes (x, y forward, z up)
            public float Rot, Len;
            public bool Vis;

            public static Pose Lerp(Pose a, Pose b, float t, float raw) => new Pose
            {
                T = Vector3.LerpUnclamped(a.T, b.T, t), Rot = Mathf.LerpUnclamped(a.Rot, b.Rot, t), Len = Mathf.LerpUnclamped(a.Len, b.Len, t),
                Vis = raw <= 0f ? a.Vis : raw >= 1f ? b.Vis : a.Vis || b.Vis,
            };
        }

        public sealed class Piece
        {
            public string Name;
            public Vector3 Origin; // grid units (voxel edges): tilt pivot and length anchor
            public readonly Dictionary<Vector3Int, byte> Start = new Dictionary<Vector3Int, byte>();
            public Dictionary<Vector3Int, byte> End;
            public Pose StartPose;
        }

        public sealed class Tween
        {
            public int Piece;
            public float A, B;
            public string Ease;
            public Pose From, To;
        }

        public struct Change
        {
            public Vector3Int P;
            public byte Before, After;
            public float T0, T1;
            /// <summary>0 pops (at the middle of its time); 1..6 grows towards +x, -x, +y, -y, +z, -z.</summary>
            public int Dir;
        }

        public sealed class Step
        {
            public string Name;
            public float Seconds, Load;
            public readonly List<Tween> Tweens = new List<Tween>();
            public readonly List<(int piece, Change[] changes)> Waves = new List<(int, Change[])>();
            public readonly List<(bool forward, float at, string name, float vol)> Sounds = new List<(bool, float, string, float)>();
            public readonly List<(string name, float a, float b, float vol)> Loops = new List<(string, float, float, float)>();
        }

        public int SX, SY, SZ;
        public Vector3Int Origin;
        public float TimeScale = 1f;
        public readonly uint[] Palette = new uint[256];
        public readonly List<Piece> Pieces = new List<Piece>();
        public readonly List<Step> Steps = new List<Step>();
        public float Seconds;

        private static MorphData cached;
        private static bool tried;

        /// <summary>Reads the file again on the next use (F6 in debug builds, after rebuilding the transformation).</summary>
        public static void ClearCache()
        {
            tried = false;
            cached = null;
            MorphPlayer.ClearCache();
        }

        public static MorphData Load()
        {
            if (tried) return cached;
            tried = true;
            string path = Path.Combine(VoxelLibrary.ModelsDirectory, "ascensions", "particle", "accelerator_morph.txt");
            try
            {
                cached = Parse(File.ReadAllLines(path));
                Plugin.Log.LogInfo($"Accelerator morph: {cached.Steps.Count} steps, {cached.Pieces.Count} pieces, {cached.Seconds:0.00} s.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Accelerator morph not loaded ({path}): {e.Message}. The role changes without the transformation.");
                cached = null;
            }
            return cached;
        }

        private static MorphData Parse(string[] lines)
        {
            var d = new MorphData();
            var byName = new Dictionary<string, int>();
            Piece piece = null;
            Step step = null;
            Change[] wave = null;
            int pending = 0, filled = 0;
            foreach (string raw in lines)
            {
                string line = raw.Contains("#") ? raw.Substring(0, raw.IndexOf('#')).Trim() : raw.Trim();
                if (line.Length == 0) continue;
                string[] t = line.Split(' ');
                int I(int i) => int.Parse(t[i], CultureInfo.InvariantCulture);
                float F(int i) => float.Parse(t[i], CultureInfo.InvariantCulture);
                Pose P(int i) => new Pose { T = new Vector3(F(i), F(i + 1), F(i + 2)), Rot = F(i + 3), Len = F(i + 4), Vis = I(i + 5) != 0 };
                if (pending > 0)
                {
                    pending--;
                    if (wave != null)
                        wave[filled++] = new Change { P = new Vector3Int(I(0), I(1), I(2)), Before = (byte)I(3), After = (byte)I(4), T0 = F(5), T1 = F(6), Dir = I(7) };
                    else piece.Start[new Vector3Int(I(0), I(1), I(2))] = (byte)I(3);
                    continue;
                }
                wave = null;
                switch (t[0])
                {
                    case "size": d.SX = I(1); d.SY = I(2); d.SZ = I(3); break;
                    case "origin": d.Origin = new Vector3Int(I(1), I(2), I(3)); break;
                    case "timescale": d.TimeScale = F(1); break;
                    case "palette":
                        for (int i = 1; i < t.Length; i++) d.Palette[i] = uint.Parse(t[i], NumberStyles.HexNumber) | 0xFF000000u;
                        break;
                    case "piece":
                        piece = new Piece { Name = t[1], Origin = new Vector3(F(3), F(4), F(5)) };
                        byName[piece.Name] = d.Pieces.Count;
                        d.Pieces.Add(piece);
                        pending = I(2);
                        break;
                    case "pose": d.Pieces[byName[t[1]]].StartPose = P(2); break;
                    case "step":
                        step = new Step { Name = t[1], Seconds = F(2), Load = F(3) };
                        d.Steps.Add(step);
                        break;
                    case "tween":
                        step.Tweens.Add(new Tween { Piece = byName[t[1]], A = F(2), B = F(3), Ease = t[4], To = P(5) });
                        break;
                    case "wave":
                        wave = new Change[I(2)];
                        filled = 0;
                        pending = wave.Length;
                        step.Waves.Add((byName[t[1]], wave));
                        if (pending == 0) wave = null;
                        break;
                    case "snd": step.Sounds.Add((t[1] == "f", F(2), t[3], F(4))); break;
                    case "loop": step.Loops.Add((t[1], F(2), F(3), F(4))); break;
                    case "end": break;
                    default: throw new FormatException("unknown line: " + line);
                }
            }
            // Each tween starts from the piece's pose before it; the pieces' contents at the end (the core).
            var pose = new Pose[d.Pieces.Count];
            var content = new Dictionary<Vector3Int, byte>[d.Pieces.Count];
            for (int i = 0; i < d.Pieces.Count; i++)
            {
                pose[i] = d.Pieces[i].StartPose;
                content[i] = new Dictionary<Vector3Int, byte>(d.Pieces[i].Start);
            }
            foreach (Step s in d.Steps)
            {
                d.Seconds += s.Seconds;
                foreach (Tween tw in s.Tweens)
                {
                    tw.From = pose[tw.Piece];
                    pose[tw.Piece] = tw.To;
                }
                foreach (var (p, changes) in s.Waves)
                    foreach (Change c in changes)
                        if (c.After == 0) content[p].Remove(c.P);
                        else content[p][c.P] = c.After;
            }
            for (int i = 0; i < d.Pieces.Count; i++) d.Pieces[i].End = content[i];
            return d;
        }

        public static float Ease(string ease, float k)
        {
            switch (ease)
            {
                case "linear": return k;
                // A heavy part: eases in, overshoots a little and settles back.
                case "settle": return k < 0.85f ? Mathf.SmoothStep(0f, 1.05f, k / 0.85f) : Mathf.Lerp(1.05f, 1f, Mathf.SmoothStep(0f, 1f, (k - 0.85f) / 0.15f));
                default: return k * k * (3f - 2f * k);
            }
        }
    }

    /// <summary>
    /// Plays the transformation on one tower (the real models are hidden meanwhile): every piece is its own mesh that
    /// slides, tilts or extends; voxel changes rebuild a piece's mesh at their time, and a voxel that grows (the rails
    /// running out, a layer appearing) is a small cube stretching from its face. A hydraulic pump runs throughout (a
    /// double start-up, then a loop that strains while something moves, and a stop); each step has its locks, hisses
    /// and gear noises. Voxels are 1/16 tile, in the tower's frame.
    /// </summary>
    internal sealed class MorphPlayer : MonoBehaviour
    {
        private const float Voxel = 1f / 16f;

        private sealed class View
        {
            public Transform Root, MeshHolder;
            public MeshFilter Filter;
            public Dictionary<Vector3Int, byte> Content;
            public bool Dirty = true;
            public readonly List<Transform> Cubes = new List<Transform>();
            public int CubesUsed;
        }

        private MorphData data;
        private bool toCore;
        private int index = -1;
        private float u, prevU;
        private Action done;
        private View[] views;
        private MorphData.Step step;
        /// <summary>Per wave entry of the current step: what is in the mesh now (0 before, 1 growing, 2 after).</summary>
        private int[][] waveState;
        private readonly Dictionary<string, SoundLoop> loops = new Dictionary<string, SoundLoop>();
        private readonly HashSet<int> posed = new HashSet<int>();
        private SoundLoop pump;
        private float pumpAge, pumpLoad;
        private static Material material;
        private static readonly Dictionary<byte, Mesh> cubes = new Dictionary<byte, Mesh>();
        /// <summary>Steps finished so far (scenarios).</summary>
        public int StepsPlayed => Mathf.Max(0, index);

        public static MorphPlayer Play(Transform tower, bool toCore, Action done)
        {
            MorphData d = MorphData.Load();
            if (d == null) return null;
            var go = new GameObject("TR_AcceleratorMorph");
            go.transform.SetParent(tower, false);
            var p = go.AddComponent<MorphPlayer>();
            p.data = d;
            p.toCore = toCore;
            p.done = done;
            p.views = new View[d.Pieces.Count];
            for (int i = 0; i < d.Pieces.Count; i++)
            {
                MorphData.Piece piece = d.Pieces[i];
                var root = new GameObject("TR_Morph_" + piece.Name).transform;
                root.SetParent(go.transform, false);
                var holder = new GameObject("Mesh").transform;
                holder.SetParent(root, false);
                var f = holder.gameObject.AddComponent<MeshFilter>();
                holder.gameObject.AddComponent<MeshRenderer>().sharedMaterial = p.Material();
                p.views[i] = new View
                {
                    Root = root, MeshHolder = holder, Filter = f,
                    Content = new Dictionary<Vector3Int, byte>(toCore ? piece.Start : piece.End),
                };
                p.SetPose(i, toCore ? piece.StartPose : p.EndPose(i));
            }
            SoundLibrary.Play("ascensions/particle_accelerator/accel_pump_start", tower.position, 0.9f, 0.02f, 2);
            p.pump = SoundLoop.Start("ascensions/particle_accelerator/accel_pump_loop", go.transform, 0f);
            p.Next();
            return p;
        }

        internal static void ClearCache()
        {
            if (material != null) Destroy(material);
            material = null;
            foreach (Mesh m in cubes.Values) if (m != null) Destroy(m);
            cubes.Clear();
        }

        /// <summary>A piece's pose at the end of the whole sequence (the core).</summary>
        private MorphData.Pose EndPose(int piece)
        {
            MorphData.Pose pose = data.Pieces[piece].StartPose;
            foreach (MorphData.Step s in data.Steps)
                foreach (MorphData.Tween tw in s.Tweens)
                    if (tw.Piece == piece) pose = tw.To;
            return pose;
        }

        private Material Material()
        {
            if (material != null) return material;
            VoxAsset template = VoxelLibrary.Get("ascensions/particle/accelerator_base");
            material = template != null ? new Material(template.Material) : new Material(Shader.Find("Standard"));
            material.name = "TR_AcceleratorMorph";
            material.mainTexture = VoxMesher.PaletteTexture(data.Palette, "TR_AcceleratorMorph_palette");
            return material;
        }

        private void OnDestroy()
        {
            if (views != null)
                foreach (View v in views)
                    if (v != null && v.Filter != null && v.Filter.sharedMesh != null) Destroy(v.Filter.sharedMesh);
            foreach (SoundLoop l in loops.Values) if (l != null) Destroy(l.gameObject);
        }

        /// <summary>Grid point (voxel edges) → tower-local position (grid y is forward = Unity z, grid z is up).</summary>
        private Vector3 GridToLocal(Vector3 g) => new Vector3((g.x + data.Origin.x) * Voxel, (g.z + data.Origin.z) * Voxel, (g.y + data.Origin.y) * Voxel);

        private void SetPose(int i, MorphData.Pose pose)
        {
            View v = views[i];
            Vector3 origin = data.Pieces[i].Origin;
            v.Root.localPosition = GridToLocal(origin + pose.T);
            // Positive tilt turns the muzzle (+y, Unity forward) up (+z, Unity up): a negative turn about Unity's x.
            v.Root.localRotation = Quaternion.AngleAxis(-pose.Rot, Vector3.right);
            v.Root.localScale = new Vector3(1f, Mathf.Max(0.001f, pose.Len), 1f);
            v.MeshHolder.localPosition = -GridToLocal(origin) + GridToLocal(Vector3.zero);
            bool show = pose.Vis && pose.Len > 0.001f;
            if (v.Root.gameObject.activeSelf != show) v.Root.gameObject.SetActive(show);
        }

        private void Rebuild(int i)
        {
            View v = views[i];
            v.Dirty = false;
            if (v.Filter.sharedMesh != null) Destroy(v.Filter.sharedMesh);
            v.Filter.sharedMesh = null;
            if (v.Content.Count == 0) return;
            var m = new VoxModel(data.SX, data.SY, data.SZ) { Palette = data.Palette };
            foreach (var kv in v.Content)
                if (kv.Key.x >= 0 && kv.Key.y >= 0 && kv.Key.z >= 0 && kv.Key.x < data.SX && kv.Key.y < data.SY && kv.Key.z < data.SZ)
                    m.Voxels[kv.Key.x, kv.Key.y, kv.Key.z] = kv.Value;
            v.Filter.sharedMesh = VoxMesher.Build(m, Voxel, Vector3.zero, "TR_Morph_" + data.Pieces[i].Name);
        }

        private Mesh Cube(byte c)
        {
            if (cubes.TryGetValue(c, out Mesh mesh) && mesh != null) return mesh;
            var m = new VoxModel(1, 1, 1) { Palette = data.Palette };
            m.Voxels[0, 0, 0] = c;
            mesh = VoxMesher.Build(m, Voxel, Vector3.zero, "TR_MorphCube");
            cubes[c] = mesh;
            return mesh;
        }

        /// <summary>A growing voxel: a cube on the piece, stretched from its face along the growth direction.</summary>
        private void ShowCube(View v, Vector3Int p, byte c, int dir, float g)
        {
            Transform t;
            if (v.CubesUsed < v.Cubes.Count) t = v.Cubes[v.CubesUsed];
            else
            {
                t = new GameObject("TR_MorphCube").transform;
                t.SetParent(v.MeshHolder, false);
                t.gameObject.AddComponent<MeshFilter>();
                t.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
                v.Cubes.Add(t);
            }
            v.CubesUsed++;
            t.gameObject.SetActive(true);
            t.GetComponent<MeshFilter>().sharedMesh = Cube(c);
            g = Mathf.Max(0.001f, g);
            // Grid x, y, z = Unity x, z, y. Negative directions anchor at the far face and scale negatively.
            Vector3 pos = new Vector3(p.x * Voxel, p.z * Voxel, p.y * Voxel), scale = Vector3.one;
            switch (dir)
            {
                case 1: scale.x = g; break;
                case 2: pos.x += Voxel; scale.x = -g; break;
                case 3: scale.z = g; break;
                case 4: pos.z += Voxel; scale.z = -g; break;
                case 5: scale.y = g; break;
                case 6: pos.y += Voxel; scale.y = -g; break;
            }
            t.localPosition = pos;
            t.localScale = scale;
        }

        private void Next()
        {
            index++;
            foreach (SoundLoop l in loops.Values) if (l != null) Destroy(l.gameObject);
            loops.Clear();
            if (index >= data.Steps.Count)
            {
                if (pump != null) Destroy(pump.gameObject);
                SoundLibrary.Play("ascensions/particle_accelerator/accel_pump_stop", transform.position, 0.9f, 0.02f, 2);
                SoundLibrary.Play("ascensions/particle_accelerator/accel_vent", transform.position, 0.8f, 0.03f, 2);
                Action d = done;
                done = null;
                Destroy(gameObject);
                d?.Invoke();
                return;
            }
            step = data.Steps[toCore ? index : data.Steps.Count - 1 - index];
            u = 0f;
            prevU = -1f;
            waveState = new int[step.Waves.Count][];
            for (int w = 0; w < step.Waves.Count; w++)
            {
                waveState[w] = new int[step.Waves[w].changes.Length];
                for (int k = 0; k < waveState[w].Length; k++) waveState[w][k] = -1;
            }
            Animate();
        }

        private void Update()
        {
            if (step == null) return;
            prevU = u;
            u = Mathf.Min(1f, u + Time.deltaTime / Mathf.Max(0.01f, step.Seconds));
            Animate();
            Pump();
            if (u >= 1f) Next();
        }

        private void Animate()
        {
            float tau = toCore ? u : 1f - u; // the step's own (forward) time
            bool moving = false;
            // In playing order; a piece with several tweens in a step (the rails: rise, slide, settle) takes its pose
            // from the last one that has begun (or the first, before any has).
            posed.Clear();
            int n = step.Tweens.Count;
            for (int k = 0; k < n; k++)
            {
                MorphData.Tween tw = step.Tweens[toCore ? k : n - 1 - k];
                float raw = tw.B > tw.A ? Mathf.Clamp01(toCore ? (tau - tw.A) / (tw.B - tw.A) : (tw.B - tau) / (tw.B - tw.A)) : 1f;
                moving |= raw > 0f && raw < 1f;
                if (!posed.Add(tw.Piece) && raw <= 0f) continue;
                MorphData.Pose from = toCore ? tw.From : tw.To, to = toCore ? tw.To : tw.From;
                SetPose(tw.Piece, MorphData.Pose.Lerp(from, to, MorphData.Ease(tw.Ease, raw), raw));
            }
            pumpLoad = moving ? step.Load : 0f;

            foreach (View v in views) v.CubesUsed = 0;
            for (int w = 0; w < step.Waves.Count; w++)
            {
                var (pi, changes) = step.Waves[w];
                View v = views[pi];
                for (int k = 0; k < changes.Length; k++)
                {
                    MorphData.Change c = changes[k];
                    bool grows = c.Dir != 0 && (c.Before == 0 || c.After == 0);
                    int state = tau >= c.T1 ? 2 : tau <= c.T0 ? 0 : grows ? 1 : tau >= (c.T0 + c.T1) / 2f ? 2 : 0;
                    if (state == 1)
                    {
                        float g = (tau - c.T0) / Mathf.Max(0.001f, c.T1 - c.T0);
                        ShowCube(v, c.P, c.After != 0 ? c.After : c.Before, c.Dir, c.After != 0 ? g : 1f - g);
                    }
                    if (state == waveState[w][k]) continue;
                    waveState[w][k] = state;
                    byte now = state == 2 ? c.After : state == 0 ? c.Before : (byte)0;
                    v.Content.TryGetValue(c.P, out byte had);
                    if (now == had) continue;
                    if (now == 0) v.Content.Remove(c.P);
                    else v.Content[c.P] = now;
                    v.Dirty = true;
                }
            }
            for (int i = 0; i < views.Length; i++)
            {
                View v = views[i];
                for (int k = v.CubesUsed; k < v.Cubes.Count; k++) if (v.Cubes[k].gameObject.activeSelf) v.Cubes[k].gameObject.SetActive(false);
                if (v.Dirty) Rebuild(i);
            }

            // One-shots at their time (in the playing direction), loops while their window lasts.
            foreach (var (forward, at, name, vol) in step.Sounds)
                if (forward == toCore && (prevU < at || (at <= 0f && prevU < 0f)) && u >= at)
                    SoundLibrary.Play(name, transform.position, vol, 0.04f, 4);
            foreach (var (name, a, b, vol) in step.Loops)
            {
                bool on = tau >= a && tau <= b && u < 1f;
                loops.TryGetValue(name, out SoundLoop l);
                if (on && l == null) { l = SoundLoop.Start(name, transform, vol); loops[name] = l; }
                else if (!on && l != null) { Destroy(l.gameObject); loops.Remove(name); }
            }
        }

        /// <summary>The pump: fades in under its start-up, strains (higher, louder) while a piece moves.</summary>
        private void Pump()
        {
            if (pump == null) return;
            pumpAge += Time.deltaTime;
            float fade = Mathf.Clamp01((pumpAge - 0.45f) / 0.35f);
            float k = 1f - Mathf.Exp(-Time.deltaTime * 6f);
            pump.Pitch = Mathf.Lerp(pump.Pitch, 1f + 0.16f * pumpLoad, k);
            pump.Volume = Mathf.Lerp(pump.Volume, fade * (0.55f + 0.35f * pumpLoad), k);
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using TreasureRun.Audio;
using TreasureRun.Core;
using TreasureRun.Treasures;
using TreasureRun.Voxels;
using TreasureRun.Vox;
using UnityEngine;
using UnityEngine.UI;

namespace TreasureRun.Ascensions.Phase3
{
    /// <summary>
    /// Ley Line Network (Mana Siphon, Legendary): siphons next to each other (any height) form a network, drawn as
    /// hollow glass pipes with gold edges (<see cref="LeyGeometry"/>, built in game per port combination and cached): a
    /// hub per siphon with ports only where pipes join, an arm to each neighbouring siphon and a funnel flush against each
    /// neighbouring crystal. Across height differences the lower siphon carries the pipe on a riser with pipe-wide corner
    /// fittings (<see cref="LeyLineLogic.Plan"/>); an intake to a lower crystal drops down to it. Only siphons touching a
    /// crystal ("taps") gather: rate × (1 + ½ per further crystal in the network), rounded down; the others are
    /// connectors (<see cref="LeyLineLogic.Solve"/>). Mana motes flow inside the pipes.
    /// </summary>
    internal sealed class LeySiphon : AscendedTower
    {
        public const string Id = "ley_line_network";
        internal static readonly List<LeySiphon> All = new List<LeySiphon>();
        private const float ArtScale = 0.625f; // 16 voxels per tile
        /// <summary>World height of one terrain step.</summary>
        internal const float Step = 1f / 3f;
        /// <summary>Fitting faces above the node (z 2..5 at 1/16 per voxel): bottom and top.</summary>
        private const float FittingBottom = 0.125f, FittingTop = 0.375f;

        public ManaSiphon Siphon { get; private set; }
        /// <summary>Mana per second this siphon gathers (0 for a connector).</summary>
        public int Income { get; private set; }
        /// <summary>Touches a crystal itself (gathers) rather than only connecting.</summary>
        public bool IsTap { get; private set; } = true;
        public int NetworkIncome { get; private set; }
        public int NetworkSiphons { get; private set; }
        /// <summary>Siphons of the network that gather (taps).</summary>
        public int NetworkTaps { get; private set; }
        public int NetworkCrystals { get; private set; }
        public long Gathered { get; private set; }
        public bool Lit { get; private set; }
        /// <summary>The pipe parts currently shown (frames and glass), named "TR_Ley_&lt;part&gt;" (for tests).</summary>
        internal readonly List<GameObject> Parts = new List<GameObject>();

        private Transform artRoot;
        private LeyPlan plan;
        private string shownKey = "";
        private int shownDelta;
        private float timer = 1f;

        public LeyCell Cell => new LeyCell(Mathf.RoundToInt(transform.position.x), Mathf.RoundToInt(transform.position.z), Mathf.RoundToInt(transform.position.y * 3f));
        public int Rate => Siphon != null ? Mathf.Max(1, Siphon.gatherRatePerSec) : 1;

        public static void Register()
        {
            AscensionRegistry.Register(new AscensionDef
            {
                Id = Id, Name = "Ley Line Network", TowerName = "Ley Siphon", Rarity = Rarity.Legendary, Kind = AscensionKind.Augment,
                Target = AscensionTarget.ManaSiphon, Component = typeof(LeySiphon), CardModel = "ascensions/siphon/ley_card",
                Description = "Connect your siphons into one large network. Siphons next to each other link up with arcane pipes. " +
                              "A siphon next to a mana crystal gathers 1 mana per second, plus half a mana for every other " +
                              "crystal its network touches; siphons without a crystal of their own connect the network.",
                OnAcquire = (run, restoring) => LeyNetwork.Ensure(),
            });
        }

        protected override void OnAscend()
        {
            Siphon = GetComponent<ManaSiphon>();
            HideVanillaModel();
            // Our art ignores the vanilla LookAt (the siphon turns towards its crystal): world-aligned pipes.
            artRoot = new GameObject("TR_LeyArt").transform;
            artRoot.SetParent(transform, false);
            artRoot.position = transform.position;
            artRoot.rotation = Quaternion.identity;
            plan = LeyLineLogic.Plan(Cell, new LeyNeighbour[4]);
            Rebuild();
            All.Add(this);
            LeyNetwork.Ensure();
            LeyNetwork.MarkDirty();
        }

        protected override void OnDestroy()
        {
            All.Remove(this);
            if (ResourceManager.instance != null && shownDelta != 0) ResourceManager.instance.UpdateManaGatherRate(-shownDelta);
            shownDelta = 0;
            if (artRoot != null) Destroy(artRoot.gameObject);
            LeyNetwork.MarkDirty();
            base.OnDestroy();
        }

        internal void SetPlan(LeyPlan p)
        {
            plan = p;
            Rebuild();
        }

        private LeyGeometry.HubKind HubKind => !IsTap ? LeyGeometry.HubKind.Connector : Lit ? LeyGeometry.HubKind.TapLit : LeyGeometry.HubKind.TapUnlit;

        /// <summary>
        /// Rebuilds the parts when the plan or the hub look changed: the hub (ports where arms leave at its level, top port
        /// for a riser), one pipe-wide corner per level above with riser segments between the fittings, and the arms.
        /// </summary>
        private void Rebuild()
        {
            var key = new StringBuilder().Append(HubKind).Append('|');
            foreach (LeyArm a in plan.Arms) key.Append((int)a.Kind).Append(':').Append(a.Rise).Append(':').Append(a.Drop).Append(';');
            if (key.ToString() == shownKey) return;
            shownKey = key.ToString();
            foreach (GameObject p in Parts) if (p != null) Destroy(p);
            Parts.Clear();

            int SidesAt(int level)
            {
                int mask = 0;
                for (int d = 0; d < 4; d++)
                    if (plan.Arms[d].Kind != LeyNeighbourKind.None && plan.Arms[d].Rise == level) mask |= 1 << d;
                return mask;
            }
            LeyGeometry.HubKind kind = HubKind;
            int hubSides = SidesAt(0);
            bool riser = plan.Riser > 0;
            Part($"hub_{kind}_{hubSides}_{riser}", () => LeyGeometry.Hub(kind, hubSides, riser), 0f, 0);

            int below = 0;
            foreach (int level in plan.Tees)
            {
                int sides = SidesAt(level);
                bool top = level < plan.Riser;
                Part($"corner_{sides}_{top}", () => LeyGeometry.Corner(sides, top, true), level * Step, 0);
                float from = below * Step + FittingTop, to = level * Step + FittingBottom;
                Part("riser", LeyGeometry.Riser, from, 0, to - from);
                below = level;
            }

            for (int d = 0; d < 4; d++)
            {
                LeyArm arm = plan.Arms[d];
                if (arm.Kind == LeyNeighbourKind.None) continue;
                int start = arm.Rise > 0 ? 10 : 11; // from a corner (4 wide) or from the hub (6 wide)
                float y = arm.Rise * Step;
                if (arm.Kind == LeyNeighbourKind.Siphon) { Part($"pipe_{start}", () => LeyGeometry.Pipe(start), y, d); continue; }
                if (arm.Drop == 0) { Part($"intake_{start}", () => LeyGeometry.Intake(start), y, d); continue; }
                // A lower crystal: the arm to the tile edge, a corner, a drop and a corner with the mouth at the crystal.
                float down = -arm.Drop * Step;
                Part("pipe_11", () => LeyGeometry.Pipe(11), 0f, d);
                Part("drop_upper", () => LeyGeometry.DropCorner(true), 0f, d);
                float dropFrom = down + FittingTop;
                Part("drop", LeyGeometry.Drop, dropFrom, d, FittingBottom - dropFrom);
                Part("drop_lower", () => LeyGeometry.DropCorner(false), down, d);
            }
        }

        /// <summary>Shows a cached part (frame + glass); <paramref name="stretch"/> stretches a 1-unit-tall part to that height.</summary>
        private void Part(string key, System.Func<LeyGeometry.Part> make, float y, int direction, float stretch = 0f)
        {
            if (!LeyParts.Get(key, make, out Mesh frame, out Mesh glass)) return;
            Vector3 scale = stretch > 0f ? new Vector3(ArtScale, ArtScale * stretch, ArtScale) : Vector3.one * ArtScale;
            Quaternion rot = Quaternion.Euler(0f, 90f * direction, 0f);
            string name = "TR_Ley_" + key.Split('_')[0];
            foreach (var (mesh, glassy) in new[] { (frame, false), (glass, true) })
            {
                if (mesh.vertexCount == 0) continue;
                var go = new GameObject(glassy ? name + "_glass" : name);
                go.transform.SetParent(artRoot, false);
                go.transform.localPosition = Vector3.up * y;
                go.transform.localRotation = rot;
                go.transform.localScale = scale;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = glassy ? LeyParts.GlassMaterial : LeyParts.FrameMaterial;
                if (glassy) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Parts.Add(go);
            }
        }

        /// <summary>Sets this siphon's income and keeps the HUD's mana rate in step (vanilla already shows its own rate).</summary>
        internal void SetIncome(int income, bool tap, int networkSiphons, int networkTaps, int networkCrystals, int networkIncome)
        {
            Income = income;
            NetworkSiphons = networkSiphons;
            NetworkTaps = networkTaps;
            NetworkCrystals = networkCrystals;
            NetworkIncome = networkIncome;
            // A siphon alone without a crystal keeps the full hub (it's not connecting anything).
            IsTap = tap || networkSiphons == 1;
            Lit = networkCrystals > 0;
            int vanillaShown = Siphon != null && Siphon.gathering ? Siphon.gatherRatePerSec : 0;
            int delta = income - vanillaShown;
            if (delta != shownDelta && ResourceManager.instance != null)
            {
                ResourceManager.instance.UpdateManaGatherRate(delta - shownDelta);
                shownDelta = delta;
            }
        }

        /// <summary>Replaces ManaSiphon.Update: gathers the network income once a second during waves.</summary>
        internal void Tick()
        {
            if (Income <= 0 || SpawnManager.instance == null || !SpawnManager.instance.combat) return;
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = 1f;
            ResourceManager.instance.AddMana(Income);
            Gathered += Income;
        }
    }

    /// <summary>
    /// Meshes of the Ley parts, built from <see cref="LeyGeometry"/> the first time a combination is needed and cached
    /// (a few dozen small meshes at most). All share the palette model's material; the glass uses an unlit alpha-blended
    /// copy (the build strips the Standard shader's transparent variants).
    /// </summary>
    internal static class LeyParts
    {
        private static readonly Dictionary<string, (Mesh frame, Mesh glass)> cache = new Dictionary<string, (Mesh, Mesh)>();
        public static Material FrameMaterial { get; private set; }
        public static Material GlassMaterial { get; private set; }

        private static bool Ensure()
        {
            if (FrameMaterial != null) return true;
            VoxAsset palette = VoxelLibrary.Get("ascensions/siphon/ley_palette");
            if (palette == null) return false;
            FrameMaterial = palette.Material;
            GlassMaterial = Fx.GhostMaterial(palette.Material.mainTexture, new Color(0.9f, 0.97f, 1f, 0.4f)) ?? palette.Material;
            return true;
        }

        private static Vector3 Origin => new Vector3(-LeyGeometry.SizeX * 0.1f / 2f, 0f, -LeyGeometry.SizeY * 0.1f / 2f);

        public static bool Get(string key, System.Func<LeyGeometry.Part> make, out Mesh frame, out Mesh glass)
        {
            frame = glass = null;
            if (!Ensure()) return false;
            if (!cache.TryGetValue(key, out var m) || m.frame == null)
            {
                LeyGeometry.Part p = make();
                m = (VoxMesher.Build(p.Frame, 0.1f, Origin, "TR_Ley_" + key), VoxMesher.Build(p.Glass, 0.1f, Origin, "TR_Ley_" + key + "_glass"));
                cache[key] = m;
            }
            (frame, glass) = m;
            return true;
        }

        /// <summary>The mote mesh (2x2x2 voxels at 0.1, standing on y = 0).</summary>
        public static Mesh Mote()
        {
            if (!Ensure()) return null;
            if (!cache.TryGetValue("mote", out var m) || m.frame == null)
                cache["mote"] = m = (VoxMesher.Build(LeyGeometry.Mote(), 0.1f, Vector3.zero, "TR_Ley_mote"), null);
            return m.frame;
        }
    }

    /// <summary>
    /// The networks: recomputed when a siphon is built or demolished (or the crystals change), it sets every siphon's
    /// income, lights and pipes, draws the mana motes flowing through them and plays the quiet flow loop.
    /// </summary>
    internal sealed class LeyNetwork : MonoBehaviour
    {
        public static LeyNetwork Instance { get; private set; }
        /// <summary>Mana per second of all networks together (the Particle Accelerator's drain will rise by this).</summary>
        public static int TotalIncome { get; private set; }
        public int Links => links.Count;

        private static bool dirty = true;
        private float check;
        private int crystalCount = -1;
        /// <summary>Mote paths: polylines from where the mana comes from to where it goes, with their lengths.</summary>
        private readonly List<(Vector3[] points, float length)> links = new List<(Vector3[], float)>();
        private Material moteMat;
        // All motes in one mesh, rewritten every frame: one draw call however many there are (no GameObjects).
        private Vector3[] moteVerts, moteNormals, batchVerts, batchNormals;
        private Vector2[] moteUvs, batchUvs;
        private int[] moteTris;
        private Mesh batch;
        private int batchMotes = -1;
        public int MoteCount => batchMotes;
        private SoundLoop hum;

        public static void Ensure()
        {
            if (!TreasureRunMode.Active || Instance != null) return;
            Instance = new GameObject("TR_LeyNetwork").AddComponent<LeyNetwork>();
            dirty = true;
        }

        public static void MarkDirty() => dirty = true;

        private void Start()
        {
            Mesh mote = LeyParts.Mote();
            if (mote == null) return;
            moteMat = LeyParts.FrameMaterial;
            moteVerts = mote.vertices;
            moteNormals = mote.normals;
            moteUvs = mote.uv;
            moteTris = mote.triangles;
            // Centre the mote on its own middle (the voxel mesh stands on y = 0).
            Vector3 c = mote.bounds.center;
            for (int i = 0; i < moteVerts.Length; i++) moteVerts[i] -= c;
            batch = new Mesh { name = "TR_LeyMotes" };
            batch.MarkDynamic();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            TotalIncome = 0;
            if (hum != null) Destroy(hum.gameObject);
            if (batch != null) Destroy(batch);
        }

        private void Update()
        {
            AscensionDef owned = AscensionRegistry.Owned(AscensionTarget.ManaSiphon);
            if (owned == null || owned.Id != LeySiphon.Id) { Destroy(gameObject); return; }
            check -= Time.deltaTime;
            if (check <= 0f)
            {
                check = 2f;
                int crystals = FindObjectsOfType<ManaCrystal>().Length;
                if (crystals != crystalCount) { crystalCount = crystals; dirty = true; }
            }
            if (dirty) { dirty = false; Recompute(); }
            DrawMotes();
            UpdateHum();
        }

        private static LeyCell CellOf(Vector3 p) => new LeyCell(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.z), Mathf.RoundToInt(p.y * 3f));

        private void Recompute()
        {
            List<LeySiphon> siphons = LeySiphon.All.Where(s => s != null && s.Siphon != null).ToList();
            ManaCrystal[] crystals = FindObjectsOfType<ManaCrystal>();
            var siphonCells = siphons.Select(s => s.Cell).ToList();
            var crystalCells = crystals.Select(c => CellOf(c.transform.position)).ToList();
            int rate = siphons.Count > 0 ? siphons[0].Rate : 1;
            LeyLineLogic.Result r = LeyLineLogic.Solve(siphonCells, crystalCells, rate);

            links.Clear();
            TotalIncome = 0;
            for (int i = 0; i < siphons.Count; i++)
            {
                LeyNetworkInfo net = r.Networks[r.NetworkOf[i]];
                siphons[i].SetIncome(r.Income[i], r.IsTap[i], net.Siphons.Count, net.Taps, net.Crystals.Count, net.Income);
                TotalIncome += r.Income[i];
                LeyCell me = siphonCells[i];
                Vector3 pos = siphons[i].transform.position;
                var around = new LeyNeighbour[4];
                for (int d = 0; d < 4; d++)
                {
                    var (dx, dz) = LeyLineLogic.Directions[d];
                    int other = siphonCells.FindIndex(c => c.X == me.X + dx && c.Z == me.Z + dz);
                    int crystal = other >= 0 ? -1 : crystalCells.FindIndex(c => c.X == me.X + dx && c.Z == me.Z + dz);
                    around[d] = other >= 0 ? new LeyNeighbour(LeyNeighbourKind.Siphon, siphonCells[other].Level)
                              : crystal >= 0 ? new LeyNeighbour(LeyNeighbourKind.Crystal, crystalCells[crystal].Level)
                              : new LeyNeighbour(LeyNeighbourKind.None, 0);
                }
                LeyPlan plan = LeyLineLogic.Plan(me, around);
                siphons[i].SetPlan(plan);

                // Motes: from each crystal into the siphon; between siphons from the one nearer a crystal to the other.
                for (int d = 0; d < 4; d++)
                {
                    var (dx, dz) = LeyLineLogic.Directions[d];
                    var dir = new Vector3(dx, 0f, dz);
                    LeyArm arm = plan.Arms[d];
                    // Inside the tubes (their hollow centre is 4 voxels up); the mouth is flush with the crystal (tile y 20)
                    // and a drop runs down the middle of its corners (tile y 18).
                    const float h = 0.25f, mouth = 0.75f, elbow = 0.625f;
                    Vector3 P(float level, float along) => pos + dir * along + Vector3.up * (h + level * LeySiphon.Step);
                    if (arm.Kind == LeyNeighbourKind.Crystal)
                    {
                        if (arm.Drop > 0) AddLink(P(-arm.Drop, mouth), P(-arm.Drop, elbow), P(0, elbow), P(0, 0));
                        else AddLink(P(arm.Rise, mouth), P(arm.Rise, 0f), P(0, 0));
                        continue;
                    }
                    if (arm.Kind != LeyNeighbourKind.Siphon) continue;
                    int other = siphonCells.FindIndex(c => c.X == me.X + dx && c.Z == me.Z + dz);
                    // Each pair once: from the lower siphon (it carries the pipe), or the lower index on equal height.
                    bool mine = siphonCells[other].Level > me.Level || siphonCells[other].Level == me.Level && other > i;
                    if (!mine || r.Distance[i] < 0 || r.Distance[i] == r.Distance[other]) continue;
                    Vector3[] path = { P(0, 0), P(arm.Rise, 0), P(arm.Rise, 1f) };
                    if (r.Distance[i] > r.Distance[other]) System.Array.Reverse(path);
                    AddLink(path);
                }
            }
            if (ModConfig.DebugTools.Value)
                Plugin.Log.LogInfo($"[ley] {siphons.Count} siphons in {r.Networks.Count} networks, {crystals.Length} crystals on the map: " +
                                   $"{TotalIncome} mana/s, {links.Count} flowing links.");
        }

        private void AddLink(params Vector3[] points)
        {
            float length = 0f;
            for (int i = 1; i < points.Length; i++) length += Vector3.Distance(points[i - 1], points[i]);
            if (length > 0.01f) links.Add((points, length));
        }

        private void DrawMotes()
        {
            if (batch == null || links.Count == 0) return;
            int motes = links.Count * 2, per = moteVerts.Length;
            if (motes != batchMotes)
            {
                // Rebuilt only when the number of motes changes (on a network change); per frame only the positions move.
                batchMotes = motes;
                batchVerts = new Vector3[motes * per];
                batchNormals = new Vector3[motes * per];
                batchUvs = new Vector2[motes * per];
                var tris = new int[motes * moteTris.Length];
                for (int m = 0; m < motes; m++)
                {
                    System.Array.Copy(moteNormals, 0, batchNormals, m * per, per);
                    System.Array.Copy(moteUvs, 0, batchUvs, m * per, per);
                    for (int t = 0; t < moteTris.Length; t++) tris[m * moteTris.Length + t] = moteTris[t] + m * per;
                }
                batch.Clear();
                batch.vertices = batchVerts;
                batch.normals = batchNormals;
                batch.uv = batchUvs;
                batch.triangles = tris;
            }
            // 0.06 across, unrotated, so a mote fits the hollow tubes (0.125 wide) without touching them.
            const float scale = 0.3f;
            const float speed = 0.9f;
            int v = 0;
            for (int i = 0; i < links.Count; i++)
            {
                var (points, length) = links[i];
                for (int k = 0; k < 2; k++)
                {
                    // Same speed on every link: position along the polyline by distance.
                    float s = Mathf.Repeat(Time.time * speed + i * 0.37f + k * length * 0.5f, length);
                    Vector3 p = points[points.Length - 1];
                    for (int j = 1; j < points.Length; j++)
                    {
                        float seg = Vector3.Distance(points[j - 1], points[j]);
                        if (s <= seg) { p = Vector3.Lerp(points[j - 1], points[j], seg > 0f ? s / seg : 0f); break; }
                        s -= seg;
                    }
                    for (int j = 0; j < per; j++) batchVerts[v++] = p + moteVerts[j] * scale;
                }
            }
            batch.vertices = batchVerts;
            batch.RecalculateBounds();
            Graphics.DrawMesh(batch, Matrix4x4.identity, moteMat, 0);
        }

        private void UpdateHum()
        {
            float volume = TotalIncome > 0 ? Mathf.Min(1f, 0.35f + 0.03f * TotalIncome) * 0.5f : 0f;
            if (volume > 0.01f && hum == null && Camera.main != null) hum = SoundLoop.Start("ascensions/ley_flow", Camera.main.transform, volume);
            if (hum != null)
            {
                hum.Volume = volume;
                if (volume <= 0.01f) { Destroy(hum.gameObject); hum = null; }
            }
        }
    }

    [HarmonyPatch]
    internal static class LeyLinePatches
    {
        private static LeySiphon LeyOf(ManaSiphon s) => TreasureRunMode.Active && s != null ? s.GetComponent<LeySiphon>() : null;

        [HarmonyPrefix, HarmonyPatch(typeof(ManaSiphon), nameof(ManaSiphon.Update))]
        private static bool UpdatePrefix(ManaSiphon __instance)
        {
            LeySiphon ley = LeyOf(__instance);
            if (ley == null) return true;
            ley.Tick();
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(ManaSiphon), nameof(ManaSiphon.SpawnUI))]
        private static bool SpawnUIPrefix(ManaSiphon __instance)
        {
            LeySiphon ley = LeyOf(__instance);
            if (ley == null) return true;
            SimpleUI ui = Object.Instantiate(__instance.UIObject, __instance.transform.position, Quaternion.identity).GetComponent<SimpleUI>();
            ui.SetDemolishable(__instance.gameObject, __instance.goldBackOnDemolish);
            // Short lines, and the font shrinks to fit the vanilla box (it was sized for one line).
            // Only gathering siphons count; connectors are just pipes.
            string text = $"Network: {ley.NetworkTaps} gathering siphon{(ley.NetworkTaps == 1 ? "" : "s")}, " +
                          $"{ley.NetworkCrystals} crystal{(ley.NetworkCrystals == 1 ? "" : "s")}\n" +
                          (ley.NetworkCrystals == 0 ? "No crystal in this network"
                           : ley.Income > 0 ? $"This siphon: {ley.Income} mana/s\nNetwork: {ley.NetworkIncome} mana/s"
                           : $"Connector (no crystal next to it)\nNetwork: {ley.NetworkIncome} mana/s");
            Text label = ui.discriptionText;
            if (label != null)
            {
                label.resizeTextForBestFit = true;
                label.resizeTextMaxSize = label.fontSize;
                label.resizeTextMinSize = Mathf.Max(1, label.fontSize / 3);
                label.verticalOverflow = VerticalWrapMode.Truncate;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
            }
            ui.SetDiscriptionText(text);
            return false;
        }
    }
}

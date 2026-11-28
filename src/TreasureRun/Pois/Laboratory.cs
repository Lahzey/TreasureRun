using System.Collections.Generic;
using System.Linq;
using TreasureRun.Ascensions;
using TreasureRun.Ascensions.Phase3;
using TreasureRun.Audio;
using TreasureRun.Core;
using TreasureRun.Voxels;
using UnityEngine;

namespace TreasureRun.Pois
{
    /// <summary>
    /// Laboratory (docs/poi-plan.md §9): a 3x3 lab in the middle of its section, the path running round it. Its centre cell
    /// is a chamber: a tower or building built there is only studied (<see cref="LabSpecimen"/>): it does nothing at all,
    /// but it counts for its kind's price like any other copy. At the start of every wave the kind in the chamber gains a
    /// wave of research (<see cref="LabResearch"/>), kept for the rest of the run. The lab is dark while the chamber is
    /// empty and comes alive while something is in it: lit fluids and screens, beams from its four emitters, a scanning
    /// ring. A click shows what it studies and sells the specimen (the cell is free again).
    /// </summary>
    public sealed class Laboratory : PoiSite
    {
        public const string ModelIdle = "pois/laboratory", ModelActive = "pois/laboratory_active";
        public const string SoundPowerUp = "pois/laboratory/power_up", SoundPowerDown = "pois/laboratory/power_down", SoundResearch = "pois/laboratory/research";

        public static readonly List<Laboratory> All = new List<Laboratory>();

        internal static readonly Color Cyan = new Color(0.55f, 0.95f, 1f, 1f), Fluid = new Color(0.5f, 1f, 0.55f, 1f);

        /// <summary>The emitter orbs in model voxels (x across, y forward, z up; tools/VoxTool LabRecipes.Orbs).</summary>
        private static readonly Vector3[] Orbs =
        {
            new Vector3(24.7f, 4.3f, 20f), new Vector3(4.3f, 4.3f, 14f), new Vector3(24.7f, 24.7f, 14f), new Vector3(5.3f, 23.7f, 6f),
        };
        /// <summary>The tank's base and the top of its fluid, in model voxels.</summary>
        private static readonly Vector3 TankBase = new Vector3(25.5f, 14.5f, 3f);

        public static PoiDef Create() => new PoiDef
        {
            Id = LaboratoryLogic.Id,
            Name = "Laboratory",
            Icon = "poi/laboratory",
            Weight = LaboratoryLogic.Weight,
            MinWave = LaboratoryLogic.MinWave,
            Description = "An abandoned laboratory with an empty chamber. Build a tower or building in it to study its kind: " +
                          "every wave that kind gets a little better, for good. While it is studied, it does nothing else.",
            Style = () => new LaboratoryStyle(),
            Attach = go => go.AddComponent<Laboratory>(),
        };

        /// <summary>The voxel model's position for a voxel centre (30 x 30 at 0.1, centred on the chamber, standing on its floor).</summary>
        private static Vector3 VoxelLocal(Vector3 v) => new Vector3((v.x + 0.5f - 15f) * 0.1f, (v.z + 0.5f) * 0.1f, (v.y + 0.5f - 15f) * 0.1f);

        // ------------------------------------------------------------------ state

        private Vector3 slot;
        private GameObject structure, chamber;
        private Transform holder, fx;
        private PoiPopup popup;
        private LabSpecimen specimen;
        private bool occupied;
        private float power, flash, refreshTimer;
        private Vector3 specimenCentre;
        private float specimenHeight = 1f;

        private LineRenderer[] beams;
        private MeshRenderer[] orbGlows;
        private GameObject scanRing, floorRing;
        private Material scanMat, floorMat;
        private ParticleSystem bubbles;

        /// <summary>The chamber's floor (the centre cell at its height).</summary>
        public Vector3 Slot => slot;
        public Vector2Int Grid => new Vector2Int(Mathf.RoundToInt(slot.x), Mathf.RoundToInt(slot.z));
        public LabSpecimen Specimen => specimen;
        public bool Empty => specimen == null;
        /// <summary>Waves of research this lab has done (over all its specimens).</summary>
        public int WavesStudied => Mathf.RoundToInt(Record.Get("waves"));
        /// <summary>How lit the lab is (0 idle .. 1 active), for scenarios.</summary>
        public float Power => power;

        public override string Status =>
            $"chamber {Grid.x},{Grid.y}: " + (specimen != null ? $"{AscensionTargets.DisplayName(specimen.Target)} ({LabResearch.Describe(specimen.Target)})" : "empty") +
            $", {WavesStudied} waves studied";

        /// <summary>
        /// The lab whose empty chamber a ray aims at past the lab's own boxes. The boxes over the eight outer cells are
        /// tall (a click on a pylon opens the popup), so the camera's ray to the chamber floor meets the near ones first;
        /// the build ghost has to look past them. Null unless the first thing the ray hits is a lab's box and the ray
        /// crosses that lab's empty chamber at its floor.
        /// </summary>
        public static Laboratory ChamberUnder(Ray ray, int mask)
        {
            if (All.Count == 0 || Mathf.Abs(ray.direction.y) < 1e-4f) return null;
            if (!Physics.Raycast(ray, out RaycastHit hit, 2000f, mask, QueryTriggerInteraction.Ignore)) return null;
            foreach (Laboratory l in All)
            {
                if (l == null || !l.Empty || hit.collider.gameObject != l.structure) continue;
                Vector3 p = ray.origin + ray.direction * ((l.slot.y - ray.origin.y) / ray.direction.y);
                if (Mathf.Abs(p.x - l.slot.x) < 0.45f && Mathf.Abs(p.z - l.slot.z) < 0.45f) return l;
            }
            return null;
        }

        /// <summary>The lab whose empty or full chamber is at a build position (the build ghost's), if any.</summary>
        public static Laboratory SlotAt(Vector3 p)
        {
            foreach (Laboratory l in All)
            {
                if (l == null) continue;
                Vector3 d = p - l.slot;
                if (Mathf.Abs(d.x) < 0.2f && Mathf.Abs(d.z) < 0.2f && Mathf.Abs(d.y) < 0.4f) return l;
            }
            return null;
        }

        // ------------------------------------------------------------------ placement

        protected override void OnPlaced()
        {
            All.Add(this);
            LabResearch.Register();
            var (sx, sz) = LaboratoryLogic.Slot;
            slot = CellWorld(sx, sz, Layout.Height[sx, sz]);
            BuildStructure();
            BuildFx();
            GameEvents.WaveStarted += OnWaveStarted;
        }

        private void OnDestroy()
        {
            All.Remove(this);
            GameEvents.WaveStarted -= OnWaveStarted;
        }

        /// <summary>
        /// The model, and solid boxes over the eight cells round the chamber (Building layer: they block building, a click
        /// opens the popup). The chamber gets its own box only while it holds something (the specimen's colliders are off).
        /// The 3x3 block is the same whatever the tile's rotation, so all of it is world-aligned.
        /// </summary>
        private void BuildStructure()
        {
            int building = LayerMask.NameToLayer("Building");
            int layer = building >= 0 ? building : PoiPopup.ClickableLayer();
            structure = new GameObject("TR_Laboratory") { layer = layer };
            structure.transform.SetParent(transform, false);
            structure.transform.position = slot;
            structure.transform.rotation = Quaternion.identity;
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dz == 0) continue;
                    var box = structure.AddComponent<BoxCollider>();
                    box.center = new Vector3(dx, 0.6f, dz);
                    box.size = new Vector3(1f, 1.2f, 1f);
                }
            PoiPopup.AddClick(structure, OpenPopup);
            chamber = new GameObject("TR_LabChamber") { layer = layer };
            chamber.transform.SetParent(structure.transform, false);
            var cbox = chamber.AddComponent<BoxCollider>();
            cbox.center = new Vector3(0f, 1.1f, 0f);
            cbox.size = new Vector3(1f, 2.2f, 1f);
            PoiPopup.AddClick(chamber, OpenPopup);
            chamber.SetActive(false);
            popup = new PoiPopup
            {
                Title = () => Def.Name,
                Text = InfoText,
                ActionLabel = () => specimen != null ? $"Sell ({SellValue}g)" : "",
                Anchor = () => slot + Vector3.up * 2.1f,
            };
            holder = new GameObject("TR_LabModel").transform;
            holder.SetParent(structure.transform, false);
            SetModel(ModelIdle);
        }

        private void SetModel(string model)
        {
            if (holder == null) return;
            for (int i = holder.childCount - 1; i >= 0; i--) Destroy(holder.GetChild(i).gameObject);
            if (VoxelLibrary.Spawn(model, slot, Quaternion.identity, holder) != null) return;
            foreach (var (dx, dz) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
            {
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(cube.GetComponent<Collider>());
                cube.transform.SetParent(holder, false);
                cube.transform.localPosition = new Vector3(dx, 0.5f, dz);
                cube.transform.localScale = new Vector3(0.4f, 1f, 0.4f);
            }
        }

        private void OpenPopup()
        {
            popup.Action = specimen != null ? (System.Action)Sell : null;
            popup.Show(slot);
        }

        private string InfoText()
        {
            if (specimen == null)
            {
                string all = LabResearch.Summary();
                return $"The chamber is empty. Build a tower or building in it: at every wave start its kind gets {Pct}% better for good " +
                       "(towers attack faster, the Lookout's mark grows, buildings yield more). In there it does nothing else." +
                       (all.Length > 0 ? $"\nResearched: {all}" : "");
            }
            string name = AscensionTargets.DisplayName(specimen.Target);
            string others = string.Join(", ", AscensionTargets.All.Where(t => t != specimen.Target && LabResearch.Waves(t) > 0)
                                                                .Select(t => $"{AscensionTargets.DisplayName(t)} {LabResearch.Describe(t)}"));
            return $"Studying the <b>{name}</b>: {LabResearch.Describe(specimen.Target)} so far, {Pct}% more at every wave start.\n" +
                   $"In here it does nothing else, but it counts for the {name}'s price." +
                   (others.Length > 0 ? $"\nAlso researched: {others}" : "");
        }

        private static int Pct => (int)System.Math.Round(LaboratoryLogic.PerWave * 100);

        /// <summary>What selling the specimen pays: a tower's sell price (as for any copy), a building's price as bought.</summary>
        public int SellValue
        {
            get
            {
                if (specimen == null) return 0;
                Tower t = specimen.GetComponent<Tower>();
                return t != null && TowerManager.instance != null ? TowerManager.instance.GetSellPrice(t.towerType) : specimen.Paid;
            }
        }

        // ------------------------------------------------------------------ the chamber

        /// <summary>
        /// Builds a tower or building into the chamber (from the build ghost, the paint-build stroke or the dev console):
        /// the vanilla prefab, but marked a specimen before any of its code runs, every behaviour and collider switched
        /// off. A tower is registered with its flyweight by hand, so it counts for the price (and refunds like a copy).
        /// </summary>
        public GameObject Insert(GameObject prefab, int paid)
        {
            if (prefab == null || specimen != null) return null;
            AscensionTarget? target = AscensionTargets.Of(prefab);
            if (!target.HasValue) return null;
            GameObject go = Instantiate(prefab, slot, Quaternion.identity);
            go.name = prefab.name + " (studied)";
            var s = go.AddComponent<LabSpecimen>();
            s.Lab = this;
            s.Target = target.Value;
            s.Paid = paid;
            foreach (Behaviour b in go.GetComponentsInChildren<Behaviour>(true)) if (b != s) b.enabled = false;
            foreach (Collider col in go.GetComponentsInChildren<Collider>(true)) col.enabled = false;
            specimen = s;
            occupied = true;
            Tower t = go.GetComponent<Tower>();
            if (t != null && TowerManager.instance != null)
            {
                t.level = 1;
                TowerManager.instance.AddNewTower(t, t.towerType);
            }
            Record.Set("specimen", (int)target.Value + 1);
            MeasureSpecimen();
            chamber.SetActive(true);
            SetModel(ModelActive);
            flash = 1f;
            SoulFx.GlowBurst(specimenCentre, Cyan, 16, 1.6f, 0.12f, 0.6f, 0.4f);
            SoundLibrary.Play(SoundPowerUp, slot, 1f, 0.03f, 2);
            Plugin.Log.LogInfo($"Laboratory at {slot}: studying {AscensionTargets.DisplayName(target.Value)} (paid {paid}g).");
            return go;
        }

        /// <summary>Sells the specimen (the popup's button): its refund, and the chamber is free again.</summary>
        public void Sell()
        {
            if (specimen == null) return;
            GameObject go = specimen.gameObject;
            Tower t = go.GetComponent<Tower>();
            int value = SellValue;
            if (t != null && TowerManager.instance != null)
            {
                // Vanilla Tower.Demolish, not the subclass's (the Radar's destroys a plane a specimen never had): the
                // flyweight refunds the latest copy's price, as for any tower.
                TowerManager.instance.RemoveTower(t, t.towerType);
                Destroy(go);
            }
            else
            {
                if (ResourceManager.instance != null) ResourceManager.instance.AddMoney(value);
                Destroy(go);
            }
            Plugin.Log.LogInfo($"Laboratory at {slot}: sold the {AscensionTargets.DisplayName(specimen.Target)} for {value}g.");
            specimen = null;
            Emptied();
            popup.Close();
        }

        private void Emptied()
        {
            occupied = false;
            Record.Set("specimen", 0);
            chamber.SetActive(false);
            SetModel(ModelIdle);
            SoundLibrary.Play(SoundPowerDown, slot, 0.9f, 0.03f, 2);
        }

        /// <summary>Where the beams aim and how high the scanning ring travels: the specimen's renderers.</summary>
        private void MeasureSpecimen()
        {
            specimenCentre = slot + Vector3.up * 0.8f;
            specimenHeight = 1.6f;
            if (specimen == null) return;
            Renderer[] rs = specimen.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && !(r is ParticleSystemRenderer)).ToArray();
            if (rs.Length == 0) return;
            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs) b.Encapsulate(r.bounds);
            specimenHeight = Mathf.Clamp(b.max.y - slot.y, 0.6f, 3f);
            specimenCentre = slot + Vector3.up * Mathf.Clamp(specimenHeight * 0.5f, 0.4f, 1.2f);
        }

        // ------------------------------------------------------------------ research

        private void OnWaveStarted(int level)
        {
            if (specimen == null || !Running) return;
            LabResearch.Advance(specimen.Target);
            Record.Set("waves", WavesStudied + 1);
            flash = 1.4f;
            SoulFx.GlowBurst(specimenCentre, Cyan, 22, 2f, 0.13f, 0.7f, 0.5f);
            SoundLibrary.Play(SoundResearch, slot, 1f, 0.02f, 2);
            GameHelpers.FloatingText(slot + Vector3.up * (specimenHeight + 0.6f),
                $"{AscensionTargets.DisplayName(specimen.Target)}: {LabResearch.Describe(specimen.Target)}", "Green", 1f, 1.6f);
            if (popup.IsOpen) popup.Refresh();
        }

        // ------------------------------------------------------------------ looks

        private void BuildFx()
        {
            fx = new GameObject("TR_LabFx").transform;
            fx.SetParent(structure.transform, false);
            Material beamMat = AccelFx.Additive("LabBeam", null, Color.white);
            beams = new LineRenderer[Orbs.Length];
            orbGlows = new MeshRenderer[Orbs.Length];
            for (int i = 0; i < Orbs.Length; i++)
            {
                var go = new GameObject("TR_LabBeam" + i);
                go.transform.SetParent(fx, false);
                LineRenderer lr = go.AddComponent<LineRenderer>();
                lr.sharedMaterial = beamMat;
                lr.useWorldSpace = true;
                lr.positionCount = 2;
                lr.textureMode = LineTextureMode.Stretch;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.enabled = false;
                beams[i] = lr;
                orbGlows[i] = SoulFx.GlowBillboard("TR_LabOrb" + i, fx, Cyan, 0.55f);
                orbGlows[i].transform.position = slot + VoxelLocal(Orbs[i]);
                orbGlows[i].enabled = false;
            }
            scanRing = AccelFx.MeshObject("LabScan", fx, AccelFx.Disc(0.66f, 0.8f, 48, 2, (r, a) => Color.white * (1f - Mathf.Abs(r - 0.5f) * 2f)), out scanMat, additive: true);
            floorRing = AccelFx.MeshObject("LabFloorRing", fx, AccelFx.Disc(0.84f, 1.0f, 64, 2, (r, a) => Color.white * (1f - Mathf.Abs(r - 0.5f) * 2f)), out floorMat, additive: true);
            floorRing.transform.position = slot + Vector3.up * 0.12f;
            scanRing.SetActive(false);
            floorRing.SetActive(false);
            bubbles = SoulFx.Particles("TR_LabBubbles", fx, Fluid, world: false);
            bubbles.transform.position = slot + VoxelLocal(TankBase);
            bubbles.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            var main = bubbles.main;
            main.startLifetime = 2.6f;
            main.startSpeed = 0.42f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
            var shape = bubbles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.24f;
            var emission = bubbles.emission;
            emission.rateOverTime = 7f;
        }

        private void Update()
        {
            if (Record == null) return;
            if (occupied && specimen == null) { occupied = false; Emptied(); } // destroyed from elsewhere
            bool active = specimen != null;
            power = Mathf.MoveTowards(power, active ? 1f : 0f, Time.deltaTime / 0.8f);
            flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 1.6f);
            UpdateFx();
            if (popup != null && popup.IsOpen && (refreshTimer -= Time.deltaTime) <= 0f)
            {
                refreshTimer = 0.5f;
                popup.Refresh();
            }
        }

        private void UpdateFx()
        {
            if (fx == null) return;
            float t = Time.time, p = power, on = p > 0.01f ? 1f : 0f;
            for (int i = 0; i < beams.Length; i++)
            {
                LineRenderer lr = beams[i];
                lr.enabled = on > 0f;
                orbGlows[i].enabled = on > 0f;
                if (on <= 0f) continue;
                Vector3 from = slot + VoxelLocal(Orbs[i]);
                // The beams reach out as the lab powers up, then hum (each flickers on its own).
                Vector3 to = Vector3.Lerp(from, specimenCentre, Mathf.Clamp01(p * 1.4f));
                lr.SetPosition(0, from);
                lr.SetPosition(1, to);
                float hum = 0.75f + 0.25f * Mathf.Sin(t * 11f + i * 1.7f) * Mathf.Sin(t * 3.1f + i);
                float w = 0.05f * p * (hum + flash * 0.8f);
                lr.startWidth = w * 1.4f;
                lr.endWidth = w * 0.7f;
                Color c = Cyan * (0.55f * p * hum + 0.5f * flash);
                lr.startColor = c;
                lr.endColor = c * 0.7f;
                SoulFx.SetColor(orbGlows[i], new Color(Cyan.r, Cyan.g, Cyan.b, Mathf.Clamp01(0.5f * p + 0.3f * hum * p + 0.4f * flash)));
            }
            scanRing.SetActive(on > 0f);
            floorRing.SetActive(on > 0f);
            if (on > 0f)
            {
                // The scanning ring rides up and down the specimen.
                float u = 0.5f - 0.5f * Mathf.Cos(t * 2f * Mathf.PI / 2.8f);
                scanRing.transform.position = slot + Vector3.up * (0.12f + u * (specimenHeight - 0.1f));
                scanMat.color = Cyan * (0.6f * p + 0.6f * flash);
                floorMat.color = Cyan * (p * (0.45f + 0.15f * Mathf.Sin(t * 4f)) + 0.6f * flash);
            }
            if (IsEmitting(bubbles) != (on > 0f))
            {
                if (on > 0f) bubbles.Play(); else bubbles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private static bool IsEmitting(ParticleSystem ps) => ps != null && ps.isEmitting;

        // ------------------------------------------------------------------ dev tools

        /// <summary>One wave of research now, as if a wave had started (dev console, scenarios).</summary>
        public bool ForceResearch()
        {
            if (specimen == null) return false;
            OnWaveStarted(GameHelpers.Level);
            return true;
        }
    }

    /// <summary>
    /// A tower or building in a <see cref="Laboratory"/>'s chamber: only a model. Its behaviours and colliders are off, its
    /// stats aren't computed (no plane for a Radar), it isn't counted as a tower by the mod (tower counts, neighbours,
    /// "on build" effects), ascensions don't attach to it. A tower still sits in its flyweight, so it counts for the price.
    /// </summary>
    public sealed class LabSpecimen : MonoBehaviour
    {
        private static readonly HashSet<GameObject> objects = new HashSet<GameObject>();

        public Laboratory Lab;
        public AscensionTarget Target;
        /// <summary>What was paid for it (a building's refund).</summary>
        public int Paid;

        private void Awake() => objects.Add(gameObject);
        private void OnDestroy() => objects.Remove(gameObject);

        public static bool Is(Component c) => c != null && objects.Contains(c.gameObject);
        public static bool Is(GameObject go) => go != null && objects.Contains(go);
    }
}

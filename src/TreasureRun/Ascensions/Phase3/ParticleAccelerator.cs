using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TreasureRun.Ascensions.Phase2;
using TreasureRun.Audio;
using TreasureRun.Core;
using TreasureRun.Treasures;
using TreasureRun.Voxels;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TreasureRun.Ascensions.Phase3
{
    /// <summary>
    /// Particle Accelerator (Particle Cannon, Legendary). Every Particle Cannon gets this component; exactly one of them
    /// is the <b>core</b> (the highest level when the ascension is gained or the core is lost; "Make Core" moves the role),
    /// every other one is a <b>capacitor</b>: it stops attacking and adds its damage to the black hole. The charge and
    /// the firing live in <see cref="AcceleratorGrid"/>; the black hole is <see cref="BlackHole"/>
    /// (<see cref="ParticleAcceleratorLogic"/>).
    /// <para>
    /// The core shows its own barrel model (<c>accelerator_barrel</c>) on the turret instead of the vanilla top: a huge
    /// barrel on a turntable with cables up from it, a charge window along its top that fills with light, and a
    /// singularity muzzle. Its base has a ring accelerator and Ley sockets on all four sides. A capacitor is the same
    /// parts rearranged, all in its base model (<c>capacitor_base</c>, the vanilla top hidden): the barrel stood on end
    /// as a battery cell with a + terminal, its base sunk so the ring lies on the floor. Changing roles plays the
    /// transformation (<see cref="MorphPlayer"/>).
    /// </para>
    /// </summary>
    internal sealed class AcceleratorCannon : AscendedTower
    {
        public const string Id = "particle_accelerator";
        internal static readonly List<AcceleratorCannon> All = new List<AcceleratorCannon>();

        public bool IsCore { get; private set; }
        public override string DisplayName => IsCore ? null : "Capacitor";

        private MeshFilter baseArt, topArt;
        private MeshRenderer baseRenderer, topRenderer;
        private GameObject barrel;
        private Transform chargeBar;
        private Material barMat;
        private bool roleSet;
        private MorphPlayer player;
        /// <summary>Turning the barrel back to straight ahead before it transforms (-1 = not turning).</summary>
        private float align = -1f;
        private Quaternion alignFrom;
        /// <summary>Turning back takes 0.25 s at normal speed (scaled like the steps, <see cref="MorphData.TimeScale"/>).</summary>
        private static float AlignSeconds => 0.25f * (MorphData.Load()?.TimeScale ?? 1f);
        /// <summary>A core ⇄ capacitor transformation is running (scenarios; capacitors and the core don't aim meanwhile).</summary>
        internal bool Morphing => player != null || align >= 0f;
        /// <summary>Length of the whole transformation (seconds), from the precomputed steps.</summary>
        internal static float MorphSeconds => (MorphData.Load()?.Seconds ?? 0f) + AlignSeconds;

        /// <summary>The barrel canvas (VoxTool AscensionRecipesAccel: 14 x 18 x 17 voxels at 0.1, centred).</summary>
        private const int BarrelX = 14, BarrelY = 18;
        /// <summary>Height of the pedestal top (z 9 of the base, 1/16 tile per voxel): the turntable sits there.</summary>
        private const float PedestalTop = 9f / 16f;

        public static void Register()
        {
            AscensionRegistry.Register(new AscensionDef
            {
                Id = Id, Name = "Particle Accelerator", Rarity = Rarity.Legendary, Kind = AscensionKind.Replace,
                Target = AscensionTarget.ParticleCannon, Component = typeof(AcceleratorCannon), CardModel = "ascensions/particle/accelerator_card",
                Description = "Bends the fabric of space itself. Your strongest Particle Cannon becomes the Accelerator: during waves " +
                              $"it charges {ParticleAcceleratorLogic.ShotCost:0} mana at up to {ParticleAcceleratorLogic.BaseDrain:0} per second " +
                              $"(never below {ParticleAcceleratorLogic.ManaFloor:P0}), then fires a black hole that pulls in every monster near its target for " +
                              $"{ParticleAcceleratorLogic.HoleSeconds:0} s and bursts. Attack speed makes the hole hit faster. Every other Particle Cannon " +
                              "becomes a Capacitor: it stops firing and adds its damage to the black hole.",
                OnAcquire = (run, restoring) => AcceleratorGrid.Ensure(),
            });
            TreasureRunMode.RunStarted += _ => BlackHole.Held.Clear();
        }

        protected override void OnAscend()
        {
            SwapVanillaMesh("BaseArt", "ascensions/particle/capacitor_base");
            baseArt = Phase2Kit.PartFilter(gameObject, "BaseArt", "TR_ascensions/particle/capacitor_base") ?? FindArt("BaseArt");
            // The vanilla top is never shown: the capacitor is all in its base model, the core has its barrel.
            topArt = FindArt("TurretArt");
            baseRenderer = baseArt != null ? baseArt.GetComponent<MeshRenderer>() : null;
            topRenderer = topArt != null ? topArt.GetComponent<MeshRenderer>() : null;
            All.Add(this);
            AcceleratorGrid.Ensure();
            AcceleratorGrid.MarkDirty();
        }

        /// <summary>The swapped art under a part (by path, whatever mesh it shows now).</summary>
        private MeshFilter FindArt(string part)
        {
            foreach (MeshFilter f in GetComponentsInChildren<MeshFilter>(true))
                for (Transform t = f.transform; t != null && t != transform; t = t.parent)
                    if (t.name == part && !f.name.StartsWith("TR_")) return f;
            return null;
        }

        protected override void OnDestroy()
        {
            All.Remove(this);
            AcceleratorGrid.MarkDirty();
            StopMorph();
            if (barMat != null) Destroy(barMat);
            if (topRenderer != null) topRenderer.enabled = true;
            if (baseRenderer != null) baseRenderer.enabled = true;
            base.OnDestroy(); // removes the barrel (an added model) and gives the vanilla meshes back
        }

        /// <summary>
        /// Switches between core (ring base and the barrel) and capacitor (battery cell). The first role is set at once;
        /// a later change plays the transformation (<see cref="MorphPlayer"/>, computed by VoxTool): the real models are
        /// hidden while the parts move. A core turns its barrel back to straight ahead first, the way the steps are laid
        /// out.
        /// </summary>
        internal void SetRole(bool core)
        {
            bool change = roleSet && core != IsCore;
            roleSet = true;
            IsCore = core;
            if (core && barrel == null) BuildBarrel();
            StopMorph();
            if (!change || MorphData.Load() == null) { ApplyRole(); return; }
            Transform turret = Tower != null && Tower.turret != null ? Tower.turret.transform : null;
            if (!core && turret != null && Quaternion.Angle(turret.localRotation, Quaternion.identity) > 1f)
            {
                align = 0f;
                alignFrom = turret.localRotation;
                SoundLibrary.Play("ascensions/particle_accelerator/accel_servo", transform.position, 0.6f, 0.03f, 3);
            }
            else StartMorph();
        }

        private void StartMorph()
        {
            align = -1f;
            if (Tower != null && Tower.turret != null) Tower.turret.transform.localRotation = Quaternion.identity;
            if (baseRenderer != null) baseRenderer.enabled = false;
            if (topRenderer != null) topRenderer.enabled = false;
            if (barrel != null) barrel.SetActive(false);
            player = MorphPlayer.Play(transform, IsCore, () =>
            {
                player = null;
                ApplyRole();
                LeyNetwork.MarkDirty(); // a new core takes its Ley pipes once it stands
            });
            if (player == null) ApplyRole();
        }

        private void StopMorph()
        {
            align = -1f;
            if (player != null) Destroy(player.gameObject);
            player = null;
        }

        /// <summary>The finished look of the current role (the real models).</summary>
        private void ApplyRole()
        {
            Show(baseArt, baseRenderer, IsCore ? "ascensions/particle/accelerator_base" : "ascensions/particle/capacitor_base");
            if (baseRenderer != null) baseRenderer.enabled = true;
            if (topRenderer != null) topRenderer.enabled = false;
            if (barrel != null) barrel.SetActive(IsCore);
            // A capacitor's turret stays straight ahead (it doesn't aim; the transformation starts from there).
            if (!IsCore && Tower != null && Tower.turret != null) Tower.turret.transform.localRotation = Quaternion.identity;
        }

        private void Update()
        {
            if (align < 0f || Tower == null || Tower.turret == null) return;
            align = Mathf.Min(1f, align + Time.deltaTime / AlignSeconds);
            float e = align * align * (3f - 2f * align);
            Tower.turret.transform.localRotation = Quaternion.Slerp(alignFrom, Quaternion.identity, e);
            if (align >= 1f) StartMorph();
        }

        private static void Show(MeshFilter f, MeshRenderer r, string model)
        {
            VoxAsset asset = VoxelLibrary.Get(model);
            if (asset == null || f == null) return;
            f.sharedMesh = asset.Mesh;
            if (r != null) r.sharedMaterial = asset.Material;
        }

        /// <summary>The barrel on the turret (it turns with the vanilla aim), its turntable on the pedestal, and the charge bar.</summary>
        private void BuildBarrel()
        {
            Transform turret = Tower.turret != null ? Tower.turret.transform : transform;
            float lift = PedestalTop - (turret.position.y - transform.position.y);
            barrel = ShowModel("ascensions/particle/accelerator_barrel", turret, new Vector3(0f, lift, 0f), null, 0.625f);
            if (barrel == null) return;
            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(bar.GetComponent<Collider>());
            bar.name = "TR_ChargeBar";
            bar.transform.SetParent(barrel.transform, false);
            barMat = AccelFx.Glow("ChargeBar", null, new Color(0.75f, 0.4f, 1f, 1f));
            var r = bar.GetComponent<MeshRenderer>();
            r.sharedMaterial = barMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            chargeBar = bar.transform;
            SetCharge(0f, false);
        }

        /// <summary>
        /// Fills the charge window (y 3..14 of the barrel, from the back) with violet light, white-hot and pulsing when
        /// full.
        /// </summary>
        internal void SetCharge(float k, bool full)
        {
            if (chargeBar == null) return;
            k = Mathf.Clamp01(k);
            float len = 1.2f * Mathf.Max(k, 0.02f);
            chargeBar.localPosition = new Vector3(0f, 1.55f, -0.6f + len / 2f);
            chargeBar.localScale = new Vector3(0.36f, 0.08f, len);
            Color c = Color.Lerp(new Color(0.55f, 0.25f, 0.95f), new Color(0.95f, 0.8f, 1f), k);
            if (full) c = Color.Lerp(new Color(0.95f, 0.85f, 1f), Color.white, 0.5f + 0.5f * Mathf.Sin(Time.time * 10f));
            barMat.color = c;
        }

        /// <summary>A point of the barrel model (canvas voxel coordinates, x/y/z with z up) in world space.</summary>
        internal Vector3 BarrelPoint(float vx, float vy, float vz) =>
            barrel != null ? barrel.transform.TransformPoint(new Vector3((vx + 0.5f) * 0.1f - BarrelX * 0.05f, (vz + 0.5f) * 0.1f, (vy + 0.5f) * 0.1f - BarrelY * 0.05f))
                           : transform.position + Vector3.up;

        /// <summary>The middle of the singularity muzzle.</summary>
        internal Vector3 Muzzle => BarrelPoint(6.5f, 17.6f, 10.5f);

        /// <summary>
        /// The four cables (as in VoxTool's CablePaths): turntable socket → bulge → barrel flank, in canvas voxels.
        /// </summary>
        internal static readonly Vector3[][] Cables = MakeCables();

        private static Vector3[][] MakeCables()
        {
            var list = new List<Vector3[]>();
            foreach (int side in new[] { 0, 1 })
                foreach (var (y0, y1, y2) in new[] { (5f, 4f, 3f), (12f, 13f, 14f) })
                {
                    float X(float x) => side == 0 ? x : 13f - x;
                    list.Add(new[] { new Vector3(X(2.5f), y0, 1.5f), new Vector3(X(0.5f), y1, 4.5f), new Vector3(X(0.5f), y2, 7.5f), new Vector3(X(1.6f), y2, 10.5f) });
                }
            return list.ToArray();
        }

        /// <summary>A point a fraction <paramref name="s"/> of the way up cable <paramref name="i"/>, in world space.</summary>
        internal Vector3 CablePoint(int i, float s)
        {
            Vector3[] p = Cables[i];
            float seg = Mathf.Clamp(s, 0f, 0.999f) * (p.Length - 1);
            int j = (int)seg;
            Vector3 v = Vector3.Lerp(p[j], p[j + 1], seg - j);
            return BarrelPoint(v.x, v.y, v.z);
        }

        internal CannonStats Stats()
        {
            Tower t = Tower;
            return new CannonStats(t.level, t.damage, t.healthDamage, t.armorDamage, t.shieldDamage);
        }

        /// <summary>Centre of the ring accelerator (z 7..8 of the base) and its radius (6 voxels), in world space.</summary>
        internal Vector3 RingCentre => baseArt != null ? baseArt.transform.TransformPoint(new Vector3(0f, 0.8f, 0f)) : transform.position + Vector3.up * 0.5f;
        internal float RingRadius => baseArt != null ? 0.6f * baseArt.transform.lossyScale.x : 0.375f;

        /// <summary>The tip of the capacitor's + terminal (z 23 of its model, over the middle).</summary>
        internal Vector3 Emitter => baseArt != null ? baseArt.transform.TransformPoint(new Vector3(0f, 2.45f, 0f)) : transform.position + Vector3.up;
    }

    /// <summary>
    /// The one Accelerator of the run: picks the core and charges it during waves: 60 mana per second, never below 25%,
    /// full after 30 s of that (with less mana it takes longer, never shorter). Ley Line networks plugged into the core
    /// add their income to the drain and charge it faster (<see cref="LeyNetwork.AcceleratorIncome"/>). It fires the
    /// black hole when full and a target is in range, and draws the charge (bunches racing round the ring, energy running
    /// up the cables, the barrel's charge window, a light, a rising whine) and each capacitor's pulse: a packet of energy
    /// arcs over to the core's ring, flashes, and a return pulse arcs back.
    /// </summary>
    internal sealed class AcceleratorGrid : MonoBehaviour
    {
        public static AcceleratorGrid Instance { get; private set; }
        private static bool dirty = true;

        public readonly AcceleratorCharge Charge = new AcceleratorCharge();
        public AcceleratorCannon Core { get; private set; }
        /// <summary>Black holes fired so far (scenarios).</summary>
        public int Fired { get; private set; }
        /// <summary>Whether the core is draining right now (scenarios, popup).</summary>
        public bool Draining { get; private set; }
        /// <summary>Mana drained so far (scenarios).</summary>
        public long Drained { get; private set; }
        /// <summary>Mana is at the 25% floor (within a second of drain): the popup says so.</summary>
        public bool LowMana { get; private set; }
        public PooledDamage Pool { get; private set; }
        /// <summary>Income of the Ley Line networks plugged into the core (added to the drain).</summary>
        public static int LeyIncome => LeyNetwork.AcceleratorIncome;

        /// <summary>The core's cell for the Ley Line Network (only the core takes pipes, never a capacitor).</summary>
        public static LeyCell? CoreCell
        {
            get
            {
                AcceleratorCannon c = Instance != null ? Instance.Core : null;
                // Not while it transforms: the sockets come up out of the ground first (it marks Ley dirty when done).
                if (c == null || c.Morphing) return null;
                Vector3 p = c.transform.position;
                return new LeyCell(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.z), Mathf.RoundToInt(p.y * 3f));
            }
        }

        private static readonly Color Bunch = new Color(0.85f, 0.65f, 1f, 1f), Packet = new Color(0.9f, 0.7f, 1f, 1f),
            Return = new Color(0.55f, 0.85f, 1f, 1f), ArcColor = new Color(0.7f, 0.45f, 1f, 0.14f);
        private QuadBatch bunches, packets;
        private Light glow;
        private SoundLoop whine;
        private readonly Dictionary<AcceleratorCannon, (LineRenderer outArc, LineRenderer backArc)> links =
            new Dictionary<AcceleratorCannon, (LineRenderer, LineRenderer)>();
        private float spin, cycle;
        private AcceleratorCannon lastCore;
        private const int ArcPoints = 14;

        public static void Ensure()
        {
            if (!TreasureRunMode.Active || Instance != null) return;
            Instance = new GameObject("TR_AcceleratorGrid").AddComponent<AcceleratorGrid>();
            dirty = true;
        }

        public static void MarkDirty() => dirty = true;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (whine != null) Destroy(whine.gameObject);
            if (glow != null) Destroy(glow.gameObject);
            bunches?.Destroy();
            packets?.Destroy();
            foreach (var (a, b) in links.Values) { if (a != null) Destroy(a.gameObject); if (b != null) Destroy(b.gameObject); }
            LeyNetwork.MarkDirty();
        }

        /// <summary>Moves the core role (the charge stays with the grid, so it carries over).</summary>
        public void MakeCore(AcceleratorCannon c)
        {
            if (c == null || c == Core) return;
            Core = c;
            foreach (AcceleratorCannon a in All()) a.SetRole(a == Core);
            CoreChanged();
        }

        private void CoreChanged()
        {
            // Reference comparison: a demolished core is "== null" to Unity, so the plain == would miss that it is gone.
            if (ReferenceEquals(Core, lastCore)) return;
            lastCore = Core;
            if (glow != null && Core != null) glow.transform.SetParent(Core.transform, false);
            if (whine != null) { Destroy(whine.gameObject); whine = null; }
            LeyNetwork.MarkDirty(); // the Ley pipes plug into the new core
        }

        private static List<AcceleratorCannon> All() => AcceleratorCannon.All.Where(a => a != null && a.Tower != null).ToList();

        private void Update()
        {
            AscensionDef owned = AscensionRegistry.Owned(AscensionTarget.ParticleCannon);
            if (owned == null || owned.Id != AcceleratorCannon.Id) { Destroy(gameObject); return; }
            List<AcceleratorCannon> all = All();
            if (dirty || Core == null || !all.Contains(Core))
            {
                dirty = false;
                if (Core == null || !all.Contains(Core))
                {
                    // The strongest becomes the core (on pick, or when the core is demolished: it keeps the charge).
                    int i = ParticleAcceleratorLogic.PickCore(all.Select(a => a.Stats()).ToList());
                    Core = i >= 0 ? all[i] : null;
                    LeyNetwork.MarkDirty();
                }
                foreach (AcceleratorCannon a in all) a.SetRole(a == Core);
            }
            CoreChanged();
            if (Core == null) { Draining = false; ShowCharge(false); DrawLinks(all, false); return; }

            Tower t = Core.Tower;
            Pool = ParticleAcceleratorLogic.Pool(all.Select(a => a.Stats()).ToList());
            bool combat = SpawnManager.instance != null && SpawnManager.instance.combat;
            Draining = false;
            ResourceManager rm = ResourceManager.instance;
            int ley = LeyIncome;
            LowMana = rm != null && rm.mana < ParticleAcceleratorLogic.ManaFloor * rm.maxMana + ParticleAcceleratorLogic.Drain(ley) + 1f;
            if (combat && rm != null && !Charge.Full)
            {
                int spend = Charge.Step(Time.deltaTime, rm.mana, rm.maxMana, ley);
                if (spend > 0) { rm.SpendMana(spend); Drained += spend; }
                Draining = !Charge.Starved;
            }
            if (Charge.Full && combat && !Core.Morphing)
            {
                Enemy target = Phase2Kit.Target(t);
                if (Combat.IsAlive(target)) Fire(target);
            }
            ShowCharge(combat);
            DrawLinks(all, combat);
        }

        /// <summary>The core's attack speed factor: its vanilla interval over its current one (cards, treasures, buffs).</summary>
        private static float AttackSpeed(Tower t) => t.rps > 0f && t.rpm > 0f ? Mathf.Clamp(60f / t.rpm / t.rps, 0.1f, 10f) : 1f;

        private void Fire(Enemy target)
        {
            Tower t = Core.Tower;
            Vector3 from = Core.Muzzle;
            float flight = Mathf.Clamp(Vector3.Distance(from, target.transform.position) / 26f, 0.18f, 0.8f);
            Vector3 aim = target.GetFuturePosition(flight);
            if (aim == Vector3.zero) aim = target.transform.position;
            aim.y = target.transform.position.y;
            var stats = new HoleStats
            {
                Pool = Pool, Slow = t.slowPercent, Bleed = t.bleedPercent, Burn = t.burnPercent, Poison = t.poisonPercent,
                Crit = t.critChance, Stun = t.stunChance, Speed = AttackSpeed(t),
            };
            SingularityShot.Launch(from, aim, flight, () => BlackHole.Open(aim, stats));
            SoundLibrary.Play("ascensions/particle_accelerator/accel_fire", from, 0.85f, 0.03f, 2);
            Fx.Burst(from, new Color(0.75f, 0.45f, 1f), 26, 0.1f, 3f);
            Charge.Reset();
            Fired++;
        }

        /// <summary>
        /// The core's charge: bunches racing round the ring (more and faster), energy running up the cables to the barrel
        /// while it drains, the charge window filling, a light at the muzzle and the whine rising.
        /// </summary>
        private void ShowCharge(bool combat)
        {
            if (Core == null) { bunches?.Clear(); if (glow != null) glow.intensity = 0f; return; }
            float k = Charge.Progress;
            bool morphing = Core.Morphing;
            Core.SetCharge(k, Charge.Full);
            if (glow == null)
            {
                var go = new GameObject("TR_AcceleratorGlow");
                go.transform.SetParent(Core.transform, false);
                glow = go.AddComponent<Light>();
                glow.type = LightType.Point;
                glow.color = new Color(0.7f, 0.4f, 1f);
                glow.range = 2.5f;
            }
            glow.transform.position = Core.Muzzle;
            glow.intensity = morphing ? 0f : Charge.Full ? 1.8f + 0.6f * Mathf.Sin(Time.time * 9f) : 1.5f * k;

            // Whine: rises in pitch as it fills; quiet hum while holding a full charge; silent between waves.
            bool sound = combat && (Draining || Charge.Full);
            // Two sources in sync so the volume can pass 1 (+15 dB over the first version after two playtests).
            if (sound && whine == null) whine = SoundLoop.Start("ascensions/particle_accelerator/accel_charge", Core.transform, 1f, 0f, 2);
            else if (!sound && whine != null) { Destroy(whine.gameObject); whine = null; }
            if (whine != null)
            {
                whine.Pitch = Charge.Full ? 1.65f : 0.7f + 0.9f * k;
                whine.Volume = Charge.Full ? 0.57f : 0.68f + 0.68f * k;
            }

            if (bunches == null) bunches = new QuadBatch("TR_AcceleratorBunches", AccelFx.Dot);
            if (morphing) { bunches.Clear(); return; }
            float speed = 2.5f + 16f * k;
            spin += speed * Time.deltaTime;
            int count = 1 + Mathf.RoundToInt(5f * k);
            Vector3 c = Core.RingCentre;
            float r = Core.RingRadius;
            bunches.Begin();
            for (int i = 0; i < count; i++)
            {
                float a0 = spin + i * Mathf.PI * 2f / count;
                for (int j = 0; j < 5; j++) // the bunch and its fading trail
                {
                    float a = a0 - j * 0.12f * (0.5f + k);
                    var p = c + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                    Color col = Bunch;
                    col.a = (1f - j / 5f) * (0.35f + 0.65f * Mathf.Max(k, 0.2f));
                    bunches.Add(p, (0.08f - j * 0.009f) * (0.8f + 0.4f * k), col);
                }
            }
            // Energy running up the four cables into the barrel (while draining; slowly glowing when full).
            if (Draining || (Charge.Full && combat))
                for (int i = 0; i < AcceleratorCannon.Cables.Length; i++)
                    for (int j = 0; j < 2; j++)
                    {
                        float s = Mathf.Repeat(Time.time * (Charge.Full ? 0.5f : 1.4f) + j * 0.5f + i * 0.23f, 1f);
                        for (int tr = 0; tr < 4; tr++)
                        {
                            Color col = Bunch;
                            col.a = (1f - tr / 4f) * 0.95f;
                            bunches.Add(Core.CablePoint(i, s - tr * 0.05f), 0.075f - tr * 0.012f, col);
                        }
                    }
            bunches.End();
        }

        private static Vector3 Bezier(Vector3 a, Vector3 c, Vector3 b, float t) =>
            (1f - t) * (1f - t) * a + 2f * (1f - t) * t * c + t * t * b;

        /// <summary>
        /// Each capacitor's pulse, looping: a bright packet arcs high from its emitter to the core's ring (where it hits,
        /// the ring flashes), then a smaller, bluer return pulse arcs back low and the emitter flashes. Faster while the
        /// core drains; slow and dim otherwise. Two faint arcs show the circuit.
        /// </summary>
        private void DrawLinks(List<AcceleratorCannon> all, bool combat)
        {
            foreach (AcceleratorCannon dead in links.Keys.Where(a => a == null || a == Core || !all.Contains(a)).ToList())
            {
                var (o, b) = links[dead];
                if (o != null) Destroy(o.gameObject);
                if (b != null) Destroy(b.gameObject);
                links.Remove(dead);
            }
            if (packets == null) packets = new QuadBatch("TR_AcceleratorPackets", AccelFx.Dot);
            packets.Begin();
            if (Core != null)
            {
                float period = Draining ? 1.3f : 2.8f;
                cycle += Time.deltaTime / period;
                float dim = Draining || (combat && Charge.Full) ? 1f : 0.55f;
                Vector3 ring = Core.RingCentre;
                int n = 0;
                foreach (AcceleratorCannon a in all)
                {
                    if (a == Core) continue;
                    if (!links.TryGetValue(a, out var arcs) || arcs.outArc == null)
                    {
                        arcs = (Arc(), Arc());
                        links[a] = arcs;
                    }
                    // No circuit while either end transforms.
                    bool off = a.Morphing || Core.Morphing;
                    arcs.outArc.enabled = arcs.backArc.enabled = !off;
                    if (off) continue;
                    Vector3 e = a.Emitter;
                    Vector3 flat = a.transform.position - Core.transform.position;
                    flat.y = 0f;
                    Vector3 dir = flat.sqrMagnitude > 1e-4f ? flat.normalized : Vector3.forward;
                    Vector3 hit = ring + dir * Core.RingRadius;
                    float dist = Vector3.Distance(e, hit);
                    Vector3 side = Vector3.Cross(Vector3.up, dir) * 0.18f;
                    Vector3 cOut = (e + hit) / 2f + Vector3.up * (0.35f + 0.12f * dist) + side;
                    Vector3 cBack = (e + hit) / 2f + Vector3.up * (0.12f + 0.05f * dist) - side;
                    for (int i = 0; i < ArcPoints; i++)
                    {
                        float t = i / (ArcPoints - 1f);
                        arcs.outArc.SetPosition(i, Bezier(e, cOut, hit, t));
                        arcs.backArc.SetPosition(i, Bezier(hit, cBack, e, t));
                    }
                    Color arc = ArcColor;
                    arc.a *= dim;
                    arcs.outArc.startColor = arcs.outArc.endColor = arc;
                    arcs.backArc.startColor = arcs.backArc.endColor = arc * new Color(0.8f, 1f, 1f, 0.8f);

                    float phase = Mathf.Repeat(cycle + n * 0.37f, 1f);
                    if (phase < 0.45f)
                    {
                        float s = phase / 0.45f;
                        for (int tr = 0; tr < 6; tr++)
                        {
                            Color col = Packet;
                            col.a = (1f - tr / 6f) * dim;
                            packets.Add(Bezier(e, cOut, hit, Mathf.Max(0f, s - tr * 0.035f)), 0.13f - tr * 0.015f, col);
                        }
                    }
                    else if (phase < 0.55f)
                    {
                        Color col = Color.white;
                        col.a = (1f - Mathf.Abs(phase - 0.5f) / 0.05f) * dim;
                        packets.Add(hit, 0.35f, col);
                    }
                    else
                    {
                        float s = (phase - 0.55f) / 0.45f;
                        for (int tr = 0; tr < 5; tr++)
                        {
                            Color col = Return;
                            col.a = (1f - tr / 5f) * 0.8f * dim;
                            packets.Add(Bezier(hit, cBack, e, Mathf.Max(0f, s - tr * 0.035f)), 0.09f - tr * 0.012f, col);
                        }
                        if (s > 0.9f)
                        {
                            Color col = Color.white;
                            col.a = (s - 0.9f) / 0.1f * dim;
                            packets.Add(e, 0.28f, col);
                        }
                    }
                    n++;
                }
            }
            packets.End();
        }

        private LineRenderer Arc()
        {
            LineRenderer line = Phase2Kit.Line("CapacitorArc", ArcColor, 0.03f, transform);
            line.positionCount = ArcPoints;
            return line;
        }
    }

    [HarmonyPatch]
    internal static class ParticleAcceleratorPatches
    {
        /// <summary>The core and the capacitors never fire vanilla shots.</summary>
        [HarmonyPrefix, HarmonyPatch(typeof(Tower), nameof(Tower.Fire))]
        private static bool TowerFirePrefix(Tower __instance) =>
            __instance.towerType != TowerType.ParticleCannon || __instance.GetComponent<AcceleratorCannon>() == null;

        /// <summary>A capacitor doesn't aim: its coil stands upright (only the core's barrel follows the target).</summary>
        [HarmonyPrefix, HarmonyPatch(typeof(Tower), nameof(Tower.AimTurret))]
        private static bool AimTurretPrefix(Tower __instance)
        {
            if (__instance.towerType != TowerType.ParticleCannon) return true;
            AcceleratorCannon a = __instance.GetComponent<AcceleratorCannon>();
            return a == null || (a.IsCore && !a.Morphing);
        }

        /// <summary>Monsters caught in a black hole don't walk.</summary>
        [HarmonyPrefix, HarmonyPatch(typeof(Pathfinder), "FixedUpdate")]
        private static bool PathfinderFixedUpdatePrefix(Pathfinder __instance) =>
            BlackHole.Held.Count == 0 || !BlackHole.Held.Contains(__instance);

        private const string ButtonName = "TR_MakeCoreButton";

        /// <summary>Popup: the charge and drain of the core, a capacitor's share and its "Make Core" button.</summary>
        [HarmonyPostfix, HarmonyPatch(typeof(TowerUI), nameof(TowerUI.SetStats))]
        private static void TowerUiSetStats(TowerUI __instance, Tower _myTower)
        {
            AcceleratorCannon a = _myTower != null ? _myTower.GetComponent<AcceleratorCannon>() : null;
            AcceleratorGrid grid = AcceleratorGrid.Instance;
            if (a == null || grid == null) return;
            // Short lines: the vanilla texts are single lines about as long as "Fire Rate: 12 RPM".
            if (a.IsCore)
            {
                int ley = AcceleratorGrid.LeyIncome;
                float secs = ParticleAcceleratorLogic.SecondsLeft(grid.Charge.Progress, ley);
                string charge = grid.Charge.Full ? "Charge: READY"
                    : $"Charge: {ParticleAcceleratorLogic.ChargedMana(grid.Charge.Progress)}/{ParticleAcceleratorLogic.ShotCost:0}";
                string drain = grid.Charge.Full ? $"Drain: {ParticleAcceleratorLogic.Drain(ley):0}/sec"
                    : grid.LowMana ? "Drain: mana low" : $"Drain: {ParticleAcceleratorLogic.Drain(ley):0}/sec, {Mathf.CeilToInt(secs)} s";
                Set(__instance.rpmText, charge);
                Set(__instance.manaUseText, drain);
            }
            else
            {
                Set(__instance.rpmText, $"Capacitor: +{_myTower.damage}");
                Set(__instance.manaUseText, $"Black hole: {grid.Pool.Base}");
            }
            MakeCoreButton(__instance, a);
        }

        private static void Set(Text text, string value)
        {
            if (text != null) text.text = value;
        }

        /// <summary>"Make Core" on a capacitor's popup (a copy of the demolish button, above it); hidden on the core.</summary>
        private static void MakeCoreButton(TowerUI ui, AcceleratorCannon a)
        {
            if (ui.demolishText == null) return;
            Button demolish = ui.demolishText.GetComponentInParent<Button>();
            if (demolish == null) return;
            Transform existing = demolish.transform.parent.Find(ButtonName);
            if (existing == null)
            {
                GameObject go = Object.Instantiate(demolish.gameObject, demolish.transform.parent, false);
                go.name = ButtonName;
                var rt = (RectTransform)go.transform;
                var src = (RectTransform)demolish.transform;
                rt.anchoredPosition = src.anchoredPosition + new Vector2(0f, src.rect.height * src.localScale.y * 1.15f);
                var button = go.GetComponent<Button>();
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener(() =>
                {
                    SFXManager.instance?.ButtonClick();
                    AcceleratorGrid.Instance?.MakeCore(a);
                    if (a != null && a.Tower != null) ui.SetStats(a.Tower);
                });
                foreach (Text text in go.GetComponentsInChildren<Text>(true)) text.text = "Make Core";
                existing = go.transform;
            }
            existing.gameObject.SetActive(!a.IsCore);
        }
    }
}

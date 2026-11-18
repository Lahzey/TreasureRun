using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TreasureRun.Audio;
using TreasureRun.Core;
using TreasureRun.Treasures;
using UnityEngine;

namespace TreasureRun.Ascensions.Phase3
{
    /// <summary>
    /// Ritual of the Sun (Obelisk, Mythical). Every Obelisk becomes a <b>Sun Obelisk</b>: a sandstone and gold pyramid
    /// with a crystal prism floating over its crown. Sun Obelisks link with beams of light (<see cref="RitualGrid"/>,
    /// rules in <see cref="RitualLogic"/>); alone or in an open chain they fire as usual (a golden beam, +25% burn). In
    /// a closed ring every prism turns to the sky and stops firing (it still gains XP), and the sun strikes the ring's
    /// centre (<see cref="SunRing"/>).
    /// </summary>
    internal sealed class SunObelisk : AscendedTower
    {
        public const string Id = "ritual_of_the_sun";
        internal static readonly List<SunObelisk> All = new List<SunObelisk>();

        /// <summary>The ring it belongs to (null: alone or in a chain).</summary>
        public SunRing Ring { get; internal set; }
        /// <summary>Its place in the ring (counter-clockwise order).</summary>
        public int RingIndex { get; internal set; }
        /// <summary>Beams of light it holds (0 to 2).</summary>
        public int Links { get; internal set; }
        /// <summary>In a ring that hasn't started breaking: no own attacks, the prism points up.</summary>
        public bool InRing => Ring != null && !Ring.Breaking;

        /// <summary>The prism's centre in the world (beams start and end there).</summary>
        public Vector3 Prism => turret != null ? turret.position : transform.position + Vector3.up * PrismHeight;

        public Obelisk Obelisk { get; private set; }

        /// <summary>Height of the prism's centre over the tower's foot (VoxTool SunPrismCentre, 1/16 tile per voxel).</summary>
        private const float PrismHeight = 21.5f / 16f;
        /// <summary>The prism model: 0.1 voxels scaled to half the pyramid's voxels; its centre is 5.5 voxels up.</summary>
        private const float PrismScale = 0.3125f, PrismCentreY = 5.5f * 0.1f * PrismScale;

        private Transform turret;
        private Vector3 turretHome;
        private Renderer beamArt;
        private LightBeam fireBeam;
        private GameObject flare;
        private Material flareMat;
        private float sparkTimer;
        /// <summary>0 = the prism aims at its target, 1 = it points at the sky.</summary>
        private float upBlend;
        private Quaternion restRotation, skyRotation;
        private bool wasUp;

        public static void Register()
        {
            AscensionRegistry.Register(new AscensionDef
            {
                Id = Id, Name = "Ritual of the Sun", TowerName = "Sun Obelisk", Rarity = Rarity.Mythical, Kind = AscensionKind.Augment,
                Target = AscensionTarget.Obelisk, Component = typeof(SunObelisk), CardModel = "ascensions/obelisk/ritual_card",
                Description = "Show your devotion and be rewarded. Obelisks link with beams of light; close a circle and the sun " +
                              "strikes its centre, burning every monster inside with the circle's average damage (100% burn, wider " +
                              $"with more obelisks). Outside circles: {RitualLogic.ObeliskBurn:P0} burn.",
                OnAcquire = (run, restoring) => RitualGrid.Ensure(),
            });
            StatLayer.Register("SunObelisk", (tower, stats) =>
            {
                if (tower.GetComponent<SunObelisk>() != null) stats.Flat(TowerStat.Burn, RitualLogic.ObeliskBurn);
            });
            TreasureRunMode.RunStarted += _ => All.Clear();
        }

        protected override void OnAscend()
        {
            Obelisk = GetComponent<Obelisk>();
            SwapVanillaMesh("BaseArt", "ascensions/obelisk/sun_obelisk");
            if (Tower.turret != null)
            {
                turret = Tower.turret.transform;
                turretHome = turret.localPosition;
                turret.localPosition = new Vector3(turretHome.x, PrismHeight, turretHome.z);
                ShowModel("ascensions/obelisk/sun_prism", turret, new Vector3(0f, -PrismCentreY, 0f), null, PrismScale);
            }
            // The vanilla red beam is replaced by our light (the game still switches it on and off: that is our cue).
            if (Obelisk != null && Obelisk.beam != null)
            {
                beamArt = Obelisk.beam.GetComponentInChildren<Renderer>(true);
                if (beamArt != null) beamArt.enabled = false;
            }
            fireBeam = new LightBeam("SunFire", transform, SunFx.FireCore, SunFx.FireGlow, 0.05f, 0.2f);
            flare = AccelFx.Billboard("SunFireFlare", transform, SunFx.Glint, Color.black, 0.5f, out flareMat, 3010, 0, true);
            flare.SetActive(false);
            All.Add(this);
            RitualGrid.Ensure();
            RitualGrid.MarkDirty();
        }

        protected override void OnDestroy()
        {
            All.Remove(this);
            RitualGrid.MarkDirty();
            if (turret != null) { turret.localPosition = turretHome; }
            if (beamArt != null) beamArt.enabled = true;
            fireBeam?.Destroy();
            if (flare != null) Destroy(flare);
            if (flareMat != null) Destroy(flareMat);
            base.OnDestroy();
        }

        /// <summary>This obelisk's stats for the sun beam.</summary>
        public RitualMemberStats Stats()
        {
            Tower t = Tower;
            return new RitualMemberStats
            {
                Damage = t.damage, HealthMult = t.healthDamage, ArmorMult = t.armorDamage, ShieldMult = t.shieldDamage,
                Slow = t.slowPercent, Bleed = t.bleedPercent, Burn = t.burnPercent, Poison = t.poisonPercent,
                Crit = t.critChance, Stun = t.stunChance, Interval = t.rps,
                ManaRate = t.consumesMana ? t.manaConsumptionRate : 0f,
            };
        }

        /// <summary>Where the obelisk's own beam hits: the middle of the target's body.</summary>
        private static Vector3 HitPoint(GameObject target)
        {
            Collider c = target.GetComponent<Collider>();
            return c != null ? c.bounds.center : target.transform.position + Vector3.up * 0.3f;
        }

        private void LateUpdate()
        {
            if (turret == null) return;
            // The prism turns to the sky when the ignition reaches it (SunRing), and back when the ring breaks.
            bool up = InRing && Ring.PrismUp(RingIndex);
            if (up && !wasUp)
            {
                restRotation = turret.rotation;
                Vector3 f = restRotation * Vector3.forward;
                skyRotation = Quaternion.FromToRotation(f, Vector3.up) * restRotation;
            }
            if (!up && wasUp) restRotation = turret.rotation;
            wasUp = up;
            upBlend = Mathf.MoveTowards(upBlend, up ? 1f : 0f, Time.deltaTime / (up ? 0.35f : 0.3f));
            if (upBlend > 0f || up)
            {
                float e = upBlend * upBlend * (3f - 2f * upBlend);
                // Leaving the ring: from the sky back to wherever the game aims it now.
                Quaternion start = up ? restRotation : turret.rotation;
                turret.rotation = Quaternion.Slerp(start, skyRotation, e);
            }

            // Its own golden beam while the game fires it (alone or in a chain).
            GameObject target = Tower.currentTarget;
            bool firing = !InRing && Obelisk != null && Obelisk.beam != null && Obelisk.beam.activeSelf && target != null;
            fireBeam.SetVisible(firing);
            flare.SetActive(firing);
            if (!firing) return;
            Vector3 from = Prism, to = HitPoint(target);
            float flick = 1f + 0.18f * Mathf.Sin(Time.time * 47f) + 0.1f * Mathf.Sin(Time.time * 113f);
            fireBeam.Set(from, to, flick, 1f);
            flare.transform.position = to;
            AccelFx.Face(flare.transform);
            flare.transform.localScale = Vector3.one * (0.42f * flick);
            flareMat.color = SunFx.FireGlow * 0.9f;
            sparkTimer -= Time.deltaTime;
            if (sparkTimer <= 0f)
            {
                sparkTimer = 0.16f;
                Fx.Burst(to, new Color(1f, 0.72f, 0.25f), 3, 0.05f, 1.1f);
            }
        }
    }

    /// <summary>
    /// The links between all Sun Obelisks (one global object while the ascension is owned). It solves the links again
    /// when an obelisk is built or demolished, and every second (ranges change with cards), keeps a <see cref="LinkBeam"/>
    /// per link and a <see cref="SunRing"/> per closed ring, and draws the light running along the beams.
    /// </summary>
    internal sealed class RitualGrid : MonoBehaviour
    {
        public static RitualGrid Instance { get; private set; }
        private static bool dirty = true;
        private float recheck;

        private readonly Dictionary<(SunObelisk, SunObelisk), LinkBeam> links = new Dictionary<(SunObelisk, SunObelisk), LinkBeam>();
        private readonly List<LinkBeam> fading = new List<LinkBeam>();
        internal readonly List<SunRing> Rings = new List<SunRing>();
        private QuadBatch light;

        /// <summary>The last solution and the obelisks it was solved for (scenarios).</summary>
        public RitualSolution Solution { get; private set; }
        public List<SunObelisk> Nodes { get; private set; } = new List<SunObelisk>();
        public IEnumerable<LinkBeam> Beams => links.Values;

        public static void Ensure()
        {
            if (!TreasureRunMode.Active || Instance != null) return;
            Instance = new GameObject("TR_RitualGrid").AddComponent<RitualGrid>();
            dirty = true;
        }

        public static void MarkDirty() => dirty = true;

        private void Awake()
        {
            light = new QuadBatch("TR_RitualLight", AccelFx.DotGlow, 3020, 0, true);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            foreach (LinkBeam b in links.Values) b.Destroy();
            foreach (LinkBeam b in fading) b.Destroy();
            links.Clear();
            fading.Clear();
            foreach (SunRing r in Rings) if (r != null) Destroy(r.gameObject);
            Rings.Clear();
            foreach (SunObelisk s in SunObelisk.All) if (s != null) { s.Ring = null; s.Links = 0; }
            light?.Destroy();
        }

        private void Update()
        {
            AscensionDef owned = AscensionRegistry.Owned(AscensionTarget.Obelisk);
            if (owned == null || owned.Id != SunObelisk.Id) { Destroy(gameObject); return; }
            recheck -= Time.deltaTime;
            if (dirty || recheck <= 0f) Solve();
            Draw();
        }

        /// <summary>Solves the links again and brings the beams and rings in line with the result.</summary>
        public void Solve()
        {
            dirty = false;
            recheck = 1f;
            Nodes = SunObelisk.All.Where(s => s != null && s.Tower != null).ToList();
            var input = Nodes.Select(s => new RitualNode(s.transform.position.x, s.transform.position.z, s.Tower.range)).ToList();
            RitualSolution sol = RitualLogic.Solve(input);
            Solution = sol;

            // Light runs along chains from their first obelisk to the last, and counter-clockwise round rings.
            var flow = new Dictionary<(SunObelisk, SunObelisk), bool>();
            void Order(List<int> seq, bool closed)
            {
                for (int i = 0; i + 1 < seq.Count || (closed && i < seq.Count); i++)
                {
                    SunObelisk a = Nodes[seq[i]], b = Nodes[seq[(i + 1) % seq.Count]];
                    flow[Key(a, b)] = Key(a, b).Item1 == a;
                }
            }
            foreach (List<int> chain in sol.Chains) Order(chain, false);
            foreach (List<int> ring in sol.Rings) Order(ring, true);

            var wanted = new HashSet<(SunObelisk, SunObelisk)>();
            var degree = new Dictionary<SunObelisk, int>();
            foreach (var (a, b) in sol.Links)
            {
                var key = Key(Nodes[a], Nodes[b]);
                wanted.Add(key);
                degree[Nodes[a]] = degree.TryGetValue(Nodes[a], out int da) ? da + 1 : 1;
                degree[Nodes[b]] = degree.TryGetValue(Nodes[b], out int db) ? db + 1 : 1;
                if (!links.TryGetValue(key, out LinkBeam beam))
                {
                    beam = new LinkBeam(key.Item1, key.Item2);
                    links[key] = beam;
                    SoundLibrary.Play("ascensions/ritual_of_the_sun/sun_link", (key.Item1.Prism + key.Item2.Prism) * 0.5f, 0.55f, 0.06f, 3);
                }
                beam.Forward = !flow.TryGetValue(key, out bool fwd) || fwd;
            }
            foreach (var key in links.Keys.Where(k => !wanted.Contains(k)).ToList())
            {
                LinkBeam beam = links[key];
                links.Remove(key);
                beam.Remove();
                fading.Add(beam);
                if (key.Item1 != null && key.Item2 != null)
                    SoundLibrary.Play("ascensions/ritual_of_the_sun/sun_unlink", (key.Item1.Prism + key.Item2.Prism) * 0.5f, 0.45f, 0.06f, 3);
            }

            // Rings: keep the ones that still stand (same obelisks), break the others, ignite the new ones.
            var keep = new HashSet<SunRing>();
            foreach (List<int> ring in sol.Rings)
            {
                List<SunObelisk> members = ring.Select(i => Nodes[i]).ToList();
                string key = SunRing.KeyOf(members);
                SunRing existing = Rings.FirstOrDefault(r => r != null && !r.Breaking && r.Key == key);
                if (existing == null)
                {
                    existing = SunRing.Ignite(members);
                    Rings.Add(existing);
                }
                keep.Add(existing);
                for (int i = 0; i < members.Count; i++) { members[i].Ring = existing; members[i].RingIndex = existing.IndexOf(members[i]); }
            }
            foreach (SunRing r in Rings.ToList())
            {
                if (r == null) { Rings.Remove(r); continue; }
                if (!keep.Contains(r) && !r.Breaking) r.Break();
            }
            foreach (SunObelisk s in Nodes)
            {
                if (s.Ring != null && !keep.Contains(s.Ring)) s.Ring = null;
                s.Links = degree.TryGetValue(s, out int d) ? d : 0;
            }
        }

        private static (SunObelisk, SunObelisk) Key(SunObelisk a, SunObelisk b) =>
            a.GetInstanceID() < b.GetInstanceID() ? (a, b) : (b, a);

        /// <summary>The ring the beam between a and b belongs to (null for a chain link).</summary>
        private static SunRing RingOf(LinkBeam b) =>
            b.A != null && b.B != null && b.A.InRing && b.A.Ring == b.B.Ring ? b.A.Ring : null;

        private void Draw()
        {
            bool combat = SpawnManager.instance != null && SpawnManager.instance.combat;
            float dt = Time.deltaTime;
            light.Begin();
            foreach (LinkBeam b in links.Values) b.Update(dt, RingOf(b), combat, light);
            for (int i = fading.Count - 1; i >= 0; i--)
                if (!fading[i].Update(dt, null, combat, light)) { fading[i].Destroy(); fading.RemoveAt(i); }
            Rings.RemoveAll(r => r == null);
            foreach (SunRing r in Rings) r.DrawLight(light);
            foreach (SunObelisk s in SunObelisk.All) if (s != null) Glint(s, combat);
            light.End();
        }

        /// <summary>A sparkle on every linked prism: brighter in a ring; a lone obelisk shows none.</summary>
        private void Glint(SunObelisk s, bool combat)
        {
            if (s.Links == 0 && !s.InRing) return;
            float t = Time.time + s.GetInstanceID() * 0.37f;
            float size = s.InRing ? 0.42f : 0.26f;
            size *= 1f + 0.12f * Mathf.Sin(t * 3.1f);
            if (s.InRing) size += s.Ring.Flash(s.RingIndex) * 0.9f;
            float k = combat ? 1f : 0.75f;
            light.Add(s.Prism, size, SunFx.PrismGlow * k);
            light.Add(s.Prism, size * 0.3f, Color.white * (0.6f * k));
        }
    }

    /// <summary>
    /// A beam of light between two Sun Obelisks' prisms. It shoots across when the link forms, carries packets of light
    /// in its flow direction (faster and brighter in a ring) and fades when the link breaks.
    /// </summary>
    internal sealed class LinkBeam
    {
        public readonly SunObelisk A, B;
        /// <summary>Light runs from A to B (else B to A).</summary>
        public bool Forward = true;
        private readonly LightBeam beam;
        private float grow, fade = 1f, ring, phase;
        private bool removing;
        private Vector3 lastA, lastB;

        public LinkBeam(SunObelisk a, SunObelisk b)
        {
            A = a; B = b;
            beam = new LightBeam("SunLink", RitualGrid.Instance != null ? RitualGrid.Instance.transform : null, SunFx.LinkCore, SunFx.LinkGlow, 0.045f, 0.26f);
            lastA = a.Prism; lastB = b.Prism;
        }

        public void Remove() => removing = true;
        public void Destroy() => beam.Destroy();
        public float Grow => grow;

        /// <summary>Animates and draws; false once a removed beam has faded out.</summary>
        public bool Update(float dt, SunRing inRing, bool combat, QuadBatch light)
        {
            if (A != null) lastA = A.Prism;
            if (B != null) lastB = B.Prism;
            if (removing || A == null || B == null) fade -= dt / 0.3f;
            if (fade <= 0f) { beam.SetVisible(false); return false; }
            grow = Mathf.MoveTowards(grow, 1f, dt / 0.28f);
            ring = Mathf.MoveTowards(ring, inRing != null ? 1f : 0f, dt / 0.4f);
            float ignite = inRing != null ? inRing.LinkIgnite(this) : 0f;
            float level = Mathf.Lerp(0.55f, 1f, ring) * (combat ? 1f : 0.8f) * fade + ignite * 1.5f;
            float w = Mathf.Lerp(1f, 1.45f, ring) * (1f + 0.08f * Mathf.Sin(Time.time * 9f + phase));

            Vector3 from = Forward ? lastA : lastB, to = Forward ? lastB : lastA;
            Vector3 end = Vector3.Lerp(from, to, Ease(grow));
            beam.SetVisible(true);
            beam.Set(from, end, w, level);
            // Packets of light along the beam.
            float len = Vector3.Distance(from, end);
            if (len < 0.05f) return true;
            float speed = Mathf.Lerp(2.2f, 4.5f, ring), spacing = Mathf.Lerp(0.9f, 0.55f, ring);
            phase += dt * speed;
            for (float s = phase % spacing; s < len; s += spacing)
            {
                float edge = Mathf.Clamp01(s / 0.25f) * Mathf.Clamp01((len - s) / 0.25f);
                light.Add(Vector3.Lerp(from, end, s / len), Mathf.Lerp(0.24f, 0.32f, ring), SunFx.Packet * (edge * level));
            }
            return true;
        }

        private static float Ease(float t) => 1f - (1f - t) * (1f - t) * (1f - t);
    }

    [HarmonyPatch]
    internal static class SunObeliskPatches
    {
        /// <summary>
        /// A Sun Obelisk in a closed ring doesn't aim or fire (its light goes to the sky) but still gains XP from its
        /// target like any obelisk; its vanilla beam and sound stay off.
        /// </summary>
        [HarmonyPrefix, HarmonyPatch(typeof(Obelisk), "Update")]
        private static bool ObeliskUpdatePrefix(Obelisk __instance)
        {
            if (!TreasureRunMode.Active) return true;
            SunObelisk s = __instance.GetComponent<SunObelisk>();
            if (s == null || !s.InRing) return true;
            if (__instance.currentTarget != null) __instance.GainXP();
            if (__instance.beam != null && __instance.beam.activeSelf) __instance.beam.SetActive(false);
            __instance.PlaySound(false);
            return false;
        }
    }
}

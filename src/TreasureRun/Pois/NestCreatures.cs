using System.Collections.Generic;
using System.Linq;
using TreasureRun.Core;
using TreasureRun.Voxels;
using UnityEngine;

namespace TreasureRun.Pois
{
    internal enum NestCreatureKind { Lizard, Hatchling }

    /// <summary>
    /// A monster from a Lizard Nest (docs/poi-plan.md §8): a vanilla monster (the Goblin, for its pathfinder, health bar,
    /// collider and death) with its art hidden and our own animated voxel model in its place, under the walk animator's
    /// direction transform (so it turns the way it walks). Stats come from <see cref="LizardNestLogic"/>.
    /// </summary>
    internal sealed class NestCreature : MonoBehaviour
    {
        public static readonly HashSet<NestCreature> Active = new HashSet<NestCreature>();

        public NestCreatureKind Kind { get; private set; }
        public LizardNest Nest { get; private set; }
        public Enemy Enemy { get; private set; }
        public int Wave { get; private set; }

        private void OnDestroy() => Active.Remove(this);

        private static GameObject Prefab()
        {
            GameObject[] tier1 = SpawnManager.instance != null ? SpawnManager.instance.tier1 : null;
            if (tier1 == null) return null;
            return tier1.FirstOrDefault(g => g != null && g.name == "Goblin") ?? tier1.FirstOrDefault(g => g != null);
        }

        /// <summary>
        /// Spawns a lizard or a hatchling at <paramref name="at"/>, walking to <paramref name="toward"/> and on along the
        /// path; registered as one of the wave's monsters.
        /// </summary>
        public static Enemy Spawn(NestCreatureKind kind, LizardNest nest, int wave, Vector3 at, Waypoint toward, float distanceFromEnd)
        {
            GameObject prefab = Prefab();
            if (prefab == null || toward == null) return null;
            GameObject go = Instantiate(prefab, at, Quaternion.identity);
            go.name = kind == NestCreatureKind.Lizard ? "TR_NestLizard" : "TR_Hatchling";
            Enemy e = go.GetComponent<Enemy>();
            bool lizard = kind == NestCreatureKind.Lizard;
            e.baseHealth = lizard ? LizardNestLogic.LizardHealthAt(wave) : LizardNestLogic.HatchlingHealthAt(wave);
            e.baseArmor = lizard ? LizardNestLogic.LizardArmorAt(wave) : 0;
            e.baseShield = 0;
            e.baseSpeed = lizard ? LizardNestLogic.LizardSpeed : LizardNestLogic.HatchlingSpeed * Random.Range(0.92f, 1.08f);
            e.healthRegen = e.armorRegen = e.shieldRegen = 0;
            e.bleedImmune = e.burnImmune = e.poisonImmune = e.slowImmune = false;
            e.level = lizard ? LizardNestLogic.LizardGoldAt(wave) : LizardNestLogic.HatchlingGold;
            e.bonusGoldDrop = 0;
            e.damageToTower = lizard ? LizardNestLogic.LizardCastleDamage : LizardNestLogic.HatchlingCastleDamage;
            e.battleCries = new BattleCry[0];
            e.SetStats();
            e.SetFirstSpawnPoint(toward);
            if (e.pathfinder != null) e.pathfinder.distanceFromEnd = distanceFromEnd;
            if (lizard)
            {
                var box = go.GetComponent<BoxCollider>();
                if (box != null) { box.size = new Vector3(0.8f, 0.45f, 0.8f); box.center = new Vector3(0f, 0.22f, 0f); }
            }

            // Our model in place of the Goblin's art.
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            WalkAnimator walk = go.GetComponent<WalkAnimator>();
            Transform dir = walk != null && walk.directionalTransform != null ? walk.directionalTransform : go.transform;
            Vector3 heading = toward.transform.position - at;
            heading.y = 0f;
            if (heading.sqrMagnitude > 1e-4f && dir != go.transform) dir.rotation = Quaternion.LookRotation(heading, Vector3.up);
            Renderer body = lizard ? LizardRig.Build(go, dir) : HatchlingRig.Build(go, dir);
            if (body != null) e.renderer = body; // the freeze tint shows on our body

            var c = go.AddComponent<NestCreature>();
            c.Kind = kind; c.Nest = nest; c.Enemy = e; c.Wave = wave;
            Active.Add(c);
            if (SpawnManager.instance != null && !SpawnManager.instance.currentEnemies.Contains(e)) SpawnManager.instance.currentEnemies.Add(e);
            Core.Combat.RaiseSpawned(e);
            return e;
        }

        /// <summary>
        /// One part of a creature: <paramref name="model"/> scaled to <paramref name="scale"/> (of the 0.1 voxel size),
        /// on a pivot whose origin is the model's point <paramref name="pivot"/> (in voxels: x across, y forward, z up).
        /// </summary>
        public static Transform Part(string model, Vector3 pivot, Transform parent, Vector3 localPosition, float scale)
        {
            var t = new GameObject(model.Substring(model.LastIndexOf('/') + 1)).transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            VoxAsset a = VoxelLibrary.Get(model);
            if (a == null) return t;
            const float s = 0.1f;
            Vector3 meshPivot = new Vector3(-a.Model.SizeX * s / 2f + pivot.x * s, pivot.z * s, -a.Model.SizeY * s / 2f + pivot.y * s);
            var art = new GameObject("Art");
            art.transform.SetParent(t, false);
            art.transform.localScale = Vector3.one * scale;
            art.transform.localPosition = -meshPivot * scale;
            art.AddComponent<MeshFilter>().sharedMesh = a.Mesh;
            art.AddComponent<MeshRenderer>().sharedMaterial = a.Material;
            return t;
        }
    }

    /// <summary>
    /// The lizard: a low body on four sprawling legs, a head and a tail of three pieces. It walks with a diagonal gait
    /// (front right with back left, then the other pair), each leg swinging forward and lifting, the spine swaying with the
    /// steps, the head steadying against it and the tail swinging behind in a travelling wave. The pace follows the
    /// distance actually walked, so a slowed lizard walks slowly and a frozen one stands still (breathing, its tail
    /// curling a little). It rises out of the nest when it spawns.
    /// </summary>
    internal sealed class LizardRig : MonoBehaviour
    {
        /// <summary>Model scale (of the 0.1 voxel size): 0.027 world units per voxel (about 1.35 long, tail included).</summary>
        public const float Scale = 0.27f;
        private const float V = 0.1f * Scale;
        /// <summary>World units walked per full gait cycle (both pairs of legs).</summary>
        private const float Stride = 0.33f;
        private const float EmergeSeconds = 0.6f;

        private Transform lift, body, head;
        private readonly Transform[] tail = new Transform[3];
        /// <summary>Front right, front left, back right, back left.</summary>
        private readonly Transform[] legs = new Transform[4];
        private Vector3 last;
        private float phase, age, idle;

        public static Renderer Build(GameObject go, Transform parent)
        {
            var root = new GameObject("TR_LizardModel").transform;
            root.SetParent(parent, false);
            var rig = go.AddComponent<LizardRig>();
            rig.lift = new GameObject("Lift").transform;
            rig.lift.SetParent(root, false);
            rig.body = NestCreature.Part("pois/lizard/body", new Vector3(5.5f, 8f, 0f), rig.lift, new Vector3(0f, 0.035f, 0f), Scale);
            rig.head = NestCreature.Part("pois/lizard/head", new Vector3(4.5f, 0.5f, 2.5f), rig.body, new Vector3(0f, 2.4f * V, 7.2f * V), Scale);
            rig.tail[0] = NestCreature.Part("pois/lizard/tail1", new Vector3(3.5f, 8f, 2.5f), rig.body, new Vector3(0f, 2.3f * V, -7.4f * V), Scale);
            rig.tail[1] = NestCreature.Part("pois/lizard/tail2", new Vector3(2.5f, 8f, 2f), rig.tail[0], new Vector3(0f, -0.4f * V, -7.3f * V), Scale);
            rig.tail[2] = NestCreature.Part("pois/lizard/tail3", new Vector3(1.5f, 9f, 1.5f), rig.tail[1], new Vector3(0f, -0.4f * V, -7.3f * V), Scale);
            Vector3 right = new Vector3(0f, 2.5f, 5f), left = new Vector3(6f, 2.5f, 5f);
            rig.legs[0] = NestCreature.Part("pois/lizard/leg_right", right, rig.body, new Vector3(4.6f * V, 3.2f * V, 4.4f * V), Scale);
            rig.legs[1] = NestCreature.Part("pois/lizard/leg_left", left, rig.body, new Vector3(-4.6f * V, 3.2f * V, 4.4f * V), Scale);
            rig.legs[2] = NestCreature.Part("pois/lizard/leg_right", right, rig.body, new Vector3(4.6f * V, 3.2f * V, -4.2f * V), Scale);
            rig.legs[3] = NestCreature.Part("pois/lizard/leg_left", left, rig.body, new Vector3(-4.6f * V, 3.2f * V, -4.2f * V), Scale);
            rig.last = go.transform.position;
            rig.Pose(0f);
            return rig.body.GetComponentInChildren<MeshRenderer>();
        }

        private void Update()
        {
            Vector3 p = transform.position;
            Vector3 d = p - last;
            last = p;
            d.y = 0f;
            float moved = Mathf.Min(d.magnitude, 0.5f);
            phase = (phase + moved / Stride) % 1f;
            age += Time.deltaTime;
            idle += Time.deltaTime;
            Pose(age);
        }

        private void Pose(float t)
        {
            const float Tau = Mathf.PI * 2f;
            float sw = Mathf.Sin(Tau * phase);
            // Legs: the diagonal pairs swing against each other; a leg lifts while it swings forward.
            for (int i = 0; i < 4; i++)
            {
                bool right = i == 0 || i == 2;
                bool pairB = i == 1 || i == 2;
                float ph = Tau * (phase + (pairB ? 0.5f : 0f));
                float swing = Mathf.Sin(ph), up = Mathf.Max(0f, Mathf.Cos(ph));
                float side = right ? 1f : -1f;
                legs[i].localRotation = Quaternion.Euler(0f, -side * swing * 28f, side * up * 22f);
            }
            // The spine sways with the steps; the head steadies against it; the tail follows in a travelling wave.
            float breathe = 0.5f + 0.5f * Mathf.Sin(idle * 2.2f);
            float bodyYaw = 7f * sw;
            body.localRotation = Quaternion.Euler(0f, bodyYaw, 2f * Mathf.Sin(Tau * phase * 2f));
            body.localScale = new Vector3(1f + 0.02f * breathe, 1f + 0.03f * breathe, 1f);
            head.localRotation = Quaternion.Euler(-2f + 3f * Mathf.Sin(idle * 1.3f), -bodyYaw * 0.8f + 6f * Mathf.Sin(idle * 0.7f), 0f);
            for (int i = 0; i < 3; i++)
            {
                float lag = Mathf.Sin(Tau * (phase - 0.15f * (i + 1))) * (10f + 5f * i);
                float curl = Mathf.Sin(idle * 0.9f - i * 0.8f) * (3f + 2f * i);
                tail[i].localRotation = Quaternion.Euler(0f, -bodyYaw * 0.6f + lag + curl, 0f);
            }
            // Rising out of the nest: from below its rim, at first a little smaller.
            float e = Mathf.Clamp01(t / EmergeSeconds);
            float rise = 1f - (1f - e) * (1f - e);
            lift.localPosition = new Vector3(0f, -0.28f * (1f - rise) + 0.008f * Mathf.Abs(Mathf.Sin(Tau * phase * 2f)), 0f);
            lift.localScale = Vector3.one * (0.75f + 0.25f * rise);
        }
    }

    /// <summary>
    /// The hatchling: a round head and a tapering tail of three pieces and a fin, with no legs. It wriggles along like a
    /// tadpole: a wave runs down its body, growing towards the fin, faster the faster it goes, and the whole body weaves
    /// a little from side to side. It pops out at a hop when its parent bursts.
    /// </summary>
    internal sealed class HatchlingRig : MonoBehaviour
    {
        /// <summary>Model scale (of the 0.1 voxel size): 0.024 world units per voxel (about half a cell long).</summary>
        public const float Scale = 0.24f;
        private const float V = 0.1f * Scale;
        /// <summary>World units per wriggle; amplitude of each piece's sway (world units), head first.</summary>
        private const float WaveLength = 0.16f, PopSeconds = 0.3f;
        private static readonly float[] Amplitude = { 0.006f, 0.014f, 0.022f, 0.03f, 0.038f };
        /// <summary>Distance of each piece's centre behind the previous one, and its height (voxels).</summary>
        private static readonly float[] Gap = { 0f, 4.6f, 3.2f, 3.0f, 2.6f };
        private static readonly float[] Height = { 2.5f, 2.0f, 1.5f, 1.0f, 1.6f };

        private Transform root;
        private readonly Transform[] pieces = new Transform[5];
        private readonly Vector3[] points = new Vector3[5];
        private Vector3 last;
        private float phase, age, weave;

        public static Renderer Build(GameObject go, Transform parent)
        {
            var rig = go.AddComponent<HatchlingRig>();
            rig.root = new GameObject("TR_HatchlingModel").transform;
            rig.root.SetParent(parent, false);
            rig.pieces[0] = NestCreature.Part("pois/hatchling/head", new Vector3(3.5f, 3.5f, 2.5f), rig.root, Vector3.zero, Scale);
            rig.pieces[1] = NestCreature.Part("pois/hatchling/body1", new Vector3(2f, 1.5f, 2f), rig.root, Vector3.zero, Scale);
            rig.pieces[2] = NestCreature.Part("pois/hatchling/body2", new Vector3(1.5f, 1.5f, 1.5f), rig.root, Vector3.zero, Scale);
            rig.pieces[3] = NestCreature.Part("pois/hatchling/body3", new Vector3(1f, 1.5f, 1f), rig.root, Vector3.zero, Scale);
            rig.pieces[4] = NestCreature.Part("pois/hatchling/fin", new Vector3(0.5f, 6f, 2f), rig.root, Vector3.zero, Scale);
            rig.last = go.transform.position;
            rig.phase = Random.value;
            rig.weave = Random.value * 10f;
            rig.Pose();
            return rig.pieces[0].GetComponentInChildren<MeshRenderer>();
        }

        private void Update()
        {
            Vector3 p = transform.position;
            Vector3 d = p - last;
            last = p;
            d.y = 0f;
            float moved = Mathf.Min(d.magnitude, 0.5f);
            phase += moved / WaveLength + Time.deltaTime * 0.8f;
            weave += moved;
            age += Time.deltaTime;
            Pose();
        }

        private void Pose()
        {
            const float Tau = Mathf.PI * 2f;
            float z = 0f;
            for (int i = 0; i < 5; i++)
            {
                z -= Gap[i] * V;
                points[i] = new Vector3(Amplitude[i] * Mathf.Sin(Tau * phase - i * 1.1f), Height[i] * V, z);
            }
            for (int i = 0; i < 5; i++)
            {
                Vector3 ahead = i == 0 ? points[0] + (points[0] - points[1]) : points[i - 1];
                Vector3 f = ahead - points[i];
                f.y = 0f;
                pieces[i].localPosition = points[i];
                if (f.sqrMagnitude > 1e-8f) pieces[i].localRotation = Quaternion.LookRotation(f, Vector3.up);
            }
            // The whole body weaves from side to side along the path; it pops out with a little hop.
            float pop = Mathf.Clamp01(age / PopSeconds);
            root.localPosition = new Vector3(0.05f * Mathf.Sin(weave * 2.2f), 0.18f * Mathf.Sin(Mathf.PI * pop) * (1f - pop * 0.3f), 0.02f);
            root.localScale = Vector3.one * (0.3f + 0.7f * (1f - (1f - pop) * (1f - pop)));
        }
    }
}

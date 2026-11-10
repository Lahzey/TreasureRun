using TreasureRun.Core;
using TreasureRun.Voxels;
using UnityEngine;

namespace TreasureRun.Ascensions
{
    /// <summary>
    /// Base of the plane ascensions: sits next to the vanilla <see cref="BiPlane"/>, hides its art and shows a voxel model
    /// under <c>artTransform</c> (vanilla banking still applies to it), finds the owning Radar and retargets instantly
    /// when the target dies (vanilla waits up to 3 s for the Radar to send a new one).
    /// </summary>
    internal abstract class AscendedPlane : MonoBehaviour
    {
        protected BiPlane plane;
        protected RadarTower owner;
        protected VoxAsset asset;
        /// <summary>Holder of the model, in voxel-mesh units (see <see cref="Vox"/>).</summary>
        protected Transform model;
        private float ownerSearch;

        /// <summary>True if this ascension flies itself (vanilla BiPlane.Update is skipped).</summary>
        protected virtual bool CustomFlight => false;
        protected abstract string ModelName { get; }
        /// <summary>Model scale (world size of one voxel / 0.1).</summary>
        protected abstract float ModelScale { get; }
        /// <summary>Model point (voxel coordinates, see <see cref="Vox"/>) placed at the plane's pivot; null = mesh centre.</summary>
        protected virtual Vector3? Pivot => null;

        public BiPlane Plane => plane;
        public RadarTower Owner => owner;

        protected virtual void Awake()
        {
            plane = GetComponent<BiPlane>();
            if (CustomFlight) PlaneAscensions.CustomFlight.Add(plane);
            Dress();
        }

        protected virtual void OnDestroy()
        {
            PlaneAscensions.CustomFlight.Remove(plane);
        }

        private void Dress()
        {
            Transform art = plane.artTransform != null ? plane.artTransform : transform;
            foreach (Renderer r in art.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            asset = VoxelLibrary.Get(ModelName);
            if (asset == null) return;

            model = new GameObject("TR_AscendedArt").transform;
            model.SetParent(art, false);
            model.localScale = Vector3.one * ModelScale;
            Vector3 pivot = Pivot.HasValue ? Vox(Pivot.Value.x, Pivot.Value.y, Pivot.Value.z) : asset.Mesh.bounds.center;
            model.localPosition = -pivot * ModelScale;
            var mesh = new GameObject("Mesh");
            mesh.transform.SetParent(model, false);
            mesh.AddComponent<MeshFilter>().sharedMesh = asset.Mesh;
            mesh.AddComponent<MeshRenderer>().sharedMaterial = asset.Material;
        }

        /// <summary>
        /// Position inside <see cref="model"/> of a point given in voxel coordinates of the model (voxel i spans i..i+1;
        /// MagicaVoxel x/y/z = Unity x/z/y), matching VoxelLibrary's default origin (centred in x/y, z from 0).
        /// </summary>
        protected Vector3 Vox(float x, float y, float z)
        {
            const float s = 0.1f;
            return new Vector3(x * s - asset.Model.SizeX * s / 2f, z * s, y * s - asset.Model.SizeY * s / 2f);
        }

        /// <summary>Plane-local position (relative to the pivot) of a model voxel point.</summary>
        protected Vector3 LocalOf(Vector3 vox) =>
            model == null ? Vector3.zero : model.localPosition + Vox(vox.x, vox.y, vox.z) * ModelScale;

        /// <summary>Adds a voxel part (e.g. a propeller) inside the model at a voxel point, centred on its own mesh.</summary>
        protected Transform AddPart(string name, Vector3 voxPoint, float relativeScale = 1f, Quaternion? rotation = null)
        {
            VoxAsset part = VoxelLibrary.Get(name);
            if (part == null || model == null) return null;
            var pivot = new GameObject("TR_Part_" + name).transform;
            pivot.SetParent(model, false);
            pivot.localPosition = Vox(voxPoint.x, voxPoint.y, voxPoint.z);
            pivot.localRotation = rotation ?? Quaternion.identity;
            pivot.localScale = Vector3.one * relativeScale;
            var mesh = new GameObject("Mesh");
            mesh.transform.SetParent(pivot, false);
            mesh.transform.localPosition = -part.Mesh.bounds.center;
            mesh.AddComponent<MeshFilter>().sharedMesh = part.Mesh;
            mesh.AddComponent<MeshRenderer>().sharedMaterial = part.Material;
            return pivot;
        }

        protected virtual void Update()
        {
            if (owner == null && (ownerSearch -= Time.deltaTime) <= 0f)
            {
                ownerSearch = 1f;
                owner = PlaneAscensions.OwnerOf(plane);
            }
            if (plane.target == null && owner != null && owner.currentTarget != null) plane.target = owner.currentTarget;
        }

        /// <summary>Seconds between the owning Radar's shots (its fire rate, including cards and treasures).</summary>
        protected float RadarShotInterval
        {
            get
            {
                float rps = owner != null ? owner.rps : plane.rps;
                return rps > 0.01f ? rps : 60f / 700f;
            }
        }

        protected Vector3 HomePoint => owner != null ? owner.transform.position : GameHelpers.CastlePosition;

        protected float RadarRange => owner != null ? owner.range : 30f;

        /// <summary>Vanilla bullet from this plane's muzzle towards a target (stats as the Radar sent them).</summary>
        protected void ShootBullet(Vector3 from, GameObject target)
        {
            if (plane.projectile == null || target == null) return;
            Vector3 aim = target.transform.position + new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(-0.2f, 0.2f), Random.Range(-0.3f, 0.3f));
            Projectile p = Instantiate(plane.projectile, from, Quaternion.LookRotation(aim - from)).GetComponent<Projectile>();
            p?.SetStats(TowerType.Radar, target, plane.projectileSpeed, plane.damage, plane.healthDamage, plane.armorDamage, plane.shieldDamage,
                        plane.slowPercent, plane.bleedPercent, plane.burnPercent, plane.poisonPercent, plane.critChance, plane.stunChance);
        }
    }

    /// <summary>
    /// Strike Fighter (Rare): the vanilla dogfight AI with jet numbers: faster, tighter turns, faster fire, more ammo,
    /// shorter breakaway before turning back in. Afterburner trail.
    /// </summary>
    internal sealed class StrikeFighter : AscendedPlane
    {
        public const float SpeedFactor = 2.2f;      // vanilla 6
        public const float TurnTime = 0.55f;        // vanilla 1.5 (max yaw 90°/turnTime per second)
        public const float FireIntervalFactor = 0.6f;
        public const int Magazine = 30;             // vanilla 20

        protected override string ModelName => "planes/strike_fighter";
        protected override float ModelScale => 0.6f;

        protected override void Awake()
        {
            base.Awake();
            plane.speed = 6f * SpeedFactor;
            plane.turnTime = TurnTime;
            plane.minDistance = 3.5f;   // vanilla 5: breaks away later
            plane.maxDistance = 11f;    // vanilla 18: turns back in sooner
            plane.maxAmmo = Magazine;
            plane.ammo = Magazine;
            AddTrail();
        }

        private void AddTrail()
        {
            Material mat = Fx.LineMaterial;
            if (mat == null || model == null) return;
            var go = new GameObject("TR_Afterburner");
            go.transform.SetParent(model, false);
            go.transform.localPosition = Vox(6.5f, -0.2f, 2.5f);
            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = mat;
            trail.time = 0.35f;
            trail.minVertexDistance = 0.1f;
            trail.widthMultiplier = 0.09f;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.75f, 0.35f), 0f), new GradientColorKey(Color.white, 0.35f) },
                      new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = g;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        protected override void Update()
        {
            base.Update();
            if (owner != null) plane.rps = owner.rps * FireIntervalFactor; // the Radar resets plane.rps in SetStats
        }
    }
}

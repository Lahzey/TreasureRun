using System;
using System.Collections.Generic;
using HarmonyLib;
using TreasureRun.Ascensions.Phase1;
using TreasureRun.Castles;
using TreasureRun.Core;
using UnityEngine;

namespace TreasureRun.Ascensions.Phase2
{
    /// <summary>An ascension that sets a new base range (the prefab value); height, levels and cards stack on top.</summary>
    internal interface IBaseRangeOverride
    {
        float BaseRange { get; }
    }

    /// <summary>Helpers shared by the phase 2 ascension components.</summary>
    [HarmonyPatch]
    internal static class Phase2Kit
    {
        private static readonly Dictionary<int, float> originalRange = new Dictionary<int, float>();

        /// <summary>
        /// Swaps the tower's base range while an <see cref="IBaseRangeOverride"/> component is on it and restores the
        /// prefab value once it is gone (revoked ascensions are destroyed immediately, then SetStats runs).
        /// </summary>
        [HarmonyPrefix, HarmonyPatch(typeof(Tower), nameof(Tower.SetStats))]
        private static void SetStatsPrefix(Tower __instance)
        {
            int id = __instance.GetInstanceID();
            var o = __instance.GetComponent<IBaseRangeOverride>();
            if (o != null)
            {
                if (!originalRange.ContainsKey(id)) originalRange[id] = __instance.baseRange;
                __instance.baseRange = o.BaseRange;
            }
            else if (originalRange.TryGetValue(id, out float r))
            {
                __instance.baseRange = r;
                originalRange.Remove(id);
            }
        }

        /// <summary>
        /// Vanilla bug: <c>Enemy.GetFuturePosition</c> returns (0, 0, 0) for a monster that stands still (frozen,
        /// stunned, speed 0), so Mortar shells and lobbed shots land at the map origin. A standing monster's future
        /// position is where it stands.
        /// </summary>
        [HarmonyPrefix, HarmonyPatch(typeof(Enemy), nameof(Enemy.GetFuturePosition))]
        private static bool FuturePositionPrefix(Enemy __instance, float t, ref Vector3 __result)
        {
            if (!TreasureRunMode.Active || __instance.currentSpeed * t > 0f) return true;
            __result = __instance.transform.position;
            return false;
        }

        /// <summary>The tower's attack speed factor from cards, treasures and buffs (vanilla interval / current interval).</summary>
        public static float AttackSpeedFactor(Tower t)
        {
            if (t == null || t.rps <= 0f || t.rpm <= 0f) return 1f;
            float vanilla = 60f / t.rpm;
            if (t is Dropper d && d.dropPoints != null) vanilla *= 10f / (10f + d.dropPoints.Length);
            return Mathf.Max(0.05f, vanilla / t.rps);
        }

        public static Enemy Target(Tower t) => t != null && t.currentTarget != null ? t.currentTarget.GetComponent<Enemy>() : null;

        /// <summary>Blast: hits every monster within <paramref name="radius"/> of a point (flat), with the tower's stats.</summary>
        public static int Blast(Tower t, Vector3 at, float radius, int baseDamage, float extraBurn = 0f)
        {
            int n = 0;
            foreach (Enemy e in Combat.EnemiesInRadius(at, radius))
            {
                if (!Combat.IsAlive(e)) continue;
                e.TakeDamage(t.towerType, baseDamage, t.healthDamage, t.armorDamage, t.shieldDamage, t.slowPercent,
                             t.bleedPercent, t.burnPercent + extraBurn, t.poisonPercent, t.critChance, t.stunChance);
                n++;
            }
            return n;
        }

        /// <summary>Flat collider radius of a monster (0.25 for the vanilla 0.5 box).</summary>
        public static float Radius(Enemy e)
        {
            Collider c = e != null ? e.GetComponent<Collider>() : null;
            if (c == null) return 0.25f;
            Vector3 ext = c.bounds.extents;
            return Mathf.Max(ext.x, ext.z);
        }

        /// <summary>The vanilla mesh filter on a part path ("TurretArt", "BaseArt") whose mesh is a voxel export.</summary>
        public static MeshFilter PartFilter(GameObject tower, string part, string meshName = null)
        {
            foreach (MeshFilter f in tower.GetComponentsInChildren<MeshFilter>(true))
            {
                if (f.sharedMesh == null) continue;
                if (meshName != null ? f.sharedMesh.name != meshName : f.sharedMesh.name != "default") continue;
                for (Transform t = f.transform; t != null && t != tower.transform; t = t.parent)
                    if (t.name == part) return f;
            }
            return null;
        }

        /// <summary>A glowing straight line that fades out (beams, aim lines).</summary>
        public static LineRenderer Line(string name, Color color, float width, Transform parent = null)
        {
            var go = new GameObject("TR_" + name);
            if (parent != null) go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = Fx.GlowMaterial;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = line.endWidth = width;
            line.numCapVertices = 2;
            line.startColor = line.endColor = color;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        public static readonly Color[] FireColors =
            { new Color(1f, 0.85f, 0.3f), new Color(1f, 0.55f, 0.15f), new Color(0.85f, 0.3f, 0.1f), new Color(0.3f, 0.28f, 0.27f) };
        public static readonly Color[] IceColors =
            { new Color(0.85f, 0.95f, 1f), new Color(0.6f, 0.85f, 1f), new Color(0.4f, 0.7f, 0.95f) };
        public static readonly Color[] SmokeColors =
            { new Color(0.75f, 0.75f, 0.72f), new Color(0.55f, 0.55f, 0.53f), new Color(0.4f, 0.4f, 0.4f) };
    }

    /// <summary>
    /// A straight piercing shot (Icicle Cannon): flies flat at hit height along a direction, sphere-casting each physics
    /// step and hitting every monster on its way once. The model descends from the muzzle to hit height over the first
    /// stretch so it reads as a shot from the tower.
    /// </summary>
    internal sealed class LineShot : MonoBehaviour
    {
        private const float CastRadius = 0.18f;
        private Vector3 dir, start;
        private float speed, maxDistance, travelled, hitY, dropDistance;
        private LayerMask mask;
        private Func<Enemy, bool> onHit;
        private readonly HashSet<Enemy> struck = new HashSet<Enemy>();

        public static LineShot Launch(string model, float scale, Vector3 from, Vector3 flatDir, float hitY, float dropDistance,
                                      float speed, float maxDistance, LayerMask mask, Func<Enemy, bool> onHit)
        {
            GameObject go = Phase1Kit.Model(model, scale);
            go.transform.position = from;
            go.transform.rotation = Quaternion.LookRotation(flatDir, Vector3.up);
            var s = go.AddComponent<LineShot>();
            s.dir = flatDir;
            s.start = from;
            s.hitY = hitY;
            s.dropDistance = Mathf.Max(0.5f, dropDistance);
            s.speed = speed;
            s.maxDistance = maxDistance;
            s.mask = mask;
            s.onHit = onHit;
            return s;
        }

        private void FixedUpdate()
        {
            float step = speed * Time.fixedDeltaTime;
            Vector3 castFrom = transform.position;
            castFrom.y = hitY;
            RaycastHit[] cast = Physics.SphereCastAll(castFrom, CastRadius, dir, step + 0.1f, mask, QueryTriggerInteraction.Collide);
            Array.Sort(cast, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit h in cast)
            {
                Enemy e = h.collider.GetComponentInParent<Enemy>();
                if (e == null || struck.Contains(e) || !Combat.IsAlive(e)) continue;
                struck.Add(e);
                bool goOn;
                try { goOn = onHit(e); }
                catch (Exception ex) { Plugin.Log.LogError($"Line shot hit failed: {ex}"); goOn = false; }
                if (!goOn) { Destroy(gameObject); return; }
            }
            travelled += step;
            Vector3 p = start + dir * travelled;
            p.y = Mathf.Lerp(start.y, hitY + 0.2f, Mathf.Clamp01(travelled / dropDistance));
            Vector3 move = p - transform.position;
            if (move.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(move, Vector3.up);
            transform.position = p;
            if (travelled > maxDistance) Destroy(gameObject);
        }
    }

    /// <summary>A model on a true ballistic arc (gravity <see cref="Ballistics.Gravity"/>) that calls back on landing.</summary>
    internal sealed class LobShot : MonoBehaviour
    {
        private Vector3 velocity;
        private float groundY, life = 8f, spin;
        private Action<Vector3> onImpact;
        private Action<LobShot> onFly;

        public Vector3 Velocity => velocity;

        public static LobShot Fire(string model, float scale, Vector3 from, Vector3 velocity, float groundY, Action<Vector3> onImpact,
                                   float spin = 200f, Action<LobShot> onFly = null)
        {
            GameObject go = Phase1Kit.Model(model, scale);
            go.transform.position = from;
            var s = go.AddComponent<LobShot>();
            s.velocity = velocity;
            s.groundY = groundY;
            s.onImpact = onImpact;
            s.spin = spin;
            s.onFly = onFly;
            return s;
        }

        /// <summary>Velocity that lands on <paramref name="to"/> after exactly <paramref name="time"/> seconds.</summary>
        public static Vector3 TimedVelocity(Vector3 from, Vector3 to, float time)
        {
            time = Mathf.Max(0.05f, time);
            Vector3 v = (to - from) / time;
            v.y += 0.5f * Ballistics.Gravity * time;
            return v;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            velocity.y -= Ballistics.Gravity * dt;
            transform.position += velocity * dt;
            if (spin != 0f) transform.Rotate(spin * dt, spin * 0.4f * dt, 0f, Space.Self);
            else if (velocity.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(velocity, Vector3.up);
            onFly?.Invoke(this);
            life -= dt;
            if ((velocity.y < 0f && transform.position.y <= groundY) || life <= 0f)
            {
                Vector3 p = transform.position;
                p.y = groundY;
                try { onImpact?.Invoke(p); }
                catch (Exception e) { Plugin.Log.LogError($"Lob impact failed: {e}"); }
                Destroy(gameObject);
            }
        }
    }

    /// <summary>Destroys the object after a delay, fading its scale (scorch marks, markers).</summary>
    internal sealed class ShrinkAway : MonoBehaviour
    {
        public float Hold = 2f, Fade = 1f;
        private float t;
        private Vector3 scale;

        private void Start() => scale = transform.localScale;

        private void Update()
        {
            t += Time.deltaTime;
            if (t < Hold) return;
            float k = 1f - (t - Hold) / Fade;
            if (k <= 0f) { Destroy(gameObject); return; }
            transform.localScale = new Vector3(scale.x * k, scale.y, scale.z * k);
        }
    }
}

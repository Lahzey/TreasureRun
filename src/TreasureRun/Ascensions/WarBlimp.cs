using System.Collections.Generic;
using HarmonyLib;
using TreasureRun.Core;
using UnityEngine;

namespace TreasureRun.Ascensions
{
    /// <summary>
    /// War Blimp (Mythical): a huge, slow airship. It hovers over the densest crowd of monsters in Radar range, fires a
    /// gatling gun continuously (one vanilla bullet per Radar shot interval, no magazine), drops bombs while it is over
    /// the track with monsters below, and carries a tower deck (<see cref="BlimpDeck"/>): a tower built there flies
    /// along.
    /// </summary>
    internal sealed class WarBlimp : AscendedPlane
    {
        public const float MaxSpeed = 1.5f, Accel = 0.6f, TurnRate = 40f, Altitude = 4.4f;
        public const float GatlingRange = 8.5f;
        /// <summary>Seconds per bomb = the Radar's seconds per shot × this.</summary>
        public const float BombIntervalFactor = 15f;
        public const float BombDamageFactor = 3f;
        public const float BlastRadius = 1f;
        /// <summary>High ground a tower on the deck counts as having (its real height would give about +15).</summary>
        public const int DeckHeightBonus = 3;
        /// <summary>Blimp hulls (axis to axis) closer than this push each other apart; the hull is ~2.2 wide.</summary>
        public const float SeparationRadius = 3.6f;
        public const float SeparationSpeed = 2.4f;
        /// <summary>Half the hull length along its axis (the envelope is ~6.3 long).</summary>
        private const float HalfLength = 2.8f;

        private static readonly List<WarBlimp> all = new List<WarBlimp>();

        // Voxel points of the war_blimp model (see VoxTool PlaneRecipes.WarBlimp).
        private static readonly Vector3 EnvelopeCentre = new Vector3(16f, 32f, 18.5f);
        private static readonly Vector3 DeckTop = new Vector3(16f, 32f, 31f);
        private static readonly Vector3 GatlingPoint = new Vector3(16f, 40f, -1.6f);
        private static readonly Vector3 BombBay = new Vector3(16f, 31f, 0f);

        private float speed, yaw, bank, retarget, fireTimer, bombTimer, spin;
        private bool placed;
        private Vector3 hoverTarget;
        private Transform gatling, barrels;
        private readonly List<Transform> propellers = new List<Transform>();

        public BlimpDeck Deck { get; private set; }

        private void OnEnable() => all.Add(this);
        private void OnDisable() => all.Remove(this);

        protected override bool CustomFlight => true;
        protected override string ModelName => "planes/war_blimp";
        protected override float ModelScale => 1f;
        protected override Vector3? Pivot => EnvelopeCentre;

        protected override void Awake()
        {
            base.Awake();
            yaw = transform.eulerAngles.y;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            gatling = AddPart("planes/gatling_mount", GatlingPoint, 1f);
            if (gatling != null)
            {
                VoxelsPart(gatling, "planes/gatling_barrels", new Vector3(0f, 0.05f, 0.8f), out barrels);
            }
            foreach (float x in new[] { 4.5f, 27.5f })
            {
                Transform p = AddPart("planes/propeller", new Vector3(x, 23.8f, 9.5f), 1.3f);
                if (p != null) propellers.Add(p);
            }
            if (model != null) Deck = BlimpDeck.Create(this, transform, LocalOf(DeckTop));
        }

        /// <summary>Child part attached to another part (local position in the parent's mesh units).</summary>
        private static void VoxelsPart(Transform parent, string name, Vector3 local, out Transform pivot)
        {
            pivot = null;
            var asset = Voxels.VoxelLibrary.Get(name);
            if (asset == null) return;
            pivot = new GameObject("TR_Part_" + name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = local;
            var mesh = new GameObject("Mesh");
            mesh.transform.SetParent(pivot, false);
            mesh.transform.localPosition = -asset.Mesh.bounds.center;
            mesh.AddComponent<MeshFilter>().sharedMesh = asset.Mesh;
            mesh.AddComponent<MeshRenderer>().sharedMaterial = asset.Material;
        }

        protected override void Update()
        {
            base.Update();
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (!placed && owner != null)
            {
                // Vanilla launches planes 50 units away; a blimp would take half a minute to arrive.
                placed = true;
                Vector3 home = HomePoint;
                if (Flat(transform.position - home).magnitude > 12f)
                    transform.position = new Vector3(home.x - 3f, Altitude, home.z - 3f);
                hoverTarget = home;
            }

            foreach (Transform p in propellers) p.Rotate(0f, 0f, 900f * dt, Space.Self);

            retarget -= dt;
            if (retarget <= 0f) { retarget = 1f; hoverTarget = PickHoverPoint(); }
            Move(dt);
            Gatling(dt);
            Bombs(dt);
        }

        private Vector3 Forward => new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad), 0f, Mathf.Cos(yaw * Mathf.Deg2Rad));
        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        private void Move(float dt)
        {
            Vector3 to = Flat(hoverTarget - transform.position);
            float dist = to.magnitude, rate = 0f, delta = 0f;
            if (dist > 0.6f)
            {
                delta = Mathf.DeltaAngle(yaw, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
                rate = Mathf.Clamp(delta * 2f, -TurnRate, TurnRate);
                yaw += rate * dt;
            }
            // Blimps can hover and turn on the spot: slow down near the target and while turning hard.
            Vector3 push = Separation(out float crowding);
            float desired = dist > 0.6f ? Mathf.Min(MaxSpeed, dist * 0.5f) * Mathf.Clamp01(1f - Mathf.Abs(delta) / 120f) : 0f;
            desired *= 1f - 0.8f * crowding; // a crowded blimp yields instead of pressing on into its neighbour
            speed = Mathf.MoveTowards(speed, desired, Accel * dt);
            Vector3 pos = transform.position + (Forward * speed + push) * dt;
            float targetY = Altitude + Mathf.Sin(Time.time * 0.9f) * 0.06f;
            pos.y = Mathf.MoveTowards(pos.y, targetY, dt * 1.5f);
            transform.position = pos;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            bank = Mathf.Lerp(bank, -rate / TurnRate * 5f, dt * 2f);
            if (plane.artTransform != null) plane.artTransform.localRotation = Quaternion.Euler(0f, 0f, bank);
        }

        /// <summary>
        /// Soft push away from other blimps (they would otherwise hover over the same crowd and clip into each other):
        /// a drift away from the nearest point of the other hull that grows quadratically as the hulls get closer,
        /// independent of where the blimp is heading. Hulls are treated as segments along their axis.
        /// <paramref name="crowding"/> is 0 (free) .. 1 (touching).
        /// </summary>
        private Vector3 Separation(out float crowding)
        {
            Vector3 push = Vector3.zero;
            crowding = 0f;
            foreach (WarBlimp other in all)
            {
                if (other == this || other == null) continue;
                if (Flat(transform.position - other.transform.position).sqrMagnitude > (SeparationRadius + 2f * HalfLength) * (SeparationRadius + 2f * HalfLength)) continue;
                ClosestHullPoints(this, other, out Vector3 mine, out Vector3 theirs);
                Vector3 away = Flat(mine - theirs);
                float d = away.magnitude;
                if (d >= SeparationRadius) continue;
                // Axes crossing: split along a stable direction.
                Vector3 dir = d > 0.01f ? away / d : (GetInstanceID() < other.GetInstanceID() ? Vector3.right : Vector3.left);
                float t = 1f - d / SeparationRadius;
                push += dir * t * t;
                crowding = Mathf.Max(crowding, t);
            }
            return Vector3.ClampMagnitude(push * 2f, 1f) * SeparationSpeed;
        }

        /// <summary>Closest pair of points on the two hull axes (sampled, good enough for steering).</summary>
        private static void ClosestHullPoints(WarBlimp a, WarBlimp b, out Vector3 pa, out Vector3 pb)
        {
            Vector3 fa = a.Forward * HalfLength, fb = b.Forward * HalfLength;
            Vector3 ca = a.transform.position, cb = b.transform.position;
            pa = ca; pb = cb;
            float best = float.MaxValue;
            for (int i = -3; i <= 3; i++)
            {
                Vector3 x = ca + fa * (i / 3f);
                for (int j = -3; j <= 3; j++)
                {
                    Vector3 y = cb + fb * (j / 3f);
                    float d = Flat(x - y).sqrMagnitude;
                    if (d < best) { best = d; pa = x; pb = y; }
                }
            }
        }

        /// <summary>Densest crowd in Radar range (count within 3 units, weighted by health), led by 2.5 s; else home.</summary>
        private Vector3 PickHoverPoint()
        {
            Vector3 home = HomePoint, pos = transform.position;
            if (SpawnManager.instance == null) return home;
            float range = RadarRange;
            var list = new List<Enemy>();
            foreach (Enemy e in SpawnManager.instance.currentEnemies)
                if (Combat.IsAlive(e) && Flat(e.transform.position - home).sqrMagnitude <= range * range) list.Add(e);
            if (list.Count == 0) return home;
            if (list.Count > 60)
            {
                list.Sort((a, b) => Flat(a.transform.position - pos).sqrMagnitude.CompareTo(Flat(b.transform.position - pos).sqrMagnitude));
                list.RemoveRange(60, list.Count - 60);
            }
            float maxHp = 1f;
            foreach (Enemy e in list) maxHp = Mathf.Max(maxHp, Combat.CurrentHp(e));
            Enemy best = null;
            float bestScore = float.MinValue;
            foreach (Enemy a in list)
            {
                float score = 0f;
                foreach (Enemy b in list)
                    if (Flat(a.transform.position - b.transform.position).sqrMagnitude < 9f) score += 1f + 0.5f * Combat.CurrentHp(b) / maxHp;
                score -= Flat(a.transform.position - pos).magnitude * 0.05f;
                if (score > bestScore) { bestScore = score; best = a; }
            }
            try { return best.pathfinder != null && best.pathfinder.currentWaypoint != null ? best.GetFuturePosition(2.5f) : best.transform.position; }
            catch { return best.transform.position; }
        }

        private Enemy GunTarget(Vector3 from)
        {
            Enemy radarTarget = owner != null && owner.currentTarget != null ? owner.currentTarget.GetComponent<Enemy>() : null;
            if (Combat.IsAlive(radarTarget) && Flat(radarTarget.transform.position - from).sqrMagnitude <= GatlingRange * GatlingRange)
                return radarTarget;
            var near = Combat.NearestEnemies(from, GatlingRange, 1);
            return near.Count > 0 ? near[0] : null;
        }

        private void Gatling(float dt)
        {
            if (gatling == null) return;
            Enemy target = GunTarget(gatling.position);
            if (target != null)
            {
                Vector3 d = target.transform.position + Vector3.up * 0.2f - gatling.position;
                gatling.rotation = Quaternion.Slerp(gatling.rotation, Quaternion.LookRotation(d, Vector3.up), dt * 10f);
            }
            spin = Mathf.MoveTowards(spin, target != null ? 1500f : 0f, dt * 2500f);
            if (barrels != null) barrels.Rotate(0f, 0f, spin * dt, Space.Self);

            fireTimer = Mathf.Max(fireTimer - dt, -RadarShotInterval);
            if (target == null || spin < 900f) return;
            int shots = 0;
            while (fireTimer <= 0f && shots++ < 4)
            {
                fireTimer += RadarShotInterval;
                Vector3 muzzle = barrels != null ? barrels.TransformPoint(new Vector3(0f, 0f, 0.5f)) : gatling.position;
                ShootBullet(muzzle, target.gameObject);
            }
        }

        private void Bombs(float dt)
        {
            bombTimer -= dt;
            if (bombTimer > 0f || model == null || SpawnManager.instance == null) return;
            bombTimer = 0.2f;
            Vector3 bay = transform.TransformPoint(LocalOf(BombBay));
            if (!PlaneAscensions.OverTrack(bay)) return;
            float fall = Mathf.Sqrt(2f * Mathf.Max(0.5f, bay.y) / AerialBomb.Gravity);
            bool worth = false;
            foreach (Enemy e in Combat.EnemiesInRadius(bay, 3f))
            {
                Vector3 p;
                try { p = e.pathfinder != null && e.pathfinder.currentWaypoint != null ? e.GetFuturePosition(fall) : e.transform.position; }
                catch { p = e.transform.position; }
                if (Flat(p - bay).sqrMagnitude <= 1.2f * 1.2f) { worth = true; break; }
            }
            if (!worth) return;
            AerialBomb.Drop(bay, Forward, plane, Mathf.RoundToInt(plane.damage * BombDamageFactor), BlastRadius, 1.2f);
            bombTimer = RadarShotInterval * BombIntervalFactor;
        }

        public string Status =>
            $"speed {speed:0.0}, hover {Flat(hoverTarget - transform.position).magnitude:0.0} away, deck {(Deck == null ? "none" : Deck.IsFree ? "free" : Deck.Passenger.name)}";
    }

    /// <summary>
    /// The War Blimp's tower slot: an invisible trigger on a layer no projectile or vanilla raycast uses, found by our
    /// own placement ray (BuildingManager.SamplePoint prefix). A tower built there is parented to the deck and flies
    /// with the blimp.
    /// </summary>
    internal sealed class BlimpDeck : MonoBehaviour
    {
        public static readonly List<BlimpDeck> All = new List<BlimpDeck>();
        private static int layer = -1;

        public WarBlimp Blimp { get; private set; }
        public Tower Passenger { get; private set; }
        public bool IsFree => Passenger == null;

        /// <summary>First unnamed layer (from 31 down), so nothing vanilla collides with or raycasts against it.</summary>
        public static int Layer
        {
            get
            {
                if (layer >= 0) return layer;
                layer = 31;
                for (int i = 31; i >= 8; i--)
                    if (string.IsNullOrEmpty(LayerMask.LayerToName(i))) { layer = i; break; }
                Plugin.Log.LogInfo($"Blimp deck layer: {layer}");
                return layer;
            }
        }

        public static BlimpDeck Create(WarBlimp blimp, Transform parent, Vector3 localTop)
        {
            var go = new GameObject("TR_BlimpDeck") { layer = Layer };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localTop;
            go.transform.localRotation = Quaternion.identity;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(1.6f, 0.4f, 1.6f);
            box.center = new Vector3(0f, -0.2f, 0f);
            var deck = go.AddComponent<BlimpDeck>();
            deck.Blimp = blimp;
            return deck;
        }

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        /// <summary>Towers only; no Radars (no blimps on blimps) and no Shredder/Encampment (they drop onto the ground around them).</summary>
        public static bool CanCarry(GameObject prefab)
        {
            Tower t = prefab != null ? prefab.GetComponent<Tower>() : null;
            return t != null && !(t is RadarTower) && !(t is Dropper);
        }

        public GameObject Build(GameObject prefab)
        {
            GameObject go = Instantiate(prefab, transform.position, transform.rotation);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            Passenger = go.GetComponent<Tower>();
            go.AddComponent<BlimpPassenger>().Deck = this;
            go.GetComponent<IBuildable>()?.SetStats();
            return go;
        }

        /// <summary>Demolishes the passenger (the blimp is going away).</summary>
        public void ReleasePassenger()
        {
            if (Passenger == null) return;
            Tower t = Passenger;
            Passenger = null;
            t.transform.SetParent(null, true);
            t.Demolish();
        }
    }

    /// <summary>Marks a tower standing on a blimp deck.</summary>
    internal sealed class BlimpPassenger : MonoBehaviour
    {
        public BlimpDeck Deck;
    }

    [HarmonyPatch]
    internal static class BlimpDeckPatches
    {
        private static BlimpDeck pending;

        /// <summary>Build mode: a ray hitting a free deck shows the ghost on the deck (it follows the blimp).</summary>
        [HarmonyPrefix, HarmonyPatch(typeof(BuildingManager), nameof(BuildingManager.SamplePoint))]
        private static bool SamplePointPrefix(BuildingManager __instance, ref bool __result)
        {
            pending = null;
            if (BlimpDeck.All.Count == 0 || Camera.main == null) return true;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 2000f, 1 << BlimpDeck.Layer, QueryTriggerInteraction.Collide)) return true;
            BlimpDeck deck = hit.collider.GetComponent<BlimpDeck>();
            if (deck == null) return true;
            if (!deck.IsFree || !BlimpDeck.CanCarry(__instance.thingToBuild))
            {
                __instance.HideGhost();
                __result = false;
                return false;
            }
            __instance.DisplayGhost(deck.transform.position, "+" + WarBlimp.DeckHeightBonus);
            pending = deck;
            __result = true;
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(BuildingManager), nameof(BuildingManager.Build))]
        private static bool BuildPrefix(BuildingManager __instance)
        {
            BlimpDeck deck = pending;
            pending = null;
            if (deck == null || !deck.IsFree || !BlimpDeck.CanCarry(__instance.thingToBuild)) return true;
            GameObject go = deck.Build(__instance.thingToBuild);
            Object.Instantiate(__instance.placementFXObject, go.transform.position + Vector3.up * 0.333f, Quaternion.identity);
            __instance.buildSpotAvailable = false;
            return false;
        }

        /// <summary>
        /// Deck towers count as +DeckHeightBonus high ground (not the ~+15 their altitude would give) and reach as far
        /// across the ground as that height allows (range is a sphere, so it is extended by the altitude).
        /// </summary>
        [HarmonyPostfix, HarmonyPatch(typeof(Tower), nameof(Tower.SetStats)), HarmonyPriority(Priority.First)]
        private static void PassengerStatsPostfix(Tower __instance)
        {
            if (__instance.GetComponent<BlimpPassenger>() == null) return;
            int excess = __instance.heightBonus - WarBlimp.DeckHeightBonus;
            float mult = 1f + TowerManager.instance.GetHeightRangeBonusMultiplier(__instance.towerType);
            __instance.damage = Mathf.Max(0, __instance.damage - excess);
            float horizontal = Mathf.Max(0.5f, __instance.range - excess / 2f * mult);
            float drop = Mathf.Max(0f, __instance.transform.position.y - 0.4f);
            __instance.range = Mathf.Sqrt(horizontal * horizontal + drop * drop);
            __instance.heightBonus = WarBlimp.DeckHeightBonus;
        }
    }
}

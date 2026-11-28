using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TreasureRun.Cards;
using TreasureRun.Core;
using TreasureRun.Treasures;
using TreasureRun.Voxels;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TreasureRun.Castles
{
    /// <summary>
    /// Siege Workshop: the castle carries a real vanilla Cannon tower (tower menu, range circle, levels, bought upgrades,
    /// target priorities and Cannon cards work unchanged), but it fires explosive shells on ballistic arcs instead of
    /// vanilla cannonballs (SiegeCastleTower). Its turret sits on the roof and shows our voxel cannon; the tower itself
    /// stays at ground level (no high-ground bonus). It never counts as a built tower and cannot be demolished.
    /// Downside: towers cost double. Castle-exclusive cards and treasures upgrade it.
    /// </summary>
    internal sealed class SiegeWorkshopBehaviour : CastleBehaviour
    {
        public const float TowerPriceMultiplier = 2f;
        /// <summary>Height of the turret ring top in castle-model voxels (cannon pivot).</summary>
        public const float CannonVoxelHeight = 26f;
        /// <summary>Size of the cannon model relative to the castle's voxel size.</summary>
        public const float CannonScale = 0.65f;
        /// <summary>Size of the treasure weapons (wall cannons, catapult) relative to the castle's voxel size.</summary>
        public const float WeaponScale = 0.55f;

        private Tower cannon;

        public override float TowerCostMultiplier => TowerPriceMultiplier;

        public override void OnGameStart() => cannon = SiegeCastleTower.Create(CastleObject);

        public override bool OnClicked()
        {
            if (cannon == null) return false;
            cannon.SpawnUI();
            return true;
        }
    }

    /// <summary>
    /// Ballistic aiming for lobbed shells (constant gravity, no drag), "angle first": a launch angle is picked from the
    /// distance (flat when close, up to <see cref="FarAngle"/> at <see cref="FarDistance"/>) and the speed that lands on
    /// the target is solved for: v² = g d² / (2 cos²θ (d tanθ − h)). The cannon therefore never aims downwards
    /// (well within the 10° limit). If that speed would be a weak dribble (target very close below the muzzle), the
    /// shell is lobbed instead: the steep solution of tan θ = (v² + √(v⁴ − g(g d² + 2 h v²))) / (g d) at a fixed lob
    /// speed, which is near vertical right next to the castle.
    /// </summary>
    internal static class Ballistics
    {
        public const float Gravity = 12f;
        public const float FarAngle = 30f, FarDistance = 14f;
        public const float LobSpeed = 5.5f;

        /// <summary>Launch velocity and flight time from <paramref name="from"/> to <paramref name="to"/>.</summary>
        public static bool Solve(Vector3 from, Vector3 to, out Vector3 velocity, out float time)
        {
            Vector3 flat = to - from;
            float h = flat.y;
            flat.y = 0f;
            float d = flat.magnitude;
            Vector3 dir = d > 0.001f ? flat / d : Vector3.forward;
            float g = Gravity;

            float angle = Mathf.Lerp(0f, FarAngle, Mathf.Clamp01(d / FarDistance));
            float rad = angle * Mathf.Deg2Rad;
            float denom = 2f * Mathf.Cos(rad) * Mathf.Cos(rad) * (d * Mathf.Tan(rad) - h);
            float v = denom > 0.001f ? Mathf.Sqrt(g * d * d / denom) : 0f;

            if (v < LobSpeed)
            {
                // Lob: steep arc at a fixed speed (raised if the target were out of reach).
                v = LobSpeed;
                float v2 = v * v;
                float disc = v2 * v2 - g * (g * d * d + 2f * h * v2);
                if (disc < 0f)
                {
                    v = Mathf.Sqrt(g * (h + Mathf.Sqrt(h * h + d * d))) * 1.02f;
                    v2 = v * v;
                    disc = Mathf.Max(0f, v2 * v2 - g * (g * d * d + 2f * h * v2));
                }
                angle = d < 0.05f ? 89f : Mathf.Atan((v2 + Mathf.Sqrt(disc)) / (g * d)) * Mathf.Rad2Deg;
                rad = angle * Mathf.Deg2Rad;
            }

            velocity = dir * (v * Mathf.Cos(rad)) + Vector3.up * (v * Mathf.Sin(rad));
            float vx = v * Mathf.Cos(rad);
            time = vx > 0.01f ? d / vx : (velocity.y + Mathf.Sqrt(Mathf.Max(0f, velocity.y * velocity.y - 2f * g * h))) / g;
            return true;
        }

        /// <summary>Aims at where the enemy will be when the shell lands (one refinement step).</summary>
        public static void SolveLeading(Vector3 from, Enemy target, out Vector3 velocity, out Vector3 aimPoint)
        {
            aimPoint = target.transform.position;
            Solve(from, aimPoint, out velocity, out float t);
            try { aimPoint = target.GetFuturePosition(t); }
            catch { /* path end: keep the current position */ }
            Solve(from, aimPoint, out velocity, out _);
        }
    }

    /// <summary>Marker + upgrade logic for the castle's cannon tower; also hosts the treasure weapons.</summary>
    [HarmonyPatch]
    internal sealed class SiegeCastleTower : MonoBehaviour
    {
        // Base stats (vanilla cannon: 20 damage, 20 rpm, range 6)
        public const float BaseRange = 12f;
        public const float BlastRadius = 1.1f;
        // Cards
        public const int DamagePerHeavierShot = 4;
        public const float FireRatePerQuickReload = 0.12f;
        public const float RangePerLongBarrel = 1.5f;
        // Treasures
        public const float WallCannonInterval = 0.6f, WallCannonDamage = 0.4f;
        public const int GrapeshotPellets = 3;
        public const float GrapeshotDamage = 0.4f;
        public const float CatapultInterval = 6f, CatapultRange = 18f, CatapultDamage = 2f, CatapultBlast = 1f;

        private Tower tower;
        private float refresh;
        private readonly List<SiegeWeapon> weapons = new List<SiegeWeapon>();
        private static bool Has(string id) => TreasureInventory.Owns(id);

        public static Tower Create(GameObject castle)
        {
            BuildButtonUI button = Resources.FindObjectsOfTypeAll<BuildButtonUI>()
                .FirstOrDefault(b => b.gameObject.scene.IsValid() && b.tower != null && b.tower.GetComponent<Cannon>() != null);
            if (button == null) { Plugin.Log.LogError("Siege Workshop: no Cannon tower prefab found."); return null; }

            Vector3 c = castle.transform.position;
            GameObject prefab = button.tower;
            bool was = prefab.activeSelf;
            prefab.SetActive(false); // add the marker before Start runs
            GameObject go = Object.Instantiate(prefab, new Vector3(c.x, 1f / 3f, c.z), Quaternion.identity);
            prefab.SetActive(was);
            go.name = "TR_SiegeCannon";
            var marker = go.AddComponent<SiegeCastleTower>();
            Tower t = go.GetComponent<Tower>();
            marker.tower = t;
            t.baseRange = BaseRange;
            marker.Dress(castle);
            go.SetActive(true);
            return t;
        }

        /// <summary>Turret onto the roof; vanilla art hidden, our voxel cannon shown; clicks go through the castle.</summary>
        private void Dress(GameObject castle)
        {
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            foreach (Collider col in GetComponentsInChildren<Collider>(true)) col.enabled = false;

            Transform mesh = castle.transform.Find("default");
            Transform turret = tower.turret.transform;
            turret.position = mesh.TransformPoint(new Vector3(0f, SiegeWorkshopBehaviour.CannonVoxelHeight * 0.1f, 0f));
            float scale = mesh.lossyScale.x * SiegeWorkshopBehaviour.CannonScale;

            VoxAsset asset = VoxelLibrary.Get("castles/siege_cannon");
            if (asset != null)
            {
                var art = new GameObject("TR_CannonArt");
                art.transform.SetParent(turret, false);
                art.transform.localScale = Vector3.one * scale / Mathf.Max(0.0001f, turret.lossyScale.x);
                art.AddComponent<MeshFilter>().sharedMesh = asset.Mesh;
                art.AddComponent<MeshRenderer>().sharedMaterial = asset.Material;
            }
            // Barrel tip of the voxel cannon: 14.5 voxels forward, 7.5 up (see CastleRecipes2.SiegeCannon).
            tower.muzzle.localPosition = new Vector3(0f, 7.5f, 14.5f) * 0.1f * scale / Mathf.Max(0.0001f, turret.lossyScale.x);
        }

        private void Update()
        {
            refresh -= Time.deltaTime;
            if (refresh > 0f) return;
            refresh = 1f;
            tower.SetStats(); // picks up new cards and treasures (the castle cannon is not in a flyweight)
            EnsureWeapon(CastleTreasures.WallCannons, () => SiegeWeapon.WallCannons(this));
            EnsureWeapon(CastleTreasures.Catapult, () => SiegeWeapon.Catapult(this));
        }

        private void EnsureWeapon(string treasure, Func<SiegeWeapon> create)
        {
            if (!Has(treasure))
            {
                // Sold at a merchant: the weapon comes off the castle.
                foreach (SiegeWeapon old in weapons.Where(w => w != null && w.Treasure == treasure).ToList())
                {
                    weapons.Remove(old);
                    Destroy(old.gameObject);
                }
                return;
            }
            if (weapons.Any(w => w.Treasure == treasure)) return;
            SiegeWeapon w = create();
            if (w != null) weapons.Add(w);
        }

        internal Tower Tower => tower;
        internal Transform Castle => CastleRuntime.Behaviour?.CastleObject?.transform;

        // ------------------------------------------------------------------ vanilla tower integration

        private static bool IsSiege(Tower t) => t != null && t.GetComponent<SiegeCastleTower>() != null;

        /// <summary>Vanilla Start, minus registering as a built tower (price increase, tower counts) and houses.</summary>
        [HarmonyPrefix, HarmonyPatch(typeof(Tower), nameof(Tower.Start))]
        private static bool TowerStartPrefix(Tower __instance)
        {
            if (!IsSiege(__instance)) return true;
            __instance.level = 1;
            __instance.SetStats();
            __instance.priorities[0] = __instance.priorities[1] = __instance.priorities[2] = Tower.Priority.Progress;
            return false;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Tower), nameof(Tower.SetStats))]
        private static void SetStatsPostfix(Tower __instance)
        {
            if (!IsSiege(__instance)) return;
            int damage = __instance.damage + DamagePerHeavierShot * CardCounters.Get(CastleCards.HeavierShot);
            if (Has(CastleTreasures.SiegeEngineer)) damage += Object.FindObjectsOfType<Tower>().Count(t => !IsSiege(t) && !Pois.LabSpecimen.Is(t));
            if (Has(CastleTreasures.GrandBombard)) damage *= 3;
            __instance.damage = damage;
            __instance.rps /= 1f + FireRatePerQuickReload * CardCounters.Get(CastleCards.QuickReload);
            if (Has(CastleTreasures.GrandBombard)) __instance.rps *= 1.3f;
            __instance.range += RangePerLongBarrel * CardCounters.Get(CastleCards.LongBarrel);
            if (Has(CastleTreasures.IncendiaryShells)) __instance.burnPercent += 1f;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Tower), nameof(Tower.Demolish))]
        private static bool DemolishPrefix(Tower __instance) => !IsSiege(__instance);

        /// <summary>The castle cannon's menu has no demolish button.</summary>
        [HarmonyPostfix, HarmonyPatch(typeof(TowerUI), nameof(TowerUI.SetStats))]
        private static void TowerUISetStatsPostfix(TowerUI __instance, Tower _myTower)
        {
            if (!IsSiege(_myTower) || __instance.demolishText == null) return;
            Transform button = __instance.demolishText.transform.parent;
            (button != null && button.GetComponent<UnityEngine.UI.Button>() != null ? button.gameObject : __instance.demolishText.gameObject).SetActive(false);
        }

        /// <summary>The turret points along the shell's launch direction (pitch never below -10°).</summary>
        [HarmonyPrefix, HarmonyPatch(typeof(Tower), nameof(Tower.AimTurret))]
        private static bool AimTurretPrefix(Tower __instance)
        {
            if (!IsSiege(__instance)) return true;
            Enemy target = __instance.currentTarget != null ? __instance.currentTarget.GetComponent<Enemy>() : null;
            if (target == null) return false;
            Ballistics.SolveLeading(__instance.muzzle.position, target, out Vector3 velocity, out _);
            Transform turret = __instance.turret.transform;
            turret.rotation = Quaternion.Slerp(turret.rotation, Quaternion.LookRotation(velocity, Vector3.up), Time.deltaTime * 10f);
            return false;
        }

        /// <summary>Explosive shell on a ballistic arc instead of the vanilla cannonball (+ Grapeshot pellets).</summary>
        [HarmonyPrefix, HarmonyPatch(typeof(Cannon), nameof(Cannon.Fire))]
        private static bool CannonFirePrefix(Cannon __instance)
        {
            if (!IsSiege(__instance)) return true;
            Enemy target = __instance.currentTarget != null ? __instance.currentTarget.GetComponent<Enemy>() : null;
            if (target == null) return false;
            Tower t = __instance;
            Vector3 from = t.muzzle.position;
            Ballistics.SolveLeading(from, target, out Vector3 velocity, out Vector3 aim);
            float ground = target.transform.position.y + 0.2f;
            SiegeShell.Fire(from, velocity, ground, 1.6f, pos => Explode(t, pos, t.damage, BlastRadius + t.blastRadius));
            Fx.VoxelBurst(from, new[] { new Color(0.85f, 0.85f, 0.8f), new Color(0.6f, 0.6f, 0.6f) }, 8, 0.07f, 1.2f, 0.3f, -0.4f);

            if (Has(CastleTreasures.Grapeshot))
            {
                int pellet = Mathf.Max(1, Mathf.RoundToInt(t.damage * GrapeshotDamage));
                foreach (Enemy e in Combat.NearestEnemies(target.transform.position, 3f, GrapeshotPellets, target))
                {
                    Enemy pe = e;
                    SiegeBall.Shoot(from, pe, 22f, 0f, 0.5f, pos => Hit(t, pe, pellet));
                }
            }
            return false;
        }

        // ------------------------------------------------------------------ damage

        /// <summary>A hit with the castle cannon's multipliers and effects (vanilla damage = base × layer multiplier).</summary>
        internal static void Hit(Tower t, Enemy e, int baseDamage)
        {
            if (!Combat.IsAlive(e)) return;
            e.TakeDamage(TowerType.Cannon, baseDamage, t.healthDamage, t.armorDamage, t.shieldDamage,
                t.slowPercent, t.bleedPercent, t.burnPercent, t.poisonPercent, t.critChance, t.stunChance);
        }

        private static readonly Color[] FireColors =
            { new Color(1f, 0.85f, 0.3f), new Color(1f, 0.55f, 0.15f), new Color(0.85f, 0.3f, 0.1f), new Color(0.3f, 0.28f, 0.27f) };

        internal static void Explode(Tower t, Vector3 pos, int damage, float radius)
        {
            Fx.VoxelBurst(pos + Vector3.up * 0.15f, FireColors, 18, 0.11f, 2.2f * radius, 0.55f, 1.2f);
            foreach (Enemy e in Combat.EnemiesInRadius(pos, radius)) Hit(t, e, damage);
        }
    }

    /// <summary>
    /// Extra castle weapons from treasures: voxel models on the castle that turn towards their target. Wall cannons sit
    /// on the four corner towers (single target); the catapult stands on the roof by the wall (small area, animated arm).
    /// </summary>
    internal sealed class SiegeWeapon : MonoBehaviour
    {
        public string Treasure;
        private SiegeCastleTower owner;
        private Transform[] mounts;
        private int nextMount;
        private float interval, timer;
        private Func<float> rangeOf;
        private bool farthest;
        private Action<SiegeWeapon, Transform, Enemy> fire;

        private static readonly Color[] RockColors =
            { new Color(0.55f, 0.52f, 0.47f), new Color(0.42f, 0.4f, 0.36f), new Color(0.47f, 0.33f, 0.2f) };

        public static SiegeWeapon WallCannons(SiegeCastleTower owner) =>
            Create(owner, CastleTreasures.WallCannons, "castles/siege_wall_cannon", null,
                new[] { new Vector3(-12.5f, 27f, -12.5f), new Vector3(12.5f, 27f, -12.5f), new Vector3(-12.5f, 27f, 12.5f), new Vector3(12.5f, 27f, 12.5f) },
                SiegeCastleTower.WallCannonInterval, () => owner.Tower.range, false, (w, mount, target) =>
                {
                    int dmg = Mathf.Max(1, Mathf.RoundToInt(owner.Tower.damage * SiegeCastleTower.WallCannonDamage));
                    Tower t = owner.Tower;
                    Vector3 muzzle = mount.position + mount.forward * 0.35f + Vector3.up * 0.15f;
                    Fx.VoxelBurst(muzzle, new[] { new Color(0.85f, 0.85f, 0.8f) }, 4, 0.05f, 0.8f, 0.25f, -0.3f);
                    SiegeBall.Shoot(muzzle, target, 20f, 0f, 0.5f, pos => SiegeCastleTower.Hit(t, target, dmg));
                });

        public static SiegeWeapon Catapult(SiegeCastleTower owner) =>
            Create(owner, CastleTreasures.Catapult, "castles/siege_catapult", "castles/siege_catapult_arm",
                new[] { new Vector3(0f, 24f, 11f) },
                SiegeCastleTower.CatapultInterval, () => SiegeCastleTower.CatapultRange, true, (w, mount, first) =>
                {
                    int volleys = TreasureInventory.Owns(CastleTreasures.TrebuchetBattery) ? 3 : 1;
                    int dmg = Mathf.RoundToInt(owner.Tower.damage * SiegeCastleTower.CatapultDamage);
                    Tower t = owner.Tower;
                    List<Enemy> targets = Combat.EnemiesInRadius(owner.transform.position, SiegeCastleTower.CatapultRange)
                        .OrderByDescending(e => e.pathfinder != null ? e.pathfinder.distanceFromEnd : 0f).Take(volleys).ToList();
                    CatapultArm arm = mount.GetComponentInChildren<CatapultArm>();
                    void Release(Vector3 from)
                    {
                        foreach (Enemy target in targets)
                        {
                            Enemy e = target;
                            if (!Combat.IsAlive(e)) continue;
                            Ballistics.SolveLeading(from, e, out Vector3 velocity, out _);
                            SiegeShell.Fire(from, velocity, e.transform.position.y + 0.2f, 1.2f, pos =>
                            {
                                Fx.VoxelBurst(pos + Vector3.up * 0.1f, RockColors, 14, 0.09f, 2.4f, 0.7f, 2f);
                                foreach (Enemy hit in Combat.EnemiesInRadius(pos, SiegeCastleTower.CatapultBlast)) SiegeCastleTower.Hit(t, hit, dmg);
                            });
                        }
                    }
                    if (arm != null) arm.Throw(Release);
                    else Release(mount.position + Vector3.up * 0.6f);
                });

        private static SiegeWeapon Create(SiegeCastleTower owner, string treasure, string model, string armModel, Vector3[] voxelPositions,
                                          float interval, Func<float> range, bool farthest, Action<SiegeWeapon, Transform, Enemy> fire)
        {
            Transform castle = owner.Castle;
            Transform mesh = castle != null ? castle.Find("default") : null;
            VoxAsset asset = VoxelLibrary.Get(model);
            VoxAsset arm = armModel != null ? VoxelLibrary.Get(armModel) : null;
            if (mesh == null || asset == null) return null;
            var root = new GameObject("TR_SiegeWeapon_" + treasure);
            var w = root.AddComponent<SiegeWeapon>();
            w.Treasure = treasure;
            w.owner = owner;
            w.interval = interval;
            w.rangeOf = range;
            w.farthest = farthest;
            w.fire = fire;
            w.mounts = voxelPositions.Select(p =>
            {
                var mount = new GameObject("Mount");
                mount.transform.SetParent(root.transform, false);
                mount.transform.position = mesh.TransformPoint(p * 0.1f);
                mount.transform.localScale = Vector3.one * mesh.lossyScale.x * SiegeWorkshopBehaviour.WeaponScale;
                mount.AddComponent<MeshFilter>().sharedMesh = asset.Mesh;
                mount.AddComponent<MeshRenderer>().sharedMaterial = asset.Material;
                if (arm != null) CatapultArm.Attach(mount.transform, arm);
                return mount.transform;
            }).ToArray();
            return w;
        }

        public Enemy Target(float range)
        {
            var list = Combat.EnemiesInRadius(owner.transform.position, range);
            if (list.Count == 0) return null;
            return farthest
                ? list.OrderByDescending(e => e.pathfinder != null ? e.pathfinder.distanceFromEnd : 0f).First()
                : list.OrderBy(e => e.pathfinder != null ? e.pathfinder.distanceFromEnd : 0f).First();
        }

        private void Update()
        {
            if (SpawnManager.instance == null || GameManager.instance.gameOver) return;
            Enemy aim = Target(rangeOf());
            // Every mount keeps facing the monster it would shoot (the catapult throws over its front, bucket at the back).
            if (aim != null)
                foreach (Transform m in mounts)
                {
                    Vector3 d = aim.transform.position - m.position;
                    d.y = 0f;
                    if (d.sqrMagnitude > 0.01f)
                        m.rotation = Quaternion.Slerp(m.rotation, Quaternion.LookRotation(d, Vector3.up), Time.deltaTime * 8f);
                }

            if (!SpawnManager.instance.combat) return;
            timer -= Time.deltaTime;
            if (timer > 0f || aim == null) return;
            timer = interval;
            Transform mount = mounts[nextMount];
            nextMount = (nextMount + 1) % mounts.Length;
            // Wall cannons: each mount shoots at what it faces (the nearest monster to the castle).
            try { fire(this, mount, aim); }
            catch (Exception e) { Plugin.Log.LogError($"Siege weapon failed: {e}"); }
        }
    }

    /// <summary>
    /// The catapult's throwing arm: rests pointing back and down (bucket behind the catapult), swings up and over the
    /// front when throwing, releases the boulder at the top, then winds back slowly.
    /// </summary>
    internal sealed class CatapultArm : MonoBehaviour
    {
        // Arm model: bucket at voxel y 0, axle at y 13.5 / z 1.5 (CastleRecipes2.SiegeCatapultArm); frame axle at
        // voxel y 7 / z 8.5 of the 12x16 frame model (centre y 8).
        private const float RestAngle = -20f, ThrowAngle = 115f;
        private Transform bucket;
        private bool busy;

        public static CatapultArm Attach(Transform frame, VoxAsset armAsset)
        {
            var pivot = new GameObject("Arm");
            pivot.transform.SetParent(frame, false);
            pivot.transform.localPosition = new Vector3(0f, 0.85f, -0.1f);
            var art = new GameObject("ArmArt");
            art.transform.SetParent(pivot.transform, false);
            art.transform.localPosition = new Vector3(0f, -0.15f, -0.45f); // axle onto the pivot
            art.AddComponent<MeshFilter>().sharedMesh = armAsset.Mesh;
            art.AddComponent<MeshRenderer>().sharedMaterial = armAsset.Material;
            var bucket = new GameObject("Bucket");
            bucket.transform.SetParent(pivot.transform, false);
            bucket.transform.localPosition = new Vector3(0f, 0.2f, -1.3f);
            var arm = pivot.AddComponent<CatapultArm>();
            arm.bucket = bucket.transform;
            pivot.transform.localRotation = Quaternion.Euler(RestAngle, 0f, 0f);
            return arm;
        }

        public void Throw(Action<Vector3> release)
        {
            if (busy) return;
            StartCoroutine(Animate(release));
        }

        private IEnumerator Animate(Action<Vector3> release)
        {
            busy = true;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.18f)
            {
                transform.localRotation = Quaternion.Euler(Mathf.Lerp(RestAngle, ThrowAngle, t * t), 0f, 0f);
                yield return null;
            }
            transform.localRotation = Quaternion.Euler(ThrowAngle, 0f, 0f);
            try { release(bucket.position); }
            catch (Exception e) { Plugin.Log.LogError($"Catapult release failed: {e}"); }
            yield return new WaitForSeconds(0.25f);
            for (float t = 0f; t < 1f; t += Time.deltaTime / 1.2f)
            {
                transform.localRotation = Quaternion.Euler(Mathf.Lerp(ThrowAngle, RestAngle, Mathf.SmoothStep(0f, 1f, t)), 0f, 0f);
                yield return null;
            }
            transform.localRotation = Quaternion.Euler(RestAngle, 0f, 0f);
            busy = false;
        }
    }

    /// <summary>A projectile on a true ballistic arc (gravity = Ballistics.Gravity) that calls back on landing.</summary>
    internal sealed class SiegeShell : MonoBehaviour
    {
        private Vector3 velocity;
        private float groundY, life = 6f;
        private Action<Vector3> onImpact;

        public static void Fire(Vector3 from, Vector3 velocity, float groundY, float scale, Action<Vector3> onImpact)
        {
            var go = new GameObject("TR_SiegeShell");
            go.transform.position = from;
            SiegeBall.AddArt(go.transform, scale);
            var shell = go.AddComponent<SiegeShell>();
            shell.velocity = velocity;
            shell.groundY = groundY;
            shell.onImpact = onImpact;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            velocity.y -= Ballistics.Gravity * dt;
            transform.position += velocity * dt;
            transform.Rotate(220f * dt, 90f * dt, 0f, Space.Self); // tumble
            life -= dt;
            if ((velocity.y < 0f && transform.position.y <= groundY) || life <= 0f)
            {
                Vector3 p = transform.position;
                p.y = groundY;
                try { onImpact?.Invoke(p); }
                catch (Exception e) { Plugin.Log.LogError($"Siege shell impact failed: {e}"); }
                Destroy(gameObject);
            }
        }
    }

    /// <summary>Blocky voxel cannonballs flying straight (or lobbed by interpolation) to a target, for small shots.</summary>
    internal sealed class SiegeBall : MonoBehaviour
    {
        private Vector3 from, to;
        private Enemy target;
        private float duration, arc, t;
        private Action<Vector3> onImpact;

        public static void AddArt(Transform parent, float scale)
        {
            VoxAsset ball = VoxelLibrary.Get("castles/siege_ball");
            if (ball == null) return;
            var art = new GameObject("Art");
            art.transform.SetParent(parent, false);
            art.transform.localScale = Vector3.one * 0.35f * scale; // 5 voxels at 0.1 → ~0.18 world at scale 1
            art.transform.localPosition = -ball.Mesh.bounds.center * 0.35f * scale;
            art.AddComponent<MeshFilter>().sharedMesh = ball.Mesh;
            art.AddComponent<MeshRenderer>().sharedMaterial = ball.Material;
        }

        /// <summary>Flies to the target at <paramref name="speed"/>, in a straight line (arc 0) or lobbed.</summary>
        public static void Shoot(Vector3 from, Enemy target, float speed, float arc, float scale, Action<Vector3> onImpact)
        {
            if (target == null) return;
            var go = new GameObject("TR_SiegeShot");
            go.transform.position = from;
            AddArt(go.transform, scale);
            var shot = go.AddComponent<SiegeBall>();
            shot.from = from;
            shot.target = target;
            shot.to = target.transform.position + Vector3.up * 0.25f;
            shot.duration = Mathf.Max(0.05f, Vector3.Distance(from, shot.to) / speed);
            shot.arc = arc;
            shot.onImpact = onImpact;
        }

        private void Update()
        {
            t += Time.deltaTime / duration;
            if (target != null && !target.dead) to = target.transform.position + Vector3.up * 0.25f;
            Vector3 p = Vector3.Lerp(from, to, Mathf.Min(1f, t));
            p.y += arc * 4f * t * (1f - t);
            transform.position = p;
            if (t < 1f) return;
            try { onImpact?.Invoke(to); }
            catch (Exception e) { Plugin.Log.LogError($"Siege shot impact failed: {e}"); }
            Destroy(gameObject);
        }
    }
}

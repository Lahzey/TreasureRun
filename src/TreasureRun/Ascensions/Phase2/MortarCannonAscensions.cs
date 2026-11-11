using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TreasureRun.Ascensions.Phase1;
using TreasureRun.Audio;
using TreasureRun.Cards;
using TreasureRun.Core;
using TreasureRun.Treasures;
using TreasureRun.Voxels;
using UnityEngine;
using UnityEngine.UI;

namespace TreasureRun.Ascensions.Phase2
{
    /// <summary>
    /// Cluster Munitions (Mortar, Rare): at 2/3 of its flight every shell splits into 5 bomblets (+1 per More Bomblets
    /// card) with 50% damage and 50% blast radius each. They land on a small ring around the target point, so a monster
    /// in the middle can be hit by all of them (<see cref="ClusterMunitionsLogic"/>).
    /// </summary>
    internal sealed class ClusterMunitions : AscendedTower
    {
        public const string Id = "cluster_munitions";
        public const string MoreBombletsCard = "more_bomblets";

        /// <summary>Set while a cluster mortar's vanilla Fire runs (the Projectile.SetStats postfix tags its shell).</summary>
        internal static ClusterMunitions Firing;
        public int Splits { get; internal set; }

        public static void Register()
        {
            AscensionRegistry.Register(new AscensionDef
            {
                Id = Id, Name = "Cluster Munitions", TowerName = "Cluster Mortar", Rarity = Rarity.Rare, Kind = AscensionKind.Augment,
                Target = AscensionTarget.Mortar, Component = typeof(ClusterMunitions), CardModel = "ascensions/mortar/cluster_mortar",
                Description = $"Shells split into {ClusterMunitionsLogic.BaseBomblets} bomblets in mid-air, each with " +
                              $"{ClusterMunitionsLogic.DamageShare:P0} of the damage and blast radius. They land close together: " +
                              "a monster in the middle can be hit by all of them.",
            });
        }

        public static void RegisterCards()
        {
            CustomCards.AscensionChain(Id, MoreBombletsCard, "More Bomblets", 5, "Tower - Mortar",
                _ => $"Cluster Munitions shells split into {ClusterMunitionsLogic.BombletsPerCard} more bomblet.", () => { });
        }

        protected override void OnAscend() => SwapVanillaMesh("BaseArt", "ascensions/mortar/cluster_mortar");
    }

    /// <summary>Rides on a cluster mortar's shell and splits it at 2/3 of the flight.</summary>
    internal sealed class ClusterShell : MonoBehaviour
    {
        private MorterShell shell;
        private ClusterMunitions owner;
        private float elapsed;
        private bool split;

        public static void Attach(MorterShell shell, ClusterMunitions owner)
        {
            var c = shell.gameObject.AddComponent<ClusterShell>();
            c.shell = shell;
            c.owner = owner;
        }

        private void FixedUpdate()
        {
            elapsed += Time.fixedDeltaTime;
            if (split || shell == null || shell.timeOfFlight <= 0f || elapsed < shell.timeOfFlight * ClusterMunitionsLogic.SplitAt) return;
            split = true;
            Split();
        }

        private void Split()
        {
            MorterShell s = shell;
            Vector3 from = transform.position;
            float remaining = Mathf.Max(0.15f, s.timeOfFlight - elapsed);
            float radius = ClusterMunitionsLogic.BombletRadius(s.blastRadius);
            int damage = ClusterMunitionsLogic.BombletDamage(s.damage);
            int count = ClusterMunitionsLogic.Count(CardCounters.Get(ClusterMunitions.MoreBombletsCard));
            List<Vector3> landings = ClusterMunitionsLogic.LandingPoints(s.destination, count, radius, GameRng.Instance);
            TowerType by = s.shotBy;
            int h = s.healthDamage, a = s.armorDamage, sh = s.shieldDamage;
            float slow = s.slowPercent, bleed = s.bleedPercent, burn = s.burnPercent, poison = s.poisonPercent, crit = s.critChance, stun = s.stunChance;
            GameObject explosion = s.explosion;
            foreach (Vector3 landing in landings)
            {
                float time = remaining * Random.Range(0.92f, 1.08f);
                LobShot.Fire("ascensions/mortar/bomblet", 0.45f, from, LobShot.TimedVelocity(from, landing, time), landing.y, pos =>
                {
                    foreach (Enemy e in Combat.EnemiesInRadius(pos, radius + 0.25f))
                        e.TakeDamage(by, damage, h, a, sh, slow, bleed, burn, poison, crit, stun);
                    if (explosion != null) Object.Instantiate(explosion, pos, Quaternion.identity).transform.localScale = Vector3.one * 0.5f;
                    else Fx.VoxelBurst(pos, Phase2Kit.FireColors, 10, 0.08f, 1.5f, 0.4f, 1f);
                }, 300f);
            }
            Fx.VoxelBurst(from, Phase2Kit.SmokeColors, 10, 0.07f, 1.2f, 0.5f, 0f);
            SoundLibrary.Play("ascensions/cluster_pop", from, 0.9f, 0.08f, 3);
            if (owner != null) owner.Splits++;
            if (s.detachOnDestruction != null) s.detachOnDestruction.transform.parent = null;
            if (s.extraFX != null) s.extraFX.OnDetach();
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Firing Line (Cannon, Uncommon): a "Set Target" button in the tower popup lets the player click a spot on the map;
    /// from then on the cannon fires in that direction non-stop while a wave is on, target or not (vanilla cannonballs,
    /// so Ricochet and Collateral still work). Before a direction is set it behaves like a normal Cannon.
    /// </summary>
    internal sealed class FiringLine : AscendedTower
    {
        public const string Id = "firing_line";

        public Vector3? Direction { get; private set; }
        public Vector3 AimPoint { get; private set; }
        public int Shots { get; private set; }
        private GameObject flag;

        public static void Register()
        {
            AscensionRegistry.Register(new AscensionDef
            {
                Id = Id, Name = "Firing Line", TowerName = "Line Cannon", Rarity = Rarity.Uncommon, Kind = AscensionKind.Augment,
                Target = AscensionTarget.Cannon, Component = typeof(FiringLine), CardModel = "ascensions/cannon/line_cannon",
                Description = "Adds a \"Set Target\" button to the Cannon: pick a spot on the map and the cannon fires in that " +
                              "direction non-stop while a wave is on, whether or not a monster is in range. Its balls fly faster and further.",
            });
        }

        protected override void OnAscend() => SwapVanillaMesh("TurretArt", "ascensions/cannon/line_cannon");

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (flag != null) Destroy(flag);
            if (FiringLineAim.Aiming == this) FiringLineAim.Cancel();
        }

        /// <summary>Sets the firing direction towards a point; false if the point is too close.</summary>
        public bool SetAim(Vector3 point)
        {
            Vector3? d = FiringLineLogic.Direction(transform.position, point);
            if (!d.HasValue) return false;
            Direction = d;
            AimPoint = point;
            if (flag == null) flag = Phase1Kit.Model("ascensions/cannon/aim_flag", 0.7f);
            flag.transform.position = point + Vector3.up * 0.3f;
            flag.transform.rotation = Quaternion.LookRotation(d.Value, Vector3.up);
            return true;
        }

        private void LateUpdate()
        {
            if (flag == null) return;
            bool show = TowerSelection.IsSelected(Tower) || FiringLineAim.Aiming == this;
            if (flag.activeSelf != show) flag.SetActive(show);
        }

        /// <summary>Set while this cannon's vanilla Fire runs (the Projectile.SetStats postfix speeds its ball up).</summary>
        internal static FiringLine Firing;

        /// <summary>Replaces Tower.Update while a direction is set.</summary>
        internal void Tick()
        {
            Tower t = Tower;
            if (t.currentTarget != null) t.GainXP();
            if (t.turret != null)
                t.turret.transform.rotation = Quaternion.Slerp(t.turret.transform.rotation, Quaternion.LookRotation(Direction.Value, Vector3.up), Time.deltaTime * 8f);
            t.timeSinceLastShot += Time.deltaTime;
            bool combat = SpawnManager.instance != null && SpawnManager.instance.combat;
            if (!FiringLineLogic.Fires(combat, true) || t.timeSinceLastShot <= t.rps) return;
            t.timeSinceLastShot = 0f;
            GameObject saved = t.currentTarget;
            t.currentTarget = null; // no homing: the ball flies straight along the line
            try
            {
                if (t.turret != null) t.turret.transform.rotation = Quaternion.LookRotation(Direction.Value, Vector3.up);
                Firing = this;
                t.Fire();
                Shots++;
            }
            finally
            {
                Firing = null;
                t.currentTarget = saved;
            }
        }
    }

    /// <summary>Aim mode of Firing Line: a target marker follows the mouse; left click sets it, right click / Esc cancels.</summary>
    internal sealed class FiringLineAim : MonoBehaviour
    {
        internal static FiringLine Aiming;
        private static FiringLineAim instance;
        private GameObject marker;
        private LineRenderer line;
        private int startFrame;

        public static void Begin(FiringLine cannon)
        {
            if (instance == null)
            {
                var go = new GameObject("TR_FiringLineAim");
                instance = go.AddComponent<FiringLineAim>();
            }
            Aiming = cannon;
            instance.startFrame = Time.frameCount;
            instance.enabled = true;
            if (instance.marker == null) instance.marker = Phase1Kit.Model("ascensions/cannon/target_marker", 0.6f, instance.transform);
            if (instance.line == null) instance.line = Phase2Kit.Line("AimLine", new Color(1f, 0.3f, 0.2f, 0.6f), 0.05f, instance.transform);
            instance.marker.SetActive(false);
            instance.line.enabled = false;
        }

        public static void Cancel()
        {
            Aiming = null;
            if (instance == null) return;
            if (instance.marker != null) instance.marker.SetActive(false);
            if (instance.line != null) instance.line.enabled = false;
        }

        private void Update()
        {
            if (Aiming == null) return;
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)) { Cancel(); return; }
            Camera cam = Camera.main;
            if (cam == null || BuildingManager.instance == null) return;
            // Grass and the track (the build mask has no path layer).
            int mask = BuildingManager.instance.buildableMask | (1 << LayerMask.NameToLayer("Path"));
            bool hit = Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition), out RaycastHit h, 2000f, mask, QueryTriggerInteraction.Ignore);
            Vector3 cannon = Aiming.transform.position;
            bool valid = hit && FiringLineLogic.Direction(cannon, h.point).HasValue;
            marker.SetActive(hit);
            line.enabled = hit;
            if (hit)
            {
                marker.transform.position = h.point + Vector3.up * 0.03f;
                marker.transform.Rotate(0f, 60f * Time.unscaledDeltaTime, 0f, Space.World);
                line.SetPosition(0, cannon + Vector3.up * 0.8f);
                line.SetPosition(1, h.point + Vector3.up * 0.1f);
                Color c = valid ? new Color(1f, 0.3f, 0.2f, 0.7f) : new Color(0.5f, 0.5f, 0.5f, 0.5f);
                line.startColor = line.endColor = c;
            }
            if (Input.GetMouseButtonDown(0) && Time.frameCount > startFrame && valid)
            {
                Aiming.SetAim(h.point);
                SFXManager.instance?.ButtonClick();
                Cancel();
            }
        }
    }

    /// <summary>
    /// Hwacha (Cannon, Epic): the cannon becomes a rocket cart. Each attack launches a volley of rockets (20 by default,
    /// <c>Ascensions.HwachaRockets</c> in the config) over about a second, each flying to a random point around the
    /// target with a small blast (<see cref="HwachaLogic"/>). Reloads twice as long as a Cannon; the rack shows empty
    /// slots until it is half reloaded.
    /// </summary>
    internal sealed class Hwacha : AscendedTower, IMinRange
    {
        public float MinRange => HwachaLogic.MinRange;
        public const string Id = "hwacha";
        private const string Loaded = "ascensions/cannon/hwacha_loaded", Empty = "ascensions/cannon/hwacha_empty";

        private MeshFilter rack;
        private Mesh loadedMesh, emptyMesh;
        private bool firing;
        public int Rockets { get; private set; }

        public static void Register()
        {
            AscensionRegistry.Register(new AscensionDef
            {
                Id = Id, Name = "Hwacha", Rarity = Rarity.Epic, Kind = AscensionKind.Replace,
                Target = AscensionTarget.Cannon, Component = typeof(Hwacha), CardModel = Loaded,
                Description = $"Instead of cannonballs, fires a volley of {HwachaLogic.DefaultRockets} rockets that land around the " +
                              $"target, each with a small blast ({HwachaLogic.DamageFactor:P0} damage, {HwachaLogic.Burn:P0} burn). " +
                              $"Reloads {HwachaLogic.IntervalFactor:0}x as long and cannot target monsters within {HwachaLogic.MinRange:0} of the cart.",
            });
            StatLayer.Register("Hwacha", (tower, stats) =>
            {
                if (tower.GetComponent<Hwacha>() != null) stats.Multiply(TowerStat.AttackSpeed, 1f / HwachaLogic.IntervalFactor);
            });
        }

        protected override void OnAscend()
        {
            SwapVanillaMesh("TurretArt", Loaded);
            loadedMesh = VoxelLibrary.Get(Loaded)?.Mesh;
            emptyMesh = VoxelLibrary.Get(Empty)?.Mesh;
            rack = Phase2Kit.PartFilter(gameObject, "TurretArt", loadedMesh != null ? loadedMesh.name : null);
            indicator = MinRangeIndicator.Attach(Tower, HwachaLogic.MinRange);
        }

        private MinRangeIndicator indicator;

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (indicator != null) Destroy(indicator.gameObject);
        }

        internal void Fire()
        {
            Enemy target = Phase2Kit.Target(Tower);
            if (target == null || firing) return;
            if (MinRangeRules.TooClose(Tower, target.transform.position)) { Tower.currentTarget = null; return; }
            if (!Phase1Kit.PayMana(Tower, Tower.finalManaConsumption)) return;
            StartCoroutine(Volley(target));
        }

        private IEnumerator Volley(Enemy target)
        {
            firing = true;
            Tower t = Tower;
            int count = Mathf.Max(1, ModConfig.HwachaRockets != null ? ModConfig.HwachaRockets.Value : HwachaLogic.DefaultRockets);
            int damage = HwachaLogic.RocketDamage(t.damage);
            float radius = HwachaLogic.BlastRadius + t.blastRadius * 0.5f; // radius around the landing point
            Vector3 last = target.transform.position;
            float start = Time.time;
            Transform art = rack != null ? rack.transform : null;
            Vector3 rest = art != null ? art.localPosition : Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                while (Time.time - start < HwachaLogic.LaunchDelay(i, count)) yield return null;
                if (Combat.IsAlive(target)) last = target.transform.position;
                Transform muzzle = t.muzzle;
                Vector3 right = muzzle != null ? muzzle.right : Vector3.right;
                Vector3 from = (muzzle != null ? muzzle.position : transform.position + Vector3.up)
                             + right * ((i % 5) - 2f) * 0.07f + Vector3.up * ((i / 5) % 4 - 1.5f) * 0.06f;
                HwachaRocket.Launch(this, from, muzzle != null ? muzzle.forward : transform.forward, Combat.IsAlive(target) ? target : null,
                                    last, HwachaLogic.ScatterOffset(GameRng.Instance), damage, radius);
                Rockets++;
                if (art != null) art.localPosition = rest - Vector3.forward * 0.03f;
                SoundLibrary.Play("ascensions/rocket_whoosh", from, 0.55f, 0.1f, 5);
                yield return null;
                if (art != null) art.localPosition = rest;
            }
            if (rack != null && emptyMesh != null) rack.sharedMesh = emptyMesh;
            firing = false;
            yield return new WaitForSeconds(Mathf.Max(0.3f, t.rps * 0.5f));
            if (rack != null && loadedMesh != null && rack.sharedMesh == emptyMesh) rack.sharedMesh = loadedMesh;
        }
    }

    /// <summary>A Hwacha rocket: a curved flight (quadratic Bézier) to a point near its target, smoke trail, small blast.</summary>
    internal sealed class HwachaRocket : MonoBehaviour
    {
        /// <summary>Firework-like blast colours: gold, orange, red, a magenta and a green spark.</summary>
        private static readonly Color[] RocketColors =
        {
            new Color(1f, 0.9f, 0.35f), new Color(1f, 0.55f, 0.1f), new Color(0.95f, 0.2f, 0.15f),
            new Color(0.95f, 0.35f, 0.85f), new Color(0.45f, 0.95f, 0.4f),
        };
        private Hwacha owner;
        private Tower tower;
        private Vector3 p0, p1, end, offset;
        private Enemy target;
        private float duration, t;
        private int damage;
        private float radius;

        public static void Launch(Hwacha owner, Vector3 from, Vector3 forward, Enemy target, Vector3 targetPos, Vector3 offset, int damage, float radius)
        {
            GameObject go = Phase1Kit.Model("ascensions/cannon/rocket", 0.3f);
            go.transform.position = from;
            var r = go.AddComponent<HwachaRocket>();
            r.owner = owner;
            r.tower = owner.Tower;
            r.target = target;
            r.offset = offset;
            r.p0 = from;
            r.end = targetPos + offset;
            float dist = Vector3.Distance(from, r.end);
            Vector3 fwd = Phase1Kit.Flat(forward).normalized;
            r.p1 = from + fwd * Mathf.Min(1.5f, dist * 0.35f) + Vector3.up * (0.8f + dist * 0.15f) + Random.insideUnitSphere * 0.4f;
            r.duration = 0.35f + dist / 9f;
            r.damage = damage;
            r.radius = radius;
            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = Fx.GlowMaterial;
            trail.time = 0.35f;
            trail.startWidth = 0.07f;
            trail.endWidth = 0.01f;
            trail.startColor = new Color(0.85f, 0.82f, 0.78f, 0.7f);
            trail.endColor = new Color(0.6f, 0.6f, 0.6f, 0f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void Update()
        {
            if (Combat.IsAlive(target)) end = target.transform.position + offset;
            t = Mathf.Min(1f, t + Time.deltaTime / duration);
            float u = 1f - t;
            Vector3 pos = u * u * p0 + 2f * u * t * p1 + t * t * end;
            Vector3 tangent = 2f * u * (p1 - p0) + 2f * t * (end - p1);
            if (tangent.sqrMagnitude > 1e-5f) transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);
            transform.position = pos;
            if (t < 1f) return;
            if (tower != null) Phase2Kit.Blast(tower, end, radius + 0.25f, damage, HwachaLogic.Burn); // + 0.25: monster collider
            Fx.VoxelBurst(end + Vector3.up * 0.12f, RocketColors, 14, 0.075f, 1.9f, 0.45f, 1.5f);
            Fx.Burst(end + Vector3.up * 0.15f, RocketColors[Random.Range(0, 3)], 8, 0.08f, 1.4f);
            SoundLibrary.Play("ascensions/rocket_pop", end, 0.5f, 0.12f, 4);
            Destroy(gameObject);
        }
    }

    [HarmonyPatch]
    internal static class MortarCannonPatches
    {
        [HarmonyPrefix, HarmonyPatch(typeof(Morter), nameof(Morter.Fire))]
        private static void MorterFirePrefix(Morter __instance)
        {
            ClusterMunitions.Firing = TreasureRunMode.Active ? __instance.GetComponent<ClusterMunitions>() : null;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Morter), nameof(Morter.Fire))]
        private static void MorterFirePostfix() => ClusterMunitions.Firing = null;

        [HarmonyPostfix, HarmonyPatch(typeof(Projectile), nameof(Projectile.SetStats))]
        private static void ShellSetStatsPostfix(Projectile __instance)
        {
            if (ClusterMunitions.Firing != null && __instance is MorterShell shell) ClusterShell.Attach(shell, ClusterMunitions.Firing);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Projectile), nameof(Projectile.SetStats))]
        private static void LineBallSetStatsPostfix(Projectile __instance)
        {
            if (FiringLine.Firing == null || !(__instance is Cannonball ball)) return;
            // Set on the ball itself (vanilla Cannon.Fire writes the range to the prefab, after this).
            ball.speed *= FiringLineLogic.BallSpeedFactor;
            ball.maximumLifeTime = FiringLine.Firing.Tower.range * FiringLineLogic.TravelFactor / Mathf.Max(0.1f, ball.speed);
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Cannon), nameof(Cannon.Fire)), HarmonyPriority(Priority.High)]
        private static bool CannonFirePrefix(Cannon __instance)
        {
            if (!TreasureRunMode.Active) return true;
            Hwacha h = __instance.GetComponent<Hwacha>();
            if (h == null) return true;
            h.Fire();
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Tower), nameof(Tower.Update))]
        private static bool TowerUpdatePrefix(Tower __instance)
        {
            if (!TreasureRunMode.Active || __instance.towerType != TowerType.Cannon) return true;
            FiringLine line = __instance.GetComponent<FiringLine>();
            if (line == null || !line.Direction.HasValue) return true;
            line.Tick();
            return false;
        }

        /// <summary>While aiming, clicks belong to the aim (no tower popups, Esc does not close other UI).</summary>
        [HarmonyPrefix, HarmonyPatch(typeof(UIManager), nameof(UIManager.Update))]
        private static bool UIManagerUpdatePrefix() => FiringLineAim.Aiming == null;

        private const string ButtonName = "TR_SetTargetButton";

        /// <summary>The "Set Target" button of a Firing Line cannon's popup (a copy of the demolish button, to its left).</summary>
        [HarmonyPostfix, HarmonyPatch(typeof(TowerUI), nameof(TowerUI.SetStats))]
        private static void TowerUiSetStats(TowerUI __instance, Tower _myTower)
        {
            FiringLine line = _myTower != null ? _myTower.GetComponent<FiringLine>() : null;
            if (line == null || __instance.demolishText == null) return;
            Button demolish = __instance.demolishText.GetComponentInParent<Button>();
            if (demolish == null) return;
            Transform existing = demolish.transform.parent.Find(ButtonName);
            if (existing != null) return;
            GameObject go = Object.Instantiate(demolish.gameObject, demolish.transform.parent, false);
            go.name = ButtonName;
            var rt = (RectTransform)go.transform;
            var src = (RectTransform)demolish.transform;
            rt.anchoredPosition = src.anchoredPosition + new Vector2(0f, src.rect.height * src.localScale.y * 1.15f);
            var button = go.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            TowerUI ui = __instance;
            button.onClick.AddListener(() =>
            {
                SFXManager.instance?.ButtonClick();
                FiringLineAim.Begin(line);
                ui.CloseUI();
            });
            foreach (Text text in go.GetComponentsInChildren<Text>(true)) text.text = line.Direction.HasValue ? "Re-aim" : "Set Target";
        }
    }
}

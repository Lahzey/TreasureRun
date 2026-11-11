using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TreasureRun.Ascensions.Phase1;
using TreasureRun.Ascensions.Phase2;
using TreasureRun.Audio;
using TreasureRun.Core;
using TreasureRun.Treasures;
using TreasureRun.Voxels;
using UnityEngine;

namespace TreasureRun.Ascensions.Phase3
{
    /// <summary>
    /// Mega Mortar (Mortar, Legendary): every Mortar becomes a Mortar Module. Modules built in a full square (2×2, 3×3, …)
    /// fuse for good into one Mega Mortar: a single tower in the middle of the square, standing on its highest tile, with
    /// the modules' levels and multipliers averaged (weighted by module count), base damage × modules, blast × side and
    /// +2 range per side above 1 (<see cref="MegaMortarLogic"/>). It only grows: a bigger square around it fuses again.
    /// The absorbed towers are gone, but their modules still count for the Mortar's build and sell prices.
    /// </summary>
    internal sealed class MortarModule : AscendedTower
    {
        public const string Id = "mega_mortar";
        internal static readonly List<MortarModule> All = new List<MortarModule>();

        /// <summary>Set while a Mega Mortar's vanilla Fire runs (the Projectile.SetStats postfix tags its shell).</summary>
        internal static MortarModule Firing;

        public int Size { get; private set; } = 1;
        public bool IsMega => Size > 1;
        /// <summary>Absorbed into a fusion or demolished as a Mega Mortar: no longer counts as modules.</summary>
        internal bool Dissolved;
        public int Shots { get; private set; }
        public MortarSquare Square => new MortarSquare(originX, originZ, Size);
        /// <summary>Height of the lowest tile under it (for the plinth).</summary>
        public float LowY { get; private set; }

        public override string DisplayName => IsMega ? "Mega Mortar" : null;

        private int originX, originZ;
        private MeshFilter art;
        private MeshRenderer artRenderer;
        private BoxCollider box;
        private Vector3 boxSize, boxCenter, turretPos;
        private GameObject plinth;
        private GameObject barrel;
        private float recoil = -1f;
        /// <summary>How far the barrel is pushed in right now (0 = rest, 1 = fully back), for tests.</summary>
        public float RecoilAmount { get; private set; }
        internal GameObject Barrel => barrel;
        internal GameObject Plinth => plinth;
        private bool shellFired;

        /// <summary>Modules hidden inside Mega Mortars (each Mega Mortar is one tower but Size² modules).</summary>
        public static int ExtraModules => All.Where(m => m != null && !m.Dissolved && m.IsMega).Sum(m => m.Size * m.Size - 1);

        public static void Register()
        {
            AscensionRegistry.Register(new AscensionDef
            {
                Id = Id, Name = "Mega Mortar", TowerName = "Mortar Module", Rarity = Rarity.Legendary, Kind = AscensionKind.Replace,
                Target = AscensionTarget.Mortar, Component = typeof(MortarModule), CardModel = "ascensions/mortar/mega_mortar_2",
                Description = "How big is too big? Mortar Modules built in a full square (2x2, 3x3, ...) fuse for good into one " +
                              "Mega Mortar: it hits as hard as all its modules together, with their levels averaged, a blast " +
                              "as many times bigger as the square is wide and +2 range per extra row. It stands on the highest " +
                              "of its tiles and keeps growing as you build bigger squares around it.",
                OnAcquire = (run, restoring) => MortarFusionGrid.Ensure(),
            });
        }

        protected override void OnAscend()
        {
            SwapVanillaMesh("BaseArt", "ascensions/mortar/mortar_module");
            foreach (MeshFilter f in GetComponentsInChildren<MeshFilter>(true))
                for (Transform t = f.transform; t != null && t != transform; t = t.parent)
                    if (t.name == "BaseArt") { art = f; break; }
            artRenderer = art != null ? art.GetComponent<MeshRenderer>() : null;
            // The barrel is its own model on the same canvas as the base, so it can recoil on its own.
            if (art != null) barrel = ShowModel("ascensions/mortar/mortar_barrel", art.transform);
            box = GetComponent<BoxCollider>();
            if (box != null) { boxSize = box.size; boxCenter = box.center; }
            if (Tower.turret != null) turretPos = Tower.turret.transform.localPosition;
            (originX, originZ) = (Mathf.RoundToInt(transform.position.x), Mathf.RoundToInt(transform.position.z));
            LowY = transform.position.y;
            All.Add(this);
            MortarFusionGrid.Ensure();
            MortarFusionGrid.MarkDirty();
        }

        protected override void OnDestroy()
        {
            All.Remove(this);
            if (plinth != null) Destroy(plinth);
            if (box != null) { box.size = boxSize; box.center = boxCenter; }
            if (Tower != null && Tower.turret != null) Tower.turret.transform.localPosition = turretPos;
            MortarFusionGrid.MarkDirty();
            base.OnDestroy(); // gives the vanilla mesh and material back
        }

        internal MortarProgress Progress()
        {
            Tower t = Tower;
            return new MortarProgress
            {
                Weight = Size * Size, Level = t.level, DamageUp = t.damageUpgrade, HealthUp = t.healthDamageUpgrade,
                ArmorUp = t.armorDamageUpgrade, ShieldUp = t.shieldDamageUpgrade,
                HealthXp = t.healthXP, ArmorXp = t.armorXP, ShieldXp = t.shieldXP,
            };
        }

        /// <summary>Turns this tower into the Mega Mortar of a fused square: moves, grows, takes the averaged progress.</summary>
        internal void BecomeMega(MortarSquare square, float lowY, float topY, MortarProgress p)
        {
            Size = square.Size;
            (originX, originZ) = (square.X, square.Z);
            LowY = lowY;
            transform.position = new Vector3(square.X + (Size - 1) / 2f, topY, square.Z + (Size - 1) / 2f);
            if (box != null)
            {
                // One collider over the whole square: it blocks building there and opens this tower's popup.
                box.size = new Vector3(boxSize.x * Size - 0.02f, boxSize.y, boxSize.z * Size - 0.02f);
                box.center = boxCenter;
            }
            VoxAsset asset = VoxelLibrary.Get($"ascensions/mortar/mega_mortar_{Mathf.Min(Size, MegaMortarLogic.MaxSize)}");
            if (asset != null && art != null)
            {
                art.sharedMesh = asset.Mesh;
                if (artRenderer != null) artRenderer.sharedMaterial = asset.Material;
            }
            VoxAsset barrelAsset = VoxelLibrary.Get($"ascensions/mortar/mega_barrel_{Mathf.Min(Size, MegaMortarLogic.MaxSize)}");
            if (barrelAsset != null && barrel != null)
            {
                barrel.GetComponent<MeshFilter>().sharedMesh = barrelAsset.Mesh;
                barrel.GetComponent<MeshRenderer>().sharedMaterial = barrelAsset.Material;
            }
            // The Mega Mortar is 4 voxels (at the art's 0.0625 per voxel) taller than a module.
            if (Tower.turret != null) Tower.turret.transform.localPosition = turretPos + Vector3.up * 0.25f;
            BuildPlinth(topY);

            Tower t = Tower;
            t.level = p.Level;
            t.damageUpgrade = p.DamageUp;
            t.healthDamageUpgrade = p.HealthUp;
            t.armorDamageUpgrade = p.ArmorUp;
            t.shieldDamageUpgrade = p.ShieldUp;
            t.healthXP = p.HealthXp;
            t.armorXP = p.ArmorXp;
            t.shieldXP = p.ShieldXp;
            t.SetStats();
        }

        /// <summary>Concrete block from the lowest tile up to the highest (only when the tiles differ in height).</summary>
        private void BuildPlinth(float topY)
        {
            if (plinth != null) Destroy(plinth);
            float height = topY - LowY;
            if (height < 0.05f) return;
            VoxAsset p = VoxelLibrary.Get($"ascensions/mortar/mega_plinth_{Mathf.Min(Size, MegaMortarLogic.MaxSize)}");
            if (p == null) return;
            plinth = new GameObject("TR_MegaPlinth");
            plinth.transform.SetParent(transform, false);
            // The model is 16 voxels (1.6 units) tall at 0.1 per voxel; shown at the art scale (0.625) that is 1 unit.
            // No face may lie in the plane of a terrain face, or the two z-fight (depth precision is low at this camera
            // distance). So the block sticks out a little: Outset past the tiles' side faces (in front of the cliff
            // faces at the square's edge), Above the highest tile's grass and Below the lowest tile's.
            const float Outset = 0.02f, Above = 0.012f, Below = 0.02f;
            float s = art != null ? art.transform.lossyScale.x : 0.625f;
            float wide = s * (Size + 2f * Outset) / Size;
            plinth.transform.localScale = new Vector3(wide, s * (height + Above + Below), wide);
            plinth.transform.localPosition = new Vector3(0f, -height - Below, 0f);
            plinth.AddComponent<MeshFilter>().sharedMesh = p.Mesh;
            plinth.AddComponent<MeshRenderer>().sharedMaterial = p.Material;
        }

        internal void OnShell(MorterShell shell)
        {
            shellFired = true;
            ModernShell.Attach(shell);
            if (!IsMega) return;
            if (shell.artObject != null) shell.artObject.transform.localScale *= 0.6f + 0.4f * Size;
            shell.gameObject.AddComponent<MegaShell>().Size = Size;
        }

        internal void BeforeFire() => shellFired = false;

        internal void AfterFire()
        {
            if (!shellFired) return; // e.g. Mana Bombs without the mana
            Shots++;
            recoil = 0f;
            if (!IsMega) return;
            Vector3 at = Tower.muzzle != null ? Tower.muzzle.position : transform.position + Vector3.up;
            SoundLibrary.Play("ascensions/mega_launch", at, 0.75f, 0.05f, 3);
            MeshRenderer r = barrel != null ? barrel.GetComponent<MeshRenderer>() : null;
            if (r != null)
            {
                Bounds bb = r.bounds;
                SteamFx.Blast(new Vector3(bb.center.x, bb.max.y - 0.04f, bb.center.z), bb.extents.x * 0.92f, Size, Size >= 3 ? VentPositions() : null);
            }
        }

        /// <summary>
        /// World positions of the vent grilles on the deck (top of the grille, middle of its tile). Model units are 0.1 per
        /// voxel: the canvas is 16 n wide and centred, and the grilles' top is at voxel height 7 (deck + 1, top face).
        /// </summary>
        private List<Vector3> VentPositions()
        {
            var list = new List<Vector3>();
            if (art == null) return list;
            float half = 8f * Size;
            foreach (var (tx, ty) in MegaMortarLogic.VentTiles(Size))
                list.Add(art.transform.TransformPoint(new Vector3((tx * 16 + 8 - half) * 0.1f, 0.7f, (ty * 16 + 8 - half) * 0.1f)));
            return list;
        }

        /// <summary>Barrel recoil: pushed in fast (0.06 s), eases back out (0.45 s).</summary>
        private void Update()
        {
            if (recoil < 0f || barrel == null) return;
            recoil += Time.deltaTime;
            const float kick = 0.06f, back = 0.45f;
            float k = recoil < kick ? recoil / kick : 1f - Mathf.SmoothStep(0f, 1f, (recoil - kick) / back);
            if (recoil >= kick + back) { k = 0f; recoil = -1f; }
            RecoilAmount = k;
            // In model units (0.1 per voxel): 2.5 voxels for a module, 3.5 for a Mega Mortar.
            barrel.transform.localPosition = Vector3.down * (IsMega ? 0.35f : 0.25f) * k;
        }

        /// <summary>Gold back for demolishing this Mega Mortar (all its modules, sold one by one).</summary>
        internal int SellValue()
        {
            TowerFlyweight fly = TowerManager.instance.morter;
            return MegaMortarLogic.SellValue(fly.basePrice, fly.priceIncrease, fly.towers.Count + ExtraModules, Size * Size);
        }
    }

    /// <summary>
    /// The modern shell of Mortar Modules: hides the vanilla shell art and shows <c>mortar_shell</c> with a red tail light
    /// that blinks by switching its renderer on and off (no light source, so it costs nothing).
    /// </summary>
    internal sealed class ModernShell : MonoBehaviour
    {
        private const float Length = 0.36f, BlinksPerSecond = 3f;
        private GameObject tailLight;
        private float time;

        public static void Attach(MorterShell shell)
        {
            if (shell.artObject == null || shell.GetComponent<ModernShell>() != null) return;
            Transform art = shell.artObject.transform;
            foreach (MeshRenderer r in art.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = false;
            // 12 voxels at 0.1 = 1.2 units long; sized like the vanilla shell whatever the art's own scale.
            float scale = Length / 1.2f / Mathf.Max(0.0001f, art.lossyScale.x);
            GameObject body = Phase1Kit.Model("ascensions/mortar/mortar_shell", scale, art);
            var c = shell.gameObject.AddComponent<ModernShell>();
            VoxAsset light = VoxelLibrary.Get("ascensions/mortar/mortar_shell_light");
            if (light == null || body.transform.childCount == 0) return;
            Transform bodyArt = body.transform.GetChild(0);
            // Same canvas as the shell, so the same offset puts the light right behind the tail cap.
            var go = new GameObject("TR_ShellLight");
            go.transform.SetParent(body.transform, false);
            go.transform.localPosition = bodyArt.localPosition;
            go.transform.localScale = bodyArt.localScale;
            go.AddComponent<MeshFilter>().sharedMesh = light.Mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = light.Material;
            c.tailLight = go;
        }

        private void Update()
        {
            if (tailLight == null) return;
            time += Time.deltaTime;
            bool on = (int)(time * BlinksPerSecond * 2f) % 2 == 0;
            if (tailLight.activeSelf != on) tailLight.SetActive(on);
        }
    }

    /// <summary>
    /// Steam venting from a Mega Mortar's barrel when it fires: soft white puffs (the steam) and thin fast streaks (the
    /// force), out to all sides from the barrel and, for 3x3 and bigger, straight up from each vent. Two pooled
    /// world-space particle systems,
    /// shared by every Mega Mortar.
    /// </summary>
    internal static class SteamFx
    {
        private static ParticleSystem puffs, streaks;
        private static Material material;
        private static readonly Color Steam = new Color(0.93f, 0.95f, 0.97f, 0.9f), Streak = new Color(1f, 1f, 1f, 0.95f);

        public static void Blast(Vector3 top, float radius, int size, List<Vector3> vents)
        {
            if (!Ensure()) return;
            float k = 0.6f + 0.25f * size;
            int dirs = 14 + 6 * size;
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < dirs; i++)
            {
                float angle = (i + Random.value * 0.6f) / dirs * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 at = top + dir * radius;
                for (int j = 0; j < 2; j++)
                {
                    ep.position = at + Random.insideUnitSphere * 0.04f * size;
                    ep.velocity = dir * Random.Range(1.4f, 3f) * k + Vector3.up * Random.Range(0f, 0.5f);
                    ep.startSize = Random.Range(0.1f, 0.2f) * k;
                    ep.startLifetime = Random.Range(0.6f, 1.1f);
                    ep.startColor = Steam;
                    puffs.Emit(ep, 1);
                }
                ep.position = at;
                ep.velocity = dir * Random.Range(5f, 8f) * k + Vector3.up * Random.Range(-0.2f, 0.3f);
                ep.startSize = 0.03f * k;
                ep.startLifetime = Random.Range(0.18f, 0.32f);
                ep.startColor = Streak;
                streaks.Emit(ep, 1);
            }
            if (vents == null) return;
            // One stream straight up out of each vent grille (about 0.4 x 0.4 at the art scale).
            foreach (Vector3 vent in vents)
                for (int i = 0; i < 10; i++)
                {
                    Vector2 spread = Random.insideUnitCircle * 0.15f;
                    Vector3 dir = new Vector3(spread.x, 1f, spread.y).normalized;
                    Vector2 off = Random.insideUnitCircle * 0.18f;
                    ep.position = vent + new Vector3(off.x, 0f, off.y);
                    ep.velocity = dir * Random.Range(2f, 3.5f) * k;
                    ep.startSize = Random.Range(0.1f, 0.18f) * k;
                    ep.startLifetime = Random.Range(0.6f, 1f);
                    ep.startColor = Steam;
                    puffs.Emit(ep, 1);
                    if (i % 2 != 0) continue;
                    ep.velocity = dir * Random.Range(6f, 9f) * k;
                    ep.startSize = 0.03f * k;
                    ep.startLifetime = Random.Range(0.18f, 0.3f);
                    ep.startColor = Streak;
                    streaks.Emit(ep, 1);
                }
        }

        private static bool Ensure()
        {
            if (material == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) return false;
                material = new Material(shader) { name = "TR_Steam", mainTexture = SoftCircle() };
            }
            if (puffs == null) puffs = Make("TR_SteamPuffs", false);
            if (streaks == null) streaks = Make("TR_SteamStreaks", true);
            return puffs != null && streaks != null;
        }

        private static ParticleSystem Make(string name, bool stretched)
        {
            var go = new GameObject(name);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.maxParticles = 3000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = stretched ? 0f : -0.03f; // steam drifts up a little
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.enabled = false;

            // Slowed down by the air: puffs quickly, streaks less.
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = stretched ? 1f : 0.3f;
            limit.dampen = stretched ? 0.06f : 0.1f;

            var color = ps.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(stretched ? 0.8f : 0.7f, 0.4f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = stretched
                ? new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.4f))
                : new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.3f, 1.2f), new Keyframe(1f, 1.8f)));

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (stretched)
            {
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.velocityScale = 0.07f;
                r.lengthScale = 1.5f;
            }
            else r.renderMode = ParticleSystemRenderMode.Billboard;
            ps.Play();
            return ps;
        }

        private static Texture2D SoftCircle()
        {
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "TR_SoftCircle", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = new Vector2(x - (n - 1) / 2f, y - (n - 1) / 2f).magnitude / (n / 2f);
                    float a = Mathf.Clamp01((1f - d) * 2.2f);
                    pixels[y * n + x] = new Color(1f, 1f, 1f, a * a);
                }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }

    /// <summary>Tag on a Mega Mortar's shell: its impact gets a big explosion and the mega blast sound.</summary>
    internal sealed class MegaShell : MonoBehaviour
    {
        public int Size;
    }

    /// <summary>Fuses Mortar Modules when one is built (or the ascension is gained): squares, largest first, for good.</summary>
    internal sealed class MortarFusionGrid : MonoBehaviour
    {
        public static MortarFusionGrid Instance { get; private set; }
        private static bool dirty = true;

        public static void Ensure()
        {
            if (!TreasureRunMode.Active || Instance != null) return;
            Instance = new GameObject("TR_MortarFusionGrid").AddComponent<MortarFusionGrid>();
            dirty = true;
        }

        public static void MarkDirty() => dirty = true;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            AscensionDef owned = AscensionRegistry.Owned(AscensionTarget.Mortar);
            if (owned == null || owned.Id != MortarModule.Id) { Destroy(gameObject); return; }
            if (!dirty) return;
            dirty = false;
            Regroup();
        }

        private static void Regroup()
        {
            var units = MortarModule.All.Where(m => m != null && !m.Dissolved && m.Tower != null).ToList();
            var contained = new List<int>();
            bool fused = false;
            for (int guard = 0; guard < 64; guard++)
            {
                MortarSquare? s = MegaMortarLogic.NextFusion(units.Select(u => u.Square).ToList(), contained);
                if (!s.HasValue) break;
                List<MortarModule> parts = contained.Select(i => units[i]).ToList();
                MortarModule survivor = parts.OrderByDescending(p => p.Size).ThenBy(p => p.Square.Z).ThenBy(p => p.Square.X).First();
                MortarProgress progress = MegaMortarLogic.Average(parts.Select(p => p.Progress()).ToList());
                float lowY = parts.Min(p => p.LowY), topY = parts.Max(p => p.transform.position.y);
                TowerFlyweight fly = TowerManager.instance.morter;
                foreach (MortarModule p in parts)
                {
                    if (p == survivor) continue;
                    // Absorbed: no refund, no longer a tower; its module now counts through the survivor's size.
                    p.Dissolved = true;
                    fly.towers.Remove(p.Tower);
                    Destroy(p.gameObject);
                }
                survivor.BecomeMega(s.Value, lowY, topY, progress);
                MegaMortarPatches.Reprice(fly);
                units.RemoveAll(u => u.Dissolved);
                fused = true;
                Vector3 c = survivor.transform.position + Vector3.up * 0.4f;
                Fx.VoxelBurst(c, Sparks, 14 * survivor.Size, 0.05f, 2f + survivor.Size, 0.6f, 2f);
                if (ModConfig.DebugTools.Value)
                    Plugin.Log.LogInfo($"[mega mortar] fused {s.Value} from {parts.Count} parts: level {survivor.Tower.level}, damage {survivor.Tower.damage}, " +
                                       $"x{survivor.Tower.healthDamage}, range {survivor.Tower.range:0.#}, blast {survivor.Tower.blastRadius:0.##}.");
            }
            if (fused && TowerManager.instance != null) MegaMortarPatches.Reprice(TowerManager.instance.morter);
        }

        private static readonly Color[] Sparks = { new Color(1f, 0.82f, 0.2f), new Color(0.4f, 0.9f, 1f), new Color(0.85f, 0.87f, 0.9f) };
    }

    [HarmonyPatch]
    internal static class MegaMortarPatches
    {
        private static MortarModule ModuleOf(Tower t)
        {
            if (!TreasureRunMode.Active || t == null || t.towerType != TowerType.Morter) return null;
            MortarModule m = t.GetComponent<MortarModule>();
            return m != null && !m.Dissolved ? m : null;
        }

        private static MortarModule MegaOf(Tower t)
        {
            if (!TreasureRunMode.Active || t == null || t.towerType != TowerType.Morter) return null;
            MortarModule m = t.GetComponent<MortarModule>();
            return m != null && m.IsMega && !m.Dissolved ? m : null;
        }

        private static bool IsMortarFlyweight(TowerFlyweight f) => TowerManager.instance != null && f == TowerManager.instance.morter;

        /// <summary>Mortar build/sell prices with the modules inside Mega Mortars counted.</summary>
        internal static void Reprice(TowerFlyweight f)
        {
            if (f == null) return;
            int owned = f.towers.Count + MortarModule.ExtraModules;
            f.currentPrice = f.basePrice + owned * f.priceIncrease;
            f.sellPrice = f.basePrice + (owned - 1) * f.priceIncrease;
            if (f.myBuildButton != null) f.myBuildButton.UpdatePriceText();
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Tower), nameof(Tower.SetStats))]
        private static void SetStatsPostfix(Tower __instance)
        {
            MortarModule m = MegaOf(__instance);
            if (m == null) return;
            __instance.damage *= MegaMortarLogic.DamageFactor(m.Size);
            __instance.range += MegaMortarLogic.RangeBonus(m.Size);
            __instance.blastRadius *= MegaMortarLogic.BlastFactor(m.Size);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(TowerFlyweight), nameof(TowerFlyweight.AddNewTower))]
        private static void AddNewTowerPostfix(TowerFlyweight __instance)
        {
            if (TreasureRunMode.Active && IsMortarFlyweight(__instance) && MortarModule.ExtraModules > 0) Reprice(__instance);
        }

        /// <summary>Selling a loose module while Mega Mortars exist: vanilla refund, counting their modules.</summary>
        [HarmonyPrefix, HarmonyPatch(typeof(TowerFlyweight), nameof(TowerFlyweight.RemoveTower))]
        private static bool RemoveTowerPrefix(TowerFlyweight __instance, Tower newTower)
        {
            if (!TreasureRunMode.Active || !IsMortarFlyweight(__instance) || MortarModule.ExtraModules == 0) return true;
            __instance.towers.Remove(newTower);
            Reprice(__instance);
            int refund = __instance.basePrice + (__instance.towers.Count + MortarModule.ExtraModules) * __instance.priceIncrease;
            ResourceManager.instance.AddMoney(refund);
            DamageTracker.instance.AddCost(__instance.towerTypeForDamageTracker, -refund);
            return false;
        }

        /// <summary>Demolishing a Mega Mortar sells all its modules (one by one, as the price falls with each).</summary>
        [HarmonyPrefix, HarmonyPatch(typeof(Tower), nameof(Tower.Demolish))]
        private static bool DemolishPrefix(Tower __instance)
        {
            MortarModule m = MegaOf(__instance);
            if (m == null) return true;
            TowerFlyweight fly = TowerManager.instance.morter;
            int refund = m.SellValue();
            m.Dissolved = true;
            fly.towers.Remove(__instance);
            Reprice(fly);
            ResourceManager.instance.AddMoney(refund);
            DamageTracker.instance.AddCost(fly.towerTypeForDamageTracker, -refund);
            Object.Destroy(__instance.gameObject);
            return false;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(TowerUI), nameof(TowerUI.UpdateText))]
        private static void TowerUiTextPostfix(TowerUI __instance)
        {
            MortarModule m = MegaOf(__instance.myTower);
            if (m != null && __instance.demolishText != null) __instance.demolishText.text = $"Demolish ({m.SellValue()}g)";
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Morter), nameof(Morter.Fire))]
        private static void FirePrefix(Morter __instance)
        {
            MortarModule.Firing = ModuleOf(__instance);
            MortarModule.Firing?.BeforeFire();
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Morter), nameof(Morter.Fire))]
        private static void FirePostfix()
        {
            MortarModule m = MortarModule.Firing;
            MortarModule.Firing = null;
            m?.AfterFire();
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Projectile), nameof(Projectile.SetStats))]
        private static void ShellSetStatsPostfix(Projectile __instance)
        {
            if (MortarModule.Firing != null && __instance is MorterShell shell) MortarModule.Firing.OnShell(shell);
        }

        [HarmonyPrefix, HarmonyPatch(typeof(MorterShell), nameof(MorterShell.OnHit))]
        private static void ShellHitPrefix(MorterShell __instance)
        {
            MegaShell mega = __instance.GetComponent<MegaShell>();
            if (mega == null) return;
            Vector3 p = __instance.transform.position;
            if (__instance.explosion != null)
            {
                GameObject ex = Object.Instantiate(__instance.explosion, p, Quaternion.identity);
                ex.transform.localScale = Vector3.one * mega.Size;
                foreach (ParticleSystem ps in ex.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ParticleSystem.MainModule main = ps.main;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                }
            }
            SoundLibrary.Play("ascensions/mega_blast", p, 0.8f, 0.05f, 3);
            Fx.VoxelBurst(p, Phase2Kit.FireColors, 10 * mega.Size, 0.07f * mega.Size, 2f + mega.Size, 0.6f, 3f);
            Fx.VoxelBurst(p, Phase2Kit.SmokeColors, 8 * mega.Size, 0.09f * mega.Size, 1f + 0.5f * mega.Size, 1.2f, -0.2f);
        }
    }
}

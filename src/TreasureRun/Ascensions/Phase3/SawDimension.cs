using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TreasureRun.Audio;
using TreasureRun.Core;
using TreasureRun.Treasures;
using TreasureRun.Voxels;
using UnityEngine;

namespace TreasureRun.Ascensions.Phase3
{
    /// <summary>
    /// The Saw Dimension (Shredder, Legendary): the floor is saw. Every Shredder becomes a Saw Engine that stops
    /// attacking and powers saws along the whole path (<see cref="SawField"/>). The saws cut every monster on the map 5
    /// times a second for Σ(Shredder damage) / 10 per second, with the Shredders' averaged multipliers and bleed. Without
    /// Shredders they sink into the floor and do nothing (<see cref="SawDimensionLogic"/>).
    /// </summary>
    internal sealed class SawEngine : AscendedTower
    {
        public const string Id = "saw_dimension";
        internal static readonly List<SawEngine> All = new List<SawEngine>();
        private Transform turret;

        public static void Register()
        {
            AscensionRegistry.Register(new AscensionDef
            {
                Id = Id, Name = "The Saw Dimension", TowerName = "Saw Engine", Rarity = Rarity.Legendary, Kind = AscensionKind.Replace,
                Target = AscensionTarget.Shredder, Component = typeof(SawEngine), CardModel = "ascensions/shredder/saw_engine_turret",
                Description = $"The floor is saw. Saws cover the whole path and cut every monster on the map {SawDimensionLogic.TicksPerSecond} " +
                              $"times a second, for a tenth of your Shredders' combined damage per second (with their bleed). " +
                              "Shredders stop attacking but keep levelling up. Without Shredders, the saws sink into the floor.",
                OnAcquire = (run, restoring) => SawField.Ensure(),
            });
        }

        protected override void OnAscend()
        {
            SwapVanillaMesh("BaseArt", "ascensions/shredder/saw_engine_base");
            SwapVanillaMesh("TurretArt", "ascensions/shredder/saw_engine_turret");
            turret = Tower.turret != null ? Tower.turret.transform : null;
            All.Add(this);
            SawField.Ensure();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            All.Remove(this);
        }

        /// <summary>Replaces Tower.Update: no aiming, no attacks, no vanilla XP (the field hands it out).</summary>
        internal void Tick()
        {
            float ext = SawField.Instance != null ? SawField.Instance.Extension : 0f;
            if (turret != null) turret.Rotate(0f, 240f * ext * Time.deltaTime, 0f, Space.Self);
        }
    }

    /// <summary>
    /// The saws of The Saw Dimension: one global object that lays saws along every path segment (rebuilt when the map
    /// grows), draws them instanced, ticks damage and XP, and raises or retracts them with the number of Shredders.
    /// </summary>
    internal sealed class SawField : MonoBehaviour
    {
        private const string SawModel = "ascensions/shredder/floor_saw", SlotModel = "ascensions/shredder/floor_saw_slot";
        private const float SawScale = 0.5f, SpinRevsPerSecond = 2.5f, Sink = 0.34f, LayoutCheck = 2f;
        private const int Batch = 1023;

        public static SawField Instance { get; private set; }

        public float Extension { get; private set; }
        public int SawCount => spots.Count;
        public long Ticks { get; private set; }
        public long Hits { get; private set; }

        private List<SawSpot> spots = new List<SawSpot>();
        private float tickTimer, carry, spin, layoutTimer;
        private int waypointCount = -1;
        private Mesh sawMesh, slotMesh;
        private Material sawMat, slotMat;
        private Vector3 sawCenter, slotCenter;
        private Matrix4x4[] sawMatrices = new Matrix4x4[0], slotMatrices = new Matrix4x4[0];
        private Matrix4x4[] batch = new Matrix4x4[Batch];
        private bool instancing = true;
        private SoundLoop hum;
        private readonly List<Tower> shredders = new List<Tower>();
        private readonly List<Enemy> victims = new List<Enemy>();
        private readonly List<int> scratch = new List<int>();

        public static void Ensure()
        {
            if (!TreasureRunMode.Active || Instance != null) return;
            var go = new GameObject("TR_SawField");
            Instance = go.AddComponent<SawField>();
        }

        private void Start()
        {
            VoxAsset saw = VoxelLibrary.Get(SawModel), slot = VoxelLibrary.Get(SlotModel);
            if (saw != null) { sawMesh = saw.Mesh; sawMat = new Material(saw.Material) { enableInstancing = true }; sawCenter = saw.Mesh.bounds.center; }
            if (slot != null) { slotMesh = slot.Mesh; slotMat = new Material(slot.Material) { enableInstancing = true }; slotCenter = slot.Mesh.bounds.center; }
            // Plain DrawMesh per saw: the game build strips the Standard shader's instancing variants, so instanced
            // draws render nothing. A few hundred saws are cheap enough.
            instancing = false;
            if (ModConfig.DebugTools.Value) Plugin.Log.LogInfo($"[saws] shader {(sawMat != null ? sawMat.shader.name : "none")}");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (hum != null) Destroy(hum.gameObject);
        }

        private void Update()
        {
            AscensionDef owned = AscensionRegistry.Owned(AscensionTarget.Shredder);
            if (owned == null || owned.Id != SawEngine.Id) { Destroy(gameObject); return; }

            shredders.Clear();
            foreach (SawEngine e in SawEngine.All) if (e != null && e.Tower != null) shredders.Add(e.Tower);

            layoutTimer -= Time.deltaTime;
            if (layoutTimer <= 0f) { layoutTimer = LayoutCheck; RefreshLayout(); }

            float dt = Time.deltaTime;
            Extension = SawDimensionLogic.Extension(Extension, shredders.Count > 0, dt);
            spin = (spin + SpinRevsPerSecond * 360f * Extension * dt) % 360f;

            tickTimer += dt;
            float step = 1f / SawDimensionLogic.TicksPerSecond;
            int guard = 0;
            while (tickTimer >= step && guard++ < 4) { tickTimer -= step; Tick(step); }
            if (guard >= 4) tickTimer = 0f;

            Draw();
            UpdateHum();
        }

        // ------------------------------------------------------------------ layout

        private void RefreshLayout()
        {
            Waypoint[] waypoints = FindObjectsOfType<Waypoint>();
            if (waypoints.Length == waypointCount) return;
            waypointCount = waypoints.Length;
            var segments = new List<(Vector3, Vector3)>();
            foreach (Waypoint w in waypoints)
            {
                if (w.next != null) segments.Add((w.transform.position, w.next.transform.position));
                if (w.splitNext != null) segments.Add((w.transform.position, w.splitNext.transform.position));
            }
            spots = SawDimensionLogic.Layout(segments);
            sawMatrices = new Matrix4x4[spots.Count];
            slotMatrices = new Matrix4x4[spots.Count];
            for (int i = 0; i < spots.Count; i++)
                slotMatrices[i] = Matrix4x4.TRS(spots[i].Position + Vector3.up * 0.012f, Quaternion.LookRotation(spots[i].Direction, Vector3.up),
                                                Vector3.one * SawScale) * Matrix4x4.Translate(new Vector3(-slotCenter.x, 0f, -slotCenter.z));
            if (ModConfig.DebugTools.Value)
                Plugin.Log.LogInfo($"[saws] {spots.Count} saws on {segments.Count} path segments ({waypoints.Length} waypoints)" +
                                   (spots.Count > 0 ? $", first at {spots[0].Position} dir {spots[0].Direction}" : "") + ".");
        }

        // ------------------------------------------------------------------ damage and XP

        private void Tick(float dt)
        {
            if (shredders.Count == 0 || SpawnManager.instance == null) return;
            Ticks++;
            victims.Clear();
            int shieldN = 0, armorN = 0, healthN = 0;
            foreach (Enemy e in SpawnManager.instance.currentEnemies)
            {
                if (!Combat.IsAlive(e)) continue;
                victims.Add(e);
                if (e.shield > 0) shieldN++; else if (e.armor > 0) armorN++; else healthN++;
            }
            if (victims.Count == 0) return;

            int sum = 0;
            foreach (Tower t in shredders) sum += t.damage;
            int baseDamage = SawDimensionLogic.Take(ref carry, SawDimensionLogic.BaseDamagePerTick(sum) * Extension);
            if (baseDamage > 0)
            {
                int h = Avg(t => t.healthDamage), a = Avg(t => t.armorDamage), s = Avg(t => t.shieldDamage);
                float slow = shredders.Average(t => t.slowPercent), bleed = shredders.Average(t => t.bleedPercent);
                float burn = shredders.Average(t => t.burnPercent), poison = shredders.Average(t => t.poisonPercent);
                float crit = shredders.Average(t => t.critChance), stun = shredders.Average(t => t.stunChance);
                foreach (Enemy e in victims)
                {
                    if (!Combat.IsAlive(e)) continue;
                    e.TakeDamage(TowerType.Shredder, baseDamage, h, a, s, slow, bleed, burn, poison, crit, stun);
                    Hits++;
                }
                Enemy spark = victims[UnityEngine.Random.Range(0, victims.Count)];
                if (spark != null) Fx.VoxelBurst(spark.transform.position + Vector3.up * 0.1f, Sparks, 4, 0.04f, 1.4f, 0.25f, 3f);
            }

            // XP: every Shredder gains as if it always had a target, split by the layers the monsters are on.
            Vector3 shares = SawDimensionLogic.LayerShares(shieldN, armorN, healthN);
            foreach (Tower t in shredders)
            {
                float xp = SawDimensionLogic.XpPerSecond(t.range) * dt;
                t.shieldXP += xp * shares.x;
                t.armorXP += xp * shares.y;
                t.healthXP += xp * shares.z;
                t.CheckXPAndLevelUp();
            }
        }

        private static readonly Color[] Sparks = { new Color(1f, 0.9f, 0.5f), new Color(1f, 0.6f, 0.2f), new Color(0.85f, 0.85f, 0.9f) };

        private int Avg(Func<Tower, int> value)
        {
            scratch.Clear();
            foreach (Tower t in shredders) scratch.Add(value(t));
            return SawDimensionLogic.Average(scratch);
        }

        // ------------------------------------------------------------------ drawing and sound

        private void Draw()
        {
            if (spots.Count == 0) return;
            if (slotMesh != null) DrawAll(slotMesh, slotMat, slotMatrices);
            if (sawMesh == null || Extension <= 0f) return;
            float drop = (1f - Extension) * Sink;
            for (int i = 0; i < spots.Count; i++)
            {
                Quaternion r = Quaternion.LookRotation(spots[i].Direction, Vector3.up) * Quaternion.Euler(spin + i * 37f, 0f, 0f);
                // The saw's centre sits on the floor (half of it shows); retracting lowers it below the surface.
                sawMatrices[i] = Matrix4x4.TRS(spots[i].Position + Vector3.down * drop, r, Vector3.one * SawScale) * Matrix4x4.Translate(-sawCenter);
            }
            DrawAll(sawMesh, sawMat, sawMatrices);
        }

        private void DrawAll(Mesh mesh, Material mat, Matrix4x4[] matrices)
        {
            if (instancing)
            {
                try
                {
                    for (int start = 0; start < matrices.Length; start += Batch)
                    {
                        int n = Mathf.Min(Batch, matrices.Length - start);
                        Array.Copy(matrices, start, batch, 0, n);
                        Graphics.DrawMeshInstanced(mesh, 0, mat, batch, n);
                    }
                    return;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"[saws] Instanced drawing failed ({e.Message}); drawing one by one.");
                    instancing = false;
                }
            }
            foreach (Matrix4x4 m in matrices) Graphics.DrawMesh(mesh, m, mat, 0);
        }

        private void UpdateHum()
        {
            float volume = Extension * Mathf.Min(1f, 0.3f + 0.07f * shredders.Count) * 0.45f;
            if (volume > 0.01f && hum == null && Camera.main != null) hum = SoundLoop.Start("ascensions/saw_hum", Camera.main.transform, volume);
            if (hum != null)
            {
                hum.Volume = volume;
                if (volume <= 0.01f) { Destroy(hum.gameObject); hum = null; }
            }
        }
    }

    [HarmonyPatch]
    internal static class SawDimensionPatches
    {
        [HarmonyPrefix, HarmonyPatch(typeof(Tower), nameof(Tower.Update))]
        private static bool TowerUpdatePrefix(Tower __instance)
        {
            if (!TreasureRunMode.Active || __instance.towerType != TowerType.Shredder) return true;
            SawEngine engine = __instance.GetComponent<SawEngine>();
            if (engine == null) return true;
            engine.Tick();
            return false;
        }
    }
}

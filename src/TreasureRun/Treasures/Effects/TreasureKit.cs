using System;
using System.Linq;
using TreasureRun.Core;
using UnityEngine;

namespace TreasureRun.Treasures.Effects
{
    /// <summary>Shared helpers for treasure modules: definition shortcut, gold, tower queries, simple VFX.</summary>
    internal static class Kit
    {
        /// <summary>Registers a treasure. Most effects are passive checks in hooks (<see cref="Has"/>); onAcquire is for one-time effects.</summary>
        public static void Add(string id, string name, Rarity rarity, string description,
                               Action<RunState, bool> onAcquire = null, string[] tags = null,
                               string exclusionGroup = null, Func<RunState, bool> available = null)
        {
            TreasureRegistry.Register(new TreasureDef
            {
                Id = id,
                Name = name,
                Rarity = rarity,
                Description = description,
                Tags = tags ?? Array.Empty<string>(),
                ExclusionGroup = exclusionGroup,
                IsAvailable = available,
                CreateEffect = () => new LambdaEffect(onAcquire ?? ((r, restoring) => { })),
            });
        }

        /// <summary>Selling (Wandering Merchant): what undoes one copy's lasting vanilla changes.</summary>
        public static void OnSell(string id, Action undo)
        {
            TreasureDef d = TreasureRegistry.Get(id);
            if (d != null) d.OnSell = undo; else Plugin.Log.LogWarning($"OnSell: unknown treasure '{id}'.");
        }

        /// <summary>Selling (Wandering Merchant): this treasure can't be sold, and why.</summary>
        public static void NoSale(string id, string reason)
        {
            TreasureDef d = TreasureRegistry.Get(id);
            if (d != null) d.NoSale = reason; else Plugin.Log.LogWarning($"NoSale: unknown treasure '{id}'.");
        }

        public static bool Has(string id) => TreasureInventory.Owns(id);
        public static int Count(string id) => TreasureInventory.CountOf(id);

        /// <summary>True if a tower of this type was built during the run (used to gate tower-specific treasures).</summary>
        public static bool TowerUsed(TowerType type) =>
            type == TowerType.Crossbow || (TowerManager.instance != null && TowerManager.instance.towersUsed.Contains(type));

        public static Func<RunState, bool> Requires(TowerType type) => _ => TowerUsed(type);

        public static Vector3 CastleTop => GameHelpers.CastlePosition + Vector3.up * 2f;

        public static void Gold(int amount, Vector3 popupAt, string label = null)
        {
            if (amount == 0 || ResourceManager.instance == null) return;
            ResourceManager.instance.AddMoney(amount);
            GameHelpers.FloatingText(popupAt + Vector3.up, $"{(amount > 0 ? "+" : "")}{amount}g{(label != null ? " " + label : "")}", "Yellow");
        }

        public static int Level => GameHelpers.Level;

        /// <summary>Mana-consuming tower types (vanilla descriptions: "Uses mana ...").</summary>
        public static bool UsesMana(TowerType t) =>
            t == TowerType.TeslaCoil || t == TowerType.Frost || t == TowerType.Obelisk || t == TowerType.FlameThrower ||
            t == TowerType.PoisonSprayer || t == TowerType.ParticleCannon || t == TowerType.VampireLair || t == TowerType.Monument;

        // ----- VFX -----

        private static GameObject explosionPrefab;
        private static bool lookedUpExplosion;

        /// <summary>Short-lived line between two points (chain lightning, ricochets). Silently skipped if no material is found.</summary>
        public static void Zap(Vector3 from, Vector3 to, Color color, float duration = 0.15f)
        {
            Material lineMaterial = Fx.LineMaterial;
            if (lineMaterial == null) return;
            var go = new GameObject("TR_Zap");
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = lineMaterial;
            line.positionCount = 4;
            line.useWorldSpace = true;
            line.startWidth = line.endWidth = 0.06f;
            line.startColor = line.endColor = color;
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = Vector3.Lerp(from, to, i / 3f) + Vector3.up * 0.5f;
                if (i > 0 && i < 3) p += UnityEngine.Random.insideUnitSphere * 0.2f;
                line.SetPosition(i, p);
            }
            UnityEngine.Object.Destroy(go, duration);
        }

        /// <summary>Spawns the vanilla mortar explosion effect (with sound) at a position.</summary>
        public static void Boom(Vector3 at, float scale = 1f)
        {
            if (!lookedUpExplosion)
            {
                lookedUpExplosion = true;
                Explosion e = Resources.FindObjectsOfTypeAll<Explosion>().FirstOrDefault(x => !x.gameObject.scene.IsValid());
                explosionPrefab = e != null ? e.gameObject : null;
            }
            if (explosionPrefab == null) return;
            var go = UnityEngine.Object.Instantiate(explosionPrefab, at, Quaternion.identity);
            go.transform.localScale *= scale;
        }
    }
}

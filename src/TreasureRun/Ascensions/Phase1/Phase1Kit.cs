using TreasureRun.Castles;
using TreasureRun.Core;
using TreasureRun.Voxels;
using UnityEngine;

namespace TreasureRun.Ascensions.Phase1
{
    /// <summary>Helpers shared by the phase 1 ascension components.</summary>
    internal static class Phase1Kit
    {
        /// <summary>A hit with the tower's own stats (layer multipliers, damage over time, crit, stun).</summary>
        public static void Hit(Tower t, Enemy e, int baseDamage, TowerType? source = null)
        {
            if (!Combat.IsAlive(e)) return;
            e.TakeDamage(source ?? t.towerType, baseDamage, t.healthDamage, t.armorDamage, t.shieldDamage,
                         t.slowPercent, t.bleedPercent, t.burnPercent, t.poisonPercent, t.critChance, t.stunChance);
        }

        /// <summary>Pays a mana tower's cost per attack like vanilla (damage × rate); false if there is not enough.</summary>
        public static bool PayMana(Tower t, float rate)
        {
            if (!t.consumesMana) return true;
            int cost = (int)(t.damage * rate);
            if (!ResourceManager.instance.CheckMana(cost)) return false;
            ResourceManager.instance.SpendMana(cost);
            return true;
        }

        public static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        /// <summary>A jagged glowing bolt between two points that fades out (lightning, arcs).</summary>
        public static void Bolt(Vector3 from, Vector3 to, Color color, float width = 0.07f, float jitter = 0.15f, int points = 7)
        {
            Material mat = Fx.GlowMaterial;
            if (mat == null) return;
            var go = new GameObject("TR_Bolt");
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = mat;
            line.useWorldSpace = true;
            line.positionCount = points;
            line.startWidth = line.endWidth = width;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            for (int i = 0; i < points; i++)
            {
                float t = i / (points - 1f);
                Vector3 p = Vector3.Lerp(from, to, t);
                if (i > 0 && i < points - 1) p += Random.insideUnitSphere * jitter;
                line.SetPosition(i, p);
            }
            line.startColor = color;
            line.endColor = new Color(color.r, color.g, color.b, color.a * 0.8f);
            go.AddComponent<FadeLine>();
        }

        /// <summary>A voxel model as a new object (centred on its mesh), e.g. projectiles.</summary>
        public static GameObject Model(string name, float scale, Transform parent = null)
        {
            var go = new GameObject("TR_" + name);
            if (parent != null) go.transform.SetParent(parent, false);
            VoxAsset asset = VoxelLibrary.Get(name);
            if (asset == null) return go;
            var art = new GameObject("Art");
            art.transform.SetParent(go.transform, false);
            art.transform.localScale = Vector3.one * scale;
            art.transform.localPosition = -asset.Mesh.bounds.center * scale;
            art.AddComponent<MeshFilter>().sharedMesh = asset.Mesh;
            art.AddComponent<MeshRenderer>().sharedMaterial = asset.Material;
            return go;
        }
    }
}

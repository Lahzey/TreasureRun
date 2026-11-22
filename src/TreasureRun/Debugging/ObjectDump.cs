using System;
using System.Linq;
using UnityEngine;

namespace TreasureRun.Debugging
{
    /// <summary>Dev helper: writes a hierarchy with components, meshes, materials, colliders and waypoint links.</summary>
    internal static class ObjectDump
    {
        public static void Write(Transform t, int depth, Action<string> print, int indent = 0)
        {
            string pad = new string(' ', indent * 2);
            GameObject go = t.gameObject;
            print($"{pad}{go.name}{(go.activeSelf ? "" : " (inactive)")}  local {t.localPosition} rot {t.localEulerAngles} scale {t.localScale}  world {t.position}  layer {LayerMask.LayerToName(go.layer)}");
            foreach (Component c in go.GetComponents<Component>())
            {
                if (c == null || c is Transform) continue;
                print($"{pad}  - {Describe(c)}");
            }
            if (depth <= 0)
            {
                if (t.childCount > 0) print($"{pad}  ({t.childCount} children)");
                return;
            }
            foreach (Transform child in t) Write(child, depth - 1, print, indent + 1);
        }

        private static string Describe(Component c)
        {
            switch (c)
            {
                case MeshFilter mf:
                    Mesh m = mf.sharedMesh;
                    return m == null ? "MeshFilter (none)" : $"MeshFilter {m.name} verts {m.vertexCount} bounds {m.bounds.center}±{m.bounds.extents} readable {m.isReadable}";
                case MeshRenderer mr:
                    return "MeshRenderer " + string.Join(", ", mr.sharedMaterials.Select(mat => mat == null ? "null"
                        : $"{mat.name} [{mat.shader?.name}] tex {(mat.mainTexture != null ? mat.mainTexture.name + " " + mat.mainTexture.width + "x" + mat.mainTexture.height : "-")} col {mat.color}"));
                case BoxCollider bc:
                    return $"BoxCollider center {bc.center} size {bc.size} trigger {bc.isTrigger}";
                case MeshCollider mc:
                    return $"MeshCollider {(mc.sharedMesh != null ? mc.sharedMesh.name + " verts " + mc.sharedMesh.vertexCount : "-")} convex {mc.convex} trigger {mc.isTrigger}";
                case Collider col:
                    return $"{col.GetType().Name} bounds {col.bounds.min}-{col.bounds.max} trigger {col.isTrigger}";
                case Waypoint w:
                    return $"Waypoint next {(w.next != null ? w.next.name + " " + w.next.transform.position : "-")} split {(w.splitNext != null ? w.splitNext.name : "-")} prev {w.previous?.Count ?? 0} distanceFromEnd {w.distanceFromEnd}";
                case SpawnableObject so:
                    return $"{so.GetType().Name} chance {so.spawnChance} levels {so.spawnLevels}";
                case TerrainTile tt:
                    return $"TerrainTile last {tt.last?.name} left {tt.left?.name} top {tt.top?.name} right {tt.right?.name}";
                case Behaviour b:
                    return $"{b.GetType().Name}{(b.enabled ? "" : " (disabled)")}";
                default:
                    return c.GetType().Name;
            }
        }
    }
}

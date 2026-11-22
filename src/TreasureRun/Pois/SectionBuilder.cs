using System.Collections.Generic;
using System.Linq;
using TreasureRun.Core;
using UnityEngine;

namespace TreasureRun.Pois
{
    /// <summary>
    /// Turns a freshly spawned vanilla tile into a POI section. Vanilla has already chosen the tile (so its exits follow
    /// the vanilla rules, e.g. forced dead ends near the end of a run), connected it and placed the next Expand buttons.
    /// The builder keeps the tile's root, its sand Ground cube and its entry and exit waypoints, and replaces everything
    /// else: the terrain becomes the generated section (<see cref="PoiSectionLogic"/>), the decorations go, and the inner
    /// waypoints follow the generated paths.
    /// </summary>
    public static class SectionBuilder
    {
        public static SectionExits ExitsOf(TerrainTile tile)
        {
            SectionExits e = SectionExits.None;
            if (tile.left != null) e |= SectionExits.Left;
            if (tile.top != null) e |= SectionExits.Top;
            if (tile.right != null) e |= SectionExits.Right;
            return e;
        }

        public static PoiSite Build(TerrainTile tile, PoiDef def, PoiRecord record)
        {
            var exits = (SectionExits)record.Exits;
            SectionLayout layout;
            SectionStyle style = def.Style();
            style.Rotation = record.Rotation;
            try { layout = PoiSectionLogic.Generate(exits, style, new SystemRng(record.Seed)); }
            catch (System.Exception e) { Plugin.Log.LogError($"POI section {def.Id} ({exits}) failed: {e.Message}"); return null; }
            string problem = PoiSectionLogic.Validate(layout);
            if (problem != null) { Plugin.Log.LogError($"POI section {def.Id} ({exits}) is invalid: {problem}"); return null; }

            Transform root = tile.transform;
            Transform ground = root.Find("Ground");
            Material grass = root.GetComponentsInChildren<MeshRenderer>(true)
                .FirstOrDefault(r => r.gameObject.layer == LayerMask.NameToLayer("Grass"))?.sharedMaterial;
            var kept = new[] { tile.last, tile.left, tile.top, tile.right }.Where(w => w != null).ToList();
            Transform waypoints = tile.last.transform.parent ?? root;

            // Everything but the Ground cube and the waypoints goes (terrain, decorations, houses, trees).
            foreach (Transform child in root.Cast<Transform>().ToList())
                if (child != ground && !kept.Any(k => k.transform.IsChildOf(child))) Object.DestroyImmediate(child.gameObject);

            BuildTerrain(root, layout, grass);
            Rewire(tile, root, waypoints, layout);

            PoiSite site = def.Attach(root.gameObject);
            site.Init(def, record, layout);
            return site;
        }

        private static void BuildTerrain(Transform root, SectionLayout layout, Material grass)
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            SectionMeshLogic.Build(layout, verts, normals, tris);
            var mesh = new Mesh { name = "TR_PoiTerrain" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            var go = new GameObject("Terrain") { layer = LayerMask.NameToLayer("Grass") };
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            if (grass != null) renderer.sharedMaterial = grass;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        /// <summary>Keeps the entry and exit waypoints (vanilla links point at them) and rebuilds the ones in between.</summary>
        private static void Rewire(TerrainTile tile, Transform root, Transform parent, SectionLayout layout)
        {
            var keep = new Dictionary<string, Waypoint> { ["last"] = tile.last, ["left"] = tile.left, ["top"] = tile.top, ["right"] = tile.right };
            foreach (Waypoint w in root.GetComponentsInChildren<Waypoint>(true))
                if (!keep.ContainsValue(w)) Object.DestroyImmediate(w.gameObject);

            var nodes = new Waypoint[layout.Waypoints.Count];
            for (int i = 0; i < nodes.Length; i++)
            {
                SectionWaypoint sw = layout.Waypoints[i];
                Waypoint w = sw.Role != null && keep.TryGetValue(sw.Role, out Waypoint kept) && kept != null ? kept : null;
                if (w == null)
                {
                    w = new GameObject("TR_Waypoint").AddComponent<Waypoint>();
                    w.transform.SetParent(parent, false);
                }
                w.transform.position = root.TransformPoint(sw.Local);
                nodes[i] = w;
            }
            foreach (Waypoint w in nodes)
            {
                w.previous.Clear();
                if (w != tile.last) { w.next = null; w.splitNext = null; }
            }
            for (int i = 0; i < nodes.Length; i++)
            {
                int n = layout.Waypoints[i].Next;
                if (n < 0) continue;
                nodes[i].next = nodes[n];
                nodes[n].previous.Add(nodes[i]);
            }
        }
    }
}

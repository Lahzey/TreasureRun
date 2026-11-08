using System;
using System.Collections.Generic;
using System.Linq;
using TreasureRun.Core;
using UnityEngine;

namespace TreasureRun.Voxels
{
    /// <summary>
    /// Replaces vanilla meshes with .vox models placed in <c>models/overrides</c>. Each override needs a sidecar whose
    /// "Source" names the vanilla renderer, exactly as VoxTool writes it when extracting:
    ///   "BiPlane/Art/RotationOffset/BiPlane/default"   (prefab: affects every future instance)
    ///   "GameScene:MainTower/default"                  (scene object)
    /// Simplest workflow: copy an extracted .vox + .json pair into models/overrides, edit the .vox in MagicaVoxel.
    /// Applied when a Treasure Run's GameScene starts, restored when the run ends.
    /// </summary>
    public static class ModelOverrides
    {
        public const string Folder = "overrides";

        private sealed class Applied
        {
            public MeshFilter Filter;
            public Mesh OriginalMesh;
            public MeshRenderer Renderer;
            public Material OriginalMaterial;
        }

        private static readonly List<Applied> applied = new List<Applied>();

        internal static void Initialize()
        {
            TreasureRunMode.RunEnded += _ => RestoreAll();
        }

        public static void ApplyAll()
        {
            RestoreAll();
            List<string> names = VoxelLibrary.AvailableNames().Where(n => n.StartsWith(Folder + "/", StringComparison.OrdinalIgnoreCase)).ToList();
            if (names.Count == 0) return;

            Transform[] allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
            int count = 0;
            foreach (string name in names)
            {
                VoxAsset asset = VoxelLibrary.Get(name);
                string source = asset?.Sidecar?.Source;
                if (string.IsNullOrEmpty(source))
                {
                    Plugin.Log.LogWarning($"Model override '{name}' has no \"Source\" in its .json sidecar; skipped.");
                    continue;
                }

                bool scene = source.Contains(":");
                string path = scene ? source.Substring(source.IndexOf(':') + 1) : source;
                int hits = 0;
                foreach (Transform t in allTransforms)
                {
                    if (t == null || t.gameObject.scene.IsValid() != scene) continue;
                    if (!MatchesPath(t, path)) continue;
                    var filter = t.GetComponent<MeshFilter>();
                    var renderer = t.GetComponent<MeshRenderer>();
                    if (filter == null || renderer == null) continue;
                    applied.Add(new Applied { Filter = filter, OriginalMesh = filter.sharedMesh, Renderer = renderer, OriginalMaterial = renderer.sharedMaterial });
                    filter.sharedMesh = asset.Mesh;
                    renderer.sharedMaterial = asset.Material;
                    hits++;
                }
                if (hits == 0) Plugin.Log.LogWarning($"Model override '{name}': nothing matches source '{source}'.");
                count += hits;
            }
            Plugin.Log.LogInfo($"Model overrides: {names.Count} file(s), {count} renderer(s) replaced.");
        }

        public static void RestoreAll()
        {
            foreach (Applied a in applied)
            {
                if (a.Filter != null) a.Filter.sharedMesh = a.OriginalMesh;
                if (a.Renderer != null) a.Renderer.sharedMaterial = a.OriginalMaterial;
            }
            applied.Clear();
        }

        /// <summary>True if the transform's full hierarchy path (root first, '/' separated) equals <paramref name="path"/>.</summary>
        private static bool MatchesPath(Transform t, string path)
        {
            if (!path.EndsWith(t.name, StringComparison.Ordinal)) return false;
            var parts = new List<string>();
            for (Transform cur = t; cur != null; cur = cur.parent) parts.Add(cur.name);
            parts.Reverse();
            return string.Join("/", parts) == path;
        }
    }
}

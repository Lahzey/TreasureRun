using System.Collections.Generic;
using TreasureRun.Voxels;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TreasureRun.Castles
{
    /// <summary>
    /// Swaps the castle mesh. Both the main menu (background, under DoubleStart) and the game scene contain a
    /// "MainTower" object whose child "default" renders the vanilla castle (32x32 voxel footprint, scaled 0.9375).
    /// Castle models are built centred on X/Z and standing on Y = 0, which matches that placement.
    /// </summary>
    public static class CastleVisuals
    {
        private sealed class Original
        {
            public MeshFilter Filter;
            public Mesh Mesh;
            public MeshRenderer Renderer;
            public Material Material;
        }

        private static readonly Dictionary<int, Original> originals = new Dictionary<int, Original>();

        /// <summary>Finds the castle root ("MainTower") in the loaded scenes.</summary>
        public static GameObject FindCastle()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (!s.isLoaded) continue;
                foreach (GameObject root in s.GetRootGameObjects())
                {
                    Transform t = root.name == "MainTower" ? root.transform : FindDeep(root.transform, "MainTower");
                    if (t != null) return t.gameObject;
                }
            }
            return null;
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                Transform found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>Shows the castle's model on the scene castle (vanilla mesh for castles without a model).</summary>
        public static void Apply(CastleDef def)
        {
            GameObject castle = FindCastle();
            if (castle == null) return;
            Transform meshT = castle.transform.Find("default");
            if (meshT == null) return;
            var filter = meshT.GetComponent<MeshFilter>();
            var renderer = meshT.GetComponent<MeshRenderer>();
            if (filter == null || renderer == null) return;

            int key = filter.GetInstanceID();
            if (!originals.TryGetValue(key, out Original orig) || orig.Filter == null)
            {
                orig = new Original { Filter = filter, Mesh = filter.sharedMesh, Renderer = renderer, Material = renderer.sharedMaterial };
                originals[key] = orig;
            }

            VoxAsset asset = def?.Model != null ? VoxelLibrary.Get(def.Model) : null;
            if (asset != null)
            {
                filter.sharedMesh = asset.Mesh;
                renderer.sharedMaterial = def.ModelAlpha < 1f ? FadeMaterial(asset.Material, def.ModelAlpha) : asset.Material;
            }
            else
            {
                filter.sharedMesh = orig.Mesh;
                renderer.sharedMaterial = orig.Material;
            }
            ApplyParts(meshT, def);
        }

        private static readonly Dictionary<Material, Material> fadeMaterials = new Dictionary<Material, Material>();

        private static Material FadeMaterial(Material source, float alpha)
        {
            if (!fadeMaterials.TryGetValue(source, out Material m) || m == null)
                fadeMaterials[source] = m = Core.Fx.FadeCopy(source, alpha);
            return m;
        }

        public const string PartPrefix = "TR_Part_";

        private static void ApplyParts(Transform mesh, CastleDef def)
        {
            for (int i = mesh.childCount - 1; i >= 0; i--)
            {
                Transform child = mesh.GetChild(i);
                if (!child.name.StartsWith(PartPrefix)) continue;
                child.gameObject.SetActive(false);
                Object.Destroy(child.gameObject);
            }
            if (def == null) return;
            bool inGame = SceneManager.GetActiveScene().name == Core.TreasureRunMode.GameSceneName;
            foreach (CastlePart part in def.Parts)
            {
                if (part.MenuOnly && inGame) continue;
                VoxAsset asset = VoxelLibrary.Get(part.Model);
                if (asset == null) continue;
                var go = new GameObject(PartPrefix + part.Name);
                go.transform.SetParent(mesh, false);
                go.transform.localPosition = part.VoxelPosition * 0.1f; // castle mesh space: 0.1 per voxel
                go.transform.localRotation = Quaternion.Euler(0f, part.Yaw, 0f);
                go.transform.localScale = Vector3.one * part.Scale;
                go.AddComponent<MeshFilter>().sharedMesh = asset.Mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = asset.Material;
                if (part.Animation == PartAnimation.Float) go.AddComponent<FloatingPart>();
            }
        }

        /// <summary>A castle part by name (null if the castle has none).</summary>
        public static Transform FindPart(GameObject castle, string name)
        {
            Transform mesh = castle != null ? castle.transform.Find("default") : null;
            return mesh != null ? mesh.Find(PartPrefix + name) : null;
        }
    }

    /// <summary>Bobs up and down and spins slowly (floating crystals and the like).</summary>
    internal sealed class FloatingPart : MonoBehaviour
    {
        private Vector3 rest;
        private float phase;

        private void Start()
        {
            rest = transform.localPosition;
            phase = Random.value * 6f;
        }

        private void Update()
        {
            float t = Time.time + phase;
            transform.localPosition = rest + Vector3.up * (Mathf.Sin(t * 1.6f) * 0.09f + 0.09f); // about one voxel
            transform.localRotation = Quaternion.Euler(0f, t * 35f, 0f);
        }
    }
}

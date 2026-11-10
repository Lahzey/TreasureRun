using System.Collections.Generic;
using TreasureRun.Voxels;
using UnityEngine;

namespace TreasureRun.Ascensions
{
    /// <summary>
    /// Base of every tower/building ascension component. One instance sits on each placed tower (or building) of the
    /// target while the ascension is owned; it owns the swapped model and the replaced or added behaviour.
    /// Vanilla methods it replaces are skipped by prefixes that check for the component.
    /// </summary>
    public abstract class AscendedTower : MonoBehaviour
    {
        public AscensionDef Def { get; private set; }
        public AscensionTarget Target { get; private set; }

        /// <summary>The vanilla tower (null for buildings).</summary>
        public Tower Tower { get; private set; }

        private readonly List<MeshRenderer> hiddenRenderers = new List<MeshRenderer>();
        private readonly List<GameObject> addedModels = new List<GameObject>();

        internal void Initialize(AscensionDef def, AscensionTarget target)
        {
            Def = def;
            Target = target;
            Tower = GetComponent<Tower>();
            OnAscend();
            if (Tower != null) Tower.SetStats(); // ascension stat changes (SetStats patches) apply at once
        }

        /// <summary>Called once, right after the component is attached (the tower's own Start has already run).</summary>
        protected virtual void OnAscend() { }

        protected virtual void OnDestroy()
        {
            RestoreVanillaModel();
        }

        // ------------------------------------------------------------------ model swap

        /// <summary>
        /// Hides the vanilla voxel art: every MeshRenderer under the object whose mesh is a MagicaVoxel export
        /// (mesh name "default"). Effect renderers (beams, particles, lines, mark icons) stay untouched.
        /// </summary>
        protected void HideVanillaModel()
        {
            foreach (MeshRenderer r in GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!r.enabled || r.name.StartsWith("TR_")) continue;
                MeshFilter f = r.GetComponent<MeshFilter>();
                if (f == null || f.sharedMesh == null || f.sharedMesh.name != "default") continue;
                r.enabled = false;
                hiddenRenderers.Add(r);
            }
        }

        /// <summary>
        /// Shows a voxel model under <paramref name="parent"/> (the tower itself when null, or e.g. its turret, so the
        /// model turns with it). VoxelLibrary places models centred on x/z, standing on y = 0.
        /// </summary>
        protected GameObject ShowModel(string modelName, Transform parent = null, Vector3? localPosition = null,
                                       Quaternion? localRotation = null, float scale = 1f)
        {
            VoxAsset asset = VoxelLibrary.Get(modelName);
            if (asset == null) return null;
            var go = new GameObject("TR_Model_" + modelName);
            go.transform.SetParent(parent != null ? parent : transform, false);
            go.transform.localPosition = localPosition ?? Vector3.zero;
            go.transform.localRotation = localRotation ?? Quaternion.identity;
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = asset.Mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = asset.Material;
            addedModels.Add(go);
            return go;
        }

        private readonly List<(MeshFilter filter, Mesh mesh, MeshRenderer renderer, Material material)> swapped =
            new List<(MeshFilter, Mesh, MeshRenderer, Material)>();

        /// <summary>
        /// Replaces the mesh of a vanilla voxel renderer in place. <paramref name="part"/> is a name on its transform
        /// path (e.g. "TurretArt", "BaseArt"). The model must be built on the vanilla model with the vanilla sidecar
        /// origin (VoxTool AscensionRecipes), so it sits exactly where the vanilla mesh was and turns with the turret.
        /// </summary>
        protected bool SwapVanillaMesh(string part, string modelName)
        {
            VoxAsset asset = VoxelLibrary.Get(modelName);
            if (asset == null) return false;
            foreach (MeshFilter f in GetComponentsInChildren<MeshFilter>(true))
            {
                if (f.sharedMesh == null || f.sharedMesh.name != "default" || !OnPath(f.transform, part)) continue;
                MeshRenderer r = f.GetComponent<MeshRenderer>();
                swapped.Add((f, f.sharedMesh, r, r != null ? r.sharedMaterial : null));
                f.sharedMesh = asset.Mesh;
                if (r != null) r.sharedMaterial = asset.Material;
                return true;
            }
            Plugin.Log.LogWarning($"{GetType().Name}: no vanilla mesh under '{part}' on {name}.");
            return false;
        }

        private bool OnPath(Transform t, string part)
        {
            for (; t != null && t != transform.parent; t = t.parent)
                if (t.name == part) return true;
            return false;
        }

        /// <summary>Removes added models and shows the vanilla art again.</summary>
        protected void RestoreVanillaModel()
        {
            foreach (var s in swapped)
            {
                if (s.filter != null) s.filter.sharedMesh = s.mesh;
                if (s.renderer != null) s.renderer.sharedMaterial = s.material;
            }
            swapped.Clear();
            foreach (GameObject go in addedModels) if (go != null) Destroy(go);
            addedModels.Clear();
            foreach (MeshRenderer r in hiddenRenderers) if (r != null) r.enabled = true;
            hiddenRenderers.Clear();
        }
    }
}

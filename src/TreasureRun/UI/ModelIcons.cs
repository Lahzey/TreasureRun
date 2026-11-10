using System.Collections.Generic;
using TreasureRun.Voxels;
using UnityEngine;

namespace TreasureRun.UI
{
    /// <summary>
    /// Renders voxel models into transparent sprites at runtime (card art for ascensions): the model is placed far
    /// away on an unused layer and photographed by a temporary orthographic camera from the game's isometric angle,
    /// lit by a temporary light. Sprites are cached per model; <see cref="ClearCache"/> after reloading models.
    /// </summary>
    internal static class ModelIcons
    {
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        private static int layer = -1;

        /// <summary>Isometric camera angle (matches the game camera's look: from the front-right, above).</summary>
        private static readonly Quaternion ViewRotation = Quaternion.Euler(32f, -135f, 0f);

        public static Sprite Get(string model, int size = 256)
        {
            if (string.IsNullOrEmpty(model)) return null;
            string key = model + "@" + size;
            if (cache.TryGetValue(key, out Sprite s) && s != null) return s;
            s = Render(model, size);
            if (s != null) cache[key] = s;
            return s;
        }

        public static void ClearCache()
        {
            foreach (Sprite s in cache.Values)
            {
                if (s == null) continue;
                Object.Destroy(s.texture);
                Object.Destroy(s);
            }
            cache.Clear();
        }

        private static int Layer
        {
            get
            {
                if (layer >= 0) return layer;
                layer = 30;
                for (int i = 30; i >= 8; i--)
                    if (string.IsNullOrEmpty(LayerMask.LayerToName(i))) { layer = i; break; }
                return layer;
            }
        }

        private static Sprite Render(string model, int size)
        {
            VoxAsset asset = VoxelLibrary.Get(model);
            if (asset == null) return null;

            Vector3 stage = new Vector3(0f, -2000f, 0f);
            var subject = new GameObject("TR_IconSubject") { layer = Layer };
            subject.transform.position = stage;
            subject.AddComponent<MeshFilter>().sharedMesh = asset.Mesh;
            var renderer = subject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = asset.Material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Bounds b = renderer.bounds;
            float radius = b.extents.magnitude;

            var camGo = new GameObject("TR_IconCamera");
            Camera cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.orthographic = true;
            cam.orthographicSize = radius * 1.02f;
            cam.cullingMask = 1 << Layer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = radius * 4f + 1f;
            camGo.transform.rotation = ViewRotation;
            camGo.transform.position = b.center - camGo.transform.forward * (radius * 2f + 0.5f);

            // Fit the projected bounding box exactly (plus a small margin) and centre on it.
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, 0f), max = new Vector3(float.MinValue, float.MinValue, 0f);
            for (int c = 0; c < 8; c++)
            {
                Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                Vector3 local = camGo.transform.InverseTransformPoint(corner);
                min = Vector3.Min(min, local);
                max = Vector3.Max(max, local);
            }
            Vector3 mid = (min + max) / 2f;
            camGo.transform.position += camGo.transform.right * mid.x + camGo.transform.up * mid.y;
            cam.orthographicSize = Mathf.Max(max.x - min.x, max.y - min.y) / 2f * 1.06f;

            var lightGo = new GameObject("TR_IconLight");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.cullingMask = 1 << Layer;
            light.intensity = 0.55f;
            light.shadows = LightShadows.None;
            lightGo.transform.rotation = Quaternion.Euler(50f, -150f, 0f);

            var rt = RenderTexture.GetTemporary(size, size, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 1;
            RenderTexture previous = RenderTexture.active;
            Texture2D tex;
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "TR_Icon_" + model, filterMode = FilterMode.Bilinear };
                tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                tex.Apply();
            }
            finally
            {
                RenderTexture.active = previous;
                cam.targetTexture = null;
                RenderTexture.ReleaseTemporary(rt);
                Object.Destroy(camGo);
                Object.Destroy(lightGo);
                Object.Destroy(subject);
            }
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}

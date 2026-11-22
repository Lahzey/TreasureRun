using TreasureRun.Core;
using UnityEngine;

namespace TreasureRun.Pois
{
    /// <summary>
    /// Cartoon electrocution: for a moment a monster is drawn as an "X-ray": its own palette with the brightness turned
    /// around (a film negative in electric blue), unlit and slightly see-through. Works for every monster without extra
    /// models: the textures are recoloured once per monster type (<see cref="Fx.RampTexture"/>), and a flash only swaps
    /// the renderers' material arrays and puts them back.
    /// </summary>
    public sealed class XRayFlash : MonoBehaviour
    {
        private static readonly Color Bright = new Color(0.86f, 0.97f, 1f), Dark = new Color(0.04f, 0.1f, 0.32f);
        private static readonly Color Tint = new Color(1f, 1f, 1f, 0.88f);

        private Renderer[] renderers;
        private Material[][] originals;
        private float until;

        public static void Flash(Enemy e, float seconds)
        {
            if (e == null) return;
            XRayFlash f = e.GetComponent<XRayFlash>() ?? e.gameObject.AddComponent<XRayFlash>();
            f.Begin(seconds);
        }

        private void Begin(float seconds)
        {
            until = Mathf.Max(until, Time.time + seconds);
            if (originals != null) return;
            renderers = GetComponentsInChildren<Renderer>(false);
            originals = new Material[renderers.Length][];
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
                Material[] mats = r.sharedMaterials;
                originals[i] = mats;
                var xray = new Material[mats.Length];
                for (int k = 0; k < mats.Length; k++)
                {
                    Texture negative = Fx.RampTexture(mats[k] != null ? mats[k].mainTexture : null, Bright, Dark);
                    xray[k] = (negative != null ? Fx.GhostMaterial(negative, Tint) : null) ?? mats[k];
                }
                r.sharedMaterials = xray;
            }
        }

        private void Update()
        {
            if (originals != null && Time.time >= until) Restore();
        }

        private void OnDisable() => Restore();

        private void Restore()
        {
            if (originals == null) return;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null && originals[i] != null) renderers[i].sharedMaterials = originals[i];
            originals = null;
            renderers = null;
        }
    }
}

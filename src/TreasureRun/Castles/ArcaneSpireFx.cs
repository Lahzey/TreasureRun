using System.Collections.Generic;
using System.Linq;
using TreasureRun.Core;
using UnityEngine;

namespace TreasureRun.Castles
{
    /// <summary>
    /// Arcane Spire effects (vanilla shaders only):
    ///  - <see cref="Lightning"/>: a bolt from a random corner mana crystal to the floating crystal (mana pulse),
    ///  - a rune circle across the whole plinth, at the height of the inner step,
    ///  - <see cref="Flash"/> (a monster obliterated): crystal and runes pulse bright blue. The crystal gets an
    ///    unlit glow shell (same mesh, alpha-blended) and a point light that is only switched on during the pulse.
    /// The floating crystal itself is a castle part (CastleDef.Parts, "crystal"), so it also shows in the menu.
    /// </summary>
    internal sealed class ArcaneSpireFx : MonoBehaviour
    {
        public const float CrystalBaseVoxel = 48f;
        public const float PlinthTopVoxel = 8.2f;
        /// <summary>World radius of the rune circle: reaches the small crystal clusters on the plinth's edge.</summary>
        private const float CircleRadius = 1.38f;
        private const float RuneRadius = 1.0f;

        private static readonly Color RuneIdle = new Color(0.45f, 0.7f, 1f, 0.3f);
        private static readonly Color Bright = new Color(0.65f, 0.88f, 1f, 1f);
        private static readonly Color BoltColor = new Color(0.6f, 0.85f, 1f, 1f);

        private Transform mesh;
        private Transform crystal;
        private MeshRenderer crystalGlow;
        private Material glowMaterial;
        private Light glow;
        private readonly List<LineRenderer> runes = new List<LineRenderer>();
        private List<Vector3> corners = new List<Vector3>();
        private float flash;

        public static ArcaneSpireFx Attach(GameObject castle)
        {
            Transform mesh = castle.transform.Find("default");
            if (mesh == null) return null;
            var fx = castle.AddComponent<ArcaneSpireFx>();
            fx.mesh = mesh;
            fx.Build();
            return fx;
        }

        public void Flash() => flash = 1f;

        private Vector3 Voxel(float z) => mesh.TransformPoint(new Vector3(0f, z * 0.1f, 0f));
        private Vector3 CrystalCenter => crystal != null ? crystal.position + Vector3.up * 0.33f : Voxel(CrystalBaseVoxel + 3.5f);

        private void Build()
        {
            crystal = CastleVisuals.FindPart(gameObject, "crystal");
            if (crystal != null)
            {
                // Glow shell: same mesh, unlit and alpha-blended, slightly larger; only rendered while pulsing.
                MeshFilter source = crystal.GetComponent<MeshFilter>();
                MeshRenderer sourceRenderer = crystal.GetComponent<MeshRenderer>();
                if (source != null && Fx.GlowMaterial != null)
                {
                    var shell = new GameObject("TR_CrystalGlow");
                    shell.transform.SetParent(crystal, false);
                    shell.transform.localScale = Vector3.one * 1.12f;
                    shell.transform.localPosition = new Vector3(0f, -0.04f, 0f);
                    shell.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                    crystalGlow = shell.AddComponent<MeshRenderer>();
                    // No texture: the shell is plain bright blue, whatever the crystal's palette colours are.
                    glowMaterial = new Material(Fx.GlowMaterial) { mainTexture = null };
                    crystalGlow.sharedMaterial = glowMaterial;
                    crystalGlow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    crystalGlow.enabled = false;
                }
            }

            var lightGo = new GameObject("TR_ArcaneGlow");
            lightGo.transform.SetParent(crystal != null ? crystal : transform, false);
            lightGo.transform.position = CrystalCenter;
            glow = lightGo.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(0.4f, 0.65f, 1f);
            glow.range = 5f;
            glow.shadows = LightShadows.None;
            glow.enabled = false; // only during a pulse

            // Corner crystals (kept starting developments), or the tile corners as a fallback.
            corners = FindObjectsOfType<ManaCrystal>()
                .Where(m => (m.transform.position - transform.position).sqrMagnitude < 30f)
                .Select(m => m.transform.position + Vector3.up * 0.4f).ToList();
            if (corners.Count == 0)
                corners = new[] { new Vector3(3, 0.6f, 3), new Vector3(-3, 0.6f, 3), new Vector3(3, 0.6f, -3), new Vector3(-3, 0.6f, -3) }
                    .Select(v => transform.position + v).ToList();

            // Rune circle across the whole plinth (at the inner step's height so the step doesn't cover it).
            Vector3 floor = Voxel(PlinthTopVoxel);
            foreach (float r in new[] { CircleRadius, CircleRadius - 0.1f })
            {
                LineRenderer ring = NewLine("TR_RuneRing", 49, r == CircleRadius ? 0.04f : 0.025f, flat: true);
                if (ring == null) break;
                for (int i = 0; i <= 48; i++)
                {
                    float a = i / 48f * Mathf.PI * 2f;
                    ring.SetPosition(i, floor + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r);
                }
                runes.Add(ring);
            }
            for (int g = 0; g < 8; g++)
            {
                float a = (g + 0.5f) / 8f * Mathf.PI * 2f;
                Vector3 center = floor + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * RuneRadius;
                Vector2[] glyph = Glyphs[g % Glyphs.Length];
                LineRenderer rune = NewLine("TR_Rune", glyph.Length, 0.035f, flat: true);
                if (rune == null) break;
                Vector3 tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                Vector3 outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                for (int i = 0; i < glyph.Length; i++)
                    rune.SetPosition(i, center + (tangent * glyph[i].x + outward * glyph[i].y) * 0.15f);
                runes.Add(rune);
            }
            Apply(0f);
        }

        /// <summary>Glyphs as polylines in [-1,1]².</summary>
        private static readonly Vector2[][] Glyphs =
        {
            new[] { new Vector2(-1, -1), new Vector2(0, 1), new Vector2(1, -1), new Vector2(-0.6f, 0.1f), new Vector2(0.6f, 0.1f) },
            new[] { new Vector2(0, -1), new Vector2(0, 1), new Vector2(1, 0.4f), new Vector2(0, -0.2f) },
            new[] { new Vector2(-1, 1), new Vector2(1, 1), new Vector2(-1, -1), new Vector2(1, -1) },
            new[] { new Vector2(-1, 0), new Vector2(0, 1), new Vector2(1, 0), new Vector2(0, -1), new Vector2(-1, 0) },
            new[] { new Vector2(-1, -1), new Vector2(-1, 1), new Vector2(0, 0), new Vector2(1, 1), new Vector2(1, -1) },
            new[] { new Vector2(0, 1), new Vector2(0, -1), new Vector2(-1, 0.3f), new Vector2(1, 0.3f) },
        };

        /// <summary>A short jagged bolt from a random corner crystal to the floating crystal.</summary>
        public void Lightning()
        {
            if (corners.Count == 0) return;
            Vector3 from = corners[Random.Range(0, corners.Count)];
            Vector3 to = CrystalCenter;
            const int points = 10;
            LineRenderer bolt = NewLine("TR_ManaBolt", points, 0.11f);
            if (bolt == null) return;
            bolt.transform.SetParent(null, true);
            for (int i = 0; i < points; i++)
            {
                float t = i / (points - 1f);
                Vector3 p = Vector3.Lerp(from, to, t);
                p.y += Mathf.Sin(t * Mathf.PI) * 0.5f;
                if (i > 0 && i < points - 1) p += Random.insideUnitSphere * 0.12f;
                bolt.SetPosition(i, p);
            }
            bolt.startColor = new Color(BoltColor.r, BoltColor.g, BoltColor.b, 0.8f);
            bolt.endColor = BoltColor;
            bolt.gameObject.AddComponent<FadeLine>();
            Fx.Burst(to, BoltColor, 10, 0.08f, 0.8f);
            Fx.Burst(from, BoltColor, 6, 0.06f, 0.6f);
        }

        private LineRenderer NewLine(string name, int points, float width, bool flat = false)
        {
            Material mat = Fx.GlowMaterial;
            if (mat == null) return null;
            var go = new GameObject(name);
            go.transform.SetParent(transform, true);
            if (flat) go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // TransformZ alignment = facing up
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = mat;
            line.useWorldSpace = true;
            line.positionCount = points;
            line.startWidth = line.endWidth = width;
            line.numCapVertices = 2;
            if (flat) line.alignment = LineAlignment.TransformZ;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private void Update()
        {
            if (flash > 0f)
            {
                flash = Mathf.Max(0f, flash - Time.deltaTime * 0.9f);
                Apply(flash);
            }
            else if (Time.frameCount % 4 == 0) Apply(0f); // idle rune shimmer, cheap
        }

        private void Apply(float f)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 2.3f);
            Color rune = Color.Lerp(new Color(RuneIdle.r, RuneIdle.g, RuneIdle.b, RuneIdle.a * (0.8f + 0.4f * pulse)), Bright, f);
            foreach (LineRenderer l in runes) { l.startColor = rune; l.endColor = rune; l.widthMultiplier = 1f + f * 1.5f; }

            bool on = f > 0.01f;
            if (crystalGlow != null)
            {
                crystalGlow.enabled = on;
                if (on) glowMaterial.color = new Color(Bright.r, Bright.g, Bright.b, 0.95f * f);
            }
            if (glow != null)
            {
                glow.enabled = on;
                glow.intensity = 4f * f;
            }
        }
    }

    /// <summary>Fades a line renderer out and destroys it.</summary>
    internal sealed class FadeLine : MonoBehaviour
    {
        private const float Duration = 0.45f;
        private LineRenderer line;
        private float t;
        private Color start, end;

        private void Start()
        {
            line = GetComponent<LineRenderer>();
            start = line.startColor;
            end = line.endColor;
        }

        private void Update()
        {
            t += Time.deltaTime / Duration;
            float a = 1f - t;
            line.startColor = new Color(start.r, start.g, start.b, start.a * a);
            line.endColor = new Color(end.r, end.g, end.b, end.a * a);
            if (t >= 1f) Destroy(gameObject);
        }
    }
}

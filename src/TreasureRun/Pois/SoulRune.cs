using System.Collections.Generic;
using TreasureRun.Ascensions;
using TreasureRun.Core;
using UnityEngine;

namespace TreasureRun.Pois
{
    /// <summary>
    /// Soulfire rune (docs/poi-plan.md §6): one of the Soul Urn's rewards, a glowing rune circle on the cell where the urn
    /// stood. The tower built on it attacks faster by <see cref="Bonus"/> (StatLayer). A War Blimp's Radar standing on it
    /// passes the bonus on to the tower on the blimp's deck, which then shows the rune too (<see cref="RuneDeckGlow"/>).
    /// The cell stays buildable; the rune can't be removed.
    /// </summary>
    public sealed class SoulRune : MonoBehaviour
    {
        public static readonly List<SoulRune> All = new List<SoulRune>();
        private static bool registered;

        /// <summary>Attack speed for the tower on it (0.4 = +40%).</summary>
        public float Bonus { get; private set; }

        private MeshRenderer ring, sigil, halo;
        private ParticleSystem motes;
        private float checkTimer, spin, brightness;
        private bool occupied;

        public static SoulRune Create(Vector3 cellGround, float bonus, Transform parent)
        {
            Register();
            var go = new GameObject("TR_SoulRune");
            go.transform.SetParent(parent, true);
            go.transform.position = cellGround;
            go.transform.rotation = Quaternion.identity;
            var rune = go.AddComponent<SoulRune>();
            rune.Bonus = bonus;
            rune.Build(go.transform, 1.06f);
            StatLayer.Refresh();
            return rune;
        }

        /// <summary>The attack speed provider: towers on a rune, and towers on the deck of a War Blimp whose Radar is on one.</summary>
        private static void Register()
        {
            if (registered) return;
            registered = true;
            StatLayer.Register("SoulRune", (tower, stats) =>
            {
                float bonus = BonusFor(tower);
                if (bonus > 0f) stats.Percent(TowerStat.AttackSpeed, bonus);
            });
        }

        /// <summary>The rune whose cell contains a point (horizontally), if any.</summary>
        public static SoulRune At(Vector3 p)
        {
            foreach (SoulRune r in All)
            {
                if (r == null) continue;
                Vector3 d = p - r.transform.position;
                if (Mathf.Abs(d.x) < 0.5f && Mathf.Abs(d.z) < 0.5f) return r;
            }
            return null;
        }

        /// <summary>
        /// The rune that powers a tower: the one under it, or, for a tower riding a War Blimp's deck, the one under the
        /// blimp's Radar (a blimp hovering over a rune gives nothing).
        /// </summary>
        public static SoulRune For(Tower t)
        {
            if (t == null || All.Count == 0) return null;
            BlimpPassenger passenger = t.GetComponent<BlimpPassenger>();
            if (passenger != null)
            {
                RadarTower owner = passenger.Deck != null && passenger.Deck.Blimp != null ? passenger.Deck.Blimp.Owner : null;
                return owner != null ? At(owner.transform.position) : null;
            }
            return At(t.transform.position);
        }

        public static float BonusFor(Tower t)
        {
            SoulRune r = For(t);
            return r != null ? r.Bonus : 0f;
        }

        /// <summary>The rune a War Blimp deck draws on (its Radar stands on it), if any.</summary>
        internal static SoulRune ForDeck(BlimpDeck deck)
        {
            RadarTower owner = deck != null && deck.Blimp != null ? deck.Blimp.Owner : null;
            return owner != null ? At(owner.transform.position) : null;
        }

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        /// <summary>
        /// The visuals: a soft halo, the outer rune circle and the turning sigil, lying just above the ground, and motes of
        /// soulfire rising from it. <paramref name="size"/> is the circle's diameter (a little over a cell, so it shows round a
        /// tower's base).
        /// </summary>
        internal void Build(Transform parent, float size)
        {
            halo = SoulFx.QuadObject("TR_RuneHalo", parent, SoulFx.FlatQuad, SoulFx.SoftGlow, Color.clear, size * 1.5f);
            halo.transform.localPosition = Vector3.up * 0.015f;
            ring = SoulFx.QuadObject("TR_RuneRing", parent, SoulFx.FlatQuad, SoulFx.RuneRing, Color.clear, size);
            ring.transform.localPosition = Vector3.up * 0.025f;
            sigil = SoulFx.QuadObject("TR_RuneSigil", parent, SoulFx.FlatQuad, SoulFx.RuneSigil, Color.clear, size * 0.58f);
            sigil.transform.localPosition = Vector3.up * 0.03f;
            motes = SoulFx.Particles("TR_RuneMotes", parent, SoulFx.Soul, true);
            motes.transform.localPosition = Vector3.up * 0.05f;
            var main = motes.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
            var shape = motes.shape;
            shape.shapeType = ParticleSystemShapeType.Cone; // a disc rising straight up
            shape.angle = 3f;
            shape.radius = size * 0.45f;
            shape.radiusThickness = 0.35f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var em = motes.emission;
            em.rateOverTime = 7f;
            motes.Play();
        }

        private void Update() => Animate(transform, ref spin, ref brightness, ref checkTimer, ref occupied, ring, sigil, halo, motes, CheckOccupied);

        /// <summary>A tower stands on the rune (it speeds up and brightens).</summary>
        private bool CheckOccupied()
        {
            Vector3 c = transform.position + Vector3.up * 0.5f;
            bool found = false;
            foreach (Collider col in Physics.OverlapBox(c, new Vector3(0.35f, 0.45f, 0.35f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
            {
                Tower t = col != null ? col.GetComponentInParent<Tower>() : null;
                if (t == null) continue;
                found = true;
                RuneShell.Ensure(t);
            }
            return found;
        }

        /// <summary>Shared animation of a rune and its copy on a blimp deck: the circle breathes, the sigil turns, the motes rise.</summary>
        internal static void Animate(Transform root, ref float spin, ref float brightness, ref float checkTimer, ref bool active,
                                     MeshRenderer ring, MeshRenderer sigil, MeshRenderer halo, ParticleSystem motes, System.Func<bool> isActive)
        {
            float dt = Time.deltaTime;
            if ((checkTimer -= dt) <= 0f)
            {
                checkTimer = 0.5f;
                bool now = isActive();
                if (now != active && motes != null)
                {
                    var em = motes.emission;
                    em.rateOverTime = now ? 16f : 7f;
                }
                active = now;
            }
            brightness = Mathf.MoveTowards(brightness, active ? 1f : 0.75f, dt * 1.5f);
            spin += dt * (active ? 90f : 35f);
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 2.6f);
            if (ring != null)
            {
                ring.transform.localRotation = Quaternion.Euler(0f, -spin * 0.25f, 0f);
                SoulFx.SetColor(ring, new Color(SoulFx.Soul.r, SoulFx.Soul.g, SoulFx.Soul.b, Mathf.Min(1f, 1.1f * brightness * pulse)));
            }
            if (sigil != null)
            {
                sigil.transform.localRotation = Quaternion.Euler(0f, spin, 0f);
                SoulFx.SetColor(sigil, new Color(SoulFx.SoulCore.r, SoulFx.SoulCore.g, SoulFx.SoulCore.b, 0.85f * brightness));
            }
            if (halo != null) SoulFx.SetColor(halo, new Color(SoulFx.Soul.r, SoulFx.Soul.g, SoulFx.Soul.b, 0.35f * brightness * pulse));
        }

        private float deckTimer;

        private void LateUpdate()
        {
            // A War Blimp whose Radar stands here carries the rune on its deck.
            if ((deckTimer -= Time.deltaTime) > 0f) return;
            deckTimer = 0.5f;
            foreach (BlimpDeck deck in BlimpDeck.All)
                if (deck != null && ForDeck(deck) == this && deck.GetComponentInChildren<RuneDeckGlow>() == null)
                    RuneDeckGlow.Attach(deck, this);
        }
    }

    /// <summary>
    /// The soulfire rune on a War Blimp's deck while the blimp's Radar stands on a rune: the same circle, sized to the deck,
    /// flying with the blimp; it goes out when the Radar no longer stands on the rune (sold, or the rune gone). The deck's
    /// tower gets the rune's attack speed (SoulRune.For).
    /// </summary>
    internal sealed class RuneDeckGlow : MonoBehaviour
    {
        private BlimpDeck deck;
        private SoulRune rune;
        private MeshRenderer ring, sigil, halo;
        private ParticleSystem motes;
        private float checkTimer, spin, brightness, coverTimer;
        private bool active;

        public static void Attach(BlimpDeck deck, SoulRune rune)
        {
            var go = new GameObject("TR_RuneDeckGlow");
            go.transform.SetParent(deck.transform, false);
            go.transform.localPosition = Vector3.up * 0.02f;
            go.transform.localRotation = Quaternion.identity;
            var glow = go.AddComponent<RuneDeckGlow>();
            glow.deck = deck;
            glow.rune = rune;
            const float size = 1.5f;
            glow.halo = SoulFx.QuadObject("TR_RuneHalo", go.transform, SoulFx.FlatQuad, SoulFx.SoftGlow, Color.clear, size * 1.4f);
            glow.ring = SoulFx.QuadObject("TR_RuneRing", go.transform, SoulFx.FlatQuad, SoulFx.RuneRing, Color.clear, size);
            glow.ring.transform.localPosition = Vector3.up * 0.01f;
            glow.sigil = SoulFx.QuadObject("TR_RuneSigil", go.transform, SoulFx.FlatQuad, SoulFx.RuneSigil, Color.clear, size * 0.58f);
            glow.sigil.transform.localPosition = Vector3.up * 0.02f;
            glow.motes = SoulFx.Particles("TR_RuneMotes", go.transform, SoulFx.Soul, false);
            var main = glow.motes.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.12f);
            var shape = glow.motes.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 3f;
            shape.radius = size * 0.45f;
            shape.radiusThickness = 0.35f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var em = glow.motes.emission;
            em.rateOverTime = 7f;
            glow.motes.Play();
            Plugin.Log.LogInfo($"Soulfire rune: a War Blimp's deck draws on the rune at {rune.transform.position} (+{rune.Bonus:0%}).");
            StatLayer.Refresh();
        }

        private void Update()
        {
            if ((coverTimer -= Time.deltaTime) <= 0f)
            {
                coverTimer = 0.5f;
                if (rune == null || deck == null || SoulRune.ForDeck(deck) != rune)
                {
                    Destroy(gameObject);
                    StatLayer.Refresh();
                    return;
                }
                if (deck.Passenger != null) RuneShell.Ensure(deck.Passenger);
            }
            SoulRune.Animate(transform, ref spin, ref brightness, ref checkTimer, ref active, ring, sigil, halo, motes,
                             () => deck != null && !deck.IsFree);
        }

        /// <summary>The deck shows the rune (for the console and scenarios).</summary>
        public static bool On(BlimpDeck deck) => deck != null && deck.GetComponentInChildren<RuneDeckGlow>() != null;
    }

    /// <summary>
    /// A ghostly shell of soulfire round a tower the rune powers (on the rune, or on the deck of a War Blimp whose Radar
    /// stands on one): every mesh of the tower is drawn a second time, a few hundredths of a unit larger on every side,
    /// unlit and translucent in the soul colour, and the shell flickers like plasma. The copies are children of the
    /// tower's own parts, so they turn with its turret; a model change (an ascension) rebuilds them, and the shell goes
    /// out when the tower no longer draws on a rune.
    /// </summary>
    internal sealed class RuneShell : MonoBehaviour
    {
        /// <summary>How far the shell stands off the tower's surface (world units).</summary>
        private const float Thickness = 0.045f;
        private const string PartName = "TR_RuneShell";

        private Tower tower;
        private readonly List<(MeshFilter source, Mesh mesh, MeshRenderer shell)> parts = new List<(MeshFilter, Mesh, MeshRenderer)>();
        private float checkTimer, phase;
        private static Material material;

        private static Material Material => material != null ? material : material = new Material(Fx.GlowMaterial) { name = "TR_RuneShell" };

        public static void Ensure(Tower t)
        {
            if (t == null || t.GetComponent<RuneShell>() != null || SoulRune.BonusFor(t) <= 0f) return;
            var shell = t.gameObject.AddComponent<RuneShell>();
            shell.tower = t;
            shell.phase = Random.value * 10f;
            shell.Build();
        }

        private void Build()
        {
            Clear();
            foreach (MeshFilter mf in GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = mf.sharedMesh;
                if (mesh == null || mf.name == PartName || mesh == SoulFx.FlatQuad || mesh == SoulFx.StandingQuad) continue;
                var source = mf.GetComponent<MeshRenderer>();
                if (source == null || !source.enabled) continue;
                var go = new GameObject(PartName) { layer = mf.gameObject.layer };
                go.transform.SetParent(mf.transform, false);
                // A constant standoff in world units: scale each local axis by its size in the world.
                Bounds b = mesh.bounds;
                Vector3 lossy = mf.transform.lossyScale;
                Vector3 scale = new Vector3(Grow(b.size.x * Mathf.Abs(lossy.x)), Grow(b.size.y * Mathf.Abs(lossy.y)), Grow(b.size.z * Mathf.Abs(lossy.z)));
                go.transform.localScale = scale;
                go.transform.localPosition = Vector3.Scale(b.center, Vector3.one - scale);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = Material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                parts.Add((mf, mesh, r));
            }
            Animate();
        }

        private static float Grow(float worldSize) => worldSize > 1e-4f ? Mathf.Min(1.6f, 1f + 2f * Thickness / worldSize) : 1f;

        private void Clear()
        {
            foreach (var (_, _, shell) in parts) if (shell != null) Destroy(shell.gameObject);
            parts.Clear();
        }

        private void Update()
        {
            if ((checkTimer -= Time.deltaTime) <= 0f)
            {
                checkTimer = 0.5f;
                if (tower == null || SoulRune.BonusFor(tower) <= 0f)
                {
                    Clear();
                    Destroy(this);
                    return;
                }
                bool changed = false;
                foreach (var (source, mesh, shell) in parts)
                    if (source == null || shell == null || source.sharedMesh != mesh) { changed = true; break; }
                if (changed) Build();
            }
            Animate();
        }

        /// <summary>Plasma flicker: the alpha and the tint waver on two beats.</summary>
        private void Animate()
        {
            float t = Time.time + phase;
            float flicker = 0.5f + 0.3f * Mathf.Sin(t * 3.1f) + 0.2f * Mathf.Sin(t * 11.7f);
            Color c = Color.Lerp(SoulFx.Soul, SoulFx.SoulCore, 0.5f + 0.5f * Mathf.Sin(t * 1.7f));
            c.a = 0.16f + 0.16f * flicker;
            foreach (var (_, _, shell) in parts) SoulFx.SetColor(shell, c);
        }

        private void OnDestroy() => Clear();
    }
}

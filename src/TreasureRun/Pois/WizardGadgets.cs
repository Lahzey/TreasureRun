using System;
using System.Collections;
using System.Collections.Generic;
using TreasureRun.Core;
using TreasureRun.Voxels;
using UnityEngine;

namespace TreasureRun.Pois
{
    /// <summary>
    /// The Wizard's Tower's apparatus (docs/poi-plan.md §7): one neat, symmetric gadget per spell (models from
    /// tools/VoxTool/Recipes/WizardRecipes.cs), set round the plain tower at different sides and heights. Each one idles
    /// (the anemometer turns, the crystal floats, the rings spin, the lantern sways, embers rise) and winds up before its
    /// spell leaves it: a bulge of fire travels up the chimney, lightning spirals up the rod, the crystal spins up in a
    /// swirl of frost, the anemometer whirls, the lantern swings and flares, the rings spin up round the flask.
    /// Angles are world angles round the tower (0 = +x, 90 = +z; the camera looks from 135).
    /// </summary>
    internal sealed class WizardGadgets : MonoBehaviour
    {
        // ----- layout (world units from the tower's foot)
        private const float ChimneyAngle = 45f, ChimneyRadius = 0.6f, ChimneyFoot = 0f, SegmentHeight = 0.2f;
        /// <summary>Segments of the chimney, from the ground to its crown above the brim.</summary>
        internal const int Segments = 16;
        private const float RodAngle = 330f, RodRadius = 0.78f, RodFoot = 2.15f, OrbHeight = 2.15f;
        private const float IceAngle = 175f, IceRadius = 0.79f, BalconyHeight = 1.35f, CrystalFloat = 0.6f;
        private const float WindAngle = 250f, ArmRadius = 0.92f, ArmHeight = 1.95f, MastOffset = 0.3f, MastHeight = 0.85f;
        private const float LanternAngle = 95f, LanternArmRadius = 0.83f, LanternArmHeight = 1.4f, HookOffset = 0.3f, LanternDrop = 0.82f;
        private const float GoldAngle = 218f, GoldRadius = 1.0f, FlaskHeight = 0.48f, RingScale = 0.85f;

        private Vector3 foot;
        private readonly List<Transform> chimney = new List<Transform>();
        private Transform crystal, rotor, rings, lanternPivot;
        private Pulse orbGlow, crystalGlow, lanternGlow, flaskGlow;
        private MeshRenderer bulgeGlow;
        private float rotorSpeed = 90f, crystalSpeed = 40f, ringSpeed = 60f, swing = 6f;
        private float rotorAngle, crystalAngle, ringAngle, swingPhase;
        private bool charging;

        /// <summary>The chimney's segments (scenarios: the bulge) and the anemometer's rotor.</summary>
        internal IReadOnlyList<Transform> ChimneySegments => chimney;
        internal Transform Rotor => rotor;

        /// <summary>A spell is winding up (no other can start).</summary>
        public bool Charging => charging;

        private static Vector3 Dir(float deg) => new Vector3(Mathf.Cos(deg * Mathf.Deg2Rad), 0f, Mathf.Sin(deg * Mathf.Deg2Rad));
        /// <summary>A part's rotation that points its model +x outwards at an angle.</summary>
        private static Quaternion Out(float deg) => Quaternion.Euler(0f, -deg, 0f);

        private Vector3 ChimneyAxis => foot + Dir(ChimneyAngle) * ChimneyRadius;
        private Vector3 RodAxis => foot + Dir(RodAngle) * RodRadius;
        private Vector3 MastTop => foot + Dir(WindAngle) * (ArmRadius + MastOffset) + Vector3.up * (ArmHeight + MastHeight);
        private Vector3 Hook => foot + Dir(LanternAngle) * (LanternArmRadius + HookOffset) + Vector3.up * (LanternArmHeight + 0.02f);

        /// <summary>Where each spell leaves the tower.</summary>
        public Vector3 Emitter(WizardSpell s)
        {
            switch (s)
            {
                case WizardSpell.Fireball: return ChimneyAxis + Vector3.up * (ChimneyFoot + Segments * SegmentHeight + 0.6f);
                case WizardSpell.Lightning: return RodAxis + Vector3.up * (RodFoot + OrbHeight);
                case WizardSpell.IceSpikes: return foot + Dir(IceAngle) * IceRadius + Vector3.up * (BalconyHeight + CrystalFloat + 0.5f);
                case WizardSpell.Tornado: return MastTop + Vector3.up * 0.15f;
                case WizardSpell.Necromancy: return Hook + Vector3.up * (0.3f - LanternDrop);
                default: return foot + Dir(GoldAngle) * GoldRadius + Vector3.up * FlaskHeight;
            }
        }

        /// <summary>Seconds a spell winds up before it leaves its gadget.</summary>
        public static float ChargeTime(WizardSpell s) => s == WizardSpell.Fireball ? 0.75f : s == WizardSpell.Goldify ? 0.5f : 0.6f;

        public static WizardGadgets Build(WizardTower t, Vector3 towerFoot)
        {
            var g = t.gameObject.AddComponent<WizardGadgets>();
            g.foot = towerFoot;
            g.Assemble(t.transform);
            return g;
        }

        private static GameObject Part(string name, Vector3 at, Quaternion rot, Transform parent, float scale = 1f)
        {
            GameObject go = VoxelLibrary.Spawn("pois/wizard/" + name, at, rot, parent);
            if (go != null && scale != 1f) go.transform.localScale = Vector3.one * scale;
            return go;
        }

        private void Assemble(Transform parent)
        {
            var root = new GameObject("TR_WizardGadgets").transform;
            root.SetParent(parent, false);
            root.position = foot;

            // Fire: the arcane chimney, the same all the way down, a third of it sunk into the tower's side (like a round
            // stair turret set into a keep); it cuts through the brim, its brazier against the hat.
            Vector3 ca = ChimneyAxis;
            for (int i = 0; i < Segments; i++)
            {
                GameObject seg = Part(i % 3 == 1 ? "chimney_rune" : "chimney_ring", ca + Vector3.up * (ChimneyFoot + i * SegmentHeight), Quaternion.identity, root);
                if (seg != null) chimney.Add(seg.transform);
            }
            Part("chimney_crown", ca + Vector3.up * (ChimneyFoot + Segments * SegmentHeight), Quaternion.identity, root);
            bulgeGlow = SoulFx.GlowBillboard("TR_ChimneyBulge", root, new Color(1f, 0.55f, 0.15f, 0f), 0.5f);
            ParticleSystem embers = SoulFx.Particles("TR_WizardEmbers", root, WizardSpells.Fire, true);
            embers.transform.position = Emitter(WizardSpell.Fireball) - Vector3.up * 0.15f;
            var main = embers.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
            var shape = embers.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 15f;
            shape.radius = 0.1f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var em = embers.emission;
            em.rateOverTime = 6f;
            embers.Play();

            // Lightning: the rod beside the hat, strapped under the brim.
            Part("rod", RodAxis + Vector3.up * RodFoot, Quaternion.identity, root);
            Part("strap", foot + Dir(RodAngle) * 0.55f + Vector3.up * (RodFoot + 0.1f), Out(RodAngle), root);
            orbGlow = Glow(Emitter(WizardSpell.Lightning), WizardSpells.Spark, 0.55f, root);

            // Ice: a balcony under the left window, the crystal floating over its pedestal.
            Vector3 balcony = foot + Dir(IceAngle) * IceRadius + Vector3.up * BalconyHeight;
            Part("ice_balcony", balcony, Out(IceAngle), root);
            GameObject c = Part("ice_crystal", balcony + Vector3.up * CrystalFloat, Quaternion.identity, root);
            if (c != null) crystal = c.transform;
            crystalGlow = Glow(Emitter(WizardSpell.IceSpikes), WizardSpells.Frost, 0.45f, root);

            // Tornado: the anemometer on its arm at the back left, high up.
            Part("anemometer_arm", foot + Dir(WindAngle) * (ArmRadius) + Vector3.up * ArmHeight, Out(WindAngle), root);
            GameObject r = Part("anemometer_rotor", MastTop, Quaternion.identity, root);
            if (r != null) rotor = r.transform;

            // Necromancy: the skull lantern hanging from its bracket by the right window.
            Part("lantern_arm", foot + Dir(LanternAngle) * LanternArmRadius + Vector3.up * LanternArmHeight, Out(LanternAngle), root);
            lanternPivot = new GameObject("TR_LanternPivot").transform;
            lanternPivot.SetParent(root, false);
            lanternPivot.position = Hook;
            lanternPivot.rotation = Out(LanternAngle);
            GameObject lantern = VoxelLibrary.Spawn("pois/wizard/lantern", Vector3.zero, Quaternion.identity, lanternPivot);
            if (lantern != null) lantern.transform.localPosition = Vector3.down * LanternDrop;
            lanternGlow = Glow(Emitter(WizardSpell.Necromancy), WizardSpells.Ghoul, 0.4f, lanternPivot);

            // Goldify: the alembic on the ground at the front left, its rings turning round the flask.
            Vector3 alembic = foot + Dir(GoldAngle) * GoldRadius;
            Part("alembic", alembic, Quaternion.identity, root);
            GameObject ringsGo = Part("alembic_rings", alembic + Vector3.up * (FlaskHeight - 0.55f * RingScale), Quaternion.identity, root, RingScale);
            if (ringsGo != null)
            {
                // Spin round the flask's centre: a pivot there, the rings hung from it.
                var pivot = new GameObject("TR_RingPivot").transform;
                pivot.SetParent(root, false);
                pivot.position = alembic + Vector3.up * FlaskHeight;
                ringsGo.transform.SetParent(pivot, true);
                rings = pivot;
            }
            flaskGlow = Glow(Emitter(WizardSpell.Goldify), WizardSpells.GoldLight, 0.35f, root);
        }

        private static Pulse Glow(Vector3 at, Color c, float size, Transform parent)
        {
            MeshRenderer r = SoulFx.GlowBillboard("TR_WizardGlow", parent, new Color(c.r, c.g, c.b, 0.5f), size);
            r.transform.position = at;
            Pulse p = r.gameObject.AddComponent<Pulse>();
            p.Init(c, 0.5f, size);
            return p;
        }

        // ------------------------------------------------------------------ idle

        private void Update()
        {
            float dt = Time.deltaTime;
            if (!charging)
            {
                rotorSpeed = Mathf.MoveTowards(rotorSpeed, 90f, 900f * dt);
                crystalSpeed = Mathf.MoveTowards(crystalSpeed, 40f, 700f * dt);
                ringSpeed = Mathf.MoveTowards(ringSpeed, 60f, 700f * dt);
                swing = Mathf.MoveTowards(swing, 6f, 20f * dt);
                foreach (Pulse p in new[] { orbGlow, crystalGlow, lanternGlow, flaskGlow })
                    if (p != null) p.Boost = Mathf.MoveTowards(p.Boost, 1f, 2f * dt);
            }
            rotorAngle += rotorSpeed * dt;
            crystalAngle += crystalSpeed * dt;
            ringAngle += ringSpeed * dt;
            swingPhase += dt * (2.2f + swing * 0.08f);
            if (rotor != null) rotor.localRotation = Quaternion.Euler(0f, rotorAngle, 0f);
            if (crystal != null)
            {
                crystal.localRotation = Quaternion.Euler(0f, crystalAngle, 0f);
                crystal.position = foot + Dir(IceAngle) * IceRadius + Vector3.up * (BalconyHeight + CrystalFloat + 0.04f * Mathf.Sin(Time.time * 1.7f));
            }
            if (rings != null) rings.localRotation = Quaternion.Euler(0f, ringAngle, 0f) * Quaternion.Euler(18f, 0f, 0f);
            if (lanternPivot != null) lanternPivot.rotation = Out(LanternAngle) * Quaternion.Euler(swing * Mathf.Sin(swingPhase), 0f, 0f);
        }

        // ------------------------------------------------------------------ wind-ups

        /// <summary>Winds the spell's gadget up, then calls <paramref name="fire"/> (the spell leaves it).</summary>
        public void Charge(WizardSpell s, Action fire)
        {
            if (charging) return;
            StartCoroutine(Run(s, fire));
        }

        private IEnumerator Run(WizardSpell s, Action fire)
        {
            charging = true;
            float d = ChargeTime(s);
            IEnumerator anim;
            switch (s)
            {
                case WizardSpell.Fireball: anim = Bulge(d); break;
                case WizardSpell.Lightning: anim = Spiral(d); break;
                case WizardSpell.IceSpikes: anim = Spin(d, v => crystalSpeed = v, 40f, 1100f, crystalGlow, WizardSpells.Frost, Emitter(s), 0.6f); break;
                case WizardSpell.Tornado: anim = Spin(d, v => rotorSpeed = v, 90f, 1800f, null, WizardSpells.Wind, Emitter(s), 0.8f); break;
                case WizardSpell.Necromancy: anim = Swing(d); break;
                default: anim = Spin(d, v => ringSpeed = v, 60f, 1100f, flaskGlow, WizardSpells.GoldLight, Emitter(s), 0.5f); break;
            }
            while (anim.MoveNext()) yield return anim.Current;
            charging = false;
            try { fire?.Invoke(); }
            catch (Exception e) { Plugin.Log.LogError($"Wizard spell failed: {e}"); }
        }

        /// <summary>The fire: a bulge travels up the chimney, as if the fireball inside pressed its walls apart.</summary>
        private IEnumerator Bulge(float d)
        {
            float t = 0f;
            while (t < d)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / d);
                float centre = Mathf.Lerp(-1.5f, Segments + 0.5f, k * k * (3f - 2f * k));
                for (int i = 0; i < chimney.Count; i++)
                {
                    float x = (i - centre) / 1.1f;
                    float s = 1f + 0.7f * Mathf.Exp(-x * x);
                    chimney[i].localScale = new Vector3(s, 1f, s);
                }
                if (bulgeGlow != null)
                {
                    bulgeGlow.transform.position = ChimneyAxis + Vector3.up * (ChimneyFoot + (Mathf.Clamp(centre, 0f, Segments) + 0.5f) * SegmentHeight);
                    SoulFx.SetColor(bulgeGlow, new Color(1f, 0.55f, 0.15f, 0.55f * Mathf.Sin(Mathf.PI * k)));
                }
                yield return null;
            }
            foreach (Transform seg in chimney) seg.localScale = Vector3.one;
            if (bulgeGlow != null) SoulFx.SetColor(bulgeGlow, new Color(1f, 0.55f, 0.15f, 0f));
        }

        /// <summary>The lightning: two strands of lightning spiral up the rod into the orb, which swells.</summary>
        private IEnumerator Spiral(float d)
        {
            const int points = 64;
            var lines = new LineRenderer[2];
            for (int k = 0; k < 2; k++)
            {
                var go = new GameObject("TR_RodSpiral");
                go.transform.SetParent(transform, false);
                LineRenderer l = go.AddComponent<LineRenderer>();
                l.sharedMaterial = Fx.GlowMaterial;
                l.useWorldSpace = true;
                l.numCapVertices = 2;
                l.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                l.receiveShadows = false;
                l.startWidth = l.endWidth = 0.07f;
                l.startColor = new Color(WizardSpells.Spark.r, WizardSpells.Spark.g, WizardSpells.Spark.b, 0.2f);
                l.endColor = WizardSpells.SparkCore;
                lines[k] = l;
            }
            Vector3 axis = RodAxis;
            float h0 = RodFoot + 0.45f, h1 = RodFoot + OrbHeight - 0.12f;
            float t = 0f;
            while (t < d)
            {
                t += Time.deltaTime;
                float head = Mathf.Clamp01(t / (d * 0.85f)), tail = Mathf.Max(0f, head - 0.45f);
                for (int k = 0; k < 2; k++)
                {
                    LineRenderer l = lines[k];
                    l.positionCount = points;
                    for (int i = 0; i < points; i++)
                    {
                        float u = Mathf.Lerp(tail, head, i / (points - 1f));
                        float a = u * Mathf.PI * 2f * 3.5f + k * Mathf.PI;
                        float r = 0.14f + UnityEngine.Random.Range(-0.02f, 0.02f);
                        l.SetPosition(i, axis + new Vector3(Mathf.Cos(a) * r, Mathf.Lerp(h0, h1, u), Mathf.Sin(a) * r));
                    }
                }
                if (orbGlow != null) orbGlow.Boost = 1f + 1.6f * head;
                yield return null;
            }
            foreach (LineRenderer l in lines) Destroy(l.gameObject);
        }

        /// <summary>A gadget that spins up (crystal, anemometer, rings) in a swirl of its colour, its glow swelling.</summary>
        private IEnumerator Spin(float d, Action<float> setSpeed, float from, float to, Pulse glow, Color color, Vector3 at, float swirl)
        {
            SwirlIn(at, color, d, swirl);
            float t = 0f;
            while (t < d)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / d);
                setSpeed(Mathf.Lerp(from, to, k * k));
                if (glow != null) glow.Boost = 1f + 1.4f * k;
                yield return null;
            }
        }

        /// <summary>The necromancy: the lantern swings harder and harder, its green glow flaring.</summary>
        private IEnumerator Swing(float d)
        {
            SwirlIn(Emitter(WizardSpell.Necromancy), WizardSpells.Ghoul, d, 0.45f);
            float t = 0f;
            while (t < d)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / d);
                swing = Mathf.Lerp(6f, 30f, k);
                if (lanternGlow != null) lanternGlow.Boost = 1f + 1.8f * k;
                yield return null;
            }
        }

        /// <summary>Motes of a colour spiralling in to a point while a spell winds up.</summary>
        private void SwirlIn(Vector3 at, Color color, float seconds, float radius)
        {
            var holder = new GameObject("TR_WizardSwirl");
            holder.transform.position = at;
            ParticleSystem ps = SoulFx.Particles("Swirl", holder.transform, color, false);
            var main = ps.main;
            main.loop = false;
            main.duration = seconds;
            main.startLifetime = 0.4f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.13f);
            var em = ps.emission;
            em.rateOverTime = 70f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.radiusThickness = 0f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.orbitalY = new ParticleSystem.MinMaxCurve(7f);
            vel.radial = new ParticleSystem.MinMaxCurve(-radius * 2.2f);
            vel.x = vel.y = vel.z = new ParticleSystem.MinMaxCurve(0f);
            ps.Play();
            Destroy(holder, seconds + 0.8f);
        }
    }
}

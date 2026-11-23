using HarmonyLib;
using TreasureRun.Ascensions;
using TreasureRun.Ascensions.Phase3;
using TreasureRun.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TreasureRun.Pois
{
    /// <summary>
    /// The wizard's land: the whole section of a Wizard's Tower. Its grass shimmers blue (a translucent copy of the
    /// section's terrain mesh, breathing) with motes of blue light rising from it. Nothing can be built on it: the build
    /// ghost doesn't show there, and a click says whose land it is (like a closed shop's "Closed"). Exception: with the
    /// Ley Line Network ascension, mana siphons may be built there (to feed the tower).
    /// </summary>
    [HarmonyPatch]
    internal static class WizardLand
    {
        public const string Refusal = "This land belongs to the wizard";
        private static readonly Color Shine = new Color(0.45f, 0.7f, 1f, 1f);
        private static Vector3? refused;
        private static float nextMessage;

        /// <summary>The cell the last build attempt was refused on (scenarios).</summary>
        internal static Vector3? Refused => refused;

        /// <summary>May this be built on the wizard's land? Only a ley siphon, and only with the Ley Line Network.</summary>
        public static bool Allows(GameObject thing) =>
            thing != null && thing.GetComponent<ManaSiphon>() != null &&
            AscensionRegistry.Owned(AscensionTarget.ManaSiphon)?.Id == LeySiphon.Id;

        /// <summary>Is building <paramref name="thing"/> at a world position refused?</summary>
        public static bool Refuses(Vector3 position, GameObject thing) =>
            TreasureRunMode.Active && WizardTower.LandAt(position) != null && !Allows(thing);

        /// <summary>The land's look: the blue shine over its grass and the rising motes.</summary>
        public static void Dress(WizardTower t)
        {
            Transform terrain = t.transform.Find("Terrain");
            MeshFilter mf = terrain != null ? terrain.GetComponent<MeshFilter>() : null;
            if (mf != null && mf.sharedMesh != null && Fx.GlowMaterial != null)
            {
                var go = new GameObject("TR_WizardShine");
                go.transform.SetParent(terrain, false);
                go.transform.localPosition = Vector3.up * 0.006f;
                go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = Fx.GlowMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                go.AddComponent<LandShine>().Init(r, Shine);
            }
            ParticleSystem motes = SoulFx.Particles("TR_WizardMotes", t.transform, new Color(0.55f, 0.78f, 1f, 1f), true);
            motes.transform.position = t.Center + Vector3.up * 0.36f;
            motes.transform.rotation = Quaternion.identity;
            var main = motes.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
            var shape = motes.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(7f, 0.05f, 7f);
            shape.rotation = Vector3.zero;
            var vel = motes.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.y = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            vel.x = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
            var em = motes.emission;
            em.rateOverTime = 14f;
            motes.Play();
        }

        /// <summary>Build mode: the ghost doesn't show on the wizard's land (War Blimp decks above it are fine).</summary>
        [HarmonyPostfix, HarmonyPatch(typeof(BuildingManager), nameof(BuildingManager.SamplePoint)), HarmonyPriority(Priority.Low)]
        private static void SamplePointPostfix(BuildingManager __instance, ref bool __result)
        {
            refused = null;
            if (!__result || WizardTower.All.Count == 0 || BlimpDeckPatches.OverDeck || __instance.currentGhost == null) return;
            Vector3 p = __instance.currentGhost.transform.position;
            if (!Refuses(p, __instance.thingToBuild)) return;
            __instance.HideGhost();
            __result = false;
            refused = p;
        }

        /// <summary>A click on the wizard's land in build mode: a floating note says whose land it is.</summary>
        [HarmonyPrefix, HarmonyPatch(typeof(BuildingManager), "Update")]
        private static void UpdatePrefix(BuildingManager __instance)
        {
            if (!__instance.buildMode || refused == null || !Input.GetMouseButtonDown(0)) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            Refuse(refused.Value);
        }

        /// <summary>The refusal note over a cell (throttled).</summary>
        public static void Refuse(Vector3 cell)
        {
            if (Time.unscaledTime < nextMessage) return;
            nextMessage = Time.unscaledTime + 0.35f;
            GameHelpers.FloatingText(cell + Vector3.up * 0.8f, Refusal, "Red", 1f, 1f);
        }
    }

    /// <summary>The blue shine over the wizard's grass: faint, breathing slowly.</summary>
    internal sealed class LandShine : MonoBehaviour
    {
        private Renderer r;
        private Color color;

        public void Init(Renderer renderer, Color c) { r = renderer; color = c; }

        private void Update()
        {
            float a = 0.1f + 0.04f * Mathf.Sin(Time.time * 1.3f) + 0.015f * Mathf.Sin(Time.time * 3.7f);
            SoulFx.SetColor(r, new Color(color.r, color.g, color.b, a));
        }
    }
}

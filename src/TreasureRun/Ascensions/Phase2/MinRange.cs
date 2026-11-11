using System.Collections.Generic;
using HarmonyLib;
using TreasureRun.Core;
using UnityEngine;

namespace TreasureRun.Ascensions.Phase2
{
    /// <summary>The tower whose popup (TowerUI) is open, i.e. the selected tower.</summary>
    [HarmonyPatch]
    internal static class TowerSelection
    {
        private static TowerUI current;

        public static Tower Selected => current != null ? current.myTower : null;

        public static bool IsSelected(Tower t) => t != null && Selected == t;

        [HarmonyPostfix, HarmonyPatch(typeof(TowerUI), nameof(TowerUI.SetStats))]
        private static void TowerUiSetStats(TowerUI __instance) => current = __instance;
    }

    /// <summary>A component on a tower that cannot target monsters closer than <see cref="MinRange"/>.</summary>
    internal interface IMinRange
    {
        float MinRange { get; }
    }

    /// <summary>
    /// Minimum range: towers with an <see cref="IMinRange"/> component skip monsters within it when picking a target
    /// (flat distance between centres). Shown as a red hatched circle while the tower is selected.
    /// </summary>
    [HarmonyPatch]
    internal static class MinRangeRules
    {
        public static float Of(Tower t)
        {
            if (t == null) return 0f;
            var m = t.GetComponent<IMinRange>();
            return m != null ? m.MinRange : 0f;
        }

        public static bool TooClose(Tower t, Vector3 monster)
        {
            float min = Of(t);
            if (min <= 0f) return false;
            Vector3 d = monster - t.transform.position;
            d.y = 0f;
            return d.sqrMagnitude < min * min;
        }

        private static readonly List<Collider> kept = new List<Collider>();

        [HarmonyPrefix, HarmonyPatch(typeof(Tower), nameof(Tower.SelectEnemy))]
        private static bool SelectEnemyPrefix(Tower __instance, ref Collider[] possibleTargets, ref GameObject __result)
        {
            if (!TreasureRunMode.Active || Of(__instance) <= 0f) return true;
            kept.Clear();
            foreach (Collider c in possibleTargets)
                if (c != null && !TooClose(__instance, c.transform.position)) kept.Add(c);
            if (kept.Count == possibleTargets.Length) return true;
            if (kept.Count == 0) { __result = null; return false; }
            possibleTargets = kept.ToArray();
            return true;
        }
    }

    /// <summary>Red, semi-transparent hatched circle showing a tower's minimum range while the tower is selected.</summary>
    internal sealed class MinRangeIndicator : MonoBehaviour
    {
        private static readonly Color Red = new Color(1f, 0.18f, 0.15f, 0.55f);
        private const float Hatch = 0.16f, Width = 0.035f;
        private Tower tower;
        private GameObject art;
        private float radius;

        public static MinRangeIndicator Attach(Tower tower, float radius)
        {
            var go = new GameObject("TR_MinRange");
            go.transform.SetParent(tower.transform, false);
            var m = go.AddComponent<MinRangeIndicator>();
            m.tower = tower;
            m.radius = radius;
            return m;
        }

        private void Build()
        {
            if (art != null) Destroy(art);
            art = new GameObject("Art");
            art.transform.SetParent(transform, false);
            Vector3 c = tower.transform.position;
            float y = c.y + 0.07f;
            LineRenderer ring = Phase2Kit.Line("MinRangeRing", Red, Width, art.transform);
            ring.loop = true;
            const int n = 48;
            ring.positionCount = n;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                ring.SetPosition(i, new Vector3(c.x + Mathf.Cos(a) * radius, y, c.z + Mathf.Sin(a) * radius));
            }
            // Hatching that reads diagonal on screen: the isometric camera looks along a world diagonal, so world x
            // lines appear slanted.
            Vector3 along = Vector3.right, across = Vector3.forward;
            for (float o = -radius + Hatch * 0.5f; o < radius; o += Hatch)
            {
                float half = Mathf.Sqrt(Mathf.Max(0f, radius * radius - o * o));
                if (half < 0.03f) continue;
                LineRenderer l = Phase2Kit.Line("MinRangeHatch", new Color(Red.r, Red.g, Red.b, Red.a * 0.8f), Width * 0.7f, art.transform);
                Vector3 mid = new Vector3(c.x, y, c.z) + across * o;
                l.SetPosition(0, mid - along * half);
                l.SetPosition(1, mid + along * half);
            }
        }

        private void LateUpdate()
        {
            if (tower == null) { Destroy(gameObject); return; }
            bool show = TowerSelection.IsSelected(tower);
            if (show && art == null) Build();
            if (art != null && art.activeSelf != show) art.SetActive(show);
        }
    }
}

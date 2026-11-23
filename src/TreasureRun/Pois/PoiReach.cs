using System.Reflection;
using HarmonyLib;
using TreasureRun.Core;
using UnityEngine;

namespace TreasureRun.Pois
{
    /// <summary>
    /// A POI's reach drawn like a tower's range while its popup is open (the vanilla TowerUI line: its material, width and
    /// colour, 60 segments at the same height). Used by the Soul Urn and the Wizard's Tower.
    /// </summary>
    public static class PoiReach
    {
        private static readonly FieldInfo TowerUiLine = AccessTools.Field(typeof(TowerUI), "line");

        /// <summary>A circle of <paramref name="radius"/> round <paramref name="centre"/> (its height is ignored), hidden until enabled.</summary>
        public static LineRenderer Create(string name, Transform parent, Vector3 centre, float radius)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var reach = go.AddComponent<LineRenderer>();
            LineRenderer template = null;
            foreach (TowerUI ui in Resources.FindObjectsOfTypeAll<TowerUI>())
                if ((template = TowerUiLine?.GetValue(ui) as LineRenderer) != null && template.sharedMaterial != null) break;
            if (template != null)
            {
                reach.sharedMaterial = template.sharedMaterial;
                reach.widthCurve = template.widthCurve;
                reach.widthMultiplier = template.widthMultiplier;
                reach.colorGradient = template.colorGradient;
                reach.numCapVertices = template.numCapVertices;
            }
            else
            {
                reach.sharedMaterial = Fx.LineMaterial ?? Fx.GlowMaterial;
                reach.startWidth = reach.endWidth = 0.05f;
            }
            reach.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            reach.receiveShadows = false;
            reach.useWorldSpace = true;
            reach.positionCount = 61;
            Vector3 c = new Vector3(centre.x, 0.4f, centre.z);
            for (int i = 0; i < 61; i++)
            {
                float a = i * 6f * Mathf.Deg2Rad;
                reach.SetPosition(i, c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius);
            }
            reach.enabled = false;
            return reach;
        }
    }
}

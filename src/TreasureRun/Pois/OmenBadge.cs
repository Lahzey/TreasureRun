using TreasureRun.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TreasureRun.Pois
{
    /// <summary>
    /// The omen on an Expand button: the POI's icon to the left of the button and its name underneath. It is a child of
    /// the button, so it pops in and out with it (hidden during waves and card draws).
    /// </summary>
    public static class OmenBadge
    {
        public const string Name = "TR_Omen";

        public static void Attach(TileSpawnLocation marker, PoiDef def)
        {
            if (marker == null || def == null) return;
            Button button = marker.GetComponentInChildren<Button>(true);
            if (button == null) { Plugin.Log.LogWarning("POI omen: the Expand button has no Button."); return; }
            var rt = (RectTransform)button.transform;
            Transform old = rt.Find(Name);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            float h = rt.rect.height > 1f ? rt.rect.height : 40f;
            float size = h * 1.45f;
            var go = new GameObject(Name, typeof(RectTransform));
            var badge = (RectTransform)go.transform;
            badge.SetParent(rt, false);
            badge.anchorMin = badge.anchorMax = new Vector2(0f, 0.5f);
            badge.pivot = new Vector2(1f, 0.5f);
            badge.sizeDelta = new Vector2(size, size);
            badge.anchoredPosition = new Vector2(-h * 0.15f, 0f);

            var icon = go.AddComponent<Image>();
            icon.sprite = IconLibrary.Get(def.Icon);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            if (icon.sprite == null) icon.color = new Color(0.35f, 0.45f, 0.8f, 0.9f);

            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                var nameGo = new GameObject("Name", typeof(RectTransform));
                var nrt = (RectTransform)nameGo.transform;
                nrt.SetParent(rt, false);
                nrt.anchorMin = new Vector2(0f, 0f);
                nrt.anchorMax = new Vector2(1f, 0f);
                nrt.pivot = new Vector2(0.5f, 1f);
                nrt.sizeDelta = new Vector2(rt.rect.width * 1.6f, h * 0.9f);
                nrt.anchoredPosition = new Vector2(0f, -h * 0.05f);
                var text = nameGo.AddComponent<Text>();
                text.font = label.font;
                // The vanilla label is a big text scaled down; match its on-screen size (the name at about half of "Expand").
                text.fontSize = Mathf.Max(8, Mathf.RoundToInt(label.fontSize * label.transform.localScale.y * 0.5f));
                text.resizeTextForBestFit = false;
                text.alignment = TextAnchor.UpperCenter;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.color = new Color(1f, 0.93f, 0.7f);
                text.raycastTarget = false;
                text.text = def.Name;
                var shadow = nameGo.AddComponent<Outline>();
                shadow.effectColor = new Color(0.15f, 0.1f, 0.05f, 0.9f);
                shadow.effectDistance = new Vector2(h * 0.03f, -h * 0.03f);
                nameGo.transform.SetParent(go.transform, true);
            }
        }
    }
}

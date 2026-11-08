using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace TreasureRun.Castles
{
    /// <summary>
    /// Makes the castle clickable like houses and towers: a trigger collider on a clickable layer (triggers are
    /// ignored by building placement) and an IBuildable that opens the vanilla building popup (SimpleUI, borrowed from
    /// the house prefab) with the castle's description and live stats (CastleBehaviour.InfoText).
    /// </summary>
    public static class CastleInfo
    {
        private sealed class Clickable : MonoBehaviour, IBuildable
        {
            public void SetStats() { }
            public void Demolish() { }
            public void SpawnUI() => Show();
        }

        private static GameObject uiPrefab;

        public static void Attach(GameObject castle)
        {
            if (castle == null || UIManager.instance == null) return;
            int mask = UIManager.instance.clickableMask.value;
            int layer = Enumerable.Range(0, 32).FirstOrDefault(i => (mask & (1 << i)) != 0);
            if (mask == 0) return;

            var go = new GameObject("TR_CastleClickTarget") { layer = layer };
            go.transform.SetParent(castle.transform, false);
            go.transform.position = castle.transform.position + Vector3.up * 1.2f;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(2.6f, 2.4f, 2.6f) / Mathf.Max(0.01f, castle.transform.lossyScale.x);
            go.AddComponent<Clickable>();
            // The castle root's own collider (Building layer) is also clickable and may be hit first.
            if (castle.GetComponent<Clickable>() == null) castle.AddComponent<Clickable>();
        }

        public static void Show()
        {
            if (CastleRuntime.Behaviour != null && CastleRuntime.Behaviour.OnClicked()) return;
            GameObject prefab = UiPrefab();
            if (prefab == null) { Plugin.Log.LogWarning("Castle info: no building popup prefab found."); return; }
            Vector3 pos = Core.GameHelpers.CastlePosition;
            SimpleUI ui = Object.Instantiate(prefab, pos, Quaternion.identity).GetComponent<SimpleUI>();
            if (ui == null) return;
            if (ui.demolishButton != null) ui.demolishButton.SetActive(false);

            // Title panel ("LevelText" in the vanilla popup) shows the castle name.
            CastleDef def = CastleRuntime.Active ?? CastleRegistry.Default;
            Text title = ui.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.name == "LevelText");
            if (title != null) title.text = def.Name;

            Text text = ui.discriptionText;
            if (text != null)
            {
                text.supportRichText = true;
                text.fontSize = Mathf.RoundToInt(text.fontSize * 0.8f);
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                ui.SetDiscriptionText(BuildText(includeName: false));
                Fit(text);
            }
        }

        /// <summary>Name, description and live stats as shown in the popup.</summary>
        public static string BuildText(bool includeName = true)
        {
            CastleDef def = CastleRuntime.Active ?? CastleRegistry.Default;
            string text = (includeName ? $"<b>{def.Name}</b>\n" : "") + Plain(def.Description);
            string info = CastleRuntime.Behaviour?.InfoText();
            if (!string.IsNullOrEmpty(info)) text += "\n\n" + info;
            return text;
        }

        /// <summary>Keeps bold, drops other tags and blank lines (the popup is small).</summary>
        private static string Plain(string s) =>
            Regex.Replace(Regex.Replace(s ?? "", "</?(i|color[^>]*)>", ""), "\n\n+", "\n");

        /// <summary>
        /// The house popup's text box (world-space canvas, text scaled 0.005) fits about four lines. Grows the text box
        /// and its background panel downwards to fit the castle text; the panel's top edge stays where it was.
        /// </summary>
        private static void Fit(Text text)
        {
            var rt = (RectTransform)text.transform;
            float width = rt.rect.width;
            if (width <= 1f) return;
            TextGenerationSettings settings = text.GetGenerationSettings(new Vector2(width, 0f));
            settings.scaleFactor = 1f;
            float preferred = text.cachedTextGeneratorForLayout.GetPreferredHeight(text.text, settings);
            float extra = preferred + text.fontSize * 0.5f - rt.rect.height;
            if (extra <= 0f) return;
            rt.sizeDelta += new Vector2(0f, extra);
            if (rt.parent is RectTransform panel)
            {
                float panelExtra = extra * rt.localScale.y;
                panel.sizeDelta += new Vector2(0f, panelExtra);
                panel.anchoredPosition -= new Vector2(0f, panelExtra / 2f);
            }
        }

        private static GameObject UiPrefab()
        {
            if (uiPrefab != null) return uiPrefab;
            House house = Resources.FindObjectsOfTypeAll<House>().FirstOrDefault(h => h.UIObject != null);
            uiPrefab = house != null ? house.UIObject : null;
            return uiPrefab;
        }
    }
}

using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TreasureRun.Pois
{
    /// <summary>
    /// Click target and popup of a POI structure. The click target is a collider on a clickable layer (one POI may have
    /// several, e.g. the Lightning Trap's four poles, all opening the same popup). The popup is the vanilla building
    /// popup (SimpleUI, borrowed from the house prefab) with a title, a text and, in place of its demolish button,
    /// either one action button or a slider (e.g. mana per second).
    /// </summary>
    public sealed class PoiPopup
    {
        public Func<string> Title, Text, ActionLabel;
        public Action Action;
        /// <summary>A whole-number slider instead of the button (when <see cref="SliderChanged"/> is set).</summary>
        public int SliderMin, SliderMax;
        public Func<int> SliderValue;
        public Action<int> SliderChanged;
        /// <summary>World position the popup opens at.</summary>
        public Func<Vector3> Anchor;

        private SimpleUI open;
        private Slider slider;

        /// <summary>Makes an object clickable for this popup: a collider (trigger or solid) on the given layer.</summary>
        public void AddTarget(GameObject go) => go.AddComponent<Target>().Popup = this;

        /// <summary>The first layer UIManager treats as clickable.</summary>
        public static int ClickableLayer()
        {
            int mask = UIManager.instance != null ? UIManager.instance.clickableMask.value : 0;
            return Enumerable.Range(0, 32).FirstOrDefault(i => (mask & (1 << i)) != 0);
        }

        private sealed class Target : MonoBehaviour, IBuildable
        {
            public PoiPopup Popup;
            public void SetStats() { }
            public void Demolish() { }
            public void SpawnUI() => Popup?.Show(transform.position);
        }

        public bool IsOpen => open != null;

        public void Show(Vector3 at)
        {
            // A click on the open popup's own slider or button also hits whatever lies behind it: don't reopen then.
            if (open != null && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            GameObject prefab = UiPrefab();
            if (prefab == null) { Plugin.Log.LogWarning("POI popup: no building popup prefab found."); return; }
            open = UnityEngine.Object.Instantiate(prefab, Anchor != null ? Anchor() : at, Quaternion.identity).GetComponent<SimpleUI>();
            if (open == null) return;
            slider = null;
            if (open.discriptionText != null)
            {
                open.discriptionText.supportRichText = true;
                open.discriptionText.fontSize = Mathf.RoundToInt(open.discriptionText.fontSize * 0.8f);
                open.discriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
                open.discriptionText.verticalOverflow = VerticalWrapMode.Overflow;
            }
            if (open.demolishButton != null)
            {
                Button b = open.demolishButton.GetComponent<Button>() ?? open.demolishButton.GetComponentInChildren<Button>(true);
                if (SliderChanged != null) open.demolishButton.SetActive(false);
                else if (Action != null && b != null)
                {
                    b.onClick = new Button.ButtonClickedEvent();
                    b.onClick.AddListener(() =>
                    {
                        Action();
                        SFXManager.instance?.ButtonClick();
                        Refresh();
                    });
                    open.demolishButton.SetActive(true);
                }
                else open.demolishButton.SetActive(false);
            }
            Refresh();
            if (open.discriptionText != null) Castles.CastleInfo.Fit(open.discriptionText);
            if (SliderChanged != null && open.discriptionText != null) slider = MakeSlider(open.discriptionText);
            Refresh();
        }

        /// <summary>Updates the open popup's texts and slider (e.g. after a change).</summary>
        public void Refresh()
        {
            if (open == null) return;
            Text title = open.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.name == "LevelText");
            if (title != null && Title != null) title.text = Title();
            if (Text != null) open.SetDiscriptionText(Text());
            if (slider != null && SliderValue != null) slider.SetValueWithoutNotify(SliderValue());
            if (slider == null && ActionLabel != null && open.demolishButton != null)
            {
                Text label = open.demolishText ?? open.demolishButton.GetComponentInChildren<Text>(true);
                if (label != null) label.text = ActionLabel();
            }
        }

        /// <summary>
        /// A uGUI slider at the bottom of the popup's text panel, built by hand in the panel's own units (the popup is a
        /// world-space canvas with tiny units, so the default pixel layout doesn't fit). The panel grows downwards for it.
        /// </summary>
        private Slider MakeSlider(Text text)
        {
            var textRt = (RectTransform)text.transform;
            if (!(textRt.parent is RectTransform panel)) return null;
            float h = panel.rect.height * 0.12f;
            panel.sizeDelta += new Vector2(0f, h * 1.6f);
            panel.anchoredPosition -= new Vector2(0f, h * 0.8f);
            textRt.anchoredPosition += new Vector2(0f, h * 0.8f);
            int layer = panel.gameObject.layer;

            RectTransform Node(string name, Transform parent, Vector2 min, Vector2 max)
            {
                var go = new GameObject(name, typeof(RectTransform)) { layer = layer };
                var rt = (RectTransform)go.transform;
                rt.SetParent(parent, false);
                rt.anchorMin = min; rt.anchorMax = max;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                return rt;
            }
            Image Paint(RectTransform rt, Color c) { var img = rt.gameObject.AddComponent<Image>(); img.color = c; return img; }

            RectTransform root = Node("TR_PoiSlider", panel, new Vector2(0.1f, 0f), new Vector2(0.9f, 0f));
            root.pivot = new Vector2(0.5f, 0f);
            root.sizeDelta = new Vector2(0f, h);
            root.anchoredPosition = new Vector2(0f, h * 0.45f);
            Paint(Node("Background", root, new Vector2(0f, 0.3f), new Vector2(1f, 0.7f)), new Color(0.36f, 0.26f, 0.15f, 1f));
            RectTransform fillArea = Node("Fill Area", root, new Vector2(0f, 0.3f), new Vector2(1f, 0.7f));
            fillArea.offsetMin = new Vector2(h * 0.5f, 0f); fillArea.offsetMax = new Vector2(-h * 0.5f, 0f);
            RectTransform fill = Node("Fill", fillArea, Vector2.zero, Vector2.one);
            fill.offsetMin = new Vector2(-h * 0.5f, 0f);
            Paint(fill, new Color(0.45f, 0.85f, 1f, 1f));
            RectTransform slideArea = Node("Handle Slide Area", root, Vector2.zero, Vector2.one);
            slideArea.offsetMin = new Vector2(h * 0.5f, 0f); slideArea.offsetMax = new Vector2(-h * 0.5f, 0f);
            RectTransform handle = Node("Handle", slideArea, new Vector2(0f, 0f), new Vector2(0f, 1f));
            handle.sizeDelta = new Vector2(h, 0f);
            Image handleImg = Paint(handle, new Color(1f, 0.95f, 0.8f, 1f));
            var outline = handle.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.3f, 0.2f, 0.1f, 1f);
            outline.effectDistance = new Vector2(h * 0.08f, -h * 0.08f);

            var s = root.gameObject.AddComponent<Slider>();
            s.fillRect = fill;
            s.handleRect = handle;
            s.targetGraphic = handleImg;
            s.direction = Slider.Direction.LeftToRight;
            s.wholeNumbers = true;
            s.minValue = SliderMin;
            s.maxValue = SliderMax;
            s.SetValueWithoutNotify(SliderValue != null ? SliderValue() : SliderMin);
            s.onValueChanged.AddListener(v =>
            {
                SliderChanged?.Invoke(Mathf.RoundToInt(v));
                Refresh();
            });
            return s;
        }

        private static GameObject uiPrefab;

        private static GameObject UiPrefab()
        {
            if (uiPrefab != null) return uiPrefab;
            House house = Resources.FindObjectsOfTypeAll<House>().FirstOrDefault(h => h.UIObject != null);
            uiPrefab = house != null ? house.UIObject : null;
            return uiPrefab;
        }
    }
}

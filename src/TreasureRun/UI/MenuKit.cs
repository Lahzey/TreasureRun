using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TreasureRun.UI
{
    /// <summary>Visual style taken from the vanilla menu (font, panel and button sprites).</summary>
    internal sealed class MenuStyle
    {
        public Font Font;
        public Sprite PanelSprite;      // UI9SliceOrange (framed panel, also the "pressed/on" look)
        public float PanelPpum = 5f;
        public Sprite ButtonSprite;     // UI9SliceOrangeFilled (normal button)
        public float ButtonPpum = 7f;
        public Color TextDark = new Color(0.2f, 0.2f, 0.2f);
        public Color TextOn = new Color(1f, 0.84f, 0.3f);
        public Color Outline = new Color(0.3f, 0.18f, 0.05f, 0.9f);
        public Color Divider = new Color(0.42f, 0.33f, 0.24f, 0.8f);
    }

    /// <summary>
    /// Tiny uGUI builder: everything is placed with layout groups and relative anchors, so the menu adapts to any
    /// resolution (the canvas uses ScaleWithScreenSize; sizes below are in 1920x1080 reference units).
    /// </summary>
    internal static class MenuKit
    {
        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rt, float left = 0, float right = 0, float bottom = 0, float top = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        public static Image Sliced(GameObject go, Sprite sprite, float ppum, Color color)
        {
            var img = go.GetComponent<Image>();
            if (img == null) img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            img.pixelsPerUnitMultiplier = ppum;
            img.color = color;
            return img;
        }

        public static VerticalLayoutGroup Vertical(GameObject go, int padding, float spacing, TextAnchor align = TextAnchor.UpperCenter)
        {
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(padding, padding, padding, padding);
            v.spacing = spacing;
            v.childAlignment = align;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup Horizontal(GameObject go, float spacing, TextAnchor align = TextAnchor.MiddleCenter, bool expandWidth = true)
        {
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = align;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = expandWidth;
            h.childForceExpandHeight = false;
            return h;
        }

        public static void FitContent(GameObject go, bool horizontal, bool vertical)
        {
            var f = go.AddComponent<ContentSizeFitter>();
            f.horizontalFit = horizontal ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            f.verticalFit = vertical ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
        }

        public static LayoutElement Size(GameObject go, float prefWidth = -1, float prefHeight = -1, float flexWidth = -1, float minHeight = -1)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            if (prefWidth >= 0) le.preferredWidth = prefWidth;
            if (prefHeight >= 0) le.preferredHeight = prefHeight;
            if (flexWidth >= 0) le.flexibleWidth = flexWidth;
            if (minHeight >= 0) le.minHeight = minHeight;
            return le;
        }

        public static Text Label(Transform parent, MenuStyle style, string text, int size, TextAnchor align = TextAnchor.MiddleCenter,
                                 FontStyle fontStyle = FontStyle.Normal)
        {
            RectTransform rt = Rect("Text", parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = style.Font;
            t.fontSize = size;
            t.fontStyle = fontStyle;
            t.alignment = align;
            t.color = style.TextDark;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = text;
            return t;
        }

        public static Image Divider(Transform parent, MenuStyle style)
        {
            RectTransform rt = Rect("Divider", parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = style.Divider;
            img.raycastTarget = false;
            Size(rt.gameObject, prefHeight: 3, minHeight: 3);
            return img;
        }

        /// <summary>A button with the vanilla button sprite and a centred label (label fills the button).</summary>
        public static MenuButton Button(Transform parent, MenuStyle style, string text, int fontSize, float height, UnityAction onClick)
        {
            RectTransform rt = Rect("Button", parent);
            Sliced(rt.gameObject, style.ButtonSprite, style.ButtonPpum, Color.white);
            var button = rt.gameObject.AddComponent<Button>();
            button.onClick.AddListener(onClick);
            Size(rt.gameObject, prefHeight: height, minHeight: height);

            Text label = Label(rt, style, text, fontSize);
            Stretch((RectTransform)label.transform, 10, 10, 4, 4);
            var outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = style.Outline;
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.enabled = false;

            var mb = rt.gameObject.AddComponent<MenuButton>();
            mb.Init(style, button, rt.GetComponent<Image>(), label, outline);
            return mb;
        }
    }

    /// <summary>Button that can show an on/off state: "on" uses the framed panel sprite with golden, outlined text.</summary>
    internal sealed class MenuButton : MonoBehaviour
    {
        private MenuStyle style;
        public Button Button { get; private set; }
        public Image Background { get; private set; }
        public Text Label { get; private set; }
        private Outline outline;

        public void Init(MenuStyle s, Button b, Image bg, Text label, Outline o)
        {
            style = s; Button = b; Background = bg; Label = label; outline = o;
        }

        public void SetOn(bool on)
        {
            Background.sprite = on ? style.PanelSprite : style.ButtonSprite;
            Background.pixelsPerUnitMultiplier = on ? style.PanelPpum : style.ButtonPpum;
            Label.color = on ? style.TextOn : style.TextDark;
            Label.fontStyle = on ? FontStyle.Bold : FontStyle.Normal;
            outline.enabled = on;
        }

        public void SetInteractable(bool interactable)
        {
            Button.interactable = interactable;
            Background.color = interactable ? Color.white : new Color(1f, 1f, 1f, 0.55f);
        }
    }

    /// <summary>Keeps the overlay visible exactly while the vanilla PlayMenu object is active.</summary>
    internal sealed class ActiveLink : MonoBehaviour
    {
        public GameObject Target;
        public Action OnShown;

        private void OnEnable()
        {
            if (Target == null) return;
            Target.SetActive(true);
            OnShown?.Invoke();
        }

        private void OnDisable()
        {
            if (Target != null) Target.SetActive(false);
        }
    }
}

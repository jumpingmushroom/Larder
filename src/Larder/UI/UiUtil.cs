using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Larder.UI
{
    internal static class UiUtil
    {
        private static Sprite _white;

        /// <summary>A plain white sprite for backgrounds when the game's own can't be borrowed.</summary>
        public static Sprite White
        {
            get
            {
                if (_white != null)
                    return _white;
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                var px = new Color32[16];
                for (int i = 0; i < px.Length; i++)
                    px[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(px);
                tex.Apply();
                tex.hideFlags = HideFlags.HideAndDontSave;
                _white = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
                _white.hideFlags = HideFlags.HideAndDontSave;
                return _white;
            }
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static void Size(GameObject go, float w, float h)
        {
            // No ?? here: Unity overloads null for destroyed objects, which ?? bypasses.
            LayoutElement le = go.GetComponent<LayoutElement>();
            if (le == null)
                le = go.AddComponent<LayoutElement>();
            le.minWidth = le.preferredWidth = w;
            le.minHeight = le.preferredHeight = h;
        }

        /// <summary>The inventory screen's own font (from its craft button), so the panel matches the game.</summary>
        public static TMP_FontAsset Font
        {
            get
            {
                InventoryGui gui = InventoryGui.instance;
                TMP_Text t = gui != null && gui.m_craftButton != null ? gui.m_craftButton.GetComponentInChildren<TMP_Text>(true) : null;
                if (t != null)
                    return t.font;
                return Hud.instance != null && Hud.instance.m_healthText != null ? Hud.instance.m_healthText.font : null;
            }
        }

        public static TextMeshProUGUI Text(Transform parent, string name, float size, TextAlignmentOptions align)
        {
            RectTransform rt = Rect(name, parent);
            // Added while inactive so TMP's Awake runs after the font is set; otherwise it looks
            // for its default LiberationSans (not shipped with the game) and logs a warning.
            rt.gameObject.SetActive(false);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = Font;
            if (font != null)
                t.font = font;
            rt.gameObject.SetActive(true);
            t.fontSize = size;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.richText = true;
            t.raycastTarget = false;
            t.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            return t;
        }

        /// <summary>A button styled like the template (sprite, colours, transitions) with our own label and listener.</summary>
        public static Button Button(Transform parent, string name, Button template, string label, UnityAction onClick)
        {
            RectTransform rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            Image timg = template != null ? template.GetComponent<Image>() : null;
            if (timg != null)
            {
                img.sprite = timg.sprite;
                img.type = timg.type;
                img.color = timg.color;
            }
            else
            {
                img.sprite = White;
                img.color = new Color(0.3f, 0.25f, 0.2f, 0.9f);
            }
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            if (template != null)
            {
                b.transition = template.transition;
                b.colors = template.colors;
                b.spriteState = template.spriteState;
            }
            b.onClick.AddListener(onClick);
            TextMeshProUGUI t = Text(rt, "Label", 15f, TextAlignmentOptions.Center);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch((RectTransform)t.transform);
            t.text = label;
            return b;
        }

        public static void SetLabel(Button b, string text, Color color)
        {
            TMP_Text t = b.GetComponentInChildren<TMP_Text>(true);
            if (t == null)
                return;
            t.text = text;
            t.color = color;
        }
    }
}

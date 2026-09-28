using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Larder.UI
{
    /// <summary>One planned food: icon, text, and an Eat button (wired in Task 8).</summary>
    internal sealed class ComboRow
    {
        public RectTransform Root;
        public Image Icon;
        public TextMeshProUGUI Text;
        public Button Eat;
        public string FoodId;

        public static ComboRow Create(Transform parent, Button template, int index)
        {
            var r = new ComboRow();
            r.Root = UiUtil.Rect("Row" + index, parent);
            var h = r.Root.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 8f;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;

            RectTransform icon = UiUtil.Rect("Icon", r.Root);
            r.Icon = icon.gameObject.AddComponent<Image>();
            r.Icon.preserveAspect = true;
            r.Icon.raycastTarget = false;
            UiUtil.Size(icon.gameObject, 40f, 40f);

            r.Text = UiUtil.Text(r.Root, "Text", 15f, TextAlignmentOptions.Left);
            r.Text.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            r.Eat = UiUtil.Button(r.Root, "Eat", template, "Eat", () => Eater.Eat(r.FoodId));
            UiUtil.Size(r.Eat.gameObject, 64f, 30f);
            r.Eat.gameObject.SetActive(false);
            return r;
        }
    }
}

using Larder.Core;
using Larder.Core.Model;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Larder.UI
{
    /// <summary>
    /// The side panel and its toggle button, parented to the inventory screen's player block
    /// (InventoryGui.m_player) so they move with it. The panel sits to its right.
    /// </summary>
    internal static class LarderPanel
    {
        private const float Width = 380f;
        private static readonly Goal[] Goals = { Goal.Balanced, Goal.Health, Goal.Stamina, Goal.Eitr };
        private static readonly Color On = new Color(1f, 0.85f, 0.4f, 1f);
        private static readonly Color Off = new Color(0.7f, 0.7f, 0.7f, 1f);

        private static RectTransform _root;
        private static Button _toggle;
        private static readonly Button[] Tabs = new Button[4];
        private static readonly ComboRow[] Rows = new ComboRow[3];
        private static TextMeshProUGUI _totals;
        private static TextMeshProUGUI _cook;
        private static TextMeshProUGUI _status;
        private static bool _hooked;

        public static bool Visible
        {
            get { return _root != null && _root.gameObject.activeSelf && InventoryGui.IsVisible(); }
        }

        internal static ComboRow[] ComboRows
        {
            get { return Rows; }
        }

        internal static TextMeshProUGUI CookText
        {
            get { return _cook; }
        }

        public static void OnShow(InventoryGui gui)
        {
            if (_root == null)
                Build(gui);
            if (_root == null)
                return;
            bool enabled = PluginConfig.Enabled.Value;
            _toggle.gameObject.SetActive(enabled);
            _root.gameObject.SetActive(enabled && PluginConfig.PanelOpen.Value);
            ApplyLayout();
            if (Visible)
                Runtime.Refresh();
        }

        public static void Toggle()
        {
            if (_root == null)
                return;
            PluginConfig.PanelOpen.Value = !PluginConfig.PanelOpen.Value;
            _root.gameObject.SetActive(PluginConfig.PanelOpen.Value);
            if (Visible)
                Runtime.Refresh();
        }

        public static void ApplyLayout()
        {
            if (_root == null)
                return;
            _root.anchoredPosition = new Vector2(12f + PluginConfig.OffsetX.Value, PluginConfig.OffsetY.Value);
            _root.localScale = Vector3.one * PluginConfig.Scale.Value;
        }

        private static void Build(InventoryGui gui)
        {
            RectTransform host = gui.m_player;
            if (host == null)
            {
                LarderPlugin.Log.LogWarning("Larder: no player inventory block to attach to; use the larder console command.");
                return;
            }

            _root = UiUtil.Rect("LarderPanel", host);
            _root.anchorMin = _root.anchorMax = new Vector2(1f, 1f);
            _root.pivot = new Vector2(0f, 1f);
            _root.sizeDelta = new Vector2(Width, 0f);

            var bg = _root.gameObject.AddComponent<Image>();
            Image hostBg = host.GetComponent<Image>();
            if (hostBg != null && hostBg.sprite != null)
            {
                bg.sprite = hostBg.sprite;
                bg.type = hostBg.type;
                bg.color = hostBg.color;
            }
            else
            {
                bg.sprite = UiUtil.White;
                bg.color = new Color(0.1f, 0.09f, 0.08f, 0.9f);
            }

            var layout = _root.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 12, 12);
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            _root.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            RectTransform tabs = UiUtil.Rect("Goals", _root);
            var th = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            th.spacing = 4f;
            th.childControlWidth = true;
            th.childControlHeight = true;
            th.childForceExpandWidth = true;
            for (int i = 0; i < Goals.Length; i++)
            {
                Goal g = Goals[i];
                Tabs[i] = UiUtil.Button(tabs, "Goal" + g, gui.m_craftButton, g.ToString(), () => SetGoal(g));
                UiUtil.Size(Tabs[i].gameObject, 80f, 28f);
            }

            UiUtil.Text(_root, "ComboHeader", 17f, TextAlignmentOptions.Left).text = "<b>Best combo</b>";
            for (int i = 0; i < Rows.Length; i++)
                Rows[i] = ComboRow.Create(_root, gui.m_craftButton, i);
            _totals = UiUtil.Text(_root, "Totals", 15f, TextAlignmentOptions.Left);
            _cook = UiUtil.Text(_root, "Cook", 14f, TextAlignmentOptions.TopLeft);
            _status = UiUtil.Text(_root, "Status", 14f, TextAlignmentOptions.Left);

            _toggle = UiUtil.Button(host, "LarderToggle", gui.m_craftButton, "Larder", Toggle);
            var trt = (RectTransform)_toggle.transform;
            trt.anchorMin = trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(1f, 0f);
            trt.sizeDelta = new Vector2(90f, 30f);
            trt.anchoredPosition = new Vector2(0f, 4f);

            if (!_hooked)
            {
                _hooked = true;
                PluginConfig.Scale.SettingChanged += (s, e) => ApplyLayout();
                PluginConfig.OffsetX.SettingChanged += (s, e) => ApplyLayout();
                PluginConfig.OffsetY.SettingChanged += (s, e) => ApplyLayout();
                PluginConfig.Radius.SettingChanged += (s, e) => Runtime.Refresh();
                PluginConfig.ShowUndiscovered.SettingChanged += (s, e) => Runtime.Refresh();
                PluginConfig.IncludeCartsAndShips.SettingChanged += (s, e) => Runtime.Refresh();
            }
        }

        private static void SetGoal(Goal g)
        {
            GoalStore.Set(Player.m_localPlayer, g);
            Runtime.Refresh();
        }

        public static void Render(PlanView v)
        {
            if (_root == null || v == null)
                return;
            for (int i = 0; i < Tabs.Length; i++)
                UiUtil.SetLabel(Tabs[i], Goals[i].ToString(), Goals[i] == v.Goal ? On : Off);

            if (v.Error != null)
            {
                foreach (ComboRow r in Rows)
                    r.Root.gameObject.SetActive(false);
                _totals.text = "";
                _cook.text = "";
                _status.text = Format.C(Format.Bad, "Larder couldn't read the game: " + v.Error);
                return;
            }

            for (int i = 0; i < Rows.Length; i++)
            {
                ComboRow r = Rows[i];
                bool has = i < v.Slots.Planned.Count;
                r.Root.gameObject.SetActive(has);
                if (!has)
                    continue;
                PlannedSlot slot = v.Slots.Planned[i];
                r.FoodId = slot.Food.Id;
                ItemDrop.ItemData item;
                r.Icon.sprite = FoodCatalog.Items.TryGetValue(slot.Food.Id, out item) ? item.GetIcon() : null;
                r.Text.text = Format.Row(slot, v);
            }

            _totals.text = v.Combo.Count > 0 ? Format.Totals(v) : "";
            _status.text = v.Combo.Count == 0
                ? "No food in your bag or in chests within " + PluginConfig.Radius.Value.ToString("0") + " m."
                : Format.Others(v);
        }
    }
}

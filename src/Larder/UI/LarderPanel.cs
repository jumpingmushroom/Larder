using System;
using Larder.Core;
using Larder.Core.Model;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Larder.UI
{
    /// <summary>
    /// The side panel and its toggle button. The toggle is parented to the inventory screen's player
    /// block (InventoryGui.m_player) above its top-right corner; the panel hangs off the inventory
    /// root (InventoryGui.m_inventoryRoot) and is positioned by <see cref="Placement"/>.
    /// </summary>
    internal static class LarderPanel
    {
        private const float Width = 380f;
        private const float MinBgAlpha = 0.94f;
        /// <summary>Seconds after Show during which every Tick re-applies the layout: the chest block is
        /// activated in InventoryGui.Update after Show, and the screen may still be animating in.</summary>
        private const float SettleTime = 0.5f;
        private static readonly Goal[] Goals = { Goal.Balanced, Goal.Health, Goal.Stamina, Goal.Eitr };
        private static readonly Color On = new Color(1f, 0.85f, 0.4f, 1f);
        private static readonly Color Off = new Color(0.7f, 0.7f, 0.7f, 1f);

        private static RectTransform _root;
        private static Button _toggle;
        private static readonly Button[] Tabs = new Button[4];
        private static readonly ComboRow[] Rows = new ComboRow[3];
        private static Button _eatAll;
        private static TextMeshProUGUI _eatAllLabel;
        private static TextMeshProUGUI _totals;
        private static TextMeshProUGUI _cook;
        private static TextMeshProUGUI _status;
        private static bool _hooked;
        private static InventoryGui _gui;
        private static float _settleUntil;

        public static bool Visible
        {
            get { return _root != null && _root.gameObject.activeSelf && InventoryGui.IsVisible(); }
        }

        public static void OnShow(InventoryGui gui)
        {
            if (_root == null)
                Build(gui);
            if (_root == null)
                return;
            _settleUntil = Time.unscaledTime + SettleTime;
            ApplyOpen();
        }

        /// <summary>The PanelOpen change this triggers is applied by ApplyOpen (hooked in BuildInternal).</summary>
        public static void Toggle()
        {
            if (_root == null)
                return;
            PluginConfig.PanelOpen.Value = !PluginConfig.PanelOpen.Value;
        }

        /// <summary>Shows or hides the toggle and panel from Enabled and PanelOpen. Runs on Show and
        /// whenever either setting changes, including from a config manager while the inventory is open.</summary>
        private static void ApplyOpen()
        {
            if (_root == null)
                return;
            bool enabled = PluginConfig.Enabled.Value;
            if (_toggle != null)
                _toggle.gameObject.SetActive(enabled);
            _root.gameObject.SetActive(enabled && PluginConfig.PanelOpen.Value);
            ApplyLayout();
            if (Visible)
                Runtime.Refresh();
        }

        /// <summary>Called every Tick while the inventory is visible; re-lays out only while settling.</summary>
        public static void Settle()
        {
            if (Visible && Time.unscaledTime < _settleUntil)
                ApplyLayout();
        }

        /// <summary>Scale and position (PLAN §2.3). Runs on Show, after every Refresh and on layout
        /// setting changes; never throws.</summary>
        public static void ApplyLayout()
        {
            if (_root == null)
                return;
            try
            {
                _root.localScale = Vector3.one * PluginConfig.Scale.Value;
                if (Visible && _gui != null)
                    Placement.Apply(_root, _gui, _toggle != null ? _toggle.transform : null);
            }
            catch (Exception e)
            {
                LarderPlugin.WarnOnce("Larder: panel layout failed", e);
            }
        }

        /// <summary>Best-effort: sets the status text if the panel exists, and never throws itself.</summary>
        public static void ShowError(string message)
        {
            try
            {
                if (_status != null)
                    _status.text = Format.C(Format.Bad, message);
            }
            catch (Exception)
            {
                // Deliberately swallowed: this is the last-resort error path.
            }
        }

        private static void Build(InventoryGui gui)
        {
            RectTransform host = gui.m_player;
            if (host == null)
            {
                LarderPlugin.Log.LogWarning("Larder: no player inventory block to attach to; use the larder console command.");
                return;
            }

            try
            {
                BuildInternal(gui, host);
                _gui = gui;
            }
            catch (Exception e)
            {
                if (_root != null)
                {
                    UnityEngine.Object.Destroy(_root.gameObject);
                    _root = null;
                }
                if (_toggle != null)
                {
                    UnityEngine.Object.Destroy(_toggle.gameObject);
                    _toggle = null;
                }
                LarderPlugin.WarnOnce("Larder: panel build failed", e);
            }
        }

        private static void BuildInternal(InventoryGui gui, RectTransform host)
        {
            // Under the inventory root rather than the player block, so it draws above the blocks
            // beside the inventory and doesn't inherit the player block's layout or scale.
            var inventoryRoot = gui.m_inventoryRoot as RectTransform;
            _root = UiUtil.Rect("LarderPanel", inventoryRoot != null ? inventoryRoot : host);
            _root.SetAsLastSibling();
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
            _root.pivot = new Vector2(0f, 1f);
            _root.sizeDelta = new Vector2(Width, 0f);

            var bg = _root.gameObject.AddComponent<Image>();
            Image hostBg = host.GetComponent<Image>();
            if (hostBg != null && hostBg.sprite != null)
            {
                bg.sprite = hostBg.sprite;
                bg.type = hostBg.type;
                Color c = hostBg.color;
                c.a = Mathf.Max(c.a, MinBgAlpha); // Opaque even where the host block is translucent.
                bg.color = c;
            }
            else
            {
                bg.sprite = UiUtil.White;
                bg.color = new Color(0.1f, 0.09f, 0.08f, MinBgAlpha);
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

            RectTransform comboHeader = UiUtil.Rect("ComboHeader", _root);
            var chLayout = comboHeader.gameObject.AddComponent<HorizontalLayoutGroup>();
            chLayout.spacing = 8f;
            chLayout.childAlignment = TextAnchor.MiddleLeft;
            chLayout.childControlWidth = true;
            chLayout.childControlHeight = true;
            chLayout.childForceExpandWidth = false;
            chLayout.childForceExpandHeight = false;
            // Fixed height so the rows below don't shift up/down as the Eat N button shows or hides.
            var chSize = comboHeader.gameObject.AddComponent<LayoutElement>();
            chSize.minHeight = chSize.preferredHeight = 28f;

            TextMeshProUGUI comboHeaderText = UiUtil.Text(comboHeader, "ComboHeaderText", 17f, TextAlignmentOptions.Left);
            comboHeaderText.text = "<b>Best combo</b>";
            comboHeaderText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            _eatAll = UiUtil.Button(comboHeader, "EatAll", gui.m_craftButton, "Eat N", Eater.EatAll);
            UiUtil.Size(_eatAll.gameObject, 72f, 28f);
            _eatAllLabel = _eatAll.GetComponentInChildren<TextMeshProUGUI>(true);
            _eatAll.gameObject.SetActive(false);

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
                PluginConfig.Enabled.SettingChanged += (s, e) => ApplyOpen();
                PluginConfig.PanelOpen.SettingChanged += (s, e) => ApplyOpen();
                PluginConfig.Scale.SettingChanged += (s, e) => ApplyLayout();
                PluginConfig.OffsetX.SettingChanged += (s, e) => ApplyLayout();
                PluginConfig.OffsetY.SettingChanged += (s, e) => ApplyLayout();
                PluginConfig.Placement.SettingChanged += (s, e) => ApplyLayout();
                PluginConfig.Radius.SettingChanged += (s, e) => Runtime.RequestRefresh();
                PluginConfig.ShowUndiscovered.SettingChanged += (s, e) => Runtime.RequestRefresh();
                PluginConfig.IncludeCartsAndShips.SettingChanged += (s, e) => Runtime.RequestRefresh();
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
                _eatAll.gameObject.SetActive(false);
                _totals.text = "";
                _cook.text = "";
                _status.text = Format.C(Format.Bad, "Larder couldn't read the game: " + v.Error);
                return;
            }

            int eatN = EatOrder.Next(v.Combo, v.Active, v.BagFoods, Game.m_foodRate).Count;
            _eatAll.gameObject.SetActive(eatN >= 2);
            if (eatN >= 2)
                _eatAllLabel.text = "Eat " + eatN;

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
                r.Icon.sprite = FoodCatalog.Items.TryGetValue(slot.Food.Id, out item) ? IconOrNull(item) : null;
                r.Text.text = Format.Row(slot, v);
                // RefreshFirst stays hidden: eating it now would push a planned food out.
                bool edible = slot.State == SlotState.EatNow || slot.State == SlotState.RefreshNow;
                r.Eat.gameObject.SetActive(edible && Eater.CanEatNow(Player.m_localPlayer, slot.Food.Id));
            }

            _totals.text = v.Combo.Count > 0 ? Format.Totals(v) : "";
            _cook.text = Format.Cook(v);
            _status.text = v.Combo.Count == 0
                ? "No food in your bag or in chests within " + PluginConfig.Radius.Value.ToString("0") + " m."
                : Format.Others(v);
        }

        /// <summary>ItemData.GetIcon() indexes m_shared.m_icons[m_variant] with no bounds check;
        /// a modded food with an empty icon array (or a bad variant) would throw. Null reads as no icon.</summary>
        private static Sprite IconOrNull(ItemDrop.ItemData item)
        {
            ItemDrop.ItemData.SharedData s = item != null ? item.m_shared : null;
            if (s == null || s.m_icons == null || s.m_icons.Length == 0)
                return null;
            int variant = item.m_variant;
            if (variant < 0 || variant >= s.m_icons.Length)
                variant = 0;
            return s.m_icons[variant];
        }
    }
}

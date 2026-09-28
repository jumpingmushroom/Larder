using System;
using Larder.UI;
using UnityEngine;

namespace Larder.Core
{
    /// <summary>Re-plans once a second while the panel is visible; nothing runs while it is closed.</summary>
    internal static class Runtime
    {
        private static float _next;

        public static PlanView Last { get; private set; }

        public static void Tick()
        {
            try
            {
                if (!PluginConfig.Enabled.Value || !InventoryGui.IsVisible())
                    return;
                if (PluginConfig.ToggleKey.Value.IsDown())
                    LarderPanel.Toggle();
                LarderPanel.Settle();
                if (!LarderPanel.Visible || Time.unscaledTime < _next)
                    return;
                Refresh();
            }
            catch (Exception e)
            {
                // Tick runs every frame; a failure here must not throw every frame.
                LarderPlugin.WarnOnce("Larder: tick failed", e);
            }
        }

        /// <summary>Requests a refresh on the next Tick instead of re-planning immediately, so
        /// dragging a slider config value doesn't re-plan on every value change.</summary>
        public static void RequestRefresh()
        {
            _next = 0f;
        }

        public static void Refresh()
        {
            _next = Time.unscaledTime + 1f;
            Player p = Player.m_localPlayer;
            if (p == null || !LarderPanel.Visible)
                return;
            try
            {
                Last = Planner.Compute(p, GoalStore.Get(p));
                LarderPanel.Render(Last);
                LarderPanel.ApplyLayout(); // Other mods may have moved their UI since the last refresh.
            }
            catch (Exception e)
            {
                LarderPlugin.WarnOnce("Larder: panel render failed", e);
                LarderPanel.ShowError("Larder: internal error, see log.");
            }
        }
    }
}

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
            if (!PluginConfig.Enabled.Value || !InventoryGui.IsVisible())
                return;
            if (PluginConfig.ToggleKey.Value.IsDown())
                LarderPanel.Toggle();
            if (!LarderPanel.Visible || Time.unscaledTime < _next)
                return;
            Refresh();
        }

        public static void Refresh()
        {
            _next = Time.unscaledTime + 1f;
            Player p = Player.m_localPlayer;
            if (p == null || !LarderPanel.Visible)
                return;
            Last = Planner.Compute(p, GoalStore.Get(p));
            LarderPanel.Render(Last);
        }
    }
}

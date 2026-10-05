using BepInEx.Configuration;

namespace Larder
{
    public enum PanelPlacement
    {
        Auto,
        Below,
        Manual
    }

    public static class PluginConfig
    {
        // General
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<float> Radius;
        public static ConfigEntry<bool> ShowUndiscovered;
        public static ConfigEntry<bool> IncludeCartsAndShips;

        // UI
        public static ConfigEntry<bool> PanelOpen;
        public static ConfigEntry<PanelPlacement> Placement;
        public static ConfigEntry<KeyboardShortcut> ToggleKey;
        public static ConfigEntry<float> Scale;
        public static ConfigEntry<float> OffsetX;
        public static ConfigEntry<float> OffsetY;

        // Logging
        public static ConfigEntry<bool> Verbose;

        private static ConfigurationManagerAttributes Attr(int order, bool advanced = false)
        {
            return new ConfigurationManagerAttributes { Order = order, IsAdvanced = advanced };
        }

        public static void Bind(ConfigFile cfg)
        {
            Enabled = cfg.Bind("General", "Enabled", true,
                new ConfigDescription("Master switch. Off hides the Larder button and panel.", null, Attr(100)));

            Radius = cfg.Bind("General", "Radius", 20f,
                new ConfigDescription("Metres to search for chests, placed feasts and cooking stations.",
                    new AcceptableValueRange<float>(5f, 50f), Attr(95)));

            ShowUndiscovered = cfg.Bind("General", "ShowUndiscovered", false,
                new ConfigDescription(
                    "Allow cook suggestions for recipes you haven't discovered yet, and name ingredients " +
                    "you haven't seen. Food you own is always shown.",
                    null, Attr(90)));

            IncludeCartsAndShips = cfg.Bind("General", "IncludeCartsAndShips", true,
                new ConfigDescription("Count carts and ship holds within range as containers.", null, Attr(85)));

            PanelOpen = cfg.Bind("UI", "PanelOpen", true,
                new ConfigDescription("Whether the panel is open. The Larder button toggles this.", null, Attr(80)));

            Placement = cfg.Bind("UI", "Placement", PanelPlacement.Auto,
                new ConfigDescription(
                    "Where the panel goes. Auto: right of the inventory and anything other mods put beside it. " +
                    "Below: under the chest window (or the inventory when no chest is open). " +
                    "Manual: at OffsetX/OffsetY from the top-left of the screen (negative OffsetY moves down). " +
                    "OffsetX/OffsetY nudge the Auto and Below positions too.",
                    null, Attr(79)));

            ToggleKey = cfg.Bind("UI", "ToggleKey", KeyboardShortcut.Empty,
                new ConfigDescription("Optional key that toggles the panel while the inventory is open.", null, Attr(78)));

            Scale = cfg.Bind("UI", "Scale", 1f,
                new ConfigDescription("Size of the panel.", new AcceptableValueRange<float>(0.6f, 1.6f), Attr(76)));

            OffsetX = cfg.Bind("UI", "OffsetX", 0f,
                new ConfigDescription("Horizontal nudge in pixels (positive is right).", new AcceptableValueRange<float>(-1500f, 1500f), Attr(74)));

            OffsetY = cfg.Bind("UI", "OffsetY", 0f,
                new ConfigDescription("Vertical nudge in pixels (positive is up).", new AcceptableValueRange<float>(-1000f, 1000f), Attr(72)));

            Verbose = cfg.Bind("Logging", "Verbose", false,
                new ConfigDescription("Log plan timing to the BepInEx log.", null, Attr(5, advanced: true)));
        }
    }
}

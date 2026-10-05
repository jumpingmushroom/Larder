using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace Larder
{
    /// <summary>
    /// A food planner in the inventory screen. Reads food, recipe, container and station data
    /// from the running game and never writes game state; eating goes through the game's own
    /// use-item path. Purely client-side.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("valheim.exe")]
    [BepInProcess("valheim.x86_64")]
    public sealed class LarderPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.jumpingmushroom.larder";
        public const string PluginName = "Larder";
        public const string PluginVersion = "0.2.1";

        internal static ManualLogSource Log;

        private static readonly HashSet<string> Warned = new HashSet<string>();
        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            PluginConfig.Bind(base.Config);
            Core.ConsoleCommands.Register();

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(LarderPlugin).Assembly);

            Logger.LogInfo(PluginName + " " + PluginVersion + " loaded.");
        }

        private void Update()
        {
            Core.Runtime.Tick();
        }

        private void OnDestroy()
        {
            if (_harmony != null)
                _harmony.UnpatchSelf();
        }

        /// <summary>Log an exception once per key, so a broken scan can't flood the log every second.</summary>
        internal static void WarnOnce(string key, Exception e)
        {
            if (Warned.Add(key))
                Log.LogWarning(key + ": " + e);
        }
    }
}

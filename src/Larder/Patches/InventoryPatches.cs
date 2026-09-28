using System;
using HarmonyLib;
using Larder.UI;

namespace Larder.Patches
{
    /// <summary>Postfix only: vanilla Show always runs; our panel follows it.</summary>
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Show))]
    internal static class InventoryShowPatch
    {
        private static void Postfix(InventoryGui __instance)
        {
            try
            {
                LarderPanel.OnShow(__instance);
            }
            catch (Exception e)
            {
                LarderPlugin.WarnOnce("Larder: panel failed to open", e);
            }
        }
    }
}

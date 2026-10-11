using System;
using HarmonyLib;

namespace RadarScanner
{
    /// <summary>Hooks the scanner so the radar overlay is added whenever the game builds its UI.</summary>
    [HarmonyPatch(typeof(MinimapControl))]
    internal static class MinimapPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch("Start")]
        private static void Start_Postfix(MinimapControl __instance)
        {
            try
            {
                RadarOverlay.Create(__instance);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Failed to create the radar overlay: " + e);
            }
        }
    }
}

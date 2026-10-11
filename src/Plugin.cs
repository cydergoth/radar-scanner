using System;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace RadarScanner
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "starvalor.radarscanner";
        public const string Name = "Radar Scanner";
        public const string Version = "1.0.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<float> SweepSeconds;
        internal static ConfigEntry<bool> Clockwise;
        internal static ConfigEntry<Color> PhosphorColor;
        internal static ConfigEntry<float> TrailDegrees;
        internal static ConfigEntry<float> FadeStrength;
        internal static ConfigEntry<float> ScreenTint;
        internal static ConfigEntry<bool> ShowGrid;
        internal static ConfigEntry<bool> RippleShips;
        internal static ConfigEntry<bool> RippleStations;
        internal static ConfigEntry<bool> RippleLoot;
        internal static ConfigEntry<bool> RippleAsteroids;
        internal static ConfigEntry<float> RippleSeconds;
        internal static ConfigEntry<float> RippleSize;
        internal static ConfigEntry<bool> RelationColors;

        private void Awake()
        {
            Log = Logger;
            Enabled = Config.Bind("General", "Enabled", true,
                "Show the radar overlay on the scanner. Turn off to get the game's plain scanner back.");
            SweepSeconds = Config.Bind("General", "SweepSeconds", 4f,
                "How long the scan line takes to go all the way round.");
            Clockwise = Config.Bind("General", "Clockwise", true,
                "Which way the scan line turns.");
            PhosphorColor = Config.Bind("General", "PhosphorColor", new Color(0.25f, 1f, 0.4f),
                "Colour of the scan line, its afterglow, the grid and the ripples (RRGGBBAA).");
            TrailDegrees = Config.Bind("General", "TrailDegrees", 120f,
                "How far the afterglow trails behind the scan line, in degrees.");
            FadeStrength = Config.Bind("General", "FadeStrength", 0.55f,
                "How much the scanner dims between passes of the scan line. 0 = no fading, 1 = fades to black.");
            ScreenTint = Config.Bind("General", "ScreenTint", 0.3f,
                "Strength of the dark phosphor tint over the scanner. 0 = none.");
            ShowGrid = Config.Bind("General", "ShowGrid", true,
                "Draw range rings, a crosshair and bearing ticks on the scanner.");

            RippleShips = Config.Bind("Ripples", "Ships", true,
                "Ripple around detected ships when the scan line passes them.");
            RippleStations = Config.Bind("Ripples", "Stations", true,
                "Ripple around discovered stations and jumpgates when the scan line passes them.");
            RippleLoot = Config.Bind("Ripples", "Loot", true,
                "Ripple around detected loot, wrecks and containers when the scan line passes them.");
            RippleAsteroids = Config.Bind("Ripples", "Asteroids", true,
                "Ripple (smaller and fainter) around detected ordinary asteroids. Huge asteroids never ripple.");
            RippleSeconds = Config.Bind("Ripples", "RippleSeconds", 1.4f,
                "How long a ripple takes to spread and fade out.");
            RippleSize = Config.Bind("Ripples", "RippleSize", 26f,
                "How wide a ship's ripple gets. The scanner is 256 wide.");
            RelationColors = Config.Bind("Ripples", "RelationColors", false,
                "Colour the ripples of ships and stations by their relation to you (hostile, friendly...) instead of the phosphor colour.");

            try
            {
                // Bind the game's non-public members now, so a game update that renames one
                // disables the mod at startup instead of failing while the scanner is drawn.
                RuntimeHelpers.RunClassConstructor(typeof(GameAccess).TypeHandle);
            }
            catch (Exception e)
            {
                Log.LogError($"{Name} is not compatible with this version of the game and is disabled: {e}");
                return;
            }

            // Lets a config change made while the game runs (for example with Configuration Manager) show at once.
            Config.SettingChanged += (sender, args) => RadarOverlay.Instance?.ApplyConfig();

            var harmony = new Harmony(Guid);
            harmony.PatchAll(typeof(MinimapPatches));
            Log.LogInfo($"{Name} {Version} loaded");
        }
    }
}

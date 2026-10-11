using HarmonyLib;
using UnityEngine;

namespace RadarScanner
{
    /// <summary>Non-public game members the mod needs, bound once through Harmony's AccessTools.</summary>
    internal static class GameAccess
    {
        /// <summary>The camera that renders the scanner, centred on the player.</summary>
        public static readonly AccessTools.FieldRef<MinimapControl, Camera> MinimapCam =
            AccessTools.FieldRefAccess<MinimapControl, Camera>("minimapCam");

        // The game switches each object's scanner icon on and off as the player detects or loses it,
        // so an active icon means "detected" by the game's own rules.
        public static readonly AccessTools.FieldRef<SpaceShip, GameObject> ShipIcon =
            AccessTools.FieldRefAccess<SpaceShip, GameObject>("minimapIcon");

        public static readonly AccessTools.FieldRef<Collectible, GameObject> LootIcon =
            AccessTools.FieldRefAccess<Collectible, GameObject>("minimapIcon");

        public static readonly AccessTools.FieldRef<Asteroid, GameObject> AsteroidIcon =
            AccessTools.FieldRefAccess<Asteroid, GameObject>("minimapIcon");

        public static readonly AccessTools.FieldRef<DockingControl, GameObject> StationIcon =
            AccessTools.FieldRefAccess<DockingControl, GameObject>("minimapIcon");
    }
}

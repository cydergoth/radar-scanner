using System.Collections.Generic;
using UnityEngine;

namespace RadarScanner
{
    internal enum ContactKind
    {
        Ship,
        Station,
        Loot,
        Asteroid,
    }

    /// <summary>Something in the sector that can ripple on the scanner.</summary>
    internal sealed class Contact
    {
        public ContactKind Kind;
        public int Id;
        public Transform Transform;
        public SpaceShip Ship;
        public AIControl AI;
        public DockingControl Docking;
        public Collectible Loot;
        public Asteroid Asteroid;
        /// <summary>A child of one of the groups that isn't a contact, such as a visual effect. Kept so it is only looked at once.</summary>
        public bool Ignored;

        /// <summary>The scan that last saw this object in the scene.</summary>
        public int Seen;
        /// <summary>Degrees since the scan line last passed, or -1 while it isn't on the scanner.</summary>
        public float Phase = -1f;
        public float LastRipple = float.NegativeInfinity;
        /// <summary>Position on the scanner, from -1 to 1 across the scanner image.</summary>
        public Vector2 MapPos;
        public bool OnScope;

        /// <summary>True while the game itself shows this object on the scanner.</summary>
        public bool Detected()
        {
            if (Ignored || Transform == null)
            {
                return false;
            }
            switch (Kind)
            {
                case ContactKind.Ship:
                    return IsShown(GameAccess.ShipIcon(Ship));
                case ContactKind.Loot:
                    return IsShown(GameAccess.LootIcon(Loot));
                case ContactKind.Asteroid:
                    return IsShown(GameAccess.AsteroidIcon(Asteroid));
                default:
                    if (Docking.station == null || !Docking.station.discovered)
                    {
                        return false;
                    }
                    // Jumpgates and hideouts have a label on the scanner but no icon.
                    GameObject icon = GameAccess.StationIcon(Docking);
                    return icon == null || icon.activeInHierarchy;
            }
        }

        private static bool IsShown(GameObject icon)
        {
            return icon != null && icon.activeInHierarchy;
        }

        public Color RelationColor(Color fallback)
        {
            if (Kind == ContactKind.Ship && AI != null)
            {
                return AI.GetRelationColor();
            }
            if (Kind == ContactKind.Station && Docking.station != null)
            {
                return Docking.station.GetRelationColor(out _);
            }
            return fallback;
        }
    }

    /// <summary>
    /// Keeps the list of objects the player currently detects. The game parents everything it
    /// spawns in a sector under a few group objects, so this walks those groups instead of
    /// searching the whole scene.
    /// </summary>
    internal sealed class RadarContacts
    {
        private readonly Dictionary<int, Contact> byId = new Dictionary<int, Contact>();
        private readonly List<Contact> all = new List<Contact>();
        private Transform ships;
        private Transform stations;
        private Transform loot;
        private Transform asteroids;
        private int serial;

        /// <summary>The contacts the game showed on the scanner at the last <see cref="Refresh"/>.</summary>
        public readonly List<Contact> Detected = new List<Contact>();

        public void Refresh()
        {
            if (ships == null)
            {
                GameObject groups = GameObject.FindGameObjectWithTag("TempObjects");
                if (groups == null)
                {
                    return;
                }
                ships = groups.transform.Find("Spaceships");
                stations = groups.transform.Find("Stations");
                loot = groups.transform.Find("Collectibles");
                asteroids = groups.transform.Find("Asteroids");
            }

            serial++;
            Scan(ships, ContactKind.Ship, Plugin.RippleShips.Value);
            Scan(stations, ContactKind.Station, Plugin.RippleStations.Value);
            Scan(loot, ContactKind.Loot, Plugin.RippleLoot.Value);
            Scan(asteroids, ContactKind.Asteroid, Plugin.RippleAsteroids.Value);

            Detected.Clear();
            for (int i = all.Count - 1; i >= 0; i--)
            {
                Contact contact = all[i];
                if (contact.Seen != serial)
                {
                    byId.Remove(contact.Id);
                    all.RemoveAt(i);
                }
                else if (contact.Detected())
                {
                    Detected.Add(contact);
                }
                else
                {
                    contact.Phase = -1f;
                    contact.OnScope = false;
                }
            }
        }

        private void Scan(Transform group, ContactKind kind, bool wanted)
        {
            if (group == null || !wanted)
            {
                return;
            }
            for (int i = 0; i < group.childCount; i++)
            {
                Transform child = group.GetChild(i);
                int id = child.GetInstanceID();
                if (!byId.TryGetValue(id, out Contact contact))
                {
                    contact = Identify(child, kind);
                    contact.Id = id;
                    byId[id] = contact;
                    all.Add(contact);
                }
                contact.Seen = serial;
            }
        }

        private static Contact Identify(Transform child, ContactKind kind)
        {
            var contact = new Contact { Kind = kind, Transform = child };
            switch (kind)
            {
                case ContactKind.Ship:
                    contact.Ship = child.GetComponent<SpaceShip>();
                    // The player is the centre of the scanner, not a contact on it.
                    contact.Ignored = contact.Ship == null || child.CompareTag("Player");
                    contact.AI = child.GetComponent<AIControl>();
                    break;
                case ContactKind.Station:
                    contact.Docking = child.GetComponentInChildren<DockingControl>(true);
                    contact.Ignored = contact.Docking == null;
                    break;
                case ContactKind.Loot:
                    contact.Loot = child.GetComponent<Collectible>();
                    contact.Ignored = contact.Loot == null;
                    break;
                default:
                    contact.Asteroid = child.GetComponent<Asteroid>();
                    contact.Ignored = contact.Asteroid == null;
                    break;
            }
            return contact;
        }
    }
}

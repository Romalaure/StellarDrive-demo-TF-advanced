using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Core.Services;
using Core.Values;
using Managers.Client.Utils;
using Planets.Interface.Model;
using Planets.Interface.Services;
using Saving.Services.Converters;
using Saving.Services.Model.State;
using Ships;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.State;
using Ships.Services;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;
using WorldObjects.Services;
using WorldTracking.Interface.Model;
using WorldTracking.Interface.Model.Spaces;
using WorldTracking.Interface.Model.Spaces.Common;
using WorldTracking.Services;
using WorldTracking.Spaces;

namespace StellarDriveDemoTFAdvanced.Worlds
{
    /// <summary>
    /// Abandoned space stations. Each is a real ship built from the game's hull and parts (so it
    /// can be entered, salvaged with the recycler, looted and docked against), with a named
    /// beacon: it shows up as a target on the star map and the targeting computer, and warp
    /// jumps can aim at it. Created once per world by the host, then saved with the world.
    /// Its entrance is an open docking door (dock a ship with a docking door against it, or float
    /// in); inside, lights, consoles, bunks, shelves and chests; outside, trusses, solar panels
    /// and blinking beacons. Stations from an older version of the mod (no docking door) are
    /// replaced when the world loads (<see cref="StationVersion"/>, tfa_stations.txt).
    /// </summary>
    internal static class AbandonedStations
    {
        internal sealed class StationSpec
        {
            public string Name;
            public ulong AnchorPlanet;
            /// <summary>From the anchor planet's center (or the black hole), in the star system's axes.</summary>
            public Vector3 Offset;
            public bool NearBlackHole;
            public int Seed;
            public Action<Blueprint> Build;
            /// <summary>Hull paint: outside, the band around the modules, inside.</summary>
            public Color32 Outside = new Color32(205, 208, 212, 255);
            public Color32 Band = new Color32(225, 120, 40, 255);
            public Color32 Inside = new Color32(176, 172, 164, 255);
            /// <summary>Share of hull faces missing (holes, torn plating).</summary>
            public float Breaches = 1f;
            /// <summary>Dark matter left in a tank aboard (units, 0 for none).</summary>
            public int DarkMatter;
            /// <summary>Rare finds in its chests: (item, chance).</summary>
            public (uint Item, int Min, int Max, double Chance)[] Rare = new (uint, int, int, double)[0];
            /// <summary>A pirate base (with the Arsenal mod): armed, hostile, see <see cref="Combat.Pirates"/>.</summary>
            public bool Pirate;
            /// <summary>The stations' build this one last changed in: worlds saved before it get it rebuilt.</summary>
            public int Version = 4;
            /// <summary>The part dressing its hull (prow, engines, plating, torn edges...), 0 for none; see <see cref="WreckDecor"/>.</summary>
            public ushort Decor;
            /// <summary>A trading ship: merchant terminals in its first module (the trading hall) and in its cargo hold.</summary>
            public bool Merchant;
        }

        public static readonly StationSpec[] All =
        {
            new StationSpec
            {
                Name = "Station Kepler (abandonnée)", AnchorPlanet = 104, Offset = new Vector3(1.6e7f, 3e6f, -9e6f), Seed = 7,
                Band = new Color32(225, 120, 40, 255),
                Rare = new[] { (Items.ModItems.QuantumCore, 1, 1, 0.2), (Items.ModItems.NexoranAlloy, 2, 5, 0.35), (Items.ModItems.Titanium, 3, 8, 0.4), (Items.ModItems.Cryonite, 2, 4, 0.2) },
                Build = b =>
                {
                    b.Module(-3, 0, -3, 3, 3, 3);          // core
                    b.Module(-1, 0, 4, 1, 2, 11);           // arms
                    b.Module(-1, 0, -11, 1, 2, -4);
                    b.Module(4, 0, -1, 11, 2, 1);
                    b.Module(-11, 0, -1, -4, 2, 1);
                    b.Module(-2, 0, 12, 2, 3, 16);          // end modules
                    b.Module(-2, 0, -17, 2, 3, -12);
                    b.Module(12, 0, -2, 16, 3, 2);
                    b.Module(-17, 0, -2, -12, 3, 2);
                    b.Module(-1, 4, -1, 1, 8, 1);           // tower
                    b.Entrance(0, 0, 16, 2);
                }
            },
            new StationSpec
            {
                // A merchant freighter at anchor, kept up by its trader: lit, whole, two counters.
                // Its bow, sensors, containers, solar wings and engines are its dressing (Blender)
                Name = "Comptoir Hermès (marchand)", AnchorPlanet = 107, Offset = new Vector3(-9e6f, 2e6f, -1.1e7f), Seed = 131, Breaches = 0f, Merchant = true,
                Band = new Color32(230, 180, 40, 255), Outside = new Color32(70, 80, 100, 255), Inside = new Color32(190, 180, 160, 255),
                Rare = new[] { (Items.ModItems.GoldBar, 1, 2, 0.3), (Items.ModItems.Titanium, 3, 8, 0.4) },
                Decor = WreckDecor.HermesId, Version = 6,
                Build = b =>
                {
                    // Bow to stern: the trading hall first (its counters), the glazed bridge and its
                    // corridor forward, the cargo hold and the engine room aft; the docking door on
                    // the hall's starboard side. Kept small: every hull face is a part to sync
                    b.Module(-3, 0, 2, 3, 3, 9, 0.3f);
                    b.Module(-1, 0, 10, 1, 2, 12);
                    b.Module(-2, 0, 13, 2, 2, 16, 0.7f);
                    b.Module(-1, 0, -1, 1, 2, 1);
                    b.Module(-3, 0, -10, 3, 2, -2);
                    b.Module(-2, 0, -13, 2, 2, -11);
                    b.Entrance(3, 0, 5, 0);
                    b.Cargo(-2, -9, 2, -4, 0, 0.15);
                }
            },
            new StationSpec
            {
                Name = "Avant-poste Orion (abandonné)", AnchorPlanet = 101, Offset = new Vector3(-1.2e7f, 5e6f, 1.3e7f), Seed = 23,
                Band = new Color32(50, 120, 220, 255), Outside = new Color32(215, 205, 185, 255),
                Rare = new[] { (Items.ModItems.QuantumCore, 1, 1, 0.25), (Items.ModItems.NexoranAlloy, 2, 5, 0.3), (Items.ModItems.Quartz, 3, 8, 0.45), (Items.ModItems.Artifact, 1, 1, 0.2) },
                Build = b =>
                {
                    b.Module(-2, 0, -4, 2, 3, 4);
                    b.Module(-1, 0, 5, 1, 2, 10);
                    b.Module(-3, 0, 11, 3, 4, 15);
                    b.Module(3, 0, -1, 9, 2, 1);
                    b.Module(10, 0, -3, 13, 5, 3);
                    b.Entrance(13, 0, 0, 0);
                }
            },
            new StationSpec
            {
                Name = "Sonde Hadès (trou noir)", NearBlackHole = true, Offset = new Vector3(0f, 2.4e5f, 9.0e5f), Seed = 61,
                Band = new Color32(200, 40, 40, 255), Outside = new Color32(70, 72, 78, 255), Inside = new Color32(120, 118, 125, 255),
                Rare = new[] { (Items.ModItems.ExoticMatter, 1, 3, 0.8), (Items.ModItems.QuantumCore, 1, 1, 0.4), (Items.ModItems.Artifact, 1, 1, 0.25) },
                Decor = WreckDecor.HadesId,
                Build = b =>
                {
                    // A research probe: a core lab, a glazed observation deck toward the bow, two
                    // arms to the side labs, an instrument spire with an observatory; outside, a
                    // gravimetric ring, a big dish, a sensor boom and solar wings (the decor)
                    b.Module(-3, 0, -3, 3, 3, 3);
                    b.Module(-2, 0, 4, 2, 2, 9, 0.75f);
                    b.Module(4, 0, -1, 8, 1, 1);
                    b.Module(-8, 0, -1, -4, 1, 1);
                    b.Module(9, 0, -3, 13, 3, 3);
                    b.Module(-13, 0, -3, -9, 3, 3);
                    b.Module(-1, 4, -1, 1, 9, 1);
                    b.Module(-3, 10, -3, 3, 11, 3, 0.6f);
                    b.Entrance(0, 0, -3, 3);
                    b.Engine(-2, 1, -3);
                    b.Engine(2, 1, -3);
                }
            },

            // ---- Wrecks drifting in the Erebos galaxy: torn ships with their cargo and dark matter

            new StationSpec
            {
                Name = "Épave du Vagabond", AnchorPlanet = 112, Offset = new Vector3(6e6f, 1.5e6f, -4e6f), Seed = 101, Breaches = 4f, DarkMatter = 12,
                Band = new Color32(40, 160, 150, 255), Outside = new Color32(120, 118, 112, 255), Inside = new Color32(90, 88, 84, 255),
                Rare = new[] { (Items.ModItems.Meteorite, 2, 6, 0.6), (Items.ModItems.QuantumCore, 1, 1, 0.25), (Items.ModItems.NexoranAlloy, 2, 4, 0.35) },
                Decor = WreckDecor.VagabondId,
                Build = b =>
                {
                    // A heavy freighter: a long cargo hold with bulkheads, side pods, the crew deck
                    // and the glazed cockpit forward, a bridge tower on the hold; its engine section
                    // broke off behind it and drifts a few meters away, the hold's stern torn open
                    b.Module(-4, 0, -18, 4, 3, 20);
                    b.Module(-3, 0, 21, 3, 2, 28);
                    b.Module(-2, 0, 29, 2, 2, 32, 0.7f);
                    b.Module(-2, 4, 12, 2, 5, 17);
                    b.Module(-3, 6, 12, 3, 7, 17, 0.65f);
                    b.Module(5, 0, -14, 8, 2, -4);
                    b.Module(-8, 0, -14, -5, 2, -4);
                    b.Module(-4, 0, -28, 4, 3, -22);
                    b.Bulkhead(-10, -4, 4, 0, 3, -1);
                    b.Bulkhead(0, -4, 4, 0, 3, -1);
                    b.Bulkhead(10, -4, 4, 0, 3, -1);
                    b.Bulkhead(20, -3, 3, 0, 2, -1);
                    b.Rupture(-3, 1, -18, 3, 3, -18);
                    b.Rupture(-2, 1, -22, 2, 2, -22);
                    b.Rupture(-4, 1, -8, -4, 3, -5);
                    b.Entrance(4, 0, 6, 0);
                    b.Cargo(-3, -17, 3, -12, 0, 0.25);
                    b.Cargo(-3, 1, 3, 8, 0, 0.2);
                    b.Engine(-3, 1, -28);
                    b.Engine(0, 1, -28);
                    b.Engine(3, 1, -28);
                    b.Engine(-2, 3, -28);
                    b.Engine(2, 3, -28);
                }
            },
            new StationSpec
            {
                Name = "Épave du Léviathan", AnchorPlanet = 114, Offset = new Vector3(-8e6f, -2e6f, 5e6f), Seed = 117, Breaches = 5f, DarkMatter = 20,
                Band = new Color32(140, 60, 200, 255), Outside = new Color32(70, 70, 80, 255), Inside = new Color32(100, 96, 110, 255),
                Rare = new[] { (Items.ModItems.ExoticMatter, 1, 2, 0.4), (Items.ModItems.QuantumCore, 1, 2, 0.35), (Items.ModItems.Meteorite, 3, 8, 0.6), (Items.ModItems.Artifact, 1, 1, 0.3) },
                Decor = WreckDecor.LeviathanId,
                Build = b =>
                {
                    // A capital ship: a long armoured spine cut by bulkheads, the bow, a hangar to
                    // starboard (the way in), a collapsed bay to port, the bridge on its tower aft
                    // and the engine block; its blades, turrets and engines are the decor
                    b.Module(-3, 0, -30, 3, 3, 30);
                    b.Module(-2, 0, 31, 2, 2, 38);
                    b.Module(4, 0, -8, 10, 3, 6);
                    b.Module(-9, 0, -18, -4, 3, -8);
                    b.Module(-1, 4, -20, 1, 8, -17);
                    b.Module(-4, 9, -21, 4, 10, -16, 0.6f);
                    b.Module(-5, 0, -38, 5, 4, -31);
                    foreach (int z in new[] { -24, -12, 0, 12, 24 })
                        b.Bulkhead(z, -3, 3, 0, 3, -1);
                    b.Bulkhead(30, -2, 2, 0, 2, -1);
                    b.Bulkhead(-31, -3, 3, 0, 3, -1);
                    b.Rupture(3, 1, 14, 3, 3, 19);
                    b.Rupture(-9, 3, -18, -6, 3, -14);
                    b.Entrance(10, 0, 0, 0);
                    b.Cargo(5, -7, 9, 5, 0, 0.3);
                    b.Cargo(-2, -28, 2, -26, 0, 0.2);
                }
            },
            // ---- Pirate bases in the Erebos galaxy, around the Bastion (and one near Pyros)

            new StationSpec
            {
                Name = "Base pirate : Forteresse Écarlate", AnchorPlanet = Worlds.PlanetCatalog.BastionId, Offset = new Vector3(5e6f, 1.2e6f, -2.5e6f), Seed = 201, Breaches = 0f, Pirate = true,
                Band = new Color32(220, 30, 20, 255), Outside = new Color32(58, 52, 54, 255), Inside = new Color32(90, 80, 80, 255), DarkMatter = 30,
                Rare = new[] { (Items.ModItems.QuantumCore, 1, 2, 0.6), (Items.ModItems.ExoticMatter, 1, 3, 0.5), (Items.ModItems.NexoranAlloy, 3, 8, 0.7), (Items.ModItems.Uranium, 4, 10, 0.7), (Items.ModItems.Artifact, 1, 1, 0.3) },
                Build = b =>
                {
                    // A fortified core with four armed arms and a command tower
                    b.Module(-4, 0, -4, 4, 4, 4);
                    b.Module(-1, 0, 5, 1, 2, 12);
                    b.Module(-1, 0, -12, 1, 2, -5);
                    b.Module(5, 0, -1, 12, 2, 1);
                    b.Module(-12, 0, -1, -5, 2, 1);
                    b.Module(-2, 5, -2, 2, 9, 2);
                    b.Entrance(0, 0, 12, 2);
                    b.Battery(-1, 2, 12, 0);  // guns at each arm's end, facing out
                    b.Battery(1, 2, 12, 0);
                    b.Battery(-1, 2, -12, 2);
                    b.Battery(1, 2, -12, 2);
                    b.Battery(12, 2, -1, 1);
                    b.Battery(12, 2, 1, 1);
                    b.Battery(-12, 2, -1, 3);
                    b.Battery(-12, 2, 1, 3);
                    b.Battery(-3, 4, 0, 3, Combat.PirateShips.Plasma);
                    b.Battery(3, 4, 0, 1, Combat.PirateShips.Plasma);
                    b.Battery(0, 4, 3, 0, Combat.PirateShips.Plasma);
                    b.Battery(0, 4, -3, 2, Combat.PirateShips.Plasma);
                    b.Battery(0, 9, 0, 0, Combat.PirateShips.Pdc);
                    b.Battery(-4, 4, -4, 2, Combat.PirateShips.Pdc);
                    b.Battery(4, 4, 4, 0, Combat.PirateShips.Pdc);
                }
            },
            new StationSpec
            {
                Name = "Base pirate : Chantier de la Rouille", AnchorPlanet = Worlds.PlanetCatalog.BastionId, Offset = new Vector3(-4e6f, -1e6f, 4.5e6f), Seed = 211, Breaches = 0.3f, Pirate = true,
                Band = new Color32(230, 110, 20, 255), Outside = new Color32(80, 64, 52, 255), Inside = new Color32(100, 90, 80, 255), DarkMatter = 15,
                Rare = new[] { (Items.ModItems.Titanium, 6, 14, 0.8), (Items.ModItems.NexoranAlloy, 3, 6, 0.6), (Items.ModItems.Meteorite, 4, 10, 0.6), (Items.ModItems.QuantumCore, 1, 1, 0.35) },
                Build = b =>
                {
                    // A shipyard: a long spine, two docking cradles and a crane tower
                    b.Module(-2, 0, -16, 2, 3, 16);
                    b.Module(3, 0, -10, 9, 2, -4);
                    b.Module(3, 0, 4, 9, 2, 10);
                    b.Module(-1, 4, 10, 1, 10, 12);
                    b.Entrance(0, 0, -16, 3);
                    b.Battery(-1, 3, 16, 0);
                    b.Battery(1, 3, 16, 0);
                    b.Battery(0, 3, -16, 2);
                    b.Battery(9, 2, -7, 1);
                    b.Battery(9, 2, 7, 1);
                    b.Battery(-2, 3, 0, 3);
                    b.Battery(-2, 3, 8, 3);
                    b.Battery(0, 10, 11, 0, Combat.PirateShips.Laser);
                    b.Battery(0, 3, -6, 2, Combat.PirateShips.Pdc);
                    b.Battery(0, 3, 6, 0, Combat.PirateShips.Pdc);
                }
            },
            new StationSpec
            {
                Name = "Base pirate : Avant-poste du Croc", AnchorPlanet = 111, Offset = new Vector3(-3e6f, 2e6f, 3.5e6f), Seed = 223, Breaches = 0.5f, Pirate = true,
                Band = new Color32(200, 40, 30, 255), Outside = new Color32(64, 60, 58, 255), Inside = new Color32(96, 90, 86, 255), DarkMatter = 10,
                Rare = new[] { (Items.ModItems.Meteorite, 4, 10, 0.7), (Items.ModItems.Uranium, 3, 8, 0.6), (Items.ModItems.ExoticMatter, 1, 2, 0.3) },
                Build = b =>
                {
                    b.Module(-3, 0, -3, 3, 3, 3);
                    b.Module(-1, 0, 4, 1, 2, 8);
                    b.Module(-1, 4, -1, 1, 7, 1);
                    b.Entrance(0, 0, 8, 2);
                    b.Battery(-1, 2, 8, 0);
                    b.Battery(1, 2, 8, 0);
                    b.Battery(3, 3, 0, 1);
                    b.Battery(-3, 3, 0, 3);
                    b.Battery(0, 3, -3, 2);
                    b.Battery(0, 7, 0, 0, Combat.PirateShips.Pdc);
                }
            },
            new StationSpec
            {
                Name = "Épave de l'Icare", AnchorPlanet = 111, Offset = new Vector3(3e6f, 4e6f, -2e6f), Seed = 131, Breaches = 6f, DarkMatter = 30,
                Band = new Color32(230, 140, 30, 255), Outside = new Color32(60, 58, 56, 255), Inside = new Color32(80, 76, 72, 255),
                Rare = new[] { (Items.ModItems.ExoticMatter, 1, 3, 0.5), (Items.ModItems.Meteorite, 4, 10, 0.7) },
                Build = b =>
                {
                    // A scorched explorer that came too close to the Eye of Erebos
                    b.Module(-2, 0, -4, 2, 2, 4);
                    b.Module(-1, 0, 5, 1, 2, 8);
                    b.Entrance(0, 0, 8, 2);
                }
            }
        };

        private static float _checkAt = -1f;
        private static bool _done;

        /// <summary>
        /// The stations' build. Worlds saved with an older one get their stations replaced when
        /// loaded (version 2: docking door entrance, furnishing, outside equipment; version 3: the
        /// Vagabond, the Leviathan and the Hades probe, rebuilt big; version 4: furniture kept out of
        /// the passages between modules and off the shafts, wreck dressings solid; version 5: the
        /// Comptoir Hermès a merchant freighter, without its hangar), only those whose
        /// <see cref="StationSpec.Version"/> is newer than the world's; destroyed pirate bases stay wrecks.
        /// </summary>
        public const int StationVersion = 5;
        private const string VersionFile = "tfa_stations.txt";
        private static bool _loadingWorld;
        private static int _savedVersion = -1;
        private static readonly HashSet<uint> DeadBases = new HashSet<uint>();
        /// <summary>
        /// Ships a saved player stands in, and ships docked to another: never left out (the player
        /// would be lost in a space that no longer exists, a docked ship held by a missing one).
        /// </summary>
        private static readonly HashSet<uint> Occupied = new HashSet<uint>();
        private static int _replaced;

        /// <summary>Host, while the game loads a world's ships.</summary>
        public static void BeginWorldLoad()
        {
            _loadingWorld = true;
            _replaced = 0;
            _savedVersion = 0;
            DeadBases.Clear();
            Occupied.Clear();
            FindOccupiedShips();
            string directory = WorldDirectory();
            if (directory == null)
                return;
            string version = Path.Combine(directory, VersionFile);
            if (File.Exists(version) && int.TryParse(File.ReadAllText(version).Trim(), out int saved))
                _savedVersion = saved;
            string pirates = Path.Combine(directory, "tfa_pirates.txt");
            if (File.Exists(pirates))
            {
                foreach (string line in File.ReadAllLines(pirates))
                {
                    string[] f = line.Split(' ');
                    if (f.Length >= 2 && f[1] == "dead" && uint.TryParse(f[0], out uint id))
                        DeadBases.Add(id);
                }
            }
        }

        public static void EndWorldLoad()
        {
            _loadingWorld = false;
            if (_replaced > 0)
                AdvancedMod.Log.Msg($"stations: {_replaced} station(s) from an older version left out, rebuilt");
        }

        /// <summary>Whether this saved ship is one of the mod's stations in an older build, to rebuild.</summary>
        public static bool ShouldReplace(IShipState ship)
        {
            if (!_loadingWorld || _savedVersion >= StationVersion || ship?.StatefulParts == null || DeadBases.Contains(ship.Id) || Occupied.Contains(ship.Id))
                return false;
            foreach (StatefulPart part in ship.StatefulParts)
            {
                if (part?.State is BeaconState beacon && All.FirstOrDefault(s => s.Name == beacon.Name) is StationSpec spec && _savedVersion < spec.Version)
                {
                    _replaced++;
                    return true;
                }
            }
            return false;
        }

        // The save's players and the ships whose inside spaces they are in
        private static void FindOccupiedShips()
        {
            try
            {
                string save = Managers.Interface.MainGameSceneLoadData.SaveFileFullName;
                if (string.IsNullOrEmpty(save) || !File.Exists(save))
                    return;
                string text = File.ReadAllText(save);
                int players = text.IndexOf("\"players\":[", StringComparison.Ordinal);
                int ships = players >= 0 ? text.IndexOf("\"ships\":[", players, StringComparison.Ordinal) : -1;
                if (players < 0 || ships < 0)
                    return;
                var spaces = new HashSet<uint>();
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text.Substring(players, ships - players), @"""spaceId"":(\d+)"))
                    spaces.Add(uint.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture));
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, @"\{""id"":(\d+),""shipId"":(\d+)\}"))
                    if (spaces.Contains(uint.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)))
                        Occupied.Add(uint.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture));
                // A player's ship docked to a station (its hangar before 0.42, or a door)
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, @"""isDocked"":true,""dockedShipId"":(\d+)"))
                    Occupied.Add(uint.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture));
            }
            catch (Exception e)
            {
                ErrorLog.Report("stations occupied", e);
            }
        }

        /// <summary>After the game saved the world: the stations' build goes beside it.</summary>
        public static void SaveVersion(string worldDirectory)
        {
            if (!string.IsNullOrEmpty(worldDirectory) && Directory.Exists(worldDirectory))
                File.WriteAllText(Path.Combine(worldDirectory, VersionFile), StationVersion.ToString(CultureInfo.InvariantCulture));
        }

        private static string WorldDirectory()
        {
            string save = Managers.Interface.MainGameSceneLoadData.SaveFileFullName;
            return string.IsNullOrEmpty(save) ? null : Path.GetDirectoryName(save);
        }

        public static void OnSceneLoaded(bool inGame)
        {
            _done = false;
            _listed = false;
            Pending.Clear();
            Shelters.Clear();
            _checkAt = inGame ? Time.unscaledTime + 8f : -1f;
        }

        // Stations still to create in this world, one at a time
        private static readonly Queue<StationSpec> Pending = new Queue<StationSpec>();
        private static bool _listed;

        /// <summary>
        /// Host: once the world has loaded, creates the stations it does not have yet, one every
        /// few seconds, to spread the work (see also <see cref="Common.SyncBatchFix"/>).
        /// </summary>
        public static void Update()
        {
            if (_done || _checkAt < 0f || Time.unscaledTime < _checkAt || !Settings.AbandonedStations.Value)
                return;
            if (!GameServices.IsServer)
            {
                _done = true;
                return;
            }
            ShipsServerTracker ships = ServiceLocator.GetService<ShipsServerTracker>();
            if (ships == null)
                return;
            if (!_listed)
            {
                _listed = true;
                var existing = new HashSet<string>(ships.Ships.SelectMany(s => s.GetAllStatefulParts<BeaconState>()).Select(p => ((BeaconState)p.State).Name));
                foreach (StationSpec spec in All)
                {
                    if (existing.Contains(spec.Name))
                        continue;
                    // Pirate bases need the Arsenal mod (their guns are its parts)
                    if (spec.Pirate && (!Power.ArsenalShield.Installed || !Settings.Pirates.Value))
                        continue;
                    Pending.Enqueue(spec);
                }
            }
            if (Pending.Count == 0)
            {
                _done = true;
                return;
            }
            StationSpec next = Pending.Dequeue();
            _checkAt = Time.unscaledTime + 3f;
            try
            {
                Spawn(next, ships);
            }
            catch (Exception e)
            {
                AdvancedMod.Log.Error($"stations: could not create {next.Name}: {e}");
            }
        }

        private static readonly List<FixedWorldSpace> Shelters = new List<FixedWorldSpace>();

        /// <summary>
        /// Stations near the black hole are held in place (as if station-keeping): its pull is not
        /// applied within this distance of them, or they would fall in over the hours.
        /// </summary>
        public const float ShelterRadius = 3000f;

        public static bool IsSheltered(WorldTracking.Interface.Services.IWorldTranspose transpose, uint spaceId, Vector3 position)
        {
            if (Shelters.Count == 0)
            {
                foreach (StationSpec spec in All.Where(s => s.NearBlackHole))
                {
                    if (TryPosition(spec, out PreciseWorldPosition at))
                        Shelters.Add(new FixedWorldSpace { Position = at });
                }
            }
            foreach (FixedWorldSpace shelter in Shelters)
            {
                Vector3Double center = transpose.Position(shelter, spaceId, Vector3Double.Zero);
                if (((Vector3)(center - (Vector3Double)position)).magnitude < ShelterRadius)
                    return true;
            }
            return false;
        }

        /// <summary>Where a station sits; also used to keep the black hole from pulling it in.</summary>
        public static bool TryPosition(StationSpec spec, out PreciseWorldPosition position)
        {
            position = default;
            if (spec.NearBlackHole)
            {
                Hole hole = BlackHole.Placed.FirstOrDefault();
                if (hole == null)
                    return false;
                position = hole.Space.Position + (Vector3Double)spec.Offset;
                return true;
            }
            IPlanetsProvider planets = ServiceLocator.GetService<IPlanetsServerProvider>() as IPlanetsProvider ?? ServiceLocator.GetService<IPlanetsClientProvider>();
            IPlanetProperties anchor = planets?.Planets.FirstOrDefault(p => p.Id == spec.AnchorPlanet);
            if (anchor == null)
                return false;
            position = new PreciseWorldPosition(anchor.WorldCell, Vector3.zero) + (Vector3Double)spec.Offset;
            return true;
        }

        private static void Spawn(StationSpec spec, ShipsServerTracker ships)
        {
            if (!TryPosition(spec, out PreciseWorldPosition position))
            {
                AdvancedMod.Log.Warning($"stations: no anchor for {spec.Name}");
                return;
            }
            var blueprint = new Blueprint(spec.Seed) { Spec = spec };
            spec.Build(blueprint);
            string json = blueprint.ToJson(spec.Name);
            var data = JsonUtility.FromJson<ShipImportExportUtils.ShipImportExportData>(json);
            SavedShipState state = new ShipsConverter().ConvertToState(data.ships).States.Cast<SavedShipState>().Single();

            var ids = ServiceLocator.GetService<WorldObjectsIdsServerTracker>();
            SpacesServer spaces = ServiceLocator.GetService<SpacesServer>();
            (uint spaceId, ISpace space) = spaces.GetOrCreateSpaceForPosition(new FixedWorldSpace { Position = position });
            AdvancedMod.Log.Msg($"stations: {spec.Name} in space {spaceId} ({space?.GetType().Name})");
            state.Id = ids.CreateUniqueIdForObject();
            state.SpaceId = spaceId;
            state.Position = Vector3.zero;
            state.Velocity = Vector3.zero;
            state.Rotation = Quaternion.Euler(spec.Seed * 7f % 30f, spec.Seed * 13f % 360f, spec.Seed * 3f % 20f);
            state.RelativeSpaceId = spaces.GetOrCreateShipRelativeSpace(state.Id);
            ships.LoadShip(state);
            if (spec.Pirate)
                Combat.Pirates.AddBase(state.Id);
            AdvancedMod.Log.Msg($"stations: created {spec.Name} (ship {state.Id}, {state.HullParts.Length} hull parts, {state.StatefulParts.Length} parts)");
        }

        /// <summary>Hull and parts of a station, written in the game's own ship save format.</summary>
        internal sealed class Blueprint
        {
            // Game part ids: iron floor and wall, glass wall and floor, beacon, chest, chemical thruster
            private const int IronFloor = 1, IronWall = 2, GlassWall = 7, GlassFloor = 36;
            private const int Beacon = 38, Chest = 30, Thruster = 19, Bed = 43, FluidTank = 23, DockingDoor = 42;
            // Furnishing: the game's star map, research table and supply crate; the mod's ceiling
            // light, LED strip, blinking beacon, shelf, console, truss, beam and solar support
            private const int StarMap = 15, ResearchTable = 22, SupplyCrate = 310;
            private const int CeilingLight = 7101, LedStrip = 7116, NavBeacon = 7114, Shelf = 7346, Console = 7302,
                Truss = 7364, Beam = 7333, SolarSupport = 7375;
            private const uint Oxygen = 108, Ethanol = 111;

            public StationSpec Spec;

            private readonly HashSet<Vector3Int> _inside = new HashSet<Vector3Int>();
            private readonly HashSet<Vector3Int> _openings = new HashSet<Vector3Int>();
            // Windows share of a module's walls (glazed bridges and decks), torn-open voxels, inner walls
            private readonly Dictionary<Vector3Int, float> _glass = new Dictionary<Vector3Int, float>();
            private readonly HashSet<Vector3Int> _ruptures = new HashSet<Vector3Int>();
            private readonly List<Vector3Int> _partitions = new List<Vector3Int>();
            private readonly List<(int X, int Y, int Z)> _engines = new List<(int, int, int)>();
            private readonly List<(int X0, int Z0, int X1, int Z1, int Y, double Loot)> _cargo = new List<(int, int, int, int, int, double)>();

            /// <summary>The outer hull faces placed (voxel, face, part), filled by <see cref="ToJson"/>: the decor dresses them.</summary>
            public readonly List<(Vector3Int V, int Face, int Part)> Faces = new List<(Vector3Int, int, int)>();
            /// <summary>The outer hull faces left missing (breaches and ruptures, not the doors), filled by <see cref="ToJson"/>.</summary>
            public readonly List<(Vector3Int V, int Face)> Holes = new List<(Vector3Int, int)>();
            public IReadOnlyList<(int x0, int y0, int z0, int x1, int y1, int z1)> Modules => _modules;
            public bool Inside(Vector3Int v) => _inside.Contains(v);
            public bool Ruptured(Vector3Int v) => _ruptures.Contains(v);
            private readonly List<(int x0, int y0, int z0, int x1, int y1, int z1)> _modules = new List<(int, int, int, int, int, int)>();
            private readonly System.Random _random;

            private readonly List<(ushort Weapon, Vector3 At, int Forward)> _batteries = new List<(ushort, Vector3, int)>();
            private readonly List<(int X, int Y, int Z, int Face)> _doors = new List<(int, int, int, int)>();
            // Floating parts with a docking collar (the hangar): their door states
            private readonly List<int> _collars = new List<int>();

            /// <summary>The docking doors (interior voxel, outer face), for the development photos.</summary>
            public IReadOnlyList<(int X, int Y, int Z, int Face)> Doors => _doors;
            // Floor cells already holding something (chests, furniture), and the door's way in
            private readonly HashSet<Vector2Int> _taken = new HashSet<Vector2Int>();

            public Blueprint(int seed)
            {
                _random = new System.Random(seed);
            }

            /// <summary>
            /// An Arsenal ship weapon on the roof of this interior voxel, facing a side (0 +z, 1 +x,
            /// 2 -z, 3 -x): a machine gun by default.
            /// </summary>
            public void Battery(int x, int y, int z, int facing, ushort weapon = Combat.PirateShips.MachineGun)
            {
                _batteries.Add((weapon, new Vector3(x, y + 0.5f + 0.04f, z), facing));
            }

            /// <summary>
            /// A box of interior voxels (inclusive bounds). Touching modules open onto each other.
            /// <paramref name="glass"/>: share of its walls that are windows (a bridge), 0 for the usual row.
            /// </summary>
            public void Module(int x0, int y0, int z0, int x1, int y1, int z1, float glass = 0f)
            {
                _modules.Add((x0, y0, z0, x1, y1, z1));
                for (int x = x0; x <= x1; x++)
                    for (int y = y0; y <= y1; y++)
                        for (int z = z0; z <= z1; z++)
                        {
                            _inside.Add(new Vector3Int(x, y, z));
                            if (glass > 0f)
                                _glass[new Vector3Int(x, y, z)] = glass;
                        }
            }

            /// <summary>
            /// An inner wall across the ship between z and z + 1, over x0..x1 and y0..y1, with a 2 x 2
            /// doorway at doorX, doorX + 1 on the floor. A few of its plates are missing.
            /// </summary>
            public void Bulkhead(int z, int x0, int x1, int y0, int y1, int doorX)
            {
                for (int x = x0; x <= x1; x++)
                {
                    for (int y = y0; y <= y1; y++)
                    {
                        if ((x == doorX || x == doorX + 1) && y <= y0 + 1)
                            continue;
                        _partitions.Add(new Vector3Int(x, y, z));
                    }
                }
                // Keep the doorway clear on both sides
                for (int x = doorX; x <= doorX + 1; x++)
                {
                    _taken.Add(new Vector2Int(x, z));
                    _taken.Add(new Vector2Int(x, z + 1));
                }
            }

            /// <summary>A torn-open part of the hull: these voxels lose every outer face (the decor frames the hole).</summary>
            public void Rupture(int x0, int y0, int z0, int x1, int y1, int z1)
            {
                for (int x = x0; x <= x1; x++)
                    for (int y = y0; y <= y1; y++)
                        for (int z = z0; z <= z1; z++)
                            _ruptures.Add(new Vector3Int(x, y, z));
            }

            /// <summary>A dead main thruster on the outer -z face (the stern) of this interior voxel, pointing back.</summary>
            public void Engine(int x, int y, int z) => _engines.Add((x, y, z));

            /// <summary>Rows of supply crates on a floor (every other cell), each holding loot with this chance, empty otherwise.</summary>
            public void Cargo(int x0, int z0, int x1, int z1, int y, double loot) => _cargo.Add((x0, z0, x1, z1, y, loot));

            /// <summary>
            /// The way in: an open docking door, 2 wide and 2 high, on the outer face (0 +x, 1 -x,
            /// 2 +z, 3 -z) of this interior voxel and the next one to its right seen from outside
            /// (+x for a +z face, -x for -z, -z for +x, +z for -x: the game turns the door's width
            /// with it). A ship's docking door docks against it; a player can also float in.
            /// </summary>
            public void Entrance(int x, int y, int z, int face)
            {
                _doors.Add((x, y, z, face));
                Vector3Int side = DoorSide(face);
                for (int a = 0; a < 2; a++)
                {
                    for (int b = 0; b < 2; b++)
                    {
                        Vector3Int v = new Vector3Int(x, y + b, z) + side * a;
                        _openings.Add(v * 10 + new Vector3Int(face, 0, 0));
                        // Keep the way in clear, one cell deep
                        _taken.Add(new Vector2Int(v.x, v.z));
                        Vector3Int inward = v - Direction(face);
                        _taken.Add(new Vector2Int(inward.x, inward.z));
                    }
                }
            }

            public string ToJson(string beaconName)
            {
                var hull = new List<string>();
                var floating = new List<string>();
                var storages = new List<string>();
                var thrusters = new List<string>();
                int nextPart = 1;

                // Every face between an interior voxel and the outside gets a hull part; a few are
                // missing (breaches), windows run along the middle of the walls
                foreach (Vector3Int v in _inside.OrderBy(v => v.x).ThenBy(v => v.y).ThenBy(v => v.z))
                {
                    for (int face = 0; face < 6; face++)
                    {
                        Vector3Int n = v + Direction(face);
                        if (_inside.Contains(n) || _openings.Contains(v * 10 + new Vector3Int(face, 0, 0)))
                            continue;
                        bool wall = face < 4;
                        if (_ruptures.Contains(v) || _random.NextDouble() < (wall ? 0.05 : 0.03) * (Spec != null ? Spec.Breaches : 1f))
                        {
                            Holes.Add((v, face));
                            continue;
                        }
                        bool window = wall && (_glass.TryGetValue(v, out float glass) ? _random.NextDouble() < glass : v.y % 4 == 1 && _random.NextDouble() < 0.35);
                        int part = window ? GlassWall : (face == 4 && _random.NextDouble() < 0.06 ? GlassFloor : wall ? IronWall : IronFloor);
                        Faces.Add((v, face, part));
                        hull.Add(HullFace(v, face, part));
                    }
                }

                // Inner walls, a few plates missing
                foreach (Vector3Int p in _partitions)
                {
                    if (_inside.Contains(p) && _inside.Contains(p + Direction(2)) && _random.NextDouble() >= 0.06 * (Spec != null ? Spec.Breaches : 1f))
                        hull.Add(HullFace(p, 2, IronWall, inner: true));
                }

                // Beacon on the roof of the first module, facing up
                var core = _modules[0];
                float cx = (core.x0 + core.x1) * 0.5f, cz = (core.z0 + core.z1) * 0.5f;
                floating.Add(Floating(nextPart, Beacon, new Vector3(Mathf.Round(cx), core.y1 + 0.5f + 0.04f, Mathf.Round(cz)), 4, 0));
                int beaconId = nextPart++;

                // Supply chests left behind on module floors
                foreach (var m in _modules)
                {
                    int count = (m.x1 - m.x0 + 1) * (m.z1 - m.z0 + 1) >= 15 ? 2 : 1;
                    for (int i = 0; i < count; i++)
                    {
                        int x = _random.Next(m.x0, m.x1 + 1), z = _random.Next(m.z0, m.z1 + 1);
                        for (int tries = 0; tries < 8 && !Usable(m, x, z); tries++)
                        {
                            x = _random.Next(m.x0, m.x1 + 1);
                            z = _random.Next(m.z0, m.z1 + 1);
                        }
                        if (!Usable(m, x, z))
                        {
                            if (!(Near(m, x, z) is Vector2Int cell))
                                continue;
                            x = cell.x;
                            z = cell.y;
                        }
                        _taken.Add(new Vector2Int(x, z));
                        // A loot case now and then instead of a plain chest (opened: no key needed)
                        // (the mod's chests in the rarity's color; the finish roll is kept so the rest of the layout stays the same)
                        int container = _random.NextDouble() < 0.4 ? LootChests.FirstId + _random.Next(0, 5) + 0 * _random.Next(0, 7) : Chest;
                        floating.Add(Floating(nextPart, container, new Vector3(x, m.y0 - 0.5f + 0.04f, z), 2, 4));
                        storages.Add($"{{\"partId\":{nextPart},\"slots\":[{Loot()}]}}");
                        nextPart++;
                    }
                }

                // Crew quarters and supplies: bunks in the end modules, oxygen and ethanol tanks in the core
                var tanks = new List<string>();
                foreach (var m in _modules.Skip(1).Where(m => (m.x1 - m.x0 + 1) * (m.z1 - m.z0 + 1) >= 12 && m.y0 == 0))
                {
                    if (Take(m, m.x1, m.z0) is Vector2Int bunk)
                        floating.Add(Floating(nextPart++, Bed, new Vector3(bunk.x, m.y0 - 0.5f + 0.04f, bunk.y), 2, 4));
                    if (_random.NextDouble() < 0.5 && Take(m, m.x0, m.z0) is Vector2Int second)
                        floating.Add(Floating(nextPart++, Bed, new Vector3(second.x, m.y0 - 0.5f + 0.04f, second.y), 2, 4));
                }
                if (Spec != null && Spec.DarkMatter > 0 && Take(core, (int)Mathf.Round(cx), core.z0) is Vector2Int dark)
                {
                    floating.Add(Floating(nextPart, FluidTank, new Vector3(dark.x, core.y0 - 0.5f + 0.04f, dark.y), 2, 4));
                    tanks.Add(string.Format(CultureInfo.InvariantCulture, "{{\"partId\":{0},\"fluidItemId\":{1},\"fluidQuantity\":{2:0.0}}}", nextPart, Items.ModItems.DarkMatter, Spec.DarkMatter));
                    nextPart++;
                }
                foreach (uint fluid in new[] { Oxygen, Ethanol })
                {
                    if (!(Take(core, fluid == Oxygen ? core.x0 : core.x1, core.z1) is Vector2Int tank))
                        continue;
                    floating.Add(Floating(nextPart, FluidTank, new Vector3(tank.x, core.y0 - 0.5f + 0.04f, tank.y), 2, 4));
                    tanks.Add(string.Format(CultureInfo.InvariantCulture, "{{\"partId\":{0},\"fluidItemId\":{1},\"fluidQuantity\":{2:0.0}}}", nextPart, fluid, 10 + _random.Next(0, 40)));
                    nextPart++;
                }

                Furnish(floating, storages, ref nextPart);
                if (Spec != null && Spec.Merchant)
                    Merchant(floating, ref nextPart);

                // Cargo rows: supply crates, most of them emptied long ago
                foreach (var c in _cargo)
                {
                    for (int x = c.X0; x <= c.X1; x += 2)
                    {
                        for (int z = c.Z0; z <= c.Z1; z += 2)
                        {
                            var module = _modules.FirstOrDefault(m => m.y0 == c.Y && x >= m.x0 && x <= m.x1 && z >= m.z0 && z <= m.z1);
                            if (module == default || !Usable(module, x, z) || !_taken.Add(new Vector2Int(x, z)))
                                continue;
                            floating.Add(Floating(nextPart, SupplyCrate, new Vector3(x, c.Y - 0.5f + 0.04f, z), _random.Next(0, 4), 4));
                            storages.Add($"{{\"partId\":{nextPart},\"slots\":[{(_random.NextDouble() < c.Loot ? Loot() : EmptySlots())}]}}");
                            nextPart++;
                        }
                    }
                }

                // Pirate batteries (the base's guns; only with the Arsenal mod, whose parts they are)
                if (Spec != null && Spec.Pirate && Power.ArsenalShield.Installed)
                {
                    foreach ((ushort weapon, Vector3 at, int facing) in _batteries)
                        floating.Add(Floating(nextPart++, weapon, at, facing, 4));
                }

                // Dead thrusters on the underside of the big modules
                foreach (var m in _modules.Where(m => (m.x1 - m.x0) >= 4))
                {
                    floating.Add(Floating(nextPart, Thruster, new Vector3(m.x0 + 0.5f, m.y0 - 0.5f - 0.04f, (m.z0 + m.z1) * 0.5f), 5, 0));
                    thrusters.Add($"{{\"partId\":{nextPart},\"fluidItemId\":100,\"fluidQuantity\":0.0}}");
                    nextPart++;
                }
                // The main engines at the stern
                foreach ((int x, int y, int z) in _engines)
                {
                    floating.Add(Floating(nextPart, Thruster, new Vector3(x, y, z - 0.5f - 0.04f), 2, 4));
                    thrusters.Add($"{{\"partId\":{nextPart},\"fluidItemId\":100,\"fluidQuantity\":0.0}}");
                    nextPart++;
                }

                // The hull's dressing, in the ship's own frame (like the pirates' fairings)
                if (Spec != null && Spec.Decor != 0)
                    floating.Add(Floating(nextPart++, Spec.Decor, Vector3.zero, 0, 4));

                var sb = new StringBuilder();
                sb.Append("{\"rootShipId\":1,\"cables\":[],\"ships\":[{");
                sb.Append("\"id\":1,\"relativeSpaceId\":0,\"spaceId\":0,");
                sb.Append("\"position\":{\"x\":0,\"y\":0,\"z\":0},\"velocity\":{\"x\":0,\"y\":0,\"z\":0},\"rotation\":{\"x\":0,\"y\":0,\"z\":0,\"w\":1},");
                sb.Append("\"rooms\":[],");
                sb.Append("\"hullParts\":[").Append(string.Join(",", hull)).Append("],");
                sb.Append("\"statefulFloatingParts\":[").Append(string.Join(",", floating)).Append("],");
                var doors = new List<string>();
                var doorStates = new List<string>();
                foreach (int collar in _collars)
                    doorStates.Add($"{{\"partId\":{collar},\"isOpen\":true,\"isDocked\":false,\"dockedShipId\":0,\"dockedPartId\":0}}");
                foreach ((int x, int y, int z, int face) in _doors)
                {
                    // In the save format a wall belongs to the voxel behind it on its -z / -x side
                    int dx = face == 1 ? x - 1 : x, dz = face == 3 ? z - 1 : z;
                    int type = face < 2 ? 2 : 1;
                    int orientation = face == 0 ? 1 : face == 1 ? 3 : face == 2 ? 0 : 2;
                    doors.Add($"{{\"id\":{nextPart},\"settingsId\":{DockingDoor},\"coord\":{{\"x\":{dx},\"y\":{y},\"z\":{dz}}},\"orientationId\":{orientation},\"voxelPartTypeId\":{type}}}");
                    doorStates.Add($"{{\"partId\":{nextPart},\"isOpen\":true,\"isDocked\":false,\"dockedShipId\":0,\"dockedPartId\":0}}");
                    nextPart++;
                }
                sb.Append("\"statefulHullParts\":[").Append(string.Join(",", doors)).Append("],");
                sb.Append("\"thrustersStates\":[").Append(string.Join(",", thrusters)).Append("],");
                sb.Append("\"dockingDoorStates\":[").Append(string.Join(",", doorStates)).Append("],");
                sb.Append("\"storageStates\":[").Append(string.Join(",", storages)).Append("],");
                sb.Append("\"beaconStates\":[{\"partId\":").Append(beaconId).Append(",\"name\":\"").Append(beaconName.Replace("\"", "'")).Append("\"}],");
                sb.Append("\"fluidTankStates\":[").Append(string.Join(",", tanks)).Append("],");
                foreach (string empty in new[] { "leverStates", "warpDriveStates", "targeterStates", "mathGateStates",
                    "unaryMathGateStates", "memoryRegisterStates", "constantStates", "transmitterStates", "rotorStates", "electrolyzerStates",
                    "woodThrusterStates", "woodGeneratorStates", "ethanolDistillerStates" })
                    sb.Append('"').Append(empty).Append("\":[],");
                sb.Length--;
                sb.Append("}]}");
                return sb.ToString();
            }

            /// <summary>
            /// The trading ship's counters: a merchant terminal against the trading hall's port wall,
            /// facing the docking door across the room, another in the cargo hold (the fifth module).
            /// </summary>
            private void Merchant(List<string> floating, ref int nextPart)
            {
                if (!TFItems.TFItemsMod.Active)
                    return;
                var hall = _modules[0];
                // The terminal's front is its -z: turned to -x (facing 3) its front looks +x
                if (Take(hall, hall.x0, (hall.z0 + hall.z1) / 2) is Vector2Int counter)
                    floating.Add(Floating(nextPart++, TFItems.Devices.Trader.TerminalId, new Vector3(counter.x, hall.y0 - 0.5f + 0.04f, counter.y), 3, 4));
                if (_modules.Count > 4 && Take(_modules[4], _modules[4].x0, (_modules[4].z0 + _modules[4].z1) / 2) is Vector2Int hold)
                    floating.Add(Floating(nextPart++, TFItems.Devices.Trader.TerminalId, new Vector3(hold.x, _modules[4].y0 - 0.5f + 0.04f, hold.y), 3, 4));
                // Its inhabitants: the engineer across the trading hall, the doctor in the next module
                if (Take(hall, hall.x1, (hall.z0 + hall.z1) / 2) is Vector2Int engineer)
                    floating.Add(Floating(nextPart++, TFItems.Devices.Npc.EngineerId, new Vector3(engineer.x, hall.y0 - 0.5f + 0.04f, engineer.y), 1, 4));
                if (_modules.Count > 1 && Take(_modules[1], _modules[1].x0, (_modules[1].z0 + _modules[1].z1) / 2) is Vector2Int doctor)
                    floating.Add(Floating(nextPart++, TFItems.Devices.Npc.DoctorId, new Vector3(doctor.x, _modules[1].y0 - 0.5f + 0.04f, doctor.y), 3, 4));
            }

            // A free floor cell of a module, or null
            private Vector2Int? FreeCell((int x0, int y0, int z0, int x1, int y1, int z1) m, bool againstWall)
            {
                for (int tries = 0; tries < 20; tries++)
                {
                    int x = _random.Next(m.x0, m.x1 + 1), z = _random.Next(m.z0, m.z1 + 1);
                    if (againstWall && x != m.x0 && x != m.x1 && z != m.z0 && z != m.z1)
                        continue;
                    var cell = new Vector2Int(x, z);
                    if (Usable(m, x, z) && _taken.Add(cell))
                        return cell;
                }
                return null;
            }

            /// <summary>
            /// Whether furniture can stand on this floor cell of a module: free, with a floor under
            /// it (not over the shaft to a module below) and not in a passage to another module
            /// (a part there blocks the way: players got stuck on them).
            /// </summary>
            private bool Usable((int x0, int y0, int z0, int x1, int y1, int z1) m, int x, int z)
            {
                if (_taken.Contains(new Vector2Int(x, z)) || _inside.Contains(new Vector3Int(x, m.y0 - 1, z)))
                    return false;
                foreach (Vector2Int d in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
                {
                    int nx = x + d.x, nz = z + d.y;
                    bool inModule = nx >= m.x0 && nx <= m.x1 && nz >= m.z0 && nz <= m.z1;
                    if (!inModule && (_inside.Contains(new Vector3Int(nx, m.y0, nz)) || _inside.Contains(new Vector3Int(nx, m.y0 + 1, nz))))
                        return false;
                }
                return true;
            }

            // The usable cell nearest to (x, z) in a module, or null
            private Vector2Int? Near((int x0, int y0, int z0, int x1, int y1, int z1) m, int x, int z)
            {
                Vector2Int? best = null;
                int bestDistance = int.MaxValue;
                for (int cx = m.x0; cx <= m.x1; cx++)
                {
                    for (int cz = m.z0; cz <= m.z1; cz++)
                    {
                        int distance = Math.Abs(cx - x) + Math.Abs(cz - z);
                        if (distance < bestDistance && Usable(m, cx, cz))
                        {
                            best = new Vector2Int(cx, cz);
                            bestDistance = distance;
                        }
                    }
                }
                return best;
            }

            // The usable cell nearest to (x, z), taken, or null
            private Vector2Int? Take((int x0, int y0, int z0, int x1, int y1, int z1) m, int x, int z)
            {
                Vector2Int? cell = Near(m, x, z);
                if (cell.HasValue)
                    _taken.Add(cell.Value);
                return cell;
            }

            /// <summary>
            /// What makes it look lived in: a light on every module's ceiling, a lit strip in the
            /// corridors, a console and a star map in the core, shelves and supply
            /// crates, a research table; outside, a truss mast with solar supports on the core and
            /// blinking beacons on the modules' far corners.
            /// </summary>
            private void Furnish(List<string> floating, List<string> storages, ref int nextPart)
            {
                bool wreck = Spec != null && Spec.Breaches >= 4f;
                // The merchant freighter's outside is all in its dressing (and fewer parts to sync)
                bool dressed = Spec != null && Spec.Merchant;
                for (int i = 0; i < _modules.Count; i++)
                {
                    var m = _modules[i];
                    float cx = Mathf.Round((m.x0 + m.x1) * 0.5f), cz = Mathf.Round((m.z0 + m.z1) * 0.5f);
                    float floor = m.y0 - 0.5f + 0.04f, ceiling = m.y1 + 0.5f - 0.04f, roof = m.y1 + 0.5f + 0.04f;
                    int area = (m.x1 - m.x0 + 1) * (m.z1 - m.z0 + 1);
                    bool corridor = Mathf.Min(m.x1 - m.x0, m.z1 - m.z0) <= 2 && area >= 6;

                    // Light: ceiling lights in rooms (most of them dead in a wreck), a lit strip along corridors
                    if ((!wreck || _random.NextDouble() < 0.3) && !_inside.Contains(new Vector3Int((int)cx, m.y1 + 1, (int)cz)))
                    {
                        if (corridor)
                        {
                            bool alongZ = (m.z1 - m.z0) > (m.x1 - m.x0);
                            floating.Add(Floating(nextPart++, LedStrip, new Vector3(cx, ceiling, cz), alongZ ? 1 : 0, 5));
                        }
                        else
                        {
                            floating.Add(Floating(nextPart++, CeilingLight, new Vector3(cx, ceiling, cz), 0, 5));
                        }
                    }

                    // Furniture on the floor, against the walls
                    if (i == 0 && area >= 9)
                    {
                        if (FreeCell(m, true) is Vector2Int console)
                            floating.Add(Floating(nextPart++, Console, new Vector3(console.x, floor, console.y), Facing(m, console), 4));
                        if (FreeCell(m, false) is Vector2Int map)
                            floating.Add(Floating(nextPart++, StarMap, new Vector3(map.x, floor, map.y), 2, 4));
                    }
                    if (!corridor && area >= 9)
                    {
                        int shelves = area >= 20 ? 2 : 1;
                        for (int s = 0; s < shelves; s++)
                        {
                            if (FreeCell(m, true) is Vector2Int shelf)
                                floating.Add(Floating(nextPart++, Shelf, new Vector3(shelf.x, floor, shelf.y), Facing(m, shelf), 4));
                        }
                        if (_random.NextDouble() < 0.6 && FreeCell(m, true) is Vector2Int crate)
                        {
                            floating.Add(Floating(nextPart, SupplyCrate, new Vector3(crate.x, floor, crate.y), _random.Next(0, 4), 4));
                            storages.Add($"{{\"partId\":{nextPart},\"slots\":[{Loot()}]}}");
                            nextPart++;
                        }
                        if (i == 1 && FreeCell(m, true) is Vector2Int table)
                            floating.Add(Floating(nextPart++, ResearchTable, new Vector3(table.x, floor, table.y), Facing(m, table), 4));
                    }

                    if (dressed)
                        continue;
                    // Outside: solar arrays along the corridors' roofs, an antenna on top of a tower
                    if (corridor)
                    {
                        bool alongZ = (m.z1 - m.z0) > (m.x1 - m.x0);
                        int from = alongZ ? m.z0 : m.x0, to = alongZ ? m.z1 : m.x1;
                        for (int c = from + 1; c < to; c += 3)
                            floating.Add(Floating(nextPart++, SolarSupport, alongZ ? new Vector3(cx, roof, c) : new Vector3(c, roof, cz), 0, 4));
                    }
                    // (not under another module stacked on it)
                    if (i > 0 && m.y0 > 0 && m.x1 - m.x0 <= 4 && m.z1 - m.z0 <= 4 && !_inside.Contains(new Vector3Int((int)cx, m.y1 + 1, (int)cz)))
                    {
                        floating.Add(Floating(nextPart++, Beam, new Vector3(cx, roof, cz), 0, 4));
                        floating.Add(Floating(nextPart++, NavBeacon, new Vector3(cx, roof + 4f, cz), 0, 4));
                    }

                    // Outside: blinking beacons on the far corners of the roof
                    if (i > 0 || _modules.Count == 1)
                    {
                        floating.Add(Floating(nextPart++, NavBeacon, new Vector3(m.x0, roof, m.z0), 0, 4));
                        floating.Add(Floating(nextPart++, NavBeacon, new Vector3(m.x1, roof, m.z1), 0, 4));
                    }
                }

                if (dressed)
                    return;
                // The core's roof: a truss mast with solar supports around it, beams to the corners
                var core = _modules[0];
                float rx = Mathf.Round((core.x0 + core.x1) * 0.5f), rz = Mathf.Round((core.z0 + core.z1) * 0.5f);
                float top = core.y1 + 0.5f + 0.04f;
                // (the beacon stands on the roof's middle: the mast goes one cell beside it)
                floating.Add(Floating(nextPart++, Truss, new Vector3(rx + 1f, top, rz), 0, 4));
                floating.Add(Floating(nextPart++, Truss, new Vector3(rx + 1f, top + 2f, rz), 0, 4));
                floating.Add(Floating(nextPart++, NavBeacon, new Vector3(rx + 1f, top + 4f, rz), 0, 4));
                if (core.x1 - core.x0 >= 2 && core.z1 - core.z0 >= 2)
                {
                    foreach ((int x, int z) in new[] { (core.x0, core.z0), (core.x1, core.z0), (core.x0, core.z1), (core.x1, core.z1) })
                    {
                        floating.Add(Floating(nextPart++, SolarSupport, new Vector3(x, top, z), 0, 4));
                        floating.Add(Floating(nextPart++, Beam, new Vector3(x, top, z), 0, 4));
                    }
                }
            }

            // Furniture against a wall faces into the room (0 +z, 1 +x, 2 -z, 3 -x)
            private static int Facing((int x0, int y0, int z0, int x1, int y1, int z1) m, Vector2Int cell)
            {
                if (cell.x == m.x0) return 1;
                if (cell.x == m.x1) return 3;
                if (cell.y == m.z0) return 0;
                return 2;
            }

            /// <summary>Where a docking door on this face takes its second column of voxels.</summary>
            internal static Vector3Int DoorSide(int face)
            {
                switch (face)
                {
                    case 0: return new Vector3Int(0, 0, -1);
                    case 1: return new Vector3Int(0, 0, 1);
                    case 3: return Vector3Int.left;
                    default: return Vector3Int.right;
                }
            }

            // Faces: 0 +x, 1 -x, 2 +z, 3 -z, 4 -y (floor), 5 +y (ceiling)
            internal static Vector3Int Direction(int face)
            {
                switch (face)
                {
                    case 0: return Vector3Int.right;
                    case 1: return Vector3Int.left;
                    case 2: return new Vector3Int(0, 0, 1);
                    case 3: return new Vector3Int(0, 0, -1);
                    case 4: return Vector3Int.down;
                    default: return Vector3Int.up;
                }
            }

            // In the save format a voxel owns its floor (below), its forward wall (+z) and its right wall (+x)
            private string HullFace(Vector3Int v, int face, int partId, bool inner = false)
            {
                Vector3Int at = v;
                int type, forward, up;
                switch (face)
                {
                    case 0: type = 2; forward = 1; up = 4; break;
                    case 1: at = v + Vector3Int.left; type = 2; forward = 3; up = 4; break;
                    case 2: type = 1; forward = 0; up = 4; break;
                    case 3: at = v + new Vector3Int(0, 0, -1); type = 1; forward = 2; up = 4; break;
                    case 4: type = 0; forward = 4; up = 0; break;
                    default: at = v + Vector3Int.up; type = 0; forward = 5; up = 2; break;
                }
                if (partId == GlassFloor && type != 0)
                    partId = IronWall;
                // The station's paint, a little weathered: light hull with a colored band around the
                // modules' middle row, light inside
                Color32 outside = Spec != null ? Spec.Outside : new Color32(150, 146, 140, 255);
                Color32 inside = Spec != null ? Spec.Inside : new Color32(80, 70, 58, 255);
                if (Spec != null && type != 0 && v.y % 4 == 2)
                    outside = Spec.Band;
                if (inner)
                    outside = inside;
                int wear = _random.Next(-14, 6);
                string front = $"{{\"x\":{Clamp(outside.r + wear)},\"y\":{Clamp(outside.g + wear)},\"z\":{Clamp(outside.b + wear)}}}";
                string back = $"{{\"x\":{Clamp(inside.r + wear / 2)},\"y\":{Clamp(inside.g + wear / 2)},\"z\":{Clamp(inside.b + wear / 2)}}}";
                return $"{{\"partId\":{partId},\"x\":{at.x},\"y\":{at.y},\"z\":{at.z},\"forwardOrientationId\":{forward},\"upOrientationId\":{up},\"voxelPartTypeId\":{type},\"FrontColor\":{front},\"BackColor\":{back}}}";
            }

            private static int Clamp(int value) => Math.Max(0, Math.Min(255, value));

            private static string Floating(int id, int settings, Vector3 coord, int forward, int up)
            {
                return string.Format(CultureInfo.InvariantCulture,
                    "{{\"id\":{0},\"settingsId\":{1},\"coord\":{{\"x\":{2},\"y\":{3},\"z\":{4}}},\"forwardOrientationId\":{5},\"upOrientationId\":{6}}}",
                    id, settings, coord.x, coord.y, coord.z, forward, up);
            }

            // What the crew left: building materials, a little fuel wood, sometimes a rare find
            private string Loot()
            {
                (int item, int min, int max, double chance)[] table =
                {
                    (101, 6, 20, 0.9), (102, 4, 16, 0.6), (112, 4, 20, 0.7), ((int)Items.ModItems.Titanium, 2, 8, 0.5),
                    (110, 6, 20, 0.4), (103, 1, 4, 0.25), (158, 1, 2, 0.1), (151, 1, 1, 0.15)
                };
                var slots = new List<string>();
                if (Spec != null)
                    table = table.Concat(Spec.Rare.Select(r => ((int)r.Item, r.Min, r.Max, r.Chance))).ToArray();
                foreach (var entry in table)
                {
                    if (slots.Count < 10 && _random.NextDouble() < entry.chance)
                        slots.Add($"{{\"hasItem\":true,\"itemId\":{entry.item},\"quantity\":{_random.Next(entry.min, entry.max + 1)}}}");
                }
                while (slots.Count < 10)
                    slots.Add("{\"hasItem\":false,\"itemId\":0,\"quantity\":0}");
                return string.Join(",", slots);
            }

            private static string EmptySlots() => string.Join(",", Enumerable.Repeat("{\"hasItem\":false,\"itemId\":0,\"quantity\":0}", 10));
        }
    }

    /// <summary>Host: the world's ships are being loaded (stations of an older build are left out, then rebuilt).</summary>
    [HarmonyLib.HarmonyPatch(typeof(Managers.Server.Saving.ServerStateLoader), "LoadWorldFromFileEntry")]
    internal static class StationsWorldLoadPatch
    {
        private static void Prefix() => AbandonedStations.BeginWorldLoad();

        private static void Finalizer() => AbandonedStations.EndWorldLoad();
    }

    [HarmonyLib.HarmonyPatch(typeof(ShipsServerTracker), nameof(ShipsServerTracker.LoadShip))]
    internal static class StationsLoadShipPatch
    {
        private static bool Prefix(IShipState shipToLoad)
        {
            try
            {
                return !AbandonedStations.ShouldReplace(shipToLoad);
            }
            catch (Exception e)
            {
                ErrorLog.Report("stations load", e);
                return true;
            }
        }
    }
}

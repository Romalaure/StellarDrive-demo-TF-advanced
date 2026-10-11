using System;
using System.Collections.Generic;
using System.Globalization;
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
                Rare = new[] { (Items.ModItems.ExoticMatter, 1, 3, 0.8), (Items.ModItems.QuantumCore, 1, 1, 0.35) },
                Build = b =>
                {
                    b.Module(-1, 0, -2, 1, 2, 2);
                    b.Module(-1, 3, -1, 1, 3, 1);
                    b.Entrance(0, 0, 2, 2);
                }
            },

            // ---- Wrecks drifting in the Erebos galaxy: torn ships with their cargo and dark matter

            new StationSpec
            {
                Name = "Épave du Vagabond", AnchorPlanet = 112, Offset = new Vector3(6e6f, 1.5e6f, -4e6f), Seed = 101, Breaches = 4f, DarkMatter = 12,
                Band = new Color32(40, 160, 150, 255), Outside = new Color32(120, 118, 112, 255), Inside = new Color32(90, 88, 84, 255),
                Rare = new[] { (Items.ModItems.Meteorite, 2, 6, 0.6), (Items.ModItems.QuantumCore, 1, 1, 0.25), (Items.ModItems.NexoranAlloy, 2, 4, 0.35) },
                Build = b =>
                {
                    // A freighter: cockpit, a long hold, the engine section broken off behind it
                    b.Module(-1, 0, 9, 1, 2, 11);
                    b.Module(-2, 0, -3, 2, 3, 8);
                    b.Module(-2, 0, -9, 2, 2, -6);
                    b.Entrance(0, 0, -3, 3);
                }
            },
            new StationSpec
            {
                Name = "Épave du Léviathan", AnchorPlanet = 114, Offset = new Vector3(-8e6f, -2e6f, 5e6f), Seed = 117, Breaches = 5f, DarkMatter = 20,
                Band = new Color32(140, 60, 200, 255), Outside = new Color32(70, 70, 80, 255), Inside = new Color32(100, 96, 110, 255),
                Rare = new[] { (Items.ModItems.ExoticMatter, 1, 2, 0.4), (Items.ModItems.QuantumCore, 1, 2, 0.35), (Items.ModItems.Meteorite, 3, 8, 0.6), (Items.ModItems.Artifact, 1, 1, 0.3) },
                Build = b =>
                {
                    // A capital ship's spine with a broken bridge tower and side bays
                    b.Module(-2, 0, -14, 2, 3, 14);
                    b.Module(-1, 4, 6, 1, 7, 9);
                    b.Module(3, 0, -4, 7, 2, 2);
                    b.Module(-7, 0, -8, -3, 2, -2);
                    b.Entrance(0, 0, 14, 2);
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

        public static void OnSceneLoaded(bool inGame)
        {
            _done = false;
            Shelters.Clear();
            _checkAt = inGame ? Time.unscaledTime + 8f : -1f;
        }

        /// <summary>Host: once the world has loaded, creates the stations it does not have yet.</summary>
        public static void Update()
        {
            if (_done || _checkAt < 0f || Time.unscaledTime < _checkAt || !Settings.AbandonedStations.Value)
                return;
            if (!GameServices.IsServer)
            {
                _done = true;
                return;
            }
            _done = true;
            ShipsServerTracker ships = ServiceLocator.GetService<ShipsServerTracker>();
            if (ships == null)
                return;
            var existing = new HashSet<string>(ships.Ships.SelectMany(s => s.GetAllStatefulParts<BeaconState>()).Select(p => ((BeaconState)p.State).Name));
            foreach (StationSpec spec in All)
            {
                if (existing.Contains(spec.Name))
                    continue;
                try
                {
                    Spawn(spec, ships);
                }
                catch (Exception e)
                {
                    AdvancedMod.Log.Error($"stations: could not create {spec.Name}: {e}");
                }
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
            (uint spaceId, ISpace _) = spaces.GetOrCreateSpaceForPosition(new FixedWorldSpace { Position = position });
            state.Id = ids.CreateUniqueIdForObject();
            state.SpaceId = spaceId;
            state.Position = Vector3.zero;
            state.Velocity = Vector3.zero;
            state.Rotation = Quaternion.Euler(spec.Seed * 7f % 30f, spec.Seed * 13f % 360f, spec.Seed * 3f % 20f);
            state.RelativeSpaceId = spaces.GetOrCreateShipRelativeSpace(state.Id);
            ships.LoadShip(state);
            AdvancedMod.Log.Msg($"stations: created {spec.Name} (ship {state.Id}, {state.HullParts.Length} hull parts, {state.StatefulParts.Length} parts)");
        }

        /// <summary>Hull and parts of a station, written in the game's own ship save format.</summary>
        internal sealed class Blueprint
        {
            // Game part ids: iron floor and wall, glass wall and floor, beacon, chest, chemical thruster
            private const int IronFloor = 1, IronWall = 2, GlassWall = 7, GlassFloor = 36;
            private const int Beacon = 38, Chest = 30, Thruster = 19, Bed = 43, FluidTank = 23;
            private const uint Oxygen = 108, Ethanol = 111;

            public StationSpec Spec;

            private readonly HashSet<Vector3Int> _inside = new HashSet<Vector3Int>();
            private readonly HashSet<Vector3Int> _openings = new HashSet<Vector3Int>();
            private readonly List<(int x0, int y0, int z0, int x1, int y1, int z1)> _modules = new List<(int, int, int, int, int, int)>();
            private readonly System.Random _random;

            public Blueprint(int seed)
            {
                _random = new System.Random(seed);
            }

            /// <summary>A box of interior voxels (inclusive bounds). Touching modules open onto each other.</summary>
            public void Module(int x0, int y0, int z0, int x1, int y1, int z1)
            {
                _modules.Add((x0, y0, z0, x1, y1, z1));
                for (int x = x0; x <= x1; x++)
                    for (int y = y0; y <= y1; y++)
                        for (int z = z0; z <= z1; z++)
                            _inside.Add(new Vector3Int(x, y, z));
            }

            /// <summary>A breached doorway, 1 wide and 2 high, on the outer face next to this interior voxel.</summary>
            public void Entrance(int x, int y, int z, int face)
            {
                _openings.Add(new Vector3Int(x, y, z) * 10 + new Vector3Int(face, 0, 0));
                _openings.Add(new Vector3Int(x, y + 1, z) * 10 + new Vector3Int(face, 0, 0));
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
                        if (_random.NextDouble() < (wall ? 0.05 : 0.03) * (Spec != null ? Spec.Breaches : 1f))
                            continue;
                        bool window = wall && v.y % 4 == 1 && _random.NextDouble() < 0.35;
                        hull.Add(HullFace(v, face, window ? GlassWall : (face == 4 && _random.NextDouble() < 0.06 ? GlassFloor : wall ? IronWall : IronFloor)));
                    }
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
                        // A loot case now and then instead of a plain chest (opened: no key needed)
                        int container = _random.NextDouble() < 0.4 ? 400 + _random.Next(0, 5) * 10 + _random.Next(0, 7) : Chest;
                        floating.Add(Floating(nextPart, container, new Vector3(x, m.y0 - 0.5f + 0.04f, z), 2, 4));
                        storages.Add($"{{\"partId\":{nextPart},\"slots\":[{Loot()}]}}");
                        nextPart++;
                    }
                }

                // Crew quarters and supplies: bunks in the end modules, oxygen and ethanol tanks in the core
                var tanks = new List<string>();
                foreach (var m in _modules.Skip(1).Where(m => (m.x1 - m.x0 + 1) * (m.z1 - m.z0 + 1) >= 12 && m.y0 == 0))
                {
                    floating.Add(Floating(nextPart++, Bed, new Vector3(m.x1, m.y0 - 0.5f + 0.04f, m.z0), 2, 4));
                    if (_random.NextDouble() < 0.5)
                        floating.Add(Floating(nextPart++, Bed, new Vector3(m.x0, m.y0 - 0.5f + 0.04f, m.z0), 2, 4));
                }
                if (Spec != null && Spec.DarkMatter > 0)
                {
                    floating.Add(Floating(nextPart, FluidTank, new Vector3(Mathf.Round(cx), core.y0 - 0.5f + 0.04f, core.z0), 2, 4));
                    tanks.Add(string.Format(CultureInfo.InvariantCulture, "{{\"partId\":{0},\"fluidItemId\":{1},\"fluidQuantity\":{2:0.0}}}", nextPart, Items.ModItems.DarkMatter, Spec.DarkMatter));
                    nextPart++;
                }
                foreach (uint fluid in new[] { Oxygen, Ethanol })
                {
                    int x = fluid == Oxygen ? core.x0 : core.x1;
                    floating.Add(Floating(nextPart, FluidTank, new Vector3(x, core.y0 - 0.5f + 0.04f, core.z1), 2, 4));
                    tanks.Add(string.Format(CultureInfo.InvariantCulture, "{{\"partId\":{0},\"fluidItemId\":{1},\"fluidQuantity\":{2:0.0}}}", nextPart, fluid, 10 + _random.Next(0, 40)));
                    nextPart++;
                }

                // Dead thrusters on the underside of the big modules
                foreach (var m in _modules.Where(m => (m.x1 - m.x0) >= 4))
                {
                    floating.Add(Floating(nextPart, Thruster, new Vector3(m.x0 + 0.5f, m.y0 - 0.5f - 0.04f, (m.z0 + m.z1) * 0.5f), 5, 0));
                    thrusters.Add($"{{\"partId\":{nextPart},\"fluidItemId\":100,\"fluidQuantity\":0.0}}");
                    nextPart++;
                }

                var sb = new StringBuilder();
                sb.Append("{\"rootShipId\":1,\"cables\":[],\"ships\":[{");
                sb.Append("\"id\":1,\"relativeSpaceId\":0,\"spaceId\":0,");
                sb.Append("\"position\":{\"x\":0,\"y\":0,\"z\":0},\"velocity\":{\"x\":0,\"y\":0,\"z\":0},\"rotation\":{\"x\":0,\"y\":0,\"z\":0,\"w\":1},");
                sb.Append("\"rooms\":[],");
                sb.Append("\"hullParts\":[").Append(string.Join(",", hull)).Append("],");
                sb.Append("\"statefulFloatingParts\":[").Append(string.Join(",", floating)).Append("],");
                sb.Append("\"statefulHullParts\":[],");
                sb.Append("\"thrustersStates\":[").Append(string.Join(",", thrusters)).Append("],");
                sb.Append("\"storageStates\":[").Append(string.Join(",", storages)).Append("],");
                sb.Append("\"beaconStates\":[{\"partId\":").Append(beaconId).Append(",\"name\":\"").Append(beaconName.Replace("\"", "'")).Append("\"}],");
                sb.Append("\"fluidTankStates\":[").Append(string.Join(",", tanks)).Append("],");
                foreach (string empty in new[] { "leverStates", "warpDriveStates", "dockingDoorStates", "targeterStates", "mathGateStates",
                    "unaryMathGateStates", "memoryRegisterStates", "constantStates", "transmitterStates", "rotorStates", "electrolyzerStates",
                    "woodThrusterStates", "woodGeneratorStates", "ethanolDistillerStates" })
                    sb.Append('"').Append(empty).Append("\":[],");
                sb.Length--;
                sb.Append("}]}");
                return sb.ToString();
            }

            // Faces: 0 +x, 1 -x, 2 +z, 3 -z, 4 -y (floor), 5 +y (ceiling)
            private static Vector3Int Direction(int face)
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
            private string HullFace(Vector3Int v, int face, int partId)
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
                    (101, 6, 20, 0.9), (102, 4, 16, 0.6), (112, 4, 20, 0.7), (104, 3, 14, 0.5),
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
        }
    }
}

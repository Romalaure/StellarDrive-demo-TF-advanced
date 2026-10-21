using System;
using System.Collections.Generic;
using System.Linq;
using Core.Services;
using Core.Values;
using HarmonyLib;
using Ships;
using Ships.Interface.Model.Parts.State;
using Ships.Services;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;
using WorldTracking.Interface.Model;
using WorldTracking.Interface.Model.Spaces;
using WorldTracking.Interface.Model.Spaces.Common;
using WorldTracking.Interface.Services;
using WorldTracking.Spaces;
using WorldTracking.Spaces.Model;

namespace StellarDriveDemoTFAdvanced.Worlds
{
    /// <summary>
    /// Host: makes the stations solid for whoever comes near. Each space has its own physics scene
    /// and the game only ever merges spaces of one kind (orbits with orbits, trajectories with
    /// trajectories). A station sits in a fixed world space, so a ship or a player coming from an
    /// orbit stayed in another scene and went through it. A free-flight space with an object
    /// within <see cref="JoinDistance"/> of a station is moved, whole, into the station's space;
    /// an object further than <see cref="LeaveDistance"/> from it flies on in a trajectory of its
    /// own (which the game turns into an orbit inside a planet's influence).
    /// </summary>
    [HarmonyPatch(typeof(SpacesServerUpdater), nameof(SpacesServerUpdater.UpdateSpaces))]
    internal static class StationSpaces
    {
        public const float JoinDistance = 1500f;
        public const float LeaveDistance = 4000f;
        private static float _nextCheck;
        private static float _nextStations;
        private static readonly List<TrackedShipServer> Stations = new List<TrackedShipServer>();

        private static void Prefix()
        {
            if (Time.unscaledTime < _nextCheck || DevHarness.Off("stationspaces"))
                return;
            _nextCheck = Time.unscaledTime + 0.25f;
            try
            {
                Update();
            }
            catch (Exception e)
            {
                _nextCheck = Time.unscaledTime + 5f;
                ErrorLog.Report("station spaces", e);
            }
        }

        private static void Update()
        {
            var spaces = ServiceLocator.GetService<SpacesServer>();
            var ships = ServiceLocator.GetService<ShipsServerTracker>();
            var transpose = ServiceLocator.GetService<IWorldTransposeServer>();
            var time = ServiceLocator.GetService<IWorldTimeProviderServer>();
            if (spaces == null || ships == null || transpose == null || time == null)
                return;
            if (Time.unscaledTime >= _nextStations)
            {
                _nextStations = Time.unscaledTime + 10f;
                var names = new HashSet<string>(AbandonedStations.All.Select(s => s.Name));
                Stations.Clear();
                Stations.AddRange(ships.Ships.Where(s => s.GetAllStatefulParts<BeaconState>().Any(p => names.Contains(((BeaconState)p.State).Name))));
            }
            var stationSpaces = new HashSet<uint>(Stations.Select(s => s.SpaceId));
            foreach (TrackedShipServer station in Stations)
            {
                if (!spaces.TryGetSpaceServerState(station.SpaceId, out ISpaceServerState home) || !(home is FixedSpaceServerState fixedHome))
                    continue;
                // In: a free-flight space (not another station's) with something near the station
                foreach (ISpaceServerState other in spaces.SpaceStates.ToList())
                {
                    if (stationSpaces.Contains(other.Id) || !(other is OrbitSpaceServerState || other is TrajectorySpaceServerState || other is FixedSpaceServerState))
                        continue;
                    if (!other.WorldObjects.Any(o => (transpose.Position(other.Id, home.GenericSpace, o.Position) - station.Position).magnitude < JoinDistance))
                        continue;
                    AdvancedMod.Log.Msg($"station spaces: {other.WorldObjects.Count} object(s) of space {other.Id} join the station {station.Id}'s space {home.Id}");
                    spaces.TransferWorldSpaceObjects(other, home);
                }
                // Out: whatever went far from it; docked ships go with the ship they are docked to
                // (a docked ship alone in another space breaks the game's physics scenes)
                foreach (ITrackedWorldObjectServer item in home.WorldObjects.ToList())
                {
                    if (item.Id == station.Id || (item.Position - station.Position).magnitude < LeaveDistance)
                        continue;
                    uint root = ships.TryGetTrackedRootShip(item.Id, out TrackedShipServer rootShip) ? rootShip.Id : item.Id;
                    if (root != item.Id || !home.WorldObjects.Contains(item))
                        continue;
                    PreciseWorldPosition at = fixedHome.GetObjectWorldPos(item);
                    ISpace free = item.IsCheatFlying
                        ? (ISpace)new FixedWorldSpace { Position = at }
                        : new TrajectorySpace { InitialPosition = at, Velocity = (Vector3Double)item.Velocity, WorldTime = time.CurrentWorldTime };
                    ISpaceServerState to = spaces.CreateSpace(free);
                    foreach (ITrackedWorldObjectServer member in home.WorldObjects.Where(o => o.Id == root || (ships.TryGetTrackedRootShip(o.Id, out TrackedShipServer r) && r.Id == root)).ToList())
                        spaces.TransferObjectToSpace(member, to);
                    AdvancedMod.Log.Msg($"station spaces: {item.Name} {item.Id} leaves the station {station.Id}'s space");
                }
            }
        }
    }
}

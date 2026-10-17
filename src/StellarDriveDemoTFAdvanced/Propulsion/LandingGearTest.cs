using System.Collections;
using System.Globalization;
using System.Linq;
using System.Text;
using Core.Services;
using Managers.Client.Utils;
using Planets;
using Saving.Services.Converters;
using Saving.Services.Model.State;
using Ships;
using Ships.Cables;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.State;
using Ships.Services;
using StellarDriveDemoTFAdvanced.Common;
using StellarDriveDemoTFAdvanced.Worlds;
using UnityEngine;
using WorldTracking.Services;
using WorldTracking.Spaces;

namespace StellarDriveDemoTFAdvanced.Propulsion
{
    /// <summary>
    /// Development check (DevLandingGearTest = planet id): the player is set down on that planet,
    /// a 6 × 5 m floor with a landing gear under each corner (deployed) is dropped 5 m above the
    /// ground, the log follows how it lands and settles on its legs, then the gear is retracted
    /// and it should come down onto its hull. Photographs on its legs and after.
    /// </summary>
    internal static class LandingGearTest
    {
        private const int Floor = 1, Constant = 46;

        public static void Install()
        {
            DevHarness.Steps.Add((22f, "landing gear test", () =>
            {
                if (Settings.DevLandingGearTest.Value != 0)
                    MelonLoader.MelonCoroutines.Start(Run());
            }));
        }

        private static IEnumerator Run()
        {
            Planet planet = PlanetFactory.Planets.FirstOrDefault(p => p.Id == (ulong)Settings.DevLandingGearTest.Value)
                ?? Object.FindObjectsByType<Planet>(FindObjectsSortMode.None).FirstOrDefault(p => p.Id == (ulong)Settings.DevLandingGearTest.Value);
            if (planet == null || !PlanetVisit.TryFindLand(planet, out Vector3 up))
            {
                AdvancedMod.Log.Warning("dev: landing gear test: no planet or land");
                yield break;
            }
            PlanetVisit.Place(planet, up, 0f);
            yield return StationVisit.Wait(10f);
            var players = ServiceLocator.GetService<Players.Services.PlayersServerTracker>();
            var player = players?.ActivePlayers.FirstOrDefault();
            SpacesServer spaces = ServiceLocator.GetService<SpacesServer>();
            ShipsServerTracker ships = ServiceLocator.GetService<ShipsServerTracker>();
            var ids = ServiceLocator.GetService<WorldObjectsIdsServerTracker>();
            if (player == null || spaces == null || ships == null || ids == null)
                yield break;
            Vector3 upward = player.Rotation * Vector3.up;
            Vector3 start = player.Position + upward * 5f + player.Rotation * Vector3.forward * 8f;
            var data = JsonUtility.FromJson<ShipImportExportUtils.ShipImportExportData>(Json());
            SavedShipState state = new ShipsConverter().ConvertToState(data.ships).States.Cast<SavedShipState>().Single();
            state.Id = ids.CreateUniqueIdForObject();
            state.SpaceId = player.SpaceId;
            state.Position = start;
            state.Rotation = player.Rotation;
            state.Velocity = Vector3.zero;
            state.RelativeSpaceId = spaces.GetOrCreateShipRelativeSpace(state.Id);
            ships.LoadShip(state);
            uint id = state.Id;
            var cables = ServiceLocator.GetService<Ships.Cables.CablesServerTracker>();
            foreach (var cable in CablesConverter.ConvertToState(data.cables))
            {
                var c = cable.State;
                var pa = c.PlugA; var pb = c.PlugB;
                var sa = pa.EntityPartSocket; sa.EntityId = id; pa.EntityPartSocket = sa;
                var sb = pb.EntityPartSocket; sb.EntityId = id; pb.EntityPartSocket = sb;
                c.PlugA = pa;
                c.PlugB = pb;
                cables?.AddCable(c);
            }
            AdvancedMod.Log.Msg($"dev: landing gear test ship {id} dropped 5 m above the ground");
            for (int i = 0; i < 24; i++)
            {
                yield return StationVisit.Wait(0.5f);
                Report(ships, id, start, upward, $"t={0.5f * (i + 1):0.0} s");
                if (i == 19)
                    yield return Parts.Showcase.Photograph(id, "gear_down");
            }
            // The constants on the legs' sockets to 0: the gear retracts
            TrackedShipServer dropped = ships.GetShip(id);
            if (dropped != null)
                foreach (StatefulPart constant in dropped.GetAllStatefulParts<ConstantState>().Where(p => p.Settings.id == Constant).ToList())
                    dropped.UpdatePartState(constant.Id, (ConstantState)constant.State with { ConstantValue = 0.0 });
            for (int i = 0; i < 10; i++)
            {
                yield return StationVisit.Wait(0.5f);
                Report(ships, id, start, upward, $"retracted t={0.5f * (i + 1):0.0} s");
            }
            yield return Parts.Showcase.Photograph(id, "gear_up");
        }

        private static void Report(ShipsServerTracker ships, uint id, Vector3 start, Vector3 upward, string when)
        {
            TrackedShipServer ship = ships.GetShip(id);
            if (ship == null)
            {
                AdvancedMod.Log.Msg($"dev: landing gear test {when}: ship gone");
                return;
            }
            var physics = ship.PhysicsState.LocalPhysicsState;
            float height = Vector3.Dot(physics.Position - start, upward);
            float tilt = Vector3.Angle(physics.Rotation * Vector3.up, upward);
            var connections = ServiceLocator.GetService<ConnectionsServerTracker>();
            string legs = string.Join(",", ship.GetAllStatefulParts<ConstantState>().Where(p => p.Settings.id == LandingGear.Id).Select(p =>
            {
                var socket = new Ships.Interface.Model.Parts.Common.EntityPartSocket(id, p.Id, LandingGear.CommandSocket);
                string wire = connections == null ? "?" : connections.IsSocketPhysicallyOccupied(socket) ? $"wired {connections.GetSignalValue(socket):0.#}" : "unwired";
                return (LandingGear.Deployed(p.State) ? "out" : "in") + " " + wire;
            }));
            AdvancedMod.Log.Msg($"dev: landing gear test {when}: height {height:0.00} m from the drop point, vertical speed {Vector3.Dot(physics.Velocity, upward):0.00} m/s, speed {physics.Velocity.magnitude:0.00}, tilt {tilt:0.0}°, legs {legs}");
        }

        // A floor of 6 × 5 cells, its bottom 0.5 m under its origin; a gear under each corner, pointing down
        private static string Json()
        {
            var hull = new StringBuilder();
            for (int x = -2; x <= 3; x++)
            {
                for (int z = 1; z <= 5; z++)
                {
                    if (hull.Length > 0)
                        hull.Append(',');
                    hull.Append($"{{\"partId\":{Floor},\"x\":{x},\"y\":0,\"z\":{z},\"forwardOrientationId\":4,\"upOrientationId\":0,\"voxelPartTypeId\":0,\"FrontColor\":{{\"x\":150,\"y\":150,\"z\":150}},\"BackColor\":{{\"x\":120,\"y\":120,\"z\":120}}}}");
                }
            }
            string Part(int id, float x, float z) => string.Format(CultureInfo.InvariantCulture,
                "{{\"id\":{0},\"settingsId\":{1},\"coord\":{{\"x\":{2},\"y\":{3},\"z\":{4}}},\"forwardOrientationId\":0,\"upOrientationId\":5}}", id, LandingGear.Id, x, -0.5f, z);
            string Box(int id, float x, float z) => string.Format(CultureInfo.InvariantCulture,
                "{{\"id\":{0},\"settingsId\":{1},\"coord\":{{\"x\":{2},\"y\":-0.46,\"z\":{3}}},\"forwardOrientationId\":2,\"upOrientationId\":4}}", id, Constant, x, z);
            string parts = string.Join(",", Part(1, -1.5f, 1.5f), Part(2, 2.5f, 1.5f), Part(3, -1.5f, 4.5f), Part(4, 2.5f, 4.5f),
                Box(5, -1.5f, 2.5f), Box(6, 2.5f, 2.5f), Box(7, -1.5f, 3.5f), Box(8, 2.5f, 3.5f));
            string Cable(int cable, int gear) => $"{{\"id\":{cable},\"type\":0,\"plugA\":{{\"shipId\":1,\"partId\":{gear + 4},\"socketId\":0}},\"plugB\":{{\"shipId\":1,\"partId\":{gear},\"socketId\":1}}}}";
            var sb = new StringBuilder();
            sb.Append("{\"rootShipId\":1,\"cables\":[").Append(string.Join(",", Enumerable.Range(1, 4).Select(g => Cable(g, g)))).Append("],\"ships\":[{");
            sb.Append("\"id\":1,\"relativeSpaceId\":0,\"spaceId\":0,\"position\":{\"x\":0,\"y\":0,\"z\":0},\"velocity\":{\"x\":0,\"y\":0,\"z\":0},\"rotation\":{\"x\":0,\"y\":0,\"z\":0,\"w\":1},");
            sb.Append("\"rooms\":[],\"hullParts\":[").Append(hull).Append("],\"statefulFloatingParts\":[").Append(parts).Append("],\"statefulHullParts\":[],\"thrustersStates\":[],\"storageStates\":[],\"fluidTankStates\":[],");
            sb.Append("\"constantStates\":[").Append(string.Join(",", Enumerable.Range(1, 8).Select(i => $"{{\"partId\":{i},\"constantValue\":{"1.0"}}}"))).Append("],");
            foreach (string empty in new[] { "dockingDoorStates", "beaconStates", "leverStates", "warpDriveStates", "targeterStates", "mathGateStates",
                         "unaryMathGateStates", "memoryRegisterStates", "transmitterStates", "rotorStates", "electrolyzerStates",
                         "woodThrusterStates", "woodGeneratorStates", "ethanolDistillerStates" })
                sb.Append('"').Append(empty).Append("\":[],");
            sb.Length--;
            sb.Append("}]}");
            return sb.ToString();
        }
    }
}

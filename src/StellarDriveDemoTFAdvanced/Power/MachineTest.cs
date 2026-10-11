using System.Collections;
using System.Globalization;
using System.Linq;
using System.Text;
using Core.Services;
using Core.Values;
using Managers.Client.Utils;
using Saving.Services.Converters;
using Saving.Services.Model.State;
using Ships;
using Ships.Cables;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.State;
using Ships.Services;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;
using Ships.Interface.Model.Parts.Common;
using WorldTracking.Services;
using WorldTracking.Interface.Model.Spaces;
using WorldTracking.Spaces;

namespace StellarDriveDemoTFAdvanced.Power
{
    /// <summary>
    /// Development check (DevMachineTest): a small wired test ship appears 40 m above the player
    /// and falls: a quantum reactor powering a force field generator (oxygen from a tank) and a
    /// cryonite condenser filling an empty tank. The log follows the field's charge, the ship's
    /// part count through the crash, and the oxygen made.
    /// </summary>
    internal static class MachineTest
    {
        private const int Floor = 1, Tank = 23;

        public static void Install()
        {
            DevHarness.Steps.Add((26f, "machine test", () =>
            {
                if (Settings.DevMachineTest.Value)
                    MelonLoader.MelonCoroutines.Start(Run());
            }));
        }

        private static IEnumerator Run()
        {
            var players = ServiceLocator.GetService<Players.Services.PlayersServerTracker>();
            var player = players?.ActivePlayers.FirstOrDefault();
            SpacesServer spaces = ServiceLocator.GetService<SpacesServer>();
            ShipsServerTracker ships = ServiceLocator.GetService<ShipsServerTracker>();
            CablesServerTracker cables = ServiceLocator.GetService<CablesServerTracker>();
            var ids = ServiceLocator.GetService<WorldObjectsIdsServerTracker>();
            if (player == null || spaces == null || ships == null || cables == null || ids == null)
            {
                AdvancedMod.Log.Warning("dev: machine test: services missing");
                yield break;
            }
            var data = JsonUtility.FromJson<ShipImportExportUtils.ShipImportExportData>(Json());
            SavedShipState state = new ShipsConverter().ConvertToState(data.ships).States.Cast<SavedShipState>().Single();
            state.Id = ids.CreateUniqueIdForObject();
            state.SpaceId = player.SpaceId;
            // The player stands upright: their frame gives the local up and a side
            Vector3 up = player.Rotation * Vector3.up;
            state.Position = player.Position + up * 40f + player.Rotation * Vector3.right * 12f;
            state.Rotation = player.Rotation;
            state.Velocity = Vector3.zero;
            state.RelativeSpaceId = spaces.GetOrCreateShipRelativeSpace(state.Id);
            ships.LoadShip(state);
            foreach (var cable in CablesConverter.ConvertToState(data.cables))
            {
                CableState c = cable.State;
                CablePlugSocket a = c.PlugA, b = c.PlugB;
                var sa = a.EntityPartSocket; sa.EntityId = state.Id; a.EntityPartSocket = sa;
                var sb = b.EntityPartSocket; sb.EntityId = state.Id; b.EntityPartSocket = sb;
                c.PlugA = a;
                c.PlugB = b;
                cables.AddCable(c);
            }
            AdvancedMod.Log.Msg($"dev: machine test ship {state.Id} dropped, {state.StatefulParts.Length} parts");
            for (int i = 0; i < 14; i++)
            {
                float until = Time.unscaledTime + 2f;
                while (Time.unscaledTime < until)
                    yield return null;
                TrackedShipServer ship = ships.GetShip(state.Id);
                if (ship == null)
                {
                    AdvancedMod.Log.Msg("dev: machine test ship gone");
                    yield break;
                }
                string Tank(ushort part) => ship.GetAllStatefulParts<FluidTankState>().Where(p => p.Id == part).Select(p => ((FluidTankState)p.State)).Select(t => $"{t.FluidQuantity:0.00} ({t.FluidFlow:0.###})").FirstOrDefault()
                    ?? ship.GetAllStatefulParts<ConstantState>().Where(p => p.Id == part).Select(p => ((ConstantState)p.State).ConstantValue.ToString("0.0")).FirstOrDefault() ?? "gone";
                AdvancedMod.Log.Msg($"dev: machine test t={2 * (i + 1)} s: parts {ship.PartCount}, reactor {Tank(1)}, field charge/status {Tank(2)}, condenser {Tank(3)}, oxygen tank {Tank(4)}, filled tank {Tank(5)}, bubble size {Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).FirstOrDefault(b => b.name == "TFA_ForceFieldBubble")?.localScale.ToString("F2")}");
                if (i == 5)
                    yield return Parts.Showcase.Photograph(state.Id, "machines");
            }
        }

        private static string Json()
        {
            var hull = new StringBuilder();
            for (int x = -2; x <= 2; x++)
            {
                for (int z = -2; z <= 2; z++)
                {
                    if (hull.Length > 0)
                        hull.Append(',');
                    hull.Append($"{{\"partId\":{Floor},\"x\":{x},\"y\":0,\"z\":{z},\"forwardOrientationId\":4,\"upOrientationId\":0,\"voxelPartTypeId\":0,\"FrontColor\":{{\"x\":150,\"y\":150,\"z\":150}},\"BackColor\":{{\"x\":120,\"y\":120,\"z\":120}}}}");
                }
            }
            string Part(int id, int settings, float x, float z) => string.Format(CultureInfo.InvariantCulture,
                "{{\"id\":{0},\"settingsId\":{1},\"coord\":{{\"x\":{2},\"y\":-0.46,\"z\":{3}}},\"forwardOrientationId\":2,\"upOrientationId\":4}}", id, settings, x, z);
            string parts = string.Join(",", Part(1, PowerCatalog.ReactorId, -1.5f, 0f), Part(2, PowerCatalog.ShieldId, 1.5f, 0f),
                Part(3, PowerCatalog.CondenserId, 0f, -1.5f), Part(4, Tank, 1.5f, 1.8f), Part(5, Tank, -1.5f, -1.8f));
            string constants = "{\"partId\":2,\"constantValue\":100.0},{\"partId\":3,\"constantValue\":0.0}";
            string tanks = "{\"partId\":1,\"fluidItemId\":0,\"fluidQuantity\":0.0},"
                + "{\"partId\":4,\"fluidItemId\":108,\"fluidQuantity\":40.0},{\"partId\":5,\"fluidItemId\":0,\"fluidQuantity\":0.0}";
            // Power: reactor 0 -> field 0, reactor 1 -> condenser 0; oxygen: tank 4 -> field 3; condenser 3 -> tank 5
            string Cable(int id, int type, int partA, int socketA, int partB, int socketB) =>
                $"{{\"id\":{id},\"type\":{type},\"plugA\":{{\"shipId\":1,\"partId\":{partA},\"socketId\":{socketA}}},\"plugB\":{{\"shipId\":1,\"partId\":{partB},\"socketId\":{socketB}}}}}";
            string cablesJson = string.Join(",", Cable(1, 2, 1, 0, 2, 1), Cable(2, 2, 1, 1, 3, 1), Cable(3, 1, 4, 0, 2, 3), Cable(4, 1, 3, 3, 5, 0));
            var sb = new StringBuilder();
            sb.Append("{\"rootShipId\":1,\"cables\":[").Append(cablesJson).Append("],\"ships\":[{");
            sb.Append("\"id\":1,\"relativeSpaceId\":0,\"spaceId\":0,\"position\":{\"x\":0,\"y\":0,\"z\":0},\"velocity\":{\"x\":0,\"y\":0,\"z\":0},\"rotation\":{\"x\":0,\"y\":0,\"z\":0,\"w\":1},");
            sb.Append("\"rooms\":[],\"hullParts\":[").Append(hull).Append("],\"statefulFloatingParts\":[").Append(parts).Append("],\"statefulHullParts\":[],\"thrustersStates\":[],\"storageStates\":[],");
            sb.Append("\"fluidTankStates\":[").Append(tanks).Append("],");
            sb.Append("\"constantStates\":[").Append(constants).Append("],");
            foreach (string empty in new[] { "beaconStates", "leverStates", "warpDriveStates", "dockingDoorStates", "targeterStates", "mathGateStates",
                         "unaryMathGateStates", "memoryRegisterStates", "transmitterStates", "rotorStates", "electrolyzerStates",
                         "woodThrusterStates", "woodGeneratorStates", "ethanolDistillerStates" })
                sb.Append('"').Append(empty).Append("\":[],");
            sb.Length--;
            sb.Append("}]}");
            return sb.ToString();
        }
    }
}

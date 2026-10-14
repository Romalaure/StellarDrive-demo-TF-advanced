using System.Collections;
using System.Globalization;
using System.Linq;
using System.Text;
using Core.Services;
using Core.Values;
using Items.Model;
using Managers.Client.Utils;
using Saving.Services.Converters;
using Saving.Services.Model.State;
using Ships;
using Ships.Cables;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;
using Ships.Services;
using StellarDriveDemoTFAdvanced.Common;
using StellarDriveDemoTFAdvanced.Items;
using UnityEngine;
using WorldTracking.Services;
using WorldTracking.Interface.Model.Spaces;
using WorldTracking.Spaces;

namespace StellarDriveDemoTFAdvanced.Power
{
    /// <summary>
    /// Development check (DevNuclearTest = engine part id): a wired test ship appears above
    /// the player (300 m up, so the engine fires in flight): an enrichment centrifuge loaded with uranium ore filling an empty tank, a
    /// nuclear reactor fed from a full tank and powering the centrifuge, and the engine, its
    /// nozzle down, whose propellant lines are plugged in after 2 s. The log follows the ore,
    /// the fuel made and burnt, the engine's thrust level and the ship's climb.
    /// </summary>
    internal static class NuclearTest
    {
        private const int Floor = 1, Tank = 23;

        public static void Install()
        {
            DevHarness.Steps.Add((26f, "nuclear test", () =>
            {
                if (Settings.DevNuclearTest.Value > 0)
                    MelonLoader.MelonCoroutines.Start(Run((ushort)Settings.DevNuclearTest.Value));
            }));
        }

        private static IEnumerator Run(ushort engineId)
        {
            var players = ServiceLocator.GetService<Players.Services.PlayersServerTracker>();
            var player = players?.ActivePlayers.FirstOrDefault();
            SpacesServer spaces = ServiceLocator.GetService<SpacesServer>();
            ShipsServerTracker ships = ServiceLocator.GetService<ShipsServerTracker>();
            CablesServerTracker cables = ServiceLocator.GetService<CablesServerTracker>();
            var ids = ServiceLocator.GetService<WorldObjectsIdsServerTracker>();
            if (player == null || spaces == null || ships == null || cables == null || ids == null
                || !Propulsion.EngineCatalog.TryGet(engineId, out Propulsion.EngineSpec spec))
            {
                AdvancedMod.Log.Warning("dev: nuclear test: services or engine missing");
                yield break;
            }
            var data = JsonUtility.FromJson<ShipImportExportUtils.ShipImportExportData>(Json(spec));
            SavedShipState state = new ShipsConverter().ConvertToState(data.ships).States.Cast<SavedShipState>().Single();
            state.Id = ids.CreateUniqueIdForObject();
            state.SpaceId = player.SpaceId;
            Vector3 up = player.Rotation * Vector3.up;
            state.Position = player.Position + up * 300f + player.Rotation * Vector3.right * 12f;
            state.Rotation = player.Rotation;
            state.Velocity = Vector3.zero;
            state.RelativeSpaceId = spaces.GetOrCreateShipRelativeSpace(state.Id);
            ships.LoadShip(state);
            void Plug(CableState c)
            {
                CablePlugSocket a = c.PlugA, b = c.PlugB;
                var sa = a.EntityPartSocket; sa.EntityId = state.Id; a.EntityPartSocket = sa;
                var sb = b.EntityPartSocket; sb.EntityId = state.Id; b.EntityPartSocket = sb;
                c.PlugA = a;
                c.PlugB = b;
                cables.AddCable(c);
            }
            var all = CablesConverter.ConvertToState(data.cables).Select(c => c.State).ToList();
            // The engine's propellant lines (the last two) wait
            foreach (CableState c in all.Take(3))
                Plug(c);
            TrackedShipServer ship = ships.GetShip(state.Id);
            if (ship != null && ship.TryGetStatefulPart(1, out StatefulPart centrifuge) && centrifuge.State is EthanolDistillerState distiller)
                ship.UpdatePartState(1, distiller with { InventorySlots = new[] { new InventorySlot { HasItem = true, ItemId = ModItems.Uranium, Quantity = 20 } } });
            AdvancedMod.Log.Msg($"dev: nuclear test ship {state.Id} dropped with {spec.Label}, {state.StatefulParts.Length} parts");
            Vector3 start = state.Position;
            float lastClimb = 0f;
            for (int i = 0; i < 16; i++)
            {
                float until = Time.unscaledTime + 2f;
                while (Time.unscaledTime < until)
                    yield return null;
                ship = ships.GetShip(state.Id);
                if (ship == null)
                {
                    AdvancedMod.Log.Msg("dev: nuclear test ship gone");
                    yield break;
                }
                if (i == 0)
                {
                    foreach (CableState c in all.Skip(3))
                        Plug(c);
                    AdvancedMod.Log.Msg("dev: nuclear test: engine propellant lines plugged in");
                }
                string Fluid(ushort part) => ship.GetAllStatefulParts<FluidTankState>().Where(p => p.Id == part).Select(p => (FluidTankState)p.State)
                    .Select(t => $"{t.FluidQuantity:0.000}({t.FluidItemId})").FirstOrDefault() ?? "gone";
                string centrifugeText = ship.GetAllStatefulParts<EthanolDistillerState>().Where(p => p.Id == 1).Select(p => (EthanolDistillerState)p.State)
                    .Select(s => $"ore {Centrifuge.Ore(s)} rate {s.NormalizedProductionRate:0.00}").FirstOrDefault() ?? "gone";
                string reactor = ship.GetAllStatefulParts<ConstantState>().Where(p => p.Id == 3).Select(p => ((ConstantState)p.State).ConstantValue.ToString("0")).FirstOrDefault() ?? "gone";
                string engine = ship.GetAllStatefulParts<ThrusterState>().Where(p => p.Id == 5).Select(p => ((ThrusterState)p.State).ThrusterStrength.ToString("0.00")).FirstOrDefault() ?? "gone";
                float climb = Vector3.Dot(ship.Position - start, up);
                float speed = (climb - lastClimb) / 2f;
                lastClimb = climb;
                AdvancedMod.Log.Msg($"dev: nuclear test t={2 * (i + 1)} s: parts {ship.PartCount}, centrifuge {centrifugeText}, made {Fluid(2)}, reactor {reactor} fuel {Fluid(4)}, engine {engine} fuel {Fluid(7)} oxygen {Fluid(6)}, climb {climb:0.0} m, climbing at {speed:0.0} m/s");
                if (i == 2)
                    yield return Parts.Showcase.Photograph(state.Id, "nuclear");
            }
        }

        private static string Json(Propulsion.EngineSpec spec)
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
            string Part(int id, int settings, float x, float y, float z, int forward, int upward) => string.Format(CultureInfo.InvariantCulture,
                "{{\"id\":{0},\"settingsId\":{1},\"coord\":{{\"x\":{2},\"y\":{3},\"z\":{4}}},\"forwardOrientationId\":{5},\"upOrientationId\":{6}}}", id, settings, x, y, z, forward, upward);
            // The engine hangs under the hull's edge, nozzle down (thrust along -up of the part: up for the ship)
            string parts = string.Join(",", Part(1, PowerCatalog.CentrifugeId, -1.5f, -0.46f, 0f, 2, 4), Part(2, PowerCatalog.NuclearTankId, -1.5f, -0.46f, -1.8f, 2, 4),
                Part(3, PowerCatalog.NuclearReactorId, 1.5f, -0.46f, 0f, 2, 4), Part(4, Tank, 1.5f, -0.46f, 1.8f, 2, 4),
                Part(5, spec.Id, 3.4f, -0.6f, 0f, 0, 5), Part(6, Tank, 0f, -0.46f, 1.8f, 2, 4), Part(7, Tank, 0f, -0.46f, -1.8f, 2, 4));
            string constants = "{\"partId\":3,\"constantValue\":0.0}";
            string tanks = $"{{\"partId\":2,\"fluidItemId\":0,\"fluidQuantity\":0.0}},{{\"partId\":4,\"fluidItemId\":{ModItems.NuclearFuel},\"fluidQuantity\":40.0}},"
                + $"{{\"partId\":6,\"fluidItemId\":108,\"fluidQuantity\":40.0}},{{\"partId\":7,\"fluidItemId\":{spec.Fuel},\"fluidQuantity\":40.0}}";
            // Centrifuge 0 -> tank 2; reactor power 1 -> centrifuge power 2; tank 4 -> reactor fluid 3; later: fuel tank 7 -> engine 0, oxygen 6 -> engine 1
            string Cable(int id, int type, int partA, int socketA, int partB, int socketB) =>
                $"{{\"id\":{id},\"type\":{type},\"plugA\":{{\"shipId\":1,\"partId\":{partA},\"socketId\":{socketA}}},\"plugB\":{{\"shipId\":1,\"partId\":{partB},\"socketId\":{socketB}}}}}";
            string cablesJson = string.Join(",", Cable(1, 1, 1, 0, 2, 0), Cable(2, 2, 3, 1, 1, 2), Cable(3, 1, 4, 0, 3, 3),
                Cable(10, 1, 7, 0, 5, 0), Cable(11, 1, 6, 0, 5, 1));
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

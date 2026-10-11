using System.Collections;
using System.Globalization;
using System.Linq;
using System.Text;
using Core.Services;
using Managers.Client.Utils;
using Saving.Services.Converters;
using Saving.Services.Model.State;
using Ships;
using Ships.Cables;
using Ships.Interface.Model;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.State;
using Ships.Services;
using StellarDriveDemoTFAdvanced.Common;
using StellarDriveDemoTFAdvanced.Items;
using UnityEngine;
using WorldTracking.Interface.Model;
using WorldTracking.Services;
using WorldTracking.Spaces;

namespace StellarDriveDemoTFAdvanced.Power
{
    /// <summary>
    /// Development check (DevPortalTest): a wired test ship beside the player with a dark matter
    /// reactor, a tank of dark matter, a collector filling a second tank, and a portal generator
    /// commanded by a constant, its target set to a planet. The ship is held still while the
    /// generator charges, then pushed through the portal; the log follows its state and where
    /// the ship ends up.
    /// </summary>
    internal static class PortalTest
    {
        private const int Floor = 1, GameTank = 23, Constant = 46;

        public static void Install()
        {
            DevHarness.Steps.Add((26f, "portal test", () =>
            {
                if (Settings.DevPortalTest.Value > 0)
                    MelonLoader.MelonCoroutines.Start(Run((ulong)Settings.DevPortalTest.Value));
            }));
        }

        private static IEnumerator Run(ulong planetId)
        {
            var players = ServiceLocator.GetService<Players.Services.PlayersServerTracker>();
            var player = players?.ActivePlayers.FirstOrDefault();
            SpacesServer spaces = ServiceLocator.GetService<SpacesServer>();
            ShipsServerTracker ships = ServiceLocator.GetService<ShipsServerTracker>();
            CablesServerTracker cables = ServiceLocator.GetService<CablesServerTracker>();
            var ids = ServiceLocator.GetService<WorldObjectsIdsServerTracker>();
            var transpose = ServiceLocator.GetService<WorldTracking.Interface.Services.IWorldTransposeServer>();
            if (player == null || spaces == null || ships == null || cables == null || ids == null)
            {
                AdvancedMod.Log.Warning("dev: portal test: services missing");
                yield break;
            }
            var data = JsonUtility.FromJson<ShipImportExportUtils.ShipImportExportData>(Json());
            SavedShipState state = new ShipsConverter().ConvertToState(data.ships).States.Cast<SavedShipState>().Single();
            state.Id = ids.CreateUniqueIdForObject();
            state.SpaceId = player.SpaceId;
            Vector3 up = player.Rotation * Vector3.up;
            Vector3 hold = player.Position + up * 25f + player.Rotation * Vector3.right * 15f;
            state.Position = hold;
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
            TrackedShipServer ship = ships.GetShip(state.Id);
            ship.State.SetTargetState(new ShipTargetState { HasTarget = true, PartId = 0, Target = new Target { TargetType = TargetType.Planet, PlanetId = planetId } });
            AdvancedMod.Log.Msg($"dev: portal test ship {state.Id}, target planet {planetId}");
            uint space = ship.SpaceId;
            Quaternion rotation = ship.Rotation;
            // The way the generator (part 3) faces
            StatefulPart generator = ship.GetAllStatefulParts<ConstantState>().First(p => p.Id == 3);
            Vector3 forward = rotation * (generator.LocalRotation * Vector3.forward);
            float start = Time.unscaledTime;
            bool pushed = false;
            float nextLog = 0f;
            while (Time.unscaledTime - start < 40f)
            {
                ship = ships.GetShip(state.Id);
                if (ship == null)
                {
                    AdvancedMod.Log.Msg("dev: portal test ship gone");
                    yield break;
                }
                double portal = ship.GetAllStatefulParts<ConstantState>().Where(p => p.Id == 3).Select(p => ((ConstantState)p.State).ConstantValue).FirstOrDefault();
                if (ship.SpaceId != space)
                {
                    AdvancedMod.Log.Msg($"dev: portal test: ship left its space ({space} -> {ship.SpaceId}), portal state {portal}");
                    break;
                }
                if (portal >= Portal.Open && !pushed)
                {
                    pushed = true;
                    // Let the portal grow, then photograph it from behind the ship
                    yield return Worlds.StationVisit.Wait(1.6f);
                    Photograph();
                }
                // Held in place while charging, then flown forward through the portal
                Vector3 velocity = pushed ? forward * 20f : Vector3.zero;
                Vector3 position = pushed ? ship.Position : hold;
                spaces.SetWorldObjectPhysics(ship, space, new LocalPhysicsState { Position = position, Rotation = rotation, Velocity = velocity });
                if (Time.unscaledTime >= nextLog)
                {
                    nextLog = Time.unscaledTime + 1f;
                    string Tank(ushort part) => ship.GetAllStatefulParts<FluidTankState>().Where(p => p.Id == part).Select(p => $"{((FluidTankState)p.State).FluidItemId}:{((FluidTankState)p.State).FluidQuantity:0.000}").FirstOrDefault() ?? "-";
                    double Value(ushort part) => ship.GetAllStatefulParts<ConstantState>().Where(p => p.Id == part).Select(p => ((ConstantState)p.State).ConstantValue).FirstOrDefault();
                    AdvancedMod.Log.Msg($"dev: portal test t={Time.unscaledTime - start:0} s: portal {portal}, dark reactor {Value(1)}, collector {Value(5)}, tank {Tank(2)}, collected {Tank(6)}, moved {Vector3.Distance(ship.Position, hold):0.0} m");
                }
                yield return null;
            }
            yield return Worlds.StationVisit.Wait(2f);
            ship = ships.GetShip(state.Id);
            var planets = ServiceLocator.GetService<Planets.Interface.Services.IPlanetsServerProvider>();
            var planet = planets?.Planets.FirstOrDefault(p => p.Id == planetId);
            if (ship != null && planet != null && transpose != null && spaces.TryGetSpace(ship.SpaceId, out WorldTracking.Interface.Model.Spaces.Common.ISpace here))
            {
                PreciseWorldPosition at = transpose.ToPreciseWorldPos(here) + (Core.Values.Vector3Double)ship.Position;
                double distance = at.DeltaDouble(new PreciseWorldPosition(planet.WorldCell, Vector3.zero)).Magnitude;
                AdvancedMod.Log.Msg($"dev: portal test: ship now in space {ship.SpaceId} ({here.GetType().Name}), {distance / 1000.0:0.0} km from the center of planet {planetId} (radius {planet.Radius / 1000f:0.0} km)");
            }
        }

        private static void Photograph()
        {
            GameObject portal = GameObject.Find("TFA_Portal");
            if (portal == null)
            {
                AdvancedMod.Log.Warning("dev: portal test: no portal visual on this client");
                return;
            }
            var cameraObject = new GameObject("TFA_PortalCamera");
            var camera = cameraObject.AddComponent<Camera>();
            if (Camera.main != null)
                camera.CopyFrom(Camera.main);
            camera.enabled = false;
            camera.fieldOfView = 55f;
            camera.farClipPlane = 5000f;
            Transform t = portal.transform;
            camera.transform.position = t.position - t.forward * 45f + t.right * 55f + t.up * 18f;
            camera.transform.rotation = Quaternion.LookRotation(t.position - camera.transform.position, t.up);
            var target = RenderTexture.GetTemporary(1600, 900, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var sheet = new Texture2D(1600, 900, TextureFormat.RGBA32, false);
            sheet.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            sheet.Apply();
            RenderTexture.active = previous;
            System.IO.Directory.CreateDirectory(DevHarness.Folder);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(DevHarness.Folder, "portal_open.png"), sheet.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.ReleaseTemporary(target);
            Object.Destroy(sheet);
            Object.Destroy(cameraObject);
            AdvancedMod.Log.Msg($"dev: portal photographed, scale {portal.transform.localScale.x:0.0}");
        }

        private static string Json()
        {
            var hull = new StringBuilder();
            for (int x = -3; x <= 3; x++)
            {
                for (int z = -3; z <= 3; z++)
                {
                    if (hull.Length > 0)
                        hull.Append(',');
                    hull.Append($"{{\"partId\":{Floor},\"x\":{x},\"y\":0,\"z\":{z},\"forwardOrientationId\":4,\"upOrientationId\":0,\"voxelPartTypeId\":0,\"FrontColor\":{{\"x\":150,\"y\":150,\"z\":150}},\"BackColor\":{{\"x\":120,\"y\":120,\"z\":120}}}}");
                }
            }
            string Part(int id, int settings, float x, float z) => string.Format(CultureInfo.InvariantCulture,
                "{{\"id\":{0},\"settingsId\":{1},\"coord\":{{\"x\":{2},\"y\":-0.46,\"z\":{3}}},\"forwardOrientationId\":2,\"upOrientationId\":4}}", id, settings, x, z);
            string parts = string.Join(",",
                Part(1, PowerCatalog.DarkReactorId, -2f, -1f), Part(2, PowerCatalog.DarkTankId, 1.8f, -1.8f), Part(3, PowerCatalog.PortalId, 0f, 2f),
                Part(4, Constant, 1.5f, 0.5f), Part(5, PowerCatalog.DarkCollectorId, -2f, 1.8f), Part(6, GameTank, -0.2f, -2f));
            string constants = "{\"partId\":1,\"constantValue\":0.0},{\"partId\":3,\"constantValue\":0.0},{\"partId\":4,\"constantValue\":1.0},{\"partId\":5,\"constantValue\":0.0}";
            string tanks = $"{{\"partId\":2,\"fluidItemId\":{ModItems.DarkMatter},\"fluidQuantity\":30.0}},{{\"partId\":6,\"fluidItemId\":0,\"fluidQuantity\":0.0}}";
            string Cable(int id, int type, int partA, int socketA, int partB, int socketB) =>
                $"{{\"id\":{id},\"type\":{type},\"plugA\":{{\"shipId\":1,\"partId\":{partA},\"socketId\":{socketA}}},\"plugB\":{{\"shipId\":1,\"partId\":{partB},\"socketId\":{socketB}}}}}";
            // Power: reactor A -> portal A, reactor B -> collector A; fluid: tank 0 -> portal, tank 1 -> reactor, collector -> empty tank; signal: constant -> portal command
            string cablesJson = string.Join(",", Cable(1, 2, 1, 1, 3, 1), Cable(2, 2, 1, 2, 5, 1), Cable(3, 1, 2, 0, 3, 3), Cable(4, 1, 2, 1, 1, 3),
                Cable(5, 0, 4, 0, 3, Portal.CommandSocket), Cable(6, 1, 5, 3, 6, 0));
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

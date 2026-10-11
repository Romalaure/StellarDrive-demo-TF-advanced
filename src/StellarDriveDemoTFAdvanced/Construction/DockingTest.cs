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
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;
using Ships.Services;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;
using WorldTracking.Services;
using WorldTracking.Spaces;

namespace StellarDriveDemoTFAdvanced.Construction
{
    /// <summary>
    /// Development check (DevDockingTest): two ships, each a floor with a docking module whose
    /// magnet is on, set collar to collar a little apart, high in the air so they fall together;
    /// the log follows whether the magnets pull them together and dock them.
    /// </summary>
    internal static class DockingTest
    {
        private const int Floor = 1, Constant = 46;

        public static void Install()
        {
            DevHarness.Steps.Add((26f, "docking test", () =>
            {
                if (Settings.DevDockingTest.Value)
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
                yield break;
            Vector3 up = player.Rotation * Vector3.up;
            Vector3 forward = player.Rotation * Vector3.forward;
            Vector3 basePosition = player.Position + up * 300f + player.Rotation * Vector3.right * 20f;
            // Ship A, then ship B turned around with its collar 1.2 m in front of A's
            uint a = Spawn(spaces, ships, cables, ids, player.SpaceId, basePosition, player.Rotation);
            StatefulPart module = ships.GetShip(a).GetAllStatefulParts<DockingDoorState>().First();
            Vector3 collarLocal = module.LocalPosition + module.LocalRotation * new Vector3(0.5f, 0.5f, 0.25f);
            Vector3 facing = player.Rotation * (module.LocalRotation * Vector3.forward);
            Vector3 collarA = basePosition + player.Rotation * collarLocal;
            Quaternion turned = player.Rotation * Quaternion.Euler(0f, 180f, 0f);
            Vector3 positionB = collarA + facing * 1.2f - turned * collarLocal;
            uint b = Spawn(spaces, ships, cables, ids, player.SpaceId, positionB, turned);
            AdvancedMod.Log.Msg($"dev: docking test ships {a} and {b}");
            for (int i = 0; i < 20; i++)
            {
                // Both fall alike: their shared velocity is taken away each frame, the magnets' pull stays
                float until = Time.unscaledTime + 1f;
                while (Time.unscaledTime < until)
                {
                    TrackedShipServer fa = ships.GetShip(a), fb = ships.GetShip(b);
                    if (fa != null && fb != null && fa.SpaceId == fb.SpaceId && !((DockingDoorState)fa.GetAllStatefulParts<DockingDoorState>().First().State).IsDocked)
                    {
                        Vector3 shared = (fa.PhysicsState.LocalPhysicsState.Velocity + fb.PhysicsState.LocalPhysicsState.Velocity) * 0.5f;
                        foreach (TrackedShipServer f in new[] { fa, fb })
                        {
                            var st = f.PhysicsState.LocalPhysicsState;
                            spaces.SetWorldObjectPhysics(f, f.SpaceId, new WorldTracking.Interface.Model.LocalPhysicsState { Position = st.Position, Rotation = st.Rotation, Velocity = st.Velocity - shared });
                        }
                    }
                    yield return null;
                }
                TrackedShipServer sa = ships.GetShip(a), sb = ships.GetShip(b);
                if (sa == null || sb == null)
                {
                    AdvancedMod.Log.Msg("dev: docking test: a ship is gone");
                    yield break;
                }
                StatefulPart pa = sa.GetAllStatefulParts<DockingDoorState>().FirstOrDefault();
                StatefulPart pb = sb.GetAllStatefulParts<DockingDoorState>().FirstOrDefault();
                var da = (DockingDoorState)pa?.State;
                var db = (DockingDoorState)pb?.State;
                Vector3 ca = sa.Position + sa.Rotation * (pa.LocalPosition + pa.LocalRotation * new Vector3(0.5f, 0.5f, 0.25f));
                Vector3 cb = sb.Position + sb.Rotation * (pb.LocalPosition + pb.LocalRotation * new Vector3(0.5f, 0.5f, 0.25f));
                float gap = sb.SpaceId == sa.SpaceId ? Vector3.Distance(ca, cb) : -1f;
                Quaternion ra = sa.Rotation * pa.LocalRotation, rb = sb.Rotation * pb.LocalRotation;
                AdvancedMod.Log.Msg($"dev: docking test t={i + 1} s: A docked {da?.IsDocked} magnet {da?.MagnetStrength}, B docked {db?.IsDocked}, collar gap {gap:0.00} m, up.up {Vector3.Dot(ra * Vector3.up, rb * Vector3.up):0.00}, right.left {Vector3.Dot(ra * Vector3.right, rb * Vector3.left):0.00}, partRot {pa.LocalRotation.eulerAngles:F0}, A at {pa.LocalPosition:F2}");
                if (i == 2)
                    yield return Parts.Showcase.Photograph(a, "docking_start");
                if (da != null && da.IsDocked)
                {
                    yield return Showcase();
                    yield break;
                }
            }

            IEnumerator Showcase()
            {
                yield return Worlds.StationVisit.Wait(1f);
                yield return Parts.Showcase.Photograph(a, "docking");
            }
        }

        private static uint Spawn(SpacesServer spaces, ShipsServerTracker ships, CablesServerTracker cables, WorldObjectsIdsServerTracker ids, uint space, Vector3 position, Quaternion rotation)
        {
            var data = JsonUtility.FromJson<ShipImportExportUtils.ShipImportExportData>(Json());
            SavedShipState state = new ShipsConverter().ConvertToState(data.ships).States.Cast<SavedShipState>().Single();
            state.Id = ids.CreateUniqueIdForObject();
            state.SpaceId = space;
            state.Position = position;
            state.Rotation = rotation;
            state.Velocity = Vector3.zero;
            state.RelativeSpaceId = spaces.GetOrCreateShipRelativeSpace(state.Id);
            ships.LoadShip(state);
            foreach (var cable in CablesConverter.ConvertToState(data.cables))
            {
                CableState c = cable.State;
                CablePlugSocket pa = c.PlugA, pb = c.PlugB;
                var sa = pa.EntityPartSocket; sa.EntityId = state.Id; pa.EntityPartSocket = sa;
                var sb = pb.EntityPartSocket; sb.EntityId = state.Id; pb.EntityPartSocket = sb;
                c.PlugA = pa;
                c.PlugB = pb;
                cables.AddCable(c);
            }
            return state.Id;
        }

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
            // The module's floor bottom is 0.5 m below its origin
            string Part(int id, int settings, float x, float y, float z) => string.Format(CultureInfo.InvariantCulture,
                "{{\"id\":{0},\"settingsId\":{1},\"coord\":{{\"x\":{2},\"y\":{3},\"z\":{4}}},\"forwardOrientationId\":2,\"upOrientationId\":4}}", id, settings, x, y, z);
            string parts = string.Join(",", Part(1, StationModules.DockingId, 0f, 0.04f, 0f), Part(2, Constant, 2.5f, -0.46f, 4f));
            string Cable(int id, int type, int partA, int socketA, int partB, int socketB) =>
                $"{{\"id\":{id},\"type\":{type},\"plugA\":{{\"shipId\":1,\"partId\":{partA},\"socketId\":{socketA}}},\"plugB\":{{\"shipId\":1,\"partId\":{partB},\"socketId\":{socketB}}}}}";
            var sb = new StringBuilder();
            sb.Append("{\"rootShipId\":1,\"cables\":[").Append(Cable(1, 0, 2, 0, 1, 0)).Append("],\"ships\":[{");
            sb.Append("\"id\":1,\"relativeSpaceId\":0,\"spaceId\":0,\"position\":{\"x\":0,\"y\":0,\"z\":0},\"velocity\":{\"x\":0,\"y\":0,\"z\":0},\"rotation\":{\"x\":0,\"y\":0,\"z\":0,\"w\":1},");
            sb.Append("\"rooms\":[],\"hullParts\":[").Append(hull).Append("],\"statefulFloatingParts\":[").Append(parts).Append("],\"statefulHullParts\":[],\"thrustersStates\":[],\"storageStates\":[],\"fluidTankStates\":[],");
            sb.Append("\"constantStates\":[{\"partId\":2,\"constantValue\":1.0}],");
            sb.Append("\"dockingDoorStates\":[{\"partId\":1,\"isOpen\":false,\"isDocked\":false,\"dockedShipId\":0,\"dockedPartId\":0}],");
            foreach (string empty in new[] { "beaconStates", "leverStates", "warpDriveStates", "targeterStates", "mathGateStates",
                         "unaryMathGateStates", "memoryRegisterStates", "transmitterStates", "rotorStates", "electrolyzerStates",
                         "woodThrusterStates", "woodGeneratorStates", "ethanolDistillerStates" })
                sb.Append('"').Append(empty).Append("\":[],");
            sb.Length--;
            sb.Append("}]}");
            return sb.ToString();
        }
    }
}

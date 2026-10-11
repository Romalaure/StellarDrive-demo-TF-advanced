using System.Collections;
using System.Globalization;
using System.Linq;
using System.Text;
using Core.Services;
using Core.Values;
using Saving.Services.Converters;
using Saving.Services.Model.State;
using Managers.Client.Utils;
using Ships;
using Ships.Services;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;
using WorldTracking.Interface.Model.Spaces;
using WorldTracking.Services;
using WorldTracking.Spaces;

namespace StellarDriveDemoTFAdvanced.Parts
{
    /// <summary>
    /// Development check (DevShowcase): the listed parts set out in a row on a test ship beside
    /// the player, then photographed from both sides, as a player would see them in game.
    /// </summary>
    internal static class Showcase
    {
        private const int Floor = 1;
        private static float Spacing = 3f;

        public static void Install()
        {
            DevHarness.Steps.Add((24f, "showcase", () =>
            {
                if (!string.IsNullOrWhiteSpace(Settings.DevShowcase.Value))
                    MelonLoader.MelonCoroutines.Start(Run());
            }));
        }

        private static IEnumerator Run()
        {
            ushort[] ids = Settings.DevShowcase.Value.Split(',').Select(s => ushort.TryParse(s.Trim(), out ushort id) ? id : (ushort)0).Where(id => id != 0).ToArray();
            // Station modules are rooms: more room between them
            Spacing = ids.Any(id => id >= 7371 && id <= 7376) ? 8f : 3f;
            var players = ServiceLocator.GetService<Players.Services.PlayersServerTracker>();
            var player = players?.ActivePlayers.FirstOrDefault();
            SpacesServer spaces = ServiceLocator.GetService<SpacesServer>();
            ShipsServerTracker ships = ServiceLocator.GetService<ShipsServerTracker>();
            var worldIds = ServiceLocator.GetService<WorldObjectsIdsServerTracker>();
            if (player == null || spaces == null || ships == null || worldIds == null || ids.Length == 0)
            {
                AdvancedMod.Log.Warning("dev: showcase: nothing to show or services missing");
                yield break;
            }
            var data = JsonUtility.FromJson<ShipImportExportUtils.ShipImportExportData>(Json(ids));
            SavedShipState state = new ShipsConverter().ConvertToState(data.ships).States.Cast<SavedShipState>().Single();
            state.Id = worldIds.CreateUniqueIdForObject();
            state.SpaceId = player.SpaceId;
            // The player stands upright: their frame gives the local up and a side
            Vector3 up = player.Rotation * Vector3.up;
            state.Position = player.Position + up * 1.5f + player.Rotation * Vector3.right * 20f;
            state.Rotation = player.Rotation;
            state.Velocity = Vector3.zero;
            state.RelativeSpaceId = spaces.GetOrCreateShipRelativeSpace(state.Id);
            ships.LoadShip(state);
            AdvancedMod.Log.Msg($"dev: showcase ship {state.Id} with {string.Join(", ", ids)}");
            for (int i = 0; i < 5; i++)
            {
                yield return Worlds.StationVisit.Wait(1f);
                TrackedShipServer s = ships.GetShip(state.Id);
                if (s != null)
                    AdvancedMod.Log.Msg($"dev: showcase ship at {s.Position - player.Position:F1} rot {s.Rotation.eulerAngles:F0} space {s.SpaceId}/{player.SpaceId} parts {s.PartCount}");
            }
            TrackedShipServer ship = ships.GetShip(state.Id);
            if (ship == null)
            {
                AdvancedMod.Log.Warning("dev: showcase ship gone");
                yield break;
            }
            yield return Photograph(state.Id, "showcase");
            AdvancedMod.Log.Msg($"dev: showcase done, ship has {ship.PartCount} parts");
        }

        /// <summary>Photographs a ship's model on this client from five angles, to dev/{prefix}_*.png.</summary>
        public static IEnumerator Photograph(uint shipId, string prefix)
        {
            // The ship's model on this client, photographed by a camera of our own
            GameObject visual = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(t => t.parent == null && t.name.StartsWith("ship " + shipId + " ") && t.name.Contains("visual")).Select(t => t.gameObject).FirstOrDefault();
            if (visual == null)
            {
                AdvancedMod.Log.Warning("dev: showcase: no visual for the ship");
                yield break;
            }
            Bounds bounds = new Bounds(visual.transform.position, Vector3.zero);
            foreach (Renderer r in visual.GetComponentsInChildren<Renderer>())
                bounds.Encapsulate(r.bounds);
            Transform frame = visual.transform;
            var dump = new StringBuilder();
            foreach (Renderer r in visual.GetComponentsInChildren<Renderer>())
            {
                string path = r.name;
                for (Transform t = r.transform.parent; t != null && t != frame; t = t.parent)
                    path = t.name + "/" + path;
                var mf = r.GetComponent<MeshFilter>();
                dump.AppendLine($"{path} mesh={mf?.sharedMesh?.name} at {frame.InverseTransformPoint(r.bounds.center):F2} size {r.bounds.size:F2}");
            }
            System.IO.Directory.CreateDirectory(DevHarness.Folder);
            System.IO.File.WriteAllText(System.IO.Path.Combine(DevHarness.Folder, prefix + "_tree.txt"), dump.ToString());
            var cameraObject = new GameObject("TFA_ShowcaseCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.fieldOfView = 40f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 2000f;
            Camera main = Camera.main;
            if (main != null)
            {
                camera.CopyFrom(main);
                camera.enabled = false;
                camera.fieldOfView = 40f;
            }
            var target = RenderTexture.GetTemporary(1600, 900, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            var sheet = new Texture2D(1600, 900, TextureFormat.RGBA32, false);
            float reach = bounds.extents.magnitude;
            (string Name, Vector3 Direction, float Distance)[] views =
            {
                ("front", new Vector3(0.15f, 0.45f, -1f), 1.4f),
                ("back", new Vector3(-0.2f, 0.4f, 1f), 1.4f),
                ("top", new Vector3(0.05f, 1f, -0.25f), 1.3f),
                ("closeA", new Vector3(0.5f, 0.35f, -1f), 0.55f),
                ("closeB", new Vector3(-0.5f, 0.3f, 1f), 0.55f),
            };
            foreach ((string name, Vector3 direction, float distance) in views)
            {
                Vector3 world = frame.TransformDirection(direction.normalized);
                Vector3 center = bounds.center;
                if (name == "closeA")
                    center = bounds.center - frame.right * bounds.extents.magnitude * 0.45f;
                if (name == "closeB")
                    center = bounds.center + frame.right * bounds.extents.magnitude * 0.1f;
                camera.transform.position = center + world * reach * distance;
                camera.transform.rotation = Quaternion.LookRotation(center - camera.transform.position, frame.up);
                camera.Render();
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                sheet.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                sheet.Apply();
                RenderTexture.active = previous;
                System.IO.Directory.CreateDirectory(DevHarness.Folder);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(DevHarness.Folder, prefix + "_" + name + ".png"), sheet.EncodeToPNG());
                yield return null;
            }
            camera.targetTexture = null;
            RenderTexture.ReleaseTemporary(target);
            Object.Destroy(sheet);
            Object.Destroy(cameraObject);
        }

        private static string Json(ushort[] ids)
        {
            int halfX = Mathf.CeilToInt((ids.Length - 1) * Spacing * 0.5f) + 2;
            var hull = new StringBuilder();
            for (int x = -halfX; x <= halfX; x++)
            {
                for (int z = -2; z <= 2; z++)
                {
                    if (hull.Length > 0)
                        hull.Append(',');
                    hull.Append($"{{\"partId\":{Floor},\"x\":{x},\"y\":0,\"z\":{z},\"forwardOrientationId\":4,\"upOrientationId\":0,\"voxelPartTypeId\":0,\"FrontColor\":{{\"x\":150,\"y\":150,\"z\":150}},\"BackColor\":{{\"x\":120,\"y\":120,\"z\":120}}}}");
                }
            }
            var parts = new StringBuilder();
            for (int i = 0; i < ids.Length; i++)
            {
                if (i > 0)
                    parts.Append(',');
                float x = (i - (ids.Length - 1) * 0.5f) * Spacing;
                parts.Append(string.Format(CultureInfo.InvariantCulture,
                    "{{\"id\":{0},\"settingsId\":{1},\"coord\":{{\"x\":{2},\"y\":-0.46,\"z\":0}},\"forwardOrientationId\":2,\"upOrientationId\":4}}", i + 1, ids[i], x));
            }
            // Default states for the parts that need one (the build tool makes them in play)
            var lists = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>
            {
                ["constantStates"] = new System.Collections.Generic.List<string>(), ["storageStates"] = new System.Collections.Generic.List<string>(),
                ["dockingDoorStates"] = new System.Collections.Generic.List<string>(), ["fluidTankStates"] = new System.Collections.Generic.List<string>()
            };
            string emptySlots = string.Join(",", Enumerable.Repeat("{\"hasItem\":false,\"itemId\":0,\"quantity\":0}", 10));
            for (int i = 0; i < ids.Length; i++)
            {
                string stateClass = (ids[i] >= 7000 ? SDModKit.Game.CustomParts.Get(ids[i]) : null)?.internalStateClass;
                int id = i + 1;
                switch (stateClass)
                {
                    case "ConstantState": lists["constantStates"].Add($"{{\"partId\":{id},\"constantValue\":0.0}}"); break;
                    case "StorageState": lists["storageStates"].Add($"{{\"partId\":{id},\"slots\":[{emptySlots}]}}"); break;
                    case "DockingDoorState": lists["dockingDoorStates"].Add($"{{\"partId\":{id},\"isOpen\":false,\"isDocked\":false,\"dockedShipId\":0,\"dockedPartId\":0}}"); break;
                    case "FluidTankState": lists["fluidTankStates"].Add($"{{\"partId\":{id},\"fluidItemId\":0,\"fluidQuantity\":0.0}}"); break;
                }
            }
            var sb = new StringBuilder();
            sb.Append("{\"rootShipId\":1,\"cables\":[],\"ships\":[{");
            sb.Append("\"id\":1,\"relativeSpaceId\":0,\"spaceId\":0,\"position\":{\"x\":0,\"y\":0,\"z\":0},\"velocity\":{\"x\":0,\"y\":0,\"z\":0},\"rotation\":{\"x\":0,\"y\":0,\"z\":0,\"w\":1},");
            sb.Append("\"rooms\":[],\"hullParts\":[").Append(hull).Append("],\"statefulFloatingParts\":[").Append(parts).Append("],\"statefulHullParts\":[],");
            foreach (var list in lists)
                sb.Append('"').Append(list.Key).Append("\":[").Append(string.Join(",", list.Value)).Append("],");
            foreach (string empty in new[] { "thrustersStates", "beaconStates", "leverStates", "warpDriveStates",
                         "targeterStates", "mathGateStates", "unaryMathGateStates", "memoryRegisterStates", "transmitterStates", "rotorStates",
                         "electrolyzerStates", "woodThrusterStates", "woodGeneratorStates", "ethanolDistillerStates" })
                sb.Append('"').Append(empty).Append("\":[],");
            sb.Length--;
            sb.Append("}]}");
            return sb.ToString();
        }
    }
}

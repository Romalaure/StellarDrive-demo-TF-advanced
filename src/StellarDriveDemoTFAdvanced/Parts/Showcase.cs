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
using WorldTracking.Interface.Values;
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
            Spacing = ids.Any(id => id >= 7371 && id <= 7390 && id != 7381) ? 8f : 3f;
            var players = ServiceLocator.GetService<Players.Services.PlayersServerTracker>();
            var player = players?.ActivePlayers.FirstOrDefault();
            for (int wait = 0; player == null && players != null && wait < 30; wait++)
            {
                yield return Worlds.StationVisit.Wait(1f);
                player = players.ActivePlayers.FirstOrDefault();
            }
            SpacesServer spaces = ServiceLocator.GetService<SpacesServer>();
            ShipsServerTracker ships = ServiceLocator.GetService<ShipsServerTracker>();
            var worldIds = ServiceLocator.GetService<WorldObjectsIdsServerTracker>();
            if (player == null || spaces == null || ships == null || worldIds == null || ids.Length == 0)
            {
                AdvancedMod.Log.Warning($"dev: showcase: nothing to show or services missing (player {player != null}, spaces {spaces != null}, ships {ships != null}, ids {worldIds != null}, parts {ids.Length})");
                yield break;
            }
            uint id = LoadShip(Json(ids), player, spaces, ships, worldIds, 20f);
            MelonLoader.MelonCoroutines.Start(Construction.SnapTest.Pin(spaces, ships, id, 30f));
            var state = new { Id = id };
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
            if (Settings.DevPaintTest.Value)
            {
                // The TF mod's paint: exterior in wood, interior in white with a blue tint, on every part of the test ship
                for (int i = 0; i < ids.Length; i++)
                {
                    PaintThroughTF(state.Id, (ushort)(i + 1), new Color32(230, 200, 170, 255), 2);
                    PaintThroughTF(state.Id, (ushort)((i + 1) | 0x8000), new Color32(150, 200, 255, 255), 4);
                }
                yield return Worlds.StationVisit.Wait(1f);
            }
            yield return Photograph(state.Id, "showcase");
            // Rooms: a look inside each, from the doorway toward the far wall
            if (Spacing > 3f)
            {
                var inside = new System.Collections.Generic.List<(Vector3, Vector3)>();
                for (int i = 0; i < ids.Length; i++)
                {
                    float x = (i - (ids.Length - 1) * 0.5f) * Spacing;
                    inside.Add((new Vector3(x + 0.6f, 1.3f, 1.6f), new Vector3(-0.25f, -0.25f, -1f)));
                }
                yield return PhotographFrom(state.Id, "showcase_inside", inside);
            }
            if (Settings.DevInteractTest.Value)
                yield return InteractTest(state.Id);
            AdvancedMod.Log.Msg($"dev: showcase done, ship has {ship.PartCount} parts");
        }

        // StellarDriveDemoTF.Paint.PaintNet.ServerSet(new PartKey(ship, part), new PaintData(color, PaintFinish.None)), by reflection
        private static void PaintThroughTF(uint ship, ushort part, Color32 color, byte material)
        {
            System.Type net = HarmonyLib.AccessTools.TypeByName("StellarDriveDemoTF.Paint.PaintNet");
            System.Type key = HarmonyLib.AccessTools.TypeByName("StellarDriveDemoTF.Paint.PartKey");
            System.Type data = HarmonyLib.AccessTools.TypeByName("StellarDriveDemoTF.Paint.PaintData");
            System.Type finish = HarmonyLib.AccessTools.TypeByName("StellarDriveDemoTF.Paint.PaintFinish");
            if (net == null || key == null || data == null || finish == null)
            {
                AdvancedMod.Log.Warning("dev: paint test: the TF mod's paint was not found");
                return;
            }
            object k = System.Activator.CreateInstance(key, ship, part);
            object d = System.Activator.CreateInstance(data, color, System.Activator.CreateInstance(finish), material);
            HarmonyLib.AccessTools.Method(net, "ServerSet").Invoke(null, new[] { k, d });
        }

        // The modules' interactions on this client: count them, use the light switches and a respawn bunk
        private static IEnumerator InteractTest(uint shipId)
        {
            var interactions = Object.FindObjectsByType<Ships.Parts.Common.PartInteraction>(FindObjectsSortMode.None)
                .Where(i => i.transform.root.name.StartsWith("ship " + shipId + " ")).ToList();
            AdvancedMod.Log.Msg("dev: interactions: " + string.Join(", ", interactions.GroupBy(i => i.GetType().Name).Select(g => g.Key + " x" + g.Count())));
            foreach (var light in interactions.OfType<Construction.LightSwitchInteraction>())
            {
                bool done = light.Interact(Players.Interface.Model.InteractionType.Interact);
                AdvancedMod.Log.Msg($"dev: light switch {light.transform.parent?.parent?.name}: used {done}");
            }
            yield return Worlds.StationVisit.Wait(0.5f);
            var dark = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Count(r => r.transform.root.name.StartsWith("ship " + shipId + " ") && r.name.EndsWith("Light") && !r.enabled);
            AdvancedMod.Log.Msg($"dev: lights off after the switches: {dark}");
            var respawn = interactions.FirstOrDefault(i => i.name == "Respawn");
            if (respawn != null)
            {
                bool done = respawn.Interact(Players.Interface.Model.InteractionType.Interact);
                yield return Worlds.StationVisit.Wait(1.5f);
                var point = ServiceLocator.GetService<Players.Interface.Services.ILocalPLayerRespawnPointProvider>()?.LocalPlayerRespawnPoint;
                AdvancedMod.Log.Msg($"dev: respawn bunk used {done}: respawn point default {point?.UseDefaultSpawn} ship {point?.ShipId} part {point?.PartId}");
            }
            // A module placed on its own, through the build tool's own call (as when aiming at nothing)
            var tool = Object.FindObjectsByType<Tools.Build.BuildToolLocalClient>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            var players = ServiceLocator.GetService<Players.Services.PlayersServerTracker>();
            var player = players?.ActivePlayers.FirstOrDefault();
            var ships = ServiceLocator.GetService<ShipsServerTracker>();
            Ships.Interface.Settings.PartSettings hub = SDModKit.Game.CustomParts.Get(Construction.StationModules.HubId);
            Camera eye = Camera.main;
            var network = ServiceLocator.GetService<Managers.Interface.Services.IShipInteractionNetworkService>();
            if (network != null && hub != null && eye != null && ships != null)
            {
                int before = ships.LoadedShipsWithChildren.Count();
                Construction.FreeModules.DevFree = true;
                Vector3 at = eye.transform.position + eye.transform.forward * 25f;
                Quaternion turn = Quaternion.LookRotation(eye.transform.forward, eye.transform.up);
                // The request the build tool sends for a module aimed at nothing (see FreeModules)
                network.CreateShip(new Managers.Interface.Model.CreateShipNetworkRequest
                {
                    PartId = hub.id,
                    WorldTransform = new WorldTracking.Interface.Model.WorldTransform { SpaceId = WorldTracking.Interface.Values.ClientOrigin.LocalPhysicsSpaceId, Position = at - turn * Construction.FreeModules.ModuleCoord, Rotation = turn }
                });
                yield return Worlds.StationVisit.Wait(2f);
                var created = ships.LoadedShipsWithChildren.OrderByDescending(sh => sh.Id).FirstOrDefault();
                AdvancedMod.Log.Msg($"dev: free module: ships {before} -> {ships.LoadedShipsWithChildren.Count()}, newest {created?.Id} with {created?.PartCount} parts: " +
                    string.Join(", ", created?.StatefulParts.Select(sp => sp.Settings.id.ToString()) ?? new string[0]));
                if (created != null)
                    yield return Photograph(created.Id, "freemodule");
            }
            else
                AdvancedMod.Log.Warning($"dev: free module: network {network != null}, hub {hub != null}, camera {eye != null}");
        }

        /// <summary>Loads a ship from export JSON beside the player (<paramref name="side"/> meters to their right); returns its id.</summary>
        public static uint LoadShip(string json, Players.TrackedPlayerServer player, SpacesServer spaces, ShipsServerTracker ships, WorldObjectsIdsServerTracker worldIds, float side)
        {
            var data = JsonUtility.FromJson<ShipImportExportUtils.ShipImportExportData>(json);
            SavedShipState state = new ShipsConverter().ConvertToState(data.ships).States.Cast<SavedShipState>().Single();
            state.Id = worldIds.CreateUniqueIdForObject();
            state.SpaceId = player.SpaceId;
            // The player stands upright: their frame gives the local up and a side
            Vector3 up = player.Rotation * Vector3.up;
            state.Position = player.Position + up * 1.5f + player.Rotation * Vector3.right * side;
            state.Rotation = player.Rotation;
            state.Velocity = Vector3.zero;
            // In the client's physics space, so the ship gets colliders on this client too
            uint physicsSpace = ClientOrigin.LocalPhysicsSpaceId;
            var transpose = ServiceLocator.GetService<WorldTracking.Interface.Services.IWorldTransposeServer>();
            if (physicsSpace != 0 && physicsSpace != state.SpaceId && transpose != null)
            {
                state.Position = transpose.Position(state.SpaceId, physicsSpace, state.Position);
                state.SpaceId = physicsSpace;
            }
            state.RelativeSpaceId = spaces.GetOrCreateShipRelativeSpace(state.Id);
            ships.LoadShip(state);
            return state.Id;
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

        /// <summary>Photographs from points in the ship's frame (position, direction), to dev/{prefix}_{n}.png.</summary>
        public static IEnumerator PhotographFrom(uint shipId, string prefix, System.Collections.Generic.IList<(Vector3 At, Vector3 Look)> views)
        {
            GameObject visual = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(t => t.parent == null && t.name.StartsWith("ship " + shipId + " ") && t.name.Contains("visual")).Select(t => t.gameObject).FirstOrDefault();
            if (visual == null)
                yield break;
            Transform frame = visual.transform;
            var cameraObject = new GameObject("TFA_ShowcaseCamera");
            var camera = cameraObject.AddComponent<Camera>();
            if (Camera.main != null)
                camera.CopyFrom(Camera.main);
            camera.enabled = false;
            camera.fieldOfView = 75f;
            camera.nearClipPlane = 0.05f;
            var target = RenderTexture.GetTemporary(1600, 900, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            var sheet = new Texture2D(1600, 900, TextureFormat.RGBA32, false);
            for (int i = 0; i < views.Count; i++)
            {
                camera.transform.position = frame.TransformPoint(views[i].At);
                camera.transform.rotation = Quaternion.LookRotation(frame.TransformDirection(views[i].Look.normalized), frame.up);
                camera.Render();
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                sheet.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                sheet.Apply();
                RenderTexture.active = previous;
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(DevHarness.Folder, prefix + "_" + i + ".png"), sheet.EncodeToPNG());
                yield return null;
            }
            camera.targetTexture = null;
            RenderTexture.ReleaseTemporary(target);
            Object.Destroy(sheet);
            Object.Destroy(cameraObject);
        }

        private static string Json(ushort[] ids)
        {
            var parts = new System.Collections.Generic.List<(ushort, Vector3, int, int)>();
            for (int i = 0; i < ids.Length; i++)
                parts.Add((ids[i], new Vector3((i - (ids.Length - 1) * 0.5f) * Spacing, -0.46f, 0f), 2, 4));
            return Json(parts, Mathf.CeilToInt((ids.Length - 1) * Spacing * 0.5f) + 2);
        }

        /// <summary>Export JSON of a test ship: a floor of hull, then the given placed parts (settings id, coordinates, forward and up orientation ids).</summary>
        public static string Json(System.Collections.Generic.IList<(ushort Id, Vector3 Coord, int Forward, int Up)> placed, int halfX)
        {
            ushort[] ids = placed.Select(p => p.Id).ToArray();
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
                Vector3 c = placed[i].Coord;
                parts.Append(string.Format(CultureInfo.InvariantCulture,
                    "{{\"id\":{0},\"settingsId\":{1},\"coord\":{{\"x\":{2},\"y\":{3},\"z\":{4}}},\"forwardOrientationId\":{5},\"upOrientationId\":{6}}}", i + 1, ids[i], c.x, c.y, c.z, placed[i].Forward, placed[i].Up));
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
                ushort settingsId = ids[i];
                string stateClass = (settingsId >= 7000 ? SDModKit.Game.CustomParts.Get(settingsId) : null)?.internalStateClass
                    ?? Resources.FindObjectsOfTypeAll<Ships.Interface.Settings.PartSettings>().FirstOrDefault(s => s != null && s.id == settingsId)?.internalStateClass;
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

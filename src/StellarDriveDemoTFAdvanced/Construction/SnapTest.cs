using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Core.Services;
using Ships.Interface.Model;
using Ships.Interface.Model.Placement;
using Ships.Interface.Services;
using Ships.Interface.Settings;
using Ships.Physics.Proxies;
using Ships.Services;
using StellarDriveDemoTFAdvanced.Common;
using StellarDriveDemoTFAdvanced.Parts;
using Tools.Build.Model;
using Tools.Build.SnapDetectors;
using Tools.Common;
using UnityEngine;
using WorldTracking.Interface.Values;
using WorldTracking.Services;
using WorldTracking.Spaces;

namespace StellarDriveDemoTFAdvanced.Construction
{
    /// <summary>
    /// Development check (DevSnapTest): a test ship with a hub, a plank wall and a module; the build
    /// tool's snapping is asked where parts go when aimed at their faces, then a second ship is
    /// built with the answers and photographed.
    /// </summary>
    internal static class SnapTest
    {
        public static void Install()
        {
            DevHarness.Steps.Add((24f, "snap test", () =>
            {
                if (Settings.DevSnapTest.Value)
                    MelonLoader.MelonCoroutines.Start(Run());
            }));
        }

        private static uint SafeId(ShipPartsAvailabilityProxy proxy)
        {
            try
            {
                return proxy.ShipId;
            }
            catch
            {
                return 0;
            }
        }

        // Holds a test ship still where it was loaded (it would fall)
        internal static IEnumerator Pin(SpacesServer spaces, ShipsServerTracker ships, uint id, float seconds)
        {
            yield return null;
            Ships.TrackedShipServer ship = ships.GetShip(id);
            if (ship == null)
                yield break;
            var start = ship.PhysicsState.LocalPhysicsState;
            float until = Time.unscaledTime + seconds;
            while (Time.unscaledTime < until)
            {
                ship = ships.GetShip(id);
                if (ship == null)
                    yield break;
                spaces.SetWorldObjectPhysics(ship, ship.SpaceId, new WorldTracking.Interface.Model.LocalPhysicsState { Position = start.Position, Rotation = start.Rotation, Velocity = Vector3.zero });
                yield return null;
            }
        }

        private static IEnumerator Run()
        {
            var players = ServiceLocator.GetService<Players.Services.PlayersServerTracker>();
            var player = players?.ActivePlayers.FirstOrDefault();
            SpacesServer spaces = ServiceLocator.GetService<SpacesServer>();
            ShipsServerTracker ships = ServiceLocator.GetService<ShipsServerTracker>();
            var worldIds = ServiceLocator.GetService<WorldObjectsIdsServerTracker>();
            if (player == null || spaces == null || ships == null || worldIds == null)
            {
                AdvancedMod.Log.Warning("dev: snap test: services missing");
                yield break;
            }
            // Forward = 0, Up = 4 (PartOrientation)
            var placed = new List<(ushort Id, Vector3 Coord, int Forward, int Up)>
            {
                (StationModules.HubId, new Vector3(0f, -0.46f, 0f), 0, 4),
                (7343, new Vector3(5f, -0.46f, 0f), 0, 4),
                (7342, new Vector3(5f, -0.46f, -2f), 0, 4),
                (StationModules.StairsId, new Vector3(0f, -0.46f, -10f), 0, 4),
            };
            uint first = Showcase.LoadShip(Showcase.Json(placed, 9), player, spaces, ships, worldIds, 20f);
            MelonLoader.MelonCoroutines.Start(Pin(spaces, ships, first, 40f));
            ShipPartsAvailabilityProxy proxy = null;
            for (int wait = 0; wait < 10 && proxy == null; wait++)
            {
                yield return Worlds.StationVisit.Wait(1f);
                proxy = Object.FindObjectsByType<ShipPartsAvailabilityProxy>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(p => SafeId(p) == first);
            }
            if (proxy == null)
            {
                var loaded = ships.GetShip(first);
                AdvancedMod.Log.Warning($"dev: snap test: ship {first} server parts {loaded?.PartCount} at {(loaded != null ? loaded.Position - player.Position : Vector3.zero)}; no proxy: " + string.Join(", ", Object.FindObjectsByType<ShipPartsAvailabilityProxy>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(p => p.name + "=" + SafeId(p))));
                yield break;
            }
            // (what is placed, where the ray starts and where it goes, in ship space)
            var probes = new (string Name, ushort Id, Vector3 From, Vector3 Direction)[]
            {
                ("corridor on the hub's front wall", StationModules.CorridorId, new Vector3(1.3f, 1.2f, 6f), Vector3.back),
                ("habitation on the hub's left wall", StationModules.HabitationId, new Vector3(-6f, 1.2f, 1.3f), Vector3.right),
                ("storage on the hub's back wall", StationModules.StorageId, new Vector3(-1.3f, 1.2f, -6f), Vector3.forward),
                ("plank wall beside the plank wall", 7343, new Vector3(5.8f, 1f, 0.01f), Vector3.left),
                ("plank floor on the plank floor", 7342, new Vector3(5.2f, 2f, -1.8f), Vector3.down),
                ("solar panel on the hub's roof", StationModules.SolarId, new Vector3(-1f, 6f, -1f), Vector3.down),
                ("storage chest on the hub's floor", 30, new Vector3(1.2f, 1.5f, -1.2f), Vector3.down),
                ("window on the plank wall's face", 7351, new Vector3(5.2f, 1f, 2f), Vector3.back),
                ("corridor at the stairs' upper door", StationModules.CorridorId, new Vector3(1.4f, 3.6f, -4f), Vector3.back),
                ("corner on the hub's right wall", StationModules.CornerId, new Vector3(6f, 1.2f, 1.3f), Vector3.left),
                ("game signal display on the hub's left wall", 82, new Vector3(-6f, 1.8f, -1.3f), Vector3.right),
            };
            PartSettings[] all = Resources.FindObjectsOfTypeAll<PartSettings>();
            foreach (var probe in probes)
            {
                PartSettings settings = all.FirstOrDefault(s => s != null && s.id == probe.Id);
                Vector3 origin = proxy.ToWorldPos(new FloatingPartPlacement(probe.From, PartOrientation.Forward));
                Vector3 direction = proxy.ToWorldRotation(Quaternion.LookRotation(probe.Direction, probe.Direction == Vector3.down ? Vector3.forward : Vector3.up)) * Vector3.forward;
                if (settings == null || !ClientScene.PhysicsScene.Raycast(origin, direction, out RaycastHit hit, 12f, RaycastMasks.BuildDetectionLayer))
                {
                    AdvancedMod.Log.Warning($"dev: snap test: {probe.Name}: no hit (settings {settings != null})");
                    continue;
                }
                FloatingSnap.HitCollider = hit.collider;
                FloatingSnap.Placing = settings;
                PartHit local = proxy.ConvertHitToLocalHit(hit);
                var props = new HitSnapDetectionProperties(local, Quaternion.identity, settings.size, settings.Bounds);
                var detector = new SnapDetectionStrategyProvider().GetSnapDetector(settings.snappingStyle, hit.collider.tag);
                IPartPlacement placement = detector.DetectSnapPlacement(props, proxy);
                string where = placement is FloatingPartPlacement f ? $"at {f.Coord:F2} forward {f.ForwardOrientation} up {f.UpOrientation}" : "nothing (tried " + FloatingSnap.LastTried + ")";
                FloatingSnap.LastTried = null;
                AdvancedMod.Log.Msg($"dev: snap test: {probe.Name}: hit {hit.collider.name} tag {hit.collider.tag} at {local.Pos:F2} normal {local.Normal:F2} style {settings.snappingStyle} -> {where}");
                if (placement is FloatingPartPlacement p)
                    placed.Add((probe.Id, p.Coord, (int)p.ForwardOrientation, (int)p.UpOrientation));
            }
            uint second = Showcase.LoadShip(Showcase.Json(placed, 9), player, spaces, ships, worldIds, 45f);
            MelonLoader.MelonCoroutines.Start(Pin(spaces, ships, second, 20f));
            yield return Worlds.StationVisit.Wait(3f);
            yield return Showcase.Photograph(second, "snaptest");
            AdvancedMod.Log.Msg("dev: snap test done");
        }
    }
}

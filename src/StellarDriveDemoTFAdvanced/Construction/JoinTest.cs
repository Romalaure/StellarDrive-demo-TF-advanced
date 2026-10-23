using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Core.Services;
using Ships.Interface.Model;
using Ships.Interface.Model.Placement;
using Ships.Interface.Settings;
using Ships.Physics.Proxies;
using Ships.Services;
using StellarDriveDemoTFAdvanced.Common;
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
    /// Development check (DevDisable "jointest"): a ship whose floor holds a railing along a cell's
    /// edge; the snapping is asked for a second railing at a right angle at its corner (free, and
    /// meeting it), a third in line with it, and a spot aimed by the cell's edge (up to the edge).
    /// </summary>
    internal static class JoinTest
    {
        public static void Install()
        {
            DevHarness.Steps.Add((26f, "join test", () =>
            {
                if (DevHarness.Off("jointest"))
                    MelonLoader.MelonCoroutines.Start(Run());
            }));
        }

        private static IEnumerator Run()
        {
            var players = ServiceLocator.GetService<Players.Services.PlayersServerTracker>();
            var player = players?.ActivePlayers.FirstOrDefault();
            SpacesServer spaces = ServiceLocator.GetService<SpacesServer>();
            ShipsServerTracker ships = ServiceLocator.GetService<ShipsServerTracker>();
            var ids = ServiceLocator.GetService<WorldObjectsIdsServerTracker>();
            if (player == null || spaces == null || ships == null || ids == null)
                yield break;
            // The first railing along x, its thin side on the cell edge z = -0.5
            var placed = new List<(ushort, Vector3, int, int)> { (7344, new Vector3(0f, -0.46f, -0.5f), 0, 4) };
            uint shipId = Parts.Showcase.LoadShip(Parts.Showcase.Json(placed, 3), player, spaces, ships, ids, 12f);
            MelonLoader.MelonCoroutines.Start(SnapTest.Pin(spaces, ships, shipId, 40f));
            ShipPartsAvailabilityProxy proxy = null;
            for (int wait = 0; wait < 10 && proxy == null; wait++)
            {
                yield return Worlds.StationVisit.Wait(1f);
                proxy = Object.FindObjectsByType<ShipPartsAvailabilityProxy>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(p =>
                {
                    try { return p.ShipId == shipId; } catch { return false; }
                });
            }
            if (proxy == null)
                yield break;
            PartSettings[] all = Resources.FindObjectsOfTypeAll<PartSettings>();
            void Ask(string name, ushort id, Vector3 from, Vector3 dir, float turn)
            {
                PartSettings settings = all.FirstOrDefault(s => s != null && s.id == id);
                Vector3 origin = proxy.ToWorldPos(new FloatingPartPlacement(from, PartOrientation.Forward));
                Vector3 direction = proxy.ToWorldRotation(Quaternion.LookRotation(dir, Mathf.Abs(dir.y) > 0.5f ? Vector3.forward : Vector3.up)) * Vector3.forward;
                if (settings == null || !ClientScene.PhysicsScene.Raycast(origin, direction, out RaycastHit hit, 12f, RaycastMasks.BuildDetectionLayer))
                {
                    AdvancedMod.Log.Msg($"dev: join test: {name}: no hit");
                    return;
                }
                FloatingSnap.HitCollider = hit.collider;
                FloatingSnap.Placing = settings;
                PartHit local = proxy.ConvertHitToLocalHit(hit);
                var props = new HitSnapDetectionProperties(local, Quaternion.Euler(0f, turn, 0f), settings.size, settings.Bounds);
                IPartPlacement placement = new SnapDetectionStrategyProvider().GetSnapDetector(settings.snappingStyle, hit.collider.tag).DetectSnapPlacement(props, proxy);
                AdvancedMod.Log.Msg($"dev: join test: {name}: hit {hit.collider.name} at {local.Pos:F2} -> " + (placement is FloatingPartPlacement f ? $"at {f.Coord:F3} forward {f.ForwardOrientation} up {f.UpOrientation}" : "refused (" + FloatingSnap.LastTried + ")"));
            }
            // The floor by the railing's end, the second railing turned: at the corner (0.5, -0.5)
            Ask("corner railing on the floor", 7344, new Vector3(0.6f, 1f, -0.1f), Vector3.down, 90f);
            // The railing's end face, a railing in line
            Ask("railing in line (its end face)", 7344, new Vector3(1.2f, 0.4f, -0.5f), Vector3.left, 0f);
            // A recessed spot aimed 1 cm from the cell's edge x = 0.5
            Ask("spot by the cell's edge", 7108, new Vector3(0.49f, 1f, 0.2f), Vector3.down, 0f);
            Ask("neon by the cell's edge", 7104, new Vector3(0.2f, 1f, 0.47f), Vector3.down, 0f);
        }
    }
}

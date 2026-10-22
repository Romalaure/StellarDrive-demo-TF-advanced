using System.Collections;
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
using WorldTracking.Services;
using WorldTracking.Spaces;
using WorldTracking.Interface.Values;

namespace StellarDriveDemoTFAdvanced.Construction
{
    /// <summary>
    /// Development check (DevDisable "hullsnap"): the build tool's snapping asked where some parts
    /// go on the hull of the biggest player ship (its underside, a wall from inside, its ceiling),
    /// logging the placement and orientation it gives.
    /// </summary>
    internal static class HullSnapProbe
    {
        public static void Install()
        {
            DevHarness.Steps.Add((26f, "hull snap probe", () =>
            {
                if (DevHarness.Off("hullsnap"))
                    MelonLoader.MelonCoroutines.Start(Run());
            }));
        }

        private static IEnumerator Run()
        {
            ShipsServerTracker ships = ServiceLocator.GetService<ShipsServerTracker>();
            var stations = new System.Collections.Generic.HashSet<string>(Worlds.AbandonedStations.All.Select(s => s.Name));
            Ships.TrackedShipServer ship = ships?.Ships.Where(s => !s.GetAllStatefulParts<Ships.Interface.Model.Parts.State.BeaconState>()
                .Any(p => stations.Contains(((Ships.Interface.Model.Parts.State.BeaconState)p.State).Name))).OrderByDescending(s => s.PartCount).FirstOrDefault();
            if (ship == null)
                yield break;
            ShipPartsAvailabilityProxy proxy = null;
            for (int wait = 0; wait < 10 && proxy == null; wait++)
            {
                yield return Worlds.StationVisit.Wait(1f);
                proxy = Object.FindObjectsByType<ShipPartsAvailabilityProxy>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(p =>
                {
                    try { return p.ShipId == ship.Id; } catch { return false; }
                });
            }
            if (proxy == null)
            {
                AdvancedMod.Log.Warning("dev: hull snap: no proxy for ship " + ship.Id);
                yield break;
            }
            // The ship's middle in its own frame, from its parts
            Vector3 middle = ship.StatefulParts.Select(p => p.LocalPosition).Aggregate(Vector3.zero, (a, b) => a + b) / Mathf.Max(1, ship.StatefulParts.Count());
            var rays = new (string Name, Vector3 From, Vector3 Direction)[]
            {
                ("underside", middle + Vector3.down * 25f, Vector3.up),
                ("wall +x from inside", middle + Vector3.up * 1.2f, Vector3.right),
                ("wall -z from inside", middle + Vector3.up * 1.2f, Vector3.back),
                ("ceiling from inside", middle + Vector3.up * 0.5f, Vector3.up),
                ("floor from inside", middle + Vector3.up * 1.5f, Vector3.down),
                ("wall -x from inside", middle + Vector3.up * 1.2f, Vector3.left),
                ("wall +z from inside", middle + Vector3.up * 1.2f, Vector3.forward),
                ("wall +x low", middle + Vector3.up * 0.6f, Vector3.right),
                ("wall -x low", middle + Vector3.up * 0.6f, Vector3.left),
            };
            ushort[] parts = { 7347, 7162, 7161, 7343, 7127 };
            PartSettings[] all = Resources.FindObjectsOfTypeAll<PartSettings>();
            foreach (var ray in rays)
            {
                Vector3 origin = proxy.ToWorldPos(new FloatingPartPlacement(ray.From, PartOrientation.Forward));
                Vector3 direction = proxy.ToWorldRotation(Quaternion.LookRotation(ray.Direction, Mathf.Abs(ray.Direction.y) > 0.5f ? Vector3.forward : Vector3.up)) * Vector3.forward;
                if (!ClientScene.PhysicsScene.Raycast(origin, direction, out RaycastHit hit, 40f, RaycastMasks.BuildDetectionLayer))
                {
                    AdvancedMod.Log.Msg($"dev: hull snap: {ray.Name}: no hit");
                    continue;
                }
                PartHit local = proxy.ConvertHitToLocalHit(hit);
                for (int tilt = 1; tilt < 4; tilt++)
                {
                    PartSettings wall = all.FirstOrDefault(x => x != null && x.id == 7343);
                    if (wall == null)
                        break;
                    BuildTilt.Force(7343, tilt);
                    FloatingSnap.HitCollider = hit.collider;
                    FloatingSnap.Placing = wall;
                    var tiltProps = new HitSnapDetectionProperties(local, Quaternion.identity, wall.size, wall.Bounds);
                    IPartPlacement tilted = new SnapDetectionStrategyProvider().GetSnapDetector(wall.snappingStyle, hit.collider.tag).DetectSnapPlacement(tiltProps, proxy);
                    AdvancedMod.Log.Msg($"dev: hull snap: {ray.Name}: 7343 tilt {tilt} -> " + (tilted is FloatingPartPlacement tf ? $"at {tf.Coord:F2} forward {tf.ForwardOrientation} up {tf.UpOrientation}" : "nothing"));
                }
                BuildTilt.Force(7343, 0);
                foreach (ushort id in parts)
                {
                    PartSettings settings = all.FirstOrDefault(s => s != null && s.id == id);
                    if (settings == null)
                        continue;
                    FloatingSnap.HitCollider = hit.collider;
                    FloatingSnap.Placing = settings;
                    var props = new HitSnapDetectionProperties(local, Quaternion.identity, settings.size, settings.Bounds);
                    IPartPlacement placement = new SnapDetectionStrategyProvider().GetSnapDetector(settings.snappingStyle, hit.collider.tag).DetectSnapPlacement(props, proxy);
                    string where = placement is FloatingPartPlacement f ? $"at {f.Coord:F2} forward {f.ForwardOrientation} up {f.UpOrientation}" : "nothing";
                    AdvancedMod.Log.Msg($"dev: hull snap: {ray.Name}: hit {hit.collider.name} tag {hit.collider.tag} normal {local.Normal:F2}: {id} -> {where}");
                }
            }
        }
    }
}

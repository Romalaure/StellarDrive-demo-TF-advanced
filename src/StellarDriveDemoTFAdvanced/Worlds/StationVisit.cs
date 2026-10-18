using System.Collections;
using System.Linq;
using System.Reflection;
using Core.Services;
using FishNet;
using HarmonyLib;
using Players;
using Players.Network.Services;
using Players.Services;
using Ships;
using Ships.Interface.Model.Parts.State;
using Ships.Parts.Common.Model;
using Ships.Services;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;
using WorldTracking.Interface.Model;
using WorldTracking.Interface.Services;

namespace StellarDriveDemoTFAdvanced.Worlds
{
    /// <summary>
    /// Development check (DevVisitStations): lists the beacons a client sees, then puts the host
    /// inside each abandoned station and outside facing it, taking screenshots.
    /// </summary>
    internal static class StationVisit
    {
        private static readonly MethodInfo NotifyAtShip = AccessTools.Method(typeof(PlayerRespawnService), "NotifyRespawnAtShip");

        public static void Install()
        {
            DevHarness.Steps.Add((32f, "station visits", () =>
            {
                if (Settings.DevVisitStations.Value)
                    MelonLoader.MelonCoroutines.Start(Visit());
            }));
        }

        private static IEnumerator Visit()
        {
            var beacons = Object.FindFirstObjectByType<ShipBeaconsClientTracker>();
            AdvancedMod.Log.Msg("dev: beacons seen by the client: " + (beacons == null ? "no tracker"
                : string.Join(", ", beacons.GetBeacons().Select(b => $"'{b.State.Name}' (ship {b.Context.ShipId})"))));
            ShipsServerTracker ships = ServiceLocator.GetService<ShipsServerTracker>();
            foreach (AbandonedStations.StationSpec spec in AbandonedStations.All)
            {
                TrackedShipServer station = ships.Ships.FirstOrDefault(s => s.GetAllStatefulParts<BeaconState>().Any(p => ((BeaconState)p.State).Name == spec.Name));
                if (station == null)
                {
                    AdvancedMod.Log.Warning("dev: station missing: " + spec.Name);
                    continue;
                }
                string tag = spec.Seed.ToString();
                Move(station, new Vector3(0f, 0.1f, 0f), Quaternion.LookRotation(Vector3.forward, Vector3.up));
                yield return Wait(8f);
                DevHarness.Screenshot($"station_{tag}_inside");
                foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Concat(Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.parent == null).Select(t => t.gameObject)).Distinct()
                    .Where(r => r.name.StartsWith("ship " + station.Id + " ")))
                {
                    var colliders = root.GetComponentsInChildren<Collider>(true);
                    AdvancedMod.Log.Msg($"dev:   {root.name} scene {root.scene.name} active {root.activeInHierarchy}: {colliders.Length} colliders ({colliders.Count(c => c.enabled && c.gameObject.activeInHierarchy)} active), {root.GetComponentsInChildren<Renderer>(true).Length} renderers, "
                        + $"{root.GetComponentsInChildren<DecorColliders>(true).Length} dressing(s) with {colliders.Count(c => c.GetComponentInParent<DecorColliders>() != null)} solid pieces");
                }
                yield return Wait(0.5f);
                Move(station, new Vector3(18f, 10f, -32f), Quaternion.LookRotation(new Vector3(-18f, -9f, 32f).normalized, Vector3.up));
                yield return Wait(5f);
                DevHarness.Screenshot($"station_{tag}_outside");
                // The docking door from outside and from inside, and the whole station, in its own frame
                var plan = new AbandonedStations.Blueprint(spec.Seed) { Spec = spec };
                spec.Build(plan);
                var views = new System.Collections.Generic.List<(Vector3 At, Vector3 Look)> { (new Vector3(26f, 16f, -38f), new Vector3(-26f, -16f, 38f)) };
                foreach ((int x, int y, int z, int face) in plan.Doors)
                {
                    Vector3 out_ = AbandonedStations.Blueprint.Direction(face);
                    Vector3 side = (Vector3)AbandonedStations.Blueprint.DoorSide(face) * 0.5f;
                    Vector3 center = new Vector3(x, y + 0.5f, z) + side + out_ * 0.5f;
                    views.Add((center + out_ * 6f + Vector3.up * 1.5f, -out_ * 6f - Vector3.up * 1.5f));
                    views.Add((center - out_ * 3.5f, out_));
                }
                yield return Parts.Showcase.PhotographFrom(station.Id, $"station_{tag}_view", views);
                if (spec.Merchant && TFItems.TFItemsMod.Active)
                {
                    // The trader's window, and a trade the player cannot pay for
                    TFItems.Devices.TraderMenu.Show();
                    TFItems.Devices.Trader.Request(0, 1);
                    yield return Wait(1.5f);
                    // (the window is drawn by OnGUI: a screen capture, not a camera render)
                    ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(DevHarness.Folder, "trader_menu.png"));
                    yield return Wait(0.5f);
                    TFItems.Devices.TraderMenu.Tab = 1;
                    yield return Wait(0.5f);
                    ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(DevHarness.Folder, "trader_missions.png"));
                    yield return Wait(0.5f);
                    TFItems.Common.ModMenu.Close();
                    // From the hangar's mouth, looking in
                    var hall = plan.Modules[0];
                    Vector3 mouth = new Vector3(hall.x1 + 0.6f + 19f, hall.y0 + 1.5f, (hall.z0 + hall.z1) * 0.5f);
                    yield return Parts.Showcase.PhotographFrom(station.Id, $"station_{tag}_hangar", new System.Collections.Generic.List<(Vector3, Vector3)>
                    {
                        (mouth + new Vector3(6f, 2f, 0f), Vector3.left),
                        (mouth + new Vector3(-3f, 0.5f, 3f), new Vector3(-1f, -0.2f, -0.6f))
                    });
                }
                AdvancedMod.Log.Msg($"dev: visited {spec.Name} (ship {station.Id}, {station.PartCount} parts)");
                yield return Wait(0.5f);
            }
        }

        internal static IEnumerator Wait(float seconds)
        {
            float until = Time.unscaledTime + seconds;
            while (Time.unscaledTime < until)
                yield return null;
        }

        // Like a respawn on a ship: into the station's own space, then the client is told
        internal static void Move(TrackedShipServer station, Vector3 local, Quaternion look)
        {
            ISpacesServerProvider spaces = ServiceLocator.GetService<ISpacesServerProvider>();
            PlayersServerTracker players = ServiceLocator.GetService<PlayersServerTracker>();
            PlayerRespawnService respawn = Object.FindFirstObjectByType<PlayerRespawnService>();
            var connection = InstanceFinder.ClientManager.Connection;
            TrackedPlayerServer player = players?.GetTrackedPlayerFromConnectionId(connection.ClientId);
            if (spaces == null || player == null || respawn == null || NotifyAtShip == null)
                return;
            spaces.SetWorldObjectPhysics(player, station.RelativeSpaceId, new LocalPhysicsState { Position = local, Rotation = look, Velocity = Vector3.zero });
            NotifyAtShip.Invoke(respawn, new object[] { connection, player.PhysicsState, look, FishNet.Transporting.Channel.Reliable });
        }
    }
}

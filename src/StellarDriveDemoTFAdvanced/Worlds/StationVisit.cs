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
                    AdvancedMod.Log.Msg($"dev:   {root.name} scene {root.scene.name} active {root.activeInHierarchy}: {colliders.Length} colliders ({colliders.Count(c => c.enabled && c.gameObject.activeInHierarchy)} active), {root.GetComponentsInChildren<Renderer>(true).Length} renderers");
                }
                yield return Wait(0.5f);
                Move(station, new Vector3(18f, 10f, -32f), Quaternion.LookRotation(new Vector3(-18f, -9f, 32f).normalized, Vector3.up));
                yield return Wait(5f);
                DevHarness.Screenshot($"station_{tag}_outside");
                AdvancedMod.Log.Msg($"dev: visited {spec.Name} (ship {station.Id}, {station.PartCount} parts)");
                yield return Wait(0.5f);
            }
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.unscaledTime + seconds;
            while (Time.unscaledTime < until)
                yield return null;
        }

        // Like a respawn on a ship: into the station's own space, then the client is told
        private static void Move(TrackedShipServer station, Vector3 local, Quaternion look)
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

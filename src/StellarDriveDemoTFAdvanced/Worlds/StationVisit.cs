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
                if (Settings.DevVisitStationSeed.Value != 0 && spec.Seed != Settings.DevVisitStationSeed.Value)
                    continue;
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
                    // The freighter from its bow, its port side, its stern and above; its hall and hold
                    var hall = plan.Modules[0];
                    yield return Parts.Showcase.PhotographFrom(station.Id, $"station_{tag}_ship", new System.Collections.Generic.List<(Vector3, Vector3)>
                    {
                        (new Vector3(9f, 3f, 30f), new Vector3(-9f, -2f, -28f)),
                        (new Vector3(-24f, 6f, 2f), new Vector3(24f, -5f, -2f)),
                        (new Vector3(10f, 5f, -32f), new Vector3(-10f, -4f, 30f)),
                        (new Vector3(14f, 22f, -4f), new Vector3(-14f, -20f, 4f)),
                        (new Vector3(hall.x1 - 0.3f, hall.y0 + 1.2f, hall.z1 - 0.3f), new Vector3(-1f, -0.15f, -0.8f)),
                        (new Vector3(2.5f, 1.2f, -2.5f), new Vector3(-0.6f, -0.15f, -1f))
                    });
                }
                AdvancedMod.Log.Msg($"dev: visited {spec.Name} (ship {station.Id}, {station.PartCount} parts)");
                yield return Wait(0.5f);
                yield return EmptyALootChest(station);
                yield return GalaxyOnMap();
                yield return FlyIn(station, ships);
            }
        }

        // The host, in a suit, coming from a free trajectory 250 m out at 20 m/s straight at the station:
        // it should join the station's space and be stopped by its hull (DevDisable stationspaces: goes through)
        private static IEnumerator FlyIn(TrackedShipServer station, ShipsServerTracker ships)
        {
            var spaces = ServiceLocator.GetService<WorldTracking.Spaces.SpacesServer>();
            var time = ServiceLocator.GetService<IWorldTimeProviderServer>();
            var transpose = ServiceLocator.GetService<IWorldTransposeServer>();
            PlayersServerTracker players = ServiceLocator.GetService<PlayersServerTracker>();
            PlayerRespawnService respawn = Object.FindFirstObjectByType<PlayerRespawnService>();
            var connection = InstanceFinder.ClientManager.Connection;
            TrackedPlayerServer player = players?.GetTrackedPlayerFromConnectionId(connection.ClientId);
            if (spaces == null || time == null || player == null || respawn == null || !(spaces.GetSpaceServerState(station.SpaceId) is WorldTracking.Spaces.Model.FixedSpaceServerState home))
            {
                AdvancedMod.Log.Warning("dev: fly-in: no player or no station space");
                yield break;
            }
            var start = home.GetObjectWorldPos(station);
            start.positionWithinSystemCell += new Vector3(0f, 0f, -250f);
            start.UpdateCells();
            var free = spaces.CreateSpace(new WorldTracking.Interface.Model.Spaces.TrajectorySpace { InitialPosition = start, Velocity = Core.Values.Vector3Double.Zero, WorldTime = time.CurrentWorldTime });
            Quaternion look = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            spaces.SetWorldObjectPhysics(player, free.Id, new LocalPhysicsState { Position = Vector3.zero, Rotation = look, Velocity = new Vector3(0f, 0f, 20f) });
            NotifyAtShip.Invoke(respawn, new object[] { connection, player.PhysicsState, look, FishNet.Transporting.Channel.Reliable });
            AdvancedMod.Log.Msg($"dev: fly-in: player in space {free.Id}, 250 m from station {station.Id} (space {station.SpaceId})");
            float until = Time.unscaledTime + 10f, nextLog = Time.unscaledTime + 1f;
            uint told = player.SpaceId;
            while (Time.unscaledTime < until)
            {
                // Pushed like a jetpack, the client told when the space changes
                spaces.SetWorldObjectPhysics(player, player.SpaceId, new LocalPhysicsState { Position = player.Position, Rotation = look, Velocity = new Vector3(0f, 0f, 8f) });
                if (player.SpaceId != told)
                {
                    told = player.SpaceId;
                    NotifyAtShip.Invoke(respawn, new object[] { connection, player.PhysicsState, look, FishNet.Transporting.Channel.Reliable });
                }
                if (Time.unscaledTime >= nextLog)
                {
                    nextLog += 1f;
                    Vector3 at = transpose.Position(player.SpaceId, station.SpaceId, player.Position) - station.Position;
                    AdvancedMod.Log.Msg($"dev: fly-in t={10f - (until - Time.unscaledTime):0} s: space {player.SpaceId}{(player.SpaceId == station.SpaceId ? " (station's)" : "")}, {at.z:0.0} m along, {at.magnitude:0.0} m from the station center, player scene '{player.ServerRigidbody.gameObject.scene.name}', station scene '{(station.IsRigidbodyLoaded ? station.RigidbodyObject.scene.name : "not loaded")}'");
                }
                yield return null;
            }
            DevHarness.Screenshot("station_flyin");
            // Far from it again: the player flies on in a space of its own
            spaces.SetWorldObjectPhysics(player, station.SpaceId, new LocalPhysicsState { Position = station.Position + new Vector3(0f, 0f, -4500f), Rotation = look, Velocity = Vector3.zero });
            NotifyAtShip.Invoke(respawn, new object[] { connection, player.PhysicsState, look, FishNet.Transporting.Channel.Reliable });
            yield return Wait(3f);
            AdvancedMod.Log.Msg($"dev: fly-out: player in space {player.SpaceId} ({spaces.GetSpace(player.SpaceId)?.GetType().Name}), station's {station.SpaceId}, {transpose.Position(player.SpaceId, station.SpaceId, player.Position).magnitude - station.Position.magnitude:0} m");
        }

        // The star maps shown: a look at one's middle hits it, and the galaxy cycles
        private static IEnumerator GalaxyOnMap()
        {
            yield return Wait(1f);
            Camera camera = MainCamera.Get();
            AdvancedMod.Log.Msg($"dev: star maps shown: {MapSystemFilter.Shown.Count}");
            foreach (MapSystemFilter map in MapSystemFilter.Shown.ToList())
            {
                Renderer any = map.GetComponentInChildren<Renderer>();
                if (any == null || camera == null)
                    continue;
                Vector3 target = any.bounds.center;
                var ray = new Ray(target - camera.transform.forward * 2f, camera.transform.forward);
                bool hit = map.Hit(ray, out float distance);
                string before = MapSystemFilter.ViewName;
                MapSystemFilter.Cycle();
                AdvancedMod.Log.Msg($"dev: star map hit {hit} at {distance:0.0} m: {before} -> {MapSystemFilter.ViewName}");
                MapSystemFilter.Cycle();
                MapSystemFilter.Cycle();
            }
        }

        // A loot chest of the station emptied slot by slot (as a player taking the loot does): it should go
        private static IEnumerator EmptyALootChest(TrackedShipServer station)
        {
            Ships.Interface.Model.Parts.StatefulPart chest = station.StatefulParts.FirstOrDefault(p => p.Settings != null && LootChests.IsLootChest(p.Settings.id));
            if (chest == null || !(chest.State is Ships.Interface.Model.Parts.StateTypes.IStoragePart storage))
            {
                AdvancedMod.Log.Msg("dev: no loot chest in " + station.Id);
                yield break;
            }
            int filled = Enumerable.Range(0, storage.SlotCount).Count(i => storage.GetSlot((byte)i).HasItem);
            for (byte i = 0; i < storage.SlotCount; i++)
                station.SetChestSlot(chest.Id, i, default(global::Items.Model.InventorySlot));
            yield return Wait(1f);
            AdvancedMod.Log.Msg($"dev: loot chest {chest.Id} ({chest.Settings.id}, {filled} slots filled) emptied: still there {station.TryGetStatefulPart(chest.Id, out _)}");
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

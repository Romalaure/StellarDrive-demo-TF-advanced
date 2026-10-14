using System.Collections;
using System.Linq;
using Core.Services;
using HarmonyLib;
using Ships;
using Ships.Cables;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.State;
using Ships.Interface.Model.Parts.State;
using Ships.Physics;
using Ships.Physics.Model;
using Ships.Services;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Combat
{
    /// <summary>A pirate's weapons read its pilot's trigger instead of a signal cable.</summary>
    [HarmonyPatch(typeof(ConnectionsTracker), nameof(ConnectionsTracker.GetSignalValue))]
    internal static class PirateTriggerPatch
    {
        private static void Postfix(ConnectionsTracker __instance, EntityPartSocket socket, ref double __result)
        {
            if (Pirates.All.Count == 0 || socket.Socket.SocketId != 0 || !(__instance is ConnectionsServerTracker))
                return;
            if (Pirates.TryWeaponSignal(socket.EntityId, socket.Socket.PartId, out double value))
                __result = value;
        }
    }

    /// <summary>A pirate's pilot steers its ship on the host's physics step.</summary>
    [HarmonyPatch(typeof(ShipServerPhysics), "UpdateRootRigidbody")]
    internal static class PirateSteeringPatch
    {
        private static readonly AccessTools.FieldRef<ShipRigidbody, Rigidbody> Body =
            AccessTools.FieldRefAccess<ShipRigidbody, Rigidbody>("_rigidbody");

        private static ShipsServerTracker _ships;

        private static void Postfix(float deltaTime, IShipStateWrite ____ship, ShipRigidbody ____shipRigidbody)
        {
            if (Pirates.All.Count == 0 || ____ship == null || !Pirates.All.TryGetValue(____ship.Id, out Pirate pirate))
                return;
            try
            {
                if (_ships == null)
                    _ships = ServiceLocator.GetService<ShipsServerTracker>();
                TrackedShipServer self = _ships?.GetShip(____ship.Id);
                Rigidbody body = ____shipRigidbody != null ? Body(____shipRigidbody) : null;
                if (self != null && body != null && !body.isKinematic)
                    Pirates.Steer(pirate, self, body, deltaTime);
            }
            catch (System.Exception e)
            {
                ErrorLog.Report("pirate pilot", e);
            }
        }
    }

    /// <summary>
    /// Development check (DevPirateTest): pirates jump in 500 m from the ship nearest the player
    /// and attack it; the log follows their distance, hull and fire, and the target's parts.
    /// </summary>
    internal static class PirateTest
    {
        public static void Install()
        {
            DevHarness.Steps.Add((26f, "pirate test", () =>
            {
                if (Settings.DevPirateTest.Value > 0)
                    MelonLoader.MelonCoroutines.Start(Run(Settings.DevPirateTest.Value));
            }));
        }

        private static IEnumerator Run(int which)
        {
            var players = ServiceLocator.GetService<Players.Services.PlayersServerTracker>();
            var player = players?.ActivePlayers.FirstOrDefault();
            ShipsServerTracker ships = ServiceLocator.GetService<ShipsServerTracker>();
            if (player == null || ships == null || !Pirates.Enabled)
            {
                AdvancedMod.Log.Warning($"dev: pirate test: no player, ships or Arsenal (enabled {Pirates.Enabled})");
                yield break;
            }
            TrackedShipServer target = ships.LoadedRootShips.Where(s => s.PhysicsState.SpaceId == player.SpaceId && s.PartCount > 3)
                .OrderBy(s => (s.PhysicsState.Position - player.Position).sqrMagnitude).FirstOrDefault();
            if (target == null)
            {
                AdvancedMod.Log.Warning("dev: pirate test: no ship near the player");
                yield break;
            }
            int startParts = target.PartCount;
            Pirates.ForceSpawn(target, which == 2 ? PirateShips.Cruiser : PirateShips.Corsair, which == 3 ? 2 : 1, 500f);
            uint[] spawned = Pirates.All.Keys.ToArray();
            for (int i = 0; i < 25; i++)
            {
                float until = Time.unscaledTime + 2f;
                while (Time.unscaledTime < until)
                    yield return null;
                foreach (Pirate pirate in Pirates.All.Values.ToList())
                {
                    TrackedShipServer self = ships.GetShip(pirate.ShipId);
                    if (self == null)
                        continue;
                    Vector3 to = target.PhysicsState.Position - self.PhysicsState.Position;
                    float angle = Vector3.Angle(self.PhysicsState.Rotation * Vector3.forward, to);
                    AdvancedMod.Log.Msg($"dev: pirate test t={2 * (i + 1)} s: planet {(pirate.PlanetCenter.HasValue ? Vector3.Angle(self.PhysicsState.Position - pirate.PlanetCenter.Value, target.PhysicsState.Rotation * Vector3.up).ToString("0") + "° off target up" : "no")}, {pirate.Design.Name} {pirate.ShipId} distance {to.magnitude:0} m, nose off by {angle:0}°, speed {(self.PhysicsState.Velocity - target.PhysicsState.Velocity).magnitude:0} m/s, hull {pirate.Health:0}, guns {pirate.Guns}, weapons {pirate.Weapons.Count}, parts {self.PartCount}; target parts {target.PartCount}/{startParts}");
                }
                // Fight back: Arsenal missiles at the first pirate, as a player would fire them
                System.Type missiles = HarmonyLib.AccessTools.TypeByName("Arsenal.Vehicles.Missiles");
                Pirate prey = Pirates.All.Values.FirstOrDefault();
                Transform preyVisual = prey != null ? Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).FirstOrDefault(t => t.parent == null && t.name == $"ship {prey.ShipId} visual") : null;
                if (missiles != null && preyVisual != null && i >= 6 && i % 2 == 0)
                {
                    Vector3 from = preyVisual.position + preyVisual.forward * 60f + preyVisual.up * 5f;
                    HarmonyLib.AccessTools.Method(missiles, "Launch").Invoke(null, new object[] { from, (preyVisual.position - from).normalized * 40f, prey.ShipId, 0u, true, 0u, -1 });
                    AdvancedMod.Log.Msg($"dev: pirate test: missile fired at {prey.ShipId}");
                }
                if (i == 5)
                {
                    Pirate first = Pirates.All.Values.FirstOrDefault();
                    if (first != null)
                        yield return Parts.Showcase.Photograph(first.ShipId, "pirate");
                }
                if (Pirates.All.Count == 0)
                {
                    AdvancedMod.Log.Msg("dev: pirate test: no pirate left");
                    yield return Worlds.StationVisit.Wait(3f);
                    foreach (uint id in spawned.Concat(ships.LoadedRootShips.Where(s => s.PartCount <= 3 && s.StatefulParts.Any(p => p.Settings != null && p.Settings.id == 30)).Select(s => s.Id)).Distinct())
                    {
                        TrackedShipServer wreck = ships.GetShip(id);
                        var chest = wreck?.StatefulParts.FirstOrDefault(p => p.Settings != null && p.Settings.id == 30);
                        int stacks = chest?.State is Ships.Interface.Model.Parts.State.StorageState storage ? Enumerable.Range(0, storage.SlotCount).Count(k => storage.GetSlot((byte)k).HasItem) : -1;
                        AdvancedMod.Log.Msg($"dev: pirate test: wreck {id}: {(wreck == null ? "gone" : $"{wreck.PartCount} parts, loot chest {(chest != null ? $"with {stacks} stack(s)" : "destroyed")}")}");
                        if (wreck != null)
                            yield return Parts.Showcase.Photograph(id, "pirate_wreck");
                    }
                    yield break;
                }
            }
        }
    }
}

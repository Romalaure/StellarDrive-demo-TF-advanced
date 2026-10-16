using System.Collections;
using System.Linq;
using Core.Services;
using Managers.Client.Utils;
using Saving.Services.Converters;
using Saving.Services.Model.State;
using WorldTracking.Spaces;
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
            if (DevHarness.Active && ____ship != null && ____shipRigidbody != null)
                StabilityProbe.Physics(____ship.Id, Body(____shipRigidbody));
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
            DevHarness.Steps.Add((24f, "pirate showcase", () =>
            {
                if (Settings.DevPirateShowcase.Value)
                    MelonLoader.MelonCoroutines.Start(Showcase());
            }));
            DevHarness.Steps.Add((26f, "pirate test", () =>
            {
                if (Settings.DevPirateTest.Value > 0)
                    MelonLoader.MelonCoroutines.Start(Run(Settings.DevPirateTest.Value));
            }));
        }

        /// <summary>Each pirate design, held still beside the player, photographed (dev/pirate_{key}_*.png).</summary>
        private static IEnumerator Showcase()
        {
            var players = ServiceLocator.GetService<Players.Services.PlayersServerTracker>();
            var player = players?.ActivePlayers.FirstOrDefault();
            for (int wait = 0; player == null && players != null && wait < 30; wait++)
            {
                yield return Worlds.StationVisit.Wait(1f);
                player = players.ActivePlayers.FirstOrDefault();
            }
            SpacesServer spaces = ServiceLocator.GetService<SpacesServer>();
            ShipsServerTracker ships = ServiceLocator.GetService<ShipsServerTracker>();
            var ids = ServiceLocator.GetService<WorldTracking.Services.WorldObjectsIdsServerTracker>();
            if (player == null || spaces == null || ships == null || ids == null)
                yield break;
            foreach (PirateDesign design in PirateShips.All)
            {
                uint id = Parts.Showcase.LoadShip(design.Json, player, spaces, ships, ids, 18f);
                MelonLoader.MelonCoroutines.Start(Construction.SnapTest.Pin(spaces, ships, id, 14f));
                yield return Worlds.StationVisit.Wait(4f);
                TrackedShipServer ship = ships.GetShip(id);
                AdvancedMod.Log.Msg($"dev: pirate showcase {design.Name}: {ship?.PartCount ?? 0} parts");
                yield return Parts.Showcase.Photograph(id, "pirate_" + design.Key);
                if (ship != null)
                    ships.RemoveShip(ship.State);
                yield return Worlds.StationVisit.Wait(1f);
            }
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
                // No ship around: a target platform appears 30 m above the player
                SpacesServer spaces = ServiceLocator.GetService<SpacesServer>();
                var ids = ServiceLocator.GetService<WorldTracking.Services.WorldObjectsIdsServerTracker>();
                var data = JsonUtility.FromJson<ShipImportExportUtils.ShipImportExportData>(PirateShips.Target());
                SavedShipState state = new ShipsConverter().ConvertToState(data.ships).States.Cast<SavedShipState>().Single();
                state.Id = ids.CreateUniqueIdForObject();
                state.SpaceId = player.SpaceId;
                state.Position = player.Position + player.Rotation * Vector3.up * 150f;
                state.Rotation = player.Rotation;
                // In the client's physics space: the Arsenal guns aim and hit there
                uint physicsSpace = WorldTracking.Interface.Values.ClientOrigin.LocalPhysicsSpaceId;
                var transpose = ServiceLocator.GetService<WorldTracking.Interface.Services.IWorldTransposeServer>();
                if (physicsSpace != 0 && physicsSpace != state.SpaceId && transpose != null)
                {
                    state.Position = transpose.Position(state.SpaceId, physicsSpace, state.Position);
                    state.SpaceId = physicsSpace;
                }
                state.RelativeSpaceId = spaces.GetOrCreateShipRelativeSpace(state.Id);
                ships.LoadShip(state);
                yield return Worlds.StationVisit.Wait(2f);
                target = ships.GetShip(state.Id);
                AdvancedMod.Log.Msg($"dev: pirate test: target platform {state.Id} made (space {state.SpaceId}, player {player.SpaceId})");
            }
            if (target == null)
                yield break;
            int startParts = target.PartCount;
            float home = Zones.TryOffset(Zones.HomePlanet, target.PhysicsState.SpaceId, target.PhysicsState.Position, out Vector3 toHome) ? toHome.magnitude / 1000f : -1f;
            AdvancedMod.Log.Msg($"dev: pirate test: target {home:0} km from the home planet, safe zone {Zones.InSafeZone(target.PhysicsState.SpaceId, target.PhysicsState.Position)}, danger {Zones.Danger(target.PhysicsState.SpaceId, target.PhysicsState.Position)}");
            if (which == 6)
            {
                // A pirate base 700 m away
                var spec = Worlds.AbandonedStations.All.First(s => s.Pirate);
                var blueprint = new Worlds.AbandonedStations.Blueprint(spec.Seed) { Spec = spec };
                spec.Build(blueprint);
                var baseData = JsonUtility.FromJson<ShipImportExportUtils.ShipImportExportData>(blueprint.ToJson(spec.Name));
                SavedShipState baseState = new ShipsConverter().ConvertToState(baseData.ships).States.Cast<SavedShipState>().Single();
                var baseIds = ServiceLocator.GetService<WorldTracking.Services.WorldObjectsIdsServerTracker>();
                SpacesServer baseSpaces = ServiceLocator.GetService<SpacesServer>();
                baseState.Id = baseIds.CreateUniqueIdForObject();
                baseState.SpaceId = target.PhysicsState.SpaceId;
                baseState.Position = target.PhysicsState.Position + target.PhysicsState.Rotation * new Vector3(700f, 60f, 0f);
                baseState.Rotation = Quaternion.LookRotation(target.PhysicsState.Position - baseState.Position);
                baseState.RelativeSpaceId = baseSpaces.GetOrCreateShipRelativeSpace(baseState.Id);
                ships.LoadShip(baseState);
                Pirates.AddBase(baseState.Id);
                AdvancedMod.Log.Msg($"dev: pirate test: base {baseState.Id} built with {baseState.StatefulParts.Length} parts");
            }
            else if (which == 16)
            {
                // The loot ping alone: a capsule dropped by the target ship as if it were a destroyed scout
                Pirates.DevDropLoot(target);
                yield return Worlds.StationVisit.Wait(3f);
                AdvancedMod.Log.Msg("dev: pirate test: loot pings " + LootPing.DevState());
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(DevHarness.Folder, "pirate_lootping.png"));
                yield return Worlds.StationVisit.Wait(2f);
                AdvancedMod.Log.Msg("dev: pirate test: loot pings " + LootPing.DevState());
                yield break;
            }
            else
            {
                // 1-4: scout, corsair, raider, cruiser; 5: one at random; 7-10: hornet, lancer, torpedo boat, dreadnought;
                // 11-15: a raid as drawn for danger 1 to 5
                if (which >= 11 && which <= 15)
                {
                    for (int danger = 1; danger <= Zones.MaxDanger; danger++)
                    {
                        var counts = new System.Collections.Generic.Dictionary<string, int>();
                        for (int k = 0; k < 200; k++)
                        {
                            string drawn = PirateShips.Random(danger).Key;
                            counts[drawn] = counts.TryGetValue(drawn, out int n) ? n + 1 : 1;
                        }
                        AdvancedMod.Log.Msg($"dev: pirate test: danger {danger} draws {string.Join(", ", counts.Select(kv => kv.Key + " " + kv.Value))}");
                    }
                }
                PirateDesign design = which >= 1 && which <= 4 ? PirateShips.All[which - 1]
                    : which >= 7 && which <= 10 ? PirateShips.All[which - 3]
                    : which >= 11 && which <= 15 ? PirateShips.Random(which - 10) : PirateShips.Random();
                Pirates.ForceSpawn(target, design, 1, 500f);
            }
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
                    AdvancedMod.Log.Msg($"dev: pirate test t={2 * (i + 1)} s: {(pirate.IsBase ? $"bearing {pirate.Bearing.Count}, " : "")}planet {(pirate.PlanetCenter.HasValue ? Vector3.Angle(self.PhysicsState.Position - pirate.PlanetCenter.Value, target.PhysicsState.Rotation * Vector3.up).ToString("0") + "° off target up" : "no")}, {pirate.Design.Name} {pirate.ShipId} distance {to.magnitude:0} m, nose off by {angle:0}°, speed {(self.PhysicsState.Velocity - target.PhysicsState.Velocity).magnitude:0} m/s, hull {pirate.Health:0}, guns {pirate.Guns}, weapons {pirate.Weapons.Count}, parts {self.PartCount}; target parts {target.PartCount}/{startParts}");
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
                    AdvancedMod.Log.Msg("dev: pirate test: loot pings " + LootPing.DevState());
                    ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(DevHarness.Folder, "pirate_lootping.png"));
                    yield return null;
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

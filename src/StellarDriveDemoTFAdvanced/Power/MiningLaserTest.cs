using System.Collections;
using System.Globalization;
using System.Linq;
using System.Text;
using Core.Services;
using Managers.Client.Utils;
using Planets.Interface.Model;
using Saving.Services.Converters;
using Saving.Services.Model.State;
using SDModKit.Game;
using Ships;
using Ships.Cables;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Interface.Model.Placement;
using Ships.Interface.Settings;
using Ships.Services;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;
using WorldTracking.Interface.Values;
using WorldTracking.Services;
using WorldTracking.Spaces;

namespace StellarDriveDemoTFAdvanced.Power
{
    /// <summary>
    /// Development check (DevMiningLaserTest): a small ship floored with reinforced hull (and the
    /// game's, to compare) lands 8 m from the rock nearest the player, a mining laser on its front
    /// edge aimed at it, powered by a quantum reactor and linked to an item terminal by an item
    /// line. The log follows the laser's status, the rock's health and the terminal's contents;
    /// photos of the ship. Also logs what the reinforced hull patches give (color, mass, cost).
    /// </summary>
    internal static class MiningLaserTest
    {
        private const int Floor = 1, ReinforcedFloor = 7421, ReinforcedGlassFloor = 7431, Terminal = 302;

        public static void Install()
        {
            DevHarness.Steps.Add((26f, "mining laser test", () =>
            {
                if (Settings.DevMiningLaserTest.Value)
                    MelonLoader.MelonCoroutines.Start(Run());
            }));
        }

        private static IEnumerator Run()
        {
            LogHull();
            var players = ServiceLocator.GetService<Players.Services.PlayersServerTracker>();
            var player = players?.ActivePlayers.FirstOrDefault();
            SpacesServer spaces = ServiceLocator.GetService<SpacesServer>();
            ShipsServerTracker ships = ServiceLocator.GetService<ShipsServerTracker>();
            CablesServerTracker cables = ServiceLocator.GetService<CablesServerTracker>();
            var ids = ServiceLocator.GetService<WorldObjectsIdsServerTracker>();
            GameObject body = GameObject.Find("LocalPlayerCollider 0");
            if (player == null || spaces == null || ships == null || cables == null || ids == null || body == null)
            {
                AdvancedMod.Log.Warning("dev: mining laser test: services missing");
                yield break;
            }
            // On a planet with rocks first (DevVisitPlanetId: which one)
            if (Settings.DevVisitPlanetId.Value != 0)
            {
                Planets.Planet planet = Worlds.PlanetFactory.Planets.FirstOrDefault(p => p.Id == (ulong)Settings.DevVisitPlanetId.Value)
                    ?? Object.FindObjectsByType<Planets.Planet>(FindObjectsSortMode.None).FirstOrDefault(p => p.Id == (ulong)Settings.DevVisitPlanetId.Value);
                if (planet != null && Worlds.PlanetVisit.TryFindLand(planet, out Vector3 land))
                {
                    Worlds.PlanetVisit.Place(planet, land, 0f);
                    AdvancedMod.Log.Msg($"dev: mining laser test: landed on {planet.name}");
                    float until = Time.unscaledTime + 15f;
                    while (Time.unscaledTime < until)
                        yield return null;
                    player = players.ActivePlayers.FirstOrDefault();
                    body = GameObject.Find("LocalPlayerCollider 0");
                    if (player == null || body == null)
                        yield break;
                }
            }
            // The nearest rock that can be mined, in the physics scene around the player
            Vector3 feet = body.transform.position;
            Vector3 up = player.Rotation * Vector3.up;
            var buffer = new Collider[8192];
            int count = ClientScene.PhysicsScene.OverlapSphere(feet, 150f, buffer, ~0, QueryTriggerInteraction.Collide);
            Collider rock = buffer.Take(count).Where(c => c != null && c.CompareTag("PlanetSurfaceObject") && c.GetComponentInParent<IMinableObject>() != null)
                .OrderBy(c => Vector3.Distance(c.bounds.center, feet)).FirstOrDefault();
            var all = Object.FindObjectsByType<Planets.Objects.MinablePlanetObject>(FindObjectsSortMode.None);
            AdvancedMod.Log.Msg($"dev: mining laser test: {all.Length} minable objects loaded, feet {feet}; nearest: " + string.Join("; ", all.OrderBy(m => Vector3.Distance(m.transform.position, feet)).Take(4)
                .Select(m => $"{m.name} at {Vector3.Distance(m.transform.position, feet):0} m scene {m.gameObject.scene.name} colliders {string.Join(",", m.GetComponentsInChildren<Collider>(true).Select(c => c.tag + "/" + c.gameObject.layer + "/" + c.enabled + "/" + c.gameObject.scene.name))}")));
            if (rock == null)
                rock = all.OrderBy(m => Vector3.Distance(m.transform.position, feet)).SelectMany(m => m.GetComponentsInChildren<Collider>()).FirstOrDefault(c => c.enabled && Vector3.Distance(c.bounds.center, feet) < 150f);
            if (rock == null)
            {
                AdvancedMod.Log.Warning($"dev: mining laser test: no rock to mine within 150 m ({count} colliders, {buffer.Take(count).Count(c => c != null && c.CompareTag("PlanetSurfaceObject"))} planet objects)");
                yield break;
            }
            IMinableObject minable = rock.GetComponentInParent<IMinableObject>();
            Vector3 toRock = Vector3.ProjectOnPlane(rock.bounds.center - feet, up);
            Vector3 aim = toRock.sqrMagnitude > 0.01f ? toRock.normalized : player.Rotation * Vector3.forward;
            AdvancedMod.Log.Msg($"dev: mining laser test: rock '{minable.Label}' health {minable.CurrentHealth:0.0}/{minable.MaxHealth:0.0} at {Vector3.Distance(rock.bounds.center, feet):0.0} m, size {rock.bounds.size}, {Height(rock.bounds.center - feet, up):0.0} m above the feet");

            var data = JsonUtility.FromJson<ShipImportExportUtils.ShipImportExportData>(Json());
            SavedShipState state = new ShipsConverter().ConvertToState(data.ships).States.Cast<SavedShipState>().Single();
            state.Id = ids.CreateUniqueIdForObject();
            state.SpaceId = player.SpaceId;
            // Server positions are the physics scene's, moved by the player's offset between the two
            Vector3 offset = player.Position - feet;
            // The laser's beam (part +Y along the ship's +Z) about 0.5 m above the ground, 8 m before the rock
            Vector3 laserFoot = feet + toRock - aim * 8f + up * 0.8f;
            state.Rotation = Quaternion.LookRotation(aim, up);
            state.Position = laserFoot + offset - state.Rotation * new Vector3(0f, 0f, 2.6f);
            state.Velocity = Vector3.zero;
            state.RelativeSpaceId = spaces.GetOrCreateShipRelativeSpace(state.Id);
            ships.LoadShip(state);
            foreach (var cable in CablesConverter.ConvertToState(data.cables))
            {
                CableState c = cable.State;
                CablePlugSocket a = c.PlugA, b = c.PlugB;
                var sa = a.EntityPartSocket; sa.EntityId = state.Id; a.EntityPartSocket = sa;
                var sb = b.EntityPartSocket; sb.EntityId = state.Id; b.EntityPartSocket = sb;
                c.PlugA = a;
                c.PlugB = b;
                cables.AddCable(c);
            }
            AdvancedMod.Log.Msg($"dev: mining laser test ship {state.Id} landed, {state.StatefulParts.Length} parts, terminal part {(CustomParts.Get(Terminal) != null ? "found" : "missing")}");
            for (int i = 0; i < 15; i++)
            {
                float until = Time.unscaledTime + 2f;
                while (Time.unscaledTime < until)
                    yield return null;
                TrackedShipServer ship = ships.GetShip(state.Id);
                if (ship == null)
                {
                    AdvancedMod.Log.Msg("dev: mining laser test ship gone");
                    yield break;
                }
                string status = ship.GetAllStatefulParts<ConstantState>().Where(p => p.Id == 2).Select(p => ((ConstantState)p.State).ConstantValue.ToString("0")).FirstOrDefault() ?? "gone";
                string contents = ship.GetAllStatefulParts<StorageState>().Where(p => p.Id == 3)
                    .Select(p => string.Join(" ", Enumerable.Range(0, ((IStoragePart)p.State).SlotCount).Select(s => ((IStoragePart)p.State).GetSlot((byte)s)).Where(s => s.HasItem).Select(s => s.ItemId + "x" + s.Quantity)))
                    .FirstOrDefault() ?? "no terminal";
                bool alive = rock != null && rock.gameObject.activeInHierarchy;
                AdvancedMod.Log.Msg($"dev: mining laser test t={2 * (i + 1)} s: status {status}, rock {(alive ? minable.CurrentHealth.ToString("0.0") : "mined")}, terminal [{contents}], ship parts {ship.PartCount}");
                if (i == 2)
                    yield return Parts.Showcase.Photograph(state.Id, "mininglaser");
            }
        }

        private static float Height(Vector3 v, Vector3 up) => Vector3.Dot(v, up);

        private static void LogHull()
        {
            var log = new StringBuilder("dev: reinforced hull:");
            foreach (ushort id in new ushort[] { 1, 7421, 2, 7422, 5, 7423, 6, 7424, 36, 7431, 7, 7432, 8, 7433 })
            {
                var settings = Resources.FindObjectsOfTypeAll<HullPartSettings>().FirstOrDefault(h => h != null && h.id == id);
                if (settings == null)
                {
                    log.Append($"\n   {id}: missing");
                    continue;
                }
                var part = new HullPart(settings, default(HullPartPlacement));
                log.Append($"\n   {id} {settings.name} '{settings.fullLabel}' {settings.hullMaterialType} {settings.hullMeshSegmentType} mass {settings.Mass:0.#} cost {string.Join("+", settings.BuildingCost.Select(c => c.ItemId + "x" + c.Quantity))} color {part.Color.Front.x},{part.Color.Front.y},{part.Color.Front.z} pivot {settings.pivotedSettings?.id}");
            }
            AdvancedMod.Log.Msg(log.ToString());
        }

        private static string Json()
        {
            var hull = new StringBuilder();
            for (int x = -2; x <= 2; x++)
            {
                for (int z = -2; z <= 2; z++)
                {
                    if (hull.Length > 0)
                        hull.Append(',');
                    // Reinforced iron on the left, the game's iron in the middle, reinforced glass on the right
                    int id = x < 0 ? ReinforcedFloor : x == 0 ? Floor : ReinforcedGlassFloor;
                    int shade = id == Floor ? 217 : 150;
                    hull.Append($"{{\"partId\":{id},\"x\":{x},\"y\":0,\"z\":{z},\"forwardOrientationId\":4,\"upOrientationId\":0,\"voxelPartTypeId\":0,\"FrontColor\":{{\"x\":{shade},\"y\":{shade},\"z\":{shade + 6}}},\"BackColor\":{{\"x\":{shade},\"y\":{shade},\"z\":{shade + 6}}}}}");
                }
            }
            string Part(int id, int settings, float x, float y, float z, int forward, int upward) => string.Format(CultureInfo.InvariantCulture,
                "{{\"id\":{0},\"settingsId\":{1},\"coord\":{{\"x\":{2},\"y\":{3},\"z\":{4}}},\"forwardOrientationId\":{5},\"upOrientationId\":{6}}}", id, settings, x, y, z, forward, upward);
            // The laser stands on the front edge, +Y along the ship's +Z (Forward 0), its own +Z up (Up 4)
            string parts = string.Join(",", Part(1, PowerCatalog.ReactorId, -1.5f, -0.46f, -1f, 2, 4), Part(2, ShipMiningLaser.Id, 0f, -0.2f, 2.6f, 4, 0),
                Part(3, Terminal, 1.5f, -0.46f, -1f, 2, 4));
            string constants = "{\"partId\":2,\"constantValue\":0.0}";
            string tanks = "{\"partId\":1,\"fluidItemId\":0,\"fluidQuantity\":0.0}";
            string empty = string.Join(",", Enumerable.Repeat("{\"hasItem\":false,\"itemId\":0,\"quantity\":0}", 10));
            string storage = "{\"partId\":3,\"slots\":[" + empty + "]}";
            string Cable(int id, int type, int partA, int socketA, int partB, int socketB) =>
                $"{{\"id\":{id},\"type\":{type},\"plugA\":{{\"shipId\":1,\"partId\":{partA},\"socketId\":{socketA}}},\"plugB\":{{\"shipId\":1,\"partId\":{partB},\"socketId\":{socketB}}}}}";
            // Power: reactor 0 -> laser 1; items: laser 4 -> terminal 0
            string cablesJson = string.Join(",", Cable(1, 2, 1, 0, 2, PowerCatalog.MachinePowerA), Cable(2, 3, 2, ShipMiningLaser.ItemSocket, 3, 0));
            var sb = new StringBuilder();
            sb.Append("{\"rootShipId\":1,\"cables\":[").Append(cablesJson).Append("],\"ships\":[{");
            sb.Append("\"id\":1,\"relativeSpaceId\":0,\"spaceId\":0,\"position\":{\"x\":0,\"y\":0,\"z\":0},\"velocity\":{\"x\":0,\"y\":0,\"z\":0},\"rotation\":{\"x\":0,\"y\":0,\"z\":0,\"w\":1},");
            sb.Append("\"rooms\":[],\"hullParts\":[").Append(hull).Append("],\"statefulFloatingParts\":[").Append(parts).Append("],\"statefulHullParts\":[],\"thrustersStates\":[],");
            sb.Append("\"storageStates\":[").Append(storage).Append("],");
            sb.Append("\"fluidTankStates\":[").Append(tanks).Append("],");
            sb.Append("\"constantStates\":[").Append(constants).Append("],");
            foreach (string list in new[] { "beaconStates", "leverStates", "warpDriveStates", "dockingDoorStates", "targeterStates", "mathGateStates",
                         "unaryMathGateStates", "memoryRegisterStates", "transmitterStates", "rotorStates", "electrolyzerStates",
                         "woodThrusterStates", "woodGeneratorStates", "ethanolDistillerStates" })
                sb.Append('"').Append(list).Append("\":[],");
            sb.Length--;
            sb.Append("}]}");
            return sb.ToString();
        }
    }
}

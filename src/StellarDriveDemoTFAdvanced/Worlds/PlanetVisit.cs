using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core.Services;
using Core.Values;
using FishNet;
using HarmonyLib;
using Planets;
using Planets.Network;
using Planets.Objects.Common.Model;
using Planets.Services;
using Planets.Terrain.Evaluation.Services;
using Players;
using Players.Network.Services;
using Players.Services;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;
using UnityEngine.SceneManagement;
using WorldTracking.Interface.Model.Spaces;
using WorldTracking.Interface.Services;

namespace StellarDriveDemoTFAdvanced.Worlds
{
    /// <summary>
    /// Development check (DevVisitPlanets): the host is moved onto each mod planet in turn, the
    /// way the game respawns a player on a planet surface, looks around, then walks up to the
    /// nearest ruin or wreck, taking screenshots.
    /// </summary>
    internal static class PlanetVisit
    {
        private static readonly MethodInfo NotifyAtSurface = AccessTools.Method(typeof(PlayerRespawnService), "NotifyRespawnAtSurface");

        public static void Install()
        {
            DevHarness.Steps.Add((30f, "planet visits", () =>
            {
                if (Settings.DevVisitPlanets.Value)
                    MelonLoader.MelonCoroutines.Start(VisitAll());
                else if (Settings.DevVisitBlackHole.Value)
                    MelonLoader.MelonCoroutines.Start(BlackHoleTour());
            }));
        }

        // From airless TF Petra, look for the black hole from six spots around the planet
        private static IEnumerator BlackHoleTour()
        {
            Planet petra = PlanetFactory.Planets.FirstOrDefault(p => p.Id == 102);
            if (petra == null)
                yield break;
            Vector3[] spots = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            for (int i = 0; i < spots.Length; i++)
            {
                Place(petra, spots[i], 0f);
                yield return Wait(6f);
                GameObject hole = Object.FindObjectsOfType<Transform>().Where(t => t.parent == null && t.name.StartsWith("TFA Black Hole (far)")).Select(t => t.gameObject).FirstOrDefault();
                IEnumerator shot = WorldDevSteps.ShootAt(hole, "blackhole_petra_" + i);
                while (shot.MoveNext())
                    yield return shot.Current;
            }
        }

        private static IEnumerator VisitAll()
        {
            foreach (Planet planet in new List<Planet>(PlanetFactory.Planets))
            {
                if (Settings.DevVisitPlanetId.Value != 0 && planet.Id != (ulong)Settings.DevVisitPlanetId.Value)
                    continue;
                if (!TryFindLand(planet, out Vector3 up))
                    continue;
                Place(planet, up, 0f);
                AdvancedMod.Log.Msg($"dev: landed on {planet.name}, ground {Elevation(planet, up):0} m above the radius");
                // How high its ground is over the whole planet (to set the altitude bands)
                var heights = new List<float>();
                var random = new System.Random(7);
                for (int k = 0; k < 1500; k++)
                {
                    var direction = new Vector3((float)(random.NextDouble() * 2 - 1), (float)(random.NextDouble() * 2 - 1), (float)(random.NextDouble() * 2 - 1));
                    if (direction.sqrMagnitude > 0.01f)
                        heights.Add(Elevation(planet, direction));
                }
                heights.Sort();
                AdvancedMod.Log.Msg($"dev: {planet.name} ground heights: " + string.Join(", ", new[] { 0, 5, 10, 20, 30, 40, 50, 60, 70, 80, 90, 95, 100 }.Select(q => $"p{q} {heights[Mathf.Min(heights.Count - 1, q * heights.Count / 100)]:0}")));
                yield return Wait(18f);
                int frames = Time.frameCount;
                float start = Time.unscaledTime;
                yield return Wait(6f);
                AdvancedMod.Log.Msg($"dev: fps on {planet.name}: {(Time.frameCount - frames) / (Time.unscaledTime - start):0.0}");
                IEnumerator cut = CutTree(planet);
                while (cut.MoveNext())
                    yield return cut.Current;
                for (int i = 0; i < 3; i++)
                {
                    Place(planet, up, i * 120f);
                    yield return Wait(2f);
                    Galaxy.LogLighting();
                    DevHarness.Screenshot($"visit_{planet.Id}_{i}");
                    yield return Wait(0.5f);
                }

                // From the air, looking down over the forests and regions (the player is falling)
                for (int i = 0; i < 2; i++)
                {
                    Move(planet, up, Quaternion.Euler(38f, i * 180f, 0f), 160f);
                    yield return Wait(1.2f);
                    DevHarness.Screenshot($"visit_{planet.Id}_air{i}");
                    yield return Wait(0.5f);
                }
                Place(planet, up, 0f);
                yield return Wait(4f);

                // The points of interest the game loaded around the player
                GameObject hotspots = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(r => r.name == planet.name + " hotspots visuals");
                PlanetObjectVisuals[] objects = hotspots != null ? hotspots.GetComponentsInChildren<PlanetObjectVisuals>() : new PlanetObjectVisuals[0];
                AdvancedMod.Log.Msg($"dev: {planet.name} has {objects.Length} point-of-interest objects loaded");
                AdvancedMod.Log.Msg($"dev: {planet.name} points of interest loaded: " + string.Join(", ", objects
                    .GroupBy(o => o.name.Replace("(Clone)", "")).Select(g => g.Key + " x" + g.Count())));
                foreach (string kind in new[] { "TFA_RuinHall", "TFA_RuinHouse", "TFA_RuinTower", "TFA_RuinTemple", "TFA_Ore", "TFA_CityTowerTall", "TFA_CityDome", "TFA_FortTowerTall", "TFA_FortTower", "TFA_FortDome" })
                {
                    // Objects unload when the player moves: skip destroyed ones
                    PlanetObjectVisuals site = objects.Where(o => o != null && o.name.StartsWith(kind))
                        .OrderBy(o => Vector3.Angle(o.planetRelativePos, up)).FirstOrDefault();
                    if (site == null)
                        continue;
                    Vector3 target = site.planetRelativePos;
                    Vector3 siteUp = target.normalized;
                    // Stand 30 m away and look at it
                    Vector3 side = Vector3.Cross(siteUp, Vector3.up).normalized;
                    if (side.sqrMagnitude < 0.01f)
                        side = Vector3.right;
                    Vector3 standUp = (target + side * 30f).normalized;
                    PlaceLooking(planet, standUp, target);
                    AdvancedMod.Log.Msg($"dev: walked to {site.name} on {planet.name}");
                    foreach (var physics in Resources.FindObjectsOfTypeAll<Planets.EnvironmentGeneration.Tracking.PlanetObjectPhysics>()
                        .Where(p => p.gameObject.scene.name != null && p.name.StartsWith("TFA_Ruin")).Take(6))
                    {
                        var colliders = physics.GetComponents<Collider>();
                        AdvancedMod.Log.Msg($"dev:   collider {physics.name} scene {physics.gameObject.scene.name} active {physics.gameObject.activeInHierarchy} layer {physics.gameObject.layer} pos {physics.transform.position} " +
                            string.Join("; ", colliders.Select(c => $"{c.GetType().Name} enabled {c.enabled} {(c is MeshCollider m ? "convex " + m.convex + " mesh " + (m.sharedMesh != null ? m.sharedMesh.name + " " + m.sharedMesh.vertexCount : "null") : "")} bounds {c.bounds.size}")));
                    }
                    yield return Wait(4f);
                    DevHarness.Screenshot($"visit_{planet.Id}_{kind}");
                    CheckSpaces(kind);
                    yield return Wait(0.5f);
                }
            }
        }

        // Active world objects left in a space that no longer exists
        private static void CheckSpaces(string tag)
        {
            ISpacesServerProvider spaces = ServiceLocator.GetService<ISpacesServerProvider>();
            var objects = ServiceLocator.GetService<WorldTracking.Services.IWorldObjectsServerProvider>();
            foreach (var o in objects.GetActiveWorldObjects())
            {
                try { spaces.GetSpace(o.SpaceId); }
                catch (System.Exception) { AdvancedMod.Log.Warning($"dev: after {tag}: {o.GetType().Name} {o.Id} is in missing space {o.SpaceId}"); }
            }
        }

        // Mines the nearest tree through the game's own request, then checks the client sees it gone
        private static IEnumerator CutTree(Planet planet)
        {
            PlanetObjectReference tree = Object.FindObjectsByType<PlanetObjectReference>(FindObjectsSortMode.None)
                .FirstOrDefault(r => r.Id is Planets.Objects.Common.Model.DenseObjectId dense && dense.PlanetId == planet.Id
                    && r.GetComponentInParent<Planets.Objects.MinablePlanetObject>() != null);
            if (tree == null)
            {
                AdvancedMod.Log.Msg($"dev: no tree collider near the player on {planet.name}");
                yield break;
            }
            var id = (Planets.Objects.Common.Model.DenseObjectId)tree.Id;
            ServiceLocator.GetService<Managers.Interface.Services.IPlanetInteractionNetworkService>().MinePlanetObject(id);
            yield return Wait(1.5f);
            var state = planet.GetComponent<Planets.Network.PlanetNetworkedState>();
            bool server = state.IsDenseObjectDestroyed(id.PlacedObjectId, true);
            bool client = state.IsDenseObjectDestroyed(id.PlacedObjectId, false);
            var demo = ModPlanetState.Demo;
            var tagged = ModPlanetState.Tag(planet.Id, id.PlacedObjectId);
            bool carried = demo != null && demo.IsDenseObjectDestroyed(tagged, true) && demo.IsDenseObjectDestroyed(tagged, false);
            bool roundTrip = ModPlanetState.TryUntag(tagged, out ulong owner, out var back) && owner == planet.Id && back.Equals(id.PlacedObjectId);
            AdvancedMod.Log.Msg($"dev: cut a tree on {planet.name}: destroyed for the server {server}, for the client {client}, collider gone {tree == null}, carried by the demo planet's synced state {carried}, tag round trip {roundTrip}");
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.unscaledTime + seconds;
            while (Time.unscaledTime < until)
                yield return null;
        }

        private static float Elevation(Planet planet, Vector3 up)
        {
            var calculator = ServiceLocator.GetService<PlanetTerrainPointCpuCalculator>();
            PlanetsServerTracker planets = ServiceLocator.GetService<PlanetsServerTracker>();
            var tracked = (TrackedPlanet)((IDictionary)AccessTools.Field(typeof(PlanetsServerTracker).BaseType, "_planets").GetValue(planets))[planet.Id];
            // The calculator gives the distance from the planet center
            return calculator.GetElevation(tracked.TerrainOperationStack, up.normalized) - planet.Radius;
        }

        // Dry land, above the shore
        internal static bool TryFindLand(Planet planet, out Vector3 up)
        {
            var random = new System.Random((int)planet.Id);
            // On the day side of the planet's own star
            Vector3 toStar = Vector3.zero;
            Stars.Star star = Galaxy.OrbitsErebos(planet.Id) ? Galaxy.Erebos : Galaxy.AllStars.FirstOrDefault(s => s != null && s != Galaxy.Erebos);
            var transpose = ServiceLocator.GetService<WorldTracking.Interface.Services.IWorldTransposeServer>();
            if (star != null && star.Space != null && transpose != null)
                toStar = transpose.Position((WorldTracking.Interface.Model.Spaces.Common.ISpace)star.Space, planet.SurfaceSpace, Vector3.zero).normalized;
            for (int attempt = 0; attempt < 256; attempt++)
            {
                up = new Vector3((float)random.NextDouble() * 2f - 1f, (float)random.NextDouble() * 2f - 1f, (float)random.NextDouble() * 2f - 1f).normalized;
                if (Elevation(planet, up) > 110f && (toStar == Vector3.zero || Vector3.Dot(up, toStar) > 0.45f))
                    return true;
            }
            up = Vector3.up;
            AdvancedMod.Log.Warning("dev: no dry land found on " + planet.name);
            return false;
        }

        // Rotations are given in the surface spot's local frame, y up: yaw turns the view
        internal static void Place(Planet planet, Vector3 up, float yaw)
        {
            Move(planet, up, Quaternion.Euler(0f, yaw, 0f));
        }

        private static void PlaceLooking(Planet planet, Vector3 up, Vector3 target)
        {
            // The target in the stand point's frame: the frame's y is the local up
            Quaternion toLocal = Quaternion.Inverse(Quaternion.FromToRotation(Vector3.up, up));
            Vector3 position = up * (planet.Radius + Elevation(planet, up));
            Vector3 local = toLocal * (target - position);
            local.y *= 0.3f;
            Move(planet, up, Quaternion.LookRotation(local.normalized, Vector3.up));
        }

        private static void Move(Planet planet, Vector3 up, Quaternion look, float height = 1.5f)
        {
            ISpacesServerProvider spaces = ServiceLocator.GetService<ISpacesServerProvider>();
            PlayersServerTracker players = ServiceLocator.GetService<PlayersServerTracker>();
            PlayerRespawnService respawn = Object.FindFirstObjectByType<PlayerRespawnService>();
            var connection = InstanceFinder.ClientManager.Connection;
            TrackedPlayerServer player = players?.GetTrackedPlayerFromConnectionId(connection.ClientId);
            if (spaces == null || player == null || respawn == null || NotifyAtSurface == null)
            {
                AdvancedMod.Log.Warning("dev: cannot move the player");
                return;
            }
            var spot = new PlanetSurfaceSpace
            {
                PlanetId = planet.Id,
                Center = (Vector3Double)up.normalized * (planet.Radius + Elevation(planet, up) + height)
            };
            spaces.MoveWorldObject(player, spot);
            // Surface spaces have the planet's axes: the spot's own frame has y along its up
            look = Quaternion.FromToRotation(Vector3.up, up.normalized) * look;
            player.SetRotation(look);
            var space = (PlanetSurfaceSpace)spaces.GetSpace(player.SpaceId);
            NotifyAtSurface.Invoke(respawn, new object[] { connection, space, player.PhysicsState, look, FishNet.Transporting.Channel.Reliable });
        }
    }
}

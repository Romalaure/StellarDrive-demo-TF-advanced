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
                GameObject hole = Object.FindObjectsOfType<Transform>().Where(t => t.parent == null && t.name == "TFA Black Hole (far)").Select(t => t.gameObject).FirstOrDefault();
                IEnumerator shot = WorldDevSteps.ShootAt(hole, "blackhole_petra_" + i);
                while (shot.MoveNext())
                    yield return shot.Current;
            }
        }

        private static IEnumerator VisitAll()
        {
            foreach (Planet planet in new List<Planet>(PlanetFactory.Planets))
            {
                if (!TryFindLand(planet, out Vector3 up))
                    continue;
                Place(planet, up, 0f);
                AdvancedMod.Log.Msg($"dev: landed on {planet.name}, ground {Elevation(planet, up):0} m above the radius");
                yield return Wait(18f);
                for (int i = 0; i < 2; i++)
                {
                    Place(planet, up, i * 150f);
                    yield return Wait(2f);
                    DevHarness.Screenshot($"visit_{planet.Id}_{i}");
                    yield return Wait(0.5f);
                }

                // The points of interest the game loaded around the player
                GameObject hotspots = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(r => r.name == planet.name + " hotspots visuals");
                PlanetObjectVisuals[] objects = hotspots != null ? hotspots.GetComponentsInChildren<PlanetObjectVisuals>() : new PlanetObjectVisuals[0];
                AdvancedMod.Log.Msg($"dev: {planet.name} has {objects.Length} point-of-interest objects loaded");
                AdvancedMod.Log.Msg($"dev: {planet.name} points of interest loaded: " + string.Join(", ", objects
                    .GroupBy(o => o.name.Replace("(Clone)", "")).Select(g => g.Key + " x" + g.Count())));
                foreach (string kind in new[] { "TFA_Ruin", "Crashed" })
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
                    yield return Wait(4f);
                    DevHarness.Screenshot($"visit_{planet.Id}_{kind}");
                    yield return Wait(0.5f);
                }
            }
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
        private static bool TryFindLand(Planet planet, out Vector3 up)
        {
            var random = new System.Random((int)planet.Id);
            for (int attempt = 0; attempt < 64; attempt++)
            {
                up = new Vector3((float)random.NextDouble() * 2f - 1f, (float)random.NextDouble() * 2f - 1f, (float)random.NextDouble() * 2f - 1f).normalized;
                if (Elevation(planet, up) > 110f)
                    return true;
            }
            up = Vector3.up;
            AdvancedMod.Log.Warning("dev: no dry land found on " + planet.name);
            return false;
        }

        // Rotations are given in the surface spot's local frame, y up: yaw turns the view
        private static void Place(Planet planet, Vector3 up, float yaw)
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

        private static void Move(Planet planet, Vector3 up, Quaternion look)
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
                Center = (Vector3Double)up.normalized * (planet.Radius + Elevation(planet, up) + 1.5)
            };
            spaces.MoveWorldObject(player, spot);
            player.SetRotation(look);
            var space = (PlanetSurfaceSpace)spaces.GetSpace(player.SpaceId);
            NotifyAtSurface.Invoke(respawn, new object[] { connection, space, player.PhysicsState, look, FishNet.Transporting.Channel.Reliable });
        }
    }
}

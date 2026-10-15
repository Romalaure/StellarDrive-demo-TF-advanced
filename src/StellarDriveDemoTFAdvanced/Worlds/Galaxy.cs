using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core.Services;
using Core.Values;
using HarmonyLib;
using Rendering.Services;
using Stars;
using Stars.Interface.Model;
using Stars.Services;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;
using WorldTracking.Interface.Model;
using WorldTracking.Interface.Model.Spaces.Common;
using WorldTracking.Interface.Services;
using WorldTracking.Interface.Values;
using Object = UnityEngine.Object;

namespace StellarDriveDemoTFAdvanced.Worlds
{
    /// <summary>
    /// The Erebos galaxy: a second star system, far out from 442 Otrera (about 0.2 light years),
    /// around its own star, an orange giant. Its star is a copy of the game's star object placed
    /// there; its planets (see <see cref="PlanetCatalog"/>) orbit it instead of Otrera. The game
    /// only expects one star, so the star it lights the scene from, draws the star map around and
    /// tracks is made the nearest one, and both stars stay drawn (each one is a bright point in the
    /// other's sky). The way there is a portal; warp drives are far too slow for such a distance.
    /// </summary>
    internal static class Galaxy
    {
        public const string StarName = "Érèbe";
        /// <summary>From Otrera's star to Erebos's, in meters.</summary>
        private static readonly Vector3Double Offset = new Vector3Double(1.35e15, 2.1e14, -9.4e14);
        /// <summary>Within this distance of Erebos's star, a position is in the Erebos galaxy.</summary>
        public const double Reach = 6e10;

        private static readonly FieldInfo PositionField = AccessTools.Field(typeof(Star), "position");
        private static readonly List<Star> Stars = new List<Star>();
        private static Star _erebos;
        private static Star _home;

        public static Star Erebos => _erebos;

        /// <summary>Creates Erebos's star once per world load (beside the game's own), before the planets that orbit it.</summary>
        public static Star EnsureStar()
        {
            if (_erebos != null)
                return _erebos;
            Star home = Object.FindObjectsByType<Star>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(s => s != null && s.gameObject.scene.name == "MainGame" && !s.name.StartsWith("TFA_"));
            if (home == null)
            {
                AdvancedMod.Log.Warning("galaxy: the game's star was not found, no Erebos");
                return null;
            }
            _home = home;
            var holder = new GameObject("TFA_StarHolder");
            holder.SetActive(false);
            try
            {
                GameObject copy = Object.Instantiate(home.gameObject, holder.transform);
                copy.name = "TFA_Star_Erebos";
                foreach (FishNet.Observing.NetworkObserver observer in copy.GetComponents<FishNet.Observing.NetworkObserver>())
                    Object.DestroyImmediate(observer);
                foreach (FishNet.Object.NetworkObject networkObject in copy.GetComponents<FishNet.Object.NetworkObject>())
                    Object.DestroyImmediate(networkObject);
                var star = copy.GetComponent<Star>();
                PreciseWorldPosition at = new PreciseWorldPosition(home.position, Vector3.zero) + Offset;
                PositionField.SetValue(star, at.worldCell);
                // An orange giant, twice the size of Otrera's star
                star.properties = new StarProperties
                {
                    radius = home.properties.radius * 2f,
                    lightIntensity = home.properties.lightIntensity * 1.1f,
                    color = new Color(1f, 0.62f, 0.32f, home.properties.color.a)
                };
                copy.transform.SetParent(home.transform.parent, false);
                _erebos = star;
                AdvancedMod.Log.Msg($"galaxy: Erebos's star placed at {at.worldCell} ({Offset.Magnitude / 9.46e15:0.00} light years from Otrera)");
            }
            finally
            {
                Object.Destroy(holder);
            }
            return _erebos;
        }

        /// <summary>Development: which star lights the scene, and how.</summary>
        public static void LogLighting()
        {
            Star nearest = Nearest();
            var light = ServiceLocator.GetService<WorldProvision.Interface.Services.ILocalLightProvider>();
            Light sun = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
            AdvancedMod.Log.Msg($"dev: lighting: {Stars.Count} stars, nearest {nearest?.name}, sun light {sun?.name} dir {sun?.transform.forward:F2} intensity {sun?.intensity:0.00}, local light {light?.LocalLightIntensity:0.00} dot {(light as WorldProvision.Services.LocalLightTracker)?.LocalPlanetLightDot:0.00}");
        }

        public static void Forget()
        {
            _erebos = null;
            _home = null;
            Stars.Clear();
        }

        internal static void Track(Star star)
        {
            if (star != null && !Stars.Contains(star))
                Stars.Add(star);
        }

        internal static void Untrack(Star star) => Stars.Remove(star);

        public static IReadOnlyList<Star> AllStars => Stars;

        private static int _nearestFrame = -1;
        private static Star _nearest;

        /// <summary>The star nearest this client's view (cached for the frame).</summary>
        public static Star Nearest()
        {
            if (Time.frameCount == _nearestFrame && _nearest != null)
                return _nearest;
            _nearestFrame = Time.frameCount;
            Stars.RemoveAll(s => s == null);
            if (Stars.Count <= 1 || ClientOrigin.LocalVisualSpace == null)
                return _nearest = Stars.FirstOrDefault();
            var transpose = GameServices.Transpose;
            if (transpose == null)
                return _nearest = Stars.FirstOrDefault();
            PreciseWorldPosition here = transpose.ToPreciseWorldPos(ClientOrigin.LocalVisualSpace);
            double Distance(Star s) => here.DeltaDouble(new PreciseWorldPosition(s.position, Vector3.zero)).Magnitude;
            Star closest = Stars.OrderBy(Distance).First();
            // Another star only takes over when clearly nearer: no back and forth (the star map, the
            // light) from one frame to the next while flying or switching spaces
            if (_nearest == null || !Stars.Contains(_nearest) || double.IsNaN(Distance(_nearest)) || Distance(closest) < Distance(_nearest) * 0.8)
                _nearest = closest;
            return _nearest;
        }

        /// <summary>Whether a position lies in the Erebos galaxy.</summary>
        public static bool IsInErebos(IWorldTranspose transpose, uint spaceId, Vector3 position)
        {
            if (_erebos == null || _erebos.Space == null || transpose == null)
                return false;
            Vector3Double fromStar = transpose.Position(spaceId, (ISpace)_erebos.Space, (Vector3Double)position);
            return fromStar.Magnitude < Reach;
        }

        /// <summary>Which star a planet orbits (the game's or Erebos's).</summary>
        public static bool OrbitsErebos(ulong planetId) => PlanetCatalog.All.Any(p => p.Id == planetId && p.System == PlanetWiki.Erebos);
    }

    /// <summary>Every star the game starts is remembered; the tracker keeps the game's own as its current one.</summary>
    [HarmonyPatch(typeof(StarsClientTracker), nameof(StarsClientTracker.TrackStar))]
    internal static class GalaxyTrackStarPatch
    {
        private static bool Prefix(StarsClientTracker __instance, Star star)
        {
            Galaxy.Track(star);
            // The copy must not replace the game's star as the tracker's own
            return star == null || !star.name.StartsWith("TFA_") || __instance.GetCurrentStar() == null;
        }
    }

    [HarmonyPatch(typeof(StarsClientTracker), nameof(StarsClientTracker.UntrackStar))]
    internal static class GalaxyUntrackStarPatch
    {
        private static bool Prefix(IStarProperties starProperties)
        {
            if (starProperties is Star star)
                Galaxy.Untrack(star);
            return !(starProperties is Star s) || !s.name.StartsWith("TFA_");
        }
    }

    /// <summary>The current star is the nearest one: light, star map and warp checks follow it.</summary>
    [HarmonyPatch(typeof(StarsClientTracker), nameof(StarsClientTracker.GetCurrentStar))]
    internal static class GalaxyCurrentStarPatch
    {
        private static void Postfix(ref IStarProperties __result)
        {
            if (Galaxy.AllStars.Count < 2)
                return;
            Star nearest = Galaxy.Nearest();
            if (nearest != null)
                __result = nearest;
        }
    }

    [HarmonyPatch(typeof(StarsClientTracker), nameof(StarsClientTracker.GetCurrentStarLifecycle))]
    internal static class GalaxyCurrentLifecyclePatch
    {
        private static void Postfix(ref StarLifecycle __result)
        {
            if (Galaxy.AllStars.Count < 2)
                return;
            Star nearest = Galaxy.Nearest();
            StarLifecycle lifecycle = nearest != null ? nearest.GetComponent<StarLifecycle>() : null;
            if (lifecycle != null)
                __result = lifecycle;
        }
    }

    /// <summary>Every star is drawn, not just the current one.</summary>
    [HarmonyPatch(typeof(Managers.Client.Services.ClientWorldVisualsUpdater), "UpdateWorldVisuals")]
    internal static class GalaxyStarVisualsPatch
    {
        private static void Postfix()
        {
            try
            {
                Star nearest = Galaxy.Nearest();
                foreach (Star star in Galaxy.AllStars)
                {
                    if (star == null || star == nearest)
                        continue;
                    star.GetComponent<StarLifecycle>()?.UpdateStarVisuals();
                }
            }
            catch (Exception e)
            {
                ErrorLog.Report("galaxy stars", e);
            }
        }
    }

    /// <summary>The sunlight comes from the nearest star.</summary>
    [HarmonyPatch(typeof(SceneLighting), nameof(SceneLighting.UpdateLighting))]
    internal static class GalaxyLightingPatch
    {
        private static readonly AccessTools.FieldRef<SceneLighting, MonoBehaviour> NearestStar = AccessTools.FieldRefAccess<SceneLighting, MonoBehaviour>("nearestStar");

        private static void Prefix(SceneLighting __instance)
        {
            if (Galaxy.AllStars.Count < 2)
                return;
            Star nearest = Galaxy.Nearest();
            if (nearest != null)
                NearestStar(__instance) = nearest;
        }
    }

    /// <summary>Day and night (the ambient light) follow the nearest star too.</summary>
    [HarmonyPatch(typeof(WorldProvision.Services.LocalLightTracker), nameof(WorldProvision.Services.LocalLightTracker.UpdateLocalLightIntensity))]
    internal static class GalaxyLocalLightPatch
    {
        private static readonly AccessTools.FieldRef<WorldProvision.Services.LocalLightTracker, MonoBehaviour> NearestStar =
            AccessTools.FieldRefAccess<WorldProvision.Services.LocalLightTracker, MonoBehaviour>("nearestStar");

        private static void Prefix(WorldProvision.Services.LocalLightTracker __instance)
        {
            if (Galaxy.AllStars.Count < 2)
                return;
            Star nearest = Galaxy.Nearest();
            if (nearest != null)
                NearestStar(__instance) = nearest;
        }
    }
}

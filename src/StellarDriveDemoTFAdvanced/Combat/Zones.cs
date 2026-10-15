using System.Collections.Generic;
using System.Linq;
using Core.Services;
using Core.Values;
using Planets.Interface.Model;
using Planets.Interface.Services;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;
using WorldTracking.Interface.Model;
using WorldTracking.Interface.Model.Spaces;
using WorldTracking.Interface.Services;

namespace StellarDriveDemoTFAdvanced.Combat
{
    /// <summary>
    /// Where pirates go or not: a safe zone around the home planet (442 Otrera b), where they never
    /// jump in and which they leave when a fight drifts into it; and the pirates' own territory
    /// around the Bastion, the artificial planet of the Erebos galaxy, where they attack far more often.
    /// </summary>
    internal static class Zones
    {
        public const ulong HomePlanet = 4;
        public const float BastionRadius = 5e6f;

        private static readonly Dictionary<ulong, FixedWorldSpace> Centers = new Dictionary<ulong, FixedWorldSpace>();

        public static void Forget() => Centers.Clear();

        /// <summary>From a position (in a space) to a planet's centre.</summary>
        public static bool TryOffset(ulong planetId, uint spaceId, Vector3 position, out Vector3 offset)
        {
            offset = Vector3.zero;
            var transpose = ServiceLocator.GetService<IWorldTransposeServer>();
            if (transpose == null)
                return false;
            if (!Centers.TryGetValue(planetId, out FixedWorldSpace center))
            {
                IPlanetsProvider planets = ServiceLocator.GetService<IPlanetsServerProvider>() as IPlanetsProvider ?? ServiceLocator.GetService<IPlanetsClientProvider>();
                IPlanetProperties planet = planets?.Planets.FirstOrDefault(p => p.Id == planetId);
                if (planet == null)
                    return false;
                Centers[planetId] = center = new FixedWorldSpace { Position = new PreciseWorldPosition(planet.WorldCell, Vector3.zero) };
            }
            try
            {
                offset = (Vector3)(transpose.Position(center, spaceId, Vector3Double.Zero) - (Vector3Double)position);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Within the safe zone around the home planet.</summary>
        public static bool InSafeZone(uint spaceId, Vector3 position)
        {
            float radius = Settings.PirateSafeRadiusKm.Value * 1000f;
            return radius > 0f && TryOffset(HomePlanet, spaceId, position, out Vector3 offset) && offset.magnitude < radius;
        }

        /// <summary>In the pirates' territory around the Bastion.</summary>
        public static bool NearBastion(uint spaceId, Vector3 position) =>
            TryOffset(Worlds.PlanetCatalog.BastionId, spaceId, position, out Vector3 offset) && offset.magnitude < BastionRadius;

        public const int MaxDanger = 5;

        /// <summary>
        /// How dangerous the pirates are here, 1 to <see cref="MaxDanger"/>: 1 just outside the safe
        /// zone, one more every Settings.PirateDangerStepKm farther from the home planet (4 at most in
        /// the Otrera system), 5 in the Erebos galaxy, their home.
        /// </summary>
        public static int Danger(uint spaceId, Vector3 position)
        {
            var transpose = ServiceLocator.GetService<IWorldTransposeServer>();
            if (Worlds.Galaxy.IsInErebos(transpose, spaceId, position) || NearBastion(spaceId, position))
                return MaxDanger;
            if (!TryOffset(HomePlanet, spaceId, position, out Vector3 offset))
                return 2;
            float beyond = offset.magnitude / 1000f - Mathf.Max(0f, Settings.PirateSafeRadiusKm.Value);
            float step = Mathf.Max(1f, Settings.PirateDangerStepKm.Value);
            return Mathf.Clamp(1 + Mathf.FloorToInt(Mathf.Max(0f, beyond) / step), 1, MaxDanger - 1);
        }

        /// <summary>Development: how far each planet is from the home planet, and its danger.</summary>
        public static void LogPlanets()
        {
            IPlanetsProvider planets = ServiceLocator.GetService<IPlanetsServerProvider>();
            IPlanetProperties home = planets?.Planets.FirstOrDefault(p => p.Id == HomePlanet);
            if (home == null)
                return;
            var origin = new PreciseWorldPosition(home.WorldCell, Vector3.zero);
            foreach (IPlanetProperties planet in planets.Planets.OrderBy(p => new PreciseWorldPosition(p.WorldCell, Vector3.zero).DeltaDouble(origin).Magnitude))
            {
                double km = new PreciseWorldPosition(planet.WorldCell, Vector3.zero).DeltaDouble(origin).Magnitude / 1000.0;
                AdvancedMod.Log.Msg($"dev: planet {planet.Id} {km:0} km from home, radius {planet.Radius / 1000f:0} km");
            }
        }
    }
}

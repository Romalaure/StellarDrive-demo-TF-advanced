using System.Collections.Generic;
using System.Linq;
using Core.Services;
using HarmonyLib;
using Planets;
using Planets.EnvironmentGeneration;
using Planets.EnvironmentGeneration.ToSpawn;
using Planets.Terrain.Evaluation.Services;
using Planets.Terrain.Model;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Worlds
{
    /// <summary>
    /// The game makes the colliders of the trees and points of interest around every player and
    /// ship near a planet again each time it has moved 50 m, at any height. Flying fast over a
    /// forest, that spawned about a thousand tree colliders a second and took a fifth of the
    /// frame. Nothing up there can touch a tree or a ruin: above 100 m over the ground no tree
    /// colliders, above 200 m no points of interest. The ground's own collider stays.
    /// </summary>
    internal static class HighFlightColliders
    {
        public const float TreeClearance = 100f, SiteClearance = 200f;

        private static readonly Dictionary<Planet, TerrainOperationStack> Stacks = new Dictionary<Planet, TerrainOperationStack>();
        private static PlanetTerrainPointCpuCalculator _calculator;

        /// <summary>Height over the ground of a point given from the planet's center (very high when unknown).</summary>
        public static float Altitude(Planet planet, Vector3 fromCenter)
        {
            if (planet == null || Common.DevHarness.Off("highflight"))
                return 0f;
            float distance = fromCenter.magnitude;
            // Far above the highest mountains: no need to look at the ground
            if (distance > planet.Radius + 12000f)
                return distance - planet.Radius;
            if (_calculator == null)
                _calculator = ServiceLocator.GetService<PlanetTerrainPointCpuCalculator>();
            if (_calculator == null)
                return 0f;
            if (!Stacks.TryGetValue(planet, out TerrainOperationStack stack) || stack == null)
            {
                if (Stacks.Count > 64)
                    Stacks.Clear();
                stack = new TerrainOperationStack(planet);
                Stacks[planet] = stack;
            }
            return distance - _calculator.GetElevation(stack, fromCenter / Mathf.Max(distance, 1f));
        }
    }

    [HarmonyPatch(typeof(PlanetDenseEnvironmentPhysics), nameof(PlanetDenseEnvironmentPhysics.GetCollidersAtPosition))]
    internal static class HighFlightTreesPatch
    {
        private static readonly AccessTools.FieldRef<PlanetDenseEnvironmentPhysics, Planet> PlanetField = AccessTools.FieldRefAccess<PlanetDenseEnvironmentPhysics, Planet>("_planet");

        private static bool Prefix(PlanetDenseEnvironmentPhysics __instance, Vector3 position, ref IEnumerable<DensePlanetObjectSpawnData> __result)
        {
            if (HighFlightColliders.Altitude(PlanetField(__instance), position) < HighFlightColliders.TreeClearance)
                return true;
            __result = Enumerable.Empty<DensePlanetObjectSpawnData>();
            return false;
        }
    }

    [HarmonyPatch(typeof(PlanetHotspotsEnvironmentPhysics), nameof(PlanetHotspotsEnvironmentPhysics.GetCollidersAtPosition))]
    internal static class HighFlightSitesPatch
    {
        private static readonly AccessTools.FieldRef<PlanetHotspotsEnvironmentPhysics, Planet> PlanetField = AccessTools.FieldRefAccess<PlanetHotspotsEnvironmentPhysics, Planet>("_planet");

        private static bool Prefix(PlanetHotspotsEnvironmentPhysics __instance, Vector3 planetRelativePos, ref IEnumerable<HotspotObjectToSpawn> __result)
        {
            if (HighFlightColliders.Altitude(PlanetField(__instance), planetRelativePos) < HighFlightColliders.SiteClearance)
                return true;
            __result = Enumerable.Empty<HotspotObjectToSpawn>();
            return false;
        }
    }
}

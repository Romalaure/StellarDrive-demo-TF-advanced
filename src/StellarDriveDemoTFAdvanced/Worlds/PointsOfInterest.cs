using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Planets.EnvironmentGeneration.Configs;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Worlds
{
    internal enum RuinStyle
    {
        Sandstone,
        Basalt,
        Granite,
        Mossy
    }

    /// <summary>
    /// Wrecks and ruins, as hotspots: the game's own system that scatters points of interest over a
    /// planet (on the demo planet: crashed parts, rocks, crystals, guide tablets). Hotspots are
    /// placed by the planet's seed, the same for every player, and loaded near the player.
    /// </summary>
    internal static class PointsOfInterest
    {
        private static readonly AccessTools.FieldRef<HotspotObjectsDistributionStep, AnimationCurve> Curve =
            AccessTools.FieldRefAccess<HotspotObjectsDistributionStep, AnimationCurve>("distributionCurve");

        /// <summary>
        /// Crash sites: a crashed hull with thrusters and a seat thrown around it, from the demo's own
        /// crash pieces. <paramref name="frequency"/> is the chance a candidate area gets one (0-1).
        /// </summary>
        public static void Wrecks(PlanetShaper.Context planet, float frequency)
        {
            GameObject hull = planet.HotspotPrefab("CrashedShipNoSmoke");
            GameObject thruster = planet.HotspotPrefab("CrashedThruster");
            GameObject seat = planet.HotspotPrefab("CrashedSeat");
            AnimationCurve curve = TemplateCurve(planet);
            // The guide tablets (and the crash pieces sharing their sites) belong to the demo planet's tutorial
            planet.ClearHotspots(c => c.distributionSteps.Any(s => s.objectPrefab != null && s.objectPrefab.name.StartsWith("TabletGuide")));
            if (hull == null || curve == null)
            {
                AdvancedMod.Log.Warning("planets: crash pieces not found, no wrecks on " + planet.Planet.name);
                return;
            }
            planet.AddHotspot(new PlanetHotspotsPlacementConfig
            {
                // Detail levels grow with chunk size; level 4 is the level of the rock fields, frequent enough to meet sites
                placementLevel = 4,
                loadDistance = 2500f,
                spawnProbability = Mathf.Clamp01(frequency * 0.35f),
                avoidDenseEnvConstraint = true,
                minElevationConstraint = true,
                minElevation = 75f,
                distributionSteps = new[]
                {
                    Step(hull, curve, 1, 1, 0f, 1f, 6f),
                    Step(thruster ?? hull, curve, 1, 3, 8f, 22f, 5f),
                    Step(seat ?? thruster ?? hull, curve, 0, 1, 5f, 14f, 3f)
                }
            });
        }

        public static void Ruins(PlanetShaper.Context planet, RuinStyle style, float frequency)
        {
            GameObject[] pieces = Ruin.Prefabs(planet, style);
            AnimationCurve curve = TemplateCurve(planet);
            if (pieces == null || pieces.Length == 0 || curve == null)
                return;
            // A site: a central monument, pillars around it, fallen blocks further out
            planet.AddHotspot(new PlanetHotspotsPlacementConfig
            {
                // Detail levels grow with chunk size; level 4 is the level of the rock fields, frequent enough to meet sites
                placementLevel = 4,
                loadDistance = 2500f,
                spawnProbability = Mathf.Clamp01(frequency * 0.35f),
                avoidDenseEnvConstraint = style != RuinStyle.Mossy,
                minElevationConstraint = true,
                minElevation = 75f,
                distributionSteps = pieces.Select((piece, i) => i == 0
                    ? Step(piece, curve, 1, 1, 0f, 1f, 12f)
                    : Step(piece, curve, 2, 5, 10f + i * 6f, 22f + i * 10f, 6f)).ToArray()
            });
        }

        /// <summary>An abandoned village: a temple in the middle, houses and a watchtower around it.</summary>
        public static void Village(PlanetShaper.Context planet, RuinStyle style, float frequency)
        {
            GameObject[] pieces = Ruin.Village(planet, style);
            AnimationCurve curve = TemplateCurve(planet);
            if (pieces == null || curve == null)
                return;
            planet.AddHotspot(new PlanetHotspotsPlacementConfig
            {
                placementLevel = 4,
                loadDistance = 2500f,
                spawnProbability = Mathf.Clamp01(frequency * 0.3f),
                avoidDenseEnvConstraint = style != RuinStyle.Mossy,
                minElevationConstraint = true,
                minElevation = 75f,
                distributionSteps = new[]
                {
                    Step(pieces[0], curve, 1, 1, 0f, 1f, 16f),
                    Step(pieces[1], curve, 2, 4, 20f, 42f, 12f),
                    Step(pieces[2], curve, 1, 3, 18f, 45f, 9f),
                    Step(pieces[3], curve, 1, 1, 30f, 48f, 10f)
                }
            });
        }

        /// <summary>A wrecked cargo ship with its debris thrown around.</summary>
        public static void CargoWrecks(PlanetShaper.Context planet, float frequency)
        {
            GameObject cargo = Worlds.Wrecks.Cargo(planet);
            GameObject thruster = planet.HotspotPrefab("CrashedThruster");
            GameObject seat = planet.HotspotPrefab("CrashedSeat");
            AnimationCurve curve = TemplateCurve(planet);
            if (cargo == null || curve == null)
                return;
            var steps = new List<HotspotObjectsDistributionStep> { Step(cargo, curve, 1, 1, 0f, 1f, 20f) };
            if (thruster != null)
                steps.Add(Step(thruster, curve, 2, 4, 22f, 50f, 6f));
            if (seat != null)
                steps.Add(Step(seat, curve, 1, 2, 15f, 35f, 4f));
            planet.AddHotspot(new PlanetHotspotsPlacementConfig
            {
                placementLevel = 4,
                loadDistance = 2500f,
                spawnProbability = Mathf.Clamp01(frequency * 0.25f),
                avoidDenseEnvConstraint = true,
                minElevationConstraint = true,
                minElevation = 75f,
                distributionSteps = steps.ToArray()
            });
        }

        private static HotspotObjectsDistributionStep Step(GameObject prefab, AnimationCurve curve, int min, int max, float near, float far, float spacing)
        {
            var step = new HotspotObjectsDistributionStep
            {
                objectPrefab = prefab,
                countMin = min,
                countMax = max,
                centerDistanceMin = near,
                centerDistanceMax = far,
                minDistanceBetweenObjects = spacing,
                rotationType = HotspotObjectsDistributionStep.RotationGenerationType.AlignedWithTerrain
            };
            Curve(step) = new AnimationCurve(curve.keys);
            return step;
        }

        private static AnimationCurve TemplateCurve(PlanetShaper.Context planet)
        {
            HotspotObjectsDistributionStep template = planet.Hotspots.hotspotsPlacement.SelectMany(c => c.distributionSteps).FirstOrDefault();
            return template != null ? Curve(template) : null;
        }
    }
}

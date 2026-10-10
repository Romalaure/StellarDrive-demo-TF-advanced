using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Planets;
using Planets.EnvironmentGeneration;
using Planets.EnvironmentGeneration.Configs;
using Planets.Interface.Model;
using Planets.Terrain;
using Planets.Terrain.Evaluation.Config;
using Planets.Terrain.Material;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StellarDriveDemoTFAdvanced.Worlds
{
    /// <summary>
    /// Reshapes a copy of the demo planet: every setting it changes is a field of the game's own
    /// planet components (terrain graph, terrain materials, atmosphere, dense flora and hotspots),
    /// so the game's generators produce the result. Called before the copy wakes up.
    /// </summary>
    internal static class PlanetShaper
    {
        // Terrain material slots of the demo planet: 0 ground (grass), 1 water/ice, 2 shore (sand)
        public const int Ground = 0;
        public const int Water = 1;
        public const int Shore = 2;

        private static readonly AccessTools.FieldRef<PlanetPhysicalProperties, bool> HasAtmosphere =
            AccessTools.FieldRefAccess<PlanetPhysicalProperties, bool>("hasAtmosphere");

        private static GameObject _prefabHolder;
        private static readonly Dictionary<string, GameObject> TintedPrefabs = new Dictionary<string, GameObject>();

        public static void Apply(Planet planet, PlanetSpec spec)
        {
            PlanetPhysicalProperties physics = planet.GetComponent<PlanetPhysicalProperties>();
            physics.gravityInG = spec.Gravity;
            physics.rotationPeriod = spec.RotationHours;
            spec.Shape?.Invoke(new Context(planet));
        }

        /// <summary>What a planet's shaping code can change.</summary>
        internal sealed class Context
        {
            public readonly Planet Planet;
            public readonly PlanetTerrain Terrain;
            public readonly PlanetPhysicalProperties Physics;
            public readonly PlanetDenseEnvironment Flora;
            public readonly PlanetHotspotsEnvironment Hotspots;

            public Context(Planet planet)
            {
                Planet = planet;
                Terrain = planet.GetComponent<PlanetTerrain>();
                Physics = planet.GetComponent<PlanetPhysicalProperties>();
                Flora = planet.GetComponent<PlanetDenseEnvironment>();
                Hotspots = planet.GetComponent<PlanetHotspotsEnvironment>();
            }

            // ---- Terrain

            /// <summary>Gives a material slot another slot's texture (the demo has grass, ice and sand) and a tint.</summary>
            public void Material(int slot, int textureFrom, Color tint, float? tile = null)
            {
                TerrainMaterialConfig[] materials = Terrain.materials;
                TerrainMaterialConfig source = materials[textureFrom];
                TerrainMaterialConfig target = materials[slot];
                target.albedoTexture = source.albedoTexture;
                target.normalMap = source.normalMap;
                target.roughnessTexture = source.roughnessTexture;
                target.footstepSoundParam = source.footstepSoundParam;
                target.albedoColor = tint;
                if (tile.HasValue)
                    target.tile = tile.Value;
            }

            /// <summary>Relief: noise strength and frequency multipliers, and a shift of the noise field (another landscape).</summary>
            public void Relief(float strength, float frequency, Vector3 shift)
            {
                NoiseStep[] steps = Terrain.terrainConfig.noiseSteps;
                for (int i = 0; i < steps.Length; i++)
                {
                    var noise = steps[i].noiseConfig;
                    noise.strength *= strength;
                    noise.frequency *= frequency;
                    noise.center += shift * (i + 1);
                    steps[i].noiseConfig = noise;
                }
            }

            /// <summary>Highest terrain allowed above the center (the demo clamps at 3 200 m).</summary>
            public void Ceiling(float max)
            {
                foreach (ClampStep clamp in Terrain.terrainConfig.clampSteps)
                    clamp.max = max;
            }

            /// <summary>Elevation below which the shore and water materials show (demo: 70 and 64.2).</summary>
            public void Shoreline(float shore, float water)
            {
                ColorElevationStep[] steps = Terrain.terrainConfig.colorElevationSteps;
                if (steps.Length > 0)
                    steps[0].bellowThreshold = shore;
                if (steps.Length > 1)
                    steps[1].bellowThreshold = water;
            }

            /// <summary>Adds impact craters to the relief, between the noise and the coloring.</summary>
            public void Craters(int count, float radiusMin, float radiusMax, float rimMin, float rimMax)
            {
                TerrainConfig config = Terrain.terrainConfig;
                ColorElevationStep first = config.colorElevationSteps.FirstOrDefault();
                if (first == null)
                    return;
                const int craterOutput = 60;
                var crater = new CraterStep
                {
                    numberOfCraters = count,
                    radiusMin = radiusMin,
                    radiusMax = radiusMax,
                    rimHeightMin = rimMin,
                    rimHeightMax = rimMax,
                    innerSlope = 0.5f,
                    centralPeak = 0.05f,
                    smoothness = 0.2f
                };
                StepIds.Set(crater, StepIds.Input(first), craterOutput);
                StepIds.Set(first, craterOutput, StepIds.Output(first));
                config.craterSteps = config.craterSteps.Concat(new[] { crater }).ToArray();
            }

            // ---- Atmosphere

            public void NoAtmosphere()
            {
                HasAtmosphere(Physics) = false;
                Physics.atmosphereProperties.isBreathable = false;
                Physics.atmosphereProperties.cloudMap = null;
            }

            /// <summary>Sky color (Rayleigh scattering per channel), haze, light intensity, clouds, breathable air.</summary>
            public void Sky(Vector3 rayleigh, float rayleighStrength, float haze, float intensity, bool clouds, bool breathable)
            {
                AtmosphereProperties air = Physics.atmosphereProperties;
                air.visualProperties.RayleighScattering = rayleigh;
                air.visualProperties.RayleighStrength = rayleighStrength;
                air.visualProperties.MieStrength = haze;
                air.visualProperties.intensity = intensity;
                air.isBreathable = breathable;
                if (!clouds)
                    air.cloudMap = null;
            }

            // ---- Flora (dense environment: 0 trees, 1 grass on the demo planet)

            /// <summary>Changes the tree mix: each entry is a prefab (possibly tinted) and its weight.</summary>
            public void Trees(params (GameObject Prefab, int Weight, float Bias)[] trees)
            {
                PlanetDensePlacementConfig config = Flora.configs[0];
                config.objectsSpawn = trees.Select(t => new PlanetDensePlacementConfig.DenseObjectPlacementType
                {
                    prefab = t.Prefab,
                    probability = t.Weight,
                    densityBiasValue = t.Bias,
                    densityBiasPower = 1f
                }).ToArray();
            }

            /// <summary>Lowest elevation trees (config 0) and grass (config 1) grow at; very high values remove them.</summary>
            public void FloraElevation(float trees, float grass)
            {
                Flora.configs[0].elevationMin = trees;
                if (Flora.configs.Length > 1)
                    Flora.configs[1].elevationMin = grass;
            }

            /// <summary>Forest patches: how much the density noise favors trees (higher offset, more ground covered).</summary>
            public void ForestDensity(float noiseOffset)
            {
                Flora.configs[0].noiseOffset = noiseOffset;
            }

            public void Grass(GameObject prefab)
            {
                if (Flora.configs.Length > 1 && Flora.configs[1].objectsSpawn.Length > 0)
                    Flora.configs[1].objectsSpawn[0].prefab = prefab;
            }

            public GameObject TreePrefab(string name) => Flora.configs[0].objectsSpawn.Select(o => o.prefab).FirstOrDefault(p => p != null && p.name == name);

            public GameObject GrassPrefab => Flora.configs.Length > 1 ? Flora.configs[1].objectsSpawn.Select(o => o.prefab).FirstOrDefault() : null;

            // ---- Points of interest

            public void ClearHotspots(Func<PlanetHotspotsPlacementConfig, bool> remove)
            {
                Hotspots.hotspotsPlacement.RemoveAll(c => remove(c));
            }

            public void AddHotspot(PlanetHotspotsPlacementConfig config)
            {
                Hotspots.hotspotsPlacement.Add(config);
            }

            /// <summary>A hotspot prefab the demo planet places (rocks, crystals, crashed parts).</summary>
            public GameObject HotspotPrefab(string name) => Hotspots.hotspotsPlacement
                .SelectMany(c => c.distributionSteps).Select(s => s.objectPrefab).FirstOrDefault(p => p != null && p.name == name);
        }

        /// <summary>
        /// A copy of a game prefab whose renderers are tinted (dry grass, autumn trees). The copy
        /// lives inactive under a hidden holder; the game instances it like its own prefab.
        /// </summary>
        public static GameObject Tinted(GameObject prefab, string suffix, Color tint, float tintStrength = 1f)
        {
            if (prefab == null)
                return null;
            string key = prefab.name + "_" + suffix;
            if (TintedPrefabs.TryGetValue(key, out GameObject existing) && existing != null)
                return existing;
            if (_prefabHolder == null)
            {
                _prefabHolder = new GameObject("TFA_PlanetPrefabs");
                _prefabHolder.SetActive(false);
                Object.DontDestroyOnLoad(_prefabHolder);
            }
            GameObject copy = Object.Instantiate(prefab, _prefabHolder.transform);
            copy.name = key;
            var tinted = new Dictionary<Material, Material>();
            foreach (Renderer renderer in copy.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material original = materials[i];
                    if (original == null)
                        continue;
                    if (!tinted.TryGetValue(original, out Material material))
                    {
                        material = new Material(original) { name = original.name + "_" + suffix };
                        foreach (string property in new[] { "_Color", "_BaseColor", "_AlbedoColor", "_Tint" })
                        {
                            if (material.HasProperty(property))
                                material.SetColor(property, Color.Lerp(material.GetColor(property), material.GetColor(property) * tint, tintStrength));
                        }
                        // Distant trees are pre-rendered impostors: their shader shifts hue toward this color by its alpha
                        if (material.HasProperty("_HueVariation"))
                            material.SetColor("_HueVariation", new Color(Mathf.Clamp01(tint.r * 0.6f), Mathf.Clamp01(tint.g * 0.6f), Mathf.Clamp01(tint.b * 0.6f), 0.45f * tintStrength));
                        tinted[original] = material;
                    }
                    materials[i] = material;
                }
                renderer.sharedMaterials = materials;
            }
            TintedPrefabs[key] = copy;
            return copy;
        }
    }

    /// <summary>Input and output buffer ids of the terrain graph steps (fields of their common base class).</summary>
    internal static class StepIds
    {
        public static int Input(object step) => (int)AccessTools.Field(step.GetType(), "inputId").GetValue(step);

        public static int Output(object step) => (int)AccessTools.Field(step.GetType(), "outputId").GetValue(step);

        public static void Set(object step, int input, int output)
        {
            AccessTools.Field(step.GetType(), "inputId").SetValue(step, input);
            AccessTools.Field(step.GetType(), "outputId").SetValue(step, output);
        }
    }
}

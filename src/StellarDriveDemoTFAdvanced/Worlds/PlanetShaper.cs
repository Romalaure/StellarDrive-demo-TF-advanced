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

        private static readonly AccessTools.FieldRef<PlanetDensePlacementConfig, int[]> SpawnTable =
            AccessTools.FieldRefAccess<PlanetDensePlacementConfig, int[]>("_spawnTable");
        private static readonly AccessTools.FieldRef<PlanetDensePlacementConfig, ComputeBuffer> NoiseBuffer =
            AccessTools.FieldRefAccess<PlanetDensePlacementConfig, ComputeBuffer>("_noiseConfigBuffer");
        private static readonly System.Reflection.MethodInfo ShallowCopy = AccessTools.Method(typeof(object), "MemberwiseClone");

        private static readonly AccessTools.FieldRef<PlanetDensePlacementConfig, float> ViewRange =
            AccessTools.FieldRefAccess<PlanetDensePlacementConfig, float>("viewRange");

        private static readonly AccessTools.FieldRef<PlanetPhysicalProperties, bool> HasAtmosphere =
            AccessTools.FieldRefAccess<PlanetPhysicalProperties, bool>("hasAtmosphere");

        private static GameObject _prefabHolder;
        private static readonly Dictionary<string, GameObject> TintedPrefabs = new Dictionary<string, GameObject>();

        public static void Apply(Planet planet, PlanetSpec spec)
        {
            PlanetPhysicalProperties physics = planet.GetComponent<PlanetPhysicalProperties>();
            physics.gravityInG = spec.Gravity;
            physics.rotationPeriod = spec.RotationHours;
            var context = new Context(planet);
            spec.Shape?.Invoke(context);
            // The wiki shows each planet's plants
            if (context.Flora != null && context.Flora.configs != null)
                Wiki.WikiImages.NoteFlora(spec.Id, context.Flora.configs.SelectMany(c => c.objectsSpawn ?? new PlanetDensePlacementConfig.DenseObjectPlacementType[0]).Select(o => o.prefab));
            if (Common.DevHarness.Active && spec.Id == 108)
                LogTerrainGraph(planet.GetComponent<PlanetTerrain>(), spec.Name);
        }

        // Development: the terrain graph's steps, in their buffer order
        private static void LogTerrainGraph(PlanetTerrain terrain, string name)
        {
            TerrainConfig c = terrain.terrainConfig;
            var steps = new List<TerrainGenerationStep>();
            steps.AddRange(c.noiseSteps ?? new NoiseStep[0]);
            steps.AddRange(c.clampSteps ?? new ClampStep[0]);
            steps.AddRange(c.absSteps ?? new AbsStep[0]);
            steps.AddRange(c.craterSteps ?? new CraterStep[0]);
            steps.AddRange(c.colorElevationSteps ?? new ColorElevationStep[0]);
            steps.AddRange(c.addSteps ?? new AddStep[0]);
            foreach (TerrainGenerationStep step in steps)
            {
                string detail = step is NoiseStep n ? $"strength {n.noiseConfig.strength} freq {n.noiseConfig.frequency} oct {n.noiseConfig.octaves} gain {n.noiseConfig.gain} lac {n.noiseConfig.lacunarity}"
                    : step is ClampStep k ? $"min {k.min} max {k.max} smooth {k.smoothness}"
                    : step is ColorElevationStep e ? $"below {e.bellowThreshold} smooth {e.smoothness} material {e.materialIndex}"
                    : step is AddStep a ? $"input2 {a.inputId2}" : "";
                AdvancedMod.Log.Msg($"dev: terrain {name}: {step.GetType().Name} {step.inputId} -> {step.outputId} {detail}");
            }
            var flora = terrain.GetComponent<PlanetDenseEnvironment>();
            var viewRange = AccessTools.Field(typeof(PlanetDensePlacementConfig), "viewRange");
            foreach (PlanetDensePlacementConfig dense in flora.configs)
                AdvancedMod.Log.Msg($"dev: terrain {name}: dense level {dense.level} view {viewRange.GetValue(dense)} physics {dense.hasPhysics} range {dense.physicsRange} objects {dense.objectsSpawn.Length} coverage {dense.approximateCoverageRatio} offset {dense.noiseOffset}");
            for (int i = 0; i < terrain.materials.Length; i++)
                AdvancedMod.Log.Msg($"dev: terrain {name}: material {i} {terrain.materials[i].albedoTexture?.name} tint {terrain.materials[i].albedoColor} tile {terrain.materials[i].tile}");
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

            /// <summary>
            /// Several slots at once, each taking another slot's texture as it was before any of
            /// them changed (to swap textures between slots) and a tint.
            /// </summary>
            public void Remap(params (int Slot, int TextureFrom, Color Tint)[] slots)
            {
                TerrainMaterialConfig[] materials = Terrain.materials;
                var originals = materials.Select(m => (m.albedoTexture, m.normalMap, m.roughnessTexture, m.footstepSoundParam, m.tile)).ToArray();
                foreach ((int slot, int from, Color tint) in slots)
                {
                    TerrainMaterialConfig target = materials[slot];
                    (target.albedoTexture, target.normalMap, target.roughnessTexture, target.footstepSoundParam, target.tile) = originals[from];
                    target.albedoColor = tint;
                }
            }

            /// <summary>
            /// Regional climates. The ground is <paramref name="cold"/> (the slot shown above every
            /// threshold: snow) above a snowline that a broad noise field raises in warm regions and
            /// lowers in cold ones (tundra there, snow only on the peaks elsewhere); below it
            /// <paramref name="land"/>; below <paramref name="sea"/> (the real elevation),
            /// <paramref name="seaSlot"/>. The noise is added to the elevation for the snowline's
            /// coloring only and taken off right after, so the relief stays the same.
            /// </summary>
            public void Climate(float snowline, float sea, int land, int seaSlot, float strength, float frequency, Vector3 shift)
            {
                TerrainConfig config = Terrain.terrainConfig;
                ColorElevationStep[] colors = config.colorElevationSteps;
                if (colors.Length < 2 || config.noiseSteps.Length == 0)
                    return;
                ColorElevationStep band = colors[0], water = colors[1];
                int input = StepIds.Input(band), output = StepIds.Output(band);
                const int warmed = 81, colored = 82;
                Planets.Terrain.Model.NoiseConfig climate = config.noiseSteps[0].noiseConfig;
                climate.strength = strength;
                climate.frequency = frequency;
                // One octave: broad climate regions, and a cheap extra term for every terrain height the CPU evaluates
                climate.octaves = 1;
                climate.center += shift;
                var add = new NoiseStep { noiseConfig = climate };
                climate.strength = -strength;
                var remove = new NoiseStep { noiseConfig = climate };
                StepIds.Set(add, input, warmed);
                StepIds.Set(band, warmed, colored);
                StepIds.Set(remove, colored, output);
                band.bellowThreshold = snowline;
                band.smoothness = 40f;
                band.materialIndex = land;
                water.bellowThreshold = sea;
                water.materialIndex = seaSlot;
                config.noiseSteps = config.noiseSteps.Concat(new[] { add, remove }).ToArray();
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
                SpawnTable(config) = null;
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
                if (GrassConfig != null)
                    GrassConfig.elevationMin = grass;
            }

            /// <summary>Forest patches: how much the density noise favors trees (higher offset, more ground covered).</summary>
            public void ForestDensity(float noiseOffset)
            {
                Flora.configs[0].noiseOffset = noiseOffset;
            }

            /// <summary>
            /// A new forest type with its own density noise, so it grows in its own regions of the
            /// planet (conifers here, broadleaf woods there, savanna elsewhere).
            /// </summary>
            public void AddForest(Vector3 noiseShift, float noiseOffset, float elevationMin, params (GameObject Prefab, int Weight, float Bias)[] trees)
            {
                trees = trees.Where(t => t.Prefab != null).ToArray();
                if (trees.Length == 0 || Common.DevHarness.Off("forests") || Common.Settings.LowPerformance.Value)
                    return;
                PlanetDensePlacementConfig copy = (PlanetDensePlacementConfig)ShallowCopy.Invoke(Flora.configs[0], null);
                // Its own spawn table and noise buffer (the copy would share the fir forest's)
                SpawnTable(copy) = null;
                NoiseBuffer(copy) = null;
                // Only one forest can be baked into the planet's look from space: the first one
                copy.isVisibleFromSpace = false;
                var noise = copy.noiseConfig;
                noise.center += noiseShift;
                copy.noiseConfig = noise;
                copy.noiseOffset = noiseOffset;
                copy.elevationMin = elevationMin;
                // Each forest is generated and drawn over its whole view range, again for every
                // config: the extra ones draw nearer (the first, seen from space, keeps the full
                // range) and collide nearer. Flying fast over TF Gaïa (four forests) cost 41 fps
                // instead of 48 without the extra ones.
                ViewRange(copy) = Mathf.Min(ViewRange(copy), 1800f);
                copy.physicsRange = Mathf.Min(copy.physicsRange, 80f);
                copy.objectsSpawn = trees.Select(t => new PlanetDensePlacementConfig.DenseObjectPlacementType
                {
                    prefab = t.Prefab,
                    probability = t.Weight,
                    densityBiasValue = t.Bias,
                    densityBiasPower = 1f
                }).ToArray();
                // The game numbers the colliding configs in order among themselves: they must come first
                int at = Flora.configs.TakeWhile(c => c.hasPhysics).Count();
                var configs = Flora.configs.ToList();
                configs.Insert(at, copy);
                Flora.configs = configs.ToArray();
            }

            /// <summary>One of the mod's broadleaf trees, built from this planet's fir.</summary>
            public GameObject Tree(TreeKind kind) => Worlds.Trees.Get(TreePrefab("FirTreeTall1"), kind);

            public void Grass(GameObject prefab)
            {
                if (GrassConfig != null && GrassConfig.objectsSpawn.Length > 0)
                    GrassConfig.objectsSpawn[0].prefab = prefab;
            }

            /// <summary>One of the mod's undergrowth plants, built from this planet's grass and fir.</summary>
            public GameObject Plant(PlantKind kind) => Worlds.Trees.Plant(GrassPrefab, TreePrefab("FirTreeTall1"), kind);

            /// <summary>The grass layer's mix: each entry is a plant and its weight (grass, ferns, flowers...).</summary>
            public void Undergrowth(float density, params (GameObject Prefab, int Weight)[] plants)
            {
                PlanetDensePlacementConfig grass = GrassConfig;
                plants = plants.Where(p => p.Prefab != null).ToArray();
                if (grass == null || plants.Length == 0)
                    return;
                SpawnTable(grass) = null;
                grass.objectsSpawn = plants.Select(p => new PlanetDensePlacementConfig.DenseObjectPlacementType
                {
                    prefab = p.Prefab,
                    probability = p.Weight,
                    densityBiasValue = 0f,
                    densityBiasPower = 1f
                }).ToArray();
                grass.noiseOffset = density;
            }

            public GameObject TreePrefab(string name) => Flora.configs[0].objectsSpawn.Select(o => o.prefab).FirstOrDefault(p => p != null && p.name == name);

            public GameObject GrassPrefab => GrassConfig?.objectsSpawn.Select(o => o.prefab).FirstOrDefault();

            // The demo planet's grass: its only config without colliders
            private PlanetDensePlacementConfig GrassConfig => Flora.configs.FirstOrDefault(c => !c.hasPhysics);

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

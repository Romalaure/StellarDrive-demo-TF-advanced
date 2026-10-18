using System;
using System.Collections.Generic;
using System.Linq;
using Planets.Objects;
using Planets.Objects.Common.Model;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StellarDriveDemoTFAdvanced.Worlds
{
    /// <summary>
    /// Nature scenes built in Blender from Kenney's Nature Kit (CC0, www.kenney.nl; see
    /// tools/blender/nature_*.py): an abandoned camp with a chest in its tent, a forest floor of
    /// fallen trunks, stumps and mushrooms, a heap of boulders. Each scene is one point of interest
    /// (a copy of the demo's large rock, like the ruins), drawn in flat colors, colliding only where
    /// it is solid (plants, moss and mushrooms are walked through).
    /// </summary>
    internal static class NatureSites
    {
        public const string Camp = "nature_camp", Clearing = "nature_clearing", Boulders = "nature_boulders";
        private const string Solid = "TFA_Solid";

        private static readonly Dictionary<string, GameObject> Built = new Dictionary<string, GameObject>();
        private static readonly Dictionary<string, Material> Looks = new Dictionary<string, Material>();
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static GameObject _holder;
        private static Texture2D _white;

        // The scenes' looks: Kenney's bright palette brought to the game's tones
        private static readonly Dictionary<string, Color> Palette = new Dictionary<string, Color>
        {
            ["Bark"] = new Color(0.38f, 0.26f, 0.17f), ["Wood"] = new Color(0.56f, 0.4f, 0.25f), ["WoodInner"] = new Color(0.8f, 0.66f, 0.47f),
            ["Leaves"] = new Color(0.3f, 0.5f, 0.22f), ["Autumn"] = new Color(0.8f, 0.45f, 0.16f), ["Stone"] = new Color(0.54f, 0.55f, 0.57f),
            ["Dirt"] = new Color(0.45f, 0.34f, 0.23f), ["Canvas"] = new Color(0.66f, 0.22f, 0.17f), ["Pale"] = new Color(0.88f, 0.86f, 0.82f),
            ["Metal"] = new Color(0.5f, 0.5f, 0.53f), ["Dark"] = new Color(0.13f, 0.13f, 0.14f), ["Accent"] = new Color(0.86f, 0.7f, 0.22f),
            ["Water"] = new Color(0.22f, 0.46f, 0.7f)
        };

        /// <summary>The camp's chest: inside the open tent (the scene's own frame).</summary>
        public static readonly Vector3[] CampChest = { new Vector3(-1.2f, 0.1f, 4.0f) };

        public static GameObject Prefab(PlanetShaper.Context planet, string scene)
        {
            if (Built.TryGetValue(scene, out GameObject cached) && cached != null)
                return cached;
            GameObject rock = planet.HotspotPrefab("RockLargeIron");
            Dictionary<string, Mesh> meshes = ObjModels.Load(scene);
            if (rock == null || meshes == null)
                return null;
            try
            {
                GameObject site = Build(rock, scene, meshes);
                Built[scene] = site;
                return site;
            }
            catch (Exception e)
            {
                AdvancedMod.Log.Error($"nature: could not build {scene}: {e}");
                return null;
            }
        }

        private static GameObject Build(GameObject rock, string scene, Dictionary<string, Mesh> meshes)
        {
            if (_holder == null)
            {
                _holder = new GameObject("TFA_NaturePrefabs");
                _holder.SetActive(false);
                Object.DontDestroyOnLoad(_holder);
            }
            GameObject copy = Object.Instantiate(rock, _holder.transform);
            copy.name = "TFA_Nature_" + scene;
            MeshRenderer renderer = copy.transform.Find("Visuals").GetComponent<MeshRenderer>();
            Material stone = renderer.sharedMaterial;
            var looks = meshes.Where(m => m.Key != Solid).OrderByDescending(m => m.Value.vertexCount).ToList();
            // The biggest look on the rock's own renderer, the others as its children
            renderer.GetComponent<MeshFilter>().sharedMesh = looks[0].Value;
            renderer.sharedMaterial = Look(stone, looks[0].Key);
            foreach (KeyValuePair<string, Mesh> look in looks.Skip(1))
            {
                var child = new GameObject(look.Key) { layer = renderer.gameObject.layer };
                child.transform.SetParent(renderer.transform, false);
                child.AddComponent<MeshFilter>().sharedMesh = look.Value;
                child.AddComponent<MeshRenderer>().sharedMaterial = Look(stone, look.Key);
            }
            PlanetObjectVisuals visuals = copy.GetComponentInChildren<PlanetObjectVisuals>(true);
            if (visuals != null)
                visuals.id = copy.name;

            Transform physics = copy.transform.Find("Physics");
            meshes.TryGetValue(Solid, out Mesh solid);
            foreach (MeshCollider collider in physics.GetComponents<MeshCollider>())
            {
                collider.convex = false;
                collider.sharedMesh = solid;
                collider.enabled = solid != null;
            }
            foreach (MinablePlanetObject minable in physics.GetComponents<MinablePlanetObject>())
                Object.DestroyImmediate(minable);
            return copy;
        }

        // The rock's planet-object material in a flat color: no rock texture, no shine
        private static Material Look(Material stone, string key)
        {
            if (Looks.TryGetValue(key, out Material cached) && cached != null)
                return cached;
            if (_white == null)
            {
                _white = new Texture2D(4, 4) { name = "TFA_NatureWhite" };
                _white.SetPixels(Enumerable.Repeat(Color.white, 16).ToArray());
                _white.Apply();
            }
            var material = new Material(stone) { name = "TFA_Nature_" + key };
            if (material.HasProperty("_Albedo"))
                material.SetTexture("_Albedo", _white);
            if (material.HasProperty("_Normal"))
                material.SetTexture("_Normal", Texture2D.normalTexture);
            if (material.HasProperty("_Metalness"))
                material.SetFloat("_Metalness", 0.05f);
            if (material.HasProperty("_Roughness"))
                material.SetFloat("_Roughness", 0.85f);
            if (material.HasProperty(ColorId))
                material.SetColor(ColorId, Palette.TryGetValue(key, out Color color) ? color : Color.white);
            Looks[key] = material;
            return material;
        }

        /// <summary>The camp's chest: wood, metal, a key now and then, the planet's own ore.</summary>
        public static List<(uint Item, int Quantity)> CampLoot(ulong planetId, System.Random random)
        {
            var loot = new List<(uint, int)> { (Items.ModItems.Wood, random.Next(6, 16)), (Items.ModItems.Iron, random.Next(4, 12)) };
            if (random.NextDouble() < 0.5)
                loot.Add((Items.ModItems.Copper, random.Next(3, 9)));
            if (random.NextDouble() < 0.15)
                loot.Add((Items.ModItems.Key, 1));
            if (random.NextDouble() < 0.35)
                loot.Add((planetId == 108 || planetId == 104 || planetId == 107 ? Items.ModItems.Amber : Items.ModItems.Quartz, random.Next(2, 6)));
            return loot;
        }
    }
}

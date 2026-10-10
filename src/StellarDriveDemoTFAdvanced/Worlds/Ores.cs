using System.Collections.Generic;
using HarmonyLib;
using Items.Model;
using Planets.Objects;
using Planets.Objects.Common.Model;
using StellarDriveDemoTFAdvanced.Items;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Worlds
{
    internal enum OreKind
    {
        Titanium,
        Quartz,
        Cryonite,
        Amber
    }

    /// <summary>
    /// Deposits of the mod's ores: copies of the demo's minable rocks and crystals (model,
    /// collision, health, mining and drop handling) with another stone color and the mod's item
    /// as their drop, mined with the mining laser like the game's own.
    /// </summary>
    internal static class Ores
    {
        private static readonly AccessTools.FieldRef<MinablePlanetObject, string> Label = AccessTools.FieldRefAccess<MinablePlanetObject, string>("label");
        private static readonly AccessTools.FieldRef<MinablePlanetObject, float> Health = AccessTools.FieldRefAccess<MinablePlanetObject, float>("initialHealth");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static readonly Dictionary<OreKind, GameObject> Built = new Dictionary<OreKind, GameObject>();
        private static GameObject _holder;

        /// <summary>A deposit prefab, or null when the demo rock it copies is not on this planet's list.</summary>
        public static GameObject Prefab(PlanetShaper.Context planet, OreKind kind)
        {
            if (Built.TryGetValue(kind, out GameObject cached) && cached != null)
                return cached;
            string donor;
            uint item;
            Color tint, glow = Color.black;
            float scale, health;
            int min, max;
            switch (kind)
            {
                case OreKind.Titanium: donor = "RockLargeIron"; item = ModItems.Titanium; tint = new Color(0.62f, 0.7f, 0.86f); scale = 0.8f; health = 22f; min = 4; max = 9; break;
                case OreKind.Quartz: donor = "CrystallSmallGlass"; item = ModItems.Quartz; tint = new Color(1.1f, 0.65f, 0.85f); scale = 1.4f; health = 3f; min = 2; max = 4; break;
                case OreKind.Cryonite: donor = "CrystallSmallGlass"; item = ModItems.Cryonite; tint = new Color(0.45f, 0.9f, 1.25f); glow = new Color(0.05f, 0.35f, 0.6f); scale = 1.6f; health = 6f; min = 2; max = 4; break;
                default: donor = "RockSmallCopper"; item = ModItems.Amber; tint = new Color(1.2f, 0.62f, 0.15f); glow = new Color(0.25f, 0.1f, 0f); scale = 1.6f; health = 5f; min = 1; max = 3; break;
            }
            GameObject rock = planet.HotspotPrefab(donor);
            ItemSettings settings = ModItems.Settings(item);
            if (rock == null || settings == null)
            {
                AdvancedMod.Log.Warning($"planets: no {donor} to copy, no {kind} deposits");
                return null;
            }
            if (_holder == null)
            {
                _holder = new GameObject("TFA_OrePrefabs");
                _holder.SetActive(false);
                Object.DontDestroyOnLoad(_holder);
            }
            GameObject copy = Object.Instantiate(rock, _holder.transform);
            copy.name = "TFA_Ore" + kind;
            foreach (Transform child in copy.transform)
                child.localScale *= scale;
            foreach (MeshRenderer renderer in copy.GetComponentsInChildren<MeshRenderer>(true))
                renderer.sharedMaterial = Tinted(renderer.sharedMaterial, kind, tint, glow);
            PlanetObjectVisuals visuals = copy.GetComponentInChildren<PlanetObjectVisuals>(true);
            if (visuals != null)
                visuals.id = copy.name;
            foreach (MinablePlanetObject minable in copy.GetComponentsInChildren<MinablePlanetObject>(true))
            {
                minable.dropRate = new ItemDropRate { item = settings, amountMin = min, amountMax = max + 1 };
                Label(minable) = settings.itemName;
                Health(minable) = health;
            }
            Built[kind] = copy;
            return copy;
        }

        private static Material Tinted(Material source, OreKind kind, Color tint, Color glow)
        {
            if (source == null)
                return null;
            var material = new Material(source) { name = source.name + "_" + kind };
            if (material.HasProperty(ColorId))
                material.SetColor(ColorId, material.GetColor(ColorId) * tint);
            if (glow != Color.black && material.HasProperty(EmissionId))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor(EmissionId, glow);
            }
            return material;
        }
    }
}

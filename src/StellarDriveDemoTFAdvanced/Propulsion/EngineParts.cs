using System.Collections.Generic;
using Ships.Interface.Model.Parts;
using Ships.Interface.Settings;
using StellarDriveDemoTFAdvanced.Parts;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Propulsion
{
    /// <summary>
    /// Registers the engines as clones of the game's Chemical Thruster: same model, flame, sound,
    /// propellant sockets and ThrusterState, so the game feeds, throttles, saves and synchronizes
    /// them like its own thruster. Size, tint, mass and cost differ; thrust and consumption are
    /// changed by <see cref="ThrustPatches"/>. Only call when SDModKit is loaded.
    /// </summary>
    internal static class EngineParts
    {
        private const string Donor = "Thruster";
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly Dictionary<string, Material> Tinted = new Dictionary<string, Material>();

        public static void Register()
        {
            foreach (EngineSpec engine in EngineCatalog.All)
            {
                EngineSpec spec = engine;
                PartFactory.Register(spec.Id, Donor, spec.Tab, spec.Row, (settings, prefab) => Configure(spec, settings, prefab));
            }
            AdvancedMod.Log.Msg($"registered {EngineCatalog.All.Length} engines");
        }

        private static void Configure(EngineSpec spec, PartSettings settings, GameObject prefab)
        {
            PartFactory.Describe(settings, spec.Label, spec.Description, spec.Mass, spec.Cost);
            Scale(prefab, spec.Scale);
            Tint(prefab, spec);
        }

        // Every direct child (model, sockets, interaction collider) scales around the part origin
        private static void Scale(GameObject prefab, float scale)
        {
            if (Mathf.Approximately(scale, 1f))
                return;
            foreach (Transform child in prefab.transform)
            {
                child.localPosition *= scale;
                child.localScale *= scale;
            }
            var bounds = prefab.GetComponent<ShipPartBounds>();
            if (bounds != null)
            {
                bounds.center *= scale;
                bounds.bounds *= scale;
            }
            // The flame follows the model's size
            foreach (ParticleSystem particles in prefab.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = particles.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
        }

        // The engine body only: the game's plugs keep their usual look
        private static void Tint(GameObject prefab, EngineSpec spec)
        {
            Transform visual = prefab.transform.Find("Visual");
            if (visual == null)
                return;
            foreach (MeshRenderer renderer in visual.GetComponentsInChildren<MeshRenderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material original = materials[i];
                    if (original == null || !original.HasProperty(ColorId) || original.shader == null || original.shader.name != "Custom/PlanetObject")
                        continue;
                    materials[i] = TintedCopy(original, spec);
                }
                renderer.sharedMaterials = materials;
            }
        }

        private static Material TintedCopy(Material original, EngineSpec spec)
        {
            string name = original.name + "_TFA" + spec.Id;
            if (Tinted.TryGetValue(name, out Material existing) && existing != null)
                return existing;
            var copy = new Material(original) { name = name };
            copy.SetColor(ColorId, original.GetColor(ColorId) * spec.Tint);
            if (spec.Glow != Color.black && copy.HasProperty("_EmissionColor"))
            {
                copy.EnableKeyword("_EMISSION");
                copy.SetColor("_EmissionColor", spec.Glow);
            }
            Tinted[name] = copy;
            return copy;
        }
    }
}

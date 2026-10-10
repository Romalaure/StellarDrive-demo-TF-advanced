using System.Collections.Generic;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
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
            Shroud(prefab, spec);
            // Plugs on the surface of the resized engine, not inside or away from it
            var bounds = prefab.GetComponent<ShipPartBounds>();
            if (bounds != null && !Mathf.Approximately(spec.Scale, 1f))
            {
                foreach (SocketObject socket in prefab.GetComponentsInChildren<SocketObject>(true))
                    SDModKit.Content.PartFinish.Seat("TFA", prefab, bounds, socket);
            }
        }

        /// <summary>
        /// An armored shroud around the engine body, in the machines' style: faceted plates with
        /// glowing vent slats and white edge strips, colored per engine.
        /// </summary>
        private static void Shroud(GameObject prefab, EngineSpec spec)
        {
            Transform visual = prefab.transform.Find("Visual");
            var bounds = prefab.GetComponent<ShipPartBounds>();
            if (visual == null || bounds == null)
                return;
            // The engine's own model, in the part's frame (the placement box is larger)
            Bounds body = new Bounds();
            bool any = false;
            foreach (MeshRenderer r in visual.GetComponentsInChildren<MeshRenderer>(true))
            {
                MeshFilter f = r.GetComponent<MeshFilter>();
                if (f == null || f.sharedMesh == null || r.GetComponentInParent<SocketObject>() != null
                    || r.sharedMaterial == null || r.sharedMaterial.shader == null || r.sharedMaterial.shader.name != "Custom/PlanetObject" || r.name.StartsWith("TF"))
                    continue;
                Bounds mb = f.sharedMesh.bounds;
                for (int k = 0; k < 8; k++)
                {
                    Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                    Vector3 p = prefab.transform.InverseTransformPoint(r.transform.TransformPoint(corner));
                    if (!any) { body = new Bounds(p, Vector3.zero); any = true; }
                    else body.Encapsulate(p);
                }
            }
            if (!any)
                return;
            Vector3 c = body.center, s = body.size;
            // The thruster's nozzle opens along its model's y axis: plates around it, never across it
            const int axis = 1;
            float length = s[axis] * 0.42f;
            var model = new Power.MachineModels.Model();
            for (int i = 0; i < 4; i++)
            {
                // The two other axes, both directions
                int side = (axis + 1 + i / 2) % 3;
                float sign = i % 2 == 0 ? 1f : -1f;
                Vector3 normal = Vector3.zero;
                normal[side] = sign;
                int across = 3 - axis - side;
                Vector3 size = Vector3.zero;
                size[axis] = length;
                size[side] = 0.03f * spec.Scale;
                size[across] = s[across] * 0.48f;
                // Snug against the nozzle's wall
                Vector3 center = c + normal * (s[side] * 0.34f);
                model.Armor.Box(center, size);
                // Three slats and a white strip along the plate's edge
                for (int k = 0; k < 3; k++)
                {
                    Vector3 slat = size;
                    slat[side] = 0.012f;
                    slat[axis] = length * 0.12f;
                    slat[across] = size[across] * 0.7f;
                    Vector3 offset = Vector3.zero;
                    offset[axis] = (k - 1) * length * 0.25f;
                    model.Glow.Box(center + normal * 0.018f * spec.Scale + offset, slat);
                }
                Vector3 strip = Vector3.zero;
                strip[axis] = length;
                strip[side] = 0.012f;
                strip[across] = 0.012f;
                Vector3 edge = Vector3.zero;
                edge[across] = size[across] * 0.5f;
                model.Light.Box(center + normal * 0.018f * spec.Scale + edge, strip);
            }
            Power.MachineModels.Attach(visual, model, "Engine" + spec.Key, spec.Glow != Color.black ? Power.MachineModels.Violet : Power.MachineModels.Red, Color.white);
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

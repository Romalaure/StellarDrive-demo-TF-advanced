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
            if (spec.Fuel != 0)
            {
                // Command socket at the back of the mount plate: a signal there sets the thrust (0-1)
                Power.PowerParts.GraftSeated(prefab, prefab.GetComponent<ShipPartBounds>(), EngineCatalog.CommandSocket, CableType.Signal,
                    new Vector3(0f, 0.07f * spec.Scale, -0.47f * spec.Scale), Vector3.back);
            }
            if (spec.Model != null)
            {
                Detailed(prefab, spec);
                return;
            }
            Tint(prefab, spec);
            Shroud(prefab, spec);
        }

        /// <summary>
        /// The engine's own model in place of the game's thruster (hidden; its flame, sound and
        /// plugs stay), sized on it so the flame still leaves the nozzle.
        /// </summary>
        private static void Detailed(GameObject prefab, EngineSpec spec)
        {
            Transform visual = prefab.transform.Find("Visual");
            if (visual == null || !BodyBounds(visual, out Bounds body))
                return;
            Power.NuclearParts.HideDonorModel(visual);
            var model = new Power.MachineModels.Model();
            spec.Model(model, body);
            Power.MachineModels.Attach(visual, model, "Engine" + spec.Key, spec.ModelGlow, spec.ModelCore);
            if (spec.Flame != Color.white)
            {
                foreach (ParticleSystem particles in prefab.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ParticleSystem.MainModule main = particles.main;
                    ParticleSystem.MinMaxGradient color = main.startColor;
                    color.color *= spec.Flame;
                    color.colorMin *= spec.Flame;
                    color.colorMax *= spec.Flame;
                    main.startColor = color;
                }
            }
        }

        /// <summary>The thruster's model, in the visual's frame: base plate at the bottom, bell above.</summary>
        private static bool BodyBounds(Transform visual, out Bounds body)
        {
            body = new Bounds();
            bool any = false;
            foreach (MeshRenderer r in visual.GetComponentsInChildren<MeshRenderer>(true))
            {
                MeshFilter f = r.GetComponent<MeshFilter>();
                if (f == null || f.sharedMesh == null || r.GetComponentInParent<SocketObject>() != null || r.name.StartsWith("TF"))
                    continue;
                Bounds mb = f.sharedMesh.bounds;
                for (int k = 0; k < 8; k++)
                {
                    Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                    Vector3 p = visual.InverseTransformPoint(r.transform.TransformPoint(corner));
                    if (!any) { body = new Bounds(p, Vector3.zero); any = true; }
                    else body.Encapsulate(p);
                }
            }
            return any;
        }

        /// <summary>
        /// An armored collar around the engine's bell, in the machines' style: an octagonal band
        /// with a white edge, and four fins on the diagonals (clear of the propellant plugs) with
        /// glowing slats. Built in the model's own frame, so it follows the engine's scale.
        /// </summary>
        private static void Shroud(GameObject prefab, EngineSpec spec)
        {
            Transform visual = prefab.transform.Find("Visual");
            if (visual == null)
                return;
            if (!BodyBounds(visual, out Bounds body))
                return;
            // One unit = the game thruster's base half-width (0.5 m)
            float u = Mathf.Max(body.extents.x, body.extents.z) / 0.5f;
            float bottom = body.min.y;
            float height = body.size.y;
            float Y(float fraction) => bottom + height * fraction;
            var model = new Power.MachineModels.Model();
            // Collar around the bell's foot
            float collarLow = Y(0.3f), collarHigh = Y(0.42f), collarRadius = 0.43f * u;
            model.Armor.Cylinder(new Vector3(0f, collarLow, 0f), new Vector3(0f, collarHigh, 0f), collarRadius, collarRadius * 0.97f, 8, capA: false, capB: false);
            model.Light.Cylinder(new Vector3(0f, collarHigh, 0f), new Vector3(0f, collarHigh + 0.012f * u, 0f), collarRadius * 0.98f, collarRadius * 0.98f, 8, capA: false, capB: false);
            model.Dark.Cylinder(new Vector3(0f, collarLow - 0.02f * u, 0f), new Vector3(0f, collarLow, 0f), collarRadius * 1.01f, collarRadius * 1.01f, 8, capA: false, capB: false);
            // Four fins on the diagonals
            float finLow = Y(0.2f), finHigh = Y(0.72f);
            for (int i = 0; i < 4; i++)
            {
                float angle = (45f + i * 90f) * Mathf.Deg2Rad;
                Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 across = Vector3.Cross(Vector3.up, outward);
                Vector3 center = outward * 0.44f * u + Vector3.up * ((finLow + finHigh) * 0.5f);
                var size = new Vector3(0.05f * u, finHigh - finLow, 0.16f * u);
                model.Armor.Box(center, size, across, Vector3.up, outward);
                // Slats on the fin's outer edge, and a white strip down its top
                for (int k = 0; k < 3; k++)
                {
                    Vector3 slat = center + outward * (size.z * 0.5f + 0.006f * u) + Vector3.up * ((k - 1) * size.y * 0.24f);
                    model.Glow.Box(slat, new Vector3(0.035f * u, size.y * 0.12f, 0.012f * u), across, Vector3.up, outward);
                }
                model.Light.Box(center + Vector3.up * (size.y * 0.5f + 0.006f * u), new Vector3(0.052f * u, 0.012f * u, size.z * 0.95f), across, Vector3.up, outward);
            }
            Power.MachineModels.Attach(visual, model, "Engine" + spec.Key, spec.Glow != Color.black ? Power.MachineModels.Violet : Power.MachineModels.Red, Color.white);
        }

        // Everything scales around the part origin. The game instantiates the model container and the
        // interactions container on their own and puts the sockets' plugs and click zones inside them at
        // the sockets' part-space positions: those two containers keep scale 1, their content scales
        private static void Scale(GameObject prefab, float scale)
        {
            if (Mathf.Approximately(scale, 1f))
                return;
            Transform visual = prefab.GetComponentInChildren<Ships.Parts.Common.Model.Reactions.IShipPartVisual>(true)?.GameObject.transform;
            Transform interactions = prefab.transform.Find("Interactions");
            foreach (Transform child in prefab.transform)
            {
                child.localPosition *= scale;
                if (child == visual || child == interactions)
                {
                    foreach (Transform inner in child)
                    {
                        inner.localPosition *= scale;
                        inner.localScale *= scale;
                    }
                }
                else
                {
                    child.localScale *= scale;
                }
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

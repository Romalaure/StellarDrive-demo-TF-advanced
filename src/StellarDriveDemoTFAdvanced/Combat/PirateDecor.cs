using Ships.Interface.Settings;
using StellarDriveDemoTFAdvanced.Common;
using StellarDriveDemoTFAdvanced.Parts;
using StellarDriveDemoTFAdvanced.Power;
using UnityEngine;
using UnityEngine.Rendering;

namespace StellarDriveDemoTFAdvanced.Combat
{
    /// <summary>
    /// The pirate ships' fairings: one part per design, not in the build menu, holding the whole
    /// dressing of its box hull in the ship's frame (the part sits at the ship's origin): a chin and
    /// ram under the glass cockpit, a canopy hood, a dorsal spine, swept wings with tip pods, canted
    /// tail fins, glowing engine nacelles, side armour, spikes and an antenna. It has no collider:
    /// shots go through it to the iron hull it dresses (the Arsenal mod holes and wears that).
    /// </summary>
    internal static class PirateDecor
    {
        public const ushort ScoutId = 7451, CorsairId = 7452, RaiderId = 7453, CruiserId = 7454;

        public static void Register()
        {
            foreach (PirateDesign design in PirateShips.All)
            {
                PirateDesign d = design;
                // No build tab: only pirates carry them
                PartFactory.Register(d.DecorId, "SignalDisplay", "", 0, (settings, prefab) => Configure(d, settings, prefab));
            }
            AdvancedMod.Log.Msg("registered the pirate fairings");
        }

        private static void Configure(PirateDesign d, PartSettings settings, GameObject prefab)
        {
            PartFactory.Describe(settings, Loc.Pick("Carénage de " + d.Name.ToLowerInvariant(), d.Name + " fairing"), Loc.Pick("Le carénage d'un vaisseau pirate.", "The fairing of a pirate ship."), 40f + 25f * d.Level, new (uint, int)[0]);
            settings.internalStateClass = PowerCatalog.MachineStateClass;
            Transform visuals = PartFactory.BlankFromSignalDisplay(prefab, Vector3.zero, Vector3.one * 0.3f);
            foreach (Collider collider in prefab.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);
            var model = new MachineModels.Model();
            Build(model, d);
            Color armor = Color.Lerp(new Color(d.HullColor.x / 255f, d.HullColor.y / 255f, d.HullColor.z / 255f) * 1.3f, new Color(0.55f, 0.55f, 0.58f), 0.3f);
            int layer = visuals.gameObject.layer;
            Add(visuals, layer, "TFA_Pirate" + d.Key + "Armor", model.Armor, LitMaterials.Get("TFA_PirateArmor_" + d.Key, armor, 0.6f, 0.45f));
            Add(visuals, layer, "TF_NoPaint_Pirate" + d.Key + "Dark", model.Dark, LitMaterials.Get("TFA_PirateDark", new Color(0.05f, 0.05f, 0.06f), 0.4f, 0.75f));
            Add(visuals, layer, "TF_NoPaint_Pirate" + d.Key + "Light", model.Light, LitMaterials.Get("TFA_PirateTrim", new Color(0.75f, 0.72f, 0.66f), 0.7f, 0.5f));
            Add(visuals, layer, "TF_NoPaint_Pirate" + d.Key + "Glow", model.Glow, LitMaterials.Get("TFA_PirateGlow_" + d.Key, d.Glow, 0f, 0.8f, d.Glow * 1.8f));
            Add(visuals, layer, "TF_NoPaint_Pirate" + d.Key + "Core", model.Core, LitMaterials.Get("TFA_PirateCore_" + d.Key, d.Glow, 0f, 0.9f, d.Glow * 3f));
        }

        private static void Add(Transform parent, int layer, string name, MeshBuilder builder, Material material)
        {
            if (!builder.HasGeometry)
                return;
            var child = new GameObject(name) { layer = layer };
            child.transform.SetParent(parent, false);
            child.AddComponent<MeshFilter>().sharedMesh = builder.Build(name);
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
        }

        // A hexahedron from its bottom and top quads (each: back-left, back-right, front-left, front-right)
        private static void Hex(MeshBuilder m, Vector3 bbl, Vector3 bbr, Vector3 bfl, Vector3 bfr, Vector3 tbl, Vector3 tbr, Vector3 tfl, Vector3 tfr) =>
            m.Block(new[] { bbl, bbr, tbl, tbr, bfl, bfr, tfl, tfr });

        /// <summary>The fairing around a box hull: x in ±(HalfWidth + 0.5), y in ±0.5, z in ±(HalfLength + 0.5), nose toward +Z.</summary>
        private static void Build(MachineModels.Model m, PirateDesign d)
        {
            float w = d.HalfWidth + 0.5f, front = d.HalfLength + 0.5f, back = -front;
            bool heavy = d.Level >= 3;
            float span = 1.4f + 0.55f * d.Level;

            // Chin under the cockpit glass, drooping to a ram
            float nose = 1.1f + 0.35f * w;
            Hex(m.Armor,
                new Vector3(-w - 0.04f, -0.56f, front - 0.3f), new Vector3(w + 0.04f, -0.56f, front - 0.3f), new Vector3(-0.22f, -0.38f, front + nose), new Vector3(0.22f, -0.38f, front + nose),
                new Vector3(-w - 0.04f, -0.02f, front - 0.3f), new Vector3(w + 0.04f, -0.02f, front - 0.3f), new Vector3(-0.18f, -0.22f, front + nose), new Vector3(0.18f, -0.22f, front + nose));
            m.Dark.Cylinder(new Vector3(0f, -0.3f, front + nose), new Vector3(0f, -0.3f, front + nose + 0.7f), 0.1f, 0.01f, 6);
            foreach (float s in new[] { -1f, 1f })
            {
                // Side spikes on the chin
                m.Dark.Cylinder(new Vector3(s * (w - 0.1f), -0.3f, front + 0.1f), new Vector3(s * (w + 0.35f), -0.32f, front + 0.9f), 0.07f, 0.01f, 6);
                // Glass frame posts
                m.Light.Box(new Vector3(s * (w - 0.02f), 0.24f, front + 0.02f), new Vector3(0.06f, 0.5f, 0.06f));
            }
            m.Light.Box(new Vector3(0f, -0.01f, front + 0.02f), new Vector3(2f * w, 0.05f, 0.06f));
            m.Glow.Box(new Vector3(0f, -0.08f, front + 0.06f), new Vector3(2f * w * 0.8f, 0.03f, 0.02f));

            // Canopy hood over the cockpit, and the dorsal spine behind it
            float hoodBack = front - 1.6f;
            Hex(m.Dark,
                new Vector3(-w * 0.62f, 0.5f, hoodBack), new Vector3(w * 0.62f, 0.5f, hoodBack), new Vector3(-w * 0.45f, 0.5f, front + 0.25f), new Vector3(w * 0.45f, 0.5f, front + 0.25f),
                new Vector3(-w * 0.45f, 0.88f, hoodBack + 0.2f), new Vector3(w * 0.45f, 0.88f, hoodBack + 0.2f), new Vector3(-w * 0.3f, 0.6f, front + 0.05f), new Vector3(w * 0.3f, 0.6f, front + 0.05f));
            m.Light.Box(new Vector3(0f, 0.9f, hoodBack + 0.6f), new Vector3(0.05f, 0.04f, 1.1f), Quaternion.Euler(-14f, 0f, 0f));
            Hex(m.Armor,
                new Vector3(-0.3f, 0.48f, back + 0.2f), new Vector3(0.3f, 0.48f, back + 0.2f), new Vector3(-0.32f, 0.48f, hoodBack + 0.1f), new Vector3(0.32f, 0.48f, hoodBack + 0.1f),
                new Vector3(-0.12f, 0.7f, back + 0.4f), new Vector3(0.12f, 0.7f, back + 0.4f), new Vector3(-0.2f, 0.86f, hoodBack + 0.2f), new Vector3(0.2f, 0.86f, hoodBack + 0.2f));
            m.Glow.Box(new Vector3(0f, 0.79f, (back + hoodBack) * 0.5f + 0.2f), new Vector3(0.04f, 0.02f, hoodBack - back - 0.6f), Quaternion.Euler(-4f, 0f, 0f));

            // Belly keel
            Hex(m.Armor,
                new Vector3(-0.25f, -0.78f, back + 0.4f), new Vector3(0.25f, -0.78f, back + 0.4f), new Vector3(-0.15f, -0.68f, front - 0.6f), new Vector3(0.15f, -0.68f, front - 0.6f),
                new Vector3(-w * 0.7f, -0.48f, back + 0.2f), new Vector3(w * 0.7f, -0.48f, back + 0.2f), new Vector3(-w * 0.7f, -0.48f, front - 0.3f), new Vector3(w * 0.7f, -0.48f, front - 0.3f));

            foreach (float s in new[] { -1f, 1f })
            {
                float root = s * w, tip = s * (w + span);
                float rootBack = back + 0.4f, rootFront = front - 1.1f, tipBack = back - 0.4f, tipFront = back + 0.9f;
                // Swept wing, thicker at the root
                Hex(m.Armor,
                    new Vector3(root, -0.3f, rootBack), new Vector3(tip, -0.2f, tipBack), new Vector3(root, -0.3f, rootFront), new Vector3(tip, -0.2f, tipFront),
                    new Vector3(root, -0.05f, rootBack), new Vector3(tip, -0.12f, tipBack), new Vector3(root, -0.05f, rootFront), new Vector3(tip, -0.12f, tipFront));
                // Lit leading edge and a dark stripe across the wing
                m.Glow.Cylinder(new Vector3(root, -0.17f, rootFront + 0.03f), new Vector3(tip, -0.16f, tipFront + 0.03f), 0.035f, 0.035f, 6);
                m.Dark.Box(Vector3.Lerp(new Vector3(root, -0.04f, (rootBack + rootFront) * 0.5f), new Vector3(tip, -0.11f, (tipBack + tipFront) * 0.5f), 0.5f),
                    new Vector3(0.12f, 0.02f, 1.2f), Quaternion.Euler(0f, s * -18f, 0f));
                // Tip pod with a running light
                m.Armor.Cylinder(new Vector3(tip, -0.16f, tipBack - 0.3f), new Vector3(tip, -0.16f, tipFront + 0.4f), 0.13f, 0.09f, 8);
                m.Core.Cylinder(new Vector3(tip, -0.16f, tipFront + 0.4f), new Vector3(tip, -0.16f, tipFront + 0.5f), 0.08f, 0.02f, 8);
                // Root spike
                m.Dark.Cylinder(new Vector3(root + s * 0.2f, -0.18f, rootFront), new Vector3(root + s * 0.35f, -0.2f, rootFront + 0.9f), 0.08f, 0.01f, 6);

                // Side armour plates, riveted
                for (float z = back + 0.6f; z < front - 0.6f; z += 1.6f)
                {
                    m.Armor.Box(new Vector3(s * (w + 0.04f), 0.08f, z + 0.65f), new Vector3(0.06f, 0.62f, 1.4f));
                    m.Light.Box(new Vector3(s * (w + 0.075f), 0.34f, z + 0.65f), new Vector3(0.012f, 0.03f, 1.3f));
                }

                // Canted tail fin
                float finX = s * Mathf.Max(0.35f, w * 0.6f), finH = heavy ? 1.3f : 1f;
                Hex(m.Armor,
                    new Vector3(finX - 0.04f, 0.5f, back + 0.1f), new Vector3(finX + 0.04f, 0.5f, back + 0.1f), new Vector3(finX - 0.04f, 0.5f, back + 1.7f), new Vector3(finX + 0.04f, 0.5f, back + 1.7f),
                    new Vector3(finX + s * 0.45f - 0.03f, 0.5f + finH, back - 0.3f), new Vector3(finX + s * 0.45f + 0.03f, 0.5f + finH, back - 0.3f),
                    new Vector3(finX + s * 0.45f - 0.03f, 0.5f + finH, back + 0.45f), new Vector3(finX + s * 0.45f + 0.03f, 0.5f + finH, back + 0.45f));
                m.Glow.Cylinder(new Vector3(finX + s * 0.45f, 0.52f + finH, back - 0.3f), new Vector3(finX + s * 0.45f, 0.52f + finH, back + 0.45f), 0.03f, 0.03f, 6);
            }

            // Engine nacelles at the stern, exhausts glowing
            float[] engines = d.Level >= 4 ? new[] { -w * 0.6f, 0f, w * 0.6f } : d.Level == 1 ? new[] { 0f } : new[] { -w * 0.5f, w * 0.5f };
            float r = d.Level == 1 ? 0.35f : 0.45f;
            foreach (float x in engines)
            {
                var c0 = new Vector3(x, -0.05f, back + 0.3f);
                var c1 = new Vector3(x, -0.05f, back - 1.3f);
                m.Armor.Cylinder(c0, c1, r, r * 1.05f, 12, capA: true, capB: false);
                m.Dark.Cylinder(c1 + Vector3.forward * 0.25f, c1, r * 0.8f, r * 0.95f, 12, capA: true, capB: false);
                m.Core.Cylinder(c1 + Vector3.forward * 0.24f, c1 + Vector3.forward * 0.2f, r * 0.75f, r * 0.75f, 12);
                m.Glow.Cylinder(c1 - Vector3.forward * 0.01f, c1 + Vector3.forward * 0.03f, r * 1.07f, r * 1.07f, 12, capA: false, capB: false);
                for (int i = 0; i < 4; i++)
                {
                    float a = (45f + i * 90f) * Mathf.Deg2Rad;
                    var o = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                    m.Light.Box(c0 + o * (r + 0.03f) + Vector3.back * 0.8f, new Vector3(0.04f, 0.04f, 1.1f), Quaternion.LookRotation(Vector3.forward, o));
                }
            }

            // Antenna mast with a lit tip
            m.Dark.Cylinder(new Vector3(0f, 0.75f, back + 0.6f), new Vector3(0f, 1.6f + 0.2f * d.Level, back + 0.4f), 0.03f, 0.015f, 6);
            m.Core.Cylinder(new Vector3(0f, 1.6f + 0.2f * d.Level, back + 0.4f), new Vector3(0f, 1.68f + 0.2f * d.Level, back + 0.4f), 0.05f, 0.05f, 6);
        }
    }
}

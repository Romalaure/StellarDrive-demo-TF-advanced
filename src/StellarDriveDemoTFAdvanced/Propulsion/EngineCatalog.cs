using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Propulsion
{
    /// <summary>
    /// The Advanced Propulsion Pack engines. All are chemical thrusters (oxygen + ethanol, fed and
    /// throttled exactly like the game's Chemical Thruster: valves on the propellant lines), scaled
    /// against it: 7 800 N at full flow, 0.05 unit/s of each propellant, 20 kg.
    /// </summary>
    internal sealed class EngineSpec
    {
        public ushort Id;
        public string Key;
        public string Label;
        public string Description;
        public float DefaultThrust;
        public float DefaultFuel;
        public float Mass;
        /// <summary>Model and footprint size relative to the Chemical Thruster.</summary>
        public float Scale;
        /// <summary>Also turns the ship: the thrust acts at the engine's position, not through the center of mass.</summary>
        public bool OffCenterThrust;
        /// <summary>Seconds to go from idle to full thrust (0 = immediate, like the game's thruster).</summary>
        public float SpoolSeconds;
        public Color Tint;
        /// <summary>Glow of the engine body (black: none).</summary>
        public Color Glow = Color.black;
        public (uint Item, int Count)[] Cost;
        /// <summary>Build menu tab and row (default: TF Avancé, propulsion row).</summary>
        public string Tab = Parts.AdvancedTab.Name;
        public int Row = Parts.AdvancedTab.PropulsionRow;

        public MelonPreferences_Entry<float> ThrustSetting;
        public MelonPreferences_Entry<float> FuelSetting;

        public float ThrustMultiplier => ThrustSetting != null ? Mathf.Clamp(ThrustSetting.Value, 0f, 10f) : DefaultThrust;
        public float FuelMultiplier => FuelSetting != null ? Mathf.Clamp(FuelSetting.Value, 0.01f, 10f) : DefaultFuel;
    }

    internal static class EngineCatalog
    {
        // The game's Chemical Thruster (Ships.Physics.Forces.ShipThrust, ShipThrustersUpdater)
        public const float BaseThrust = Ships.Interface.Model.Parts.State.ThrusterState.EthanolXOxygenStrength;
        public const float BaseFlow = Ships.Interface.Model.Parts.State.ThrusterState.MaxFluidConsumptionRate;

        public static readonly EngineSpec[] All =
        {
            new EngineSpec
            {
                Id = 7311, Key = "Vector", Label = "Propulseur vectoriel",
                Description = "Petit propulseur de manœuvre. Sa poussée s'applique là où il est monté : décentré, il fait aussi tourner le vaisseau (couple réel). 1 950 N, consommation proportionnelle. Ergols et commande comme le propulseur chimique (vannes).",
                DefaultThrust = 0.25f, DefaultFuel = 0.25f, Mass = 6f, Scale = 0.55f, OffCenterThrust = true,
                Tint = new Color(1f, 0.82f, 0.62f), Cost = new[] { (101u, 2), (112u, 1) }
            },
            new EngineSpec
            {
                Id = 7312, Key = "Heavy", Label = "Moteur lourd",
                Description = "Moteur principal pour gros vaisseaux et charges lourdes : 23 400 N, trois fois le propulseur chimique, un peu moins efficace (+10 % d'ergols par newton). Met 1,5 s à atteindre sa pleine poussée.",
                DefaultThrust = 3f, DefaultFuel = 3.3f, Mass = 75f, Scale = 1.5f, SpoolSeconds = 1.5f,
                Tint = new Color(0.62f, 0.64f, 0.68f), Cost = new[] { (101u, 8), (104u, 4), (112u, 2) }
            },
            new EngineSpec
            {
                Id = 7313, Key = "Precision", Label = "Moteur de précision",
                Description = "Très faible poussée (624 N) pour les petites corrections de trajectoire et d'orientation. Décentré, il produit un couple réel. Réponse immédiate.",
                DefaultThrust = 0.08f, DefaultFuel = 0.08f, Mass = 4f, Scale = 0.4f, OffCenterThrust = true,
                Tint = new Color(0.7f, 0.86f, 1f), Cost = new[] { (101u, 1), (112u, 1) }
            },
            new EngineSpec
            {
                Id = 7314, Key = "Efficient", Label = "Moteur économe",
                Description = "Privilégie l'autonomie : 4 680 N pour seulement 35 % des ergols du propulseur chimique (environ 1,7 fois plus de poussée par unité). Montée en régime lente : 4 s.",
                DefaultThrust = 0.6f, DefaultFuel = 0.35f, Mass = 28f, Scale = 1.1f, SpoolSeconds = 4f,
                Tint = new Color(0.72f, 0.92f, 0.74f), Cost = new[] { (101u, 4), (104u, 2), (112u, 3) }
            },
            new EngineSpec
            {
                Id = 7315, Key = "Exotic", Label = "Moteur à matière exotique",
                Description = "La matière exotique ramassée près des trous noirs, brûlée dans un champ quantique : 78 000 N (dix propulseurs chimiques) pour à peine 10 % de leurs ergols. Montée en régime : 3 s. Ergols et commande comme le propulseur chimique (vannes).",
                DefaultThrust = 10f, DefaultFuel = 0.1f, Mass = 140f, Scale = 1.9f, SpoolSeconds = 3f,
                Tint = new Color(0.42f, 0.3f, 0.62f), Glow = new Color(0.18f, 0.05f, 0.35f),
                Cost = new[] { (Items.ModItems.ExoticMatter, 3), (Items.ModItems.QuantumCore, 1), (Items.ModItems.Titanium, 12), (Items.ModItems.NexoranAlloy, 6) },
                Tab = Parts.AdvancedTab.ArtifactsName, Row = Parts.AdvancedTab.ArtifactsPropulsionRow
            }
        };

        private static readonly Dictionary<ushort, EngineSpec> ById = new Dictionary<ushort, EngineSpec>();

        static EngineCatalog()
        {
            foreach (EngineSpec spec in All)
                ById[spec.Id] = spec;
        }

        public static bool TryGet(ushort partSettingsId, out EngineSpec spec) => ById.TryGetValue(partSettingsId, out spec);

        /// <summary>Balancing values in MelonPreferences. In multiplayer the host's values drive the physics.</summary>
        public static void LoadSettings()
        {
            MelonPreferences_Category category = MelonPreferences.CreateCategory("TFAdvancedEngines", "TF Advanced - engines");
            foreach (EngineSpec spec in All)
            {
                spec.ThrustSetting = category.CreateEntry(spec.Key + "Thrust", spec.DefaultThrust,
                    description: $"{spec.Label}: thrust as a multiple of the Chemical Thruster's {BaseThrust:0} N.");
                spec.FuelSetting = category.CreateEntry(spec.Key + "Fuel", spec.DefaultFuel,
                    description: $"{spec.Label}: oxygen and ethanol use at full thrust, as a multiple of the Chemical Thruster's {BaseFlow} unit/s each.");
            }
        }
    }
}

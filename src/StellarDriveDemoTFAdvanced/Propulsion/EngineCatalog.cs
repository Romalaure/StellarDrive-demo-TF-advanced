using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using StellarDriveDemoTFAdvanced.Common;

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
        /// <summary>
        /// Special propellant on one of the two propellant sockets (0: the Chemical Thruster's
        /// oxygen + ethanol). With it, the other socket takes oxygen as reaction mass.
        /// </summary>
        public uint Fuel;
        /// <summary>Oxygen use (reaction mass) of a special-propellant engine, as a multiple of the Chemical Thruster's flow.</summary>
        public float ReactionMass;
        /// <summary>The engine's own detailed model, built in the thruster's frame (null: the game's thruster with a collar).</summary>
        public System.Action<Power.MachineModels.Model, Bounds> Model;
        public Color ModelGlow = Power.MachineModels.Red;
        public Color ModelCore = Color.white;
        /// <summary>Flame color (white: the game's).</summary>
        public Color Flame = Color.white;
        /// <summary>Build menu tab and row (default: TF Avancé, propulsion row).</summary>
        public string Tab = Parts.AdvancedTab.Name;
        public int Row = Parts.AdvancedTab.PropulsionRow;

        public MelonPreferences_Entry<float> ThrustSetting;
        public MelonPreferences_Entry<float> FuelSetting;

        public float ThrustMultiplier => ThrustSetting != null ? Mathf.Clamp(ThrustSetting.Value, 0f, 200f) : DefaultThrust;
        public float FuelMultiplier => FuelSetting != null ? Mathf.Clamp(FuelSetting.Value, 0.001f, 10f) : DefaultFuel;

        /// <summary>Flow on a propellant line, as a multiple of the Chemical Thruster's: the special propellant or the oxygen beside it.</summary>
        public float FlowOn(uint fluidItemId) => Fuel == 0 || fluidItemId == Fuel ? FuelMultiplier : ReactionMass;
    }

    internal static class EngineCatalog
    {
        // The game's Chemical Thruster (Ships.Physics.Forces.ShipThrust, ShipThrustersUpdater)
        public const float BaseThrust = Ships.Interface.Model.Parts.State.ThrusterState.EthanolXOxygenStrength;
        public const float BaseFlow = Ships.Interface.Model.Parts.State.ThrusterState.MaxFluidConsumptionRate;
        /// <summary>Signal socket of the nuclear and dark matter engines: when wired, it sets their thrust (0-1).</summary>
        public const byte CommandSocket = 4;

        public static readonly EngineSpec[] All =
        {
            new EngineSpec
            {
                Id = 7311, Key = "Vector", Label = Loc.Pick("Propulseur vectoriel", "Vector thruster", "Propulsor vectorial"),
                Description = Loc.Pick("Petit propulseur de manœuvre. Sa poussée s'applique là où il est monté : décentré, il fait aussi tourner le vaisseau (couple réel). 1 950 N, consommation proportionnelle. Ergols et commande comme le propulseur chimique (vannes).", "Small manoeuvring thruster. Its thrust acts where it is mounted: off centre, it also turns the ship (real torque). 1,950 N, use in proportion. Propellants and control as the Chemical Thruster (valves).", "Pequeño propulsor de maniobra. Su empuje actúa donde está montado: descentrado, también gira la nave (par real). 1.950 N, consumo proporcional. Propergoles y control como el Propulsor químico (válvulas)."),
                DefaultThrust = 0.25f, DefaultFuel = 0.25f, Mass = 6f, Scale = 0.55f, OffCenterThrust = true,
                Tint = new Color(1f, 0.82f, 0.62f), Cost = new[] { (101u, 2), (112u, 1) }
            },
            new EngineSpec
            {
                Id = 7312, Key = "Heavy", Label = Loc.Pick("Moteur lourd", "Heavy engine", "Motor pesado"),
                Description = Loc.Pick("Moteur principal pour gros vaisseaux et charges lourdes : 23 400 N, trois fois le propulseur chimique, un peu moins efficace (+10 % d'ergols par newton). Met 1,5 s à atteindre sa pleine poussée.", "Main engine for big ships and heavy loads: 23,400 N, three Chemical Thrusters, a little less efficient (+10% propellant per newton). Takes 1.5 s to reach full thrust.", "Motor principal para naves grandes y cargas pesadas: 23.400 N, tres Propulsores químicos, algo menos eficiente (+10 % de propergol por newton). Tarda 1,5 s en alcanzar el empuje máximo."),
                DefaultThrust = 3f, DefaultFuel = 3.3f, Mass = 75f, Scale = 1.5f, SpoolSeconds = 1.5f,
                Tint = new Color(0.62f, 0.64f, 0.68f), Cost = new[] { (101u, 12), (112u, 2) }
            },
            new EngineSpec
            {
                Id = 7313, Key = "Precision", Label = Loc.Pick("Moteur de précision", "Precision engine", "Motor de precisión"),
                Description = Loc.Pick("Très faible poussée (624 N) pour les petites corrections de trajectoire et d'orientation. Décentré, il produit un couple réel. Réponse immédiate.", "Very low thrust (624 N) for small course and attitude corrections. Off centre, it gives a real torque. Immediate response.", "Empuje muy bajo (624 N) para pequeñas correcciones de rumbo y actitud. Descentrado, da un par real. Respuesta inmediata."),
                DefaultThrust = 0.08f, DefaultFuel = 0.08f, Mass = 4f, Scale = 0.4f, OffCenterThrust = true,
                Tint = new Color(0.7f, 0.86f, 1f), Cost = new[] { (101u, 1), (112u, 1) }
            },
            new EngineSpec
            {
                Id = 7314, Key = "Efficient", Label = Loc.Pick("Moteur économe", "Efficient engine", "Motor eficiente"),
                Description = Loc.Pick("Privilégie l'autonomie : 4 680 N pour seulement 35 % des ergols du propulseur chimique (environ 1,7 fois plus de poussée par unité). Montée en régime lente : 4 s.", "Built for range: 4,680 N for only 35% of the Chemical Thruster's propellant (about 1.7 times more thrust per unit). Slow spool-up: 4 s.", "Hecho para la autonomía: 4.680 N con solo el 35 % del propergol del Propulsor químico (unas 1,7 veces más empuje por unidad). Arranque lento: 4 s."),
                DefaultThrust = 0.6f, DefaultFuel = 0.35f, Mass = 28f, Scale = 1.1f, SpoolSeconds = 4f,
                Tint = new Color(0.72f, 0.92f, 0.74f), Cost = new[] { (101u, 6), (112u, 3) }
            },
            new EngineSpec
            {
                Id = 7315, Key = "Exotic", Label = Loc.Pick("Moteur à matière exotique", "Exotic matter engine", "Motor de materia exótica"),
                Description = Loc.Pick("La matière exotique ramassée près des trous noirs, brûlée dans un champ quantique : 78 000 N (dix propulseurs chimiques) pour à peine 10 % de leurs ergols. Montée en régime : 3 s. Ergols et commande comme le propulseur chimique (vannes).", "Exotic matter gathered near black holes, burnt in a quantum field: 78,000 N (ten Chemical Thrusters) for barely 10% of their propellant. Spool-up: 3 s. Propellants and control as the Chemical Thruster (valves).", "Materia exótica recogida cerca de los agujeros negros, quemada en un campo cuántico: 78.000 N (diez Propulsores químicos) con apenas el 10 % de su propergol. Arranque: 3 s. Propergoles y control como el Propulsor químico (válvulas)."),
                DefaultThrust = 10f, DefaultFuel = 0.1f, Mass = 140f, Scale = 1.9f, SpoolSeconds = 3f,
                Tint = Color.white, Model = EngineModels.Exotic, ModelGlow = new Color(1f, 0.3f, 0.75f), ModelCore = new Color(1f, 0.5f, 0.9f), Flame = new Color(1f, 0.6f, 0.95f),
                Cost = new[] { (Items.ModItems.ExoticMatter, 3), (Items.ModItems.QuantumCore, 1), (Items.ModItems.Titanium, 12), (Items.ModItems.NexoranAlloy, 6) },
                Tab = Parts.AdvancedTab.ArtifactsName, Row = Parts.AdvancedTab.ArtifactsPropulsionRow
            },
            new EngineSpec
            {
                Id = 7316, Key = "NuclearCompact", Label = Loc.Pick("Propulseur nucléaire", "Nuclear thruster", "Propulsor nuclear"),
                Description = Loc.Pick("Un petit réacteur chauffe de l'oxygène et le rejette par une tuyère refroidie : 93 600 N (douze propulseurs chimiques) pour 0,002 unité/s de combustible nucléaire et 0,02 d'oxygène. Une prise de propergol reçoit le combustible nucléaire, l'autre l'oxygène. Commande : un signal sur sa prise de commande (0-1), sinon une vanne sur sa conduite, sinon le siège de pilotage. Montée en régime : 2 s.", "A small reactor heats oxygen and throws it out through a cooled nozzle: 93,600 N (twelve Chemical Thrusters) for 0.002 unit/s of nuclear fuel and 0.02 of oxygen. One propellant socket takes nuclear fuel, the other oxygen. Control: a signal on its command socket (0-1), else a valve on its line, else the pilot seat. Spool-up: 2 s.", "Un pequeño reactor calienta oxígeno y lo expulsa por una tobera refrigerada: 93.600 N (doce Propulsores químicos) por 0,002 unidad/s de combustible nuclear y 0,02 de oxígeno. Una toma de propergol recibe combustible nuclear, la otra oxígeno. Control: una señal en su toma de mando (0-1), si no una válvula en su línea, si no el asiento del piloto. Arranque: 2 s."),
                DefaultThrust = 12f, DefaultFuel = 0.04f, ReactionMass = 0.4f, Fuel = Items.ModItems.NuclearFuel, Mass = 110f, Scale = 1.3f, SpoolSeconds = 2f,
                Tint = Color.white, Model = EngineModels.Nuclear, ModelGlow = Power.MachineModels.Green, ModelCore = new Color(1f, 0.55f, 0.15f), Flame = new Color(0.75f, 1f, 0.85f),
                Cost = new[] { (Items.ModItems.Titanium, 15), (Items.ModItems.Uranium, 4), (Items.ModItems.NexoranAlloy, 4), (112u, 10) },
                Row = Parts.AdvancedTab.NuclearRow
            },
            new EngineSpec
            {
                Id = 7317, Key = "NuclearHeavy", Label = Loc.Pick("Moteur nucléaire lourd", "Heavy nuclear engine", "Motor nuclear pesado"),
                Description = Loc.Pick("Moteur nucléaire thermique de grande taille, cœur à quatre turbopompes et radiateurs : 234 000 N (trente propulseurs chimiques) pour 0,005 unité/s de combustible nucléaire et 0,05 d'oxygène. Une prise de propergol reçoit le combustible nucléaire, l'autre l'oxygène. Commande : un signal sur sa prise de commande (0-1), sinon une vanne sur sa conduite, sinon le siège de pilotage. Montée en régime : 4 s.", "A large nuclear thermal engine, core with four turbopumps and radiators: 234,000 N (thirty Chemical Thrusters) for 0.005 unit/s of nuclear fuel and 0.05 of oxygen. One propellant socket takes nuclear fuel, the other oxygen. Control: a signal on its command socket (0-1), else a valve on its line, else the pilot seat. Spool-up: 4 s.", "Un gran motor térmico nuclear, núcleo con cuatro turbobombas y radiadores: 234.000 N (treinta Propulsores químicos) por 0,005 unidad/s de combustible nuclear y 0,05 de oxígeno. Una toma de propergol recibe combustible nuclear, la otra oxígeno. Control: una señal en su toma de mando (0-1), si no una válvula en su línea, si no el asiento del piloto. Arranque: 4 s."),
                DefaultThrust = 30f, DefaultFuel = 0.1f, ReactionMass = 1f, Fuel = Items.ModItems.NuclearFuel, Mass = 320f, Scale = 2.2f, SpoolSeconds = 4f,
                Tint = Color.white, Model = EngineModels.NuclearHeavy, ModelGlow = Power.MachineModels.Green, ModelCore = new Color(1f, 0.55f, 0.15f), Flame = new Color(0.75f, 1f, 0.85f),
                Cost = new[] { (Items.ModItems.Titanium, 40), (Items.ModItems.Uranium, 10), (Items.ModItems.NexoranAlloy, 10), (Items.ModItems.QuantumCore, 1), (112u, 20) },
                Row = Parts.AdvancedTab.NuclearRow
            },
            new EngineSpec
            {
                Id = 7318, Key = "DarkMatter", Label = Loc.Pick("Moteur à matière noire", "Dark matter engine", "Motor de materia oscura"),
                Description = Loc.Pick("Le moteur le plus puissant : la matière noire s'annihile dans une chambre de confinement et une tuyère magnétique en anneaux dirige le jet. 624 000 N (quatre-vingts propulseurs chimiques) pour 0,002 unité/s de matière noire et 0,03 d'oxygène. Une prise de propergol reçoit la matière noire, l'autre l'oxygène. Commande : un signal sur sa prise de commande (0-1), sinon une vanne sur sa conduite, sinon le siège de pilotage. Montée en régime : 3 s.", "The most powerful engine: dark matter annihilates in a containment chamber and a magnetic nozzle of rings steers the jet. 624,000 N (eighty Chemical Thrusters) for 0.002 unit/s of dark matter and 0.03 of oxygen. One propellant socket takes dark matter, the other oxygen. Control: a signal on its command socket (0-1), else a valve on its line, else the pilot seat. Spool-up: 3 s.", "El motor más potente: la materia oscura se aniquila en una cámara de confinamiento y una tobera magnética de anillos dirige el chorro. 624.000 N (ochenta Propulsores químicos) por 0,002 unidad/s de materia oscura y 0,03 de oxígeno. Una toma de propergol recibe materia oscura, la otra oxígeno. Control: una señal en su toma de mando (0-1), si no una válvula en su línea, si no el asiento del piloto. Arranque: 3 s."),
                DefaultThrust = 80f, DefaultFuel = 0.04f, ReactionMass = 0.6f, Fuel = Items.ModItems.DarkMatter, Mass = 260f, Scale = 2f, SpoolSeconds = 3f,
                Tint = Color.white, Model = EngineModels.DarkMatter, ModelGlow = Power.MachineModels.Violet, ModelCore = new Color(1f, 0.65f, 1f), Flame = new Color(0.8f, 0.55f, 1f),
                Cost = new[] { (Items.ModItems.Meteorite, 30), (Items.ModItems.QuantumCore, 3), (Items.ModItems.ExoticMatter, 4), (Items.ModItems.NexoranAlloy, 15), (Items.ModItems.Titanium, 25) },
                Row = Parts.AdvancedTab.SuperiorRow
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
                    description: spec.Fuel == 0
                        ? $"{spec.Label}: oxygen and ethanol use at full thrust, as a multiple of the Chemical Thruster's {BaseFlow} unit/s each."
                        : $"{spec.Label}: {(spec.Fuel == Items.ModItems.DarkMatter ? "dark matter" : "nuclear fuel")} use at full thrust, as a multiple of the Chemical Thruster's {BaseFlow} unit/s.");
            }
        }
    }
}

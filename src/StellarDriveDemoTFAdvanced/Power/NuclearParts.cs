using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Settings;
using StellarDriveDemoTFAdvanced.Items;
using StellarDriveDemoTFAdvanced.Parts;
using UnityEngine;
using StellarDriveDemoTFAdvanced.Common;

namespace StellarDriveDemoTFAdvanced.Power
{
    /// <summary>
    /// The nuclear machines: the enrichment centrifuge (uranium ore to nuclear fuel), the nuclear
    /// fuel tank and the nuclear reactor. Same style as the other machines, in green. Only call
    /// when SDModKit is loaded.
    /// </summary>
    internal static class NuclearParts
    {
        private const uint Iron = 101, Glass = 102, Copper = 112;

        public static void Register()
        {
            PartFactory.Register(PowerCatalog.CentrifugeId, "EthanolDistiller", AdvancedTab.Name, AdvancedTab.NuclearRow, ConfigureCentrifuge);
            PartFactory.Register(PowerCatalog.NuclearTankId, "FluidTank", AdvancedTab.Name, AdvancedTab.NuclearRow, ConfigureTank);
            PartFactory.Register(PowerCatalog.NuclearReactorId, "SignalDisplay", AdvancedTab.Name, AdvancedTab.NuclearRow, ConfigureReactor);
            AdvancedMod.Log.Msg("registered the enrichment centrifuge, the nuclear fuel tank and the nuclear reactor");
        }

        /// <summary>Hides a donor's own model (its plugs stay), so a new one can be built in its place.</summary>
        internal static void HideDonorModel(Transform visual)
        {
            foreach (MeshRenderer renderer in visual.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.GetComponentInParent<SocketObject>() == null && !renderer.name.StartsWith("TF"))
                    renderer.enabled = false;
            }
        }

        private static void ConfigureCentrifuge(PartSettings settings, GameObject prefab)
        {
            float oreRate = PowerCatalog.CentrifugeOrePerSecond.Value;
            PartFactory.Describe(settings, Loc.Pick("Centrifugeuse d'enrichissement", "Enrichment centrifuge", "Centrifugadora de enriquecimiento"),
                Loc.Pick($"Enrichit le minerai d'uranium placé dans sa trappe (à la main ou par une ligne d'objets) en combustible nucléaire, envoyé dans la conduite de sa prise de fluide (vide ou déjà de combustible) : {oreRate:0.###} minerai/s, {PowerCatalog.CentrifugeFuelPerOre.Value:0.##} unité de combustible par minerai. Consomme {PowerCatalog.CentrifugeWatts.Value:0} W. Branchement comme le distillateur d'éthanol du jeu.", $"Enriches the uranium ore put in its hatch (by hand or by an item line) into nuclear fuel, sent into the line on its fluid socket (empty or already nuclear fuel): {oreRate:0.###} ore/s, {PowerCatalog.CentrifugeFuelPerOre.Value:0.##} unit of fuel per ore. Uses {PowerCatalog.CentrifugeWatts.Value:0} W. Wired like the game's ethanol distiller.", $"Enriquece el mineral de uranio puesto en su trampilla (a mano o por una línea de objetos) en combustible nuclear, enviado a la línea de su toma de fluido (vacía o ya con combustible nuclear): {oreRate:0.###} mineral/s, {PowerCatalog.CentrifugeFuelPerOre.Value:0.##} unidad de combustible por mineral. Consume {PowerCatalog.CentrifugeWatts.Value:0} W. Se conecta como el destilador de etanol del juego."),
                180f, new[] { (ModItems.Titanium, 18), ((uint)Iron, 8), ((uint)Copper, 12), ((uint)Glass, 6), (ModItems.NexoranAlloy, 2) });
            Transform visual = prefab.transform.Find("Visual");
            if (visual == null)
                return;
            HideDonorModel(visual);
            var model = new MachineModels.Model();
            // Plinth and deck
            model.Armor.Box(new Vector3(0f, 0.05f, 0f), new Vector3(0.9f, 0.1f, 0.9f));
            model.Dark.Box(new Vector3(0f, 0.105f, 0f), new Vector3(0.86f, 0.012f, 0.86f));
            model.Light.Box(new Vector3(0f, 0.1f, -0.452f), new Vector3(0.86f, 0.012f, 0.01f));
            // The cascade: three tall rotors at the back, each in a ribbed casing with a glowing band
            const float rotorZ = 0.17f, rotorTop = 1.12f;
            foreach (float x in new[] { -0.27f, 0f, 0.27f })
            {
                var foot = new Vector3(x, 0.11f, rotorZ);
                model.Armor.Cylinder(foot, foot + Vector3.up * 0.08f, 0.13f, 0.11f, 12);
                model.Armor.Cylinder(foot + Vector3.up * 0.08f, new Vector3(x, rotorTop, rotorZ), 0.095f, 0.095f, 16, capA: false, capB: false);
                model.Dark.Cylinder(new Vector3(x, rotorTop, rotorZ), new Vector3(x, rotorTop + 0.04f, rotorZ), 0.1f, 0.06f, 16);
                for (int i = 0; i < 6; i++)
                {
                    float y = 0.3f + i * 0.14f;
                    model.Light.Cylinder(new Vector3(x, y, rotorZ), new Vector3(x, y + 0.012f, rotorZ), 0.1f, 0.1f, 16, capA: false, capB: false);
                }
                model.Glow.Cylinder(new Vector3(x, 0.62f, rotorZ), new Vector3(x, 0.7f, rotorZ), 0.098f, 0.098f, 16, capA: false, capB: false);
                // Feed pipe from the header down into the rotor's top
                model.Dark.Cylinder(new Vector3(x, rotorTop + 0.04f, rotorZ), new Vector3(x, rotorTop + 0.08f, rotorZ), 0.025f, 0.025f, 8);
            }
            // Header pipe across the rotors' tops, down to the front hopper
            model.Dark.Cylinder(new Vector3(-0.33f, rotorTop + 0.08f, rotorZ), new Vector3(0.33f, rotorTop + 0.08f, rotorZ), 0.03f, 0.03f, 10);
            model.Dark.Cylinder(new Vector3(0.33f, rotorTop + 0.08f, rotorZ), new Vector3(0.36f, rotorTop + 0.08f, -0.05f), 0.03f, 0.03f, 10);
            model.Dark.Cylinder(new Vector3(0.36f, rotorTop + 0.08f, -0.05f), new Vector3(0.36f, 0.62f, -0.05f), 0.03f, 0.03f, 10);
            // Front: the control housing, its vents and the ore hopper (the hatch the player opens)
            const float faceZ = -0.42f;
            model.Armor.Box(new Vector3(0f, 0.38f, -0.22f), new Vector3(0.82f, 0.56f, 0.4f));
            model.Light.Box(new Vector3(0f, 0.665f, faceZ + 0.004f), new Vector3(0.8f, 0.012f, 0.012f));
            MachineModels.Vent(model, -0.24f, 0.32f, faceZ, 0.22f, 0.22f, 3);
            MachineModels.Vent(model, 0.24f, 0.32f, faceZ, 0.22f, 0.22f, 3);
            MachineModels.Grille(model, 0.32f, faceZ, 0.1f, 0.24f);
            // Hopper: a slanted funnel on top of the housing, open to the front
            model.Armor.Block(new[]
            {
                new Vector3(-0.3f, 0.66f, -0.4f), new Vector3(0.3f, 0.66f, -0.4f), new Vector3(-0.38f, 1.02f, -0.44f), new Vector3(0.38f, 1.02f, -0.44f),
                new Vector3(-0.3f, 0.66f, -0.06f), new Vector3(0.3f, 0.66f, -0.06f), new Vector3(-0.38f, 0.96f, -0.04f), new Vector3(0.38f, 0.96f, -0.04f)
            });
            model.Dark.Box(new Vector3(0f, 0.86f, -0.43f), new Vector3(0.6f, 0.26f, 0.02f), Quaternion.Euler(-6f, 0f, 0f));
            model.Glow.Box(new Vector3(0f, 1.0f, -0.445f), new Vector3(0.66f, 0.02f, 0.012f));
            // Status lamps and a radiation warning band on the housing's sides
            foreach (float x in new[] { -0.412f, 0.412f })
            {
                model.Glow.Box(new Vector3(x, 0.52f, -0.22f), new Vector3(0.008f, 0.04f, 0.3f));
                model.Core.Box(new Vector3(x, 0.2f, -0.32f), new Vector3(0.01f, 0.05f, 0.05f));
            }
            MachineModels.Attach(visual, model, "Centrifuge", MachineModels.Green, MachineModels.Green);
        }

        private static void ConfigureTank(PartSettings settings, GameObject prefab)
        {
            PartFactory.Describe(settings, Loc.Pick("Réservoir de combustible nucléaire", "Nuclear fuel tank", "Tanque de combustible nuclear"),
                Loc.Pick("Le réservoir de fluide du jeu, blindé de plomb et de titane pour le combustible nucléaire (il prend aussi les autres fluides). Branchement et prise signal comme le réservoir du jeu.", "The game's fluid tank, shielded with lead and titanium for nuclear fuel (it takes the other fluids too). Wiring and signal socket as the game's tank.", "El tanque de fluido del juego, blindado con plomo y titanio para combustible nuclear (también acepta los otros fluidos). Conexiones y toma de señal como el tanque del juego."),
                40f, new[] { (ModItems.Titanium, 4), ((uint)Iron, 8), ((uint)Glass, 4) });
            SuperiorParts.Recolor(prefab, new Color(0.5f, 0.62f, 0.42f), new Color(0.05f, 0.25f, 0.02f));
            TankModels.Armor(prefab, TankModels.Kind.Nuclear);
        }

        private static void ConfigureReactor(PartSettings settings, GameObject prefab)
        {
            PartFactory.Describe(settings, Loc.Pick("Réacteur nucléaire", "Nuclear reactor", "Reactor nuclear"),
                Loc.Pick($"Une cuve sous pression où le combustible nucléaire chauffe l'eau d'un circuit de turbines : brûle {PowerCatalog.NuclearReactorBurnPerSecond.Value:0.###} unité/s de combustible nucléaire prise sur sa prise de fluide et donne {PowerCatalog.NuclearReactorWatts.Value:0} W aux réseaux branchés sur ses deux prises électriques (partagés s'il y en a deux). Sa prise signal vaut 100 quand il tourne, 0 à sec.", $"A pressure vessel where nuclear fuel heats the water of a turbine loop: burns {PowerCatalog.NuclearReactorBurnPerSecond.Value:0.###} unit/s of nuclear fuel from its fluid socket and gives {PowerCatalog.NuclearReactorWatts.Value:0} W to the networks on its two power sockets (shared if there are two). Its signal socket reads 100 while it runs, 0 when dry.", $"Una vasija a presión donde el combustible nuclear calienta el agua de un circuito de turbina: quema {PowerCatalog.NuclearReactorBurnPerSecond.Value:0.###} unidad/s de combustible nuclear de su toma de fluido y da {PowerCatalog.NuclearReactorWatts.Value:0} W a las redes de sus dos tomas de energía (repartidos si hay dos). Su toma de señal da 100 mientras funciona, 0 cuando está seco."),
                320f, new[] { (ModItems.Titanium, 20), ((uint)Iron, 30), ((uint)Copper, 20), (ModItems.QuantumCore, 1), (ModItems.NexoranAlloy, 4) });
            settings.internalStateClass = PowerCatalog.MachineStateClass;
            const float width = 1.2f, depth = 1.2f;
            Transform visuals = PartFactory.BlankFromSignalDisplay(prefab, new Vector3(0f, 0.75f, 0f), new Vector3(width, 1.5f, depth));
            var model = new MachineModels.Model();
            // Base housing with vents, as the other machines
            model.Armor.Cylinder(Vector3.zero, new Vector3(0f, 0.1f, 0f), width * 0.58f, width * 0.56f, 8);
            MachineModels.Housing(model, width * 0.92f, 0.45f, depth * 0.92f, 0.85f);
            float face = depth * 0.92f * 0.47f;
            MachineModels.Vent(model, -0.3f, 0.24f, face, 0.24f, 0.22f, 4);
            MachineModels.Vent(model, 0.3f, 0.24f, face, 0.24f, 0.22f, 4);
            MachineModels.Grille(model, 0.24f, face, 0.12f, 0.24f);
            // The pressure vessel: a ribbed cylinder with a domed head
            const float vesselR = 0.3f, vesselLow = 0.4f, vesselHigh = 1.08f;
            model.Armor.Cylinder(new Vector3(0f, vesselLow, 0f), new Vector3(0f, vesselLow + 0.06f, 0f), vesselR + 0.06f, vesselR + 0.03f, 16);
            model.Armor.Cylinder(new Vector3(0f, vesselLow + 0.06f, 0f), new Vector3(0f, vesselHigh, 0f), vesselR, vesselR, 20, capA: false, capB: false);
            foreach (float y in new[] { 0.58f, 0.76f, 0.94f })
                model.Light.Cylinder(new Vector3(0f, y, 0f), new Vector3(0f, y + 0.014f, 0f), vesselR + 0.008f, vesselR + 0.008f, 20, capA: false, capB: false);
            model.Armor.Cylinder(new Vector3(0f, vesselHigh, 0f), new Vector3(0f, vesselHigh + 0.12f, 0f), vesselR + 0.02f, vesselR * 0.72f, 20, capA: false, capB: false);
            model.Armor.Cylinder(new Vector3(0f, vesselHigh + 0.12f, 0f), new Vector3(0f, vesselHigh + 0.2f, 0f), vesselR * 0.72f, vesselR * 0.3f, 20, capA: false, capB: true);
            // Viewing slits on the vessel, lit by the core's blue-green glow
            for (int i = 0; i < 6; i++)
            {
                float a = (30f + i * 60f) * Mathf.Deg2Rad;
                var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                model.Glow.Box(outward * (vesselR + 0.004f) + Vector3.up * 0.67f, new Vector3(0.03f, 0.24f, 0.006f), Quaternion.LookRotation(outward));
            }
            // Control rod drives on the head, with lit tips
            for (int i = 0; i < 5; i++)
            {
                Vector3 p = i == 0 ? Vector3.zero : Quaternion.Euler(0f, 45f + i * 90f, 0f) * new Vector3(0.11f, 0f, 0f);
                float baseY = vesselHigh + (i == 0 ? 0.2f : 0.15f);
                model.Dark.Cylinder(p + Vector3.up * (baseY - 0.02f), p + Vector3.up * (baseY + 0.16f), 0.025f, 0.025f, 8);
                model.Core.Cylinder(p + Vector3.up * (baseY + 0.16f), p + Vector3.up * (baseY + 0.19f), 0.03f, 0.02f, 8);
            }
            // Coolant loops: four pipes leave the vessel, bend down to the steam boxes on the housing
            for (int i = 0; i < 4; i++)
            {
                float a = (45f + i * 90f) * Mathf.Deg2Rad;
                var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 start = outward * vesselR + Vector3.up * 0.98f;
                Vector3 bend = outward * 0.46f + Vector3.up * 0.98f;
                Vector3 foot = outward * 0.46f + Vector3.up * 0.47f;
                model.Light.Cylinder(start, bend, 0.035f, 0.035f, 10);
                model.Light.Cylinder(bend, foot, 0.035f, 0.035f, 10);
                model.Dark.Cylinder(bend - Vector3.up * 0.03f, bend + Vector3.up * 0.03f, 0.05f, 0.05f, 10);
                model.Armor.Box(foot + Vector3.up * 0.03f, new Vector3(0.14f, 0.1f, 0.14f), Quaternion.LookRotation(outward));
            }
            // Core: a green-lit band at the vessel's foot
            model.Core.Cylinder(new Vector3(0f, vesselLow + 0.065f, 0f), new Vector3(0f, vesselLow + 0.09f, 0f), vesselR + 0.01f, vesselR + 0.01f, 20, capA: false, capB: false);
            MachineModels.Attach(visuals, model, "NuclearReactor", MachineModels.Green, new Color(0.3f, 0.9f, 1f));
            SuperiorParts.Sockets(prefab, width * 0.92f * 0.47f, face, 0.24f);
        }
    }
}

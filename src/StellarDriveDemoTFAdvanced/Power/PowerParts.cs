using System.Linq;
using SDModKit.Content;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Settings;
using Ships.Parts.Common;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Parts.StateTypes;
using StellarDriveDemoTFAdvanced.Common;
using StellarDriveDemoTFAdvanced.Parts;
using UnityEngine;
using UnityEngine.Rendering;

namespace StellarDriveDemoTFAdvanced.Power
{
    /// <summary>
    /// Registers the battery (the game's decorative Battery Bank, made functional) and the circuit
    /// breaker. Both get the game's FluidTankState as their state (see <see cref="PowerCatalog"/>).
    /// Only call when SDModKit is loaded.
    /// </summary>
    internal static class PowerParts
    {
        private const string StateClass = "FluidTankState";

        public static void Register()
        {
            PartFactory.Register(PowerCatalog.BatteryId, "SignalDisplay", AdvancedTab.PropulsionRow, ConfigureBattery);
            PartFactory.Register(PowerCatalog.BreakerId, "SignalDisplay", AdvancedTab.PropulsionRow, ConfigureBreaker);
            PartFactory.Register(PowerCatalog.ReactorId, "SignalDisplay", AdvancedTab.ArtifactsName, AdvancedTab.ArtifactsPowerRow, ConfigureReactor);
            PartFactory.Register(PowerCatalog.CryoBatteryId, "SignalDisplay", AdvancedTab.ArtifactsName, AdvancedTab.ArtifactsPowerRow, ConfigureCryoBattery);
            foreach (ForceField.Tier shield in ForceField.Tiers)
            {
                ForceField.Tier tier = shield;
                PartFactory.Register(tier.Id, "SignalDisplay", AdvancedTab.Name, AdvancedTab.ShieldRow, (settings, prefab) => ConfigureShield(tier, settings, prefab));
            }
            PartFactory.Register(PowerCatalog.CondenserId, "SignalDisplay", AdvancedTab.ArtifactsName, AdvancedTab.ArtifactsPowerRow, ConfigureCondenser);
            PartFactory.Register(PowerCatalog.QuantumGyroId, "Gyroscope", AdvancedTab.ArtifactsName, AdvancedTab.ArtifactsPowerRow, ConfigureQuantumGyro);
            AdvancedMod.Log.Msg("registered the battery, the circuit breaker, the quantum reactor, the cryonite battery and the force field generators");
        }

        private static void ConfigureBattery(PartSettings settings, GameObject prefab)
        {
            PartFactory.Describe(settings, Loc.Pick("Batterie", "Battery"),
                Loc.Pick($"Stocke l'énergie du réseau électrique branché sur ses prises. Se recharge avec la puissance que les générateurs ont en réserve (ils brûlent alors plus de bois) et la rend quand le réseau en manque. {PowerCatalog.BatteryCapacityKJ.Value:0} kJ, charge {PowerCatalog.BatteryChargeWatts.Value:0} W, décharge {PowerCatalog.BatteryDischargeWatts.Value:0} W, rendement {PowerCatalog.BatteryEfficiency.Value:P0}. Sa prise signal donne la charge en %.", $"Stores the energy of the power network on its sockets. Charges from the power the generators have to spare (they then burn more wood) and gives it back when the network runs short. {PowerCatalog.BatteryCapacityKJ.Value:0} kJ, charge {PowerCatalog.BatteryChargeWatts.Value:0} W, discharge {PowerCatalog.BatteryDischargeWatts.Value:0} W, efficiency {PowerCatalog.BatteryEfficiency.Value:P0}. Its signal socket gives the charge in %."),
                100f, new[] { (101u, 6), (112u, 6), (104u, 2) });
            settings.internalStateClass = StateClass;
            BuildCell(prefab, "Battery", 0.9f, 0.8f, 0.7f, MachineModels.Red, false);
        }

        // A battery-like cell: housing, two vents and the grille on the front, power on both sides, charge signal on the back
        private static void BuildCell(GameObject prefab, string name, float width, float height, float depth, Color glow, bool crystal)
        {
            Transform visuals = PartFactory.BlankFromSignalDisplay(prefab, new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, depth));
            var model = new MachineModels.Model();
            MachineModels.Housing(model, width, height, depth, 0.6f);
            float face = depth * 0.47f;
            float ventY = height * 0.38f;
            MachineModels.Vent(model, -width * 0.26f, ventY, face, width * 0.26f, height * 0.42f, 4);
            MachineModels.Vent(model, width * 0.26f, ventY, face, width * 0.26f, height * 0.42f, 4);
            MachineModels.Grille(model, ventY, face, width * 0.12f, height * 0.46f);
            if (crystal)
            {
                // A cryonite crystal behind a window on top
                model.Dark.Box(new Vector3(0f, height + 0.01f, 0f), new Vector3(width * 0.3f, 0.02f, depth * 0.25f));
                model.Core.Cylinder(new Vector3(0f, height, 0f), new Vector3(0f, height + 0.16f, 0f), 0.07f, 0f, 6);
                model.Core.Cylinder(new Vector3(0.07f, height, 0.02f), new Vector3(0.1f, height + 0.1f, 0.03f), 0.045f, 0f, 6);
            }
            MachineModels.Attach(visuals, model, name, glow, glow);
            var bounds = prefab.GetComponent<ShipPartBounds>();
            GraftRow(prefab, bounds, height * 0.35f, -depth * 0.47f, 0.22f,
                (PowerCatalog.BatteryPowerA, CableType.Power), (PowerCatalog.StatusSignal, CableType.Signal), (PowerCatalog.BatteryPowerB, CableType.Power));
        }

        private static void ConfigureReactor(PartSettings settings, GameObject prefab)
        {
            PartFactory.Describe(settings, Loc.Pick("Réacteur quantique", "Quantum reactor"),
                Loc.Pick($"Un noyau quantique tenu dans une cage d'alliage nexorien : fournit {PowerCatalog.ReactorWatts.Value:0} W en continu au réseau branché sur ses prises, sans carburant et sans fin (trois générateurs à bois). Sa prise signal vaut 100 quand il alimente un réseau. Fabriqué avec les trouvailles rares de la cité abandonnée de TF Nexora et des ruines.", $"A quantum core held in a Nexoran alloy cage: gives {PowerCatalog.ReactorWatts.Value:0} W, steadily, to the network on its sockets, with no fuel and no end (three wood generators). Its signal socket reads 100 while it powers a network. Built with the rare finds of the abandoned city of TF Nexora and of the ruins."),
                180f, new[] { (Items.ModItems.QuantumCore, 2), (Items.ModItems.NexoranAlloy, 6), (Items.ModItems.Titanium, 8), (Items.ModItems.Artifact, 2), (112u, 10) });
            settings.internalStateClass = StateClass;
            const float width = 1f, height = 1.05f, depth = 1f;
            Transform visuals = PartFactory.BlankFromSignalDisplay(prefab, new Vector3(0f, 0.68f, 0f), new Vector3(width, 1.36f, depth));
            var model = new MachineModels.Model();
            MachineModels.Housing(model, width, height, depth, 0.55f);
            float face = depth * 0.47f;
            // Two red vent stacks around the black grille, on the front and the back
            foreach (float side in new[] { 1f, -1f })
            {
                float z = face * side;
                MachineModels.Vent(model, -0.28f, 0.42f, z, 0.24f, 0.5f, 5);
                MachineModels.Vent(model, 0.28f, 0.42f, z, 0.24f, 0.5f, 5);
                MachineModels.Grille(model, 0.42f, z, 0.14f, 0.56f);
            }
            // The core in its cage on top
            float top = height;
            model.Core.Cylinder(new Vector3(0f, top, 0f), new Vector3(0f, top + 0.16f, 0f), 0.13f, 0.18f, 10);
            model.Core.Cylinder(new Vector3(0f, top + 0.16f, 0f), new Vector3(0f, top + 0.3f, 0f), 0.18f, 0.06f, 10);
            foreach (float a in new[] { 45f, 135f, 225f, 315f })
                model.Armor.Box(new Vector3(0f, top + 0.15f, 0f) + Quaternion.Euler(0f, a, 0f) * new Vector3(0.23f, 0f, 0f), new Vector3(0.04f, 0.32f, 0.04f));
            model.Armor.Cylinder(new Vector3(0f, top + 0.3f, 0f), new Vector3(0f, top + 0.34f, 0f), 0.27f, 0.27f, 8);
            model.Light.Cylinder(new Vector3(0f, top + 0.005f, 0f), new Vector3(0f, top + 0.02f, 0f), 0.26f, 0.26f, 8, capA: false, capB: false);
            MachineModels.Attach(visuals, model, "Reactor", MachineModels.Red, MachineModels.Violet);
            var bounds = prefab.GetComponent<ShipPartBounds>();
            GraftRow(prefab, bounds, 0.36f, BackOf(bounds), 0.22f,
                (PowerCatalog.BatteryPowerA, CableType.Power), (PowerCatalog.StatusSignal, CableType.Signal), (PowerCatalog.BatteryPowerB, CableType.Power));
        }

        private static void ConfigureCryoBattery(PartSettings settings, GameObject prefab)
        {
            float capacity = PowerCatalog.BatteryCapacityKJ.Value * PowerCatalog.CryoBatteryScale;
            PartFactory.Describe(settings, Loc.Pick("Batterie à cryonite", "Cryonite battery"),
                Loc.Pick($"Les cristaux de cryonite de TF Glacia gardent l'énergie presque sans perte : {capacity:0} kJ (huit batteries), charge {PowerCatalog.BatteryChargeWatts.Value * PowerCatalog.CryoBatteryRateScale:0} W, décharge {PowerCatalog.BatteryDischargeWatts.Value * PowerCatalog.CryoBatteryRateScale:0} W. Branchement et prise signal comme la batterie.", $"The cryonite crystals of TF Glacia hold energy almost without loss: {capacity:0} kJ (eight batteries), charge {PowerCatalog.BatteryChargeWatts.Value * PowerCatalog.CryoBatteryRateScale:0} W, discharge {PowerCatalog.BatteryDischargeWatts.Value * PowerCatalog.CryoBatteryRateScale:0} W. Wiring and signal socket as the battery."),
                160f, new[] { (Items.ModItems.Cryonite, 8), (Items.ModItems.Titanium, 4), (112u, 8), (104u, 4) });
            settings.internalStateClass = StateClass;
            BuildCell(prefab, "CryoBattery", 1.1f, 0.9f, 0.8f, MachineModels.Cyan, true);
        }

        private static void ConfigureCondenser(PartSettings settings, GameObject prefab)
        {
            PartFactory.Describe(settings, Loc.Pick("Condenseur à cryonite", "Cryonite condenser"),
                Loc.Pick($"Des cristaux de cryonite refroidis par l'électricité du vaisseau libèrent de l'oxygène : {PowerCatalog.CondenserOxygenPerSecond.Value:0.###} unité/s dans la conduite branchée sur sa prise de fluide (vide ou déjà d'oxygène), pour {PowerCatalog.CondenserWatts.Value:0} W. Sa prise signal donne sa production en millièmes d'unité/s.", $"Cryonite crystals cooled by the ship's power release oxygen: {PowerCatalog.CondenserOxygenPerSecond.Value:0.###} unit/s into the line on its fluid socket (empty or already oxygen), for {PowerCatalog.CondenserWatts.Value:0} W. Its signal socket gives its output in thousandths of a unit/s."),
                90f, new[] { (Items.ModItems.Cryonite, 6), (Items.ModItems.Titanium, 3), (112u, 6), (102u, 4) });
            settings.internalStateClass = PowerCatalog.MachineStateClass;
            const float width = 0.8f, height = 0.75f, depth = 0.7f;
            Transform visuals = PartFactory.BlankFromSignalDisplay(prefab, new Vector3(0f, 0.55f, 0f), new Vector3(width, 1.1f, depth));
            var model = new MachineModels.Model();
            MachineModels.Housing(model, width, height, depth, 0.75f);
            float face = depth * 0.47f;
            MachineModels.Vent(model, -0.2f, 0.32f, face, 0.2f, 0.34f, 4);
            MachineModels.Vent(model, 0.2f, 0.32f, face, 0.2f, 0.34f, 4);
            // A glass column of cryonite crystals on top
            model.Armor.Cylinder(new Vector3(0f, height, 0f), new Vector3(0f, height + 0.04f, 0f), 0.2f, 0.2f, 8);
            model.Dark.Cylinder(new Vector3(0f, height + 0.04f, 0f), new Vector3(0f, height + 0.3f, 0f), 0.16f, 0.16f, 8, capA: false, capB: false);
            model.Armor.Cylinder(new Vector3(0f, height + 0.3f, 0f), new Vector3(0f, height + 0.34f, 0f), 0.2f, 0.2f, 8);
            foreach (float a in new[] { 0f, 120f, 240f })
            {
                Vector3 at = Quaternion.Euler(0f, a, 0f) * new Vector3(0.06f, 0f, 0f) + new Vector3(0f, height + 0.05f, 0f);
                model.Core.Cylinder(at, at + new Vector3(0f, 0.18f + a / 2400f, 0f), 0.04f, 0f, 6);
            }
            MachineModels.Attach(visuals, model, "Condenser", MachineModels.Cyan, MachineModels.Cyan);
            var bounds = prefab.GetComponent<ShipPartBounds>();
            GraftRow(prefab, bounds, 0.3f, -depth * 0.47f, 0.2f,
                (PowerCatalog.MachinePowerA, CableType.Power), (PowerCatalog.MachineSignal, CableType.Signal), (Condenser.OxygenSocket, CableType.Fluid), (PowerCatalog.MachinePowerB, CableType.Power));
        }

        // The game's gyroscope, its body in violet with a lit quantum ring; torque from QuantumGyroscopePatch
        private static void ConfigureQuantumGyro(PartSettings settings, GameObject prefab)
        {
            PartFactory.Describe(settings, Loc.Pick("Gyroscope quantique", "Quantum gyroscope"),
                Loc.Pick($"Un noyau quantique en rotation dans une cage d'alliage : le couple de {PowerCatalog.QuantumGyroFactor.Value:0} gyroscopes, branché et commandé comme le gyroscope du jeu.", $"A quantum core spinning in an alloy cage: the torque of {PowerCatalog.QuantumGyroFactor.Value:0} gyroscopes, wired and controlled like the game's gyroscope."),
                60f, new[] { (Items.ModItems.QuantumCore, 1), (Items.ModItems.NexoranAlloy, 4), (Items.ModItems.Titanium, 4), (112u, 4) });
            Tint(prefab, "QuantumGyro", new Color(0.7f, 0.55f, 1f), new Color(0.15f, 0.05f, 0.3f));
            var bounds = prefab.GetComponent<ShipPartBounds>();
            Transform visual = prefab.transform.Find("Visual") ?? prefab.transform.Find("Visuals");
            if (visual == null || bounds == null)
                return;
            var model = new MachineModels.Model();
            Vector3 c = bounds.center;
            float r = Mathf.Max(bounds.bounds.x, bounds.bounds.z) * 0.52f;
            model.Core.Cylinder(c + Vector3.down * 0.02f, c + Vector3.up * 0.02f, r, r, 24, capA: false, capB: false);
            model.Light.Cylinder(c + Vector3.up * 0.03f, c + Vector3.up * 0.04f, r * 1.02f, r * 1.02f, 24, capA: false, capB: false);
            MachineModels.Attach(visual, model, "QuantumGyro", MachineModels.Violet, MachineModels.Violet);
        }

        private static void ConfigureShield(ForceField.Tier tier, PartSettings settings, GameObject prefab)
        {
            string fluid = tier.Fluid == Items.ModItems.DarkMatter ? Loc.Pick("de matière noire", "of dark matter") : tier.Fluid == Items.ModItems.NuclearFuel ? Loc.Pick("de combustible nucléaire", "of nuclear fuel") : Loc.Pick("d'oxygène", "of oxygen");
            float cooldown = tier.Cooldown;
            PartFactory.Describe(settings, tier.Label,
                Loc.Pick($"{tier.Flavor} Enveloppe tout le vaisseau (et ce qui y est amarré) dans un champ qui encaisse les chocs, les forces de marée et les armes à la place de la coque : {tier.Capacity:0} points (un choc compte sa variation de vitesse en m/s), {tier.Regen:0.#} points/s de recharge. Il lui faut {tier.Watts:0} W sur ses prises électriques (le réseau doit pouvoir les fournir en entier) et {tier.FluidPerSecond:0.###} unité/s {fluid} sur sa prise de fluide. À zéro, il se casse et redémarre rechargé au bout de {(cooldown >= 120f ? $"{cooldown / 60f:0} min" : $"{cooldown:0} s")}. Sa prise signal donne la charge en % ; éteint : -1 pas de réseau électrique, -2 pas de {fluid.Substring(fluid.IndexOf(' ') + 1)}, -3 pas assez de courant (il réessaie toutes les 5 s) ; -10 et moins : secondes avant redémarrage. Les anneaux clignotent en orange ou en rouge quand il lui manque du fluide ou du courant.", $"{tier.Flavor} Wraps the whole ship (and what is docked to it) in a field that takes impacts, tidal forces and weapons instead of the hull: {tier.Capacity:0} points (an impact counts its change of speed in m/s), {tier.Regen:0.#} points/s of recharge. It needs {tier.Watts:0} W on its power sockets (the grid must be able to supply all of it) and {tier.FluidPerSecond:0.###} unit/s {fluid} on its fluid socket. At zero, it breaks and restarts recharged after {(cooldown >= 120f ? $"{cooldown / 60f:0} min" : $"{cooldown:0} s")}. Its signal socket gives the charge in %; off: -1 no power grid, -2 no {fluid.Substring(fluid.IndexOf(' ') + 1)}, -3 not enough power (it retries every 5 s); -10 and below: seconds before restart. The rings blink orange or red when it lacks fluid or power."),
                tier.Mass, tier.Cost);
            BuildShield(settings, prefab, tier);
        }

        private static void BuildShield(PartSettings settings, GameObject prefab, ForceField.Tier tier)
        {
            settings.internalStateClass = PowerCatalog.MachineStateClass;
            const float width = 0.95f, height = 0.42f, depth = 0.95f;
            float scale = tier.ModelScale;
            Transform visuals = PartFactory.BlankFromSignalDisplay(prefab, new Vector3(0f, 0.65f, 0f) * scale, new Vector3(width, 1.3f, depth) * scale);
            var model = new MachineModels.Model();
            // Octagonal plinth, a low housing with vents and grille
            model.Armor.Cylinder(new Vector3(0f, 0f, 0f), new Vector3(0f, 0.07f, 0f), width * 0.56f, width * 0.54f, 8);
            model.Light.Cylinder(new Vector3(0f, 0.07f, 0f), new Vector3(0f, 0.08f, 0f), width * 0.545f, width * 0.545f, 8, capA: false, capB: false);
            MachineModels.Housing(model, width * 0.9f, height, depth * 0.9f, 0.62f);
            float face = depth * 0.9f * 0.47f;
            MachineModels.Vent(model, -0.22f, 0.2f, face, 0.2f, 0.18f, 3);
            MachineModels.Vent(model, 0.22f, 0.2f, face, 0.2f, 0.18f, 3);
            MachineModels.Grille(model, 0.2f, face, 0.1f, 0.2f);
            MachineModels.Vent(model, 0f, 0.2f, -face, 0.34f, 0.18f, 3);
            // Central column with three field rings
            float top = height;
            model.Dark.Cylinder(new Vector3(0f, top, 0f), new Vector3(0f, top + 0.42f, 0f), 0.09f, 0.07f, 8);
            model.Armor.Cylinder(new Vector3(0f, top, 0f), new Vector3(0f, top + 0.05f, 0f), 0.2f, 0.16f, 8);
            var ring = new MeshBuilder();
            for (int i = 0; i < 3; i++)
            {
                float y = top + 0.1f + i * 0.11f;
                float r = 0.17f - i * 0.025f;
                ring.Cylinder(new Vector3(0f, y, 0f), new Vector3(0f, y + 0.035f, 0f), r, r, 16);
            }
            // Four emitter prongs rising from the corners, bent toward the core
            float coreY = top + 0.66f;
            for (int i = 0; i < 4; i++)
            {
                float angle = (45f + i * 90f) * Mathf.Deg2Rad;
                Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 across = Vector3.Cross(Vector3.up, outward);
                Vector3 foot = outward * 0.4f + Vector3.up * (top - 0.08f);
                Vector3 knee = outward * 0.36f + Vector3.up * (top + 0.42f);
                Vector3 tip = outward * 0.16f + Vector3.up * (coreY + 0.04f);
                Prong(model.Armor, foot, knee, across, 0.07f, 0.05f);
                Prong(model.Armor, knee, tip, across, 0.05f, 0.035f);
                Vector3 leg = (knee - foot).normalized;
                model.Light.Box(Vector3.Lerp(foot, knee, 0.5f) + outward * 0.03f, new Vector3(0.012f, (knee - foot).magnitude * 0.9f, 0.012f), across, leg, Vector3.Cross(across, leg));
                model.Core.Cylinder(tip, tip - outward * 0.05f + Vector3.up * 0.01f, 0.025f, 0.008f, 6);
            }
            // The floating core, a crystal turned by ForceFieldVisuals
            var core = new MeshBuilder();
            core.Cylinder(new Vector3(0f, -0.11f, 0f), new Vector3(0f, 0f, 0f), 0f, 0.075f, 6, capA: false, capB: false);
            core.Cylinder(new Vector3(0f, 0f, 0f), new Vector3(0f, 0.11f, 0f), 0.075f, 0f, 6, capA: false, capB: false);
            MachineModels.Attach(visuals, model, "Shield" + tier.Level, tier.Glow, tier.Glow);
            var ringObject = new GameObject(ForceFieldVisuals.RingName) { layer = visuals.gameObject.layer };
            ringObject.transform.SetParent(visuals, false);
            ringObject.AddComponent<MeshFilter>().sharedMesh = ring.Build(ForceFieldVisuals.RingName);
            var ringRenderer = ringObject.AddComponent<MeshRenderer>();
            ringRenderer.sharedMaterial = LitMaterials.Get("TFA_ShieldRing", Color.white, 0f, 0.8f, Color.white);
            var coreObject = new GameObject(ForceFieldVisuals.CoreName) { layer = visuals.gameObject.layer };
            coreObject.transform.SetParent(visuals, false);
            coreObject.transform.localPosition = new Vector3(0f, coreY, 0f);
            coreObject.AddComponent<MeshFilter>().sharedMesh = core.Build(ForceFieldVisuals.CoreName);
            coreObject.AddComponent<MeshRenderer>().sharedMaterial = LitMaterials.Get("TFA_ShieldRing", Color.white, 0f, 0.8f, Color.white);
            // The compact model: the same, smaller
            if (!Mathf.Approximately(scale, 1f))
            {
                foreach (Transform child in visuals)
                {
                    child.localPosition *= scale;
                    child.localScale *= scale;
                }
            }
            new GameObject(ForceFieldVisuals.TierPrefix + tier.Level).transform.SetParent(visuals, false);
            visuals.gameObject.AddComponent<ForceFieldVisuals>();
            var bounds = prefab.GetComponent<ShipPartBounds>();
            GraftRow(prefab, bounds, 0.2f * scale, -face * scale, 0.22f * scale,
                (PowerCatalog.MachinePowerA, CableType.Power), (PowerCatalog.MachineSignal, CableType.Signal), (ForceField.OxygenSocket, CableType.Fluid), (PowerCatalog.MachinePowerB, CableType.Power));
        }

        // A square-section arm from a to b, narrowing
        private static void Prong(MeshBuilder m, Vector3 a, Vector3 b, Vector3 across, float wa, float wb)
        {
            Vector3 along = (b - a).normalized;
            Vector3 normal = Vector3.Cross(across, along).normalized;
            Vector3 ha = across * wa * 0.5f, hb = across * wb * 0.5f, na = normal * wa * 0.5f, nb = normal * wb * 0.5f;
            m.Block(new[]
            {
                a - ha - na, a + ha - na, b - hb - nb, b + hb - nb,
                a - ha + na, a + ha + na, b - hb + nb, b + hb + nb
            });
        }

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        // The donor's body in another color, a faint glow; plugs keep their look
        private static void Tint(GameObject prefab, string name, Color color, Color glow)
        {
            var copies = new System.Collections.Generic.Dictionary<Material, Material>();
            foreach (MeshRenderer renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.GetComponentInParent<SocketObject>() != null)
                    continue;
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material original = materials[i];
                    if (original == null || original.name.StartsWith("Plugs") || !original.HasProperty(ColorId))
                        continue;
                    if (!copies.TryGetValue(original, out Material copy))
                    {
                        copy = new Material(original) { name = original.name + "_" + name };
                        copy.SetColor(ColorId, original.GetColor(ColorId) * color);
                        if (copy.HasProperty(EmissionColorId))
                        {
                            copy.EnableKeyword("_EMISSION");
                            copy.SetColor(EmissionColorId, glow);
                        }
                        copies[original] = copy;
                    }
                    materials[i] = copy;
                }
                renderer.sharedMaterials = materials;
            }
        }

        private static void ConfigureBreaker(PartSettings settings, GameObject prefab)
        {
            PartFactory.Describe(settings, Loc.Pick("Disjoncteur", "Circuit breaker"),
                Loc.Pick($"Relie deux réseaux électriques : prise d'entrée (côté générateurs) et prise de sortie (côté consommateurs). Fait passer jusqu'à {PowerCatalog.BreakerRatingWatts.Value:0} W. Au-delà pendant {PowerCatalog.BreakerTripSeconds.Value:0.#} s, il disjoncte. Prise de commande : sans câble il est fermé ; avec un câble, un signal > 0 le ferme et 0 l'ouvre, et repasser à 0 puis > 0 le réarme. Sa prise d'état donne 0 fermé, 1 ouvert, 2 disjoncté.", $"Links two power networks: input socket (generators side) and output socket (consumers side). Lets through up to {PowerCatalog.BreakerRatingWatts.Value:0} W. Above that for {PowerCatalog.BreakerTripSeconds.Value:0.#} s, it trips. Control socket: without a cable it is closed; with one, a signal > 0 closes it and 0 opens it, and going back to 0 then > 0 resets it. Its status socket gives 0 closed, 1 open, 2 tripped."),
                4f, new[] { (101u, 2), (112u, 3) });
            settings.internalStateClass = StateClass;

            var size = new Vector3(0.4f, 0.12f, 0.36f);
            Transform visuals = PartFactory.BlankFromSignalDisplay(prefab, new Vector3(0f, size.y * 0.5f, 0f), size);
            int layer = visuals.gameObject.layer;

            var housing = new MeshBuilder { Panel = GameSkin.PlainPanel };
            housing.Box(new Vector3(0f, 0.055f, 0f), new Vector3(0.4f, 0.11f, 0.36f));
            housing.Box(new Vector3(0f, 0.115f, -0.08f), new Vector3(0.14f, 0.01f, 0.1f));
            var lever = new MeshBuilder { Panel = GameSkin.PlainPanel };
            lever.Box(new Vector3(0f, 0.035f, 0f), new Vector3(0.05f, 0.07f, 0.03f));
            var led = new MeshBuilder();
            led.Box(new Vector3(0.13f, 0.112f, -0.08f), new Vector3(0.03f, 0.006f, 0.03f));

            GameSkin.AddMesh(visuals, layer, "TFA_BreakerHousing", housing, LitMaterials.Get("TFA_BreakerHousing", new Color(0.78f, 0.79f, 0.8f), 0.2f, 0.5f));
            var leverPivot = new GameObject(BreakerVisuals.LeverName) { layer = layer };
            leverPivot.transform.SetParent(visuals, false);
            leverPivot.transform.localPosition = new Vector3(0f, 0.12f, -0.08f);
            GameSkin.AddMesh(leverPivot.transform, layer, "TF_NoPaint_BreakerLever", lever, LitMaterials.Get("TFA_BreakerLever", new Color(0.1f, 0.1f, 0.11f), 0.1f, 0.6f));
            AddMesh(visuals, layer, BreakerVisuals.LedName, led, LitMaterials.Get("TFA_BreakerLed", Color.white, 0f, 0.8f, Color.white));
            visuals.gameObject.AddComponent<BreakerVisuals>();

            // Power in on the left side, power out on the right side, the two signals on top
            var bounds = prefab.GetComponent<ShipPartBounds>();
            GraftRow(prefab, bounds, 0.06f, BackOf(bounds), 0.12f,
                (PowerCatalog.BreakerInput, CableType.Power), (PowerCatalog.StatusSignal, CableType.Signal), (PowerCatalog.BreakerControl, CableType.Signal), (PowerCatalog.BreakerOutput, CableType.Power));
        }

        // A plug looks into the part; SDModKit then sets it just clear of the part's bounds
        /// <summary>
        /// Sockets in one row on the part's back face, left to right as listed: every cable leaves
        /// the part along the same axis.
        /// </summary>
        internal static void GraftRow(GameObject prefab, ShipPartBounds bounds, float y, float backZ, float spacing, params (byte Id, CableType Type)[] sockets)
        {
            for (int i = 0; i < sockets.Length; i++)
                GraftSeated(prefab, bounds, sockets[i].Id, sockets[i].Type, new Vector3((i - (sockets.Length - 1) * 0.5f) * spacing, y, backZ), Vector3.back);
        }

        // The back face of a part's placement box
        internal static float BackOf(ShipPartBounds bounds) => bounds != null ? bounds.center.z - bounds.bounds.z * 0.47f : -0.3f;

        internal static void GraftSeated(GameObject prefab, ShipPartBounds bounds, byte id, CableType type, Vector3 position, Vector3 outward)
        {
            Quaternion rotation = Quaternion.LookRotation(-outward, Mathf.Abs(outward.y) > 0.9f ? Vector3.forward : Vector3.up);
            GameObject socket = ContentBuilder.GraftSocket(prefab, id, type, position, rotation);
            if (socket != null)
                PartFinish.Seat("TFA", prefab, bounds, socket.GetComponent<SocketObject>());
        }

        private static void AddMesh(Transform parent, int layer, string name, MeshBuilder builder, Material material)
        {
            var child = new GameObject(name) { layer = layer };
            child.transform.SetParent(parent, false);
            child.AddComponent<MeshFilter>().sharedMesh = builder.Build(name);
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
        }
    }

    /// <summary>Breaker lever and status light, from the synchronized state: green closed, grey open, red tripped.</summary>
    internal sealed class BreakerVisuals : DefaultShipPartVisuals
    {
        public const string LeverName = "TFA_BreakerLeverPivot";
        public const string LedName = "TF_NoPaint_BreakerLed";

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private Transform _lever;
        private Renderer _led;
        private MaterialPropertyBlock _block;

        private void Awake()
        {
            _lever = transform.Find(LeverName);
            Transform led = transform.Find(LedName);
            _led = led != null ? led.GetComponent<Renderer>() : null;
            _block = new MaterialPropertyBlock();
            Show(PowerCatalog.BreakerClosed);
        }

        public override void SetNetworkState(IStatefulPartState state)
        {
            if (state is FluidTankState tank)
                Show(Mathf.Round(tank.FluidQuantity));
        }

        public override void ReactToNetworkStateChange(StatefulPartStateChangeEvent change)
        {
            SetNetworkState(change.NewState);
        }

        private void Show(float status)
        {
            Color color = status == PowerCatalog.BreakerTripped ? new Color(1f, 0.15f, 0.1f)
                : status == PowerCatalog.BreakerOpen ? new Color(0.35f, 0.37f, 0.4f)
                : new Color(0.2f, 1f, 0.35f);
            if (_lever != null)
                _lever.localRotation = Quaternion.Euler(status == PowerCatalog.BreakerClosed ? -25f : 25f, 0f, 0f);
            if (_led != null)
            {
                _led.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, color);
                _block.SetColor(EmissionColorId, status == PowerCatalog.BreakerOpen ? Color.black : color * 2.5f);
                _led.SetPropertyBlock(_block);
            }
        }
    }
}

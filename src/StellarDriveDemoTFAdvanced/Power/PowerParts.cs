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
            PartFactory.Register(PowerCatalog.ShieldId, "SignalDisplay", AdvancedTab.ArtifactsName, AdvancedTab.ArtifactsPowerRow, ConfigureShield);
            PartFactory.Register(PowerCatalog.CondenserId, "SignalDisplay", AdvancedTab.ArtifactsName, AdvancedTab.ArtifactsPowerRow, ConfigureCondenser);
            PartFactory.Register(PowerCatalog.QuantumGyroId, "Gyroscope", AdvancedTab.ArtifactsName, AdvancedTab.ArtifactsPowerRow, ConfigureQuantumGyro);
            AdvancedMod.Log.Msg("registered the battery, the circuit breaker, the quantum reactor, the cryonite battery and the force field generator");
        }

        private static void ConfigureBattery(PartSettings settings, GameObject prefab)
        {
            PartFactory.Describe(settings, "Batterie",
                $"Stocke l'énergie du réseau électrique branché sur ses prises. Se recharge avec la puissance que les générateurs ont en réserve (ils brûlent alors plus de bois) et la rend quand le réseau en manque. {PowerCatalog.BatteryCapacityKJ.Value:0} kJ, charge {PowerCatalog.BatteryChargeWatts.Value:0} W, décharge {PowerCatalog.BatteryDischargeWatts.Value:0} W, rendement {PowerCatalog.BatteryEfficiency.Value:P0}. Sa prise signal donne la charge en %.",
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
            GraftSeated(prefab, bounds, PowerCatalog.BatteryPowerA, CableType.Power, new Vector3(-width * 0.47f, height * 0.35f, 0f), Vector3.left);
            GraftSeated(prefab, bounds, PowerCatalog.BatteryPowerB, CableType.Power, new Vector3(width * 0.47f, height * 0.35f, 0f), Vector3.right);
            GraftSeated(prefab, bounds, PowerCatalog.StatusSignal, CableType.Signal, new Vector3(0f, height * 0.45f, -depth * 0.47f), Vector3.back);
        }

        private static void ConfigureReactor(PartSettings settings, GameObject prefab)
        {
            PartFactory.Describe(settings, "Réacteur quantique",
                $"Un noyau quantique tenu dans une cage d'alliage nexorien : fournit {PowerCatalog.ReactorWatts.Value:0} W en continu au réseau branché sur ses prises, sans carburant et sans fin (trois générateurs à bois). Sa prise signal vaut 100 quand il alimente un réseau. Fabriqué avec les trouvailles rares de la cité abandonnée de TF Nexora et des ruines.",
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
            GraftSeated(prefab, bounds, PowerCatalog.BatteryPowerA, CableType.Power, new Vector3(-width * 0.47f, 0.36f, 0f), Vector3.left);
            GraftSeated(prefab, bounds, PowerCatalog.BatteryPowerB, CableType.Power, new Vector3(width * 0.47f, 0.36f, 0f), Vector3.right);
            GraftSeated(prefab, bounds, PowerCatalog.StatusSignal, CableType.Signal, new Vector3(-width * 0.47f, 0.6f, 0.25f), Vector3.left);
        }

        private static void ConfigureCryoBattery(PartSettings settings, GameObject prefab)
        {
            float capacity = PowerCatalog.BatteryCapacityKJ.Value * PowerCatalog.CryoBatteryScale;
            PartFactory.Describe(settings, "Batterie à cryonite",
                $"Les cristaux de cryonite de TF Glacia gardent l'énergie presque sans perte : {capacity:0} kJ (huit batteries), charge {PowerCatalog.BatteryChargeWatts.Value * PowerCatalog.CryoBatteryRateScale:0} W, décharge {PowerCatalog.BatteryDischargeWatts.Value * PowerCatalog.CryoBatteryRateScale:0} W. Branchement et prise signal comme la batterie.",
                160f, new[] { (Items.ModItems.Cryonite, 8), (Items.ModItems.Titanium, 4), (112u, 8), (104u, 4) });
            settings.internalStateClass = StateClass;
            BuildCell(prefab, "CryoBattery", 1.1f, 0.9f, 0.8f, MachineModels.Cyan, true);
        }

        private static void ConfigureCondenser(PartSettings settings, GameObject prefab)
        {
            PartFactory.Describe(settings, "Condenseur à cryonite",
                $"Des cristaux de cryonite refroidis par l'électricité du vaisseau libèrent de l'oxygène : {PowerCatalog.CondenserOxygenPerSecond.Value:0.###} unité/s dans la conduite branchée sur sa prise de fluide (vide ou déjà d'oxygène), pour {PowerCatalog.CondenserWatts.Value:0} W. Sa prise signal donne sa production en millièmes d'unité/s.",
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
            GraftSeated(prefab, bounds, PowerCatalog.MachinePowerA, CableType.Power, new Vector3(-width * 0.47f, 0.3f, 0f), Vector3.left);
            GraftSeated(prefab, bounds, PowerCatalog.MachinePowerB, CableType.Power, new Vector3(width * 0.47f, 0.3f, 0f), Vector3.right);
            GraftSeated(prefab, bounds, PowerCatalog.MachineSignal, CableType.Signal, new Vector3(0.18f, 0.35f, -depth * 0.47f), Vector3.back);
            GraftSeated(prefab, bounds, Condenser.OxygenSocket, CableType.Fluid, new Vector3(-0.18f, 0.3f, -depth * 0.47f), Vector3.back);
        }

        // The game's gyroscope, its body in violet with a lit quantum ring; torque from QuantumGyroscopePatch
        private static void ConfigureQuantumGyro(PartSettings settings, GameObject prefab)
        {
            PartFactory.Describe(settings, "Gyroscope quantique",
                $"Un noyau quantique en rotation dans une cage d'alliage : le couple de {PowerCatalog.QuantumGyroFactor.Value:0} gyroscopes, branché et commandé comme le gyroscope du jeu.",
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

        private static void ConfigureShield(PartSettings settings, GameObject prefab)
        {
            PartFactory.Describe(settings, "Générateur de champ de force",
                $"Enveloppe tout le vaisseau (et ce qui y est amarré) dans un champ qui encaisse les chocs et les forces de marée à la place de la coque : {PowerCatalog.ShieldCapacity.Value:0} points (un choc compte sa variation de vitesse en m/s). Consomme {PowerCatalog.ShieldWatts.Value:0} W sur ses prises électriques et {PowerCatalog.ShieldOxygenPerSecond.Value:0.###} unité/s d'oxygène sur sa prise de fluide. À zéro, il se casse et redémarre au bout de {PowerCatalog.ShieldCooldownSeconds.Value / 60f:0} min, rechargé. Sa prise signal donne la charge en % (-1 éteint, négatif : secondes avant redémarrage).",
                120f, new[] { (Items.ModItems.QuantumCore, 1), (Items.ModItems.Cryonite, 6), (Items.ModItems.NexoranAlloy, 4), (Items.ModItems.Titanium, 6), (112u, 8) });
            settings.internalStateClass = PowerCatalog.MachineStateClass;
            const float width = 0.95f, height = 0.7f, depth = 0.95f;
            Transform visuals = PartFactory.BlankFromSignalDisplay(prefab, new Vector3(0f, 0.6f, 0f), new Vector3(width, 1.2f, depth));
            var model = new MachineModels.Model();
            MachineModels.Housing(model, width, height, depth, 0.7f);
            float face = depth * 0.47f;
            MachineModels.Vent(model, -0.25f, 0.28f, face, 0.24f, 0.3f, 3);
            MachineModels.Vent(model, 0.25f, 0.28f, face, 0.24f, 0.3f, 3);
            MachineModels.Grille(model, 0.28f, face, 0.12f, 0.34f);
            // The emitter: a mast, a lit ring and a dish
            model.Armor.Cylinder(new Vector3(0f, height, 0f), new Vector3(0f, height + 0.3f, 0f), 0.09f, 0.07f, 8);
            model.Armor.Cylinder(new Vector3(0f, height + 0.38f, 0f), new Vector3(0f, height + 0.46f, 0f), 0.12f, 0.38f, 12, capA: true, capB: false);
            var ring = new MeshBuilder();
            ring.Cylinder(new Vector3(0f, height + 0.28f, 0f), new Vector3(0f, height + 0.34f, 0f), 0.26f, 0.26f, 16);
            MachineModels.Attach(visuals, model, "Shield", MachineModels.Red, MachineModels.Cyan);
            var ringObject = new GameObject(ForceFieldVisuals.RingName) { layer = visuals.gameObject.layer };
            ringObject.transform.SetParent(visuals, false);
            ringObject.AddComponent<MeshFilter>().sharedMesh = ring.Build(ForceFieldVisuals.RingName);
            var ringRenderer = ringObject.AddComponent<MeshRenderer>();
            ringRenderer.sharedMaterial = LitMaterials.Get("TFA_ShieldRing", Color.white, 0f, 0.8f, Color.white);
            visuals.gameObject.AddComponent<ForceFieldVisuals>();
            var bounds = prefab.GetComponent<ShipPartBounds>();
            GraftSeated(prefab, bounds, PowerCatalog.MachinePowerA, CableType.Power, new Vector3(-width * 0.47f, 0.28f, 0f), Vector3.left);
            GraftSeated(prefab, bounds, PowerCatalog.MachinePowerB, CableType.Power, new Vector3(width * 0.47f, 0.28f, 0f), Vector3.right);
            GraftSeated(prefab, bounds, PowerCatalog.MachineSignal, CableType.Signal, new Vector3(0.2f, 0.4f, -depth * 0.47f), Vector3.back);
            GraftSeated(prefab, bounds, ForceField.OxygenSocket, CableType.Fluid, new Vector3(-0.2f, 0.3f, -depth * 0.47f), Vector3.back);
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
            PartFactory.Describe(settings, "Disjoncteur",
                $"Relie deux réseaux électriques : prise d'entrée (côté générateurs) et prise de sortie (côté consommateurs). Fait passer jusqu'à {PowerCatalog.BreakerRatingWatts.Value:0} W. Au-delà pendant {PowerCatalog.BreakerTripSeconds.Value:0.#} s, il disjoncte. Prise de commande : sans câble il est fermé ; avec un câble, un signal > 0 le ferme et 0 l'ouvre, et repasser à 0 puis > 0 le réarme. Sa prise d'état donne 0 fermé, 1 ouvert, 2 disjoncté.",
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
            GraftSeated(prefab, bounds, PowerCatalog.BreakerInput, CableType.Power, new Vector3(-0.2f, 0.06f, 0f), Vector3.left);
            GraftSeated(prefab, bounds, PowerCatalog.BreakerOutput, CableType.Power, new Vector3(0.2f, 0.06f, 0f), Vector3.right);
            GraftSeated(prefab, bounds, PowerCatalog.StatusSignal, CableType.Signal, new Vector3(-0.1f, 0.12f, 0.09f), Vector3.up);
            GraftSeated(prefab, bounds, PowerCatalog.BreakerControl, CableType.Signal, new Vector3(0.1f, 0.12f, 0.09f), Vector3.up);
        }

        // A plug looks into the part; SDModKit then sets it just clear of the part's bounds
        private static void GraftSeated(GameObject prefab, ShipPartBounds bounds, byte id, CableType type, Vector3 position, Vector3 outward)
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

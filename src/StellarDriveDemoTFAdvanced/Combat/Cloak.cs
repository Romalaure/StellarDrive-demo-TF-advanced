using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Interface.Settings;
using Ships.Parts.Common;
using StellarDriveDemoTFAdvanced.Common;
using StellarDriveDemoTFAdvanced.Parts;
using StellarDriveDemoTFAdvanced.Power;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Combat
{
    /// <summary>
    /// Stealth generator: while powered, it blurs the ship (and everything docked to it) on the
    /// pirates' sensors. Raids find it three times less often and may lose its track on the way;
    /// pirates only see it within 2.5 km (bases 1.2 km), must come closer to fire, aim far worse
    /// and lock their missiles later. The simulation is the host's (<see cref="CombatPower"/>,
    /// <see cref="Pirates"/>); its signal socket gives 1 while it hides the ship.
    /// </summary>
    internal static class Cloak
    {
        public static readonly Color Glow = new Color(0.45f, 0.6f, 1f);

        public static void Register()
        {
            PartFactory.Register(PowerCatalog.CloakId, "SignalDisplay", AdvancedTab.Name, AdvancedTab.CombatRow, Configure);
        }

        private static void Configure(PartSettings settings, GameObject prefab)
        {
            float watts = Mathf.Max(0f, PowerCatalog.CloakWatts.Value);
            PartFactory.Describe(settings, Loc.Pick("Générateur d'invisibilité", "Stealth generator", "Generador de sigilo"),
                Loc.Pick($"Brouille la signature du vaisseau (et de ce qui y est amarré) sur les capteurs pirates : les raids le trouvent trois fois moins souvent et peuvent perdre sa trace en route, les pirates ne le voient qu'à moins de 2,5 km (les bases 1,2 km), doivent s'approcher pour tirer, visent bien plus mal et verrouillent leurs missiles plus tard. Il lui faut {watts:0} W sur ses prises électriques. Sa prise signal vaut 1 quand il masque le vaisseau ; éteint : -1 pas de réseau électrique, -3 pas assez de courant.",
                    $"Blurs the ship's signature (and what is docked to it) on pirate sensors: raids find it three times less often and may lose its track on the way, pirates only see it within 2.5 km (bases 1.2 km), must come closer to fire, aim far worse and lock their missiles later. It needs {watts:0} W on its power sockets. Its signal socket reads 1 while it hides the ship; off: -1 no power grid, -3 not enough power.", $"Difumina la firma de la nave (y de lo que está acoplado a ella) en los sensores piratas: las incursiones la encuentran tres veces menos y pueden perder su rastro por el camino, los piratas solo la ven a menos de 2,5 km (bases 1,2 km), deben acercarse más para disparar, apuntan mucho peor y fijan sus misiles más tarde. Necesita {watts:0} W en sus tomas de energía. Su toma de señal da 1 mientras oculta la nave; apagado: -1 sin red eléctrica, -3 energía insuficiente."),
                220f, new[] { (Items.ModItems.QuantumCore, 2), (Items.ModItems.Cryonite, 10), (Items.ModItems.Quartz, 8), (Items.ModItems.Titanium, 12), (112u, 15) });
            settings.internalStateClass = PowerCatalog.MachineStateClass;
            const float width = 0.9f, height = 0.5f, depth = 0.9f;
            Transform visuals = PartFactory.BlankFromSignalDisplay(prefab, new Vector3(0f, 0.7f, 0f), new Vector3(width, 1.4f, depth));
            var model = new MachineModels.Model();
            MachineModels.Housing(model, width, height, depth, 0.7f);
            float face = depth * 0.47f;
            MachineModels.Vent(model, -0.22f, 0.22f, face, 0.2f, 0.2f, 3);
            MachineModels.Vent(model, 0.22f, 0.22f, face, 0.2f, 0.2f, 3);
            MachineModels.Grille(model, 0.22f, face, 0.1f, 0.22f);
            // A glass column with the emitter crystal, a cap on four thin struts
            float top = height;
            model.Armor.Cylinder(new Vector3(0f, top, 0f), new Vector3(0f, top + 0.06f, 0f), 0.24f, 0.2f, 12);
            model.Dark.Cylinder(new Vector3(0f, top + 0.06f, 0f), new Vector3(0f, top + 0.8f, 0f), 0.05f, 0.05f, 10);
            foreach (float a in new[] { 45f, 135f, 225f, 315f })
                model.Armor.Box(new Vector3(0f, top + 0.45f, 0f) + Quaternion.Euler(0f, a, 0f) * new Vector3(0.2f, 0f, 0f), new Vector3(0.025f, 0.8f, 0.025f));
            model.Armor.Cylinder(new Vector3(0f, top + 0.84f, 0f), new Vector3(0f, top + 0.9f, 0f), 0.17f, 0.24f, 12);
            model.Light.Cylinder(new Vector3(0f, top + 0.83f, 0f), new Vector3(0f, top + 0.84f, 0f), 0.17f, 0.17f, 12, capA: false, capB: false);
            MachineModels.Attach(visuals, model, "Cloak", Glow, Glow);
            // Three rings turned by CloakVisuals, and the crystal
            for (int i = 0; i < 3; i++)
            {
                var ring = new MachineModels.Model();
                float r = 0.16f - i * 0.025f;
                ring.Glow.Cylinder(new Vector3(0f, -0.012f, 0f), new Vector3(0f, 0.012f, 0f), r, r, 24, capA: false, capB: false);
                ring.Armor.Box(new Vector3(r, 0f, 0f), new Vector3(0.03f, 0.03f, 0.03f));
                ring.Armor.Box(new Vector3(-r, 0f, 0f), new Vector3(0.03f, 0.03f, 0.03f));
                var holder = new GameObject(CloakVisuals.RingPrefix + i) { layer = visuals.gameObject.layer };
                holder.transform.SetParent(visuals, false);
                holder.transform.localPosition = new Vector3(0f, top + 0.25f + i * 0.2f, 0f);
                MachineModels.Attach(holder.transform, ring, "CloakRing" + i, Glow, Glow);
            }
            var crystal = new MachineModels.Model();
            crystal.Core.Cylinder(new Vector3(0f, -0.09f, 0f), new Vector3(0f, 0f, 0f), 0f, 0.05f, 6, capA: false, capB: false);
            crystal.Core.Cylinder(new Vector3(0f, 0f, 0f), new Vector3(0f, 0.09f, 0f), 0.05f, 0f, 6, capA: false, capB: false);
            var core = new GameObject(CloakVisuals.CoreName) { layer = visuals.gameObject.layer };
            core.transform.SetParent(visuals, false);
            core.transform.localPosition = new Vector3(0f, top + 0.45f, 0f);
            MachineModels.Attach(core.transform, crystal, "CloakCrystal", Glow, Glow);
            visuals.gameObject.AddComponent<CloakVisuals>();
            var bounds = prefab.GetComponent<ShipPartBounds>();
            PowerParts.GraftRow(prefab, bounds, 0.22f, PowerParts.BackOf(bounds), 0.22f,
                (PowerCatalog.MachinePowerA, CableType.Power), (PowerCatalog.MachineSignal, CableType.Signal), (PowerCatalog.MachinePowerB, CableType.Power));
        }
    }

    /// <summary>Rings turning (each its own way) and the crystal shown while the generator hides the ship.</summary>
    internal sealed class CloakVisuals : DefaultShipPartVisuals
    {
        public const string RingPrefix = "TF_NoPaint_CloakRing_";
        public const string CoreName = "TF_NoPaint_CloakCore";

        private readonly Transform[] _rings = new Transform[3];
        private Transform _core;
        private bool _up;

        private void Awake()
        {
            foreach (Transform child in transform)
            {
                if (child.name.StartsWith(RingPrefix) && int.TryParse(child.name.Substring(RingPrefix.Length), out int i) && i >= 0 && i < 3)
                    _rings[i] = child;
                else if (child.name == CoreName)
                    _core = child;
            }
            Show(false);
        }

        public override void SetNetworkState(IStatefulPartState state)
        {
            if (state is ConstantState constant)
                Show(constant.ConstantValue > 0);
        }

        public override void ReactToNetworkStateChange(StatefulPartStateChangeEvent change) => SetNetworkState(change.NewState);

        private void Show(bool up)
        {
            _up = up;
            if (_core != null)
                _core.gameObject.SetActive(up);
        }

        private void LateUpdate()
        {
            if (!_up)
                return;
            Camera camera = MainCamera.Get();
            if (camera == null || (camera.transform.position - transform.position).sqrMagnitude > 80f * 80f)
                return;
            float t = Time.time;
            for (int i = 0; i < 3; i++)
            {
                if (_rings[i] != null)
                    _rings[i].localRotation = Quaternion.Euler(Mathf.Sin(t * 0.7f + i) * 25f, t * (i % 2 == 0 ? 90f : -120f), 0f);
            }
            if (_core != null)
                _core.localRotation = Quaternion.Euler(0f, -t * 60f, 0f);
        }
    }
}

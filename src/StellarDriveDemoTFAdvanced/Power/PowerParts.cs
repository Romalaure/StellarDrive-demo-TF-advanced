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
            PartFactory.Register(PowerCatalog.BatteryId, "Battery", AdvancedTab.PropulsionRow, ConfigureBattery);
            PartFactory.Register(PowerCatalog.BreakerId, "SignalDisplay", AdvancedTab.PropulsionRow, ConfigureBreaker);
            AdvancedMod.Log.Msg("registered the battery and the circuit breaker");
        }

        private static void ConfigureBattery(PartSettings settings, GameObject prefab)
        {
            PartFactory.Describe(settings, "Batterie",
                $"Stocke l'énergie du réseau électrique branché sur ses prises. Se recharge avec la puissance que les générateurs ont en réserve (ils brûlent alors plus de bois) et la rend quand le réseau en manque. {PowerCatalog.BatteryCapacityKJ.Value:0} kJ, charge {PowerCatalog.BatteryChargeWatts.Value:0} W, décharge {PowerCatalog.BatteryDischargeWatts.Value:0} W, rendement {PowerCatalog.BatteryEfficiency.Value:P0}. Sa prise signal donne la charge en %.",
                100f, new[] { (101u, 6), (112u, 6), (104u, 2) });
            settings.internalStateClass = StateClass;

            // The Battery Bank's own two power sockets stay; a signal socket reporting the charge is added beside the first
            SocketObject power = prefab.GetComponentsInChildren<SocketObject>(true).OrderBy(s => s.Id).FirstOrDefault(s => s.Type == CableType.Power);
            var bounds = prefab.GetComponent<ShipPartBounds>();
            if (power == null || bounds == null)
            {
                AdvancedMod.Log.Warning("battery: the Battery Bank has no power socket or bounds, no charge signal");
                return;
            }
            Vector3 position = prefab.transform.InverseTransformPoint(power.transform.position);
            Vector3 outward = -prefab.transform.InverseTransformDirection(power.transform.forward).normalized;
            Vector3 side = Vector3.Cross(outward, Mathf.Abs(outward.y) > 0.9f ? Vector3.forward : Vector3.up).normalized;
            GraftSeated(prefab, bounds, PowerCatalog.StatusSignal, CableType.Signal, position + side * 0.3f, outward);
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

            var housing = new MeshBuilder();
            housing.Box(new Vector3(0f, 0.055f, 0f), new Vector3(0.4f, 0.11f, 0.36f));
            housing.Box(new Vector3(0f, 0.115f, -0.08f), new Vector3(0.14f, 0.01f, 0.1f));
            var lever = new MeshBuilder();
            lever.Box(new Vector3(0f, 0.035f, 0f), new Vector3(0.05f, 0.07f, 0.03f));
            var led = new MeshBuilder();
            led.Box(new Vector3(0.13f, 0.112f, -0.08f), new Vector3(0.03f, 0.006f, 0.03f));

            AddMesh(visuals, layer, "TFA_BreakerHousing", housing, LitMaterials.Get("TFA_BreakerHousing", new Color(0.78f, 0.79f, 0.8f), 0.2f, 0.5f));
            var leverPivot = new GameObject(BreakerVisuals.LeverName) { layer = layer };
            leverPivot.transform.SetParent(visuals, false);
            leverPivot.transform.localPosition = new Vector3(0f, 0.12f, -0.08f);
            AddMesh(leverPivot.transform, layer, "TF_NoPaint_BreakerLever", lever, LitMaterials.Get("TFA_BreakerLever", new Color(0.1f, 0.1f, 0.11f), 0.1f, 0.6f));
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

using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Interface.Settings;
using Ships.Parts.Common;
using Ships.Parts.Common.Model;
using Ships.Parts.Common.Utils;
using StellarDriveDemoTFAdvanced.Common;
using StellarDriveDemoTFAdvanced.Parts;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Power
{
    /// <summary>
    /// The mining drone bay (part 7480): a landing pad with a small drone on it. While powered and
    /// commanded, with a chest on its item line, the drone flies to the nearest rock, crystal, ore
    /// or tree within its range, mines it, flies back and empties what it got into the chests.
    /// The host runs it with the ship mining laser (<see cref="ShipMiningLaser"/>); the bay's
    /// ConstantState value tells every client where the drone is, for its flight.
    /// </summary>
    internal static class DroneBay
    {
        public const ushort Id = 7480;
        public const float Range = 40f, Speed = 7f, Watts = 500f;
        /// <summary>ConstantValue: on its pad with nothing in range, flying out, mining, flying back.</summary>
        public const double Docked = 1, Out = 3, Mining = 2, Back = 4;

        public static void Register()
        {
            PartFactory.Register(Id, "SignalDisplay", AdvancedTab.Name, AdvancedTab.ArtifactsRow, Configure);
            AdvancedMod.Log.Msg("registered the mining drone bay");
        }

        private static void Configure(PartSettings settings, GameObject prefab)
        {
            PartFactory.Describe(settings, Loc.Pick("Baie de drone de minage", "Mining drone bay", "Bahía de dron minero"),
                Loc.Pick($"Un drone part seul miner le rocher, le cristal, le minerai ou l'arbre le plus proche (jusqu'à {Range:0} m), revient et vide sa récolte dans le coffre relié à sa prise d'objets (jaune) par une ligne d'objets. Sans coffre relié, il reste posé. {Watts:0} W. Prise de commande : sans câble il travaille toujours, avec un câble quand le signal est au-dessus de 0. Prise d'état : 3 part, 2 mine, 4 revient, 1 posé, 0 éteint, -1 sans courant, -2 sans coffre, -3 coffre plein.",
                    $"A drone flies off on its own to mine the nearest rock, crystal, ore or tree (up to {Range:0} m), comes back and empties what it got into the chest linked to its item socket (yellow) by an item line. With no chest linked it stays on its pad. {Watts:0} W. Command socket: with no cable it always works, with a cable while the signal is above 0. Status socket: 3 flying out, 2 mining, 4 coming back, 1 docked, 0 off, -1 no power, -2 no chest, -3 chest full.",
                    $"Un dron sale solo a minar la roca, el cristal, el mineral o el árbol más cercano (hasta {Range:0} m), vuelve y vacía lo que obtuvo en el cofre unido a su toma de objetos (amarilla) por una línea de objetos. Sin cofre unido se queda posado. {Watts:0} W. Toma de mando: sin cable trabaja siempre, con cable cuando la señal es mayor que 0. Toma de estado: 3 sale, 2 mina, 4 vuelve, 1 posado, 0 apagado, -1 sin energía, -2 sin cofre, -3 cofre lleno."),
                40f, new[] { (101u, 10), (112u, 8), (102u, 2) });
            settings.internalStateClass = PowerCatalog.MachineStateClass;
            settings.snappingStyle = Ships.Interface.Model.SnappingStyle.PreciseOnAny;
            const float width = 0.9f, depth = 0.9f;
            Transform visuals = PartFactory.BlankFromSignalDisplay(prefab, new Vector3(0f, 0.15f, 0f), new Vector3(width, 0.3f, depth));

            // The pad: a low square base, a lit landing ring and corner lights
            var model = new MachineModels.Model();
            model.Armor.Box(new Vector3(0f, 0.04f, 0f), new Vector3(width, 0.08f, depth));
            model.Dark.Box(new Vector3(0f, 0.085f, 0f), new Vector3(width - 0.1f, 0.01f, depth - 0.1f));
            model.Glow.Cylinder(new Vector3(0f, 0.09f, 0f), new Vector3(0f, 0.095f, 0f), 0.34f, 0.34f, 32, false, false);
            model.Glow.Cylinder(new Vector3(0f, 0.09f, 0f), new Vector3(0f, 0.095f, 0f), 0.3f, 0.3f, 32, false, false);
            foreach (float x in new[] { -0.39f, 0.39f })
                foreach (float z in new[] { -0.39f, 0.39f })
                    model.Light.Box(new Vector3(x, 0.095f, z), new Vector3(0.05f, 0.02f, 0.05f));
            MachineModels.Attach(visuals, model, "DroneBay", new Color(0.3f, 0.9f, 1f), Color.white);
            visuals.gameObject.AddComponent<DroneVisuals>();

            var bounds = prefab.GetComponent<ShipPartBounds>();
            PowerParts.GraftRow(prefab, bounds, 0.04f, -depth * 0.5f, 0.11f,
                (PowerCatalog.MachinePowerA, CableType.Power), (ShipMiningLaser.StatusSocket, CableType.Signal), (ShipMiningLaser.CommandSocket, CableType.Signal),
                (ShipMiningLaser.ItemSocket, SDModKit.Game.ItemCables.Item), (PowerCatalog.MachinePowerB, CableType.Power));
        }
    }

    /// <summary>The drone: on its pad, or flying to the rock it mines and back, rotors turning, a beam while it mines.</summary>
    internal sealed class DroneVisuals : DefaultShipPartVisuals, IContextAwarePart
    {
        private static readonly Vector3 Pad = new Vector3(0f, 0.18f, 0f);
        private static Mesh _body, _rotor, _glow, _beam;

        private bool _hasContext;
        private uint _shipId;
        private ushort _partId;
        private Ships.Interface.Model.State.IShipStateRead _ship;
        private double _value;
        private double _shown = -99;
        private Transform _drone, _beamObject;
        private readonly Transform[] _rotors = new Transform[4];
        private Vector3 _from, _to, _rock;
        private float _start, _duration;

        public void SetContext(ShipPartContext context)
        {
            _shipId = context.Part.ShipId;
            _partId = context.Part.PartId;
            _ship = context.ShipStateRead;
            _hasContext = true;
        }

        public override void SetNetworkState(IStatefulPartState state)
        {
            if (state is ConstantState constant)
                _value = constant.ConstantValue;
        }

        public override void ReactToNetworkStateChange(StatefulPartStateChangeEvent change) => SetNetworkState(change.NewState);

        private void Start() => Build();

        private void LateUpdate()
        {
            if (_drone == null)
                return;
            try
            {
                Fly();
            }
            catch (System.Exception e)
            {
                ErrorLog.Report("mining drone", e);
                _value = 0;
            }
        }

        private void Fly()
        {
            Vector3 dock = transform.TransformPoint(Pad);
            bool flying = _hasContext && _value >= DroneBay.Mining;
            if (_value != _shown)
            {
                // A new leg: out to the rock this client sees nearest, mining over it, or back home
                double previous = _shown;
                _shown = _value;
                _from = _drone.position;
                if (_value == DroneBay.Out)
                {
                    _rock = FindRock(out bool found);
                    _to = found ? _rock + transform.up * 1.6f : dock + transform.up * 3f;
                }
                else if (_value == DroneBay.Back)
                {
                    _to = dock;
                }
                else if (_value == DroneBay.Mining && previous != DroneBay.Out)
                {
                    _rock = FindRock(out bool found);
                    _to = found ? _rock + transform.up * 1.6f : _drone.position;
                }
                _start = Time.time;
                _duration = Mathf.Max(0.3f, Vector3.Distance(_from, _to) / DroneBay.Speed);
                if (DevHarness.Active && _hasContext)
                    AdvancedMod.Log.Msg($"dev: drone {_shipId}:{_partId} status {_value}: at {Vector3.Distance(_drone.position, dock):0.0} m from its pad, going {Vector3.Distance(_to, dock):0.0} m from it, rock {(_rock == Vector3.zero ? "none" : Vector3.Distance(_rock, dock).ToString("0.0") + " m")}");
            }
            if (!flying)
            {
                // Docked (also when off): follows the pad
                _drone.position = Vector3.MoveTowards(_drone.position, dock, DroneBay.Speed * Time.deltaTime * 2f);
                _drone.rotation = transform.rotation;
                _beamObject.gameObject.SetActive(false);
                Spin(_value > 0 ? 300f : 0f);
                return;
            }
            float t = Mathf.Clamp01((Time.time - _start) / _duration);
            float eased = t * t * (3f - 2f * t);
            Vector3 at = Vector3.Lerp(_from, _to, eased) + transform.up * Mathf.Sin(eased * Mathf.PI) * 1.2f;
            if (_value == DroneBay.Mining)
                at = _to + transform.up * (0.1f * Mathf.Sin(Time.time * 2.3f));
            Vector3 step = at - _drone.position;
            _drone.position = at;
            Vector3 heading = Vector3.ProjectOnPlane(step, transform.up);
            if (heading.sqrMagnitude > 1e-5f)
                _drone.rotation = Quaternion.Slerp(_drone.rotation, Quaternion.LookRotation(heading, transform.up) * Quaternion.Euler(12f, 0f, 0f), 0.2f);
            Spin(1500f);
            bool beam = _value == DroneBay.Mining && _rock != Vector3.zero;
            _beamObject.gameObject.SetActive(beam);
            if (beam)
            {
                Vector3 down = _rock - _drone.position;
                _beamObject.position = _drone.position + down * 0.5f;
                _beamObject.rotation = Quaternion.FromToRotation(Vector3.up, down);
                float flicker = 1f + 0.15f * Mathf.Sin(Time.time * 47f);
                _beamObject.localScale = new Vector3(0.04f * flicker, down.magnitude, 0.04f * flicker);
            }
        }

        private Vector3 FindRock(out bool found)
        {
            found = false;
            if (_ship == null || !_ship.TryGetStatefulPart(_partId, out StatefulPart part) || !ShipMiningLaser.DroneTarget(_shipId, part, out Component rock, out Vector3 point))
                return Vector3.zero;
            found = rock != null;
            return point;
        }

        private void Spin(float degreesPerSecond)
        {
            foreach (Transform rotor in _rotors)
                if (rotor != null)
                    rotor.localRotation *= Quaternion.AngleAxis(degreesPerSecond * Time.deltaTime, Vector3.up);
        }

        private void OnDisable()
        {
            if (_drone != null)
                _drone.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (_drone != null)
                _drone.gameObject.SetActive(true);
        }

        private void OnDestroy()
        {
            if (_drone != null)
                Destroy(_drone.gameObject);
        }

        // A small quadcopter: a body with a lit eye, four arms and rotors; it lives in the world (not
        // under the part) so it can leave the ship
        private void Build()
        {
            if (_body == null)
            {
                var body = new MeshBuilder();
                body.Box(new Vector3(0f, 0.08f, 0f), new Vector3(0.28f, 0.09f, 0.36f));
                body.Box(new Vector3(0f, 0.14f, -0.02f), new Vector3(0.18f, 0.04f, 0.22f));
                foreach (float x in new[] { -1f, 1f })
                    foreach (float z in new[] { -1f, 1f })
                    {
                        body.Box(new Vector3(x * 0.2f, 0.1f, z * 0.22f), new Vector3(0.2f, 0.025f, 0.03f));
                        body.Cylinder(new Vector3(x * 0.3f, 0.09f, z * 0.3f), new Vector3(x * 0.3f, 0.13f, z * 0.3f), 0.03f, 0.03f, 10);
                        body.Box(new Vector3(x * 0.1f, 0.015f, z * 0.12f), new Vector3(0.02f, 0.05f, 0.02f));
                    }
                _body = body.Build("TFA_DroneBody");
                var rotor = new MeshBuilder();
                rotor.Box(Vector3.zero, new Vector3(0.24f, 0.006f, 0.035f));
                rotor.Box(Vector3.zero, new Vector3(0.035f, 0.006f, 0.24f));
                _rotor = rotor.Build("TFA_DroneRotor");
                var glow = new MeshBuilder();
                glow.Box(new Vector3(0f, 0.08f, 0.182f), new Vector3(0.12f, 0.035f, 0.01f));
                glow.Box(new Vector3(0f, 0.034f, 0f), new Vector3(0.12f, 0.008f, 0.12f));
                _glow = glow.Build("TFA_DroneGlow");
                var beam = new MeshBuilder();
                beam.Box(Vector3.zero, Vector3.one);
                _beam = beam.Build("TFA_DroneBeam");
            }
            int layer = gameObject.layer;
            var drone = new GameObject("TFA_MiningDrone") { layer = layer };
            _drone = drone.transform;
            _drone.SetPositionAndRotation(transform.TransformPoint(Pad), transform.rotation);
            Piece(_drone, _body, LitMaterials.Get("TFA_DroneBody", new Color(0.82f, 0.84f, 0.86f), 0.4f, 0.35f), layer, true);
            Piece(_drone, _glow, LitMaterials.Get("TFA_DroneGlow", new Color(0.3f, 0.9f, 1f), 0f, 0.1f, new Color(0.3f, 0.9f, 1f) * 1.2f), layer, false);
            Material dark = LitMaterials.Get("TFA_DroneRotor", new Color(0.08f, 0.08f, 0.09f), 0.2f, 0.3f);
            int i = 0;
            foreach (float x in new[] { -1f, 1f })
                foreach (float z in new[] { -1f, 1f })
                {
                    Transform rotor = Piece(_drone, _rotor, dark, layer, false);
                    rotor.localPosition = new Vector3(x * 0.3f, 0.14f, z * 0.3f);
                    _rotors[i++] = rotor;
                }
            _beamObject = Piece(null, _beam, LitMaterials.Get("TFA_DroneBeam", new Color(0.4f, 0.95f, 1f), 0f, 0.1f, new Color(0.4f, 0.95f, 1f) * 1.3f), layer, false);
            _beamObject.SetParent(_drone, true);
            _beamObject.gameObject.SetActive(false);
        }

        private static Transform Piece(Transform parent, Mesh mesh, Material material, int layer, bool shadows)
        {
            var piece = new GameObject("TF_NoPaint_Drone") { layer = layer };
            if (parent != null)
                piece.transform.SetParent(parent, false);
            piece.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = piece.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return piece.transform;
        }
    }
}

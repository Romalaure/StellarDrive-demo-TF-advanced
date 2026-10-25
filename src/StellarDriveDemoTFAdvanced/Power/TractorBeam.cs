using System;
using System.Collections.Generic;
using System.Linq;
using Core.Services;
using FishNet.Connection;
using HarmonyLib;
using Players.Interface.Model;
using Ships;
using Ships.Cables;
using Ships.Cables.Power;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Joints;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Interface.Model.State;
using Ships.Interface.Settings;
using Ships.Parts.Common;
using Ships.Physics;
using Ships.Services;
using StellarDriveDemoTFAdvanced.Common;
using StellarDriveDemoTFAdvanced.Parts;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using UnityEngine;
using UnityEngine.Rendering;
using Loc = StellarDriveDemoTFAdvanced.Common.Loc;

namespace StellarDriveDemoTFAdvanced.Power
{
    /// <summary>
    /// The tractor beam: an emitter dish on the hull whose beam leaves it along +Y (out of the
    /// surface it is placed on). Switched on (use key T on it, or the tractor key, Y, from the
    /// pilot seat) and powered, it locks onto the nearest ship in its beam (another ship, a wreck,
    /// a pirate) within <see cref="Range"/> and pulls it in to <see cref="HoldDistance"/> in front
    /// of the dish, then holds it there: the target's speed is matched to the emitter's and its
    /// spin damped, with a force capped by <see cref="MaxForce"/> (a heavy ship comes slowly).
    /// Command socket: with a cable, it only works while the signal is above 0. The host runs it;
    /// the ConstantState value is (target × 10 + status) × 2 + switched on.
    /// </summary>
    internal static class TractorBeam
    {
        public const ushort Id = 7486;
        /// <summary>The hangar's crane arm: the same beam, short (<see cref="CraneRange"/>), holding close under its hook.</summary>
        public const ushort CraneId = 8270;
        public const byte CommandSocket = 3;
        public const float Range = 150f, HoldDistance = 14f, MaxForce = 800000f, MaxAcceleration = 12f, Watts = 3000f;
        public const float CraneRange = 30f, CraneHold = 3f, CraneWatts = 1200f;

        public static bool IsBeam(ushort id) => id == Id || id == CraneId;

        private static float RangeOf(ushort id) => id == CraneId ? CraneRange : Range;

        private static float HoldOf(ushort id) => id == CraneId ? CraneHold : HoldDistance;
        // Status
        public const int Off = 0, Searching = 1, Locked = 2, NoPower = 3;

        public static readonly Vector3 Muzzle = new Vector3(0f, 0.62f, 0f);

        public static (bool On, int Status, uint Target) Decode(double value)
        {
            long v = (long)Math.Round(Math.Max(0.0, value));
            long rest = v / 2;
            return (v % 2 == 1, (int)(rest % 10), (uint)(rest / 10));
        }

        public static double Encode(bool on, int status, uint target) => ((double)target * 10 + status) * 2 + (on ? 1 : 0);

        // ---- Part

        public static void Register()
        {
            PartFactory.Register(Id, "SignalDisplay", AdvancedTab.Name, AdvancedTab.ArtifactsRow, (settings, prefab) => Configure(Id, settings, prefab));
            PartFactory.Register(CraneId, "SignalDisplay", AdvancedTab.ConstructionName, AdvancedTab.HangarRow, (settings, prefab) => Configure(CraneId, settings, prefab));
        }

        private static void Configure(ushort id, PartSettings settings, GameObject prefab)
        {
            if (id == CraneId)
                PartFactory.Describe(settings, Loc.Pick("Bras de grue", "Crane arm", "Brazo grúa"),
                    Loc.Pick($"Un treuil à crochet pour le plafond du hangar : allumé (regarde-le et appuie sur T) et alimenté ({CraneWatts:0} W), il accroche le vaisseau sous lui jusqu'à {CraneRange:0} m, le monte à {CraneHold:0} m sous le crochet et l'y tient. Branché comme le rayon tracteur.",
                        $"A hook winch for the hangar's ceiling: switched on (look at it and press T) and powered ({CraneWatts:0} W), it hooks the ship under it up to {CraneRange:0} m away, lifts it to {CraneHold:0} m under the hook and holds it there. Wired like the tractor beam.",
                        $"Un torno con gancho para el techo del hangar: encendido (míralo y pulsa T) y con energía ({CraneWatts:0} W), engancha la nave bajo él hasta {CraneRange:0} m, la sube a {CraneHold:0} m bajo el gancho y la sujeta. Se conecta como el rayo tractor."),
                    40f, new[] { (101u, 8), (112u, 6), (103u, 2) });
            else
                PartFactory.Describe(settings, Loc.Pick("Rayon tracteur", "Tractor beam", "Rayo tractor"),
                Loc.Pick($"Un émetteur à fixer sur la coque : son rayon part tout droit de la surface où on le pose. Allumé (regarde-le et appuie sur T, ou touche Y depuis le siège de pilotage) et alimenté ({Watts:0} W), il accroche le vaisseau le plus proche dans son rayon (vaisseau, épave, pirate) jusqu'à {Range:0} m, le tire jusqu'à {HoldDistance:0} m devant lui et le garde là, à ta vitesse. Un vaisseau lourd vient lentement. Éteins-le pour le lâcher. Prise de commande : avec un câble, il ne marche que si le signal est au-dessus de 0. Prise d'état : 2 accroché, 1 cherche, 0 éteint, 3 sans courant.",
                    $"An emitter that bolts to the hull: its beam goes straight out of the surface it is placed on. Switched on (look at it and press T, or the Y key from the pilot seat) and powered ({Watts:0} W), it locks onto the nearest ship in its beam (a ship, a wreck, a pirate) up to {Range:0} m, pulls it in to {HoldDistance:0} m in front of it and holds it there, at your speed. A heavy ship comes slowly. Switch it off to let go. Command socket: with a cable it only works while the signal is above 0. Status socket: 2 locked, 1 searching, 0 off, 3 no power.",
                    $"Un emisor que se fija al casco: su rayo sale recto de la superficie donde se coloca. Encendido (míralo y pulsa T, o la tecla Y desde el asiento de pilotaje) y con energía ({Watts:0} W), engancha la nave más cercana en su rayo (nave, pecio, pirata) hasta {Range:0} m, la atrae hasta {HoldDistance:0} m delante de él y la mantiene ahí, a tu velocidad. Una nave pesada llega despacio. Apágalo para soltarla. Toma de mando: con un cable solo funciona si la señal es mayor que 0. Toma de estado: 2 enganchado, 1 busca, 0 apagado, 3 sin energía."),
                90f, new[] { (Items.ModItems.Titanium, 6), (Items.ModItems.Cryonite, 3), (112u, 10), (102u, 4) });
            settings.internalStateClass = PowerCatalog.MachineStateClass;
            settings.snappingStyle = Ships.Interface.Model.SnappingStyle.PreciseOnAny;
            const float width = 0.8f;
            Transform visuals = PartFactory.BlankFromSignalDisplay(prefab, new Vector3(0f, 0.32f, 0f), new Vector3(width, 0.64f, width));
            // Its Blender model (tools/blender/construction.py); the shapes below are the fallback
            if (!Construction.MaterialParts.BlenderModel(id, visuals) && id == Id)
            {
                // A square foot, a turning ring, a dish of coils around a central emitter
                var model = new MachineModels.Model();
                model.Armor.Box(new Vector3(0f, 0.06f, 0f), new Vector3(width, 0.12f, width));
                model.Dark.Box(new Vector3(0f, 0.125f, 0f), new Vector3(width - 0.08f, 0.01f, width - 0.08f));
                model.Armor.Cylinder(new Vector3(0f, 0.12f, 0f), new Vector3(0f, 0.22f, 0f), 0.3f, 0.26f, 24);
                model.Glow.Cylinder(new Vector3(0f, 0.22f, 0f), new Vector3(0f, 0.235f, 0f), 0.26f, 0.26f, 24);
                // The dish: a cone opening upward, its rim armored
                model.Dark.Cylinder(new Vector3(0f, 0.235f, 0f), new Vector3(0f, 0.42f, 0f), 0.16f, 0.38f, 28, capA: true, capB: false);
                model.Armor.Cylinder(new Vector3(0f, 0.42f, 0f), new Vector3(0f, 0.46f, 0f), 0.39f, 0.39f, 28, capA: false, capB: false);
                for (int i = 0; i < 8; i++)
                {
                    Quaternion turn = Quaternion.AngleAxis(i * 45f, Vector3.up);
                    // Coils around the dish, pointing at the emitter
                    model.Core.Cylinder(turn * new Vector3(0.3f, 0.44f, 0f), turn * new Vector3(0.24f, 0.52f, 0f), 0.03f, 0.022f, 10);
                    model.Armor.Box(turn * new Vector3(0.36f, 0.33f, 0f), new Vector3(0.05f, 0.18f, 0.05f), turn);
                }
                // The emitter: a rod with three rings and a lit tip
                model.Armor.Cylinder(new Vector3(0f, 0.25f, 0f), new Vector3(0f, 0.56f, 0f), 0.05f, 0.04f, 14);
                foreach (float y in new[] { 0.34f, 0.42f, 0.5f })
                    model.Glow.Cylinder(new Vector3(0f, y, 0f), new Vector3(0f, y + 0.015f, 0f), 0.07f, 0.07f, 16);
                model.Light.Cylinder(new Vector3(0f, 0.56f, 0f), new Vector3(0f, 0.6f, 0f), 0.045f, 0.02f, 14);
                MachineModels.Attach(visuals, model, "TractorBeam", MachineModels.Green, new Color(0.6f, 1f, 0.75f));
            }
            TFItems.Common.UsablePart.AddBox(visuals, new Vector3(0f, 0.32f, 0f), new Vector3(width + 0.1f, 0.7f, width + 0.1f));
            if (id == CraneId)
                visuals.gameObject.AddComponent<CraneVisuals>();
            else
                visuals.gameObject.AddComponent<TractorVisuals>();
            var bounds = prefab.GetComponent<ShipPartBounds>();
            PowerParts.GraftRow(prefab, bounds, 0.06f, -width * 0.5f, 0.12f,
                (PowerCatalog.MachinePowerA, CableType.Power), (PowerCatalog.MachineSignal, CableType.Signal), (CommandSocket, CableType.Signal), (PowerCatalog.MachinePowerB, CableType.Power));
        }

        // ---- Switching (players ask the host)

        public static void Install() => TFNet.OnServer(TFMessageKind.TractorSwitch, OnSwitch);

        public static void Switch(uint shipId, ushort partId, bool on) =>
            TFNet.SendToServer(TFMessageKind.TractorSwitch, shipId, partId, on ? "1" : "0");

        private static void OnSwitch(NetworkConnection sender, TFMessageKind kind, uint shipId, ushort partId, string text)
        {
            var ships = StellarDriveDemoTFAdvanced.Common.GameServices.ShipsServer;
            if (ships == null || !ships.TryGetTrackedShip(shipId, out TrackedShipServer ship))
                return;
            bool on = text == "1";
            foreach (StatefulPart part in ship.GetAllStatefulParts<ConstantState>().ToList())
            {
                if (part.Settings == null || !IsBeam(part.Settings.id) || partId != 0 && part.Id != partId)
                    continue;
                var state = (ConstantState)part.State;
                (bool _, int status, uint target) = Decode(state.ConstantValue);
                ship.UpdatePartState(part.Id, state with { ConstantValue = Encode(on, on ? status : Off, on ? target : 0) });
            }
        }

        // ---- Host simulation

        internal sealed class Runtime
        {
            public PowerGroupServerState Group;
            public bool Commanded;
            public uint Target;
            public float Hold;
            public float Supply;
            public double Written = double.NaN;
        }

        internal struct Entry
        {
            public TrackedShipServer Ship;
            public StatefulPart Part;
            public Runtime Runtime;
        }

        private static readonly Dictionary<(uint, ushort), Runtime> Runtimes = new Dictionary<(uint, ushort), Runtime>();
        private static readonly List<Entry> Beams = new List<Entry>();
        /// <summary>Target root ship → the beams holding it (read by the physics step).</summary>
        internal static readonly Dictionary<uint, List<Entry>> Pulls = new Dictionary<uint, List<Entry>>();

        public static void Forget()
        {
            Runtimes.Clear();
            Beams.Clear();
            Pulls.Clear();
        }

        public static void BeforeTransfers(PowerServerTracker power, ShipsServerTracker ships, ConnectionsServerTracker connections, float deltaTime)
        {
            Beams.Clear();
            foreach (TrackedShipServer ship in ships.LoadedShipsWithChildren)
            {
                foreach (StatefulPart part in ship.GetAllStatefulParts<ConstantState>())
                {
                    if (part.Settings == null || !IsBeam(part.Settings.id))
                        continue;
                    if (!Runtimes.TryGetValue((ship.Id, part.Id), out Runtime rt))
                        Runtimes[(ship.Id, part.Id)] = rt = new Runtime();
                    rt.Group = null;
                    Beams.Add(new Entry { Ship = ship, Part = part, Runtime = rt });
                    var command = new EntityPartSocket(ship.Id, part.Id, CommandSocket);
                    rt.Commanded = Decode(((ConstantState)part.State).ConstantValue).On
                        && (!connections.IsSocketPhysicallyOccupied(command) || connections.GetSignalValue(command) > 0.0);
                    if (!rt.Commanded)
                        continue;
                    if (!power.TryGetGroupFromSocket(new EntityPartSocket(ship.Id, part.Id, PowerCatalog.MachinePowerA), out PowerGroupServerState group)
                        && !power.TryGetGroupFromSocket(new EntityPartSocket(ship.Id, part.Id, PowerCatalog.MachinePowerB), out group))
                        continue;
                    group.TotalPowerNeeded += (part.Settings.id == CraneId ? CraneWatts : Watts) * deltaTime;
                    rt.Group = group;
                }
            }
        }

        public static void AfterTransfers(ShipsServerTracker ships)
        {
            Pulls.Clear();
            foreach (Entry entry in Beams)
            {
                Runtime rt = entry.Runtime;
                var state = (ConstantState)entry.Part.State;
                bool on = Decode(state.ConstantValue).On;
                int status;
                try
                {
                    rt.Supply = rt.Group != null ? Mathf.Clamp01(rt.Group.NormalizedConsumerRate) : 0f;
                    if (!rt.Commanded)
                        status = Off;
                    else if (rt.Supply < 0.5f)
                        status = NoPower;
                    else
                        status = Aim(ships, entry) ? Locked : Searching;
                }
                catch (Exception e)
                {
                    ErrorLog.Report("tractor beam", e);
                    status = Off;
                }
                if (status != Locked)
                    rt.Target = 0;
                else
                {
                    if (!Pulls.TryGetValue(rt.Target, out List<Entry> list))
                        Pulls[rt.Target] = list = new List<Entry>();
                    list.Add(entry);
                }
                double value = Encode(on, status, rt.Target);
                if (value == rt.Written && value == state.ConstantValue)
                    continue;
                rt.Written = value;
                if (value != state.ConstantValue)
                    entry.Ship.UpdatePartState(entry.Part.Id, state with { ConstantValue = value });
            }
        }

        /// <summary>The muzzle and the beam's direction, in the emitter ship's space.</summary>
        internal static void Ray(TrackedShipServer ship, StatefulPart part, out Vector3 origin, out Vector3 direction)
        {
            origin = ship.Position + ship.Rotation * (part.LocalPosition + part.LocalRotation * Muzzle);
            direction = ship.Rotation * (part.LocalRotation * Vector3.up);
        }

        private static Vector3 Center(TrackedShipServer ship) => ship.Position + ship.Rotation * ship.CenterOfMass;

        // Keeps the target while it stays in the beam (wider), else the nearest root ship in it
        private static bool Aim(ShipsServerTracker ships, Entry entry)
        {
            Runtime rt = entry.Runtime;
            float range = RangeOf(entry.Part.Settings.id), hold = HoldOf(entry.Part.Settings.id);
            Ray(entry.Ship, entry.Part, out Vector3 origin, out Vector3 direction);
            var own = new HashSet<uint>();
            if (ships.TryGetTrackedRootShip(entry.Ship.Id, out TrackedShipServer root))
                foreach (RootRelativeShipData data in ships.GetRootConnectedShips(root.Id))
                    if (data.Ship != null)
                        own.Add(data.Ship.Id);
            own.Add(entry.Ship.Id);
            bool InBeam(TrackedShipServer ship, float cone, float range, out float distance)
            {
                distance = 0f;
                // shortcut: same space only (near ships share one), a ship in another space is out of reach
                if (ship == null || own.Contains(ship.Id) || ship.SpaceId != entry.Ship.SpaceId)
                    return false;
                Vector3 to = Center(ship) - origin;
                distance = to.magnitude;
                return distance < range && distance > 0.5f && Vector3.Angle(direction, to) < cone;
            }
            if (rt.Target != 0 && ships.TryGetTrackedShip(rt.Target, out TrackedShipServer held) && InBeam(held, 40f, range * 1.3f, out float now))
            {
                // Pulled in to the hold distance, a few meters a second
                rt.Hold = Mathf.Max(hold, Mathf.Min(rt.Hold, now + 2f) - 4f * Time.fixedDeltaTime);
                return true;
            }
            TrackedShipServer best = null;
            float bestDistance = float.MaxValue;
            foreach (TrackedShipServer ship in ships.LoadedRootShips)
            {
                if (InBeam(ship, 12f, range, out float distance) && distance < bestDistance)
                {
                    best = ship;
                    bestDistance = distance;
                }
            }
            if (best == null)
                return false;
            rt.Target = best.Id;
            rt.Hold = Mathf.Max(hold, bestDistance);
            AdvancedMod.Log.Msg($"tractor beam {entry.Ship.Id}:{entry.Part.Id} locked on ship {best.Id} at {bestDistance:0} m");
            return true;
        }

        /// <summary>The pull on a held ship's rigidbody (host physics, every step).</summary>
        internal static void Pull(IShipStateWrite target, Rigidbody body, ShipsServerTracker ships)
        {
            if (!Pulls.TryGetValue(target.Id, out List<Entry> beams) || body == null || body.isKinematic)
                return;
            foreach (Entry entry in beams)
            {
                if (entry.Ship == null || entry.Part == null || entry.Ship.SpaceId != target.PhysicsState.SpaceId)
                    continue;
                Ray(entry.Ship, entry.Part, out Vector3 origin, out Vector3 direction);
                Vector3 goal = origin + direction * entry.Runtime.Hold;
                Vector3 center = body.worldCenterOfMass;
                Vector3 emitterVelocity = ships.TryGetTrackedRootShip(entry.Ship.Id, out TrackedShipServer root) ? root.PhysicsState.Velocity : entry.Ship.PhysicsState.Velocity;
                // A critically damped spring toward the hold point, relative to the emitter's motion
                Vector3 accel = (goal - center) * 0.5f + (emitterVelocity - body.velocity) * 1.4f;
                float cap = Mathf.Min(MaxForce / Mathf.Max(1f, body.mass), MaxAcceleration) * entry.Runtime.Supply;
                body.AddForce(Vector3.ClampMagnitude(accel, cap), ForceMode.Acceleration);
                body.AddTorque(Vector3.ClampMagnitude(-body.angularVelocity * 1.5f, cap * 0.2f), ForceMode.Acceleration);
            }
        }
    }

    [HarmonyPatch(typeof(ShipServerPhysics), "UpdateRootRigidbody")]
    internal static class TractorBeamPhysicsPatch
    {
        private static readonly AccessTools.FieldRef<ShipRigidbody, Rigidbody> Body = AccessTools.FieldRefAccess<ShipRigidbody, Rigidbody>("_rigidbody");
        private static ShipsServerTracker _ships;

        private static void Postfix(IShipStateWrite ____ship, ShipRigidbody ____shipRigidbody)
        {
            try
            {
                if (____ship == null || ____shipRigidbody == null || TractorBeam.Pulls.Count == 0 || !TractorBeam.Pulls.ContainsKey(____ship.Id))
                    return;
                if (_ships == null)
                    _ships = ServiceLocator.GetService<ShipsServerTracker>();
                if (_ships != null)
                    TractorBeam.Pull(____ship, Body(____shipRigidbody), _ships);
            }
            catch (Exception e)
            {
                ErrorLog.Report("tractor beam pull", e);
            }
        }
    }

    /// <summary>
    /// The tractor beam on screen: a green beam from the dish to the ship it holds (rings running
    /// back along it), a faint short one while it searches; the use key switches it. From the
    /// pilot seat, the tractor key switches every beam of the ship flown.
    /// </summary>
    internal class TractorVisuals : TFItems.Common.UsablePart
    {
        private static readonly Color BeamColor = new Color(0.35f, 1f, 0.6f);
        private static Material _beamMaterial;
        private static Mesh _beamMesh;
        public static readonly Dictionary<PartKey, double> Values = new Dictionary<PartKey, double>();

        protected double _value;
        private Transform _beam, _ring;

        public override string Label => Loc.Pick("Rayon tracteur", "Tractor beam", "Rayo tractor") + (TractorBeam.Decode(_value).On ? Loc.Pick(" (allumé)", " (on)", " (encendido)") : Loc.Pick(" (éteint)", " (off)", " (apagado)"));

        public override void Use() => TractorBeam.Switch(Key.ShipId, Key.PartId, !TractorBeam.Decode(_value).On);

        public override void SetNetworkState(IStatefulPartState state)
        {
            if (state is ConstantState constant)
                _value = constant.ConstantValue;
        }

        public override void ReactToNetworkStateChange(StatefulPartStateChangeEvent change) => SetNetworkState(change.NewState);

        private void LateUpdate()
        {
            if (!HasContext)
                return;
            Values[Key] = _value;
            (bool on, int status, uint target) = TractorBeam.Decode(_value);
            bool shown = on && (status == TractorBeam.Locked || status == TractorBeam.Searching);
            if (!shown)
            {
                if (_beam != null)
                    _beam.gameObject.SetActive(false);
                return;
            }
            if (_beam == null && !Build())
                return;
            Vector3 muzzle = transform.TransformPoint(TractorBeam.Muzzle);
            float length = 6f;
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 7f);
            Vector3 direction = transform.up;
            var ships = TFItems.Common.GameServices.ShipsClient;
            if (status == TractorBeam.Locked && ships != null && ships.TryGetTrackedShip(target, out var held) && held != null
                && ships.TryGetShipVisualTransform(target, out Transform heldTransform) && heldTransform != null)
            {
                Vector3 to = heldTransform.TransformPoint(held.CenterOfMass) - muzzle;
                length = to.magnitude;
                if (length > 0.1f)
                    direction = to / length;
            }
            else
            {
                pulse *= 0.4f;
            }
            _beam.gameObject.SetActive(true);
            _beam.SetPositionAndRotation(muzzle, Quaternion.FromToRotation(Vector3.up, direction));
            float width = (status == TractorBeam.Locked ? 0.7f : 0.25f) * pulse;
            _beam.localScale = new Vector3(width, length / Mathf.Max(0.001f, transform.lossyScale.y), width);
            // A ring running back along the beam toward the dish
            float t = 1f - Mathf.Repeat(Time.time * 0.6f, 1f);
            _ring.localPosition = new Vector3(0f, t, 0f);
            _ring.localScale = new Vector3(1.6f, 0.02f, 1.6f);
        }

        private bool Build()
        {
            if (_beamMaterial == null)
            {
                _beamMaterial = TransparentMaterials.Additive("TFA_TractorBeam", null);
                if (_beamMaterial == null)
                    return false;
                _beamMaterial.SetColor("_BaseColor", BeamColor * 0.9f);
                _beamMesh = Tube();
            }
            var beam = new GameObject("TF_NoPaint_TractorBeam") { layer = gameObject.layer };
            beam.transform.SetParent(transform, false);
            beam.AddComponent<MeshFilter>().sharedMesh = _beamMesh;
            var renderer = beam.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _beamMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var ring = new GameObject("TF_NoPaint_TractorRing") { layer = gameObject.layer };
            ring.transform.SetParent(beam.transform, false);
            ring.AddComponent<MeshFilter>().sharedMesh = _beamMesh;
            var ringRenderer = ring.AddComponent<MeshRenderer>();
            ringRenderer.sharedMaterial = _beamMaterial;
            ringRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _beam = beam.transform;
            _ring = ring.transform;
            return true;
        }

        // An open tube one unit long along +Y, one unit across, its vertex alpha fading toward the far end
        private static Mesh Tube()
        {
            const int sides = 16;
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            for (int i = 0; i <= sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                var round = new Vector3(Mathf.Cos(a) * 0.5f, 0f, Mathf.Sin(a) * 0.5f);
                vertices.Add(round);
                vertices.Add(round + Vector3.up);
                colors.Add(new Color(1f, 1f, 1f, 0.55f));
                colors.Add(new Color(1f, 1f, 1f, 0.2f));
                if (i < sides)
                {
                    int at = i * 2;
                    triangles.AddRange(new[] { at, at + 1, at + 2, at + 2, at + 1, at + 3 });
                    triangles.AddRange(new[] { at, at + 2, at + 1, at + 2, at + 3, at + 1 });
                }
            }
            var mesh = new Mesh { name = "TFA_TractorTube" };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.bounds = new Bounds(new Vector3(0f, 0.5f, 0f), Vector3.one);
            return mesh;
        }
    }

    /// <summary>The tractor key from the pilot seat: every tractor beam of the ship flown, on or off.</summary>
    internal static class TractorKey
    {
        private static readonly KeySetting Key = new KeySetting(() => TFItems.Common.Settings.TractorKey);

        public static void Update()
        {
            PlayerState state = TFItems.Common.GameServices.LocalState;
            if (state.Type != PlayerStateType.Piloting || ModMenu.IsOpen || !Key.WasPressed)
                return;
            uint ship = state.EntityId;
            bool any = TractorVisuals.Values.Any(v => v.Key.ShipId == ship && TractorBeam.Decode(v.Value).On);
            if (!TractorVisuals.Values.Keys.Any(k => k.ShipId == ship))
                return;
            TractorBeam.Switch(ship, 0, !any);
        }
    }

    /// <summary>The crane arm: the tractor beam's work under another name.</summary>
    internal sealed class CraneVisuals : TractorVisuals
    {
        public override string Label => Loc.Pick("Bras de grue", "Crane arm", "Brazo grúa") + (TractorBeam.Decode(_value).On ? Loc.Pick(" (allumé)", " (on)", " (encendido)") : Loc.Pick(" (éteint)", " (off)", " (apagado)"));
    }
}

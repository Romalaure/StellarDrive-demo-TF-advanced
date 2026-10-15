using System.Collections.Generic;
using System.Linq;
using Core.Services;
using Core.Values;
using Planets.Interface.Model;
using Planets.Interface.Services;
using Ships;
using Ships.Cables;
using Ships.Cables.Fluids;
using Ships.Cables.Power;
using Ships.Interface.Model;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.State;
using Ships.Parts.Common;
using Ships.Services;
using Ships.Interface.Services;
using StellarDriveDemoTFAdvanced.Common;
using StellarDriveDemoTFAdvanced.Items;
using UnityEngine;
using UnityEngine.Rendering;
using WorldTracking.Interface.Model;
using WorldTracking.Interface.Model.Spaces;
using WorldTracking.Interface.Model.Spaces.Common;
using Ships.Interface.Model.Parts.StateTypes;
using WorldTracking.Interface.Services;
using WorldTracking.Interface.Values;
using WorldTracking.Spaces;

namespace StellarDriveDemoTFAdvanced.Power
{
    /// <summary>
    /// Portal generator (host simulation). A signal above 0 on its command socket starts it: it
    /// charges for <see cref="PowerCatalog.PortalChargeSeconds"/> on its power sockets, burns
    /// dark matter from its fluid socket and opens a portal straight in front of it, fixed in
    /// space. The ship (with everything docked to it) that flies its center through the portal
    /// comes out next to the target chosen on its targeting computer or star map: a planet
    /// (high above it, on the side it came from) or a beacon (a few hundred meters away). The
    /// jump uses the game's own warp space: the ship enters it, is moved, and leaves it like at
    /// the end of a warp.
    /// The state is one value in the part's ConstantState, emitted on socket 0: 0 ready, 1-99
    /// charging (%), 100 open, -1 no target, -2 not enough dark matter, -3 no power,
    /// -(10 + seconds) cooling down.
    /// </summary>
    internal static class Portal
    {
        public const byte CommandSocket = 4;
        public const double Open = 100, NoTarget = -1, NoDarkMatter = -2, NoPower = -3;

        private enum Phase { Idle, Charging, Open, Cooling }

        private sealed class Runtime
        {
            public Phase Phase;
            public float Charge;
            public float Until;
            public bool Command;
            public double Problem;
            public PowerGroupServerState Group;
            public FluidGroupServerState Line;
            // Where the portal stands, in the space it opened in
            public uint SpaceId;
            public Vector3 Center;
            public Vector3 Normal;
            public float LastSide;
            public float LastWrite = -10f;
            public double Written = double.NaN;
        }

        private struct Entry
        {
            public TrackedShipServer Ship;
            public StatefulPart Part;
            public Runtime Runtime;
        }

        private static readonly Dictionary<(uint, ushort), Runtime> Runtimes = new Dictionary<(uint, ushort), Runtime>();
        private static readonly List<Entry> Generators = new List<Entry>();

        public static void Forget()
        {
            Runtimes.Clear();
            Generators.Clear();
        }

        public static void BeforeTransfers(PowerServerTracker power, ShipsServerTracker ships, ConnectionsServerTracker connections, float deltaTime)
        {
            Generators.Clear();
            float now = Time.unscaledTime;
            foreach (TrackedShipServer ship in ships.LoadedShipsWithChildren)
            {
                foreach (StatefulPart part in ship.GetAllStatefulParts<ConstantState>())
                {
                    if (part.Settings.id != PowerCatalog.PortalId)
                        continue;
                    if (!Runtimes.TryGetValue((ship.Id, part.Id), out Runtime rt))
                        Runtimes[(ship.Id, part.Id)] = rt = new Runtime();
                    rt.Group = null;
                    rt.Line = null;
                    var entry = new Entry { Ship = ship, Part = part, Runtime = rt };
                    Generators.Add(entry);

                    bool command = connections.GetSignalValue(new EntityPartSocket(ship.Id, part.Id, CommandSocket)) > 0.0;
                    bool start = command && !rt.Command;
                    rt.Command = command;
                    if (rt.Phase == Phase.Cooling && now >= rt.Until)
                        rt.Phase = Phase.Idle;
                    if (rt.Phase == Phase.Open && now >= rt.Until)
                        Close(rt, now);
                    if (rt.Phase == Phase.Idle && start)
                    {
                        rt.Problem = 0;
                        if (!Destination(ships, ship, out _, out _, out _))
                            rt.Problem = NoTarget;
                        else
                        {
                            rt.Phase = Phase.Charging;
                            rt.Charge = 0f;
                        }
                    }
                    if (rt.Phase == Phase.Charging && !command)
                    {
                        rt.Phase = Phase.Idle;
                        rt.Charge = 0f;
                    }
                    if (rt.Phase != Phase.Charging)
                        continue;
                    if (!power.TryGetGroupFromSocket(new EntityPartSocket(ship.Id, part.Id, PowerCatalog.MachinePowerA), out PowerGroupServerState group)
                        && !power.TryGetGroupFromSocket(new EntityPartSocket(ship.Id, part.Id, PowerCatalog.MachinePowerB), out group))
                    {
                        rt.Problem = NoPower;
                        continue;
                    }
                    group.TotalPowerNeeded += Mathf.Max(0f, PowerCatalog.PortalWatts.Value) * deltaTime;
                    rt.Group = group;
                    connections.TryGetFluidGroupCacheFromSocket(new EntityPartSocket(ship.Id, part.Id, PowerCatalog.MachineFluid), out FluidGroupServerState line);
                    rt.Line = line;
                }
            }
        }

        public static void AfterTransfers(float deltaTime)
        {
            float now = Time.unscaledTime;
            foreach (Entry entry in Generators)
            {
                Runtime rt = entry.Runtime;
                if (rt.Phase == Phase.Charging && rt.Group != null)
                {
                    if (rt.Group.NormalizedConsumerRate > 0.95f)
                    {
                        rt.Problem = 0;
                        rt.Charge += deltaTime;
                    }
                    else
                        rt.Problem = NoPower;
                    if (rt.Charge >= Mathf.Max(0.5f, PowerCatalog.PortalChargeSeconds.Value))
                    {
                        float fuel = Mathf.Max(0f, PowerCatalog.PortalDarkMatter.Value);
                        if (rt.Line == null || rt.Line.FluidItemId != ModItems.DarkMatter || rt.Line.Available < fuel)
                        {
                            rt.Problem = NoDarkMatter;
                            rt.Phase = Phase.Idle;
                            rt.Charge = 0f;
                        }
                        else if (OpenPortal(entry, now))
                        {
                            rt.Line.RemoveFluid(fuel);
                        }
                    }
                }
                Write(entry, now);
            }
        }

        private static bool OpenPortal(Entry entry, float now)
        {
            Runtime rt = entry.Runtime;
            var transpose = ServiceLocator.GetService<IWorldTransposeServer>();
            var spaces = ServiceLocator.GetService<ISpacesServerProvider>();
            if (transpose == null || spaces == null)
                return false;
            // The portal stands in the space of the whole ship (not in a docked ship's own frame)
            var ships = ServiceLocator.GetService<ShipsServerTracker>();
            uint space = ships != null && ships.TryGetRootShip(entry.Ship.Id, out IShipStateRead root) ? root.SpaceId : entry.Ship.SpaceId;
            Vector3 partPosition = entry.Ship.Position + entry.Ship.Rotation * entry.Part.LocalPosition;
            Vector3 forward = entry.Ship.Rotation * (entry.Part.LocalRotation * Vector3.forward);
            float distance = Mathf.Max(5f, PowerCatalog.PortalDistance.Value);
            Vector3 center = partPosition + forward * distance;
            rt.SpaceId = space;
            rt.Center = transpose.Position(entry.Ship.SpaceId, space, center);
            Vector3 tip = transpose.Position(entry.Ship.SpaceId, space, center + forward);
            rt.Normal = (tip - rt.Center).normalized;
            rt.LastSide = float.NaN;
            rt.Phase = Phase.Open;
            rt.Until = now + Mathf.Max(3f, PowerCatalog.PortalOpenSeconds.Value);
            rt.Charge = 0f;
            AdvancedMod.Log.Msg($"portal {entry.Ship.Id}:{entry.Part.Id} opened in space {space} at {rt.Center}");
            return true;
        }

        private static void Close(Runtime rt, float now)
        {
            rt.Phase = Phase.Cooling;
            rt.Until = now + Mathf.Max(1f, PowerCatalog.PortalCooldownSeconds.Value);
        }

        private static void Write(Entry entry, float now)
        {
            Runtime rt = entry.Runtime;
            double value;
            switch (rt.Phase)
            {
                case Phase.Open: value = Open; break;
                case Phase.Cooling: value = -(10 + Mathf.Ceil(rt.Until - now)); break;
                case Phase.Charging: value = rt.Problem != 0 ? rt.Problem : Mathf.Clamp(Mathf.Floor(rt.Charge / Mathf.Max(0.5f, PowerCatalog.PortalChargeSeconds.Value) * 100f), 1f, 99f); break;
                default: value = rt.Problem; break;
            }
            bool kindChanged = double.IsNaN(rt.Written) || value == Open != (rt.Written == Open) || System.Math.Sign(value) != System.Math.Sign(rt.Written);
            if (!kindChanged && (value == rt.Written || now - rt.LastWrite < 0.5f))
                return;
            rt.LastWrite = now;
            rt.Written = value;
            var state = (ConstantState)entry.Part.State;
            entry.Ship.UpdatePartState(entry.Part.Id, state with { ConstantValue = value });
        }

        /// <summary>Host, every frame: ships flying through an open portal jump to their target.</summary>
        public static void Update()
        {
            if (Generators.Count == 0)
                return;
            var ships = ServiceLocator.GetService<ShipsServerTracker>();
            var transpose = ServiceLocator.GetService<IWorldTransposeServer>();
            var spaces = ServiceLocator.GetService<SpacesServer>();
            if (ships == null || transpose == null || spaces == null)
                return;
            float now = Time.unscaledTime;
            foreach (Entry entry in Generators.ToList())
            {
                Runtime rt = entry.Runtime;
                if (rt.Phase != Phase.Open)
                    continue;
                if (!spaces.HasSpace(rt.SpaceId) || !ships.TryGetRootShip(entry.Ship.Id, out IShipStateRead root))
                {
                    Close(rt, now);
                    continue;
                }
                Vector3 position = transpose.Position(root.SpaceId, rt.SpaceId, root.Position);
                Vector3 offset = position - rt.Center;
                float side = Vector3.Dot(offset, rt.Normal);
                float across = (offset - rt.Normal * side).magnitude;
                bool crossed = !float.IsNaN(rt.LastSide) && rt.LastSide < 0f && side >= 0f;
                rt.LastSide = side;
                if (!crossed || across > Mathf.Max(2f, PowerCatalog.PortalRadius.Value))
                    continue;
                try
                {
                    Jump(ships, spaces, transpose, entry, root);
                }
                catch (System.Exception e)
                {
                    ErrorLog.Report("portal", e);
                }
                Close(rt, now);
            }
        }

        private static void Jump(ShipsServerTracker ships, SpacesServer spaces, IWorldTransposeServer transpose, Entry entry, IShipStateRead root)
        {
            if (!Destination(ships, entry.Ship, out PreciseWorldPosition destination, out uint? joinSpace, out string name))
            {
                AdvancedMod.Log.Warning("portal: the target is gone, nothing happens");
                return;
            }
            spaces.CreateWarpSpaceForShip(root.Id, entry.Part.Id);
            uint warp = ships.GetShipPhysicsState(root.Id).SpaceId;
            spaces.UpdateWarpSpace(warp, destination);
            if (joinSpace.HasValue && spaces.TryGetSpace(joinSpace.Value, out ISpace target) && (target is OrbitSpace || target is TrajectorySpace))
                spaces.ExitWarpSpace(warp, joinSpace.Value);
            else
                spaces.ExitWarpSpace(warp);
            AdvancedMod.Log.Msg($"portal: ship {root.Id} went through to {name}");
        }

        /// <summary>Where the ship's target is: a point high above a planet, or near a beacon.</summary>
        private static bool Destination(ShipsServerTracker ships, TrackedShipServer ship, out PreciseWorldPosition destination, out uint? joinSpace, out string name)
        {
            destination = default;
            joinSpace = null;
            name = null;
            ShipTargetState targetState = ship.ShipTargetState;
            if (!targetState.HasTarget && ships.TryGetRootShip(ship.Id, out IShipStateRead root) && root is TrackedShipServer rootShip)
                targetState = rootShip.ShipTargetState;
            if (!targetState.HasTarget)
                return false;
            var transpose = ServiceLocator.GetService<IWorldTransposeServer>();
            var spaces = ServiceLocator.GetService<ISpacesServerProvider>();
            if (transpose == null || spaces == null || !spaces.TryGetSpace(ship.SpaceId, out ISpace here))
                return false;
            PreciseWorldPosition from = transpose.ToPreciseWorldPos(here) + (Vector3Double)ship.Position;
            Target target = targetState.Target;
            if (target.TargetType == TargetType.Planet)
            {
                IPlanetProperties planet = ServiceLocator.GetService<IPlanetsServerProvider>()?.Planets.FirstOrDefault(p => p.Id == target.PlanetId);
                if (planet == null)
                    return false;
                var center = new PreciseWorldPosition(planet.WorldCell, Vector3.zero);
                Vector3Double away = from.DeltaDouble(center);
                Vector3 direction = away.Magnitude > 1.0 ? ((Vector3)away).normalized : Vector3.up;
                destination = center + (Vector3Double)(direction * (planet.Radius + Mathf.Max(6000f, planet.Radius * 0.06f)));
                name = "planet " + planet.Id;
                return true;
            }
            if (!ships.TryGetTrackedShip(target.Beacon.ShipId, out TrackedShipServer beacon))
                return false;
            uint beaconSpace = beacon.SpaceId;
            Vector3 beaconPosition = beacon.Position;
            if (spaces.GetSpace(beaconSpace) is ShipRelativeSpace && ships.TryGetRootShip(beacon.Id, out IShipStateRead beaconRoot))
            {
                beaconPosition = transpose.Position(beaconSpace, beaconRoot.SpaceId, beaconPosition);
                beaconSpace = beaconRoot.SpaceId;
            }
            if (!spaces.TryGetSpace(beaconSpace, out ISpace there))
                return false;
            PreciseWorldPosition at = transpose.ToPreciseWorldPos(there) + (Vector3Double)beaconPosition;
            Vector3Double back = from.DeltaDouble(at);
            Vector3 offset = back.Magnitude > 1.0 ? ((Vector3)back).normalized * 300f : Vector3.up * 300f;
            destination = at + (Vector3Double)offset;
            joinSpace = beaconSpace;
            name = "beacon of ship " + beacon.Id;
            return true;
        }
    }
}

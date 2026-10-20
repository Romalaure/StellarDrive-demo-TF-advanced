using System.Collections.Generic;
using Core.Services;
using HarmonyLib;
using Ships.Cables.Fluids;
using Ships.Interface.Model.Joints;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Placement;
using Ships.Interface.Model.State;
using Ships.Interface.Services;
using Ships.Parts.Thruster;
using Ships.Physics;
using Ships.Physics.Forces;
using Ships.Physics.Model;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Propulsion
{
    /// <summary>
    /// Host-side physics of the Propulsion Pack. The game keeps running its own thruster code for
    /// these engines (they are Chemical Thrusters for it); these patches only correct, for the
    /// pack's part ids, the three things the game hardcodes: propellant demand, propellant use
    /// with spool-up, and thrust. Game thrusters are untouched. All patched methods belong to
    /// classes and return void or a Vector3 from a class method (never a struct's own method).
    /// </summary>
    internal static class ThrustMath
    {
        public static float Thrust(EngineSpec spec, float level) => level * EngineCatalog.BaseThrust * spec.ThrustMultiplier;

        public static Vector3 Direction(StatefulPart part) => -part.Placement.UpOrientation.GetVector3();
    }

    /// <summary>Each engine asks its propellant lines for its own flow instead of the Chemical Thruster's.</summary>
    [HarmonyPatch(typeof(ShipThrustersUpdater), nameof(ShipThrustersUpdater.WriteFluidTargetConsumption))]
    internal static class EngineFuelDemandPatch
    {
        private static void Postfix(ShipThrustersUpdater __instance, float deltaTime, IShipStateWrite ____ship)
        {
            try
            {
                float baseQuantity = EngineCatalog.BaseFlow * deltaTime;
                foreach (StatefulPart part in ____ship.GetAllStatefulParts<ThrusterState>())
                {
                    if (!EngineCatalog.TryGet(part.Settings.id, out EngineSpec spec))
                        continue;
                    if (FluidGroups.TryGet(__instance, part.Id, out FluidGroupServerState a, out FluidGroupServerState b))
                    {
                        // The game already asked for 1x; add the difference (negative for frugal engines)
                        a.AddConsumptionTarget(baseQuantity * (spec.FlowOn(a.FluidItemId) - 1f));
                        b.AddConsumptionTarget(baseQuantity * (spec.FlowOn(b.FluidItemId) - 1f));
                    }
                }
            }
            catch (System.Exception e)
            {
                ErrorLog.Report("engine fuel demand", e);
            }
        }
    }

    /// <summary>
    /// Propellant actually burnt, and spool-up: thrust level moves toward what the lines can feed.
    /// Replaces the game's thruster update (same propellant use for its own thrusters) so each
    /// thruster's state is written once, and only when it changes: the game wrote every thruster
    /// at every physics step, and an engine held at 0 by its pilot seat was written twice a step
    /// (the game's fed level, then 0), each change sent over the network (10 dark matter engines:
    /// ~1 500 writes/s, 20 ms a frame on the host).
    /// </summary>
    [HarmonyPatch(typeof(ShipThrustersUpdater), nameof(ShipThrustersUpdater.Update))]
    internal static class EngineFuelUsePatch
    {
        private static bool Prefix(ShipThrustersUpdater __instance, float deltaTime, IShipStateWrite ____ship, Ships.Cables.ConnectionsServerTracker ____connections)
        {
            if (DevHarness.Off("engines") || ____ship == null)
                return true;
            try
            {
                float gameQuantity = 0.05f * deltaTime;
                float baseQuantity = EngineCatalog.BaseFlow * deltaTime;
                foreach (StatefulPart part in ____ship.GetAllStatefulParts<ThrusterState>())
                {
                    var state = (ThrusterState)part.State;
                    float previous = state.ThrusterStrength;
                    // What the game computes: the share of the demand the lines can feed
                    bool groups = FluidGroups.TryGet(__instance, part.Id, out FluidGroupServerState a, out FluidGroupServerState b);
                    float fed = groups ? Mathf.Min(a.EffectiveConsumptionRate, b.EffectiveConsumptionRate) : 0f;
                    if (groups && fed > 0f)
                    {
                        a.RemoveFluid(fed * gameQuantity);
                        b.RemoveFluid(fed * gameQuantity);
                    }
                    float level = fed;
                    if (EngineCatalog.TryGet(part.Settings.id, out EngineSpec spec))
                    {
                        // Spool-up only slows rising thrust: an engine never burns more than its lines feed it
                        // Nuclear and dark matter engines fed straight from their tanks answer the pilot's
                        // seat: they push only while someone at a seat of the ship asks for thrust their
                        // way. Fed through a valve or a pump, the valve (a seat, a transmitter...) rules
                        // A signal wired to their command socket sets their thrust instead
                        float target = fed;
                        var command = new Ships.Interface.Model.Parts.Common.EntityPartSocket(____ship.Id, part.Id, EngineCatalog.CommandSocket);
                        // (a cable, or a part plugged straight into the socket like a small transmitter)
                        if (spec.Fuel != 0 && ____connections != null && ____connections.TryGetSignalConnectionGroupWithSocket(command, out Ships.Cables.Model.ConnectionGroup commandGroup))
                            target = Mathf.Min(fed, Mathf.Clamp01((float)System.Math.Max(____connections.GetSignalValue(command), Channels.Relayed(____ship, ____connections, commandGroup))));
                        else if (spec.Fuel != 0 && Settings.EngineSeatControl.Value && !(groups && ValveLines.Feeds(____ship, ____connections, a, b)))
                            target = Mathf.Min(fed, PilotControl.Command(____ship, ThrustMath.Direction(part)));
                        level = spec.SpoolSeconds > 0f && target > previous ? Mathf.MoveTowards(previous, target, deltaTime / spec.SpoolSeconds) : target;
                        if (groups)
                        {
                            // The game's share (fed x 1x) is burnt above; the engine burns level x its own flow
                            a.RemoveFluid((level * spec.FlowOn(a.FluidItemId) - fed) * baseQuantity);
                            b.RemoveFluid((level * spec.FlowOn(b.FluidItemId) - fed) * baseQuantity);
                        }
                    }
                    if (Mathf.Abs(level - previous) > 0.0005f || (level == 0f && previous != 0f))
                        ____ship.UpdatePartState(part.Id, state with { ThrusterStrength = level });
                }
            }
            catch (System.Exception e)
            {
                ErrorLog.Report("engine fuel use", e);
            }
            return false;
        }
    }

    /// <summary>Thrust of each engine instead of the Chemical Thruster's 7 800 N.</summary>
    [HarmonyPatch(typeof(ShipThrust), "CalculateShipThrustForce")]
    internal static class EngineThrustPatch
    {
        private static void Postfix(IShipStateRead stateRead, ref Vector3 __result)
        {
            try
            {
                foreach (StatefulPart part in stateRead.GetAllStatefulParts<ThrusterState>())
                {
                    if (!EngineCatalog.TryGet(part.Settings.id, out EngineSpec spec))
                        continue;
                    float level = ((ThrusterState)part.State).ThrusterStrength;
                    // The game already added level x 7 800 N along the same axis
                    __result += ThrustMath.Direction(part) * (ThrustMath.Thrust(spec, level) - level * EngineCatalog.BaseThrust);
                }
                // The compact wood thruster: the game added level x 3 000 N
                foreach (StatefulPart part in stateRead.GetAllStatefulParts<WoodThrusterState>())
                {
                    if (part.Settings.id != EngineParts.WoodCompactId)
                        continue;
                    float level = ((WoodThrusterState)part.State).NormalizedThrustLevel;
                    __result += -part.Placement.UpOrientation.GetVector3() * (level * 3000f * (EngineParts.WoodCompactThrust - 1f));
                }
            }
            catch (System.Exception e)
            {
                ErrorLog.Report("engine thrust", e);
            }
        }
    }

    /// <summary>The compact wood thruster burns in proportion to its thrust: the game took level x 0.05 wood/s, part of it goes back.</summary>
    [HarmonyPatch(typeof(Ships.Parts.WoodThruster.WoodThrustersUpdater), nameof(Ships.Parts.WoodThruster.WoodThrustersUpdater.Update))]
    internal static class CompactWoodBurnPatch
    {
        private static readonly HarmonyLib.AccessTools.FieldRef<Ships.Parts.WoodThruster.WoodThrustersUpdater, IShipStateWrite> Ship =
            HarmonyLib.AccessTools.FieldRefAccess<Ships.Parts.WoodThruster.WoodThrustersUpdater, IShipStateWrite>("_ship");

        private static void Postfix(Ships.Parts.WoodThruster.WoodThrustersUpdater __instance, float deltaTime)
        {
            try
            {
                IShipStateWrite ship = Ship(__instance);
                foreach (StatefulPart part in ship.GetAllStatefulParts<WoodThrusterState>())
                {
                    if (part.Settings.id != EngineParts.WoodCompactId)
                        continue;
                    var state = (WoodThrusterState)part.State;
                    if (state.NormalizedThrustLevel <= 0f)
                        continue;
                    float wood = state.GetWoodAvailable() + state.NormalizedThrustLevel * 0.05f * deltaTime * (1f - EngineParts.WoodCompactThrust);
                    int whole = Mathf.FloorToInt(wood);
                    ship.UpdatePartState(part.Id, state with
                    {
                        ItemSlot = whole > 0 ? new global::Items.Model.InventorySlot { HasItem = true, Quantity = (byte)whole, ItemId = global::Items.Model.ItemsConstants.WoodItemId } : new global::Items.Model.InventorySlot { HasItem = false },
                        PartialItemConsumptionLevel = wood - whole
                    });
                }
            }
            catch (System.Exception e)
            {
                ErrorLog.Report("compact wood thruster", e);
            }
        }
    }

    /// <summary>
    /// The game applies all thrust at the center of mass. Vector and precision engines push where
    /// they are mounted instead: their thrust also gives a real torque, r x F around the center of
    /// mass of the whole docked/rotor assembly, applied to the root's rigidbody.
    /// </summary>
    [HarmonyPatch(typeof(ShipServerPhysics), "UpdateRootRigidbody")]
    internal static class EngineTorquePatch
    {
        private static readonly AccessTools.FieldRef<ShipRigidbody, Rigidbody> Body =
            AccessTools.FieldRefAccess<ShipRigidbody, Rigidbody>("_rigidbody");

        private static IShipsServerProvider _ships;
        private static readonly Dictionary<uint, (float Until, bool Has)> Checked = new Dictionary<uint, (float, bool)>();

        private static void Postfix(ShipPhysicsCalculationProperties props, IShipStateWrite ____ship, ShipRigidbody ____shipRigidbody)
        {
            try
            {
                if (____ship == null || ____shipRigidbody == null || DevHarness.Off("torque"))
                    return;
                if (_ships == null)
                    _ships = ServiceLocator.GetService<IShipsServerProvider>();
                if (_ships == null)
                    return;
                // Most ships have no vector or precision engine: remembered per root, rechecked each second
                float now = Time.unscaledTime;
                if (Checked.TryGetValue(____ship.Id, out var known) && now < known.Until && !known.Has)
                    return;
                Vector3 centerOfMass = props.CenterOfMassIncludingConnectedShips;
                Vector3 torque = Vector3.zero;
                bool any = false;
                foreach (RootRelativeShipData data in _ships.GetRootConnectedShips(____ship.Id))
                {
                    if (data.Ship == null)
                        continue;
                    foreach (StatefulPart part in data.Ship.GetAllStatefulParts<ThrusterState>())
                    {
                        if (!EngineCatalog.TryGet(part.Settings.id, out EngineSpec spec) || !spec.OffCenterThrust)
                            continue;
                        float level = ((ThrusterState)part.State).ThrusterStrength;
                        if (level <= 0f)
                            continue;
                        Vector3 position = data.PositionRelativeToRoot + data.RotationRelativeToRoot * part.LocalPosition;
                        Vector3 force = data.RotationRelativeToRoot * ThrustMath.Direction(part) * ThrustMath.Thrust(spec, level);
                        torque += Vector3.Cross(position - centerOfMass, force);
                        any = true;
                    }
                }
                bool has = any;
                if (!has)
                {
                    foreach (RootRelativeShipData data in _ships.GetRootConnectedShips(____ship.Id))
                    {
                        if (data.Ship == null)
                            continue;
                        foreach (StatefulPart part in data.Ship.GetAllStatefulParts<ThrusterState>())
                        {
                            if (EngineCatalog.TryGet(part.Settings.id, out EngineSpec spec) && spec.OffCenterThrust)
                                has = true;
                        }
                    }
                }
                if (Checked.Count > 512)
                    Checked.Clear();
                Checked[____ship.Id] = (now + 1f, has);
                if (!any)
                    return;
                Rigidbody body = Body(____shipRigidbody);
                if (body != null && !body.isKinematic)
                    body.AddRelativeTorque(torque, ForceMode.Force);
            }
            catch (System.Exception e)
            {
                ErrorLog.Report("engine torque", e);
            }
        }
    }

    /// <summary>
    /// Nuclear and dark matter engines: their propellant sockets carry their special propellant
    /// and oxygen (either way round) instead of oxygen and ethanol.
    /// </summary>
    [HarmonyPatch(typeof(ShipThrustersUpdater), "TryGetValidFluidGroups")]
    internal static class EnginePropellantPatch
    {
        private const uint Oxygen = 108;

        private static bool Prefix(ushort partID, out FluidGroupServerState group0, out FluidGroupServerState group1, ref bool __result,
            IShipStateWrite ____ship, Ships.Cables.ConnectionsServerTracker ____connections)
        {
            group0 = null;
            group1 = null;
            if (____ship == null || !____ship.TryGetStatefulPart(partID, out StatefulPart part) || !EngineCatalog.TryGet(part.Settings.id, out EngineSpec spec) || spec.Fuel == 0)
                return true;
            __result = false;
            if (!____connections.TryGetFluidGroupCacheFromSocket(new Ships.Interface.Model.Parts.Common.EntityPartSocket(____ship.Id, partID, 0), out group0)
                || !____connections.TryGetFluidGroupCacheFromSocket(new Ships.Interface.Model.Parts.Common.EntityPartSocket(____ship.Id, partID, 1), out group1))
                return false;
            uint a = group0.FluidItemId, b = group1.FluidItemId;
            __result = (a == spec.Fuel && b == Oxygen) || (a == Oxygen && b == spec.Fuel);
            return false;
        }
    }

    /// <summary>
    /// The pilot's move input, from every seat in use on the ship or the ships docked to it, in
    /// this ship's frame (basic seat: up/down and forward/back; advanced seat: all three axes).
    /// </summary>
    internal static class PilotControl
    {
        private static IShipsServerProvider _ships;
        private static readonly System.Collections.Generic.List<Vector3> Inputs = new System.Collections.Generic.List<Vector3>();
        private static uint _cachedShip;
        private static int _cachedFrame = -1;

        /// <summary>How much thrust (0-1) the pilots ask along this direction of the ship.</summary>
        public static float Command(IShipStateWrite ship, Vector3 thrustDirection)
        {
            if (_cachedShip != ship.Id || _cachedFrame != Time.frameCount)
            {
                _cachedShip = ship.Id;
                _cachedFrame = Time.frameCount;
                Collect(ship);
            }
            float best = 0f;
            foreach (Vector3 input in Inputs)
                best = Mathf.Max(best, Vector3.Dot(input, thrustDirection));
            return Mathf.Clamp01(best);
        }

        private static void Collect(IShipStateWrite ship)
        {
            Inputs.Clear();
            if (_ships == null)
                _ships = ServiceLocator.GetService<IShipsServerProvider>();
            Quaternion mine = Quaternion.identity;
            var assembly = _ships?.GetRootConnectedShips(ship.Id);
            if (assembly != null)
            {
                foreach (RootRelativeShipData data in assembly)
                {
                    if (data.Ship != null && data.Ship.Id == ship.Id)
                        mine = data.RotationRelativeToRoot;
                }
            }
            if (assembly == null || assembly.Count == 0)
            {
                AddSeats(ship, Quaternion.identity);
                return;
            }
            foreach (RootRelativeShipData data in assembly)
            {
                if (data.Ship != null)
                    AddSeats(data.Ship, Quaternion.Inverse(mine) * data.RotationRelativeToRoot);
            }
        }

        private static void AddSeats(IShipStateRead ship, Quaternion toShip)
        {
            foreach (StatefulPart seat in ship.GetAllStatefulParts<PilotSeatState>())
            {
                var state = (PilotSeatState)seat.State;
                if (state.IsInUse)
                    Inputs.Add(toShip * (seat.LocalRotation * new Vector3(0f, (float)state.MoveInputY, (float)state.MoveInputZ)));
            }
            foreach (StatefulPart seat in ship.GetAllStatefulParts<PilotSeatAdvState>())
            {
                var state = (PilotSeatAdvState)seat.State;
                if (state.IsInUse)
                    Inputs.Add(toShip * (seat.LocalRotation * state.MoveInput));
            }
        }
    }

    /// <summary>
    /// The game links small transmitters of one channel only within a ship: a transmitter on an
    /// engine pod (a ship of its own on a rotor) never hears the main ship's. For an engine's
    /// command line, a transmitter on it also hears its channel on every ship of the assembly.
    /// </summary>
    internal static class Channels
    {
        private static IShipsServerProvider _ships;

        public static double Relayed(IShipStateWrite ship, Ships.Cables.ConnectionsServerTracker connections, Ships.Cables.Model.ConnectionGroup commandGroup)
        {
            double best = 0;
            foreach (StatefulPart part in ship.GetAllStatefulParts<SignalTransmitterState>())
            {
                var state = (SignalTransmitterState)part.State;
                if (string.IsNullOrEmpty(state.Channel)
                    || !connections.TryGetSignalConnectionGroupWithSocket(new Ships.Interface.Model.Parts.Common.EntityPartSocket(ship.Id, part.Id, 0), out Ships.Cables.Model.ConnectionGroup group)
                    || group.Id != commandGroup.Id)
                    continue;
                if (_ships == null)
                    _ships = ServiceLocator.GetService<IShipsServerProvider>();
                var assembly = _ships?.GetRootConnectedShips(ship.Id);
                if (assembly == null)
                    continue;
                foreach (RootRelativeShipData data in assembly)
                {
                    if (data.Ship == null || data.Ship.Id == ship.Id)
                        continue;
                    foreach (StatefulPart other in data.Ship.GetAllStatefulParts<SignalTransmitterState>())
                    {
                        var sender = (SignalTransmitterState)other.State;
                        if (sender.Channel == state.Channel && System.Math.Abs(sender.SignalValue) > System.Math.Abs(best))
                            best = sender.SignalValue;
                    }
                }
            }
            return best;
        }
    }

    /// <summary>
    /// Lines fed only through a valve or a pump: no tank on the line itself, but a valve or a pump
    /// on it. An engine on such a line gets fuel only when the valve lets it through.
    /// </summary>
    internal static class ValveLines
    {
        private static readonly System.Collections.Generic.HashSet<FluidGroupServerState> Valved = new System.Collections.Generic.HashSet<FluidGroupServerState>();
        private static readonly System.Collections.Generic.HashSet<FluidGroupServerState> Tanked = new System.Collections.Generic.HashSet<FluidGroupServerState>();
        private static IShipsServerProvider _ships;
        private static uint _cachedShip;
        private static int _cachedFrame = -1;

        public static bool Feeds(IShipStateWrite ship, Ships.Cables.ConnectionsServerTracker connections, FluidGroupServerState a, FluidGroupServerState b)
        {
            if (_cachedShip != ship.Id || _cachedFrame != Time.frameCount)
            {
                _cachedShip = ship.Id;
                _cachedFrame = Time.frameCount;
                Valved.Clear();
                Tanked.Clear();
                if (connections != null)
                {
                    if (_ships == null)
                        _ships = ServiceLocator.GetService<IShipsServerProvider>();
                    var assembly = _ships?.GetRootConnectedShips(ship.Id);
                    if (assembly == null || assembly.Count == 0)
                        Collect(ship, connections);
                    else
                    {
                        foreach (RootRelativeShipData data in assembly)
                        {
                            if (data.Ship != null)
                                Collect(data.Ship, connections);
                        }
                    }
                }
            }
            return Only(a) || Only(b);
        }

        private static bool Only(FluidGroupServerState group) => group != null && Valved.Contains(group) && !Tanked.Contains(group);

        private static void Collect(IShipStateRead ship, Ships.Cables.ConnectionsServerTracker connections)
        {
            // Valves and pumps join two lines on their sockets 2 and 3; tanks sit on a line by sockets 0 and 1
            Add<FluidValveState>(ship, connections, Valved, 2, 3);
            Add<FluidPumpState>(ship, connections, Valved, 2, 3);
            Add<FluidTankState>(ship, connections, Tanked, 0, 1);
        }

        private static void Add<T>(IShipStateRead ship, Ships.Cables.ConnectionsServerTracker connections, System.Collections.Generic.HashSet<FluidGroupServerState> into, byte first, byte last)
            where T : Ships.Interface.Model.Parts.StateTypes.IStatefulPartState
        {
            foreach (StatefulPart part in ship.GetAllStatefulParts<T>())
            {
                for (byte socket = first; socket <= last; socket++)
                {
                    if (connections.TryGetFluidGroupCacheFromSocket(new Ships.Interface.Model.Parts.Common.EntityPartSocket(ship.Id, part.Id, socket), out FluidGroupServerState group))
                        into.Add(group);
                }
            }
        }
    }

    /// <summary>The game's own check that a thruster's sockets 0 and 1 carry oxygen and ethanol.</summary>
    internal static class FluidGroups
    {
        private delegate bool TryGetValid(ShipThrustersUpdater updater, ushort partId, out FluidGroupServerState a, out FluidGroupServerState b);

        private static readonly TryGetValid Method =
            AccessTools.MethodDelegate<TryGetValid>(AccessTools.Method(typeof(ShipThrustersUpdater), "TryGetValidFluidGroups"));

        public static bool TryGet(ShipThrustersUpdater updater, ushort partId, out FluidGroupServerState a, out FluidGroupServerState b)
        {
            return Method(updater, partId, out a, out b);
        }
    }
}

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
                        float extra = baseQuantity * (spec.FuelMultiplier - 1f);
                        a.AddConsumptionTarget(extra);
                        b.AddConsumptionTarget(extra);
                    }
                }
            }
            catch (System.Exception e)
            {
                ErrorLog.Report("engine fuel demand", e);
            }
        }
    }

    /// <summary>Propellant actually burnt, and spool-up: thrust level moves toward what the lines can feed.</summary>
    [HarmonyPatch(typeof(ShipThrustersUpdater), nameof(ShipThrustersUpdater.Update))]
    internal static class EngineFuelUsePatch
    {
        private static readonly Dictionary<ushort, float> PreviousLevels = new Dictionary<ushort, float>();

        private static void Prefix(IShipStateWrite ____ship)
        {
            PreviousLevels.Clear();
            foreach (StatefulPart part in ____ship.GetAllStatefulParts<ThrusterState>())
            {
                if (EngineCatalog.TryGet(part.Settings.id, out _))
                    PreviousLevels[part.Id] = ((ThrusterState)part.State).ThrusterStrength;
            }
        }

        private static void Postfix(ShipThrustersUpdater __instance, float deltaTime, IShipStateWrite ____ship)
        {
            if (PreviousLevels.Count == 0)
                return;
            try
            {
                float baseQuantity = EngineCatalog.BaseFlow * deltaTime;
                foreach (StatefulPart part in ____ship.GetAllStatefulParts<ThrusterState>())
                {
                    if (!EngineCatalog.TryGet(part.Settings.id, out EngineSpec spec) || !PreviousLevels.TryGetValue(part.Id, out float previous))
                        continue;
                    var state = (ThrusterState)part.State;
                    // What the game just computed: the share of the demand the lines could feed
                    float fed = state.ThrusterStrength;
                    // Spool-up only slows rising thrust: an engine never burns more than its lines feed it
                    float level = spec.SpoolSeconds > 0f && fed > previous ? Mathf.MoveTowards(previous, fed, deltaTime / spec.SpoolSeconds) : fed;
                    if (FluidGroups.TryGet(__instance, part.Id, out FluidGroupServerState a, out FluidGroupServerState b))
                    {
                        // The game removed fed x 1x; the engine burns level x its own flow
                        float correction = (level * spec.FuelMultiplier - fed) * baseQuantity;
                        a.RemoveFluid(correction);
                        b.RemoveFluid(correction);
                    }
                    if (!Mathf.Approximately(level, fed))
                        ____ship.UpdatePartState(part.Id, state with { ThrusterStrength = level });
                }
            }
            catch (System.Exception e)
            {
                ErrorLog.Report("engine fuel use", e);
            }
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
            }
            catch (System.Exception e)
            {
                ErrorLog.Report("engine thrust", e);
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

        private static void Postfix(ShipPhysicsCalculationProperties props, IShipStateWrite ____ship, ShipRigidbody ____shipRigidbody)
        {
            try
            {
                if (____ship == null || ____shipRigidbody == null)
                    return;
                if (_ships == null)
                    _ships = ServiceLocator.GetService<IShipsServerProvider>();
                if (_ships == null)
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

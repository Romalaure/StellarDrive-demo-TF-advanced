using System.Collections.Generic;
using HarmonyLib;
using Ships;
using Ships.Cables;
using Ships.Cables.Fluids;
using Ships.Cables.Power;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.State;
using Ships.Physics.Forces;
using Ships.Services;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Power
{
    /// <summary>
    /// Cryonite condenser (host simulation): cryonite crystals cooled by the ship's power pull
    /// oxygen out of nothing (the crystals' own lattice, the lore says) into the fluid network on
    /// its fluid socket. It runs when its power network feeds it and the network has room for
    /// oxygen (empty or already oxygen). Its ConstantState value (emitted on its signal socket) is
    /// the oxygen it makes, in thousandths of a unit per second.
    /// </summary>
    internal static class Condenser
    {
        public const byte OxygenSocket = PowerCatalog.MachineFluid;
        private const uint Oxygen = 108;

        private sealed class Runtime
        {
            public PowerGroupServerState Group;
            public FluidGroupServerState Fluid;
            public float LastWrite = -10f;
            public float Written = -1f;
        }

        private struct Entry
        {
            public TrackedShipServer Ship;
            public StatefulPart Part;
            public Runtime Runtime;
        }

        private static readonly Dictionary<(uint, ushort), Runtime> Runtimes = new Dictionary<(uint, ushort), Runtime>();
        private static readonly List<Entry> Condensers = new List<Entry>();

        public static void Forget()
        {
            Runtimes.Clear();
            Condensers.Clear();
        }

        public static void BeforeTransfers(PowerServerTracker power, ShipsServerTracker ships, ConnectionsServerTracker connections, float deltaTime)
        {
            Condensers.Clear();
            foreach (TrackedShipServer ship in ships.LoadedShipsWithChildren)
            {
                foreach (StatefulPart part in ship.GetAllStatefulParts<ConstantState>())
                {
                    if (part.Settings.id != PowerCatalog.CondenserId)
                        continue;
                    if (!Runtimes.TryGetValue((ship.Id, part.Id), out Runtime rt))
                        Runtimes[(ship.Id, part.Id)] = rt = new Runtime();
                    rt.Group = null;
                    rt.Fluid = null;
                    Condensers.Add(new Entry { Ship = ship, Part = part, Runtime = rt });
                    if (!power.TryGetGroupFromSocket(new EntityPartSocket(ship.Id, part.Id, PowerCatalog.MachinePowerA), out PowerGroupServerState group)
                        && !power.TryGetGroupFromSocket(new EntityPartSocket(ship.Id, part.Id, PowerCatalog.MachinePowerB), out group))
                        continue;
                    if (!connections.TryGetFluidGroupCacheFromSocket(new EntityPartSocket(ship.Id, part.Id, OxygenSocket), out FluidGroupServerState fluid))
                        continue;
                    float oxygen = Mathf.Max(0f, PowerCatalog.CondenserOxygenPerSecond.Value) * deltaTime;
                    bool oxygenLine = fluid.FluidItemId == Oxygen || fluid.FluidItemId == 0 || fluid.Available <= 0f;
                    if (!oxygenLine || fluid.ReceivingCapacity < oxygen)
                        continue;
                    group.TotalPowerNeeded += Mathf.Max(0f, PowerCatalog.CondenserWatts.Value) * deltaTime;
                    rt.Group = group;
                    rt.Fluid = fluid;
                }
            }
        }

        public static void AfterTransfers(float deltaTime)
        {
            float now = Time.unscaledTime;
            foreach (Entry entry in Condensers)
            {
                Runtime rt = entry.Runtime;
                float rate = 0f;
                if (rt.Group != null && rt.Fluid != null)
                {
                    // Oxygen in proportion to the power it got
                    rate = Mathf.Max(0f, PowerCatalog.CondenserOxygenPerSecond.Value) * Mathf.Clamp01(rt.Group.NormalizedConsumerRate);
                    if (rate > 0f)
                        rt.Fluid.AddFluid(Oxygen, rate * deltaTime);
                }
                if (Mathf.Abs(rate - rt.Written) < 0.0005f || now - rt.LastWrite < 1f && (rate > 0f) == (rt.Written > 0f))
                    continue;
                rt.LastWrite = now;
                rt.Written = rate;
                var state = (ConstantState)entry.Part.State;
                entry.Ship.UpdatePartState(entry.Part.Id, state with { ConstantValue = System.Math.Round(rate * 1000.0) });
            }
        }
    }

    /// <summary>The quantum gyroscope turns the ship with several gyroscopes' torque.</summary>
    [HarmonyPatch(typeof(ShipRotation), "CalculateShipRotationForce")]
    internal static class QuantumGyroscopePatch
    {
        private const float GameTorque = 8000f;

        private static void Postfix(IShipStateRead stateRead, ref Vector3 __result)
        {
            try
            {
                float extra = Mathf.Max(0f, PowerCatalog.QuantumGyroFactor.Value) - 1f;
                if (extra == 0f)
                    return;
                foreach (StatefulPart part in stateRead.GetAllStatefulParts<GyroscopeState>())
                {
                    if (part.Settings.id != PowerCatalog.QuantumGyroId)
                        continue;
                    __result += part.LocalRotation * ((GyroscopeState)part.State).RotationStrength * (GameTorque * extra);
                }
            }
            catch (System.Exception e)
            {
                ErrorLog.Report("quantum gyroscope", e);
            }
        }
    }
}

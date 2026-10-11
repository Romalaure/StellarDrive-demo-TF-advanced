using System.Collections.Generic;
using Ships;
using Ships.Cables;
using Ships.Cables.Fluids;
using Ships.Cables.Power;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;
using Ships.Services;
using StellarDriveDemoTFAdvanced.Construction;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Power
{
    /// <summary>
    /// The life support module's oxygen plant (host simulation): it draws power and makes oxygen
    /// into the line on its air socket (empty or already oxygen), the line every module of the
    /// station breathes from (see <see cref="StationAir"/>). Its signal: oxygen made, in
    /// thousandths of a unit per second.
    /// </summary>
    internal static class LifeSupport
    {
        public const uint Oxygen = 108;
        public const float Watts = 2500f, PerSecond = 0.1f;

        private sealed class Runtime
        {
            public PowerGroupServerState Group;
            public FluidGroupServerState Line;
            public float LastWrite = -10f;
            public double Written = double.NaN;
        }

        private static readonly Dictionary<(uint, ushort), Runtime> Runtimes = new Dictionary<(uint, ushort), Runtime>();
        private static readonly List<(TrackedShipServer Ship, StatefulPart Part, Runtime Runtime)> Plants = new List<(TrackedShipServer, StatefulPart, Runtime)>();

        public static void Forget()
        {
            Runtimes.Clear();
            Plants.Clear();
        }

        public static void BeforeTransfers(PowerServerTracker power, ShipsServerTracker ships, ConnectionsServerTracker connections, float deltaTime)
        {
            Plants.Clear();
            foreach (TrackedShipServer ship in ships.LoadedShipsWithChildren)
            {
                foreach (StatefulPart part in ship.GetAllStatefulParts<ConstantState>())
                {
                    if (part.Settings.id != StationModules.LifeSupportId)
                        continue;
                    if (!Runtimes.TryGetValue((ship.Id, part.Id), out Runtime rt))
                        Runtimes[(ship.Id, part.Id)] = rt = new Runtime();
                    rt.Group = null;
                    rt.Line = null;
                    Plants.Add((ship, part, rt));
                    if (!power.TryGetGroupFromSocket(new EntityPartSocket(ship.Id, part.Id, PowerCatalog.MachinePowerA), out PowerGroupServerState group)
                        && !power.TryGetGroupFromSocket(new EntityPartSocket(ship.Id, part.Id, PowerCatalog.MachinePowerB), out group))
                        continue;
                    if (!connections.TryGetFluidGroupCacheFromSocket(new EntityPartSocket(ship.Id, part.Id, PowerCatalog.MachineFluid), out FluidGroupServerState line))
                        continue;
                    bool ownLine = line.FluidItemId == Oxygen || line.FluidItemId == 0 || line.Available <= 0f;
                    if (!ownLine || line.ReceivingCapacity < PerSecond * deltaTime)
                        continue;
                    group.TotalPowerNeeded += Watts * deltaTime;
                    rt.Group = group;
                    rt.Line = line;
                }
            }
        }

        public static void AfterTransfers(float deltaTime)
        {
            float now = Time.unscaledTime;
            foreach ((TrackedShipServer ship, StatefulPart part, Runtime rt) in Plants)
            {
                float made = 0f;
                if (rt.Group != null && rt.Line != null)
                {
                    made = PerSecond * Mathf.Clamp01(rt.Group.NormalizedConsumerRate);
                    if (made > 0f)
                        rt.Line.AddFluid(Oxygen, made * deltaTime);
                }
                double value = System.Math.Round(made * 1000.0);
                bool kindChanged = double.IsNaN(rt.Written) || (value > 0) != (rt.Written > 0);
                if (!kindChanged && (System.Math.Abs(value - rt.Written) < 0.5 || now - rt.LastWrite < 1f))
                    continue;
                rt.LastWrite = now;
                rt.Written = value;
                ship.UpdatePartState(part.Id, ((ConstantState)part.State) with { ConstantValue = value });
            }
        }
    }
}

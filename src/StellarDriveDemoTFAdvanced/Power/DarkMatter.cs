using System.Collections.Generic;
using Core.Services;
using Ships;
using Ships.Cables;
using Ships.Cables.Fluids;
using Ships.Cables.Power;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;
using Ships.Services;
using StellarDriveDemoTFAdvanced.Items;
using UnityEngine;
using WorldTracking.Interface.Services;

namespace StellarDriveDemoTFAdvanced.Power
{
    /// <summary>
    /// Dark matter (host simulation), the energy of the superior machines. It is a fluid item:
    /// it flows in pipes and fills tanks like oxygen.
    ///  - The dark matter collector draws power and pulls dark matter out of space into the line
    ///    on its fluid socket: slowly anywhere, five times faster near a black hole, three times in
    ///    the Erebos galaxy. Its signal: what it makes, in ten-thousandths of a unit per second.
    ///  - The dark matter reactor burns dark matter from its fluid socket and gives its power to
    ///    the networks on its two power sockets. Its signal: 100 while it runs, 0 out of fuel.
    /// Both keep their value in a ConstantState (saved, synchronized, emitted on socket 0).
    /// </summary>
    internal static class DarkMatter
    {
        public const byte FluidSocket = PowerCatalog.MachineFluid;
        private static uint Fluid => ModItems.DarkMatter;

        private sealed class Runtime
        {
            public PowerGroupServerState Group;
            public PowerGroupServerState Other;
            public FluidGroupServerState Line;
            public float Rate;
            public float PendingOutput;
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
        private static readonly List<Entry> Collectors = new List<Entry>();
        private static readonly List<Entry> Reactors = new List<Entry>();
        private static IWorldTransposeServer _transpose;

        public static void Forget()
        {
            Runtimes.Clear();
            Collectors.Clear();
            Reactors.Clear();
            _transpose = null;
        }

        /// <summary>How much faster dark matter gathers here: near a black hole, or in the Erebos galaxy.</summary>
        public static float Boost(uint spaceId, Vector3 position)
        {
            if (_transpose == null)
                _transpose = ServiceLocator.GetService<IWorldTransposeServer>();
            float boost = 1f;
            if (_transpose != null && Worlds.BlackHole.TryNearest(_transpose, spaceId, position, out Worlds.Hole hole, out Vector3 toHole)
                && toHole.magnitude < hole.TidalRadius * 40f)
                boost = 5f;
            if (_transpose != null && Worlds.Galaxy.IsInErebos(_transpose, spaceId, position))
                boost = Mathf.Max(boost, 3f);
            return boost;
        }

        public static void BeforeTransfers(PowerServerTracker power, ShipsServerTracker ships, ConnectionsServerTracker connections, float deltaTime)
        {
            Collectors.Clear();
            Reactors.Clear();
            foreach (TrackedShipServer ship in ships.LoadedShipsWithChildren)
            {
                foreach (StatefulPart part in ship.GetAllStatefulParts<ConstantState>())
                {
                    ushort id = part.Settings.id;
                    if (id != PowerCatalog.DarkCollectorId && id != PowerCatalog.DarkReactorId)
                        continue;
                    if (!Runtimes.TryGetValue((ship.Id, part.Id), out Runtime rt))
                        Runtimes[(ship.Id, part.Id)] = rt = new Runtime();
                    rt.Group = rt.Other = null;
                    rt.Line = null;
                    rt.PendingOutput = 0f;
                    var entry = new Entry { Ship = ship, Part = part, Runtime = rt };
                    if (id == PowerCatalog.DarkCollectorId)
                    {
                        Collectors.Add(entry);
                        PrepareCollector(power, connections, entry, deltaTime);
                    }
                    else
                    {
                        Reactors.Add(entry);
                        PrepareReactor(power, connections, entry, deltaTime);
                    }
                }
            }
        }

        private static void PrepareCollector(PowerServerTracker power, ConnectionsServerTracker connections, Entry entry, float deltaTime)
        {
            Runtime rt = entry.Runtime;
            if (!power.TryGetGroupFromSocket(new EntityPartSocket(entry.Ship.Id, entry.Part.Id, PowerCatalog.MachinePowerA), out PowerGroupServerState group)
                && !power.TryGetGroupFromSocket(new EntityPartSocket(entry.Ship.Id, entry.Part.Id, PowerCatalog.MachinePowerB), out group))
                return;
            if (!connections.TryGetFluidGroupCacheFromSocket(new EntityPartSocket(entry.Ship.Id, entry.Part.Id, FluidSocket), out FluidGroupServerState line))
                return;
            rt.Rate = Mathf.Max(0f, PowerCatalog.DarkCollectorPerSecond.Value) * Boost(entry.Ship.SpaceId, entry.Ship.Position);
            bool ownLine = line.FluidItemId == Fluid || line.FluidItemId == 0 || line.Available <= 0f;
            if (!ownLine || line.ReceivingCapacity < rt.Rate * deltaTime)
                return;
            group.TotalPowerNeeded += Mathf.Max(0f, PowerCatalog.DarkCollectorWatts.Value) * deltaTime;
            rt.Group = group;
            rt.Line = line;
        }

        private static void PrepareReactor(PowerServerTracker power, ConnectionsServerTracker connections, Entry entry, float deltaTime)
        {
            Runtime rt = entry.Runtime;
            power.TryGetGroupFromSocket(new EntityPartSocket(entry.Ship.Id, entry.Part.Id, PowerCatalog.MachinePowerA), out PowerGroupServerState a);
            power.TryGetGroupFromSocket(new EntityPartSocket(entry.Ship.Id, entry.Part.Id, PowerCatalog.MachinePowerB), out PowerGroupServerState b);
            if (a == null && b == null)
                return;
            float burn = Mathf.Max(0f, PowerCatalog.DarkReactorBurnPerSecond.Value) * deltaTime;
            if (!connections.TryGetFluidGroupCacheFromSocket(new EntityPartSocket(entry.Ship.Id, entry.Part.Id, FluidSocket), out FluidGroupServerState line)
                || line.FluidItemId != Fluid || line.Available < burn)
                return;
            line.RemoveFluid(burn);
            float output = Mathf.Max(0f, PowerCatalog.DarkReactorWatts.Value) * deltaTime;
            if (a != null && b != null && a != b)
            {
                a.TotalPowerProductionCapacity += output * 0.5f;
                b.TotalPowerProductionCapacity += output * 0.5f;
                rt.Group = a;
                rt.Other = b;
            }
            else
            {
                (a ?? b).TotalPowerProductionCapacity += output;
                rt.Group = a ?? b;
            }
            rt.PendingOutput = output;
        }

        public static void AfterTransfers(float deltaTime)
        {
            float now = Time.unscaledTime;
            foreach (Entry entry in Collectors)
            {
                Runtime rt = entry.Runtime;
                float made = 0f;
                if (rt.Group != null && rt.Line != null)
                {
                    made = rt.Rate * Mathf.Clamp01(rt.Group.NormalizedConsumerRate);
                    if (made > 0f)
                        rt.Line.AddFluid(Fluid, made * deltaTime);
                }
                Write(entry, System.Math.Round(made * 10000.0), now);
            }
            foreach (Entry entry in Reactors)
                Write(entry, entry.Runtime.Group != null ? 100 : 0, now);
        }

        private static void Write(Entry entry, double value, float now)
        {
            Runtime rt = entry.Runtime;
            bool kindChanged = double.IsNaN(rt.Written) || (value > 0) != (rt.Written > 0);
            if (!kindChanged && (System.Math.Abs(value - rt.Written) < 0.5 || now - rt.LastWrite < 1f))
                return;
            rt.LastWrite = now;
            rt.Written = value;
            var state = (ConstantState)entry.Part.State;
            entry.Ship.UpdatePartState(entry.Part.Id, state with { ConstantValue = value });
        }
    }
}

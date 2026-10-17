using System.Collections.Generic;
using Core.Services;
using HarmonyLib;
using Ships;
using Ships.Cables;
using Ships.Cables.Power;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Physics;
using Ships.Services;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Power
{
    /// <summary>
    /// Host-side simulation of batteries and circuit breakers, inside the game's own power tick.
    /// Each tick the game sums, per power network, what consumers need and what producers could
    /// give, then splits power from those totals (PowerGroupServerState). Just before that split:
    ///  - a closed breaker moves power from its input network's spare capacity to its output
    ///    network's shortfall, up to its rating (a consumer on one side, a producer on the other);
    ///  - a battery covers its network's shortfall from its charge, or charges from spare
    ///    capacity, which makes the generators really burn more wood.
    /// Right after the split, what the game actually delivered is booked into the charge.
    /// Energy is conserved: nothing is created, stored energy came from a generator.
    /// </summary>
    internal static class PowerGrid
    {
        private sealed class Runtime
        {
            public float Stored;
            public bool Initialized;
            public PowerGroupServerState Group;
            public PowerGroupServerState Output;
            public float PendingCharge;
            public float PendingDischarge;
            public float PendingTransfer;
            public float Flow;
            public float Status;
            public bool Commanded = true;
            public float Overload;
            public float TrippedAt;
            public float LastWrite = -10f;
            public float WrittenQuantity = -1f;
            public float WrittenFlow;
        }

        private struct Entry
        {
            public TrackedShipServer Ship;
            public StatefulPart Part;
            public Runtime Runtime;
        }

        public struct Trip
        {
            public float Time;
            public uint ShipId;
            public ushort PartId;
            public float DemandWatts;
        }

        private const float WriteInterval = 1f;
        private const int JournalSize = 20;

        private static readonly Dictionary<(uint, ushort), Runtime> Runtimes = new Dictionary<(uint, ushort), Runtime>();
        private static readonly List<Entry> Breakers = new List<Entry>();
        private static readonly List<Entry> Batteries = new List<Entry>();
        private static readonly List<Entry> Reactors = new List<Entry>();
        private static ShipsServerTracker _ships;
        private static ConnectionsServerTracker _connections;

        /// <summary>Host only: the last breaker trips, newest first.</summary>
        public static readonly List<Trip> Journal = new List<Trip>();

        public static void Forget()
        {
            Runtimes.Clear();
            Breakers.Clear();
            Batteries.Clear();
            Reactors.Clear();
            Journal.Clear();
            ForceField.Forget();
            FieldDoor.Forget();
            Condenser.Forget();
            ShipMiningLaser.Forget();
            DarkMatter.Forget();
            Centrifuge.Forget();
            Portal.Forget();
            Solar.Forget();
            LifeSupport.Forget();
            Combat.CombatPower.Forget();
            _ships = null;
            _connections = null;
        }

        public static void BeforeTransfers(PowerServerTracker power, float deltaTime)
        {
            if (deltaTime <= 0f)
                return;
            if (_ships == null)
                _ships = ServiceLocator.GetService<ShipsServerTracker>();
            if (_connections == null)
                _connections = ServiceLocator.GetService<ConnectionsServerTracker>();
            if (_ships == null || _connections == null)
                return;

            Collect();
            foreach (Entry breaker in Breakers)
                PrepareBreaker(power, breaker, deltaTime);
            foreach (Entry battery in Batteries)
                PrepareBattery(power, battery, deltaTime);
            foreach (Entry reactor in Reactors)
                PrepareReactor(power, reactor, deltaTime);
            ForceField.BeforeTransfers(power, _ships, _connections, deltaTime);
            FieldDoor.BeforeTransfers(power, _ships, deltaTime);
            Propulsion.LandingGear.Command(_ships, _connections);
            Condenser.BeforeTransfers(power, _ships, _connections, deltaTime);
            ShipMiningLaser.BeforeTransfers(power, _ships, _connections, deltaTime);
            DarkMatter.BeforeTransfers(power, _ships, _connections, deltaTime);
            Portal.BeforeTransfers(power, _ships, _connections, deltaTime);
            Solar.BeforeTransfers(power, _ships, deltaTime);
            LifeSupport.BeforeTransfers(power, _ships, _connections, deltaTime);
            Combat.CombatPower.BeforeTransfers(power, _ships, deltaTime);
        }

        public static void AfterTransfers(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;
            float now = Time.unscaledTime;
            foreach (Entry breaker in Breakers)
            {
                Runtime rt = breaker.Runtime;
                rt.Flow = rt.PendingTransfer / deltaTime;
                Write(breaker, rt.Status, rt.Flow, now);
            }
            ForceField.AfterTransfers(deltaTime);
            FieldDoor.AfterTransfers(deltaTime);
            Condenser.AfterTransfers(deltaTime);
            ShipMiningLaser.AfterTransfers(deltaTime);
            DarkMatter.AfterTransfers(deltaTime);
            Portal.AfterTransfers(deltaTime);
            Solar.AfterTransfers();
            LifeSupport.AfterTransfers(deltaTime);
            Combat.CombatPower.AfterTransfers(deltaTime);
            float efficiency = Mathf.Clamp(PowerCatalog.BatteryEfficiency.Value, 0.5f, 1f);
            foreach (Entry reactor in Reactors)
            {
                Runtime rt = reactor.Runtime;
                // What the network really drew from it
                float delivered = rt.Group != null ? rt.PendingDischarge * rt.Group.NormalizedGeneratorRate : 0f;
                if (rt.Output != null)
                    delivered += rt.PendingTransfer * rt.Output.NormalizedGeneratorRate;
                rt.Flow = -delivered / deltaTime;
                Write(reactor, rt.Group != null ? 100f : 0f, rt.Flow, now);
            }
            foreach (Entry battery in Batteries)
            {
                float capacity = PowerCatalog.CapacityJoules(battery.Part.Settings.id);
                Runtime rt = battery.Runtime;
                float charged = 0f, discharged = 0f;
                if (rt.Group != null)
                {
                    charged = rt.PendingCharge * rt.Group.NormalizedConsumerRate;
                    discharged = rt.PendingDischarge * rt.Group.NormalizedGeneratorRate;
                }
                rt.Stored = Mathf.Clamp(rt.Stored + charged * efficiency - discharged, 0f, capacity);
                rt.Flow = (charged - discharged) / deltaTime;
                Write(battery, rt.Stored / capacity * 100f, rt.Flow, now);
            }
        }

        // Batteries, breakers and reactors of every ship the game simulates, in a fixed order
        private static void Collect()
        {
            Breakers.Clear();
            Batteries.Clear();
            Reactors.Clear();
            foreach (TrackedShipServer ship in _ships.LoadedShipsWithChildren)
            {
                foreach (StatefulPart part in ship.GetAllStatefulParts<FluidTankState>())
                {
                    ushort settingsId = part.Settings.id;
                    if (!PowerCatalog.IsPowerPart(settingsId))
                        continue;
                    if (!Runtimes.TryGetValue((ship.Id, part.Id), out Runtime rt))
                    {
                        rt = new Runtime();
                        Runtimes[(ship.Id, part.Id)] = rt;
                    }
                    rt.Group = null;
                    rt.Output = null;
                    rt.PendingCharge = rt.PendingDischarge = rt.PendingTransfer = 0f;
                    var entry = new Entry { Ship = ship, Part = part, Runtime = rt };
                    if (PowerCatalog.IsBattery(settingsId))
                        Batteries.Add(entry);
                    else if (settingsId == PowerCatalog.ReactorId)
                        Reactors.Add(entry);
                    else
                        Breakers.Add(entry);
                }
            }
            Breakers.Sort(Compare);
            Batteries.Sort(Compare);
            Reactors.Sort(Compare);
        }

        private static int Compare(Entry a, Entry b)
        {
            int ship = a.Ship.Id.CompareTo(b.Ship.Id);
            return ship != 0 ? ship : a.Part.Id.CompareTo(b.Part.Id);
        }

        private static void PrepareBreaker(PowerServerTracker power, Entry entry, float deltaTime)
        {
            Runtime rt = entry.Runtime;
            var state = (FluidTankState)entry.Part.State;
            float now = Time.unscaledTime;
            if (!rt.Initialized)
            {
                rt.Initialized = true;
                rt.Status = Mathf.Round(state.FluidQuantity);
                rt.TrippedAt = now;
            }

            // Control: no cable means closed; with a cable, a signal above 0 closes it
            var control = new EntityPartSocket(entry.Ship.Id, entry.Part.Id, PowerCatalog.BreakerControl);
            bool wired = _connections.IsSocketPhysicallyOccupied(control);
            bool commanded = !wired || _connections.GetSignalValue(control) > 0.0;
            bool risingEdge = commanded && !rt.Commanded;
            rt.Commanded = commanded;

            if (rt.Status == PowerCatalog.BreakerTripped)
            {
                float autoReset = PowerCatalog.BreakerAutoResetSeconds.Value;
                bool reset = wired ? risingEdge : autoReset > 0f && now - rt.TrippedAt >= autoReset;
                if (!reset)
                    return;
                rt.Overload = 0f;
            }
            rt.Status = commanded ? PowerCatalog.BreakerClosed : PowerCatalog.BreakerOpen;
            if (!commanded)
                return;

            if (!power.TryGetGroupFromSocket(new EntityPartSocket(entry.Ship.Id, entry.Part.Id, PowerCatalog.BreakerInput), out PowerGroupServerState input)
                || !power.TryGetGroupFromSocket(new EntityPartSocket(entry.Ship.Id, entry.Part.Id, PowerCatalog.BreakerOutput), out PowerGroupServerState output)
                || input == output)
                return;

            float shortfall = output.TotalPowerNeeded - output.TotalPowerProductionCapacity;
            if (shortfall <= 0f)
            {
                rt.Overload = 0f;
                return;
            }
            float rating = Mathf.Max(1f, PowerCatalog.BreakerRatingWatts.Value) * deltaTime;
            if (shortfall > rating)
            {
                rt.Overload += deltaTime;
                if (rt.Overload >= Mathf.Max(0f, PowerCatalog.BreakerTripSeconds.Value))
                {
                    TripBreaker(entry, shortfall / deltaTime, now);
                    return;
                }
            }
            else
            {
                rt.Overload = 0f;
            }
            float spare = Mathf.Max(0f, input.TotalPowerProductionCapacity - input.TotalPowerNeeded);
            float transfer = Mathf.Min(shortfall, rating, spare);
            if (transfer <= 0f)
                return;
            input.TotalPowerNeeded += transfer;
            output.TotalPowerProductionCapacity += transfer;
            rt.PendingTransfer = transfer;
            rt.Group = input;
            rt.Output = output;
        }

        private static void TripBreaker(Entry entry, float demandWatts, float now)
        {
            Runtime rt = entry.Runtime;
            rt.Status = PowerCatalog.BreakerTripped;
            rt.TrippedAt = now;
            rt.Overload = 0f;
            Journal.Insert(0, new Trip { Time = now, ShipId = entry.Ship.Id, PartId = entry.Part.Id, DemandWatts = demandWatts });
            if (Journal.Count > JournalSize)
                Journal.RemoveAt(Journal.Count - 1);
            AdvancedMod.Log.Msg($"breaker {entry.Ship.Id}:{entry.Part.Id} tripped: output asked {demandWatts:0} W, rating {PowerCatalog.BreakerRatingWatts.Value:0} W");
        }

        // A quantum reactor adds its output to its network's production capacity every tick, without
        // burning anything: unlike a battery, this energy is created (the reactor is the endless source
        // the artifacts it is built from promise)
        private static void PrepareReactor(PowerServerTracker power, Entry entry, float deltaTime)
        {
            Runtime rt = entry.Runtime;
            rt.Initialized = true;
            power.TryGetGroupFromSocket(new EntityPartSocket(entry.Ship.Id, entry.Part.Id, PowerCatalog.BatteryPowerA), out PowerGroupServerState a);
            power.TryGetGroupFromSocket(new EntityPartSocket(entry.Ship.Id, entry.Part.Id, PowerCatalog.BatteryPowerB), out PowerGroupServerState b);
            if (a == null && b == null)
                return;
            float output = Mathf.Max(0f, PowerCatalog.ReactorWatts.Value) * deltaTime;
            if (a != null && b != null && a != b)
            {
                // Two separate networks share its output
                a.TotalPowerProductionCapacity += output * 0.5f;
                b.TotalPowerProductionCapacity += output * 0.5f;
                rt.Group = a;
                rt.Output = b;
                rt.PendingDischarge = output * 0.5f;
                rt.PendingTransfer = output * 0.5f;
                return;
            }
            PowerGroupServerState group = a ?? b;
            group.TotalPowerProductionCapacity += output;
            rt.Group = group;
            rt.PendingDischarge = output;
        }

        private static void PrepareBattery(PowerServerTracker power, Entry entry, float deltaTime)
        {
            Runtime rt = entry.Runtime;
            ushort settingsId = entry.Part.Settings.id;
            float capacity = PowerCatalog.CapacityJoules(settingsId);
            float rates = PowerCatalog.RateScale(settingsId);
            if (!rt.Initialized)
            {
                rt.Initialized = true;
                rt.Stored = Mathf.Clamp01(((FluidTankState)entry.Part.State).FluidQuantity / 100f) * capacity;
            }
            if (!power.TryGetGroupFromSocket(new EntityPartSocket(entry.Ship.Id, entry.Part.Id, PowerCatalog.BatteryPowerA), out PowerGroupServerState group)
                && !power.TryGetGroupFromSocket(new EntityPartSocket(entry.Ship.Id, entry.Part.Id, PowerCatalog.BatteryPowerB), out group))
                return;
            rt.Group = group;

            float shortfall = group.TotalPowerNeeded - group.TotalPowerProductionCapacity;
            if (shortfall > 0f)
            {
                float discharge = Mathf.Min(shortfall, Mathf.Max(0f, PowerCatalog.BatteryDischargeWatts.Value) * rates * deltaTime, rt.Stored);
                if (discharge > 0f)
                {
                    group.TotalPowerProductionCapacity += discharge;
                    rt.PendingDischarge = discharge;
                }
                return;
            }
            float efficiency = Mathf.Clamp(PowerCatalog.BatteryEfficiency.Value, 0.5f, 1f);
            float room = (capacity - rt.Stored) / efficiency;
            float charge = Mathf.Min(-shortfall, Mathf.Max(0f, PowerCatalog.BatteryChargeWatts.Value) * rates * deltaTime, room);
            if (charge > 0f)
            {
                group.TotalPowerNeeded += charge;
                rt.PendingCharge = charge;
            }
        }

        // The part state carries the charge or status to every player; written only on real changes, at most once per second otherwise
        private static void Write(Entry entry, float quantity, float flow, float now)
        {
            Runtime rt = entry.Runtime;
            bool changed = Mathf.Abs(quantity - rt.WrittenQuantity) >= 0.1f
                || Mathf.Abs(flow - rt.WrittenFlow) > Mathf.Max(5f, Mathf.Abs(rt.WrittenFlow) * 0.05f);
            bool statusChanged = entry.Part.Settings.id == PowerCatalog.BreakerId && !Mathf.Approximately(quantity, rt.WrittenQuantity);
            if (!statusChanged && (!changed || now - rt.LastWrite < WriteInterval))
                return;
            rt.LastWrite = now;
            rt.WrittenQuantity = quantity;
            rt.WrittenFlow = flow;
            var state = (FluidTankState)entry.Part.State;
            entry.Ship.UpdatePartState(entry.Part.Id, state with { FluidItemId = 0u, FluidQuantity = quantity, FluidFlow = flow });
        }
    }

    [HarmonyPatch(typeof(PowerServerTracker), nameof(PowerServerTracker.UpdatePowerTransfers))]
    internal static class PowerGridPatch
    {
        private static void Prefix(PowerServerTracker __instance, float deltaTime)
        {
            try
            {
                PowerGrid.BeforeTransfers(__instance, deltaTime);
            }
            catch (System.Exception e)
            {
                ErrorLog.Report("power grid", e);
            }
        }

        private static void Postfix(float deltaTime)
        {
            try
            {
                PowerGrid.AfterTransfers(deltaTime);
            }
            catch (System.Exception e)
            {
                ErrorLog.Report("power grid", e);
            }
        }
    }

    /// <summary>The game gives a fluid tank 0.5 kg per unit of fluid; batteries and breakers hold no fluid.</summary>
    [HarmonyPatch(typeof(StatefulPartsMassTracker), "EnsureMassChangeIsAccounted")]
    internal static class PowerPartMassPatch
    {
        private static void Prefix(StatefulPart part, ref IStatefulPartState state)
        {
            if (part?.Settings != null && PowerCatalog.HoldsNoFluid(part.Settings.id) && state is FluidTankState tank && tank.FluidQuantity != 0f)
                state = tank with { FluidQuantity = 0f };
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Ships;
using Ships.Cables;
using Ships.Cables.Fluids;
using Ships.Cables.Power;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;
using Ships.Physics;
using Ships.Physics.Collision;
using Ships.Services;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Power
{
    /// <summary>
    /// Force field generator (host simulation). While it gets power on its power sockets and
    /// oxygen on its fluid socket, it wraps the whole ship (and everything docked to it) in a
    /// field that takes the hits instead of the hull: collision impacts and black hole tides drain
    /// the field's charge rather than tearing parts off. At zero the generator breaks down and
    /// needs <see cref="PowerCatalog.ShieldCooldownSeconds"/> to restart, at full charge. While it
    /// runs undamaged it slowly regains charge.
    /// The state is one value in the part's ConstantState (saved, synchronized to every player and
    /// emitted on its signal socket): the charge in percent (above 0) while the field is up, -1
    /// while it is off (no power or no oxygen), -(seconds + 1) while it restarts.
    /// </summary>
    internal static class ForceField
    {
        public const byte OxygenSocket = PowerCatalog.MachineFluid;
        private const uint Oxygen = 108;
        private const float WriteInterval = 0.5f;

        /// <summary>
        /// The generator models: the reinforced and dark matter fields hold several times more and
        /// recharge faster, for much more power (the dark matter one burns dark matter, not oxygen).
        /// Scales apply to the ShieldCapacity / ShieldRegenPerSecond / ShieldWatts /
        /// ShieldOxygenPerSecond settings.
        /// </summary>
        public sealed class Tier
        {
            public ushort Id;
            public int Level;
            public float CapacityScale, RegenScale, WattsScale, FluidScale;
            public uint Fluid;
            public Color Glow;

            public float Capacity => Mathf.Max(1f, PowerCatalog.ShieldCapacity.Value * CapacityScale);
            public float Regen => Mathf.Max(0f, PowerCatalog.ShieldRegenPerSecond.Value * RegenScale);
            public float Watts => Mathf.Max(0f, PowerCatalog.ShieldWatts.Value * WattsScale);
            public float FluidPerSecond => Mathf.Max(0f, PowerCatalog.ShieldOxygenPerSecond.Value * FluidScale);
        }

        public static readonly Tier[] Tiers =
        {
            new Tier { Id = PowerCatalog.ShieldId, Level = 1, CapacityScale = 1f, RegenScale = 1f, WattsScale = 1f, FluidScale = 1f, Fluid = Oxygen, Glow = MachineModels.Cyan },
            new Tier { Id = PowerCatalog.ShieldMk2Id, Level = 2, CapacityScale = 3f, RegenScale = 3f, WattsScale = 3f, FluidScale = 2.5f, Fluid = Oxygen, Glow = new Color(1f, 0.72f, 0.2f) },
            new Tier { Id = PowerCatalog.ShieldMk3Id, Level = 3, CapacityScale = 8f, RegenScale = 8f, WattsScale = 5f, FluidScale = 1f, Fluid = Items.ModItems.DarkMatter, Glow = MachineModels.Violet },
        };

        public static Tier TierOf(ushort partSettingsId)
        {
            foreach (Tier tier in Tiers)
            {
                if (tier.Id == partSettingsId)
                    return tier;
            }
            return null;
        }

        public static bool IsGenerator(ushort partSettingsId) => TierOf(partSettingsId) != null;

        private sealed class Runtime
        {
            public Tier Tier;
            public float Charge = -1f;
            public float CooldownUntil;
            public bool Up;
            public PowerGroupServerState Group;
            public float PendingPower;
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
        /// <summary>Ship id -> its working generators this tick.</summary>
        private static readonly Dictionary<uint, List<Entry>> ByShip = new Dictionary<uint, List<Entry>>();

        public static void Forget()
        {
            Runtimes.Clear();
            Generators.Clear();
            ByShip.Clear();
        }

        public static void BeforeTransfers(PowerServerTracker power, ShipsServerTracker ships, ConnectionsServerTracker connections, float deltaTime)
        {
            Generators.Clear();
            ByShip.Clear();
            float now = Time.unscaledTime;
            foreach (TrackedShipServer ship in ships.LoadedShipsWithChildren)
            {
                foreach (StatefulPart part in ship.GetAllStatefulParts<ConstantState>())
                {
                    Tier tier = TierOf(part.Settings.id);
                    if (tier == null)
                        continue;
                    if (!Runtimes.TryGetValue((ship.Id, part.Id), out Runtime rt) || rt.Tier != tier)
                    {
                        rt = new Runtime { Tier = tier };
                        Runtimes[(ship.Id, part.Id)] = rt;
                    }
                    float capacity = tier.Capacity;
                    if (rt.Charge < 0f)
                    {
                        // From the save: the charge, or the time left before restarting
                        double saved = ((ConstantState)part.State).ConstantValue;
                        rt.Charge = saved > 0 ? Mathf.Clamp01((float)saved / 100f) * capacity : saved <= -2 ? 0f : capacity;
                        if (saved <= -2)
                            rt.CooldownUntil = now + (float)(-saved - 1);
                    }
                    rt.Group = null;
                    rt.PendingPower = 0f;
                    rt.Up = false;
                    var entry = new Entry { Ship = ship, Part = part, Runtime = rt };
                    Generators.Add(entry);
                    Prepare(power, connections, entry, deltaTime, now);
                }
            }
        }

        private static void Prepare(PowerServerTracker power, ConnectionsServerTracker connections, Entry entry, float deltaTime, float now)
        {
            Runtime rt = entry.Runtime;
            if (now < rt.CooldownUntil)
                return;
            if (rt.Charge <= 0f && rt.CooldownUntil > 0f)
            {
                // Restarted after the breakdown
                rt.Charge = rt.Tier.Capacity;
                rt.CooldownUntil = 0f;
            }
            if (!power.TryGetGroupFromSocket(new EntityPartSocket(entry.Ship.Id, entry.Part.Id, PowerCatalog.MachinePowerA), out PowerGroupServerState group)
                && !power.TryGetGroupFromSocket(new EntityPartSocket(entry.Ship.Id, entry.Part.Id, PowerCatalog.MachinePowerB), out group))
                return;
            var oxygenSocket = new EntityPartSocket(entry.Ship.Id, entry.Part.Id, OxygenSocket);
            float oxygen = rt.Tier.FluidPerSecond * deltaTime;
            if (!connections.TryGetFluidGroupCacheFromSocket(oxygenSocket, out FluidGroupServerState fluid) || fluid.FluidItemId != rt.Tier.Fluid || fluid.Available < oxygen)
                return;
            // It draws its power like a consumer; it runs if the network had it last tick
            float watts = rt.Tier.Watts * deltaTime;
            group.TotalPowerNeeded += watts;
            rt.Group = group;
            rt.PendingPower = watts;
            fluid.RemoveFluid(oxygen);
        }

        public static void AfterTransfers(float deltaTime)
        {
            float now = Time.unscaledTime;
            foreach (Entry entry in Generators)
            {
                Runtime rt = entry.Runtime;
                bool cooling = now < rt.CooldownUntil;
                rt.Up = !cooling && rt.Group != null && rt.Group.NormalizedConsumerRate > 0.95f;
                if (rt.Up)
                {
                    rt.Charge = Mathf.Min(rt.Tier.Capacity, rt.Charge + rt.Tier.Regen * deltaTime);
                    if (!ByShip.TryGetValue(entry.Ship.Id, out List<Entry> list))
                        ByShip[entry.Ship.Id] = list = new List<Entry>();
                    list.Add(entry);
                }
                Write(entry, now);
            }
        }

        /// <summary>
        /// Host: a hit of <paramref name="damage"/> points on the ship or anything docked to it.
        /// True when a field took it (the hull is spared).
        /// </summary>
        public static bool TryAbsorb(ShipsServerTracker ships, uint shipId, float damage)
        {
            if (ByShip.Count == 0 || damage <= 0f)
                return false;
            Entry? holder = null;
            foreach (var data in ships.GetRootConnectedShips(shipId))
            {
                if (ByShip.TryGetValue(data.Ship.Id, out List<Entry> list))
                {
                    Entry best = list.OrderByDescending(e => e.Runtime.Charge).First();
                    if (best.Runtime.Up && best.Runtime.Charge > 0f && (holder == null || best.Runtime.Charge > holder.Value.Runtime.Charge))
                        holder = best;
                }
            }
            if (holder == null)
                return false;
            Runtime rt = holder.Value.Runtime;
            rt.Charge -= damage;
            if (rt.Charge <= 0f)
            {
                rt.Charge = 0f;
                rt.Up = false;
                rt.CooldownUntil = Time.unscaledTime + Mathf.Max(1f, PowerCatalog.ShieldCooldownSeconds.Value);
                ByShip[holder.Value.Ship.Id].Remove(holder.Value);
                AdvancedMod.Log.Msg($"force field {holder.Value.Ship.Id}:{holder.Value.Part.Id} broke down, back in {PowerCatalog.ShieldCooldownSeconds.Value:0} s");
            }
            Write(holder.Value, Time.unscaledTime, force: true);
            return true;
        }

        /// <summary>Host: whether a working field covers the ship or anything docked to it.</summary>
        public static bool IsUp(ShipsServerTracker ships, uint shipId)
        {
            if (ByShip.Count == 0)
                return false;
            foreach (var data in ships.GetRootConnectedShips(shipId))
            {
                if (ByShip.TryGetValue(data.Ship.Id, out List<Entry> list) && list.Any(e => e.Runtime.Up && e.Runtime.Charge > 0f))
                    return true;
            }
            return false;
        }

        private static void Write(Entry entry, float now, bool force = false)
        {
            Runtime rt = entry.Runtime;
            double value = now < rt.CooldownUntil ? -Mathf.Ceil(rt.CooldownUntil - now) - 1
                : rt.Up ? System.Math.Max(0.1, System.Math.Round(rt.Charge / rt.Tier.Capacity * 1000.0) / 10.0) : -1;
            bool kindChanged = double.IsNaN(rt.Written) || System.Math.Sign(value) != System.Math.Sign(rt.Written) || (value == -1) != (rt.Written == -1);
            if (!force && !kindChanged && (System.Math.Abs(value - rt.Written) < 0.5 || now - rt.LastWrite < WriteInterval))
                return;
            rt.LastWrite = now;
            rt.Written = value;
            var state = (ConstantState)entry.Part.State;
            entry.Ship.UpdatePartState(entry.Part.Id, state with { ConstantValue = value });
        }
    }

    /// <summary>Collision damage: an impact the ship's force field takes does not break any part.</summary>
    [HarmonyPatch(typeof(ShipCollisionDestruction), "ProcessCollisionContact")]
    internal static class ForceFieldCollisionPatch
    {
        private static ShipsServerTracker _ships;

        private static bool Prefix(ContactPoint contactPoint, Ships.Interface.Model.State.IShipStateWrite ____ship, ShipRigidbody ___shipRigidbody)
        {
            try
            {
                if (____ship == null || contactPoint.otherCollider == null)
                    return true;
                if (_ships == null)
                    _ships = Core.Services.ServiceLocator.GetService<ShipsServerTracker>();
                // Damage points: the impact's velocity change, in m/s
                float damage = contactPoint.impulse.magnitude / Mathf.Max(1f, ___shipRigidbody.Mass);
                if (damage < 0.5f)
                    return true;
                return !ForceField.TryAbsorb(_ships, ____ship.Id, damage);
            }
            catch (System.Exception e)
            {
                ErrorLog.Report("force field", e);
                return true;
            }
        }
    }
}

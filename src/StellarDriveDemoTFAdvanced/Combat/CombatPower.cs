using System.Collections.Generic;
using Ships;
using Ships.Cables.Power;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;
using Ships.Services;
using StellarDriveDemoTFAdvanced.Power;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Combat
{
    /// <summary>
    /// Host simulation of the combat machines (radars and stealth generators): each one draws its
    /// power like a consumer and runs when its network can give all of it. Their state is one
    /// value in the part's ConstantState (saved, synchronized, emitted on the signal socket):
    /// a radar's number of contacts (0 and up) while it runs, a stealth generator's 1; off: -1 no
    /// power network, -3 not enough power (it then stops asking for 5 s, not to starve the ship).
    /// After each power tick, the ships (whole assemblies, by root id) a radar watches over and
    /// those a stealth generator hides are known.
    /// </summary>
    internal static class CombatPower
    {
        private const float WriteInterval = 0.5f;

        private sealed class Runtime
        {
            public PowerGroupServerState Group;
            public bool Up;
            public int Status = -1;
            public float LowPower, RetryAt;
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
        private static readonly List<Entry> Machines = new List<Entry>();
        private static ShipsServerTracker _ships;

        /// <summary>Root ship id -> range (m) of its best working radar.</summary>
        public static readonly Dictionary<uint, float> RadarRanges = new Dictionary<uint, float>();
        /// <summary>Root ship ids hidden by a working stealth generator.</summary>
        public static readonly HashSet<uint> CloakedRoots = new HashSet<uint>();

        public static void Forget()
        {
            Runtimes.Clear();
            Machines.Clear();
            RadarRanges.Clear();
            CloakedRoots.Clear();
            _ships = null;
        }

        private static float Watts(ushort id)
        {
            Radar.Tier tier = Radar.TierOf(id);
            if (tier != null)
                return Mathf.Max(0f, PowerCatalog.RadarWatts.Value) * tier.WattsScale;
            return id == PowerCatalog.CloakId ? Mathf.Max(0f, PowerCatalog.CloakWatts.Value) : -1f;
        }

        public static void BeforeTransfers(PowerServerTracker power, ShipsServerTracker ships, float deltaTime)
        {
            Machines.Clear();
            _ships = ships;
            float now = Time.unscaledTime;
            foreach (TrackedShipServer ship in ships.LoadedShipsWithChildren)
            {
                foreach (StatefulPart part in ship.GetAllStatefulParts<ConstantState>())
                {
                    float watts = Watts(part.Settings.id);
                    if (watts < 0f)
                        continue;
                    if (!Runtimes.TryGetValue((ship.Id, part.Id), out Runtime rt))
                        Runtimes[(ship.Id, part.Id)] = rt = new Runtime();
                    rt.Group = null;
                    rt.Up = false;
                    Machines.Add(new Entry { Ship = ship, Part = part, Runtime = rt });
                    if (!power.TryGetGroupFromSocket(new EntityPartSocket(ship.Id, part.Id, PowerCatalog.MachinePowerA), out PowerGroupServerState group)
                        && !power.TryGetGroupFromSocket(new EntityPartSocket(ship.Id, part.Id, PowerCatalog.MachinePowerB), out group))
                    {
                        rt.Status = -1;
                        continue;
                    }
                    if (now < rt.RetryAt)
                    {
                        rt.Status = -3;
                        continue;
                    }
                    group.TotalPowerNeeded += watts * deltaTime;
                    rt.Group = group;
                }
            }
        }

        public static void AfterTransfers(float deltaTime)
        {
            float now = Time.unscaledTime;
            RadarRanges.Clear();
            CloakedRoots.Clear();
            foreach (Entry entry in Machines)
            {
                Runtime rt = entry.Runtime;
                rt.Up = rt.Group != null && rt.Group.NormalizedConsumerRate > 0.9f;
                if (rt.Group != null && !rt.Up)
                {
                    rt.Status = -3;
                    rt.LowPower += deltaTime;
                    if (rt.LowPower > 1.5f)
                    {
                        rt.LowPower = 0f;
                        rt.RetryAt = now + 5f;
                    }
                }
                else if (rt.Up)
                {
                    rt.LowPower = 0f;
                }
                uint root = Root(entry.Ship.Id);
                double value = rt.Status;
                Radar.Tier tier = Radar.TierOf(entry.Part.Settings.id);
                if (rt.Up && tier != null)
                {
                    float range = tier.RangeKm * 1000f;
                    if (!RadarRanges.TryGetValue(root, out float best) || range > best)
                        RadarRanges[root] = range;
                    value = Radar.ContactCount(root, range);
                }
                else if (rt.Up)
                {
                    CloakedRoots.Add(root);
                    value = 1;
                }
                Write(entry, value, now);
            }
        }

        private static uint Root(uint shipId) =>
            _ships != null && _ships.TryGetTrackedRootShip(shipId, out TrackedShipServer root) && root != null ? root.Id : shipId;

        private static void Write(Entry entry, double value, float now)
        {
            Runtime rt = entry.Runtime;
            if (value == rt.Written || (!double.IsNaN(rt.Written) && System.Math.Sign(value) == System.Math.Sign(rt.Written) && now - rt.LastWrite < WriteInterval))
                return;
            rt.LastWrite = now;
            rt.Written = value;
            var state = (ConstantState)entry.Part.State;
            entry.Ship.UpdatePartState(entry.Part.Id, state with { ConstantValue = value });
        }
    }
}

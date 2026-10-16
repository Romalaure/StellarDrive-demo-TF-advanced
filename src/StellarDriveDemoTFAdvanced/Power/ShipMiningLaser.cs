using System;
using System.Collections.Generic;
using System.Linq;
using Core.Services;
using Items.Model;
using Planets.Interface.Model;
using Planets.Network;
using Planets.Objects.Common.Model;
using Planets.Rings.Asteroid;
using Planets.Services;
using SDModKit.Game;
using Ships;
using Ships.Cables;
using Ships.Cables.Model;
using Ships.Cables.Power;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Services;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;
using WorldObjects.Interface.Physics;
using WorldObjects.Services;
using WorldTracking.Interface.Values;

namespace StellarDriveDemoTFAdvanced.Power
{
    /// <summary>
    /// The ship mining laser (host simulation): the player's mining laser, mounted on the hull. Its
    /// beam leaves the part along +Y (out of the surface it is placed on) and mines the rocks,
    /// crystals and asteroid ores it hits like the hand tool, only harder and further; what they
    /// give goes into the chests on the item line plugged in its item socket, never to a player.
    /// It fires while powered and commanded (no cable on its command socket: always), and only
    /// with a chest on its line. Its ConstantState value (emitted on its status socket) tells
    /// every client what it does, for the beam: see the status constants.
    /// </summary>
    internal static class ShipMiningLaser
    {
        public const ushort Id = 7399;
        public const byte StatusSocket = PowerCatalog.MachineSignal;
        public const byte CommandSocket = 3;
        public const byte ItemSocket = 4;

        // ConstantValue: off, beam out (nothing to mine in range), mining; below zero, why it is off
        public const double Off = 0, Firing = 1, Mining = 2, NoPower = -1, NoChest = -2, ChestFull = -3;

        /// <summary>Where the beam leaves the part, in part space.</summary>
        public static readonly Vector3 Muzzle = new Vector3(0f, 1.02f, 0f);
        public static readonly Vector3 Beam = Vector3.up;

        private static readonly int Mask = LayerMask.GetMask("Default", "Ship", "WorldObject");
        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        private sealed class Runtime
        {
            public bool Commanded;
            public PowerGroupServerState Group;
            public double Written = double.NaN;
            public float LastWrite = -10f;
        }

        private struct Entry
        {
            public TrackedShipServer Ship;
            public StatefulPart Part;
            public Runtime Runtime;
        }

        private static readonly Dictionary<(uint, ushort), Runtime> Runtimes = new Dictionary<(uint, ushort), Runtime>();
        private static readonly List<Entry> Lasers = new List<Entry>();
        private static ShipsServerTracker _ships;
        private static ConnectionsServerTracker _connections;

        public static void Forget()
        {
            Runtimes.Clear();
            Lasers.Clear();
            _ships = null;
            _connections = null;
        }

        public static void BeforeTransfers(PowerServerTracker power, ShipsServerTracker ships, ConnectionsServerTracker connections, float deltaTime)
        {
            Lasers.Clear();
            _ships = ships;
            _connections = connections;
            foreach (TrackedShipServer ship in ships.LoadedShipsWithChildren)
            {
                foreach (StatefulPart part in ship.GetAllStatefulParts<ConstantState>())
                {
                    if (part.Settings.id != Id)
                        continue;
                    if (!Runtimes.TryGetValue((ship.Id, part.Id), out Runtime rt))
                        Runtimes[(ship.Id, part.Id)] = rt = new Runtime();
                    rt.Group = null;
                    Lasers.Add(new Entry { Ship = ship, Part = part, Runtime = rt });
                    // Command: no cable means on; with a cable, a signal above 0
                    var command = new EntityPartSocket(ship.Id, part.Id, CommandSocket);
                    rt.Commanded = !connections.IsSocketPhysicallyOccupied(command) || connections.GetSignalValue(command) > 0.0;
                    if (!rt.Commanded)
                        continue;
                    if (!power.TryGetGroupFromSocket(new EntityPartSocket(ship.Id, part.Id, PowerCatalog.MachinePowerA), out PowerGroupServerState group)
                        && !power.TryGetGroupFromSocket(new EntityPartSocket(ship.Id, part.Id, PowerCatalog.MachinePowerB), out group))
                        continue;
                    group.TotalPowerNeeded += Mathf.Max(0f, PowerCatalog.MiningLaserWatts.Value) * deltaTime;
                    rt.Group = group;
                }
            }
        }

        public static void AfterTransfers(float deltaTime)
        {
            float now = Time.unscaledTime;
            foreach (Entry entry in Lasers)
            {
                Runtime rt = entry.Runtime;
                double status;
                try
                {
                    status = Run(entry, deltaTime);
                }
                catch (Exception e)
                {
                    ErrorLog.Report("ship mining laser", e);
                    status = Off;
                }
                if (status == rt.Written || now - rt.LastWrite < 0.25f)
                    continue;
                rt.LastWrite = now;
                rt.Written = status;
                var state = (ConstantState)entry.Part.State;
                entry.Ship.UpdatePartState(entry.Part.Id, state with { ConstantValue = status });
            }
        }

        private static double Run(Entry entry, float deltaTime)
        {
            Runtime rt = entry.Runtime;
            if (!rt.Commanded)
                return Off;
            float supply = rt.Group != null ? Mathf.Clamp01(rt.Group.NormalizedConsumerRate) : 0f;
            if (supply < 0.5f)
                return NoPower;
            List<(TrackedShipServer Ship, ushort Part)> chests = Chests(entry.Ship.Id, entry.Part.Id);
            if (chests.Count == 0)
                return NoChest;
            IMinableObject target = Target(entry.Ship.Id, entry.Part);
            if (target == null)
                return Firing;
            float damage = Mathf.Max(0f, PowerCatalog.MiningLaserDamagePerSecond.Value) * supply * deltaTime;
            if (target.CurrentHealth - damage > 0.01f)
            {
                // The rock's own health, so its health bar and cracks show the laser's progress
                target.ReduceHealth(damage);
                return Mining;
            }
            return Harvest((Component)target, chests) ? Mining : ChestFull;
        }

        /// <summary>
        /// The first thing the beam meets, if it can be mined. Cast in the client physics scene the
        /// game's mining laser uses, from where the ship's rigidbody is there (host only).
        /// </summary>
        public static IMinableObject Target(uint shipId, StatefulPart part)
        {
            if (!Ray(shipId, part, out Ray ray, out Transform shipBody))
                return null;
            return Cast(ray, shipBody, out RaycastHit hit) && hit.collider.CompareTag("PlanetSurfaceObject")
                ? hit.collider.GetComponentInParent<IMinableObject>()
                : null;
        }

        /// <summary>The beam in the client physics scene: from the muzzle, out of the part's surface.</summary>
        public static bool Ray(uint shipId, StatefulPart part, out Ray ray, out Transform shipBody)
        {
            ray = default;
            shipBody = null;
            ShipsClientTracker client = GameServices.ShipsClient;
            if (client == null || !client.TryGetTrackedShip(shipId, out TrackedShipClient ship) || ship == null)
                return false;
            // A ship just loaded has no rigidbody yet (its getter throws until then)
            try
            {
                shipBody = ship.ShipRigidBody != null ? ship.ShipRigidBody.transform : null;
            }
            catch (NullReferenceException)
            {
                shipBody = null;
            }
            if (shipBody == null)
                return false;
            ray = new Ray(shipBody.TransformPoint(part.LocalPosition + part.LocalRotation * Muzzle), shipBody.TransformDirection(part.LocalRotation * Beam));
            return true;
        }

        /// <summary>The nearest hit along the beam, the laser's own ship left out.</summary>
        public static bool Cast(Ray ray, Transform shipBody, out RaycastHit nearest)
        {
            nearest = default;
            float range = Mathf.Max(1f, PowerCatalog.MiningLaserRange.Value);
            int count = ClientScene.PhysicsScene.Raycast(ray.origin, ray.direction, Hits, range, Mask, QueryTriggerInteraction.UseGlobal);
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = Hits[i];
                if (hit.collider == null || (shipBody != null && hit.collider.transform.IsChildOf(shipBody)))
                    continue;
                if (!found || hit.distance < nearest.distance)
                {
                    nearest = hit;
                    found = true;
                }
            }
            return found;
        }

        // ---- The item line

        /// <summary>The parts with an inventory on the item line plugged in the laser's item socket.</summary>
        private static List<(TrackedShipServer Ship, ushort Part)> Chests(uint shipId, ushort partId)
        {
            var chests = new List<(TrackedShipServer, ushort)>();
            if (_connections == null || _ships == null
                || !ItemCables.TryGetGroup(_connections, new EntityPartSocket(shipId, partId, ItemSocket), out ConnectionGroup group))
                return chests;
            foreach (EntityPartSocket socket in group.Sockets.Keys)
            {
                if (socket.EntityId == shipId && socket.Socket.PartId == partId)
                    continue;
                if (!_ships.TryGetTrackedShip(socket.EntityId, out TrackedShipServer ship) || !ship.TryGetStatefulPart(socket.Socket.PartId, out StatefulPart part))
                    continue;
                if (part.State is IStoragePart && !chests.Any(c => c.Item1 == ship && c.Item2 == part.Id))
                    chests.Add((ship, part.Id));
            }
            return chests;
        }

        /// <summary>Puts the whole drop in the chests (stacks first, then empty slots), or nothing if it does not fit.</summary>
        private static bool Store(ItemDrop drop, List<(TrackedShipServer Ship, ushort Part)> chests)
        {
            if (drop.Quantity == 0)
                return true;
            byte stack = ItemMove.MaxStack(drop.ItemId);
            var storages = new List<(TrackedShipServer Ship, ushort Part, IStoragePart Storage)>();
            int room = 0;
            foreach ((TrackedShipServer ship, ushort partId) in chests)
            {
                if (!ship.TryGetStatefulPart(partId, out StatefulPart part) || !(part.State is IStoragePart storage) || !ItemMove.Accepts(storage, drop.ItemId))
                    continue;
                storages.Add((ship, partId, storage));
                for (byte i = 0; i < storage.SlotCount; i++)
                {
                    InventorySlot slot = storage.GetSlot(i);
                    if (!slot.HasItem || slot.Quantity == 0)
                        room += stack;
                    else if (slot.ItemId == drop.ItemId)
                        room += Mathf.Max(0, stack - slot.Quantity);
                }
            }
            if (room < drop.Quantity)
                return false;
            int left = drop.Quantity;
            // Topping up the stacks already there before opening new ones
            foreach (bool fresh in new[] { false, true })
            {
                foreach ((TrackedShipServer ship, ushort partId, IStoragePart start) in storages)
                {
                    if (left <= 0)
                        break;
                    ship.TryGetStatefulPart(partId, out StatefulPart current);
                    IStoragePart storage = current.State as IStoragePart ?? start;
                    bool changed = false;
                    for (byte i = 0; i < storage.SlotCount && left > 0; i++)
                    {
                        InventorySlot slot = storage.GetSlot(i);
                        bool empty = !slot.HasItem || slot.Quantity == 0;
                        if (fresh != empty || !empty && slot.ItemId != drop.ItemId)
                            continue;
                        int put = Mathf.Min(left, stack - (empty ? 0 : slot.Quantity));
                        if (put <= 0)
                            continue;
                        slot.HasItem = true;
                        slot.ItemId = drop.ItemId;
                        slot.Quantity = (byte)((empty ? 0 : slot.Quantity) + put);
                        storage = storage.WithUpdatedSlot(i, slot);
                        left -= put;
                        changed = true;
                    }
                    if (changed)
                        ship.UpdatePartState(partId, storage);
                }
            }
            return true;
        }

        // ---- Mining, as the game's server does it for the hand tool, the drop into the chests

        private static bool Harvest(Component target, List<(TrackedShipServer Ship, ushort Part)> chests)
        {
            var reference = target.GetComponent<PlanetObjectReference>();
            if (reference != null && reference.Id != null)
            {
                var planets = ServiceLocator.GetService<PlanetsServerTracker>();
                var colliders = ServiceLocator.GetService<PlanetCollidersServer>();
                if (planets == null || colliders == null)
                    return true;
                ulong planetId = PlanetOf(reference.Id);
                TrackedPlanetServer planet = planets.GetPlanetServer(planetId);
                if (planet == null)
                    return true;
                if (!Store(colliders.GetSpawnedObjectItemDrop(reference.Id), chests))
                    return false;
                switch (reference.Id)
                {
                    case DenseObjectId dense:
                        planet.MineDenseObject(dense.PlacedObjectId);
                        break;
                    case HotspotObjectId hotspot:
                        planet.DestroyHotspotObject(hotspot.PlacedObjectId);
                        break;
                    case StaticObjectId stat:
                        planet.DestroyStaticObject(stat.Index);
                        break;
                    case AsteroidResourcePlanetObjectId asteroid:
                        planet.DestroyAsteroidResource(asteroid.ResourceId);
                        break;
                }
                return true;
            }
            var provider = target.GetComponent<IWorldObjectIdProvider>();
            var worldObjects = ServiceLocator.GetService<WorldObjectsServerTracker>();
            if (provider == null || worldObjects == null || !worldObjects.TryGetWorldObjectItemContent(provider.Id, out ItemDrop item))
                return true;
            if (!Store(item, chests))
                return false;
            worldObjects.RemoveWorldObject(provider.Id);
            return true;
        }

        private static ulong PlanetOf(IPlanetObjectId id)
        {
            switch (id)
            {
                case DenseObjectId dense: return dense.PlanetId;
                case HotspotObjectId hotspot: return hotspot.PlanetId;
                case StaticObjectId stat: return stat.PlanetId;
                case AsteroidResourcePlanetObjectId asteroid: return asteroid.PlanetId;
                default: return 0;
            }
        }
    }
}

using System.Collections.Generic;
using Core.Values;
using Planets.Interface.Model;
using Planets.Interface.Services;
using Players.Interface.Model;
using Players.Services;
using Ships;
using Ships.Cables;
using Ships.Cables.Power;
using Ships.Interface.Model.Joints;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Interface.Model.Placement;
using Ships.Interface.Model.State;
using Ships.Parts.MainControl.Hologram;
using Ships.Services;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;
using WorldTracking.Interface.Model.Spaces;
using WorldTracking.Interface.Model.Spaces.Common;
using WorldTracking.Interface.Services;

namespace StellarDriveDemoTFAdvanced.ShipComputer
{
    /// <summary>
    /// Reads the active ship into a <see cref="TelemetrySnapshot"/>. Runs on the client side, so it
    /// works the same for the host and for joined players; the host additionally reads the server's
    /// power networks. Constants mirror the game's own (ShipThrust, ShipThrustersUpdater, the
    /// generator and machine updaters) so derived values match what the game simulates.
    /// </summary>
    internal sealed class TelemetrySampler
    {
        // Ships.Physics.Forces.ShipThrust and the thruster updaters
        private const float ChemicalThrustPerStrength = ThrusterState.EthanolXOxygenStrength;
        private const float WoodThrust = WoodThrusterState.ThrustStrength;
        private const float PropellantRate = ThrusterState.MaxFluidConsumptionRate;
        private const float GeneratorWatts = WoodPowerGeneratorState.PowerProductionRate;
        private const float ElectrolyzerWatts = ElectrolyzerState.PowerConsumptionRate;
        private const float DistillerWatts = EthanolDistillerState.PowerConsumptionRate;
        // Socket ids the game's updaters read
        private const byte GyroPowerSocket = 3;
        private const byte GeneratorPowerSocket = 0;
        private const byte ElectrolyzerPowerSocket = 1;
        private const byte DistillerPowerSocket = 2;

        private readonly Dictionary<uint, IShipVelocityProvider> _velocityProviders = new Dictionary<uint, IShipVelocityProvider>();
        private readonly Dictionary<uint, PowerNetworkReading> _networks = new Dictionary<uint, PowerNetworkReading>();
        private readonly Dictionary<uint, PowerGroupServerState> _serverGroups = new Dictionary<uint, PowerGroupServerState>();
        private readonly HashSet<uint> _connectedIds = new HashSet<uint>();
        private readonly List<RootRelativeShipData> _connected = new List<RootRelativeShipData>();

        // Previous attitude, to derive the turn rate between two readings
        private uint _lastShipId;
        private uint _lastSpaceId;
        private Quaternion _lastRotation;
        private float _lastTime = -1f;

        /// <summary>Reads the ship tree whose root is <paramref name="rootId"/> (the ship a dashboard is mounted on, resolved to its root).</summary>
        public void Sample(TelemetrySnapshot snap, uint rootId)
        {
            snap.Clear();
            snap.IsHost = GameServices.IsServer;
            ShipsClientTracker ships = GameServices.ShipsClient;
            if (ships == null || !ships.TryGetTrackedRootShip(rootId, out TrackedShipClient root) || root.IsShipDestroyed)
            {
                snap.NoShipReason = "Vaisseau introuvable.";
                _lastTime = -1f;
                return;
            }

            snap.HasShip = true;
            snap.ShipId = root.Id;
            CollectConnected(root);
            LocalPilotSeat(out uint shipId, out ushort? seatId);
            snap.ShipSource = seatId.HasValue ? "piloté" : "au repos";
            snap.PartCount = root.PartCount;
            snap.Mass = root.GetMassIncludingConnectedShips();
            snap.ConnectedShipCount = _connected.Count - 1;

            ShipPhysicsState physics = root.VisualPhysicsState;
            ISpace space = null;
            GameServices.Spaces?.TryGetSpace(physics.SpaceId, out space);

            ReadMotion(snap, root, physics, space);
            ReadAttitude(snap, root, physics, space, shipId, seatId);
            ReadPropulsion(snap);
            ReadPower(snap);
            ReadTraffic(snap, root, physics);
        }

        // The seat the local player flies this ship tree from, if any: it defines forward for the attitude
        private void LocalPilotSeat(out uint shipId, out ushort? seatId)
        {
            shipId = 0;
            seatId = null;
            PlayersClientTracker players = GameServices.PlayersClient;
            if (players != null && players.TryGetLocalPlayerState(out PlayerState state) && state.Type == PlayerStateType.Piloting
                && _connectedIds.Contains(state.EntityId))
            {
                shipId = state.EntityId;
                seatId = state.PartId;
            }
        }

        private void CollectConnected(TrackedShipClient root)
        {
            _connected.Clear();
            _connectedIds.Clear();
            foreach (RootRelativeShipData data in GameServices.ShipsClient.GetRootConnectedShips(root.Id))
            {
                if (data.Ship != null && _connectedIds.Add(data.Ship.Id))
                    _connected.Add(data);
            }
            if (_connectedIds.Add(root.Id))
                _connected.Insert(0, new RootRelativeShipData(root));
        }

        private void ReadMotion(TelemetrySnapshot snap, TrackedShipClient root, ShipPhysicsState physics, ISpace space)
        {
            snap.SpaceKind = SpaceName(space);
            IShipVelocityProvider velocity = VelocityProvider(root);
            bool surface = space is PlanetSurfaceSpace || space is OrbitSpace || space is WarpSpace;
            bool inSpace = space is OrbitSpace || space is TrajectorySpace || space is WarpSpace;
            Vector3? surfaceVelocity = null;
            if (velocity != null)
            {
                if (surface)
                    surfaceVelocity = velocity.GetVisualVelocityRelativeTo(RelativeSpaceType.Surface);
                else if (space is PlanetRingSpace)
                    surfaceVelocity = velocity.GetVisualVelocityRelativeTo(RelativeSpaceType.Rings);
                if (inSpace)
                    snap.SpaceSpeed = velocity.GetVisualVelocityRelativeTo(RelativeSpaceType.Space).magnitude;
            }
            snap.SurfaceSpeed = surfaceVelocity?.magnitude;

            // The nearest of the mod's black holes: distance, its pull, and the clock rate there
            if (Worlds.BlackHole.TryNearest(GameServices.Transpose, physics.SpaceId, physics.Position, out Worlds.Hole hole, out Vector3 toHole)
                && toHole.magnitude < hole.InfluenceRadius)
            {
                snap.BlackHole = hole;
                snap.BlackHoleDistance = toHole.magnitude;
                snap.BlackHoleGravity = hole.Acceleration(toHole).magnitude;
                snap.BlackHoleTimeDilation = hole.TimeDilation(toHole.magnitude);
            }

            if (!TryPlanetUp(space, physics.Position, out IPlanetProperties planet, out Vector3 up, out double distance, out bool nearest))
                return;
            snap.PlanetName = nearest ? planet.Name + " (la plus proche)" : planet.Name;
            snap.Altitude = (float)(distance - planet.Radius);
            if (surfaceVelocity.HasValue)
                snap.VerticalSpeed = Vector3.Dot(surfaceVelocity.Value, up);
        }

        private void ReadAttitude(TelemetrySnapshot snap, TrackedShipClient root, ShipPhysicsState physics, ISpace space, uint shipId, ushort? seatId)
        {
            Quaternion frame = FlightFrame(shipId, seatId, out snap.AttitudeFrame);
            Quaternion flight = physics.Rotation * frame;

            if (TryPlanetUp(space, physics.Position, out _, out Vector3 up, out _, out _))
            {
                Vector3 forward = flight * Vector3.forward;
                Vector3 right = flight * Vector3.right;
                snap.Pitch = Mathf.Asin(Mathf.Clamp(Vector3.Dot(forward, up), -1f, 1f)) * Mathf.Rad2Deg;
                snap.Roll = Mathf.Asin(Mathf.Clamp(-Vector3.Dot(right, up), -1f, 1f)) * Mathf.Rad2Deg;
            }

            // Turn rate: not synchronized to clients, so derived from two successive orientations
            float now = Time.time;
            if (_lastTime >= 0f && _lastShipId == root.Id && _lastSpaceId == physics.SpaceId)
            {
                float dt = now - _lastTime;
                if (dt > 0.01f && dt < 2f)
                {
                    (physics.Rotation * Quaternion.Inverse(_lastRotation)).ToAngleAxis(out float angle, out Vector3 axis);
                    if (angle > 180f)
                        angle -= 360f;
                    Vector3 world = float.IsInfinity(axis.x) || float.IsNaN(axis.x) ? Vector3.zero : axis * (angle / dt);
                    snap.AngularVelocity = Quaternion.Inverse(flight) * world;
                }
            }
            _lastShipId = root.Id;
            _lastSpaceId = physics.SpaceId;
            _lastRotation = physics.Rotation;
            _lastTime = now;
        }

        // The pilot seat being used, else the first seat found; its +Z faces forward, +Y up
        private Quaternion FlightFrame(uint shipId, ushort? seatId, out string label)
        {
            foreach (RootRelativeShipData data in _connected)
            {
                if (seatId.HasValue && data.Ship.Id == shipId && data.Ship.TryGetStatefulPart(seatId.Value, out StatefulPart seat))
                {
                    label = "siège utilisé";
                    return data.RotationRelativeToRoot * seat.LocalRotation;
                }
            }
            foreach (RootRelativeShipData data in _connected)
            {
                foreach (StatefulPart seat in data.Ship.GetAllStatefulParts<PilotSeatState>())
                {
                    label = "siège pilote";
                    return data.RotationRelativeToRoot * seat.LocalRotation;
                }
                foreach (StatefulPart seat in data.Ship.GetAllStatefulParts<PilotSeatAdvState>())
                {
                    label = "commandes avancées";
                    return data.RotationRelativeToRoot * seat.LocalRotation;
                }
            }
            label = "grille du vaisseau";
            return Quaternion.identity;
        }

        private void ReadPropulsion(TelemetrySnapshot snap)
        {
            ConnectionsClientTracker connections = GameServices.ConnectionsClient;
            CablesClientTracker cables = GameServices.CablesClient;
            PowerServerTracker power = GameServices.PowerServer;
            foreach (RootRelativeShipData data in _connected)
            {
                IShipStateRead ship = data.Ship;
                foreach (StatefulPart part in ship.GetAllStatefulParts<ThrusterState>())
                {
                    float strength = Mathf.Max(0f, ((ThrusterState)part.State).ThrusterStrength);
                    // Propulsion Pack engines are Chemical Thrusters for the game, with their own thrust and flow
                    float thrustScale = 1f, flowScale = 1f;
                    string name = "Chimique";
                    if (Propulsion.EngineCatalog.TryGet(part.Settings.id, out Propulsion.EngineSpec engine))
                    {
                        thrustScale = engine.ThrustMultiplier;
                        flowScale = engine.FuelMultiplier;
                        name = engine.Label;
                    }
                    var reading = new ThrusterReading
                    {
                        Kind = ThrusterKind.Chemical,
                        Name = name,
                        ShipId = ship.Id,
                        PartId = part.Id,
                        Direction = data.RotationRelativeToRoot * -part.Placement.UpOrientation.GetVector3(),
                        Level = Mathf.Clamp01(strength),
                        Thrust = strength * ChemicalThrustPerStrength * thrustScale,
                        MaxThrust = ChemicalThrustPerStrength * thrustScale,
                        PropellantFlow = strength * PropellantRate * flowScale
                    };
                    if (connections != null)
                    {
                        var socketA = new EntityPartSocket(ship.Id, part.Id, 0);
                        var socketB = new EntityPartSocket(ship.Id, part.Id, 1);
                        reading.FuelSocketsConnected = connections.IsSocketPhysicallyOccupied(socketA) && connections.IsSocketPhysicallyOccupied(socketB);
                        reading.FluidA = CableFluid(connections, cables, socketA);
                        reading.FluidB = CableFluid(connections, cables, socketB);
                    }
                    AddThruster(snap, reading);
                }
                foreach (StatefulPart part in ship.GetAllStatefulParts<WoodThrusterState>())
                {
                    var state = (WoodThrusterState)part.State;
                    AddThruster(snap, new ThrusterReading
                    {
                        Kind = ThrusterKind.Wood,
                        Name = "Bois",
                        ShipId = ship.Id,
                        PartId = part.Id,
                        Direction = data.RotationRelativeToRoot * -part.Placement.UpOrientation.GetVector3(),
                        Level = Mathf.Clamp01(state.NormalizedThrustLevel),
                        Thrust = state.NormalizedThrustLevel * WoodThrust,
                        MaxThrust = WoodThrust,
                        Wood = state.GetWoodAvailable(),
                        TroubleStarting = state.IsHavingTroubleStarting
                    });
                }
                foreach (StatefulPart part in ship.GetAllStatefulParts<GyroscopeState>())
                {
                    Vector3 strength = ((GyroscopeState)part.State).RotationStrength;
                    var socket = new EntityPartSocket(ship.Id, part.Id, GyroPowerSocket);
                    var reading = new GyroReading
                    {
                        ShipId = ship.Id,
                        PartId = part.Id,
                        Strength = Mathf.Max(Mathf.Abs(strength.x), Mathf.Abs(strength.y), Mathf.Abs(strength.z)),
                        PowerConnected = connections != null && connections.IsSocketPhysicallyOccupied(socket)
                    };
                    if (power != null && power.TryGetGroupFromSocket(socket, out PowerGroupServerState group) && group.TotalPowerNeeded > 0f)
                        reading.PowerSatisfaction = group.NormalizedConsumerRate;
                    snap.Gyros.Add(reading);
                }
                foreach (StatefulPart part in ship.GetAllStatefulParts<PilotSeatState>())
                {
                    var state = (PilotSeatState)part.State;
                    Vector3 strength = state.InternalGyroStrength;
                    snap.Gyros.Add(new GyroReading
                    {
                        ShipId = ship.Id,
                        PartId = part.Id,
                        IsSeat = true,
                        SeatInUse = state.IsInUse,
                        Strength = Mathf.Max(Mathf.Abs(strength.x), Mathf.Abs(strength.y), Mathf.Abs(strength.z)),
                        PowerConnected = true
                    });
                }
                foreach (StatefulPart part in ship.GetAllStatefulParts<WarpDriveState>())
                {
                    var state = (WarpDriveState)part.State;
                    snap.WarpDrives.Add(new WarpReading { PartId = part.Id, State = WarpName(state.State), Timer = state.WarpStartTimer });
                }
            }
        }

        private static void AddThruster(TelemetrySnapshot snap, ThrusterReading reading)
        {
            snap.Thrusters.Add(reading);
            snap.NetThrust += reading.Direction * reading.Thrust;
            snap.MaxThrust += reading.MaxThrust;
        }

        private static uint CableFluid(ConnectionsClientTracker connections, CablesClientTracker cables, EntityPartSocket socket)
        {
            if (cables == null || !connections.TryGetSocketCable(socket, CableType.Fluid, out SocketConnection cable))
                return 0;
            return cables.TryGetFluidCableState(cable.CableId, out CableFluidState state) && state.FluidQuantity > 0f ? state.FluidItemId : 0;
        }

        private void ReadPower(TelemetrySnapshot snap)
        {
            ConnectionsClientTracker connections = GameServices.ConnectionsClient;
            CablesClientTracker cables = GameServices.CablesClient;
            PowerServerTracker power = GameServices.PowerServer;
            _serverGroups.Clear();
            _networks.Clear();

            foreach (RootRelativeShipData data in _connected)
            {
                IShipStateRead ship = data.Ship;
                foreach (StatefulPart part in ship.GetAllStatefulParts<WoodPowerGeneratorState>())
                {
                    var state = (WoodPowerGeneratorState)part.State;
                    var socket = new EntityPartSocket(ship.Id, part.Id, GeneratorPowerSocket);
                    snap.Generators.Add(new GeneratorReading
                    {
                        PartId = part.Id,
                        Level = state.NormalizedProductionLevel,
                        Watts = state.NormalizedProductionLevel * GeneratorWatts,
                        Wood = state.GetWoodAvailable(),
                        Connected = Plugged(connections, socket)
                    });
                    TrackServerGroup(power, socket);
                }
                foreach (StatefulPart part in ship.GetAllStatefulParts<ElectrolyzerState>())
                {
                    var state = (ElectrolyzerState)part.State;
                    var socket = new EntityPartSocket(ship.Id, part.Id, ElectrolyzerPowerSocket);
                    snap.Consumers.Add(new ConsumerReading
                    {
                        Name = "Électrolyseur",
                        PartId = part.Id,
                        Connected = Plugged(connections, socket),
                        Level = state.NormalizedProductionRate,
                        Watts = state.NormalizedProductionRate * ElectrolyzerWatts
                    });
                    TrackServerGroup(power, socket);
                }
                foreach (StatefulPart part in ship.GetAllStatefulParts<EthanolDistillerState>())
                {
                    var state = (EthanolDistillerState)part.State;
                    var socket = new EntityPartSocket(ship.Id, part.Id, DistillerPowerSocket);
                    snap.Consumers.Add(new ConsumerReading
                    {
                        Name = "Distillateur",
                        PartId = part.Id,
                        Connected = Plugged(connections, socket),
                        Level = state.NormalizedProductionRate,
                        Watts = state.NormalizedProductionRate * DistillerWatts
                    });
                    TrackServerGroup(power, socket);
                }
                foreach (StatefulPart part in ship.GetAllStatefulParts<GyroscopeState>())
                {
                    var socket = new EntityPartSocket(ship.Id, part.Id, GyroPowerSocket);
                    Vector3 strength = ((GyroscopeState)part.State).RotationStrength;
                    // The gyroscope's draw depends on its target, which the game does not keep: no watts here
                    snap.Consumers.Add(new ConsumerReading
                    {
                        Name = "Gyroscope",
                        PartId = part.Id,
                        Connected = Plugged(connections, socket),
                        Level = Mathf.Max(Mathf.Abs(strength.x), Mathf.Abs(strength.y), Mathf.Abs(strength.z))
                    });
                    TrackServerGroup(power, socket);
                }
                foreach (StatefulPart part in ship.GetAllStatefulParts<FluidTankState>())
                {
                    var tank = (FluidTankState)part.State;
                    if (part.Settings.id == Power.PowerCatalog.BatteryId)
                    {
                        var socket = new EntityPartSocket(ship.Id, part.Id, Power.PowerCatalog.BatteryPowerA);
                        var other = new EntityPartSocket(ship.Id, part.Id, Power.PowerCatalog.BatteryPowerB);
                        snap.Batteries.Add(new BatteryReading
                        {
                            PartId = part.Id,
                            Percent = tank.FluidQuantity,
                            Flow = tank.FluidFlow,
                            Connected = Plugged(connections, socket) || Plugged(connections, other)
                        });
                        TrackServerGroup(power, socket);
                    }
                    else if (part.Settings.id == Power.PowerCatalog.BreakerId)
                    {
                        snap.Breakers.Add(new BreakerReading
                        {
                            PartId = part.Id,
                            Status = Mathf.Round(tank.FluidQuantity),
                            Flow = tank.FluidFlow,
                            InputConnected = Plugged(connections, new EntityPartSocket(ship.Id, part.Id, Power.PowerCatalog.BreakerInput)),
                            OutputConnected = Plugged(connections, new EntityPartSocket(ship.Id, part.Id, Power.PowerCatalog.BreakerOutput)),
                            ControlWired = Plugged(connections, new EntityPartSocket(ship.Id, part.Id, Power.PowerCatalog.BreakerControl))
                        });
                        TrackServerGroup(power, new EntityPartSocket(ship.Id, part.Id, Power.PowerCatalog.BreakerInput));
                        TrackServerGroup(power, new EntityPartSocket(ship.Id, part.Id, Power.PowerCatalog.BreakerOutput));
                    }
                }
                foreach (StatefulPart part in ship.StatefulParts)
                {
                    // "Battery Bank" exists in the build menu but no game code reads or writes it
                    if (part.Settings != null && part.Settings.name == "Battery")
                        snap.DecorativeBatteries++;
                }
            }

            if (power != null)
            {
                snap.HostPowerDetails = true;
                float dt = PowerTickWatch.LastDeltaTime;
                foreach (PowerGroupServerState group in _serverGroups.Values)
                {
                    snap.Networks.Add(new PowerNetworkReading
                    {
                        Cables = group.CableIds.Count,
                        Watts = dt > 0f ? group.TotalPowerTransfer / dt : 0f,
                        Demand = dt > 0f ? group.TotalPowerNeeded / dt : (float?)null,
                        Capacity = dt > 0f ? group.TotalPowerProductionCapacity / dt : (float?)null,
                        Satisfaction = group.TotalPowerNeeded > 0f ? group.NormalizedConsumerRate : (float?)null
                    });
                }
                return;
            }

            // Joined player: what the cables carry, grouped by power network
            if (cables == null)
                return;
            foreach (RootRelativeShipData data in _connected)
            {
                foreach (CableData cable in cables.GetShipCables(data.Ship.Id))
                {
                    if (cable.State.Type != CableType.Power || !cables.TryGetCableGroupId(cable.Id, out uint groupId))
                        continue;
                    cables.TryGetCableWattsValue(cable.Id, out float watts);
                    _networks.TryGetValue(groupId, out PowerNetworkReading network);
                    network.Cables++;
                    network.Watts = Mathf.Max(network.Watts, watts);
                    _networks[groupId] = network;
                }
            }
            snap.Networks.AddRange(_networks.Values);
        }

        private void TrackServerGroup(PowerServerTracker power, EntityPartSocket socket)
        {
            if (power != null && power.TryGetGroupFromSocket(socket, out PowerGroupServerState group))
                _serverGroups[group.GroupId] = group;
        }

        private static bool Plugged(ConnectionsClientTracker connections, EntityPartSocket socket)
        {
            return connections != null && connections.IsSocketPhysicallyOccupied(socket);
        }

        private void ReadTraffic(TelemetrySnapshot snap, TrackedShipClient root, ShipPhysicsState physics)
        {
            float range = Mathf.Max(10f, Settings.TrafficRange.Value);
            foreach (TrackedShipClient other in GameServices.ShipsClient.GetAllTrackedShipsInSpace(physics.SpaceId))
            {
                if (other == null || _connectedIds.Contains(other.Id) || other.IsShipDestroyed)
                    continue;
                ShipPhysicsState otherPhysics = other.VisualPhysicsState;
                Vector3 offset = otherPhysics.Position - physics.Position;
                float distance = offset.magnitude;
                if (distance > range)
                    continue;
                Vector3 relativeVelocity = otherPhysics.Velocity - physics.Velocity;
                float speedSq = relativeVelocity.sqrMagnitude;
                var contact = new TrafficContact
                {
                    ShipId = other.Id,
                    Distance = distance,
                    ClosingSpeed = distance > 0.01f ? -Vector3.Dot(offset / distance, relativeVelocity) : 0f,
                    ClosestDistance = distance
                };
                if (speedSq > 1e-4f)
                {
                    float t = -Vector3.Dot(offset, relativeVelocity) / speedSq;
                    if (t > 0f)
                    {
                        contact.TimeToClosest = t;
                        contact.ClosestDistance = (offset + relativeVelocity * t).magnitude;
                    }
                }
                snap.Traffic.Add(contact);
            }
            snap.Traffic.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        }

        private IShipVelocityProvider VelocityProvider(TrackedShipClient root)
        {
            if (_velocityProviders.TryGetValue(root.Id, out IShipVelocityProvider provider) && provider is Object alive && alive != null)
                return provider;
            provider = root.IsVisualLoaded && root.VisualObject != null ? root.VisualObject.GetComponent<IShipVelocityProvider>() : null;
            if (provider != null)
                _velocityProviders[root.Id] = provider;
            return provider;
        }

        // Planet center seen from the ship's space, the same way the game's targeting computer locates planets
        private static bool TryPlanetUp(ISpace space, Vector3 position, out IPlanetProperties planet, out Vector3 up, out double distance, out bool nearest)
        {
            planet = null;
            up = Vector3.up;
            distance = 0;
            nearest = false;
            IPlanetsClientProvider planets = GameServices.Planets;
            IWorldTransposeClient transpose = GameServices.Transpose;
            if (space == null || planets == null || transpose == null)
                return false;
            if (space is IPlanetSpace planetSpace)
                planet = planets.GetPlanetProps(planetSpace.PlanetId);
            else if (space is OrbitSpace orbit)
                planet = planets.GetPlanetProps(orbit.PlanetId);
            else
            {
                planet = planets.GetNearestPlanet(space);
                nearest = true;
            }
            if (planet == null)
                return false;
            Vector3Double center = transpose.Position(planet.FixedWorldSpace, space, Vector3Double.Zero);
            Vector3Double offset = (Vector3Double)position - center;
            distance = offset.Magnitude;
            if (distance <= 0.0)
                return false;
            up = (Vector3)(offset / distance);
            return true;
        }

        private static string SpaceName(ISpace space)
        {
            switch (space)
            {
                case PlanetSurfaceSpace _: return "Surface planétaire";
                case OrbitSpace _: return "Orbite";
                case TrajectorySpace _: return "Trajectoire libre";
                case WarpSpace _: return "Distorsion (warp)";
                case PlanetRingSpace _: return "Anneau planétaire";
                case null: return null;
                default: return space.GetType().Name;
            }
        }

        private static string WarpName(WarpDriveStateType state)
        {
            switch (state)
            {
                case WarpDriveStateType.Idle: return "Repos";
                case WarpDriveStateType.WarmingUp: return "Préchauffage";
                case WarpDriveStateType.Warping: return "En distorsion";
                case WarpDriveStateType.WarpInterrupted: return "Interrompu";
                case WarpDriveStateType.CantWarpFromHere: return "Impossible ici";
                default: return state.ToString();
            }
        }
    }
}

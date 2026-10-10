using System.Collections.Generic;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.ShipComputer
{
    /// <summary>
    /// One reading of the active ship. Every value comes from the game's own state; a value the game
    /// does not expose in the current situation stays null and is shown as unavailable.
    /// </summary>
    internal sealed class TelemetrySnapshot
    {
        // Ship
        public bool HasShip;
        public string NoShipReason;
        public uint ShipId;
        public string ShipSource;
        public bool IsHost;
        public int PartCount;
        public int ConnectedShipCount;
        public float Mass;

        // Where the ship is
        public string SpaceKind;
        public string PlanetName;
        public float? Altitude;
        public float? SurfaceSpeed;
        public float? SpaceSpeed;
        public float? VerticalSpeed;

        // Attitude, in the flight frame (pilot seat) or the ship grid
        public string AttitudeFrame;
        public float? Pitch;
        public float? Roll;
        public Vector3? AngularVelocity;

        public readonly List<ThrusterReading> Thrusters = new List<ThrusterReading>();
        public readonly List<GyroReading> Gyros = new List<GyroReading>();
        public readonly List<WarpReading> WarpDrives = new List<WarpReading>();
        public Vector3 NetThrust;
        public float MaxThrust;

        public readonly List<GeneratorReading> Generators = new List<GeneratorReading>();
        public readonly List<ConsumerReading> Consumers = new List<ConsumerReading>();
        public readonly List<PowerNetworkReading> Networks = new List<PowerNetworkReading>();
        public bool HostPowerDetails;
        public int DecorativeBatteries;

        public readonly List<TrafficContact> Traffic = new List<TrafficContact>();

        public void Clear()
        {
            HasShip = false;
            NoShipReason = null;
            ShipId = 0;
            ShipSource = null;
            IsHost = false;
            PartCount = 0;
            ConnectedShipCount = 0;
            Mass = 0f;
            SpaceKind = null;
            PlanetName = null;
            Altitude = null;
            SurfaceSpeed = null;
            SpaceSpeed = null;
            VerticalSpeed = null;
            AttitudeFrame = null;
            Pitch = null;
            Roll = null;
            AngularVelocity = null;
            Thrusters.Clear();
            Gyros.Clear();
            WarpDrives.Clear();
            NetThrust = Vector3.zero;
            MaxThrust = 0f;
            Generators.Clear();
            Consumers.Clear();
            Networks.Clear();
            HostPowerDetails = false;
            DecorativeBatteries = 0;
            Traffic.Clear();
        }
    }

    internal enum ThrusterKind
    {
        Chemical,
        Wood
    }

    internal struct ThrusterReading
    {
        public ThrusterKind Kind;
        public string Name;
        public uint ShipId;
        public ushort PartId;
        /// <summary>Thrust direction in the root ship's frame.</summary>
        public Vector3 Direction;
        public float Level;
        public float Thrust;
        public float MaxThrust;
        /// <summary>Chemical: units per second of each propellant, from the game's consumption formula.</summary>
        public float PropellantFlow;
        /// <summary>Chemical: both propellant sockets (0 and 1) have something plugged in.</summary>
        public bool FuelSocketsConnected;
        /// <summary>Chemical: fluids seen in the cables plugged into sockets 0 and 1 (0 = empty or no cable).</summary>
        public uint FluidA;
        public uint FluidB;
        /// <summary>Wood: logs left (including the partly burnt one).</summary>
        public float Wood;
        public bool TroubleStarting;
    }

    internal struct GyroReading
    {
        public uint ShipId;
        public ushort PartId;
        public bool IsSeat;
        public bool SeatInUse;
        public float Strength;
        public bool PowerConnected;
        /// <summary>Host only: share of the requested power the network delivered (0-1), null elsewhere.</summary>
        public float? PowerSatisfaction;
    }

    internal struct WarpReading
    {
        public ushort PartId;
        public string State;
        public float Timer;
    }

    internal struct GeneratorReading
    {
        public ushort PartId;
        public float Watts;
        public float Level;
        public float Wood;
        public bool Connected;
    }

    internal struct ConsumerReading
    {
        public string Name;
        public ushort PartId;
        public bool Connected;
        /// <summary>Power drawn, from the game's own formula for that machine; null when the game has no formula to read back.</summary>
        public float? Watts;
        public float Level;
    }

    internal struct PowerNetworkReading
    {
        public int Cables;
        /// <summary>Power carried by the network's cables, as synchronized by the game (W).</summary>
        public float Watts;
        /// <summary>Host only, from the server's power tracker (W).</summary>
        public float? Demand;
        public float? Capacity;
        public float? Satisfaction;
    }

    internal struct TrafficContact
    {
        public uint ShipId;
        public float Distance;
        public float ClosingSpeed;
        public float? TimeToClosest;
        public float ClosestDistance;
    }
}

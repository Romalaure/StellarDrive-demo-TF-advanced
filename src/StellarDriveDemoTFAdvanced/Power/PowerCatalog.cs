using MelonLoader;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Power
{
    /// <summary>
    /// Power Grid Overhaul parts and their balancing. Both parts keep their state in the game's
    /// FluidTankState (saved with the ship, synchronized to every player, and emitted by the game
    /// on signal socket 2): FluidQuantity holds the battery charge in percent or the breaker
    /// status code, FluidFlow the power through the part in watts. No fluid ever touches them:
    /// they have no fluid socket.
    /// </summary>
    internal static class PowerCatalog
    {
        public const ushort BatteryId = 7321;
        public const ushort BreakerId = 7322;
        /// <summary>Artifacts: endless power from a quantum core.</summary>
        public const ushort ReactorId = 7323;
        /// <summary>Artifacts: a battery eight times bigger, charged and drained faster.</summary>
        public const ushort CryoBatteryId = 7324;
        public const float CryoBatteryScale = 8f;
        public const float CryoBatteryRateScale = 3f;
        /// <summary>Artifacts: force field generator.</summary>
        public const ushort ShieldId = 7325;
        /// <summary>Artifacts: makes oxygen from power.</summary>
        public const ushort CondenserId = 7326;
        /// <summary>Artifacts: a gyroscope with several gyroscopes' torque.</summary>
        public const ushort QuantumGyroId = 7327;

        // Force field generator and condenser: ConstantState (one value, saved, synchronized, emitted on
        // socket 0), not FluidTankState, which the game would take for a tank on their fluid socket
        public const string MachineStateClass = "ConstantState";
        public const byte MachineSignal = 0;
        public const byte MachinePowerA = 1;
        public const byte MachinePowerB = 2;
        public const byte MachineFluid = 3;

        // Socket ids
        public const byte BatteryPowerA = 0;
        public const byte BatteryPowerB = 1;
        public const byte StatusSignal = 2;
        public const byte BreakerInput = 0;
        public const byte BreakerOutput = 1;
        public const byte BreakerControl = 3;

        // Breaker status codes in FluidQuantity
        public const float BreakerClosed = 0f;
        public const float BreakerOpen = 1f;
        public const float BreakerTripped = 2f;

        public static MelonPreferences_Entry<float> BatteryCapacityKJ { get; private set; }
        public static MelonPreferences_Entry<float> BatteryChargeWatts { get; private set; }
        public static MelonPreferences_Entry<float> BatteryDischargeWatts { get; private set; }
        public static MelonPreferences_Entry<float> BatteryEfficiency { get; private set; }
        public static MelonPreferences_Entry<float> BreakerRatingWatts { get; private set; }
        public static MelonPreferences_Entry<float> BreakerTripSeconds { get; private set; }
        public static MelonPreferences_Entry<float> BreakerAutoResetSeconds { get; private set; }

        public static MelonPreferences_Entry<float> ReactorWatts { get; private set; }
        public static MelonPreferences_Entry<float> ShieldWatts { get; private set; }
        public static MelonPreferences_Entry<float> CondenserWatts { get; private set; }
        public static MelonPreferences_Entry<float> CondenserOxygenPerSecond { get; private set; }
        public static MelonPreferences_Entry<float> QuantumGyroFactor { get; private set; }
        public static MelonPreferences_Entry<float> ShieldOxygenPerSecond { get; private set; }
        public static MelonPreferences_Entry<float> ShieldCapacity { get; private set; }
        public static MelonPreferences_Entry<float> ShieldRegenPerSecond { get; private set; }
        public static MelonPreferences_Entry<float> ShieldCooldownSeconds { get; private set; }

        /// <summary>Parts whose FluidTankState carries a status, not fluid (no fluid mass).</summary>
        public static bool HoldsNoFluid(ushort partSettingsId) => IsPowerPart(partSettingsId) || partSettingsId == ShieldId || partSettingsId == CondenserId;

        public static bool IsPowerPart(ushort partSettingsId) =>
            partSettingsId == BatteryId || partSettingsId == BreakerId || partSettingsId == ReactorId || partSettingsId == CryoBatteryId;

        public static bool IsBattery(ushort partSettingsId) => partSettingsId == BatteryId || partSettingsId == CryoBatteryId;

        public static float CapacityJoules(ushort partSettingsId) => BatteryCapacityJoules * (partSettingsId == CryoBatteryId ? CryoBatteryScale : 1f);

        public static float RateScale(ushort partSettingsId) => partSettingsId == CryoBatteryId ? CryoBatteryRateScale : 1f;

        public static float BatteryCapacityJoules => Mathf.Max(1f, BatteryCapacityKJ.Value) * 1000f;

        public static void LoadSettings()
        {
            MelonPreferences_Category category = MelonPreferences.CreateCategory("TFAdvancedPower", "TF Advanced - power grid");
            // The wood generator gives 4 000 W, burning one log every 10 s at full load: 40 kJ per log
            BatteryCapacityKJ = category.CreateEntry("BatteryCapacityKJ", 1200f,
                description: "Battery capacity in kilojoules. Default: 5 minutes of a wood generator at full power (30 logs).");
            BatteryChargeWatts = category.CreateEntry("BatteryChargeWatts", 2000f,
                description: "Most power a battery takes from its network's spare generator capacity (W).");
            BatteryDischargeWatts = category.CreateEntry("BatteryDischargeWatts", 4000f,
                description: "Most power a battery gives when its network lacks power (W).");
            BatteryEfficiency = category.CreateEntry("BatteryEfficiency", 0.9f,
                description: "Share of the charging energy that is stored (0.5 to 1).");
            BreakerRatingWatts = category.CreateEntry("BreakerRatingWatts", 4000f,
                description: "Power a circuit breaker carries from its input network to its output network (W).");
            BreakerTripSeconds = category.CreateEntry("BreakerTripSeconds", 0.5f,
                description: "A breaker trips when its output network asks for more than its rating for this long (s).");
            ReactorWatts = category.CreateEntry("ReactorWatts", 12000f,
                description: "Power a quantum reactor gives its network, without fuel, without end (W). Three wood generators: 12 000 W.");
            CondenserWatts = category.CreateEntry("CondenserWatts", 1500f,
                description: "Power a cryonite condenser draws while it makes oxygen (W).");
            CondenserOxygenPerSecond = category.CreateEntry("CondenserOxygenPerSecond", 0.05f,
                description: "Oxygen a cryonite condenser puts in the line on its fluid socket (unit/s). A chemical thruster at full thrust burns 0.05.");
            QuantumGyroFactor = category.CreateEntry("QuantumGyroFactor", 5f,
                description: "Torque of a quantum gyroscope, in gyroscopes.");
            ShieldWatts = category.CreateEntry("ShieldWatts", 2000f,
                description: "Power a force field generator draws while its field is up (W).");
            ShieldOxygenPerSecond = category.CreateEntry("ShieldOxygenPerSecond", 0.02f,
                description: "Oxygen a force field generator draws from its fluid socket while its field is up (unit/s).");
            ShieldCapacity = category.CreateEntry("ShieldCapacity", 150f,
                description: "Damage a force field takes before breaking down, in points: an impact counts its velocity change in m/s, a part torn by black hole tides 2.");
            ShieldRegenPerSecond = category.CreateEntry("ShieldRegenPerSecond", 0.5f,
                description: "Charge a working force field regains per second (points).");
            ShieldCooldownSeconds = category.CreateEntry("ShieldCooldownSeconds", 300f,
                description: "After breaking down, the generator restarts after this long, fully charged (s).");
            BreakerAutoResetSeconds = category.CreateEntry("BreakerAutoResetSeconds", 10f,
                description: "A tripped breaker with no control cable closes again after this long (s). 0: only a control signal resets it.");
        }
    }
}

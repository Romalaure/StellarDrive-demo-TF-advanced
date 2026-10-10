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

        public static bool IsPowerPart(ushort partSettingsId) => partSettingsId == BatteryId || partSettingsId == BreakerId;

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
            BreakerAutoResetSeconds = category.CreateEntry("BreakerAutoResetSeconds", 10f,
                description: "A tripped breaker with no control cable closes again after this long (s). 0: only a control signal resets it.");
        }
    }
}

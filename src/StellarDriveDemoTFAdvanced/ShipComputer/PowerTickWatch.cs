using HarmonyLib;
using Ships.Cables.Power;

namespace StellarDriveDemoTFAdvanced.ShipComputer
{
    /// <summary>
    /// The server's power networks store energy per physics tick. Remembering the tick length the
    /// game used turns those values back into watts exactly as the game does for its cables.
    /// Read-only: nothing in the power simulation is changed.
    /// </summary>
    [HarmonyPatch(typeof(PowerServerTracker), nameof(PowerServerTracker.UpdatePowerTransfers))]
    internal static class PowerTickWatch
    {
        public static float LastDeltaTime { get; private set; }

        private static void Postfix(float deltaTime)
        {
            LastDeltaTime = deltaTime;
        }
    }
}

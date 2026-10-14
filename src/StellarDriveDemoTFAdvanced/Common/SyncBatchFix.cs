using HarmonyLib;
using Managers.Server.Network.Model;

namespace StellarDriveDemoTFAdvanced.Common
{
    /// <summary>
    /// The game sends every ship's position to every player 25 times a second, cut into batches
    /// of about 1000 bytes by an estimate of 28 bytes per space and 44 per ship. A far space (a
    /// station or wreck out in the Erebos galaxy, each in a space of its own) takes more than
    /// that: with enough of them a batch outgrows the message, gets cut, and the players misread
    /// everything after it ("Size of -216 is invalid"), ships included. A safer estimate makes
    /// smaller batches.
    /// </summary>
    [HarmonyPatch(typeof(UnreliableSyncData), nameof(UnreliableSyncData.ApproximateSizeInBytes), MethodType.Getter)]
    internal static class SyncBatchFix
    {
        private static void Postfix(UnreliableSyncData __instance, ref int __result)
        {
            if (__result > 0)
                __result = __instance.Spaces.Count * 72 + __instance.ShipPhysicsUpdates.Count * 64 + __instance.WorldObjectPhysicsUpdates.Count * 48;
        }
    }
}

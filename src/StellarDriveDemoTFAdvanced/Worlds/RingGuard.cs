using HarmonyLib;
using Planets.Rings;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Worlds
{
    /// <summary>
    /// A game bug: at the very outer edge of an asteroid ring the density lookup reads one sample
    /// past its table (IndexOutOfRangeException), and since it runs in the host's physics tick,
    /// a ship sitting on that edge stops the whole world. The lookup is clamped to the table.
    /// </summary>
    [HarmonyPatch(typeof(PlanetRing), nameof(PlanetRing.ReadDensitySampleAtDepthPos))]
    internal static class RingDensityGuardPatch
    {
        private static bool Prefix(PlanetRing __instance, double depthPos, ref float __result)
        {
            float[] samples = __instance.GetDensitySamples();
            if (samples == null || samples.Length == 0)
            {
                __result = 0f;
                return false;
            }
            float t = Mathf.Clamp01((float)(depthPos - __instance.InnerWidth) / Mathf.Max(1f, __instance.OuterWidth - __instance.InnerWidth));
            __result = samples[Mathf.Clamp(Mathf.FloorToInt(t * samples.Length), 0, samples.Length - 1)];
            return false;
        }
    }
}

using System.Collections;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Common
{
    /// <summary>Development check: frames per second and the slowest frames, measured at a few moments.</summary>
    internal static class FpsProbe
    {
        public static void Install()
        {
            foreach (float at in new[] { 20f, 35f })
            {
                float when = at;
                DevHarness.Steps.Add((when, "fps " + when, () => MelonLoader.MelonCoroutines.Start(Measure("at " + when + " s"))));
            }
        }

        public static IEnumerator Measure(string label)
        {
            int frames = 0;
            float worst = 0f, start = Time.unscaledTime, last = start;
            while (Time.unscaledTime - start < 4f)
            {
                yield return null;
                float now = Time.unscaledTime;
                worst = Mathf.Max(worst, now - last);
                last = now;
                frames++;
            }
            AdvancedMod.Log.Msg($"dev: fps {label}: {frames / (Time.unscaledTime - start):0.0}, slowest frame {worst * 1000f:0} ms");
        }
    }
}

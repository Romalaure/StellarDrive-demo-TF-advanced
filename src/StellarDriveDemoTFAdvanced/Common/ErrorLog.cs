using System;
using System.Collections.Generic;

namespace StellarDriveDemoTFAdvanced.Common
{
    /// <summary>
    /// Logs an error once per distinct message and scope, then counts repeats silently,
    /// so a failure that happens every frame does not flood the MelonLoader log.
    /// </summary>
    internal static class ErrorLog
    {
        private static readonly Dictionary<string, int> Seen = new Dictionary<string, int>();

        public static void Report(string scope, Exception e)
        {
            string key = scope + "|" + e.GetType().Name + "|" + e.Message;
            Seen.TryGetValue(key, out int count);
            Seen[key] = count + 1;
            if (count == 0)
                AdvancedMod.Log.Error($"{scope}: {e}");
            else if ((count + 1) % 500 == 0)
                AdvancedMod.Log.Warning($"{scope}: same error {count + 1} times: {e.Message}");
        }
    }
}

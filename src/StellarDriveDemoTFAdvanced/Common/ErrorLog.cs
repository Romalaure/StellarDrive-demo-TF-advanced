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

        /// <summary>Runs one part of the mod's update: if it fails, the parts after it still run.</summary>
        public static void Run(string scope, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Report(scope, e);
            }
        }

        /// <summary>Draws one window: a window failing half drawn would leave its scale, color or greyed state to the next ones.</summary>
        public static void RunGui(string scope, Action draw)
        {
            try
            {
                draw();
            }
            catch (UnityEngine.ExitGUIException)
            {
                throw;
            }
            catch (Exception e)
            {
                UnityEngine.GUI.matrix = UnityEngine.Matrix4x4.identity;
                UnityEngine.GUI.color = UnityEngine.Color.white;
                UnityEngine.GUI.enabled = true;
                Report(scope, e);
            }
        }

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

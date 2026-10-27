using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace StellarDriveDemoTFAdvanced.Common
{
    /// <summary>
    /// The mod's saved files: written through a temporary file swapped in at the end (a crash or a
    /// full disk mid-write leaves the old file whole), and an unreadable file is set aside rather
    /// than overwritten by the next save.
    /// </summary>
    internal static class SafeFile
    {
        public static void WriteAllText(string path, string text)
        {
            string temp = path + ".tmp";
            File.WriteAllText(temp, text);
            if (File.Exists(path))
                File.Replace(temp, path, null);
            else
                File.Move(temp, path);
        }

        public static void WriteAllLines(string path, IEnumerable<string> lines) =>
            WriteAllText(path, string.Join(Environment.NewLine, lines) + Environment.NewLine);

        /// <summary>The JSON file read, or default when it is missing or unreadable (then kept as .corrupt-date for the player).</summary>
        public static T ReadJson<T>(string path) where T : class
        {
            if (!File.Exists(path))
                return null;
            try
            {
                return JsonConvert.DeserializeObject<T>(File.ReadAllText(path));
            }
            catch (JsonException e)
            {
                SetAside(path, e.Message);
                return null;
            }
        }

        public static void SetAside(string path, string why)
        {
            string aside = path + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            try
            {
                File.Move(path, aside);
                AdvancedMod.Log.Error($"{Path.GetFileName(path)} could not be read ({why}); kept as {Path.GetFileName(aside)}");
            }
            catch (Exception e)
            {
                AdvancedMod.Log.Error($"{Path.GetFileName(path)} could not be read ({why}) nor set aside: {e.Message}");
            }
        }
    }
}

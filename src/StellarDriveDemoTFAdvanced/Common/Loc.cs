using System;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace StellarDriveDemoTFAdvanced.Common
{
    /// <summary>
    /// French or English. The setting Language picks it ("auto": the game's language, else the
    /// system's; anything but French is English). Every text the players see is written in both
    /// languages where it is used: <c>Loc.Pick("français", "English")</c>.
    /// </summary>
    internal static class Loc
    {
        private static bool _english;
        private static float _checkedAt = -100f;

        public static bool English
        {
            get
            {
                if (Time.realtimeSinceStartup - _checkedAt > 5f || _checkedAt < 0f)
                {
                    _checkedAt = Time.realtimeSinceStartup;
                    _english = Detect();
                }
                return _english;
            }
        }

        public static string Pick(string french, string english) => English ? english : french;

        private static bool Detect()
        {
            string setting = Settings.Language?.Value?.Trim().ToLowerInvariant() ?? "auto";
            if (setting.StartsWith("fr"))
                return false;
            if (setting.StartsWith("en"))
                return true;
            string code = GameLocale();
            if (!string.IsNullOrEmpty(code))
                return !code.StartsWith("fr", StringComparison.OrdinalIgnoreCase);
            return Application.systemLanguage != SystemLanguage.French;
        }

        // The game's selected locale, once its localization is loaded
        private static string GameLocale()
        {
            try
            {
                return LocalizationSettings.HasSettings ? LocalizationSettings.SelectedLocale?.Identifier.Code : null;
            }
            catch
            {
                return null;
            }
        }
    }
}

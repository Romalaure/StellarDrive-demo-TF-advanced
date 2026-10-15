using System;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace StellarDriveDemoTFAdvanced.Common
{
    /// <summary>
    /// French, English or Spanish. The setting Language picks it ("auto": the game's language,
    /// else the system's; anything but French or Spanish is English). Every text the players see
    /// is written in each language where it is used: <c>Loc.Pick("français", "English", "español")</c>
    /// (without the Spanish text, Spanish players read the English one).
    /// </summary>
    internal static class Loc
    {
        public enum Language { French, English, Spanish }

        private static Language _current;
        private static float _checkedAt = -100f;

        public static Language Current
        {
            get
            {
                if (Time.realtimeSinceStartup - _checkedAt > 5f || _checkedAt < 0f)
                {
                    _checkedAt = Time.realtimeSinceStartup;
                    _current = Detect();
                }
                return _current;
            }
        }

        public static bool English => Current == Language.English;

        public static bool Spanish => Current == Language.Spanish;

        public static string Pick(string french, string english, string spanish = null)
        {
            switch (Current)
            {
                case Language.French: return french;
                case Language.Spanish: return spanish ?? english;
                default: return english;
            }
        }

        public static T Pick<T>(T french, T english, T spanish)
        {
            switch (Current)
            {
                case Language.French: return french;
                case Language.Spanish: return spanish;
                default: return english;
            }
        }

        private static Language Detect()
        {
            string setting = Settings.Language?.Value?.Trim().ToLowerInvariant() ?? "auto";
            Language? chosen = FromCode(setting);
            if (chosen.HasValue)
                return chosen.Value;
            string code = GameLocale();
            if (!string.IsNullOrEmpty(code))
                return FromCode(code.ToLowerInvariant()) ?? Language.English;
            switch (Application.systemLanguage)
            {
                case SystemLanguage.French: return Language.French;
                case SystemLanguage.Spanish: return Language.Spanish;
                default: return Language.English;
            }
        }

        private static Language? FromCode(string code)
        {
            if (code.StartsWith("fr"))
                return Language.French;
            if (code.StartsWith("es"))
                return Language.Spanish;
            if (code.StartsWith("en"))
                return Language.English;
            return null;
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

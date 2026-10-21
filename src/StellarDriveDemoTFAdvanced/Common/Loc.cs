using System;
using System.Collections.Generic;
using Items.Model;
using Research.Interface.Values;
using Ships.Interface.Settings;
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
                    bool first = _checkedAt < 0f;
                    _checkedAt = Time.realtimeSinceStartup;
                    Language detected = Detect();
                    bool changed = !first && detected != _current;
                    _current = detected;
                    if (changed)
                        Relabel();
                    if (first)
                        AdvancedMod.Log?.Msg($"language at start: {detected} (game {GameLocale() ?? "not loaded"}, saved {SavedLocale()}, system {Application.systemLanguage})");
                }
                return _current;
            }
        }

        // Every text picked, in its three languages: names and descriptions are written once into
        // the game's item, part and research settings, so they are translated again when the language changes
        private static readonly Dictionary<string, string[]> Texts = new Dictionary<string, string[]>();

        private static void Remember(string french, string english, string spanish)
        {
            // shortcut: stops at 20000 texts (on-screen texts with numbers add new ones), raise it if late names stay untranslated
            if (french == null || english == null || french == english || Texts.Count > 20000)
                return;
            var texts = new[] { french, english, spanish ?? english };
            foreach (string text in texts)
                if (!Texts.ContainsKey(text))
                    Texts[text] = texts;
        }

        private static string Translate(string text)
        {
            if (text == null || !Texts.TryGetValue(text, out string[] texts))
                return text;
            return texts[(int)_current];
        }

        // Made while the game runs (by the mods), not one of the game's own assets: those keep their names
        private static bool Made(UnityEngine.Object o) => o != null && o.GetInstanceID() < 0;

        /// <summary>The names and descriptions already given to the game, in the current language.</summary>
        private static void Relabel()
        {
            try
            {
                foreach (Items.ModItem item in Items.ModItems.All)
                    item.Name = Translate(item.Name);
                foreach (ItemSettings item in Resources.FindObjectsOfTypeAll<ItemSettings>())
                    if (Made(item))
                        item.itemName = Translate(item.itemName);
                foreach (PartSettings part in Resources.FindObjectsOfTypeAll<PartSettings>())
                {
                    if (!Made(part))
                        continue;
                    part.fullLabel = Translate(part.fullLabel);
                    part.description = Translate(part.description);
                }
                foreach (ResearchSettings tech in Resources.FindObjectsOfTypeAll<ResearchSettings>())
                    if (Made(tech))
                        tech.techName = Translate(tech.techName);
                AdvancedMod.Log.Msg("language: names and descriptions now in " + _current);
            }
            catch (Exception e)
            {
                ErrorLog.Report("language change", e);
            }
        }

        public static bool English => Current == Language.English;

        public static bool Spanish => Current == Language.Spanish;

        public static string Pick(string french, string english, string spanish = null)
        {
            Remember(french, english, spanish);
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
            // Before the game's localization is loaded (the mod's start): the language the game saved
            code = SavedLocale();
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

        // Unity Localization's saved choice (its PlayerPrefs locale selector)
        private static string SavedLocale()
        {
            try
            {
                return PlayerPrefs.GetString("selected-locale", "").Trim('\0', ' ');
            }
            catch
            {
                return null;
            }
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

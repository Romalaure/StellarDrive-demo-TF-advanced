using System.Collections.Generic;
using Core.Values;
using TMPro;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.ShipComputer
{
    /// <summary>
    /// Colors and font taken from the game itself: its GameColorPalette asset (the colors of every
    /// native menu) and the font its UI texts use. Fallbacks only apply if those are not loaded yet.
    /// </summary>
    internal static class ComputerStyle
    {
        private static GameColorPalette _palette;
        private static TMP_FontAsset _font;

        // Amber for warnings: the palette has no warning color, this one sits between its text and invalid colors
        private static readonly Color WarningColor = new Color32(232, 180, 79, 255);

        public static Color Background => Pick(p => p.uiBackgroundColor, new Color(0.04f, 0.05f, 0.07f, 0.96f));
        public static Color Line => Pick(p => p.uiLineColor, new Color(0.45f, 0.78f, 0.92f, 1f));
        public static Color Text => Pick(p => p.uiTextColor, new Color(0.78f, 0.85f, 0.9f, 1f));
        public static Color TextStrong => Pick(p => p.uiTextColorSelected, Color.white);
        public static Color TextDim => Pick(p => p.uiTextColorUnavailable, new Color(0.55f, 0.6f, 0.65f, 1f));
        public static Color Info => Line;
        public static Color Warning => WarningColor;
        public static Color Error => Pick(p => p.uiTextColorInvalid, new Color(0.92f, 0.45f, 0.42f, 1f));

        public static bool HasGamePalette => _palette != null;

        public static TMP_FontAsset Font
        {
            get
            {
                if (_font == null)
                    _font = MostUsedFont() ?? TMP_Settings.defaultFontAsset;
                return _font;
            }
        }

        /// <summary>Looks the palette up again (once per scene, never per frame).</summary>
        public static void Refresh()
        {
            if (_palette == null)
            {
                GameColorPalette[] palettes = Resources.FindObjectsOfTypeAll<GameColorPalette>();
                _palette = palettes.Length > 0 ? palettes[0] : null;
            }
            if (_font == null)
                _font = MostUsedFont();
        }

        public static string Hex(Color color)
        {
            Color32 c = color;
            return $"#{c.r:X2}{c.g:X2}{c.b:X2}";
        }

        private static Color Pick(System.Func<GameColorPalette, Color> read, Color fallback)
        {
            return _palette != null ? read(_palette) : fallback;
        }

        // The font of the game's own UI: the one most of its loaded texts use, glow variants aside
        private static TMP_FontAsset MostUsedFont()
        {
            var counts = new Dictionary<TMP_FontAsset, int>();
            foreach (TMP_Text text in Resources.FindObjectsOfTypeAll<TMP_Text>())
            {
                if (text == null || text.font == null || !(text is TextMeshProUGUI))
                    continue;
                counts.TryGetValue(text.font, out int n);
                counts[text.font] = n + 1;
            }
            TMP_FontAsset best = null;
            int bestCount = 0;
            foreach (KeyValuePair<TMP_FontAsset, int> pair in counts)
            {
                if (pair.Value > bestCount)
                {
                    best = pair.Key;
                    bestCount = pair.Value;
                }
            }
            return best;
        }
    }
}

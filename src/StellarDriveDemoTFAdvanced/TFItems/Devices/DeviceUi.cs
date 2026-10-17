using System.Collections.Generic;
using System.Linq;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.TFItems.Devices
{
    /// <summary>
    /// Shared IMGUI look for the mod's windows, in the style of StellarDrive's own menus (the build
    /// menu): a see-through night blue panel in a rounded grey metal frame with yellow and black
    /// hazard stripes in two corners, blue buttons with a light edge, pale blue text in the game's
    /// font. In GUI units scaled to 1080p.
    /// </summary>
    internal static class DeviceUi
    {
        /// <summary>The game's blue (selected tabs, highlights).</summary>
        public static readonly Color Accent = new Color(0.2f, 0.56f, 0.9f);
        public static readonly Color Chip = new Color(0.1f, 0.27f, 0.42f, 0.95f);
        private static readonly Color PanelFill = new Color(0.05f, 0.12f, 0.19f, 0.93f);
        private static readonly Color Frame = new Color(0.62f, 0.66f, 0.7f, 1f);
        private static readonly Color TextColor = new Color(0.86f, 0.94f, 1f);
        private static readonly Color MutedColor = new Color(0.55f, 0.7f, 0.82f);

        private static GUIStyle _panel, _title, _text, _muted, _button, _selected, _field, _toast;
        private static Texture2D _white, _hazard;
        private static Font _font;
        private static bool _fontSearched;

        public static GUIStyle Text { get { Ensure(); return _text; } }
        public static GUIStyle Muted { get { Ensure(); return _muted; } }
        public static GUIStyle Button { get { Ensure(); return _button; } }
        public static GUIStyle Selected { get { Ensure(); return _selected; } }
        public static GUIStyle Field { get { Ensure(); return _field; } }
        public static GUIStyle Title { get { Ensure(); return _title; } }

        /// <summary>The game's UI font (from its TextMeshPro fonts), or null for Unity's.</summary>
        public static Font GameFont
        {
            get
            {
                if (!_fontSearched)
                {
                    _fontSearched = true;
                    _font = FindGameFont();
                }
                return _font;
            }
        }

        /// <summary>Draws a centered window and returns the area for its content.</summary>
        public static Rect Window(float screenWidth, float screenHeight, float width, float height, string title)
        {
            Ensure();
            height = Mathf.Min(height, screenHeight - 40f);
            var rect = new Rect((screenWidth - width) / 2f, (screenHeight - height) / 2f, width, height);
            Panel(rect);
            GUI.Label(new Rect(rect.x + 22f, rect.y + 14f, width - 44f, 28f), title, _title);
            // A thin blue rule under the title, like the build menu's tab bar
            Fill(new Rect(rect.x + 22f, rect.y + 44f, width - 44f, 2f), Accent * 0.9f);
            return new Rect(rect.x + 22f, rect.y + 54f, width - 44f, height - 72f);
        }

        /// <summary>A window at the left (<paramref name="left"/> from the screen's edge), vertically centered.</summary>
        public static Rect WindowAt(float screenWidth, float screenHeight, float left, float width, float height, string title)
        {
            Ensure();
            height = Mathf.Min(height, screenHeight - 40f);
            var rect = new Rect(left, (screenHeight - height) / 2f, width, height);
            Panel(rect);
            GUI.Label(new Rect(rect.x + 22f, rect.y + 14f, width - 44f, 28f), title, _title);
            Fill(new Rect(rect.x + 22f, rect.y + 44f, width - 44f, 2f), Accent * 0.9f);
            return new Rect(rect.x + 22f, rect.y + 54f, width - 44f, height - 72f);
        }

        /// <summary>The game's panel: night blue, a grey metal frame, hazard stripes in two corners.</summary>
        public static void Panel(Rect rect)
        {
            Ensure();
            GUI.Box(rect, GUIContent.none, _panel);
            Color previous = GUI.color;
            GUI.DrawTexture(new Rect(rect.x + 30f, rect.y + 2f, 30f, 6f), _hazard);
            GUI.DrawTexture(new Rect(rect.xMax - 62f, rect.yMax - 8f, 30f, 6f), _hazard);
            GUI.color = previous;
        }

        public static void Toast(float screenWidth, float screenHeight, string message)
        {
            Ensure();
            var rect = new Rect((screenWidth - 560f) / 2f, screenHeight - 170f, 560f, 46f);
            Panel(rect);
            GUI.Label(new Rect(rect.x + 16f, rect.y, rect.width - 32f, rect.height), message, _toast);
        }

        public static void Fill(Rect rect, Color color)
        {
            Ensure();
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _white);
            GUI.color = previous;
        }

        private static GUIStyle Label(int size, Color color, bool wrap, FontStyle style = FontStyle.Normal)
        {
            var label = new GUIStyle(GUI.skin.label) { fontSize = size, wordWrap = wrap, fontStyle = style };
            if (GameFont != null)
                label.font = GameFont;
            label.normal.textColor = color;
            return label;
        }

        private static void Ensure()
        {
            if (_panel != null)
                return;
            _white = UiTextures.Solid(Color.white);
            _hazard = UiTextures.Hazard();
            _panel = UiTextures.FramedStyle(PanelFill, Frame, 14, 4);
            _title = Label(19, TextColor, false, FontStyle.Bold);
            _text = Label(15, TextColor, true);
            _muted = Label(13, MutedColor, true);
            _toast = Label(14, TextColor, true);
            _toast.alignment = TextAnchor.MiddleCenter;

            _button = UiTextures.FramedStyle(Chip, new Color(0.3f, 0.58f, 0.85f, 1f), 6, 1);
            if (GameFont != null)
                _button.font = GameFont;
            _button.fontSize = 15;
            _button.alignment = TextAnchor.MiddleLeft;
            _button.padding = new RectOffset(14, 14, 6, 6);
            _button.normal.textColor = TextColor;
            _button.hover.background = UiTextures.Framed(new Color(0.14f, 0.36f, 0.56f, 1f), new Color(0.55f, 0.8f, 1f, 1f), 6, 1);
            _button.hover.textColor = Color.white;
            _button.active.background = UiTextures.Framed(Accent, new Color(0.75f, 0.9f, 1f, 1f), 6, 1);
            _button.active.textColor = Color.white;

            _selected = new GUIStyle(_button);
            _selected.normal.background = UiTextures.Framed(Accent, new Color(0.75f, 0.9f, 1f, 1f), 6, 1);
            _selected.normal.textColor = Color.white;
            _selected.hover.background = _selected.normal.background;

            _field = new GUIStyle(GUI.skin.textField) { fontSize = 15, padding = new RectOffset(10, 10, 8, 8) };
            if (GameFont != null)
                _field.font = GameFont;
            _field.normal.background = UiTextures.Framed(new Color(0.02f, 0.07f, 0.12f, 1f), new Color(0.25f, 0.45f, 0.65f, 1f), 6, 1);
            _field.focused.background = UiTextures.Framed(new Color(0.03f, 0.1f, 0.17f, 1f), Accent, 6, 1);
            _field.normal.textColor = TextColor;
            _field.focused.textColor = Color.white;
        }

        // The font the game's menus draw with: the one most of its TextMeshPro texts use, when its
        // source font is in the build; else any font the game ships besides Unity's default
        private static Font FindGameFont()
        {
            try
            {
                var counts = new Dictionary<Font, int>();
                foreach (TMPro.TMP_Text text in Resources.FindObjectsOfTypeAll<TMPro.TMP_Text>())
                {
                    Font source = text != null && text.font != null ? text.font.sourceFontFile : null;
                    if (source == null)
                        continue;
                    counts.TryGetValue(source, out int n);
                    counts[source] = n + 1;
                }
                Font best = counts.OrderByDescending(c => c.Value).Select(c => c.Key).FirstOrDefault();
                if (best == null)
                    best = Resources.FindObjectsOfTypeAll<TMPro.TMP_FontAsset>().Select(f => f.sourceFontFile).FirstOrDefault(f => f != null);
                if (best == null)
                    best = Resources.FindObjectsOfTypeAll<Font>().FirstOrDefault(f => f != null && !f.name.Contains("Arial") && !f.name.Contains("LegacyRuntime"));
                TFMod.Log.Msg($"windows font: {(best != null ? best.name : "Unity's default")}");
                return best;
            }
            catch (System.Exception e)
            {
                TFMod.Log.Warning("windows font: " + e.Message);
                return null;
            }
        }
    }
}

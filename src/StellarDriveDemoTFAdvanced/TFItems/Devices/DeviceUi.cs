using StellarDriveDemoTFAdvanced.TFItems.Common;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.TFItems.Devices
{
    /// <summary>Shared IMGUI look for the device windows (dark rounded panel, orange accent), in GUI units scaled to 1080p.</summary>
    internal static class DeviceUi
    {
        public static readonly Color Accent = new Color(0.98f, 0.5f, 0.12f);
        public static readonly Color Chip = new Color(0.18f, 0.19f, 0.23f, 1f);

        private static GUIStyle _panel, _title, _text, _muted, _button, _selected, _field, _toast;
        private static Texture2D _white;

        public static GUIStyle Text { get { Ensure(); return _text; } }
        public static GUIStyle Muted { get { Ensure(); return _muted; } }
        public static GUIStyle Button { get { Ensure(); return _button; } }
        public static GUIStyle Selected { get { Ensure(); return _selected; } }
        public static GUIStyle Field { get { Ensure(); return _field; } }
        public static GUIStyle Title { get { Ensure(); return _title; } }

        /// <summary>Draws a centered window and returns the area for its content.</summary>
        public static Rect Window(float screenWidth, float screenHeight, float width, float height, string title)
        {
            Ensure();
            height = Mathf.Min(height, screenHeight - 40f);
            var rect = new Rect((screenWidth - width) / 2f, (screenHeight - height) / 2f, width, height);
            GUI.Box(rect, GUIContent.none, _panel);
            Fill(new Rect(rect.x + 18f, rect.y, 60f, 3f), Accent);
            GUI.Label(new Rect(rect.x + 18f, rect.y + 12f, width - 36f, 26f), title, _title);
            return new Rect(rect.x + 18f, rect.y + 48f, width - 36f, height - 64f);
        }

        public static void Toast(float screenWidth, float screenHeight, string message)
        {
            Ensure();
            var rect = new Rect((screenWidth - 560f) / 2f, screenHeight - 170f, 560f, 44f);
            GUI.Box(rect, GUIContent.none, _panel);
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

        private static void Ensure()
        {
            if (_panel != null)
                return;
            _white = UiTextures.Solid(Color.white);
            var text = new Color(0.93f, 0.94f, 0.96f);
            var muted = new Color(0.6f, 0.63f, 0.69f);
            _panel = UiTextures.RoundedStyle(new Color(0.075f, 0.08f, 0.1f, 0.96f), 12);
            _title = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold };
            _title.normal.textColor = text;
            _text = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            _text.normal.textColor = text;
            _muted = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            _muted.normal.textColor = muted;
            _toast = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true, alignment = TextAnchor.MiddleCenter };
            _toast.normal.textColor = text;

            _button = UiTextures.RoundedStyle(Chip, 8);
            _button.fontSize = 14;
            _button.alignment = TextAnchor.MiddleLeft;
            _button.padding = new RectOffset(14, 14, 6, 6);
            _button.normal.textColor = text;
            _button.hover.background = UiTextures.Rounded(new Color(0.25f, 0.27f, 0.32f, 1f), 8);
            _button.hover.textColor = Color.white;
            _button.active.background = UiTextures.Rounded(Accent * 0.8f, 8);
            _button.active.textColor = Color.white;

            _selected = new GUIStyle(_button);
            _selected.normal.background = UiTextures.Rounded(new Color(0.55f, 0.28f, 0.07f, 1f), 8);
            _selected.hover.background = _selected.normal.background;

            _field = new GUIStyle(GUI.skin.textField) { fontSize = 14, padding = new RectOffset(10, 10, 8, 8) };
            _field.normal.background = UiTextures.Rounded(new Color(0.03f, 0.035f, 0.045f, 1f), 6);
            _field.focused.background = UiTextures.Rounded(new Color(0.05f, 0.055f, 0.07f, 1f), 6);
            _field.normal.textColor = text;
            _field.focused.textColor = Color.white;
        }
    }
}

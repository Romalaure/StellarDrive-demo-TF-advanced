using UnityEngine;

namespace StellarDriveDemoTFAdvanced.TFItems.Common
{
    /// <summary>Textures for the IMGUI panels: plain colors and anti-aliased rounded rectangles.</summary>
    internal static class UiTextures
    {
        /// <summary>Rounded rectangle meant to be 9-sliced (border = radius + 1), so it scales to any size.</summary>
        public static Texture2D Rounded(Color color, int radius)
        {
            int size = radius * 2 + 4;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(0f, Mathf.Max(radius - (x + 0.5f), (x + 0.5f) - (size - radius)));
                float dy = Mathf.Max(0f, Mathf.Max(radius - (y + 0.5f), (y + 0.5f) - (size - radius)));
                float coverage = Mathf.Clamp01(radius + 0.5f - Mathf.Sqrt(dx * dx + dy * dy));
                texture.SetPixel(x, y, new Color(color.r, color.g, color.b, color.a * coverage));
            }
            texture.Apply();
            return texture;
        }

        public static GUIStyle RoundedStyle(Color color, int radius) => new GUIStyle
        {
            normal = { background = Rounded(color, radius) },
            border = new RectOffset(radius + 1, radius + 1, radius + 1, radius + 1),
            padding = new RectOffset(8, 8, 4, 4)
        };

        public static Texture2D Solid(Color color)
        {
            var texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }
}

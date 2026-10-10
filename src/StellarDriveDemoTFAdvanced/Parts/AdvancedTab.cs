using SDModKit.Game;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Parts
{
    /// <summary>The "TF Avancé" build menu tab, one row per family (the menu has no scrolling: 3 rows max).</summary>
    internal static class AdvancedTab
    {
        public const string Name = "TF Avancé";

        public const int ComputersRow = 0;
        public const int PropulsionRow = 1;

        /// <summary>Only call when SDModKit is loaded.</summary>
        public static void Declare()
        {
            BuildTabs.Declare(Name, 52, Icon());
        }

        // A small screen with three text lines, drawn in code
        private static Texture2D Icon()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "TFA_TabIcon", hideFlags = HideFlags.HideAndDontSave };
            var clear = new Color(1f, 1f, 1f, 0f);
            var frame = new Color(0.85f, 0.86f, 0.9f, 1f);
            var glass = new Color(0.1f, 0.14f, 0.18f, 1f);
            var line = new Color(0.45f, 0.8f, 0.95f, 1f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Color c = clear;
                    bool inFrame = x >= 6 && x < 58 && y >= 16 && y < 54;
                    bool inGlass = x >= 10 && x < 54 && y >= 20 && y < 50;
                    if (inGlass)
                    {
                        c = glass;
                        // Text lines of different lengths
                        if ((y >= 42 && y < 45 && x < 46) || (y >= 34 && y < 37 && x < 38) || (y >= 26 && y < 29 && x < 50))
                            c = x >= 14 ? line : glass;
                    }
                    else if (inFrame)
                        c = frame;
                    // Stand
                    else if (y >= 8 && y < 16 && x >= 28 && x < 36)
                        c = frame;
                    else if (y >= 5 && y < 8 && x >= 18 && x < 46)
                        c = frame;
                    texture.SetPixel(x, y, c);
                }
            }
            texture.Apply();
            return texture;
        }
    }
}

using SDModKit.Game;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Parts
{
    /// <summary>
    /// The mod's two build menu tabs. "TF Avancé": ship computers, propulsion and power, the
    /// artifact machines. "TF Construction": structure, wood, glass and iron sets, station
    /// modules. The game's menu does not scroll; a tab with more rows or longer rows than its
    /// panel holds is scaled down to fit (<see cref="BuildMenuFitPatch"/>).
    /// </summary>
    internal static class AdvancedTab
    {
        public const string Name = "TF Avancé";

        public const int ComputersRow = 0;
        // Engines, battery and breaker share the second row (8 parts per row at most)
        public const int PropulsionRow = 1;

        /// <summary>Parts built from the rare finds of ruins, the abandoned city and the stations: their own row of the same tab.</summary>
        public const string ArtifactsName = Name;
        public const int ArtifactsRow = 2;
        public const int ArtifactsPowerRow = ArtifactsRow;
        public const int ArtifactsPropulsionRow = ArtifactsRow;
        /// <summary>Dark matter machines (portal and the like).</summary>
        public const int SuperiorRow = 3;

        public const string ConstructionName = "TF Construction";
        public const int ConstructionRow = 0;
        public const int WoodRow = 1;
        public const int GlassRow = 2;
        public const int IronRow = 3;

        /// <summary>Space station modules, their own tab.</summary>
        public const string StationName = "TF Station";
        public const int StationRow = 0;

        /// <summary>Only call when SDModKit is loaded.</summary>
        public static void Declare()
        {
            BuildTabs.Declare(Name, 52, Icon());
            BuildTabs.Declare(ConstructionName, 53, ConstructionIcon());
            BuildTabs.Declare(StationName, 54, StationIcon());
        }

        // Two station modules joined by a corridor, a solar wing above
        private static Texture2D StationIcon()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "TFA_StationTabIcon", hideFlags = HideFlags.HideAndDontSave };
            var clear = new Color(1f, 1f, 1f, 0f);
            var hull = new Color(0.85f, 0.86f, 0.9f, 1f);
            var window = new Color(0.45f, 0.8f, 0.95f, 1f);
            var solar = new Color(0.3f, 0.45f, 0.9f, 1f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Color c = clear;
                    bool left = x >= 4 && x < 26 && y >= 10 && y < 30, right = x >= 38 && x < 60 && y >= 10 && y < 30;
                    if (left || right)
                        c = (y == 20 && x % 4 != 0) ? window : hull;
                    else if (x >= 26 && x < 38 && y >= 16 && y < 24)
                        c = hull;
                    if (x == 31 || x == 32)
                        c = y >= 30 && y < 44 ? hull : c;
                    if (y >= 44 && y < 56 && x >= 8 && x < 56 && x != 31 && x != 32)
                        c = (x % 6 == 0 || y == 50) ? hull : solar;
                    texture.SetPixel(x, y, c);
                }
            }
            texture.Apply();
            return texture;
        }

        // A steel I-beam over two crossed planks
        private static Texture2D ConstructionIcon()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "TFA_ConstructionTabIcon", hideFlags = HideFlags.HideAndDontSave };
            var clear = new Color(1f, 1f, 1f, 0f);
            var steel = new Color(0.85f, 0.86f, 0.9f, 1f);
            var wood = new Color(0.82f, 0.6f, 0.38f, 1f);
            var glass = new Color(0.55f, 0.85f, 0.95f, 1f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Color c = clear;
                    // Glass pane in the back
                    if (x >= 34 && x < 58 && y >= 30 && y < 58)
                        c = (x == 34 || x == 57 || y == 30 || y == 57) ? steel : glass;
                    // Planks
                    if (y >= 8 && y < 18 && x >= 4 && x < 46)
                        c = (x % 14 == 0) ? clear : wood;
                    // I-beam standing
                    if ((y >= 10 && y < 14 || y >= 50 && y < 54) && x >= 12 && x < 34)
                        c = steel;
                    if (y >= 14 && y < 50 && x >= 20 && x < 26)
                        c = steel;
                    texture.SetPixel(x, y, c);
                }
            }
            texture.Apply();
            return texture;
        }

        // A violet crystal cluster
        private static Texture2D CrystalIcon()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "TFA_ArtifactsTabIcon", hideFlags = HideFlags.HideAndDontSave };
            var clear = new Color(1f, 1f, 1f, 0f);
            var light = new Color(0.85f, 0.7f, 1f, 1f);
            var dark = new Color(0.5f, 0.3f, 0.85f, 1f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Color c = clear;
                    // Three crystals: center tall, two leaning shorter ones
                    if (Crystal(x, y, 32, 8, 54, 8))
                        c = x < 32 ? light : dark;
                    else if (Crystal(x - (y - 8) / 4, y, 18, 8, 36, 6) || Crystal(x + (y - 8) / 4, y, 46, 8, 38, 6))
                        c = (x + y) % 9 < 5 ? light : dark;
                    texture.SetPixel(x, y, c);
                }
            }
            texture.Apply();
            return texture;
        }

        // A pointed prism from y0 up to y1, half width w, centered on cx
        private static bool Crystal(int x, int y, int cx, int y0, int y1, int w)
        {
            if (y < y0 || y > y1)
                return false;
            int tip = y1 - w * 2;
            int half = y <= tip ? w : w * (y1 - y) / (y1 - tip);
            return x >= cx - half && x <= cx + half;
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

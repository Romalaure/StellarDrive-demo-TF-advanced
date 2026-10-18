using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Common
{
    /// <summary>
    /// The surface textures of the mod's own models (before, plain colors): plates with a groove
    /// round their edges, rivets in their corners, a fine grain and wear, in shades of white so
    /// the material's color tints it, and the relief that goes with it. Metal gets brushed streaks,
    /// paint a softer grain. Made once at start (256 x 256, a few hundred kilobytes). The models'
    /// faces are mapped one texture per face: four plates of slightly different shades. The setting
    /// ClassicTextures brings back the plain colors.
    /// </summary>
    internal static class SurfaceTextures
    {
        private const int Size = 256;
        private static Texture2D _metal, _paint, _relief, _grain;

        public static bool Enabled => !Settings.ClassicTextures.Value;

        public static Texture2D Metal => _metal != null ? _metal : (_metal = Albedo("TFA_SurfaceMetal", true));
        public static Texture2D Paint => _paint != null ? _paint : (_paint = Albedo("TFA_SurfacePaint", false));
        public static Texture2D Relief => _relief != null ? _relief : (_relief = Normal("TFA_SurfaceRelief"));

        /// <summary>Natural things (wood, stone, leaves): blotches and grain, no plates.</summary>
        public static Texture2D Grain => _grain != null ? _grain : (_grain = MakeGrain("TFA_SurfaceGrain"));

        private static Texture2D MakeGrain(string name)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float u = (x + 0.5f) / Size, v = (y + 0.5f) / Size;
                    float blotch = Noise(u * 4f, v * 4f, 4, 31) * 0.55f + Noise(u * 12f, v * 12f, 12, 32) * 0.3f + Noise(u * 48f, v * 48f, 48, 33) * 0.15f;
                    float shade = Mathf.Clamp01(0.78f + (blotch - 0.5f) * 0.45f);
                    pixels[y * Size + x] = new Color(shade, shade, shade, 1f);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(true);
            return texture;
        }

        // ---- Height: the plate (0 in its groove, 1 on its face), rivets on top, grain

        // The texture holds 2 x 2 plates: a point's place in its own plate
        private const int Plates = 2;

        private static float Local(float t) => t * Plates - Mathf.Floor(t * Plates);

        private static float Groove(float u, float v)
        {
            u = Local(u);
            v = Local(v);
            float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((edge - 0.02f) / 0.035f));
        }

        private static float Rivet(float u, float v)
        {
            u = Local(u);
            v = Local(v);
            float best = 0f;
            foreach (float cu in new[] { 0.1f, 0.9f })
            {
                foreach (float cv in new[] { 0.1f, 0.9f })
                {
                    float d = Mathf.Sqrt((u - cu) * (u - cu) + (v - cv) * (v - cv)) / 0.032f;
                    if (d < 1f)
                        best = Mathf.Max(best, Mathf.Sqrt(1f - d * d));
                }
            }
            return best;
        }

        // Smooth value noise, tiling
        private static float Noise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            float H(int i, int j)
            {
                int h = (((i % period) + period) % period) * 73856093 ^ (((j % period) + period) % period) * 19349663 ^ seed * 83492791;
                h = (h << 13) ^ h;
                return ((h * (h * h * 15731 + 789221) + 1376312589) & 0x7fffffff) / 2147483647f;
            }
            float sx = fx * fx * (3f - 2f * fx), sy = fy * fy * (3f - 2f * fy);
            return Mathf.Lerp(Mathf.Lerp(H(x0, y0), H(x0 + 1, y0), sx), Mathf.Lerp(H(x0, y0 + 1), H(x0 + 1, y0 + 1), sx), sy);
        }

        private static float Height(float u, float v) => Groove(u, v) * 0.85f + Rivet(u, v) * 0.35f + (Noise(u * 32f, v * 32f, 32, 3) - 0.5f) * 0.04f;

        private static Texture2D Albedo(string name, bool brushed)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float u = (x + 0.5f) / Size, v = (y + 0.5f) / Size;
                    float groove = Groove(u, v), rivet = Rivet(u, v);
                    // Broad patches of wear, fine grain, and for metal streaks along u
                    float wear = Noise(u * 4f, v * 4f, 4, 11) * 0.6f + Noise(u * 8f, v * 8f, 8, 12) * 0.4f;
                    float grain = Noise(u * 64f, v * 64f, 64, 13);
                    float streaks = brushed ? Noise(u * 4f, v * 96f, 4, 14) : 0.5f;
                    // Each plate its own shade, a little lighter or darker
                    int plate = Mathf.FloorToInt(u * Plates) + Mathf.FloorToInt(v * Plates) * Plates;
                    float tone = (Noise(plate * 3.7f, plate * 1.3f, 16, 21) - 0.5f) * 0.16f;
                    float shade = 0.86f + tone + (wear - 0.5f) * 0.2f + (grain - 0.5f) * 0.07f + (streaks - 0.5f) * 0.12f;
                    // The groove is dark, the rivets catch the light
                    shade *= Mathf.Lerp(0.35f, 1f, groove);
                    shade += rivet * 0.14f;
                    // Grime along the grooves
                    float lu = Local(u), lv = Local(v);
                    float edge = Mathf.Min(Mathf.Min(lu, 1f - lu), Mathf.Min(lv, 1f - lv));
                    shade -= Mathf.Clamp01(1f - edge / 0.12f) * 0.1f * wear;
                    shade = Mathf.Clamp01(shade);
                    pixels[y * Size + x] = new Color(shade, shade, shade, 1f);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(true);
            return texture;
        }

        // A tangent-space normal map from the height (URP reads x from red, y from green)
        private static Texture2D Normal(string name)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true, true) { name = name, wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            var heights = new float[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    heights[y * Size + x] = Height((x + 0.5f) / Size, (y + 0.5f) / Size);
            var pixels = new Color[Size * Size];
            const float strength = 9f;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float dx = heights[y * Size + (x + 1) % Size] - heights[y * Size + (x + Size - 1) % Size];
                    float dy = heights[((y + 1) % Size) * Size + x] - heights[((y + Size - 1) % Size) * Size + x];
                    Vector3 n = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
                    pixels[y * Size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(true);
            return texture;
        }

        /// <summary>Gives a URP Lit material the plate texture and its relief (not for glowing ones).</summary>
        public static void Apply(Material material, float metallic)
        {
            if (!Enabled || material == null)
                return;
            Texture2D albedo = metallic >= 0.45f ? Metal : Paint;
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", albedo);
            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", albedo);
            if (material.HasProperty("_BumpMap"))
            {
                material.SetTexture("_BumpMap", Relief);
                material.SetFloat("_BumpScale", 0.6f);
                material.EnableKeyword("_NORMALMAP");
            }
        }
    }
}

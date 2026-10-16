using System;
using System.Collections.Generic;
using UnityEngine;
using Loc = StellarDriveDemoTFAdvanced.Common.Loc;

namespace StellarDriveDemoTFAdvanced.TFItems.Paint
{
    internal enum CamoStyle { Spots, Stripes, Digital }

    internal sealed class CamoPalette
    {
        public readonly string Name;
        private readonly Color[] _colors;

        public CamoPalette(string name, params string[] hex)
        {
            Name = name;
            _colors = hex == null ? null : Array.ConvertAll(hex, Parse);
        }

        // "#rrggbb"; plain code so palettes also load outside the game (model previews)
        private static Color Parse(string hex)
        {
            int rgb = Convert.ToInt32(hex.TrimStart('#'), 16);
            return new Color((rgb >> 16 & 0xff) / 255f, (rgb >> 8 & 0xff) / 255f, (rgb & 0xff) / 255f);
        }

        /// <summary>Four colors, lightest first. A palette without colors is shades of the brush color.</summary>
        public Color[] Colors(Color brush)
        {
            if (_colors != null)
                return _colors;
            Color.RGBToHSV(brush, out float h, out float s, out float v);
            return new[]
            {
                Color.HSVToRGB(h, s * 0.8f, Mathf.Min(1f, v * 1.25f + 0.08f)),
                brush,
                Color.HSVToRGB(h, Mathf.Min(1f, s * 1.1f), v * 0.68f),
                Color.HSVToRGB(h, Mathf.Min(1f, s * 1.15f), v * 0.4f)
            };
        }
    }

    /// <summary>
    /// Camouflage brush: instead of one color, each wall, floor or part gets one of the palette's
    /// colors, picked from a 3D noise pattern at its position in the ship. Painting a whole area
    /// segment by segment therefore draws blotches, stripes or pixels that run across walls and
    /// parts. Only the resulting plain colors are sent and saved, so nothing else changes for the
    /// game or other players.
    /// </summary>
    internal static class Camouflage
    {
        public static bool Enabled;
        public static int PaletteIndex;
        public static CamoStyle Style = CamoStyle.Spots;
        /// <summary>Size of the patches, in meters (a wall segment is one meter).</summary>
        public static float Scale = 3f;
        public static int Seed = 1;

        public const float MinScale = 1f;
        public const float MaxScale = 10f;

        public static readonly CamoPalette[] Palettes =
        {
            new CamoPalette(Loc.Pick("Forêt", "Forest", "Bosque"), "#7d8a52", "#4f5f32", "#5b4a33", "#23261c"),
            new CamoPalette(Loc.Pick("Désert", "Desert", "Desierto"), "#d8c49a", "#bfa070", "#9c7a4f", "#6e5a40"),
            new CamoPalette(Loc.Pick("Arctique", "Arctic", "Ártico"), "#f2f4f5", "#c9d0d6", "#8f9aa3", "#4c555c"),
            new CamoPalette(Loc.Pick("Urbain", "Urban", "Urbano"), "#b3b5b8", "#7c7f84", "#4d5055", "#26282b"),
            new CamoPalette(Loc.Pick("Marine", "Navy", "Marina"), "#7f95ad", "#4e6a8a", "#2c4463", "#151f30"),
            new CamoPalette(Loc.Pick("Nuit", "Night", "Noche"), "#4a4f5c", "#2f3340", "#1d2029", "#0c0d12"),
            new CamoPalette(Loc.Pick("Automne", "Autumn", "Otoño"), "#d39a4a", "#a8592c", "#6d4a2a", "#3a2a1c"),
            new CamoPalette(Loc.Pick("Ton sur ton", "Tone on tone", "Tono sobre tono"), null)
        };

        public static CamoPalette Palette => Palettes[Mathf.Clamp(PaletteIndex, 0, Palettes.Length - 1)];

        public static readonly string[] StyleNames = { "Taches", "Rayures", "Numérique" };

        /// <summary>The palette color for a point of the ship, in ship space (meters).</summary>
        public static Color ColorAt(Vector3 shipPosition, Color brush)
        {
            Color[] colors = Palette.Colors(brush);
            return colors[Band(shipPosition, colors.Length)];
        }

        /// <summary>Which of n palette colors a point gets.</summary>
        public static int Band(Vector3 shipPosition, int count)
        {
            Vector3 p = shipPosition / Mathf.Clamp(Scale, MinScale, MaxScale) + SeedOffset();
            switch (Style)
            {
                case CamoStyle.Stripes:
                {
                    // Wavy stripes in the darkest color over a blotched background
                    float wave = Mathf.Sin((p.x * 0.9f + p.z * 0.35f + Fbm(p * 0.6f) * 3.2f) * Mathf.PI);
                    if (wave > 0.55f)
                        return count - 1;
                    return Quantize(Field(p * 0.7f), count - 1);
                }
                case CamoStyle.Digital:
                {
                    // Coarse blocks: the field sampled on a grid a third of the patch size
                    Vector3 cell = new Vector3(Mathf.Floor(p.x * 3f), Mathf.Floor(p.y * 3f), Mathf.Floor(p.z * 3f)) / 3f;
                    return Quantize(Field(cell), count);
                }
                default:
                    return Quantize(Field(p), count);
            }
        }

        /// <summary>A small picture of the pattern on a flat wall, for the HUD.</summary>
        public static void FillPreview(Texture2D texture, Color brush, float metersAcross)
        {
            Color[] colors = Palette.Colors(brush);
            int w = texture.width, h = texture.height;
            var pixels = new Color[w * h];
            float step = metersAcross / w;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                // Snap to whole meters like the walls the brush paints
                var position = new Vector3(Mathf.Floor(x * step), Mathf.Floor(y * step), 0f);
                pixels[y * w + x] = colors[Band(position, colors.Length)];
            }
            texture.SetPixels(pixels);
            texture.Apply();
        }

        // ---- Noise ----

        private static Vector3 SeedOffset() =>
            new Vector3(Hash(Seed, 11, 7) * 512f, Hash(Seed, 23, 3) * 512f, Hash(Seed, 5, 41) * 512f);

        // Thresholds that split the field into equally common bands
        private static float[] _quantiles;

        private static int Quantize(float value, int count)
        {
            EnsureQuantiles();
            for (int i = 1; i < count; i++)
            {
                if (value < Quantile(i / (float)count))
                    return i - 1;
            }
            return count - 1;
        }

        private static float Quantile(float q) => _quantiles[Mathf.Clamp(Mathf.RoundToInt(q * (_quantiles.Length - 1)), 0, _quantiles.Length - 1)];

        private static void EnsureQuantiles()
        {
            if (_quantiles != null)
                return;
            var samples = new List<float>(4096);
            for (int i = 0; i < 4096; i++)
                samples.Add(Field(new Vector3(Hash(i, 1, 2) * 300f, Hash(i, 3, 4) * 300f, Hash(i, 5, 6) * 300f)));
            samples.Sort();
            _quantiles = samples.ToArray();
        }

        // Domain-warped fractal noise: soft, organic blotches
        private static float Field(Vector3 p)
        {
            var warp = new Vector3(
                Value(p + new Vector3(17.1f, 3.2f, 8.7f)) - 0.5f,
                Value(p + new Vector3(2.9f, 31.4f, 12.3f)) - 0.5f,
                Value(p + new Vector3(44.2f, 6.6f, 27.5f)) - 0.5f);
            return Fbm(p + warp * 1.4f);
        }

        private static float Fbm(Vector3 p) =>
            Value(p) * 0.62f + Value(p * 2.03f + new Vector3(5.2f, 1.3f, 7.7f)) * 0.28f + Value(p * 4.1f + new Vector3(9.4f, 2.8f, 3.1f)) * 0.1f;

        private static float Value(Vector3 p)
        {
            int x = Mathf.FloorToInt(p.x), y = Mathf.FloorToInt(p.y), z = Mathf.FloorToInt(p.z);
            float fx = Smooth(p.x - x), fy = Smooth(p.y - y), fz = Smooth(p.z - z);
            float a = Mathf.Lerp(Hash(x, y, z), Hash(x + 1, y, z), fx);
            float b = Mathf.Lerp(Hash(x, y + 1, z), Hash(x + 1, y + 1, z), fx);
            float c = Mathf.Lerp(Hash(x, y, z + 1), Hash(x + 1, y, z + 1), fx);
            float d = Mathf.Lerp(Hash(x, y + 1, z + 1), Hash(x + 1, y + 1, z + 1), fx);
            return Mathf.Lerp(Mathf.Lerp(a, b, fy), Mathf.Lerp(c, d, fy), fz);
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);

        private static float Hash(int x, int y, int z)
        {
            unchecked
            {
                uint h = (uint)x * 0x8da6b343u ^ (uint)y * 0xd8163841u ^ (uint)z * 0xcb1ab31fu;
                h ^= h >> 13;
                h *= 0x5bd1e995u;
                h ^= h >> 15;
                return (h & 0xffffff) / 16777216f;
            }
        }
    }
}

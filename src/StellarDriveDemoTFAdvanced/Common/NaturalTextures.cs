using System;
using System.Collections.Generic;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Common
{
    /// <summary>
    /// Detailed textures drawn once in code, each with its relief (normal map): dressed stone
    /// masonry for the ruins (blocks, recessed joints, chipped edges, cracks, grime, moss or
    /// frost by style), weathered wood, hand-forged iron and engraved metal for the chests. All tile seamlessly.
    /// </summary>
    internal static class NaturalTextures
    {
        public enum Stone { Plain, Sandstone, Mossy, Icy }

        internal const int Size = 512;
        // Masonry: one tile covers 3 m, in 6 courses of 0.5 m
        private const float TileMeters = 3f;
        private const int Courses = 6;

        private static float[] _stoneHeight, _stoneShade;
        private static readonly Dictionary<Stone, Texture2D> StoneAlbedo = new Dictionary<Stone, Texture2D>();
        private static Texture2D _stoneNormal, _wood, _woodNormal, _iron, _ironNormal, _engraved, _engravedNormal;
        private static float[] _woodHeight, _ironHeight, _engravedHeight;

        /// <summary>Texture coordinates per meter the masonry expects (planar mapping).</summary>
        public const float StoneScale = 1f / TileMeters;

        public static Texture2D StoneNormal
        {
            get
            {
                if (_stoneNormal == null)
                {
                    MakeStone();
                    _stoneNormal = NormalMap("TFA_StoneNormal", _stoneHeight, 6f);
                }
                return _stoneNormal;
            }
        }

        public static Texture2D StoneTexture(Stone style)
        {
            if (StoneAlbedo.TryGetValue(style, out Texture2D cached) && cached != null)
                return cached;
            MakeStone();
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int i = y * Size + x;
                    float u = (x + 0.5f) / Size, v = (y + 0.5f) / Size;
                    float h = _stoneHeight[i], shade = _stoneShade[i];
                    Color c = new Color(shade, shade, shade);
                    float joint = 1f - Mathf.Clamp01(h * 3f);
                    switch (style)
                    {
                        case Stone.Sandstone:
                            // Fine strata across the blocks
                            float strata = 0.5f + 0.5f * Mathf.Sin((v * 96f + Fbm(u * 4f, v * 4f, 4, 71) * 3f) * Mathf.PI);
                            c *= 0.9f + 0.1f * strata;
                            break;
                        case Stone.Mossy:
                            // Moss in the joints, along the courses' tops and in patches
                            float patch = Fbm(u * 6f, v * 6f, 6, 81);
                            float moss = Mathf.Clamp01(joint * 1.4f + Mathf.Clamp01((patch - 0.52f) * 5f));
                            moss *= 0.75f + 0.25f * Noise(u * 64f, v * 64f, 64, 82);
                            c = Color.Lerp(c, new Color(0.28f, 0.42f, 0.16f) * (0.8f + 0.4f * patch), moss * 0.85f);
                            break;
                        case Stone.Icy:
                            // Frost in the joints and as bright crusts
                            float frost = Mathf.Clamp01(joint * 1.2f + Mathf.Clamp01((Fbm(u * 5f, v * 5f, 5, 91) - 0.55f) * 4f));
                            c = Color.Lerp(c, new Color(0.92f, 0.97f, 1f), frost * 0.7f);
                            break;
                    }
                    pixels[i] = new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), 1f);
                }
            }
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true) { name = "TFA_Stone" + style, wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            texture.SetPixels32(pixels);
            texture.Apply(true);
            StoneAlbedo[style] = texture;
            return texture;
        }

        // The masonry's height (1 on a block's face, 0 in the joints) and its grey shade
        private static void MakeStone()
        {
            if (_stoneHeight != null)
                return;
            var random = new System.Random(4021);
            // Each course: block joints at random places, the pattern wrapping around the tile
            var cuts = new List<float>[Courses];
            var tones = new List<float>[Courses];
            for (int r = 0; r < Courses; r++)
            {
                cuts[r] = new List<float>();
                tones[r] = new List<float>();
                float at = (float)random.NextDouble() * 0.2f;
                float start = at;
                while (at < start + 1f - 0.12f)
                {
                    cuts[r].Add(at);
                    tones[r].Add(0.72f + 0.22f * (float)random.NextDouble());
                    at += 0.2f + 0.22f * (float)random.NextDouble();
                }
            }
            _stoneHeight = new float[Size * Size];
            _stoneShade = new float[Size * Size];
            float rowMeters = TileMeters / Courses;
            for (int y = 0; y < Size; y++)
            {
                float v = (y + 0.5f) / Size;
                int row = Mathf.Min(Courses - 1, (int)(v * Courses));
                float inRow = v * Courses - row;
                List<float> rowCuts = cuts[row];
                for (int x = 0; x < Size; x++)
                {
                    float u = (x + 0.5f) / Size;
                    // The block under this point and its distance to its joints, in meters
                    int block = rowCuts.Count - 1;
                    float uu = u < rowCuts[0] ? u + 1f : u;
                    for (int k = 0; k < rowCuts.Count; k++)
                    {
                        float next = k + 1 < rowCuts.Count ? rowCuts[k + 1] : rowCuts[0] + 1f;
                        if (uu >= rowCuts[k] && uu < next)
                        {
                            block = k;
                            break;
                        }
                    }
                    float left = rowCuts[block], right = block + 1 < rowCuts.Count ? rowCuts[block + 1] : rowCuts[0] + 1f;
                    float edgeX = Mathf.Min(uu - left, right - uu) * TileMeters;
                    float edgeY = Mathf.Min(inRow, 1f - inRow) * rowMeters;
                    float edge = Mathf.Min(edgeX, edgeY);
                    // Chipped edges: the bevel eaten away where the noise says so
                    float chip = Fbm(u * 24f, v * 24f, 24, 11);
                    float bevel = 0.035f + 0.06f * Mathf.Clamp01((chip - 0.45f) * 3f);
                    float face = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((edge - 0.012f) / bevel));
                    // The block's face: weathered, slightly domed, with cracks
                    float rough = (Fbm(u * 32f, v * 32f, 32, 12) - 0.5f) * 0.12f + (Noise(u * 128f, v * 128f, 128, 13) - 0.5f) * 0.05f;
                    float crackNoise = Fbm(u * 10f + row * 3.1f, v * 10f, 10, 14);
                    float crack = (1f - Mathf.Clamp01(Mathf.Abs(crackNoise - 0.5f) / 0.012f)) * Mathf.Clamp01((Noise(u * 5f, v * 5f, 5, 15) - 0.55f) * 6f);
                    float h = face * (0.88f + rough) - crack * 0.35f * face;
                    int i = y * Size + x;
                    _stoneHeight[i] = Mathf.Clamp01(h);
                    // Shade: the block's own tone, grime streaking down, darker joints and cracks
                    float tone = tones[row][block];
                    float streak = Noise(u * 48f, v * 4f, 48, 4, 16);
                    float grime = Mathf.Clamp01((streak - 0.5f) * 1.6f) * 0.18f * (1f - inRow);
                    float speckle = (Noise(u * 160f, v * 160f, 160, 17) - 0.5f) * 0.1f + (Fbm(u * 16f, v * 16f, 16, 18) - 0.5f) * 0.18f;
                    float shade = Mathf.Lerp(0.38f, tone + speckle - grime, face) - crack * 0.25f;
                    // Worn, lighter corners
                    shade += (1f - Mathf.Clamp01(edge / 0.08f)) * face * 0.06f;
                    _stoneShade[i] = Mathf.Clamp01(shade);
                }
            }
        }

        /// <summary>
        /// Weathered oak, its grain along u (one tile per meter): rings bent round knots, long
        /// fibers, splits, water stains, sun-bleached patches and scratches. The chest's boards are
        /// its geometry, so no joints here.
        /// </summary>
        public static Texture2D Wood
        {
            get
            {
                if (_wood != null)
                    return _wood;
                var random = new System.Random(77);
                var knots = new (Vector2 At, float Radius)[6];
                for (int k = 0; k < knots.Length; k++)
                    knots[k] = (Next2(random), 0.01f + 0.02f * (float)random.NextDouble());
                var splits = new (Vector2 At, float Length)[10];
                for (int k = 0; k < splits.Length; k++)
                    splits[k] = (Next2(random), 0.04f + 0.14f * (float)random.NextDouble());
                (Vector2 A, Vector2 B)[] scratches = Scratches(random, 16, 0.03f, 0.12f);
                var pixels = new Color[Size * Size];
                _woodHeight = new float[Size * Size];
                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        float u = (x + 0.5f) / Size, v = (y + 0.5f) / Size;
                        var p = new Vector2(u, v);
                        // Knots: the rings close round them, a dark core with its own rings
                        float bend = 0f, knot = 0f, knotRings = 0f;
                        foreach ((Vector2 at, float radius) in knots)
                        {
                            float dist = Wrap(p - at).magnitude;
                            bend += 3f * radius / (dist + radius);
                            float core = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((dist - radius * 0.55f) / (radius * 0.45f)));
                            if (core > knot)
                            {
                                knot = core;
                                knotRings = 0.5f + 0.5f * Mathf.Sin(dist / radius * 14f);
                            }
                        }
                        float warp = Fbm(u * 3f, v * 12f, 3, 12, 30) * 6f;
                        float ring = Mathf.Pow(0.5f + 0.5f * Mathf.Sin((v * 70f + warp + bend) * Mathf.PI * 2f), 3f);
                        float fiber = Mathf.Clamp01((Noise(u * 4f, v * 256f, 4, 256, 41) - 0.62f) * 4f);
                        // Splits along the grain, widest in their middle
                        float split = 0f;
                        foreach ((Vector2 at, float length) in splits)
                        {
                            Vector2 d = Wrap(p - at);
                            float along = Mathf.Abs(d.x) / (length * 0.5f);
                            if (along >= 1f)
                                continue;
                            float width = 0.0016f * (1f - along * along);
                            split = Mathf.Max(split, 1f - Mathf.Clamp01(Mathf.Abs(d.y - 0.002f * Mathf.Sin(d.x * 90f)) / width));
                        }
                        float scratch = ScratchAt(p, scratches, 0.0011f);
                        float stain = Mathf.Clamp01((Fbm(u * 4f, v * 4f, 4, 51) - 0.55f) * 3f);
                        float bleach = Mathf.Clamp01((Fbm(u * 3f, v * 3f, 3, 52) - 0.58f) * 3f);
                        Color c = Color.Lerp(new Color(0.5f, 0.32f, 0.18f), new Color(0.63f, 0.44f, 0.27f), Fbm(u * 2f, v * 8f, 2, 8, 53));
                        c *= 1f - 0.35f * ring - 0.15f * fiber;
                        c = Color.Lerp(c, new Color(0.2f, 0.11f, 0.05f) * (0.8f + 0.3f * knotRings), knot * 0.9f);
                        c *= 1f - stain * 0.35f;
                        c = Color.Lerp(c, new Color(0.56f, 0.54f, 0.5f) * (0.85f + 0.3f * ring), bleach * 0.5f);
                        c = Color.Lerp(c, c * 1.4f, scratch * 0.6f);
                        c = Color.Lerp(c, new Color(0.05f, 0.03f, 0.02f), split);
                        int i = y * Size + x;
                        pixels[i] = c;
                        // Weathered: the soft wood between the rings worn lower
                        _woodHeight[i] = Mathf.Clamp01(0.7f - 0.25f * ring - 0.1f * fiber + 0.1f * knot - 0.6f * split - 0.1f * scratch);
                    }
                }
                return _wood = Finish("TFA_Wood", pixels);
            }
        }

        public static Texture2D WoodNormal => _woodNormal != null ? _woodNormal : (_woodNormal = NormalMap("TFA_WoodNormal", Wood == null ? null : _woodHeight, 4f));

        /// <summary>
        /// Hand-forged iron (white-ish greys, tinted by the material): hammer marks side by side,
        /// pits, rust in patches and running down in streaks, bright scratches.
        /// </summary>
        public static Texture2D Iron
        {
            get
            {
                if (_iron != null)
                    return _iron;
                (Vector2 A, Vector2 B)[] scratches = Scratches(new System.Random(61), 24, 0.02f, 0.09f);
                var pixels = new Color[Size * Size];
                _ironHeight = new float[Size * Size];
                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        float u = (x + 0.5f) / Size, v = (y + 0.5f) / Size;
                        // Each hammer blow a shallow dish, ridges between them
                        float blow = Mathf.Clamp01(Cells(u * 28f, v * 28f, 28, 60) * 1.4f);
                        float pit = Mathf.Clamp01((Noise(u * 180f, v * 180f, 180, 64) - 0.8f) * 8f);
                        float rust = Mathf.Clamp01((Fbm(u * 6f, v * 6f, 6, 61) - 0.55f) * 4f);
                        rust = Mathf.Max(rust, Mathf.Clamp01((Noise(u * 64f, v * 3f, 64, 3, 65) - 0.62f) * 3f) * Fbm(u * 8f, v * 8f, 8, 66));
                        rust *= 0.6f + 0.4f * Noise(u * 90f, v * 90f, 90, 62);
                        float scratch = ScratchAt(new Vector2(u, v), scratches, 0.0009f) * (1f - rust);
                        float shade = 0.62f + blow * 0.22f + (Noise(u * 200f, v * 200f, 200, 63) - 0.5f) * 0.08f - pit * 0.3f;
                        Color rustColor = Color.Lerp(new Color(0.42f, 0.18f, 0.07f), new Color(0.72f, 0.4f, 0.16f), Noise(u * 40f, v * 40f, 40, 67));
                        Color c = Color.Lerp(new Color(shade, shade, shade), rustColor, rust * 0.85f);
                        c = Color.Lerp(c, Color.white, scratch * 0.4f);
                        int i = y * Size + x;
                        pixels[i] = c;
                        _ironHeight[i] = Mathf.Clamp01(blow * 0.5f + rust * 0.25f * Noise(u * 120f, v * 120f, 120, 68) - pit * 0.3f - scratch * 0.1f + 0.3f);
                    }
                }
                return _iron = Finish("TFA_Iron", pixels);
            }
        }

        public static Texture2D IronNormal => _ironNormal != null ? _ironNormal : (_ironNormal = NormalMap("TFA_IronNormal", Iron == null ? null : _ironHeight, 3f));

        /// <summary>
        /// Polished metal engraved with scrollwork (a vine with curls and dots, in framed rows),
        /// tarnish darkening the grooves. White-ish: the rarity's color tints it.
        /// </summary>
        public static Texture2D Engraved
        {
            get
            {
                if (_engraved != null)
                    return _engraved;
                const int cells = 6;
                var pixels = new Color[Size * Size];
                _engravedHeight = new float[Size * Size];
                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        float u = (x + 0.5f) / Size, v = (y + 0.5f) / Size;
                        var c = new Vector2(u * cells - Mathf.Floor(u * cells), v * cells - Mathf.Floor(v * cells));
                        float vine = Mathf.Abs(c.y - 0.5f - 0.22f * Mathf.Sin(c.x * Mathf.PI * 2f));
                        float curls = Mathf.Min(Mathf.Abs((c - new Vector2(0.25f, 0.3f)).magnitude - 0.14f), Mathf.Abs((c - new Vector2(0.75f, 0.7f)).magnitude - 0.14f));
                        float dots = Mathf.Min((c - new Vector2(0.25f, 0.3f)).magnitude, (c - new Vector2(0.75f, 0.7f)).magnitude) - 0.035f;
                        float frame = Mathf.Min(c.y, 1f - c.y) - 0.02f;
                        float groove = Mathf.Min(Mathf.Min(vine, curls), Mathf.Min(Mathf.Max(dots, 0f), Mathf.Abs(frame)));
                        float cut = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((groove - 0.012f) / 0.018f));
                        float tarnish = Mathf.Clamp01((Fbm(u * 5f, v * 5f, 5, 71) - 0.5f) * 2f);
                        float brushed = (Noise(u * 2f, v * 512f, 2, 512, 72) - 0.5f) * 0.08f;
                        float shade = 0.92f + brushed - tarnish * 0.15f - cut * (0.45f + 0.2f * tarnish);
                        int i = y * Size + x;
                        pixels[i] = new Color(shade, shade, shade, 1f);
                        _engravedHeight[i] = 1f - cut * 0.6f;
                    }
                }
                return _engraved = Finish("TFA_Engraved", pixels);
            }
        }

        public static Texture2D EngravedNormal => _engravedNormal != null ? _engravedNormal : (_engravedNormal = NormalMap("TFA_EngravedNormal", Engraved == null ? null : _engravedHeight, 3f));

        private static Vector2 Next2(System.Random random) => new Vector2((float)random.NextDouble(), (float)random.NextDouble());

        // The shortest offset on the tile (it wraps around)
        private static Vector2 Wrap(Vector2 d) => new Vector2(Mathf.Repeat(d.x + 0.5f, 1f) - 0.5f, Mathf.Repeat(d.y + 0.5f, 1f) - 0.5f);

        private static (Vector2 A, Vector2 B)[] Scratches(System.Random random, int count, float shortest, float longest)
        {
            var scratches = new (Vector2, Vector2)[count];
            for (int k = 0; k < count; k++)
            {
                Vector2 a = Next2(random);
                float angle = (float)random.NextDouble() * Mathf.PI, length = shortest + (longest - shortest) * (float)random.NextDouble();
                scratches[k] = (a, a + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * length);
            }
            return scratches;
        }

        private static float ScratchAt(Vector2 p, (Vector2 A, Vector2 B)[] scratches, float width)
        {
            float best = 0f;
            foreach ((Vector2 a, Vector2 b) in scratches)
            {
                Vector2 q = a + Wrap(p - a), ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude);
                // Thinner toward the ends
                best = Mathf.Max(best, 1f - Mathf.Clamp01((q - (a + ab * t)).magnitude / (width * (1f - Mathf.Abs(t * 2f - 1f) * 0.7f))));
            }
            return best;
        }

        /// <summary>An albedo texture from its pixels (written to the dev folder while the harness runs, to look at).</summary>
        internal static Texture2D Finish(string name, Color[] pixels)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color(Mathf.Clamp01(pixels[i].r), Mathf.Clamp01(pixels[i].g), Mathf.Clamp01(pixels[i].b), 1f);
            texture.SetPixels(pixels);
            texture.Apply(true);
            if (DevHarness.Active)
            {
                try
                {
                    System.IO.Directory.CreateDirectory(DevHarness.Folder);
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(DevHarness.Folder, name + ".png"), texture.EncodeToPNG());
                    AdvancedMod.Log.Msg($"dev: texture {name} made");
                }
                catch (Exception e)
                {
                    AdvancedMod.Log.Warning($"dev: could not write {name}: {e.Message}");
                }
            }
            return texture;
        }

        /// <summary>A URP Lit material wearing a texture and its relief, tiled per meter (planar UVs of the mod's models).</summary>
        public static Material Lit(string name, Texture2D albedo, Texture2D normal, Color tint, float metallic, float smoothness, float tiling)
        {
            Material material = LitMaterials.Get(name, tint, metallic, smoothness);
            if (material == null)
                return null;
            foreach (string map in new[] { "_BaseMap", "_MainTex" })
            {
                if (!material.HasProperty(map))
                    continue;
                material.SetTexture(map, albedo);
                material.SetTextureScale(map, Vector2.one * tiling);
            }
            if (material.HasProperty("_BumpMap"))
            {
                material.SetTexture("_BumpMap", normal);
                material.SetTextureScale("_BumpMap", Vector2.one * tiling);
                material.SetFloat("_BumpScale", 1f);
                material.EnableKeyword("_NORMALMAP");
            }
            return material;
        }

        internal static Texture2D NormalMap(string name, float[] heights, float strength)
        {
            var pixels = new Color32[Size * Size];
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
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true, true) { name = name, wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            texture.SetPixels32(pixels);
            texture.Apply(true);
            return texture;
        }

        // ---- Tileable value noise (period in cells over the 0-1 tile)

        internal static float Fbm(float x, float y, int period, int seed) => Fbm(x, y, period, period, seed);

        internal static float Fbm(float x, float y, int periodX, int periodY, int seed)
        {
            float sum = 0f, amplitude = 0.5f, total = 0f;
            for (int octave = 0; octave < 4; octave++)
            {
                int scale = 1 << octave;
                sum += Noise(x * scale, y * scale, periodX * scale, periodY * scale, seed + octave * 101) * amplitude;
                total += amplitude;
                amplitude *= 0.5f;
            }
            return sum / total;
        }

        internal static float Noise(float x, float y, int period, int seed) => Noise(x, y, period, period, seed);

        internal static float Noise(float x, float y, int periodX, int periodY, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            float H(int i, int j) => Hash(i, j, periodX, periodY, seed);
            float sx = fx * fx * (3f - 2f * fx), sy = fy * fy * (3f - 2f * fy);
            return Mathf.Lerp(Mathf.Lerp(H(x0, y0), H(x0 + 1, y0), sx), Mathf.Lerp(H(x0, y0 + 1), H(x0 + 1, y0 + 1), sx), sy);
        }

        private static float Hash(int i, int j, int periodX, int periodY, int seed)
        {
            int h = (((i % periodX) + periodX) % periodX) * 73856093 ^ (((j % periodY) + periodY) % periodY) * 19349663 ^ seed * 83492791;
            h = (h << 13) ^ h;
            return ((h * (h * h * 15731 + 789221) + 1376312589) & 0x7fffffff) / 2147483647f;
        }

        /// <summary>Tileable cellular noise: the distance (in cells) to the nearest of one random point per cell.</summary>
        internal static float Cells(float x, float y, int period, int seed)
        {
            int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y);
            float best = 9f;
            for (int j = -1; j <= 1; j++)
            {
                for (int i = -1; i <= 1; i++)
                {
                    int gx = cx + i, gy = cy + j;
                    float dx = x - (gx + Hash(gx, gy, period, period, seed)), dy = y - (gy + Hash(gx, gy, period, period, seed + 7));
                    best = Mathf.Min(best, dx * dx + dy * dy);
                }
            }
            return Mathf.Sqrt(best);
        }
    }
}

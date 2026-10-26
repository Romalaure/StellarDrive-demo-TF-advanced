using System.Collections.Generic;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Common
{
    /// <summary>
    /// Glowing runes for the portal and its generator: a strip of glyphs drawn once (crisp strokes
    /// with a soft glow, between two thin lines with ticks), wrapped around flat rings.
    /// </summary>
    internal static class RuneTexture
    {
        private const int Width = 2048, Height = 128, Glyphs = 24;
        private const float Reach = 30f;
        private static Texture2D _strip;

        /// <summary>White glyphs on transparent (tint them with the material's color).</summary>
        public static Texture2D Strip
        {
            get
            {
                if (_strip == null)
                    _strip = Draw();
                return _strip;
            }
        }

        private static Texture2D Draw()
        {
            var random = new System.Random(7395);
            float cell = Width / (float)Glyphs;
            // Each glyph: 3 to 5 strokes between the nodes of a 3 x 4 grid
            var strokes = new List<(Vector2 A, Vector2 B)>[Glyphs];
            for (int g = 0; g < Glyphs; g++)
            {
                strokes[g] = new List<(Vector2, Vector2)>();
                int count = 3 + random.Next(0, 3);
                // A spine down the middle or a side, then branches
                int spine = random.Next(0, 3);
                strokes[g].Add((Node(g, spine, 0, cell), Node(g, spine, 3, cell)));
                for (int k = 1; k < count; k++)
                {
                    int x0 = random.Next(0, 3), y0 = random.Next(0, 4), x1 = random.Next(0, 3), y1 = random.Next(0, 4);
                    if (x0 == x1 && y0 == y1)
                        x1 = (x1 + 1) % 3;
                    strokes[g].Add((Node(g, x0, y0, cell), Node(g, x1, y1, cell)));
                }
                // Some glyphs get a small ring (a dot)
                if (random.NextDouble() < 0.35)
                {
                    Vector2 c = Node(g, random.Next(0, 3), random.Next(0, 4), cell);
                    for (int i = 0; i < 8; i++)
                    {
                        float a0 = i * Mathf.PI / 4f, a1 = (i + 1) * Mathf.PI / 4f;
                        strokes[g].Add((c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * 7f, c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * 7f));
                    }
                }
            }

            var pixels = new Color32[Width * Height];
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    int g = Mathf.Min(Glyphs - 1, (int)(x / cell));
                    float d = float.MaxValue;
                    // Strokes of this glyph and its neighbours (a glow may spill over)
                    for (int n = g - 1; n <= g + 1; n++)
                    {
                        Vector2 offset = n < 0 ? new Vector2(-Width, 0f) : n >= Glyphs ? new Vector2(Width, 0f) : Vector2.zero;
                        foreach ((Vector2 a, Vector2 b) in strokes[(n + Glyphs) % Glyphs])
                        {
                            Vector2 s0 = a + offset, s1 = b + offset;
                            // The glow is under 1/255 past 28 px
                            if (p.x < Mathf.Min(s0.x, s1.x) - Reach || p.x > Mathf.Max(s0.x, s1.x) + Reach || p.y < Mathf.Min(s0.y, s1.y) - Reach || p.y > Mathf.Max(s0.y, s1.y) + Reach)
                                continue;
                            d = Mathf.Min(d, Segment(p, s0, s1));
                        }
                    }
                    // The two rails and their ticks
                    float rail = Mathf.Min(Mathf.Abs(y - 10f), Mathf.Abs(y - (Height - 10f)));
                    float tickX = Mathf.Abs(Mathf.Repeat(x, cell * 0.5f) - cell * 0.25f);
                    bool tickBand = y < 22f || y > Height - 22f;
                    float tick = tickBand ? Mathf.Max(tickX - 1f, Mathf.Min(Mathf.Abs(y - 10f), Mathf.Abs(y - (Height - 10f))) - 10f) : float.MaxValue;
                    float core = Mathf.Clamp01(3.2f - d) + Mathf.Clamp01(1.6f - rail) * 0.9f + Mathf.Clamp01(1.2f - tick) * 0.7f;
                    float glow = Mathf.Exp(-d / 6f) * 0.45f + Mathf.Exp(-rail / 4f) * 0.25f;
                    byte alpha = (byte)(Mathf.Clamp01(core + glow) * 255f);
                    // Hot white strokes, the glow tinted by the material
                    byte white = (byte)(Mathf.Clamp01(0.55f + core * 0.45f) * 255f);
                    pixels[y * Width + x] = new Color32(white, white, white, alpha);
                }
            }
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, true) { name = "TFA_Runes", wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            texture.SetPixels32(pixels);
            texture.Apply(true);
            return texture;
        }

        private static Vector2 Node(int glyph, int x, int y, float cell) =>
            new Vector2(glyph * cell + cell * (0.28f + 0.22f * x), 32f + y * 21.3f);

        private static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f));
            return (p - (a + ab * t)).magnitude;
        }

        /// <summary>A flat ring in the XY plane at z (u around, the strip <paramref name="repeats"/> times; v from inner to outer edge).</summary>
        public static Mesh Ring(string name, float inner, float outer, float z, int segments, float repeats)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var dir = new Vector3(-Mathf.Sin(a), Mathf.Cos(a), 0f);
                float u = i / (float)segments * repeats;
                vertices.Add(dir * inner + Vector3.forward * z);
                vertices.Add(dir * outer + Vector3.forward * z);
                uvs.Add(new Vector2(u, 0f));
                uvs.Add(new Vector2(u, 1f));
                normals.Add(Vector3.back);
                normals.Add(Vector3.back);
                if (i < segments)
                {
                    int k = i * 2;
                    triangles.AddRange(new[] { k, k + 1, k + 2, k + 1, k + 3, k + 2 });
                }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

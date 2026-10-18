using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace StellarDriveDemoTFAdvanced.Worlds
{
    /// <summary>
    /// Auroras in the night sky of TF Glacia, and of TF Gaïa's cold places (up high, or near the
    /// poles): curtains of green light edged with violet, slowly waving, kilometers above the
    /// player. Only at night, on the ground; visual only. A few hundred vertices moved every
    /// other frame.
    /// </summary>
    internal static class Aurora
    {
        private const int Curtains = 3, Columns = 48;
        private static GameObject _root;
        private static Mesh[] _meshes;
        private static Vector3[][] _base;
        private static Vector3[] _work;
        private static float _shown;
        private static int _frame;

        public static void Update()
        {
            bool wanted = Settings.Weather.Value && !Settings.LowPerformance.Value && Weather.Outside && ((IsCold() && IsNight()) || Settings.DevWeather.Value == "Aurora");
            _shown = Mathf.MoveTowards(_shown, wanted ? 1f : 0f, Time.unscaledDeltaTime / 8f);
            if (_shown <= 0f)
            {
                if (_root != null)
                    _root.SetActive(false);
                return;
            }
            Camera camera = MainCamera.Get();
            if (camera == null)
                return;
            Ensure();
            _root.SetActive(true);
            _root.transform.position = camera.transform.position;
            _root.transform.rotation = Quaternion.FromToRotation(Vector3.up, Weather.Up);
            if (++_frame % 2 != 0)
                return;
            float t = Time.time;
            for (int c = 0; c < Curtains; c++)
            {
                Vector3[] source = _base[c];
                for (int i = 0; i < source.Length; i++)
                {
                    Vector3 p = source[i];
                    // Slow waves along the curtain, a little faster up high
                    float wave = Mathf.Sin(p.x * 0.0012f + t * 0.25f + c) * 160f + Mathf.Sin(p.x * 0.004f - t * 0.6f) * 50f;
                    _work[i] = p + new Vector3(0f, 0f, wave * (0.6f + 0.4f * (i % 2)));
                }
                _meshes[c].vertices = _work;
                Color glow = Color.Lerp(new Color(0.2f, 1f, 0.5f), new Color(0.5f, 0.9f, 1f), 0.5f + 0.5f * Mathf.Sin(t * 0.15f + c));
                _root.transform.GetChild(c).GetComponent<Renderer>().material.SetColor("_BaseColor", new Color(glow.r, glow.g, glow.b, 0.7f * _shown));
            }
        }

        private static bool IsCold()
        {
            if (Weather.PlanetId == 105)
                return true;
            // Gaïa: high ground or toward a pole (its axis is the planet's y)
            return Weather.PlanetId == 108 && (Weather.Elevation > 600f || Mathf.Abs(Weather.Up.y) > 0.7f);
        }

        private static bool IsNight()
        {
            Light sun = RenderSettings.sun;
            if (sun == null)
                return false;
            // The sun more than a little below the horizon
            return Vector3.Dot(Weather.Up, -sun.transform.forward) < -0.08f;
        }

        // Vertical rays of light: brighter and dimmer stripes across, fading toward the top
        private static Texture2D Rays()
        {
            const int width = 256, height = 32;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "TFA_AuroraRays", wrapMode = TextureWrapMode.Repeat };
            var random = new System.Random(5);
            var stripes = new float[width];
            for (int x = 0; x < width; x++)
                stripes[x] = 0.45f + 0.55f * (float)random.NextDouble();
            for (int x = 0; x < width; x++)
            {
                // Smoothed a little across
                float ray = (stripes[x] * 2f + stripes[(x + 1) % width] + stripes[(x + width - 1) % width]) * 0.25f;
                for (int y = 0; y < height; y++)
                {
                    float v = y / (float)(height - 1);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, ray * (1f - v) * Mathf.Clamp01(v * 6f + 0.3f)));
                }
            }
            texture.Apply();
            return texture;
        }

        private static void Ensure()
        {
            if (_root != null)
                return;
            _root = new GameObject("TFA_Aurora");
            _meshes = new Mesh[Curtains];
            _base = new Vector3[Curtains][];
            _work = new Vector3[(Columns + 1) * 2];
            Material material = TransparentMaterials.Additive("TFA_AuroraMat", Rays());
            for (int c = 0; c < Curtains; c++)
            {
                // An arc of curtain 6 to 9 km long, 2.5 to 4 km up, leaning toward a pole
                float radius = 5000f + c * 1500f, height = 2500f + c * 600f, depth = 1200f + c * 300f, arc = 70f + c * 15f;
                var vertices = new Vector3[(Columns + 1) * 2];
                var colors = new Color[vertices.Length];
                var uvs = new Vector2[vertices.Length];
                var triangles = new int[Columns * 6];
                for (int i = 0; i <= Columns; i++)
                {
                    float a = (-arc * 0.5f + arc * i / Columns + c * 40f) * Mathf.Deg2Rad;
                    var ground = new Vector3(Mathf.Sin(a) * radius, 0f, Mathf.Cos(a) * radius);
                    vertices[i * 2] = ground + Vector3.up * height;
                    vertices[i * 2 + 1] = ground + Vector3.up * (height + depth);
                    // Bright green bottom edge, fading violet top, dim at both ends
                    float ends = Mathf.Sin(Mathf.PI * i / Columns);
                    colors[i * 2] = new Color(0.3f, 1f, 0.55f, 0.85f * ends);
                    colors[i * 2 + 1] = new Color(0.55f, 0.3f, 1f, 0f);
                    uvs[i * 2] = new Vector2(i * 4f / Columns, 0f);
                    uvs[i * 2 + 1] = new Vector2(i * 4f / Columns, 1f);
                }
                for (int i = 0; i < Columns; i++)
                {
                    int k = i * 2;
                    triangles[i * 6] = k;
                    triangles[i * 6 + 1] = k + 1;
                    triangles[i * 6 + 2] = k + 3;
                    triangles[i * 6 + 3] = k;
                    triangles[i * 6 + 4] = k + 3;
                    triangles[i * 6 + 5] = k + 2;
                }
                var mesh = new Mesh { name = "TFA_AuroraCurtain" + c };
                mesh.vertices = vertices;
                mesh.colors = colors;
                mesh.uv = uvs;
                mesh.triangles = triangles;
                mesh.bounds = new Bounds(Vector3.up * 3000f, Vector3.one * 30000f);
                mesh.MarkDynamic();
                _meshes[c] = mesh;
                _base[c] = vertices;
                var curtain = new GameObject("Curtain" + c);
                curtain.transform.SetParent(_root.transform, false);
                curtain.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = curtain.AddComponent<MeshRenderer>();
                renderer.material = material != null ? new Material(material) : null;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }
    }
}

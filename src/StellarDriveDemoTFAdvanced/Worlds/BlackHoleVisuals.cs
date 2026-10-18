using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;
using UnityEngine.SceneManagement;
using WorldTracking.Interface.Model.Spaces.Common;
using WorldTracking.Interface.Services;
using WorldTracking.Interface.Values;

namespace StellarDriveDemoTFAdvanced.Worlds
{
    /// <summary>
    /// What the black holes look like, drawn like the game draws its star from afar: in the
    /// visual origin's frame, scaled by 1/10 000 for the game's far camera. Shaders cannot be
    /// compiled at runtime, so the light bending is worked out on the processor instead: every
    /// point of the accretion disk is moved to where its light reaches the viewer
    /// (Beloborodov's approximation of the Schwarzschild light paths), which gives the picture
    /// of a real black hole:
    ///  - the shadow, opaque black, 2.6 r_s (photon capture radius), with its thin photon ring;
    ///  - the disk's near side crossing in front of the shadow, its far side lifted into an arc
    ///    over and under the shadow (primary image), and a thin second image hugging the shadow
    ///    (light that went half around the hole);
    ///  - the gas turning faster inside (Kepler: ω ∝ r^-1.5), so its streaks wind into spirals,
    ///    two texture layers crossfading so they never wind up into stripes;
    ///  - hot blue-white inside, red outside, brighter and bluer on the side coming toward the
    ///    viewer (Doppler beaming), dimmer near the horizon (gravitational redshift).
    /// The shape is worked out again when the viewing direction changes, the turning texture
    /// each frame, and only while the hole looks bigger than a few pixels.
    /// </summary>
    internal static class BlackHoleVisuals
    {
        private const float WorldScale = 0.0001f;
        private const float DiskInner = 3f;
        private const float DiskOuter = 22f;
        private const float CriticalImpact = 2.598f;
        private const int Rings = 34, Spokes = 160;
        /// <summary>One crossfade layer lives this long before it starts again, unwound (s).</summary>
        private const float LayerCycle = 36f;
        /// <summary>Turns per second of the gas at the inner edge.</summary>
        private const float InnerTurnRate = 1f / 14f;

        private sealed class View
        {
            public Hole Hole;
            public GameObject Root;
            public Transform Billboards;
            public Mesh LayerA, LayerB;
            public Vector3[] Positions;
            public Color[] Colors;
            public Vector2[] UvA, UvB;
            public Vector3 LastDirection;
            public float NextShape;
            public bool LoggedCamera;
            public Renderer LayerRenderer, Visible;
        }

        private static readonly List<View> Views = new List<View>();
        private static Material _layerA, _layerB, _shadow, _photon, _halo;
        private static Camera _photoCamera;

        // The disk's grid, in its own plane (y up): radius and angle of each vertex
        private static float[] _radius, _angle, _u, _v, _rate;
        private static int _gridVertices;
        private static int[] _triangles;

        public static void Destroy()
        {
            foreach (View view in Views)
            {
                if (view.Root != null)
                    Object.Destroy(view.Root);
            }
            Views.Clear();
        }

        private static Camera _far;
        private static float _nextFarLookup;

        // The game's far camera (its root is named "Distant..."), looked up at most once per second
        private static Camera FarCamera()
        {
            if (_far != null && _far.isActiveAndEnabled)
                return _far;
            if (Time.unscaledTime < _nextFarLookup)
                return null;
            _nextFarLookup = Time.unscaledTime + 1f;
            _far = Camera.allCameras.FirstOrDefault(c => c.transform.root.name.Contains("Distant"));
            return _far;
        }

        public static void LateUpdate()
        {
            if (!BlackHole.Active)
            {
                foreach (View view in Views)
                {
                    if (view.Root != null)
                        view.Root.SetActive(false);
                }
                return;
            }
            try
            {
                IWorldTransposeClient transpose = GameServices.Transpose;
                Camera far = FarCamera();
                if (transpose == null || far == null || ClientOrigin.LocalVisualSpace == null)
                    return;
                if (Views.Count == 0)
                {
                    foreach (Hole hole in BlackHole.Placed)
                    {
                        View built = Build(far, hole);
                        if (built == null)
                            return;
                        Views.Add(built);
                    }
                }
                Crossfade();
                Camera viewer = _photoCamera != null ? _photoCamera : far;
                foreach (View view in Views)
                    Place(view, transpose, viewer, _photoCamera == null);
            }
            catch (System.Exception e)
            {
                ErrorLog.Report("black hole visuals", e);
            }
        }

        // Each layer fades in, lives a cycle and fades out while the other, half a cycle apart, takes over
        private static void Crossfade()
        {
            float phase = Time.time / LayerCycle;
            float a = 1f - Mathf.Abs(2f * Mathf.Repeat(phase, 1f) - 1f);
            _layerA.SetColor("_BaseColor", new Color(1f, 1f, 1f, a));
            _layerB.SetColor("_BaseColor", new Color(1f, 1f, 1f, 1f - a));
        }

        private static void Place(View view, IWorldTransposeClient transpose, Camera viewer, bool follow)
        {
            if (view.Root == null || view.Hole.Space == null)
                return;
            view.Root.SetActive(true);
            if (follow)
            {
                Vector3 position = transpose.Position((ISpace)view.Hole.Space, ClientOrigin.LocalVisualSpace, Vector3.zero);
                view.Root.transform.position = position * WorldScale;
                view.Root.transform.localScale = Vector3.one * (view.Hole.Horizon * WorldScale);
            }

            Vector3 toCamera = viewer.transform.position - view.Root.transform.position;
            if (toCamera.sqrMagnitude < 1e-10f)
                return;
            Vector3 direction = toCamera.normalized;
            view.Billboards.rotation = Quaternion.LookRotation(-direction, viewer.transform.up);

            // Too small to see any detail: keep the last picture
            float apparent = view.Root.transform.localScale.x * DiskOuter / toCamera.magnitude;
            if (apparent < 0.004f && view.Positions != null && view.LastDirection != Vector3.zero)
                return;
            if (view.Positions == null || Vector3.Angle(direction, view.LastDirection) > 0.15f || Time.unscaledTime >= view.NextShape)
            {
                Shape(view, direction);
                view.LastDirection = direction;
                view.NextShape = Time.unscaledTime + 0.5f;
            }
            if (view.Visible == null)
                view.Visible = view.LayerRenderer;
            if (view.Visible == null || view.Visible.isVisible || !follow)
                Turn(view);
            if (!view.LoggedCamera && follow)
            {
                view.LoggedCamera = true;
                AdvancedMod.Log.Msg($"black hole visuals: {view.Hole.Name}, far camera {viewer.name} clip {viewer.nearClipPlane}..{viewer.farClipPlane}, layer {view.Root.layer}, at {toCamera.magnitude / WorldScale / 1000f:0} km");
            }
        }

        // ---- Light bending

        /// <summary>
        /// Impact parameter (in r_s) of the light leaving radius r (in r_s) that reaches a far
        /// viewer after turning by ψ around the hole: 1 - cos α = (1 - cos ψ)(1 - 1/r), b = r sin α / √(1 - 1/r).
        /// </summary>
        private static float Impact(float r, float psi)
        {
            float g = 1f - 1f / r;
            float cosAlpha = Mathf.Clamp(1f - (1f - Mathf.Cos(psi)) * g, -1f, 1f);
            return r * Mathf.Sqrt(1f - cosAlpha * cosAlpha) / Mathf.Sqrt(g);
        }

        /// <summary>Where each point of the disk is seen from this direction, and how bright and what color.</summary>
        private static void Shape(View view, Vector3 toViewer)
        {
            Quaternion tilt = view.Hole.DiskTilt;
            Quaternion toBillboard = Quaternion.Inverse(view.Billboards.rotation);
            Vector3 eye = toViewer;
            int n = _gridVertices;
            for (int i = 0; i < n; i++)
            {
                float r = _radius[i], phi = _angle[i];
                float cos = Mathf.Cos(phi), sin = Mathf.Sin(phi);
                Vector3 radial = tilt * new Vector3(cos, 0f, sin);
                Vector3 velocity = tilt * new Vector3(-sin, 0f, cos);
                float cosPsi = Mathf.Clamp(Vector3.Dot(radial, eye), -1f, 1f);
                float psi = Mathf.Acos(cosPsi);
                Vector3 across = radial - eye * cosPsi;
                Vector3 screen = across.sqrMagnitude > 1e-8f ? toBillboard * across.normalized : Vector3.up;
                screen.z = 0f;
                screen.Normalize();

                // Primary image: the short way round
                float b1 = Impact(r, psi);
                view.Positions[i] = screen * b1;
                // Secondary image: the long way round (2π - ψ), on the other side, closing in on the shadow
                float behind = Impact(r, Mathf.PI);
                float b2 = CriticalImpact + (behind - CriticalImpact) * Mathf.Exp(-(Mathf.PI - psi) * 1.3f) * 0.55f;
                view.Positions[n + i] = -screen * b2;

                // Doppler: orbital speed β = √(1 / (2(r - 1))) in light speeds, redshift √(1 - 1/r)
                float beta = Mathf.Sqrt(0.5f / (r - 1f));
                float gamma = 1f / Mathf.Sqrt(1f - beta * beta);
                float approach = Vector3.Dot(velocity, eye);
                float shift = Mathf.Sqrt(1f - 1f / r) / (gamma * (1f - beta * approach));
                view.Colors[i] = Tint(shift, 1f);
                float approach2 = -approach;
                float shift2 = Mathf.Sqrt(1f - 1f / r) / (gamma * (1f - beta * approach2));
                view.Colors[n + i] = Tint(shift2, 0.45f);
            }
            view.LayerA.vertices = view.Positions;
            view.LayerA.colors = view.Colors;
            view.LayerB.vertices = view.Positions;
            view.LayerB.colors = view.Colors;
        }

        // Brightness about g³ (beaming), bluer coming, redder going
        private static Color Tint(float g, float scale)
        {
            float brightness = Mathf.Clamp(g * g * g, 0.1f, 2.8f) * scale * 1.35f;
            Color shade = g >= 1f ? Color.Lerp(Color.white, new Color(0.75f, 0.88f, 1.25f), Mathf.Clamp01((g - 1f) * 2.5f))
                : Color.Lerp(Color.white, new Color(1.25f, 0.62f, 0.38f), Mathf.Clamp01((1f - g) * 2.2f));
            Color c = shade * brightness;
            c.a = 1f;
            return c;
        }

        /// <summary>The gas turning: u shifts by the turns made, faster inside; each layer restarts unwound when invisible.</summary>
        private static void Turn(View view)
        {
            float t = Time.time / LayerCycle;
            float ageA = Mathf.Repeat(t, 1f) * LayerCycle, ageB = Mathf.Repeat(t + 0.5f, 1f) * LayerCycle;
            float seedA = Mathf.Floor(t) * 0.37f, seedB = Mathf.Floor(t + 0.5f) * 0.61f + 0.5f;
            int n = _gridVertices;
            for (int i = 0; i < n; i++)
            {
                float rate = _rate[i], u = _u[i], v = _v[i];
                var a = new Vector2(u - (ageA * rate + seedA) * 3f, v);
                var b = new Vector2(u - (ageB * rate + seedB) * 3f, v);
                view.UvA[i] = view.UvA[n + i] = a;
                view.UvB[i] = view.UvB[n + i] = b;
            }
            view.LayerA.SetUVs(0, view.UvA);
            view.LayerB.SetUVs(0, view.UvB);
        }

        // ---- Building

        private static View Build(Camera far, Hole hole)
        {
            Material glowTemplate = Resources.FindObjectsOfTypeAll<Material>()
                .FirstOrDefault(m => m != null && m.shader != null && m.shader.name == "Universal Render Pipeline/Particles/Unlit");
            if (glowTemplate == null)
            {
                ErrorLog.Report("black hole visuals", new System.Exception("no particle material to copy"));
                return null;
            }
            int layer = FarLayer(far);
            if (_layerA == null)
            {
                Texture2D diskTexture = DiskTexture();
                Texture2D ringTexture = RingTexture();
                _shadow = Opaque(glowTemplate, Color.black);
                _layerA = Glow(glowTemplate, diskTexture, Color.white, "TFA_BlackHoleDiskA");
                _layerB = Glow(glowTemplate, diskTexture, Color.white, "TFA_BlackHoleDiskB");
                _photon = Glow(glowTemplate, ringTexture, new Color(1f, 0.9f, 0.75f, 1f), "TFA_BlackHolePhoton");
                _halo = Glow(glowTemplate, HaloTexture(), new Color(1f, 0.62f, 0.32f, 0.16f), "TFA_BlackHoleHalo");
                Grid();
            }
            var view = new View { Hole = hole };
            view.Root = new GameObject($"TFA Black Hole (far) {hole.Name}") { layer = layer };
            Object.DontDestroyOnLoad(view.Root);

            // Units: 1 = event horizon radius. Everything is drawn as seen, facing the viewer
            view.Billboards = Child("Billboards", view.Root.transform, layer).transform;
            GameObject shadow = Child("Shadow", view.Billboards, layer);
            shadow.transform.localPosition = new Vector3(0f, 0f, 0.05f);
            Mesh(shadow, Disc(CriticalImpact, 128), _shadow);
            Mesh(Child("PhotonRing", view.Billboards, layer), Annulus(2.57f, 2.78f, 160, 1f), _photon);
            Mesh(Child("Halo", view.Billboards, layer), Annulus(2.6f, 16f, 96, 1f), _halo);

            int count = _gridVertices * 2;
            view.Positions = new Vector3[count];
            view.Colors = new Color[count];
            view.UvA = new Vector2[count];
            view.UvB = new Vector2[count];
            view.LayerA = DiskMesh("TFA_BlackHoleDiskA", count);
            view.LayerB = DiskMesh("TFA_BlackHoleDiskB", count);
            Mesh(Child("DiskA", view.Billboards, layer), view.LayerA, _layerA);
            view.LayerRenderer = view.Billboards.Find("DiskA").GetComponent<Renderer>();
            Mesh(Child("DiskB", view.Billboards, layer), view.LayerB, _layerB);
            return view;
        }

        // Rings closer together inside, where the disk is brightest and bends most
        private static void Grid()
        {
            _gridVertices = Rings * (Spokes + 1);
            _radius = new float[_gridVertices];
            _angle = new float[_gridVertices];
            _u = new float[_gridVertices];
            _v = new float[_gridVertices];
            _rate = new float[_gridVertices];
            for (int ring = 0; ring < Rings; ring++)
            {
                float t = ring / (Rings - 1f);
                float r = DiskInner + (DiskOuter - DiskInner) * t * t;
                for (int spoke = 0; spoke <= Spokes; spoke++)
                {
                    int i = ring * (Spokes + 1) + spoke;
                    _radius[i] = r;
                    _angle[i] = spoke * Mathf.PI * 2f / Spokes;
                    _u[i] = spoke * 3f / Spokes;
                    _v[i] = t * t;
                    _rate[i] = InnerTurnRate * Mathf.Pow(DiskInner / r, 1.5f);
                }
            }
            var triangles = new List<int>();
            for (int image = 0; image < 2; image++)
            {
                int offset = image * _gridVertices;
                for (int ring = 0; ring < Rings - 1; ring++)
                {
                    for (int spoke = 0; spoke < Spokes; spoke++)
                    {
                        int a = offset + ring * (Spokes + 1) + spoke, b = a + 1, c = a + Spokes + 1, d = c + 1;
                        triangles.AddRange(new[] { a, c, d, a, d, b });
                    }
                }
            }
            _triangles = triangles.ToArray();
        }

        private static Mesh DiskMesh(string name, int count)
        {
            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.MarkDynamic();
            mesh.vertices = new Vector3[count];
            mesh.uv = new Vector2[count];
            mesh.colors = new Color[count];
            mesh.triangles = _triangles;
            // The picture always fits in the halo: no bounds to work out again
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2f * (DiskOuter + 6f));
            return mesh;
        }

        // The layer the game draws its far star on, so the far camera sees the hole too
        private static int FarLayer(Camera far)
        {
            GameObject star = SceneManager.GetActiveScene().GetRootGameObjects()
                .FirstOrDefault(r => r.name.EndsWith("(far)") || r.name.EndsWith("(mid)"));
            if (star != null)
                return star.layer;
            for (int i = 0; i < 32; i++)
            {
                if ((far.cullingMask & (1 << i)) != 0)
                    return i;
            }
            return 0;
        }

        private static GameObject Child(string name, Transform parent, int layer)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void Mesh(GameObject go, Mesh mesh, Material material)
        {
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static Material Glow(Material template, Texture texture, Color color, string name)
        {
            var material = new Material(template) { name = name };
            material.shaderKeywords = new string[0];
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", color);
            // Additive, transparent, no depth write, vertex colors multiplied
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = 3000;
            return material;
        }

        private static Material Opaque(Material template, Color color)
        {
            var material = new Material(template) { name = "TFA_BlackHoleShadow" };
            material.shaderKeywords = new string[0];
            material.SetTexture("_BaseMap", Texture2D.whiteTexture);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            material.SetFloat("_ZWrite", 1f);
            material.SetFloat("_Cull", 0f);
            // Drawn after the opaque scene but before the glows, so it hides what lies behind it
            material.renderQueue = 2990;
            return material;
        }

        // ---- Meshes (in event horizon radii), facing the viewer (XY plane)

        private static Mesh Disc(float radius, int segments)
        {
            var vertices = new List<Vector3> { Vector3.zero };
            var uvs = new List<Vector2> { new Vector2(0.5f, 0.5f) };
            var triangles = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                vertices.Add(new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius);
                uvs.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
                if (i > 0)
                {
                    triangles.Add(0);
                    triangles.Add(i + 1);
                    triangles.Add(i);
                }
            }
            var mesh = new Mesh { name = "TFA_BlackHoleDisc" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Ring in the XY plane; u runs around, v outward.</summary>
        private static Mesh Annulus(float inner, float outer, int segments, float uRepeat)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var direction = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                vertices.Add(direction * inner);
                vertices.Add(direction * outer);
                float u = (float)i / segments * uRepeat;
                uvs.Add(new Vector2(u, 0f));
                uvs.Add(new Vector2(u, 1f));
                if (i > 0)
                {
                    int b = i * 2;
                    triangles.AddRange(new[] { b - 2, b - 1, b + 1, b - 2, b + 1, b });
                }
            }
            var mesh = new Mesh { name = "TFA_BlackHoleRing" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---- Procedural textures

        /// <summary>
        /// u: around the disk (three repeats), v: inner (blue-white, ~10 000 K) to outer (deep red,
        /// ~2 000 K). Fine gas filaments along the orbit, clumps, dark lanes, brightest just past
        /// the inner edge and fading out.
        /// </summary>
        private static Texture2D DiskTexture()
        {
            const int width = 1024, height = 128;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, true) { name = "TFA_AccretionDisk", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4, hideFlags = HideFlags.DontUnloadUnusedAsset };
            var hot = new Color(0.92f, 0.96f, 1f);
            var white = new Color(1f, 0.93f, 0.72f);
            var warm = new Color(1f, 0.66f, 0.24f);
            var cool = new Color(0.98f, 0.34f, 0.06f);
            var cold = new Color(0.5f, 0.07f, 0.02f);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = y / (height - 1f);
                Color temperature = v < 0.08f ? Color.Lerp(hot, white, v / 0.08f)
                    : v < 0.3f ? Color.Lerp(white, warm, (v - 0.08f) / 0.22f)
                    : v < 0.6f ? Color.Lerp(warm, cool, (v - 0.3f) / 0.3f)
                    : Color.Lerp(cool, cold, (v - 0.6f) / 0.4f);
                float profile = Mathf.SmoothStep(0f, 1f, v / 0.025f) * Mathf.Pow(1f - v, 2.2f) * (1f + 1.4f * Mathf.Exp(-v * 14f));
                for (int x = 0; x < width; x++)
                {
                    // Periodic in u so the texture wraps around
                    float angle = (float)x / width * Mathf.PI * 2f;
                    float cx = Mathf.Cos(angle), sx = Mathf.Sin(angle);
                    // Filaments: thin bands across v, slowly changing along u
                    float lanes = Mathf.PerlinNoise(v * 46f + cx * 1.5f + 3.1f, sx * 1.5f + 8.2f);
                    lanes = 0.45f + 0.75f * Mathf.Pow(lanes, 1.6f);
                    float clumps = 0.6f + 0.55f * Mathf.PerlinNoise(cx * 4f + v * 10f + 21.7f, sx * 4f + v * 3f + 5.4f);
                    float fine = 0.82f + 0.18f * Mathf.PerlinNoise(cx * 22f + 40f, sx * 22f + v * 70f);
                    float brightness = profile * lanes * clumps * fine * 2.1f;
                    Color c = temperature * Mathf.Min(brightness, 1.8f);
                    c.a = Mathf.Clamp01(brightness);
                    pixels[y * width + x] = c;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(true);
            return texture;
        }

        /// <summary>v outward: a soft glow fading smoothly to nothing (fine steps, dithered: no visible bands).</summary>
        private static Texture2D HaloTexture()
        {
            const int width = 4, height = 256;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "TFA_BlackHoleHalo", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontUnloadUnusedAsset };
            var random = new System.Random(5);
            for (int y = 0; y < height; y++)
            {
                float v = y / (height - 1f);
                float glow = Mathf.Exp(-v * 5f) * Mathf.SmoothStep(0f, 1f, v / 0.04f) * (1f - v);
                for (int x = 0; x < width; x++)
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(glow + ((float)random.NextDouble() - 0.5f) / 255f)));
            }
            texture.Apply();
            return texture;
        }

        /// <summary>v across a thin ring: bright core, soft falloff.</summary>
        private static Texture2D RingTexture()
        {
            const int width = 8, height = 64;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "TFA_PhotonRing", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontUnloadUnusedAsset };
            for (int y = 0; y < height; y++)
            {
                float v = y / (height - 1f);
                float core = Mathf.Exp(-Mathf.Pow((v - 0.08f) / 0.06f, 2f)) + 0.35f * Mathf.Exp(-v * 4f);
                var c = new Color(1f, 1f, 1f, Mathf.Clamp01(core));
                for (int x = 0; x < width; x++)
                    texture.SetPixel(x, y, c);
            }
            texture.Apply();
            return texture;
        }

        // ---- Development: the first hole photographed up close from several angles

        public static IEnumerator DevPhotos()
        {
            for (int wait = 0; wait < 600 && Views.Count == 0; wait++)
                yield return null;
            View view = Views.FirstOrDefault(v => v.Root != null);
            if (view == null)
            {
                AdvancedMod.Log.Warning("dev: black hole photos: no hole built");
                yield break;
            }
            var holder = new GameObject("TFA_BlackHolePhotoCamera");
            Camera camera = holder.AddComponent<Camera>();
            Camera far = FarCamera();
            if (far != null)
            {
                camera.CopyFrom(far);
                // The far camera's own renderer: the default one leaves the far layer out
                var farData = far.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                object index = farData != null ? HarmonyLib.AccessTools.Field(farData.GetType(), "m_RendererIndex")?.GetValue(farData) : null;
                var data = holder.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                if (index is int rendererIndex)
                    data.SetRenderer(rendererIndex);
                data.renderPostProcessing = false;
                AdvancedMod.Log.Msg($"dev: black hole photo: far camera renderer {index}, type {farData?.renderType}");
            }
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.005f, 0.006f, 0.012f, 1f);
            camera.cullingMask = 1 << view.Root.layer;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 5000f;
            camera.fieldOfView = 40f;
            var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            camera.targetTexture = target;
            _photoCamera = camera;
            // Far from everything, but not so far that float precision shakes the picture
            view.Root.transform.position = new Vector3(0f, 20000f, 0f);
            view.Root.transform.localScale = Vector3.one;
            float[] inclinations = { 84f, 70f, 40f, 12f };
            for (int shot = 0; shot < inclinations.Length * 2; shot++)
            {
                float inclination = inclinations[shot / 2] * Mathf.Deg2Rad;
                Quaternion tilt = view.Hole.DiskTilt;
                Vector3 normal = tilt * Vector3.up;
                Vector3 side = tilt * Vector3.right;
                Vector3 direction = (normal * Mathf.Cos(inclination) + side * Mathf.Sin(inclination)).normalized;
                holder.transform.position = view.Root.transform.position + direction * 70f;
                holder.transform.rotation = Quaternion.LookRotation(-direction, normal);
                // The second shot of each angle 6 s later: the gas has turned
                float until = Time.unscaledTime + (shot % 2 == 0 ? 1f : 6f);
                while (Time.unscaledTime < until)
                    yield return null;
                camera.Render();
                if (shot == 0)
                    AdvancedMod.Log.Msg($"dev: black hole photo: root {view.Root.activeInHierarchy} at {view.Root.transform.position} scale {view.Root.transform.localScale.x}, layer {view.Root.layer}, renderers {string.Join(", ", view.Root.GetComponentsInChildren<MeshRenderer>().Select(r => $"{r.name} {r.enabled} {r.bounds.center}/{r.bounds.size.magnitude:0.0} {r.sharedMaterial?.shader?.name}"))}; camera at {holder.transform.position} mask {camera.cullingMask} clip {camera.nearClipPlane}-{camera.farClipPlane}, first vertex {view.Positions[0]}");
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                RenderTexture.active = previous;
                Directory.CreateDirectory(DevHarness.Folder);
                File.WriteAllBytes(Path.Combine(DevHarness.Folder, $"blackhole_close_{shot}.png"), image.EncodeToPNG());
                Object.Destroy(image);
            }
            _photoCamera = null;
            camera.targetTexture = null;
            target.Release();
            Object.Destroy(holder);
            AdvancedMod.Log.Msg("dev: black hole photos taken");
        }
    }
}

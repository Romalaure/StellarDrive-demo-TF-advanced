using System.Collections.Generic;
using System.Linq;
using Core.Services;
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
    /// visual origin's frame, scaled by 1/10 000 for the game's far camera. A real black hole is
    /// seen through its light bending, which needs a lensing shader the game does not ship (and
    /// shaders cannot be compiled at runtime), so the classic image is built from its parts:
    ///  - the shadow, opaque black, 2.6 r_s (photon capture radius);
    ///  - the accretion disk, from the innermost stable orbit (3 r_s) out, hottest (blue-white)
    ///    inside and red outside, turning, brighter on the side moving toward the viewer (Doppler);
    ///  - the lensed image of the disk's far side, a ring around the shadow facing the viewer;
    ///  - the photon ring, a thin bright line at the shadow's edge, and a faint glow.
    /// </summary>
    internal static class BlackHoleVisuals
    {
        private const float WorldScale = 0.0001f;
        private const float DiskInner = 3f;
        private const float DiskOuter = 40f;
        private const int Segments = 160;

        private sealed class View
        {
            public Hole Hole;
            public GameObject Root;
            public Transform DiskTransform;
            public Transform Billboards;
            public Mesh DiskMesh;
            public Color[] DiskBaseColors;
            public Vector3[] DiskTangents;
            public Color[] DiskColors;
            public float NextDoppler;
            public bool LoggedCamera;
        }

        private static readonly List<View> Views = new List<View>();
        private static Material _diskMaterial;

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
            bool show = BlackHole.Active;
            if (!show)
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
                if (_diskMaterial != null)
                    _diskMaterial.SetTextureOffset("_BaseMap", new Vector2(Time.time * 0.004f, 0f));
                foreach (View view in Views)
                    Place(view, transpose, far);
            }
            catch (System.Exception e)
            {
                ErrorLog.Report("black hole visuals", e);
            }
        }

        private static void Place(View view, IWorldTransposeClient transpose, Camera far)
        {
            if (view.Root == null || view.Hole.Space == null)
                return;
            view.Root.SetActive(true);
            Vector3 position = transpose.Position((ISpace)view.Hole.Space, ClientOrigin.LocalVisualSpace, Vector3.zero);
            view.Root.transform.position = position * WorldScale;
            view.Root.transform.localScale = Vector3.one * (view.Hole.Horizon * WorldScale);

            // Rings face the viewer; the disk keeps its own plane and turns
            Vector3 toCamera = far.transform.position - view.Root.transform.position;
            if (toCamera.sqrMagnitude > 1e-6f)
                view.Billboards.rotation = Quaternion.LookRotation(-toCamera.normalized, far.transform.up);
            view.DiskTransform.localRotation = view.Hole.DiskTilt;
            if (Time.unscaledTime >= view.NextDoppler)
            {
                view.NextDoppler = Time.unscaledTime + 0.1f;
                Doppler(view, toCamera.normalized);
            }
            if (!view.LoggedCamera)
            {
                view.LoggedCamera = true;
                AdvancedMod.Log.Msg($"black hole visuals: {view.Hole.Name}, far camera {far.name} clip {far.nearClipPlane}..{far.farClipPlane}, layer {view.Root.layer}, at {position.magnitude / 1000f:0} km");
            }
        }

        // Approaching material is blueshifted and beamed: brighter on the side turning toward the viewer
        private static void Doppler(View view, Vector3 toCamera)
        {
            Vector3 local = Quaternion.Inverse(view.DiskTransform.rotation) * toCamera;
            for (int i = 0; i < view.DiskColors.Length; i++)
            {
                float approach = Vector3.Dot(view.DiskTangents[i], local);
                float beaming = Mathf.Clamp(1f + approach * 0.75f, 0.25f, 1.9f);
                Color c = view.DiskBaseColors[i] * beaming;
                c.a = view.DiskBaseColors[i].a;
                view.DiskColors[i] = c;
            }
            view.DiskMesh.colors = view.DiskColors;
        }

        private static Material _shadow, _lensed, _photon, _halo;

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
            if (_diskMaterial == null)
            {
                Texture2D diskTexture = DiskTexture();
                Texture2D ringTexture = RingTexture();
                _shadow = Opaque(glowTemplate, Color.black);
                _diskMaterial = Glow(glowTemplate, diskTexture, Color.white, "TFA_BlackHoleDisk");
                _lensed = Glow(glowTemplate, diskTexture, new Color(1f, 0.85f, 0.7f, 0.85f), "TFA_BlackHoleLensed");
                _photon = Glow(glowTemplate, ringTexture, new Color(1f, 0.92f, 0.8f, 1f), "TFA_BlackHolePhoton");
                _halo = Glow(glowTemplate, ringTexture, new Color(1f, 0.5f, 0.25f, 0.18f), "TFA_BlackHoleHalo");
            }
            var view = new View { Hole = hole };
            view.Root = new GameObject($"TFA Black Hole (far) {hole.Name}") { layer = layer };
            Object.DontDestroyOnLoad(view.Root);

            // Units: 1 = event horizon radius
            view.Billboards = Child("Billboards", view.Root.transform, layer).transform;
            Mesh(Child("Shadow", view.Billboards, layer), Disc(hole.Shadow / hole.Horizon, 96), _shadow);
            Mesh(Child("PhotonRing", view.Billboards, layer), Annulus(2.58f, 2.75f, 128, 1f, true), _photon);
            Mesh(Child("LensedDisk", view.Billboards, layer), Annulus(2.6f, 4.6f, 128, 2f, true), _lensed);
            Mesh(Child("Halo", view.Billboards, layer), Annulus(2.6f, 60f, 64, 1f, true), _halo);

            GameObject disk = Child("Disk", view.Root.transform, layer);
            view.DiskTransform = disk.transform;
            view.DiskMesh = Annulus(DiskInner, DiskOuter, Segments, 3f, false);
            view.DiskBaseColors = DiskColors(view.DiskMesh);
            view.DiskColors = (Color[])view.DiskBaseColors.Clone();
            view.DiskTangents = view.DiskMesh.vertices.Select(v => new Vector3(-v.z, 0f, v.x).normalized).ToArray();
            Mesh(disk, view.DiskMesh, _diskMaterial);
            return view;
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
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", color);
            // Additive, transparent, no depth write
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000;
            return material;
        }

        private static Material Opaque(Material template, Color color)
        {
            var material = new Material(template) { name = "TFA_BlackHoleShadow" };
            material.SetTexture("_BaseMap", Texture2D.whiteTexture);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            material.SetFloat("_ZWrite", 1f);
            material.SetFloat("_Cull", 0f);
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            // Drawn after the opaque scene but before the glows, so it hides what lies behind it
            material.renderQueue = 2990;
            return material;
        }

        // ---- Meshes (in event horizon radii)

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

        /// <summary>Ring in the XY plane (facing the viewer, <paramref name="facing"/>) or the XZ plane (the disk). u runs around, v outward.</summary>
        private static Mesh Annulus(float inner, float outer, int segments, float uRepeat, bool facing)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var direction = facing ? new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) : new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
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

        // Inner edge hottest and brightest, fading out
        private static Color[] DiskColors(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            var colors = new Color[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                float t = Mathf.InverseLerp(DiskInner, DiskOuter, vertices[i].magnitude);
                colors[i] = new Color(1f, 1f, 1f, 1f - t * 0.85f);
            }
            mesh.colors = colors;
            return colors;
        }

        // ---- Procedural textures

        /// <summary>u: around the disk (turbulent streaks), v: inner (blue-white, ~10 000 K) to outer (deep red, ~2 000 K).</summary>
        private static Texture2D DiskTexture()
        {
            const int width = 512, height = 64;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, true) { name = "TFA_AccretionDisk", wrapMode = TextureWrapMode.Repeat, hideFlags = HideFlags.DontUnloadUnusedAsset };
            var hot = new Color(0.82f, 0.9f, 1f);
            var warm = new Color(1f, 0.83f, 0.5f);
            var cool = new Color(1f, 0.42f, 0.12f);
            var cold = new Color(0.55f, 0.08f, 0.03f);
            for (int y = 0; y < height; y++)
            {
                float v = y / (height - 1f);
                Color temperature = v < 0.25f ? Color.Lerp(hot, warm, v / 0.25f)
                    : v < 0.6f ? Color.Lerp(warm, cool, (v - 0.25f) / 0.35f)
                    : Color.Lerp(cool, cold, (v - 0.6f) / 0.4f);
                // Brightest near the inner edge, soft at both edges
                float profile = Mathf.SmoothStep(0f, 1f, v / 0.06f) * Mathf.Pow(1f - v, 1.6f);
                for (int x = 0; x < width; x++)
                {
                    float u = (float)x / width;
                    // Streaks: periodic in u so the texture wraps around the disk
                    float angle = u * Mathf.PI * 2f;
                    float streaks = 0.55f + 0.45f * Mathf.PerlinNoise(Mathf.Cos(angle) * 3f + v * 9f + 7.3f, Mathf.Sin(angle) * 3f + v * 2f + 1.1f);
                    float fine = 0.8f + 0.2f * Mathf.PerlinNoise(Mathf.Cos(angle) * 18f + 3f, Mathf.Sin(angle) * 18f + v * 30f);
                    float brightness = profile * streaks * fine * 1.6f;
                    Color c = temperature * brightness;
                    c.a = Mathf.Clamp01(brightness);
                    texture.SetPixel(x, y, c);
                }
            }
            texture.Apply(true);
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
    }
}

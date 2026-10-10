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
    /// What the black hole looks like, drawn like the game draws its star from afar: in the
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

        private static GameObject _root;
        private static Transform _diskTransform;
        private static Transform _billboards;
        private static Mesh _diskMesh;
        private static Color[] _diskBaseColors;
        private static Vector3[] _diskTangents;
        private static Color[] _diskColors;
        private static Material _diskMaterial;
        private static float _nextDoppler;
        private static bool _loggedCamera;
        private static readonly Quaternion DiskTilt = Quaternion.Euler(48f, 35f, -12f);

        public static void Destroy()
        {
            if (_root != null)
                Object.Destroy(_root);
            _root = null;
            _loggedCamera = false;
        }

        public static void LateUpdate()
        {
            if (!BlackHole.Active || SceneManager.GetActiveScene().name != "MainGame")
            {
                if (_root != null)
                    _root.SetActive(false);
                return;
            }
            try
            {
                IWorldTransposeClient transpose = ServiceLocator.GetService<IWorldTransposeClient>();
                Camera far = Camera.allCameras.FirstOrDefault(c => c.transform.root.name.Contains("Distant"));
                if (transpose == null || far == null || ClientOrigin.LocalVisualSpace == null)
                    return;
                if (_root == null && !Build(far))
                    return;
                _root.SetActive(true);
                Vector3 position = transpose.Position((ISpace)BlackHole.Space, ClientOrigin.LocalVisualSpace, Vector3.zero);
                _root.transform.position = position * WorldScale;
                _root.transform.localScale = Vector3.one * (BlackHole.Horizon * WorldScale);

                // Rings face the viewer; the disk keeps its own plane and turns
                Vector3 toCamera = far.transform.position - _root.transform.position;
                if (toCamera.sqrMagnitude > 1e-6f)
                    _billboards.rotation = Quaternion.LookRotation(-toCamera.normalized, far.transform.up);
                _diskTransform.localRotation = DiskTilt;
                _diskMaterial.SetTextureOffset("_BaseMap", new Vector2(Time.time * 0.004f, 0f));
                if (Time.unscaledTime >= _nextDoppler)
                {
                    _nextDoppler = Time.unscaledTime + 0.1f;
                    Doppler(toCamera.normalized);
                }
                if (!_loggedCamera)
                {
                    _loggedCamera = true;
                    AdvancedMod.Log.Msg($"black hole visuals: far camera {far.name} clip {far.nearClipPlane}..{far.farClipPlane}, layer {_root.layer}, at {position.magnitude / 1000f:0} km");
                }
            }
            catch (System.Exception e)
            {
                ErrorLog.Report("black hole visuals", e);
            }
        }

        // Approaching material is blueshifted and beamed: brighter on the side turning toward the viewer
        private static void Doppler(Vector3 toCamera)
        {
            Vector3 view = Quaternion.Inverse(_diskTransform.rotation) * toCamera;
            for (int i = 0; i < _diskColors.Length; i++)
            {
                float approach = Vector3.Dot(_diskTangents[i], view);
                float beaming = Mathf.Clamp(1f + approach * 0.75f, 0.25f, 1.9f);
                Color c = _diskBaseColors[i] * beaming;
                c.a = _diskBaseColors[i].a;
                _diskColors[i] = c;
            }
            _diskMesh.colors = _diskColors;
        }

        private static bool Build(Camera far)
        {
            Material glowTemplate = Resources.FindObjectsOfTypeAll<Material>()
                .FirstOrDefault(m => m != null && m.shader != null && m.shader.name == "Universal Render Pipeline/Particles/Unlit");
            if (glowTemplate == null)
            {
                ErrorLog.Report("black hole visuals", new System.Exception("no particle material to copy"));
                return false;
            }
            int layer = FarLayer(far);
            _root = new GameObject("TFA Black Hole (far)") { layer = layer };
            Object.DontDestroyOnLoad(_root);

            Texture2D diskTexture = DiskTexture();
            Texture2D ringTexture = RingTexture();
            Material shadow = Opaque(glowTemplate, Color.black);
            _diskMaterial = Glow(glowTemplate, diskTexture, Color.white, "TFA_BlackHoleDisk");
            Material lensed = Glow(glowTemplate, diskTexture, new Color(1f, 0.85f, 0.7f, 0.85f), "TFA_BlackHoleLensed");
            Material photon = Glow(glowTemplate, ringTexture, new Color(1f, 0.92f, 0.8f, 1f), "TFA_BlackHolePhoton");
            Material halo = Glow(glowTemplate, ringTexture, new Color(1f, 0.5f, 0.25f, 0.18f), "TFA_BlackHoleHalo");

            // Units: 1 = event horizon radius
            _billboards = Child("Billboards", _root.transform, layer).transform;
            Mesh(Child("Shadow", _billboards, layer), Disc(BlackHole.Shadow / BlackHole.Horizon, 96), shadow);
            Mesh(Child("PhotonRing", _billboards, layer), Annulus(2.58f, 2.75f, 128, 1f, true), photon);
            Mesh(Child("LensedDisk", _billboards, layer), Annulus(2.6f, 4.6f, 128, 2f, true), lensed);
            Mesh(Child("Halo", _billboards, layer), Annulus(2.6f, 60f, 64, 1f, true), halo);

            GameObject disk = Child("Disk", _root.transform, layer);
            _diskTransform = disk.transform;
            _diskMesh = Annulus(DiskInner, DiskOuter, Segments, 3f, false);
            _diskBaseColors = DiskColors(_diskMesh);
            _diskColors = (Color[])_diskBaseColors.Clone();
            _diskTangents = _diskMesh.vertices.Select(v => new Vector3(-v.z, 0f, v.x).normalized).ToArray();
            Mesh(disk, _diskMesh, _diskMaterial);
            return true;
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

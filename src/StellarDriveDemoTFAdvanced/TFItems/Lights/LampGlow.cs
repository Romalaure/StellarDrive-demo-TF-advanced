using System.Collections.Generic;
using HarmonyLib;
using Ships.Visuals;
using UnityEngine;
using UnityEngine.Rendering;

namespace StellarDriveDemoTFAdvanced.TFItems.Lights
{
    /// <summary>
    /// The light a lamp casts on its surroundings. The game's shaders only take the sun into
    /// account, so a Unity Light lights nothing. Instead, each lamp lights the vertices of the
    /// hull and parts around it on the CPU, into overlay copies of those meshes drawn on top with
    /// a blend that brightens what is under them (dst x (1 + light)), plus a little added light so
    /// dark corners still catch it. It is computed once, in the ship's frame, and redone only when
    /// the lamp, the hull or the parts around it change: a lamp costs a few draw calls and nothing
    /// per frame. Blinking, color and brightness only change a shader color.
    /// </summary>
    internal sealed class LampGlow
    {
        public const string OverlayName = "TF_NoPaint_LampGlow";

        // Lights reach at most this far, whatever their range, to keep rebuilds cheap
        private const float MaxReach = 22f;
        private const float MinValue = 0.004f;
        // Share of the light added on top (lets dark surfaces show the light)
        private const float AddedShare = 0.07f;

        private static Material _brighten;
        private static Material _add;
        private static bool _shaderMissing;
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        // One rebuild per frame across all lamps, so a world full of lamps loads smoothly
        private static int _lastRebuildFrame = -1;

        private readonly Transform _lamp;
        private readonly Light _light;
        private readonly bool _omni;
        private readonly List<Overlay> _overlays = new List<Overlay>();
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();

        private ShipLightingRoot _root;
        private bool _rootSearched;
        private int _builtHullVersion = -1;
        private int _builtPartsVersion = -1;
        private Vector3 _builtPosition;
        private Quaternion _builtRotation;
        private float _nextCheck;
        private Vector4 _color;
        private bool _hasColor;

        private struct Overlay
        {
            public Renderer Renderer;
            public float Factor;
        }

        /// <param name="omni">Light all around (rotating beacons), whatever the light's own type and direction.</param>
        public LampGlow(Transform lamp, Light light, bool omni)
        {
            _lamp = lamp;
            _light = light;
            _omni = omni;
        }

        /// <summary>Called every frame by the lamp; rebuilds when something around it changed.</summary>
        public void Tick()
        {
            if (_light == null || Time.unscaledTime < _nextCheck)
                return;
            _nextCheck = Time.unscaledTime + 0.5f + Random.value * 0.25f;
            if (Common.Settings.LampLightStrength.Value <= 0f)
            {
                Clear();
                return;
            }
            if (!_rootSearched)
            {
                _rootSearched = true;
                _root = ShipLightingRoot.For(_lamp);
            }
            if (_root == null || !_root.IsValid)
                return;
            _root.Refresh();

            Transform frame = _root.Transform;
            Vector3 position = frame.InverseTransformPoint(_light.transform.position);
            Quaternion rotation = Quaternion.Inverse(frame.rotation) * _light.transform.rotation;
            bool moved = (position - _builtPosition).sqrMagnitude > 0.0004f || (!_omni && Quaternion.Angle(rotation, _builtRotation) > 1f);
            if (!moved && _builtHullVersion == _root.HullVersion && _builtPartsVersion == _root.PartsVersion && !OverlaysLost())
                return;
            if (_lastRebuildFrame == Time.frameCount)
            {
                _nextCheck = 0f;
                return;
            }
            _lastRebuildFrame = Time.frameCount;
            Rebuild();
            _builtPosition = position;
            _builtRotation = rotation;
            _builtHullVersion = _root.HullVersion;
            _builtPartsVersion = _root.PartsVersion;
        }

        /// <summary>Light color times brightness (linear, may exceed 1); zero hides the overlays.</summary>
        public void SetColor(Color color, float brightness)
        {
            Color linear = color.linear;
            float strength = brightness * Mathf.Max(0f, Common.Settings.LampLightStrength.Value);
            _color = new Vector4(linear.r * strength, linear.g * strength, linear.b * strength, AddedShare);
            _hasColor = true;
            ApplyColor();
        }

        public void Clear()
        {
            foreach (Overlay overlay in _overlays)
            {
                if (overlay.Renderer != null)
                    Object.Destroy(overlay.Renderer.gameObject);
            }
            _overlays.Clear();
            _builtHullVersion = -1;
        }

        private bool OverlaysLost()
        {
            foreach (Overlay overlay in _overlays)
            {
                if (overlay.Renderer == null)
                    return true;
            }
            return false;
        }

        private void ApplyColor()
        {
            if (!_hasColor)
                return;
            bool visible = _color.x + _color.y + _color.z > 0.001f;
            foreach (Overlay overlay in _overlays)
            {
                if (overlay.Renderer == null)
                    continue;
                overlay.Renderer.enabled = visible;
                if (!visible)
                    continue;
                _block.SetVector(ColorId, new Vector4(_color.x * overlay.Factor, _color.y * overlay.Factor, _color.z * overlay.Factor, _color.w));
                overlay.Renderer.SetPropertyBlock(_block);
            }
        }

        private void Rebuild()
        {
            Clear();
            if (!EnsureMaterials())
                return;

            var source = new LightSource(_light, _omni);
            Transform ownPart = _lamp.parent != null ? _lamp.parent : _lamp;
            foreach (MeshRenderer target in _root.Renderers)
            {
                if (target == null || !target.enabled || !target.gameObject.activeInHierarchy)
                    continue;
                if (target.transform.IsChildOf(ownPart))
                    continue;
                Bounds bounds = target.bounds;
                if (bounds.SqrDistance(source.Position) > source.Reach * source.Reach)
                    continue;
                MeshFilter filter = target.GetComponent<MeshFilter>();
                Mesh mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null)
                    continue;
                if (!Lightable(target))
                    continue;

                if (mesh.isReadable)
                    AddVertexLit(target, mesh, source);
                else
                    AddUniform(target, mesh, bounds, source);
            }
            ApplyColor();
        }

        private static int _technicalLayers = -1;

        // Opaque ship surfaces only: the game's part and hull shaders and the mod's own models,
        // never technical renderers (object ids, build previews, thumbnails, UI)
        private static bool Lightable(MeshRenderer target)
        {
            if (_technicalLayers == -1)
            {
                _technicalLayers = 0;
                foreach (string name in new[] { "UI", "WorldUI", "BuildToolRender", "PartThumbnailRender", "Id", "Planet", "DistantObject", "Water" })
                {
                    int layer = LayerMask.NameToLayer(name);
                    if (layer >= 0)
                        _technicalLayers |= 1 << layer;
                }
            }
            if ((_technicalLayers & (1 << target.gameObject.layer)) != 0)
                return false;
            Material material = target.sharedMaterial;
            if (material == null || material.renderQueue >= 2450 || material.shader == null)
                return false;
            string shader = material.shader.name;
            if (shader == "Universal Render Pipeline/Lit")
                return true;
            return shader.StartsWith("Custom/Planet") && shader.IndexOf("Transparent", System.StringComparison.Ordinal) < 0
                && shader.IndexOf("Window", System.StringComparison.Ordinal) < 0 && shader.IndexOf("Screen", System.StringComparison.Ordinal) < 0
                && shader.IndexOf("Display", System.StringComparison.Ordinal) < 0 && shader.IndexOf("Cable", System.StringComparison.Ordinal) < 0
                && shader.IndexOf("Foliage", System.StringComparison.Ordinal) < 0;
        }

        // Lights each vertex and keeps only the lit triangles
        private void AddVertexLit(MeshRenderer target, Mesh mesh, LightSource source)
        {
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            if (vertices.Length == 0 || normals.Length != vertices.Length)
                return;
            Matrix4x4 toWorld = target.transform.localToWorldMatrix;
            var values = new float[vertices.Length];
            bool any = false;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 world = toWorld.MultiplyPoint3x4(vertices[i]);
                Vector3 normal = toWorld.MultiplyVector(normals[i]).normalized;
                values[i] = source.At(world, normal);
                any |= values[i] > MinValue;
            }
            if (!any)
                return;

            var remap = new int[vertices.Length];
            for (int i = 0; i < remap.Length; i++)
                remap[i] = -1;
            var outVertices = new List<Vector3>();
            var outColors = new List<Color>();
            var outTriangles = new List<int>();
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                if (mesh.GetTopology(sub) != MeshTopology.Triangles)
                    continue;
                int[] triangles = mesh.GetTriangles(sub);
                for (int t = 0; t + 2 < triangles.Length; t += 3)
                {
                    int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                    if (values[a] <= MinValue && values[b] <= MinValue && values[c] <= MinValue)
                        continue;
                    outTriangles.Add(Remap(a));
                    outTriangles.Add(Remap(b));
                    outTriangles.Add(Remap(c));
                }
            }
            if (outTriangles.Count == 0)
                return;

            int Remap(int index)
            {
                if (remap[index] < 0)
                {
                    remap[index] = outVertices.Count;
                    outVertices.Add(vertices[index]);
                    float v = values[index];
                    outColors.Add(new Color(v, v, v, 1f));
                }
                return remap[index];
            }

            var overlayMesh = new Mesh { name = OverlayName };
            if (outVertices.Count > 65000)
                overlayMesh.indexFormat = IndexFormat.UInt32;
            overlayMesh.SetVertices(outVertices);
            overlayMesh.SetColors(outColors);
            overlayMesh.SetTriangles(outTriangles, 0);
            overlayMesh.bounds = mesh.bounds;
            // Two materials on one submesh draw it twice: brighten, then add
            AddOverlay(target, overlayMesh, new[] { _brighten, _add }, 1f).AddComponent<OwnedMesh>().Mesh = overlayMesh;
        }

        // Meshes the CPU cannot read get one light level for the whole renderer
        private void AddUniform(MeshRenderer target, Mesh mesh, Bounds bounds, LightSource source)
        {
            Vector3 closest = bounds.ClosestPoint(source.Position);
            Vector3 toLight = source.Position - closest;
            Vector3 normal = toLight.sqrMagnitude > 1e-6f ? toLight.normalized : Vector3.up;
            // Not every face of the part faces the lamp
            float value = source.At(closest, normal) * 0.6f;
            if (value <= MinValue * 4f)
                return;
            var materials = new Material[Mathf.Max(1, mesh.subMeshCount)];
            for (int i = 0; i < materials.Length; i++)
                materials[i] = _brighten;
            AddOverlay(target, mesh, materials, value);
        }

        private GameObject AddOverlay(MeshRenderer target, Mesh mesh, Material[] materials, float factor)
        {
            var overlay = new GameObject(OverlayName) { layer = target.gameObject.layer };
            overlay.transform.SetParent(target.transform, false);
            overlay.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = overlay.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.allowOcclusionWhenDynamic = false;
            // Hidden until the lamp gives its color
            renderer.enabled = false;
            _overlays.Add(new Overlay { Renderer = renderer, Factor = factor });
            return overlay;
        }

        private static bool EnsureMaterials()
        {
            if (_brighten != null && _add != null)
                return true;
            if (_shaderMissing)
                return false;
            // Built into every Unity player; a plain vertex color shader with settable blending
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
            {
                _shaderMissing = true;
                TFMod.Log.Warning("Hidden/Internal-Colored not found: lamps will not light their surroundings");
                return false;
            }
            _brighten = Make(shader, "TF_LampGlowBrighten", BlendMode.DstColor, BlendMode.One);
            _add = Make(shader, "TF_LampGlowAdd", BlendMode.SrcAlpha, BlendMode.One);
            return true;
        }

        private static Material Make(Shader shader, string name, BlendMode source, BlendMode destination)
        {
            var material = new Material(shader) { name = name, renderQueue = 2550 };
            material.SetInt("_SrcBlend", (int)source);
            material.SetInt("_DstBlend", (int)destination);
            material.SetInt("_ZWrite", 0);
            material.SetInt("_ZTest", (int)CompareFunction.LessEqual);
            material.SetInt("_Cull", (int)CullMode.Back);
            material.SetFloat("_ZBias", -1f);
            return material;
        }

        /// <summary>A lamp's light in world space, evaluated per surface point.</summary>
        private readonly struct LightSource
        {
            public readonly Vector3 Position;
            public readonly float Reach;
            private readonly Vector3 _forward;
            private readonly bool _spot;
            private readonly float _cosOuter;
            private readonly float _cosInner;
            private readonly float _intensity;

            public LightSource(Light light, bool omni)
            {
                Position = light.transform.position;
                _forward = light.transform.forward;
                Reach = Mathf.Min(light.range, MaxReach);
                _spot = light.type == LightType.Spot && !omni;
                _cosOuter = Mathf.Cos(light.spotAngle * 0.5f * Mathf.Deg2Rad);
                _cosInner = Mathf.Cos(light.innerSpotAngle * 0.5f * Mathf.Deg2Rad);
                // Catalog intensities run from about 1.6 (small lamps) to 9 (long spotlights)
                _intensity = light.intensity * 0.9f;
            }

            public float At(Vector3 point, Vector3 normal)
            {
                Vector3 toLight = Position - point;
                float distanceSq = toLight.sqrMagnitude;
                if (distanceSq > Reach * Reach)
                    return 0f;
                float distance = Mathf.Sqrt(distanceSq);
                Vector3 direction = distance > 1e-4f ? toLight / distance : normal;
                float facing = Vector3.Dot(normal, direction);
                if (facing <= 0f)
                    return 0f;
                // Smooth end at the reach, inverse square-ish near the lamp
                float ratio = distanceSq / (Reach * Reach);
                float window = Mathf.Clamp01(1f - ratio * ratio);
                float attenuation = window * window / (1f + 0.18f * distanceSq);
                float cone = 1f;
                if (_spot)
                {
                    float cos = Vector3.Dot(-direction, _forward);
                    cone = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_cosOuter, _cosInner, cos));
                    if (cone <= 0f)
                        return 0f;
                }
                // Wrap a little so surfaces at grazing angles still catch some light
                float lambert = Mathf.Clamp01((facing + 0.15f) / 1.15f);
                return lambert * attenuation * cone * _intensity;
            }
        }
    }

    /// <summary>Destroys an overlay's own mesh with it, also when the hull chunk under it is rebuilt.</summary>
    internal sealed class OwnedMesh : MonoBehaviour
    {
        public Mesh Mesh;

        private void OnDestroy()
        {
            if (Mesh != null)
                Destroy(Mesh);
        }
    }

    /// <summary>
    /// The ship a lamp is on, seen from the lighting: its hull meshes and part renderers, and
    /// version numbers that change when they do.
    /// </summary>
    internal sealed class ShipLightingRoot
    {
        private static readonly Dictionary<ShipHullVisuals, ShipLightingRoot> Roots = new Dictionary<ShipHullVisuals, ShipLightingRoot>();
        private static readonly Dictionary<ShipHullVisuals, int> HullVersions = new Dictionary<ShipHullVisuals, int>();

        public Transform Transform { get; }
        public int PartsVersion { get; private set; }
        public int HullVersion => _hull != null && HullVersions.TryGetValue(_hull, out int version) ? version : 0;
        public bool IsValid => Transform != null && _hull != null;
        public IReadOnlyList<MeshRenderer> Renderers => _renderers;

        private readonly ShipHullVisuals _hull;
        private readonly List<MeshRenderer> _renderers = new List<MeshRenderer>();
        private float _nextScan;
        private int _scannedHull = -1;

        private ShipLightingRoot(Transform transform, ShipHullVisuals hull)
        {
            Transform = transform;
            _hull = hull;
        }

        /// <summary>The lighting root of the ship holding this object: the first parent that also holds the hull.</summary>
        public static ShipLightingRoot For(Transform part)
        {
            for (Transform current = part.parent; current != null; current = current.parent)
            {
                ShipHullVisuals hull = current.GetComponentInChildren<ShipHullVisuals>(true);
                if (hull == null)
                    continue;
                if (!Roots.TryGetValue(hull, out ShipLightingRoot root) || !root.IsValid)
                {
                    root = new ShipLightingRoot(current, hull);
                    Roots[hull] = root;
                }
                return root;
            }
            return null;
        }

        public static void HullChanged(ShipHullVisuals hull)
        {
            if (hull == null)
                return;
            HullVersions.TryGetValue(hull, out int version);
            HullVersions[hull] = version + 1;
        }

        /// <summary>Rescans the ship's renderers now and then; the parts version changes when their set does.</summary>
        public void Refresh()
        {
            int hullVersion = HullVersion;
            if (Time.unscaledTime < _nextScan && hullVersion == _scannedHull)
                return;
            _nextScan = Time.unscaledTime + 3f;
            _scannedHull = hullVersion;
            int before = _renderers.Count;
            int signature = 0;
            _renderers.Clear();
            foreach (MeshRenderer renderer in Transform.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (renderer.gameObject.name == LampGlow.OverlayName)
                    continue;
                _renderers.Add(renderer);
                signature = signature * 31 + renderer.GetInstanceID();
            }
            if (_renderers.Count != before || signature != _signature)
                PartsVersion++;
            _signature = signature;
        }

        private int _signature;
    }

    /// <summary>A hull chunk rebuilt its meshes: lamps of that ship light them again.</summary>
    [HarmonyPatch(typeof(ShipHullVisualChunk), nameof(ShipHullVisualChunk.UpdateMesh))]
    internal static class HullChunkLightingPatch
    {
        private static void Postfix(ShipHullVisualChunk __instance)
        {
            ShipLightingRoot.HullChanged(__instance.GetComponentInParent<ShipHullVisuals>());
        }
    }
}

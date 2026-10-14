using System.Collections.Generic;
using System.Linq;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Interface.Settings;
using Ships.Parts.Common;
using StellarDriveDemoTFAdvanced.Common;
using StellarDriveDemoTFAdvanced.Parts;
using UnityEngine;
using UnityEngine.Rendering;

namespace StellarDriveDemoTFAdvanced.Power
{
    /// <summary>
    /// The power machines' models, in one style: faceted armored housings in the game's part look
    /// (paintable), recessed vents with glowing slats, thin white light strips along the edges
    /// and a black grille. Each part is a blank Signal Display (see <see cref="PartFactory.BlankFromSignalDisplay"/>)
    /// with this model and its own sockets set on its faces.
    /// </summary>
    internal static class MachineModels
    {
        public static readonly Color Red = new Color(1f, 0.12f, 0.1f);
        public static readonly Color Cyan = new Color(0.25f, 0.85f, 1f);
        public static readonly Color Violet = new Color(0.75f, 0.3f, 1f);
        public static readonly Color Green = new Color(0.35f, 1f, 0.2f);

        internal sealed class Model
        {
            // Double-sided: funnels, rings and open tubes are seen from every side
            public readonly MeshBuilder Armor = new MeshBuilder { Panel = GameSkin.PlainPanel, DoubleSided = true };
            public readonly MeshBuilder Dark = new MeshBuilder { DoubleSided = true };
            public readonly MeshBuilder Glow = new MeshBuilder { DoubleSided = true };
            public readonly MeshBuilder Light = new MeshBuilder { DoubleSided = true };
            public readonly MeshBuilder Core = new MeshBuilder { DoubleSided = true };
        }

        /// <summary>A housing: a plinth, straight sides, then a top narrowing toward its cap (faceted like a hull).</summary>
        public static void Housing(Model m, float width, float height, float depth, float taper)
        {
            m.Armor.Box(new Vector3(0f, 0.04f, 0f), new Vector3(width, 0.08f, depth));
            float body = height * 0.7f;
            m.Armor.Box(new Vector3(0f, 0.08f + (body - 0.08f) * 0.5f, 0f), new Vector3(width * 0.94f, body - 0.08f, depth * 0.94f));
            // Faceted top: corners in MeshBuilder.Block's bit order (x, y, z)
            float x0 = width * 0.47f, z0 = depth * 0.47f, x1 = x0 * taper, z1 = z0 * taper;
            m.Armor.Block(new[]
            {
                new Vector3(-x0, body, -z0), new Vector3(x0, body, -z0), new Vector3(-x1, height, -z1), new Vector3(x1, height, -z1),
                new Vector3(-x0, body, z0), new Vector3(x0, body, z0), new Vector3(-x1, height, z1), new Vector3(x1, height, z1)
            });
            // White strips along the top facets' front and back edges, and down the front corners
            m.Light.Box(new Vector3(0f, body + 0.005f, z0 + 0.004f), new Vector3(width * 0.9f, 0.012f, 0.012f));
            m.Light.Box(new Vector3(0f, body + 0.005f, -z0 - 0.004f), new Vector3(width * 0.9f, 0.012f, 0.012f));
            foreach (float x in new[] { -x0 - 0.004f, x0 + 0.004f })
                m.Light.Box(new Vector3(x, 0.08f + (body - 0.08f) * 0.5f, z0 + 0.004f), new Vector3(0.012f, body - 0.12f, 0.012f));
        }

        /// <summary>A recessed vent on the front face (z = <paramref name="faceZ"/>) with glowing slats.</summary>
        public static void Vent(Model m, float x, float y, float faceZ, float width, float height, int slats)
        {
            // Outward from the face, whichever side it is on
            float o = faceZ >= 0f ? 1f : -1f;
            m.Dark.Box(new Vector3(x, y, faceZ + o * 0.004f), new Vector3(width, height, 0.012f));
            m.Armor.Box(new Vector3(x, y + height * 0.5f + 0.012f, faceZ + o * 0.01f), new Vector3(width + 0.04f, 0.024f, 0.02f));
            m.Armor.Box(new Vector3(x, y - height * 0.5f - 0.012f, faceZ + o * 0.01f), new Vector3(width + 0.04f, 0.024f, 0.02f));
            for (int i = 0; i < slats; i++)
            {
                float sy = y - height * 0.5f + height * (i + 0.5f) / slats;
                m.Glow.Box(new Vector3(x, sy, faceZ + o * 0.012f), new Vector3(width * 0.84f, height / slats * 0.42f, 0.008f));
            }
        }

        /// <summary>The black central grille, a column of dark bars.</summary>
        public static void Grille(Model m, float y, float faceZ, float width, float height)
        {
            float o = faceZ >= 0f ? 1f : -1f;
            m.Dark.Box(new Vector3(0f, y, faceZ + o * 0.006f), new Vector3(width, height, 0.012f));
            for (int i = 0; i < 6; i++)
                m.Armor.Box(new Vector3(0f, y - height * 0.5f + height * (i + 0.5f) / 6f, faceZ + o * 0.014f), new Vector3(width * 0.8f, 0.012f, 0.008f));
        }

        /// <summary>Renderers for a model under the part's visuals: armor in the game's part look, the rest lit and glowing.</summary>
        public static void Attach(Transform visuals, Model m, string name, Color glow, Color core)
        {
            int layer = visuals.gameObject.layer;
            GameSkin.AddMesh(visuals, layer, "TFA_" + name + "Armor", m.Armor, LitMaterials.Get("TFA_MachineArmor", new Color(0.42f, 0.43f, 0.46f), 0.55f, 0.45f));
            Add(visuals, layer, "TF_NoPaint_" + name + "Dark", m.Dark, LitMaterials.Get("TFA_MachineDark", new Color(0.04f, 0.04f, 0.05f), 0.3f, 0.5f));
            Add(visuals, layer, "TF_NoPaint_" + name + "Glow", m.Glow, LitMaterials.Get("TFA_MachineGlow_" + ColorUtilityName(glow), glow, 0f, 0.8f, glow * 1.4f));
            Add(visuals, layer, "TF_NoPaint_" + name + "Light", m.Light, LitMaterials.Get("TFA_MachineLight", Color.white, 0f, 0.8f, Color.white * 1.2f));
            if (m.Core.HasGeometry)
                Add(visuals, layer, "TF_NoPaint_" + name + "Core", m.Core, LitMaterials.Get("TFA_MachineCore_" + ColorUtilityName(core), core, 0f, 0.9f, core * 2.5f));
        }

        private static string ColorUtilityName(Color c) => $"{(int)(c.r * 255)}_{(int)(c.g * 255)}_{(int)(c.b * 255)}";

        private static void Add(Transform parent, int layer, string name, MeshBuilder builder, Material material)
        {
            if (!builder.HasGeometry)
                return;
            var child = new GameObject(name) { layer = layer };
            child.transform.SetParent(parent, false);
            child.AddComponent<MeshFilter>().sharedMesh = builder.Build(name);
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }

    /// <summary>
    /// The force field: a translucent bubble around the whole ship while the field is up, flaring
    /// when it takes a hit; the generator's ring is lit while up, dark while off or restarting.
    /// </summary>
    internal sealed class ForceFieldVisuals : DefaultShipPartVisuals
    {
        public const string RingName = "TF_NoPaint_ShieldRing";
        public const string CoreName = "TF_NoPaint_ShieldCore";
        /// <summary>Empty child naming the generator model (its level), for the colors.</summary>
        public const string TierPrefix = "TF_ShieldTier_";

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static Material _bubbleMaterial;
        private Mesh _shell, _grid;
        private Vector3[] _shellPoints, _gridPoints;
        private Color32[] _shellColors, _gridColors;
        // Impact ripple: progress 0-1 (-1: none) and where it started, on the unit sphere
        private float _ripple = -1f;
        private Vector3 _impact = Vector3.up;

        private Renderer _ring;
        private Renderer _core;
        private float _coreY;
        private GameObject _bubble;
        private Renderer _bubbleRenderer;
        private MaterialPropertyBlock _block;
        private bool _up;
        private float _charge = 100f;
        private float _flash;
        private float _nextFit;
        private Color _glow = MachineModels.Cyan;

        private void Awake()
        {
            foreach (Transform child in transform)
            {
                if (child.name.StartsWith(TierPrefix) && int.TryParse(child.name.Substring(TierPrefix.Length), out int level))
                {
                    ForceField.Tier tier = System.Array.Find(ForceField.Tiers, t => t.Level == level);
                    if (tier != null)
                        _glow = tier.Glow;
                }
            }
            Transform ring = transform.Find(RingName);
            _ring = ring != null ? ring.GetComponent<Renderer>() : null;
            Transform core = transform.Find(CoreName);
            _core = core != null ? core.GetComponent<Renderer>() : null;
            _coreY = core != null ? core.localPosition.y : 0f;
            _block = new MaterialPropertyBlock();
            Show(false);
        }

        public override void SetNetworkState(IStatefulPartState state)
        {
            if (!(state is ConstantState constant))
                return;
            float value = (float)constant.ConstantValue;
            if (value > 0f && value < _charge - 0.05f)
            {
                _flash = 1f;
                _ripple = 0f;
                _impact = Random.onUnitSphere;
            }
            _charge = value > 0f ? value : 0f;
            Show(value > 0f);
        }

        public override void ReactToNetworkStateChange(StatefulPartStateChangeEvent change)
        {
            SetNetworkState(change.NewState);
        }

        private void Show(bool up)
        {
            _up = up;
            Color color = up ? _glow : new Color(0.1f, 0.12f, 0.14f);
            foreach (Renderer lit in new[] { _ring, _core })
            {
                if (lit == null)
                    continue;
                lit.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, color);
                _block.SetColor(EmissionColorId, up ? color * 3f : Color.black);
                lit.SetPropertyBlock(_block);
            }
        }

        private void LateUpdate()
        {
            // The core turns and bobs while the field is up
            if (_core != null && _up)
            {
                _core.transform.localRotation = Quaternion.Euler(0f, Time.time * 90f, 0f);
                Vector3 p = _core.transform.localPosition;
                p.y = _coreY + Mathf.Sin(Time.time * 1.7f) * 0.025f;
                _core.transform.localPosition = p;
            }
            // Only placed parts (on a ship) get a bubble, not the build menu's previews
            Transform ship = transform.root;
            bool onShip = ship != null && ship.name.StartsWith("ship ");
            if (!_up || !onShip)
            {
                if (_bubble != null)
                    _bubble.SetActive(false);
                return;
            }
            if (_bubble == null && !CreateBubble(ship))
                return;
            _bubble.SetActive(true);
            if (Time.unscaledTime >= _nextFit)
            {
                _nextFit = Time.unscaledTime + 2f;
                Fit(ship);
            }
            // Arsenal missiles and rockets go off on the bubble
            if (ArsenalShield.Installed && uint.TryParse(ship.name.Split(' ')[1], out uint shipId))
            {
                Vector3? hit = ArsenalShield.StopThreats(_bubble.transform, shipId);
                if (hit.HasValue)
                {
                    _flash = 1f;
                    _ripple = 0f;
                    _impact = hit.Value;
                }
            }
            _flash = Mathf.MoveTowards(_flash, 0f, Time.deltaTime * 2.5f);
            Paint();
        }

        /// <summary>
        /// The bubble's look, recomputed each frame in its vertex colors (the shader multiplies them
        /// in): a bright rim where the field is seen edge-on and almost nothing face-on, like a real
        /// energy shell; a hexagonal lattice over it; a scan band rising through it; ripples running
        /// out from each impact; cyan when charged, toward red as it empties, flickering when low.
        /// </summary>
        private void Paint()
        {
            Camera camera = MainCamera.Get();
            if (camera == null || _shell == null)
                return;
            Transform bubble = _bubble.transform;
            Vector3 eye = bubble.InverseTransformPoint(camera.transform.position);
            float t = Time.time;
            float low = 1f - Mathf.Clamp01(_charge / 100f);
            Color tint = Color.Lerp(Color.Lerp(_glow, Color.white, 0.1f), new Color(1f, 0.3f, 0.2f), low * low);
            // A failing field flickers
            float flicker = low > 0.6f && Mathf.PerlinNoise(t * 9f, 0.3f) < (low - 0.5f) ? 0.35f : 1f;
            float pulse = (0.9f + 0.1f * Mathf.Sin(t * 2.2f)) * flicker;
            float scan = Mathf.Repeat(t * 0.45f, 2.6f) - 1.3f;
            if (_ripple >= 0f)
            {
                _ripple += Time.deltaTime * 0.9f;
                if (_ripple > 1f)
                    _ripple = -1f;
            }
            float Glow(Vector3 p, float rim, float fill, float band)
            {
                Vector3 n = p.normalized;
                float facing = Mathf.Abs(Vector3.Dot(n, (eye - p).normalized));
                float fresnel = Mathf.Pow(1f - facing, 3f);
                float scanBand = Mathf.Exp(-Mathf.Pow((n.y - scan) / 0.07f, 2f)) * band;
                float ring = 0f;
                if (_ripple >= 0f)
                {
                    float angle = Mathf.Acos(Mathf.Clamp(Vector3.Dot(n, _impact), -1f, 1f)) / Mathf.PI;
                    ring = Mathf.Exp(-Mathf.Pow((angle - _ripple) / 0.06f, 2f)) * (1f - _ripple) * 1.1f;
                }
                return Mathf.Clamp01((fill + rim * fresnel + scanBand + ring + _flash * 0.35f) * pulse);
            }
            for (int i = 0; i < _shellPoints.Length; i++)
                _shellColors[i] = Color32(tint, Glow(_shellPoints[i], 0.8f, 0.02f, 0.18f));
            for (int i = 0; i < _gridPoints.Length; i++)
                _gridColors[i] = Color32(tint, Glow(_gridPoints[i], 0.75f, 0.06f, 0.6f));
            _shell.colors32 = _shellColors;
            _grid.colors32 = _gridColors;
        }

        private static Color32 Color32(Color tint, float alpha) =>
            new Color32((byte)(tint.r * 255f), (byte)(tint.g * 255f), (byte)(tint.b * 255f), (byte)(alpha * 255f));

        private bool CreateBubble(Transform ship)
        {
            if (_bubbleMaterial == null)
                _bubbleMaterial = TransparentMaterials.Additive("TFA_ForceField", null);
            if (_bubbleMaterial == null)
                return false;
            _bubble = new GameObject("TFA_ForceFieldBubble") { layer = gameObject.layer };
            _bubble.transform.SetParent(ship, false);
            // The shell: a smooth geodesic sphere (radius 0.5, like Unity's sphere, scaled to the ship)
            Geodesic(3, out List<Vector3> points, out List<int> triangles);
            _shellPoints = points.Select(p => p * 0.5f).ToArray();
            _shellColors = new Color32[_shellPoints.Length];
            _shell = new Mesh { name = "TFA_ForceFieldShell" };
            _shell.SetVertices(_shellPoints);
            _shell.SetNormals(points);
            _shell.SetTriangles(triangles, 0);
            _shell.MarkDynamic();
            AddPart(_bubble.transform, "Shell", _shell);
            // The lattice: the hexagons (and twelve pentagons) of the dual of a coarser geodesic sphere, as lines
            Geodesic(2, out List<Vector3> coarse, out List<int> coarseTriangles);
            var centres = new List<Vector3>();
            for (int i = 0; i < coarseTriangles.Count; i += 3)
                centres.Add((coarse[coarseTriangles[i]] + coarse[coarseTriangles[i + 1]] + coarse[coarseTriangles[i + 2]]).normalized * 0.503f);
            var edges = new Dictionary<(int, int), int>();
            var lines = new List<int>();
            for (int tri = 0; tri < coarseTriangles.Count / 3; tri++)
            {
                for (int k = 0; k < 3; k++)
                {
                    int a = coarseTriangles[tri * 3 + k], b = coarseTriangles[tri * 3 + (k + 1) % 3];
                    var key = (Mathf.Min(a, b), Mathf.Max(a, b));
                    if (edges.TryGetValue(key, out int other))
                    {
                        lines.Add(other);
                        lines.Add(tri);
                    }
                    else
                        edges[key] = tri;
                }
            }
            _gridPoints = centres.ToArray();
            _gridColors = new Color32[_gridPoints.Length];
            _grid = new Mesh { name = "TFA_ForceFieldLattice" };
            _grid.SetVertices(_gridPoints);
            _grid.SetIndices(lines.ToArray(), MeshTopology.Lines, 0);
            _grid.MarkDynamic();
            AddPart(_bubble.transform, "Lattice", _grid);
            _bubbleRenderer = _bubble.GetComponentInChildren<MeshRenderer>();
            return true;
        }

        private void AddPart(Transform parent, string name, Mesh mesh)
        {
            var part = new GameObject("TF_NoPaint_ForceField" + name) { layer = gameObject.layer };
            part.transform.SetParent(parent, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _bubbleMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // An icosahedron split <paramref name="depth"/> times, its points on the unit sphere
        private static void Geodesic(int depth, out List<Vector3> points, out List<int> triangles)
        {
            float g = (1f + Mathf.Sqrt(5f)) * 0.5f;
            points = new List<Vector3>
            {
                new Vector3(-1, g, 0), new Vector3(1, g, 0), new Vector3(-1, -g, 0), new Vector3(1, -g, 0),
                new Vector3(0, -1, g), new Vector3(0, 1, g), new Vector3(0, -1, -g), new Vector3(0, 1, -g),
                new Vector3(g, 0, -1), new Vector3(g, 0, 1), new Vector3(-g, 0, -1), new Vector3(-g, 0, 1)
            };
            for (int i = 0; i < points.Count; i++)
                points[i] = points[i].normalized;
            triangles = new List<int> { 0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1 };
            for (int d = 0; d < depth; d++)
            {
                var middles = new Dictionary<(int, int), int>();
                var list = points;
                int Middle(int a, int b)
                {
                    var key = (Mathf.Min(a, b), Mathf.Max(a, b));
                    if (!middles.TryGetValue(key, out int index))
                    {
                        index = list.Count;
                        list.Add(((list[a] + list[b]) * 0.5f).normalized);
                        middles[key] = index;
                    }
                    return index;
                }
                var next = new List<int>();
                for (int i = 0; i < triangles.Count; i += 3)
                {
                    int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                    int ab = Middle(a, b), bc = Middle(b, c), ca = Middle(c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                triangles = next;
            }
        }

        // An ellipsoid around the ship's whole model, in the ship's own axes
        private void Fit(Transform ship)
        {
            bool any = false;
            var local = new Bounds();
            foreach (Renderer r in ship.GetComponentsInChildren<Renderer>())
            {
                // Not the bubble itself (shell and lattice): it would measure itself and grow at each fit
                if (r.transform.IsChildOf(_bubble.transform) || !r.enabled || !r.gameObject.activeInHierarchy)
                    continue;
                Bounds b = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = ship.InverseTransformPoint(corner);
                    if (!any)
                    {
                        local = new Bounds(p, Vector3.zero);
                        any = true;
                    }
                    else
                        local.Encapsulate(p);
                }
            }
            if (!any)
                return;
            _bubble.transform.localPosition = local.center;
            _bubble.transform.localRotation = Quaternion.identity;
            _bubble.transform.localScale = Vector3.Max(local.size * 1.3f, Vector3.one * 2f);
        }

        private void OnDestroy()
        {
            if (_bubble != null)
                Destroy(_bubble);
            if (_shell != null)
                Destroy(_shell);
            if (_grid != null)
                Destroy(_grid);
        }
    }
}

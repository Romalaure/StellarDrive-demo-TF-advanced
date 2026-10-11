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
        private static Texture2D _hexTexture;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static Material _bubbleMaterial;
        private static Mesh _sphere;

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

        private void Awake()
        {
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
                _flash = 1f;
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
            Color color = up ? MachineModels.Cyan : new Color(0.1f, 0.12f, 0.14f);
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
            _flash = Mathf.MoveTowards(_flash, 0f, Time.deltaTime * 2.5f);
            // Hexagons drifting over the bubble, redder as the charge falls, flaring on hits
            float low = 1f - Mathf.Clamp01(_charge / 100f);
            float alpha = 0.16f + 0.12f * low + 0.5f * _flash + 0.05f * Mathf.Sin(Time.time * 2f);
            Color tint = Color.Lerp(new Color(0.3f, 0.85f, 1f), new Color(1f, 0.35f, 0.25f), low * low);
            _bubbleRenderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, new Color(tint.r, tint.g, tint.b, alpha));
            _bubbleRenderer.SetPropertyBlock(_block);
        }

        private bool CreateBubble(Transform ship)
        {
            if (_bubbleMaterial == null)
            {
                _bubbleMaterial = TransparentMaterials.Additive("TFA_ForceField", HexTexture());
                _bubbleMaterial?.SetTextureScale("_BaseMap", new Vector2(14f, 7f));
            }
            if (_bubbleMaterial == null)
                return false;
            if (_sphere == null)
            {
                GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _sphere = primitive.GetComponent<MeshFilter>().sharedMesh;
                Destroy(primitive);
            }
            _bubble = new GameObject("TFA_ForceFieldBubble") { layer = gameObject.layer };
            _bubble.transform.SetParent(ship, false);
            _bubble.AddComponent<MeshFilter>().sharedMesh = _sphere;
            _bubbleRenderer = _bubble.AddComponent<MeshRenderer>();
            _bubbleRenderer.sharedMaterial = _bubbleMaterial;
            _bubbleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _bubbleRenderer.receiveShadows = false;
            return true;
        }

        // Thin hexagon outlines over a faint fill (in alpha), tiling
        private static Texture2D HexTexture()
        {
            if (_hexTexture != null)
                return _hexTexture;
            const int w = 128, h = 222;
            _hexTexture = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "TFA_ForceFieldHex", wrapMode = TextureWrapMode.Repeat };
            var pixels = new Color32[w * h];
            // Hexagon centres on a lattice that repeats every w by h (h = w * sqrt 3)
            var centres = new System.Collections.Generic.List<Vector2>();
            for (int row = -1; row <= 3; row++)
                for (int col = -1; col <= 2; col++)
                    centres.Add(new Vector2(col * w / 2f + (row % 2 != 0 ? w / 4f : 0f), row * h / 2f));
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float best = float.MaxValue, second = float.MaxValue;
                    var at = new Vector2(x, y);
                    foreach (Vector2 c in centres)
                    {
                        float d = (at - c).magnitude;
                        if (d < best) { second = best; best = d; }
                        else if (d < second) second = d;
                    }
                    float line = Mathf.Clamp01(1f - (second - best) / 12f);
                    byte a = (byte)(Mathf.Clamp01(0.1f + line * 0.9f) * 255f);
                    pixels[y * w + x] = new Color32(255, 255, 255, a);
                }
            }
            _hexTexture.SetPixels32(pixels);
            _hexTexture.Apply(true);
            return _hexTexture;
        }

        // An ellipsoid around the ship's whole model, in the ship's own axes
        private void Fit(Transform ship)
        {
            bool any = false;
            var local = new Bounds();
            foreach (Renderer r in ship.GetComponentsInChildren<Renderer>())
            {
                if (r == _bubbleRenderer)
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
        }
    }
}

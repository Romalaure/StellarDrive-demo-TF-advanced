using System.Collections.Generic;
using System.Linq;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Parts.Common.Model;
using Ships.Parts.Common.Model.Reactions;
using Ships.Parts.Common.Utils;
using Ships.Interface.Settings;
using Ships.Parts.Common;
using StellarDriveDemoTFAdvanced.Common;
using StellarDriveDemoTFAdvanced.Parts;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using StellarDriveDemoTFAdvanced.TFItems.Devices;
using UnityEngine;
using UnityEngine.Rendering;
using Loc = StellarDriveDemoTFAdvanced.Common.Loc;
using MainCamera = StellarDriveDemoTFAdvanced.Common.MainCamera;

namespace StellarDriveDemoTFAdvanced.Power
{
    /// <summary>The force field door's part: a low emitter rail laid across the floor of the doorway.</summary>
    internal static class FieldDoorParts
    {
        public const float RailWidth = 1f, RailHeight = 0.16f, RailDepth = 0.36f;

        public static void Configure(PartSettings settings, GameObject prefab)
        {
            float w = PowerCatalog.FieldDoorWattsPerSquareMeter.Value;
            PartFactory.Describe(settings, Loc.Pick("Porte à champ de force", "Force field door", "Puerta de campo de fuerza"),
                Loc.Pick($"Un émetteur à poser au sol, au milieu d'une ouverture de la coque, face à l'ouverture. Regarde-le et appuie sur la touche d'utilisation (T) pour choisir la taille du champ (de 1 × 1 à {FieldDoor.MaxSize} × {FieldDoor.MaxSize} m, par exemple 2 × 2 ou 10 × 20), s'il part des deux côtés de l'émetteur ou seulement à sa gauche ou à sa droite (valeurs négatives : de l'autre côté, ou vers le bas depuis un plafond), s'il est traversable ou solide, et l'allumer. Sa couleur se change au pistolet à peinture. Le champ garde l'oxygène comme une porte fermée ; traversable, on passe à travers, et par-dessus l'émetteur (qui se peint et se démonte même allumé). Il consomme {w:0} W par m² sur ses prises électriques (le réseau doit pouvoir les fournir en entier) ; sans courant, il s'éteint et l'air s'échappe.",
                    $"An emitter set on the floor in the middle of an opening in the hull, facing the opening. Look at it and press the use key (T) to choose the field's size (from 1 × 1 to {FieldDoor.MaxSize} × {FieldDoor.MaxSize} m, for example 2 × 2 or 10 × 20), whether it spreads on both sides of the emitter or only to its left or its right (negative values: to the other side, or down from a ceiling), whether it is walk-through or solid, and switch it on. Its color changes with the paint gun. The field holds the oxygen like a closed door, but you walk through it, and over the emitter (which can be painted and taken down even when on). It draws {w:0} W per m² on its power sockets (the grid must be able to supply all of it); without power it goes out and the air escapes.",
                    $"Un emisor que se coloca en el suelo, en medio de una abertura del casco, de cara a la abertura. Míralo y pulsa la tecla de uso (T) para elegir el tamaño del campo (de 1 × 1 a {FieldDoor.MaxSize} × {FieldDoor.MaxSize} m, por ejemplo 2 × 2 o 10 × 20), si se extiende a ambos lados del emisor o solo a su izquierda o a su derecha (valores negativos: hacia el otro lado, o hacia abajo desde un techo), si es atravesable o sólido, y encenderlo. Su color se cambia con la pistola de pintura. El campo retiene el oxígeno como una puerta cerrada, pero se puede atravesar, y por encima del emisor (que se puede pintar y desmontar incluso encendido). Consume {w:0} W por m² en sus tomas de energía (la red debe poder suministrarlos todos); sin energía se apaga y el aire se escapa."),
                40f, new[] { (Items.ModItems.Titanium, 4), (Items.ModItems.Cryonite, 2), (112u, 6), (102u, 4) });
            settings.internalStateClass = PowerCatalog.MachineStateClass;
            Transform visuals = PartFactory.BlankFromSignalDisplay(prefab, new Vector3(0f, RailHeight * 0.5f, 0f), new Vector3(RailWidth, RailHeight, RailDepth));
            MachineModels.Attach(visuals, Model(), "FieldDoor", MachineModels.Cyan, new Color(0.75f, 0.95f, 1f));
            // Low enough to step over: players walk through the doorway
            DeviceModels.FlattenColliders(prefab, new Vector3(RailWidth, 0.06f, RailDepth));
            FieldDoorCollider.Attach(prefab);
            UsablePart.AddBox(visuals, new Vector3(0f, 0.18f, 0f), new Vector3(RailWidth + 0.1f, 0.4f, RailDepth + 0.15f));
            visuals.gameObject.AddComponent<FieldDoorVisuals>();
            var bounds = prefab.GetComponent<ShipPartBounds>();
            PowerParts.GraftRow(prefab, bounds, 0.07f, -RailDepth * 0.47f, 0.5f,
                (FieldDoor.PowerA, CableType.Power), (FieldDoor.PowerB, CableType.Power));
        }

        // A rail with sloped sides, a lit emitter slit along its top, vent slats and two end caps with status lights
        private static MachineModels.Model Model()
        {
            var m = new MachineModels.Model();
            float hx = RailWidth * 0.5f, hz = RailDepth * 0.5f;
            m.Armor.Box(new Vector3(0f, 0.015f, 0f), new Vector3(RailWidth, 0.03f, RailDepth));
            float x0 = hx - 0.07f;
            m.Armor.Block(new[]
            {
                new Vector3(-x0, 0.03f, -hz + 0.04f), new Vector3(x0, 0.03f, -hz + 0.04f), new Vector3(-x0, 0.12f, -0.06f), new Vector3(x0, 0.12f, -0.06f),
                new Vector3(-x0, 0.03f, hz - 0.04f), new Vector3(x0, 0.03f, hz - 0.04f), new Vector3(-x0, 0.12f, 0.06f), new Vector3(x0, 0.12f, 0.06f)
            });
            // The emitter slit: a dark lens with the light along it
            m.Dark.Box(new Vector3(0f, 0.122f, 0f), new Vector3(x0 * 2f - 0.02f, 0.006f, 0.07f));
            m.Glow.Box(new Vector3(0f, 0.127f, 0f), new Vector3(x0 * 2f - 0.04f, 0.006f, 0.022f));
            for (int i = -3; i <= 3; i++)
            {
                m.Core.Box(new Vector3(i * 0.12f, 0.131f, 0f), new Vector3(0.03f, 0.004f, 0.03f));
                // Vent slats on both slopes
                foreach (float side in new[] { -1f, 1f })
                    m.Dark.Box(new Vector3(i * 0.12f + 0.06f, 0.075f, side * 0.105f), new Vector3(0.05f, 0.012f, 0.05f), Quaternion.Euler(side * -42f, 0f, 0f));
            }
            // End caps, a status light on each
            foreach (float side in new[] { -1f, 1f })
            {
                var cap = new Vector3(side * (hx - 0.035f), 0.08f, 0f);
                m.Armor.Box(cap, new Vector3(0.07f, 0.16f, RailDepth - 0.02f));
                m.Light.Box(cap + new Vector3(side * 0.036f, 0f, 0f), new Vector3(0.004f, 0.1f, RailDepth - 0.08f));
                m.Glow.Box(cap + new Vector3(0f, 0.081f, 0f), new Vector3(0.04f, 0.004f, 0.1f));
                m.Dark.Box(cap + new Vector3(0f, -0.02f, -hz + 0.005f), new Vector3(0.05f, 0.08f, 0.012f));
            }
            // Floor bolts
            foreach (float x in new[] { -hx + 0.11f, hx - 0.11f })
                foreach (float z in new[] { -hz + 0.03f, hz - 0.03f })
                    m.Dark.Cylinder(new Vector3(x, 0.03f, z), new Vector3(x, 0.036f, z), 0.012f, 0.01f, 8);
            return m;
        }
    }

    /// <summary>
    /// The force field door on screen: the field (a lit slab the size of the setting, bright
    /// along its edges, with a lattice, a scan band running through it and a ripple where someone
    /// walks through), rising from the rail when it comes up, in the color painted on the door
    /// (cyan unpainted), denser when solid; the rail's lights by status (the field's color up,
    /// orange blinking without enough power, red without a network, dark off). Its setting is
    /// shown as an orange outline while its window is open.
    /// </summary>
    internal sealed class FieldDoorVisuals : UsablePart
    {
        /// <summary>Last state seen of each door (for its window).</summary>
        public static readonly Dictionary<PartKey, double> Values = new Dictionary<PartKey, double>();

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        public static readonly Color FieldColor = new Color(0.35f, 0.85f, 1f);
        private static readonly Color PreviewColor = new Color(1f, 0.55f, 0.15f);
        private static readonly Color Dark = new Color(0.1f, 0.12f, 0.14f);
        private static Material _fieldMaterial;

        private FieldDoor.Config _config = new FieldDoor.Config(FieldDoor.DefaultWidth, FieldDoor.DefaultHeight, FieldDoor.Status.Off);
        private readonly List<Renderer> _lights = new List<Renderer>();
        private MaterialPropertyBlock _block;
        private GameObject _field;
        private MeshRenderer _fieldRenderer;
        private Mesh _mesh;
        private Vector2[] _points;
        private Color32[] _colors;
        private long _built = -1;
        private int _builtWidth, _builtHeight;
        private float _rise;
        private float _touchAge = -1f;
        private Vector2 _touch;
        private Vector3 _lastEye;
        private bool _hadEye;
        private Color _shownLight = Color.clear;
        private Color _tint;
        private float _nextPaint, _nextTint;

        public override string Label => Loc.Pick("Porte à champ de force", "Force field door", "Puerta de campo de fuerza");

        public override void Use() => FieldDoorMenu.Show(Key);

        /// <summary>The color painted on a door, made bright enough to glow (cyan unpainted).</summary>
        public static Color ColorOf(PartKey key)
        {
            if (!TFItems.Lights.TfPaint.TryGet(key.ShipId, key.PartId, out Color painted))
                return FieldColor;
            float max = painted.maxColorComponent;
            // A black or very dark paint would add nothing: the field keeps its hue, made visible
            return max < 0.02f ? new Color(0.25f, 0.25f, 0.3f) : painted * (Mathf.Max(max, 0.6f) / max);
        }

        protected override void Awake()
        {
            base.Awake();
            _block = new MaterialPropertyBlock();
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.name.EndsWith("FieldDoorGlow") || renderer.name.EndsWith("FieldDoorCore"))
                    _lights.Add(renderer);
            }
            _tint = FieldColor;
        }

        public override void SetContext(ShipPartContext context)
        {
            base.SetContext(context);
            if (Values.TryGetValue(Key, out double value))
                _config = FieldDoor.Decode(value);
        }

        public override void SetNetworkState(IStatefulPartState state)
        {
            if (!(state is ConstantState constant))
                return;
            FieldDoor.Config config = FieldDoor.Decode(constant.ConstantValue);
            if (config.Sealed && !_config.Sealed)
                _rise = 0f;
            _config = config;
            if (HasContext)
                Values[Key] = constant.ConstantValue;
        }

        public override void ReactToNetworkStateChange(StatefulPartStateChangeEvent change) => SetNetworkState(change.NewState);

        private void LateUpdate()
        {
            if (!HasContext)
                return;
            Values[Key] = _config.Value;
            if (Time.unscaledTime >= _nextTint)
            {
                // The paint gun changes it any time
                _nextTint = Time.unscaledTime + 0.5f;
                Color tint = ColorOf(Key);
                if (tint != _tint)
                {
                    _tint = tint;
                    _shownLight = Color.clear;
                    _nextPaint = 0f;
                }
            }
            UpdateLights();
            bool preview = FieldDoorMenu.IsEditing(Key);
            bool shown = _config.Sealed || preview;
            if (!shown)
            {
                if (_field != null)
                    _field.SetActive(false);
                return;
            }
            FieldDoor.Config look = preview ? FieldDoorMenu.Pending : _config;
            if (_field == null && !CreateField())
                return;
            if (look.Setting != _built)
                Build(look);
            _field.SetActive(true);
            _rise = _config.Sealed ? Mathf.MoveTowards(_rise, 1f, Time.deltaTime * 1.6f) : 1f;
            Touch();
            if (Time.unscaledTime < _nextPaint)
                return;
            Camera camera = MainCamera.Get();
            float far = camera != null ? (camera.transform.position - transform.position).magnitude : 0f;
            // (a sheet that was never painted is see-through, never "visible": painted anyway up close)
            if (!_fieldRenderer.isVisible && far > 30f)
                return;
            _nextPaint = Time.unscaledTime + (far > 60f ? 0.25f : far > 25f ? 0.066f : 0f);
            Paint(_config.Sealed && !preview ? _tint : PreviewColor, !_config.Sealed || preview, look.Solid);
        }

        private void UpdateLights()
        {
            Color color;
            bool blink = Mathf.Repeat(Time.time, 1f) < 0.5f;
            switch (_config.Status)
            {
                case FieldDoor.Status.Up: color = _tint; break;
                case FieldDoor.Status.LowPower:
                case FieldDoor.Status.Starting: color = blink ? new Color(1f, 0.55f, 0.05f) : Dark; break;
                case FieldDoor.Status.NoNetwork: color = blink ? new Color(1f, 0.08f, 0.05f) : Dark; break;
                default: color = Dark; break;
            }
            if (color == _shownLight)
                return;
            _shownLight = color;
            bool lit = color != Dark;
            foreach (Renderer light in _lights)
            {
                if (light == null)
                    continue;
                light.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, color);
                _block.SetColor(EmissionColorId, lit ? color * 2.5f : Color.black);
                light.SetPropertyBlock(_block);
            }
        }

        private bool CreateField()
        {
            if (_fieldMaterial == null)
                _fieldMaterial = TransparentMaterials.Additive("TFA_FieldDoor", null);
            if (_fieldMaterial == null)
                return false;
            // (TF_NoPaint: the paint gun's tint goes to its colors, not onto its material)
            _field = new GameObject("TF_NoPaint_FieldDoorField") { layer = gameObject.layer };
            _field.transform.SetParent(transform, false);
            _mesh = new Mesh { name = "TFA_FieldDoorSheet" };
            _mesh.MarkDynamic();
            _field.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _fieldRenderer = _field.AddComponent<MeshRenderer>();
            _fieldRenderer.sharedMaterial = _fieldMaterial;
            _fieldRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _fieldRenderer.receiveShadows = false;
            return true;
        }

        // Two grids of vertices (5 per meter, up to 120 across), one on each face of the field,
        // joined along the four edges: a slab, into the floor (or the ceiling) so no gap is left
        private void Build(FieldDoor.Config config)
        {
            _built = config.Setting;
            int width = config.Width, height = config.Height;
            _builtWidth = width;
            _builtHeight = height;
            float shift = config.Shift, up = config.FlipHeight ? -1f : 1f;
            // Two sheets, the field's thickness apart, with lit edges between them
            float half = config.Thickness * 0.5f;
            int nx = Mathf.Clamp(width * 5, 6, 120) + 1, ny = Mathf.Clamp(height * 5, 6, 120) + 1;
            int face = nx * ny;
            var vertices = new Vector3[face * 2];
            _points = new Vector2[face * 2];
            _colors = new Color32[face * 2];
            for (int j = 0; j < ny; j++)
            {
                for (int i = 0; i < nx; i++)
                {
                    float x = (i / (float)(nx - 1) - 0.5f) * width, y = j / (float)(ny - 1) * height;
                    float along = j == 0 ? -0.05f : y;
                    for (int s = 0; s < 2; s++)
                    {
                        vertices[s * face + j * nx + i] = new Vector3(x + shift, along * up, s == 0 ? -half : half);
                        _points[s * face + j * nx + i] = new Vector2(x, y);
                    }
                }
            }
            var triangles = new List<int>((nx - 1) * (ny - 1) * 12 + (nx + ny) * 12);
            for (int s = 0; s < 2; s++)
            {
                for (int j = 0; j < ny - 1; j++)
                {
                    for (int i = 0; i < nx - 1; i++)
                    {
                        int a = s * face + j * nx + i, b = a + 1, c = a + nx, d = c + 1;
                        triangles.AddRange(new[] { a, c, b, b, c, d });
                    }
                }
            }
            // The edges: bottom and top rows, left and right columns, from one face to the other
            void Edge(int p, int q) => triangles.AddRange(new[] { p, q, p + face, q, q + face, p + face });
            for (int i = 0; i < nx - 1; i++)
            {
                Edge(i, i + 1);
                Edge((ny - 1) * nx + i, (ny - 1) * nx + i + 1);
            }
            for (int j = 0; j < ny - 1; j++)
            {
                Edge(j * nx, (j + 1) * nx);
                Edge(j * nx + nx - 1, (j + 1) * nx + nx - 1);
            }
            _mesh.Clear();
            _mesh.indexFormat = vertices.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            _mesh.vertices = vertices;
            _mesh.SetTriangles(triangles, 0);
            _mesh.colors32 = _colors;
            _mesh.RecalculateBounds();
            _nextPaint = 0f;
        }

        // The local player walking through: a ripple from where they crossed
        private void Touch()
        {
            if (_touchAge >= 0f)
            {
                _touchAge += Time.deltaTime;
                if (_touchAge > 1.4f)
                    _touchAge = -1f;
            }
            Camera camera = MainCamera.Get();
            if (camera == null || !_config.Sealed)
            {
                _hadEye = false;
                return;
            }
            Vector3 eye = transform.InverseTransformPoint(camera.transform.position);
            float along = _config.FlipHeight ? -eye.y : eye.y;
            if (_hadEye && Mathf.Sign(eye.z) != Mathf.Sign(_lastEye.z) && Mathf.Abs(eye.z - _lastEye.z) < 1.5f + _config.Thickness
                && Mathf.Abs(eye.x - _config.Shift) < _config.Width * 0.5f + 0.3f && along > -0.5f && along < _config.Height + 0.5f)
            {
                _touchAge = 0f;
                _touch = new Vector2(eye.x - _config.Shift, Mathf.Clamp(along - 0.5f, 0f, _config.Height));
            }
            _lastEye = eye;
            _hadEye = true;
        }

        private void Paint(Color tint, bool outline, bool solid)
        {
            if (_mesh == null || _points == null)
                return;
            float t = Time.time;
            float halfWidth = _builtWidth * 0.5f, height = _builtHeight;
            float top = _rise * height;
            float scan = Mathf.Repeat(t * 0.9f, height + 2f) - 1f;
            float ring = _touchAge >= 0f ? _touchAge * 3.2f : -10f;
            float ringFade = _touchAge >= 0f ? 1f - _touchAge / 1.4f : 0f;
            float pulse = 0.9f + 0.1f * Mathf.Sin(t * 2.4f);
            // Solid: a denser sheet, its lattice standing out
            float fill = solid ? 0.2f : 0.06f, mesh = solid ? 0.3f : 0.1f;
            var color = new Color32((byte)(Mathf.Clamp01(tint.r) * 255f), (byte)(Mathf.Clamp01(tint.g) * 255f), (byte)(Mathf.Clamp01(tint.b) * 255f), 0);
            for (int i = 0; i < _points.Length; i++)
            {
                Vector2 p = _points[i];
                float edge = Mathf.Min(Mathf.Min(p.x + halfWidth, halfWidth - p.x), Mathf.Min(p.y, height - p.y));
                float rim = Mathf.Exp(-edge / 0.12f);
                float alpha;
                if (outline)
                {
                    alpha = rim * 0.85f * (0.75f + 0.25f * Mathf.Sin(t * 5f));
                }
                else
                {
                    // Hexagon-like lattice: two crossing wave sets, sharpened
                    float lattice = Mathf.Abs(Mathf.Sin(p.x * 4.2f + p.y * 2.4f) * Mathf.Sin(p.x * 4.2f - p.y * 2.4f));
                    lattice = Mathf.Pow(lattice, 6f) * (0.6f + 0.4f * Mathf.Sin(t * 1.3f + p.x * 0.7f + p.y * 0.5f));
                    float band = Mathf.Exp(-Mathf.Pow((p.y - scan) / 0.18f, 2f));
                    float wave = 0f;
                    if (ringFade > 0f)
                    {
                        float d = (p - _touch).magnitude;
                        wave = Mathf.Exp(-Mathf.Pow((d - ring) / 0.22f, 2f)) * ringFade;
                    }
                    alpha = (fill + mesh * lattice + 0.22f * band + 0.75f * rim + 0.7f * wave) * pulse;
                    // Rising from the rail when it comes up, its front edge bright
                    if (p.y > top)
                        alpha = 0f;
                    else if (_rise < 1f)
                        alpha += Mathf.Exp(-(top - p.y) / 0.1f) * 0.8f;
                }
                color.a = (byte)(Mathf.Clamp01(alpha) * 255f);
                _colors[i] = color;
            }
            _mesh.colors32 = _colors;
        }
    }

    /// <summary>The force field door's window: size (negative: the other way), side, solid, on and off.</summary>
    internal static class FieldDoorMenu
    {
        private static bool _open;
        private static PartKey _door;
        private static int _width = FieldDoor.DefaultWidth, _height = FieldDoor.DefaultHeight;
        private static FieldDoor.Side _side;
        private static bool _solid;
        private static int _thick;

        private static readonly (int W, int H)[] Presets = { (1, 2), (2, 2), (2, 3), (3, 3), (4, 4), (6, 4), (8, 6), (10, 20) };

        /// <summary>The setting being chosen (shown as an outline on the door).</summary>
        public static FieldDoor.Config Pending => new FieldDoor.Config(_width, _height, FieldDoor.Status.Off, _side, _solid, _thick);

        public static bool IsEditing(PartKey door) => _open && ModMenu.IsOpen && _door.Equals(door);

        public static void Show(PartKey door)
        {
            _door = door;
            _open = true;
            FieldDoor.Config config = Current(door);
            _width = config.SignedWidth;
            _height = config.SignedHeight;
            _side = config.Side;
            _solid = config.Solid;
            _thick = config.Thick;
            ModMenu.Open(() => _open = false);
        }

        public static FieldDoor.Config Current(PartKey door) =>
            FieldDoorVisuals.Values.TryGetValue(door, out double value) ? FieldDoor.Decode(value) : new FieldDoor.Config(FieldDoor.DefaultWidth, FieldDoor.DefaultHeight, FieldDoor.Status.Off);

        public static void Draw()
        {
            if (!_open || !ModMenu.IsOpen)
                return;
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.7f, 2.5f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float screenWidth = Screen.width / scale, screenHeight = Screen.height / scale;
            FieldDoor.Config config = Current(_door);
            Rect area = DeviceUi.Window(screenWidth, screenHeight, 620f, 640f, Loc.Pick("PORTE À CHAMP DE FORCE", "FORCE FIELD DOOR", "PUERTA DE CAMPO DE FUERZA"));
            GUILayout.BeginArea(area);

            GUILayout.Label(Describe(config), DeviceUi.Text);
            GUILayout.Label(Loc.Pick($"Champ actuel : {config.SignedWidth} × {config.SignedHeight} m, {Thickness(config.Thickness)} d'épaisseur{(config.Solid ? ", solide" : "")}", $"Current field: {config.SignedWidth} × {config.SignedHeight} m, {Thickness(config.Thickness)} thick{(config.Solid ? ", solid" : "")}", $"Campo actual: {config.SignedWidth} × {config.SignedHeight} m, {Thickness(config.Thickness)} de grosor{(config.Solid ? ", sólido" : "")}"), DeviceUi.Muted);
            GUILayout.Space(10f);

            GUILayout.Label(Loc.Pick("TAILLE (le contour orange la montre)", "SIZE (the orange outline shows it)", "TAMAÑO (el contorno naranja lo muestra)"), DeviceUi.Muted);
            _width = SizeRow(Loc.Pick("Largeur", "Width", "Ancho"), _width);
            _height = SizeRow(Loc.Pick("Hauteur", "Height", "Alto"), _height);
            GUILayout.Label(Loc.Pick("En négatif : largeur → de l'autre côté, hauteur → vers le bas (émetteur au plafond).", "Negative: width → to the other side, height → downward (emitter on a ceiling).", "En negativo: ancho → hacia el otro lado, alto → hacia abajo (emisor en el techo)."), DeviceUi.Muted);
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            foreach ((int w, int h) in Presets)
            {
                if (GUILayout.Button($"{w}×{h}", Mathf.Abs(_width) == w && Mathf.Abs(_height) == h ? DeviceUi.Selected : DeviceUi.Button, GUILayout.Height(30f)))
                {
                    _width = _width < 0 ? -w : w;
                    _height = _height < 0 ? -h : h;
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            GUILayout.Label(Loc.Pick("Côté", "Side", "Lado"), DeviceUi.Text, GUILayout.Width(90f));
            foreach ((FieldDoor.Side side, string label) in new[]
            {
                (FieldDoor.Side.Left, Loc.Pick("À gauche", "Left", "Izquierda")),
                (FieldDoor.Side.Center, Loc.Pick("Des deux côtés", "Both sides", "Ambos lados")),
                (FieldDoor.Side.Right, Loc.Pick("À droite", "Right", "Derecha"))
            })
            {
                if (GUILayout.Button(label, side == _side ? DeviceUi.Selected : DeviceUi.Button, GUILayout.Height(30f)))
                    _side = side;
            }
            GUILayout.EndHorizontal();
            GUILayout.Label(Loc.Pick("Gauche et droite vues face à l'émetteur, du côté sans prises.", "Left and right as seen facing the emitter, from the side without plugs.", "Izquierda y derecha vistas de frente al emisor, desde el lado sin tomas."), DeviceUi.Muted);
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            GUILayout.Label(Loc.Pick("Épaisseur", "Thickness", "Grosor"), DeviceUi.Text, GUILayout.Width(90f));
            foreach (int i in Enumerable.Range(0, FieldDoor.Thicknesses.Length).OrderBy(i => FieldDoor.Thicknesses[i]))
            {
                if (GUILayout.Button(Thickness(FieldDoor.Thicknesses[i]), i == _thick ? DeviceUi.Selected : DeviceUi.Button, GUILayout.Height(30f)))
                    _thick = i;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            GUILayout.Label(Loc.Pick("État", "State", "Estado"), DeviceUi.Text, GUILayout.Width(90f));
            if (GUILayout.Button(Loc.Pick("Traversable", "Walk-through", "Atravesable"), !_solid ? DeviceUi.Selected : DeviceUi.Button, GUILayout.Height(30f)))
                _solid = false;
            if (GUILayout.Button(Loc.Pick("Solide", "Solid", "Sólido"), _solid ? DeviceUi.Selected : DeviceUi.Button, GUILayout.Height(30f)))
                _solid = true;
            GUILayout.EndHorizontal();
            GUILayout.Label(Loc.Pick("Solide : personne ne passe, comme un mur. La couleur se change au pistolet à peinture.", "Solid: nobody goes through, like a wall. Change its color with the paint gun.", "Sólido: nadie pasa, como un muro. El color se cambia con la pistola de pintura."), DeviceUi.Muted);
            GUILayout.Space(6f);
            float watts = FieldDoor.Watts(Pending);
            GUILayout.Label(Loc.Pick($"Surface {Mathf.Abs(_width * _height)} m², consommation {watts:0} W", $"Area {Mathf.Abs(_width * _height)} m², draw {watts:0} W", $"Superficie {Mathf.Abs(_width * _height)} m², consumo {watts:0} W"), DeviceUi.Text);
            GUILayout.FlexibleSpace();

            bool changed = Pending.Setting != config.Setting;
            GUILayout.BeginHorizontal();
            if (changed && GUILayout.Button(Loc.Pick("Appliquer", "Apply", "Aplicar"), DeviceUi.Button, GUILayout.Height(36f)))
                FieldDoor.Request(_door, Pending, config.On);
            if (GUILayout.Button(config.On ? Loc.Pick("Couper le champ", "Switch off", "Apagar el campo") : Loc.Pick("Activer le champ", "Switch on", "Activar el campo"), DeviceUi.Button, GUILayout.Height(36f)))
                FieldDoor.Request(_door, Pending, !config.On);
            if (GUILayout.Button(Loc.Pick("Fermer (Échap)", "Close (Esc)", "Cerrar (Esc)"), DeviceUi.Button, GUILayout.Height(36f)))
                ModMenu.Close();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            GUI.matrix = previous;
        }

        private static string Thickness(float meters) => meters < 1f ? Mathf.RoundToInt(meters * 100f) + " cm" : meters.ToString("0") + " m";

        // From -MaxSize to MaxSize, never 0 (it jumps over it)
        private static int SizeRow(string label, int value)
        {
            int before = value;
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, DeviceUi.Text, GUILayout.Width(90f));
            if (GUILayout.Button("−", DeviceUi.Button, GUILayout.Width(36f), GUILayout.Height(30f)))
                value--;
            float slid = GUILayout.HorizontalSlider(value, -FieldDoor.MaxSize, FieldDoor.MaxSize, GUILayout.Height(22f), GUILayout.ExpandWidth(true));
            if (Mathf.RoundToInt(slid) != value)
                value = Mathf.RoundToInt(slid);
            if (GUILayout.Button("+", DeviceUi.Button, GUILayout.Width(36f), GUILayout.Height(30f)))
                value++;
            GUILayout.Label(value + " m", DeviceUi.Text, GUILayout.Width(56f));
            GUILayout.EndHorizontal();
            if (value == 0)
                value = before > 0 ? -1 : 1;
            return Mathf.Clamp(value, -FieldDoor.MaxSize, FieldDoor.MaxSize);
        }

        public static string Describe(FieldDoor.Config config)
        {
            switch (config.Status)
            {
                case FieldDoor.Status.Up: return config.Solid ? Loc.Pick("Champ solide : l'air est retenu, personne ne passe.", "Solid field: the air is held in, nobody gets through.", "Campo sólido: el aire queda retenido, nadie pasa.") : Loc.Pick("Champ actif : l'air est retenu.", "Field up: the air is held in.", "Campo activo: el aire queda retenido.");
                case FieldDoor.Status.Starting: return Loc.Pick("Démarrage…", "Starting…", "Arrancando…");
                case FieldDoor.Status.NoNetwork: return Loc.Pick("Pas de réseau électrique sur ses prises.", "No power grid on its sockets.", "Sin red eléctrica en sus tomas.");
                case FieldDoor.Status.LowPower: return Loc.Pick("Pas assez de courant (il réessaie toutes les 5 s).", "Not enough power (it retries every 5 s).", "Energía insuficiente (reintenta cada 5 s).");
                default: return Loc.Pick("Éteint : l'ouverture laisse passer l'air.", "Off: the opening lets the air through.", "Apagado: la abertura deja pasar el aire.");
            }
        }
    }

    /// <summary>
    /// The door's collision, handed to the game as the part's dynamic collider: the rail, flush
    /// with the floor (walked over, always there for the tools), and the field itself while it is
    /// up and solid.
    /// </summary>
    internal sealed class FieldDoorCollider : MonoBehaviour, IShipPartDynamicCollider
    {
        private const string RailName = "Rail", FieldName = "Field";

        public GameObject GameObject => gameObject;

        public static void Attach(GameObject prefab)
        {
            int ship = LayerMask.NameToLayer("Ship");
            var root = new GameObject("TFA_FieldDoorCollider") { layer = ship };
            root.transform.SetParent(prefab.transform, false);
            var rail = new GameObject(RailName) { layer = ship, tag = "ShipFloatingPart" };
            rail.transform.SetParent(root.transform, false);
            // Flush with the floor: walked over without a bump, and still what the paint gun, the
            // recycler and the build tool aim at, field up or not
            rail.transform.localPosition = new Vector3(0f, 0.015f, 0f);
            rail.AddComponent<BoxCollider>().size = new Vector3(FieldDoorParts.RailWidth, 0.03f, FieldDoorParts.RailDepth);
            var field = new GameObject(FieldName) { layer = ship, tag = "ShipFloatingPart" };
            field.transform.SetParent(root.transform, false);
            field.AddComponent<BoxCollider>().enabled = false;
            root.AddComponent<FieldDoorCollider>();
        }

        public void SetNetworkState(IStatefulPartState state)
        {
            FieldDoor.Config config = FieldDoor.Decode(state);
            foreach (BoxCollider box in GetComponentsInChildren<BoxCollider>(true))
            {
                if (box.name == FieldName)
                {
                    box.enabled = config.Sealed && config.Solid;
                    float up = config.FlipHeight ? -1f : 1f;
                    box.transform.localPosition = new Vector3(config.Shift, config.Height * 0.5f * up, 0f);
                    box.size = new Vector3(config.Width, config.Height, config.Thickness);
                }
            }
        }

        public void ReactToNetworkStateChange(StatefulPartStateChangeEvent change) => SetNetworkState(change.NewState);
    }
}

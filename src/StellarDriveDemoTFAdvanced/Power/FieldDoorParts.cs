using System.Collections.Generic;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Parts.Common.Model;
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
                Loc.Pick($"Un émetteur à poser au sol, au milieu d'une ouverture de la coque, face à l'ouverture. Regarde-le et appuie sur la touche d'utilisation (T) pour choisir la taille du champ (de 1 × 1 à {FieldDoor.MaxSize} × {FieldDoor.MaxSize} m, par exemple 2 × 2 ou 10 × 20) et l'allumer. Le champ garde l'oxygène comme une porte fermée, mais on passe à travers. Il consomme {w:0} W par m² sur ses prises électriques (le réseau doit pouvoir les fournir en entier) ; sans courant, il s'éteint et l'air s'échappe.",
                    $"An emitter set on the floor in the middle of an opening in the hull, facing the opening. Look at it and press the use key (T) to choose the field's size (from 1 × 1 to {FieldDoor.MaxSize} × {FieldDoor.MaxSize} m, for example 2 × 2 or 10 × 20) and switch it on. The field holds the oxygen like a closed door, but you walk through it. It draws {w:0} W per m² on its power sockets (the grid must be able to supply all of it); without power it goes out and the air escapes.",
                    $"Un emisor que se coloca en el suelo, en medio de una abertura del casco, de cara a la abertura. Míralo y pulsa la tecla de uso (T) para elegir el tamaño del campo (de 1 × 1 a {FieldDoor.MaxSize} × {FieldDoor.MaxSize} m, por ejemplo 2 × 2 o 10 × 20) y encenderlo. El campo retiene el oxígeno como una puerta cerrada, pero se puede atravesar. Consume {w:0} W por m² en sus tomas de energía (la red debe poder suministrarlos todos); sin energía se apaga y el aire se escapa."),
                40f, new[] { (Items.ModItems.Titanium, 4), (Items.ModItems.Cryonite, 2), (112u, 6), (102u, 4) });
            settings.internalStateClass = PowerCatalog.MachineStateClass;
            Transform visuals = PartFactory.BlankFromSignalDisplay(prefab, new Vector3(0f, RailHeight * 0.5f, 0f), new Vector3(RailWidth, RailHeight, RailDepth));
            MachineModels.Attach(visuals, Model(), "FieldDoor", MachineModels.Cyan, new Color(0.75f, 0.95f, 1f));
            // Low enough to step over: players walk through the doorway
            DeviceModels.FlattenColliders(prefab, new Vector3(RailWidth, 0.06f, RailDepth));
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
    /// The force field door on screen: the field (a lit sheet the size of the setting, bright
    /// along its edges, with a lattice, a scan band rising through it and a ripple where someone
    /// walks through), rising from the rail when it comes up; the rail's lights by status (cyan
    /// up, orange blinking without enough power, red without a network, dark off). Its size is
    /// shown as an orange outline while its window is open.
    /// </summary>
    internal sealed class FieldDoorVisuals : UsablePart
    {
        /// <summary>Last state seen of each door (for its window).</summary>
        public static readonly Dictionary<PartKey, double> Values = new Dictionary<PartKey, double>();

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly Color FieldColor = new Color(0.35f, 0.85f, 1f);
        private static readonly Color PreviewColor = new Color(1f, 0.55f, 0.15f);
        private static Material _fieldMaterial;

        private FieldDoor.Config _config = new FieldDoor.Config(FieldDoor.DefaultWidth, FieldDoor.DefaultHeight, FieldDoor.Status.Off);
        private readonly List<Renderer> _lights = new List<Renderer>();
        private MaterialPropertyBlock _block;
        private GameObject _field;
        private MeshRenderer _fieldRenderer;
        private Mesh _mesh;
        private Vector2[] _points;
        private Color32[] _colors;
        private int _builtWidth, _builtHeight;
        private float _rise;
        private float _touchAge = -1f;
        private Vector2 _touch;
        private Vector3 _lastEye;
        private bool _hadEye;
        private Color _shownLight = Color.clear;
        private float _nextPaint;

        public override string Label => Loc.Pick("Porte à champ de force", "Force field door", "Puerta de campo de fuerza");

        public override void Use() => FieldDoorMenu.Show(Key);

        protected override void Awake()
        {
            base.Awake();
            _block = new MaterialPropertyBlock();
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.name.EndsWith("FieldDoorGlow") || renderer.name.EndsWith("FieldDoorCore"))
                    _lights.Add(renderer);
            }
        }

        public override void SetContext(ShipPartContext context)
        {
            base.SetContext(context);
            Values.TryGetValue(Key, out double value);
            if (Values.ContainsKey(Key))
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
            UpdateLights();
            bool preview = FieldDoorMenu.IsEditing(Key);
            bool shown = _config.Sealed || preview;
            if (!shown)
            {
                if (_field != null)
                    _field.SetActive(false);
                return;
            }
            int width = preview ? FieldDoorMenu.PendingWidth : _config.Width, height = preview ? FieldDoorMenu.PendingHeight : _config.Height;
            if (_field == null && !CreateField())
                return;
            if (width != _builtWidth || height != _builtHeight)
                Build(width, height);
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
            Paint(_config.Sealed ? FieldColor : PreviewColor, !_config.Sealed);
        }

        private void UpdateLights()
        {
            Color color;
            switch (_config.Status)
            {
                case FieldDoor.Status.Up: color = FieldColor; break;
                case FieldDoor.Status.LowPower:
                case FieldDoor.Status.Starting: color = Mathf.Repeat(Time.time, 1f) < 0.5f ? new Color(1f, 0.55f, 0.05f) : new Color(0.1f, 0.12f, 0.14f); break;
                case FieldDoor.Status.NoNetwork: color = Mathf.Repeat(Time.time, 1f) < 0.5f ? new Color(1f, 0.08f, 0.05f) : new Color(0.1f, 0.12f, 0.14f); break;
                default: color = new Color(0.1f, 0.12f, 0.14f); break;
            }
            if (color == _shownLight)
                return;
            _shownLight = color;
            bool lit = color.maxColorComponent > 0.5f;
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
            _field = new GameObject("TFA_FieldDoorField") { layer = gameObject.layer };
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

        // A grid of vertices over the sheet (5 per meter, up to 120 across)
        private void Build(int width, int height)
        {
            _builtWidth = width;
            _builtHeight = height;
            int nx = Mathf.Clamp(width * 5, 6, 120) + 1, ny = Mathf.Clamp(height * 5, 6, 120) + 1;
            var vertices = new Vector3[nx * ny];
            _points = new Vector2[nx * ny];
            _colors = new Color32[nx * ny];
            for (int j = 0; j < ny; j++)
            {
                for (int i = 0; i < nx; i++)
                {
                    float x = (i / (float)(nx - 1) - 0.5f) * width, y = j / (float)(ny - 1) * height;
                    // The sheet starts on the rail's emitter slit
                    vertices[j * nx + i] = new Vector3(x, Mathf.Max(y, 0.13f), 0f);
                    _points[j * nx + i] = new Vector2(x, y);
                }
            }
            var triangles = new int[(nx - 1) * (ny - 1) * 6];
            int t = 0;
            for (int j = 0; j < ny - 1; j++)
            {
                for (int i = 0; i < nx - 1; i++)
                {
                    int a = j * nx + i, b = a + 1, c = a + nx, d = c + 1;
                    triangles[t++] = a; triangles[t++] = c; triangles[t++] = b;
                    triangles[t++] = b; triangles[t++] = c; triangles[t++] = d;
                }
            }
            _mesh.Clear();
            _mesh.indexFormat = vertices.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            _mesh.vertices = vertices;
            _mesh.triangles = triangles;
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
            if (_hadEye && Mathf.Sign(eye.z) != Mathf.Sign(_lastEye.z) && Mathf.Abs(eye.z - _lastEye.z) < 1.5f
                && Mathf.Abs(eye.x) < _config.Width * 0.5f + 0.3f && eye.y > -0.5f && eye.y < _config.Height + 0.5f)
            {
                _touchAge = 0f;
                _touch = new Vector2(eye.x, Mathf.Clamp(eye.y - 0.5f, 0f, _config.Height));
            }
            _lastEye = eye;
            _hadEye = true;
        }

        private void Paint(Color tint, bool outline)
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
            var color = new Color32((byte)(tint.r * 255f), (byte)(tint.g * 255f), (byte)(tint.b * 255f), 0);
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
                    alpha = (0.06f + 0.1f * lattice + 0.22f * band + 0.75f * rim + 0.7f * wave) * pulse;
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

    /// <summary>The force field door's window: size, presets, power, on and off.</summary>
    internal static class FieldDoorMenu
    {
        private static bool _open;
        private static PartKey _door;
        private static int _width = FieldDoor.DefaultWidth, _height = FieldDoor.DefaultHeight;

        private static readonly (int W, int H)[] Presets = { (1, 2), (2, 2), (2, 3), (3, 3), (4, 4), (6, 4), (8, 6), (10, 20) };

        public static int PendingWidth => _width;
        public static int PendingHeight => _height;

        public static bool IsEditing(PartKey door) => _open && ModMenu.IsOpen && _door.Equals(door);

        public static void Show(PartKey door)
        {
            _door = door;
            _open = true;
            FieldDoor.Config config = Current();
            _width = config.Width;
            _height = config.Height;
            ModMenu.Open(() => _open = false);
        }

        private static FieldDoor.Config Current() =>
            FieldDoorVisuals.Values.TryGetValue(_door, out double value) ? FieldDoor.Decode(value) : new FieldDoor.Config(FieldDoor.DefaultWidth, FieldDoor.DefaultHeight, FieldDoor.Status.Off);

        public static void Draw()
        {
            if (!_open || !ModMenu.IsOpen)
                return;
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.7f, 2.5f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float screenWidth = Screen.width / scale, screenHeight = Screen.height / scale;
            FieldDoor.Config config = Current();
            Rect area = DeviceUi.Window(screenWidth, screenHeight, 560f, 430f, Loc.Pick("PORTE À CHAMP DE FORCE", "FORCE FIELD DOOR", "PUERTA DE CAMPO DE FUERZA"));
            GUILayout.BeginArea(area);

            GUILayout.Label(Describe(config), DeviceUi.Text);
            GUILayout.Label(Loc.Pick($"Champ actuel : {config.Width} × {config.Height} m", $"Current field: {config.Width} × {config.Height} m", $"Campo actual: {config.Width} × {config.Height} m"), DeviceUi.Muted);
            GUILayout.Space(10f);

            GUILayout.Label(Loc.Pick("TAILLE (le contour orange la montre)", "SIZE (the orange outline shows it)", "TAMAÑO (el contorno naranja lo muestra)"), DeviceUi.Muted);
            _width = SizeRow(Loc.Pick("Largeur", "Width", "Ancho"), _width);
            _height = SizeRow(Loc.Pick("Hauteur", "Height", "Alto"), _height);
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            foreach ((int w, int h) in Presets)
            {
                if (GUILayout.Button($"{w}×{h}", w == _width && h == _height ? DeviceUi.Selected : DeviceUi.Button, GUILayout.Height(30f)))
                {
                    _width = w;
                    _height = h;
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);
            float watts = FieldDoor.Watts(new FieldDoor.Config(_width, _height, FieldDoor.Status.Up));
            GUILayout.Label(Loc.Pick($"Surface {_width * _height} m², consommation {watts:0} W", $"Area {_width * _height} m², draw {watts:0} W", $"Superficie {_width * _height} m², consumo {watts:0} W"), DeviceUi.Text);
            GUILayout.Label(Loc.Pick("Pose l'émetteur au sol, au milieu de l'ouverture : le champ part de lui, centré, vers le haut. Prends la taille de l'ouverture (il déborde un peu sur la coque autour).",
                "Set the emitter on the floor in the middle of the opening: the field rises from it, centered. Take the opening's size (it overlaps the hull around a little).",
                "Coloca el emisor en el suelo, en medio de la abertura: el campo sube desde él, centrado. Toma el tamaño de la abertura (se solapa un poco con el casco alrededor)."), DeviceUi.Muted);
            GUILayout.FlexibleSpace();

            bool resized = _width != config.Width || _height != config.Height;
            GUILayout.BeginHorizontal();
            if (resized && GUILayout.Button(Loc.Pick("Appliquer la taille", "Apply size", "Aplicar tamaño"), DeviceUi.Button, GUILayout.Height(36f)))
                FieldDoor.Request(_door, _width, _height, config.On);
            if (GUILayout.Button(config.On ? Loc.Pick("Couper le champ", "Switch off", "Apagar el campo") : Loc.Pick("Activer le champ", "Switch on", "Activar el campo"), DeviceUi.Button, GUILayout.Height(36f)))
                FieldDoor.Request(_door, _width, _height, !config.On);
            if (GUILayout.Button(Loc.Pick("Fermer (Échap)", "Close (Esc)", "Cerrar (Esc)"), DeviceUi.Button, GUILayout.Height(36f)))
                ModMenu.Close();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            GUI.matrix = previous;
        }

        private static int SizeRow(string label, int value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, DeviceUi.Text, GUILayout.Width(90f));
            if (GUILayout.Button("−", DeviceUi.Button, GUILayout.Width(36f), GUILayout.Height(30f)))
                value--;
            float slid = GUILayout.HorizontalSlider(value, 1f, FieldDoor.MaxSize, GUILayout.Height(22f), GUILayout.ExpandWidth(true));
            value = Mathf.RoundToInt(slid);
            if (GUILayout.Button("+", DeviceUi.Button, GUILayout.Width(36f), GUILayout.Height(30f)))
                value++;
            GUILayout.Label(value + " m", DeviceUi.Text, GUILayout.Width(50f));
            GUILayout.EndHorizontal();
            return Mathf.Clamp(value, 1, FieldDoor.MaxSize);
        }

        private static string Describe(FieldDoor.Config config)
        {
            switch (config.Status)
            {
                case FieldDoor.Status.Up: return Loc.Pick("Champ actif : l'air est retenu.", "Field up: the air is held in.", "Campo activo: el aire queda retenido.");
                case FieldDoor.Status.Starting: return Loc.Pick("Démarrage…", "Starting…", "Arrancando…");
                case FieldDoor.Status.NoNetwork: return Loc.Pick("Pas de réseau électrique sur ses prises.", "No power grid on its sockets.", "Sin red eléctrica en sus tomas.");
                case FieldDoor.Status.LowPower: return Loc.Pick("Pas assez de courant (il réessaie toutes les 5 s).", "Not enough power (it retries every 5 s).", "Energía insuficiente (reintenta cada 5 s).");
                default: return Loc.Pick("Éteint : l'ouverture laisse passer l'air.", "Off: the opening lets the air through.", "Apagado: la abertura deja pasar el aire.");
            }
        }
    }
}

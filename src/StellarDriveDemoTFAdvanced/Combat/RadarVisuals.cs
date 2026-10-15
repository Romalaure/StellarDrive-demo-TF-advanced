using System.Collections.Generic;
using System.Text;
using Ships;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Parts.Common;
using Ships.Parts.Common.Model;
using Ships.Parts.Common.Utils;
using StellarDriveDemoTFAdvanced.Common;
using StellarDriveDemoTFAdvanced.ShipComputer;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace StellarDriveDemoTFAdvanced.Combat
{
    /// <summary>
    /// The radar's screen: a round scope (range rings on a log scale, a turning sweep, the
    /// contacts as blips that light up as the sweep passes them) and the list of the nearest
    /// contacts beside it. Drawn only while a player is near (and the antenna only turns while
    /// powered), so a ship full of radars costs nothing when nobody looks.
    /// </summary>
    internal sealed class RadarVisuals : DefaultShipPartVisuals, IContextAwarePart
    {
        public const string AntennaName = "TF_NoPaint_RadarAntenna";
        public const string ScreenName = "TFA_RadarScreen_";

        private const float ScopeRadius = 0.19f;
        private static readonly Vector2 ScopeCenter = new Vector2(-0.2f, 0f);
        private const float ViewDistance = 20f;
        private static Material _material;

        private Transform _anchor, _antenna;
        private Radar.Tier _tier;
        private bool _hasContext;
        private uint _shipId;
        private double _value = -1;

        private GameObject _scope;
        private Transform _sweep;
        private Mesh _blips;
        private TextMeshPro _text;
        private float _nextRefresh;
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Color> _colors = new List<Color>();
        private readonly List<int> _triangles = new List<int>();
        private readonly List<(Vector2 At, Color Color, float Size)> _marks = new List<(Vector2, Color, float)>();
        private readonly StringBuilder _builder = new StringBuilder();

        private void Awake()
        {
            foreach (Transform child in transform)
            {
                if (child.name == AntennaName)
                    _antenna = child;
                else if (child.name.StartsWith(ScreenName) && int.TryParse(child.name.Substring(ScreenName.Length), out int level))
                {
                    _anchor = child;
                    _tier = System.Array.Find(Radar.Tiers, t => t.Level == level);
                }
            }
        }

        public void SetContext(ShipPartContext context)
        {
            _shipId = context.Part.ShipId;
            _hasContext = true;
        }

        public override void SetNetworkState(IStatefulPartState state)
        {
            if (state is ConstantState constant)
                _value = constant.ConstantValue;
        }

        public override void ReactToNetworkStateChange(StatefulPartStateChangeEvent change) => SetNetworkState(change.NewState);

        private bool Powered => _value >= 0;

        private bool _logged;

        private void LateUpdate()
        {
            Camera camera = MainCamera.Get();
            float sqr = camera != null && _anchor != null ? (camera.transform.position - _anchor.position).sqrMagnitude : float.MaxValue;
            if (DevHarness.Active && !_logged && transform.root.name.StartsWith("ship "))
            {
                _logged = true;
                AdvancedMod.Log.Msg($"dev: radar screen {transform.root.name}: context {_hasContext}, tier {_tier?.Level}, anchor {_anchor != null}, camera {camera?.name} at {Mathf.Sqrt(sqr):0.0} m, value {_value}");
            }
            if (!_hasContext || _anchor == null || _tier == null)
                return;
            if (_antenna != null && Powered && sqr < 120f * 120f)
                _antenna.localRotation = Quaternion.Euler(0f, Time.time * (40f + 25f * _tier.Level), 0f);
            float view = DevHarness.Active ? 40f : ViewDistance;
            if (sqr > view * view)
            {
                if (_scope != null && _scope.activeSelf)
                    _scope.SetActive(false);
                return;
            }
            if (_scope == null && !Build())
                return;
            if (!_scope.activeSelf)
            {
                _scope.SetActive(true);
                _nextRefresh = 0f;
            }
            _sweep.gameObject.SetActive(Powered);
            if (Powered)
                _sweep.localRotation = Quaternion.Euler(0f, 0f, -Time.time * 120f);
            if (Time.unscaledTime < _nextRefresh)
                return;
            _nextRefresh = Time.unscaledTime + 0.2f;
            try
            {
                Refresh(sqr < 30f * 30f);
            }
            catch (System.Exception e)
            {
                ErrorLog.Report("radar screen", e);
            }
        }

        private bool Build()
        {
            if (_material == null)
                _material = TransparentMaterials.Additive("TFA_RadarScope", null);
            if (_material == null)
                return false;
            int layer = gameObject.layer;
            _scope = new GameObject("TFA_RadarScope") { layer = layer };
            _scope.transform.SetParent(_anchor, false);
            // Rings (log scale: 10, 100, 1 000, 10 000 km within range) and a cross
            var grid = new MeshData();
            Color dim = _tier.Glow * 0.45f;
            grid.Ring(ScopeCenter, ScopeRadius, 0.004f, _tier.Glow * 0.9f);
            for (float km = 10f; km < _tier.RangeKm * 0.99f; km *= 10f)
                grid.Ring(ScopeCenter, Radius(km * 1000f), 0.002f, dim);
            grid.Line(ScopeCenter + Vector2.left * ScopeRadius, ScopeCenter + Vector2.right * ScopeRadius, 0.0015f, dim);
            grid.Line(ScopeCenter + Vector2.down * ScopeRadius, ScopeCenter + Vector2.up * ScopeRadius, 0.0015f, dim);
            AddChild("Grid", grid.Build("TFA_RadarGrid"), Vector3.zero);
            // The sweep: a wedge fading behind its edge
            var wedge = new MeshData();
            const int steps = 16;
            for (int i = 0; i < steps; i++)
            {
                float a0 = i / (float)steps * 50f * Mathf.Deg2Rad, a1 = (i + 1) / (float)steps * 50f * Mathf.Deg2Rad;
                Color c0 = _tier.Glow * (0.35f * (1f - i / (float)steps)), c1 = _tier.Glow * (0.35f * (1f - (i + 1) / (float)steps));
                wedge.Triangle(Vector2.zero, new Vector2(Mathf.Sin(a0), Mathf.Cos(a0)) * ScopeRadius, new Vector2(Mathf.Sin(a1), Mathf.Cos(a1)) * ScopeRadius, _tier.Glow * 0.35f, c0, c1);
            }
            _sweep = AddChild("Sweep", wedge.Build("TFA_RadarSweep"), new Vector3(ScopeCenter.x, ScopeCenter.y, -0.001f)).transform;
            _blips = new Mesh { name = "TFA_RadarBlips" };
            _blips.MarkDynamic();
            AddChild("Blips", _blips, new Vector3(0f, 0f, -0.002f));
            var text = new GameObject("TFA_RadarText") { layer = layer };
            text.transform.SetParent(_scope.transform, false);
            text.transform.localPosition = new Vector3(0.215f, 0f, -0.003f);
            _text = text.AddComponent<TextMeshPro>();
            _text.font = ComputerStyle.Font;
            _text.fontSize = 0.32f;
            _text.color = Color.Lerp(_tier.Glow, Color.white, 0.4f);
            _text.alignment = TextAlignmentOptions.TopLeft;
            _text.textWrappingMode = TextWrappingModes.NoWrap;
            _text.overflowMode = TextOverflowModes.Truncate;
            _text.rectTransform.sizeDelta = new Vector2(0.4f, 0.4f);
            return true;
        }

        private GameObject AddChild(string name, Mesh mesh, Vector3 position)
        {
            var child = new GameObject(name) { layer = _scope.layer };
            child.transform.SetParent(_scope.transform, false);
            child.transform.localPosition = position;
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return child;
        }

        // Distance on the scope: log scale, 1 km at the center, the range on the rim
        private float Radius(float meters)
        {
            float km = Mathf.Max(0f, meters / 1000f);
            return ScopeRadius * Mathf.Clamp01(Mathf.Log10(1f + km) / Mathf.Log10(1f + _tier.RangeKm));
        }

        private void Refresh(bool alarm)
        {
            _marks.Clear();
            _builder.Length = 0;
            string range = Radar.Distance(_tier.RangeKm * 1000f);
            uint root = Root();
            List<Radar.Contact> contacts = Powered ? Radar.ContactsOf(root) : null;
            if (!Powered)
            {
                _builder.Append("<b>RADAR</b> ").Append(range).Append('\n')
                    .Append("<color=#ff6050>").Append(_value <= -3 ? Loc.Pick("COURANT INSUFFISANT", "NOT ENOUGH POWER") : Loc.Pick("HORS TENSION", "NO POWER")).Append("</color>");
            }
            else
            {
                Quaternion toPart = PartFrame(root);
                int count = contacts?.Count ?? 0;
                _builder.Append("<b>RADAR</b> ").Append(range).Append("  ").Append(count).Append(Loc.Pick(" contact(s)", " contact(s)")).Append('\n');
                if (count == 0)
                    _builder.Append("<color=#7fa0a0>").Append(Loc.Pick("Aucun pirate détecté", "No pirate detected")).Append("</color>");
                float sweep = Mathf.Repeat(Time.time * 120f, 360f);
                for (int i = 0; i < count; i++)
                {
                    Radar.Contact c = contacts[i];
                    Vector3 local = toPart * c.Local;
                    float d = c.Local.magnitude;
                    Vector2 flat = new Vector2(local.x, local.z);
                    Vector2 dir = flat.sqrMagnitude > 1e-6f ? flat.normalized : Vector2.up;
                    // Bright when the sweep has just passed it, fading until the next turn
                    float bearing = Mathf.Repeat(Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg, 360f);
                    float since = Mathf.Repeat(sweep - bearing, 360f) / 360f;
                    Color color = c.Kind == Radar.Kind.Base ? new Color(1f, 0.55f, 0.1f) : c.Kind == Radar.Kind.Raid ? new Color(1f, 0.15f, 0.3f) : new Color(1f, 0.2f, 0.15f);
                    _marks.Add((ScopeCenter + dir * Radius(d), color * (0.35f + 0.65f * (1f - since)), c.Kind == Radar.Kind.Base ? 0.012f : 0.008f));
                    if (i < 7)
                    {
                        string hex = c.Kind == Radar.Kind.Base ? "#ff9020" : "#ff4040";
                        _builder.Append("<color=").Append(hex).Append(">■</color> ").Append(Radar.NameOf(c)).Append("  ").Append(Radar.Distance(d));
                        if (Mathf.Abs(c.Local.y) > d * 0.2f)
                            _builder.Append(c.Local.y > 0f ? Loc.Pick(" (dessus)", " (above)") : Loc.Pick(" (dessous)", " (below)"));
                        if (c.Kind == Radar.Kind.Raid)
                            _builder.Append("  <color=#ffd040>").Append(Loc.Pick("contact ", "contact ")).Append(c.Eta.ToString("0")).Append(" s</color>");
                        _builder.Append('\n');
                    }
                    if (alarm && c.Kind == Radar.Kind.Raid)
                        Radar.Alarm(root, c);
                }
                if (count > 7)
                    _builder.Append("<color=#7fa0a0>+").Append(count - 7).Append("</color>");
            }
            Radar.ForgetAlarms(root, contacts);
            _text.text = _builder.ToString();
            // The ship at the center
            _marks.Add((ScopeCenter, new Color(0.3f, 1f, 0.5f), 0.006f));
            _vertices.Clear();
            _colors.Clear();
            _triangles.Clear();
            foreach ((Vector2 at, Color color, float size) in _marks)
            {
                int start = _vertices.Count;
                _vertices.Add(new Vector3(at.x, at.y + size, 0f));
                _vertices.Add(new Vector3(at.x + size, at.y, 0f));
                _vertices.Add(new Vector3(at.x, at.y - size, 0f));
                _vertices.Add(new Vector3(at.x - size, at.y, 0f));
                for (int k = 0; k < 4; k++)
                    _colors.Add(color);
                _triangles.Add(start); _triangles.Add(start + 1); _triangles.Add(start + 2);
                _triangles.Add(start); _triangles.Add(start + 2); _triangles.Add(start + 3);
            }
            _blips.Clear();
            _blips.SetVertices(_vertices);
            _blips.SetColors(_colors);
            _blips.SetTriangles(_triangles, 0);
            _blips.RecalculateBounds();
        }

        private uint Root()
        {
            var ships = GameServices.ShipsClient;
            return ships != null && ships.TryGetTrackedRootShip(_shipId, out TrackedShipClient root) && root != null ? root.Id : _shipId;
        }

        // From the root ship's frame to this radar's frame (its front at the top of the scope)
        private Quaternion PartFrame(uint root)
        {
            Transform rootVisual = ShipVisuals.IdOf(transform.root) == root ? transform.root : ShipVisuals.Find(root);
            if (rootVisual == null)
                return Quaternion.identity;
            return Quaternion.Inverse(Quaternion.Inverse(rootVisual.rotation) * transform.rotation);
        }

        private void OnDestroy()
        {
            if (_blips != null)
                Destroy(_blips);
        }

        /// <summary>Flat glowing shapes in the screen's plane (vertex colors, additive).</summary>
        private sealed class MeshData
        {
            private readonly List<Vector3> _v = new List<Vector3>();
            private readonly List<Color> _c = new List<Color>();
            private readonly List<int> _t = new List<int>();

            public void Triangle(Vector2 a, Vector2 b, Vector2 c, Color ca, Color cb, Color cc)
            {
                int s = _v.Count;
                _v.Add(a); _v.Add(b); _v.Add(c);
                _c.Add(ca); _c.Add(cb); _c.Add(cc);
                _t.Add(s); _t.Add(s + 1); _t.Add(s + 2);
            }

            public void Line(Vector2 a, Vector2 b, float width, Color color)
            {
                Vector2 side = new Vector2(-(b - a).y, (b - a).x).normalized * width * 0.5f;
                int s = _v.Count;
                _v.Add(a - side); _v.Add(a + side); _v.Add(b + side); _v.Add(b - side);
                for (int i = 0; i < 4; i++)
                    _c.Add(color);
                _t.Add(s); _t.Add(s + 1); _t.Add(s + 2);
                _t.Add(s); _t.Add(s + 2); _t.Add(s + 3);
            }

            public void Ring(Vector2 center, float radius, float width, Color color)
            {
                const int segments = 48;
                for (int i = 0; i < segments; i++)
                {
                    float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                    Line(center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius, center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius, width, color);
                }
            }

            public Mesh Build(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(_v);
                mesh.SetColors(_c);
                mesh.SetTriangles(_t, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}

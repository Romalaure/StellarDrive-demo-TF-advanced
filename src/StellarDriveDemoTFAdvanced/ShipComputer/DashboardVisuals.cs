using System.Globalization;
using System.Linq;
using Ships;
using Ships.Parts.Common;
using Ships.Parts.Common.Model;
using Ships.Parts.Common.Utils;
using Ships.Services;
using StellarDriveDemoTFAdvanced.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StellarDriveDemoTFAdvanced.ShipComputer
{
    /// <summary>
    /// The screen of a ship computer part. Like the game's own displays (Signal Display, Stellar
    /// Core), it is a world-space canvas with TextMeshPro texts, here in the game's UI font and
    /// palette. It shows the ship the part is mounted on (the root of its docking/rotor tree), and
    /// only redraws after a new reading, while the player's camera is near enough.
    /// The screen's size and content are read from the name of an anchor child
    /// ("TFA_Screen layout=all w=960 h=580"), because this component is cloned by Instantiate.
    /// </summary>
    internal sealed class DashboardVisuals : DefaultShipPartVisuals, IContextAwarePart
    {
        public const string AnchorPrefix = "TFA_Screen";
        public const string AllSections = "all";

        private const float HeaderHeight = 44f;
        private const float Margin = 14f;
        private const float Gap = 14f;
        private const float CheckInterval = 0.1f;

        private Transform _anchor;
        private string _layout = AllSections;
        private float _width = 960f;
        private float _height = 580f;

        private bool _built;
        private TextMeshProUGUI _header;
        private TextMeshProUGUI[] _panels;
        private ComputerSection[] _sections;
        private Image[] _backgrounds;
        private Image _divider;
        private readonly TelemetryFormatter _formatter = new TelemetryFormatter();

        private bool _hasContext;
        private uint _shipId;
        private float _nextCheck;
        private int _shownVersion = -1;
        private uint _shownRoot;
        private bool _showingIdle;

        /// <summary>Development renders: draw whatever the distance to the player camera.</summary>
        internal bool IgnoreDistance;

        public static string AnchorName(string layout, float width, float height) =>
            string.Format(CultureInfo.InvariantCulture, "{0} layout={1} w={2} h={3}", AnchorPrefix, layout, width, height);

        private void Awake()
        {
            foreach (Transform child in transform)
            {
                if (!child.name.StartsWith(AnchorPrefix))
                    continue;
                _anchor = child;
                foreach (string token in child.name.Split(' '))
                {
                    string[] pair = token.Split('=');
                    if (pair.Length != 2)
                        continue;
                    if (pair[0] == "layout")
                        _layout = pair[1];
                    else if (pair[0] == "w")
                        float.TryParse(pair[1], NumberStyles.Float, CultureInfo.InvariantCulture, out _width);
                    else if (pair[0] == "h")
                        float.TryParse(pair[1], NumberStyles.Float, CultureInfo.InvariantCulture, out _height);
                }
            }
            TryBuild();
        }

        public void SetContext(ShipPartContext context)
        {
            _shipId = context.Part.ShipId;
            _hasContext = true;
            _shownVersion = -1;
        }

        private void Update()
        {
            if (!_built && !TryBuild())
                return;
            float now = Time.unscaledTime;
            if (now < _nextCheck)
                return;
            _nextCheck = now + CheckInterval;

            // Build menu previews and placement ghosts have no ship
            if (!_hasContext)
            {
                ShowIdle();
                return;
            }
            Camera camera = MainCamera.Get();
            float range = Mathf.Max(2f, Settings.ComputerViewDistance.Value);
            if (!IgnoreDistance && (camera == null || (camera.transform.position - _anchor.position).sqrMagnitude > range * range))
                return;
            ShipsClientTracker ships = GameServices.ShipsClient;
            if (ships == null || !ships.TryGetTrackedRootShip(_shipId, out TrackedShipClient root))
                return;

            TelemetryHub.Reading reading = TelemetryHub.Get(root.Id);
            if (reading.Version == _shownVersion && root.Id == _shownRoot)
                return;
            _shownVersion = reading.Version;
            _shownRoot = root.Id;
            Draw(reading);
        }

        private void Draw(TelemetryHub.Reading reading)
        {
            _showingIdle = false;
            ApplyColors();
            TelemetrySnapshot snap = reading.Snapshot;
            string ship = snap.HasShip ? $"  <color={ComputerStyle.Hex(ComputerStyle.TextDim)}>#{snap.ShipId} · {snap.ShipSource}</color>" : "";
            _header.text = Loc.Pick("<b>ORDINATEUR DE BORD</b>", "<b>SHIP COMPUTER</b>") + ship + AlertBadge(reading);
            for (int i = 0; i < _panels.Length; i++)
            {
                string title = $"<color={ComputerStyle.Hex(ComputerStyle.Line)}><b>{SectionTitle(_sections[i])}</b></color>\n";
                _panels[i].text = title + _formatter.Write(_sections[i], snap, reading.Messages, reading.SampleMs, TelemetryHub.Rate);
            }
        }

        private void ShowIdle()
        {
            if (_showingIdle)
                return;
            _showingIdle = true;
            ApplyColors();
            _header.text = Loc.Pick("<b>ORDINATEUR DE BORD</b>", "<b>SHIP COMPUTER</b>");
            string dim = ComputerStyle.Hex(ComputerStyle.TextDim);
            for (int i = 0; i < _panels.Length; i++)
                _panels[i].text = $"<color={ComputerStyle.Hex(ComputerStyle.Line)}><b>{SectionTitle(_sections[i])}</b></color>\n<color={dim}>{Loc.Pick("en attente du vaisseau", "waiting for the ship")}</color>";
        }

        // Shown in the header, so alerts are seen even on a screen without the DIAGNOSTICS section
        private static string AlertBadge(TelemetryHub.Reading reading)
        {
            int errors = 0, warnings = 0;
            foreach (DiagnosticMessage message in reading.Messages)
            {
                if (message.Severity == Severity.Error)
                    errors++;
                else if (message.Severity == Severity.Warning)
                    warnings++;
            }
            if (errors > 0)
                return $"   <color={ComputerStyle.Hex(ComputerStyle.Error)}>● {errors} {Loc.Pick("erreur(s)", "error(s)")}</color>";
            if (warnings > 0)
                return $"   <color={ComputerStyle.Hex(ComputerStyle.Warning)}>● {warnings} {Loc.Pick("alerte(s)", "warning(s)")}</color>";
            return "";
        }

        private static string SectionTitle(ComputerSection section)
        {
            switch (section)
            {
                case ComputerSection.Flight: return "FLIGHT";
                case ComputerSection.Propulsion: return "PROPULSION";
                case ComputerSection.Power: return "POWER";
                default: return "DIAGNOSTICS";
            }
        }

        private bool TryBuild()
        {
            if (_built)
                return true;
            if (_anchor == null)
                return false;
            ComputerStyle.Refresh();
            TMP_FontAsset font = ComputerStyle.Font;
            if (font == null)
                return false;

            int layer = _anchor.gameObject.layer;
            // A copy of the part carries the canvas its original built (with the idle text): only ours stays
            foreach (Transform old in _anchor.Cast<Transform>().Where(t => t.name == "TFA_Canvas").ToList())
            {
                old.gameObject.SetActive(false);
                Destroy(old.gameObject);
            }
            var canvasObject = new GameObject("TFA_Canvas", typeof(RectTransform)) { layer = layer };
            canvasObject.transform.SetParent(_anchor, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = (RectTransform)canvasObject.transform;
            rect.sizeDelta = new Vector2(_width, _height);
            // Canvas units are millimeters
            rect.localScale = Vector3.one * 0.001f;

            _sections = SectionsFor(_layout);
            _panels = new TextMeshProUGUI[_sections.Length];
            _backgrounds = new Image[_sections.Length];

            _header = NewText(rect, font, layer, 22f, 12f);
            _header.alignment = TextAlignmentOptions.MidlineLeft;
            Place(_header.rectTransform, Margin, Margin * 0.5f, _width - 2f * Margin, HeaderHeight - Margin * 0.5f);
            _divider = NewImage(rect, layer);
            Place(_divider.rectTransform, Margin, HeaderHeight, _width - 2f * Margin, 2f);

            float top = HeaderHeight + Gap * 0.6f;
            bool grid = _sections.Length == 4;
            float cellWidth = grid ? (_width - 2f * Margin - Gap) * 0.5f : _width - 2f * Margin;
            float cellHeight = grid ? (_height - top - Margin - Gap) * 0.5f : _height - top - Margin;
            for (int i = 0; i < _sections.Length; i++)
            {
                float x = Margin + (grid ? (i % 2) * (cellWidth + Gap) : 0f);
                float y = top + (grid ? (i / 2) * (cellHeight + Gap) : 0f);
                _backgrounds[i] = NewImage(rect, layer);
                Place(_backgrounds[i].rectTransform, x, y, cellWidth, cellHeight);
                _panels[i] = NewText(rect, font, layer, grid ? 21f : 28f, grid ? 11f : 13f);
                _panels[i].alignment = TextAlignmentOptions.TopLeft;
                _panels[i].lineSpacing = 0f;
                Place(_panels[i].rectTransform, x + 10f, y + 8f, cellWidth - 20f, cellHeight - 16f);
            }
            _built = true;
            ShowIdle();
            return true;
        }

        private void ApplyColors()
        {
            Color panel = Color.Lerp(ComputerStyle.Background, ComputerStyle.Line, 0.06f);
            panel.a = 0.55f;
            foreach (Image background in _backgrounds)
                background.color = panel;
            _divider.color = ComputerStyle.Line;
            _header.color = ComputerStyle.TextStrong;
            foreach (TextMeshProUGUI text in _panels)
                text.color = ComputerStyle.Text;
        }

        private static ComputerSection[] SectionsFor(string layout)
        {
            switch (layout)
            {
                case "flight": return new[] { ComputerSection.Flight };
                case "propulsion": return new[] { ComputerSection.Propulsion };
                case "power": return new[] { ComputerSection.Power };
                case "diagnostics": return new[] { ComputerSection.Diagnostics };
                default: return new[] { ComputerSection.Flight, ComputerSection.Propulsion, ComputerSection.Power, ComputerSection.Diagnostics };
            }
        }

        // Rect from the canvas' top-left corner, in millimeters
        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static TextMeshProUGUI NewText(Transform parent, TMP_FontAsset font, int layer, float maxSize, float minSize)
        {
            var go = new GameObject("TFA_Text", typeof(RectTransform)) { layer = layer };
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.richText = true;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Truncate;
            // Long lists shrink to fit their panel instead of spilling over
            text.enableAutoSizing = true;
            text.fontSizeMax = maxSize;
            text.fontSizeMin = minSize;
            text.fontSize = maxSize;
            return text;
        }

        private static Image NewImage(Transform parent, int layer)
        {
            var go = new GameObject("TFA_Panel", typeof(RectTransform)) { layer = layer };
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
        }
    }
}

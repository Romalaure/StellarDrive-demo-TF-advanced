using System.Globalization;
using Ships.Parts.Common;
using Ships.Parts.Common.Model;
using Ships.Parts.Common.Utils;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.TFItems.Lights
{
    /// <summary>
    /// Drives a lamp's light and glowing diffuser. Lamps have no plug and are always on. Painting
    /// the lamp sets the light color; unpainted, it keeps the color its Light was built with.
    /// Beacons blink and rotating beacons sweep their beam, both purely client-side. The light
    /// itself is drawn by LampGlow: the game's shaders ignore Unity lights, so the Light component
    /// only carries the lamp's color, range and cone.
    /// Everything is looked up from child objects, since this component is cloned by Instantiate
    /// (its own fields are not copied).
    /// </summary>
    internal sealed class LampVisuals : DefaultShipPartVisuals, IContextAwarePart
    {
        public const string LightName = "TF_LampLight";
        public const string DiffuserName = "TF_NoPaint_Diffuser";
        /// <summary>Prefix of an empty child whose name carries the effects: "TF_LampFx blink=1 spin=240".</summary>
        public const string EffectsName = "TF_LampFx";

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        // Brightness against the surroundings: brighter in the dark, softer in open daylight
        private const float DarkBoost = 1.4f;
        private const float DaylightDim = 0.4f;

        private Light _light;
        private Renderer[] _diffusers;
        private Color _defaultColor;
        private Quaternion _baseRotation;
        private float _blink;
        private float _spin;
        private float _environment = 1f;
        private float _targetEnvironment = 1f;
        private float _nextEnvironmentCheck;
        private MaterialPropertyBlock _block;
        private LampGlow _glow;

        private bool _hasContext;
        private PartKey _key;
        private float _nextRefresh;
        private float _shownLevel = -1f;
        private Color _shownColor;

        public static string EffectsObjectName(float blink, float spin) =>
            string.Format(CultureInfo.InvariantCulture, "{0} blink={1} spin={2}", EffectsName, blink, spin);

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            Transform lightObject = transform.Find(LightName);
            _light = lightObject != null ? lightObject.GetComponent<Light>() : null;
            _defaultColor = _light != null ? _light.color : LampCatalog.DefaultLight;
            _baseRotation = lightObject != null ? lightObject.localRotation : Quaternion.identity;
            Transform diffuser = transform.Find(DiffuserName);
            _diffusers = diffuser != null ? diffuser.GetComponents<Renderer>() : new Renderer[0];
            ReadEffects();
            if (_light != null)
            {
                _light.enabled = false;
                _glow = new LampGlow(transform, _light, _spin != 0f);
            }
            // Build menu previews have no context: show the lamp lit, without casting light
            Apply(1f, _defaultColor, castLight: false);
        }

        private void ReadEffects()
        {
            foreach (Transform child in transform)
            {
                if (!child.name.StartsWith(EffectsName))
                    continue;
                foreach (string part in child.name.Split(' '))
                {
                    string[] pair = part.Split('=');
                    if (pair.Length != 2 || !float.TryParse(pair[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                        continue;
                    if (pair[0] == "blink")
                        _blink = value;
                    else if (pair[0] == "spin")
                        _spin = value;
                }
            }
        }

        public void SetContext(ShipPartContext context)
        {
            _hasContext = true;
            _key = new PartKey(context.Part.ShipId, context.Part.PartId);
            // First update applies the light, even when nothing else changes
            _shownLevel = -1f;
            _nextRefresh = 0f;
        }

        private void Update()
        {
            if (!_hasContext)
                return;

            if (_spin != 0f && _light != null)
            {
                // Sweep around the lamp's axis (out of its mounting surface)
                float angle = Time.time * _spin % 360f;
                _light.transform.localRotation = Quaternion.AngleAxis(angle, Vector3.up) * _baseRotation;
            }

            _glow?.Tick();

            bool animated = _blink > 0f || _spin != 0f;
            if (!animated && Time.unscaledTime < _nextRefresh)
                return;
            _nextRefresh = Time.unscaledTime + 0.2f;

            // Beacons flash a short pulse once per period
            float level = 1f;
            if (_blink > 0f)
                level = Time.time % _blink < _blink * 0.3f ? 1f : 0f;
            // A rotating beacon's sweep reads as a pulse on the walls around it
            if (_spin != 0f)
                level = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.time * _spin * Mathf.Deg2Rad));

            Color color = TfPaint.TryGet(_key.ShipId, _key.PartId, out Color painted) ? painted : _defaultColor;

            bool environmentChanged = UpdateEnvironment();
            if (!environmentChanged && Mathf.Approximately(level, _shownLevel) && color == _shownColor)
                return;
            Apply(level, color, castLight: true);
        }

        // Eases the lamp toward the brightness its surroundings call for
        private void OnDestroy()
        {
            _glow?.Clear();
        }

        private bool UpdateEnvironment()
        {
            if (!Settings.AdaptiveLamps.Value)
            {
                bool changed = !Mathf.Approximately(_environment, 1f);
                _environment = 1f;
                return changed;
            }
            if (Time.unscaledTime >= _nextEnvironmentCheck)
            {
                _nextEnvironmentCheck = Time.unscaledTime + 0.5f + Random.value * 0.2f;
                _targetEnvironment = Mathf.Lerp(DarkBoost, DaylightDim, EnvironmentLight.Daylight(transform.position));
            }
            float next = Mathf.MoveTowards(_environment, _targetEnvironment, 0.25f);
            if (Mathf.Approximately(next, _environment))
                return false;
            _environment = next;
            return true;
        }

        private void Apply(float level, Color color, bool castLight)
        {
            _shownLevel = level;
            _shownColor = color;
            if (_glow != null && castLight)
            {
                // Lamp light matters at night and indoors; in open daylight it barely shows
                float surroundings = Mathf.Lerp(0.15f, 1.2f, Mathf.InverseLerp(DaylightDim, DarkBoost, _environment));
                _glow.SetColor(color, level * surroundings);
            }
            foreach (Renderer renderer in _diffusers)
            {
                renderer.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, Color.Lerp(new Color(0.35f, 0.36f, 0.38f), color, level));
                // The diffuser glows harder in daylight so the lamp still reads as lit (soft: a bright
                // glow bloomed and flashed)
                _block.SetColor(EmissionColorId, color * (1.6f * level * Mathf.Lerp(1.3f, 1f, Mathf.InverseLerp(DaylightDim, DarkBoost, _environment))));
                renderer.SetPropertyBlock(_block);
            }
        }
    }
}

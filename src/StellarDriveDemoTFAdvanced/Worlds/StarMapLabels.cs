using System.Collections.Generic;
using System.Linq;
using Core.Services;
using Core.Values;
using HarmonyLib;
using Planets.Interface.Model;
using Planets.Interface.Services;
using Ships.Parts.StarMap;
using Ships.Parts.StarMap.Model;
using StellarDriveDemoTFAdvanced.Common;
using StellarDriveDemoTFAdvanced.ShipComputer;
using TMPro;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Worlds
{
    /// <summary>
    /// The ship's star map shows every planet as the same grey ball. It now names each planet,
    /// colors the mod planets by biome, and marks the black holes and the abandoned stations
    /// (which are reached through their beacons).
    /// </summary>
    [HarmonyPatch(typeof(PlanetsHologram), nameof(PlanetsHologram.Generate))]
    internal static class StarMapLabels
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color HoleColor = new Color(1f, 0.55f, 0.2f);
        private static readonly Color StationColor = new Color(0.4f, 1f, 0.75f);

        private static void Postfix(PlanetsHologram __instance)
        {
            try
            {
                Decorate(__instance);
            }
            catch (System.Exception e)
            {
                ErrorLog.Report("star map labels", e);
            }
        }

        private static void Decorate(PlanetsHologram map)
        {
            // The star map has a second, unseen hologram under its interactions, on the ship's
            // physics body (for clicking planets). That body runs ahead of the drawn ship in flight:
            // anything shown there appears doubled in front of the ship and shakes. Only the drawn map is decorated.
            if (map.GetComponentInParent<StarMapVisuals>() == null)
                return;
            int layer = map.gameObject.layer;
            // Generated again (ship context changes): one set of labels and markers only
            foreach (Transform child in map.transform.Cast<Transform>().Where(c => c.name.StartsWith("TFA_Map")).ToList())
                Object.Destroy(child.gameObject);
            Material line = map.GetComponentsInChildren<LineRenderer>(true).Select(l => l.sharedMaterial).FirstOrDefault(m => m != null);
            MapSystemFilter filter = map.GetComponent<MapSystemFilter>() ?? map.gameObject.AddComponent<MapSystemFilter>();
            filter.Clear();
            foreach (PlanetHologram hologram in map.Holograms.Values)
            {
                PlanetSpec spec = PlanetCatalog.All.FirstOrDefault(p => p.Id == hologram.Id);
                if (spec != null)
                    Tint(hologram.HologramObject, spec.MapColor);
                string name = hologram.Id == PlanetFactory.TemplateId ? hologram.Planet.Name + Loc.Pick(" (départ)", " (start)", " (inicio)") : hologram.Planet.Name;
                GameObject label = Label(map.transform, layer, name, hologram.Position + Vector3.up * (hologram.Radius + 0.025f), spec != null ? Color.Lerp(spec.MapColor, Color.white, 0.4f) : Color.white);
                // The planet, the orbit drawn right after it, and its label belong to its star system
                bool erebos = spec != null && spec.System == PlanetWiki.Erebos;
                filter.Add(erebos, hologram.HologramObject);
                filter.Add(erebos, label);
                int index = hologram.HologramObject.transform.GetSiblingIndex();
                if (index + 1 < map.transform.childCount && map.transform.GetChild(index + 1).GetComponent<OrbitHologram>() != null)
                    filter.Add(erebos, map.transform.GetChild(index + 1).gameObject);
            }

            IPlanetsProvider planets = ServiceLocator.GetService<IPlanetsClientProvider>();
            if (planets == null)
                return;
            if (Settings.BlackHoleEnabled.Value)
            {
                for (int i = 0; i < BlackHole.All.Length; i++)
                {
                    Hole hole = BlackHole.All[i];
                    Vector3 offset = hole.OffsetFromAnchor * (planets.Planets.Any(p => p.Id == hole.AnchorPlanet) ? 1f : 1f + i);
                    if (!TryNear(map, planets, hole.AnchorPlanet, offset, 0.045f, out Vector3 local))
                        continue;
                    float size = 0.012f + hole.Horizon / 25000f * 0.008f;
                    var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Object.DestroyImmediate(ball.GetComponent<Collider>());
                    ball.name = "TFA_MapHole";
                    ball.layer = layer;
                    ball.transform.SetParent(map.transform, false);
                    ball.transform.localPosition = local;
                    ball.transform.localScale = Vector3.one * size * 2f;
                    ball.GetComponent<MeshRenderer>().sharedMaterial = LitMaterials.Get("TFA_MapHole", Color.black, 0f, 0.9f);
                    if (line != null)
                        Ring(map.transform, layer, line, local, size * 2.2f, HoleColor);
                    bool erebosHole = Galaxy.OrbitsErebos(hole.AnchorPlanet);
                    filter.Add(erebosHole, ball);
                    filter.Add(erebosHole, Label(map.transform, layer, hole.Name, local - Vector3.up * (size + 0.035f), HoleColor, 0.16f));
                    if (line != null)
                        filter.Add(erebosHole, map.transform.GetChild(map.transform.childCount - 2).gameObject);
                }
            }
            if (Settings.AbandonedStations.Value)
            {
                foreach (AbandonedStations.StationSpec station in AbandonedStations.All)
                {
                    Hole near = BlackHole.All[0];
                    ulong anchorId = station.NearBlackHole ? near.AnchorPlanet : station.AnchorPlanet;
                    Vector3 offset = station.NearBlackHole ? near.OffsetFromAnchor + station.Offset * 4f : station.Offset;
                    // Further out than the holes, so the two never sit on each other
                    if (!TryNear(map, planets, anchorId, offset, station.NearBlackHole ? 0.085f : 0.05f, out Vector3 local))
                        continue;
                    var pin = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Object.DestroyImmediate(pin.GetComponent<Collider>());
                    pin.name = "TFA_MapStation";
                    pin.layer = layer;
                    pin.transform.SetParent(map.transform, false);
                    pin.transform.localPosition = local;
                    pin.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
                    pin.transform.localScale = Vector3.one * 0.012f;
                    pin.GetComponent<MeshRenderer>().sharedMaterial = LitMaterials.Get("TFA_MapStation", StationColor, 0.2f, 0.6f, StationColor * 0.8f);
                    bool erebosStation = Galaxy.OrbitsErebos(anchorId);
                    filter.Add(erebosStation, pin);
                    filter.Add(erebosStation, Label(map.transform, layer, station.Name, local + Vector3.up * 0.012f, StationColor, 0.14f));
                }
            }
        }

        // A few thousand kilometers from a planet is inside its ball at the map's scale: the marker
        // sits just outside the ball, on the side it really is
        private static bool TryNear(PlanetsHologram map, IPlanetsProvider planets, ulong anchorId, Vector3 offset, float gap, out Vector3 local)
        {
            local = Vector3.zero;
            IPlanetProperties anchor = planets.Planets.FirstOrDefault(p => p.Id == anchorId) ?? planets.Planets.FirstOrDefault(p => p.Id == PlanetFactory.TemplateId);
            if (anchor == null || !map.Holograms.TryGetValue(anchor.Id, out PlanetHologram ball))
                return false;
            Vector3 center = map.CalculateStarRelativePosition((Vector3)anchor.ParentRelativePos);
            Vector3 toward = map.CalculateStarRelativePosition((Vector3)(anchor.ParentRelativePos + (Vector3Double)offset)) - center;
            Vector3 direction = toward.sqrMagnitude > 1e-12f ? toward.normalized : Vector3.right;
            local = ball.Position + direction * (ball.Radius + gap);
            return true;
        }

        private static void Tint(GameObject hologram, Color color)
        {
            var block = new MaterialPropertyBlock();
            foreach (Renderer renderer in hologram.GetComponentsInChildren<Renderer>(true))
            {
                renderer.GetPropertyBlock(block);
                block.SetColor(ColorId, color);
                block.SetColor(BaseColorId, color);
                renderer.SetPropertyBlock(block);
            }
        }

        private static GameObject Label(Transform parent, int layer, string text, Vector3 localPosition, Color color, float size = 0.22f)
        {
            var go = new GameObject("TFA_MapLabel") { layer = layer };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var label = go.AddComponent<TextMeshPro>();
            label.font = ComputerStyle.Font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.Bottom;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.sizeDelta = new Vector2(1f, 0.1f);
            label.rectTransform.pivot = new Vector2(0.5f, 0f);
            go.AddComponent<FaceCamera>();
            return go;
        }

        private static void Ring(Transform parent, int layer, Material material, Vector3 center, float radius, Color color)
        {
            var go = new GameObject("TFA_MapHoleDisk") { layer = layer };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = Quaternion.Euler(20f, 0f, 10f);
            var ring = go.AddComponent<LineRenderer>();
            ring.sharedMaterial = material;
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.widthMultiplier = 0.004f;
            ring.startColor = ring.endColor = color;
            const int points = 40;
            ring.positionCount = points;
            var positions = new Vector3[points];
            for (int i = 0; i < points; i++)
            {
                float a = i * Mathf.PI * 2f / points;
                positions[i] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
            }
            ring.SetPositions(positions);
        }
    }

    /// <summary>
    /// Keeps a star map label upright on the map and turned toward the player, around the map's
    /// vertical axis only: the text never tilts or spins with the camera.
    /// </summary>
    internal sealed class FaceCamera : MonoBehaviour
    {
        private void LateUpdate()
        {
            Camera camera = MainCamera.Get();
            if (camera == null || transform.parent == null || (camera.transform.position - transform.position).sqrMagnitude > 30f * 30f)
                return;
            Vector3 up = transform.parent.up;
            Vector3 away = Vector3.ProjectOnPlane(transform.position - camera.transform.position, up);
            if (away.sqrMagnitude < 1e-6f)
                return;
            transform.rotation = Quaternion.LookRotation(away.normalized, up);
        }
    }

    /// <summary>
    /// The star map shows one star system: by default the one the ship is in (Otrera's planets or
    /// Erebos's); the targeting menu of the star map can show the other (see <see cref="TargetingFilters"/>).
    /// </summary>
    internal sealed class MapSystemFilter : MonoBehaviour
    {
        /// <summary>The galaxy chosen (targeting computer menu, or the use key on the star map): null follows the ship, true Erebos, false Otrera.</summary>
        public static bool? View;

        /// <summary>The drawn star maps, for looking at one and switching its galaxy.</summary>
        public static readonly List<MapSystemFilter> Shown = new List<MapSystemFilter>();

        /// <summary>Follow the ship, then Otrera, then Erebos.</summary>
        public static void Cycle() => View = View == null ? false : View == false ? true : (bool?)null;

        public static string ViewName => (ShowsErebos ? PlanetWiki.SystemName(PlanetWiki.Erebos) : "442 Otrera")
            + (View == null ? Loc.Pick(" (où est le vaisseau)", " (where the ship is)", " (donde está la nave)") : "");

        private void OnEnable() => Shown.Add(this);

        private void OnDisable() => Shown.Remove(this);

        /// <summary>Whether a ray meets this map's hologram (its planets' box), and how far.</summary>
        public bool Hit(Ray ray, out float distance)
        {
            distance = 0f;
            if (Time.unscaledTime >= _nextBounds)
            {
                _nextBounds = Time.unscaledTime + 1f;
                _hasBounds = false;
                foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
                {
                    if (!_hasBounds)
                    {
                        _bounds = renderer.bounds;
                        _hasBounds = true;
                    }
                    else
                    {
                        _bounds.Encapsulate(renderer.bounds);
                    }
                }
                if (_hasBounds)
                    _bounds.Expand(0.2f);
            }
            return _hasBounds && _bounds.IntersectRay(ray, out distance);
        }

        private float _nextBounds;
        private bool _hasBounds;
        private Bounds _bounds;

        public static bool ShowsErebos => View ?? (Galaxy.Erebos != null && Galaxy.Nearest() == Galaxy.Erebos);

        private readonly List<GameObject> _otrera = new List<GameObject>();
        private readonly List<GameObject> _erebos = new List<GameObject>();
        private float _next;
        private bool? _shown;

        public void Clear()
        {
            _otrera.Clear();
            _erebos.Clear();
            _shown = null;
        }

        public void Add(bool erebos, GameObject item)
        {
            if (item != null)
                (erebos ? _erebos : _otrera).Add(item);
        }

        private void Update()
        {
            if (Time.unscaledTime < _next)
                return;
            _next = Time.unscaledTime + 0.25f;
            bool inErebos = ShowsErebos;
            if (_shown == inErebos)
                return;
            _shown = inErebos;
            foreach (GameObject item in _otrera)
                if (item != null)
                    item.SetActive(!inErebos);
            foreach (GameObject item in _erebos)
                if (item != null)
                    item.SetActive(inErebos);
        }
    }
}

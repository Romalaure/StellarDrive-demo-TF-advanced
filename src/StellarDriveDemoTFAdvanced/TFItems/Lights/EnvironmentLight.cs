using System.Linq;
using Core.Services;
using Planets.Interface.Model;
using Planets.Interface.Services;
using UnityEngine;
using WorldTracking.Interface.Services;
using WorldTracking.Interface.Values;

namespace StellarDriveDemoTFAdvanced.TFItems.Lights
{
    /// <summary>
    /// Estimates how much sunlight reaches a point, from 0 (night, indoors) to 1 (open daylight):
    /// the sun's height above the nearest planet's horizon, and whether something blocks the sun.
    /// </summary>
    internal static class EnvironmentLight
    {
        // Ships and terrain block the sun; a covered spot still gets some bounced light
        private const float IndoorLight = 0.12f;
        private const float OcclusionDistance = 400f;

        private static Light _sun;
        private static IPlanetsClientProvider _planets;
        private static IWorldTransposeClient _transpose;
        private static float _nextLookup;
        private static int _mask = -1;
        private static int _planetFrame = -1;
        private static bool _hasPlanet;
        private static Vector3 _planetCenter;
        private static float _planetRadius;

        public static float Daylight(Vector3 position)
        {
            Light sun = Sun();
            if (sun == null || !sun.isActiveAndEnabled)
                return 0f;
            Vector3 toSun = -sun.transform.forward;

            float day = 1f;
            // The nearest planet is the same for every lamp: looked up once a frame
            if (_planetFrame != Time.frameCount)
            {
                _planetFrame = Time.frameCount;
                IPlanetProperties planet = _planets?.GetNearestPlanet(ClientOrigin.LocalVisualSpace);
                _hasPlanet = planet != null && _transpose != null;
                if (_hasPlanet)
                {
                    _planetCenter = _transpose.Position(planet.FixedWorldSpace, ClientOrigin.LocalVisualSpace, Vector3.zero);
                    _planetRadius = planet.Radius;
                }
            }
            if (_hasPlanet)
            {
                Vector3 fromCenter = position - _planetCenter;
                // Within a few radii, the planet decides day and night
                if (fromCenter.magnitude < _planetRadius * 3f)
                    day = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.08f, 0.25f, Vector3.Dot(fromCenter.normalized, toSun)));
            }
            if (day <= 0.001f)
                return 0f;

            if (_mask == -1)
                _mask = LayerMask.GetMask("Default", "Ship", "WorldObject", "Terrain", "PlanetTerrain");
            bool covered = ClientScene.PhysicsScene.Raycast(position + toSun * 0.35f, toSun, out _, OcclusionDistance, _mask, QueryTriggerInteraction.Ignore);
            return day * (covered ? IndoorLight : 1f) * Mathf.Clamp01(sun.intensity);
        }

        private static Light Sun()
        {
            if (_sun != null && _planets != null)
                return _sun;
            if (Time.unscaledTime < _nextLookup)
                return _sun;
            _nextLookup = Time.unscaledTime + 2f;

            _planets = ServiceLocator.GetService<IPlanetsClientProvider>();
            _transpose = ServiceLocator.GetService<IWorldTransposeClient>();
            _sun = RenderSettings.sun;
            if (_sun == null)
            {
                _sun = Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
                    .Where(l => l.type == LightType.Directional)
                    .OrderByDescending(l => l.intensity)
                    .FirstOrDefault();
            }
            return _sun;
        }

        public static void Invalidate()
        {
            _sun = null;
            _planets = null;
            _transpose = null;
            _nextLookup = 0f;
        }
    }
}

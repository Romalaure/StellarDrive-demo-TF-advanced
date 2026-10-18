using System;
using Core.Values;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;
using UnityEngine.Rendering;
using WorldTracking.Interface.Model;
using WorldTracking.Interface.Model.Spaces;
using WorldTracking.Interface.Model.Spaces.Common;
using WorldTracking.Interface.Services;
using Object = UnityEngine.Object;

namespace StellarDriveDemoTFAdvanced.Worlds
{
    internal enum WeatherKind { Clear, Rain, Storm, Snow, Sand, Ash }

    /// <summary>
    /// Weather on the mod's planets, around the player standing on the ground (not inside a ship):
    /// rain, storms with lightning and thunder flashes, snow (on TF Glacia, and on TF Gaïa above
    /// the snowline), sandstorms on TF Aridia, ash and embers on Pyros. Each planet changes weather
    /// every ten minutes by a seeded draw from the clock, so players together see the same sky;
    /// it fades in and out. Visual only, for each player.
    /// </summary>
    internal static class Weather
    {
        private const float SlotSeconds = 600f, Fade = 12f;

        private static GameObject _root;
        private static ParticleSystem _rain, _snow, _sand, _embers;
        private static LineRenderer _bolt;
        private static WeatherKind _kind;
        private static float _intensity, _nextCheck, _nextBolt, _flash;
        private static Vector3 _up = Vector3.up;
        private static bool _outside;
        private static Texture2D _soft;

        /// <summary>What is falling right now on this planet at this height, for the planet's weather plan.</summary>
        public static WeatherKind Plan(ulong planetId, float elevation)
        {
            string forced = Settings.DevWeather.Value;
            if (!string.IsNullOrEmpty(forced) && Enum.TryParse(forced, true, out WeatherKind dev))
                return dev;
            long slot = (long)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() / SlotSeconds);
            double roll = new System.Random(unchecked((int)(slot * 7919 + (long)planetId * 104729))).NextDouble();
            switch (planetId)
            {
                case 108: // Gaïa: rain, sometimes a storm; snow up high
                    if (roll < 0.15) return elevation > 750f ? WeatherKind.Snow : WeatherKind.Storm;
                    if (roll < 0.45) return elevation > 750f ? WeatherKind.Snow : WeatherKind.Rain;
                    return WeatherKind.Clear;
                case 104: // Verdance, temperate
                    return roll < 0.12 ? WeatherKind.Storm : roll < 0.4 ? WeatherKind.Rain : WeatherKind.Clear;
                case 107: // Sylvara, jungle: wet
                    return roll < 0.2 ? WeatherKind.Storm : roll < 0.6 ? WeatherKind.Rain : WeatherKind.Clear;
                case 112: // Thalassa, ocean
                    return roll < 0.25 ? WeatherKind.Storm : roll < 0.5 ? WeatherKind.Rain : WeatherKind.Clear;
                case 105: // Glacia
                    return roll < 0.6 ? WeatherKind.Snow : WeatherKind.Clear;
                case 101: // Aridia
                    return roll < 0.45 ? WeatherKind.Sand : WeatherKind.Clear;
                case 111: // Pyros
                    return roll < 0.55 ? WeatherKind.Ash : WeatherKind.Clear;
                default:
                    return WeatherKind.Clear;
            }
        }

        public static void Update()
        {
            if (!Settings.Weather.Value)
            {
                Clear();
                return;
            }
            if (Time.unscaledTime >= _nextCheck)
            {
                _nextCheck = Time.unscaledTime + 1f;
                Check();
            }
            WeatherKind shown = _outside ? _kind : WeatherKind.Clear;
            float target = shown == WeatherKind.Clear ? 0f : 1f;
            _intensity = Mathf.MoveTowards(_intensity, target, Time.unscaledDeltaTime / Fade);
            if (_intensity <= 0f && _root == null)
                return;
            Camera camera = MainCamera.Get();
            if (camera == null)
                return;
            Ensure();
            _root.transform.position = camera.transform.position;
            _root.transform.rotation = Quaternion.FromToRotation(Vector3.up, _up);
            Emit(_rain, _kind == WeatherKind.Rain ? 2600f : _kind == WeatherKind.Storm ? 5200f : 0f);
            Emit(_snow, _kind == WeatherKind.Snow ? 1400f : _kind == WeatherKind.Ash ? 900f : 0f);
            Emit(_sand, _kind == WeatherKind.Sand ? 90f : 0f);
            Emit(_embers, _kind == WeatherKind.Ash ? 60f : 0f);
            ParticleSystem.MainModule snow = _snow.main;
            snow.startColor = _kind == WeatherKind.Ash ? new Color(0.25f, 0.23f, 0.22f, 0.85f) : new Color(1f, 1f, 1f, 0.9f);
            Lightning(camera);
        }

        private static void Check()
        {
            _outside = false;
            var players = TFItems.Common.GameServices.PlayersClient;
            ISpacesClientProvider spaces = GameServices.Spaces;
            if (players == null || spaces == null || !players.TryGetLocalPlayer(out ITrackedWorldObject player) || player == null)
                return;
            if (!spaces.TryGetSpace(player.SpaceId, out ISpace space) || !(space is PlanetSurfaceSpace surface))
                return;
            if (!PlanetFactory.IsModPlanet(surface.PlanetId))
                return;
            // Near a planet, single precision is enough (a few hundred km)
            Vector3 fromCenter = (Vector3)(surface.Center + (Vector3Double)player.Position);
            float distance = fromCenter.magnitude;
            Planets.Planet planet = null;
            foreach (Planets.Planet p in PlanetFactory.Planets)
            {
                if (p != null && p.Id == surface.PlanetId)
                    planet = p;
            }
            float elevation = planet != null ? distance - planet.Radius : 0f;
            // Weather in the air near the ground only (not from orbit)
            if (planet == null || elevation > 4000f)
                return;
            _up = fromCenter.normalized;
            _kind = Plan(surface.PlanetId, elevation);
            _outside = true;
        }

        private static void Emit(ParticleSystem system, float rate)
        {
            if (system == null)
                return;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = rate * _intensity;
        }

        private static void Clear()
        {
            _intensity = 0f;
            if (_root != null)
                Object.Destroy(_root);
            _root = null;
        }

        // ---- Lightning: a jagged bolt far off and a flash of the screen

        private static void Lightning(Camera camera)
        {
            _flash = Mathf.Max(0f, _flash - Time.unscaledDeltaTime * 4f);
            if (_bolt != null)
                _bolt.enabled = _flash > 0.3f;
            if (_kind != WeatherKind.Storm || _intensity < 0.6f || !_outside)
                return;
            if (Time.unscaledTime < _nextBolt)
                return;
            _nextBolt = Time.unscaledTime + UnityEngine.Random.Range(5f, 16f);
            Vector3 up = _up;
            Vector3 side = Vector3.Cross(up, Vector3.forward).normalized;
            if (side.sqrMagnitude < 0.01f)
                side = Vector3.Cross(up, Vector3.right).normalized;
            Vector3 across = Vector3.Cross(up, side);
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f), far = UnityEngine.Random.Range(180f, 650f);
            Vector3 ground = camera.transform.position + (side * Mathf.Cos(angle) + across * Mathf.Sin(angle)) * far - up * 20f;
            const int points = 14;
            _bolt.positionCount = points;
            for (int i = 0; i < points; i++)
            {
                float t = i / (float)(points - 1);
                Vector3 jitter = i == 0 || i == points - 1 ? Vector3.zero : (side * UnityEngine.Random.Range(-14f, 14f) + across * UnityEngine.Random.Range(-14f, 14f)) * (1f - t * 0.5f);
                _bolt.SetPosition(i, ground + up * (420f * (1f - t)) + jitter);
            }
            _flash = 1f;
        }

        /// <summary>The flash over the whole screen, after the game's frame (OnGUI).</summary>
        public static void DrawFlash()
        {
            Color previous = GUI.color;
            // An overcast veil: grey under rain and storms, ochre in a sandstorm, dark in ash
            if (_intensity > 0.01f && _outside)
            {
                Color veil = _kind == WeatherKind.Storm ? new Color(0.12f, 0.14f, 0.18f, 0.32f) : _kind == WeatherKind.Rain ? new Color(0.2f, 0.22f, 0.26f, 0.18f)
                    : _kind == WeatherKind.Sand ? new Color(0.6f, 0.45f, 0.25f, 0.3f) : _kind == WeatherKind.Ash ? new Color(0.15f, 0.1f, 0.08f, 0.25f) : new Color(0.85f, 0.88f, 0.92f, 0.12f);
                veil.a *= _intensity;
                GUI.color = veil;
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            }
            if (_flash <= 0.01f)
            {
                GUI.color = previous;
                return;
            }
            GUI.color = new Color(0.85f, 0.9f, 1f, 0.35f * _flash);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        // ---- The particle systems, made once, following the camera

        private static void Ensure()
        {
            if (_root != null)
                return;
            _root = new GameObject("TFA_Weather");
            _soft = _soft != null ? _soft : SoftDot();
            Material additive = TransparentMaterials.Additive("TFA_WeatherAdditive", _soft);
            Material blended = TransparentMaterials.Blended("TFA_WeatherDust", Color.white);
            if (blended != null)
            {
                blended = new Material(blended) { name = "TFA_WeatherDustSoft" };
                blended.SetTexture("_BaseMap", _soft);
            }
            _rain = System("Rain", additive, 9000, 0.9f, new Vector3(50f, 1f, 50f), 22f, 0.025f, 0.035f, new Color(0.75f, 0.82f, 0.95f, 0.45f), true);
            _snow = System("Snow", blended ?? additive, 6000, 7f, new Vector3(60f, 1f, 60f), 1.6f, 0.05f, 0.11f, Color.white, false);
            _sand = System("Sand", blended ?? additive, 600, 6f, new Vector3(80f, 20f, 80f), 0.5f, 4f, 9f, new Color(0.78f, 0.6f, 0.38f, 0.32f), false);
            _embers = System("Embers", additive, 600, 5f, new Vector3(50f, 6f, 50f), -0.6f, 0.05f, 0.1f, new Color(1f, 0.45f, 0.12f, 0.9f), false);
            // Snow and ash drift, sand blows sideways
            ParticleSystem.VelocityOverLifetimeModule drift = _snow.velocityOverLifetime;
            drift.enabled = true;
            drift.space = ParticleSystemSimulationSpace.Local;
            drift.x = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
            drift.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            drift.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
            ParticleSystem.VelocityOverLifetimeModule wind = _sand.velocityOverLifetime;
            wind.enabled = true;
            wind.space = ParticleSystemSimulationSpace.Local;
            wind.x = new ParticleSystem.MinMaxCurve(9f, 14f);
            wind.y = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            wind.z = new ParticleSystem.MinMaxCurve(2f, 4f);
            ParticleSystem.ShapeModule sandShape = _sand.shape;
            sandShape.position = new Vector3(-30f, 4f, 0f);

            var bolt = new GameObject("Bolt");
            bolt.transform.SetParent(_root.transform, false);
            _bolt = bolt.AddComponent<LineRenderer>();
            _bolt.useWorldSpace = true;
            _bolt.widthMultiplier = 2.2f;
            _bolt.sharedMaterial = TransparentMaterials.Additive("TFA_WeatherBolt", null);
            _bolt.startColor = _bolt.endColor = new Color(0.85f, 0.9f, 1f, 1f);
            _bolt.shadowCastingMode = ShadowCastingMode.Off;
            _bolt.enabled = false;
        }

        // A box of falling particles over the camera (y: its local up), falling at `fall` m/s
        private static ParticleSystem System(string name, Material material, int max, float life, Vector3 box, float fall, float sizeMin, float sizeMax, Color color, bool streaks)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(_root.transform, false);
            var system = holder.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.duration = 10f;
            main.playOnAwake = false;
            main.startLifetime = life;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = box;
            shape.position = new Vector3(0f, fall > 0f ? 14f : -1f, 0f);
            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(-fall * 1.1f, -fall * 0.9f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) };
            gradient.alphaKeys = new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(1f, 0.85f), new GradientAlphaKey(0f, 1f) };
            fade.color = new ParticleSystem.MinMaxGradient(gradient);
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            var renderer = holder.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (streaks)
            {
                // Rain: thin streaks along their fall
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = 0.035f;
                renderer.lengthScale = 1f;
            }
            else
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
            }
            system.Play();
            return system;
        }

        // A round soft dot: snowflakes, ash, dust
        private static Texture2D SoftDot()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "TFA_WeatherDot", wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = new Vector2(x - size * 0.5f + 0.5f, y - size * 0.5f + 0.5f).magnitude / (size * 0.5f);
                    float a = Mathf.Clamp01(1f - d);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            }
            texture.Apply();
            return texture;
        }
    }
}

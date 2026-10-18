using Core.Services;
using Core.Values;
using StellarDriveDemoTFAdvanced.Common;
using Ships;
using Ships.Services;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using WorldTracking.Interface.Model;
using WorldTracking.Interface.Model.Spaces;
using WorldTracking.Interface.Model.Spaces.Common;
using WorldTracking.Interface.Services;

namespace StellarDriveDemoTFAdvanced.Worlds
{
    /// <summary>
    /// Flying fast and low over a planet, the shadows were drawn up to 500 m away for ground that
    /// passes in a blur: about a sixth of the graphics card's work at 400 m/s (84 fps without
    /// them instead of 79). Above 120 m/s near a planet the shadows reach 150 m only, and come
    /// back to the player's distance once under 80 m/s for 2 s (setting FastFlightShadows).
    /// </summary>
    internal static class FastFlight
    {
        private const float FastSpeed = 120f, SlowSpeed = 80f, FastShadows = 150f;

        private static Vector3 _last;
        private static uint _lastSpace;
        private static float _speed, _slowSince, _base = -1f, _applied = -1f;
        private static bool _fast;

        /// <summary>The local player's (or their ship's) speed over the planet's ground, m/s (0 away from planets).</summary>
        public static float Speed => _speed;

        /// <summary>Every frame.</summary>
        public static void Update()
        {
            Measure();
            float now = Time.unscaledTime;
            if (_speed > FastSpeed)
            {
                _fast = true;
                _slowSince = now;
            }
            else if (_speed > SlowSpeed)
                _slowSince = now;
            else if (_fast && now - _slowSince > 2f)
                _fast = false;
            Shadows(_fast && Settings.FastFlightShadows.Value);
        }

        private static void Measure()
        {
            var players = TFItems.Common.GameServices.PlayersClient;
            var spaces = ServiceLocator.GetService<ISpacesClientProvider>();
            if (players == null || spaces == null || !players.TryGetLocalPlayer(out ITrackedWorldObject player) || player == null)
            {
                _speed = 0f;
                return;
            }
            Vector3 position = player.Position;
            uint spaceId = player.SpaceId;
            if (spaces.TryGetSpace(spaceId, out ISpace space) && space is ShipRelativeSpace aboard)
            {
                ShipsClientTracker ships = TFItems.Common.GameServices.ShipsClient;
                if (ships == null || !ships.TryGetTrackedRootShip(aboard.ShipId, out TrackedShipClient root))
                {
                    _speed = 0f;
                    return;
                }
                position = root.PhysicsState.Position;
                spaceId = root.PhysicsState.SpaceId;
                spaces.TryGetSpace(spaceId, out space);
            }
            float dt = Time.unscaledDeltaTime;
            if (!(space is IPlanetSpace) || spaceId != _lastSpace || dt <= 0f)
            {
                _speed = space is IPlanetSpace ? _speed : 0f;
                _last = position;
                _lastSpace = spaceId;
                return;
            }
            float measured = (position - _last).magnitude / dt;
            _last = position;
            // A jump (teleport, space change inside the frame) is not a speed
            if (measured > 30000f)
                return;
            _speed = Mathf.Lerp(_speed, measured, Mathf.Clamp01(dt * 4f));
        }

        private static void Shadows(bool reduced)
        {
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp))
                return;
            // The game's options changed the distance meanwhile: that is the player's new choice
            if (_applied >= 0f && !Mathf.Approximately(urp.shadowDistance, _applied))
                _base = -1f;
            if (_base < 0f)
                _base = urp.shadowDistance;
            float wanted = reduced ? Mathf.Min(_base, FastShadows) : _base;
            if (Mathf.Approximately(urp.shadowDistance, wanted))
            {
                _applied = wanted;
                return;
            }
            urp.shadowDistance = wanted;
            _applied = wanted;
            if (DevHarness.Active)
                AdvancedMod.Log.Msg($"dev: fast flight: shadows to {wanted:0} m at {_speed:0} m/s");
        }
    }
}

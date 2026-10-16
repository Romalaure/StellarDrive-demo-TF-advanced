using HarmonyLib;
using Players.Interface.Model;
using Players.Network.Model;
using Players.Visuals.Camera;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StellarDriveDemoTFAdvanced.TFItems.Devices
{
    /// <summary>
    /// Third person while flying: from the pilot seat, a key (V) moves the view behind and above
    /// the ship, looking where the pilot looks (mouse look still works, and recenters behind the
    /// ship). The mouse wheel sets the distance. Only the camera moves: controls, the ship and the
    /// other players see nothing different.
    /// </summary>
    internal static class ThirdPerson
    {
        private static readonly KeySetting Key = new KeySetting(() => Settings.ThirdPersonKey);

        public static bool Active { get; private set; }

        private static Transform _ship;
        private static Vector3 _localCenter;
        private static float _radius = 5f;
        private static float _nextMeasure;
        private static Vector3 _smoothedPosition;
        private static bool _hasSmoothed;

        public static void Update()
        {
            PlayerState state = GameServices.LocalState;
            if (state.Type != PlayerStateType.Piloting)
            {
                _hasSmoothed = false;
                return;
            }
            if (!ModMenu.IsOpen && Key.WasPressed)
            {
                Active = !Active;
                _hasSmoothed = false;
            }
            if (Active && !ModMenu.IsOpen && Mouse.current != null)
            {
                float scroll = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                    Settings.ThirdPersonDistance.Value = Mathf.Clamp(Settings.ThirdPersonDistance.Value * (scroll > 0f ? 0.9f : 1.1f), 0.35f, 5f);
            }
        }

        /// <summary>Where the camera goes, given where the pilot camera is looking.</summary>
        public static bool TryPlace(uint shipId, Quaternion look, out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = default;
            var ships = GameServices.ShipsClient;
            if (ships == null || !ships.TryGetShipVisualTransform(shipId, out Transform ship) || ship == null)
                return false;
            Measure(ship);

            Vector3 up = ship.up;
            Vector3 center = ship.TransformPoint(_localCenter);
            Vector3 forward = look * Vector3.forward;
            float distance = Mathf.Max(4f, _radius * 2.2f) * Mathf.Clamp(Settings.ThirdPersonDistance.Value, 0.35f, 5f);
            Vector3 target = center - forward * distance + up * (_radius * 0.45f + 1f);

            // The ship moves fast; follow it exactly, smoothing only the swing around it
            Vector3 offset = target - center;
            if (_hasSmoothed)
                offset = Vector3.Lerp(_smoothedPosition, offset, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 10f));
            _smoothedPosition = offset;
            _hasSmoothed = true;

            position = center + offset;
            rotation = Quaternion.LookRotation(center + up * (_radius * 0.15f) - position, up);
            return true;
        }

        // The ship's center and size, from its renderers, every couple of seconds
        private static void Measure(Transform ship)
        {
            if (ship == _ship && Time.unscaledTime < _nextMeasure)
                return;
            _ship = ship;
            _nextMeasure = Time.unscaledTime + 2f;
            bool any = false;
            var bounds = new Bounds();
            foreach (Renderer renderer in ship.GetComponentsInChildren<Renderer>(false))
            {
                if (!(renderer is MeshRenderer) || renderer.gameObject.name == Lights.LampGlow.OverlayName)
                    continue;
                if (!any)
                {
                    bounds = renderer.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            if (!any)
            {
                _localCenter = Vector3.zero;
                _radius = 5f;
                return;
            }
            _localCenter = ship.InverseTransformPoint(bounds.center);
            _radius = Mathf.Clamp(bounds.extents.magnitude, 2f, 80f);
        }
    }

    /// <summary>After the game places the pilot camera, moves it behind the ship in third person.</summary>
    [HarmonyPatch(typeof(PlayerCamerasUpdater), nameof(PlayerCamerasUpdater.UpdateCameras))]
    internal static class ThirdPersonCameraPatch
    {
        private static readonly AccessTools.FieldRef<PlayerCamerasUpdater, PlayerCamera> Camera =
            AccessTools.FieldRefAccess<PlayerCamerasUpdater, PlayerCamera>("_camera");
        private static readonly AccessTools.FieldRef<PlayerCamerasUpdater, PlayerDistantCamera> Distant =
            AccessTools.FieldRefAccess<PlayerCamerasUpdater, PlayerDistantCamera>("_distantCamera");

        private static void Postfix(PlayerCamerasUpdater __instance, PlayerState playerState)
        {
            if (!ThirdPerson.Active || playerState.Type != PlayerStateType.Piloting)
                return;
            PlayerCamera camera = Camera(__instance);
            PlayerDistantCamera distant = Distant(__instance);
            if (camera == null)
                return;
            if (!ThirdPerson.TryPlace(playerState.EntityId, camera.Rotation, out Vector3 position, out Quaternion rotation))
                return;
            camera.UpdateCamera(position, rotation);
            distant?.UpdateCamera(position, rotation);
        }
    }
}

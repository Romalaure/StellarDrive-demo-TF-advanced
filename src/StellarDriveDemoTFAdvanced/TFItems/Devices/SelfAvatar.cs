using System.Collections.Generic;
using HarmonyLib;
using Players.Interface.Model;
using Players.Network.Model;
using Players.Visuals.Camera;
using UnityEngine;
using UnityEngine.Rendering;

namespace StellarDriveDemoTFAdvanced.TFItems.Devices
{
    /// <summary>
    /// This player as the others see them (a still copy of the game's other-player avatar, in this
    /// player's outfit), drawn only where the player's own body falls short: the first-person body
    /// is arms, torso and legs without a head, and it follows the view. So the camera tablet's feed
    /// draws the avatar instead of it, and the photo mode, whose view leaves the body, shows the
    /// avatar where the player stands (the first-person body, flying with the view, is hidden).
    /// Never drawn in the player's own view otherwise.
    /// </summary>
    internal static class SelfAvatar
    {
        private static GameObject _holder, _avatar;
        private static Renderer[] _renderers = new Renderer[0];
        private static readonly List<Renderer> HiddenBody = new List<Renderer>();
        private static int _version = -1;
        private static bool _walking, _hooked, _photoShown;
        private static Transform _view;

        /// <summary>Where the player is this frame (from the cameras' update, walking only).</summary>
        internal static void Track(PlayerCamera camera, Vector3 body, Quaternion bodyRotation, Quaternion look, bool walking)
        {
            _view = camera != null ? camera.transform : null;
            _walking = walking;
            if (!walking || !Ensure())
                return;
            Vector3 up = bodyRotation * Vector3.up;
            Vector3 forward = Vector3.ProjectOnPlane(look * Vector3.forward, up);
            if (forward.sqrMagnitude < 0.01f)
                forward = Vector3.ProjectOnPlane(bodyRotation * Vector3.forward, up);
            _holder.transform.SetPositionAndRotation(body, Quaternion.LookRotation(forward, up));
            if (_version != OutfitDresser.Version)
            {
                _version = OutfitDresser.Version;
                OutfitDresser.Wear(_avatar, Outfits.Mine);
                _renderers = _avatar.GetComponentsInChildren<Renderer>(true);
                Show(_photoShown);
            }
        }

        private static bool Ensure()
        {
            if (_avatar != null)
                return true;
            if (_holder == null)
            {
                _holder = new GameObject("TFA_SelfAvatar");
                Object.DontDestroyOnLoad(_holder);
            }
            _avatar = CosmeticCabin.Mannequin(_holder.transform);
            if (_avatar == null)
                return false;
            // The holder is the body's center, as the game places the other players' avatars
            _avatar.transform.localPosition = Vector3.zero;
            _avatar.transform.localRotation = Quaternion.identity;
            _renderers = _avatar.GetComponentsInChildren<Renderer>(true);
            _version = -1;
            Show(false);
            if (!_hooked)
            {
                _hooked = true;
                RenderPipelineManager.beginCameraRendering += Begin;
                RenderPipelineManager.endCameraRendering += End;
            }
            return true;
        }

        // Forced off rather than disabled: the pieces the avatar keeps disabled (the tools and
        // accessories it holds only in use) stay so when it is shown
        private static void Show(bool shown)
        {
            foreach (Renderer renderer in _renderers)
                if (renderer != null)
                    renderer.forceRenderingOff = !shown;
        }

        // The first-person body (and the tool in hand) under the view
        private static void HideBody(bool hidden)
        {
            if (hidden)
            {
                if (HiddenBody.Count > 0 || _view == null)
                    return;
                foreach (Renderer renderer in _view.GetComponentsInChildren<Renderer>(false))
                {
                    if (renderer.enabled)
                    {
                        renderer.enabled = false;
                        HiddenBody.Add(renderer);
                    }
                }
                return;
            }
            foreach (Renderer renderer in HiddenBody)
                if (renderer != null)
                    renderer.enabled = true;
            HiddenBody.Clear();
        }

        /// <summary>Each frame: the photo mode shows the avatar (and hides the body flying with the view).</summary>
        public static void Update()
        {
            bool photo = PhotoMode.Active && _walking && _avatar != null;
            if (photo == _photoShown)
                return;
            _photoShown = photo;
            Show(photo);
            HideBody(photo);
        }

        // The tablet's feed: the avatar instead of the body, only while it renders
        private static void Begin(ScriptableRenderContext context, Camera camera)
        {
            if (_photoShown || !_walking || !Cameras.CameraTablet.IsFeed(camera))
                return;
            Show(true);
            HideBody(true);
        }

        private static void End(ScriptableRenderContext context, Camera camera)
        {
            if (_photoShown || !Cameras.CameraTablet.IsFeed(camera))
                return;
            Show(false);
            HideBody(false);
        }
    }

    /// <summary>Where the player is, from the game's own camera placement (before the views that move it).</summary>
    [HarmonyPatch(typeof(PlayerCamerasUpdater), nameof(PlayerCamerasUpdater.UpdateCameras))]
    internal static class SelfAvatarTrackPatch
    {
        private static readonly AccessTools.FieldRef<PlayerCamerasUpdater, PlayerCamera> Camera =
            AccessTools.FieldRefAccess<PlayerCamerasUpdater, PlayerCamera>("_camera");

        [HarmonyPriority(Priority.First)]
        private static void Postfix(PlayerCamerasUpdater __instance, Vector3 pos, Quaternion rot, PlayerState playerState)
        {
            try
            {
                PlayerCamera camera = Camera(__instance);
                SelfAvatar.Track(camera, pos, rot, camera != null ? camera.Rotation : rot, playerState.Type != PlayerStateType.Piloting);
            }
            catch (System.Exception e)
            {
                StellarDriveDemoTFAdvanced.Common.ErrorLog.Report("self avatar", e);
            }
        }
    }
}

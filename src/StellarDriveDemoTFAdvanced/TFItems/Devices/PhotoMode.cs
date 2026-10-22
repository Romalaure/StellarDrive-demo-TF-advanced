using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Players.Visuals.Camera;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Loc = StellarDriveDemoTFAdvanced.Common.Loc;

namespace StellarDriveDemoTFAdvanced.TFItems.Devices
{
    /// <summary>
    /// Photo mode (F8): the view leaves the player and flies freely (mouse to look, ZQSD/WASD to
    /// move, Space/Ctrl up and down, Shift faster, wheel to zoom), the game's interface is hidden,
    /// F cycles filters, P or Enter takes the photo into UserData/TFAdvanced/photos (where the
    /// photo frames find it), Escape or F8 comes back.
    /// </summary>
    internal static class PhotoMode
    {
        private static readonly KeySetting Key = new KeySetting(() => Settings.PhotoKey);

        public static bool Active { get; private set; }

        /// <summary>The mod draws nothing of its own while a photo is taken.</summary>
        public static bool Capturing { get; private set; }

        public static string Folder => Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "TFAdvanced", "photos");

        private static Vector3 _position;
        private static float _yaw, _pitch, _roll;
        private static float _fov = 60f, _gameFov = 60f;
        private static bool _help = true;
        private static int _filter;
        private static readonly List<Canvas> Hidden = new List<Canvas>();
        private static string _toast;
        private static float _toastUntil;

        private static Volume _volume;
        private static ColorAdjustments _color;
        private static Vignette _vignette;

        private static string[] FilterNames => new[]
        {
            Loc.Pick("Aucun", "None", "Ninguno"), Loc.Pick("Noir et blanc", "Black and white", "Blanco y negro"), Loc.Pick("Sépia", "Sepia", "Sepia"),
            Loc.Pick("Couleurs vives", "Vivid", "Vivos"), Loc.Pick("Cinéma", "Cinema", "Cine"), Loc.Pick("Vision nocturne", "Night vision", "Visión nocturna")
        };

        public static void Update()
        {
            if (!Active)
            {
                if (Key.WasPressed && !ModMenu.IsOpen && GameServices.ShipsClient != null && !Cameras.CameraTablet.Open)
                    Enter();
                return;
            }
            // Escape closes the mod window, and with it photo mode (see Enter)
            if (!ModMenu.IsOpen)
            {
                Exit();
                return;
            }
            if (Key.WasPressed)
            {
                ModMenu.Close();
                return;
            }
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard == null || mouse == null || Capturing)
                return;
            Vector2 look = mouse.delta.ReadValue() * 0.08f * Mathf.Clamp(_fov / 60f, 0.2f, 1.5f);
            _yaw += look.x;
            _pitch = Mathf.Clamp(_pitch - look.y, -89f, 89f);
            if (keyboard.rKey.isPressed)
                _roll += 40f * Time.unscaledDeltaTime;
            if (keyboard.tKey.wasPressedThisFrame)
                _roll = 0f;
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
                _fov = Mathf.Clamp(_fov * (scroll > 0f ? 0.92f : 1.08f), 12f, 100f);
            Quaternion rotation = Rotation;
            Vector3 move = Vector3.zero;
            if (keyboard.wKey.isPressed || keyboard.zKey.isPressed || keyboard.upArrowKey.isPressed) move += Vector3.forward;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) move += Vector3.back;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) move += Vector3.right;
            if (keyboard.aKey.isPressed || keyboard.qKey.isPressed || keyboard.leftArrowKey.isPressed) move += Vector3.left;
            if (keyboard.spaceKey.isPressed) move += Vector3.up;
            if (keyboard.leftCtrlKey.isPressed || keyboard.cKey.isPressed) move += Vector3.down;
            float speed = keyboard.leftShiftKey.isPressed ? 16f : keyboard.leftAltKey.isPressed ? 0.8f : 4f;
            _position += rotation * move * speed * Time.unscaledDeltaTime;
            if (keyboard.fKey.wasPressedThisFrame)
                SetFilter((_filter + 1) % FilterNames.Length);
            if (keyboard.hKey.wasPressedThisFrame)
                _help = !_help;
            if (keyboard.pKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || mouse.leftButton.wasPressedThisFrame)
                MelonLoader.MelonCoroutines.Start(Capture());
        }

        private static Quaternion Rotation => Quaternion.Euler(_pitch, _yaw, _roll);

        /// <summary>Development: photo mode entered, turned around with a filter, a photo taken, left.</summary>
        internal static IEnumerator DevShot()
        {
            Enter();
            if (!Active)
                yield break;
            _yaw += 180f;
            _position += Rotation * Vector3.back * 2f + Vector3.up * 1.5f;
            SetFilter(4);
            yield return new WaitForSecondsRealtime(1.5f);
            StellarDriveDemoTFAdvanced.Common.DevHarness.Screenshot("photomode_help");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Capture();
            yield return new WaitForSecondsRealtime(1f);
            SetFilter(0);
            ModMenu.Close();
        }

        /// <summary>Starts the photo mode (the camera item's click), when in game with no window open.</summary>
        public static void Start()
        {
            if (!Active && !ModMenu.IsOpen && GameServices.ShipsClient != null && !Cameras.CameraTablet.Open)
                Enter();
        }

        private static void Enter()
        {
            Camera camera = MainCamera.Get();
            if (camera == null)
                return;
            _position = camera.transform.position;
            Vector3 angles = camera.transform.rotation.eulerAngles;
            _yaw = angles.y;
            _pitch = angles.x > 180f ? angles.x - 360f : angles.x;
            _roll = 0f;
            _gameFov = _fov = camera.fieldOfView;
            Active = true;
            ModMenu.Open(Exit);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            // The game's interface (screen space canvases) goes while the mode lasts
            Hidden.Clear();
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (canvas != null && canvas.isRootCanvas && canvas.enabled && canvas.renderMode != RenderMode.WorldSpace)
                {
                    canvas.enabled = false;
                    Hidden.Add(canvas);
                }
            }
            SetFilter(_filter);
        }

        private static void Exit()
        {
            if (!Active)
                return;
            Active = false;
            foreach (Canvas canvas in Hidden)
                if (canvas != null)
                    canvas.enabled = true;
            Hidden.Clear();
            if (_volume != null)
                _volume.weight = 0f;
            SetFov(_gameFov);
        }

        /// <summary>Leaving the world ends the mode.</summary>
        public static void Reset()
        {
            if (Active)
                Exit();
        }

        private static IEnumerator Capture()
        {
            Capturing = true;
            yield return null;
            Directory.CreateDirectory(Folder);
            string path = Path.Combine(Folder, "photo_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
            ScreenCapture.CaptureScreenshot(path);
            yield return null;
            yield return null;
            Capturing = false;
            _toast = Loc.Pick("Photo enregistrée : ", "Photo saved: ", "Foto guardada: ") + Path.GetFileName(path);
            _toastUntil = Time.unscaledTime + 3f;
            TFMod.Log.Msg("photo mode: saved " + path);
        }

        // ---- The view: placed after the game places its cameras

        private static Camera _far;

        internal static void Place(PlayerCamera camera, PlayerDistantCamera distant)
        {
            camera.UpdateCamera(_position, Rotation);
            distant?.UpdateCamera(_position, Rotation);
            SetFov(_fov);
        }

        private static void SetFov(float fov)
        {
            Camera main = MainCamera.Get();
            if (main != null)
                main.fieldOfView = fov;
            if (_far == null)
                _far = Camera.allCameras.FirstOrDefault(c => c != main && c.transform.root.name.Contains("Distant"));
            if (_far != null)
                _far.fieldOfView = fov;
        }

        // ---- Filters: a global post-processing volume above the game's

        private static void SetFilter(int filter)
        {
            _filter = filter;
            if (_volume == null && !CreateVolume())
                return;
            _volume.weight = filter == 0 ? 0f : 1f;
            _color.saturation.Override(0f);
            _color.contrast.Override(0f);
            _color.postExposure.Override(0f);
            _color.colorFilter.Override(Color.white);
            _vignette.intensity.Override(0f);
            switch (filter)
            {
                case 1:
                    _color.saturation.Override(-100f);
                    _color.contrast.Override(18f);
                    break;
                case 2:
                    _color.saturation.Override(-100f);
                    _color.colorFilter.Override(new Color(1f, 0.85f, 0.64f));
                    _vignette.intensity.Override(0.3f);
                    break;
                case 3:
                    _color.saturation.Override(40f);
                    _color.contrast.Override(15f);
                    break;
                case 4:
                    _color.saturation.Override(-15f);
                    _color.contrast.Override(22f);
                    _color.colorFilter.Override(new Color(1f, 0.95f, 0.86f));
                    _vignette.intensity.Override(0.42f);
                    break;
                case 5:
                    _color.saturation.Override(-80f);
                    _color.postExposure.Override(1.6f);
                    _color.colorFilter.Override(new Color(0.55f, 1f, 0.55f));
                    _vignette.intensity.Override(0.5f);
                    break;
            }
        }

        private static bool CreateVolume()
        {
            Volume game = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None).FirstOrDefault(v => v != null && v.isGlobal);
            var host = new GameObject("TFA_PhotoVolume") { layer = game != null ? game.gameObject.layer : 0 };
            Object.DontDestroyOnLoad(host);
            _volume = host.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 1000f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _volume.sharedProfile = profile;
            _color = profile.Add<ColorAdjustments>(true);
            _vignette = profile.Add<Vignette>(true);
            _vignette.smoothness.Override(0.5f);
            return true;
        }

        // ---- On screen: a short help, hidden while the photo is taken

        private static GUIStyle _text;

        public static void Draw()
        {
            if (_toast != null && Time.unscaledTime < _toastUntil && !Capturing)
                DeviceUi.Toast(Screen.width, Screen.height, _toast);
            if (!Active || Capturing || !_help)
                return;
            if (_text == null)
                _text = new GUIStyle(DeviceUi.Text) { fontSize = 15, alignment = TextAnchor.UpperLeft, wordWrap = true, richText = true };
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.7f, 2.5f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            var rect = new Rect(24f, 24f, 440f, 170f);
            DeviceUi.Panel(rect);
            GUI.Label(new Rect(rect.x + 14f, rect.y + 10f, rect.width - 28f, rect.height - 20f),
                "<b>" + Loc.Pick("MODE PHOTO", "PHOTO MODE", "MODO FOTO") + "</b>\n" +
                Loc.Pick("Souris : regarder · ZQSD : bouger · Espace/Ctrl : monter/descendre\nMaj : vite · Alt : lent · Molette : zoom · R / T : pencher / redresser\nF : filtre (", "Mouse: look · WASD: move · Space/Ctrl: up/down\nShift: fast · Alt: slow · Wheel: zoom · R / T: tilt / level\nF: filter (", "Ratón: mirar · WASD: mover · Espacio/Ctrl: subir/bajar\nMayús: rápido · Alt: lento · Rueda: zoom · R / T: inclinar / nivelar\nF: filtro (") +
                FilterNames[_filter] + ")\n" +
                Loc.Pick("P, Entrée ou clic : photo · H : cacher l'aide · Échap : quitter", "P, Enter or click: photo · H: hide help · Esc: leave", "P, Intro o clic: foto · H: ocultar ayuda · Esc: salir"), _text);
            GUI.matrix = previous;
        }
    }

    /// <summary>After the game places the player's cameras, photo mode puts them where it flies.</summary>
    [HarmonyPatch(typeof(PlayerCamerasUpdater), nameof(PlayerCamerasUpdater.UpdateCameras))]
    internal static class PhotoModeCameraPatch
    {
        private static readonly AccessTools.FieldRef<PlayerCamerasUpdater, PlayerCamera> Camera =
            AccessTools.FieldRefAccess<PlayerCamerasUpdater, PlayerCamera>("_camera");
        private static readonly AccessTools.FieldRef<PlayerCamerasUpdater, PlayerDistantCamera> Distant =
            AccessTools.FieldRefAccess<PlayerCamerasUpdater, PlayerDistantCamera>("_distantCamera");

        // After the third person view's own postfix
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(PlayerCamerasUpdater __instance)
        {
            if (!PhotoMode.Active)
                return;
            PlayerCamera camera = Camera(__instance);
            if (camera != null)
                PhotoMode.Place(camera, Distant(__instance));
        }
    }
}

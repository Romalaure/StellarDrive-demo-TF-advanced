using System;
using System.Collections.Generic;
using System.Linq;
using Core.Services;
using HarmonyLib;
using Players.Interface.Services;
using Players.Inventory;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace StellarDriveDemoTFAdvanced.TFItems.Cameras
{
    /// <summary>
    /// The camera tablet: a key (F9) brings up a tablet on screen showing what a placed
    /// surveillance camera films. Left/right arrows switch camera, P picks the camera part in the
    /// build tool to place a new one, Z cycles small / large / full screen. Keyboard only, so the
    /// game keeps the mouse. One shared camera renders the feed, only while the tablet is open, at
    /// a capped rate, no wider than the screen shows it and, unless CameraSky is on, without the
    /// game's full screen atmosphere and cloud passes (they cost more than the scene itself).
    /// </summary>
    internal static class CameraTablet
    {
        private const string FeedCameraName = "TF_TabletCamera";

        public static bool Open { get; private set; }

        private static int _index;
        private static Camera _feed;
        private static RenderTexture _texture;
        private static float _lastRender = float.NegativeInfinity;
        private static string _message;
        private static float _messageUntil;

        private static readonly KeySetting TabletKey = new KeySetting(() => Settings.TabletKey);

        // Screen size of the image, in GUI units, from the last drawn frame
        private static float _shownWidth = 640f;
        private static float _guiScale = 1f;

        private static readonly AccessTools.FieldRef<ToolBelt, Core.Values.IntValue> SelectedTool =
            AccessTools.FieldRefAccess<ToolBelt, Core.Values.IntValue>("currentSelectedId");

        // ---- Input and feed ----

        public static void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;
            bool inWorld = GameServices.ShipsClient != null;
            if (!inWorld)
            {
                Close();
                return;
            }

            if (TabletKey.WasPressed)
            {
                if (Open)
                    Close();
                else
                    Open = true;
            }
            if (!Open)
                return;

            List<CameraPartVisuals> cameras = Cameras();
            if (keyboard.rightArrowKey.wasPressedThisFrame)
                _index++;
            if (keyboard.leftArrowKey.wasPressedThisFrame)
                _index--;
            if (cameras.Count > 0)
                _index = (_index % cameras.Count + cameras.Count) % cameras.Count;
            if (keyboard.escapeKey.wasPressedThisFrame)
                Close();
            else if (keyboard.pKey.wasPressedThisFrame)
                StartPlacing();
            else if (keyboard.zKey.wasPressedThisFrame)
                Settings.TabletSize.Value = (Mathf.Clamp(Settings.TabletSize.Value, 0, 2) + 1) % 3;
        }

        public static void LateUpdate()
        {
            bool render = false;
            CameraPartVisuals camera = Open ? Current() : null;
            if (camera != null && Time.unscaledTime - _lastRender >= 1f / Mathf.Clamp(Settings.CameraFps.Value, 1, 30))
            {
                Camera viewer = MainCamera.Get();
                if (viewer != null)
                {
                    EnsureFeed(viewer);
                    PlaceFeed(camera);
                    render = true;
                    _lastRender = Time.unscaledTime;
                }
            }
            // Enabled during LateUpdate, the camera renders this frame; otherwise the texture keeps its last image
            if (_feed != null && _feed.enabled != render)
                _feed.enabled = render;
            if (_feed != null && render)
                ApplyQuality();
        }

        // Without the sky passes the feed is a plain scene render: the game's atmosphere and cloud
        // features only run for cameras of the Game type
        private static void ApplyQuality()
        {
            CameraType type = Settings.CameraSky.Value ? CameraType.Game : CameraType.Reflection;
            if (_feed.cameraType != type)
                _feed.cameraType = type;
        }

        private static void Close()
        {
            Open = false;
            if (_feed != null)
                _feed.enabled = false;
        }

        private static List<CameraPartVisuals> Cameras() =>
            CameraPartVisuals.All
                .Where(c => c != null && c.HasContext && c.Eye != null)
                .OrderBy(c => c.Key.ShipId).ThenBy(c => c.Key.PartId)
                .ToList();

        private static CameraPartVisuals Current()
        {
            List<CameraPartVisuals> cameras = Cameras();
            return cameras.Count == 0 ? null : cameras[Mathf.Clamp(_index, 0, cameras.Count - 1)];
        }

        // Selects the camera part and takes out the build tool, like picking it in the build menu
        private static void StartPlacing()
        {
            if (!CameraParts.Registered)
            {
                ShowMessage("La caméra a besoin de SDModKit.");
                return;
            }
            var parts = ServiceLocator.GetService<ILocalSelectedPartProvider>();
            if (parts == null)
                return;
            parts.SelectPart(CameraParts.CameraId);
            ToolBelt belt = UnityEngine.Object.FindFirstObjectByType<ToolBelt>();
            if (belt != null && SelectedTool(belt).Get() != 1)
                belt.SelectTool(1);
            Close();
            ShowMessage("Caméra sélectionnée dans l'outil de construction : vise et clique pour la poser.");
        }

        private static void ShowMessage(string text)
        {
            _message = text;
            _messageUntil = Time.unscaledTime + 5f;
        }

        private static void PlaceFeed(CameraPartVisuals camera)
        {
            _feed.transform.SetPositionAndRotation(camera.Eye.position, camera.Eye.rotation);
        }

        // Places the feed again right before it renders, once ships have their final pose this frame
        private static void BeforeCameraRenders(ScriptableRenderContext context, Camera camera)
        {
            if (camera != _feed || camera == null)
                return;
            CameraPartVisuals current = Current();
            if (current != null)
                PlaceFeed(current);
        }

        private static readonly string[] HiddenLayers = { "UI", "WorldUI", "BuildToolRender", "PartThumbnailRender", "Id" };

        private static void EnsureFeed(Camera viewer)
        {
            // Never render more pixels than the tablet screen shows
            int shown = Mathf.RoundToInt(_shownWidth * _guiScale);
            int width = Mathf.Clamp(Mathf.Min(Settings.CameraResolution.Value, shown), 160, 2560) / 16 * 16;
            int height = width * 9 / 16;
            if (_texture == null || Mathf.Abs(_texture.width - width) > 32)
            {
                if (_texture != null)
                {
                    if (_feed != null)
                        _feed.targetTexture = null;
                    _texture.Release();
                    UnityEngine.Object.Destroy(_texture);
                }
                _texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "TF_TabletTexture", useMipMap = false };
                _texture.Create();
                if (_feed != null)
                    _feed.targetTexture = _texture;
            }
            if (_feed != null)
                return;

            var cameraObject = new GameObject(FeedCameraName);
            UnityEngine.Object.DontDestroyOnLoad(cameraObject);
            _feed = cameraObject.AddComponent<Camera>();
            _feed.CopyFrom(viewer);
            _feed.targetTexture = _texture;
            _feed.depth = viewer.depth - 1f;
            _feed.fieldOfView = 75f;
            _feed.nearClipPlane = 0.03f;
            _feed.farClipPlane = Mathf.Min(viewer.farClipPlane, 2000f);
            _feed.allowMSAA = false;
            _feed.allowHDR = false;
            _feed.useOcclusionCulling = false;
            _feed.enabled = false;
            int hidden = 0;
            foreach (string name in HiddenLayers)
            {
                int layer = LayerMask.NameToLayer(name);
                if (layer >= 0)
                    hidden |= 1 << layer;
            }
            _feed.cullingMask = viewer.cullingMask & ~hidden;
            var data = _feed.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                data.renderType = CameraRenderType.Base;
                data.renderPostProcessing = false;
                data.renderShadows = false;
                data.antialiasing = AntialiasingMode.None;
                data.requiresDepthOption = CameraOverrideOption.Off;
                data.requiresColorOption = CameraOverrideOption.Off;
            }
            RenderPipelineManager.beginCameraRendering += BeforeCameraRenders;
        }

        // ---- Drawing ----

        private static GUIStyle _bezel, _screen, _title, _text, _small, _hint;
        private static Texture2D _white;

        public static void Draw()
        {
            if (!Open && Time.unscaledTime >= _messageUntil)
                return;
            EnsureStyles();
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.7f, 2.5f);
            _guiScale = scale;
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;

            if (Open)
                DrawTablet(width, height);
            else
                DrawMessage(width, height);

            GUI.matrix = previous;
        }

        private static void DrawMessage(float width, float height)
        {
            var rect = new Rect((width - 560f) / 2f, height - 170f, 560f, 44f);
            GUI.Box(rect, GUIContent.none, _screen);
            GUI.Label(new Rect(rect.x + 16f, rect.y, rect.width - 32f, rect.height), _message, _text);
        }

        private static void DrawTablet(float width, float height)
        {
            // A landscape tablet: dark bezel around a 16:9 screen, at the bottom right; larger sizes
            // are centered, the last one fills the screen
            int size = Mathf.Clamp(Settings.TabletSize.Value, 0, 2);
            float border = size == 2 ? 10f : 22f;
            float screenWidth = size == 0 ? 640f
                : size == 1 ? Mathf.Min(width * 0.7f, (height - 160f) * 16f / 9f)
                : Mathf.Min(width - 20f, (height - 64f) * 16f / 9f);
            float screenHeight = screenWidth * 9f / 16f;
            float tabletWidth = screenWidth + border * 2f, tabletHeight = screenHeight + border * 2f + 34f;
            var tablet = size == 0
                ? new Rect(width - tabletWidth - 40f, height - tabletHeight - 62f, tabletWidth, tabletHeight)
                : new Rect((width - tabletWidth) / 2f, (height - tabletHeight) / 2f, tabletWidth, tabletHeight);
            _shownWidth = screenWidth;
            GUI.Box(tablet, GUIContent.none, _bezel);
            var screen = new Rect(tablet.x + border, tablet.y + border, screenWidth, screenHeight);
            GUI.Box(screen, GUIContent.none, _screen);

            List<CameraPartVisuals> cameras = Cameras();
            CameraPartVisuals current = Current();
            if (current != null && _texture != null)
            {
                GUI.DrawTexture(screen, _texture, ScaleMode.ScaleAndCrop, false);
                // On-screen overlay like a security monitor
                int number = cameras.IndexOf(current) + 1;
                Fill(new Rect(screen.x, screen.y, screen.width, 30f), new Color(0f, 0f, 0f, 0.45f));
                GUI.Label(new Rect(screen.x + 12f, screen.y + 4f, 300f, 22f), $"CAM {number:00} / {cameras.Count:00}", _title);
                bool blink = Time.unscaledTime % 1f < 0.6f;
                if (blink)
                    Fill(new Rect(screen.xMax - 70f, screen.y + 10f, 10f, 10f), new Color(1f, 0.15f, 0.1f));
                GUI.Label(new Rect(screen.xMax - 56f, screen.y + 4f, 50f, 22f), "REC", _title);
                GUI.Label(new Rect(screen.x + 12f, screen.yMax - 26f, 300f, 22f), DateTime.Now.ToString("HH:mm:ss"), _small);
            }
            else
            {
                GUI.Label(new Rect(screen.x, screen.y + screenHeight / 2f - 40f, screenWidth, 30f), "AUCUNE CAMÉRA", _text);
                GUI.Label(new Rect(screen.x + 40f, screen.y + screenHeight / 2f - 4f, screenWidth - 80f, 50f),
                    "Appuie sur P pour en poser une avec l'outil de construction (elle est aussi dans l'onglet TF).", _text);
            }

            GUI.Label(new Rect(tablet.x + border, screen.yMax + 8f, screenWidth, 24f),
                "<  >  changer de caméra     Z  " + (size == 2 ? "réduire" : "agrandir") + "     P  poser une caméra     " + TabletKey.Label + " / Échap  fermer", _hint);
            // Front camera dot and home button, for the tablet look
            Fill(new Rect(tablet.center.x - 3f, tablet.y + 8f, 6f, 6f), new Color(0.25f, 0.27f, 0.3f));
        }

        private static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _white);
            GUI.color = previous;
        }

        private static void EnsureStyles()
        {
            if (_bezel != null)
                return;
            _white = UiTextures.Solid(Color.white);
            _bezel = UiTextures.RoundedStyle(new Color(0.07f, 0.075f, 0.09f, 0.97f), 18);
            _screen = UiTextures.RoundedStyle(new Color(0.02f, 0.025f, 0.03f, 1f), 4);
            var text = new Color(0.93f, 0.94f, 0.96f);
            _title = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            _title.normal.textColor = text;
            _text = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true, alignment = TextAnchor.MiddleCenter };
            _text.normal.textColor = text;
            _small = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            _small.normal.textColor = new Color(0.85f, 0.9f, 0.85f);
            _hint = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
            _hint.normal.textColor = new Color(0.6f, 0.63f, 0.69f);
        }
    }
}

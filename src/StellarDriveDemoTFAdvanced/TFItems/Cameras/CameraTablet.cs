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
using Loc = StellarDriveDemoTFAdvanced.Common.Loc;

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
        // The game draws space, stars and planets with a second, "distant" camera under the main
        // one: the feed copies it too, as the base its own view is stacked on (else the sky is black)
        private static Camera _farFeed;
        private static Camera _far;
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

        /// <summary>Development: with a camera in DevShowcase, the tablet opened full screen and photographed.</summary>
        public static void Install()
        {
            StellarDriveDemoTFAdvanced.Common.DevHarness.Steps.Add((70f, "camera tablet", () =>
            {
                if (!("," + StellarDriveDemoTFAdvanced.Common.Settings.DevShowcase.Value.Replace(" ", "") + ",").Contains("," + CameraParts.CameraId + ","))
                    return;
                MelonLoader.MelonCoroutines.Start(DevShots());
            }));
        }

        private static bool _devMirror;

        private static System.Collections.IEnumerator DevShots()
        {
            Open = true;
            Settings.TabletSize.Value = 2;
            for (int i = 0; i < Cameras().Count && i < 3; i++)
            {
                _index = i;
                yield return new WaitForSecondsRealtime(2f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(StellarDriveDemoTFAdvanced.Common.DevHarness.Folder, $"camera_tablet_{i}.png"));
                yield return new WaitForSecondsRealtime(0.5f);
            }
            _devMirror = true;
            yield return new WaitForSecondsRealtime(1.5f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(StellarDriveDemoTFAdvanced.Common.DevHarness.Folder, "camera_tablet_mirror.png"));
            yield return new WaitForSecondsRealtime(0.5f);
            _devMirror = false;
            TFMod.Log.Msg($"dev: camera tablet: {Cameras().Count} camera(s), sky {Settings.CameraSky.Value}");
            Close();
        }

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
            if (_farFeed != null && _farFeed.enabled != render)
                _farFeed.enabled = render;
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
            if (_farFeed != null)
                _farFeed.enabled = false;
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
                ShowMessage(Loc.Pick("La caméra a besoin de SDModKit.", "The camera needs SDModKit.", "La cámara necesita SDModKit."));
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
            ShowMessage(Loc.Pick("Caméra sélectionnée dans l'outil de construction : vise et clique pour la poser.", "Camera selected in the build tool: aim and click to place it.", "Cámara seleccionada en la herramienta de construcción: apunta y haz clic para colocarla."));
        }

        private static void ShowMessage(string text)
        {
            _message = text;
            _messageUntil = Time.unscaledTime + 5f;
        }

        private static void PlaceFeed(CameraPartVisuals camera)
        {
            Camera main = MainCamera.Get();
            // (development: the player's own view, to compare with the screen)
            if (_devMirror && main != null)
                _feed.transform.SetPositionAndRotation(main.transform.position, main.transform.rotation);
            else
                _feed.transform.SetPositionAndRotation(camera.Eye.position, camera.Eye.rotation);
            if (_farFeed != null && _far != null && main != null)
                _farFeed.transform.SetPositionAndRotation(_far.transform.position, _far.transform.rotation * Quaternion.Inverse(main.transform.rotation) * _feed.transform.rotation);
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
                if (_farFeed != null)
                    _farFeed.targetTexture = _texture;
                else if (_feed != null)
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

            _far = Camera.allCameras.FirstOrDefault(c => c != viewer && c.transform.root.name.Contains("Distant"));
            var farData = _far != null ? _far.GetComponent<UniversalAdditionalCameraData>() : null;
            if (_far == null || farData == null || data == null)
                return;
            var farObject = new GameObject(FeedCameraName + "Far");
            UnityEngine.Object.DontDestroyOnLoad(farObject);
            _farFeed = farObject.AddComponent<Camera>();
            _farFeed.CopyFrom(_far);
            _farFeed.enabled = false;
            _farFeed.depth = _feed.depth - 1f;
            _farFeed.fieldOfView = _feed.fieldOfView;
            _farFeed.allowMSAA = false;
            _farFeed.allowHDR = false;
            var farFeedData = _farFeed.GetUniversalAdditionalCameraData();
            // The distant camera's own renderer: the default one leaves its layer out
            if (AccessTools.Field(farData.GetType(), "m_RendererIndex")?.GetValue(farData) is int renderer)
                farFeedData.SetRenderer(renderer);
            farFeedData.renderType = CameraRenderType.Base;
            farFeedData.renderPostProcessing = true;
            farFeedData.renderShadows = false;
            data.renderType = CameraRenderType.Overlay;
            _feed.targetTexture = null;
            farFeedData.cameraStack.Add(_feed);
            _farFeed.targetTexture = _texture;
            TFMod.Log.Msg($"camera tablet: sky from the distant camera {_far.name}");
            if (StellarDriveDemoTFAdvanced.Common.DevHarness.Active)
            {
                var viewerData = viewer.GetUniversalAdditionalCameraData();
                TFMod.Log.Msg($"dev: cameras: main {viewer.name} type {viewerData?.renderType} clear {viewer.clearFlags} depth {viewer.depth} target {viewer.targetTexture?.name} stack [{string.Join(",", viewerData?.cameraStack?.Select(c => c?.name) ?? new string[0])}]; far {_far.name} type {farData.renderType} clear {_far.clearFlags} depth {_far.depth} mask {_far.cullingMask} target {_far.targetTexture?.name} stack [{string.Join(",", farData.cameraStack?.Select(c => c?.name) ?? new string[0])}]; all: {string.Join("; ", Camera.allCameras.Select(c => c.name + " d" + c.depth + " " + c.GetUniversalAdditionalCameraData()?.renderType))}");
            }
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
            Devices.DeviceUi.Panel(tablet);
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
                GUI.Label(new Rect(screen.x, screen.y + screenHeight / 2f - 40f, screenWidth, 30f), Loc.Pick("AUCUNE CAMÉRA", "NO CAMERA", "NINGUNA CÁMARA"), _text);
                GUI.Label(new Rect(screen.x + 40f, screen.y + screenHeight / 2f - 4f, screenWidth - 80f, 50f),
                    Loc.Pick("Appuie sur P pour en poser une avec l'outil de construction (elle est aussi dans l'onglet TF).", "Press P to place one with the build tool (it is also in the TF tab).", "Pulsa P para colocar una con la herramienta de construcción (también está en la pestaña TF)."), _text);
            }

            GUI.Label(new Rect(tablet.x + border, screen.yMax + 8f, screenWidth, 24f),
                Loc.Pick("<  >  changer de caméra     Z  " + (size == 2 ? "réduire" : "agrandir") + "     P  poser une caméra     " + TabletKey.Label + " / Échap  fermer",
                    "<  >  switch camera     Z  " + (size == 2 ? "shrink" : "enlarge") + "     P  place a camera     " + TabletKey.Label + " / Esc  close",
                    "<  >  cambiar de cámara     Z  " + (size == 2 ? "reducir" : "ampliar") + "     P  colocar una cámara     " + TabletKey.Label + " / Esc  cerrar"), _hint);
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
            _bezel = UiTextures.FramedStyle(new Color(0.05f, 0.12f, 0.19f, 0.97f), new Color(0.62f, 0.66f, 0.7f, 1f), 18, 4);
            _screen = UiTextures.RoundedStyle(new Color(0.02f, 0.025f, 0.03f, 1f), 4);
            var text = new Color(0.93f, 0.94f, 0.96f);
            _title = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            if (Devices.DeviceUi.GameFont != null)
                _title.font = Devices.DeviceUi.GameFont;
            _title.normal.textColor = text;
            _text = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true, alignment = TextAnchor.MiddleCenter };
            if (Devices.DeviceUi.GameFont != null)
                _text.font = Devices.DeviceUi.GameFont;
            _text.normal.textColor = text;
            _small = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            if (Devices.DeviceUi.GameFont != null)
                _small.font = Devices.DeviceUi.GameFont;
            _small.normal.textColor = new Color(0.85f, 0.9f, 0.85f);
            _hint = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
            if (Devices.DeviceUi.GameFont != null)
                _hint.font = Devices.DeviceUi.GameFont;
            _hint.normal.textColor = new Color(0.6f, 0.63f, 0.69f);
        }
    }
}

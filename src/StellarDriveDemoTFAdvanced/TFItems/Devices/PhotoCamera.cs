using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Items.Model;
using Players.Inventory;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using UnityEngine;
using UnityEngine.InputSystem;
using Loc = StellarDriveDemoTFAdvanced.Common.Loc;

namespace StellarDriveDemoTFAdvanced.TFItems.Devices
{
    /// <summary>
    /// The camera (item 7418): held in the hand (selected in the toolbar), a left click starts the
    /// photo mode (the F8 key does too). Given by the tool rack, which shows one on its board.
    /// </summary>
    internal static class PhotoCamera
    {
        public const uint ItemId = 7418;

        private static ItemSettings _item;
        private static GameObject _holder;
        private static GUIStyle _hint;
        private static bool _holding;

        private static readonly AccessTools.FieldRef<ToolBelt, Core.Values.IntValue> SelectedTool =
            AccessTools.FieldRefAccess<ToolBelt, Core.Values.IntValue>("currentSelectedId");

        public static ItemSettings Item => _item ?? (_item = Create());

        public static string Name => Loc.Pick("Appareil photo", "Camera", "Cámara de fotos");

        /// <summary>The camera's shapes, lens toward +z, in meters (the rack hangs a copy).</summary>
        public static Dictionary<string, MeshBuilder> Shapes()
        {
            var body = new MeshBuilder();
            var dark = new MeshBuilder();
            var glass = new MeshBuilder();
            var light = new MeshBuilder();
            // Body with a grip on the right, a viewfinder hump on top
            body.Box(new Vector3(0f, 0.045f, 0f), new Vector3(0.14f, 0.09f, 0.05f));
            body.Box(new Vector3(0f, 0.1f, -0.002f), new Vector3(0.05f, 0.025f, 0.04f));
            dark.Box(new Vector3(0.055f, 0.045f, 0.012f), new Vector3(0.032f, 0.088f, 0.034f));
            dark.Box(new Vector3(0f, 0.045f, -0.026f), new Vector3(0.12f, 0.07f, 0.004f));
            // Lens: barrel, focus ring, glass
            dark.Cylinder(new Vector3(-0.012f, 0.045f, 0.025f), new Vector3(-0.012f, 0.045f, 0.065f), 0.03f, 0.028f, 20);
            body.Cylinder(new Vector3(-0.012f, 0.045f, 0.045f), new Vector3(-0.012f, 0.045f, 0.055f), 0.031f, 0.031f, 20);
            glass.Cylinder(new Vector3(-0.012f, 0.045f, 0.065f), new Vector3(-0.012f, 0.045f, 0.068f), 0.022f, 0.02f, 20);
            // Dials, shutter button, flash window and a small status light
            dark.Cylinder(new Vector3(-0.045f, 0.09f, 0f), new Vector3(-0.045f, 0.1f, 0f), 0.012f, 0.012f, 14);
            body.Cylinder(new Vector3(0.05f, 0.09f, 0.008f), new Vector3(0.05f, 0.097f, 0.008f), 0.007f, 0.007f, 12);
            glass.Box(new Vector3(0.035f, 0.075f, 0.026f), new Vector3(0.03f, 0.012f, 0.002f));
            light.Box(new Vector3(-0.055f, 0.075f, 0.026f), new Vector3(0.006f, 0.006f, 0.002f));
            return new Dictionary<string, MeshBuilder> { ["Body"] = body, ["Dark"] = dark, ["Glass"] = glass, ["Light"] = light };
        }

        public static Material Look(string part)
        {
            switch (part)
            {
                case "Body": return LitMaterials.Get("TF_CameraBody", new Color(0.72f, 0.74f, 0.78f), 0.5f, 0.4f);
                case "Dark": return LitMaterials.Get("TF_CameraDark", new Color(0.08f, 0.085f, 0.095f), 0.2f, 0.35f);
                case "Glass": return LitMaterials.Get("TF_CameraGlass", new Color(0.1f, 0.16f, 0.24f), 0.3f, 0.45f);
                default: return LitMaterials.Get("TF_CameraLight", new Color(1f, 0.3f, 0.2f), 0f, 0.1f, new Color(1f, 0.3f, 0.2f) * 0.8f);
            }
        }

        private static ItemSettings Create()
        {
            if (_holder == null)
            {
                // Inactive holder: the model is a template, cloned for thumbnails and dropped items
                _holder = new GameObject("TF_CameraModels");
                _holder.SetActive(false);
                Object.DontDestroyOnLoad(_holder);
            }
            var model = new GameObject("TF_Camera");
            model.transform.SetParent(_holder.transform, false);
            foreach (KeyValuePair<string, MeshBuilder> part in Shapes())
            {
                var child = new GameObject(part.Key);
                child.transform.SetParent(model.transform, false);
                child.AddComponent<MeshFilter>().sharedMesh = part.Value.Build("TF_Camera_" + part.Key);
                child.AddComponent<MeshRenderer>().sharedMaterial = Look(part.Key);
            }
            var item = ScriptableObject.CreateInstance<ItemSettings>();
            item.name = "TF_Camera";
            item.hideFlags = HideFlags.DontUnloadUnusedAsset;
            item.id = ItemId;
            item.itemName = Name;
            item.mass = 0.6f;
            item.maxStackSize = 1;
            item.itemObject = model;
            return item;
        }

        public static void Update()
        {
            _holding = IsHeld();
            if (_holding && !PhotoMode.Active && !ModMenu.IsOpen && !Cameras.CameraTablet.Open && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                PhotoMode.Start();
        }

        private static bool IsHeld()
        {
            if (GameServices.ShipsClient == null)
                return false;
            ToolBelt belt = GameServices.Belt;
            var toolbar = GameServices.Toolbar;
            if (belt == null || toolbar == null)
                return false;
            int selected = SelectedTool(belt).Get();
            if (selected < 1)
                return false;
            InventorySlot slot = toolbar.GetSlot((byte)(selected - 1));
            return slot.HasItem && slot.ItemId == ItemId;
        }

        public static void Draw()
        {
            if (!_holding || ModMenu.IsOpen || PhotoMode.Active)
                return;
            if (_hint == null)
                _hint = new GUIStyle(DeviceUi.Text) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.7f, 2.5f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale, height = Screen.height / scale;
            var rect = new Rect(width / 2f - 220f, height - 150f, 440f, 34f);
            DeviceUi.Panel(rect);
            GUI.Label(rect, Loc.Pick("Appareil photo  ·  clic gauche pour le mode photo", "Camera  ·  left click for the photo mode", "Cámara  ·  clic izquierdo para el modo foto"), _hint);
            GUI.matrix = previous;
        }
    }

    /// <summary>Adds the camera to the game's item list, on every machine with the mod.</summary>
    [HarmonyPatch(typeof(ItemSettingsList), "ResetDict")]
    internal static class PhotoCameraItemPatch
    {
        private static void Prefix(ref ItemSettings[] ___items)
        {
            if (___items == null || ___items.Any(i => i != null && i.id == PhotoCamera.ItemId))
                return;
            ___items = ___items.Concat(new[] { PhotoCamera.Item }).ToArray();
        }
    }
}

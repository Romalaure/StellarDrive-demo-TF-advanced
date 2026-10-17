using System.Linq;
using Core.Services;
using HarmonyLib;
using Items.Model;
using Players;
using Players.Inventory;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using UnityEngine;
using UnityEngine.InputSystem;
using Loc = StellarDriveDemoTFAdvanced.Common.Loc;

namespace StellarDriveDemoTFAdvanced.TFItems.Devices
{
    /// <summary>
    /// The schematic tablet (moved here from the TF mod, same item id 7201, so the tablets already
    /// in inventories and chests come back): an inventory item; held in the hand (selected in the
    /// toolbar), a left click opens the schematics window. Given by the tool rack.
    /// </summary>
    internal static class SchematicTablet
    {
        public const uint ItemId = 7201;

        private static ItemSettings _item;
        private static GameObject _holder;
        private static GUIStyle _hint;
        private static bool _holding;

        private static readonly AccessTools.FieldRef<ToolBelt, Core.Values.IntValue> SelectedTool =
            AccessTools.FieldRefAccess<ToolBelt, Core.Values.IntValue>("currentSelectedId");

        public static ItemSettings Item => _item ?? (_item = Create());

        private static ItemSettings Create()
        {
            if (_holder == null)
            {
                // Inactive holder: the model is a template, cloned for thumbnails and dropped items
                _holder = new GameObject("TF_ItemModels");
                _holder.SetActive(false);
                Object.DontDestroyOnLoad(_holder);
            }
            var model = new GameObject("TF_SchematicTablet");
            model.transform.SetParent(_holder.transform, false);
            var body = new MeshBuilder();
            var screen = new MeshBuilder();
            body.Box(new Vector3(0f, 0.008f, 0f), new Vector3(0.24f, 0.016f, 0.34f));
            screen.Box(new Vector3(0f, 0.0165f, 0.01f), new Vector3(0.21f, 0.002f, 0.28f));
            AddMesh(model, "Body", body, LitMaterials.Get("TF_TabletBody", new Color(0.1f, 0.11f, 0.13f), 0.2f, 0.8f));
            AddMesh(model, "Screen", screen, LitMaterials.Get("TF_TabletScreen", new Color(0.3f, 0.75f, 1f), 0f, 0.9f, new Color(0.25f, 0.65f, 1f) * 2f));

            var item = ScriptableObject.CreateInstance<ItemSettings>();
            item.name = "TF_SchematicTablet";
            item.hideFlags = HideFlags.DontUnloadUnusedAsset;
            item.id = ItemId;
            item.itemName = Loc.Pick("Tablette schématique", "Schematic tablet", "Tableta esquemática");
            item.mass = 1f;
            item.maxStackSize = 1;
            item.itemObject = model;
            return item;
        }

        private static void AddMesh(GameObject parent, string name, MeshBuilder shape, Material material)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = shape.Build("TF_Tablet_" + name);
            child.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        public static void Update()
        {
            _holding = IsHeld();
            if (_holding && !ModMenu.IsOpen && !Cameras.CameraTablet.Open && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                SchematicsMenu.Show();
        }

        private static bool IsHeld()
        {
            if (GameServices.ShipsClient == null)
                return false;
            ToolBelt belt = Object.FindFirstObjectByType<ToolBelt>();
            var toolbar = ServiceLocator.GetService<PlayerToolbarProvider>();
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
            if (!_holding || ModMenu.IsOpen)
                return;
            if (_hint == null)
                _hint = new GUIStyle(DeviceUi.Text) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.7f, 2.5f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale, height = Screen.height / scale;
            var rect = new Rect(width / 2f - 220f, height - 150f, 440f, 34f);
            // (made once: a style built here each frame left a texture behind every frame)
            DeviceUi.Panel(rect);
            GUI.Label(rect, Loc.Pick("Tablette schématique  ·  clic gauche pour l'ouvrir", "Schematic tablet  ·  left click to open it", "Tableta esquemática  ·  clic izquierdo para abrirla"), _hint);
            GUI.matrix = previous;
        }
    }

    /// <summary>Adds the schematic tablet to the game's item list, on every machine with the mod.</summary>
    [HarmonyPatch(typeof(ItemSettingsList), "ResetDict")]
    internal static class SchematicTabletItemPatch
    {
        private static void Prefix(ref ItemSettings[] ___items)
        {
            if (___items == null || ___items.Any(i => i != null && i.id == SchematicTablet.ItemId))
                return;
            ___items = ___items.Concat(new[] { SchematicTablet.Item }).ToArray();
        }
    }
}

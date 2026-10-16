using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Core.Services;
using FishNet.Connection;
using HarmonyLib;
using Items.Model;
using Players;
using Players.Inventory;
using Ships.Interface.Settings;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using UnityEngine;
using UnityEngine.InputSystem;
using Loc = StellarDriveDemoTFAdvanced.Common.Loc;

namespace StellarDriveDemoTFAdvanced.TFItems.Devices
{
    /// <summary>
    /// The tool rack: a wall panel holding a paint gun and a schematic tablet. Look at it and press
    /// the use key to take one of them, or one of the game's base tools if it was lost, as an item
    /// in your inventory (one of each per player).
    /// </summary>
    internal static class ToolRack
    {
        public const ushort RackId = 7175;

        /// <summary>The tools every player starts with (the game's default toolbar).</summary>
        public static readonly uint[] BaseTools = { 0u, 1u, 2u };

        private static readonly Vector3 BoundsCenter = new Vector3(0f, 0.06f, 0f);
        private static readonly Vector3 BoundsSize = new Vector3(0.9f, 0.12f, 0.6f);

        public static void Register()
        {
            Lights.LampParts.RegisterDonorPart(RackId, TFTab.DevicesRow, Configure, TFTab.ObjectsName);
            TFMod.Log.Msg("registered the tool rack");
        }

        /// <summary>Network handler; installed whether or not the part could be registered.</summary>
        public static void Install()
        {
            TFNet.OnServer(TFMessageKind.GiveTool, ServerGive);
        }

        private static void Configure(PartSettings settings, GameObject prefab)
        {
            Transform visuals = DeviceModels.Build(settings, prefab, Loc.Pick("Râtelier à outils", "Tool rack", "Soporte de herramientas"),
                Loc.Pick("Panneau à fixer au mur : regarde-le et appuie sur la touche d'utilisation (T) pour reprendre un outil de base perdu (et, avec le mod TF, un pistolet à peinture ou une tablette schématique).", "Wall panel: look at it and press the use key (T) to take back a lost base tool (and, with the TF mod, a paint gun or a schematic tablet).", "Panel de pared: míralo y pulsa la tecla de uso (T) para recuperar una herramienta básica perdida (y, con el mod TF, una pistola de pintura o una tableta esquemática)."),
                4f, new[] { (101u, 2), (110u, 2) }, BoundsCenter, BoundsSize, Shapes(), Materials());
            UsablePart.AddBox(visuals, BoundsCenter, BoundsSize + new Vector3(0f, 0.2f, 0f));
            visuals.gameObject.AddComponent<ToolRackVisuals>();
        }

        private static readonly Color Orange = new Color(0.95f, 0.45f, 0.1f);

        private static Dictionary<string, DeviceMaterial> Materials() => new Dictionary<string, DeviceMaterial>
        {
            ["TF_Rack_Board"] = new DeviceMaterial(new Color(0.24f, 0.27f, 0.3f), 0.2f, 0.35f),
            ["TF_NoPaint_RackHoles"] = new DeviceMaterial(new Color(0.05f, 0.055f, 0.06f), 0f, 0.1f),
            ["TF_NoPaint_RackMetal"] = new DeviceMaterial(new Color(0.75f, 0.77f, 0.8f), 1f, 0.8f),
            ["TF_NoPaint_RackAccent"] = new DeviceMaterial(Orange, 0.2f, 0.55f),
            ["TF_NoPaint_RackLetters"] = new DeviceMaterial(new Color(1f, 0.97f, 0.9f), 0f, 0.6f, new Color(1f, 0.9f, 0.75f) * 1.5f),
            ["TF_NoPaint_RackGunBody"] = new DeviceMaterial(new Color(0.72f, 0.75f, 0.79f), 0.55f, 0.55f),
            ["TF_NoPaint_RackGunDark"] = new DeviceMaterial(new Color(0.11f, 0.115f, 0.125f), 0.1f, 0.3f),
            ["TF_NoPaint_RackGunPaint"] = new DeviceMaterial(new Color(0.2f, 0.75f, 0.95f), 0.1f, 0.85f),
            ["TF_NoPaint_RackGunGlow"] = new DeviceMaterial(new Color(0.3f, 0.85f, 1f), 0f, 0.9f, new Color(0.3f, 0.85f, 1f) * 2f),
            ["TF_NoPaint_RackTablet"] = new DeviceMaterial(new Color(0.08f, 0.09f, 0.11f), 0.1f, 0.8f),
            ["TF_NoPaint_RackScreen"] = new DeviceMaterial(new Color(0.05f, 0.2f, 0.4f), 0f, 0.9f, new Color(0.08f, 0.3f, 0.6f)),
            ["TF_NoPaint_RackBlueprint"] = new DeviceMaterial(new Color(0.6f, 0.9f, 1f), 0f, 0.9f, new Color(0.5f, 0.85f, 1f) * 2.2f),
            ["TF_NoPaint_RackCanRed"] = new DeviceMaterial(new Color(0.85f, 0.12f, 0.1f), 0.3f, 0.7f),
            ["TF_NoPaint_RackCanYellow"] = new DeviceMaterial(new Color(0.95f, 0.78f, 0.1f), 0.3f, 0.7f),
            ["TF_NoPaint_RackCanGreen"] = new DeviceMaterial(new Color(0.15f, 0.7f, 0.3f), 0.3f, 0.7f)
        };

        // Lies on its mounting surface (+y out of it); seen on a wall, x is across and z is up.
        // A pegboard in a metal frame with an orange TF header, the paint gun hung on the left, the
        // schematic tablet in clips on the right and a shelf of paint cans along the bottom.
        internal static Dictionary<string, MeshBuilder> Shapes()
        {
            var shapes = new Dictionary<string, MeshBuilder>();
            MeshBuilder Of(string name)
            {
                if (!shapes.TryGetValue(name, out MeshBuilder builder))
                    shapes[name] = builder = new MeshBuilder();
                return builder;
            }
            MeshBuilder board = Of("TF_Rack_Board"), holes = Of("TF_NoPaint_RackHoles"), metal = Of("TF_NoPaint_RackMetal");
            MeshBuilder accent = Of("TF_NoPaint_RackAccent"), letters = Of("TF_NoPaint_RackLetters");

            // Board, metal frame and corner screws
            board.Box(new Vector3(0f, 0.01f, 0f), new Vector3(0.88f, 0.02f, 0.58f));
            metal.Box(new Vector3(0f, 0.015f, 0.29f), new Vector3(0.9f, 0.03f, 0.02f));
            metal.Box(new Vector3(0f, 0.015f, -0.29f), new Vector3(0.9f, 0.03f, 0.02f));
            metal.Box(new Vector3(0.44f, 0.015f, 0f), new Vector3(0.02f, 0.03f, 0.56f));
            metal.Box(new Vector3(-0.44f, 0.015f, 0f), new Vector3(0.02f, 0.03f, 0.56f));

            // Pegboard holes below the header
            for (int ix = 0; ix < 13; ix++)
            {
                for (int iz = 0; iz < 7; iz++)
                    holes.Box(new Vector3(-0.39f + ix * 0.065f, 0.0205f, -0.255f + iz * 0.065f), new Vector3(0.012f, 0.002f, 0.012f));
            }

            // Header: orange band with "TF" and two screws
            accent.Box(new Vector3(0f, 0.025f, 0.245f), new Vector3(0.86f, 0.012f, 0.07f));
            float ly = 0.0325f;
            // T
            letters.Box(new Vector3(-0.355f, ly, 0.262f), new Vector3(0.04f, 0.004f, 0.01f));
            letters.Box(new Vector3(-0.355f, ly, 0.24f), new Vector3(0.01f, 0.004f, 0.044f));
            // F
            letters.Box(new Vector3(-0.31f, ly, 0.245f), new Vector3(0.01f, 0.004f, 0.044f));
            letters.Box(new Vector3(-0.292f, ly, 0.262f), new Vector3(0.036f, 0.004f, 0.01f));
            letters.Box(new Vector3(-0.297f, ly, 0.244f), new Vector3(0.026f, 0.004f, 0.009f));
            // Small white line after the letters, like a label strip
            letters.Box(new Vector3(0.05f, ly, 0.245f), new Vector3(0.5f, 0.003f, 0.006f));
            foreach (float x in new[] { -0.41f, 0.41f })
                metal.Cylinder(new Vector3(x, 0.03f, 0.245f), new Vector3(x, 0.036f, 0.245f), 0.009f, 0.009f, 10);

            // Paint gun hung on its side, nozzle to the left, gauge facing out
            // (gun x -> out of the wall reversed, gun y -> up the wall, gun z -> left), scaled 1.05
            var gunMatrix = new Matrix4x4(new Vector4(0f, -1.05f, 0f, 0f), new Vector4(0f, 0f, 1.05f, 0f),
                new Vector4(-1.05f, 0f, 0f, 0f), new Vector4(-0.21f, 0.068f, -0.03f, 1f));
            foreach (KeyValuePair<Paint.PaintGunModel.Part, MeshBuilder> part in Paint.PaintGunModel.BuildShapes())
            {
                string key;
                switch (part.Key)
                {
                    case Paint.PaintGunModel.Part.Body: key = "TF_NoPaint_RackGunBody"; break;
                    case Paint.PaintGunModel.Part.Dark: key = "TF_NoPaint_RackGunDark"; break;
                    case Paint.PaintGunModel.Part.Accent: key = "TF_NoPaint_RackAccent"; break;
                    case Paint.PaintGunModel.Part.Chrome: key = "TF_NoPaint_RackMetal"; break;
                    case Paint.PaintGunModel.Part.Paint: key = "TF_NoPaint_RackGunPaint"; break;
                    default: key = "TF_NoPaint_RackGunGlow"; break;
                }
                Of(key).Append(part.Value, gunMatrix);
            }
            // Two pegs holding it, under the barrel and the handle
            foreach (Vector3 peg in new[] { new Vector3(-0.27f, 0f, 0.045f), new Vector3(-0.16f, 0f, -0.145f) })
                metal.Cylinder(new Vector3(peg.x, 0.02f, peg.z), new Vector3(peg.x, 0.1f, peg.z), 0.006f, 0.006f, 8);

            // Schematic tablet in three clips, a ship blueprint glowing on its screen
            MeshBuilder tablet = Of("TF_NoPaint_RackTablet"), screen = Of("TF_NoPaint_RackScreen"), blueprint = Of("TF_NoPaint_RackBlueprint");
            const float tx = 0.23f, tz = 0.02f, ty = 0.04f;
            tablet.Box(new Vector3(tx, ty, tz), new Vector3(0.26f, 0.016f, 0.34f));
            screen.Box(new Vector3(tx, ty + 0.0085f, tz + 0.005f), new Vector3(0.226f, 0.002f, 0.29f));
            float by = ty + 0.0105f;
            // Hull outline: nose, two sides, stern
            blueprint.Box(new Vector3(tx, by, tz + 0.1f), new Vector3(0.05f, 0.002f, 0.005f));
            // Bow lines, turned 30 degrees about the screen normal
            const float cos = 0.866f, sin = 0.5f;
            blueprint.Box(new Vector3(tx - 0.045f, by, tz + 0.075f), new Vector3(0.005f, 0.002f, 0.05f), new Vector3(cos, 0f, -sin), Vector3.up, new Vector3(sin, 0f, cos));
            blueprint.Box(new Vector3(tx + 0.045f, by, tz + 0.075f), new Vector3(0.005f, 0.002f, 0.05f), new Vector3(cos, 0f, sin), Vector3.up, new Vector3(-sin, 0f, cos));
            blueprint.Box(new Vector3(tx - 0.058f, by, tz - 0.01f), new Vector3(0.005f, 0.002f, 0.13f));
            blueprint.Box(new Vector3(tx + 0.058f, by, tz - 0.01f), new Vector3(0.005f, 0.002f, 0.13f));
            blueprint.Box(new Vector3(tx, by, tz - 0.075f), new Vector3(0.12f, 0.002f, 0.005f));
            // Wings, engines and a grid line across the screen
            blueprint.Box(new Vector3(tx - 0.08f, by, tz - 0.03f), new Vector3(0.045f, 0.002f, 0.005f));
            blueprint.Box(new Vector3(tx + 0.08f, by, tz - 0.03f), new Vector3(0.045f, 0.002f, 0.005f));
            blueprint.Box(new Vector3(tx - 0.03f, by, tz - 0.09f), new Vector3(0.02f, 0.002f, 0.025f));
            blueprint.Box(new Vector3(tx + 0.03f, by, tz - 0.09f), new Vector3(0.02f, 0.002f, 0.025f));
            blueprint.Box(new Vector3(tx, by, tz + 0.03f), new Vector3(0.09f, 0.002f, 0.002f));
            blueprint.Box(new Vector3(tx, by, tz - 0.02f), new Vector3(0.002f, 0.002f, 0.17f));
            // Clips: two under the tablet, one on top
            metal.Box(new Vector3(tx - 0.08f, 0.04f, tz - 0.178f), new Vector3(0.03f, 0.04f, 0.012f));
            metal.Box(new Vector3(tx + 0.08f, 0.04f, tz - 0.178f), new Vector3(0.03f, 0.04f, 0.012f));
            metal.Box(new Vector3(tx, 0.04f, tz + 0.178f), new Vector3(0.05f, 0.04f, 0.012f));

            // Shelf along the bottom with three paint cans
            metal.Box(new Vector3(0f, 0.055f, -0.262f), new Vector3(0.84f, 0.09f, 0.008f));
            metal.Box(new Vector3(0f, 0.1f, -0.25f), new Vector3(0.84f, 0.006f, 0.03f));
            string[] cans = { "TF_NoPaint_RackCanRed", "TF_NoPaint_RackCanYellow", "TF_NoPaint_RackCanGreen" };
            for (int i = 0; i < cans.Length; i++)
            {
                float x = 0.12f + i * 0.09f;
                Of(cans[i]).Cylinder(new Vector3(x, 0.055f, -0.258f), new Vector3(x, 0.055f, -0.19f), 0.03f, 0.03f, 16);
                metal.Cylinder(new Vector3(x, 0.055f, -0.19f), new Vector3(x, 0.055f, -0.182f), 0.031f, 0.026f, 16);
            }
            return shapes;
        }

        public static void RequestTool(uint itemId) => TFNet.SendToServer(TFMessageKind.GiveTool, itemId, 0, "");

        // Host: a base tool (build tool, recycler, mining laser) for the player who asked, if they lack it
        private static void ServerGive(NetworkConnection sender, TFMessageKind kind, uint itemId, ushort b, string text)
        {
            if (sender == null || (System.Array.IndexOf(BaseTools, itemId) < 0 && !(itemId == SchematicTablet.ItemId && TFItemsMod.ToolsActive)))
                return;
            TrackedPlayerServer player = GameServices.PlayersServer?.GetTrackedPlayerFromConnectionId(sender.ClientId);
            if (player == null)
                return;
            var drop = new ItemDrop { ItemId = itemId, Quantity = 1 };
            if (!player.HasItemsInInventory(new[] { drop }))
                player.AddItemToInventory(drop);
        }
    }

    /// <summary>
    /// The paint gun and the schematic tablet belong to the TF mod (its paint and schematics):
    /// when it is installed, the rack asks it for them, by name.
    /// </summary>
    internal static class TfTools
    {
        private static bool _resolved;
        private static MethodInfo _request, _findPaint;
        private static FieldInfo _tabletId;

        public static bool Available
        {
            get
            {
                if (!_resolved)
                {
                    _resolved = true;
                    try
                    {
                        _request = AccessTools.Method(AccessTools.TypeByName("StellarDriveDemoTF.Devices.ToolRack"), "RequestTool");
                        _findPaint = AccessTools.Method(AccessTools.TypeByName("StellarDriveDemoTF.Paint.PaintToolGiver"), "FindPaintToolItem");
                        Type tablet = AccessTools.TypeByName("StellarDriveDemoTF.Devices.SchematicTablet");
                        _tabletId = tablet != null ? AccessTools.Field(tablet, "ItemId") : null;
                    }
                    catch (Exception)
                    {
                        _request = null;
                    }
                }
                return _request != null;
            }
        }

        public static bool HasTablet => Available && _tabletId != null;

        public static void PaintGun()
        {
            object item = _findPaint?.Invoke(null, null);
            uint id = item is ItemSettings settings ? settings.id : 3u;
            _request.Invoke(null, new object[] { id });
        }

        public static void Tablet() => _request.Invoke(null, new object[] { Convert.ToUInt32(_tabletId.GetValue(null)) });
    }

    internal sealed class ToolRackVisuals : UsablePart
    {
        public override string Label => Loc.Pick(Loc.Pick("Râtelier à outils", "Tool rack", "Soporte de herramientas"), "Tool rack", "Soporte de herramientas");

        public override void Use() => ToolRackMenu.Show();
    }

    internal static class ToolRackMenu
    {
        private static bool _open;

        public static void Show()
        {
            _open = true;
            ModMenu.Open(() => _open = false);
        }

        public static void Draw()
        {
            if (!_open || !ModMenu.IsOpen)
                return;
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.7f, 2.5f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            Rect area = DeviceUi.Window(Screen.width / scale, Screen.height / scale, 480f, 520f, Loc.Pick("RÂTELIER À OUTILS", "TOOL RACK", "SOPORTE DE HERRAMIENTAS"));
            GUILayout.BeginArea(area);
            GUILayout.Label(Loc.Pick("Prends un outil : il va dans ton inventaire (un de chaque par joueur).", "Take a tool: it goes to your inventory (one of each per player).", "Coge una herramienta: va a tu inventario (una de cada por jugador)."), DeviceUi.Text);
            GUILayout.Space(10f);
            if (TfTools.Available)
            {
                if (GUILayout.Button(Loc.Pick("Pistolet à peinture  (mod TF)", "Paint gun  (TF mod)", "Pistola de pintura  (mod TF)"), DeviceUi.Button, GUILayout.Height(44f)))
                {
                    TfTools.PaintGun();
                    ModMenu.Close();
                }
                GUILayout.Space(6f);
                if (TfTools.HasTablet && GUILayout.Button(Loc.Pick("Tablette schématique  (mod TF)", "Schematic tablet  (TF mod)", "Tableta esquemática  (mod TF)"), DeviceUi.Button, GUILayout.Height(44f)))
                {
                    TfTools.Tablet();
                    ModMenu.Close();
                }
                GUILayout.Space(12f);
            }
            else if (TFItemsMod.ToolsActive)
            {
                if (GUILayout.Button(Loc.Pick("Tablette schématique", "Schematic tablet", "Tableta esquemática"), DeviceUi.Button, GUILayout.Height(44f)))
                {
                    ToolRack.RequestTool(SchematicTablet.ItemId);
                    ModMenu.Close();
                }
                GUILayout.Space(12f);
            }
            GUILayout.Label(Loc.Pick("Outils de base (si tu les as perdus) :", "Base tools (if you lost them):", "Herramientas básicas (si las perdiste):"), DeviceUi.Text);
            GUILayout.Space(4f);
            foreach (uint tool in ToolRack.BaseTools)
            {
                if (GUILayout.Button(ToolName(tool), DeviceUi.Button, GUILayout.Height(40f)))
                {
                    ToolRack.RequestTool(tool);
                    ModMenu.Close();
                }
                GUILayout.Space(4f);
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(Loc.Pick("Fermer (Échap)", "Close (Esc)", "Cerrar (Esc)"), DeviceUi.Button, GUILayout.Height(34f)))
                ModMenu.Close();
            GUILayout.EndArea();
            GUI.matrix = previous;
        }

        private static readonly Dictionary<uint, string> Names = new Dictionary<uint, string>();

        // The game's own item name (build tool, recycler, mining laser)
        private static string ToolName(uint itemId)
        {
            if (!Names.TryGetValue(itemId, out string name))
            {
                name = Resources.FindObjectsOfTypeAll<ItemSettings>().FirstOrDefault(i => i != null && i.id == itemId)?.itemName;
                if (string.IsNullOrEmpty(name))
                    return Loc.Pick("Outil ", "Tool ", "Herramienta ") + itemId;
                Names[itemId] = name;
            }
            return name;
        }
    }
}

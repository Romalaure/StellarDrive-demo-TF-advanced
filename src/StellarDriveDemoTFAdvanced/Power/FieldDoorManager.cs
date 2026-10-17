using System.Collections.Generic;
using Ships.Interface.Model.Joints;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.State;
using Ships.Interface.Settings;
using StellarDriveDemoTFAdvanced.Parts;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using StellarDriveDemoTFAdvanced.TFItems.Devices;
using UnityEngine;
using Loc = StellarDriveDemoTFAdvanced.Common.Loc;

namespace StellarDriveDemoTFAdvanced.Power
{
    /// <summary>
    /// The force field door manager: a console that lists every force field door of its ship
    /// (and what is docked to it), how many there are and how many are up, and switches them
    /// on and off, one by one or all at once. No cable: it sees the whole ship.
    /// </summary>
    internal static class FieldDoorManager
    {
        public const ushort Id = 7340;

        public static void Configure(PartSettings settings, GameObject prefab)
        {
            PartFactory.Describe(settings, Loc.Pick("Gestionnaire de portes à champ de force", "Force field door manager", "Gestor de puertas de campo de fuerza"),
                Loc.Pick("Une console qui montre toutes les portes à champ de force du vaisseau (et de ce qui y est amarré) : combien il y en a, lesquelles sont actives, et permet de les allumer ou de les couper une par une ou toutes ensemble. Regarde-la et appuie sur la touche d'utilisation (T). Pas besoin de câble.",
                    "A console showing every force field door of the ship (and of what is docked to it): how many there are, which ones are up, and switching them on or off one by one or all together. Look at it and press the use key (T). No cable needed.",
                    "Una consola que muestra todas las puertas de campo de fuerza de la nave (y de lo que está acoplado a ella): cuántas hay, cuáles están activas, y permite encenderlas o apagarlas una a una o todas a la vez. Mírala y pulsa la tecla de uso (T). No hace falta cable."),
                25f, new[] { (101u, 4), (102u, 3), (112u, 4), (Items.ModItems.Titanium, 1) });
            const float width = 0.7f, height = 1.25f, depth = 0.45f;
            Transform visuals = PartFactory.BlankFromSignalDisplay(prefab, new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, depth));
            MachineModels.Attach(visuals, Model(width, depth), "DoorManager", MachineModels.Cyan, new Color(0.75f, 0.95f, 1f));
            UsablePart.AddBox(visuals, new Vector3(0f, height * 0.5f, 0f), new Vector3(width + 0.1f, height, depth + 0.1f));
            visuals.gameObject.AddComponent<FieldDoorManagerVisuals>();
        }

        // A pedestal with a slanted screen on top: lines of door "cards", a header bar and two buttons
        private static MachineModels.Model Model(float width, float depth)
        {
            var m = new MachineModels.Model();
            // Base plate and column
            m.Armor.Box(new Vector3(0f, 0.025f, 0f), new Vector3(width * 0.8f, 0.05f, depth));
            m.Armor.Block(new[]
            {
                new Vector3(-0.16f, 0.05f, -0.14f), new Vector3(0.16f, 0.05f, -0.14f), new Vector3(-0.12f, 0.85f, -0.08f), new Vector3(0.12f, 0.85f, -0.08f),
                new Vector3(-0.16f, 0.05f, 0.14f), new Vector3(0.16f, 0.05f, 0.14f), new Vector3(-0.12f, 0.85f, 0.1f), new Vector3(0.12f, 0.85f, 0.1f)
            });
            m.Light.Box(new Vector3(0f, 0.45f, 0.13f), new Vector3(0.012f, 0.6f, 0.01f));
            m.Dark.Box(new Vector3(0f, 0.3f, 0.152f), new Vector3(0.18f, 0.22f, 0.01f));
            for (int i = 0; i < 4; i++)
                m.Glow.Box(new Vector3(0f, 0.22f + i * 0.05f, 0.158f), new Vector3(0.14f, 0.01f, 0.004f));
            // The slanted screen: a frame, tilted 30° back, facing +z
            Quaternion tilt = Quaternion.Euler(-30f, 0f, 0f);
            var center = new Vector3(0f, 1.0f, 0.02f);
            Vector3 right = tilt * Vector3.right, up = tilt * Vector3.up, normal = tilt * Vector3.forward;
            m.Armor.Box(center, new Vector3(width, 0.46f, 0.06f), right, up, normal);
            m.Dark.Box(center + normal * 0.031f, new Vector3(width - 0.06f, 0.4f, 0.004f), right, up, normal);
            // Header bar, then door rows: a status light and a bar each
            m.Core.Box(center + normal * 0.034f + up * 0.16f, new Vector3(width - 0.12f, 0.03f, 0.003f), right, up, normal);
            for (int r = 0; r < 4; r++)
            {
                Vector3 row = center + normal * 0.034f + up * (0.08f - r * 0.065f);
                m.Glow.Box(row - right * (width * 0.5f - 0.08f), new Vector3(0.025f, 0.025f, 0.003f), right, up, normal);
                m.Light.Box(row + right * 0.02f, new Vector3(width * 0.55f - r * 0.04f, 0.014f, 0.003f), right, up, normal);
            }
            // Side handles and the hinge under the screen
            foreach (float s in new[] { -1f, 1f })
                m.Armor.Box(center + right * s * (width * 0.5f + 0.015f), new Vector3(0.03f, 0.3f, 0.05f), right, up, normal);
            m.Armor.Cylinder(new Vector3(-0.14f, 0.82f, 0f), new Vector3(0.14f, 0.82f, 0f), 0.04f, 0.04f, 10);
            return m;
        }

        /// <summary>The doors of the ship (and docked ships) a part is on, as this game sees them.</summary>
        public static List<(PartKey Key, FieldDoor.Config Config)> Doors(uint shipId)
        {
            var doors = new List<(PartKey, FieldDoor.Config)>();
            var ships = StellarDriveDemoTFAdvanced.Common.GameServices.ShipsClientProvider;
            if (ships == null || !ships.TryGetRootShip(shipId, out IShipStateRead root))
                return doors;
            foreach (RootRelativeShipData data in ships.GetRootConnectedShips(root.Id))
            {
                if (data.Ship == null)
                    continue;
                foreach (StatefulPart part in data.Ship.StatefulParts)
                {
                    if (part?.Settings != null && part.Settings.id == FieldDoor.Id)
                        doors.Add((new PartKey(data.Ship.Id, part.Id), FieldDoor.Decode(part.State)));
                }
            }
            return doors;
        }
    }

    internal sealed class FieldDoorManagerVisuals : UsablePart
    {
        public override string Label => Loc.Pick("Gestionnaire de portes", "Door manager", "Gestor de puertas");

        public override void Use() => FieldDoorManagerMenu.Show(Key);
    }

    /// <summary>The manager's window: the doors, their state, a switch each and all at once.</summary>
    internal static class FieldDoorManagerMenu
    {
        private static bool _open;
        private static PartKey _console;
        private static Vector2 _scroll;

        public static void Show(PartKey console)
        {
            _console = console;
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
            float screenWidth = Screen.width / scale, screenHeight = Screen.height / scale;
            List<(PartKey Key, FieldDoor.Config Config)> doors = FieldDoorManager.Doors(_console.ShipId);
            int up = doors.FindAll(d => d.Config.Sealed).Count, on = doors.FindAll(d => d.Config.On).Count;
            Rect area = DeviceUi.Window(screenWidth, screenHeight, 720f, 640f, Loc.Pick("GESTIONNAIRE DE PORTES À CHAMP DE FORCE", "FORCE FIELD DOOR MANAGER", "GESTOR DE PUERTAS DE CAMPO DE FUERZA"));
            GUILayout.BeginArea(area);
            GUILayout.Label(Loc.Pick($"{doors.Count} porte(s) connectée(s) au vaisseau, {up} active(s), {on} allumée(s).", $"{doors.Count} door(s) connected to the ship, {up} up, {on} switched on.", $"{doors.Count} puerta(s) conectada(s) a la nave, {up} activa(s), {on} encendida(s)."), DeviceUi.Text);
            GUILayout.Space(8f);
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            if (doors.Count == 0)
                GUILayout.Label(Loc.Pick("Aucune porte à champ de force sur ce vaisseau.", "No force field door on this ship.", "Ninguna puerta de campo de fuerza en esta nave."), DeviceUi.Muted);
            for (int i = 0; i < doors.Count; i++)
            {
                (PartKey key, FieldDoor.Config config) = doors[i];
                GUILayout.BeginHorizontal();
                Rect light = GUILayoutUtility.GetRect(14f, 30f, GUILayout.Width(14f));
                DeviceUi.Fill(new Rect(light.x, light.y + 8f, 12f, 12f), config.Sealed ? FieldDoorVisuals.ColorOf(key) : config.On ? new Color(1f, 0.55f, 0.05f) : new Color(0.3f, 0.32f, 0.36f));
                GUILayout.Label(Loc.Pick($"Porte {i + 1}", $"Door {i + 1}", $"Puerta {i + 1}"), DeviceUi.Text, GUILayout.Width(80f));
                GUILayout.Label($"{config.SignedWidth} × {config.SignedHeight} m{(config.Solid ? Loc.Pick(", solide", ", solid", ", sólida") : "")}", DeviceUi.Muted, GUILayout.Width(130f));
                GUILayout.Label(Short(config), DeviceUi.Muted, GUILayout.ExpandWidth(true));
                if (GUILayout.Button(config.Solid ? Loc.Pick("Solide", "Solid", "Sólida") : Loc.Pick("Traversable", "Walk-through", "Atravesable"), config.Solid ? DeviceUi.Selected : DeviceUi.Button, GUILayout.Width(120f), GUILayout.Height(30f)))
                    FieldDoor.Request(key, WithSolid(config, !config.Solid), config.On);
                if (GUILayout.Button(config.On ? Loc.Pick("Couper", "Off", "Apagar") : Loc.Pick("Activer", "On", "Activar"), config.On ? DeviceUi.Selected : DeviceUi.Button, GUILayout.Width(90f), GUILayout.Height(30f)))
                    FieldDoor.Request(key, config, !config.On);
                GUILayout.EndHorizontal();
                GUILayout.Space(3f);
            }
            GUILayout.EndScrollView();
            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Loc.Pick("Tout activer", "All on", "Activar todas"), DeviceUi.Button, GUILayout.Height(36f)))
                foreach ((PartKey key, FieldDoor.Config config) in doors)
                    if (!config.On)
                        FieldDoor.Request(key, config, true);
            if (GUILayout.Button(Loc.Pick("Tout couper", "All off", "Apagar todas"), DeviceUi.Button, GUILayout.Height(36f)))
                foreach ((PartKey key, FieldDoor.Config config) in doors)
                    if (config.On)
                        FieldDoor.Request(key, config, false);
            if (GUILayout.Button(Loc.Pick("Fermer (Échap)", "Close (Esc)", "Cerrar (Esc)"), DeviceUi.Button, GUILayout.Height(36f)))
                ModMenu.Close();
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Loc.Pick("Tout solide", "All solid", "Todas sólidas"), DeviceUi.Button, GUILayout.Height(32f)))
                foreach ((PartKey key, FieldDoor.Config config) in doors)
                    if (!config.Solid)
                        FieldDoor.Request(key, WithSolid(config, true), config.On);
            if (GUILayout.Button(Loc.Pick("Tout traversable", "All walk-through", "Todas atravesables"), DeviceUi.Button, GUILayout.Height(32f)))
                foreach ((PartKey key, FieldDoor.Config config) in doors)
                    if (config.Solid)
                        FieldDoor.Request(key, WithSolid(config, false), config.On);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            GUI.matrix = previous;
        }

        private static FieldDoor.Config WithSolid(FieldDoor.Config config, bool solid) =>
            new FieldDoor.Config(config.SignedWidth, config.SignedHeight, config.Status, config.Side, solid);

        private static string Short(FieldDoor.Config config)
        {
            switch (config.Status)
            {
                case FieldDoor.Status.Up: return Loc.Pick("active", "up", "activa");
                case FieldDoor.Status.Starting: return Loc.Pick("démarrage", "starting", "arrancando");
                case FieldDoor.Status.NoNetwork: return Loc.Pick("pas de réseau", "no grid", "sin red");
                case FieldDoor.Status.LowPower: return Loc.Pick("pas assez de courant", "not enough power", "energía insuficiente");
                default: return Loc.Pick("éteinte", "off", "apagada");
            }
        }
    }
}

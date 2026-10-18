using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Core.Services;
using FishNet.Connection;
using FishNet.Transporting;
using HarmonyLib;
using Items.Model;
using Players;
using Players.Interface.Model;
using Players.Inventory;
using Players.Network.Services;
using Ships;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Placement;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using UnityEngine;
using UnityEngine.InputSystem;
using WorldTracking.Interface.Model;
using WorldTracking.Interface.Services;
using Loc = StellarDriveDemoTFAdvanced.Common.Loc;

namespace StellarDriveDemoTFAdvanced.TFItems.Devices
{
    /// <summary>
    /// The teleporter: an item no recipe makes (it is only given, with a give command). Held in
    /// the hand, a left click opens its list: every planet (set down on dry land, on the day
    /// side), the stations and every other beacon (set down beside the beacon, in its ship).
    /// The host makes the list and moves the player.
    /// </summary>
    internal static class Teleporter
    {
        public const uint ItemId = 7419;

        private static ItemSettings _item;
        private static GameObject _holder;
        private static bool _holding;

        private static readonly AccessTools.FieldRef<ToolBelt, Core.Values.IntValue> SelectedTool =
            AccessTools.FieldRefAccess<ToolBelt, Core.Values.IntValue>("currentSelectedId");
        private static readonly System.Reflection.MethodInfo NotifyRespawnAtShip = AccessTools.Method(typeof(PlayerRespawnService), "NotifyRespawnAtShip");

        public static ItemSettings Item => _item ?? (_item = Create());

        private static ItemSettings Create()
        {
            if (_holder == null)
            {
                _holder = new GameObject("TF_TeleporterModels");
                _holder.SetActive(false);
                Object.DontDestroyOnLoad(_holder);
            }
            var model = new GameObject("TF_Teleporter");
            model.transform.SetParent(_holder.transform, false);
            var body = new MeshBuilder();
            var glow = new MeshBuilder();
            var dark = new MeshBuilder();
            // A handheld beacon: a grip, a round head with a glowing ring and a lens
            body.Cylinder(new Vector3(0f, 0f, 0f), new Vector3(0f, 0.16f, 0f), 0.028f, 0.032f, 14);
            body.Cylinder(new Vector3(0f, 0.16f, 0f), new Vector3(0f, 0.2f, 0f), 0.06f, 0.07f, 20);
            dark.Cylinder(new Vector3(0f, 0.2f, 0f), new Vector3(0f, 0.215f, 0f), 0.07f, 0.06f, 20);
            glow.Cylinder(new Vector3(0f, 0.185f, 0f), new Vector3(0f, 0.19f, 0f), 0.072f, 0.072f, 20, false, false);
            glow.Cylinder(new Vector3(0f, 0.215f, 0f), new Vector3(0f, 0.25f, 0f), 0.03f, 0f, 8);
            for (int i = 0; i < 3; i++)
                dark.Box(new Vector3(0f, 0.04f + i * 0.04f, 0.03f), new Vector3(0.03f, 0.015f, 0.006f));
            AddMesh(model, "Body", body, LitMaterials.Get("TF_TeleporterBody", new Color(0.88f, 0.9f, 0.93f), 0.4f, 0.75f));
            AddMesh(model, "Dark", dark, LitMaterials.Get("TF_TeleporterDark", new Color(0.08f, 0.09f, 0.11f), 0.4f, 0.7f));
            AddMesh(model, "Glow", glow, LitMaterials.Get("TF_TeleporterGlow", new Color(0.55f, 0.35f, 1f), 0f, 0.9f, new Color(0.55f, 0.3f, 1f) * 2.5f));

            var item = ScriptableObject.CreateInstance<ItemSettings>();
            item.name = "TF_Teleporter";
            item.hideFlags = HideFlags.DontUnloadUnusedAsset;
            item.id = ItemId;
            item.itemName = Loc.Pick("Téléporteur", "Teleporter", "Teletransportador");
            item.mass = 1f;
            item.maxStackSize = 1;
            item.itemObject = model;
            return item;
        }

        private static void AddMesh(GameObject parent, string name, MeshBuilder shape, Material material)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = shape.Build("TF_Teleporter_" + name);
            child.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        public static void Install()
        {
            StellarDriveDemoTFAdvanced.Common.DevHarness.Steps.Add((60f, "teleporter test", () =>
            {
                if (StellarDriveDemoTFAdvanced.Common.Settings.DevTeleporterTest.Value)
                    MelonLoader.MelonCoroutines.Start(DevTest());
            }));
            TFNet.OnServer(TFMessageKind.TeleporterList, (sender, kind, a, b, text) => TFNet.SendTo(sender, TFMessageKind.TeleporterDestinations, 0, 0, ServerList()));
            TFNet.OnServer(TFMessageKind.TeleporterGo, ServerGo);
            TFNet.OnClient(TFMessageKind.TeleporterDestinations, (kind, a, b, text) => TeleporterMenu.Destinations = Parse(text));
        }

        public static void Update()
        {
            _holding = IsHeld();
            if (_holding && !ModMenu.IsOpen && !Cameras.CameraTablet.Open && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                TeleporterMenu.Show();
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

        public static void DrawHint()
        {
            if (!_holding || ModMenu.IsOpen)
                return;
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.7f, 2.5f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale, height = Screen.height / scale;
            var rect = new Rect(width / 2f - 220f, height - 150f, 440f, 36f);
            DeviceUi.Panel(rect);
            GUI.Label(rect, Loc.Pick("Téléporteur  ·  clic gauche pour choisir où aller", "Teleporter  ·  left click to choose where to go", "Teletransportador  ·  clic izquierdo para elegir destino"), Hint);
            GUI.matrix = previous;
        }

        private static GUIStyle _hint;
        private static GUIStyle Hint => _hint ?? (_hint = new GUIStyle(DeviceUi.Text) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false });

        // ---- Destinations: "kind|a|b|name" lines; kind p (planet a), s (station: ship a, part b), b (beacon)

        internal sealed class Destination
        {
            public char Kind;
            public uint A;
            public ushort B;
            public string Name;
        }

        private static List<Destination> Parse(string text)
        {
            var list = new List<Destination>();
            foreach (string line in (text ?? "").Split('\n'))
            {
                string[] f = line.Split(new[] { '|' }, 4);
                if (f.Length == 4 && f[0].Length == 1 && uint.TryParse(f[1], out uint a) && ushort.TryParse(f[2], out ushort b))
                    list.Add(new Destination { Kind = f[0][0], A = a, B = b, Name = f[3] });
            }
            return list;
        }

        // Host: the planets, the stations and the other beacons
        private static string ServerList()
        {
            var text = new StringBuilder();
            foreach (Planets.Planet planet in Object.FindObjectsByType<Planets.Planet>(FindObjectsSortMode.None).OrderBy(p => p.Id))
            {
                if (planet == null || planet.Radius <= 0f)
                    continue;
                text.Append("p|").Append(planet.Id.ToString(CultureInfo.InvariantCulture)).Append("|0|").Append(Clean(Worlds.PlanetCatalog.All.FirstOrDefault(s => s.Id == planet.Id)?.Name ?? planet.name)).Append('\n');
            }
            var ships = GameServices.ShipsServer;
            if (ships != null)
            {
                var stations = new HashSet<string>(Worlds.AbandonedStations.All.Select(s => s.Name));
                foreach (TrackedShipServer ship in ships.Ships)
                {
                    foreach (StatefulPart part in ship.GetAllStatefulParts<BeaconState>())
                    {
                        string name = ((BeaconState)part.State).Name;
                        if (string.IsNullOrEmpty(name))
                            continue;
                        text.Append(stations.Contains(name) ? "s|" : "b|").Append(ship.Id).Append('|').Append(part.Id).Append('|').Append(Clean(name)).Append('\n');
                    }
                }
            }
            return text.ToString();
        }

        private static System.Collections.IEnumerator DevTest()
        {
            List<Destination> all = Parse(ServerList());
            TFMod.Log.Msg($"dev: teleporter: {all.Count} destinations: " + string.Join("; ", all.Select(d => d.Kind + " " + d.Name)));
            TeleporterMenu.Show();
            yield return new WaitForSecondsRealtime(1.5f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(StellarDriveDemoTFAdvanced.Common.DevHarness.Folder, "teleporter_menu.png"));
            yield return new WaitForSecondsRealtime(1f);
            ModMenu.Close();
            yield return StellarDriveDemoTFAdvanced.Common.FpsProbe.Measure("before teleport");
            int planet = StellarDriveDemoTFAdvanced.Common.Settings.DevVisitPlanetId.Value;
            foreach (Destination target in new[] { all.FirstOrDefault(d => d.Kind == 'p' && d.A == (planet != 0 ? planet : 108)), all.FirstOrDefault(d => d.Kind == 's') })
            {
                if (target == null)
                    continue;
                Go(target);
                // The frame rate on arrival (a player saw 1 fps on TF Gaia)
                for (int i = 0; i < 12; i++)
                    yield return StellarDriveDemoTFAdvanced.Common.FpsProbe.Measure($"after teleport to {target.Name} {i * 4}-{i * 4 + 4} s");
                var player = GameServices.PlayersServer?.ActivePlayers.FirstOrDefault();
                var spaces = ServiceLocator.GetService<ISpacesServerProvider>();
                TFMod.Log.Msg($"dev: teleporter: went to {target.Name}: player space {player?.SpaceId} {(player != null && spaces != null ? spaces.GetSpace(player.SpaceId)?.ToString() : "")}");
                StellarDriveDemoTFAdvanced.Common.DevHarness.Screenshot("teleporter_" + target.Kind);
                if (target.Kind == 'p')
                    DumpTerrainShader();
            }
        }

        // Development: what the planet terrain's shader takes
        private static void DumpTerrainShader()
        {
            var chunk = Object.FindObjectsByType<Planets.Terrain.Meshes.PlanetChunkMeshObject>(FindObjectsSortMode.None).FirstOrDefault();
            Material material = chunk != null ? chunk.GetComponent<MeshRenderer>().sharedMaterial : null;
            if (material?.shader == null)
                return;
            var text = new System.Text.StringBuilder();
            text.AppendLine($"shader {material.shader.name} keywords [{string.Join(" ", material.shaderKeywords)}]");
            for (int i = 0; i < material.shader.GetPropertyCount(); i++)
            {
                string name = material.shader.GetPropertyName(i);
                var type = material.shader.GetPropertyType(i);
                string value = type == UnityEngine.Rendering.ShaderPropertyType.Texture ? material.GetTexture(name)?.name
                    : type == UnityEngine.Rendering.ShaderPropertyType.Color || type == UnityEngine.Rendering.ShaderPropertyType.Vector ? material.GetVector(name).ToString()
                    : material.GetFloat(name).ToString(System.Globalization.CultureInfo.InvariantCulture);
                text.AppendLine($"{name} ({type}) = {value}");
            }
            foreach (string keyword in material.shader.keywordSpace.keywordNames)
                text.AppendLine("keyword " + keyword);
            System.IO.File.WriteAllText(System.IO.Path.Combine(StellarDriveDemoTFAdvanced.Common.DevHarness.Folder, "terrain_shader.txt"), text.ToString());
        }

        private static string Clean(string name) => (name ?? "").Replace("\n", " ").Replace("|", "/");

        /// <summary>Asks the host to go to a destination.</summary>
        public static void Go(Destination destination) =>
            TFNet.SendToServer(TFMessageKind.TeleporterGo, destination.A, destination.B, destination.Kind.ToString());

        private static void ServerGo(NetworkConnection sender, TFMessageKind kind, uint a, ushort b, string text)
        {
            if (sender == null || string.IsNullOrEmpty(text))
                return;
            TrackedPlayerServer player = GameServices.PlayersServer?.GetTrackedPlayerFromConnectionId(sender.ClientId);
            if (player == null)
                return;
            PlayerStateType state = player.StateVal;
            if (state != PlayerStateType.Walking && state != PlayerStateType.CheatFlying)
                return;
            if (text == "p")
            {
                Planets.Planet planet = Object.FindObjectsByType<Planets.Planet>(FindObjectsSortMode.None).FirstOrDefault(p => p.Id == a);
                if (planet == null)
                    return;
                Worlds.PlanetVisit.TryFindLand(planet, out Vector3 up);
                Worlds.PlanetVisit.MoveConnection(planet, up, Quaternion.identity, 1.5f, sender);
                TFMod.Log.Msg($"teleporter: {player.Name} to {planet.name}");
                return;
            }
            var ships = ServiceLocator.GetService<Ships.Interface.Services.IShipsServerProvider>();
            var spaces = ServiceLocator.GetService<ISpacesServerProvider>();
            if (ships == null || spaces == null || !ships.TryGetShip(a, out Ships.Interface.Model.State.IShipStateRead ship) || !ship.TryGetPartPlacement(b, out IPartPlacement placement))
                return;
            // Beside the beacon, a little above the floor it stands on
            var physics = new LocalPhysicsState
            {
                Position = placement.LocalPosition + placement.Rotation * new Vector3(0f, 1.2f, -1.4f),
                Rotation = placement.Rotation,
                Velocity = Vector3.zero
            };
            spaces.SetWorldObjectPhysics(player, ship.RelativeSpaceId, physics);
            Quaternion look = Quaternion.LookRotation(placement.Rotation * Vector3.forward, placement.Rotation * Vector3.up);
            PlayerRespawnService respawn = Object.FindFirstObjectByType<PlayerRespawnService>();
            if (respawn != null && NotifyRespawnAtShip != null)
                NotifyRespawnAtShip.Invoke(respawn, new object[] { sender, player.PhysicsState, look, Channel.Reliable });
            TFMod.Log.Msg($"teleporter: {player.Name} to ship {a} beacon {b}");
        }
    }

    /// <summary>The teleporter's window: planets, stations, beacons.</summary>
    internal static class TeleporterMenu
    {
        public static List<Teleporter.Destination> Destinations = new List<Teleporter.Destination>();
        private static bool _open;
        private static int _tab;
        private static Vector2 _scroll;

        public static void Show()
        {
            _open = true;
            Destinations = new List<Teleporter.Destination>();
            TFNet.SendToServer(TFMessageKind.TeleporterList, 0, 0, "");
            ModMenu.Open(() => _open = false);
        }

        public static void Draw()
        {
            if (!_open || !ModMenu.IsOpen)
                return;
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.7f, 2.5f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale, height = Screen.height / scale;
            Rect area = DeviceUi.Window(width, height, 620f, 640f, Loc.Pick("TÉLÉPORTEUR", "TELEPORTER", "TELETRANSPORTADOR"));
            GUILayout.BeginArea(area);
            GUILayout.BeginHorizontal();
            string[] tabs = { Loc.Pick("Planètes", "Planets", "Planetas"), Loc.Pick("Stations", "Stations", "Estaciones"), Loc.Pick("Balises", "Beacons", "Balizas") };
            for (int i = 0; i < tabs.Length; i++)
                if (GUILayout.Button(tabs[i], i == _tab ? DeviceUi.Selected : DeviceUi.Button, GUILayout.Height(32f)))
                    _tab = i;
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
            char kind = _tab == 0 ? 'p' : _tab == 1 ? 's' : 'b';
            List<Teleporter.Destination> shown = Destinations.Where(d => d.Kind == kind).ToList();
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            if (shown.Count == 0)
                GUILayout.Label(Destinations.Count == 0 ? Loc.Pick("Recherche des destinations…", "Looking for destinations…", "Buscando destinos…") : Loc.Pick("Aucune destination ici.", "No destination here.", "Ningún destino aquí."), DeviceUi.Muted);
            foreach (Teleporter.Destination destination in shown)
            {
                if (GUILayout.Button(destination.Name, DeviceUi.Button, GUILayout.Height(34f)))
                {
                    Teleporter.Go(destination);
                    ModMenu.Close();
                }
                GUILayout.Space(3f);
            }
            GUILayout.EndScrollView();
            GUILayout.Label(Loc.Pick("Planètes : posé sur la terre ferme, côté jour. Stations et balises : à côté de la balise.", "Planets: set down on dry land, on the day side. Stations and beacons: beside the beacon.", "Planetas: en tierra firme, del lado de día. Estaciones y balizas: junto a la baliza."), DeviceUi.Muted);
            if (GUILayout.Button(Loc.Pick("Fermer (Échap)", "Close (Esc)", "Cerrar (Esc)"), DeviceUi.Button, GUILayout.Height(36f)))
                ModMenu.Close();
            GUILayout.EndArea();
            GUI.matrix = previous;
        }
    }

    /// <summary>Adds the teleporter to the game's item list (no recipe: only given).</summary>
    [HarmonyPatch(typeof(ItemSettingsList), "ResetDict")]
    internal static class TeleporterItemPatch
    {
        private static void Prefix(ref ItemSettings[] ___items)
        {
            if (___items == null || ___items.Any(i => i != null && i.id == Teleporter.ItemId))
                return;
            ___items = ___items.Concat(new[] { Teleporter.Item }).ToArray();
        }
    }
}

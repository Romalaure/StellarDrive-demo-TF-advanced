using System.Collections.Generic;
using System.Linq;
using Core.Services;
using FishNet.Connection;
using FishNet.Transporting;
using HarmonyLib;
using Players;
using Players.Interface.Model;
using Players.Network.Services;
using Ships;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Placement;
using Ships.Interface.Model.State;
using Ships.Interface.Services;
using Ships.Interface.Settings;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using UnityEngine;
using WorldTracking.Interface.Model;
using WorldTracking.Interface.Services;
using Loc = StellarDriveDemoTFAdvanced.Common.Loc;

namespace StellarDriveDemoTFAdvanced.TFItems.Devices
{
    /// <summary>
    /// The teleport capsule (model and animation in <see cref="CapsuleModel"/>). Look at it and press the use
    /// key to pick another capsule (on this ship or any other) and be sent there. It only works
    /// with at least two capsules in the world. The host moves the player, the same way the game
    /// respawns a player at a ship.
    /// </summary>
    internal static class TeleportCapsule
    {
        public const ushort CapsuleId = 7151;
        public const int MinCapsules = 2;

        private static readonly Vector3 BoundsCenter = new Vector3(0f, 1.15f, 0f);
        private static readonly Vector3 BoundsSize = new Vector3(1.3f, 2.3f, 1.3f);

        public static void Register()
        {
            Lights.LampParts.RegisterDonorPart(CapsuleId, TFTab.DevicesRow, Configure, TFTab.ObjectsName);
            TFMod.Log.Msg("registered the teleport capsule");
        }

        /// <summary>Capsule names chosen by players, kept by the host and saved with the world.</summary>
        internal static readonly Dictionary<PartKey, string> ServerNames = new Dictionary<PartKey, string>();
        internal static readonly Dictionary<PartKey, string> ClientNames = new Dictionary<PartKey, string>();

        public const int MaxNameLength = 32;

        /// <summary>Network handlers; installed whether or not the part could be registered.</summary>
        public static void Install()
        {
            TFNet.OnServer(TFMessageKind.Teleport, ServerTeleport);
            TFNet.OnClient(TFMessageKind.TeleportRefused, (kind, a, b, text) => TeleportMenu.Refused(text));
            TFNet.OnServer(TFMessageKind.CapsuleName, ServerRename);
            TFNet.OnClient(TFMessageKind.CapsuleName, (kind, ship, part, name) =>
            {
                var key = new PartKey(ship, part);
                if (string.IsNullOrEmpty(name))
                    ClientNames.Remove(key);
                else
                    ClientNames[key] = name;
            });
            TFNet.SyncRequested += connection =>
            {
                foreach (KeyValuePair<PartKey, string> entry in ServerNames.ToList())
                    TFNet.SendTo(connection, TFMessageKind.CapsuleName, entry.Key.ShipId, entry.Key.PartId, entry.Value);
            };
            TFNet.Disconnected += () => ClientNames.Clear();
        }

        /// <summary>Letters, digits, spaces and common punctuation, at most 32 characters.</summary>
        public static string CleanName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "";
            var clean = new System.Text.StringBuilder();
            foreach (char c in name.Trim())
            {
                if (clean.Length >= MaxNameLength)
                    break;
                if (!char.IsControl(c) && c != '<' && c != '>')
                    clean.Append(c);
            }
            return clean.ToString().Trim();
        }

        public static string NameOf(PartKey capsule, int number) =>
            ClientNames.TryGetValue(capsule, out string name) && name.Length > 0 ? name : "Capsule " + number;

        private static void ServerRename(NetworkConnection sender, TFMessageKind kind, uint shipId, ushort partId, string text)
        {
            var ships = ServiceLocator.GetService<IShipsServerProvider>();
            if (ships == null || !ships.TryGetShip(shipId, out IShipStateRead ship) || !ship.TryGetStatefulPart(partId, out StatefulPart part)
                || part.Settings == null || part.Settings.id != CapsuleId)
                return;
            string name = CleanName(text);
            var key = new PartKey(shipId, partId);
            if (name.Length == 0)
                ServerNames.Remove(key);
            else
                ServerNames[key] = name;
            TFNet.SendToAll(TFMessageKind.CapsuleName, shipId, partId, name);
        }

        private static void Configure(PartSettings settings, GameObject prefab)
        {
            Transform visuals = DeviceModels.Build(settings, prefab, Loc.Pick("Capsule de téléportation", "Teleport capsule", "Cápsula de teletransporte"),
                Loc.Pick("Entre dans la capsule, regarde-la et appuie sur la touche d'utilisation (T) pour choisir une autre capsule et t'y téléporter, sur ce vaisseau ou un autre. Il faut au moins deux capsules.", "Step into the capsule, look at it and press the use key (T) to pick another capsule and teleport there, on this ship or another. Needs at least two capsules.", "Entra en la cápsula, mírala y pulsa la tecla de uso (T) para elegir otra cápsula y teletransportarte allí, en esta nave o en otra. Hacen falta al menos dos cápsulas."),
                60f, new[] { (101u, 10), (112u, 6), (102u, 4), (103u, 2) }, BoundsCenter, BoundsSize, Shapes(), CapsuleModel.Materials());
            DeviceModels.FlattenColliders(prefab, new Vector3(1.2f, 0.1f, 1.2f));
            UsablePart.AddBox(visuals, BoundsCenter, BoundsSize);
            visuals.gameObject.AddComponent<TeleportCapsuleVisuals>();
            visuals.gameObject.AddComponent<CapsuleAnimation>();
        }

        internal static Dictionary<string, MeshBuilder> Shapes() => CapsuleModel.Shapes();

        /// <summary>Every capsule this client knows about, in a stable order.</summary>
        public static List<PartKey> KnownCapsules()
        {
            var result = new List<PartKey>();
            var ships = GameServices.ShipsClient;
            if (ships == null)
                return result;
            foreach (TrackedShipClient ship in ships.Ships)
            {
                foreach (StatefulPart part in ship.StatefulParts)
                {
                    if (part.Settings != null && part.Settings.id == CapsuleId)
                        result.Add(new PartKey(ship.Id, part.Id));
                }
            }
            return result.OrderBy(k => k.ShipId).ThenBy(k => k.PartId).ToList();
        }

        // ---- Server ----

        private static readonly System.Reflection.MethodInfo NotifyRespawnAtShip =
            AccessTools.Method(typeof(PlayerRespawnService), "NotifyRespawnAtShip");

        private static void ServerTeleport(NetworkConnection sender, TFMessageKind kind, uint shipId, ushort partId, string text)
        {
            if (sender == null)
                return;
            TrackedPlayerServer player = GameServices.PlayersServer?.GetTrackedPlayerFromConnectionId(sender.ClientId);
            if (player == null)
                return;
            PlayerStateType state = player.StateVal;
            if (state != PlayerStateType.Walking && state != PlayerStateType.CheatFlying)
            {
                Refuse(sender, "hands");
                return;
            }

            var ships = ServiceLocator.GetService<IShipsServerProvider>();
            var shipsTracker = GameServices.ShipsServer;
            var spaces = ServiceLocator.GetService<ISpacesServerProvider>();
            if (ships == null || shipsTracker == null || spaces == null)
                return;
            int capsules = shipsTracker.Ships.Sum(s => s.StatefulParts.Count(p => p.Settings != null && p.Settings.id == CapsuleId));
            if (capsules < MinCapsules)
            {
                Refuse(sender, "two");
                return;
            }
            if (!ships.TryGetShip(shipId, out IShipStateRead ship) || !ship.TryGetStatefulPart(partId, out StatefulPart part)
                || part.Settings == null || part.Settings.id != CapsuleId || !ship.TryGetPartPlacement(partId, out IPartPlacement placement))
            {
                Refuse(sender, "gone");
                return;
            }

            // Standing on the capsule's pad, facing out of its door
            var physics = new LocalPhysicsState
            {
                Position = placement.LocalPosition + placement.Rotation * (Vector3.up * 1.05f),
                Rotation = placement.Rotation,
                Velocity = Vector3.zero
            };
            spaces.SetWorldObjectPhysics(player, ship.RelativeSpaceId, physics);
            Quaternion look = Quaternion.LookRotation(placement.Rotation * Vector3.back, placement.Rotation * Vector3.up);

            PlayerRespawnService respawn = Object.FindFirstObjectByType<PlayerRespawnService>();
            if (respawn == null || NotifyRespawnAtShip == null)
            {
                TFMod.Log.Warning("teleport: the respawn service is missing, the player may not see the move");
                return;
            }
            NotifyRespawnAtShip.Invoke(respawn, new object[] { sender, player.PhysicsState, look, Channel.Reliable });
        }

        private static void Refuse(NetworkConnection sender, string reason) =>
            TFNet.SendTo(sender, TFMessageKind.TeleportRefused, 0, 0, reason);
    }

    internal sealed class TeleportCapsuleVisuals : UsablePart
    {
        public override string Label => Loc.Pick("Capsule de téléportation", "Teleport capsule", "Cápsula de teletransporte");

        public override void Use() => TeleportMenu.Show(Key);
    }

    /// <summary>The capsule's destination list.</summary>
    internal static class TeleportMenu
    {
        private static bool _open;
        private static PartKey _from;
        private static Vector2 _scroll;
        private static string _message;
        private static float _messageUntil;
        private static float _flashUntil;
        private static string _nameField = "";

        public static void Show(PartKey from)
        {
            _from = from;
            TeleportCapsule.ClientNames.TryGetValue(from, out string current);
            _nameField = current ?? "";
            _open = true;
            _scroll = Vector2.zero;
            ModMenu.Open(() => _open = false);
        }

        public static void Refused(string reason)
        {
            switch (reason)
            {
                case "hands": reason = Loc.Pick("Lâche ce que tu tiens avant de te téléporter.", "Put down what you hold before teleporting.", "Suelta lo que llevas antes de teletransportarte."); break;
                case "two": reason = Loc.Pick("Il faut au moins deux capsules pour se téléporter.", "Teleporting needs at least two capsules.", "Hacen falta al menos dos cápsulas para teletransportarse."); break;
                case "gone": reason = Loc.Pick("Cette capsule n'existe plus.", "This capsule no longer exists.", "Esta cápsula ya no existe."); break;
            }
            _message = reason;
            _messageUntil = Time.unscaledTime + 5f;
            _flashUntil = 0f;
        }

        public static void Draw()
        {
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.7f, 2.5f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale, height = Screen.height / scale;

            if (Time.unscaledTime < _flashUntil)
            {
                // Teleport flash: bright cyan fading out
                float t = (_flashUntil - Time.unscaledTime) / 0.8f;
                DeviceUi.Fill(new Rect(0f, 0f, width, height), new Color(0.75f, 0.95f, 1f, t * 0.85f));
            }
            if (_open && ModMenu.IsOpen)
                DrawWindow(width, height);
            else if (Time.unscaledTime < _messageUntil)
                DeviceUi.Toast(width, height, _message);
            GUI.matrix = previous;
        }

        private static void DrawWindow(float width, float height)
        {
            List<PartKey> capsules = TeleportCapsule.KnownCapsules();
            Rect area = DeviceUi.Window(width, height, 540f, 560f, Loc.Pick("CAPSULE DE TÉLÉPORTATION", "TELEPORT CAPSULE", "CÁPSULA DE TELETRANSPORTE"));
            GUILayout.BeginArea(area);
            GUILayout.Label(Loc.Pick("NOM DE CETTE CAPSULE", "NAME OF THIS CAPSULE", "NOMBRE DE ESTA CÁPSULA"), DeviceUi.Muted);
            GUILayout.BeginHorizontal();
            _nameField = GUILayout.TextField(_nameField ?? "", TeleportCapsule.MaxNameLength, DeviceUi.Field, GUILayout.Height(34f), GUILayout.ExpandWidth(true));
            if (GUILayout.Button(Loc.Pick("Renommer", "Rename", "Renombrar"), DeviceUi.Button, GUILayout.Width(110f), GUILayout.Height(34f)))
            {
                _nameField = TeleportCapsule.CleanName(_nameField);
                TFNet.SendToServer(TFMessageKind.CapsuleName, _from.ShipId, _from.PartId, _nameField);
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(12f);
            if (capsules.Count < TeleportCapsule.MinCapsules)
            {
                GUILayout.Label(Loc.Pick("Il faut au moins deux capsules pour se téléporter.", "Teleporting needs at least two capsules.", "Hacen falta al menos dos cápsulas para teletransportarse."), DeviceUi.Text);
                GUILayout.Space(6f);
                GUILayout.Label(Loc.Pick("Pose une autre capsule (onglet TF, ligne Équipements) sur ce vaisseau ou un autre, puis reviens ici.", "Place another capsule (TF Équipement tab) on this ship or another, then come back here.", "Coloca otra cápsula (pestaña TF Équipement) en esta nave o en otra, y vuelve aquí."), DeviceUi.Muted);
            }
            else
            {
                GUILayout.Label(Loc.Pick("Choisis ta destination :", "Pick your destination:", "Elige tu destino:"), DeviceUi.Text);
                GUILayout.Space(6f);
                _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(290f));
                int number = 0;
                foreach (PartKey capsule in capsules)
                {
                    number++;
                    bool here = capsule.Equals(_from);
                    string where = capsule.ShipId == _from.ShipId ? Loc.Pick("ce vaisseau", "this ship", "esta nave") : Loc.Pick("vaisseau n°", "ship #", "nave n.º") + capsule.ShipId;
                    string label = $"{TeleportCapsule.NameOf(capsule, number)}   ·   {where}" + (here ? Loc.Pick("   ·   tu es ici", "   ·   you are here", "   ·   estás aquí") : "");
                    GUI.enabled = !here;
                    if (GUILayout.Button(label, DeviceUi.Button, GUILayout.Height(40f)))
                        Go(capsule);
                    GUI.enabled = true;
                    GUILayout.Space(4f);
                }
                GUILayout.EndScrollView();
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(Loc.Pick("Fermer (Échap)", "Close (Esc)", "Cerrar (Esc)"), DeviceUi.Button, GUILayout.Height(34f)))
                ModMenu.Close();
            GUILayout.EndArea();
        }

        private static void Go(PartKey capsule)
        {
            ModMenu.Close();
            _flashUntil = Time.unscaledTime + 0.8f;
            CapsuleAnimation.FlareUntil = Time.unscaledTime + 2.5f;
            TFNet.SendToServer(TFMessageKind.Teleport, capsule.ShipId, capsule.PartId, "");
        }
    }
}

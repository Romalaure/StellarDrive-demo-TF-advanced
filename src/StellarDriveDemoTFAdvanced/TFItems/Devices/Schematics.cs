using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Core.Services;
using FishNet.Connection;
using Managers.Client.Utils;
using MelonLoader.Utils;
using Newtonsoft.Json;
using Players;
using Players.Services;
using Saving.Services.Converters;
using Saving.Services.Model.State;
using Ships;
using Ships.Cables;
using Ships.Interface.Model.Joints;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.State;
using Ships.Services;
using StellarDriveDemoTFAdvanced.TFItems.Common;

using UnityEngine;
using WorldTracking.Interface.Model.Spaces;
using WorldTracking.Interface.Model.Spaces.Common;
using WorldTracking.Services;
using WorldTracking.Spaces;
using StellarDriveDemoTFAdvanced.TFItems.Paint;
using Loc = StellarDriveDemoTFAdvanced.Common.Loc;

namespace StellarDriveDemoTFAdvanced.TFItems.Devices
{
    /// <summary>
    /// Ship schematics, opened from the schematic tablet item: copy the nearest ship (with everything docked to it, its cables, TF paint,
    /// capsule names and KeySeat seat keys) to a file in UserData/TF/schematics, and build it again later, in this
    /// world or another, in front of you. Built on the game's own (developer) ship export and
    /// import. Every player can use them: the host does the copy and the build in its world, and
    /// the files are kept on the PC of the player who copied, sent over the network in pieces.
    /// Building is free: no resources are taken.
    /// </summary>
    internal static class Schematics
    {
        private const int FormatVersion = 1;

        private static string Folder => Path.Combine(MelonEnvironment.UserDataDirectory, "TF", "schematics");

        private sealed class SchematicFile
        {
            public int version = FormatVersion;
            public string name;
            public string created;
            public int ships;
            public int parts;
            public string game;
            public List<SavedPaint> paint = new List<SavedPaint>();
            public List<SavedName> capsules = new List<SavedName>();
            public List<SavedName> seatKeys = new List<SavedName>();
        }

        private sealed class SavedPaint
        {
            public uint ship;
            public ushort part;
            public string color;
            public bool finish;
            public byte gloss, metal, glow;
        }

        private sealed class SavedName
        {
            public uint ship;
            public ushort part;
            public string name;
        }

        public sealed class Entry
        {
            public string Path;
            public string Name;
            public string Created;
            public int Ships;
            public int Parts;
        }

        public static List<Entry> List()
        {
            var result = new List<Entry>();
            if (!Directory.Exists(Folder))
                return result;
            foreach (string path in Directory.GetFiles(Folder, "*.json"))
            {
                try
                {
                    // Only the header is needed for the list
                    var file = JsonConvert.DeserializeObject<SchematicFile>(File.ReadAllText(path));
                    if (file == null)
                        continue;
                    result.Add(new Entry { Path = path, Name = file.name ?? System.IO.Path.GetFileNameWithoutExtension(path), Created = file.created, Ships = file.ships, Parts = file.parts });
                }
                catch (Exception e)
                {
                    TFMod.Log.Warning($"schematic {path} unreadable: {e.Message}");
                }
            }
            return result.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static void Delete(Entry entry)
        {
            try
            {
                File.Delete(entry.Path);
            }
            catch (Exception e)
            {
                TFMod.Log.Warning("could not delete schematic: " + e.Message);
            }
        }

        /// <summary>The ship the copy would take: the root of the ship nearest to the player.</summary>
        public static TrackedShipServer NearestShip()
        {
            TrackedPlayerLocalClient player = ServiceLocator.GetService<PlayersClientTracker>()?.LocalPlayer;
            ShipsServerTracker ships = GameServices.ShipsServer;
            if (player == null || ships == null)
                return null;
            TrackedShipClient nearest = NearestShipUtils.GetNearestShipClient(player.SpaceId, player.Position);
            return nearest != null && ships.TryGetTrackedRootShip(nearest.Id, out TrackedShipServer root) ? root : null;
        }

        /// <summary>Host: saves the nearest ship and everything attached to it. Returns a message for the player.</summary>
        public static string Copy(string name)
        {
            if (!TFNet.IsServer)
                return RequestCopy(name);
            name = TeleportCapsule.CleanName(name);
            if (name.Length == 0)
                return Loc.Pick("Donne un nom à la schématique.", "Name the schematic.", "Ponle un nombre al esquema.");
            string message = Export(NearestShip(), name, out SchematicFile file);
            if (file != null)
                Save(file);
            return message;
        }

        private static void Save(SchematicFile file)
        {
            Directory.CreateDirectory(Folder);
            string path = System.IO.Path.Combine(Folder, FileNameFor(file.name));
            File.WriteAllText(path, JsonConvert.SerializeObject(file, Formatting.Indented));
            TFMod.Log.Msg($"schematic '{file.name}' saved: {file.ships} ship(s), {file.parts} part(s) -> {path}");
        }

        /// <summary>The ship and what is attached to it as a schematic. Returns a message for the player.</summary>
        private static string Export(TrackedShipServer ship, string name, out SchematicFile file)
        {
            file = null;
            ShipsServerTracker service = GameServices.ShipsServer;
            SpacesServer spaces = ServiceLocator.GetService<SpacesServer>();
            if (service == null || spaces == null || ship == null)
                return Loc.Pick("Aucun vaisseau à proximité.", "No ship nearby.", "Ninguna nave cerca.");

            // The game's export: snapshots of the root ship and the ships attached to it, with
            // attached ships placed relative to their parent
            var data = new ShipImportExportUtils.ShipImportExportData { rootShipId = ship.Id };
            var snapshots = new Dictionary<uint, ShipStateSnapshot>();
            foreach (TrackedShipServer attached in AttachedShips(ship, service, spaces))
            {
                var snapshot = (ShipStateSnapshot)attached.GetStateSnapshot();
                snapshots[snapshot.Id] = snapshot;
            }
            ShipStateSnapshot rootSnapshot = snapshots[ship.Id];
            uint rootSpace = rootSnapshot.SpaceId;
            Vector3 rootPosition = rootSnapshot.Position;
            Quaternion rootRotation = rootSnapshot.Rotation;
            foreach (uint id in snapshots.Keys.ToList())
            {
                ShipStateSnapshot snapshot = snapshots[id];
                if (snapshot.Id == data.rootShipId)
                {
                    snapshot.SpaceId = 0u;
                }
                else if (spaces.TryGetSpace(snapshot.SpaceId, out ISpace space) && space is ShipRelativeSpace relative && snapshots.ContainsKey(relative.ShipId))
                {
                    snapshot.SpaceId = relative.ShipId;
                }
                else if (snapshot.SpaceId == rootSpace)
                {
                    // Ships flying beside the root (a rotor's turning part): stored relative to the root
                    snapshot.Position = Quaternion.Inverse(rootRotation) * (snapshot.Position - rootPosition);
                    snapshot.Rotation = Quaternion.Inverse(rootRotation) * snapshot.Rotation;
                    snapshot.SpaceId = data.rootShipId;
                }
                else
                {
                    TFMod.Log.Warning($"schematic: attached ship {id} is in another space, left out");
                    snapshots.Remove(id);
                    continue;
                }
                snapshots[id] = snapshot;
            }
            data.ships = new ShipsConverter().ConvertToSerializable(new SavedShipStates(snapshots.Values.Cast<IShipState>().ToArray()));
            CablesServerTracker cablesTracker = ServiceLocator.GetService<CablesServerTracker>();
            var cables = new Dictionary<uint, CableData>();
            if (cablesTracker != null)
            {
                var shipIds = new HashSet<uint>(snapshots.Keys);
                foreach (uint shipId in shipIds)
                {
                    foreach (CableData cable in cablesTracker.GetShipCables(shipId))
                    {
                        var a = cable.State.PlugA;
                        var b = cable.State.PlugB;
                        if (!a.IsHeldByPlayer && shipIds.Contains(a.EntityId) && !b.IsHeldByPlayer && shipIds.Contains(b.EntityId))
                            cables[cable.Id] = cable;
                    }
                }
            }
            data.cables = CablesConverter.ConvertToSerializable(cables.Values.ToArray());

            file = new SchematicFile
            {
                name = name,
                created = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                ships = snapshots.Count,
                parts = snapshots.Values.Sum(s => s.StatefulParts.Count() + s.HullParts.Count()),
                game = JsonUtility.ToJson(data)
            };
            foreach (KeyValuePair<PartKey, PaintData> paint in PaintNet.Server.All)
            {
                if (!snapshots.ContainsKey(paint.Key.ShipId))
                    continue;
                PaintData value = paint.Value;
                file.paint.Add(new SavedPaint
                {
                    ship = paint.Key.ShipId,
                    part = paint.Key.PartId,
                    color = $"{value.Color.r:X2}{value.Color.g:X2}{value.Color.b:X2}",
                    finish = value.Finish.Enabled,
                    gloss = value.Finish.Gloss,
                    metal = value.Finish.Metal,
                    glow = value.Finish.Glow
                });
            }
            foreach (KeyValuePair<PartKey, string> capsule in TeleportCapsule.ServerNames)
            {
                if (snapshots.ContainsKey(capsule.Key.ShipId))
                    file.capsules.Add(new SavedName { ship = capsule.Key.ShipId, part = capsule.Key.PartId, name = capsule.Value });
            }

            // KeySeat command seats: their custom keys (name holds KeySeat's own encoding)
            foreach ((uint seatShip, ushort seatPart, string keys) in KeySeatCompat.Export(snapshots.Keys))
                file.seatKeys.Add(new SavedName { ship = seatShip, part = seatPart, name = keys });

            return Loc.Pick($"« {name} » enregistrée : {file.ships} vaisseau(x), {file.parts} pièces.", $"“{name}” saved: {file.ships} ship(s), {file.parts} parts.", $"« {name} » guardado: {file.ships} nave(s), {file.parts} piezas.");
        }

        /// <summary>
        /// The ship and everything attached to it: docked ships, the turning parts of its rotors
        /// (separate ships held by the rotor) and ships inside its space, then the same for every
        /// ship found.
        /// </summary>
        private static List<TrackedShipServer> AttachedShips(TrackedShipServer root, ShipsServerTracker ships, SpacesServer spaces)
        {
            var found = new Dictionary<uint, TrackedShipServer>();
            var queue = new Queue<TrackedShipServer>();
            void Add(uint id)
            {
                if (!found.ContainsKey(id) && ships.TryGetTrackedShip(id, out TrackedShipServer attached))
                {
                    found[id] = attached;
                    queue.Enqueue(attached);
                }
            }
            Add(root.Id);
            foreach (RootRelativeShipData connected in ships.GetRootConnectedShips(root.Id))
                Add(connected.Ship.Id);
            while (queue.Count > 0)
            {
                TrackedShipServer current = queue.Dequeue();
                foreach (StatefulPart part in current.StatefulParts)
                {
                    if (part.State is RotorState rotor && rotor.AnchoredShipId != 0)
                        Add(rotor.AnchoredShipId);
                    else if (part.State is DockingDoorState door && door.IsDocked)
                        Add(door.DockedConnectedPart.ShipId);
                }
                foreach (TrackedShipServer other in ships.Ships)
                {
                    if (spaces.TryGetSpace(other.SpaceId, out ISpace space) && space is ShipRelativeSpace relative && relative.ShipId == current.Id)
                        Add(other.Id);
                }
            }
            return found.Values.ToList();
        }

        /// <summary>Builds a saved schematic in front of the player. Returns a message for the player.</summary>
        public static string Build(Entry entry)
        {
            string text;
            try
            {
                text = File.ReadAllText(entry.Path);
            }
            catch (Exception e)
            {
                return Loc.Pick("Fichier illisible : " + e.Message, "Unreadable file: " + e.Message, "Archivo ilegible: " + e.Message);
            }
            if (!TFNet.IsServer)
                return RequestBuild(text);
            TrackedPlayerLocalClient player = ServiceLocator.GetService<PlayersClientTracker>()?.LocalPlayer;
            if (player == null)
                return Loc.Pick("Le monde n'est pas prêt.", "The world is not ready.", "El mundo no está listo.");
            return BuildText(text, player.SpaceId, player.Position, player.Rotation);
        }

        /// <summary>Server: builds a schematic file's text ahead of the given player pose.</summary>
        private static string BuildText(string text, uint playerSpace, Vector3 playerPosition, Quaternion playerRotation)
        {
            SchematicFile file;
            ShipImportExportUtils.ShipImportExportData data;
            try
            {
                file = JsonConvert.DeserializeObject<SchematicFile>(text);
                data = JsonUtility.FromJson<ShipImportExportUtils.ShipImportExportData>(file.game);
            }
            catch (Exception e)
            {
                return Loc.Pick("Fichier illisible : " + e.Message, "Unreadable file: " + e.Message, "Archivo ilegible: " + e.Message);
            }
            if (data?.ships == null || data.ships.Length == 0)
                return Loc.Pick("Cette schématique est vide.", "This schematic is empty.", "Este esquema está vacío.");

            var ids = ServiceLocator.GetService<WorldObjectsIdsServerTracker>();
            ShipsServerTracker ships = GameServices.ShipsServer;
            SpacesServer spaces = ServiceLocator.GetService<SpacesServer>();
            CablesServerTracker cablesTracker = ServiceLocator.GetService<CablesServerTracker>();
            if (ids == null || ships == null || spaces == null)
                return Loc.Pick("Le monde n'est pas prêt.", "The world is not ready.", "El mundo no está listo.");

            // Where to build: in the player's world space (outside any ship), ahead of them
            if (!TryWorldPlacement(playerSpace, playerPosition, playerRotation, out uint spaceId, out Vector3 position, out Quaternion rotation))
                return Loc.Pick("Impossible de trouver où construire.", "Could not find where to build.", "No se encuentra dónde construir.");

            CableData[] cables = CablesConverter.ConvertToState(data.cables ?? new Saving.Serialization.V12.Cables.SerializableCable[0]);
            SavedShipState[] states = new ShipsConverter().ConvertToState(data.ships).States.Cast<SavedShipState>().ToArray();
            uint rootId = data.rootShipId;
            var newIds = new Dictionary<uint, uint>();
            var oldIds = new Dictionary<uint, uint>();
            foreach (SavedShipState state in states)
            {
                state.Velocity = Vector3.zero;
                uint fresh = ids.CreateUniqueIdForObject();
                newIds[state.Id] = fresh;
                oldIds[fresh] = state.Id;
                state.Id = fresh;
            }
            for (int i = 0; i < cables.Length; i++)
            {
                CableData cable = cables[i];
                CableState cableState = cable.State;
                CablePlugSocket plugA = cableState.PlugA;
                EntityPartSocket socketA = plugA.EntityPartSocket;
                socketA.EntityId = newIds[socketA.EntityId];
                plugA.EntityPartSocket = socketA;
                cableState.PlugA = plugA;
                CablePlugSocket plugB = cableState.PlugB;
                EntityPartSocket socketB = plugB.EntityPartSocket;
                socketB.EntityId = newIds[socketB.EntityId];
                plugB.EntityPartSocket = socketB;
                cableState.PlugB = plugB;
                cable.State = cableState;
                cables[i] = cable;
            }
            float extent = 4f;
            var droppedParts = new HashSet<(uint, ushort)>();
            foreach (SavedShipState state in states)
            {
                if (oldIds[state.Id] != rootId)
                    state.SpaceId = newIds[state.SpaceId];
                var kept = new List<StatefulPart>();
                foreach (StatefulPart part in state.StatefulParts)
                {
                    if (part.State is RotorState rotor)
                    {
                        // A rotor whose turning part is not in the schematic (saved by an older
                        // version) would grab the original ship's turning part: leave it out
                        if (!newIds.TryGetValue(rotor.AnchoredShipId, out uint anchored))
                        {
                            droppedParts.Add((state.Id, part.Id));
                            continue;
                        }
                        part.State = rotor with { AnchoredShipId = anchored };
                    }
                    else if (part.State is DockingDoorState door && door.IsDocked)
                    {
                        if (newIds.TryGetValue(door.DockedConnectedPart.ShipId, out uint docked))
                        {
                            PartContext connected = door.DockedConnectedPart;
                            connected.ShipId = docked;
                            part.State = door with { DockedConnectedPart = connected };
                        }
                        else
                        {
                            part.State = door with { IsDocked = false };
                        }
                    }
                    kept.Add(part);
                }
                state.StatefulParts = kept.ToArray();
                if (oldIds[state.Id] == rootId)
                {
                    foreach (HullPart hull in state.HullParts)
                        extent = Mathf.Max(extent, hull.HullPlacement.Coord.magnitude);
                }
            }
            foreach (SavedShipState state in states)
            {
                if (oldIds[state.Id] != rootId)
                    continue;
                state.SpaceId = spaceId;
                // Ahead of the player, far enough for the ship not to land on them, a little up
                Vector3 forward = Vector3.ProjectOnPlane(rotation * Vector3.forward, rotation * Vector3.up).normalized;
                state.Position = position + forward * (extent + 5f) + rotation * Vector3.up * 2f;
                state.Rotation = rotation;
            }
            var relativeSpaces = new Dictionary<uint, uint>();
            foreach (SavedShipState state in states)
            {
                uint relative = spaces.GetOrCreateShipRelativeSpace(state.Id);
                relativeSpaces[state.Id] = relative;
                state.RelativeSpaceId = relative;
            }
            foreach (SavedShipState state in states)
            {
                if (oldIds[state.Id] != rootId)
                    state.SpaceId = relativeSpaces[state.SpaceId];
            }
            foreach (SavedShipState state in states)
                ships.LoadShip(state);
            if (cablesTracker != null)
            {
                foreach (CableData cable in cables)
                {
                    EntityPartSocket a = cable.State.PlugA.EntityPartSocket, b = cable.State.PlugB.EntityPartSocket;
                    if (droppedParts.Contains((a.EntityId, a.Socket.PartId)) || droppedParts.Contains((b.EntityId, b.Socket.PartId)))
                        continue;
                    cablesTracker.AddCable(cable.State);
                }
            }
            if (droppedParts.Count > 0)
                TFMod.Log.Warning($"schematic '{file.name}': {droppedParts.Count} rotor(s) left out, their turning part was not saved; copy the ship again");

            // TF extras follow the ships to their new ids
            foreach (SavedPaint paint in file.paint ?? new List<SavedPaint>())
            {
                if (!newIds.TryGetValue(paint.ship, out uint ship) || !ColorUtility.TryParseHtmlString("#" + paint.color, out Color color))
                    continue;
                var finish = paint.finish ? new PaintFinish { Enabled = true, Gloss = paint.gloss, Metal = paint.metal, Glow = paint.glow } : PaintFinish.None;
                PaintNet.ServerSet(new PartKey(ship, paint.part), new PaintData(PaintNet.ToColor32(color), finish));
            }
            foreach (SavedName capsule in file.capsules ?? new List<SavedName>())
            {
                if (newIds.TryGetValue(capsule.ship, out uint ship))
                    TFNet.SendToServer(TFMessageKind.CapsuleName, ship, capsule.part, capsule.name);
            }
            foreach (SavedName seat in file.seatKeys ?? new List<SavedName>())
            {
                if (newIds.TryGetValue(seat.ship, out uint ship))
                    KeySeatCompat.Import(ship, seat.part, seat.name);
            }
            TFMod.Log.Msg($"schematic '{file.name}' built: {states.Length} ship(s)");
            return Loc.Pick($"« {file.name} » construite devant toi.", $"“{file.name}” built in front of you.", $"« {file.name} » construido delante de ti.");
        }

        // Climbs out of ship spaces to the space the outermost ship flies in
        private static bool TryWorldPlacement(uint playerSpace, Vector3 playerPosition, Quaternion playerRotation, out uint spaceId, out Vector3 position, out Quaternion rotation)
        {
            spaceId = playerSpace;
            position = playerPosition;
            rotation = playerRotation;
            SpacesServer spaces = ServiceLocator.GetService<SpacesServer>();
            ShipsServerTracker ships = GameServices.ShipsServer;
            for (int depth = 0; depth < 8; depth++)
            {
                if (!spaces.TryGetSpace(spaceId, out ISpace space))
                    return false;
                if (!(space is ShipRelativeSpace relative))
                    return true;
                if (!ships.TryGetTrackedShip(relative.ShipId, out TrackedShipServer ship))
                    return false;
                position = ship.Position + ship.Rotation * position;
                rotation = ship.Rotation * rotation;
                spaceId = ship.SpaceId;
            }
            return false;
        }

        // ---- Other players: the host copies and builds, the files travel in pieces ----

        private const int ChunkLength = 16000;
        private const int MaxChunks = 1024;
        private static uint _nextTransfer;
        private static readonly Dictionary<(int, uint), string[]> Incoming = new Dictionary<(int, uint), string[]>();

        public static void Install()
        {
            TFNet.OnServer(TFMessageKind.SchematicCopy, ServerCopy);
            TFNet.OnServer(TFMessageKind.SchematicChunk, (sender, kind, transfer, index, text) =>
            {
                if (sender != null && Receive(sender.ClientId, transfer, index, text, out string json))
                    ServerBuild(sender, json);
            });
            TFNet.OnClient(TFMessageKind.SchematicChunk, (kind, transfer, index, text) =>
            {
                if (Receive(-1, transfer, index, text, out string json))
                    ClientSave(json);
            });
            TFNet.OnClient(TFMessageKind.SchematicResult, (kind, a, b, text) => SchematicsMenu.ShowResult(text));
            TFNet.Disconnected += () => Incoming.Clear();
        }

        /// <summary>Client: the nearest ship, as this player sees it.</summary>
        public static TrackedShipClient NearestShipClient()
        {
            TrackedPlayerLocalClient player = ServiceLocator.GetService<PlayersClientTracker>()?.LocalPlayer;
            return player == null ? null : NearestShipUtils.GetNearestShipClient(player.SpaceId, player.Position);
        }

        private static string RequestCopy(string name)
        {
            name = TeleportCapsule.CleanName(name);
            if (name.Length == 0)
                return Loc.Pick("Donne un nom à la schématique.", "Name the schematic.", "Ponle un nombre al esquema.");
            TrackedShipClient ship = NearestShipClient();
            if (ship == null)
                return Loc.Pick("Aucun vaisseau à proximité.", "No ship nearby.", "Ninguna nave cerca.");
            TFNet.SendToServer(TFMessageKind.SchematicCopy, ship.Id, 0, name);
            return Loc.Pick("Copie demandée à l'hôte…", "Copy asked of the host…", "Copia pedida al anfitrión…");
        }

        private static string RequestBuild(string text)
        {
            SendChunks(text, (transfer, index, chunk) => TFNet.SendToServer(TFMessageKind.SchematicChunk, transfer, index, chunk));
            return Loc.Pick("Schématique envoyée à l'hôte…", "Schematic sent to the host…", "Esquema enviado al anfitrión…");
        }

        private static void ServerCopy(NetworkConnection sender, TFMessageKind kind, uint shipId, ushort b, string name)
        {
            if (sender == null)
                return;
            name = TeleportCapsule.CleanName(name);
            string message;
            SchematicFile file = null;
            if (name.Length == 0)
                message = Loc.Pick("Donne un nom à la schématique.", "Name the schematic.", "Ponle un nombre al esquema.");
            else if (GameServices.ShipsServer == null || !GameServices.ShipsServer.TryGetTrackedRootShip(shipId, out TrackedShipServer root))
                message = Loc.Pick("Aucun vaisseau à proximité.", "No ship nearby.", "Ninguna nave cerca.");
            else
                message = Run(() => Export(root, name, out file));
            if (file != null)
            {
                string json = JsonConvert.SerializeObject(file);
                SendChunks(json, (transfer, index, chunk) => TFNet.SendTo(sender, TFMessageKind.SchematicChunk, transfer, index, chunk));
                TFMod.Log.Msg($"schematic '{file.name}' copied for client {sender.ClientId} ({json.Length} chars)");
            }
            TFNet.SendTo(sender, TFMessageKind.SchematicResult, 0, 0, message);
        }

        private static void ServerBuild(NetworkConnection sender, string json)
        {
            TrackedPlayerServer player = GameServices.PlayersServer?.GetTrackedPlayerFromConnectionId(sender.ClientId);
            string message = player == null
                ? Loc.Pick("Le monde n'est pas prêt.", "The world is not ready.", "El mundo no está listo.")
                : Run(() => BuildText(json, player.SpaceId, player.Position, player.Rotation));
            TFNet.SendTo(sender, TFMessageKind.SchematicResult, 0, 0, message);
        }

        private static void ClientSave(string json)
        {
            try
            {
                var file = JsonConvert.DeserializeObject<SchematicFile>(json);
                if (file?.name == null)
                    return;
                Save(file);
            }
            catch (Exception e)
            {
                TFMod.Log.Error("schematic received but not saved: " + e);
                SchematicsMenu.ShowResult(Loc.Pick("Schématique reçue mais pas enregistrée : " + e.Message, "Schematic received but not saved: " + e.Message, "Esquema recibido pero no guardado: " + e.Message));
            }
        }

        private static string Run(Func<string> action)
        {
            try
            {
                return action();
            }
            catch (Exception e)
            {
                TFMod.Log.Error("schematic: " + e);
                return Loc.Pick("Erreur : " + e.Message, "Error: " + e.Message, "Error: " + e.Message);
            }
        }

        // The text is gzipped and cut into pieces small enough for one network message each
        private static void SendChunks(string text, Action<uint, ushort, string> send)
        {
            string packed;
            using (var buffer = new MemoryStream())
            {
                using (var zip = new GZipStream(buffer, CompressionMode.Compress))
                {
                    byte[] bytes = System.Text.Encoding.UTF8.GetBytes(text);
                    zip.Write(bytes, 0, bytes.Length);
                }
                packed = Convert.ToBase64String(buffer.ToArray());
            }
            int count = Math.Max(1, (packed.Length + ChunkLength - 1) / ChunkLength);
            if (count > MaxChunks)
                throw new InvalidOperationException("schematic too large to send");
            uint transfer = ++_nextTransfer;
            for (int i = 0; i < count; i++)
            {
                int start = i * ChunkLength;
                send(transfer, (ushort)i, count + "|" + packed.Substring(start, Math.Min(ChunkLength, packed.Length - start)));
            }
        }

        private static bool Receive(int from, uint transfer, ushort index, string text, out string json)
        {
            json = null;
            int bar = text.IndexOf('|');
            if (bar <= 0 || !int.TryParse(text.Substring(0, bar), out int count) || count < 1 || count > MaxChunks || index >= count)
                return false;
            var key = (from, transfer);
            if (!Incoming.TryGetValue(key, out string[] pieces) || pieces.Length != count)
            {
                // One transfer at a time per sender is plenty
                foreach (var stale in Incoming.Keys.Where(k => k.Item1 == from).ToList())
                    Incoming.Remove(stale);
                Incoming[key] = pieces = new string[count];
            }
            pieces[index] = text.Substring(bar + 1);
            if (pieces.Any(p => p == null))
                return false;
            Incoming.Remove(key);
            try
            {
                using (var zip = new GZipStream(new MemoryStream(Convert.FromBase64String(string.Concat(pieces))), CompressionMode.Decompress))
                using (var reader = new StreamReader(zip, System.Text.Encoding.UTF8))
                    json = reader.ReadToEnd();
                return true;
            }
            catch (Exception e)
            {
                TFMod.Log.Warning("schematic transfer unreadable: " + e.Message);
                return false;
            }
        }

        private static string FileNameFor(string name)
        {
            var clean = new string(name.Select(c => System.IO.Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray());
            return clean + ".json";
        }
    }

    /// <summary>The schematics window.</summary>
    internal static class SchematicsMenu
    {
        private static bool _open;
        private static string _name = "";
        private static string _message;
        private static Vector2 _scroll;
        private static List<Schematics.Entry> _entries = new List<Schematics.Entry>();

        /// <summary>A message from the host about a copy or build asked by this player.</summary>
        public static void ShowResult(string message)
        {
            _message = message;
            _entries = Schematics.List();
        }

        public static void Show()
        {
            _open = true;
            _message = null;
            _entries = Schematics.List();
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

            Rect area = DeviceUi.Window(width, height, 620f, 680f, Loc.Pick("SCHÉMATIQUES DE VAISSEAUX", "SHIP SCHEMATICS", "ESQUEMAS DE NAVES"));
            GUILayout.BeginArea(area);
            bool host = TFNet.IsServer;
            TrackedShipServer nearest = host ? Schematics.NearestShip() : null;
            TrackedShipClient nearestClient = host ? null : Schematics.NearestShipClient();
            GUILayout.Label(Loc.Pick("COPIER LE VAISSEAU LE PLUS PROCHE", "COPY THE NEAREST SHIP", "COPIAR LA NAVE MÁS CERCANA"), DeviceUi.Muted);
            GUILayout.Label(nearest != null
                ? Loc.Pick($"Vaisseau n°{nearest.Id} : {nearest.StatefulParts.Count()} pièces, {nearest.GetHullParts().Count} éléments de coque (+ les vaisseaux attachés)", $"Ship #{nearest.Id}: {nearest.StatefulParts.Count()} parts, {nearest.GetHullParts().Count} hull pieces (+ the ships attached)", $"Nave n.º {nearest.Id}: {nearest.StatefulParts.Count()} piezas, {nearest.GetHullParts().Count} elementos de casco (+ las naves acopladas)")
                : nearestClient != null
                    ? Loc.Pick($"Vaisseau n°{nearestClient.Id} : {nearestClient.StatefulParts.Count()} pièces (+ les vaisseaux attachés)", $"Ship #{nearestClient.Id}: {nearestClient.StatefulParts.Count()} parts (+ the ships attached)", $"Nave n.º {nearestClient.Id}: {nearestClient.StatefulParts.Count()} piezas (+ las naves acopladas)")
                    : Loc.Pick("Aucun vaisseau à proximité.", "No ship nearby.", "Ninguna nave cerca."), DeviceUi.Text);
            GUILayout.BeginHorizontal();
            _name = GUILayout.TextField(_name ?? "", TeleportCapsule.MaxNameLength, DeviceUi.Field, GUILayout.Height(34f), GUILayout.ExpandWidth(true));
            GUI.enabled = nearest != null || nearestClient != null;
            if (GUILayout.Button(Loc.Pick("Copier", "Copy", "Copiar"), DeviceUi.Button, GUILayout.Width(100f), GUILayout.Height(34f)))
            {
                _message = Run(() => Schematics.Copy(_name));
                _entries = Schematics.List();
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.Space(14f);

            GUILayout.Label(Loc.Pick("SCHÉMATIQUES ENREGISTRÉES (tous les mondes)", "SAVED SCHEMATICS (every world)", "ESQUEMAS GUARDADOS (todos los mundos)"), DeviceUi.Muted);
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(360f));
            if (_entries.Count == 0)
                GUILayout.Label(Loc.Pick("Aucune pour l'instant : copie un vaisseau ci-dessus.", "None yet: copy a ship above.", "Ninguno por ahora: copia una nave arriba."), DeviceUi.Muted);
            foreach (Schematics.Entry entry in _entries.ToList())
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(Loc.Pick($"{entry.Name}\n{entry.Ships} vaisseau(x) · {entry.Parts} pièces · {entry.Created}", $"{entry.Name}\n{entry.Ships} ship(s) · {entry.Parts} parts · {entry.Created}", $"{entry.Name}\n{entry.Ships} nave(s) · {entry.Parts} piezas · {entry.Created}"), DeviceUi.Text, GUILayout.ExpandWidth(true));
                if (GUILayout.Button(Loc.Pick("Construire", "Build", "Construir"), DeviceUi.Button, GUILayout.Width(110f), GUILayout.Height(40f)))
                    _message = Run(() => Schematics.Build(entry));
                if (GUILayout.Button(Loc.Pick("Suppr.", "Delete", "Borrar"), DeviceUi.Button, GUILayout.Width(70f), GUILayout.Height(40f)))
                {
                    Schematics.Delete(entry);
                    _entries = Schematics.List();
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(4f);
            }
            GUILayout.EndScrollView();
            if (_message != null)
                GUILayout.Label(_message, DeviceUi.Text);
            GUILayout.FlexibleSpace();
            GUILayout.Label(Loc.Pick("Construire est gratuit et place le vaisseau devant toi. Les schématiques sont sur ton PC, dans UserData/TF/schematics.", "Building is free and puts the ship in front of you. Schematics are on your PC, in UserData/TF/schematics.", "Construir es gratis y coloca la nave delante de ti. Los esquemas están en tu PC, en UserData/TF/schematics."), DeviceUi.Muted);
            if (GUILayout.Button(Loc.Pick("Fermer (Échap)", "Close (Esc)", "Cerrar (Esc)"), DeviceUi.Button, GUILayout.Height(34f)))
                ModMenu.Close();
            GUILayout.EndArea();
            GUI.matrix = previous;
        }

        private static string Run(Func<string> action)
        {
            try
            {
                return action();
            }
            catch (Exception e)
            {
                TFMod.Log.Error("schematic: " + e);
                return Loc.Pick("Erreur : " + e.Message, "Error: " + e.Message, "Error: " + e.Message);
            }
        }
    }
}

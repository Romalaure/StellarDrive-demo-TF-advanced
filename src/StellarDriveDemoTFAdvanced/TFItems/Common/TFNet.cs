using System;
using System.Collections.Generic;
using FishNet;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Serializing;
using FishNet.Transporting;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.TFItems.Common
{
    /// <summary>What a TF network message is about.</summary>
    internal enum TFMessageKind : byte
    {
        /// <summary>Client to server, once after joining: send me the radios and outfits.</summary>
        Sync = 1,
        /// <summary>Client to server: move me to the capsule A (ship) / B (part).</summary>
        Teleport = 2,
        /// <summary>Server to one client: the teleport was refused, Text says why.</summary>
        TeleportRefused = 3,
        /// <summary>Client to server, then server to everyone: capsule A/B is now called Text.</summary>
        CapsuleName = 4,
        /// <summary>Client to server: give me the tool item A (paint gun or schematic tablet).</summary>
        GiveTool = 5,
        /// <summary>Server to one client: tool item A given (B = 1) or already owned (B = 0).</summary>
        ToolGiven = 6,

        /// <summary>Client to server, then server to everyone: radio A/B now plays Text.</summary>
        Radio = 10,
        /// <summary>Server to one client: the host does not let you change radio A/B.</summary>
        RadioRefused = 11,
        /// <summary>Client to server, then server to everyone: player A wears Text ("RRGGBB RRGGBB").</summary>
        Outfit = 20,
        /// <summary>Client to server: force field door A/B, Text is "width height on" (the host writes its state).</summary>
        FieldDoor = 25,
        /// <summary>Client to server: send me the teleporter's destinations.</summary>
        TeleporterList = 40,
        /// <summary>Server to one client: the destinations, one "kind|a|b|name" per line.</summary>
        TeleporterDestinations = 41,
        /// <summary>Client to server: teleport me to Text = p (planet A) or s/b (ship A, beacon part B).</summary>
        TeleporterGo = 42,
        /// <summary>Client to server: make the merchant offer A, B times.</summary>
        Trade = 50,
        /// <summary>Server to one client: how the trade went (B = times done), Text is for the player.</summary>
        TradeResult = 51,
        /// <summary>Client to server: send me the trader's reputation and the day's missions done.</summary>
        TraderInfo = 52,
        /// <summary>Server to one client: Text is "reputation|day|done indexes".</summary>
        TraderInfoReply = 53,
        /// <summary>Client to server: deliver the day's mission A.</summary>
        TraderMission = 54,

        /// <summary>Client to server: copy the ship A (and what is attached to it) as schematic Text.</summary>
        SchematicCopy = 30,
        /// <summary>Either way: piece B of schematic file transfer A, Text is "count|data".</summary>
        SchematicChunk = 31,
        /// <summary>Server to one client: how the copy or build went, Text is for the player.</summary>
        SchematicResult = 32
    }

    /// <summary>
    /// One small message type for the radio, the wardrobe and the teleport capsule, sent as a
    /// FishNet broadcast. Every player needs the mod; the host decides.
    /// </summary>
    internal static class TFNet
    {
        private struct TFMessage : IBroadcast
        {
            public byte Kind;
            public uint A;
            public ushort B;
            public string Text;
        }

        public delegate void ServerHandler(NetworkConnection sender, TFMessageKind kind, uint a, ushort b, string text);
        public delegate void ClientHandler(TFMessageKind kind, uint a, ushort b, string text);

        private static readonly Dictionary<TFMessageKind, ServerHandler> ServerHandlers = new Dictionary<TFMessageKind, ServerHandler>();
        private static readonly Dictionary<TFMessageKind, ClientHandler> ClientHandlers = new Dictionary<TFMessageKind, ClientHandler>();

        private static NetworkManager _network;
        private static bool _clientWasStarted;
        private static bool _synced;
        private static float _nextSyncAttempt;

        /// <summary>Raised on the server when a client asks for the full state after joining.</summary>
        public static event Action<NetworkConnection> SyncRequested;

        /// <summary>Raised on clients once connected to a world, after asking for its state.</summary>
        public static event Action Synced;

        /// <summary>Raised on clients when they leave a world.</summary>
        public static event Action Disconnected;

        public static void InstallSerializers()
        {
            GenericWriter<TFMessage>.SetWrite((writer, m) =>
            {
                writer.WriteUInt8Unpacked(m.Kind);
                writer.WriteUInt32(m.A);
                writer.WriteUInt16(m.B);
                writer.WriteString(m.Text ?? "");
            });
            GenericReader<TFMessage>.SetRead(reader => new TFMessage
            {
                Kind = reader.ReadUInt8Unpacked(),
                A = reader.ReadUInt32(),
                B = reader.ReadUInt16(),
                Text = reader.ReadStringAllocated()
            });
        }

        public static void OnServer(TFMessageKind kind, ServerHandler handler) => ServerHandlers[kind] = handler;

        public static void OnClient(TFMessageKind kind, ClientHandler handler) => ClientHandlers[kind] = handler;

        public static bool IsServer => _network != null && _network.IsServerStarted;

        public static void Update()
        {
            EnsureHandlers();
            NetworkManager network = _network;
            bool clientStarted = network != null && network.IsClientStarted;
            if (!clientStarted)
            {
                if (_clientWasStarted)
                {
                    _synced = false;
                    Disconnected?.Invoke();
                }
                _clientWasStarted = false;
                return;
            }
            _clientWasStarted = true;

            if (_synced || Time.unscaledTime < _nextSyncAttempt)
                return;
            _nextSyncAttempt = Time.unscaledTime + 1f;
            if (network.ClientManager.Connection == null || !network.ClientManager.Connection.IsAuthenticated)
                return;
            if (GameServices.ShipsClient == null)
                return;
            _synced = true;
            SendToServer(TFMessageKind.Sync, 0, 0, "");
            Synced?.Invoke();
        }

        public static void SendToServer(TFMessageKind kind, uint a, ushort b, string text)
        {
            NetworkManager network = _network;
            if (network == null)
                return;
            var message = new TFMessage { Kind = (byte)kind, A = a, B = b, Text = text ?? "" };
            // On the host, handle our own requests directly instead of looping through the transport
            if (network.IsServerStarted)
            {
                NetworkConnection local = null;
                NetworkConnection clientSide = network.ClientManager.Connection;
                if (clientSide != null)
                    network.ServerManager.Clients.TryGetValue(clientSide.ClientId, out local);
                OnServerMessage(local, message, Channel.Reliable);
            }
            else if (network.IsClientStarted)
            {
                network.ClientManager.Broadcast(message);
            }
        }

        /// <summary>Server only: tells every client (the host included).</summary>
        public static void SendToAll(TFMessageKind kind, uint a, ushort b, string text)
        {
            if (!IsServer)
                return;
            _network.ServerManager.Broadcast(new TFMessage { Kind = (byte)kind, A = a, B = b, Text = text ?? "" });
        }

        /// <summary>Server only: tells one client.</summary>
        public static void SendTo(NetworkConnection connection, TFMessageKind kind, uint a, ushort b, string text)
        {
            if (!IsServer || connection == null)
                return;
            _network.ServerManager.Broadcast(connection, new TFMessage { Kind = (byte)kind, A = a, B = b, Text = text ?? "" });
        }

        private static void EnsureHandlers()
        {
            // (InstanceFinder.NetworkManager logs a line each call while there is none, as in the menu)
            NetworkManager network = (FishNet.Managing.NetworkManager.Instances.Count > 0 ? FishNet.Managing.NetworkManager.Instances[0] : null);
            if (network == _network)
                return;
            if (_network != null)
            {
                try
                {
                    _network.ServerManager.UnregisterBroadcast<TFMessage>(OnServerMessage);
                    _network.ClientManager.UnregisterBroadcast<TFMessage>(OnClientMessage);
                }
                catch (Exception)
                {
                    // the old manager is being torn down
                }
            }
            _network = network;
            _synced = false;
            if (network == null)
                return;
            network.ServerManager.RegisterBroadcast<TFMessage>(OnServerMessage, true);
            network.ClientManager.RegisterBroadcast<TFMessage>(OnClientMessage);
        }

        private static void OnServerMessage(NetworkConnection sender, TFMessage message, Channel channel)
        {
            var kind = (TFMessageKind)message.Kind;
            try
            {
                if (kind == TFMessageKind.Sync)
                {
                    if (sender != null)
                        SyncRequested?.Invoke(sender);
                    return;
                }
                if (ServerHandlers.TryGetValue(kind, out ServerHandler handler))
                    handler(sender, kind, message.A, message.B, message.Text ?? "");
            }
            catch (Exception e)
            {
                TFMod.Log.Error($"TF message {kind} on the server: {e}");
            }
        }

        private static void OnClientMessage(TFMessage message, Channel channel)
        {
            var kind = (TFMessageKind)message.Kind;
            try
            {
                if (ClientHandlers.TryGetValue(kind, out ClientHandler handler))
                    handler(kind, message.A, message.B, message.Text ?? "");
            }
            catch (Exception e)
            {
                TFMod.Log.Error($"TF message {kind} on the client: {e}");
            }
        }
    }
}

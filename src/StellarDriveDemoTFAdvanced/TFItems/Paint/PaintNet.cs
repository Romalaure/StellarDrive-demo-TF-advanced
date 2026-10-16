using System;
using System.Collections.Generic;
using System.Linq;
using FishNet;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Serializing;
using FishNet.Transporting;
using Ships.Interface.Model.State;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.TFItems.Paint
{
    /// <summary>
    /// Server-authoritative paint for floating parts. Clients ask the server to paint, the server
    /// validates, stores and tells every client. Clients ask for the full set once after joining.
    /// Every player needs the mod to see the colors; the host needs it for painting to work at all.
    /// </summary>
    internal static class PaintNet
    {
        private struct PaintRequest : IBroadcast
        {
            public uint ShipId;
            public ushort PartId;
            public PaintData Paint;
            public bool ReplaceSame;
        }

        private struct PaintUpdate : IBroadcast
        {
            public uint ShipId;
            public ushort PartId;
            public bool Cleared;
            public PaintData Paint;
        }

        private struct SyncRequest : IBroadcast
        {
            public byte Unused;
        }

        private struct GiveToolRequest : IBroadcast
        {
            public byte Unused;
        }

        /// <summary>Authoritative paint, only filled on the host.</summary>
        public static readonly PaintStore Server = new PaintStore();

        /// <summary>Paint this client knows about, used for rendering.</summary>
        public static readonly PaintStore Client = new PaintStore();

        private static NetworkManager _network;
        private static bool _clientWasStarted;
        private static bool _syncRequested;
        private static float _nextSyncAttempt;

        public static void InstallSerializers()
        {
            GenericWriter<PaintRequest>.SetWrite((writer, m) =>
            {
                writer.WriteUInt32(m.ShipId);
                writer.WriteUInt16(m.PartId);
                WritePaint(writer, m.Paint);
                writer.WriteBoolean(m.ReplaceSame);
            });
            GenericReader<PaintRequest>.SetRead(reader => new PaintRequest
            {
                ShipId = reader.ReadUInt32(),
                PartId = reader.ReadUInt16(),
                Paint = ReadPaint(reader),
                ReplaceSame = reader.ReadBoolean()
            });

            GenericWriter<PaintUpdate>.SetWrite((writer, m) =>
            {
                writer.WriteUInt32(m.ShipId);
                writer.WriteUInt16(m.PartId);
                writer.WriteBoolean(m.Cleared);
                WritePaint(writer, m.Paint);
            });
            GenericReader<PaintUpdate>.SetRead(reader => new PaintUpdate
            {
                ShipId = reader.ReadUInt32(),
                PartId = reader.ReadUInt16(),
                Cleared = reader.ReadBoolean(),
                Paint = ReadPaint(reader)
            });

            GenericWriter<SyncRequest>.SetWrite((writer, m) => writer.WriteUInt8Unpacked(m.Unused));
            GenericReader<SyncRequest>.SetRead(reader => new SyncRequest { Unused = reader.ReadUInt8Unpacked() });

            GenericWriter<GiveToolRequest>.SetWrite((writer, m) => writer.WriteUInt8Unpacked(m.Unused));
            GenericReader<GiveToolRequest>.SetRead(reader => new GiveToolRequest { Unused = reader.ReadUInt8Unpacked() });
        }

        public static void Update()
        {
            EnsureHandlers();

            NetworkManager network = _network;
            bool clientStarted = network != null && network.IsClientStarted;
            if (!clientStarted)
            {
                if (_clientWasStarted)
                {
                    Client.Clear();
                    _syncRequested = false;
                }
                _clientWasStarted = false;
                return;
            }
            _clientWasStarted = true;

            if (_syncRequested || Time.unscaledTime < _nextSyncAttempt)
                return;
            _nextSyncAttempt = Time.unscaledTime + 1f;

            if (network.ClientManager.Connection == null || !network.ClientManager.Connection.IsAuthenticated)
                return;
            if (GameServices.ShipsClient == null)
                return;

            _syncRequested = true;
            SendToServer(new SyncRequest(), OnSyncRequest);
        }

        /// <summary>Called by the paint tool on this client.</summary>
        public static void RequestPaint(PartKey key, PaintData paint, bool replaceSame)
        {
            SendToServer(new PaintRequest
            {
                ShipId = key.ShipId,
                PartId = key.PartId,
                Paint = paint,
                ReplaceSame = replaceSame
            }, OnPaintRequest);
        }

        /// <summary>Asks the server for a paint tool, if this player has none.</summary>
        public static void RequestPaintTool()
        {
            SendToServer(new GiveToolRequest(), OnGiveToolRequest);
        }

        /// <summary>Host only: paints a part directly (schematics) and tells every client.</summary>
        public static void ServerSet(PartKey key, PaintData paint)
        {
            Server.Set(key, paint);
            if (_network != null && _network.IsServerStarted)
                _network.ServerManager.Broadcast(new PaintUpdate { ShipId = key.ShipId, PartId = key.PartId, Paint = paint });
        }

        /// <summary>A part was removed from its ship; forget its paint on whichever side this runs.</summary>
        public static void ForgetPart(PartKey key)
        {
            foreach (PartKey side in new[] { key.Exterior, key.Interior })
            {
                Server.Remove(side);
                Client.Remove(side);
            }
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
                    _network.ServerManager.UnregisterBroadcast<PaintRequest>(OnPaintRequest);
                    _network.ServerManager.UnregisterBroadcast<SyncRequest>(OnSyncRequest);
                    _network.ServerManager.UnregisterBroadcast<GiveToolRequest>(OnGiveToolRequest);
                    _network.ClientManager.UnregisterBroadcast<PaintUpdate>(OnPaintUpdate);
                }
                catch (Exception)
                {
                    // the old manager is being torn down
                }
            }

            _network = network;
            _syncRequested = false;
            Client.Clear();
            if (network == null)
                return;

            network.ServerManager.RegisterBroadcast<PaintRequest>(OnPaintRequest, true);
            network.ServerManager.RegisterBroadcast<SyncRequest>(OnSyncRequest, true);
            network.ServerManager.RegisterBroadcast<GiveToolRequest>(OnGiveToolRequest, true);
            network.ClientManager.RegisterBroadcast<PaintUpdate>(OnPaintUpdate);
        }

        // On the host, handle our own requests directly instead of looping through the transport
        private static void SendToServer<T>(T message, Action<NetworkConnection, T, Channel> serverHandler) where T : struct, IBroadcast
        {
            NetworkManager network = _network;
            if (network == null)
                return;

            if (network.IsServerStarted)
            {
                NetworkConnection local = null;
                NetworkConnection clientSide = network.ClientManager.Connection;
                if (clientSide != null)
                    network.ServerManager.Clients.TryGetValue(clientSide.ClientId, out local);
                serverHandler(local, message, Channel.Reliable);
            }
            else if (network.IsClientStarted)
            {
                network.ClientManager.Broadcast(message);
            }
        }

        private static void OnGiveToolRequest(NetworkConnection sender, GiveToolRequest request, Channel channel)
        {
            PaintToolGiver.ServerGive(sender);
        }

        private static void OnPaintRequest(NetworkConnection sender, PaintRequest request, Channel channel)
        {
            if (!Settings.PaintAllParts.Value)
                return;

            var ships = GameServices.ShipsServer;
            if (ships == null || !ships.TryGetShip(request.ShipId, out IShipStateRead ship))
                return;
            var target = new PartKey(request.ShipId, request.PartId);
            if (!ship.TryGetStatefulPart(target.RealPartId, out _))
                return;
            List<PartKey> toPaint;
            if (request.ReplaceSame)
            {
                // Same rule as the game's hull "replace all identical colors": every part of this ship
                // with the clicked part's paint (or every unpainted part, if it is unpainted)
                bool targetPainted = Server.TryGet(target, out PaintData targetPaint);
                toPaint = ship.StatefulParts
                    .Select(p => target.IsInterior ? new PartKey(request.ShipId, p.Id).Interior : new PartKey(request.ShipId, p.Id))
                    .Where(k => Server.TryGet(k, out PaintData p) ? targetPainted && p.SameAs(targetPaint) : !targetPainted)
                    .ToList();
            }
            else
            {
                toPaint = new List<PartKey> { target };
            }

            foreach (PartKey key in toPaint)
            {
                Server.Set(key, request.Paint);
                _network.ServerManager.Broadcast(new PaintUpdate { ShipId = key.ShipId, PartId = key.PartId, Paint = request.Paint });
            }
        }

        private static void OnSyncRequest(NetworkConnection sender, SyncRequest request, Channel channel)
        {
            if (sender == null)
                return;
            foreach (KeyValuePair<PartKey, PaintData> entry in Server.All.ToList())
            {
                _network.ServerManager.Broadcast(sender, new PaintUpdate
                {
                    ShipId = entry.Key.ShipId,
                    PartId = entry.Key.PartId,
                    Paint = entry.Value
                });
            }
        }

        private static void OnPaintUpdate(PaintUpdate update, Channel channel)
        {
            var key = new PartKey(update.ShipId, update.PartId);
            if (update.Cleared)
                Client.Remove(key);
            else
                Client.Set(key, update.Paint);
            PartTint.Refresh(key);
        }

        private static void WritePaint(Writer writer, PaintData paint)
        {
            writer.WriteUInt8Unpacked(paint.Color.r);
            writer.WriteUInt8Unpacked(paint.Color.g);
            writer.WriteUInt8Unpacked(paint.Color.b);
            writer.WriteBoolean(paint.Finish.Enabled);
            writer.WriteUInt8Unpacked(paint.Finish.Gloss);
            writer.WriteUInt8Unpacked(paint.Finish.Metal);
            writer.WriteUInt8Unpacked(paint.Finish.Glow);
            writer.WriteUInt8Unpacked(paint.Material);
        }

        private static PaintData ReadPaint(Reader reader)
        {
            var color = new Color32(reader.ReadUInt8Unpacked(), reader.ReadUInt8Unpacked(), reader.ReadUInt8Unpacked(), 255);
            var finish = new PaintFinish
            {
                Enabled = reader.ReadBoolean(),
                Gloss = reader.ReadUInt8Unpacked(),
                Metal = reader.ReadUInt8Unpacked(),
                Glow = reader.ReadUInt8Unpacked()
            };
            return new PaintData(color, finish, reader.ReadUInt8Unpacked());
        }

        /// <summary>Same byte conversion the game uses for hull paint.</summary>
        public static Color32 ToColor32(Color color)
        {
            return new Color32((byte)(color.r * 255f), (byte)(color.g * 255f), (byte)(color.b * 255f), 255);
        }
    }
}

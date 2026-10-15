using System;
using FishNet;
using FishNet.Broadcast;
using FishNet.Managing;
using FishNet.Serializing;
using FishNet.Transporting;

namespace StellarDriveDemoTFAdvanced.Common
{
    /// <summary>
    /// The mod's own messages from the host to every player, as a FishNet broadcast (its own
    /// message type, separate from the TF mod's). Every player needs the mod; a player without it
    /// simply ignores them. Used by the radar: the host computes the contacts, players draw them.
    /// </summary>
    internal static class TFANet
    {
        public const byte RadarContacts = 1;

        private struct TFAMessage : IBroadcast
        {
            public byte Kind;
            public uint A;
            public string Text;
        }

        public delegate void ClientHandler(byte kind, uint a, string text);

        public static event ClientHandler Received;

        private static NetworkManager _network;
        private static bool _serializers;
        private static bool _broken;

        private static void InstallSerializers()
        {
            if (_serializers)
                return;
            _serializers = true;
            GenericWriter<TFAMessage>.SetWrite((writer, m) =>
            {
                writer.WriteUInt8Unpacked(m.Kind);
                writer.WriteUInt32(m.A);
                writer.WriteString(m.Text ?? "");
            });
            GenericReader<TFAMessage>.SetRead(reader => new TFAMessage
            {
                Kind = reader.ReadUInt8Unpacked(),
                A = reader.ReadUInt32(),
                Text = reader.ReadStringAllocated()
            });
        }

        /// <summary>Every frame: follows the game's network manager (a new one per world).</summary>
        public static void Update()
        {
            if (_broken)
                return;
            try
            {
                NetworkManager network = InstanceFinder.NetworkManager;
                if (network == _network)
                    return;
                InstallSerializers();
                if (_network != null)
                {
                    try
                    {
                        _network.ClientManager.UnregisterBroadcast<TFAMessage>(OnClientMessage);
                    }
                    catch (Exception)
                    {
                        // the old manager is being torn down
                    }
                }
                _network = network;
                if (network != null)
                    network.ClientManager.RegisterBroadcast<TFAMessage>(OnClientMessage);
            }
            catch (Exception e)
            {
                // Never retried: the radar then works for the host alone
                _broken = true;
                AdvancedMod.Log.Warning("network messages unavailable, the radar is shown to the host only: " + e.GetBaseException().Message);
            }
        }

        /// <summary>Host only: tells every player (its own game included).</summary>
        public static void SendToAll(byte kind, uint a, string text)
        {
            if (_broken || _network == null || !_network.IsServerStarted)
                return;
            try
            {
                _network.ServerManager.Broadcast(new TFAMessage { Kind = kind, A = a, Text = text ?? "" });
            }
            catch (Exception e)
            {
                ErrorLog.Report("network message", e);
            }
        }

        private static void OnClientMessage(TFAMessage message, Channel channel)
        {
            try
            {
                Received?.Invoke(message.Kind, message.A, message.Text);
            }
            catch (Exception e)
            {
                ErrorLog.Report("network message", e);
            }
        }
    }
}

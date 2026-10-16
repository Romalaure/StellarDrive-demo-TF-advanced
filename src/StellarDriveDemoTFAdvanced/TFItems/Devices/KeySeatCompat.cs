using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Managers.Interface;
using Ships.Interface.Model;
using Ships.Interface.Model.Parts;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.TFItems.Devices
{
    /// <summary>
    /// Works with tidic84's KeySeat mod (also shipped as CustomSeat.dll), read by reflection so TF
    /// does not need it. KeySeat keeps each command seat's custom keys on the host and writes them
    /// to keyseat.txt only when the game is saved; here they are written as soon as they change,
    /// and ship schematics carry them so a built copy gets the same keys.
    /// </summary>
    internal static class KeySeatCompat
    {
        /// <summary>One loaded copy of KeySeat (several can be installed under different file names).</summary>
        private sealed class Copy
        {
            public IDictionary Server;
            public MethodInfo Write, Read, SaveTo, SendToAll, WriteSnapshot;
            public object SnapshotOp;
        }

        private static List<Copy> _copies;
        private static bool _dirty;
        private static float _dirtySince;

        private static List<Copy> Copies => _copies ?? (_copies = Find());

        public static bool Installed => Copies.Count > 0;

        internal static IEnumerable<Type> ConfigsTypes() =>
            AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("KeySeat.SeatConfigs", false))
                .Where(t => t != null);

        private static List<Copy> Find()
        {
            var copies = new List<Copy>();
            foreach (Type configs in ConfigsTypes())
            {
                try
                {
                    Assembly assembly = configs.Assembly;
                    Type config = assembly.GetType("KeySeat.SeatConfig", true);
                    Type net = assembly.GetType("KeySeat.KeySeatNet", true);
                    Type op = assembly.GetType("KeySeat.Op", true);
                    var copy = new Copy
                    {
                        Server = (IDictionary)AccessTools.Field(configs, "Server").GetValue(null),
                        Write = AccessTools.Method(config, "Write"),
                        Read = AccessTools.Method(config, "Read"),
                        SaveTo = AccessTools.Method(configs, "AfterSave"),
                        SendToAll = AccessTools.Method(net, "SendToAll"),
                        WriteSnapshot = AccessTools.Method(configs, "WriteSnapshot"),
                        SnapshotOp = Enum.Parse(op, "Snapshot")
                    };
                    if (copy.Server == null || copy.Write == null || copy.Read == null)
                        continue;
                    copies.Add(copy);
                }
                catch (Exception e)
                {
                    TFMod.Log.Warning($"KeySeat in {configs.Assembly.GetName().Name} not supported: {e.Message}");
                }
            }
            if (copies.Count > 1)
                TFMod.Log.Warning($"KeySeat is installed {copies.Count} times (CustomSeat.dll and KeySeat.dll?): keep only one, they fight over the seat keys");
            return copies;
        }

        /// <summary>Host: the custom keys of the seats on these ships, by ship and part, as text.</summary>
        public static List<(uint ship, ushort part, string keys)> Export(ICollection<uint> shipIds)
        {
            var result = new List<(uint, ushort, string)>();
            var seen = new HashSet<(uint, ushort)>();
            foreach (Copy copy in Copies)
            {
                foreach (DictionaryEntry entry in copy.Server)
                {
                    var seat = (PartContext)entry.Key;
                    if (!shipIds.Contains(seat.ShipId) || !seen.Add((seat.ShipId, seat.PartId)))
                        continue;
                    using (var stream = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(stream))
                            copy.Write.Invoke(entry.Value, new object[] { writer });
                        result.Add((seat.ShipId, seat.PartId, Convert.ToBase64String(stream.ToArray())));
                    }
                }
            }
            return result;
        }

        /// <summary>Host: gives a seat its custom keys (from Export) and tells every player.</summary>
        public static void Import(uint ship, ushort part, string keys)
        {
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(keys ?? "");
            }
            catch (FormatException)
            {
                return;
            }
            foreach (Copy copy in Copies)
            {
                try
                {
                    using (var reader = new BinaryReader(new MemoryStream(bytes)))
                        copy.Server[new PartContext(ship, part)] = copy.Read.Invoke(null, new object[] { reader });
                }
                catch (Exception e)
                {
                    TFMod.Log.Warning("could not give a seat its keys: " + e.GetBaseException().Message);
                }
            }
            MarkChanged();
        }

        public static bool HasKeys(uint ship, ushort part)
        {
            if (!TFNet.IsServer)
                return false;
            var seat = new PartContext(ship, part);
            return Copies.Any(c => c.Server.Contains(seat));
        }

        /// <summary>Host: the keys changed; they are sent to every player and written to the world shortly.</summary>
        public static void MarkChanged()
        {
            if (!_dirty)
                _dirtySince = Time.unscaledTime;
            _dirty = true;
        }

        public static void Update()
        {
            if (!_dirty || Time.unscaledTime - _dirtySince < 0.5f)
                return;
            _dirty = false;
            // Not while a world is being left: KeySeat empties its list then
            if (!TFNet.IsServer || GameServices.ShipsServer == null)
                return;
            string save = MainGameSceneLoadData.SaveFileFullName;
            string world = string.IsNullOrEmpty(save) ? null : Path.GetDirectoryName(save);
            foreach (Copy copy in Copies)
            {
                try
                {
                    // KeySeat's own snapshot message and file, so it reads them back as usual
                    if (copy.SendToAll != null && copy.WriteSnapshot != null)
                    {
                        Delegate body = Delegate.CreateDelegate(typeof(Action<BinaryWriter>), copy.WriteSnapshot);
                        copy.SendToAll.Invoke(null, new[] { copy.SnapshotOp, body });
                    }
                    // An empty list would delete the file: never done from here
                    if (world != null && Directory.Exists(world) && copy.SaveTo != null && copy.Server.Count > 0)
                        copy.SaveTo.Invoke(null, new object[] { world });
                }
                catch (Exception e)
                {
                    TFMod.Log.Warning("could not save the seat keys: " + e.GetBaseException().Message);
                }
            }
        }

        public static void Reset()
        {
            _dirty = false;
        }
    }

    /// <summary>A seat's keys were set or its seat removed: write keyseat.txt at once, not at the next game save.</summary>
    [HarmonyPatch]
    internal static class KeySeatChangePatch
    {
        private static bool Prepare() => KeySeatCompat.ConfigsTypes().Any();

        private static IEnumerable<MethodBase> TargetMethods() =>
            KeySeatCompat.ConfigsTypes().Select(t => (MethodBase)AccessTools.Method(t, "ServerOnSet")).Where(m => m != null);

        private static void Postfix() => KeySeatCompat.MarkChanged();
    }

    [HarmonyPatch(typeof(ShipState), nameof(ShipState.RemoveStatefulPart))]
    internal static class KeySeatRemovePatch
    {
        private static bool Prepare() => KeySeatCompat.ConfigsTypes().Any();

        // Only when the removed part was a seat with keys
        private static void Prefix(ShipState __instance, ushort id, out bool __state) =>
            __state = KeySeatCompat.HasKeys(__instance.Id, id);

        private static void Postfix(bool __state)
        {
            if (__state)
                KeySeatCompat.MarkChanged();
        }
    }
}

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using FishNet.Connection;
using HarmonyLib;
using Ships;
using Ships.Cables.Power;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Interface.Values;
using Ships.Rooms;
using Ships.Services;
using StellarDriveDemoTFAdvanced.Common;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Power
{
    /// <summary>
    /// Force field door: an emitter set on the floor of a doorway raises a field of the size the
    /// player chooses (1 to <see cref="MaxSize"/> m wide and high, negative to go the other way).
    /// The field holds the air like a closed door; players (and anything else) go through it,
    /// unless it is set solid. It takes its color from the paint gun. It runs on the ship's power
    /// (<see cref="PowerCatalog.FieldDoorWattsPerSquareMeter"/> per m²).
    /// The whole setting is one value in the part's ConstantState (saved, synchronized):
    /// (flags × 100000 + side × 10000 + width × 100 + height) × 10 + status, flags being
    /// 1 width negative, 2 height negative, 4 solid; the side <see cref="Side"/> (0 for the doors
    /// of 0.28) and the status <see cref="Status"/>.
    /// The host runs the power; every game (host and players) seals its copy of the ship's rooms
    /// from the state, as the game does for its doors (<see cref="FieldDoorRooms"/>).
    /// </summary>
    internal static class FieldDoor
    {
        public const ushort Id = 7339;
        public const int MaxSize = 50;
        public const int DefaultWidth = 2, DefaultHeight = 3;
        // Its only sockets (the game indexes a part's sockets by id: they must be 0 and up)
        public const byte PowerA = 0, PowerB = 1;

        public enum Status
        {
            /// <summary>Switched off by a player.</summary>
            Off = 0,
            /// <summary>The field is up: the doorway is sealed.</summary>
            Up = 1,
            /// <summary>Switched on, but no power network on its sockets.</summary>
            NoNetwork = 2,
            /// <summary>Switched on, but its network cannot give it all its power (it retries every 5 s).</summary>
            LowPower = 3,
            /// <summary>Just switched on, the host has not powered it yet.</summary>
            Starting = 4
        }

        /// <summary>
        /// Where the field goes from the emitter, as seen facing its front (the side without plugs):
        /// centered on it, or only to the left or to the right of it.
        /// </summary>
        public enum Side
        {
            Center = 0,
            Left = 1,
            Right = 2
        }

        public readonly struct Config
        {
            /// <summary>Size in meters (always positive; see <see cref="FlipWidth"/>, <see cref="FlipHeight"/>).</summary>
            public readonly int Width, Height;
            public readonly Side Side;
            public readonly Status Status;
            /// <summary>Negative width: the field goes to the other side.</summary>
            public readonly bool FlipWidth;
            /// <summary>Negative height: the field goes down from the emitter (set on a ceiling).</summary>
            public readonly bool FlipHeight;
            /// <summary>Solid: nothing goes through the field.</summary>
            public readonly bool Solid;

            /// <summary><paramref name="width"/> and <paramref name="height"/> signed (0 counts as 1).</summary>
            public Config(int width, int height, Status status, Side side = Side.Center, bool solid = false)
            {
                FlipWidth = width < 0;
                FlipHeight = height < 0;
                Width = Mathf.Clamp(Mathf.Abs(width), 1, MaxSize);
                Height = Mathf.Clamp(Mathf.Abs(height), 1, MaxSize);
                Side = side;
                Status = status;
                Solid = solid;
            }

            public int SignedWidth => FlipWidth ? -Width : Width;
            public int SignedHeight => FlipHeight ? -Height : Height;
            public bool On => Status != Status.Off;
            public bool Sealed => Status == Status.Up;
            private int Flags => (FlipWidth ? 1 : 0) | (FlipHeight ? 2 : 0) | (Solid ? 4 : 0);
            /// <summary>Everything but the status.</summary>
            public long Setting => Flags * 100000L + (int)Side * 10000 + Width * 100 + Height;
            public double Value => Setting * 10 + (int)Status;
            public Config With(Status status) => new Config(SignedWidth, SignedHeight, status, Side, Solid);

            /// <summary>The side the field really goes to: a negative width mirrors it.</summary>
            public Side Toward => !FlipWidth ? Side : Side == Side.Left ? Side.Right : Side == Side.Right ? Side.Left : Side.Center;

            /// <summary>
            /// The field's middle along the emitter (its local x, m). Facing the front, the
            /// emitter's +x is on the left.
            /// </summary>
            public float Shift => Shift(Width, Toward);
        }

        public static float Shift(int width, Side side) => side == Side.Left ? width * 0.5f : side == Side.Right ? -width * 0.5f : 0f;

        public static Config Decode(double value)
        {
            long v = (long)System.Math.Round(System.Math.Abs(value));
            int status = (int)(v % 10);
            long setting = v / 10;
            int flags = (int)(setting / 100000 % 8);
            long rest = setting % 100000;
            int side = (int)(rest / 10000);
            long size = rest % 10000;
            int width = (int)(size / 100), height = (int)(size % 100);
            if (width < 1 || height < 1 || width > MaxSize || height > MaxSize)
            {
                width = DefaultWidth;
                height = DefaultHeight;
            }
            if ((flags & 1) != 0)
                width = -width;
            if ((flags & 2) != 0)
                height = -height;
            return new Config(width, height, status <= (int)Status.Starting ? (Status)status : Status.Off, side <= (int)Side.Right ? (Side)side : Side.Center, (flags & 4) != 0);
        }

        public static Config Decode(IStatefulPartState state) =>
            state is ConstantState constant ? Decode(constant.ConstantValue) : new Config(DefaultWidth, DefaultHeight, Status.Off);

        public static float Watts(Config config) => Mathf.Max(0f, PowerCatalog.FieldDoorWattsPerSquareMeter.Value) * config.Width * config.Height;

        // ---- Requests from the players (the door's window, the manager screen): "setting on"

        public static void Install() => TFNet.OnServer(TFMessageKind.FieldDoor, OnRequest);

        /// <summary>Asks the host for this setting (size, side, solid) and on (or off).</summary>
        public static void Request(PartKey door, Config config, bool on) =>
            TFNet.SendToServer(TFMessageKind.FieldDoor, door.ShipId, door.PartId, $"{config.Setting} {(on ? 1 : 0)}");

        private static void OnRequest(NetworkConnection sender, TFMessageKind kind, uint shipId, ushort partId, string text)
        {
            string[] words = text.Split(' ');
            if (words.Length != 2 || !long.TryParse(words[0], out long setting) || setting < 0 || setting >= 800000 || !int.TryParse(words[1], out int on))
                return;
            var ships = StellarDriveDemoTFAdvanced.Common.GameServices.ShipsServer;
            if (ships == null || !(ships.GetShip(shipId) is TrackedShipServer ship))
                return;
            StatefulPart part = Find(ship, partId);
            if (part == null || !(part.State is ConstantState state))
                return;
            Config old = Decode(state);
            Config wanted = Decode(setting * 10.0);
            // On, it keeps how it runs (a resize does not drop the field); the host powers it next tick
            Status status = on == 0 ? Status.Off : old.On ? old.Status : Status.Starting;
            Config config = wanted.With(status);
            if (config.Value == state.ConstantValue)
                return;
            ship.UpdatePartState(part.Id, state with { ConstantValue = config.Value });
            AdvancedMod.Log.Msg($"force field door {shipId}:{partId}: {config.SignedWidth} x {config.SignedHeight} m, {config.Side}{(config.Solid ? ", solid" : "")}, {config.Status}");
        }

        private static StatefulPart Find(TrackedShipServer ship, ushort partId)
        {
            foreach (StatefulPart part in ship.StatefulParts)
            {
                if (part != null && part.Id == partId && part.Settings != null && part.Settings.id == Id)
                    return part;
            }
            return null;
        }

        // ---- Power (host)

        private sealed class Runtime
        {
            public PowerGroupServerState Group;
            public float LowPower, RetryAt;
            public Status Status;
        }

        private struct Entry
        {
            public TrackedShipServer Ship;
            public StatefulPart Part;
            public Runtime Runtime;
        }

        private static readonly Dictionary<(uint, ushort), Runtime> Runtimes = new Dictionary<(uint, ushort), Runtime>();
        private static readonly List<Entry> Doors = new List<Entry>();

        public static void Forget()
        {
            Runtimes.Clear();
            Doors.Clear();
        }

        public static void BeforeTransfers(PowerServerTracker power, ShipsServerTracker ships, float deltaTime)
        {
            Doors.Clear();
            if (DevHarness.Off("fielddoorpower"))
                return;
            float now = Time.unscaledTime;
            foreach (TrackedShipServer ship in ships.LoadedShipsWithChildren)
            {
                foreach (StatefulPart part in ship.GetAllStatefulParts<ConstantState>())
                {
                    if (part.Settings == null || part.Settings.id != Id)
                        continue;
                    Config config = Decode(part.State);
                    if (!config.On)
                    {
                        Runtimes.Remove((ship.Id, part.Id));
                        continue;
                    }
                    if (!Runtimes.TryGetValue((ship.Id, part.Id), out Runtime rt))
                        Runtimes[(ship.Id, part.Id)] = rt = new Runtime();
                    rt.Group = null;
                    rt.Status = config.Status;
                    Doors.Add(new Entry { Ship = ship, Part = part, Runtime = rt });
                    if (!power.TryGetGroupFromSocket(new EntityPartSocket(ship.Id, part.Id, PowerA), out PowerGroupServerState group)
                        && !power.TryGetGroupFromSocket(new EntityPartSocket(ship.Id, part.Id, PowerB), out group))
                    {
                        rt.Status = Status.NoNetwork;
                        continue;
                    }
                    if (now < rt.RetryAt)
                    {
                        rt.Status = Status.LowPower;
                        continue;
                    }
                    group.TotalPowerNeeded += Watts(config) * deltaTime;
                    rt.Group = group;
                }
            }
        }

        public static void AfterTransfers(float deltaTime)
        {
            float now = Time.unscaledTime;
            foreach (Entry entry in Doors)
            {
                Runtime rt = entry.Runtime;
                if (rt.Group != null)
                {
                    if (rt.Group.NormalizedConsumerRate > 0.9f)
                    {
                        rt.LowPower = 0f;
                        rt.Status = Status.Up;
                    }
                    else
                    {
                        // A short dip does not drop the field (and let the air out)
                        rt.LowPower += deltaTime;
                        if (rt.LowPower > 1.5f)
                        {
                            rt.LowPower = 0f;
                            rt.RetryAt = now + 5f;
                            rt.Status = Status.LowPower;
                        }
                        else if (rt.Status != Status.Up)
                        {
                            rt.Status = Status.LowPower;
                        }
                    }
                }
                Config config = Decode(entry.Part.State);
                if (config.On && config.Status != rt.Status)
                {
                    var state = (ConstantState)entry.Part.State;
                    entry.Ship.UpdatePartState(entry.Part.Id, state with { ConstantValue = config.With(rt.Status).Value });
                }
            }
        }
    }

    /// <summary>
    /// The rooms (air) side, on every game: while a field door is up, its field is a wall in the
    /// ship's room voxels, added and removed like the game does for its doors closing and opening.
    /// The field is laid in the plane of the hull's faces when it stands along the ship's grid,
    /// and reaches half a meter past its sides, so it closes a doorway of its size even placed a
    /// little off.
    /// </summary>
    internal static class FieldDoorRooms
    {
        private sealed class Applied
        {
            public double Value;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3Int[] Walls, Scan;
        }

        private static readonly AccessTools.FieldRef<ShipRooms, ShipRoomsVoxelUpdater> Updater =
            AccessTools.FieldRefAccess<ShipRooms, ShipRoomsVoxelUpdater>("_voxelUpdater");
        private static readonly ConditionalWeakTable<ShipRooms, Dictionary<(ushort, Vector3Int), Applied>> Fields =
            new ConditionalWeakTable<ShipRooms, Dictionary<(ushort, Vector3Int), Applied>>();
        // The key of a door on the rooms' own ship (a docked ship's are keyed by their offset)
        private static readonly Vector3Int OwnShip = new Vector3Int(int.MinValue, 0, 0);

        private const float Margin = 0.5f;

        /// <summary>
        /// Brings the rooms up to date with this door. <paramref name="offset"/> and
        /// <paramref name="rotation"/>: where a docked ship's voxels sit in the rooms' ship
        /// (null for the rooms' own ship).
        /// </summary>
        public static void Refresh(ShipRooms rooms, StatefulPart part, IStatefulPartState state, bool removed, Vector3? offset, Quaternion rotation)
        {
            if (rooms == null || part?.Settings == null || part.Settings.id != FieldDoor.Id)
                return;
            try
            {
                Dictionary<(ushort, Vector3Int), Applied> fields = Fields.GetOrCreateValue(rooms);
                var key = (part.Id, offset.HasValue ? Vector3Int.RoundToInt(offset.Value) : OwnShip);
                FieldDoor.Config config = FieldDoor.Decode(state);
                bool seal = !removed && config.Sealed;
                fields.TryGetValue(key, out Applied applied);
                if (seal && applied != null && SameField(FieldDoor.Decode(applied.Value), config) && applied.Position == part.LocalPosition && applied.Rotation == part.LocalRotation)
                    return;
                ShipRoomsVoxelUpdater updater = Updater(rooms);
                if (applied != null)
                {
                    updater.RemoveHullVoxel(applied.Walls, applied.Scan);
                    fields.Remove(key);
                }
                if (!seal)
                    return;
                Voxels(part.LocalPosition, part.LocalRotation, config.Width, config.Height, config.Shift, config.FlipHeight, out Vector3Int[] walls, out Vector3Int[] scan);
                if (offset.HasValue)
                {
                    Convert(walls, offset.Value, rotation);
                    Convert(scan, offset.Value, rotation);
                }
                updater.AddHullVoxel(walls, scan);
                if (DevHarness.Active)
                    AdvancedMod.Log.Msg($"dev: force field door {part.Id} seals its rooms: {config.Width} x {config.Height} m, {walls.Length} wall voxels{(offset.HasValue ? " (docked ship)" : "")}");
                fields[key] = new Applied { Value = config.Value, Position = part.LocalPosition, Rotation = part.LocalRotation, Walls = walls, Scan = scan };
            }
            catch (System.Exception e)
            {
                ErrorLog.Report("force field door rooms", e);
            }
        }

        private static bool SameField(FieldDoor.Config a, FieldDoor.Config b) => a.Setting == b.Setting && a.Sealed == b.Sealed;

        private static Vector3Int ToVoxel(Vector3 position) => Vector3Int.RoundToInt((position + Vector3.one * 0.5f) * ShipRoomsConstants.RoomVoxelResolution);

        /// <summary>
        /// The field's wall voxels (three voxels thick) and the voxels on both sides of it the
        /// game grows the rooms from, in the ship's room voxels (4 per meter, hull faces at k + ½ m).
        /// </summary>
        public static void Voxels(Vector3 position, Quaternion rotation, int width, int height, float shift, bool down, out Vector3Int[] walls, out Vector3Int[] scan)
        {
            Vector3 right = rotation * Vector3.right, up = rotation * (down ? Vector3.down : Vector3.up), normal = rotation * Vector3.forward;
            Vector3 center = position + right * shift;
            // Along the grid: in the plane of the nearest hull faces
            int axis = Mathf.Abs(normal.x) > Mathf.Abs(normal.y) ? (Mathf.Abs(normal.x) > Mathf.Abs(normal.z) ? 0 : 2) : (Mathf.Abs(normal.y) > Mathf.Abs(normal.z) ? 1 : 2);
            if (Mathf.Abs(normal[axis]) > 0.99f)
                center[axis] = Mathf.Round(center[axis] - 0.5f) + 0.5f;
            float voxel = 1f / ShipRoomsConstants.RoomVoxelResolution, step = voxel * 0.5f;
            var wallSet = new HashSet<Vector3Int>();
            float halfWidth = width * 0.5f;
            for (float u = -halfWidth - Margin; u <= halfWidth + Margin + 0.001f; u += step)
            {
                for (float v = -Margin; v <= height + Margin + 0.001f; v += step)
                {
                    Vector3 p = center + right * u + up * v;
                    for (int t = -1; t <= 1; t++)
                        wallSet.Add(ToVoxel(p + normal * (t * voxel)));
                }
            }
            var scanSet = new HashSet<Vector3Int>();
            for (float u = -halfWidth + 0.25f; u < halfWidth; u += 0.5f)
            {
                for (float v = 0.25f; v < height; v += 0.5f)
                {
                    Vector3 p = center + right * u + up * v;
                    foreach (float side in new[] { -3f, 3f })
                    {
                        Vector3Int seed = ToVoxel(p + normal * (side * voxel));
                        if (!wallSet.Contains(seed))
                            scanSet.Add(seed);
                    }
                }
            }
            walls = new Vector3Int[wallSet.Count];
            wallSet.CopyTo(walls);
            scan = new Vector3Int[scanSet.Count];
            scanSet.CopyTo(scan);
        }

        // As ShipRooms.ConvertRelativeVoxel: a docked ship's voxels into the rooms' ship
        private static void Convert(Vector3Int[] voxels, Vector3 offset, Quaternion rotation)
        {
            Vector3 half = Vector3.one * ShipRoomsConstants.RoomVoxelResolution / 2f;
            for (int i = 0; i < voxels.Length; i++)
                voxels[i] = Vector3Int.RoundToInt(rotation * (voxels[i] - half) + offset + half);
        }
    }

    [HarmonyPatch(typeof(ShipRooms))]
    internal static class FieldDoorRoomsPatch
    {
        [HarmonyPostfix, HarmonyPatch(nameof(ShipRooms.InitializeStatefulPart))]
        private static void Initialize(ShipRooms __instance, StatefulPart part) =>
            FieldDoorRooms.Refresh(__instance, part, part?.State, false, null, Quaternion.identity);

        [HarmonyPostfix, HarmonyPatch(nameof(ShipRooms.RemoveStatefulPart))]
        private static void Remove(ShipRooms __instance, StatefulPart part) =>
            FieldDoorRooms.Refresh(__instance, part, part?.State, true, null, Quaternion.identity);

        [HarmonyPostfix, HarmonyPatch(nameof(ShipRooms.OnStatefulPartStateChange))]
        private static void Change(ShipRooms __instance, StatefulPartStateChangeEvent change) =>
            FieldDoorRooms.Refresh(__instance, change.Part, change.NewState, false, null, Quaternion.identity);

        [HarmonyPostfix, HarmonyPatch(nameof(ShipRooms.InitializeRelativeStatefulPart))]
        private static void InitializeRelative(ShipRooms __instance, StatefulPart part, Vector3 offset, Quaternion rotation) =>
            FieldDoorRooms.Refresh(__instance, part, part?.State, false, offset, rotation);

        [HarmonyPostfix, HarmonyPatch(nameof(ShipRooms.RemoveRelativeStatefulPart))]
        private static void RemoveRelative(ShipRooms __instance, StatefulPart part, Vector3 offset, Quaternion rotation) =>
            FieldDoorRooms.Refresh(__instance, part, part?.State, true, offset, rotation);

        [HarmonyPostfix, HarmonyPatch(nameof(ShipRooms.OnRelativeStatefulPartStateChange))]
        private static void ChangeRelative(ShipRooms __instance, StatefulPartStateChangeEvent change, Vector3 offset, Quaternion rotation) =>
            FieldDoorRooms.Refresh(__instance, change.Part, change.NewState, false, offset, rotation);
    }
}

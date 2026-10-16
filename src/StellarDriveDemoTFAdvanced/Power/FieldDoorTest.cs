using System.Collections.Generic;
using System.Linq;
using Core.Services;
using Ships.Interface.Model.State;
using Ships.Rooms;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;
using WorldProvision.Interface.Services;

namespace StellarDriveDemoTFAdvanced.Power
{
    /// <summary>
    /// Development check (DevFieldDoorTest): in a room voxel grid of its own (the game's room
    /// code, not a ship's), a 4 × 3 × 4 m box of hull walls with a 2 × 2 m doorway, then the
    /// force field door's voxels in it: the log tells whether the box holds a room (air) without
    /// the field (it must not) and with it, placed in the doorway, off-center, turned round.
    /// </summary>
    internal static class FieldDoorTest
    {
        public static void Install()
        {
            DevHarness.Steps.Add((20f, "force field door test", () =>
            {
                if (Settings.DevFieldDoorTest.Value)
                    Run();
            }));
            // With DevShowcase 7339: its window on the showcase's door, a resize and switching off
            DevHarness.Steps.Add((52f, "force field door window", () =>
            {
                if (Settings.DevFieldDoorTest.Value)
                    MelonLoader.MelonCoroutines.Start(Window());
            }));
        }

        private static System.Collections.IEnumerator Window()
        {
            FieldDoorVisuals door = Object.FindObjectsByType<FieldDoorVisuals>(FindObjectsSortMode.None).FirstOrDefault(d => d.HasContext);
            if (door == null)
            {
                AdvancedMod.Log.Warning("dev: force field door window: no placed door");
                yield break;
            }
            FieldDoorMenu.Show(door.Key);
            yield return new WaitForSecondsRealtime(1.5f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(DevHarness.Folder, "fielddoor_menu.png"));
            yield return new WaitForSecondsRealtime(1f);
            FieldDoor.Request(door.Key, 4, 3, true);
            yield return new WaitForSecondsRealtime(1.5f);
            AdvancedMod.Log.Msg($"dev: force field door window: after resize {Describe(door)}");
            FieldDoor.Request(door.Key, 4, 3, false);
            yield return new WaitForSecondsRealtime(1.5f);
            AdvancedMod.Log.Msg($"dev: force field door window: after switching off {Describe(door)}");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(DevHarness.Folder, "fielddoor_preview.png"));
            yield return new WaitForSecondsRealtime(1f);
            FieldDoor.Request(door.Key, 4, 3, true);
            yield return new WaitForSecondsRealtime(1.5f);
            AdvancedMod.Log.Msg($"dev: force field door window: switched on again {Describe(door)}");
            TFItems.Common.ModMenu.Close();
        }

        private static string Describe(FieldDoorVisuals door)
        {
            FieldDoor.Config config = FieldDoor.Decode(FieldDoorVisuals.Values.TryGetValue(door.Key, out double value) ? value : 0);
            return $"{config.Width} x {config.Height} {config.Status}";
        }

        private static void Run()
        {
            var ships = GameServices.ShipsServer;
            IShipStateRead ship = ships?.LoadedShipsWithChildren.FirstOrDefault();
            var env = ServiceLocator.GetService<IWorldEnvironmentProvider>();
            if (ship == null || env == null)
            {
                AdvancedMod.Log.Warning("dev: force field door test: no ship or environment");
                return;
            }
            Check("doorway open", ship, env, null);
            Check("field in the doorway", ship, env, (new Vector3(1.5f, -0.5f, 3.5f), Quaternion.identity, 2, 2));
            Check("field 0.3 m inside, 0.3 m off-center", ship, env, (new Vector3(1.8f, -0.5f, 3.2f), Quaternion.identity, 2, 2));
            Check("field turned round", ship, env, (new Vector3(1.5f, -0.5f, 3.6f), Quaternion.Euler(0f, 180f, 0f), 2, 2));
            Check("field too small (1 x 1)", ship, env, (new Vector3(1.5f, -0.5f, 3.5f), Quaternion.identity, 1, 1));
            Check("field 10 x 20 over the box", ship, env, (new Vector3(1.5f, -0.5f, 3.5f), Quaternion.identity, 10, 20));
            // Up, then down again: the room goes
            var tree = new ShipRoomsVoxelsTree();
            var gases = new ShipRoomsGases();
            var updater = new ShipRoomsVoxelUpdater(tree, gases, env, ship);
            Box(updater);
            FieldDoorRooms.Voxels(new Vector3(1.5f, -0.5f, 3.5f), Quaternion.identity, 2, 2, out Vector3Int[] walls, out Vector3Int[] scan);
            updater.AddHullVoxel(walls, scan);
            updater.UpdateRooms();
            bool up = tree.SampleRoomAtPosition(new Vector3(1.5f, 1f, 1.5f)).RoomId != 0;
            updater.RemoveHullVoxel(walls, scan);
            updater.UpdateRooms();
            bool down = tree.SampleRoomAtPosition(new Vector3(1.5f, 1f, 1.5f)).RoomId != 0;
            AdvancedMod.Log.Msg($"dev: force field door test: field up then down: room {(up ? "yes" : "NO")} then {(down ? "STILL" : "gone")}");
        }

        private static void Check(string name, IShipStateRead ship, IWorldEnvironmentProvider env, (Vector3 At, Quaternion Rotation, int W, int H)? field)
        {
            var tree = new ShipRoomsVoxelsTree();
            var gases = new ShipRoomsGases();
            var updater = new ShipRoomsVoxelUpdater(tree, gases, env, ship);
            Box(updater);
            int fieldVoxels = 0;
            if (field.HasValue)
            {
                FieldDoorRooms.Voxels(field.Value.At, field.Value.Rotation, field.Value.W, field.Value.H, out Vector3Int[] walls, out Vector3Int[] scan);
                updater.AddHullVoxel(walls, scan);
                fieldVoxels = walls.Length;
            }
            updater.UpdateRooms();
            var inside = tree.SampleRoomAtPosition(new Vector3(1.5f, 1f, 1.5f));
            var outside = tree.SampleRoomAtPosition(new Vector3(1.5f, 1f, 5f));
            string gas = gases.TryGetRoomGasState(inside.RoomId, out var state) ? $", volume {state.RoomVolume:0.###}" : "";
            AdvancedMod.Log.Msg($"dev: force field door test: {name}: room inside {(inside.RoomId != 0 ? "yes (" + inside.RoomId + ")" : "no")}{gas}, outside {outside.RoomId}, field voxels {fieldVoxels}");
        }

        // Hull-like walls: planes of room voxels on the cells' faces (k + ½ m, voxel 4k + 4), around
        // the cells x 0-3, y 0-2, z 0-3, with a doorway 2 m wide (x 0.5-2.5) and 2 m high in the +z wall
        private static void Box(ShipRoomsVoxelUpdater updater)
        {
            var walls = new List<Vector3Int>();
            for (int a = 0; a <= 16; a++)
            {
                for (int b = 0; b <= 16; b++)
                {
                    if (b <= 12)
                    {
                        walls.Add(new Vector3Int(0, b, a));
                        walls.Add(new Vector3Int(16, b, a));
                        walls.Add(new Vector3Int(a, b, 0));
                        bool doorway = a > 4 && a < 12 && b > 0 && b < 8;
                        if (!doorway)
                            walls.Add(new Vector3Int(a, b, 16));
                    }
                    walls.Add(new Vector3Int(a, 0, b));
                    walls.Add(new Vector3Int(a, 12, b));
                }
            }
            updater.AddHullVoxel(walls.Distinct().ToArray(), new[] { new Vector3Int(8, 6, 8), new Vector3Int(8, 4, 20) });
        }
    }
}

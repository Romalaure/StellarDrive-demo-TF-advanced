using System.Collections.Generic;
using HarmonyLib;
using Ships.Interface.Model;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using Ships.Interface.Model.Placement;
using Ships.Interface.Services;
using Tools.Build.Model;
using Tools.Build.SnapDetectors.Model;
using Tools.Build.SnapDetectors.Types;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Construction
{
    /// <summary>
    /// How some parts sit on the hull. The game lays a precisely placed part on the face aimed at
    /// (its up out of the face), which suits flat parts but not these:
    ///  - a standing part (the speaker) aimed at a wall stands upright against it, its front out
    ///    of the wall, instead of lying on its back.
    /// </summary>
    [HarmonyPatch(typeof(PreciseFloatingSnapDetector), nameof(PreciseFloatingSnapDetector.DetectSnapPlacement))]
    internal static class HullMount
    {
        private const float Gap = 0.01f, FloorTop = 0.04f;

        /// <summary>Parts made to stand: upright against walls.</summary>
        internal static readonly HashSet<ushort> Standing = new HashSet<ushort> { TFItems.Devices.Radio.SpeakerId, TFItems.Devices.Radio.RadioId };

        /// <summary>Standing parts whose front is their -z (the radio's screen, the speaker's cones: the Blender models' front): turned around to face out.</summary>
        internal static bool FrontIsBack(ushort id) => id == TFItems.Devices.Radio.RadioId || id == TFItems.Devices.Radio.SpeakerId;

        private static void Postfix(HitSnapDetectionProperties props, IShipPartPlacements shipGrid, ref IPartPlacement __result)
        {
            if (!(__result is FloatingPartPlacement placed) || FloatingSnap.Placing == null)
                return;
            ushort id = FloatingSnap.Placing.id;
            if (FloatingSnap.Placing.name.StartsWith("TFA_") && !DiagonalDoors.IsDoor(id))
                FreeSeen = Time.unscaledTime;
            if (TFItems.Lights.LampCatalog.IsLamp(id) && id != TFItems.Lights.LampCatalog.FloorPanelId)
            {
                ToTheEdge(props, shipGrid, ref __result, placed);
                return;
            }
            if (MaterialParts.IsConstruction(id) || id == TFItems.Lights.LampCatalog.FloorPanelId)
            {
                Aligned(props, shipGrid, ref __result, placed, MaterialParts.IsConstruction(id) ? BuildTilt.For(id) : Quaternion.identity);
                return;
            }
            Vector3 n = placed.UpOrientation.GetVector3();
            bool wall = Mathf.Abs(n.y) <= 0.1f && (Mathf.Abs(n.x) <= 0.1f || Mathf.Abs(n.z) <= 0.1f);
            if (Standing.Contains(id) && wall)
            {
                Vector3 facing = FrontIsBack(id) ? -n : n;
                Quaternion rotation = Quaternion.LookRotation(facing, Vector3.up);
                Bounds bounds = props.Bounds;
                Vector3 center = rotation * bounds.center;
                float along = Vector3.Dot(props.Hit.Pos, n) + Gap - (Vector3.Dot(center, n) - FloatingSnap.Extent(bounds, rotation, n));
                Vector3 coord = props.Hit.Pos - center;
                coord += n * (along - Vector3.Dot(coord, n));
                var upright = new FloatingPartPlacement(coord, FrontIsBack(id) ? placed.UpOrientation.GetInverse() : placed.UpOrientation, PartOrientation.Up);
                if (shipGrid.IsFloatingPlacementFree(upright, bounds))
                    __result = placed = upright;
            }
            if (FloatingSnap.Placing.name.StartsWith("TFA_") && !StationModules.ById.ContainsKey(id) && !DiagonalDoors.IsDoor(id))
                OnGrid(props, shipGrid, ref __result, placed);
        }

        private static readonly KeySetting FreeKey = new KeySetting(() => Settings.BuildFreeKey);

        /// <summary>The free placement key is held: parts go where the player aims, to the centimeter.</summary>
        internal static bool Free => FreeKey.IsHeld;
        internal static string FreeLabel => FreeKey.Label;
        /// <summary>When a TF part was last placed on the grid, for the key hint.</summary>
        internal static float FreeSeen = -10f;

        /// <summary>The game's grid for precise parts (25 cm, shifted 12.5 cm along a side under 1 m), or 1 cm in free placement.</summary>
        internal static float Grid(float at, float size) =>
            Free ? Mathf.Round(at * 100f) / 100f
            : size < 1f ? Mathf.Round(at * 4f - 0.5f) / 4f + 0.125f
            : Mathf.Round(at * 4f) / 4f;

        // The game grids a part's origin, which is off the middle on many of this mod's models
        private static void OnGrid(HitSnapDetectionProperties props, IShipPartPlacements shipGrid, ref IPartPlacement result, FloatingPartPlacement placed)
        {
            Vector3 n = placed.UpOrientation.GetVector3();
            Quaternion rotation = placed.Rotation;
            Bounds bounds = props.Bounds;
            Vector3 offset = rotation * bounds.center;
            Vector3 coord = placed.Coord;
            foreach (Vector3 axis in new[] { Vector3.right, Vector3.up, Vector3.forward })
            {
                if (Mathf.Abs(Vector3.Dot(axis, n)) > 0.5f)
                    continue;
                float middle = Vector3.Dot(coord + offset, axis);
                float at = Free ? Vector3.Dot(props.Hit.Pos, axis) : middle;
                coord += axis * (Grid(at, 2f * FloatingSnap.Extent(bounds, rotation, axis)) - middle);
            }
            var gridded = new FloatingPartPlacement(coord, placed.ForwardOrientation, placed.UpOrientation);
            if (shipGrid.IsFloatingPlacementFree(gridded, bounds))
                result = gridded;
        }

        /// <summary>
        /// A lamp on the hull goes where the player aims on the game's grid, up to the edge of the cell
        /// it is in but not past it (the game kept it well inside; past the edge it would stand where
        /// a wall goes and keep the wall from being placed). Longer than a cell, it only takes the grid.
        /// </summary>
        private static void ToTheEdge(HitSnapDetectionProperties props, IShipPartPlacements shipGrid, ref IPartPlacement result, FloatingPartPlacement placed)
        {
            Vector3 n = placed.UpOrientation.GetVector3();
            Quaternion rotation = placed.Rotation;
            Bounds bounds = props.Bounds;
            Vector3 offset = rotation * bounds.center;
            Vector3 coord = placed.Coord;
            foreach (Vector3 axis in new[] { Vector3.right, Vector3.up, Vector3.forward })
            {
                if (Mathf.Abs(Vector3.Dot(axis, n)) > 0.5f)
                    continue;
                float half = FloatingSnap.Extent(bounds, rotation, axis);
                float aim = Vector3.Dot(props.Hit.Pos, axis);
                float center = Grid(aim, 2f * half);
                if (half < 0.5f && !Free)
                {
                    float cell = Mathf.Round(aim);
                    center = Mathf.Clamp(center, cell - 0.5f + half + 0.003f, cell + 0.5f - half - 0.003f);
                }
                coord += axis * (center - Vector3.Dot(offset, axis) - Vector3.Dot(coord, axis));
            }
            var edge = new FloatingPartPlacement(coord, placed.ForwardOrientation, placed.UpOrientation);
            if (shipGrid.IsFloatingPlacementFree(edge, bounds))
                result = edge;
        }

        /// <summary>
        /// Construction parts and furniture on the hull line up with its cells (centered on whole
        /// meters): across the face, a part 1 or 3 m long is centered on a cell, 2 m on a cell's edge,
        /// a thin side (railings, partitions) on a cell's edge, anything else on a 25 cm grid, so floor plates, partitions and windows sit edge to edge
        /// with the hull and with each other.
        /// </summary>
        private static void Aligned(HitSnapDetectionProperties props, IShipPartPlacements shipGrid, ref IPartPlacement result, FloatingPartPlacement placed, Quaternion tilt)
        {
            Vector3 n = placed.UpOrientation.GetVector3();
            // Turned with the tilt key, then snapped to the ship's axes
            Quaternion turned = Quaternion.LookRotation(placed.ForwardOrientation.GetVector3(), n) * tilt;
            PartOrientation up = FloatingSnap.Nearest(turned * Vector3.up);
            PartOrientation forward = FloatingSnap.Nearest(turned * Vector3.forward, up.GetVector3());
            Quaternion rotation = Quaternion.LookRotation(forward.GetVector3(), up.GetVector3());
            Bounds bounds = props.Bounds;
            // Its side toward the face lying on the face, around where the player aims
            Vector3 offset = rotation * bounds.center;
            float along = Vector3.Dot(props.Hit.Pos, n) + 0.002f - (Vector3.Dot(offset, n) - FloatingSnap.Extent(bounds, rotation, n));
            Vector3 coord = props.Hit.Pos - offset;
            coord += n * (along - Vector3.Dot(coord, n));
            Vector3 center = coord + offset;
            foreach (Vector3 axis in new[] { Vector3.right, Vector3.up, Vector3.forward })
            {
                if (Mathf.Abs(Vector3.Dot(axis, n)) > 0.5f)
                    continue;
                float size = 2f * FloatingSnap.Extent(bounds, rotation, axis);
                float at = Vector3.Dot(center, axis);
                int meters = Mathf.RoundToInt(size);
                // (a thin piece's box is a few centimeters short of its length)
                bool whole = meters >= 1 && Mathf.Abs(size - meters) < 0.1f;
                // A thin part (railing, partition, window) stands on a cell's edge like the hull's walls,
                // so two of them at a right angle meet at the cell's corner
                float target = Free ? Grid(at, size)
                    : whole ? meters % 2 == 1 ? Mathf.Round(at) : Mathf.Floor(at) + 0.5f
                    : size < 0.15f ? Mathf.Round(at - 0.5f) + 0.5f
                    : Grid(at, size);
                // Up the ship: the hull's floors stand 4 cm over the cells' edges, the part on top of them
                if (axis == Vector3.up && whole && !Free)
                    target += FloorTop;
                coord += axis * (target - at);
            }
            var aligned = new FloatingPartPlacement(coord, forward, up);
            if (shipGrid.IsFloatingPlacementFree(aligned, bounds))
                result = aligned;
        }
    }
}

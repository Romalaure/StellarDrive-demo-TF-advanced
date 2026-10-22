using System.Collections.Generic;
using HarmonyLib;
using Ships.Interface.Model;
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
    ///  - the landing gear aimed at the top of a floor goes under that floor, its leg toward the
    ///    ground (players build from inside; on the floor it pointed up and took the floor's room);
    ///  - a standing part (the speaker) aimed at a wall stands upright against it, its front out
    ///    of the wall, instead of lying on its back.
    /// </summary>
    [HarmonyPatch(typeof(PreciseFloatingSnapDetector), nameof(PreciseFloatingSnapDetector.DetectSnapPlacement))]
    internal static class HullMount
    {
        // A hull floor's thickness: its top and bottom faces
        private const float FloorThickness = 0.08f, Gap = 0.01f;

        /// <summary>Parts made to stand: upright against walls.</summary>
        internal static readonly HashSet<ushort> Standing = new HashSet<ushort> { TFItems.Devices.Radio.SpeakerId };

        private static void Postfix(HitSnapDetectionProperties props, IShipPartPlacements shipGrid, ref IPartPlacement __result)
        {
            if (!(__result is FloatingPartPlacement placed) || FloatingSnap.Placing == null)
                return;
            ushort id = FloatingSnap.Placing.id;
            if (id == Propulsion.LandingGear.Id && placed.UpOrientation == PartOrientation.Up)
            {
                var under = new FloatingPartPlacement(placed.Coord - Vector3.up * FloorThickness, placed.ForwardOrientation, PartOrientation.Down);
                if (shipGrid.IsFloatingPlacementFree(under, props.Bounds))
                    __result = under;
                return;
            }
            if (MaterialParts.IsConstruction(id))
            {
                Aligned(props, shipGrid, ref __result, placed);
                return;
            }
            Vector3 n = placed.UpOrientation.GetVector3();
            if (!Standing.Contains(id) || Mathf.Abs(n.y) > 0.1f || Mathf.Abs(n.x) > 0.1f && Mathf.Abs(n.z) > 0.1f)
                return;
            Quaternion rotation = Quaternion.LookRotation(n, Vector3.up);
            Bounds bounds = props.Bounds;
            Vector3 center = rotation * bounds.center;
            float along = Vector3.Dot(props.Hit.Pos, n) + Gap - (Vector3.Dot(center, n) - FloatingSnap.Extent(bounds, rotation, n));
            Vector3 coord = props.Hit.Pos - center;
            coord += n * (along - Vector3.Dot(coord, n));
            var upright = new FloatingPartPlacement(coord, placed.UpOrientation, PartOrientation.Up);
            if (shipGrid.IsFloatingPlacementFree(upright, bounds))
                __result = upright;
        }

        /// <summary>
        /// Construction parts and furniture on the hull line up with its cells (centered on whole
        /// meters): across the face, a part 1 or 3 m long is centered on a cell, 2 m on a cell's edge,
        /// anything else on a 25 cm grid, so floor plates, partitions and windows sit edge to edge
        /// with the hull and with each other.
        /// </summary>
        private static void Aligned(HitSnapDetectionProperties props, IShipPartPlacements shipGrid, ref IPartPlacement result, FloatingPartPlacement placed)
        {
            Vector3 n = placed.UpOrientation.GetVector3();
            Quaternion rotation = Quaternion.LookRotation(placed.ForwardOrientation.GetVector3(), n);
            Bounds bounds = props.Bounds;
            Vector3 coord = placed.Coord;
            Vector3 center = coord + rotation * bounds.center;
            foreach (Vector3 axis in new[] { Vector3.right, Vector3.up, Vector3.forward })
            {
                if (Mathf.Abs(Vector3.Dot(axis, n)) > 0.5f)
                    continue;
                float size = 2f * FloatingSnap.Extent(bounds, rotation, axis);
                float at = Vector3.Dot(center, axis);
                int meters = Mathf.RoundToInt(size);
                float target = meters >= 1 && Mathf.Abs(size - meters) < 0.03f
                    ? meters % 2 == 1 ? Mathf.Round(at) : Mathf.Floor(at) + 0.5f
                    : Mathf.Round(at * 4f) / 4f;
                coord += axis * (target - at);
            }
            var aligned = new FloatingPartPlacement(coord, placed.ForwardOrientation, placed.UpOrientation);
            if (shipGrid.IsFloatingPlacementFree(aligned, bounds))
                result = aligned;
        }
    }
}

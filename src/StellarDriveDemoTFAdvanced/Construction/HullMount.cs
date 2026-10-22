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
    }
}

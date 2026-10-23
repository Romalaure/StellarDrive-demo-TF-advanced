using System.Linq;
using HarmonyLib;
using Ships.Interface.Model;
using Ships.Interface.Model.Placement;
using Ships.Interface.Services;
using Ships.Interface.Settings;
using Ships.Parts.Common.Model;
using Tools.Build;
using Tools.Build.Model;
using Tools.Build.SnapDetectors;
using Tools.Build.SnapDetectors.Model;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Construction
{
    /// <summary>
    /// Placed parts on placed parts. The game snaps a precisely placed part (machines, furniture,
    /// this mod's construction sets and station modules) only onto hull: aimed at another placed
    /// part, nothing happens. This adds that case:
    ///  - on a top or bottom face, the part stands on it (or hangs under it), like on a floor;
    ///  - on a side face, the part keeps its upright and goes against that face, its base level
    ///    with the base of the part it touches (planks beside planks, walls in a row);
    ///  - a station module set against another module lines up with it, doorway to doorway,
    ///    turned so its doors face the module it joins.
    /// Positions are on a 25 cm grid counted from the part aimed at.
    /// </summary>
    internal static class FloatingSnap
    {
        /// <summary>The collider the build tool's ray hit and the part being placed, for the detector.</summary>
        internal static Collider HitCollider;
        internal static PartSettings Placing;
        /// <summary>The last place tried, for the development check.</summary>
        internal static string LastTried;

        // Construction parts touch (a row of planks, walls in a line); lamps on a fine grid, so they reach a part's edge
        private const float Gap = 0.02f, BuildGap = 0.002f, ModuleGap = 0.04f, Step = 0.25f, LampStep = 0.05f;
        private static readonly Detector Instance = new Detector();

        public static bool IsPrecise(SnappingStyle style) =>
            style == SnappingStyle.PreciseOnAny || style == SnappingStyle.PreciseOnFloor || style == SnappingStyle.PreciseOnWall;

        [HarmonyPatch(typeof(PartPreviewDetection), "TryFindSnapInRayHit")]
        private static class RememberHit
        {
            private static void Prefix(SnapDetectionState state, RaycastHit hit)
            {
                HitCollider = hit.collider;
                Placing = state.partSettings;
            }
        }

        [HarmonyPatch(typeof(SnapDetectionStrategyProvider), nameof(SnapDetectionStrategyProvider.GetSnapDetector))]
        private static class OnPlacedParts
        {
            private static void Postfix(SnappingStyle originType, string targetTag, ref ISnapDetector __result)
            {
                if (IsPrecise(originType) && (targetTag == "ShipFloatingPart" || targetTag == "Interactable"))
                    __result = Instance;
            }
        }

        private static readonly PartOrientation[] Axes = { PartOrientation.Forward, PartOrientation.Right, PartOrientation.Back, PartOrientation.Left, PartOrientation.Up, PartOrientation.Down };

        internal static PartOrientation Nearest(Vector3 direction, Vector3? across = null)
        {
            PartOrientation best = PartOrientation.Up;
            float bestDot = float.NegativeInfinity;
            foreach (PartOrientation axis in Axes)
            {
                Vector3 v = axis.GetVector3();
                if (across.HasValue && Mathf.Abs(Vector3.Dot(v, across.Value)) > 0.5f)
                    continue;
                float dot = Vector3.Dot(direction, v);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = axis;
                }
            }
            return best;
        }

        /// <summary>The part the collider belongs to: its frame in the ship and its settings id (0 when unknown).</summary>
        private static bool TryTarget(IShipPartPlacements ship, out Vector3 position, out Quaternion rotation, out ushort settingsId)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            settingsId = 0;
            if (HitCollider == null)
                return false;
            ShipPartIdentifier part = HitCollider.GetComponentInParent<ShipPartIdentifier>();
            if (part == null)
                return false;
            Transform t = part.transform;
            position = ship.ToLocalPos(t.position);
            rotation = ship.ToLocalRotation(t.rotation);
            // The game names a part's collider object after its settings: "TFA_7371_DynamicCollider"
            string name = t.name;
            if (name.StartsWith("TFA_"))
            {
                int end = name.IndexOf('_', 4);
                ushort.TryParse(end > 4 ? name.Substring(4, end - 4) : name.Substring(4), out settingsId);
            }
            return true;
        }

        /// <summary>
        /// The outer face of the box hit, along <paramref name="n"/> in ship space: the ray's hit
        /// point is a little off, enough to put the part into the face.
        /// </summary>
        private static float? FacePlane(IShipPartPlacements ship, Vector3 n)
        {
            if (!(HitCollider is BoxCollider box))
                return null;
            float best = float.NegativeInfinity;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = box.center + Vector3.Scale(box.size * 0.5f, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                best = Mathf.Max(best, Vector3.Dot(ship.ToLocalPos(box.transform.TransformPoint(corner)), n));
            }
            return best;
        }

        private static Vector3 ModuleOffset(ushort id) =>
            StationModules.ById.TryGetValue(id, out StationModules.Spec spec) ? spec.Offset : Vector3.zero;

        // Half the size of rotated bounds along an axis
        internal static float Extent(Bounds bounds, Quaternion rotation, Vector3 axis)
        {
            Vector3 e = bounds.extents;
            return Mathf.Abs(Vector3.Dot(rotation * Vector3.right, axis)) * e.x
                + Mathf.Abs(Vector3.Dot(rotation * Vector3.up, axis)) * e.y
                + Mathf.Abs(Vector3.Dot(rotation * Vector3.forward, axis)) * e.z;
        }

        private static float Snap(float value, float origin, float step = Step) => origin + Mathf.Round((value - origin) / step) * step;

        private sealed class Detector : ISnapDetector
        {
            public Pivot? DetectPivot(HitSnapDetectionProperties props, IShipPartPlacements shipGrid) => null;

            public IPartPlacement DetectSnapPlacement(HitSnapDetectionProperties props, IShipPartPlacements shipGrid)
            {
                try
                {
                    return Detect(props, shipGrid);
                }
                catch (System.Exception e)
                {
                    Common.ErrorLog.Report("placing on a placed part", e);
                    return null;
                }
            }

            private static IPartPlacement Detect(HitSnapDetectionProperties props, IShipPartPlacements ship)
            {
                PartOrientation face = Nearest(props.Hit.Normal);
                Vector3 n = face.GetVector3();
                if (Vector3.Dot(props.Hit.Normal.normalized, n) < 0.7f)
                    return null;
                bool hasTarget = TryTarget(ship, out Vector3 targetPos, out Quaternion targetRot, out ushort targetId);
                ushort placingId = Placing != null ? Placing.id : (ushort)0;
                SnappingStyle style = Placing != null ? Placing.snappingStyle : SnappingStyle.PreciseOnAny;
                bool modules = StationModules.ById.ContainsKey(placingId) && StationModules.ById.ContainsKey(targetId);

                // The part's upright: the preview's, snapped to the ship's axes
                PartOrientation upright = Nearest(props.PartUp);
                bool side = Mathf.Abs(Vector3.Dot(n, upright.GetVector3())) < 0.5f;
                if (style == SnappingStyle.PreciseOnWall && !side)
                    return null;
                // Constructions and modules stand upright against a side; other parts are mounted
                // on it like on the hull: lying on the face, or (wall parts) hung facing out of it
                PartOrientation up, forward;
                if (!side || Builds(placingId))
                {
                    up = side ? upright : face;
                    forward = Nearest(props.PartForward, up.GetVector3());
                }
                else if (HullMount.Standing.Contains(placingId))
                {
                    // Standing parts stand against the side, their front out of it
                    up = upright;
                    forward = HullMount.FrontIsBack(placingId) ? face.GetInverse() : face;
                }
                else if (style == SnappingStyle.PreciseOnWall)
                {
                    up = upright;
                    forward = face.GetInverse();
                }
                else if (style == SnappingStyle.PreciseOnFloor)
                {
                    return null;
                }
                else
                {
                    up = face;
                    forward = Nearest(Quaternion.FromToRotation(props.PartUp, n) * props.PartForward, n);
                }
                Vector3 upVector = up.GetVector3();
                Quaternion rotation = Quaternion.LookRotation(forward.GetVector3(), upVector);
                // A construction part turned further with the tilt key
                if (MaterialParts.IsConstruction(placingId) && !modules)
                {
                    Quaternion tilted = rotation * BuildTilt.For(placingId);
                    up = Nearest(tilted * Vector3.up);
                    upVector = up.GetVector3();
                    forward = Nearest(tilted * Vector3.forward, upVector);
                    rotation = Quaternion.LookRotation(forward.GetVector3(), upVector);
                }
                Bounds bounds = props.Bounds;
                Vector3 frame = hasTarget ? targetPos + targetRot * ModuleOffset(targetId) : Vector3.zero;

                // Along the face's normal: just clear of the face
                float plane = FacePlane(ship, n) ?? Vector3.Dot(props.Hit.Pos, n);

                // A module against a module: doorway to doorway, at the doorway's floor
                if (modules && side && TryJoin(props, n, upVector, frame, targetRot, targetId, placingId, out Quaternion turn, out Vector3 joined))
                {
                    forward = Nearest(turn * Vector3.forward);
                    Vector3 c = turn * bounds.center;
                    float a = plane + ModuleGap - (Vector3.Dot(c, n) - Extent(bounds, turn, n));
                    Vector3 coordJoined = joined - n * Vector3.Dot(joined, n) + n * a;
                    var joinedPlacement = new FloatingPartPlacement(coordJoined, forward, up);
                    LastTried = $"{coordJoined:F2} {forward}/{up} joined";
                    return ship.IsFloatingPlacementFree(joinedPlacement, bounds) ? joinedPlacement : (IPartPlacement)null;
                }

                Vector3 center = rotation * bounds.center;
                float along = plane + (modules ? ModuleGap : Builds(placingId) ? BuildGap : Gap) - (Vector3.Dot(center, n) - Extent(bounds, rotation, n));
                float step = TFItems.Lights.LampCatalog.IsLamp(placingId) ? LampStep : Step;
                // Thin pieces in a line (railings, partitions): their boxes stop short of their ends, the
                // gap between the boxes is what brings the models end to end (they share their end post)
                if (side && Mathf.Abs(Vector3.Dot(rotation * Vector3.right, n)) > 0.9f && MaterialParts.IsThin(placingId) && MaterialParts.IsThin(targetId))
                    along += MaterialParts.EndInset(placingId) + MaterialParts.EndInset(targetId) - BuildGap;

                // Across: around where the player aims, on a grid from the part aimed at
                Vector3 coord = Vector3.zero;
                Vector3 ownOffset = rotation * ModuleOffset(placingId);
                foreach (PartOrientation axisOrientation in new[] { PartOrientation.Right, PartOrientation.Up, PartOrientation.Forward })
                {
                    Vector3 axis = axisOrientation.GetVector3();
                    if (Mathf.Abs(Vector3.Dot(axis, n)) > 0.5f)
                        continue;
                    float origin = Vector3.Dot(frame, axis) - Vector3.Dot(ownOffset, axis);
                    float value;
                    if (side && Builds(placingId) && Mathf.Abs(Vector3.Dot(axis, upVector)) > 0.5f)
                        value = hasTarget ? origin : Snap(Vector3.Dot(props.Hit.Pos, axis), 0f);  // base level with the target's
                    else if (modules)
                        value = origin;  // module centered on the one it joins
                    else
                        value = Snap(Vector3.Dot(props.Hit.Pos, axis) - Vector3.Dot(center, axis), origin, step);
                    coord += axis * value;
                }
                coord += n * along;
                var placement = new FloatingPartPlacement(coord, forward, up);
                LastTried = $"{coord:F2} {forward}/{up} bounds {bounds.center:F2} {bounds.size:F2}";
                return ship.IsFloatingPlacementFree(placement, bounds) ? placement : (IPartPlacement)null;
            }

            /// <summary>
            /// Turns the new module so one of its doorways faces the aimed wall (the turn closest to the
            /// preview's), and places it so that doorway lines up with the aimed wall's doorway nearest
            /// to the crosshair, at that doorway's floor. Returns its part origin (the component along
            /// the wall's normal is set by the caller).
            /// </summary>
            private static bool TryJoin(HitSnapDetectionProperties props, Vector3 n, Vector3 up, Vector3 frame, Quaternion targetRot, ushort targetId, ushort placingId,
                out Quaternion rotation, out Vector3 origin)
            {
                rotation = Quaternion.identity;
                origin = Vector3.zero;
                StationModules.Spec target = StationModules.ById[targetId], placing = StationModules.ById[placingId];
                // The aimed wall, in the target's frame
                Quaternion toTarget = Quaternion.Inverse(targetRot);
                Vector3 nLocal = toTarget * n;
                StationModules.Side wall = new[] { StationModules.Side.Front, StationModules.Side.Back, StationModules.Side.Left, StationModules.Side.Right }
                    .OrderByDescending(sd => Vector3.Dot(StationModules.Normal(sd), nLocal)).First();
                Vector3 hitLocal = toTarget * (props.Hit.Pos - frame);
                int level = Mathf.Clamp(Mathf.FloorToInt(hitLocal.y / target.LevelHeight), 0, target.Levels - 1);
                float aimed = Vector3.Dot(hitLocal, StationModules.Along(wall));
                var doors = target.AllDoors.Where(d => d.Side == wall).ToList();
                // No doorway there: the middle of the wall, at the aimed floor
                StationModules.Door door = doors.Count > 0
                    ? doors.OrderBy(d => Mathf.Abs(d.Offset - aimed) + (d.Level == level ? 0f : 100f)).First()
                    : new StationModules.Door(wall, 0f, level);
                Vector3 doorPoint = frame + targetRot * (StationModules.Along(wall) * door.Offset + Vector3.up * (door.Level * target.LevelHeight));

                // The new module's turns about the up axis, each with its doorways facing the wall
                float best = float.NegativeInfinity;
                bool found = false;
                foreach (PartOrientation f in Axes)
                {
                    Vector3 forward = f.GetVector3();
                    if (Mathf.Abs(Vector3.Dot(forward, up)) > 0.5f)
                        continue;
                    Quaternion turn = Quaternion.LookRotation(forward, up);
                    foreach (StationModules.Door own in placing.AllDoors)
                    {
                        if (Vector3.Dot(turn * StationModules.Normal(own.Side), -n) < 0.9f)
                            continue;
                        // The preview's heading first, then the lowest doorway
                        float score = Vector3.Dot(forward, props.PartForward) - own.Level * 0.1f - Mathf.Abs(own.Offset) * 0.01f;
                        if (score <= best)
                            continue;
                        best = score;
                        found = true;
                        rotation = turn;
                        Vector3 ownDoor = placing.Offset + StationModules.Along(own.Side) * own.Offset + Vector3.up * (own.Level * placing.LevelHeight);
                        origin = doorPoint - turn * ownDoor;
                    }
                }
                return found;
            }
        }

        /// <summary>Parts that stand upright against a side face: this mod's construction sets, furniture, station modules and solar panels.</summary>
        private static bool Builds(ushort id) =>
            StationModules.ById.ContainsKey(id) || id == StationModules.SolarId || (id >= 7331 && id <= 7367) || MaterialParts.IsFurniture(id);
    }
}

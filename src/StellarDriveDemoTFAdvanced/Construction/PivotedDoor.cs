using System;
using System.Linq;
using HarmonyLib;
using Ships.Interface.Model.Parts;
using Ships.Interface.Settings;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StellarDriveDemoTFAdvanced.Construction
{
    /// <summary>
    /// The game's door turned onto the diagonal walls: placed like a wall (the rotate key turns
    /// it 45°), it switches to its pivoted twin, the way the game's walls switch to their diagonal
    /// piece. The twin is the game's door narrowed to one square's diagonal (1.41 m), two squares
    /// high, standing in the square's middle like a diagonal wall: real hull, it seals its room
    /// while shut and opens with the interaction key like the game's door. Only call when SDModKit
    /// is loaded (it copies the door's model).
    /// </summary>
    internal static class PivotedDoor
    {
        public const ushort Id = 8235;
        private const ushort GameDoor = 14;
        // A 2 m door across a 1.41 m diagonal
        private static readonly float Narrow = Mathf.Sqrt(0.5f);

        public static HullPartSettings Clone { get; private set; }
        private static float _nextTry;

        /// <summary>Makes the twin once the game's door is loaded (from <see cref="ReinforcedHull.EnsureBuilt"/>).</summary>
        public static void EnsureBuilt()
        {
            if (Clone != null || Time.realtimeSinceStartup < _nextTry)
                return;
            _nextTry = Time.realtimeSinceStartup + 1f;
            try
            {
                HullPartSettings door = Resources.FindObjectsOfTypeAll<HullPartSettings>().FirstOrDefault(s => s != null && s.id == GameDoor && s.part != null);
                if (door == null)
                    return;
                GameObject part = SDModKit.Game.CustomParts.CloneIntoHolder(door.part, "TFA_" + Id);
                if (part == null)
                    return;
                HullPartSettings clone = Object.Instantiate(door);
                clone.name = "TFA_DoorPivoted";
                clone.hideFlags = HideFlags.DontUnloadUnusedAsset;
                clone.id = Id;
                clone.size = new Vector3(1f, 2f, 1f);
                clone.part = part;
                clone.pivotedSettings = null;
                clone.snappingStyle = Ships.Interface.Model.SnappingStyle.WallPivotable;
                Shape(part);
                // The door now snaps like a wall: the rotate key reaches the diagonals
                door.snappingStyle = Ships.Interface.Model.SnappingStyle.WallPivotable;
                door.pivotedSettings = clone;
                Clone = clone;
                AdvancedMod.Log.Msg($"pivoted door: the game's door ({door.name}) turns onto the diagonal walls as {Id}");
            }
            catch (Exception e)
            {
                ErrorLog.Report("pivoted door", e);
            }
        }

        // The door's model and colliders sit on (0.5, 0.5) of its 2 x 2: centered on the square, narrowed
        private static void Shape(GameObject part)
        {
            foreach (Transform t in part.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "Frame" && t.name != "Wrapper")
                    continue;
                t.localPosition = new Vector3(0f, 0.5f, t.localPosition.z);
                t.localScale = Vector3.Scale(t.localScale, new Vector3(Narrow, 1f, 1f));
            }
            var bounds = part.GetComponent<ShipPartBounds>();
            if (bounds != null)
            {
                bounds.center = new Vector3(0f, bounds.center.y, bounds.center.z);
                bounds.bounds = new Vector3(bounds.bounds.x * Narrow, bounds.bounds.y, bounds.bounds.z);
            }
            var interaction = part.GetComponentInChildren<Ships.Parts.Door.DoorInteraction>(true);
            if (interaction == null)
                return;
            var box = interaction.GetComponent<BoxCollider>();
            if (box != null)
            {
                box.center = new Vector3(box.center.x - 0.5f, box.center.y, box.center.z);
                box.size = new Vector3(box.size.x * Narrow, box.size.y, box.size.z);
            }
            var openPos = AccessTools.Field(typeof(Ships.Parts.Door.DoorInteraction), "openPos");
            var openSize = AccessTools.Field(typeof(Ships.Parts.Door.DoorInteraction), "openSize");
            if (openPos?.GetValue(interaction) is Vector3 pos)
                openPos.SetValue(interaction, new Vector3((pos.x - 0.5f) * Narrow, pos.y, pos.z));
            if (openSize?.GetValue(interaction) is Vector3 size)
                openSize.SetValue(interaction, new Vector3(size.x * Narrow, size.y, size.z));
            TurnIntoPivotedFrame(part);
        }

        // The game turns a pivoted piece by (forward along the diagonal, up out of the wall): its
        // right points down. The door, made upright, is turned into that frame, or it lay down.
        private static readonly Quaternion Turn = Quaternion.LookRotation(Vector3.up, Vector3.left);

        private static void TurnIntoPivotedFrame(GameObject part)
        {
            foreach (Transform child in part.transform)
            {
                // The game copies the collider object alone, at the part's place: turn what it holds
                if (child.GetComponent<Ships.Parts.Common.Model.Reactions.IShipPartDynamicCollider>() != null)
                {
                    foreach (Transform piece in child)
                        Rotate(piece);
                }
                else
                    Rotate(child);
            }
            var bounds = part.GetComponent<ShipPartBounds>();
            if (bounds != null)
            {
                bounds.center = Turn * bounds.center;
                Vector3 s = Turn * bounds.bounds;
                bounds.bounds = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
            }
        }

        private static void Rotate(Transform t)
        {
            t.localPosition = Turn * t.localPosition;
            t.localRotation = Turn * t.localRotation;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using SDModKit.Game;
using Ships.Interface.Model.Parts;
using Ships.Interface.Settings;
using StellarDriveDemoTFAdvanced.Common;
using StellarDriveDemoTFAdvanced.Parts;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StellarDriveDemoTFAdvanced.Construction
{
    /// <summary>
    /// Hull walls with windows: a plate with two round portholes and a framed window, 2 × 2 like
    /// the game's door, placed like a wall (the rotate key turns them onto the diagonals, where a
    /// porthole or a window in a 1.41 m wall takes their place). Each is the game's door kept
    /// shut: real hull, it seals its room and holds air; it never opens (no prompt, no use), and
    /// its model is the Blender one (tools/blender/construction.py). Only call when SDModKit is loaded.
    /// </summary>
    internal static class HullWindows
    {
        public const ushort Porthole = 8257, PortholePivoted = 8258, Window = 8259, WindowPivoted = 8260;
        private const ushort GameDoor = 14;

        private static readonly Dictionary<ushort, HullPartSettings> Built = new Dictionary<ushort, HullPartSettings>();
        private static bool _registered;
        private static float _nextTry;

        public static IEnumerable<HullPartSettings> Clones => Built.Values.Where(c => c != null);

        public static bool IsWindow(ushort id) => id >= Porthole && id <= WindowPivoted;

        public static void Register()
        {
            foreach (ushort id in new[] { Porthole, Window })
            {
                ushort straight = id;
                PartFactory.Ids.Add(straight);
                PartFactory.Ids.Add((ushort)(straight + 1));
                BuildTabs.Add(AdvancedTab.ConstructionName, AdvancedTab.GlassRow, () => Get(straight));
            }
            _registered = true;
        }

        public static HullPartSettings Get(ushort id)
        {
            EnsureBuilt();
            return Built.TryGetValue(id, out HullPartSettings clone) && clone != null ? clone : null;
        }

        /// <summary>Makes the four once the game's door is loaded (from <see cref="ReinforcedHull.EnsureBuilt"/>).</summary>
        public static void EnsureBuilt()
        {
            if (!_registered || Built.Count == 4 || Time.realtimeSinceStartup < _nextTry)
                return;
            _nextTry = Time.realtimeSinceStartup + 1f;
            try
            {
                HullPartSettings door = Resources.FindObjectsOfTypeAll<HullPartSettings>().FirstOrDefault(s => s != null && s.id == GameDoor && s.part != null);
                if (door == null)
                    return;
                foreach (ushort id in new[] { Porthole, Window })
                {
                    HullPartSettings pivoted = Make(door, (ushort)(id + 1), true);
                    HullPartSettings straight = Make(door, id, false);
                    if (pivoted == null || straight == null)
                        continue;
                    straight.pivotedSettings = pivoted;
                    Built[id] = straight;
                    Built[(ushort)(id + 1)] = pivoted;
                }
                AdvancedMod.Log.Msg($"hull windows: {Built.Count} made from the game's door");
            }
            catch (Exception e)
            {
                ErrorLog.Report("hull windows", e);
            }
        }

        private static HullPartSettings Make(HullPartSettings door, ushort id, bool pivoted)
        {
            GameObject part = SDModKit.Game.CustomParts.CloneIntoHolder(door.part, "TFA_" + id);
            if (part == null)
                return null;
            HullPartSettings clone = Object.Instantiate(door);
            clone.name = "TFA_" + id;
            clone.hideFlags = HideFlags.DontUnloadUnusedAsset;
            clone.id = id;
            clone.part = part;
            clone.pivotedSettings = null;
            clone.snappingStyle = Ships.Interface.Model.SnappingStyle.WallPivotable;
            if (pivoted)
                clone.size = new Vector3(1f, 2f, 1f);
            bool porthole = id == Porthole || id == PortholePivoted;
            string fr = porthole ? "Paroi à hublots" : "Paroi vitrée", en = porthole ? "Porthole wall" : "Window wall", es = porthole ? "Pared con ojos de buey" : "Pared con ventana";
            PartFactory.Describe(clone, Loc.Pick(fr, en, es),
                porthole
                    ? Loc.Pick("Une paroi de coque de 2 × 2 m percée de deux hublots ronds boulonnés. Se pose comme un mur (la touche de rotation la met en diagonale, avec un hublot) ; étanche, elle garde l'air de la pièce.",
                        "A 2 × 2 m hull wall with two bolted round portholes. Placed like a wall (the rotate key puts it on a diagonal, with one porthole); airtight, it keeps the room's air.",
                        "Una pared de casco de 2 × 2 m con dos ojos de buey redondos atornillados. Se coloca como una pared (la tecla de rotación la pone en diagonal, con un ojo de buey); estanca, guarda el aire de la sala.")
                    : Loc.Pick("Une paroi de coque de 2 × 2 m avec une large fenêtre encadrée d'acier. Se pose comme un mur (la touche de rotation la met en diagonale) ; étanche, elle garde l'air de la pièce.",
                        "A 2 × 2 m hull wall with a wide steel-framed window. Placed like a wall (the rotate key puts it on a diagonal); airtight, it keeps the room's air.",
                        "Una pared de casco de 2 × 2 m con una amplia ventana enmarcada en acero. Se coloca como una pared (la tecla de rotación la pone en diagonal); estanca, guarda el aire de la sala."),
                40f, porthole ? new[] { (101u, 4), (102u, 2) } : new[] { (101u, 3), (102u, 4) });
            Dress(part, id, pivoted);
            return clone;
        }

        // The door's model hidden, the window's in its place; the door stays shut for good
        private static void Dress(GameObject part, ushort id, bool pivoted)
        {
            part.AddComponent<Marker>();
            Transform visuals = part.transform.Find("Visuals");
            if (visuals == null)
                return;
            foreach (MeshRenderer renderer in visuals.GetComponentsInChildren<MeshRenderer>(true))
                renderer.enabled = false;
            var holder = new GameObject("TFA_Window") { layer = visuals.gameObject.layer };
            holder.transform.SetParent(visuals, false);
            // On the door's middle: (0.5, 0.5) of its 2 × 2; the pivoted one is centered on its square
            holder.transform.localPosition = pivoted ? new Vector3(0f, 0.5f, 0f) : new Vector3(0.5f, 0.5f, 0f);
            if (!MaterialParts.BlenderModel(id, holder.transform))
                AdvancedMod.Log.Warning($"hull windows: no model for {id}");
            if (pivoted)
                PivotedDoor.Shape(part);
        }

        /// <summary>Marks a window's part: its door never opens.</summary>
        internal sealed class Marker : MonoBehaviour
        {
        }
    }

    /// <summary>A hull window is a door that never opens: no prompt, and the use does nothing.</summary>
    [HarmonyPatch(typeof(Ships.Parts.Door.DoorInteraction), nameof(Ships.Parts.Door.DoorInteraction.Interact))]
    internal static class HullWindowShutPatch
    {
        private static bool Prefix(Ships.Parts.Door.DoorInteraction __instance, ref bool __result)
        {
            if (__instance == null || __instance.GetComponentInParent<HullWindows.Marker>() == null)
                return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Ships.Parts.Door.DoorInteraction), nameof(Ships.Parts.Door.DoorInteraction.Interactions), MethodType.Getter)]
    internal static class HullWindowPromptPatch
    {
        private static void Postfix(Ships.Parts.Door.DoorInteraction __instance, ref IReadOnlyCollection<Players.Interface.Model.PartInteractionConfig> __result)
        {
            if (__instance != null && __instance.GetComponentInParent<HullWindows.Marker>() != null)
                __result = new List<Players.Interface.Model.PartInteractionConfig>();
        }
    }
}

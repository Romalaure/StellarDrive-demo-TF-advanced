using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Items.Model;
using Research;
using SDModKit.Game;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Settings;
using Ships.Parts.Common;
using Ships.Parts.Common.Model.Reactions;
using Ships.Parts.SignalDisplay;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StellarDriveDemoTFAdvanced.Parts
{
    /// <summary>
    /// New ship parts are clones of an existing game part (the donor) under a new id, made by
    /// SDModKit, which also adds them to the build menu and lets the game save and load them like
    /// its own parts. Only call into this class when SDModKit is loaded.
    /// </summary>
    internal static class PartFactory
    {
        /// <summary>Every part id this mod adds (7301 and up; the TF mod uses 7101-7201).</summary>
        public static readonly HashSet<ushort> Ids = new HashSet<ushort>();

        private static readonly AccessTools.FieldRef<PartSettings, List<ItemInstance>> BuildingCost =
            AccessTools.FieldRefAccess<PartSettings, List<ItemInstance>>("buildingCost");

        public static void Register(ushort id, string donor, int row, Action<PartSettings, GameObject> configure) =>
            Register(id, donor, AdvancedTab.Name, row, configure);

        public static void Register(ushort id, string donor, string tab, int row, Action<PartSettings, GameObject> configure)
        {
            Ids.Add(id);
            CustomParts.Register(new CustomPartDefinition
            {
                Id = id,
                Name = "TFA_" + id,
                Donor = donor,
                BuildTab = tab,
                BuildRow = row,
                Configure = (settings, prefab) =>
                {
                    configure(settings, prefab);
                    EnsureVisual(prefab);
                }
            });
        }

        /// <summary>
        /// The build menu thumbnail, the placement preview and the build tool screen all copy the
        /// part's IShipPartVisual object: a part without one throws and breaks the build tool.
        /// </summary>
        private static void EnsureVisual(GameObject prefab)
        {
            if (prefab.GetComponentInChildren<IShipPartVisual>(true) != null)
                return;
            Transform visuals = prefab.transform.Find("Visuals") ?? prefab.transform.Find("Visual") ?? prefab.transform;
            visuals.gameObject.AddComponent<DefaultShipPartVisuals>();
        }

        public static void Describe(PartSettings settings, string label, string description, float mass, (uint Item, int Count)[] cost)
        {
            settings.fullLabel = label;
            settings.description = description;
            settings.localizedDescription = null;
            settings.mass = mass;
            SetCost(settings, cost);
        }

        /// <summary>
        /// Turns a cloned Signal Display into a blank part of the given size: its display model,
        /// text and plug removed. Returns the "Visuals" transform to build the new model under.
        /// </summary>
        public static Transform BlankFromSignalDisplay(GameObject prefab, Vector3 boundsCenter, Vector3 boundsSize)
        {
            Transform visuals = prefab.transform.Find("Visuals");
            GameSkin.Capture(prefab);
            Object.DestroyImmediate(visuals.GetComponent<SignalDisplayVisuals>());
            foreach (Transform child in visuals.Cast<Transform>().ToList())
                Object.DestroyImmediate(child.gameObject);

            SetBounds(prefab, boundsCenter, boundsSize);

            // A part's sockets are read from the SocketObjects in its prefab: removing them removes the plug
            foreach (SocketObject socket in prefab.GetComponentsInChildren<SocketObject>(true))
            {
                if (socket.visualObject != null)
                    Object.DestroyImmediate(socket.visualObject);
                if (socket != null && socket.interactive != null)
                    Object.DestroyImmediate(socket.interactive);
                if (socket != null)
                    Object.DestroyImmediate(socket.gameObject);
            }
            return visuals;
        }

        /// <summary>Placement box and the interaction collider (look at it, recycle it) of the whole part.</summary>
        public static void SetBounds(GameObject prefab, Vector3 center, Vector3 size)
        {
            var bounds = prefab.GetComponent<ShipPartBounds>();
            if (bounds != null)
            {
                bounds.center = center;
                bounds.bounds = size;
            }
            Transform interactions = prefab.transform.Find("Interactions");
            if (interactions == null)
                return;
            foreach (BoxCollider box in interactions.GetComponentsInChildren<BoxCollider>(true))
            {
                box.transform.localPosition = center;
                box.transform.localRotation = Quaternion.identity;
                box.center = Vector3.zero;
                box.size = size;
            }
        }

        private static void SetCost(PartSettings settings, (uint Item, int Count)[] cost)
        {
            ItemSettings[] items = Resources.FindObjectsOfTypeAll<ItemSettings>();
            var list = new List<ItemInstance>();
            foreach ((uint item, int count) in cost)
            {
                ItemSettings itemSettings = items.FirstOrDefault(i => i != null && i.id == item);
                if (itemSettings != null)
                    list.Add(new ItemInstance(itemSettings, count));
            }
            BuildingCost(settings) = list;
        }
    }

    /// <summary>The mod's parts are in no research node, so the build menu would keep them locked forever.</summary>
    [HarmonyPatch(typeof(ResearchClientProxy), nameof(ResearchClientProxy.IsPartUnlocked))]
    internal static class PartUnlockPatch
    {
        private static void Postfix(PartSettings part, ref bool __result)
        {
            if (!__result && part != null && PartFactory.Ids.Contains(part.id))
                __result = true;
        }
    }
}

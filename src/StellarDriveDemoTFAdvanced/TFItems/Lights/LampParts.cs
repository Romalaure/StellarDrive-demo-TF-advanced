using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Items.Model;
using SDModKit.Game;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Settings;
using Ships.Parts.SignalDisplay;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using UnityEngine;
using UnityEngine.Rendering;

namespace StellarDriveDemoTFAdvanced.TFItems.Lights
{
    /// <summary>
    /// Registers the lamps as new ship parts through SDModKit, which clones a donor part (here the
    /// Signal Display, a small synced part whose display and plug are removed) under a new id and adds it to
    /// the build menu. Only call this when SDModKit is loaded.
    /// </summary>
    internal static class LampParts
    {
        private const string Donor = "SignalDisplay";

        private static readonly Dictionary<LampGroup, Material> Materials = new Dictionary<LampGroup, Material>();

        public static void Register()
        {
            foreach (LampSpec lamp in LampCatalog.All)
            {
                LampSpec spec = lamp;
                RegisterDonorPart(spec.Id, TFTab.LampsRow, (settings, prefab) => Configure(spec, settings, prefab));
            }
            TFMod.Log.Msg($"registered {LampCatalog.All.Length} lamps");
        }

        /// <summary>A new part cloned from the Signal Display, in the TF build tab.</summary>
        internal static void RegisterDonorPart(ushort id, int row, System.Action<PartSettings, GameObject> configure, string tab = TFTab.Name)
        {
            CustomParts.Register(new CustomPartDefinition
            {
                Id = id,
                Name = "TF_" + id,
                Donor = Donor,
                BuildTab = tab,
                BuildRow = row,
                Configure = configure
            });
        }

        private static void Configure(LampSpec spec, PartSettings settings, GameObject prefab)
        {
            Transform visuals = PrepareDonor(settings, prefab, spec.Label, spec.Description, spec.Mass, spec.Cost,
                spec.BoundsCenter, spec.BoundsSize);
            BuildModel(spec, visuals);
            visuals.gameObject.AddComponent<LampVisuals>();
        }

        /// <summary>
        /// Turns a cloned Signal Display into a blank part: new label, cost and size, its display
        /// model removed. Returns the "Visuals" transform to build the new model under.
        /// </summary>
        internal static Transform PrepareDonor(PartSettings settings, GameObject prefab, string label, string description, float mass,
            (uint Item, int Count)[] cost, Vector3 boundsCenter, Vector3 boundsSize)
        {
            settings.fullLabel = label;
            settings.description = description;
            settings.localizedDescription = null;
            settings.mass = mass;
            SetCost(settings, cost);

            Transform visuals = prefab.transform.Find("Visuals");
            // The game's part material, for the devices' surfaces (before the donor's model goes)
            StellarDriveDemoTFAdvanced.Parts.GameSkin.Capture(prefab);
            Object.DestroyImmediate(visuals.GetComponent<SignalDisplayVisuals>());
            foreach (Transform child in visuals.Cast<Transform>().ToList())
                Object.DestroyImmediate(child.gameObject);

            var bounds = prefab.GetComponent<ShipPartBounds>();
            if (bounds != null)
            {
                bounds.center = boundsCenter;
                bounds.bounds = boundsSize;
            }

            // The interaction collider (look at it, pick it up) covers the whole part
            Transform interactions = prefab.transform.Find("Interactions");
            if (interactions != null)
            {
                foreach (BoxCollider box in interactions.GetComponentsInChildren<BoxCollider>(true))
                {
                    box.transform.localPosition = boundsCenter;
                    box.transform.localRotation = Quaternion.identity;
                    box.center = Vector3.zero;
                    box.size = boundsSize;
                }
            }

            // No plug: lamps are always on and mirrors need none. A part's sockets are read from
            // the SocketObjects in its prefab, so removing them removes the plug
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

        private static void BuildModel(LampSpec spec, Transform visuals)
        {
            EnsureMaterials();
            int layer = visuals.gameObject.layer;
            foreach (KeyValuePair<LampGroup, MeshBuilder> entry in spec.BuildShapes())
            {
                // Lamp bodies keep their look; painting a lamp colors its light instead
                string name = entry.Key == LampGroup.Diffuser ? LampVisuals.DiffuserName : "TF_NoPaint_" + entry.Key;
                var child = new GameObject(name) { layer = layer };
                child.transform.SetParent(visuals, false);
                child.AddComponent<MeshFilter>().sharedMesh = entry.Value.Build("TF_Lamp" + spec.Id + "_" + entry.Key);
                var renderer = child.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = Materials[entry.Key];
                renderer.shadowCastingMode = entry.Key == LampGroup.Diffuser ? ShadowCastingMode.Off : ShadowCastingMode.On;
            }

            var lightObject = new GameObject(LampVisuals.LightName) { layer = layer };
            lightObject.transform.SetParent(visuals, false);
            lightObject.transform.localPosition = spec.LightPosition;
            // Lights shine along their forward axis: out of the mounting surface (+y), or diagonally for corner lamps
            Vector3 direction = spec.LightDirection.normalized;
            lightObject.transform.localRotation = Quaternion.LookRotation(direction, Mathf.Abs(direction.z) > 0.99f ? Vector3.up : Vector3.forward);
            var light = lightObject.AddComponent<Light>();
            light.type = spec.LightType;
            light.range = spec.Range;
            light.intensity = spec.Intensity;
            light.color = spec.Color ?? LampCatalog.DefaultLight;
            light.shadows = LightShadows.None;
            if (spec.LightType == LightType.Spot)
            {
                light.spotAngle = spec.SpotAngle;
                light.innerSpotAngle = spec.SpotAngle * 0.6f;
            }

            if (spec.Blink > 0f || spec.Spin != 0f)
                new GameObject(LampVisuals.EffectsObjectName(spec.Blink, spec.Spin)) { layer = layer }.transform.SetParent(visuals, false);
        }

        private static void EnsureMaterials()
        {
            if (Materials.Count > 0 && Materials.Values.All(m => m != null))
                return;
            Materials[LampGroup.Frame] = LitMaterials.Get("TF_LampFrame", new Color(0.2f, 0.21f, 0.23f), 0.6f, 0.45f);
            Materials[LampGroup.Trim] = LitMaterials.Get("TF_LampTrim", new Color(0.82f, 0.83f, 0.86f), 1f, 0.8f);
            // "glass" in the name keeps the paint tool from tinting it
            Materials[LampGroup.Diffuser] = LitMaterials.Get("TF_LampGlass", new Color(0.95f, 0.93f, 0.88f), 0f, 0.9f, LampCatalog.DefaultLight * 3f);
        }

        private static readonly AccessTools.FieldRef<PartSettings, List<ItemInstance>> BuildingCost =
            AccessTools.FieldRefAccess<PartSettings, List<ItemInstance>>("buildingCost");

        internal static void SetCost(PartSettings settings, (uint Item, int Count)[] cost)
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
}

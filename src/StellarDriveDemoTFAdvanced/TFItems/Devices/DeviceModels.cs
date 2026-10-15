using System.Collections.Generic;
using Ships.Interface.Settings;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using StellarDriveDemoTFAdvanced.TFItems.Lights;
using UnityEngine;
using UnityEngine.Rendering;

namespace StellarDriveDemoTFAdvanced.TFItems.Devices
{
    /// <summary>A flat material for one piece of a device model.</summary>
    internal readonly struct DeviceMaterial
    {
        public readonly Color Color;
        public readonly float Metal;
        public readonly float Smooth;
        public readonly Color? Glow;

        public DeviceMaterial(Color color, float metal, float smooth, Color? glow = null)
        {
            Color = color;
            Metal = metal;
            Smooth = smooth;
            Glow = glow;
        }
    }

    /// <summary>
    /// Builds the radio, wardrobe and capsule: a cloned Signal Display (like the lamps) with its
    /// model replaced by meshes made here. Pieces named "TF_NoPaint_..." keep their color when
    /// painted; the others take the paint.
    /// </summary>
    internal static class DeviceModels
    {
        public static Transform Build(PartSettings settings, GameObject prefab, string label, string description, float mass,
            (uint Item, int Count)[] cost, Vector3 boundsCenter, Vector3 boundsSize,
            Dictionary<string, MeshBuilder> shapes, Dictionary<string, DeviceMaterial> materials)
        {
            Transform visuals = LampParts.PrepareDonor(settings, prefab, label, description, mass, cost, boundsCenter, boundsSize);
            int layer = visuals.gameObject.layer;
            foreach (KeyValuePair<string, MeshBuilder> entry in shapes)
            {
                DeviceMaterial material = materials[entry.Key];
                var child = new GameObject(entry.Key) { layer = layer };
                child.transform.SetParent(visuals, false);
                child.AddComponent<MeshFilter>().sharedMesh = entry.Value.Build(settings.id + "_" + entry.Key);
                var renderer = child.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = LitMaterials.Get("TF_" + settings.id + "_" + entry.Key, material.Color, material.Metal, material.Smooth, material.Glow);
                renderer.shadowCastingMode = material.Glow.HasValue ? ShadowCastingMode.Off : ShadowCastingMode.On;
            }
            return visuals;
        }

        /// <summary>Makes every collider of the part a thin slab at its base, so players can walk into it.</summary>
        public static void FlattenColliders(GameObject prefab, Vector3 size)
        {
            foreach (BoxCollider box in prefab.GetComponentsInChildren<BoxCollider>(true))
            {
                box.transform.localPosition = new Vector3(0f, size.y / 2f, 0f);
                box.transform.localRotation = Quaternion.identity;
                box.center = Vector3.zero;
                box.size = size;
            }
        }
    }
}

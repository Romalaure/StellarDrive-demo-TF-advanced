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
    /// painted; the others take the paint. Their surfaces use the game's part shader (plain
    /// color, no atlas), so they are lit like every other part: the sun and the mod's lamps only,
    /// dark in a dark ship. A URP Lit surface also took the sky's ambient light and stood out,
    /// bright, in dark rooms. Glowing pieces keep their own emissive material.
    /// </summary>
    internal static class DeviceModels
    {
        private static readonly Dictionary<string, Material> GameLook = new Dictionary<string, Material>();
        private static Texture2D _white;

        /// <summary>The game's part material in one flat color, or null before it is known.</summary>
        public static Material Flat(string name, Color color, float metal, float smooth)
        {
            if (GameLook.TryGetValue(name, out Material cached) && cached != null)
                return cached;
            Material source = StellarDriveDemoTFAdvanced.Parts.GameSkin.Material;
            if (source == null)
                return null;
            if (_white == null)
            {
                _white = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "TF_White" };
                _white.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
                _white.Apply();
            }
            var material = new Material(source) { name = name };
            Shader shader = material.shader;
            for (int i = 0; i < shader.GetPropertyCount(); i++)
            {
                if (shader.GetPropertyType(i) == ShaderPropertyType.Texture)
                {
                    if (StellarDriveDemoTFAdvanced.Common.DevHarness.Active && GameLook.Count == 0)
                        TFMod.Log.Msg($"dev: part shader texture {shader.GetPropertyName(i)} = {source.GetTexture(shader.GetPropertyNameId(i))?.name}");
                }
            }
            foreach (string texture in new[] { "_Albedo", "_ColorPalette", "_MainTex", "_BaseMap" })
                if (material.HasProperty(texture))
                    material.SetTexture(texture, _white);
            foreach (string property in new[] { "_Color", "_BaseColor" })
                if (material.HasProperty(property))
                    material.SetColor(property, color);
            if (material.HasProperty("_Metalness"))
                material.SetFloat("_Metalness", metal);
            if (material.HasProperty("_Roughness"))
                material.SetFloat("_Roughness", 1f - smooth);
            GameLook[name] = material;
            return material;
        }

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
                string name = "TF_" + settings.id + "_" + entry.Key;
                renderer.sharedMaterial = (material.Glow.HasValue ? null : Flat(name, material.Color, material.Metal, material.Smooth))
                    ?? LitMaterials.Get(name, material.Color, material.Metal, material.Smooth, material.Glow);
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

using System.Collections.Generic;
using HarmonyLib;
using Items.Model;
using Items.Services;
using Players.Visuals;
using Rendering.Services;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using Tools.Interface.Model;
using Tools.PaintTool;
using UI.Interface.Model;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace StellarDriveDemoTFAdvanced.TFItems.Paint
{
    /// <summary>
    /// Replaces the demo's placeholder paint tool (a box, a handle and a can) with a gravity-feed
    /// spray gun whose paint cup and nozzle tip show the selected color.
    /// Built on every tool instance: the local first-person tool and the copies other players see.
    /// </summary>
    internal static class PaintGunModel
    {
        private const string ModelName = "TF_PaintGun";
        private const string OldMeshName = "PaintTool";
        private const string OldPreviewName = "ColorPreview";

        internal enum Part { Body, Dark, Accent, Chrome, Paint, Glow }

        internal static readonly Color DefaultPaint = new Color(0.95f, 0.45f, 0.1f);

        private static Dictionary<Part, Mesh> _meshes;
        private static readonly Dictionary<Part, Material> Materials = new Dictionary<Part, Material>();

        /// <summary>Swaps the model under a tool's "Visuals" transform. Safe to call repeatedly.</summary>
        public static void Install(Transform visuals, bool firstPerson)
        {
            if (visuals == null || visuals.Find(ModelName) != null)
                return;
            Transform oldMesh = visuals.Find(OldMeshName);
            var oldRenderer = oldMesh != null ? oldMesh.GetComponent<MeshRenderer>() : null;
            if (oldRenderer == null)
                return;

            EnsureAssets();

            var root = new GameObject(ModelName);
            root.layer = oldMesh.gameObject.layer;
            root.transform.SetParent(visuals, false);
            root.transform.localPosition = oldMesh.localPosition;
            root.transform.localRotation = oldMesh.localRotation;
            root.transform.localScale = oldMesh.localScale;

            foreach (KeyValuePair<Part, Mesh> entry in _meshes)
            {
                var child = new GameObject(ModelName + "_" + entry.Key);
                child.layer = root.layer;
                child.transform.SetParent(root.transform, false);
                child.AddComponent<MeshFilter>().sharedMesh = entry.Value;
                var renderer = child.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = Materials[entry.Key];
                renderer.shadowCastingMode = firstPerson ? ShadowCastingMode.Off : oldRenderer.shadowCastingMode;
                renderer.receiveShadows = oldRenderer.receiveShadows;
            }

            oldRenderer.enabled = false;
            foreach (Transform child in visuals.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == OldPreviewName && child.TryGetComponent(out MeshRenderer preview))
                    preview.enabled = false;
            }

            root.AddComponent<PaintGunColor>();
        }

        private static void EnsureAssets()
        {
            if (_meshes == null)
                _meshes = BuildMeshes();

            if (Materials.TryGetValue(Part.Body, out Material existing) && existing != null)
                return;
            Materials.Clear();
            Materials[Part.Body] = LitMaterials.Get(ModelName + "_Body", new Color(0.72f, 0.75f, 0.79f), 0.55f, 0.55f);
            Materials[Part.Dark] = LitMaterials.Get(ModelName + "_Dark", new Color(0.11f, 0.115f, 0.125f), 0.1f, 0.3f);
            Materials[Part.Accent] = LitMaterials.Get(ModelName + "_Accent", new Color(0.96f, 0.47f, 0.08f), 0.2f, 0.5f);
            Materials[Part.Chrome] = LitMaterials.Get(ModelName + "_Chrome", new Color(0.86f, 0.87f, 0.9f), 1f, 0.85f);
            Materials[Part.Paint] = LitMaterials.Get(ModelName + "_Paint", DefaultPaint, 0f, 0.8f);
            Materials[Part.Glow] = LitMaterials.Get(ModelName + "_Glow", DefaultPaint, 0f, 0.9f, DefaultPaint * 1.5f);
        }

        private static readonly Color[] SprayColors =
        {
            new Color(0.95f, 0.2f, 0.25f), new Color(1f, 0.8f, 0.15f), new Color(0.25f, 0.85f, 0.4f),
            new Color(0.2f, 0.7f, 1f), new Color(0.75f, 0.35f, 1f)
        };

        /// <summary>
        /// The model shown in the inventory and toolbar: the spray gun with an orange cup, spraying
        /// a burst of colored drops. Made for one thumbnail render, which destroys it.
        /// </summary>
        public static GameObject CreateThumbnailModel()
        {
            EnsureAssets();
            var root = new GameObject(ModelName + "_Thumbnail");
            foreach (KeyValuePair<Part, Mesh> entry in _meshes)
                AddRenderer(root, entry.Key.ToString(), entry.Value, new Material(Materials[entry.Key]));
            // Drops leaving the nozzle, growing as they spread
            Vector3[] drops =
            {
                new Vector3(0f, 0.122f, 0.168f), new Vector3(0.012f, 0.106f, 0.198f), new Vector3(-0.012f, 0.134f, 0.228f),
                new Vector3(0.008f, 0.1f, 0.258f), new Vector3(-0.004f, 0.142f, 0.282f)
            };
            for (int i = 0; i < drops.Length; i++)
            {
                float radius = 0.007f + i * 0.0025f;
                var drop = new MeshBuilder();
                drop.Cylinder(drops[i] - new Vector3(0f, 0f, radius), drops[i] + new Vector3(0f, 0f, radius), radius * 0.7f, radius * 0.7f, 12);
                drop.Cylinder(drops[i] - new Vector3(0f, radius * 0.6f, 0f), drops[i] + new Vector3(0f, radius * 0.6f, 0f), radius, radius, 12);
                Color color = SprayColors[i % SprayColors.Length];
                AddRenderer(root, "Drop" + i, drop.Build(ModelName + "_Drop" + i), LitMaterials.Get(ModelName + "_Drop" + i, color, 0f, 0.9f, color * 0.8f));
            }
            return root;
        }

        private static void AddRenderer(GameObject root, string name, Mesh mesh, Material material)
        {
            var child = new GameObject(name);
            child.transform.SetParent(root.transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            child.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Dictionary<Part, Mesh> BuildMeshes()
        {
            var meshes = new Dictionary<Part, Mesh>();
            foreach (KeyValuePair<Part, MeshBuilder> entry in BuildShapes())
                meshes[entry.Key] = entry.Value.Build(ModelName + "_" + entry.Key);
            return meshes;
        }

        // Unity units are meters; +z is the spray direction, +y is up, the handle sits around z = 0
        internal static Dictionary<Part, MeshBuilder> BuildShapes()
        {
            var b = new Dictionary<Part, MeshBuilder>();
            foreach (Part part in System.Enum.GetValues(typeof(Part)))
                b[part] = new MeshBuilder();

            const float bodyY = 0.118f;
            const float cupZ = 0.035f;

            // Body: round barrel with a flat underside that meets the handle
            b[Part.Body].Cylinder(new Vector3(0f, bodyY, -0.07f), new Vector3(0f, bodyY, 0.085f), 0.03f, 0.03f, 20);
            b[Part.Body].Box(new Vector3(0f, 0.096f, 0.005f), new Vector3(0.044f, 0.03f, 0.13f));
            // Accent stripe along the barrel
            b[Part.Accent].Cylinder(new Vector3(0f, bodyY, 0.045f), new Vector3(0f, bodyY, 0.06f), 0.0315f, 0.0315f, 20);

            // Front: chrome collar, orange air cap with its two horns, fluid tip and glowing nozzle
            b[Part.Chrome].Cylinder(new Vector3(0f, bodyY, 0.085f), new Vector3(0f, bodyY, 0.1f), 0.031f, 0.027f, 20);
            b[Part.Accent].Cylinder(new Vector3(0f, bodyY, 0.1f), new Vector3(0f, bodyY, 0.124f), 0.027f, 0.019f, 20);
            b[Part.Accent].Box(new Vector3(0.022f, bodyY, 0.128f), new Vector3(0.009f, 0.02f, 0.024f));
            b[Part.Accent].Box(new Vector3(-0.022f, bodyY, 0.128f), new Vector3(0.009f, 0.02f, 0.024f));
            b[Part.Chrome].Cylinder(new Vector3(0f, bodyY, 0.124f), new Vector3(0f, bodyY, 0.138f), 0.009f, 0.006f, 12);
            b[Part.Glow].Cylinder(new Vector3(0f, bodyY, 0.138f), new Vector3(0f, bodyY, 0.143f), 0.0045f, 0.0035f, 12);

            // Rear: fan width knob (top) and fluid flow knob (bottom)
            b[Part.Dark].Cylinder(new Vector3(0f, 0.13f, -0.07f), new Vector3(0f, 0.13f, -0.09f), 0.011f, 0.011f, 10);
            b[Part.Chrome].Cylinder(new Vector3(0f, 0.13f, -0.09f), new Vector3(0f, 0.13f, -0.094f), 0.008f, 0.008f, 10);
            b[Part.Dark].Cylinder(new Vector3(0f, 0.1f, -0.07f), new Vector3(0f, 0.1f, -0.086f), 0.008f, 0.008f, 10);

            // Side pressure gauge, its face shows the paint color
            b[Part.Chrome].Cylinder(new Vector3(-0.026f, bodyY, 0.02f), new Vector3(-0.038f, bodyY, 0.02f), 0.014f, 0.014f, 16);
            b[Part.Glow].Cylinder(new Vector3(-0.038f, bodyY, 0.02f), new Vector3(-0.0395f, bodyY, 0.02f), 0.011f, 0.011f, 16);

            // Gravity cup on top: chrome neck, collar, paint-colored cup, dark lid and vent cap
            b[Part.Chrome].Cylinder(new Vector3(0f, 0.142f, cupZ), new Vector3(0f, 0.158f, cupZ), 0.009f, 0.009f, 12);
            b[Part.Body].Cylinder(new Vector3(0f, 0.158f, cupZ), new Vector3(0f, 0.166f, cupZ), 0.02f, 0.029f, 20);
            b[Part.Paint].Cylinder(new Vector3(0f, 0.166f, cupZ), new Vector3(0f, 0.212f, cupZ), 0.029f, 0.033f, 20);
            b[Part.Dark].Cylinder(new Vector3(0f, 0.212f, cupZ), new Vector3(0f, 0.22f, cupZ), 0.035f, 0.035f, 20);
            b[Part.Accent].Cylinder(new Vector3(0f, 0.22f, cupZ), new Vector3(0f, 0.228f, cupZ), 0.01f, 0.008f, 12);

            // Handle, tilted back, with a wider butt
            b[Part.Dark].Block(new[]
            {
                new Vector3(-0.021f, -0.085f, -0.072f), new Vector3(0.021f, -0.085f, -0.072f),
                new Vector3(-0.019f, 0.085f, -0.035f), new Vector3(0.019f, 0.085f, -0.035f),
                new Vector3(-0.021f, -0.085f, -0.022f), new Vector3(0.021f, -0.085f, -0.022f),
                new Vector3(-0.019f, 0.085f, 0.022f), new Vector3(0.019f, 0.085f, 0.022f)
            });
            b[Part.Body].Block(new[]
            {
                new Vector3(-0.024f, -0.097f, -0.078f), new Vector3(0.024f, -0.097f, -0.078f),
                new Vector3(-0.024f, -0.085f, -0.076f), new Vector3(0.024f, -0.085f, -0.076f),
                new Vector3(-0.024f, -0.097f, -0.018f), new Vector3(0.024f, -0.097f, -0.018f),
                new Vector3(-0.024f, -0.085f, -0.018f), new Vector3(0.024f, -0.085f, -0.018f)
            });

            // Air inlet under the handle with an orange hose fitting
            b[Part.Chrome].Cylinder(new Vector3(0f, -0.097f, -0.048f), new Vector3(0f, -0.122f, -0.048f), 0.0075f, 0.0075f, 12);
            b[Part.Accent].Cylinder(new Vector3(0f, -0.104f, -0.048f), new Vector3(0f, -0.112f, -0.048f), 0.0105f, 0.0105f, 12);

            // Trigger: two chrome segments curving down in front of the handle
            b[Part.Chrome].Block(new[]
            {
                new Vector3(-0.011f, 0.045f, 0.03f), new Vector3(0.011f, 0.045f, 0.03f),
                new Vector3(-0.011f, 0.083f, 0.028f), new Vector3(0.011f, 0.083f, 0.028f),
                new Vector3(-0.011f, 0.045f, 0.04f), new Vector3(0.011f, 0.045f, 0.04f),
                new Vector3(-0.011f, 0.083f, 0.038f), new Vector3(0.011f, 0.083f, 0.038f)
            });
            b[Part.Chrome].Block(new[]
            {
                new Vector3(-0.011f, 0.002f, 0.018f), new Vector3(0.011f, 0.002f, 0.018f),
                new Vector3(-0.011f, 0.045f, 0.03f), new Vector3(0.011f, 0.045f, 0.03f),
                new Vector3(-0.011f, 0.002f, 0.027f), new Vector3(0.011f, 0.002f, 0.027f),
                new Vector3(-0.011f, 0.045f, 0.04f), new Vector3(0.011f, 0.045f, 0.04f)
            });

            return b;
        }
    }

    /// <summary>Keeps the cup and nozzle of the local player's spray gun in the selected color.</summary>
    internal sealed class PaintGunColor : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private Renderer[] _paintRenderers;
        private Renderer[] _glowRenderers;
        private PaintTool _tool;
        private Color _shown = new Color(-1f, 0f, 0f);

        private void Awake()
        {
            _paintRenderers = FindRenderers(PaintGunModel.Part.Paint);
            _glowRenderers = FindRenderers(PaintGunModel.Part.Glow);
            _tool = GetComponentInParent<PaintTool>();
        }

        private void Update()
        {
            // Only the local tool has an active PaintTool; copies seen on other players keep the default
            Color color = PaintGunModel.DefaultPaint;
            if (_tool != null && _tool.enabled)
            {
                var selection = GameServices.PaintSelection;
                if (selection != null)
                    color = selection.SelectedPaintColor;
            }
            if (color == _shown)
                return;
            _shown = color;

            foreach (Renderer renderer in _paintRenderers)
            {
                renderer.GetPropertyBlock(_block);
                _block.SetColor(BaseColor, color);
                renderer.SetPropertyBlock(_block);
            }
            foreach (Renderer renderer in _glowRenderers)
            {
                renderer.GetPropertyBlock(_block);
                _block.SetColor(BaseColor, color);
                _block.SetColor(EmissionColor, color * 1.5f);
                renderer.SetPropertyBlock(_block);
            }
        }

        private Renderer[] FindRenderers(PaintGunModel.Part part)
        {
            Transform child = transform.Find(gameObject.name + "_" + part);
            return child != null ? child.GetComponents<Renderer>() : new Renderer[0];
        }
    }

    [HarmonyPatch(typeof(PaintTool), nameof(PaintTool.EnableTool))]
    internal static class PaintToolEnablePatch
    {
        private static void Postfix(PaintTool __instance)
        {
            ToolVisuals visuals = __instance.GetComponentInChildren<ToolVisuals>(true);
            if (visuals != null)
                PaintGunModel.Install(visuals.transform, firstPerson: true);
        }
    }

    [HarmonyPatch(typeof(OtherPlayerToolVisual), "OnItemSelectionChange")]
    internal static class OtherPlayerToolVisualPatch
    {
        private static readonly AccessTools.FieldRef<OtherPlayerToolVisual, ToolVisuals> CurrentTool =
            AccessTools.FieldRefAccess<OtherPlayerToolVisual, ToolVisuals>("_currentTool");

        private static void Postfix(OtherPlayerToolVisual __instance)
        {
            ToolVisuals visuals = CurrentTool(__instance);
            if (visuals != null)
                PaintGunModel.Install(visuals.transform, firstPerson: false);
        }
    }

    /// <summary>
    /// The game draws a tool's inventory icon from its prefab, the demo's placeholder box and can;
    /// the paint gun gets its own icon instead.
    /// </summary>
    [HarmonyPatch(typeof(ItemThumbnailTextureCache), nameof(ItemThumbnailTextureCache.GetThumbnail))]
    internal static class PaintGunThumbnailPatch
    {
        private static readonly AccessTools.FieldRef<ItemThumbnailTextureCache, Dictionary<uint, Texture2D>> Thumbnails =
            AccessTools.FieldRefAccess<ItemThumbnailTextureCache, Dictionary<uint, Texture2D>>("_thumbnails");
        private static readonly AccessTools.FieldRef<ItemThumbnailTextureCache, ObjectThumbnailRenderer> Renderer =
            AccessTools.FieldRefAccess<ItemThumbnailTextureCache, ObjectThumbnailRenderer>("_thumbnailRenderer");
        private static readonly AccessTools.FieldRef<ItemThumbnailTextureCache, IThumbnailsResolutionProvider> Resolution =
            AccessTools.FieldRefAccess<ItemThumbnailTextureCache, IThumbnailsResolutionProvider>("_thumbnailsResolutionProvider");
        private static readonly AccessTools.FieldRef<ItemThumbnailTextureCache, ItemSettingsList> Items =
            AccessTools.FieldRefAccess<ItemThumbnailTextureCache, ItemSettingsList>("items");

        // Nozzle to the left and a little toward the viewer, top tipped forward to show the cup
        private static readonly Quaternion Pose = Quaternion.LookRotation(new Vector3(-1f, 0.12f, -0.35f), new Vector3(0f, 1f, -0.25f));

        private static bool Prefix(ItemThumbnailTextureCache __instance, uint itemId, ref Texture2D __result)
        {
            Dictionary<uint, Texture2D> thumbnails = Thumbnails(__instance);
            if (thumbnails == null || thumbnails.ContainsKey(itemId))
                return true;
            ItemSettings item = Items(__instance)?.GetItemSettingsById(itemId);
            if (item == null || item.toolSettings == null || item.toolSettings.prefab == null
                || item.toolSettings.prefab.GetComponentInChildren<PaintTool>(true) == null)
                return true;
            ObjectThumbnailRenderer renderer = Renderer(__instance);
            IThumbnailsResolutionProvider resolution = Resolution(__instance);
            if (renderer == null || resolution == null)
                return true;
            int size = resolution.GetItemSmallThumbnailResolution();
            var texture = new Texture2D(size, size, GraphicsFormat.R8G8B8A8_UNorm, TextureCreationFlags.None) { wrapMode = TextureWrapMode.Clamp };
            try
            {
                renderer.Render(PaintGunModel.CreateThumbnailModel(), Pose, texture);
            }
            catch (System.Exception e)
            {
                TFMod.Log.Warning("paint gun icon not drawn, the game's is used: " + e.Message);
                Object.Destroy(texture);
                return true;
            }
            thumbnails[itemId] = texture;
            __result = texture;
            return false;
        }
    }
}

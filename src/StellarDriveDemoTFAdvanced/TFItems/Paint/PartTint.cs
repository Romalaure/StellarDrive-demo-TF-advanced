using System.Linq;
using Ships.Parts.Common.Model.Reactions;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.TFItems.Paint
{
    /// <summary>
    /// Applies paint to a part's visual through property blocks, so shared materials stay untouched.
    /// Ship parts use the game's Custom/PlanetObject shader (_Color tint, _Roughness, _Metalness,
    /// _EmissionColor); URP Lit names are set too for parts added by other mods.
    /// </summary>
    internal static class PartTint
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int RoughnessId = Shader.PropertyToID("_Roughness");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int MetalnessId = Shader.PropertyToID("_Metalness");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        // Glow 100% gives this much HDR emission
        private const float MaxGlow = 4f;

        // Materials whose look depends on their own color: glass, screens, holograms, fluids
        private static readonly string[] SkippedMaterialWords = { "glass", "screen", "display", "holo", "window", "fluid", "liquid", "laser", "text", "font", "sdf" };
        private static readonly string[] SkippedShaderWords = { "hologram", "screen", "textmesh", "particles", "laser", "transparent", "window", "display", "warp", "skybox" };

        private static readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();

        /// <summary>Re-applies the stored paint (or removes it) of a part, if its visual is loaded.</summary>
        public static void Refresh(PartKey key)
        {
            var ships = GameServices.ShipsClient;
            if (ships == null || !ships.TryGetTrackedShip(key.ShipId, out var ship))
                return;
            if (!ship.TryGetFloatingPartVisual(key.RealPartId, out IShipPartVisual visual) || visual == null)
                return;
            Apply(visual.GameObject, key);
        }

        /// <summary>Interior renderers: they take the part's interior color.</summary>
        public static bool IsInterior(Renderer renderer) => renderer.gameObject.name.Contains("TF_PaintInner");

        private static readonly System.Text.RegularExpressions.Regex VariantName = new System.Text.RegularExpressions.Regex(@"TF_MatVariant_(\d+)_");

        /// <summary>The material variant a renderer belongs to, -1 for none.</summary>
        public static int Variant(Renderer renderer)
        {
            var match = VariantName.Match(renderer.gameObject.name);
            return match.Success ? int.Parse(match.Groups[1].Value) : -1;
        }

        private static bool HasVariant(GameObject root, int variant, bool interior) =>
            root.GetComponentsInChildren<Renderer>(true).Any(r => Variant(r) == variant && IsInterior(r) == interior);

        /// <summary>Whether the part has interior surfaces with a color of their own.</summary>
        public static bool HasInterior(GameObject root) => root != null && root.GetComponentsInChildren<Renderer>(true).Any(IsInterior);

        public static void Apply(GameObject root, PartKey key)
        {
            if (root == null)
                return;
            bool outside = PaintNet.Client.TryGet(key.Exterior, out PaintData outer);
            bool inside = PaintNet.Client.TryGet(key.Interior, out PaintData inner);
            // Parts built in several materials: only the chosen variant of each side is drawn
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                int variant = Variant(renderer);
                if (variant < 0)
                    continue;
                bool interior = IsInterior(renderer);
                int chosen = interior ? (inside ? inner.Material : 0) : (outside ? outer.Material : 0);
                renderer.enabled = variant == chosen || (variant == 0 && !HasVariant(root, chosen, interior));
            }
            foreach (Renderer renderer in Paintable(root))
            {
                bool interior = IsInterior(renderer);
                if (interior ? inside : outside)
                    Apply(renderer, interior ? inner : outer);
                else
                    Clear(renderer);
            }
        }

        private static void Apply(Renderer renderer, PaintData paint)
        {
            Color color = paint.Color;
            PaintFinish finish = paint.Finish;
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material material = materials[i];
                    if (!IsPaintable(material))
                        continue;

                    renderer.GetPropertyBlock(Block, i);
                    Block.SetColor(ColorId, color);
                    Block.SetColor(BaseColorId, color);
                    if (finish.Enabled)
                    {
                        Block.SetFloat(RoughnessId, 1f - finish.GlossValue);
                        Block.SetFloat(SmoothnessId, finish.GlossValue);
                        Block.SetFloat(MetalnessId, finish.MetalValue);
                        Block.SetFloat(MetallicId, finish.MetalValue);
                        Block.SetColor(EmissionColorId, color * (finish.GlowValue * MaxGlow));
                    }
                    else
                    {
                        // Back to the material's own values
                        Block.Clear();
                        Block.SetColor(ColorId, color);
                        Block.SetColor(BaseColorId, color);
                    }
                    renderer.SetPropertyBlock(Block, i);
                }
            }
        }

        private static void Clear(Renderer renderer)
        {
            Block.Clear();
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                if (IsPaintable(materials[i]))
                    renderer.SetPropertyBlock(Block, i);
            }
        }

        private static System.Collections.Generic.IEnumerable<Renderer> Paintable(GameObject root)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                // Mod-built parts can opt out (lamps take the paint as light color instead)
                if ((renderer is MeshRenderer || renderer is SkinnedMeshRenderer) && !renderer.gameObject.name.StartsWith("TF_NoPaint"))
                    yield return renderer;
            }
        }

        private static bool IsPaintable(Material material)
        {
            if (material == null || (!material.HasProperty(ColorId) && !material.HasProperty(BaseColorId)))
                return false;
            // Transparent and overlay materials
            if (material.renderQueue >= 2450)
                return false;

            string name = material.name.ToLowerInvariant();
            foreach (string word in SkippedMaterialWords)
            {
                if (name.Contains(word))
                    return false;
            }
            string shader = material.shader != null ? material.shader.name.ToLowerInvariant() : "";
            foreach (string word in SkippedShaderWords)
            {
                if (shader.Contains(word))
                    return false;
            }
            return true;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace StellarDriveDemoTFAdvanced.Common
{
    /// <summary>
    /// Glowing see-through materials (force field bubble, portal heart): the game's URP
    /// Particles/Unlit shader in additive blending, both faces drawn, with a texture whose alpha
    /// gives the shape. Copied from a material the game ships (Shader.Find only sees the shaders
    /// the build includes).
    /// </summary>
    internal static class TransparentMaterials
    {
        private const string ParticlesUnlit = "Universal Render Pipeline/Particles/Unlit";
        private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

        public static Material Additive(string name, Texture texture)
        {
            if (Cache.TryGetValue(name, out Material cached) && cached != null)
                return cached;
            Material template = null;
            foreach (Material m in Resources.FindObjectsOfTypeAll<Material>())
            {
                // Prefer the game's own materials over other mods'
                if (m != null && m.shader != null && m.shader.name == ParticlesUnlit && (template == null || template.name.Contains(".")))
                    template = m;
            }
            if (template == null)
                return null;
            var material = new Material(template) { name = name };
            // Whatever the source material did (color modes, emission, flipbooks): plain texture times color
            material.shaderKeywords = new[] { "_SURFACE_TYPE_TRANSPARENT" };
            if (material.HasProperty("_ColorMode"))
                material.SetFloat("_ColorMode", 0f);
            material.SetTexture("_BaseMap", texture != null ? texture : Texture2D.whiteTexture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = 3000;
            Cache[name] = material;
            return material;
        }

        /// <summary>A see-through tinted surface (alpha blended, not added): dark glass behind texts.</summary>
        public static Material Blended(string name, Color color)
        {
            if (Cache.TryGetValue(name, out Material cached) && cached != null)
                return cached;
            Material additive = Additive(name + "_Base", null);
            if (additive == null)
                return null;
            var material = new Material(additive) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            // Before the texts drawn over it
            material.renderQueue = 2990;
            Cache[name] = material;
            return material;
        }
    }
}

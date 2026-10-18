using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Common
{
    /// <summary>
    /// Flat-colored URP Lit materials for models built by the mod. The game's own part shader
    /// needs texture atlases, so mod models use URP Lit, cloned from a material the game ships
    /// (Shader.Find only sees shaders included in the build).
    /// </summary>
    internal static class LitMaterials
    {
        private const string LitShader = "Universal Render Pipeline/Lit";

        private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();
        private static Material _template;

        public static Material Get(string name, Color color, float metallic, float smoothness, Color? emission = null)
        {
            if (Cache.TryGetValue(name, out Material cached) && cached != null)
                return cached;

            Material template = Template();
            Material material = template != null ? new Material(template) : new Material(Shader.Find(LitShader));
            material.name = name;
            material.shaderKeywords = new string[0];
            foreach (string texture in new[] { "_BaseMap", "_MainTex", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap", "_EmissionMap", "_DetailAlbedoMap", "_DetailNormalMap" })
            {
                if (material.HasProperty(texture))
                    material.SetTexture(texture, null);
            }
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            // Opaque surface
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.renderQueue = 2000;
            if (!emission.HasValue)
                StellarDriveDemoTFAdvanced.Common.SurfaceTextures.Apply(material, metallic);
            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                material.SetColor("_EmissionColor", emission.Value);
            }
            Cache[name] = material;
            return material;
        }

        private static Material Template()
        {
            if (_template != null)
                return _template;
            _template = Resources.FindObjectsOfTypeAll<Material>()
                .Where(m => m != null && m.shader != null && m.shader.name == LitShader && m.renderQueue < 2450)
                .OrderBy(m => m.name)
                .FirstOrDefault();
            if (_template == null)
                AdvancedMod.Log.Warning("no URP Lit material found to copy, falling back to Shader.Find");
            return _template;
        }
    }
}

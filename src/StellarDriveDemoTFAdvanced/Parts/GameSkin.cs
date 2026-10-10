using System.Collections.Generic;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;
using UnityEngine.Rendering;

namespace StellarDriveDemoTFAdvanced.Parts
{
    /// <summary>
    /// The look of the game's own parts for the mod's models: the game's part shader
    /// (Custom/PlanetObject, lit by the planet's sun and ambient light like every part) with its
    /// hand-drawn grey "LogicGates" atlas, and the ship palette color the donor part reads from UV1.
    /// Taken from the donor part before its model is removed. Mod models map each face to one of
    /// the atlas panels below, outline included, like the game's own parts.
    /// </summary>
    internal static class GameSkin
    {
        /// <summary>Plain grey panel with a drawn edge (LogicGates atlas, about 385 x 145 px).</summary>
        public static readonly Rect PlainPanel = new Rect(0.155f, 0.604f, 0.183f, 0.064f);
        /// <summary>Tall grey panel, for beams and long faces (about 165 x 430 px).</summary>
        public static readonly Rect TallPanel = new Rect(0.508f, 0.05f, 0.08f, 0.21f);

        private static Material _material;
        private static Vector2 _palette;

        public static bool Ready => _material != null;

        /// <summary>Remembers the donor's part material and palette color (first call only).</summary>
        public static void Capture(GameObject donor)
        {
            if (_material != null || donor == null)
                return;
            foreach (MeshRenderer renderer in donor.GetComponentsInChildren<MeshRenderer>(true))
            {
                Material material = renderer.sharedMaterial;
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (material == null || material.shader == null || material.shader.name != "Custom/PlanetObject" || filter == null || filter.sharedMesh == null)
                    continue;
                // Sockets use the "Plugs" atlas; the body uses the part's own one
                if (material.name.StartsWith("Plugs"))
                    continue;
                _material = material;
                _palette = MostUsedUv1(filter.sharedMesh);
                AdvancedMod.Log.Msg($"part look: {material.name} ({material.shader.name}), palette uv {_palette}");
                return;
            }
            AdvancedMod.Log.Warning("part look: no game part material found on the donor, mod parts keep plain colors");
        }

        /// <summary>A renderer showing <paramref name="builder"/> with the game's part look, or <paramref name="fallback"/> if it is not available.</summary>
        public static MeshRenderer AddMesh(Transform parent, int layer, string name, MeshBuilder builder, Material fallback, ShadowCastingMode shadows = ShadowCastingMode.On)
        {
            var child = new GameObject(name) { layer = layer };
            child.transform.SetParent(parent, false);
            child.AddComponent<MeshFilter>().sharedMesh = builder.Build(name, Ready ? _palette : (Vector2?)null);
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material != null ? _material : fallback;
            renderer.shadowCastingMode = shadows;
            return renderer;
        }

        private static Vector2 MostUsedUv1(Mesh mesh)
        {
            var uvs = new List<Vector2>();
            if (mesh.isReadable)
                mesh.GetUVs(1, uvs);
            if (uvs.Count == 0)
                return Vector2.zero;
            var counts = new Dictionary<Vector2, int>();
            Vector2 best = uvs[0];
            int bestCount = 0;
            foreach (Vector2 uv in uvs)
            {
                var key = new Vector2(Mathf.Round(uv.x * 1000f) / 1000f, Mathf.Round(uv.y * 1000f) / 1000f);
                counts.TryGetValue(key, out int n);
                counts[key] = ++n;
                if (n > bestCount)
                {
                    bestCount = n;
                    best = uv;
                }
            }
            return best;
        }
    }
}

using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.TFItems.Devices
{
    /// <summary>
    /// The speaker's surfaces, each with its relief, one tile per 25 cm (the model's UVs are
    /// scaled up for them): satin paint over the cabinet's wood, woven acoustic cloth on the
    /// baffle and plinth, brushed metal, a twill-woven cone. Shades of white: the material's color
    /// tints them. The setting ClassicTextures keeps the plain colors.
    /// </summary>
    internal static class SpeakerTextures
    {
        private const float UvScale = 4f;
        private const int Size = NaturalTextures.Size;
        private static Texture2D _case, _caseNormal, _cloth, _clothNormal, _brushed, _brushedNormal, _twill, _twillNormal;

        /// <summary>Gives the speaker's pieces (named by their material key) their textures.</summary>
        public static void Dress(Transform visuals)
        {
            if (!SurfaceTextures.Enabled)
                return;
            foreach (MeshRenderer renderer in visuals.GetComponentsInChildren<MeshRenderer>(true))
            {
                Texture2D albedo, normal;
                switch (renderer.name)
                {
                    case "TF_Speaker_Case": albedo = Case(out normal); break;
                    case "TF_NoPaint_SpeakerTrim": albedo = Cloth(out normal); break;
                    case "TF_NoPaint_SpeakerMetal": albedo = Brushed(out normal); break;
                    case "TF_NoPaint_SpeakerCone": albedo = Twill(out normal); break;
                    default: continue;
                }
                Material material = renderer.sharedMaterial;
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (material == null || filter == null || filter.sharedMesh == null || !material.HasProperty("_Albedo"))
                    continue;
                Mesh mesh = Object.Instantiate(filter.sharedMesh);
                mesh.name = filter.sharedMesh.name;
                Vector2[] uvs = mesh.uv;
                for (int i = 0; i < uvs.Length; i++)
                    uvs[i] *= UvScale;
                mesh.uv = uvs;
                filter.sharedMesh = mesh;
                material.SetTexture("_Albedo", albedo);
                if (material.HasProperty("_Normal"))
                    material.SetTexture("_Normal", normal);
            }
        }

        // Satin paint: orange peel, the wood's grain faintly under it, a few scuffs
        private static Texture2D Case(out Texture2D normal)
        {
            if (_case == null)
            {
                var pixels = new Color[Size * Size];
                var heights = new float[Size * Size];
                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        float u = (x + 0.5f) / Size, v = (y + 0.5f) / Size;
                        float peel = NaturalTextures.Noise(u * 128f, v * 128f, 128, 201);
                        float grain = Mathf.Pow(0.5f + 0.5f * Mathf.Sin((v * 24f + NaturalTextures.Fbm(u * 2f, v * 6f, 2, 6, 202) * 3f) * Mathf.PI * 2f), 4f);
                        float scuff = Mathf.Clamp01((NaturalTextures.Fbm(u * 8f, v * 8f, 8, 203) - 0.68f) * 4f);
                        float shade = 0.93f + (peel - 0.5f) * 0.05f - grain * 0.035f - scuff * 0.07f;
                        pixels[y * Size + x] = new Color(shade, shade, shade, 1f);
                        heights[y * Size + x] = peel * 0.25f + grain * 0.15f;
                    }
                }
                _case = NaturalTextures.Finish("TFA_SpeakerCase", pixels);
                _caseNormal = NaturalTextures.NormalMap("TFA_SpeakerCaseNormal", heights, 1.5f);
            }
            normal = _caseNormal;
            return _case;
        }

        // Plain-woven acoustic cloth, a 2 mm thread, uneven slubs
        private static Texture2D Cloth(out Texture2D normal)
        {
            if (_cloth == null)
            {
                var pixels = new Color[Size * Size];
                var heights = new float[Size * Size];
                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        float u = (x + 0.5f) / Size, v = (y + 0.5f) / Size;
                        float h = Weave(u, v, 128, 1);
                        float slub = NaturalTextures.Noise(u * 4f, v * 128f, 4, 128, 211) * 0.6f + NaturalTextures.Noise(u * 128f, v * 4f, 128, 4, 212) * 0.4f;
                        float shade = 0.62f + 0.32f * h + (slub - 0.5f) * 0.12f;
                        pixels[y * Size + x] = new Color(shade, shade, shade, 1f);
                        heights[y * Size + x] = h;
                    }
                }
                _cloth = NaturalTextures.Finish("TFA_SpeakerCloth", pixels);
                _clothNormal = NaturalTextures.NormalMap("TFA_SpeakerClothNormal", heights, 1.6f);
            }
            normal = _clothNormal;
            return _cloth;
        }

        // Brushed metal: fine streaks along u
        private static Texture2D Brushed(out Texture2D normal)
        {
            if (_brushed == null)
            {
                var pixels = new Color[Size * Size];
                var heights = new float[Size * Size];
                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        float u = (x + 0.5f) / Size, v = (y + 0.5f) / Size;
                        float streak = NaturalTextures.Noise(u * 2f, v * 512f, 2, 512, 221) * 0.6f + NaturalTextures.Noise(u * 8f, v * 256f, 8, 256, 222) * 0.4f;
                        float shade = 0.86f + (streak - 0.5f) * 0.2f;
                        pixels[y * Size + x] = new Color(shade, shade, shade, 1f);
                        heights[y * Size + x] = streak * 0.3f;
                    }
                }
                _brushed = NaturalTextures.Finish("TFA_SpeakerBrushed", pixels);
                _brushedNormal = NaturalTextures.NormalMap("TFA_SpeakerBrushedNormal", heights, 1f);
            }
            normal = _brushedNormal;
            return _brushed;
        }

        // The cone: a 2/2 twill of 4 mm tows, the two directions catching the light differently
        private static Texture2D Twill(out Texture2D normal)
        {
            if (_twill == null)
            {
                var pixels = new Color[Size * Size];
                var heights = new float[Size * Size];
                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        float u = (x + 0.5f) / Size, v = (y + 0.5f) / Size;
                        float h = Weave(u, v, 64, 2, out bool warp);
                        float shade = (0.5f + 0.45f * h) * (warp ? 1f : 0.78f);
                        pixels[y * Size + x] = new Color(shade, shade, shade, 1f);
                        heights[y * Size + x] = h;
                    }
                }
                _twill = NaturalTextures.Finish("TFA_SpeakerTwill", pixels);
                _twillNormal = NaturalTextures.NormalMap("TFA_SpeakerTwillNormal", heights, 2f);
            }
            normal = _twillNormal;
            return _twill;
        }

        private static float Weave(float u, float v, int threads, int run) => Weave(u, v, threads, run, out _);

        // Threads along u (warp) and v (weft), each passing over <paramref name="run"/> of the other
        // in turn (1: plain weave, 2: twill): the height of the thread on top, rounded across it
        private static float Weave(float u, float v, int threads, int run, out bool warp)
        {
            float tu = u * threads, tv = v * threads;
            int i = Mathf.FloorToInt(tu), j = Mathf.FloorToInt(tv);
            warp = ((i + j) / run & 1) == 0;
            float across = warp ? tv - j : tu - i;
            return 0.35f + 0.65f * Mathf.Sin(across * Mathf.PI);
        }
    }
}

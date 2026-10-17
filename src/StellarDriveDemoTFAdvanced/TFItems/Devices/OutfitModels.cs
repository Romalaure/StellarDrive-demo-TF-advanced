using System.Collections.Generic;
using System.Linq;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using UnityEngine;
using UnityEngine.Rendering;

namespace StellarDriveDemoTFAdvanced.TFItems.Devices
{
    /// <summary>
    /// Puts an outfit on the game's player avatar: its suit, helmet, backpack and visor tinted
    /// (property blocks on its own renderers), and the accessories built here, fixed to its bones
    /// so they follow its animation: a hat on the helmet, sunglasses over the visor, a jacket
    /// (torso, collar, sleeves on the arm bones, tails on the hips).
    /// </summary>
    internal static class OutfitModels
    {
        private const string Prefix = "TFA_Outfit";
        private static readonly int ColorId = Shader.PropertyToID("_Color"), BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();

        public static readonly string[] HatNames = { "-", "cap", "cowboy", "tophat", "beanie", "crown" };

        public static void Dress(Transform avatar, Outfit outfit)
        {
            foreach (Transform old in avatar.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith(Prefix)).ToList())
                Object.Destroy(old.gameObject);
            foreach (SkinnedMeshRenderer renderer in avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                string name = renderer.name;
                Color? tint = name.StartsWith("PlayerModel") ? outfit.Suit
                    : name.StartsWith("Helmet2_Shell") ? outfit.Helmet
                    : name.StartsWith("Helmet2_Visor") ? outfit.Visor
                    : name.StartsWith("Backpiece") || name.StartsWith("Harnest") ? outfit.Pack : null;
                Block.Clear();
                if (tint.HasValue)
                {
                    Block.SetColor(ColorId, tint.Value);
                    Block.SetColor(BaseColorId, tint.Value);
                }
                renderer.SetPropertyBlock(Block);
            }
            Transform head = Bone(avatar, "mixamorig:Head");
            if (head != null && outfit.Hat > 0)
                Attach(head, "Hat", Hat(outfit.Hat), outfit.Hat == 5 ? new Color(1f, 0.78f, 0.2f) : outfit.HatColor, outfit.Hat == 5 ? 1f : 0.1f, outfit.Hat == 5 ? 0.85f : 0.3f);
            if (head != null && outfit.Glasses > 0)
            {
                (MeshBuilder frame, MeshBuilder lens) = Glasses(outfit.Glasses);
                Attach(head, "GlassesFrame", frame, new Color(0.08f, 0.08f, 0.09f), 0.8f, 0.8f);
                AttachLit(head, "GlassesLens", lens, outfit.Lens, 0.6f, 0.97f);
            }
            if (outfit.Jacket > 0)
                Jacket(avatar, outfit.Jacket, outfit.JacketColor);
        }

        private static Transform Bone(Transform avatar, string name) => avatar.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        // In the game's part look (lit like the rest of the world), or plain URP Lit before it is known
        private static void Attach(Transform bone, string name, MeshBuilder builder, Color color, float metal, float smooth)
        {
            Material material = DeviceModels.Flat($"TF_Outfit_{ColorUtility.ToHtmlStringRGB(color)}_{metal:0.0}", color, metal, smooth)
                ?? LitMaterials.Get($"TF_OutfitLit_{ColorUtility.ToHtmlStringRGB(color)}_{metal:0.0}", color, metal, smooth);
            Add(bone, name, builder, material);
        }

        private static void AttachLit(Transform bone, string name, MeshBuilder builder, Color color, float metal, float smooth) =>
            Add(bone, name, builder, LitMaterials.Get($"TF_OutfitGloss_{ColorUtility.ToHtmlStringRGB(color)}", color, metal, smooth));

        private static void Add(Transform bone, string name, MeshBuilder builder, Material material)
        {
            var child = new GameObject(Prefix + name) { layer = bone.gameObject.layer };
            child.transform.SetParent(bone, false);
            child.AddComponent<MeshFilter>().sharedMesh = builder.Build(Prefix + name);
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
        }

        // ---- Hats, in the head bone's frame (y up the head, z forward); the helmet's top is ~0.24 above the bone
        private const float HelmetTop = 0.235f, HeadZ = 0.03f;

        private static MeshBuilder Hat(int kind)
        {
            var m = new MeshBuilder();
            var top = new Vector3(0f, HelmetTop, HeadZ);
            switch (kind)
            {
                case 1: // Cap: a dome and a peak forward
                    for (int i = 0; i < 4; i++)
                    {
                        float a0 = i / 4f * Mathf.PI * 0.5f, a1 = (i + 1) / 4f * Mathf.PI * 0.5f;
                        m.Cylinder(top + Vector3.up * (Mathf.Sin(a0) * 0.07f - 0.04f), top + Vector3.up * (Mathf.Sin(a1) * 0.07f - 0.04f), 0.125f * Mathf.Cos(a0), Mathf.Max(0.001f, 0.125f * Mathf.Cos(a1)), 18, i == 0, false);
                    }
                    m.Box(top + new Vector3(0f, -0.035f, 0.15f), new Vector3(0.18f, 0.01f, 0.1f), Quaternion.Euler(-8f, 0f, 0f));
                    m.Cylinder(top + Vector3.up * 0.03f, top + Vector3.up * 0.04f, 0.012f, 0.012f, 8);
                    break;
                case 2: // Cowboy: a pinched crown, a wide brim, a band
                    m.Cylinder(top + Vector3.down * 0.05f, top + Vector3.down * 0.035f, 0.26f, 0.25f, 24);
                    m.Cylinder(top + Vector3.down * 0.04f, top + Vector3.up * 0.07f, 0.12f, 0.1f, 18);
                    m.Box(top + Vector3.up * 0.07f, new Vector3(0.05f, 0.02f, 0.18f));
                    m.Cylinder(top + Vector3.down * 0.035f, top + Vector3.down * 0.015f, 0.123f, 0.121f, 18);
                    break;
                case 3: // Top hat: a tall cylinder on a brim, a band
                    m.Cylinder(top + Vector3.down * 0.045f, top + Vector3.down * 0.035f, 0.19f, 0.19f, 24);
                    m.Cylinder(top + Vector3.down * 0.04f, top + Vector3.up * 0.18f, 0.115f, 0.125f, 20);
                    m.Cylinder(top + Vector3.down * 0.035f, top + Vector3.down * 0.005f, 0.118f, 0.117f, 20);
                    break;
                case 4: // Beanie: a soft dome, a turned-up rim and a pompom
                    m.Cylinder(top + Vector3.down * 0.06f, top + Vector3.down * 0.02f, 0.135f, 0.135f, 20);
                    for (int i = 0; i < 4; i++)
                    {
                        float a0 = i / 4f * Mathf.PI * 0.5f, a1 = (i + 1) / 4f * Mathf.PI * 0.5f;
                        m.Cylinder(top + Vector3.up * (Mathf.Sin(a0) * 0.09f - 0.02f), top + Vector3.up * (Mathf.Sin(a1) * 0.09f - 0.02f), 0.128f * Mathf.Cos(a0), Mathf.Max(0.001f, 0.128f * Mathf.Cos(a1)), 18, false, false);
                    }
                    m.Cylinder(top + Vector3.up * 0.065f, top + Vector3.up * 0.11f, 0.03f, 0.02f, 10);
                    break;
                case 5: // Crown: a ring of points
                    m.Cylinder(top + Vector3.down * 0.03f, top + Vector3.up * 0.02f, 0.12f, 0.12f, 24, false, false);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4f;
                        Vector3 at = top + new Vector3(Mathf.Cos(a) * 0.115f, 0.02f, Mathf.Sin(a) * 0.115f);
                        m.Cylinder(at, at + Vector3.up * 0.055f, 0.022f, 0f, 4);
                    }
                    break;
            }
            return m;
        }

        // ---- Sunglasses over the visor (its front ~0.15 forward of the head bone, at eye height)
        private static (MeshBuilder Frame, MeshBuilder Lens) Glasses(int kind)
        {
            var frame = new MeshBuilder();
            var lens = new MeshBuilder();
            const float y = 0.05f, z = 0.175f;
            switch (kind)
            {
                case 1: // Round-cornered lenses, a bridge, the arms back to the helmet
                    foreach (float s in new[] { -1f, 1f })
                    {
                        lens.Box(new Vector3(s * 0.045f, y, z), new Vector3(0.07f, 0.045f, 0.006f));
                        frame.Box(new Vector3(s * 0.045f, y + 0.025f, z), new Vector3(0.076f, 0.008f, 0.01f));
                        frame.Box(new Vector3(s * 0.083f, y, z - 0.04f), new Vector3(0.006f, 0.008f, 0.09f));
                    }
                    frame.Box(new Vector3(0f, y + 0.012f, z), new Vector3(0.024f, 0.006f, 0.008f));
                    break;
                case 2: // Aviators: drop-shaped lenses (tilted), thin gold-ish bars
                    foreach (float s in new[] { -1f, 1f })
                    {
                        lens.Cylinder(new Vector3(s * 0.046f, y, z - 0.003f), new Vector3(s * 0.046f, y, z + 0.003f), 0.034f, 0.034f, 14);
                        lens.Box(new Vector3(s * 0.05f, y - 0.022f, z), new Vector3(0.05f, 0.02f, 0.006f), Quaternion.Euler(0f, 0f, s * 20f));
                        frame.Box(new Vector3(s * 0.085f, y + 0.01f, z - 0.04f), new Vector3(0.005f, 0.006f, 0.09f));
                    }
                    frame.Box(new Vector3(0f, y + 0.026f, z), new Vector3(0.13f, 0.005f, 0.006f));
                    frame.Box(new Vector3(0f, y + 0.008f, z), new Vector3(0.02f, 0.005f, 0.006f));
                    break;
                case 3: // A wide single visor band, wrapping the helmet's front
                    for (int i = 0; i < 6; i++)
                    {
                        float a0 = Mathf.Lerp(-0.7f, 0.7f, i / 6f), a1 = Mathf.Lerp(-0.7f, 0.7f, (i + 1) / 6f);
                        Vector3 p0 = new Vector3(Mathf.Sin(a0) * 0.13f, y, Mathf.Cos(a0) * 0.13f + 0.035f), p1 = new Vector3(Mathf.Sin(a1) * 0.13f, y, Mathf.Cos(a1) * 0.13f + 0.035f);
                        Vector3 mid = (p0 + p1) * 0.5f, along = (p1 - p0).normalized, outward = new Vector3(mid.x, 0f, mid.z - 0.035f).normalized;
                        lens.Box(mid, new Vector3((p1 - p0).magnitude + 0.002f, 0.05f, 0.006f), along, Vector3.up, outward);
                    }
                    foreach (float s in new[] { -1f, 1f })
                        frame.Box(new Vector3(s * 0.115f, y, -0.01f), new Vector3(0.008f, 0.02f, 0.1f));
                    break;
            }
            return (frame, lens);
        }

        // ---- Jacket: torso on the spine, collar on the neck, sleeves on the arms, tails on the hips
        private static void Jacket(Transform avatar, int kind, Color color)
        {
            Transform spine = Bone(avatar, "mixamorig:Spine1"), chest = Bone(avatar, "mixamorig:Spine2"), hips = Bone(avatar, "mixamorig:Hips");
            if (chest != null)
            {
                var torso = new MeshBuilder();
                // An open front: two panels, the back, the sides
                torso.Box(new Vector3(0f, 0.05f, -0.085f), new Vector3(0.27f, 0.28f, 0.02f));
                foreach (float s in new[] { -1f, 1f })
                {
                    torso.Box(new Vector3(s * 0.08f, 0.05f, 0.082f), new Vector3(0.1f, 0.28f, 0.02f));
                    torso.Box(new Vector3(s * 0.13f, 0.05f, 0f), new Vector3(0.02f, 0.28f, 0.17f));
                    // Lapels
                    torso.Box(new Vector3(s * 0.045f, 0.15f, 0.094f), new Vector3(0.045f, 0.07f, 0.008f), Quaternion.Euler(0f, 0f, s * -25f));
                }
                if (kind >= 2)
                    torso.Cylinder(new Vector3(0f, 0.19f, -0.005f), new Vector3(0f, 0.24f, -0.005f), 0.085f, 0.08f, 16, false, false);
                Attach(chest, "JacketTorso", torso, color, 0.05f, 0.35f);
            }
            if (spine != null)
            {
                var lower = new MeshBuilder();
                lower.Box(new Vector3(0f, -0.02f, -0.082f), new Vector3(0.26f, 0.15f, 0.02f));
                foreach (float s in new[] { -1f, 1f })
                {
                    lower.Box(new Vector3(s * 0.078f, -0.02f, 0.08f), new Vector3(0.1f, 0.15f, 0.02f));
                    lower.Box(new Vector3(s * 0.125f, -0.02f, 0f), new Vector3(0.02f, 0.15f, 0.16f));
                }
                Attach(spine, "JacketWaist", lower, color, 0.05f, 0.35f);
            }
            if (kind >= 2)
            {
                foreach (string side in new[] { "Left", "Right" })
                {
                    foreach ((string bone, float length, float radius) in new[] { ("Arm", 0.2f, 0.05f), ("ForeArm", 0.17f, 0.045f) })
                    {
                        Transform t = Bone(avatar, "mixamorig:" + side + bone);
                        if (t == null)
                            continue;
                        var sleeve = new MeshBuilder();
                        sleeve.Cylinder(Vector3.zero, Vector3.up * length, radius, radius * 0.92f, 12);
                        Attach(t, "Sleeve" + side + bone, sleeve, color, 0.05f, 0.35f);
                    }
                }
            }
            if (kind == 3 && hips != null)
            {
                // Long coat: tails down to the knees, open at the front
                var tails = new MeshBuilder();
                tails.Box(new Vector3(0f, -0.2f, -0.095f), new Vector3(0.28f, 0.42f, 0.02f));
                foreach (float s in new[] { -1f, 1f })
                {
                    tails.Box(new Vector3(s * 0.085f, -0.2f, 0.09f), new Vector3(0.1f, 0.42f, 0.02f));
                    tails.Box(new Vector3(s * 0.14f, -0.2f, 0f), new Vector3(0.02f, 0.42f, 0.18f));
                }
                Attach(hips, "JacketTails", tails, color, 0.05f, 0.35f);
            }
        }
    }
}

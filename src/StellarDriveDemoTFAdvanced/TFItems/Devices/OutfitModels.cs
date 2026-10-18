using System.Collections.Generic;
using System.Linq;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using UnityEngine;
using UnityEngine.Rendering;

namespace StellarDriveDemoTFAdvanced.TFItems.Devices
{
    /// <summary>
    /// Puts an outfit on the game's player avatar: its suit, helmet, backpack and visor tinted
    /// (property blocks on its own renderers), and the accessories built here, fitted to it:
    /// the jacket is the suit's own skinned mesh (torso, then the arms, then the hips and thighs
    /// for a long coat), pushed out a little and bound to the same bones, so it follows every
    /// animation exactly; the hat and the sunglasses are measured against the helmet and the
    /// visor as they stand (their size and where their top and front are), then fixed to the head bone.
    /// </summary>
    internal static class OutfitModels
    {
        private const string Prefix = "TFA_Outfit";
        private static readonly int ColorId = Shader.PropertyToID("_Color"), BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();

        public static readonly string[] HatNames = { "-", "cap", "cowboy", "tophat", "beanie", "crown" };

        // The suit's bones a jacket covers: the torso (vest), the arms (jacket), the hips and thighs (long coat)
        private static readonly string[] TorsoBones = { "Spine", "Spine1", "Spine2", "Neck", "LeftShoulder", "RightShoulder" };
        private static readonly string[] ArmBones = { "LeftArm", "LeftForeArm", "RightArm", "RightForeArm" };
        private static readonly string[] CoatBones = { "Hips", "LeftUpLeg", "RightUpLeg" };

        // Jackets cut from a suit mesh, kept per mesh and kind
        private static readonly Dictionary<(int, int), Mesh> Jackets = new Dictionary<(int, int), Mesh>();

        /// <summary>
        /// <paramref name="firstPerson"/>: this player's own body, seen from inside the helmet: the
        /// colors and the jacket only (a hat or glasses would sit in the camera).
        /// </summary>
        public static void Dress(Transform avatar, Outfit outfit, bool firstPerson = false)
        {
            foreach (Transform old in avatar.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith(Prefix)).ToList())
                Object.Destroy(old.gameObject);
            SkinnedMeshRenderer suit = null, shell = null, visor = null;
            foreach (SkinnedMeshRenderer renderer in avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                string name = renderer.name;
                if (name.StartsWith(Prefix))
                    continue;
                if (name.StartsWith("PlayerModel") && suit == null)
                    suit = renderer;
                else if (name.StartsWith("Helmet2_Shell"))
                    shell = renderer;
                else if (name.StartsWith("Helmet2_Visor"))
                    visor = renderer;
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

            if (outfit.Jacket > 0 && suit != null)
                Jacket(suit, outfit.Jacket, outfit.JacketColor);
            if (firstPerson)
                return;

            Transform head = Bone(avatar, "mixamorig:Head");
            if (head == null)
                return;
            if (outfit.Hat > 0 && shell != null && Measure(shell, avatar, head, out Shape helmet))
            {
                // Sized on the helmet's crown, sitting on its top
                Fix(head, avatar, "Hat", Hat(outfit.Hat), helmet.Center(helmet.Top), helmet.Radius,
                    outfit.Hat == 5 ? new Color(1f, 0.78f, 0.2f) : outfit.HatColor, outfit.Hat == 5 ? 1f : 0.1f, outfit.Hat == 5 ? 0.85f : 0.3f, lit: false);
            }
            if (outfit.Glasses > 0 && visor != null && Measure(visor, avatar, head, out Shape glass))
            {
                // Over the visor at eye height, as wide as it, but in front of the helmet's rim:
                // the visor sits recessed in the shell, glasses on it would be hidden inside
                float front = glass.Front;
                if (shell != null && Measure(shell, avatar, head, out Shape rim, glass.EyeHeight))
                    front = Mathf.Max(front, rim.Front);
                Vector3 at = glass.Center(glass.EyeHeight) + Vector3.forward * (front - glass.Middle.z + 0.065f);
                float width = glass.Width * 1.22f;
                (MeshBuilder frame, MeshBuilder lens) = Glasses(outfit.Glasses);
                // A faint glow of their own: on the dark visor, in a dark ship, they still read
                var metal = new Color(0.86f, 0.86f, 0.9f);
                Fix(head, avatar, "GlassesFrame", frame, at, width, metal, 0.5f, 0.85f, lit: true, glow: metal * 0.32f);
                Fix(head, avatar, "GlassesLens", lens, at, width, outfit.Lens, 0.6f, 0.97f, lit: true, glow: Color.Lerp(outfit.Lens, new Color(0.55f, 0.75f, 1f), 0.35f) * 0.3f);
                if (StellarDriveDemoTFAdvanced.Common.DevHarness.Active)
                    TFMod.Log.Msg($"dev: outfit glasses: visor front {glass.Front:0.000}, helmet front {front:0.000}, eye {glass.EyeHeight:0.000}, width {width:0.000}");
            }
            if (StellarDriveDemoTFAdvanced.Common.DevHarness.Active)
                TFMod.Log.Msg($"dev: outfit on {avatar.name}: suit {(suit != null ? suit.name + " readable " + suit.sharedMesh?.isReadable : "none")}, helmet {(shell != null ? Measure(shell, avatar, head, out Shape h) ? $"top {h.Top:0.000} radius {h.Radius:0.000}, head bone at {Quaternion.Inverse(avatar.rotation) * (head.position - avatar.position)}" : "not measured" : "none")}");
        }

        private static Transform Bone(Transform avatar, string name) => avatar.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        // ---- Measures, in the avatar's frame (x right, y up, z forward, from its origin, in meters)

        private struct Shape
        {
            public Vector3 Middle;       // the middle of its horizontal extent
            public float Top, Bottom, Front;
            public float Radius;         // around its crown (the top quarter)
            public float Width;          // half its width at eye height
            public float EyeHeight;      // the middle of its height

            public Vector3 Center(float y) => new Vector3(Middle.x, y, Middle.z);
        }

        private static bool Measure(SkinnedMeshRenderer renderer, Transform avatar, Transform head, out Shape shape, float? eyeAt = null)
        {
            shape = default;
            try
            {
                // Skinned by hand from the bones as they stand: exact, whatever the scales of the
                // avatar's parents (the game's avatar is made in centimeters)
                Mesh mesh = renderer.sharedMesh;
                Transform[] bones = renderer.bones;
                if (mesh == null || !mesh.isReadable || bones == null || bones.Length == 0)
                    return false;
                Vector3[] vertices = mesh.vertices;
                BoneWeight[] weights = mesh.boneWeights;
                Matrix4x4[] bind = mesh.bindposes;
                if (vertices.Length == 0 || weights.Length != vertices.Length)
                    return false;
                var skin = new Matrix4x4[bones.Length];
                for (int i = 0; i < bones.Length && i < bind.Length; i++)
                    skin[i] = bones[i] != null ? bones[i].localToWorldMatrix * bind[i] : Matrix4x4.zero;
                Vector3 Skin(int i)
                {
                    BoneWeight w = weights[i];
                    Vector3 v = vertices[i];
                    return skin[w.boneIndex0].MultiplyPoint3x4(v) * w.weight0 + skin[w.boneIndex1].MultiplyPoint3x4(v) * w.weight1
                        + skin[w.boneIndex2].MultiplyPoint3x4(v) * w.weight2 + skin[w.boneIndex3].MultiplyPoint3x4(v) * w.weight3;
                }
                Quaternion inverse = Quaternion.Inverse(avatar.rotation);
                var points = new Vector3[vertices.Length];
                for (int i = 0; i < vertices.Length; i++)
                    points[i] = inverse * (Skin(i) - avatar.position);
                float top = points.Max(p => p.y), bottom = points.Min(p => p.y);
                float minX = points.Min(p => p.x), maxX = points.Max(p => p.x), minZ = points.Min(p => p.z), maxZ = points.Max(p => p.z);
                var middle = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
                float crown = top - (top - bottom) * 0.25f, eye = eyeAt ?? (top + bottom) * 0.5f, band = eyeAt.HasValue ? 0.02f : (top - bottom) * 0.2f;
                float radius = points.Where(p => p.y >= crown).Select(p => new Vector2(p.x - middle.x, p.z - middle.z).magnitude).DefaultIfEmpty(0f).Max();
                var eyes = points.Where(p => Mathf.Abs(p.y - eye) <= band).ToList();
                shape = new Shape
                {
                    Middle = middle,
                    Top = top,
                    Bottom = bottom,
                    Front = eyes.Count > 0 ? eyes.Max(p => p.z) : maxZ,
                    Radius = radius,
                    Width = eyes.Count > 0 ? (eyes.Max(p => p.x) - eyes.Min(p => p.x)) * 0.5f : (maxX - minX) * 0.5f,
                    EyeHeight = eye
                };
                return radius > 0.01f;
            }
            catch (System.Exception e)
            {
                TFMod.Log.Warning($"outfits: could not measure {renderer.name}: {e.Message}");
                return false;
            }
        }

        // A model built around its origin (y up, z forward, 1 unit = scale meters) placed at a point
        // of the avatar's frame, then fixed to the head bone so it follows the head from now on
        private static void Fix(Transform bone, Transform avatar, string name, MeshBuilder builder, Vector3 at, float scale, Color color, float metal, float smooth, bool lit, Color? glow = null)
        {
            Material material = glow.HasValue ? LitMaterials.Get($"TF_OutfitGlow_{ColorUtility.ToHtmlStringRGB(color)}_{ColorUtility.ToHtmlStringRGB(glow.Value)}", color, metal, smooth, glow.Value)
                : lit ? LitMaterials.Get($"TF_OutfitGloss_{ColorUtility.ToHtmlStringRGB(color)}", color, metal, smooth)
                : DeviceModels.Flat($"TF_Outfit_{ColorUtility.ToHtmlStringRGB(color)}_{metal:0.0}", color, metal, smooth)
                  ?? LitMaterials.Get($"TF_OutfitLit_{ColorUtility.ToHtmlStringRGB(color)}_{metal:0.0}", color, metal, smooth);
            var child = new GameObject(Prefix + name) { layer = bone.gameObject.layer };
            child.transform.SetPositionAndRotation(avatar.position + avatar.rotation * at, avatar.rotation);
            child.transform.localScale = Vector3.one * scale;
            child.transform.SetParent(bone, true);
            child.AddComponent<MeshFilter>().sharedMesh = builder.Build(Prefix + name);
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
        }

        // ---- Hats, for a helmet of radius 1 whose top is the origin

        private static MeshBuilder Hat(int kind)
        {
            var m = new MeshBuilder();
            switch (kind)
            {
                case 1: // Cap: a soft crown down the helmet's sides, a curved peak forward, a button
                    Dome(m, -0.48f, 0.08f, 0.97f, 0.6f, 6);
                    for (int i = 0; i < 5; i++)
                    {
                        float a0 = Mathf.Lerp(-0.75f, 0.75f, i / 5f), a1 = Mathf.Lerp(-0.75f, 0.75f, (i + 1) / 5f);
                        Vector3 p0 = new Vector3(Mathf.Sin(a0) * 0.95f, -0.44f, Mathf.Cos(a0) * 0.95f), p1 = new Vector3(Mathf.Sin(a1) * 0.95f, -0.44f, Mathf.Cos(a1) * 0.95f);
                        Vector3 q0 = new Vector3(Mathf.Sin(a0) * 1.55f, -0.56f, Mathf.Cos(a0) * 1.55f), q1 = new Vector3(Mathf.Sin(a1) * 1.55f, -0.56f, Mathf.Cos(a1) * 1.55f);
                        Vector3 t = Vector3.up * 0.04f;
                        m.Block(new[] { p0, p1, p0 + t, p1 + t, q0, q1, q0 + t, q1 + t });
                    }
                    m.Cylinder(Vector3.up * 0.08f, Vector3.up * 0.14f, 0.1f, 0.08f, 10);
                    break;
                case 2: // Cowboy: a wide brim, a pinched crown, a band
                    m.Cylinder(Vector3.up * -0.42f, Vector3.up * -0.36f, 1.95f, 1.9f, 28);
                    m.Cylinder(Vector3.up * -0.4f, Vector3.up * 0.5f, 0.95f, 0.82f, 22);
                    m.Box(Vector3.up * 0.5f, new Vector3(0.35f, 0.12f, 1.3f));
                    m.Cylinder(Vector3.up * -0.36f, Vector3.up * -0.2f, 0.97f, 0.95f, 22);
                    break;
                case 3: // Top hat: a tall crown on a brim, a band
                    m.Cylinder(Vector3.up * -0.4f, Vector3.up * -0.34f, 1.4f, 1.4f, 28);
                    m.Cylinder(Vector3.up * -0.38f, Vector3.up * 1.15f, 0.92f, 0.98f, 24);
                    m.Cylinder(Vector3.up * -0.34f, Vector3.up * -0.12f, 0.94f, 0.94f, 24);
                    break;
                case 4: // Beanie: a turned-up rim, a soft dome, a pompom
                    m.Cylinder(Vector3.up * -0.6f, Vector3.up * -0.36f, 1.03f, 1.0f, 22);
                    Dome(m, -0.36f, 0.18f, 0.98f, 0f, 6);
                    m.Cylinder(Vector3.up * 0.14f, Vector3.up * 0.42f, 0.22f, 0.12f, 12);
                    break;
                case 5: // Crown: an open ring of points with a jewel band
                    m.Cylinder(Vector3.up * -0.32f, Vector3.up * 0.02f, 0.86f, 0.9f, 28, false, false);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4f;
                        Vector3 at = new Vector3(Mathf.Cos(a) * 0.88f, 0.02f, Mathf.Sin(a) * 0.88f);
                        m.Cylinder(at, at + Vector3.up * 0.42f, 0.15f, 0f, 4);
                        m.Box(new Vector3(Mathf.Cos(a + 0.39f) * 0.9f, -0.15f, Mathf.Sin(a + 0.39f) * 0.9f), Vector3.one * 0.1f);
                    }
                    break;
            }
            return m;
        }

        // A dome from y0 (radius r0) up to y1, narrowing to r1
        private static void Dome(MeshBuilder m, float y0, float y1, float r0, float r1, int steps)
        {
            for (int i = 0; i < steps; i++)
            {
                float a0 = i / (float)steps * Mathf.PI * 0.5f, a1 = (i + 1) / (float)steps * Mathf.PI * 0.5f;
                float ra = Mathf.Lerp(r1, r0, Mathf.Cos(a0)), rb = Mathf.Lerp(r1, r0, Mathf.Cos(a1));
                m.Cylinder(Vector3.up * Mathf.Lerp(y0, y1, Mathf.Sin(a0)), Vector3.up * Mathf.Lerp(y0, y1, Mathf.Sin(a1)), ra, Mathf.Max(0.001f, rb), 20, i == 0, i == steps - 1);
            }
        }

        // ---- Sunglasses, for a visor of half width 1, on its front at the origin

        private static (MeshBuilder Frame, MeshBuilder Lens) Glasses(int kind)
        {
            var frame = new MeshBuilder();
            var lens = new MeshBuilder();
            switch (kind)
            {
                case 1: // Square lenses in full rims, a bridge, short arms to the helmet's side
                    foreach (float s in new[] { -1f, 1f })
                    {
                        lens.Box(new Vector3(s * 0.34f, 0f, 0f), new Vector3(0.5f, 0.34f, 0.04f));
                        Rim(frame, new Vector3(s * 0.34f, 0f, 0f), 0.56f, 0.4f, 0.07f);
                        frame.Box(new Vector3(s * 0.64f, 0.14f, -0.3f), new Vector3(0.06f, 0.07f, 0.6f));
                    }
                    frame.Box(new Vector3(0f, 0.1f, 0f), new Vector3(0.14f, 0.06f, 0.07f));
                    break;
                case 2: // Aviators: drop-shaped lenses, thin bars
                    foreach (float s in new[] { -1f, 1f })
                    {
                        lens.Cylinder(new Vector3(s * 0.32f, 0f, -0.02f), new Vector3(s * 0.32f, 0f, 0.02f), 0.24f, 0.24f, 16);
                        lens.Box(new Vector3(s * 0.35f, -0.15f, 0f), new Vector3(0.34f, 0.14f, 0.04f), Quaternion.Euler(0f, 0f, s * 20f));
                        // A thin round rim around each drop
                        for (int i = 0; i < 16; i++)
                        {
                            float a = i * Mathf.PI / 8f;
                            var at = new Vector3(s * 0.32f + Mathf.Cos(a) * 0.27f, Mathf.Sin(a) * 0.27f - (Mathf.Sin(a) < 0f ? 0.06f : 0f), 0.01f);
                            frame.Box(at, new Vector3(0.11f, 0.045f, 0.05f), Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg + 90f));
                        }
                        frame.Box(new Vector3(s * 0.62f, 0.12f, -0.3f), new Vector3(0.05f, 0.05f, 0.6f));
                    }
                    frame.Box(new Vector3(0f, 0.24f, 0.01f), new Vector3(1.05f, 0.045f, 0.05f));
                    frame.Box(new Vector3(0f, 0.08f, 0.01f), new Vector3(0.16f, 0.045f, 0.05f));
                    break;
                case 3: // A wide single band wrapping the visor's front
                    for (int i = 0; i < 8; i++)
                    {
                        float a0 = Mathf.Lerp(-0.8f, 0.8f, i / 8f), a1 = Mathf.Lerp(-0.8f, 0.8f, (i + 1) / 8f);
                        Vector3 p0 = new Vector3(Mathf.Sin(a0), 0f, (Mathf.Cos(a0) - 1f) * 0.9f), p1 = new Vector3(Mathf.Sin(a1), 0f, (Mathf.Cos(a1) - 1f) * 0.9f);
                        Vector3 mid = (p0 + p1) * 0.5f, along = (p1 - p0).normalized, outward = Vector3.Cross(along, Vector3.up).normalized;
                        if (outward.z < 0f)
                            outward = -outward;
                        lens.Box(mid, new Vector3((p1 - p0).magnitude + 0.01f, 0.3f, 0.04f), along, Vector3.up, outward);
                    }
                    // Bright edges along the band, top and bottom, and the hinges
                    for (int i = 0; i < 8; i++)
                    {
                        float a0 = Mathf.Lerp(-0.8f, 0.8f, i / 8f), a1 = Mathf.Lerp(-0.8f, 0.8f, (i + 1) / 8f);
                        Vector3 p0 = new Vector3(Mathf.Sin(a0), 0f, (Mathf.Cos(a0) - 1f) * 0.9f), p1 = new Vector3(Mathf.Sin(a1), 0f, (Mathf.Cos(a1) - 1f) * 0.9f);
                        Vector3 mid = (p0 + p1) * 0.5f + Vector3.forward * 0.01f, along = (p1 - p0).normalized, outward = Vector3.Cross(along, Vector3.up).normalized;
                        if (outward.z < 0f)
                            outward = -outward;
                        foreach (float y in new[] { -0.16f, 0.16f })
                            frame.Box(mid + Vector3.up * y, new Vector3((p1 - p0).magnitude + 0.02f, 0.05f, 0.06f), along, Vector3.up, outward);
                    }
                    foreach (float s in new[] { -1f, 1f })
                        frame.Box(new Vector3(s * 0.74f, 0f, -0.32f), new Vector3(0.07f, 0.2f, 0.16f));
                    break;
            }
            return (frame, lens);
        }

        // A rectangular rim of bars around a lens centered at c (w x h outside, bar thickness t)
        private static void Rim(MeshBuilder m, Vector3 c, float w, float h, float t)
        {
            m.Box(c + new Vector3(0f, (h - t) * 0.5f, 0.01f), new Vector3(w, t, 0.06f));
            m.Box(c + new Vector3(0f, -(h - t) * 0.5f, 0.01f), new Vector3(w, t, 0.06f));
            m.Box(c + new Vector3((w - t) * 0.5f, 0f, 0.01f), new Vector3(t, h, 0.06f));
            m.Box(c + new Vector3(-(w - t) * 0.5f, 0f, 0.01f), new Vector3(t, h, 0.06f));
        }

        // ---- Jacket: the suit's own skinned mesh, cut and pushed out

        private static void Jacket(SkinnedMeshRenderer suit, int kind, Color color)
        {
            Mesh mesh = suit.sharedMesh;
            if (mesh == null || suit.bones == null || suit.bones.Length == 0)
                return;
            if (!Jackets.TryGetValue((mesh.GetInstanceID(), kind), out Mesh jacket) || jacket == null)
            {
                jacket = Cut(mesh, suit.bones, kind);
                Jackets[(mesh.GetInstanceID(), kind)] = jacket;
            }
            if (jacket == null)
                return;
            Material material = DeviceModels.Flat($"TF_Outfit_{ColorUtility.ToHtmlStringRGB(color)}_0.1", color, 0.1f, 0.3f)
                ?? LitMaterials.Get($"TF_OutfitLit_{ColorUtility.ToHtmlStringRGB(color)}_0.1", color, 0.1f, 0.3f);
            // Beside the suit, in the same frame, on the same bones
            var child = new GameObject(Prefix + "Jacket") { layer = suit.gameObject.layer };
            child.transform.SetParent(suit.transform.parent, false);
            child.transform.localPosition = suit.transform.localPosition;
            child.transform.localRotation = suit.transform.localRotation;
            child.transform.localScale = suit.transform.localScale;
            var renderer = child.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = jacket;
            renderer.bones = suit.bones;
            renderer.rootBone = suit.rootBone;
            renderer.localBounds = suit.localBounds;
            renderer.updateWhenOffscreen = suit.updateWhenOffscreen;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = suit.shadowCastingMode;
            renderer.enabled = suit.enabled;
        }

        // The triangles of the suit mostly carried by the jacket's bones, a little out along their normals
        private static Mesh Cut(Mesh mesh, Transform[] bones, int kind)
        {
            if (!mesh.isReadable)
            {
                TFMod.Log.Warning($"outfits: the suit's mesh {mesh.name} cannot be read, no jacket");
                return null;
            }
            var names = new HashSet<string>(TorsoBones.Concat(kind >= 2 ? ArmBones : new string[0]).Concat(kind >= 3 ? CoatBones : new string[0]).Select(b => "mixamorig:" + b));
            var covered = new bool[bones.Length];
            for (int i = 0; i < bones.Length; i++)
                covered[i] = bones[i] != null && names.Contains(bones[i].name);

            Vector3[] vertices = mesh.vertices, normals = mesh.normals;
            Vector2[] uvs = mesh.uv;
            BoneWeight[] weights = mesh.boneWeights;
            if (weights.Length != vertices.Length || normals.Length != vertices.Length)
                return null;
            float W(int bone, float weight) => bone >= 0 && bone < covered.Length && covered[bone] ? weight : 0f;
            var keep = new bool[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                BoneWeight w = weights[i];
                keep[i] = W(w.boneIndex0, w.weight0) + W(w.boneIndex1, w.weight1) + W(w.boneIndex2, w.weight2) + W(w.boneIndex3, w.weight3) >= 0.5f;
            }

            // A little thicker than the suit, in the mesh's own units
            float push = 0.012f * mesh.bounds.size.magnitude / 2.1f;
            var map = new Dictionary<int, int>();
            var outVertices = new List<Vector3>();
            var outNormals = new List<Vector3>();
            var outUvs = new List<Vector2>();
            var outWeights = new List<BoneWeight>();
            var triangles = new List<int>();
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                int[] source = mesh.GetTriangles(sub);
                for (int t = 0; t + 2 < source.Length; t += 3)
                {
                    if (!keep[source[t]] || !keep[source[t + 1]] || !keep[source[t + 2]])
                        continue;
                    for (int k = 0; k < 3; k++)
                    {
                        int v = source[t + k];
                        if (!map.TryGetValue(v, out int index))
                        {
                            index = outVertices.Count;
                            map[v] = index;
                            outVertices.Add(vertices[v] + normals[v] * push);
                            outNormals.Add(normals[v]);
                            outUvs.Add(uvs.Length == vertices.Length ? uvs[v] : Vector2.zero);
                            outWeights.Add(weights[v]);
                        }
                        triangles.Add(index);
                    }
                }
            }
            if (triangles.Count == 0)
                return null;
            var jacket = new Mesh { name = Prefix + "Jacket" + kind };
            if (outVertices.Count > 65000)
                jacket.indexFormat = IndexFormat.UInt32;
            jacket.SetVertices(outVertices);
            jacket.SetNormals(outNormals);
            jacket.SetUVs(0, outUvs);
            jacket.boneWeights = outWeights.ToArray();
            jacket.bindposes = mesh.bindposes;
            jacket.SetTriangles(triangles, 0);
            jacket.RecalculateBounds();
            return jacket;
        }
    }
}

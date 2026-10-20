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
            SkinnedMeshRenderer suit = null, shell = null, visor = null, pack = null;
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
                else if (name.StartsWith("Backpiece"))
                    pack = renderer;
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
                foreach (Piece piece in HatPieces(outfit.Hat, outfit.HatColor))
                    Fix(head, avatar, "Hat", piece, helmet.Center(helmet.Top), helmet.Radius);
            }
            if (outfit.Glasses > 0 && visor != null && Measure(visor, avatar, head, out Shape glass))
            {
                // Over the visor at eye height, as wide as it, but in front of the helmet's rim:
                // the visor sits recessed in the shell, glasses on it would be hidden inside
                float front = glass.Front;
                if (shell != null && Measure(shell, avatar, head, out Shape rim, glass.EyeHeight))
                    front = Mathf.Max(front, rim.Front);
                Vector3 at = glass.Center(glass.EyeHeight) + Vector3.forward * (front - glass.Middle.z + 0.065f);
                foreach (Piece piece in GlassesPieces(outfit.Glasses, outfit.Lens))
                    Fix(head, avatar, "Glasses", piece, at, glass.Width * 1.22f);
                if (StellarDriveDemoTFAdvanced.Common.DevHarness.Active)
                    TFMod.Log.Msg($"dev: outfit glasses: visor front {glass.Front:0.000}, helmet front {front:0.000}, eye {glass.EyeHeight:0.000}");
            }
            Transform spine = Bone(avatar, "mixamorig:Spine2");
            if (outfit.Back > 0 && pack != null && spine != null && Measure(pack, avatar, spine, out Shape bag))
            {
                // Behind the backpack, at its middle height, sized on its half width
                var at = new Vector3(bag.Middle.x, bag.EyeHeight, bag.Back);
                foreach (Piece piece in BackPieces(outfit.Back, outfit.BackColor))
                    Fix(spine, avatar, "Back", piece, at, bag.Width);
            }
            if (StellarDriveDemoTFAdvanced.Common.DevHarness.Active)
                TFMod.Log.Msg($"dev: outfit on {avatar.name}: suit {(suit != null ? suit.name + " readable " + suit.sharedMesh?.isReadable : "none")}, helmet {(shell != null ? Measure(shell, avatar, head, out Shape h) ? $"top {h.Top:0.000} radius {h.Radius:0.000}, head bone at {Quaternion.Inverse(avatar.rotation) * (head.position - avatar.position)}" : "not measured" : "none")}");
        }

        private static Transform Bone(Transform avatar, string name) => avatar.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        // ---- Measures, in the avatar's frame (x right, y up, z forward, from its origin, in meters)

        private struct Shape
        {
            public Vector3 Middle;       // the middle of its horizontal extent
            public float Top, Bottom, Front, Back;
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
                    Back = minZ,
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

        /// <summary>One look of an accessory: its mesh and material.</summary>
        internal sealed class Piece
        {
            public MeshBuilder Mesh = new MeshBuilder();
            public Color Color;
            public float Metal, Smooth;
            public bool Lit;
            public Color? Glow;

            public Piece(Color color, float metal, float smooth, bool lit = false, Color? glow = null)
            {
                Color = color;
                Metal = metal;
                Smooth = smooth;
                Lit = lit;
                Glow = glow;
            }

            public Material Material() => Glow.HasValue ? LitMaterials.Get($"TF_OutfitGlow_{ColorUtility.ToHtmlStringRGB(Color)}_{ColorUtility.ToHtmlStringRGB(Glow.Value)}", Color, Metal, Smooth, Glow.Value)
                : Lit ? LitMaterials.Get($"TF_OutfitGloss_{ColorUtility.ToHtmlStringRGB(Color)}_{Metal:0.0}", Color, Metal, Smooth)
                : DeviceModels.Flat($"TF_Outfit_{ColorUtility.ToHtmlStringRGB(Color)}_{Metal:0.0}", Color, Metal, Smooth)
                  ?? LitMaterials.Get($"TF_OutfitLit_{ColorUtility.ToHtmlStringRGB(Color)}_{Metal:0.0}", Color, Metal, Smooth);
        }

        // A model built around its origin (y up, z forward, 1 unit = scale meters) placed at a point
        // of the avatar's frame, then fixed to a bone so it follows it from now on
        private static void Fix(Transform bone, Transform avatar, string name, Piece piece, Vector3 at, float scale)
        {
            if (!piece.Mesh.HasGeometry)
                return;
            var child = new GameObject(Prefix + name) { layer = bone.gameObject.layer };
            child.transform.SetPositionAndRotation(avatar.position + avatar.rotation * at, avatar.rotation);
            child.transform.localScale = Vector3.one * scale;
            child.transform.SetParent(bone, true);
            child.AddComponent<MeshFilter>().sharedMesh = piece.Mesh.Build(Prefix + name);
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = piece.Material();
            renderer.shadowCastingMode = piece.Glow.HasValue ? ShadowCastingMode.Off : ShadowCastingMode.On;
        }

        private static Color Shade(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f);
        private static readonly Color Gold = new Color(1f, 0.78f, 0.2f), Ivory = new Color(0.93f, 0.88f, 0.76f), Steel = new Color(0.62f, 0.64f, 0.68f), Black = new Color(0.08f, 0.08f, 0.09f);

        /// <summary>The hat's pieces (its color, a darker band, the details), for a helmet of radius 1 whose top is the origin.</summary>
        public static List<Piece> HatPieces(int kind, Color color)
        {
            bool metalHat = kind == 5 || kind == 7;
            var main = new Piece(kind == 5 ? Gold : kind == 7 ? Steel : color, metalHat ? 0.9f : 0.1f, metalHat ? 0.7f : 0.3f);
            var band = new Piece(kind == 7 ? Shade(Steel, 0.6f) : kind == 8 ? Gold : Shade(color, 0.45f), kind == 8 ? 0.9f : 0.2f, 0.4f);
            var extra = new Piece(kind == 7 ? Ivory : kind == 9 ? Black : kind == 5 ? new Color(0.85f, 0.1f, 0.2f) : Gold, kind == 9 ? 0.2f : 0.6f, 0.6f, lit: kind == 5);
            if (kind == 10)
                main = new Piece(Gold, 0.5f, 0.6f, glow: Gold * 1.4f);
            Hat(kind, main.Mesh, band.Mesh, extra.Mesh);
            return new List<Piece> { main, band, extra };
        }

        /// <summary>The glasses' frame and lenses, for a visor of half width 1, on its front at the origin.</summary>
        public static List<Piece> GlassesPieces(int kind, Color lensColor)
        {
            // A faint glow of their own: on the dark visor, in a dark ship, they still read
            var metal = new Color(0.86f, 0.86f, 0.9f);
            var frame = kind == 5 ? new Piece(Black, 0.3f, 0.5f) : new Piece(metal, 0.5f, 0.85f, lit: true, glow: metal * 0.32f);
            var lens = new Piece(lensColor, 0.6f, 0.97f, lit: true, glow: Color.Lerp(lensColor, new Color(0.55f, 0.75f, 1f), 0.35f) * 0.3f);
            Glasses(kind, frame.Mesh, lens.Mesh);
            return new List<Piece> { frame, lens };
        }

        /// <summary>What hangs on the back, for a backpack of half width 1, its back's middle at the origin (-z is behind).</summary>
        public static List<Piece> BackPieces(int kind, Color color)
        {
            var main = new Piece(color, kind == 2 ? 0.6f : 0.1f, kind == 2 ? 0.6f : 0.3f);
            var trim = new Piece(kind == 1 ? Gold : kind == 3 ? Shade(color, 0.75f) : Steel, kind == 3 ? 0.1f : 0.8f, 0.6f);
            Color light = kind == 4 ? new Color(1f, 0.2f, 0.15f) : new Color(0.3f, 0.8f, 1f);
            var glow = new Piece(light, 0f, 0.1f, glow: light * 2f);
            Back(kind, main.Mesh, trim.Mesh, glow.Mesh);
            return new List<Piece> { main, trim, glow };
        }

        /// <summary>A jacket on its own for the cabin's tile (the worn one is cut from the suit): torso, then sleeves, then long skirts.</summary>
        public static List<Piece> JacketIconPieces(int kind, Color color)
        {
            var cloth = new Piece(color, 0.1f, 0.3f);
            var trim = new Piece(Shade(color, 0.55f), 0.2f, 0.4f);
            MeshBuilder m = cloth.Mesh;
            m.Cylinder(new Vector3(0f, -0.6f, 0f), new Vector3(0f, 0.55f, 0f), 0.5f, 0.56f, 20);
            m.Cylinder(new Vector3(0f, 0.55f, 0f), new Vector3(0f, 0.7f, 0f), 0.56f, 0.3f, 20, false, false);
            trim.Mesh.Box(new Vector3(0f, -0.02f, 0.5f), new Vector3(0.06f, 1.2f, 0.06f));
            trim.Mesh.Cylinder(new Vector3(0f, 0.66f, 0f), new Vector3(0f, 0.8f, 0f), 0.3f, 0.28f, 16, false, false);
            if (kind >= 2)
            {
                foreach (float s in new[] { -1f, 1f })
                {
                    m.Cylinder(new Vector3(s * 0.55f, 0.5f, 0f), new Vector3(s * 0.75f, -0.45f, 0.05f), 0.2f, 0.16f, 14);
                    trim.Mesh.Cylinder(new Vector3(s * 0.75f, -0.45f, 0.05f), new Vector3(s * 0.77f, -0.55f, 0.05f), 0.17f, 0.17f, 14);
                }
            }
            if (kind >= 3)
                m.Cylinder(new Vector3(0f, -0.6f, 0f), new Vector3(0f, -1.6f, 0f), 0.5f, 0.62f, 20, false, true);
            return new List<Piece> { cloth, trim };
        }

        // ---- Back accessories

        private static void Back(int kind, MeshBuilder main, MeshBuilder trim, MeshBuilder glow)
        {
            switch (kind)
            {
                case 1: // Cape: a clasp bar across the shoulders, a curved cloth flaring down, a gold hem
                    {
                        const int columns = 8, rows = 6;
                        Vector3 At(float u, float v)
                        {
                            // u -1..1 across, v 0 (shoulders) .. 1 (hem)
                            float width = Mathf.Lerp(1.5f, 2.6f, v);
                            float x = u * width;
                            float y = Mathf.Lerp(1.7f, -3.3f, v);
                            float z = -0.12f - 0.55f * v * v - 0.25f * (1f - u * u) * v + 0.08f * Mathf.Sin(u * 6f) * v;
                            return new Vector3(x, y, z);
                        }
                        for (int i = 0; i < columns; i++)
                        {
                            for (int j = 0; j < rows; j++)
                            {
                                float u0 = Mathf.Lerp(-1f, 1f, i / (float)columns), u1 = Mathf.Lerp(-1f, 1f, (i + 1) / (float)columns);
                                float v0 = j / (float)rows, v1 = (j + 1) / (float)rows;
                                Vector3 a = At(u0, v0), b = At(u1, v0), c = At(u0, v1), d = At(u1, v1);
                                Vector3 t = Vector3.forward * 0.05f;
                                main.Block(new[] { a, b, c, d, a + t, b + t, c + t, d + t });
                            }
                        }
                        trim.Box(new Vector3(0f, 1.72f, -0.1f), new Vector3(3.1f, 0.14f, 0.12f));
                        foreach (float s in new[] { -1f, 1f })
                            trim.Cylinder(new Vector3(s * 1.55f, 1.72f, -0.1f), new Vector3(s * 1.55f, 1.72f, 0.05f), 0.16f, 0.16f, 16);
                        for (int i = 0; i < columns; i++)
                        {
                            Vector3 a = At(Mathf.Lerp(-1f, 1f, i / (float)columns), 1f), b = At(Mathf.Lerp(-1f, 1f, (i + 1) / (float)columns), 1f);
                            trim.Box((a + b) * 0.5f + Vector3.up * 0.06f, new Vector3((b - a).magnitude + 0.02f, 0.14f, 0.09f), (b - a).normalized, Vector3.up, Vector3.Cross((b - a).normalized, Vector3.up));
                        }
                        break;
                    }
                case 2: // Jetpack: two tanks on a frame, nozzles under them with a blue light inside
                    foreach (float s in new[] { -1f, 1f })
                    {
                        var x = new Vector3(s * 0.55f, 0f, -0.55f);
                        main.Cylinder(x + Vector3.down * 0.9f, x + Vector3.up * 0.9f, 0.42f, 0.42f, 24, false, false);
                        Dome(main, x + Vector3.up * 0.9f, 0.42f, 0.32f);
                        Dome(main, x + Vector3.down * 0.9f, 0.42f, -0.32f);
                        trim.Cylinder(x + Vector3.down * 1.15f, x + Vector3.down * 1.55f, 0.18f, 0.3f, 20, true, false);
                        glow.Cylinder(x + Vector3.down * 1.5f, x + Vector3.down * 1.54f, 0.26f, 0.26f, 20);
                        foreach (float y in new[] { -0.5f, 0.5f })
                            trim.Cylinder(x + Vector3.up * (y - 0.06f), x + Vector3.up * (y + 0.06f), 0.44f, 0.44f, 24, false, false);
                    }
                    trim.Box(new Vector3(0f, 0.3f, -0.25f), new Vector3(0.8f, 1.4f, 0.3f));
                    glow.Box(new Vector3(0f, 0.5f, -0.41f), new Vector3(0.3f, 0.5f, 0.02f));
                    break;
                case 3: // Wings: feathers fanned out from the shoulder blades
                    foreach (float s in new[] { -1f, 1f })
                    {
                        var root = new Vector3(s * 0.35f, 0.9f, -0.25f);
                        for (int i = 0; i < 7; i++)
                        {
                            float angle = Mathf.Lerp(25f, -55f, i / 6f) * Mathf.Deg2Rad;
                            float length = Mathf.Lerp(3.4f, 2.0f, i / 6f);
                            Vector3 along = new Vector3(s * Mathf.Cos(angle), Mathf.Sin(angle), -0.25f).normalized;
                            Vector3 side = Vector3.Cross(along, Vector3.forward).normalized;
                            Vector3 center = root + along * (length * 0.5f) + Vector3.back * (i * 0.03f);
                            (i % 2 == 0 ? main : trim).Box(center, new Vector3(length, 0.42f, 0.06f), along, side, Vector3.Cross(along, side));
                        }
                        trim.Cylinder(root, root + new Vector3(s * 1.2f, 0.5f, -0.2f), 0.14f, 0.08f, 12);
                    }
                    break;
                case 4: // Antenna: a mast with rings on a small box, a blinking red tip
                    main.Box(new Vector3(0.6f, 1.0f, -0.25f), new Vector3(0.5f, 0.4f, 0.4f));
                    trim.Cylinder(new Vector3(0.6f, 1.2f, -0.25f), new Vector3(0.6f, 4.6f, -0.25f), 0.07f, 0.035f, 10);
                    for (int i = 0; i < 4; i++)
                        trim.Cylinder(new Vector3(0.6f, 1.8f + i * 0.7f, -0.25f), new Vector3(0.6f, 1.86f + i * 0.7f, -0.25f), 0.16f - i * 0.025f, 0.16f - i * 0.025f, 12);
                    glow.Cylinder(new Vector3(0.6f, 4.55f, -0.25f), new Vector3(0.6f, 4.8f, -0.25f), 0.12f, 0.12f, 12);
                    break;
            }
        }

        // A rounded cap on a cylinder end (height > 0 up, < 0 down)
        private static void Dome(MeshBuilder m, Vector3 at, float radius, float height)
        {
            const int steps = 4;
            for (int i = 0; i < steps; i++)
            {
                float a0 = i / (float)steps * Mathf.PI * 0.5f, a1 = (i + 1) / (float)steps * Mathf.PI * 0.5f;
                m.Cylinder(at + Vector3.up * height * Mathf.Sin(a0), at + Vector3.up * height * Mathf.Sin(a1), radius * Mathf.Cos(a0), Mathf.Max(0.001f, radius * Mathf.Cos(a1)), 24, false, i == steps - 1);
            }
        }

        // ---- Hats, for a helmet of radius 1 whose top is the origin

        // main: the hat's own color; band: its darker band; extra: details (gold, horns, jewels...)
        private static void Hat(int kind, MeshBuilder m, MeshBuilder band, MeshBuilder extra)
        {
            switch (kind)
            {
                case 1: // Cap: a soft crown with seams, a curved peak forward, a button, a back strap
                    Dome(m, -0.48f, 0.08f, 0.97f, 0.6f, 8);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * Mathf.PI / 3f;
                        Vector3 low = new Vector3(Mathf.Sin(a) * 0.98f, -0.44f, Mathf.Cos(a) * 0.98f), high = new Vector3(Mathf.Sin(a) * 0.15f, 0.12f, Mathf.Cos(a) * 0.15f);
                        band.Cylinder(low, Vector3.Lerp(low, high, 0.5f) + Vector3.up * 0.12f, 0.018f, 0.018f, 6);
                    }
                    for (int i = 0; i < 9; i++)
                    {
                        float a0 = Mathf.Lerp(-0.8f, 0.8f, i / 9f), a1 = Mathf.Lerp(-0.8f, 0.8f, (i + 1) / 9f);
                        float d0 = 1f - Mathf.Abs(a0) * 0.25f, d1 = 1f - Mathf.Abs(a1) * 0.25f;
                        Vector3 p0 = new Vector3(Mathf.Sin(a0) * 0.95f, -0.44f, Mathf.Cos(a0) * 0.95f), p1 = new Vector3(Mathf.Sin(a1) * 0.95f, -0.44f, Mathf.Cos(a1) * 0.95f);
                        Vector3 q0 = new Vector3(Mathf.Sin(a0) * 1.6f * d0, -0.58f, Mathf.Cos(a0) * 1.6f * d0), q1 = new Vector3(Mathf.Sin(a1) * 1.6f * d1, -0.58f, Mathf.Cos(a1) * 1.6f * d1);
                        Vector3 t = Vector3.up * 0.04f;
                        band.Block(new[] { p0, p1, p0 + t, p1 + t, q0, q1, q0 + t, q1 + t });
                    }
                    band.Cylinder(Vector3.up * 0.07f, Vector3.up * 0.14f, 0.1f, 0.08f, 12);
                    extra.Box(new Vector3(0f, -0.4f, -0.96f), new Vector3(0.22f, 0.08f, 0.04f));
                    break;
                case 2: // Cowboy: a brim curling up at the sides, a pinched crown, a band with a buckle
                    for (int i = 0; i < 32; i++)
                    {
                        float a0 = i * Mathf.PI / 16f, a1 = (i + 1) * Mathf.PI / 16f;
                        float Lift(float a) => 0.32f * Mathf.Pow(Mathf.Abs(Mathf.Sin(a)), 3f);
                        Vector3 p0 = new Vector3(Mathf.Sin(a0) * 0.92f, -0.42f, Mathf.Cos(a0) * 0.92f), p1 = new Vector3(Mathf.Sin(a1) * 0.92f, -0.42f, Mathf.Cos(a1) * 0.92f);
                        Vector3 q0 = new Vector3(Mathf.Sin(a0) * 1.95f, -0.42f + Lift(a0), Mathf.Cos(a0) * 1.95f), q1 = new Vector3(Mathf.Sin(a1) * 1.95f, -0.42f + Lift(a1), Mathf.Cos(a1) * 1.95f);
                        Vector3 t = Vector3.up * 0.05f;
                        m.Block(new[] { p0, p1, p0 + t, p1 + t, q0, q1, q0 + t, q1 + t });
                    }
                    m.Cylinder(Vector3.up * -0.4f, Vector3.up * 0.42f, 0.95f, 0.84f, 28);
                    m.Box(Vector3.up * 0.44f, new Vector3(0.3f, 0.14f, 1.2f));
                    foreach (float s in new[] { -1f, 1f })
                        m.Box(new Vector3(s * 0.45f, 0.4f, 0.55f), new Vector3(0.2f, 0.12f, 0.3f), Quaternion.Euler(0f, s * 30f, 0f));
                    band.Cylinder(Vector3.up * -0.36f, Vector3.up * -0.2f, 0.97f, 0.95f, 28, false, false);
                    extra.Box(new Vector3(0f, -0.28f, 0.97f), new Vector3(0.18f, 0.14f, 0.04f));
                    break;
                case 3: // Top hat: a tall crown on a curled brim, a ribbon with a bow
                    m.Cylinder(Vector3.up * -0.4f, Vector3.up * -0.34f, 1.42f, 1.4f, 32);
                    m.Cylinder(Vector3.up * -0.4f, Vector3.up * -0.32f, 1.45f, 1.45f, 32, false, false);
                    m.Cylinder(Vector3.up * -0.38f, Vector3.up * 1.15f, 0.9f, 0.98f, 28);
                    band.Cylinder(Vector3.up * -0.34f, Vector3.up * -0.08f, 0.93f, 0.94f, 28, false, false);
                    band.Box(new Vector3(0.9f, -0.21f, 0.25f), new Vector3(0.1f, 0.22f, 0.3f), Quaternion.Euler(0f, 15f, 0f));
                    break;
                case 4: // Beanie: a ribbed turned-up rim, a soft dome, a pompom
                    m.Cylinder(Vector3.up * -0.6f, Vector3.up * -0.34f, 1.04f, 1.0f, 28);
                    for (int i = 0; i < 24; i++)
                    {
                        float a = i * Mathf.PI / 12f;
                        band.Box(new Vector3(Mathf.Sin(a) * 1.04f, -0.47f, Mathf.Cos(a) * 1.04f), new Vector3(0.06f, 0.24f, 0.04f), Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f));
                    }
                    Dome(m, -0.34f, 0.2f, 0.98f, 0f, 8);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * Mathf.PI / 3f;
                        extra.Cylinder(Vector3.up * 0.2f, new Vector3(Mathf.Sin(a) * 0.18f, 0.4f, Mathf.Cos(a) * 0.18f), 0.1f, 0.12f, 8);
                    }
                    break;
                case 5: // Crown: a jewelled ring of points over a velvet cap
                    m.Cylinder(Vector3.up * -0.32f, Vector3.up * 0.02f, 0.86f, 0.9f, 32, false, false);
                    m.Cylinder(Vector3.up * -0.34f, Vector3.up * -0.28f, 0.9f, 0.9f, 32, false, false);
                    Dome(band, -0.3f, 0.2f, 0.84f, 0.1f, 6);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4f;
                        Vector3 at = new Vector3(Mathf.Cos(a) * 0.88f, 0.02f, Mathf.Sin(a) * 0.88f);
                        m.Cylinder(at, at + Vector3.up * 0.42f, 0.15f, 0f, 6);
                        m.Cylinder(at + Vector3.up * 0.42f, at + Vector3.up * 0.48f, 0.05f, 0.05f, 8);
                        extra.Box(new Vector3(Mathf.Cos(a + 0.39f) * 0.91f, -0.15f, Mathf.Sin(a + 0.39f) * 0.91f), Vector3.one * 0.11f, Quaternion.Euler(45f, (a + 0.39f) * Mathf.Rad2Deg, 45f));
                    }
                    extra.Box(Vector3.up * 0.3f, Vector3.one * 0.12f, Quaternion.Euler(45f, 0f, 45f));
                    break;
                case 6: // Beret: a soft flat disc leaning to one side, a thin band, a little stalk
                    {
                        Quaternion lean = Quaternion.Euler(0f, 0f, -14f);
                        Vector3 L(float x, float y, float z) => lean * new Vector3(x, y, z) + Vector3.up * -0.05f;
                        band.Cylinder(L(0f, -0.36f, 0f), L(0f, -0.26f, 0f), 0.97f, 0.97f, 28, false, false);
                        m.Cylinder(L(0f, -0.26f, 0f), L(0f, -0.08f, 0f), 0.98f, 1.22f, 32, false, false);
                        m.Cylinder(L(0f, -0.08f, 0f), L(0f, 0.06f, 0f), 1.22f, 1.12f, 32, false, false);
                        m.Cylinder(L(0f, 0.06f, 0f), L(0f, 0.14f, 0f), 1.12f, 0.6f, 32, false, true);
                        band.Cylinder(L(0f, 0.13f, 0f), L(0f, 0.25f, 0f), 0.05f, 0.03f, 8);
                        break;
                    }
                case 7: // Viking helmet: a steel dome with a ridge and riveted rim, a nose guard, curved horns
                    Dome(m, -0.46f, 0.18f, 1.03f, 0f, 8);
                    band.Cylinder(Vector3.up * -0.5f, Vector3.up * -0.34f, 1.06f, 1.05f, 28, false, false);
                    band.Box(new Vector3(0f, -0.05f, 0f), new Vector3(0.12f, 0.5f, 2.05f));
                    band.Box(new Vector3(0f, -0.62f, 1.04f), new Vector3(0.14f, 0.36f, 0.06f));
                    for (int i = 0; i < 16; i++)
                    {
                        float a = i * Mathf.PI / 8f;
                        extra.Cylinder(new Vector3(Mathf.Sin(a) * 1.07f, -0.42f, Mathf.Cos(a) * 1.07f), new Vector3(Mathf.Sin(a) * 1.1f, -0.42f, Mathf.Cos(a) * 1.1f), 0.035f, 0.03f, 6);
                    }
                    foreach (float s in new[] { -1f, 1f })
                    {
                        const int segments = 8;
                        Vector3 Horn(float t) => new Vector3(s * (0.9f + 0.75f * t), -0.15f + 0.95f * t * t, 0.15f * t);
                        for (int i = 0; i < segments; i++)
                        {
                            float t0 = i / (float)segments, t1 = (i + 1) / (float)segments;
                            extra.Cylinder(Horn(t0), Horn(t1), Mathf.Lerp(0.2f, 0.02f, t0), Mathf.Lerp(0.2f, 0.02f, t1), 10, i == 0, i == segments - 1);
                        }
                    }
                    break;
                case 8: // Pirate tricorne: a crown and three upturned brim sides edged with gold
                    m.Cylinder(Vector3.up * -0.4f, Vector3.up * 0.3f, 0.96f, 0.86f, 28);
                    for (int i = 0; i < 3; i++)
                    {
                        float a = (i * 120f + 60f) * Mathf.Deg2Rad;
                        Vector3 outward = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                        Vector3 across = Vector3.Cross(Vector3.up, outward);
                        Vector3 foot = outward * 1.05f + Vector3.up * -0.38f;
                        Vector3 top = outward * 1.3f + Vector3.up * 0.25f;
                        float w = 1.9f;
                        m.Block(new[]
                        {
                            foot - across * w * 0.5f, foot + across * w * 0.5f, top - across * w * 0.42f, top + across * w * 0.42f,
                            foot - across * w * 0.5f + outward * 0.06f, foot + across * w * 0.5f + outward * 0.06f, top - across * w * 0.42f + outward * 0.06f, top + across * w * 0.42f + outward * 0.06f
                        });
                        band.Box(top + outward * 0.03f + Vector3.up * 0.03f, new Vector3(w * 0.86f, 0.06f, 0.1f), across, Vector3.up, outward);
                    }
                    extra.Cylinder(new Vector3(0f, 0.05f, 1.25f), new Vector3(0f, 0.05f, 1.32f), 0.16f, 0.16f, 12);
                    break;
                case 9: // Headphones: a padded band over the top, big ear cups with a lit ring
                    for (int i = 0; i < 14; i++)
                    {
                        float a0 = Mathf.Lerp(-1.45f, 1.45f, i / 14f), a1 = Mathf.Lerp(-1.45f, 1.45f, (i + 1) / 14f);
                        Vector3 p0 = new Vector3(Mathf.Sin(a0) * 1.08f, Mathf.Cos(a0) * 1.08f - 1.08f + 0.04f, 0f), p1 = new Vector3(Mathf.Sin(a1) * 1.08f, Mathf.Cos(a1) * 1.08f - 1.08f + 0.04f, 0f);
                        Vector3 along = (p1 - p0).normalized, outward = Vector3.Cross(Vector3.forward, along);
                        m.Box((p0 + p1) * 0.5f, new Vector3((p1 - p0).magnitude + 0.02f, 0.1f, 0.34f), along, outward, Vector3.forward);
                        band.Box((p0 + p1) * 0.5f - outward * 0.07f, new Vector3((p1 - p0).magnitude + 0.02f, 0.06f, 0.26f), along, outward, Vector3.forward);
                    }
                    foreach (float s in new[] { -1f, 1f })
                    {
                        var cup = new Vector3(s * 1.1f, -0.95f, 0f);
                        m.Cylinder(cup, cup + Vector3.right * s * 0.28f, 0.42f, 0.38f, 28);
                        extra.Cylinder(cup - Vector3.right * s * 0.1f, cup, 0.44f, 0.44f, 28);
                        band.Cylinder(cup + Vector3.right * s * 0.28f, cup + Vector3.right * s * 0.31f, 0.3f, 0.3f, 24, false, true);
                        m.Box(cup + Vector3.up * 0.42f + Vector3.right * s * 0.1f, new Vector3(0.12f, 0.3f, 0.12f));
                    }
                    break;
                case 10: // Halo: a glowing ring floating over the head
                    for (int i = 0; i < 32; i++)
                    {
                        float a0 = i * Mathf.PI / 16f, a1 = (i + 1) * Mathf.PI / 16f;
                        Vector3 p0 = new Vector3(Mathf.Sin(a0) * 0.72f, 0.42f, Mathf.Cos(a0) * 0.72f), p1 = new Vector3(Mathf.Sin(a1) * 0.72f, 0.42f, Mathf.Cos(a1) * 0.72f);
                        m.Cylinder(p0, p1, 0.07f, 0.07f, 8);
                    }
                    break;
            }
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

        private static void Glasses(int kind, MeshBuilder frame, MeshBuilder lens)
        {
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
                        lens.Cylinder(new Vector3(s * 0.32f, 0f, -0.02f), new Vector3(s * 0.32f, 0f, 0.02f), 0.24f, 0.24f, 20);
                        lens.Box(new Vector3(s * 0.35f, -0.15f, 0f), new Vector3(0.34f, 0.14f, 0.04f), Quaternion.Euler(0f, 0f, s * 20f));
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
                case 5: // Ski goggles: the same band, taller, in a thick frame with a strap around the helmet
                    {
                        float tall = kind == 5 ? 0.46f : 0.3f;
                        for (int i = 0; i < 10; i++)
                        {
                            float a0 = Mathf.Lerp(-0.8f, 0.8f, i / 10f), a1 = Mathf.Lerp(-0.8f, 0.8f, (i + 1) / 10f);
                            Vector3 p0 = new Vector3(Mathf.Sin(a0), 0f, (Mathf.Cos(a0) - 1f) * 0.9f), p1 = new Vector3(Mathf.Sin(a1), 0f, (Mathf.Cos(a1) - 1f) * 0.9f);
                            Vector3 mid = (p0 + p1) * 0.5f, along = (p1 - p0).normalized, outward = Vector3.Cross(along, Vector3.up).normalized;
                            if (outward.z < 0f)
                                outward = -outward;
                            lens.Box(mid, new Vector3((p1 - p0).magnitude + 0.01f, tall, 0.04f), along, Vector3.up, outward);
                            float edge = kind == 5 ? 0.1f : 0.05f;
                            foreach (float y in new[] { -tall * 0.5f, tall * 0.5f })
                                frame.Box(mid + outward * 0.01f + Vector3.up * y, new Vector3((p1 - p0).magnitude + 0.02f, edge, kind == 5 ? 0.12f : 0.06f), along, Vector3.up, outward);
                        }
                        foreach (float s in new[] { -1f, 1f })
                            frame.Box(new Vector3(s * 0.74f, 0f, -0.32f), new Vector3(0.09f, tall * 0.75f, 0.16f));
                        if (kind == 5)
                        {
                            // The strap: an arc behind, around the helmet
                            for (int i = 0; i < 16; i++)
                            {
                                float a0 = Mathf.Lerp(0.75f, Mathf.PI * 2f - 0.75f, i / 16f), a1 = Mathf.Lerp(0.75f, Mathf.PI * 2f - 0.75f, (i + 1) / 16f);
                                Vector3 c = new Vector3(0f, 0f, -1.05f);
                                Vector3 p0 = c + new Vector3(Mathf.Sin(a0) * 1.12f, 0f, Mathf.Cos(a0) * 1.12f), p1 = c + new Vector3(Mathf.Sin(a1) * 1.12f, 0f, Mathf.Cos(a1) * 1.12f);
                                Vector3 along = (p1 - p0).normalized;
                                frame.Box((p0 + p1) * 0.5f, new Vector3((p1 - p0).magnitude + 0.02f, 0.2f, 0.04f), along, Vector3.up, Vector3.Cross(along, Vector3.up));
                            }
                        }
                        break;
                    }
                case 4: // Round glasses: two thin round rims, a curved bridge, arms
                    foreach (float s in new[] { -1f, 1f })
                    {
                        var c = new Vector3(s * 0.33f, 0f, 0f);
                        lens.Cylinder(c + Vector3.back * 0.02f, c + Vector3.forward * 0.02f, 0.25f, 0.25f, 24);
                        for (int i = 0; i < 20; i++)
                        {
                            float a = i * Mathf.PI / 10f;
                            frame.Box(c + new Vector3(Mathf.Cos(a) * 0.27f, Mathf.Sin(a) * 0.27f, 0.01f), new Vector3(0.09f, 0.04f, 0.05f), Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg + 90f));
                        }
                        frame.Box(new Vector3(s * 0.62f, 0.05f, -0.3f), new Vector3(0.045f, 0.045f, 0.6f));
                    }
                    for (int i = 0; i < 4; i++)
                    {
                        float a0 = Mathf.Lerp(0.5f, 2.64f, i / 4f), a1 = Mathf.Lerp(0.5f, 2.64f, (i + 1) / 4f);
                        Vector3 p0 = new Vector3(Mathf.Cos(a0) * 0.1f, Mathf.Sin(a0) * 0.08f, 0.01f), p1 = new Vector3(Mathf.Cos(a1) * 0.1f, Mathf.Sin(a1) * 0.08f, 0.01f);
                        frame.Box((p0 + p1) * 0.5f, new Vector3((p1 - p0).magnitude + 0.02f, 0.04f, 0.05f), Quaternion.FromToRotation(Vector3.right, p1 - p0));
                    }
                    break;
            }
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

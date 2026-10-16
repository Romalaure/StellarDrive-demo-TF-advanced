using System.Collections.Generic;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using UnityEngine;
using UnityEngine.Rendering;

namespace StellarDriveDemoTFAdvanced.TFItems.Devices
{
    /// <summary>
    /// The teleport capsule's model: a skirted pad with a rotating floor emitter, three profiled
    /// pillars, a curved back wall with lit slits and conduits, a roof with an emitter crown, a
    /// console on the right pillar and two energy rings. The pieces named in
    /// <see cref="CapsuleAnimation"/> move in play.
    /// </summary>
    internal static class CapsuleModel
    {
        public const float Radius = 0.6f, Height = 2.25f;
        public const string Frame = "TF_Capsule_Frame", Base = "TF_NoPaint_CapsuleBase", Trim = "TF_NoPaint_CapsuleTrim",
            Chrome = "TF_NoPaint_CapsuleChrome", Glow = "TF_NoPaint_CapsuleGlow", Screen = "TF_NoPaint_CapsuleScreen",
            RingA = "TF_NoPaint_CapsuleRingA", RingB = "TF_NoPaint_CapsuleRingB", Emitter = "TF_NoPaint_CapsuleEmitter",
            Crown = "TF_NoPaint_CapsuleCrown";

        // Pillars at the back and both sides; the front (-z, 270°) stays open as the door
        private static readonly float[] Pillars = { 90f, 210f, 330f };

        public static Dictionary<string, DeviceMaterial> Materials() => new Dictionary<string, DeviceMaterial>
        {
            [Frame] = new DeviceMaterial(new Color(0.86f, 0.88f, 0.91f), 0.5f, 0.7f),
            [Base] = new DeviceMaterial(new Color(0.14f, 0.15f, 0.18f), 0.7f, 0.5f),
            [Trim] = new DeviceMaterial(new Color(0.95f, 0.45f, 0.1f), 0.3f, 0.6f),
            [Chrome] = new DeviceMaterial(new Color(0.78f, 0.8f, 0.84f), 1f, 0.85f),
            [Glow] = new DeviceMaterial(new Color(0.4f, 0.9f, 1f), 0f, 0.8f, new Color(0.3f, 0.85f, 1f) * 2.2f),
            [Screen] = new DeviceMaterial(new Color(0.1f, 0.35f, 0.5f), 0f, 0.9f, new Color(0.2f, 0.7f, 1f) * 1.6f),
            [RingA] = new DeviceMaterial(new Color(0.4f, 0.9f, 1f), 0f, 0.8f, new Color(0.3f, 0.85f, 1f) * 5f),
            [RingB] = new DeviceMaterial(new Color(0.4f, 0.9f, 1f), 0f, 0.8f, new Color(0.3f, 0.85f, 1f) * 5f),
            [Emitter] = new DeviceMaterial(new Color(0.4f, 0.9f, 1f), 0f, 0.8f, new Color(0.3f, 0.85f, 1f) * 1.8f),
            [Crown] = new DeviceMaterial(new Color(0.4f, 0.9f, 1f), 0f, 0.8f, new Color(0.3f, 0.85f, 1f) * 1.8f)
        };

        private static Vector3 Around(float degrees, float radius, float y)
        {
            float a = degrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius);
        }

        // A ring of tube around the vertical axis, from one angle to another
        private static void Arc(MeshBuilder mesh, float radius, float y, float tube, float from, float to, int segments)
        {
            for (int i = 0; i < segments; i++)
            {
                float a0 = from + (to - from) * i / segments, a1 = from + (to - from) * (i + 1) / segments;
                mesh.Cylinder(Around(a0, radius, y), Around(a1, radius, y), tube, tube, 8);
            }
        }

        // A flat box standing on the circle at an angle, facing the axis
        private static void Panel(MeshBuilder mesh, float degrees, float radius, float y, Vector3 size)
        {
            mesh.Box(Around(degrees, radius, y), size, Quaternion.Euler(0f, 90f - degrees, 0f));
        }

        public static Dictionary<string, MeshBuilder> Shapes()
        {
            var frame = new MeshBuilder();
            var dark = new MeshBuilder();
            var trim = new MeshBuilder();
            var chrome = new MeshBuilder();
            var glow = new MeshBuilder();
            var screen = new MeshBuilder();
            var ringA = new MeshBuilder();
            var ringB = new MeshBuilder();
            var emitter = new MeshBuilder();
            var crown = new MeshBuilder();
            const float r = Radius, h = Height;

            // ---- Pad: a tapered painted skirt, an orange band, a dark deck and a glowing edge
            frame.Cylinder(Vector3.zero, new Vector3(0f, 0.1f, 0f), r + 0.04f, r, 40);
            trim.Cylinder(new Vector3(0f, 0.1f, 0f), new Vector3(0f, 0.12f, 0f), r + 0.005f, r + 0.005f, 40, false, false);
            dark.Cylinder(new Vector3(0f, 0.1f, 0f), new Vector3(0f, 0.13f, 0f), r - 0.02f, r - 0.04f, 40, false, true);
            glow.Cylinder(new Vector3(0f, 0.13f, 0f), new Vector3(0f, 0.137f, 0f), r - 0.07f, r - 0.07f, 40, false, true);
            dark.Cylinder(new Vector3(0f, 0.137f, 0f), new Vector3(0f, 0.14f, 0f), r - 0.1f, r - 0.1f, 40, false, true);
            // Vents in the skirt and a step at the door
            for (int i = 0; i < 10; i++)
            {
                float degrees = 300f + i * 30f;
                if (degrees % 360f > 235f && degrees % 360f < 305f)
                    continue;
                Panel(dark, degrees, r + 0.022f, 0.05f, new Vector3(0.12f, 0.035f, 0.01f));
            }
            dark.Box(new Vector3(0f, 0.035f, -r - 0.08f), new Vector3(0.62f, 0.07f, 0.18f));
            trim.Box(new Vector3(0f, 0.072f, -r - 0.165f), new Vector3(0.62f, 0.006f, 0.012f));
            glow.Box(new Vector3(0f, 0.072f, -r - 0.05f), new Vector3(0.5f, 0.004f, 0.02f));
            // Chrome hub in the middle of the deck; the emitter blades turn around it
            chrome.Cylinder(new Vector3(0f, 0.14f, 0f), new Vector3(0f, 0.16f, 0f), 0.11f, 0.08f, 24);
            glow.Cylinder(new Vector3(0f, 0.16f, 0f), new Vector3(0f, 0.165f, 0f), 0.05f, 0.05f, 24, false, true);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f;
                emitter.Box(Around(a + 15f, 0.29f, 0.145f), new Vector3(0.2f, 0.008f, 0.045f), Quaternion.Euler(0f, -(a + 15f), 0f));
                emitter.Box(Around(a + 30f, 0.4f, 0.145f), new Vector3(0.06f, 0.008f, 0.06f), Quaternion.Euler(0f, -(a + 30f), 0f));
            }

            // ---- Pillars: a painted profile with a dark inner face, a lit strip, chrome caps and orange bands
            foreach (float degrees in Pillars)
            {
                Quaternion facing = Quaternion.Euler(0f, 90f - degrees, 0f);
                Vector3 at = Around(degrees, r - 0.06f, 0f);
                Vector3 inward = -at.normalized;
                float length = h - 0.32f;
                frame.Box(at + Vector3.up * (0.14f + length / 2f), new Vector3(0.12f, length, 0.08f), facing);
                dark.Box(at + inward * 0.042f + Vector3.up * (0.14f + length / 2f), new Vector3(0.07f, length - 0.1f, 0.01f), facing);
                glow.Box(at + inward * 0.048f + Vector3.up * (0.14f + length / 2f), new Vector3(0.018f, length - 0.3f, 0.006f), facing);
                chrome.Box(at + Vector3.up * 0.17f, new Vector3(0.15f, 0.06f, 0.1f), facing);
                chrome.Box(at + Vector3.up * (h - 0.21f), new Vector3(0.15f, 0.06f, 0.1f), facing);
                foreach (float y in new[] { 0.62f, 1.62f })
                    trim.Box(at + Vector3.up * y, new Vector3(0.125f, 0.035f, 0.085f), facing);
                // Bolts on the outside
                for (int b = 0; b < 4; b++)
                    chrome.Cylinder(at - inward * 0.04f + Vector3.up * (0.4f + b * 0.45f), at - inward * 0.048f + Vector3.up * (0.4f + b * 0.45f), 0.012f, 0.012f, 8);
            }

            // ---- Back wall between the side pillars: curved panels with lit slits, two conduits
            for (int i = 0; i < 10; i++)
            {
                float degrees = 30f + 12f * i + 6f;
                if (Mathf.Abs(degrees - 90f) < 8f)
                    continue;
                Panel(frame, degrees, r - 0.04f, 1.05f, new Vector3(0.12f, 1.5f, 0.025f));
                Panel(dark, degrees, r - 0.056f, 1.05f, new Vector3(0.1f, 1.42f, 0.008f));
                Panel(glow, degrees, r - 0.062f, 1.05f + (i % 2 == 0 ? 0.35f : -0.35f), new Vector3(0.02f, 0.4f, 0.004f));
            }
            foreach (float degrees in new[] { 55f, 125f })
            {
                chrome.Cylinder(Around(degrees, r + 0.0f, 0.12f), Around(degrees, r + 0.0f, h - 0.16f), 0.025f, 0.025f, 10);
                for (int c = 0; c < 4; c++)
                    dark.Cylinder(Around(degrees, r, 0.4f + c * 0.45f), Around(degrees, r, 0.44f + c * 0.45f), 0.034f, 0.034f, 10);
            }

            // ---- Roof: painted shell, orange band, dark underside, glow ring and the emitter crown
            frame.Cylinder(new Vector3(0f, h - 0.16f, 0f), new Vector3(0f, h - 0.04f, 0f), r + 0.03f, r - 0.02f, 40);
            frame.Cylinder(new Vector3(0f, h - 0.04f, 0f), new Vector3(0f, h, 0f), r - 0.02f, r - 0.2f, 40, false, true);
            trim.Cylinder(new Vector3(0f, h - 0.12f, 0f), new Vector3(0f, h - 0.095f, 0f), r + 0.035f, r + 0.03f, 40, false, false);
            dark.Cylinder(new Vector3(0f, h - 0.17f, 0f), new Vector3(0f, h - 0.16f, 0f), r - 0.02f, r - 0.02f, 40, true, false);
            glow.Cylinder(new Vector3(0f, h - 0.176f, 0f), new Vector3(0f, h - 0.17f, 0f), r - 0.08f, r - 0.08f, 40, true, false);
            dark.Cylinder(new Vector3(0f, h - 0.182f, 0f), new Vector3(0f, h - 0.176f, 0f), r - 0.12f, r - 0.12f, 40, true, false);
            chrome.Cylinder(new Vector3(0f, h - 0.3f, 0f), new Vector3(0f, h - 0.18f, 0f), 0.06f, 0.16f, 24);
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f;
                crown.Box(Around(a, 0.24f, h - 0.2f), new Vector3(0.14f, 0.012f, 0.03f), Quaternion.Euler(0f, -a, 25f));
            }
            glow.Cylinder(new Vector3(0f, h - 0.33f, 0f), new Vector3(0f, h - 0.3f, 0f), 0.04f, 0.06f, 16);

            // ---- Console on the right pillar: angled dark panel, screen, three buttons and a handle
            Vector3 pillar = Around(330f, r - 0.06f, 0f), inwardC = -pillar.normalized;
            Quaternion tilt = Quaternion.Euler(0f, 90f - 330f, 0f) * Quaternion.Euler(-20f, 0f, 0f);
            Vector3 panel = pillar + inwardC * 0.1f + Vector3.up * 1.18f;
            dark.Box(panel, new Vector3(0.2f, 0.26f, 0.04f), tilt);
            chrome.Box(pillar + inwardC * 0.06f + Vector3.up * 1.05f, new Vector3(0.05f, 0.08f, 0.08f), tilt);
            screen.Box(panel + tilt * new Vector3(0f, 0.035f, -0.022f), new Vector3(0.16f, 0.12f, 0.004f), tilt);
            for (int b = 0; b < 3; b++)
                glow.Box(panel + tilt * new Vector3(-0.05f + b * 0.05f, -0.08f, -0.024f), new Vector3(0.03f, 0.022f, 0.008f), tilt);
            chrome.Box(Around(210f, r - 0.1f, 1.2f) + Vector3.up * 0f, new Vector3(0.02f, 0.3f, 0.02f), Quaternion.Euler(0f, 90f - 210f, 0f));

            // ---- Energy rings (moved up and down in play); built at height 0 around the axis
            Arc(ringA, r - 0.14f, 0f, 0.018f, 0f, 360f, 36);
            for (int i = 0; i < 6; i++)
                ringA.Box(Around(i * 60f, r - 0.14f, 0f), new Vector3(0.06f, 0.05f, 0.05f), Quaternion.Euler(0f, -i * 60f, 0f));
            Arc(ringB, r - 0.18f, 0f, 0.012f, 0f, 360f, 36);
            for (int i = 0; i < 4; i++)
                ringB.Box(Around(i * 90f + 45f, r - 0.18f, 0f), new Vector3(0.04f, 0.07f, 0.04f), Quaternion.Euler(0f, -(i * 90f + 45f), 0f));

            return new Dictionary<string, MeshBuilder>
            {
                [Frame] = frame,
                [Base] = dark,
                [Trim] = trim,
                [Chrome] = chrome,
                [Glow] = glow,
                [Screen] = screen,
                [RingA] = ringA,
                [RingB] = ringB,
                [Emitter] = emitter,
                [Crown] = crown
            };
        }
    }

    /// <summary>
    /// The capsule in play: the floor emitter and the roof crown turn, two energy rings sweep up
    /// and down the column, a see-through beam shimmers and sparks spiral up. All of it wakes up
    /// when someone stands inside and flares when the local player teleports.
    /// </summary>
    internal sealed class CapsuleAnimation : MonoBehaviour
    {
        private const int Sparks = 14;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
        private static readonly Color Cyan = new Color(0.3f, 0.85f, 1f);
        private static Texture2D _beamTexture;
        private static Mesh _beamMesh;

        private Transform _ringA, _ringB, _emitter, _crown;
        private Renderer _ringARenderer, _ringBRenderer, _beam;
        private readonly Transform[] _sparks = new Transform[Sparks];
        private MaterialPropertyBlock _block;
        private float _activity = 0.2f, _spin, _phase;

        /// <summary>Set by the teleport menu: every capsule near the player flares until then.</summary>
        public static float FlareUntil;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _phase = Random.value * 10f;
            _ringA = transform.Find(CapsuleModel.RingA);
            _ringB = transform.Find(CapsuleModel.RingB);
            _emitter = transform.Find(CapsuleModel.Emitter);
            _crown = transform.Find(CapsuleModel.Crown);
            _ringARenderer = _ringA != null ? _ringA.GetComponent<Renderer>() : null;
            _ringBRenderer = _ringB != null ? _ringB.GetComponent<Renderer>() : null;
            // Crown and emitter turn around the axis: their meshes are built around it
            if (_crown != null)
                _crown.localPosition = Vector3.zero;
            BuildBeam();
            BuildSparks();
        }

        private void BuildBeam()
        {
            Material material = StellarDriveDemoTFAdvanced.Common.TransparentMaterials.Additive("TF_CapsuleBeam", BeamTexture());
            if (material == null)
                return;
            if (_beamMesh == null)
            {
                var mesh = new MeshBuilder();
                mesh.Cylinder(new Vector3(0f, 0.15f, 0f), new Vector3(0f, CapsuleModel.Height - 0.3f, 0f), 0.4f, 0.36f, 32, false, false);
                _beamMesh = mesh.Build("TF_CapsuleBeam");
            }
            var beam = new GameObject("TF_NoPaint_CapsuleBeam") { layer = gameObject.layer };
            beam.transform.SetParent(transform, false);
            beam.AddComponent<MeshFilter>().sharedMesh = _beamMesh;
            _beam = beam.AddComponent<MeshRenderer>();
            _beam.sharedMaterial = material;
            _beam.shadowCastingMode = ShadowCastingMode.Off;
            _beam.receiveShadows = false;
        }

        private void BuildSparks()
        {
            Transform glow = transform.Find(CapsuleModel.Glow);
            Material material = glow != null && glow.GetComponent<Renderer>() != null ? glow.GetComponent<Renderer>().sharedMaterial : null;
            if (material == null)
                return;
            Mesh cube = SparkMesh();
            for (int i = 0; i < Sparks; i++)
            {
                var spark = new GameObject("TF_NoPaint_CapsuleSpark") { layer = gameObject.layer };
                spark.transform.SetParent(transform, false);
                spark.AddComponent<MeshFilter>().sharedMesh = cube;
                var renderer = spark.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                _sparks[i] = spark.transform;
            }
        }

        private static Mesh _sparkMesh;

        private static Mesh SparkMesh()
        {
            if (_sparkMesh == null)
            {
                var mesh = new MeshBuilder();
                mesh.Box(Vector3.zero, new Vector3(0.018f, 0.018f, 0.018f));
                _sparkMesh = mesh.Build("TF_CapsuleSpark");
            }
            return _sparkMesh;
        }

        // Vertical streaks fading out at both ends
        private static Texture2D BeamTexture()
        {
            if (_beamTexture != null)
                return _beamTexture;
            const int width = 64, height = 128;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, true) { name = "TF_CapsuleBeam", wrapMode = TextureWrapMode.Repeat };
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = y / (float)(height - 1);
                float ends = Mathf.SmoothStep(0f, 1f, Mathf.Min(v, 1f - v) * 5f);
                for (int x = 0; x < width; x++)
                {
                    float u = x / (float)width;
                    float streak = Mathf.Pow(Mathf.PerlinNoise(u * 18f, v * 1.5f), 3f) * 1.6f + 0.15f * Mathf.PerlinNoise(u * 60f, v * 8f);
                    float a = Mathf.Clamp01(streak) * ends * 0.55f;
                    pixels[y * width + x] = new Color(0.55f, 0.92f, 1f, a);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(true);
            return _beamTexture = texture;
        }

        private void Update()
        {
            Camera camera = MainCamera.Get();
            if (camera == null)
                return;
            Vector3 local = transform.InverseTransformPoint(camera.transform.position);
            float distance = local.magnitude;
            if (distance > 45f)
                return;
            // Someone (the local player) inside: wake up; flare after a teleport
            bool inside = new Vector2(local.x, local.z).magnitude < CapsuleModel.Radius && local.y > -0.2f && local.y < CapsuleModel.Height + 0.2f;
            float target = Time.unscaledTime < FlareUntil && distance < 6f ? 2.2f : inside || StellarDriveDemoTFAdvanced.Common.DevHarness.Off("capsuleidle") ? 1f : 0.2f;
            _activity = Mathf.MoveTowards(_activity, target, Time.deltaTime * (target > _activity ? 3f : 0.8f));
            float t = Time.time + _phase;
            _spin += Time.deltaTime * (20f + 160f * _activity);

            if (_emitter != null)
                _emitter.localRotation = Quaternion.Euler(0f, _spin, 0f);
            if (_crown != null)
                _crown.localRotation = Quaternion.Euler(0f, -_spin * 0.7f, 0f);
            float speed = 0.5f + 1.3f * _activity, low = 0.3f, high = CapsuleModel.Height - 0.45f;
            if (_ringA != null)
            {
                _ringA.localPosition = Vector3.up * Mathf.Lerp(low, high, 0.5f + 0.5f * Mathf.Sin(t * speed));
                _ringA.localRotation = Quaternion.Euler(0f, _spin * 1.4f, 0f);
            }
            if (_ringB != null)
            {
                _ringB.localPosition = Vector3.up * Mathf.Lerp(low, high, 0.5f + 0.5f * Mathf.Sin(t * speed + Mathf.PI));
                _ringB.localRotation = Quaternion.Euler(0f, -_spin * 1.9f, 0f);
            }
            float pulse = 0.8f + 0.2f * Mathf.Sin(t * 5f);
            // Bright enough to glow, not so much that the cyan burns out to white
            Tint(_ringARenderer, Cyan * (0.8f + 1.4f * _activity) * pulse);
            Tint(_ringBRenderer, Cyan * (0.8f + 1.4f * _activity) * (1.8f - pulse));
            if (_beam != null)
            {
                _beam.enabled = _activity > 0.05f;
                _beam.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, new Color(1f, 1f, 1f, Mathf.Clamp01(0.25f + 0.55f * _activity) * pulse));
                _block.SetVector(BaseMapStId, new Vector4(2f, 1f, t * 0.05f, -t * (0.2f + 0.6f * _activity)));
                _beam.SetPropertyBlock(_block);
            }
            for (int i = 0; i < Sparks; i++)
            {
                Transform spark = _sparks[i];
                if (spark == null)
                    continue;
                // Each spark spirals up the column, then starts again at the floor
                float life = Mathf.Repeat(t * (0.25f + 0.35f * _activity) + i / (float)Sparks, 1f);
                float angle = i * 137.5f + life * 300f;
                float radius = 0.12f + 0.24f * Mathf.PerlinNoise(i * 3.1f, 0.5f);
                spark.localPosition = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * radius, 0.16f + life * (CapsuleModel.Height - 0.5f), Mathf.Sin(angle * Mathf.Deg2Rad) * radius);
                spark.localScale = Vector3.one * (Mathf.Sin(life * Mathf.PI) * (0.6f + 0.8f * _activity));
            }
        }

        private void Tint(Renderer renderer, Color emission)
        {
            if (renderer == null)
                return;
            renderer.GetPropertyBlock(_block);
            _block.SetColor(EmissionColorId, emission);
            renderer.SetPropertyBlock(_block);
        }
    }
}

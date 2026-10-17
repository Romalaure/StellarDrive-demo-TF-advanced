using System.Collections.Generic;
using Ships.Parts.Common;
using Ships.Parts.Common.Model;
using Ships.Parts.Common.Utils;
using Ships.Interface.Settings;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using StellarDriveDemoTFAdvanced.TFItems.Lights;
using UnityEngine;
using UnityEngine.Rendering;
using Loc = StellarDriveDemoTFAdvanced.Common.Loc;

namespace StellarDriveDemoTFAdvanced.TFItems.Cameras
{
    /// <summary>
    /// The surveillance camera: a small part on a bracket whose head looks out of the surface it is
    /// fixed on, tilted 30 degrees (rotate it while placing to aim). It renders nothing by itself;
    /// the tablet shows what the selected camera sees. Only call Register when SDModKit is loaded.
    /// </summary>
    internal static class CameraParts
    {
        public const ushort CameraId = 7141;
        public const string EyeName = "TF_CameraEye";

        // Direction the head looks, in part space (out of the surface, tilted toward -z)
        private static readonly Vector3 Look = new Vector3(0f, 0.866f, -0.5f);
        // Its up axis, perpendicular to Look
        private static readonly Vector3 LookUp = new Vector3(0f, 0.5f, 0.866f);
        private static readonly Vector3 Pivot = new Vector3(0f, 0.1f, 0f);

        public static bool Registered { get; private set; }

        public static void Register()
        {
            LampParts.RegisterDonorPart(CameraId, TFTab.CamerasRow, Configure, TFTab.ObjectsName);
            Registered = true;
            TFMod.Log.Msg("registered the surveillance camera");
        }

        private static void Configure(PartSettings settings, GameObject prefab)
        {
            Transform visuals = LampParts.PrepareDonor(settings, prefab, Loc.Pick("Caméra de surveillance", "Surveillance camera", "Cámara de vigilancia"),
                Loc.Pick("Caméra à fixer sur un mur, un plafond ou la coque. Elle regarde à l'opposé de sa surface, inclinée de 30° : tourne-la en la posant pour viser. Ouvre la tablette (F9) pour voir ce qu'elle filme ; dans la tablette, P permet d'en poser une nouvelle.", "Camera for a wall, a ceiling or the hull. It looks away from its surface, tilted 30°: turn it while placing it to aim. Open the tablet (F9) to see what it films; in the tablet, P places a new one.", "Cámara para una pared, un techo o el casco. Mira en dirección opuesta a su superficie, inclinada 30°: gírala al colocarla para apuntar. Abre la tableta (F9) para ver lo que graba; en la tableta, P coloca una nueva."),
                1f, new[] { (101u, 1), (102u, 1), (112u, 1) },
                new Vector3(0f, 0.11f, -0.04f), new Vector3(0.14f, 0.22f, 0.22f));
            int layer = visuals.gameObject.layer;

            foreach (KeyValuePair<string, MeshBuilder> entry in BuildShapes())
            {
                (Color color, float metal, float smooth, Color? glow) = MaterialFor(entry.Key);
                var child = new GameObject(entry.Key) { layer = layer };
                child.transform.SetParent(visuals, false);
                child.AddComponent<MeshFilter>().sharedMesh = entry.Value.Build("TF_Camera_" + entry.Key);
                var renderer = child.AddComponent<MeshRenderer>();
                // Lit like the game's parts (see DeviceModels.Flat); glowing pieces keep their own material
                renderer.sharedMaterial = (glow.HasValue ? null : Devices.DeviceModels.Flat("TF_Camera_" + entry.Key, color, metal, smooth))
                    ?? LitMaterials.Get("TF_Camera_" + entry.Key, color, metal, smooth, glow);
                renderer.shadowCastingMode = glow.HasValue ? ShadowCastingMode.Off : ShadowCastingMode.On;
            }

            var eye = new GameObject(EyeName) { layer = layer };
            eye.transform.SetParent(visuals, false);
            eye.transform.localPosition = Pivot + Look * 0.125f;
            eye.transform.localRotation = Quaternion.LookRotation(Look, LookUp);

            visuals.gameObject.AddComponent<CameraPartVisuals>();
        }

        // Body takes the paint; the rest does not ("TF_NoPaint", "glass")
        private static (Color, float, float, Color?) MaterialFor(string name)
        {
            switch (name)
            {
                case "TF_Camera_Body": return (new Color(0.9f, 0.91f, 0.93f), 0.2f, 0.75f, null);
                case "TF_NoPaint_CameraMount": return (new Color(0.1f, 0.11f, 0.13f), 0.5f, 0.6f, null);
                case "TF_NoPaint_CameraMetal": return (new Color(0.6f, 0.64f, 0.7f), 1f, 0.85f, null);
                case "TF_NoPaint_CameraGlass": return (new Color(0.02f, 0.03f, 0.06f), 0.1f, 0.97f, null);
                case "TF_NoPaint_CameraGlow": return (new Color(0.3f, 0.85f, 1f), 0f, 0.9f, new Color(0.25f, 0.8f, 1f) * 2.4f);
                default: return (new Color(1f, 0.1f, 0.08f), 0f, 0.6f, new Color(1f, 0.1f, 0.08f) * 3f);
            }
        }

        /// <summary>
        /// The camera's meshes by renderer name, for the game and for previews outside it: a round
        /// base with a lit ring, a yoke holding the head on its tilt axis, a sleek pod narrowing to
        /// a lens in three rings with a lit iris, cooling fins down its back, a sensor strip, a
        /// short antenna and the red recording light.
        /// </summary>
        internal static Dictionary<string, MeshBuilder> BuildShapes()
        {
            var body = new MeshBuilder();
            var mount = new MeshBuilder();
            var metal = new MeshBuilder();
            var glass = new MeshBuilder();
            var glow = new MeshBuilder();
            var led = new MeshBuilder();
            Vector3 right = Vector3.right;
            // A point in the head's frame: x across, y its up, z along its look
            Vector3 H(float x, float y, float z) => Pivot + right * x + LookUp * y + Look * z;

            // ---- Base: a disk on the surface, a chamfered top, a lit ring and the swivel
            mount.Cylinder(new Vector3(0f, 0f, 0f), new Vector3(0f, 0.012f, 0f), 0.055f, 0.055f, 24);
            mount.Cylinder(new Vector3(0f, 0.012f, 0f), new Vector3(0f, 0.02f, 0f), 0.055f, 0.045f, 24);
            glow.Cylinder(new Vector3(0f, 0.0125f, 0f), new Vector3(0f, 0.014f, 0f), 0.057f, 0.057f, 24, false, false);
            metal.Cylinder(new Vector3(0f, 0.02f, 0f), new Vector3(0f, 0.034f, 0f), 0.026f, 0.022f, 16);
            foreach (float a in new[] { 45f, 135f, 225f, 315f })
            {
                Vector3 bolt = Quaternion.Euler(0f, a, 0f) * new Vector3(0.042f, 0.0135f, 0f);
                metal.Cylinder(bolt, bolt + Vector3.up * 0.003f, 0.005f, 0.005f, 8);
            }
            // ---- Yoke: two arms from the swivel up to the tilt axis, the axis caps
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 foot = new Vector3(s * 0.02f, 0.034f, 0f);
                Vector3 top = Pivot + right * s * 0.046f;
                mount.Box((foot + top) * 0.5f + right * s * 0.012f, new Vector3(0.012f, (top - foot).magnitude + 0.01f, 0.024f));
                mount.Box(foot + right * s * 0.006f, new Vector3(0.026f, 0.01f, 0.026f));
                metal.Cylinder(top, top + right * s * 0.012f, 0.016f, 0.016f, 16);
                glow.Cylinder(top + right * s * 0.012f, top + right * s * 0.013f, 0.008f, 0.008f, 12);
            }
            metal.Cylinder(Pivot - right * 0.04f, Pivot + right * 0.04f, 0.009f, 0.009f, 12);

            // ---- Head: a pod in rings, wide at the back, narrowing toward the lens
            float[] stations = { -0.07f, -0.06f, -0.02f, 0.04f, 0.08f, 0.1f };
            float[] radii = { 0.018f, 0.034f, 0.038f, 0.036f, 0.03f, 0.028f };
            for (int k = 0; k < stations.Length - 1; k++)
                body.Cylinder(H(0f, 0f, stations[k]), H(0f, 0f, stations[k + 1]), radii[k], radii[k + 1], 20, k == 0, false);
            // A flat top deck with the sensor strip, a seam ring
            body.Box(H(0f, 0.032f, 0.01f), new Vector3(0.05f, 0.012f, 0.1f), right, LookUp, Look);
            glow.Box(H(0f, 0.0385f, 0.02f), new Vector3(0.012f, 0.002f, 0.06f), right, LookUp, Look);
            mount.Cylinder(H(0f, 0f, 0.035f), H(0f, 0f, 0.042f), 0.0372f, 0.0372f, 20, false, false);
            // Cooling fins along the back
            for (int f = 0; f < 4; f++)
            {
                float z = -0.055f + f * 0.014f;
                mount.Cylinder(H(0f, 0f, z), H(0f, 0f, z + 0.005f), 0.04f, 0.04f, 20, false, false);
            }
            // ---- Lens: three rings, the glass and a lit iris behind it
            metal.Cylinder(H(0f, 0f, 0.1f), H(0f, 0f, 0.11f), 0.029f, 0.027f, 22);
            mount.Cylinder(H(0f, 0f, 0.11f), H(0f, 0f, 0.122f), 0.026f, 0.025f, 22, true, false);
            metal.Cylinder(H(0f, 0f, 0.121f), H(0f, 0f, 0.124f), 0.026f, 0.026f, 22, false, false);
            glow.Cylinder(H(0f, 0f, 0.1175f), H(0f, 0f, 0.1185f), 0.012f, 0.012f, 18);
            glass.Cylinder(H(0f, 0f, 0.119f), H(0f, 0f, 0.1235f), 0.021f, 0.02f, 22);
            // A short sun visor over the lens
            body.Box(H(0f, 0.029f, 0.112f), new Vector3(0.052f, 0.004f, 0.03f), right, LookUp, Look);
            // ---- Antenna and the recording light
            metal.Cylinder(H(-0.022f, 0.036f, -0.04f), H(-0.022f, 0.075f, -0.05f), 0.0025f, 0.0018f, 8);
            led.Cylinder(H(-0.022f, 0.075f, -0.05f), H(-0.022f, 0.08f, -0.051f), 0.004f, 0.004f, 8);
            led.Box(H(0.018f, 0.0385f, 0.09f), new Vector3(0.008f, 0.003f, 0.008f), right, LookUp, Look);

            return new Dictionary<string, MeshBuilder>
            {
                ["TF_Camera_Body"] = body,
                ["TF_NoPaint_CameraMount"] = mount,
                ["TF_NoPaint_CameraMetal"] = metal,
                ["TF_NoPaint_CameraGlass"] = glass,
                ["TF_NoPaint_CameraGlow"] = glow,
                ["TF_NoPaint_CameraLed"] = led
            };
        }
    }

    /// <summary>Keeps the list of placed cameras, with the point and direction each one films from.</summary>
    internal sealed class CameraPartVisuals : DefaultShipPartVisuals, IContextAwarePart
    {
        public static readonly List<CameraPartVisuals> All = new List<CameraPartVisuals>();

        public PartKey Key { get; private set; }
        public Transform Eye { get; private set; }
        public bool HasContext { get; private set; }

        private void Awake()
        {
            Eye = transform.Find(CameraParts.EyeName);
        }

        public void SetContext(ShipPartContext context)
        {
            Key = new PartKey(context.Part.ShipId, context.Part.PartId);
            HasContext = true;
        }

        private void OnEnable() => All.Add(this);

        private void OnDisable() => All.Remove(this);
    }
}

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
                renderer.sharedMaterial = LitMaterials.Get("TF_Camera_" + entry.Key, color, metal, smooth, glow);
                renderer.shadowCastingMode = glow.HasValue ? ShadowCastingMode.Off : ShadowCastingMode.On;
            }

            var eye = new GameObject(EyeName) { layer = layer };
            eye.transform.SetParent(visuals, false);
            eye.transform.localPosition = Pivot + Look * 0.125f;
            eye.transform.localRotation = Quaternion.LookRotation(Look, LookUp);

            visuals.gameObject.AddComponent<CameraPartVisuals>();
        }

        // Body takes the paint; lens glass and the recording light do not ("TF_NoPaint", "glass")
        private static (Color, float, float, Color?) MaterialFor(string name)
        {
            switch (name)
            {
                case "TF_Camera_Body": return (new Color(0.88f, 0.89f, 0.9f), 0.1f, 0.6f, null);
                case "TF_NoPaint_CameraMount": return (new Color(0.2f, 0.21f, 0.23f), 0.6f, 0.45f, null);
                case "TF_NoPaint_CameraGlass": return (new Color(0.02f, 0.03f, 0.05f), 0f, 0.95f, null);
                default: return (new Color(1f, 0.1f, 0.08f), 0f, 0.6f, new Color(1f, 0.1f, 0.08f) * 3f);
            }
        }

        /// <summary>The camera's meshes by renderer name, for the game and for previews outside it.</summary>
        internal static Dictionary<string, MeshBuilder> BuildShapes()
        {
            var body = new MeshBuilder();
            var mount = new MeshBuilder();
            var glass = new MeshBuilder();
            var led = new MeshBuilder();
            Vector3 right = Vector3.right;

            mount.Box(new Vector3(0f, 0.01f, 0f), new Vector3(0.1f, 0.02f, 0.1f));
            mount.Cylinder(new Vector3(0f, 0.02f, 0f), Pivot, 0.016f, 0.016f, 12);
            mount.Cylinder(Pivot - right * 0.03f, Pivot + right * 0.03f, 0.022f, 0.022f, 14);
            // Head: housing with a sun hood over the lens
            body.Box(Pivot + Look * 0.04f, new Vector3(0.075f, 0.065f, 0.15f), right, LookUp, Look);
            body.Box(Pivot + Look * 0.105f + LookUp * 0.036f, new Vector3(0.085f, 0.008f, 0.05f), right, LookUp, Look);
            mount.Cylinder(Pivot + Look * 0.11f, Pivot + Look * 0.122f, 0.027f, 0.027f, 18);
            glass.Cylinder(Pivot + Look * 0.118f, Pivot + Look * 0.124f, 0.021f, 0.021f, 18);
            led.Box(Pivot + Look * 0.1f + right * 0.025f + LookUp * 0.034f, new Vector3(0.01f, 0.006f, 0.01f), right, LookUp, Look);

            return new Dictionary<string, MeshBuilder>
            {
                ["TF_Camera_Body"] = body,
                ["TF_NoPaint_CameraMount"] = mount,
                ["TF_NoPaint_CameraGlass"] = glass,
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

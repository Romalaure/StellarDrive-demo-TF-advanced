using System.Collections;
using System.IO;
using System.Linq;
using Ships;
using Ships.Interface.Model.Parts;
using Ships.Parts.Common.Utils;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.ShipComputer
{
    /// <summary>
    /// Development check run by <see cref="DevHarness"/>: a full and a small ship computer screen,
    /// fed with the biggest ship of the loaded world, rendered far below the scene to
    /// UserData/TFAdvanced/dev/dashboard.png.
    /// </summary>
    internal static class DashboardDevRender
    {
        public static void Install()
        {
            DevHarness.Steps.Add((28f, "dashboard render", () => MelonLoader.MelonCoroutines.Start(Render())));
        }

        private static IEnumerator Render()
        {
            TrackedShipClient ship = GameServices.ShipsClient?.Ships.OrderByDescending(s => s.PartCount).FirstOrDefault();
            if (ship == null)
            {
                AdvancedMod.Log.Warning("dev: no ship for the dashboard render");
                yield break;
            }
            var origin = new Vector3(0f, -30000f, 0f);
            GameObject big = Screen(origin + new Vector3(-0.3f, 0f, 0f), DashboardVisuals.AllSections, 960f, 580f, ship.Id);
            GameObject small = Screen(origin + new Vector3(0.52f, 0.12f, 0f), "power", 520f, 340f, ship.Id);
            GameObject small2 = Screen(origin + new Vector3(0.52f, -0.26f, 0f), "propulsion", 520f, 340f, ship.Id);
            float until = Time.unscaledTime + 2f;
            while (Time.unscaledTime < until)
                yield return null;

            var cameraObject = new GameObject("TFA_DashboardCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.13f, 0.15f);
            camera.fieldOfView = 40f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 50f;
            camera.transform.position = origin + new Vector3(0.1f, 0f, -1.75f);
            camera.transform.rotation = Quaternion.identity;
            var target = RenderTexture.GetTemporary(2000, 1250, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            Directory.CreateDirectory(DevHarness.Folder);
            File.WriteAllBytes(Path.Combine(DevHarness.Folder, "dashboard.png"), image.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.ReleaseTemporary(target);
            Object.Destroy(image);
            Object.Destroy(cameraObject);
            Object.Destroy(big);
            Object.Destroy(small);
            Object.Destroy(small2);
            AdvancedMod.Log.Msg($"dev: dashboard rendered with ship {ship.Id} ({ship.PartCount} parts)");
        }

        // A bare screen (no part model) facing -Z, toward the render camera
        private static GameObject Screen(Vector3 position, string layout, float width, float height, uint shipId)
        {
            var root = new GameObject("TFA_DevScreen");
            root.SetActive(false);
            root.transform.position = position;
            var anchor = new GameObject(DashboardVisuals.AnchorName(layout, width, height));
            anchor.transform.SetParent(root.transform, false);
            DashboardVisuals screen = root.AddComponent<DashboardVisuals>();
            screen.IgnoreDistance = true;
            root.SetActive(true);
            screen.SetContext(new ShipPartContext { Part = new PartContext(shipId, 0) });
            return root;
        }
    }
}

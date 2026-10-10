using System.IO;
using MelonLoader.Utils;
using SDModKit.Game;
using Ships.Interface.Settings;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Common
{
    /// <summary>
    /// Development check run with <see cref="SelfTest"/>: renders some of the mod's parts, and a
    /// game part for comparison, to PNG files in UserData/TFAdvanced/snapshots, far below the scene.
    /// </summary>
    internal static class PartSnapshots
    {
        private static readonly ushort[] Ids = { 17, 30, 7301, 7302, 7303, 7311, 7312, 7321, 7322, 7331, 7334, 7335, 7337, 7338 };

        public static void Run()
        {
            string folder = Path.Combine(MelonEnvironment.UserDataDirectory, "TFAdvanced", "snapshots");
            Directory.CreateDirectory(folder);
            var origin = new Vector3(0f, -20000f, 0f);
            var cameraObject = new GameObject("TFA_SnapshotCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.32f, 0.36f, 0.42f, 1f);
            camera.fieldOfView = 30f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 100f;
            var lightObject = new GameObject("TFA_SnapshotLight");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            var target = RenderTexture.GetTemporary(512, 512, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            int done = 0;
            try
            {
                foreach (ushort id in Ids)
                {
                    PartSettings settings = id < 7000 ? FindGame(id) : CustomParts.Get(id);
                    if (settings == null || settings.part == null)
                        continue;
                    GameObject instance = Object.Instantiate(settings.part, origin, Quaternion.identity);
                    Bounds bounds = new Bounds(origin, Vector3.one * 0.2f);
                    foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
                        bounds.Encapsulate(renderer.bounds);
                    float distance = bounds.extents.magnitude / Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.1f;
                    Vector3 direction = new Vector3(0.6f, 0.55f, -0.8f).normalized;
                    camera.transform.position = bounds.center + direction * distance;
                    camera.transform.LookAt(bounds.center);
                    camera.Render();
                    Save(target, Path.Combine(folder, $"{id}_{settings.name}.png"));
                    Object.DestroyImmediate(instance);
                    done++;
                }
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.ReleaseTemporary(target);
                Object.Destroy(cameraObject);
                Object.Destroy(lightObject);
            }
            AdvancedMod.Log.Msg($"part snapshots: {done} written to {folder}");
        }

        private static PartSettings FindGame(ushort id)
        {
            foreach (PartSettings settings in Resources.FindObjectsOfTypeAll<PartSettings>())
            {
                if (settings != null && settings.id == id && settings.name != null && !settings.name.StartsWith("TF"))
                    return settings;
            }
            return null;
        }

        private static void Save(RenderTexture source, string path)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = source;
            var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            copy.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, copy.EncodeToPNG());
            Object.Destroy(copy);
        }
    }
}

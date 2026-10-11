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
        private static readonly ushort[] Ids = { 7312, 7315, 7321, 7323, 7324, 7325 };

        /// <summary>Extra parts to render, by asset name (game parts) or id, from SnapshotParts.</summary>
        private static System.Collections.Generic.IEnumerable<PartSettings> Targets()
        {
            string list = Settings.DevSnapshotParts.Value;
            if (string.IsNullOrWhiteSpace(list))
            {
                foreach (ushort id in Ids)
                    yield return CustomParts.Get(id);
                yield break;
            }
            foreach (string entry in list.Split(','))
            {
                string key = entry.Trim();
                if (ushort.TryParse(key, out ushort id))
                    yield return id < 7000 ? FindGame(id) : CustomParts.Get(id);
                else
                    yield return PartLookup.ByAssetName(key);
            }
        }

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
            var sheet = new Texture2D(1024, 512, TextureFormat.RGBA32, false);
            camera.targetTexture = target;
            int done = 0;
            try
            {
                foreach (PartSettings settings in Targets())
                {
                    if (settings == null || settings.part == null)
                        continue;
                    ushort id = settings.id;
                    GameObject instance = Object.Instantiate(settings.part, origin, Quaternion.identity);
                    Bounds bounds = new Bounds(origin, Vector3.one * 0.2f);
                    foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
                        bounds.Encapsulate(renderer.bounds);
                    float distance = bounds.extents.magnitude / Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.1f;
                    Vector3[] views = { new Vector3(0.6f, 0.55f, -0.8f), new Vector3(-0.7f, -0.35f, 0.6f) };
                    for (int v = 0; v < views.Length; v++)
                    {
                        camera.transform.position = bounds.center + views[v].normalized * distance;
                        camera.transform.LookAt(bounds.center);
                        camera.Render();
                        RenderTexture previous = RenderTexture.active;
                        RenderTexture.active = target;
                        sheet.ReadPixels(new Rect(0, 0, 512, 512), v * 512, 0);
                        RenderTexture.active = previous;
                    }
                    sheet.Apply();
                    File.WriteAllBytes(Path.Combine(folder, $"{id}_{settings.name}.png"), sheet.EncodeToPNG());
                    var sockets = new System.Text.StringBuilder();
                    foreach (Ships.Interface.Model.Parts.Common.SocketObject socket in instance.GetComponentsInChildren<Ships.Interface.Model.Parts.Common.SocketObject>(true))
                        sockets.Append($" {socket.Id}:{socket.Type}@{socket.transform.position - origin:F2}");
                    var partBounds = instance.GetComponent<Ships.Interface.Model.Parts.ShipPartBounds>();
                    AdvancedMod.Log.Msg($"snapshot {id} {settings.name}: renderers {bounds.center - origin:F2}±{bounds.extents:F2}, part box {partBounds?.center:F2}/{partBounds?.bounds:F2}, sockets{sockets}");
                    Object.DestroyImmediate(instance);
                    done++;
                }
            }
            finally
            {
                Object.Destroy(sheet);
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

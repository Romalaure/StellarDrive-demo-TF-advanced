using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Worlds
{
    /// <summary>
    /// Development check run by <see cref="DevHarness"/>: the mod's trees, village buildings and
    /// cargo wreck models side by side, rendered far below the scene to UserData/TFAdvanced/dev/models.png.
    /// </summary>
    internal static class TreeDevRender
    {
        public static void Install()
        {
            DevHarness.Steps.Add((26f, "models render", () => MelonLoader.MelonCoroutines.Start(Render())));
        }

        private static IEnumerator Render()
        {
            var origin = new Vector3(0f, -40000f, 0f);
            var shown = new List<GameObject>();
            var prefabs = Trees.AllBuilt.Concat(Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(t => t.parent != null && (t.parent.name == "TFA_CityPrefabs" || t.parent.name == "TFA_RuinPrefabs" && t.name.Contains("Gate_Ice")))
                .Select(t => t.gameObject)).ToList();
            float x = 0f;
            foreach (GameObject prefab in prefabs)
            {
                MeshRenderer source = prefab.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault();
                if (source == null)
                    continue;
                Mesh mesh = source.GetComponent<MeshFilter>().sharedMesh;
                float width = Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.z);
                x += width * 0.5f + 2f;
                var copy = new GameObject("TFA_DevModel_" + prefab.name);
                copy.transform.position = origin + new Vector3(x, 0f, 0f);
                copy.AddComponent<MeshFilter>().sharedMesh = mesh;
                copy.AddComponent<MeshRenderer>().sharedMaterials = source.sharedMaterials;
                shown.Add(copy);
                foreach (MeshRenderer extra in prefab.GetComponentsInChildren<MeshRenderer>(true).Skip(1))
                {
                    var part = new GameObject("TFA_DevModelPart");
                    part.transform.SetParent(copy.transform, false);
                    part.AddComponent<MeshFilter>().sharedMesh = extra.GetComponent<MeshFilter>().sharedMesh;
                    part.AddComponent<MeshRenderer>().sharedMaterials = extra.sharedMaterials;
                }
                x += width * 0.5f + 2f;
                AdvancedMod.Log.Msg($"dev: model {prefab.name}: {mesh.vertexCount} vertices, {mesh.bounds.size}");
            }
            var lightObject = new GameObject("TFA_DevLight");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            lightObject.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            yield return null;

            var cameraObject = new GameObject("TFA_ModelsCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.7f, 0.85f);
            camera.fieldOfView = 40f;
            camera.nearClipPlane = 0.5f;
            camera.farClipPlane = 2000f;
            float distance = x * 0.5f / Mathf.Tan(20f * Mathf.Deg2Rad) * 0.62f;
            camera.transform.position = origin + new Vector3(x * 0.5f, 24f, -distance);
            camera.transform.rotation = Quaternion.Euler(8f, 0f, 0f);
            var target = RenderTexture.GetTemporary(2400, 1000, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            Directory.CreateDirectory(DevHarness.Folder);
            File.WriteAllBytes(Path.Combine(DevHarness.Folder, "models.png"), image.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.ReleaseTemporary(target);
            Object.Destroy(image);
            Object.Destroy(cameraObject);
            Object.Destroy(lightObject);
            foreach (GameObject copy in shown)
                Object.Destroy(copy);
            AdvancedMod.Log.Msg($"dev: {shown.Count} models rendered");
        }
    }
}

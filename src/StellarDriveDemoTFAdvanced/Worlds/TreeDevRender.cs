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
            var prefabs = Trees.AllBuilt.Where(t => t.name.Contains("Kapok") || t.name.Contains("Palm") || t.name.Contains("Jungle") || t.name.EndsWith("_Oak") || t.name.EndsWith("_Birch") || t.name.EndsWith("_Acacia")).Concat(Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(t => t.parent != null && (t.parent.name == "TFA_CavePrefabs" || t.parent.name == "TFA_CityPrefabs" && (t.name.Contains("Dome") || t.name.EndsWith("Tower")) || t.parent.name == "TFA_OrePrefabs"
                    || t.parent.name == "TFA_RuinPrefabs" && t.name.EndsWith("_Mossy") && (t.name.Contains("Hall") || t.name.Contains("House_") || t.name.Contains("Tower") || t.name.Contains("Temple"))))
                .Select(t => t.gameObject)).ToList();
            prefabs.AddRange(Trees.AllPlants);

            float x = 0f;
            foreach (GameObject prefab in prefabs)
            {
                MeshRenderer source = prefab.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault();
                if (source == null)
                    continue;
                Mesh mesh = source.GetComponent<MeshFilter>().sharedMesh;
                float width = Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.z) * (prefab.name.StartsWith("TFA_Item") ? 8f : 1f);
                x += width * 0.5f + 2f;
                var copy = new GameObject("TFA_DevModel_" + prefab.name);
                copy.transform.position = origin + new Vector3(x, 0f, 0f);
                if (prefab.name.StartsWith("TFA_Item"))
                    copy.transform.localScale = Vector3.one * 8f;
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
            // One framed tile per model: 6 columns
            const int columns = 6, tile = 400;
            int rows = (shown.Count + columns - 1) / columns;
            var target = RenderTexture.GetTemporary(columns * tile, rows * tile, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            Vector3 view = new Vector3(0.45f, 0.5f, -1f).normalized;
            for (int i = 0; i < shown.Count; i++)
            {
                Bounds bounds = new Bounds(shown[i].transform.position, Vector3.zero);
                foreach (Renderer r in shown[i].GetComponentsInChildren<Renderer>())
                    bounds.Encapsulate(r.bounds);
                float size = bounds.extents.magnitude;
                camera.rect = new Rect((float)(i % columns) / columns, 1f - (float)(i / columns + 1) / rows, 1f / columns, 1f / rows);
                camera.transform.position = bounds.center + view * (size / Mathf.Tan(20f * Mathf.Deg2Rad) * 1.05f);
                camera.transform.LookAt(bounds.center);
                camera.nearClipPlane = Mathf.Max(0.01f, size * 0.05f);
                // Other models out of the frame
                foreach (GameObject other in shown)
                    other.SetActive(other == shown[i]);
                camera.Render();
            }
            // Inside the caves: from their mouth (toward -z) into the chamber
            foreach (GameObject cave in shown.Where(c => c.name.Contains("TFA_Cave")))
            {
                foreach (GameObject other in shown)
                    other.SetActive(other == cave);
                camera.rect = new Rect(0f, 0f, 1f, 1f);
                camera.fieldOfView = 75f;
                camera.nearClipPlane = 0.1f;
                var inside = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = inside;
                camera.transform.position = cave.transform.position + new Vector3(0f, 1.2f, -7f);
                camera.transform.rotation = Quaternion.LookRotation(Vector3.forward + Vector3.up * 0.15f);
                camera.Render();
                RenderTexture.active = inside;
                var shot = new Texture2D(inside.width, inside.height, TextureFormat.RGB24, false);
                shot.ReadPixels(new Rect(0, 0, inside.width, inside.height), 0, 0);
                shot.Apply();
                File.WriteAllBytes(Path.Combine(DevHarness.Folder, "inside_" + cave.name.Replace("TFA_DevModel_", "") + ".png"), shot.EncodeToPNG());
                Object.Destroy(shot);
                RenderTexture.active = null;
                RenderTexture.ReleaseTemporary(inside);
                camera.fieldOfView = 40f;
                camera.targetTexture = target;
            }
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

using System.Collections;
using System.IO;
using System.Linq;
using Ships.Parts.StarMap;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Worlds
{
    /// <summary>
    /// Development check: a copy of the game's star map hologram, generated like a placed star
    /// map does, listed in the log and rendered to UserData/TFAdvanced/dev/starmap.png.
    /// </summary>
    internal static class StarMapProbe
    {
        public static void Install()
        {
            DevHarness.Steps.Add((24f, "star map probe", () => MelonLoader.MelonCoroutines.Start(Probe())));
        }

        private static IEnumerator Probe()
        {
            PlanetsHologram prefab = Resources.FindObjectsOfTypeAll<PlanetsHologram>().FirstOrDefault(h => h.gameObject.scene.name == null);
            if (prefab == null)
            {
                AdvancedMod.Log.Warning("dev: no star map prefab");
                yield break;
            }
            var client = Core.Services.ServiceLocator.GetService<Planets.Interface.Services.IPlanetsClientProvider>();
            var server = Core.Services.ServiceLocator.GetService<Planets.Interface.Services.IPlanetsServerProvider>();
            AdvancedMod.Log.Msg("dev: client planets: " + (client == null ? "none" : string.Join(", ", client.Planets.Select(p => p.Name + " #" + p.Id))) + " (" + client?.GetType().FullName + ")");
            AdvancedMod.Log.Msg("dev: server planets: " + (server == null ? "none" : string.Join(", ", server.Planets.Select(p => p.Name + " #" + p.Id))));
            foreach (string name in new[] { "Generate", "GeneratePlanetsHolograms", "GeneratePlanetHologram", "CalculatePlanetPos" })
            {
                var info = HarmonyLib.Harmony.GetPatchInfo(HarmonyLib.AccessTools.Method(typeof(PlanetsHologram), name));
                if (info != null)
                    AdvancedMod.Log.Msg($"dev: {name} patched by " + string.Join(", ", info.Owners));
            }
            AdvancedMod.Log.Msg("dev: rings on " + string.Join(", ", Object.FindObjectsByType<Planets.Rings.PlanetRing>(FindObjectsSortMode.None).Select(r => r.name + " " + r.InnerWidth / 1000f + "-" + r.OuterWidth / 1000f + " km")));
            var origin = new Vector3(0f, -50000f, 0f);
            var root = new GameObject("TFA_DevStarMap");
            root.SetActive(false);
            root.transform.position = origin;
            PlanetsHologram map = Object.Instantiate(prefab.gameObject, root.transform).GetComponent<PlanetsHologram>();
            map.transform.localPosition = Vector3.zero;
            root.SetActive(true);
            map.Generate();
            AdvancedMod.Log.Msg("dev: star map shows " + string.Join(", ", map.Holograms.Values.Select(h =>
                $"{h.Planet.Name} #{h.Id} at {h.Position} r {h.Radius:0.000}")));
            yield return null;
            yield return null;

            var cameraObject = new GameObject("TFA_StarMapCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.06f, 0.08f);
            camera.fieldOfView = 40f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 100f;
            camera.transform.position = map.transform.position + new Vector3(0f, 0.9f, -0.75f);
            camera.transform.LookAt(map.transform.position);
            var target = RenderTexture.GetTemporary(1600, 1200, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            Directory.CreateDirectory(DevHarness.Folder);
            File.WriteAllBytes(Path.Combine(DevHarness.Folder, "starmap.png"), image.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.ReleaseTemporary(target);
            Object.Destroy(image);
            Object.Destroy(cameraObject);
            Object.Destroy(root);
        }
    }
}

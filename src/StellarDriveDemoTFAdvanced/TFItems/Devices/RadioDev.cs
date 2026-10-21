using System.Collections;
using System.IO;
using System.Linq;
using Core.Services;
using Players.Inventory;
using Players.Interface.Services;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace StellarDriveDemoTFAdvanced.TFItems.Devices
{
    /// <summary>
    /// Development check (a radio in DevShowcase): plays a game track on the showcase's radio, logs
    /// what a listener hears around it (its speakers included), whether the build tool's preview
    /// of a speaker counts as a source, and the radio's brightness over a burst of frames.
    /// </summary>
    internal static class RadioDev
    {
        public static void Install()
        {
            StellarDriveDemoTFAdvanced.Common.DevHarness.Steps.Add((110f, "radio", () =>
            {
                if (("," + StellarDriveDemoTFAdvanced.Common.Settings.DevShowcase.Value.Replace(" ", "") + ",").Contains("," + Radio.RadioId + ","))
                    MelonLoader.MelonCoroutines.Start(Run());
            }));
        }

        private static IEnumerator Run()
        {
            RadioVisuals radio = UsablePart.All.OfType<RadioVisuals>().FirstOrDefault(r => r.HasContext);
            if (radio == null)
            {
                TFMod.Log.Warning("dev: radio: none placed");
                yield break;
            }
            string track = Radio.GameTracks().FirstOrDefault();
            Radio.Request(radio.Key, "game|" + track);
            yield return new WaitForSecondsRealtime(3f);
            LogSources("playing");
            // What a listener hears walking away from the radio, sideways
            Vector3 right = radio.transform.right;
            for (int d = 0; d <= 30; d += 3)
            {
                RadioSound.Mix(radio.Key, radio.Center - radio.transform.forward * d, right, out float volume, out float pan);
                TFMod.Log.Msg($"dev: radio heard {d} m in front: volume {volume:0.00} pan {pan:0.00}");
            }
            for (int d = 0; d <= 30; d += 6)
            {
                RadioSound.Mix(radio.Key, radio.Center + Vector3.up * 40f + right * d, right, out float volume, out float pan);
                TFMod.Log.Msg($"dev: radio heard 40 m above, {d} m aside: volume {volume:0.00} pan {pan:0.00}");
            }

            // The build tool holding a speaker: does its preview count as a source?
            var parts = ServiceLocator.GetService<ILocalSelectedPartProvider>();
            ToolBelt belt = Object.FindFirstObjectByType<ToolBelt>();
            if (parts != null && belt != null)
            {
                parts.SelectPart(Radio.SpeakerId);
                belt.SelectTool(1);
                yield return new WaitForSecondsRealtime(2f);
                LogSources("build tool holding a speaker");
                belt.SelectTool(0);
                yield return new WaitForSecondsRealtime(1f);
            }

            yield return Burst(radio);
            Radio.Request(radio.Key, "");
        }

        private static void LogSources(string when)
        {
            Camera camera = MainCamera.Get();
            Vector3 listener = camera != null ? camera.transform.position : Vector3.zero;
            TFMod.Log.Msg($"dev: radio sources ({when}): " + string.Join("; ", UsablePart.All.Where(p => p is RadioVisuals || p is SpeakerVisuals)
                .Select(p => $"{p.GetType().Name} ctx {p.HasContext} key {p.Key} at {(p.Center - listener).magnitude:0.0} m path {Path(p.transform)}")));
        }

        private static string Path(Transform t)
        {
            string path = t.name;
            for (int i = 0; i < 4 && t.parent != null; i++)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }

        // Frames of the radio from close by, with the game's post-processing (bloom): its brightness
        // from one frame to the next, and the first and brightest frames saved
        private static IEnumerator Burst(RadioVisuals radio)
        {
            Camera main = MainCamera.Get();
            var cameraObject = new GameObject("TFA_RadioDevCamera");
            var camera = cameraObject.AddComponent<Camera>();
            if (main != null)
                camera.CopyFrom(main);
            camera.enabled = false;
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.05f;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            var target = new RenderTexture(640, 360, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            var read = new Texture2D(640, 360, TextureFormat.RGBA32, false);
            float brightest = -1f;
            for (int i = 0; i < 24; i++)
            {
                Vector3 eye = radio.transform.TransformPoint(new Vector3(0.15f * Mathf.Sin(i * 0.4f), 0.3f, -0.9f));
                camera.transform.position = eye;
                camera.transform.rotation = Quaternion.LookRotation(radio.Center - eye, radio.transform.up);
                camera.Render();
                RenderTexture.active = target;
                read.ReadPixels(new Rect(0, 0, 640, 360), 0, 0);
                read.Apply();
                RenderTexture.active = null;
                Color[] pixels = read.GetPixels(160, 60, 320, 240);
                float mean = pixels.Average(c => c.grayscale);
                int white = pixels.Count(c => c.r > 0.97f && c.g > 0.97f && c.b > 0.97f);
                TFMod.Log.Msg($"dev: radio frame {i}: mean {mean:0.000}, white pixels {white}");
                if (i == 0 || mean > brightest)
                {
                    brightest = Mathf.Max(brightest, mean);
                    File.WriteAllBytes(System.IO.Path.Combine(StellarDriveDemoTFAdvanced.Common.DevHarness.Folder, i == 0 ? "radio_frame_first.png" : "radio_frame_brightest.png"), read.EncodeToPNG());
                }
                yield return new WaitForSecondsRealtime(0.08f);
            }
            camera.targetTexture = null;
            Object.Destroy(target);
            Object.Destroy(cameraObject);
        }
    }
}

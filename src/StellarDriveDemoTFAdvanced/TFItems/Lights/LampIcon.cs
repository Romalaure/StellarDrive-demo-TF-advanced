using HarmonyLib;
using Research;
using Ships.Interface.Settings;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.TFItems.Lights
{
    /// <summary>Build tab icon: a light bulb with rays, drawn in code.</summary>
    internal static class LampIcon
    {
        public static Texture2D Create()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "TF_LampIcon", hideFlags = HideFlags.HideAndDontSave };
            var clear = new Color(1f, 1f, 1f, 0f);
            var bulb = new Color(1f, 0.85f, 0.4f, 1f);
            var metal = new Color(0.85f, 0.86f, 0.9f, 1f);
            var center = new Vector2(32f, 38f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    Color c = clear;
                    float d = Vector2.Distance(p, center);
                    if (d < 14f)
                        c = bulb;
                    // Neck and screw base
                    else if (y >= 14 && y < 26 && Mathf.Abs(p.x - 32f) < 8f - (26 - y) * 0.15f)
                        c = (y / 3) % 2 == 0 ? metal : metal * 0.75f;
                    // Rays
                    else if (d > 18f && d < 26f)
                    {
                        float angle = Mathf.Atan2(p.y - center.y, p.x - center.x) * Mathf.Rad2Deg;
                        if (angle > -20f && Mathf.Abs(Mathf.DeltaAngle(angle, Mathf.Round(angle / 40f) * 40f)) < 5f)
                            c = bulb;
                    }
                    texture.SetPixel(x, y, c);
                }
            }
            texture.Apply();
            return texture;
        }
    }

    /// <summary>Lamps (and the TF devices: camera, capsule, radio, speaker, rack, cabin, photo frame) are in no research node, so the build menu would keep them locked forever.</summary>
    [HarmonyPatch(typeof(ResearchClientProxy), nameof(ResearchClientProxy.IsPartUnlocked))]
    internal static class LampUnlockPatch
    {
        private static void Postfix(PartSettings part, ref bool __result)
        {
            if (!__result && part != null && (LampCatalog.IsLamp(part.id) || part.id == Cameras.CameraParts.CameraId
                || part.id == Devices.TeleportCapsule.CapsuleId || part.id == Devices.Radio.RadioId || part.id == Devices.Radio.SpeakerId
                || part.id == Devices.ToolRack.RackId || part.id == Devices.CosmeticCabin.CabinId || part.id == Devices.PhotoFrame.Id))
                __result = true;
        }
    }
}

using System;
using System.Linq;
using System.Reflection;
using MelonLoader;

namespace StellarDriveDemoTFAdvanced.TFItems
{
    /// <summary>The TF mod's log name inside this mod (the moved code logs through it).</summary>
    internal static class TFMod
    {
        public static MelonLogger.Instance Log => AdvancedMod.Log;
    }

    /// <summary>
    /// The objects moved here from the StellarDrive Demo TF mod: lamps, surveillance camera and its
    /// tablet, teleport capsule, radio, tool rack and infinite parts. Same part ids as in the TF
    /// mod, so the ones already built in a world stay. A TF mod from before the move (0.14 and
    /// older) still registers them itself: this mod then leaves them to it, so nothing is doubled.
    /// The TF mod from 0.15 on leaves them to this mod.
    /// </summary>
    internal static class TFItemsMod
    {
        private static bool? _active;

        /// <summary>Whether this mod provides the TF objects (false while an old TF mod does).</summary>
        public static bool Active => _active ?? (_active = !OldTfProvides()).Value;

        private static bool OldTfProvides()
        {
            Assembly tf = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "StellarDriveDemoTF");
            if (tf == null)
                return false;
            var info = tf.GetCustomAttributes(typeof(MelonInfoAttribute), false).OfType<MelonInfoAttribute>().FirstOrDefault();
            bool old = info == null || !Version.TryParse(info.Version, out Version version) || version < new Version(0, 15, 0);
            if (old)
                AdvancedMod.Log.Warning($"StellarDrive Demo TF {info?.Version} provides the lamps, camera, capsule, radio, rack and infinite parts: update it to 0.15 or later, they are now part of this mod");
            return old;
        }

        public static void Initialize()
        {
            if (!Active)
                return;
            Common.TFNet.InstallSerializers();
            Devices.TeleportCapsule.Install();
            Devices.Radio.Install();
            Devices.ToolRack.Install();
        }

        /// <summary>Only call when SDModKit is loaded.</summary>
        public static void RegisterParts(Action<string, Action> attempt)
        {
            if (!Active)
                return;
            attempt("the TF equipment tab", Common.TFTab.Declare);
            attempt("the lamps", Lights.LampParts.Register);
            attempt("the camera", Cameras.CameraParts.Register);
            attempt("the infinite parts", Infinite.InfiniteParts.Register);
            attempt("the teleport capsule", Devices.TeleportCapsule.Register);
            attempt("the radio", Devices.Radio.Register);
            attempt("the tool rack", Devices.ToolRack.Register);
        }

        public static void OnSceneLoaded()
        {
            if (Active)
                Common.GameServices.Invalidate();
        }

        public static void Update()
        {
            if (!Active)
                return;
            Cameras.CameraTablet.Update();
            Common.TFNet.Update();
            Common.UsableParts.Update();
            Devices.RadioSound.Update();
        }

        public static void LateUpdate()
        {
            if (Active)
                Cameras.CameraTablet.LateUpdate();
        }

        public static void OnGUI()
        {
            if (!Active)
                return;
            Cameras.CameraTablet.Draw();
            Common.UsableParts.Draw();
            Devices.TeleportMenu.Draw();
            Devices.RadioMenu.Draw();
            Devices.ToolRackMenu.Draw();
        }
    }
}

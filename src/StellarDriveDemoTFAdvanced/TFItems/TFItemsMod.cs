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

        private static bool? _tools;

        /// <summary>
        /// The schematic tablet and its schematics, the paint gun (parts, camouflage, finishes,
        /// tf-paint.json), the third person view and the KeySeat compatibility, moved here from the TF mod too: provided while no TF mod is installed
        /// (a TF mod, any version, still provides them itself).
        /// </summary>
        public static bool ToolsActive => _tools ?? (_tools = Active && !AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "StellarDriveDemoTF")).Value;

        /// <summary>The patch classes of those tools (and all of the Paint namespace), skipped while a TF mod provides them.</summary>
        public static readonly string[] ToolPatches = { "ThirdPersonCameraPatch", "KeySeatChangePatch", "KeySeatRemovePatch", "SchematicTabletItemPatch" };

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
            // The [TFAdvancedObjects] settings: every lamp, the tablet, the radio and the use key read them
            Common.Settings.Load();
            if (!Active)
                return;
            Common.TFNet.InstallSerializers();
            Devices.TeleportCapsule.Install();
            Devices.Radio.Install();
            Devices.Seats.Install();
            Devices.RadioShare.Install();
            Devices.RadioDev.Install();
            Devices.PhotoFrame.Install();
            Common.TFNet.Disconnected += Devices.PhotoMode.Reset;
            Devices.ToolRack.Install();
            Power.FieldDoor.Install();
            Devices.Outfits.Install();
            Devices.Teleporter.Install();
            Devices.Trader.Install();
            Devices.Npc.Install();
            Cameras.CameraTablet.Install();
            Construction.Signs.Install();
            Construction.DiagonalDoors.Install();
            Construction.FurnitureUses.Install();
            StellarDriveDemoTFAdvanced.Power.TractorBeam.Install();
            if (ToolsActive)
            {
                Paint.PaintNet.InstallSerializers();
                Devices.Schematics.Install();
            }
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
            attempt("the photo frame", Devices.PhotoFrame.Register);
            attempt("the cosmetic cabin", Devices.CosmeticCabin.Register);
            attempt("the merchant terminal", Devices.Trader.Register);
            attempt("the station's inhabitants", Devices.Npc.Register);
            attempt("the tool rack", Devices.ToolRack.Register);
        }

        public static void OnSceneLoaded()
        {
            if (Active)
                StellarDriveDemoTFAdvanced.Common.ErrorLog.Run("Common.GameServices.Invalidate", () => Common.GameServices.Invalidate());
            if (ToolsActive)
                StellarDriveDemoTFAdvanced.Common.ErrorLog.Run("Devices.KeySeatCompat.Reset", () => Devices.KeySeatCompat.Reset());
        }

        public static void Update()
        {
            if (!Active)
                return;
            StellarDriveDemoTFAdvanced.Common.ErrorLog.Run("Cameras.CameraTablet.Update", () => Cameras.CameraTablet.Update());
            StellarDriveDemoTFAdvanced.Common.ErrorLog.Run("Common.TFNet.Update", () => Common.TFNet.Update());
            StellarDriveDemoTFAdvanced.Common.ErrorLog.Run("Common.UsableParts.Update", () => Common.UsableParts.Update());
            StellarDriveDemoTFAdvanced.Common.ErrorLog.Run("Devices.Outfits.Update", () => Devices.Outfits.Update());
            StellarDriveDemoTFAdvanced.Common.ErrorLog.Run("Devices.Teleporter.Update", () => Devices.Teleporter.Update());
            StellarDriveDemoTFAdvanced.Common.ErrorLog.Run("Devices.RadioSound.Update", () => Devices.RadioSound.Update());
            StellarDriveDemoTFAdvanced.Common.ErrorLog.Run("Devices.Radio.ServerUpdate", () => Devices.Radio.ServerUpdate());
            StellarDriveDemoTFAdvanced.Common.ErrorLog.Run("Devices.PhotoMode.Update", () => Devices.PhotoMode.Update());
            StellarDriveDemoTFAdvanced.Common.ErrorLog.Run("Devices.SelfAvatar.Update", () => Devices.SelfAvatar.Update());
            StellarDriveDemoTFAdvanced.Common.ErrorLog.Run("StellarDriveDemoTFAdvanced.Power.TractorKey.Update", () => StellarDriveDemoTFAdvanced.Power.TractorKey.Update());
            StellarDriveDemoTFAdvanced.Common.ErrorLog.Run("Devices.PhotoCamera.Update", () => Devices.PhotoCamera.Update());
            StellarDriveDemoTFAdvanced.Common.ErrorLog.Run("Devices.Seats.Update", () => Devices.Seats.Update());
            if (ToolsActive)
            {
                StellarDriveDemoTFAdvanced.Common.ErrorLog.Run("Paint.PaintNet.Update", () => Paint.PaintNet.Update());
                StellarDriveDemoTFAdvanced.Common.ErrorLog.Run("Paint.PaintHud.Update", () => Paint.PaintHud.Update());
                StellarDriveDemoTFAdvanced.Common.ErrorLog.Run("Paint.PaintZoop.Update", () => Paint.PaintZoop.Update());
                StellarDriveDemoTFAdvanced.Common.ErrorLog.Run("Devices.ThirdPerson.Update", () => Devices.ThirdPerson.Update());
                StellarDriveDemoTFAdvanced.Common.ErrorLog.Run("Devices.SchematicTablet.Update", () => Devices.SchematicTablet.Update());
                StellarDriveDemoTFAdvanced.Common.ErrorLog.Run("Devices.KeySeatCompat.Update", () => Devices.KeySeatCompat.Update());
            }
        }

        public static void LateUpdate()
        {
            if (Active)
                StellarDriveDemoTFAdvanced.Common.ErrorLog.Run("Cameras.CameraTablet.LateUpdate", () => Cameras.CameraTablet.LateUpdate());
        }

        public static void OnGUI()
        {
            if (!Active)
                return;
            StellarDriveDemoTFAdvanced.Common.ErrorLog.RunGui("Cameras.CameraTablet.Draw", () => Cameras.CameraTablet.Draw());
            StellarDriveDemoTFAdvanced.Common.ErrorLog.RunGui("TFNet.DrawVersionWarning", () => Common.TFNet.DrawVersionWarning());
            StellarDriveDemoTFAdvanced.Common.ErrorLog.RunGui("Common.UsableParts.Draw", () => Common.UsableParts.Draw());
            StellarDriveDemoTFAdvanced.Common.ErrorLog.RunGui("Devices.TeleportMenu.Draw", () => Devices.TeleportMenu.Draw());
            StellarDriveDemoTFAdvanced.Common.ErrorLog.RunGui("Devices.RadioMenu.Draw", () => Devices.RadioMenu.Draw());
            StellarDriveDemoTFAdvanced.Common.ErrorLog.RunGui("Devices.ToolRackMenu.Draw", () => Devices.ToolRackMenu.Draw());
            StellarDriveDemoTFAdvanced.Common.ErrorLog.RunGui("Power.FieldDoorMenu.Draw", () => Power.FieldDoorMenu.Draw());
            StellarDriveDemoTFAdvanced.Common.ErrorLog.RunGui("Power.FieldDoorManagerMenu.Draw", () => Power.FieldDoorManagerMenu.Draw());
            StellarDriveDemoTFAdvanced.Common.ErrorLog.RunGui("Devices.CabinMenu.Draw", () => Devices.CabinMenu.Draw());
            StellarDriveDemoTFAdvanced.Common.ErrorLog.RunGui("Devices.TeleporterMenu.Draw", () => Devices.TeleporterMenu.Draw());
            StellarDriveDemoTFAdvanced.Common.ErrorLog.RunGui("Devices.TraderMenu.Draw", () => Devices.TraderMenu.Draw());
            StellarDriveDemoTFAdvanced.Common.ErrorLog.RunGui("Devices.NpcMenu.Draw", () => Devices.NpcMenu.Draw());
            StellarDriveDemoTFAdvanced.Common.ErrorLog.RunGui("Devices.Teleporter.DrawHint", () => Devices.Teleporter.DrawHint());
            StellarDriveDemoTFAdvanced.Common.ErrorLog.RunGui("Devices.PhotoFrameMenu.Draw", () => Devices.PhotoFrameMenu.Draw());
            StellarDriveDemoTFAdvanced.Common.ErrorLog.RunGui("Construction.SignMenu.Draw", () => Construction.SignMenu.Draw());
            StellarDriveDemoTFAdvanced.Common.ErrorLog.RunGui("Devices.PhotoMode.Draw", () => Devices.PhotoMode.Draw());
            StellarDriveDemoTFAdvanced.Common.ErrorLog.RunGui("Devices.PhotoCamera.Draw", () => Devices.PhotoCamera.Draw());
            if (ToolsActive)
            {
                StellarDriveDemoTFAdvanced.Common.ErrorLog.RunGui("Paint.PaintHud.Draw", () => Paint.PaintHud.Draw());
                StellarDriveDemoTFAdvanced.Common.ErrorLog.RunGui("Paint.PaintZoop.Draw", () => Paint.PaintZoop.Draw());
                StellarDriveDemoTFAdvanced.Common.ErrorLog.RunGui("Devices.SchematicsMenu.Draw", () => Devices.SchematicsMenu.Draw());
                StellarDriveDemoTFAdvanced.Common.ErrorLog.RunGui("Devices.SchematicTablet.Draw", () => Devices.SchematicTablet.Draw());
            }
        }
    }
}

using System;
using Core.Services;
using HarmonyLib;
using Players.LocalClient;
using Players.LocalClient.Controls;
using UI.Common;
using UI.Interface.Model;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.TFItems.Common
{
    /// <summary>
    /// A mod window that takes the mouse like the game's own menus: registered with the game's
    /// menu tracker (cursor shown, Escape closes it) and with the player's controls turned off
    /// while it is open, so looking around, moving and tools do not react to the clicks.
    /// One window at a time.
    /// </summary>
    internal static class ModMenu
    {
        private static MenuHandle _handle;
        private static Action _onClose;

        public static bool IsOpen { get; private set; }

        public static void Open(Action onClose)
        {
            if (IsOpen)
                Close();
            EnsureHandle();
            IsOpen = true;
            _onClose = onClose;
            MenuTracker tracker = ServiceLocator.GetService<MenuTracker>();
            if (tracker != null)
            {
                tracker.TrackMenu(_handle);
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            ReloadControls();
        }

        public static void Close()
        {
            if (!IsOpen)
                return;
            IsOpen = false;
            Action onClose = _onClose;
            _onClose = null;
            MenuTracker tracker = ServiceLocator.GetService<MenuTracker>();
            if (tracker != null && _handle != null)
            {
                tracker.UntrackMenu(_handle);
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            ReloadControls();
            try
            {
                onClose?.Invoke();
            }
            catch (Exception e)
            {
                TFMod.Log.Error("closing a TF window: " + e);
            }
        }

        /// <summary>Leaving the world closes the window without touching the game's menus.</summary>
        public static void Reset()
        {
            IsOpen = false;
            _onClose = null;
        }

        private static void EnsureHandle()
        {
            if (_handle != null)
                return;
            var host = new GameObject("TF_ModMenu");
            UnityEngine.Object.DontDestroyOnLoad(host);
            _handle = host.AddComponent<MenuHandle>();
        }

        private static readonly System.Reflection.MethodInfo Reload = AccessTools.Method(typeof(PlayerControlsActivator), "ReloadEnabledScripts");

        private static void ReloadControls()
        {
            foreach (PlayerControlsActivator activator in UnityEngine.Object.FindObjectsByType<PlayerControlsActivator>(FindObjectsSortMode.None))
            {
                try
                {
                    Reload?.Invoke(activator, null);
                }
                catch (Exception e)
                {
                    TFMod.Log.Warning("could not refresh the player controls: " + e.Message);
                }
            }
        }

        /// <summary>The object the game's menu tracker holds while a mod window is open; Escape closes it.</summary>
        private sealed class MenuHandle : MonoBehaviour, IClosableMenu
        {
            public void Close() => ModMenu.Close();
        }
    }

    /// <summary>While a mod window is open, only the escape control stays on (it closes the window).</summary>
    [HarmonyPatch(typeof(PlayerControlsActivator), "ShouldScriptBeActivated")]
    internal static class ModMenuControlsPatch
    {
        private static void Postfix(MonoBehaviour script, ref bool __result)
        {
            if (__result && ModMenu.IsOpen && !(script is PlayerEscapeControl))
                __result = false;
        }
    }
}

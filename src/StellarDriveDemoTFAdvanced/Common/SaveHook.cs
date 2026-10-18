using System;
using System.Linq;
using HarmonyLib;
using Saving.Services;
using Saving.Settings;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Common
{
    /// <summary>
    /// Saves the mod's own world files (TF objects, paint, loot sites, stations, pirates) when the
    /// game saves, through the game saver's own OnSaveProcessStarted event rather than a Harmony
    /// patch on GameSaver.SaveGame: other mods that guard the save (and refuse foreign save
    /// hooks) leave it alone. The saver is looked up once per world.
    /// </summary>
    internal static class SaveHook
    {
        private static readonly AccessTools.FieldRef<GameSaver, SaveSettings> Settings = AccessTools.FieldRefAccess<GameSaver, SaveSettings>("saveSettings");
        private static GameSaver _hooked;
        private static float _next;

        public static void Update()
        {
            if (_hooked != null || Time.unscaledTime < _next)
                return;
            _next = Time.unscaledTime + 2f;
            GameSaver saver = UnityEngine.Object.FindFirstObjectByType<GameSaver>();
            if (saver == null)
                return;
            saver.OnSaveProcessStarted += Saving;
            _hooked = saver;
        }

        public static void Install()
        {
            // Development: DevDisable savetest makes the game save once the world is up
            DevHarness.Steps.Add((28f, "save test", () =>
            {
                if (DevHarness.Off("savetest"))
                    MelonLoader.MelonCoroutines.Start(SaveTest());
            }));
        }

        private static System.Collections.IEnumerator SaveTest()
        {
            float until = Time.unscaledTime + 30f;
            while (_hooked == null && Time.unscaledTime < until)
                yield return null;
            if (_hooked == null)
            {
                AdvancedMod.Log.Warning("dev: save test: no game saver");
                yield break;
            }
            {
                System.Reflection.MethodInfo save = AccessTools.GetDeclaredMethods(typeof(GameSaver)).Find(m => m.Name == "SaveGame" && m.IsPublic);
                object parameters = Activator.CreateInstance(save.GetParameters()[0].ParameterType);
                save.Invoke(_hooked, new[] { parameters });
                string folder = WorldDirectory();
                AdvancedMod.Log.Msg("dev: save test: the game saved; " + folder + ": " + string.Join(", ", System.IO.Directory.GetFiles(folder).Select(f => System.IO.Path.GetFileName(f) + " " + System.IO.File.GetLastWriteTime(f).ToString("HH:mm:ss"))));
            }
        }

        private static void Saving()
        {
            string directory = WorldDirectory();
            if (string.IsNullOrEmpty(directory) || !System.IO.Directory.Exists(directory))
            {
                AdvancedMod.Log.Warning("save: the world's folder was not found, the mod's world files are not saved");
                return;
            }
            Run("TF world data", () => { if (TFItems.TFItemsMod.Active) TFItems.Common.WorldData.Save(directory); });
            Run("part colors", () => { if (TFItems.TFItemsMod.ToolsActive) TFItems.Paint.PaintPersistence.Save(directory); });
            Run("loot caches", () => Worlds.LootCaches.SaveSites(directory));
            Run("stations", () => Worlds.AbandonedStations.SaveVersion(directory));
            Run("pirates", () => Combat.Pirates.Save(directory));
            Run("trader", () => { if (TFItems.TFItemsMod.Active) TFItems.Devices.TraderLedger.Save(directory); });
        }

        // The folder the game saves the current world in, found the way the game does
        private static string WorldDirectory()
        {
            try
            {
                Type load = AccessTools.TypeByName("MainGameSceneLoadData");
                object full = load == null ? null : (object)AccessTools.Property(load, "SaveFileFullName")?.GetValue(null) ?? AccessTools.Field(load, "SaveFileFullName")?.GetValue(null);
                SaveSettings settings = _hooked != null ? Settings(_hooked) : null;
                if (full is string name && settings != null && settings.TryGetSaveDirectoryAndName(name, out string directory, out _))
                    return directory;
                return full is string path ? System.IO.Path.GetDirectoryName(path) : null;
            }
            catch (Exception e)
            {
                ErrorLog.Report("save folder", e);
                return null;
            }
        }

        private static void Run(string what, Action save)
        {
            try
            {
                save();
            }
            catch (Exception e)
            {
                AdvancedMod.Log.Error($"could not save the {what}: {e}");
            }
        }
    }
}

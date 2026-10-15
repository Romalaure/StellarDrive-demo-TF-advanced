using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Managers.Server.Saving;
using Newtonsoft.Json;
using Saving.Services;
using Ships.Interface.Model.Parts;
using StellarDriveDemoTFAdvanced.TFItems.Devices;

namespace StellarDriveDemoTFAdvanced.TFItems.Common
{
    /// <summary>
    /// TF data saved with a world, in tf-data.json next to its world.json (like tf-paint.json):
    /// capsule names. The game's own save is untouched.
    /// </summary>
    internal static class WorldData
    {
        private const string FileName = "tf-data.json";

        private sealed class SaveFile
        {
            public int version = 1;
            public List<SavedName> capsules = new List<SavedName>();
        }

        private sealed class SavedName
        {
            public uint ship;
            public ushort part;
            public string name;
        }

        public static void Save(string worldDirectory)
        {
            var ships = GameServices.ShipsServer;
            var file = new SaveFile
            {
                capsules = TeleportCapsule.ServerNames
                    .Where(e => ships == null || ships.TryGetStatefulPart(new PartContext(e.Key.ShipId, e.Key.PartId), out _))
                    .Select(e => new SavedName { ship = e.Key.ShipId, part = e.Key.PartId, name = e.Value })
                    .ToList()
            };
            string path = Path.Combine(worldDirectory, FileName);
            if (file.capsules.Count == 0)
            {
                if (File.Exists(path))
                    File.Delete(path);
                return;
            }
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonConvert.SerializeObject(file, Formatting.Indented));
            if (File.Exists(path))
                File.Replace(temp, path, null);
            else
                File.Move(temp, path);
        }

        public static void Load(string worldDirectory)
        {
            TeleportCapsule.ServerNames.Clear();
            string path = Path.Combine(worldDirectory, FileName);
            if (!File.Exists(path))
                return;
            var file = JsonConvert.DeserializeObject<SaveFile>(File.ReadAllText(path));
            foreach (SavedName saved in file?.capsules ?? new List<SavedName>())
            {
                string name = TeleportCapsule.CleanName(saved?.name);
                if (name.Length > 0)
                    TeleportCapsule.ServerNames[new PartKey(saved.ship, saved.part)] = name;
            }
        }
    }

    [HarmonyPatch(typeof(GameSaver), "SaveGame", typeof(string))]
    internal static class WorldDataSavePatch
    {
        private static void Postfix(string worldDirectoryFullName)
        {
            try
            {
                WorldData.Save(worldDirectoryFullName);
            }
            catch (Exception e)
            {
                TFMod.Log.Error("could not save TF world data: " + e);
            }
        }
    }

    [HarmonyPatch(typeof(ServerStateLoader), "LoadWorldFromPathAndName")]
    internal static class WorldDataLoadPatch
    {
        private static void Prefix(string path)
        {
            try
            {
                WorldData.Load(path);
            }
            catch (Exception e)
            {
                TFMod.Log.Error("could not load TF world data: " + e);
            }
        }
    }
}

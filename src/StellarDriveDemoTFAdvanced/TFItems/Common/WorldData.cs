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
    /// capsule names, the photos hung in the frames and the station inhabitants hired. The game's own save is untouched.
    /// </summary>
    internal static class WorldData
    {
        private const string FileName = "tf-data.json";

        private sealed class SaveFile
        {
            public int version = 1;
            public List<SavedName> capsules = new List<SavedName>();
            /// <summary>Photo frames: the photo's hash as the name (the photo is in UserData/TFAdvanced/photocache).</summary>
            public List<SavedName> photos = new List<SavedName>();
            /// <summary>Station inhabitants hired (their contract signed), not yet taken away.</summary>
            public List<SavedName> contracts = new List<SavedName>();
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
                    .ToList(),
                photos = PhotoFrame.ServerFrames
                    .Where(e => ships == null || ships.TryGetStatefulPart(new PartContext(e.Key.ShipId, e.Key.PartId), out _))
                    .Select(e => new SavedName { ship = e.Key.ShipId, part = e.Key.PartId, name = e.Value })
                    .ToList(),
                contracts = Npc.Contracts
                    .Where(k => ships == null || ships.TryGetStatefulPart(new PartContext(k.ShipId, k.PartId), out _))
                    .Select(k => new SavedName { ship = k.ShipId, part = k.PartId, name = "hired" })
                    .ToList()
            };
            string path = Path.Combine(worldDirectory, FileName);
            if (file.capsules.Count == 0 && file.photos.Count == 0 && file.contracts.Count == 0)
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
            PhotoFrame.ServerFrames.Clear();
            Npc.Contracts.Clear();
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
            foreach (SavedName saved in file?.photos ?? new List<SavedName>())
            {
                if (saved?.name != null && saved.name.Length == 40)
                    PhotoFrame.ServerFrames[new PartKey(saved.ship, saved.part)] = saved.name;
            }
            foreach (SavedName saved in file?.contracts ?? new List<SavedName>())
            {
                if (saved != null)
                    Npc.Contracts.Add(new PartKey(saved.ship, saved.part));
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

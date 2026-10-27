using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Managers.Server.Saving;
using Newtonsoft.Json;
using Saving.Services;
using Ships.Interface.Model.Parts;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.TFItems.Paint
{
    /// <summary>
    /// Part colors are kept in tf-paint.json next to the world's world.json, so the game's own
    /// save stays untouched and loads fine without the mod.
    /// </summary>
    internal static class PaintPersistence
    {
        private const string FileName = "tf-paint.json";
        // 1: color only; 2: optional finish; 3: optional material
        private const int FormatVersion = 3;

        private sealed class SaveFile
        {
            public int version = FormatVersion;
            public List<SavedPaint> parts = new List<SavedPaint>();
        }

        private sealed class SavedPaint
        {
            public uint ship;
            public ushort part;
            public string color;
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public SavedFinish finish;
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public byte? material;
        }

        private sealed class SavedFinish
        {
            public byte gloss;
            public byte metal;
            public byte glow;
        }

        public static void Save(string worldDirectory)
        {
            PruneRemovedParts();

            var file = new SaveFile
            {
                parts = PaintNet.Server.All
                    .OrderBy(e => e.Key.ShipId).ThenBy(e => e.Key.PartId)
                    .Select(e => new SavedPaint
                    {
                        ship = e.Key.ShipId,
                        part = e.Key.PartId,
                        color = "#" + ColorUtility.ToHtmlStringRGB(e.Value.Color),
                        finish = e.Value.Finish.Enabled
                            ? new SavedFinish { gloss = e.Value.Finish.Gloss, metal = e.Value.Finish.Metal, glow = e.Value.Finish.Glow }
                            : null,
                        material = e.Value.Material > 0 ? e.Value.Material : (byte?)null
                    })
                    .ToList()
            };

            string path = Path.Combine(worldDirectory, FileName);
            if (file.parts.Count == 0)
            {
                if (File.Exists(path))
                    File.Delete(path);
                return;
            }

StellarDriveDemoTFAdvanced.Common.SafeFile.WriteAllText(path, JsonConvert.SerializeObject(file, Formatting.Indented));
        }

        public static void Load(string worldDirectory)
        {
            PaintNet.Server.Clear();
            string path = Path.Combine(worldDirectory, FileName);
            var file = StellarDriveDemoTFAdvanced.Common.SafeFile.ReadJson<SaveFile>(path);
            if (file?.parts == null)
                return;

            foreach (SavedPaint saved in file.parts)
            {
                if (saved?.color == null || !ColorUtility.TryParseHtmlString(saved.color, out Color color))
                    continue;
                PaintFinish finish = saved.finish == null
                    ? PaintFinish.None
                    : new PaintFinish { Enabled = true, Gloss = saved.finish.gloss, Metal = saved.finish.metal, Glow = saved.finish.glow };
                PaintNet.Server.Set(new PartKey(saved.ship, saved.part), new PaintData(color, finish, saved.material ?? 0));
            }
            TFMod.Log.Msg($"loaded {PaintNet.Server.Count} painted part(s)");
        }

        // Drops colors of parts that no longer exist (ship destroyed, part gone while unloaded, ...)
        private static void PruneRemovedParts()
        {
            var ships = GameServices.ShipsServer;
            if (ships == null)
                return;
            foreach (PartKey key in PaintNet.Server.All.Select(e => e.Key).ToList())
            {
                if (!ships.TryGetStatefulPart(new PartContext(key.ShipId, key.RealPartId), out _))
                    PaintNet.Server.Remove(key);
            }
        }
    }

    [HarmonyPatch(typeof(ServerStateLoader), "LoadWorldFromPathAndName")]
    internal static class ServerStateLoaderPatch
    {
        private static void Prefix(string path)
        {
            try
            {
                PaintPersistence.Load(path);
            }
            catch (Exception e)
            {
                TFMod.Log.Error("could not load part colors: " + e);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Core.Services;
using Core.Values;
using HarmonyLib;
using Managers.Client.Utils;
using Managers.Interface;
using Planets.EnvironmentGeneration;
using Planets.EnvironmentGeneration.ToSpawn;
using Saving.Services;
using Saving.Services.Converters;
using Saving.Services.Model.State;
using Ships;
using Ships.Services;
using StellarDriveDemoTFAdvanced.Common;
using StellarDriveDemoTFAdvanced.Items;
using UnityEngine;
using WorldObjects.Services;
using WorldTracking.Interface.Model;
using WorldTracking.Interface.Model.Spaces;
using WorldTracking.Interface.Model.Spaces.Common;
using WorldTracking.Services;
using WorldTracking.Interface.Services;
using WorldTracking.Spaces;

namespace StellarDriveDemoTFAdvanced.Worlds
{
    /// <summary>
    /// Chests inside the mod's buildings (village houses, halls, towers and temples, the city's
    /// lobbies and domes, the pirate fortress's towers and domes on the Bastion), filled with what that place and planet has: local ores, materials, and
    /// sometimes the rare finds the advanced parts are built from. A chest is a one-part ship, like
    /// any part set down on the ground: the game saves it, synchronizes it and lets players open,
    /// empty or recycle it. The host places it the first time a building's collider is loaded
    /// near a player; the sites already given a chest are kept next to the world save
    /// (tfa_sites.txt), written when the game saves, so a chest is never placed twice.
    /// </summary>
    internal static class LootCaches
    {
        private const string SitesFile = "tfa_sites.txt";
        // The game's opened loot cases: 400 + 10 x color (gray, green, blue, purple, gold) + finish
        // (standard, metallic, timber, rusted, crystal, icy, alloy)
        private const int Gray = 0, Green = 1, Blue = 2, Purple = 3, Gold = 4;
        private const int Standard = 0, Metallic = 1, Timber = 2, Rusted = 3, Crystal = 4, Icy = 5, Alloy = 6;

        // The case's color tells its rarity (gray common .. gold legendary), its finish the place
        private static int CasePart(Kind kind, string prefix, ulong planetId, int rarity)
        {
            int color = rarity, finish;
            switch (kind)
            {
                case Kind.Temple: finish = Timber; break;
                case Kind.CityCore:
                case Kind.City: finish = Alloy; break;
                case Kind.FortCore:
                case Kind.Fort: finish = Rusted; break;
                default:
                    finish = prefix.Contains("Tower") ? Rusted : prefix.Contains("Hall") ? Metallic : Timber;
                    break;
            }
            if (planetId == 105 && kind != Kind.City && kind != Kind.CityCore)
                finish = Icy;
            else if (planetId == 101 && finish == Timber)
                finish = Crystal;
            return 400 + color * 10 + finish;
        }

        private enum Kind { Village, Temple, City, CityCore, Fort, FortCore }

        private sealed class Site
        {
            public string Prefix;
            public Kind Kind;
            public Vector3[] Spots;
            /// <summary>Another mod's building: its own loot case and contents.</summary>
            public int? CasePart;
            public Func<ulong, System.Random, List<(uint Item, int Quantity)>> Loot;
        }

        private static readonly List<Site> ExtraSites = new List<Site>();

        /// <summary>
        /// A building of another mod gets chests like the mod's own: at <paramref name="spots"/> (its own
        /// frame), the game's loot case <paramref name="casePart"/> (400 + 10 x color + finish), filled by
        /// <paramref name="loot"/> (planet id, seeded random; at most 10 stacks).
        /// </summary>
        public static void AddSite(string prefix, Vector3[] spots, int casePart, Func<ulong, System.Random, List<(uint Item, int Quantity)>> loot)
        {
            ExtraSites.RemoveAll(s => s.Prefix == prefix);
            ExtraSites.Add(new Site { Prefix = prefix, Kind = Kind.Village, Spots = spots, CasePart = casePart, Loot = loot });
        }

        // Chest spots in each building's own frame (meters, on the inside floor)
        private static readonly Site[] Sites =
        {
            new Site { Prefix = "TFA_RuinHouseSmall_", Kind = Kind.Village, Spots = new[] { new Vector3(1.8f, 0.12f, -1.5f) } },
            new Site { Prefix = "TFA_RuinHouse_", Kind = Kind.Village, Spots = new[] { new Vector3(2.5f, 0.12f, -2.1f) } },
            new Site { Prefix = "TFA_RuinTower_", Kind = Kind.Village, Spots = new[] { new Vector3(-1.2f, 0.22f, -1.2f) } },
            new Site { Prefix = "TFA_RuinHall_", Kind = Kind.Village, Spots = new[] { new Vector3(2.6f, 0.12f, -5.4f), new Vector3(2.6f, 0.12f, 5.2f) } },
            new Site { Prefix = "TFA_RuinTemple_", Kind = Kind.Temple, Spots = new[] { new Vector3(0f, 0.92f, 0.6f) } },
            new Site { Prefix = "TFA_CityTowerTall", Kind = Kind.CityCore, Spots = new[] { new Vector3(-5.6f, 0.07f, -6.6f) } },
            new Site { Prefix = "TFA_CityTower", Kind = Kind.City, Spots = new[] { new Vector3(-5.6f, 0.07f, -6.6f) } },
            new Site { Prefix = "TFA_CityDome", Kind = Kind.City, Spots = new[] { new Vector3(6f, 0.12f, -5f) } },
            // The pirate fortress's towers and domes are built like the city's: same lobby, same floor
            new Site { Prefix = "TFA_FortTowerTall", Kind = Kind.FortCore, Spots = new[] { new Vector3(-5.6f, 0.07f, -6.6f), new Vector3(5.6f, 0.07f, -6.6f) } },
            new Site { Prefix = "TFA_FortTower", Kind = Kind.Fort, Spots = new[] { new Vector3(-5.6f, 0.07f, -6.6f) } },
            new Site { Prefix = "TFA_FortDome", Kind = Kind.Fort, Spots = new[] { new Vector3(6f, 0.12f, -5f) } }
        };

        // The mod's own nature camps (Kenney's Nature Kit scenes)
        static LootCaches()
        {
            AddSite("TFA_Nature_" + NatureSites.Camp, NatureSites.CampChest, 412, NatureSites.CampLoot);
        }

        private static readonly HashSet<string> Known = new HashSet<string>();
        private static readonly Queue<(string Key, ulong PlanetId, Vector3 Position, Quaternion Rotation, Site Site, int Spot)> Pending = new Queue<(string, ulong, Vector3, Quaternion, Site, int)>();
        private static string _loadedFor;

        public static void OnSceneLoaded(bool inGame)
        {
            Known.Clear();
            Pending.Clear();
            _loadedFor = null;
            if (inGame)
                LoadSites();
        }

        /// <summary>The folder of the world being played, or null.</summary>
        public static string WorldDirectory()
        {
            string save = MainGameSceneLoadData.SaveFileFullName;
            return string.IsNullOrEmpty(save) ? null : Path.GetDirectoryName(save);
        }

        private static string SitesPath()
        {
            string save = MainGameSceneLoadData.SaveFileFullName;
            if (string.IsNullOrEmpty(save))
                return null;
            string directory = Path.GetDirectoryName(save);
            return string.IsNullOrEmpty(directory) ? null : Path.Combine(directory, SitesFile);
        }

        private static void LoadSites()
        {
            string path = SitesPath();
            _loadedFor = path;
            if (path == null || !File.Exists(path))
                return;
            foreach (string line in File.ReadAllLines(path))
            {
                if (!string.IsNullOrWhiteSpace(line))
                    Known.Add(line.Trim());
            }
            AdvancedMod.Log.Msg($"caches: {Known.Count} building(s) already given a chest in this world");
        }

        /// <summary>Called after the game wrote its save: the sites list goes next to it.</summary>
        public static void SaveSites(string worldDirectory)
        {
            if (string.IsNullOrEmpty(worldDirectory) || Known.Count == 0)
                return;
            File.WriteAllLines(Path.Combine(worldDirectory, SitesFile), Known.OrderBy(k => k));
        }

        /// <summary>Host: a building's collider was just made near a player.</summary>
        public static void OnHotspotCollider(HotspotObjectToSpawn toSpawn)
        {
            if (!GameServices.IsServer || toSpawn?.Prefab == null)
                return;
            string name = toSpawn.Prefab.name;
            Site site = Sites.Concat(ExtraSites).FirstOrDefault(s => name.StartsWith(s.Prefix, StringComparison.Ordinal));
            if (site == null)
                return;
            ulong planetId = toSpawn.ObjectId.PlanetId;
            var placed = toSpawn.ObjectId.PlacedObjectId;
            for (int i = 0; i < site.Spots.Length; i++)
            {
                string key = $"{planetId}:{placed.ChunkHash}:{placed.IdWithinChunk}:{i}";
                if (!Known.Add(key))
                    continue;
                Pending.Enqueue((key, planetId, toSpawn.Transform.Position, toSpawn.Transform.Rotation, site, i));
            }
        }

        private static readonly List<uint> Placed = new List<uint>();
        private static float _nextReport;

        /// <summary>Host: places the chests found during the last physics tick.</summary>
        public static void Update()
        {
            if (DevHarness.Active && Placed.Count > 0 && Time.unscaledTime > _nextReport)
            {
                _nextReport = Time.unscaledTime + 5f;
                ShipsServerTracker tracker = ServiceLocator.GetService<ShipsServerTracker>();
                AdvancedMod.Log.Msg("dev: chests " + string.Join(", ", Placed.Take(4).Select(id => tracker.TryGetRootShip(id, out var ship) ? $"{id} space {ship.SpaceId} pos {ship.Position} " + string.Join(";", ship.GetAllStatefulParts<Ships.Interface.Model.Parts.State.StorageState>().Select(p => p.Settings.name + " " + Enumerable.Range(0, 10).Count(i => ((Ships.Interface.Model.Parts.State.StorageState)p.State).GetSlot((byte)i).HasItem) + " of 10 slots filled")) : id + " gone")));
            }
            while (Pending.Count > 0)
            {
                var item = Pending.Dequeue();
                try
                {
                    Place(item.Key, item.PlanetId, item.Position, item.Rotation, item.Site, item.Spot);
                }
                catch (Exception e)
                {
                    ErrorLog.Report("loot caches", e);
                }
            }
        }

        private static void Place(string key, ulong planetId, Vector3 position, Quaternion rotation, Site site, int spot)
        {
            ShipsServerTracker ships = ServiceLocator.GetService<ShipsServerTracker>();
            SpacesServer spaces = ServiceLocator.GetService<SpacesServer>();
            IWorldTranspose transpose = ServiceLocator.GetService<IWorldTransposeServer>();
            var ids = ServiceLocator.GetService<WorldObjectsIdsServerTracker>();
            if (ships == null || spaces == null || transpose == null || ids == null)
                return;
            var random = new System.Random(key.GetHashCode());
            int rarity = -1;

            // Surface spaces of one planet share the planet's axes: the building's rotation applies as is
            Vector3 up = rotation * Vector3.up;
            Vector3 point = position + rotation * site.Spots[spot] + up * 0.06f;
            var probe = new PlanetSurfaceSpace { PlanetId = planetId, Center = (Vector3Double)point };
            (uint spaceId, ISpace _) = spaces.GetOrCreateSpaceForPosition(probe);

            string json = site.Loot != null
                ? ChestShipJson(site.CasePart ?? 400, site.Loot(planetId, random).Take(10).ToList())
                : ChestShipJson(CasePart(site.Kind, site.Prefix, planetId, rarity = Rarity(site.Kind, site.Prefix, random)), LootFor(site.Kind, planetId, rarity, random));
            var data = JsonUtility.FromJson<ShipImportExportUtils.ShipImportExportData>(json);
            SavedShipState state = new ShipsConverter().ConvertToState(data.ships).States.Cast<SavedShipState>().Single();
            state.Id = ids.CreateUniqueIdForObject();
            state.SpaceId = spaceId;
            state.Position = (Vector3)transpose.Position(probe, spaceId, Vector3Double.Zero);
            state.Velocity = Vector3.zero;
            state.Rotation = rotation * Quaternion.Euler(0f, random.Next(0, 4) * 90f + 180f, 0f);
            state.RelativeSpaceId = spaces.GetOrCreateShipRelativeSpace(state.Id);
            ships.LoadShip(state);
            Placed.Add(state.Id);
            AdvancedMod.Log.Msg($"caches: chest {state.Id} placed in {site.Prefix.TrimEnd('_')} on planet {planetId}" + (rarity >= 0 ? $", rarity {rarity}" : ""));
        }

        // ---- Loot

        private struct Drop
        {
            public uint Item;
            public int Min, Max;
            public double Chance;
            public Drop(uint item, int min, int max, double chance) { Item = item; Min = min; Max = max; Chance = chance; }
        }

        private static IEnumerable<Drop> LootTable(Kind kind, ulong planetId)
        {
            if (kind == Kind.Fort || kind == Kind.FortCore)
            {
                // The pirates' plunder: what they took from ships and stations
                bool core = kind == Kind.FortCore;
                yield return new Drop(ModItems.NexoranAlloy, 2, 8, 0.75);
                yield return new Drop(ModItems.Titanium, 4, 12, 0.6);
                yield return new Drop(ModItems.Uranium, 2, 8, 0.5);
                yield return new Drop(ModItems.Meteorite, 2, 6, 0.45);
                yield return new Drop(ModItems.QuantumCore, 1, core ? 2 : 1, core ? 0.7 : 0.3);
                yield return new Drop(ModItems.ExoticMatter, 1, 2, core ? 0.5 : 0.2);
                yield return new Drop(ModItems.GoldBar, 1, 3, core ? 0.6 : 0.3);
                yield return new Drop(ModItems.Artifact, 1, 1, core ? 0.35 : 0.12);
                yield return new Drop(ModItems.Iron, 6, 20, 0.5);
                yield return new Drop(ModItems.Copper, 4, 12, 0.4);
                yield break;
            }
            if (kind == Kind.City || kind == Kind.CityCore)
            {
                yield return new Drop(ModItems.NexoranAlloy, 2, 6, 0.85);
                yield return new Drop(ModItems.QuantumCore, 1, 1, kind == Kind.CityCore ? 0.6 : 0.3);
                yield return new Drop(ModItems.Titanium, 3, 10, 0.6);
                yield return new Drop(ModItems.Glass, 4, 12, 0.5);
                yield return new Drop(ModItems.Copper, 3, 10, 0.4);
                yield return new Drop(ModItems.Quartz, 2, 6, 0.4);
                yield return new Drop(ModItems.StellarShard, 1, 3, 0.3);
                yield break;
            }
            yield return new Drop(ModItems.Artifact, 1, kind == Kind.Temple ? 2 : 1, kind == Kind.Temple ? 0.8 : 0.35);
            yield return new Drop(ModItems.Wood, 4, 12, 0.55);
            yield return new Drop(ModItems.Iron, 3, 10, 0.5);
            yield return new Drop(ModItems.Copper, 2, 8, 0.4);
            yield return new Drop(ModItems.Glass, 2, 6, 0.3);
            yield return new Drop(ModItems.Key, 1, 1, 0.12);
            yield return new Drop(ModItems.GoldBar, 1, 2, kind == Kind.Temple ? 0.35 : 0.08);
            // What this planet's ground holds
            switch (planetId)
            {
                case 101: yield return new Drop(ModItems.Quartz, 3, 8, 0.6); break;
                case 102: yield return new Drop(ModItems.Titanium, 3, 8, 0.65); break;
                case 103: yield return new Drop(ModItems.Titanium, 2, 6, 0.5); break;
                case 104: yield return new Drop(ModItems.Amber, 2, 5, 0.5); break;
                case 105: yield return new Drop(ModItems.Cryonite, 2, 6, 0.6); yield return new Drop(ModItems.Titanium, 2, 5, 0.35); break;
                case 106: yield return new Drop(ModItems.NexoranAlloy, 1, 4, 0.5); break;
                case 107: yield return new Drop(ModItems.Amber, 3, 8, 0.7); break;
            }
        }

        // Chances of each rarity (common, uncommon, rare, epic, legendary) by place: the richer the place, the rarer its chests
        private static readonly Dictionary<Kind, int[]> RarityWeights = new Dictionary<Kind, int[]>
        {
            [Kind.Village] = new[] { 45, 32, 16, 6, 1 },
            [Kind.Temple] = new[] { 0, 20, 40, 28, 12 },
            [Kind.City] = new[] { 10, 30, 34, 20, 6 },
            [Kind.CityCore] = new[] { 0, 8, 34, 36, 22 },
            [Kind.Fort] = new[] { 18, 34, 28, 15, 5 },
            [Kind.FortCore] = new[] { 0, 8, 30, 36, 26 }
        };

        private static int Rarity(Kind kind, string prefix, System.Random random)
        {
            int[] weights = RarityWeights[kind];
            // Big halls and domes hold better chests than small houses
            int shift = prefix.Contains("Hall") || prefix.Contains("Dome") ? 1 : 0;
            int roll = random.Next(0, weights.Sum());
            for (int i = 0; i < weights.Length; i++)
            {
                roll -= weights[i];
                if (roll < 0)
                    return Mathf.Min(Gold, i + (shift > 0 && random.NextDouble() < 0.3 ? shift : 0));
            }
            return Gray;
        }

        // The rare finds an epic or legendary chest always holds one (two) of
        private static readonly (uint Item, int Min, int Max)[] Treasures =
        {
            (ModItems.Artifact, 1, 1), (ModItems.QuantumCore, 1, 1), (ModItems.ExoticMatter, 1, 2), (ModItems.GoldBar, 1, 3), (ModItems.Meteorite, 2, 4), (ModItems.NexoranAlloy, 3, 6)
        };

        private static List<(uint Item, int Quantity)> LootFor(Kind kind, ulong planetId, int rarity, System.Random random)
        {
            var loot = new List<(uint Item, int Quantity)>();
            // Rarer chests: each drop more likely and more plentiful
            float more = 1f + 0.4f * rarity;
            foreach (Drop drop in LootTable(kind, planetId))
            {
                double chance = 1.0 - Math.Pow(1.0 - drop.Chance, 0.6 + 0.4 * rarity);
                if (loot.Count < 10 && random.NextDouble() < chance)
                    loot.Add((drop.Item, Mathf.Max(1, Mathf.RoundToInt(random.Next(drop.Min, drop.Max + 1) * more))));
            }
            for (int i = 0; i < rarity - 2 && loot.Count < 10; i++)
            {
                var treasure = Treasures[random.Next(0, Treasures.Length)];
                int index = loot.FindIndex(l => l.Item == treasure.Item);
                int count = random.Next(treasure.Min, treasure.Max + 1);
                if (index >= 0)
                    loot[index] = (treasure.Item, loot[index].Quantity + count);
                else
                    loot.Add((treasure.Item, count));
            }
            if (loot.Count == 0)
                loot.Add((ModItems.Iron, random.Next(2, 6)));
            // Best finds first in the chest
            return loot.OrderByDescending(l => Treasures.Any(t => t.Item == l.Item)).ToList();
        }

        // The game's ship save format: one chest, no hull
        private static string ChestShipJson(int casePart, List<(uint Item, int Quantity)> loot)
        {
            var slots = loot.Select(l => $"{{\"hasItem\":true,\"itemId\":{l.Item},\"quantity\":{l.Quantity}}}").ToList();
            while (slots.Count < 10)
                slots.Add("{\"hasItem\":false,\"itemId\":0,\"quantity\":0}");
            var sb = new StringBuilder();
            sb.Append("{\"rootShipId\":1,\"cables\":[],\"ships\":[{");
            sb.Append("\"id\":1,\"relativeSpaceId\":0,\"spaceId\":0,");
            sb.Append("\"position\":{\"x\":0,\"y\":0,\"z\":0},\"velocity\":{\"x\":0,\"y\":0,\"z\":0},\"rotation\":{\"x\":0,\"y\":0,\"z\":0,\"w\":1},");
            sb.Append("\"rooms\":[],\"hullParts\":[],");
            sb.Append(string.Format(CultureInfo.InvariantCulture,
                "\"statefulFloatingParts\":[{{\"id\":1,\"settingsId\":{0},\"coord\":{{\"x\":0,\"y\":0,\"z\":0}},\"forwardOrientationId\":2,\"upOrientationId\":4}}],", casePart));
            sb.Append("\"statefulHullParts\":[],\"thrustersStates\":[],");
            sb.Append("\"storageStates\":[{\"partId\":1,\"slots\":[").Append(string.Join(",", slots)).Append("]}],");
            foreach (string empty in new[] { "beaconStates", "leverStates", "warpDriveStates", "fluidTankStates", "dockingDoorStates", "targeterStates", "mathGateStates",
                         "unaryMathGateStates", "memoryRegisterStates", "constantStates", "transmitterStates", "rotorStates", "electrolyzerStates",
                         "woodThrusterStates", "woodGeneratorStates", "ethanolDistillerStates" })
                sb.Append('"').Append(empty).Append("\":[],");
            sb.Length--;
            sb.Append("}]}");
            return sb.ToString();
        }
    }

    /// <summary>Host: building colliders are made near players; that is when their chests are placed.</summary>
    [HarmonyPatch(typeof(PlanetHotspotsEnvironmentPhysics), nameof(PlanetHotspotsEnvironmentPhysics.SpawnCollider))]
    internal static class LootCachesColliderPatch
    {
        private static void Postfix(HotspotObjectToSpawn toSpawn)
        {
            try
            {
                LootCaches.OnHotspotCollider(toSpawn);
            }
            catch (Exception e)
            {
                ErrorLog.Report("loot caches", e);
            }
        }
    }
}

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Core.Utils;
using HarmonyLib;
using Planets;
using Planets.EnvironmentGeneration;
using Planets.EnvironmentGeneration.ToSpawn;
using Planets.Interface.Model;
using Planets.Network.Model;
using Planets.Objects.Common.Model;
using Planets.Terrain.Evaluation.Services;
using Planets.Terrain.Model;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Worlds
{
    /// <summary>
    /// The game works out where every point of interest's objects stand (rocks, crash sites, and
    /// the mod's ruins, villages, ore deposits, caves) again for every loaded chunk, every time it
    /// looks for colliders around a player or a ship: three or four terrain height evaluations per
    /// object. On planets with many sites (TF Gaïa: villages, ruins, caves, deposits) that took up
    /// to 4 s of every 5 s while moving around: 1 frame per second. The placement never changes
    /// for a chunk, so it is worked out once and kept; only the "destroyed" filter (mined rocks)
    /// is applied again on each call.
    /// </summary>
    [HarmonyPatch(typeof(PlanetHotspotsEnvironmentGeneration), nameof(PlanetHotspotsEnvironmentGeneration.CalculateHotspotObjects))]
    internal static class HotspotObjectsCache
    {
        private sealed class Entry
        {
            public int Count;
            public Vector3 First;
            public HotspotObjectToSpawn[] Objects;
        }

        private static readonly ConditionalWeakTable<PlanetHotspotsEnvironmentGeneration, Dictionary<ulong, Entry>> Caches =
            new ConditionalWeakTable<PlanetHotspotsEnvironmentGeneration, Dictionary<ulong, Entry>>();

        private static readonly AccessTools.FieldRef<PlanetHotspotsEnvironmentGeneration, Planet> PlanetField =
            AccessTools.FieldRefAccess<PlanetHotspotsEnvironmentGeneration, Planet>("_planet");
        private static readonly AccessTools.FieldRef<PlanetHotspotsEnvironmentGeneration, IPlanetStateReadProxy> StateField =
            AccessTools.FieldRefAccess<PlanetHotspotsEnvironmentGeneration, IPlanetStateReadProxy>("_stateReader");
        private static readonly AccessTools.FieldRef<PlanetHotspotsEnvironmentGeneration, PlanetTerrainPointCpuCalculator> TerrainField =
            AccessTools.FieldRefAccess<PlanetHotspotsEnvironmentGeneration, PlanetTerrainPointCpuCalculator>("_terrainCalculator");
        private static readonly AccessTools.FieldRef<PlanetHotspotsEnvironmentGeneration, TerrainOperationStack> StackField =
            AccessTools.FieldRefAccess<PlanetHotspotsEnvironmentGeneration, TerrainOperationStack>("_terrainOperationStack");

        // Chunks kept per planet before the cache starts over (a few thousand objects each at most)
        private const int MaxChunks = 3000;

        private static bool Prefix(PlanetHotspotsEnvironmentGeneration __instance, ulong chunkHash, PlanetHotspotsEnvironmentGeneration.HotspotToSpawn[] hotspotToSpawns, ref HotspotObjectToSpawn[] __result)
        {
            if (hotspotToSpawns == null)
                return true;
            Dictionary<ulong, Entry> cache = Caches.GetOrCreateValue(__instance);
            if (!cache.TryGetValue(chunkHash, out Entry entry) || entry.Count != hotspotToSpawns.Length
                || (hotspotToSpawns.Length > 0 && entry.First != hotspotToSpawns[0].PosOnSphere))
            {
                if (cache.Count >= MaxChunks)
                    cache.Clear();
                entry = new Entry { Count = hotspotToSpawns.Length, First = hotspotToSpawns.Length > 0 ? hotspotToSpawns[0].PosOnSphere : Vector3.zero, Objects = Place(__instance, chunkHash, hotspotToSpawns) };
                cache[chunkHash] = entry;
            }
            IPlanetStateReadProxy state = StateField(__instance);
            var alive = new List<HotspotObjectToSpawn>(entry.Objects.Length);
            foreach (HotspotObjectToSpawn spawn in entry.Objects)
            {
                if (!state.IsHotspotObjectDestroyed(spawn.ObjectId.PlacedObjectId))
                    alive.Add(spawn);
            }
            __result = alive.ToArray();
            return false;
        }

        // The game's own placement (same seed, same order, same ids), without the destroyed filter
        private static HotspotObjectToSpawn[] Place(PlanetHotspotsEnvironmentGeneration generation, ulong chunkHash, PlanetHotspotsEnvironmentGeneration.HotspotToSpawn[] hotspots)
        {
            Random.InitState(MathUtils.ToIntHash(chunkHash));
            PlanetTerrainPointCpuCalculator terrain = TerrainField(generation);
            TerrainOperationStack stack = StackField(generation);
            ulong planetId = PlanetField(generation).Id;
            uint index = 0u;
            var objects = new List<HotspotObjectToSpawn>();
            foreach (PlanetHotspotsEnvironmentGeneration.HotspotToSpawn hotspot in hotspots)
            {
                float elevation = terrain.GetElevation(stack, hotspot.PosOnSphere);
                foreach (HotspotObjectToSpawn spawn in hotspot.Placement.GenerateObjectsToSpawn(hotspot.PosOnSphere * elevation))
                {
                    spawn.ObjectId = new HotspotObjectId { PlanetId = planetId, PlacedObjectId = new ChunkObjectId(chunkHash, index) };
                    objects.Add(spawn);
                    index++;
                }
            }
            return objects.ToArray();
        }
    }
}

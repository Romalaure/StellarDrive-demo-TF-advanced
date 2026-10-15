using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Planets;
using Planets.Interface.Model;
using Planets.Network;
using Planets.Network.Model;
using Planets.Objects.Common.Model;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Worlds
{
    /// <summary>
    /// What was cut, mined or broken on the mod planets, for every player.
    /// The game keeps it in each planet's networked state (FishNet sync collections). The mod
    /// planets have no network object of their own (FishNet only spawns the scene's), so their
    /// state never reached the clients. It now rides on the demo planet's state, which FishNet
    /// does synchronize (and saves, for broken points of interest): each destroyed object of a
    /// mod planet is also written there under a tagged id, its chunk hash salted per planet (the
    /// demo planet never asks for those chunks) and the mod planet's number in the id's high byte.
    ///  - On the host, the mod planet's client side reads its own server side directly.
    ///  - On a client, the mod planet's state is read from the demo planet's tagged entries, and
    ///    the demo planet's destroy events for tagged ids are passed on to the mod planet.
    /// </summary>
    internal static class ModPlanetState
    {
        private const ulong TemplateId = PlanetFactory.TemplateId;
        private const uint IdMask = 0x00FFFFFF;

        private static readonly Dictionary<PlanetNetworkedState, ulong> PlanetOf = new Dictionary<PlanetNetworkedState, ulong>();
        private static PlanetNetworkedState _demo;
        private static float _nextDemoLookup;

        public static void Forget()
        {
            PlanetOf.Clear();
            _demo = null;
            _nextDemoLookup = 0f;
        }

        public static ulong PlanetId(PlanetNetworkedState state)
        {
            if (state == null)
                return 0;
            if (!PlanetOf.TryGetValue(state, out ulong id))
            {
                Planet planet = state.GetComponent<Planet>();
                id = planet != null ? planet.Id : 0;
                PlanetOf[state] = id;
            }
            return id;
        }

        public static bool IsMod(PlanetNetworkedState state) => PlanetFactory.IsModPlanet(PlanetId(state));

        /// <summary>The demo planet's networked state (the carrier).</summary>
        public static PlanetNetworkedState Demo
        {
            get
            {
                // Read for every tree and rock of a chunk: a missing one is searched for once per second
                if (_demo == null && Time.unscaledTime >= _nextDemoLookup)
                {
                    _nextDemoLookup = Time.unscaledTime + 1f;
                    _demo = Object.FindObjectsByType<PlanetNetworkedState>(FindObjectsSortMode.None).FirstOrDefault(s => PlanetId(s) == TemplateId);
                }
                return _demo;
            }
        }

        private static ulong Salt(ulong planetId) => 0x9E3779B97F4A7C15UL * (planetId * 2 + 1);

        public static ChunkObjectId Tag(ulong planetId, ChunkObjectId id) =>
            new ChunkObjectId(id.ChunkHash ^ Salt(planetId), (id.IdWithinChunk & IdMask) | ((uint)(planetId - 100) << 24));

        public static bool TryUntag(ChunkObjectId tagged, out ulong planetId, out ChunkObjectId id)
        {
            uint index = tagged.IdWithinChunk >> 24;
            planetId = 100 + index;
            id = default;
            if (index == 0 || !PlanetFactory.IsModPlanet(planetId))
                return false;
            id = new ChunkObjectId(tagged.ChunkHash ^ Salt(planetId), tagged.IdWithinChunk & IdMask);
            return true;
        }

        /// <summary>On a client (not the host), a mod planet's state comes from the demo planet's.</summary>
        public static bool ReadFromCarrier(PlanetNetworkedState state) => !GameServices.IsServer && IsMod(state) && Demo != null;

        public static PlanetNetworkedState ModState(ulong planetId) =>
            PlanetOf.Keys.FirstOrDefault(s => s != null && PlanetId(s) == planetId)
            ?? Object.FindObjectsByType<PlanetNetworkedState>(FindObjectsSortMode.None).FirstOrDefault(s => PlanetId(s) == planetId);
    }

    /// <summary>Host: the client side of a mod planet reads the server side.</summary>
    [HarmonyPatch]
    internal static class ModPlanetStateHostReadPatch
    {
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            foreach (string name in new[] { "IsDenseObjectDestroyed", "IsHotspotObjectDestroyed", "TryGetChunkDenseObjects", "GetDestroyedHotspotObjects",
                         "IsAsteroidResourceDestroyed", "GetDestroyedAsteroidResourcesInChunks", "IsStaticResourceDestroyed" })
                yield return AccessTools.Method(typeof(PlanetNetworkedState), name);
        }

        private static void Prefix(PlanetNetworkedState __instance, ref bool asServer)
        {
            if (!asServer && GameServices.IsServer && ModPlanetState.IsMod(__instance))
                asServer = true;
        }
    }

    /// <summary>Client: a mod planet's dense objects (trees, plants) as the demo planet's tagged entries.</summary>
    [HarmonyPatch(typeof(PlanetNetworkedState), nameof(PlanetNetworkedState.IsDenseObjectDestroyed))]
    internal static class ModPlanetDenseReadPatch
    {
        private static bool Prefix(PlanetNetworkedState __instance, ChunkObjectId objectId, ref bool __result)
        {
            if (!ModPlanetState.ReadFromCarrier(__instance))
                return true;
            __result = ModPlanetState.Demo.IsDenseObjectDestroyed(ModPlanetState.Tag(ModPlanetState.PlanetId(__instance), objectId), false);
            return false;
        }
    }

    [HarmonyPatch(typeof(PlanetNetworkedState), nameof(PlanetNetworkedState.TryGetChunkDenseObjects))]
    internal static class ModPlanetChunkReadPatch
    {
        private static bool Prefix(PlanetNetworkedState __instance, ulong chunkHash, ref HashSet<uint> destroyedIds, ref bool __result)
        {
            if (!ModPlanetState.ReadFromCarrier(__instance))
                return true;
            ulong planetId = ModPlanetState.PlanetId(__instance);
            ChunkObjectId probe = ModPlanetState.Tag(planetId, new ChunkObjectId(chunkHash, 0));
            __result = ModPlanetState.Demo.TryGetChunkDenseObjects(probe.ChunkHash, out HashSet<uint> tagged, false);
            destroyedIds = new HashSet<uint>();
            if (__result)
            {
                uint marker = (uint)(planetId - 100) << 24;
                foreach (uint id in tagged)
                {
                    if ((id & 0xFF000000) == marker)
                        destroyedIds.Add(id & 0x00FFFFFF);
                }
            }
            __result = destroyedIds.Count > 0;
            return false;
        }
    }

    [HarmonyPatch(typeof(PlanetNetworkedState), nameof(PlanetNetworkedState.IsHotspotObjectDestroyed))]
    internal static class ModPlanetHotspotReadPatch
    {
        private static bool Prefix(PlanetNetworkedState __instance, ChunkObjectId objectId, ref bool __result)
        {
            if (!ModPlanetState.ReadFromCarrier(__instance))
                return true;
            __result = ModPlanetState.Demo.IsHotspotObjectDestroyed(ModPlanetState.Tag(ModPlanetState.PlanetId(__instance), objectId), false);
            return false;
        }
    }

    [HarmonyPatch(typeof(PlanetNetworkedState), nameof(PlanetNetworkedState.GetDestroyedHotspotObjects))]
    internal static class ModPlanetHotspotListPatch
    {
        private static bool Prefix(PlanetNetworkedState __instance, ref IReadOnlyCollection<ChunkObjectId> __result)
        {
            if (!ModPlanetState.ReadFromCarrier(__instance))
                return true;
            ulong planetId = ModPlanetState.PlanetId(__instance);
            var list = new List<ChunkObjectId>();
            foreach (ChunkObjectId tagged in ModPlanetState.Demo.GetDestroyedHotspotObjects(false))
            {
                if (ModPlanetState.TryUntag(tagged, out ulong owner, out ChunkObjectId id) && owner == planetId)
                    list.Add(id);
            }
            __result = list;
            return false;
        }
    }

    /// <summary>Host: everything destroyed on a mod planet is also written, tagged, to the demo planet's synchronized state.</summary>
    [HarmonyPatch]
    internal static class ModPlanetServerWritePatch
    {
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(PlanetNetworkedState), nameof(PlanetNetworkedState.ServerMineDenseObject));
            yield return AccessTools.Method(typeof(PlanetNetworkedState), nameof(PlanetNetworkedState.ServerCollisionDestroyDenseObject));
            yield return AccessTools.Method(typeof(PlanetNetworkedState), nameof(PlanetNetworkedState.ServerDestroyHotspotObject));
        }

        private static void Postfix(PlanetNetworkedState __instance, ChunkObjectId objectId, System.Reflection.MethodBase __originalMethod)
        {
            try
            {
                if (!ModPlanetState.IsMod(__instance) || ModPlanetState.Demo == null)
                    return;
                ChunkObjectId tagged = ModPlanetState.Tag(ModPlanetState.PlanetId(__instance), objectId);
                switch (__originalMethod.Name)
                {
                    case nameof(PlanetNetworkedState.ServerMineDenseObject): ModPlanetState.Demo.ServerMineDenseObject(tagged); break;
                    case nameof(PlanetNetworkedState.ServerCollisionDestroyDenseObject): ModPlanetState.Demo.ServerCollisionDestroyDenseObject(tagged); break;
                    default: ModPlanetState.Demo.ServerDestroyHotspotObject(tagged); break;
                }
            }
            catch (System.Exception e)
            {
                ErrorLog.Report("mod planet state sync", e);
            }
        }
    }

    /// <summary>
    /// Destroy events: on the host a mod planet's client side gets its server side's events; on a
    /// client the demo planet's events for tagged ids are passed on to the mod planet they belong to.
    /// </summary>
    [HarmonyPatch(typeof(PlanetNetworkedState), "OnPlanetObjectDestroyed")]
    internal static class ModPlanetStateEventPatch
    {
        // A tagged id is a mod planet's object: the demo planet must not act on it (its own object
        // arrays do not reach that far), on the host's server side or any client
        private static bool Prefix(PlanetNetworkedState __instance, PlanetDestroyedObjectEvent destroyedEvent)
        {
            try
            {
                if (ModPlanetState.PlanetId(__instance) != PlanetFactory.TemplateId)
                    return true;
                if (destroyedEvent.ObjectId is DenseObjectId dense && ModPlanetState.TryUntag(dense.PlacedObjectId, out _, out _))
                    return false;
                if (destroyedEvent.ObjectId is HotspotObjectId hotspot && ModPlanetState.TryUntag(hotspot.PlacedObjectId, out _, out _))
                    return false;
            }
            catch (System.Exception e)
            {
                ErrorLog.Report("mod planet state", e);
            }
            return true;
        }

        private static void Postfix(PlanetNetworkedState __instance, PlanetDestroyedObjectEvent destroyedEvent, bool asServer)
        {
            try
            {
                if (GameServices.IsServer)
                {
                    if (asServer && ModPlanetState.IsMod(__instance))
                        __instance.OnObjectDestroyedClient?.Invoke(destroyedEvent);
                    return;
                }
                if (asServer || ModPlanetState.PlanetId(__instance) != PlanetFactory.TemplateId)
                    return;
                IPlanetObjectId forwarded = null;
                ulong planetId = 0;
                if (destroyedEvent.ObjectId is DenseObjectId dense && ModPlanetState.TryUntag(dense.PlacedObjectId, out planetId, out ChunkObjectId denseId))
                    forwarded = new DenseObjectId { PlanetId = planetId, PlacedObjectId = denseId };
                else if (destroyedEvent.ObjectId is HotspotObjectId hotspot && ModPlanetState.TryUntag(hotspot.PlacedObjectId, out planetId, out ChunkObjectId hotspotId))
                    forwarded = new HotspotObjectId { PlanetId = planetId, PlacedObjectId = hotspotId };
                if (forwarded == null)
                    return;
                PlanetNetworkedState target = ModPlanetState.ModState(planetId);
                target?.OnObjectDestroyedClient?.Invoke(new PlanetDestroyedObjectEvent(forwarded, destroyedEvent.IsCollisionDestruction));
            }
            catch (System.Exception e)
            {
                ErrorLog.Report("mod planet state", e);
            }
        }
    }
}

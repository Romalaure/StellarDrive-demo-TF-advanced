using System.Collections.Generic;
using HarmonyLib;
using Planets;
using Planets.Interface.Model;
using Planets.Network.Model;
using Planets.Network;
using StellarDriveDemoTFAdvanced.Common;

namespace StellarDriveDemoTFAdvanced.Worlds
{
    /// <summary>
    /// What was cut, mined or broken on the mod planets. The game keeps it in a networked state
    /// (FishNet sync collections) that the server writes and clients read. The mod planets have no
    /// network object (FishNet only spawns the scene's own), so their client side never received
    /// anything: felled trees stayed visible. On the host, the client side of a mod planet now
    /// reads the server's collections and gets the destroy events at once.
    /// </summary>
    internal static class ModPlanetState
    {
        private static readonly Dictionary<PlanetNetworkedState, bool> IsModPlanet = new Dictionary<PlanetNetworkedState, bool>();

        /// <summary>True when the client side of this planet's state must read the server's (mod planet, on the host).</summary>
        public static bool UseServerSide(PlanetNetworkedState state)
        {
            if (state == null || !GameServices.IsServer)
                return false;
            if (!IsModPlanet.TryGetValue(state, out bool mod))
            {
                Planet planet = state.GetComponent<Planet>();
                mod = planet != null && PlanetFactory.IsModPlanet(planet.Id);
                IsModPlanet[state] = mod;
            }
            return mod;
        }

        public static void Forget() => IsModPlanet.Clear();
    }

    [HarmonyPatch]
    internal static class ModPlanetStateReadPatch
    {
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            foreach (string name in new[] { "IsDenseObjectDestroyed", "IsHotspotObjectDestroyed", "TryGetChunkDenseObjects", "GetDestroyedHotspotObjects",
                         "IsAsteroidResourceDestroyed", "GetDestroyedAsteroidResourcesInChunks", "IsStaticResourceDestroyed" })
                yield return AccessTools.Method(typeof(PlanetNetworkedState), name);
        }

        private static void Prefix(PlanetNetworkedState __instance, ref bool asServer)
        {
            if (!asServer && ModPlanetState.UseServerSide(__instance))
                asServer = true;
        }
    }

    [HarmonyPatch(typeof(PlanetNetworkedState), "OnPlanetObjectDestroyed")]
    internal static class ModPlanetStateEventPatch
    {
        private static void Postfix(PlanetNetworkedState __instance, PlanetDestroyedObjectEvent destroyedEvent, bool asServer)
        {
            if (!asServer || !ModPlanetState.UseServerSide(__instance))
                return;
            try
            {
                __instance.OnObjectDestroyedClient?.Invoke(destroyedEvent);
            }
            catch (System.Exception e)
            {
                ErrorLog.Report("mod planet state", e);
            }
        }
    }
}

using Core.Services;
using FishNet;
using Planets.Interface.Services;
using Players.Services;
using Ships.Cables;
using Ships.Cables.Power;
using Ships.Services;
using UnityEngine;
using WorldTracking.Interface.Services;

namespace StellarDriveDemoTFAdvanced.Common
{
    /// <summary>
    /// Cached ServiceLocator lookups. ServiceLocator scans tagged objects on every call,
    /// so a missing service is looked up again at most once per second.
    /// </summary>
    internal static class GameServices
    {
        private static readonly CachedService<ShipsClientTracker> ShipsClientCache = new CachedService<ShipsClientTracker>();
        private static readonly CachedService<PlayersClientTracker> PlayersClientCache = new CachedService<PlayersClientTracker>();
        private static readonly CachedService<CablesClientTracker> CablesClientCache = new CachedService<CablesClientTracker>();
        private static readonly CachedService<ConnectionsClientTracker> ConnectionsClientCache = new CachedService<ConnectionsClientTracker>();
        private static readonly CachedService<PowerServerTracker> PowerServerCache = new CachedService<PowerServerTracker>();
        private static readonly CachedService<ISpacesClientProvider> SpacesClientCache = new CachedService<ISpacesClientProvider>();
        private static readonly CachedService<IWorldTransposeClient> TransposeClientCache = new CachedService<IWorldTransposeClient>();
        private static readonly CachedService<IPlanetsClientProvider> PlanetsClientCache = new CachedService<IPlanetsClientProvider>();

        public static ShipsClientTracker ShipsClient => ShipsClientCache.Get();
        public static PlayersClientTracker PlayersClient => PlayersClientCache.Get();
        public static CablesClientTracker CablesClient => CablesClientCache.Get();
        public static ConnectionsClientTracker ConnectionsClient => ConnectionsClientCache.Get();
        public static ISpacesClientProvider Spaces => SpacesClientCache.Get();
        public static IWorldTransposeClient Transpose => TransposeClientCache.Get();
        public static IPlanetsClientProvider Planets => PlanetsClientCache.Get();

        /// <summary>The host's power tracker; null on a client that does not run the server.</summary>
        public static PowerServerTracker PowerServer => IsServer ? PowerServerCache.Get() : null;

        public static bool IsServer => InstanceFinder.IsServerStarted;

        public static void Invalidate()
        {
            ShipsClientCache.Reset();
            PlayersClientCache.Reset();
            CablesClientCache.Reset();
            ConnectionsClientCache.Reset();
            PowerServerCache.Reset();
            SpacesClientCache.Reset();
            TransposeClientCache.Reset();
            PlanetsClientCache.Reset();
        }

        private sealed class CachedService<T> where T : class
        {
            private const float RetryDelay = 1f;

            private T _value;
            private float _nextLookup;

            public T Get()
            {
                // Unity objects compare equal to null once destroyed
                if (_value is Object unityObject && unityObject == null)
                    _value = null;
                if (_value == null && Time.unscaledTime >= _nextLookup)
                {
                    _value = ServiceLocator.GetService<T>();
                    _nextLookup = Time.unscaledTime + RetryDelay;
                }
                return _value;
            }

            public void Reset()
            {
                _value = null;
                _nextLookup = 0f;
            }
        }
    }
}

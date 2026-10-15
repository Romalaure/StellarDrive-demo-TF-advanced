using Core.Services;
using Players.Interface.Model;
using Players.Interface.Services;
using Players.Services;
using Ships.Cables;
using Ships.Services;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.TFItems.Common
{
    /// <summary>
    /// Cached ServiceLocator lookups. ServiceLocator scans tagged objects on every call,
    /// so a missing service is looked up again at most once per second.
    /// </summary>
    internal static class GameServices
    {
        private static readonly CachedService<ShipsClientTracker> ShipsClientCache = new CachedService<ShipsClientTracker>();
        private static readonly CachedService<ShipsServerTracker> ShipsServerCache = new CachedService<ShipsServerTracker>();
        private static readonly CachedService<PlayersServerTracker> PlayersServerCache = new CachedService<PlayersServerTracker>();
        private static readonly CachedService<IPlayerPaintToolSelectionTracker> PaintSelectionCache = new CachedService<IPlayerPaintToolSelectionTracker>();
        private static readonly CachedService<CablesClientTracker> CablesClientCache = new CachedService<CablesClientTracker>();
        private static readonly CachedService<PlayersClientTracker> PlayersClientCache = new CachedService<PlayersClientTracker>();

        public static CablesClientTracker CablesClient => CablesClientCache.Get();
        public static PlayersClientTracker PlayersClient => PlayersClientCache.Get();

        /// <summary>What the local player is doing (walking, piloting...), or Unset outside a world.</summary>
        public static PlayerState LocalState
        {
            get
            {
                PlayersClientTracker players = PlayersClient;
                return players != null && players.TryGetLocalPlayerState(out PlayerState state) ? state : default;
            }
        }

        public static ShipsClientTracker ShipsClient => ShipsClientCache.Get();
        public static ShipsServerTracker ShipsServer => ShipsServerCache.Get();
        public static PlayersServerTracker PlayersServer => PlayersServerCache.Get();
        public static IPlayerPaintToolSelectionTracker PaintSelection => PaintSelectionCache.Get();

        public static void Invalidate()
        {
            ShipsClientCache.Reset();
            ShipsServerCache.Reset();
            PlayersServerCache.Reset();
            PaintSelectionCache.Reset();
            CablesClientCache.Reset();
            PlayersClientCache.Reset();
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

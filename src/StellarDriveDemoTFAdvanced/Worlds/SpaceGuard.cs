using System;
using System.Collections.Generic;
using Core.Services;
using HarmonyLib;
using Ships.Services;
using StellarDriveDemoTFAdvanced.Common;
using WorldTracking.Spaces;

namespace StellarDriveDemoTFAdvanced.Worlds
{
    /// <summary>
    /// Keeps the host's physics running. Each tick the game lists the "active" spaces (those
    /// holding an active player, ship or object) and the next physics tick asks the spaces
    /// tracker for each of them. Another mod (StellarUnlock) adds spaces of its own to that list,
    /// and a space it adds can have been removed by the game since (a player leaving a surface
    /// spot, a ship changing space). The first lookup of the next tick (the docking magnets
    /// cache) then throws "Space id N does not exist", which aborts the whole physics tick: planet
    /// colliders, mining, felled trees, cargo, the mod's chests all stop being handled, every
    /// tick, until the game is restarted. Spaces that no longer exist are taken out of the list
    /// once everyone has added theirs, and a failing magnets update only skips the magnets.
    /// </summary>
    [HarmonyPatch(typeof(ActiveSpacesServerTracker), nameof(ActiveSpacesServerTracker.UpdateActiveSpaces))]
    internal static class ActiveSpacesFilterPatch
    {
        private static readonly AccessTools.FieldRef<ActiveSpacesServerTracker, HashSet<uint>> Set =
            AccessTools.FieldRefAccess<ActiveSpacesServerTracker, HashSet<uint>>("_activeSpaces");
        private static readonly AccessTools.FieldRef<ActiveSpacesServerTracker, List<uint>> List =
            AccessTools.FieldRefAccess<ActiveSpacesServerTracker, List<uint>>("_activeSpacesList");
        private static SpacesServer _spaces;
        private static int _removed;

        [HarmonyPriority(Priority.Last)]
        private static void Postfix(ActiveSpacesServerTracker __instance)
        {
            try
            {
                if (_spaces == null)
                    _spaces = ServiceLocator.GetService<SpacesServer>();
                if (_spaces == null)
                    return;
                List<uint> list = List(__instance);
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    uint id = list[i];
                    if (_spaces.TryGetSpaceServerState(id, out _))
                        continue;
                    list.RemoveAt(i);
                    Set(__instance).Remove(id);
                    if (_removed++ % 200 == 0)
                        AdvancedMod.Log.Msg($"spaces: removed space {id}, which no longer exists, from the active spaces ({_removed} so far)");
                }
            }
            catch (Exception e)
            {
                _spaces = null;
                ErrorLog.Report("active spaces filter", e);
            }
        }
    }

    [HarmonyPatch(typeof(SpacesMagnetsServerCache), nameof(SpacesMagnetsServerCache.UpdateCache))]
    internal static class MagnetsCacheGuardPatch
    {
        private static int _skipped;
        private static int _logged;

        private static Exception Finalizer(Exception __exception)
        {
            if (__exception != null && !(__exception is ArgumentException) && _logged++ == 0)
                AdvancedMod.Log.Error("docking magnets update failed: " + __exception);
            if (!(__exception is ArgumentException))
                return __exception;
            if (_skipped++ % 500 == 0)
                AdvancedMod.Log.Warning($"skipped a docking magnets update: {__exception.Message} ({_skipped} so far)");
            return null;
        }
    }
}

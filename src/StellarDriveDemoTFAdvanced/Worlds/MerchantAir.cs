using System.Collections.Generic;
using HarmonyLib;
using Players;
using Players.Network;
using Ships;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.State;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;
using WorldTracking.Interface.Model.Spaces;

namespace StellarDriveDemoTFAdvanced.Worlds
{
    /// <summary>
    /// The merchant freighter (Comptoir Hermès) has its own air (host): a player in one of its
    /// rooms, or anywhere within its hull, breathes without the station needing an oxygen line.
    /// </summary>
    [HarmonyPatch(typeof(PlayerStatusUpdater), "TryTakeOxygen")]
    internal static class MerchantAir
    {
        private static readonly AccessTools.FieldRef<PlayerStatusUpdater, PlayerNetworkState> State = AccessTools.FieldRefAccess<PlayerStatusUpdater, PlayerNetworkState>("_state");
        private static Bounds? _hull;
        private static bool _logged;

        private static void Postfix(PlayerStatusUpdater __instance, float amount, ref float __result)
        {
            if (__result >= amount)
                return;
            try
            {
                var physics = State(__instance).GetPhysicsState(asServer: true);
                var spaces = GameServices.SpacesServer;
                var ships = GameServices.ShipsServer;
                var transpose = GameServices.TransposeServer;
                if (spaces == null || ships == null || transpose == null)
                    return;
                if (!(spaces.GetSpace(physics.SpaceId) is ShipRelativeSpace relative) || !ships.TryGetRootShip(relative.ShipId, out IShipStateRead root))
                    return;
                if (!(ships.GetShip(root.Id) is TrackedShipServer ship))
                    return;
                StatefulPart decor = null;
                foreach (StatefulPart part in ship.StatefulParts)
                {
                    if (part?.Settings != null && part.Settings.id == WreckDecor.HermesId)
                    {
                        decor = part;
                        break;
                    }
                }
                if (decor == null)
                    return;
                Vector3 position = transpose.Position(physics.SpaceId, ship.RelativeSpaceId, physics.Position);
                bool inside = root.SampleRoomAtPosition(position).IsRoom;
                if (!inside && HullBounds() is Bounds hull)
                    inside = hull.Contains(Quaternion.Inverse(decor.LocalRotation) * (position - decor.LocalPosition));
                if (inside)
                    __result = amount;
                if (DevHarness.Active && !_logged)
                {
                    _logged = true;
                    AdvancedMod.Log.Msg($"dev: merchant air: player at {position}, room {root.SampleRoomAtPosition(position).IsRoom}, breathes {inside}");
                }
            }
            catch (System.Exception e)
            {
                ErrorLog.Report("merchant air", e);
            }
        }

        // The freighter's dressing (its outside, in the decor part's frame), a little inside its skin
        private static Bounds? HullBounds()
        {
            if (_hull.HasValue)
                return _hull;
            Dictionary<string, Mesh> meshes = ObjModels.Load("merchant_hermes");
            if (meshes == null)
                return null;
            Bounds? all = null;
            foreach (KeyValuePair<string, Mesh> group in meshes)
            {
                if (!group.Key.StartsWith("Hull"))
                    continue;
                Bounds b = group.Value.bounds;
                if (all.HasValue)
                    b.Encapsulate(all.Value);
                all = b;
            }
            if (all.HasValue)
            {
                Bounds shrunk = all.Value;
                shrunk.Expand(-1f);
                _hull = shrunk;
            }
            return _hull;
        }
    }
}

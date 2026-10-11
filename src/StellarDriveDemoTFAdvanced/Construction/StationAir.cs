using Core.Services;
using HarmonyLib;
using Players;
using Players.Network;
using Ships;
using Ships.Cables;
using Ships.Cables.Fluids;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.State;
using Ships.Services;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;
using WorldTracking.Interface.Model.Spaces;
using WorldTracking.Interface.Services;

namespace StellarDriveDemoTFAdvanced.Construction
{
    /// <summary>
    /// Air in the station modules (host). The game only fills rooms closed by hull; a player inside
    /// a module breathes when any module of the same station (the ship and everything docked to
    /// it) is piped to a line holding oxygen: the oxygen they use is taken from that line.
    /// </summary>
    [HarmonyPatch(typeof(PlayerStatusUpdater), "TryTakeOxygen")]
    internal static class StationAir
    {
        private const uint Oxygen = 108;
        private static readonly AccessTools.FieldRef<PlayerStatusUpdater, PlayerNetworkState> State = AccessTools.FieldRefAccess<PlayerStatusUpdater, PlayerNetworkState>("_state");

        private static void Postfix(PlayerStatusUpdater __instance, float amount, ref float __result)
        {
            if (__result >= amount)
                return;
            try
            {
                var physics = State(__instance).GetPhysicsState(asServer: true);
                var spaces = ServiceLocator.GetService<ISpacesServerProvider>();
                var ships = ServiceLocator.GetService<ShipsServerTracker>();
                var transpose = ServiceLocator.GetService<IWorldTransposeServer>();
                var connections = ServiceLocator.GetService<ConnectionsServerTracker>();
                if (spaces == null || ships == null || transpose == null || connections == null)
                    return;
                if (!(spaces.GetSpace(physics.SpaceId) is ShipRelativeSpace relative) || !ships.TryGetRootShip(relative.ShipId, out IShipStateRead root))
                    return;
                bool inside = false;
                FluidGroupServerState air = null;
                foreach (var data in ships.GetRootConnectedShips(root.Id))
                {
                    if (!(ships.GetShip(data.Ship.Id) is TrackedShipServer ship))
                        continue;
                    Vector3 position = transpose.Position(physics.SpaceId, ship.RelativeSpaceId, physics.Position);
                    foreach (StatefulPart part in ship.StatefulParts)
                    {
                        if (part?.Settings == null || !StationModules.ById.TryGetValue(part.Settings.id, out StationModules.Spec spec))
                            continue;
                        Vector3 local = Quaternion.Inverse(part.LocalRotation) * (position - part.LocalPosition);
                        bool here = StationModules.Interior(spec).Contains(local);
                        // An airlock open to the outside has no air
                        if (here && spec.Id == StationModules.AirlockId && AirlockColliders.OuterOpen(part.State as Ships.Interface.Model.Parts.State.LeverState))
                            return;
                        inside |= here;
                        if (air == null && connections.TryGetFluidGroupCacheFromSocket(new EntityPartSocket(ship.Id, part.Id, spec.AirSocket), out FluidGroupServerState line)
                            && line.FluidItemId == Oxygen && line.Available >= amount)
                            air = line;
                    }
                }
                if (!inside || air == null)
                    return;
                air.RemoveFluid(amount);
                __result = amount;
            }
            catch (System.Exception e)
            {
                ErrorLog.Report("station air", e);
            }
        }
    }
}

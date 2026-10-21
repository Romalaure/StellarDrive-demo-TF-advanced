using System.Linq;
using System.Reflection;
using FishNet.Connection;
using HarmonyLib;
using Managers.Interface.Model;
using Managers.Server.Services;
using Ships;
using Ships.Interface.Model.Parts;
using StellarDriveDemoTFAdvanced.TFItems.Common;

namespace StellarDriveDemoTFAdvanced.TFItems.Devices
{
    /// <summary>
    /// Host: a station's inhabitant stays at its post. Picking it up and recycling it are refused
    /// until its contract is signed (<see cref="Npc.Held"/>), and nobody places a new one from the
    /// build tool (a copied part, with Demo Build Pick): an inhabitant only ever comes from a station.
    /// </summary>
    internal static class NpcGuard
    {
        internal static MethodBase Logic(string rpc) => AccessTools.GetDeclaredMethods(typeof(ShipInteractionService))
            .FirstOrDefault(m => m.Name.StartsWith("RpcLogic___" + rpc + "_ServerRPC"));

        // Refused, the player told why
        internal static bool Allow(uint shipId, ushort partId, NetworkConnection sender)
        {
            TrackedShipServer ship = GameServices.ShipsServer?.GetShip(shipId);
            if (ship == null || !ship.TryGetStatefulPart(partId, out StatefulPart part) || !Npc.Held(ship, part))
                return true;
            Npc.Tell(sender, Npc.HeldMessage);
            return false;
        }
    }

    [HarmonyPatch]
    internal static class NpcPickUpPatch
    {
        private static MethodBase TargetMethod() => NpcGuard.Logic("PickUpPart");

        private static bool Prefix(PartInteractNetworkRequest __0, NetworkConnection __1) => NpcGuard.Allow(__0.ShipId, __0.PartId, __1);
    }

    [HarmonyPatch]
    internal static class NpcRecyclePatch
    {
        private static MethodBase TargetMethod() => NpcGuard.Logic("RemoveFloatingPart");

        private static bool Prefix(RemoveFloatingPartNetworkRequest __0, NetworkConnection __1) => NpcGuard.Allow(__0.ShipId, __0.PartId, __1);
    }

    [HarmonyPatch]
    internal static class NpcPlacePatch
    {
        private static MethodBase TargetMethod() => NpcGuard.Logic("PlaceFloatingPart");

        private static bool Prefix(PlaceFloatingPartNetworkRequest __0, NetworkConnection __1)
        {
            if (!Npc.IsNpc(__0.SettingsId))
                return true;
            Npc.Tell(__1, StellarDriveDemoTFAdvanced.Common.Loc.Pick("Un habitant ne se construit pas : embauche-le dans une station.", "An inhabitant cannot be built: hire one at a station.", "Un habitante no se construye: contrátalo en una estación."));
            return false;
        }
    }
}

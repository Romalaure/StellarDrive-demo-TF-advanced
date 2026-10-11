using System.Collections.Generic;
using HarmonyLib;
using Managers.Interface.Model;
using Managers.Interface.Services;
using Managers.Server.Services;
using Players;
using Players.Services;
using Ships;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Placement;
using Ships.Interface.Services;
using Ships.Interface.Settings;
using Ships.Services;
using Tools.Build;
using Tools.Build.Model;
using Core.Utils;
using UnityEngine;
using WorldTracking.Interface.Model;
using WorldTracking.Interface.Values;

namespace StellarDriveDemoTFAdvanced.Construction
{
    /// <summary>
    /// Station modules placed on their own, in space or on the ground: aimed at nothing, the build
    /// tool's preview stays valid (further away, the size of the module) and placing it creates a
    /// new station holding the module, on a single hull floor tile hidden under its floor. The
    /// game creates new ships this way with hull parts only (CreateShip request); the host handles
    /// the request for a module itself, with the same cost check.
    /// </summary>
    internal static class FreeModules
    {
        private const ushort IronFloor = 1;
        /// <summary>The module's origin in the new ship: the floor tile's top, like a part placed on a hull floor.</summary>
        public static readonly Vector3 ModuleCoord = new Vector3(0f, -0.46f, 0f);

        /// <summary>Development check only: no cost.</summary>
        internal static bool DevFree;

        public static bool IsModule(PartSettings settings) => settings != null && StationModules.ById.ContainsKey(settings.id);

        [HarmonyPatch(typeof(PartPreview), "IsPartPlaceableInWorld")]
        private static class Placeable
        {
            private static readonly AccessTools.FieldRef<PartPreview, PartSettings> Selected = AccessTools.FieldRefAccess<PartPreview, PartSettings>("_actualPreviewSettings");

            private static void Postfix(PartPreview __instance, ref bool __result)
            {
                if (!__result && IsModule(Selected(__instance)))
                    __result = true;
            }
        }

        // In the open, the preview stands far enough ahead for the player not to be inside the module
        [HarmonyPatch(typeof(PartPreviewDetection), "CalculateSkyPlacement")]
        private static class FarEnough
        {
            private static void Postfix(SnapDetectionState state, CameraPhysicsData cameraPhysics, ref (Vector3, bool) __result)
            {
                if (!IsModule(state.partSettings) || !StationModules.ById.TryGetValue(state.partSettings.id, out StationModules.Spec spec))
                    return;
                float reach = Mathf.Max(spec.Size.x, spec.Size.z) * 0.5f + 1.5f;
                __result.Item1 += cameraPhysics.Direction.normalized * reach;
            }
        }

        [HarmonyPatch(typeof(BuildToolLocalClient), "CreateShipWithPart")]
        private static class AskForStation
        {
            private static readonly AccessTools.FieldRef<BuildToolLocalClient, IShipInteractionNetworkService> Network =
                AccessTools.FieldRefAccess<BuildToolLocalClient, IShipInteractionNetworkService>("_interactionService");

            private static bool Prefix(BuildToolLocalClient __instance, PreviewPlacement previewPlacement)
            {
                if (!IsModule(previewPlacement.PartSettings))
                    return true;
                Quaternion rotation = previewPlacement.Rotation;
                Network(__instance)?.CreateShip(new CreateShipNetworkRequest
                {
                    PartId = previewPlacement.PartSettings.id,
                    WorldTransform = new WorldTransform
                    {
                        SpaceId = ClientOrigin.LocalPhysicsSpaceId,
                        // The ship's origin is the floor tile's middle, above the module's base
                        Position = previewPlacement.Position - rotation * ModuleCoord,
                        Rotation = rotation
                    }
                });
                return false;
            }
        }

        [HarmonyPatch(typeof(ShipInteractionService), "RpcLogic___CreateShip_ServerRPC___2476380455")]
        private static class CreateStation
        {
            private static bool Prefix(ShipInteractionService __instance, CreateShipNetworkRequest __0, FishNet.Connection.NetworkConnection __1)
            {
                var services = Traverse.Create(__instance);
                var parts = services.Field<PartSettingsProvider>("_partSettingsProvider").Value;
                PartSettings module = parts?.GetPartSettingById(__0.PartId);
                if (!IsModule(module))
                    return true;
                try
                {
                    Create(services, parts, module, __0, __1);
                }
                catch (System.Exception e)
                {
                    Common.ErrorLog.Report("module placed on its own", e);
                }
                return false;
            }

            private static void Create(Traverse services, PartSettingsProvider parts, PartSettings module, CreateShipNetworkRequest request, FishNet.Connection.NetworkConnection sender)
            {
                if (sender == null || !services.Field<WorldTracking.Interface.Services.ISpacesServerProvider>("_spaces").Value.HasSpace(request.WorldTransform.SpaceId))
                    return;
                TrackedPlayerServer player = services.Field<PlayersServerTracker>("_players").Value.GetTrackedPlayerFromConnectionId(sender.ClientId);
                if (player == null || !(parts.GetPartSettingById(IronFloor) is HullPartSettings floor))
                    return;
                if (!player.IsUsingCheatFreeBuild && !DevFree)
                {
                    var cost = module.BuildingCost;
                    if (!player.HasItemsInInventory(cost))
                    {
                        AdvancedMod.Log.Msg($"{module.fullLabel} placed on its own: not enough materials");
                        return;
                    }
                    player.RemoveItemsFromInventory(cost);
                }
                var tile = services.Method("GetInitialHullPart", floor).GetValue<HullPart>();
                uint id = services.Field<ShipCreationService>("_shipCreationService").Value.CreateShip(tile, request.WorldTransform);
                TrackedShipServer ship = services.Field<ShipsServerTracker>("_ships").Value.GetShip(id);
                ship?.AddStatefulPart(module, new FloatingPartPlacement(ModuleCoord, PartOrientation.Forward, PartOrientation.Up));
                AdvancedMod.Log.Msg($"station {id} created around a {module.fullLabel}");
            }
        }
    }
}

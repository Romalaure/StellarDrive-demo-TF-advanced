using System.Linq;
using FishNet.Connection;
using Items.Model;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using Tools.PaintTool;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.TFItems.Paint
{
    /// <summary>
    /// The demo ships the paint tool but never hands it out (the default toolbar only holds the build
    /// tool, recycler and mining laser). The TF tool rack asks the host for one.
    /// </summary>
    internal static class PaintToolGiver
    {
        public static void ServerGive(NetworkConnection sender)
        {
            if (sender == null)
                return;
            var players = GameServices.PlayersServer;
            var player = players?.GetTrackedPlayerFromConnectionId(sender.ClientId);
            if (player == null)
                return;

            ItemSettings paintTool = FindPaintToolItem();
            if (paintTool == null)
            {
                TFMod.Log.Warning("no paint tool item found in this game version");
                return;
            }

            var drop = new ItemDrop { ItemId = paintTool.id, Quantity = 1 };
            if (player.HasItemsInInventory(new[] { drop }))
                return;
            player.AddItemToInventory(drop);
            TFMod.Log.Msg($"gave the paint tool (item {paintTool.id}) to {player.Name}");
        }

        public static ItemSettings FindPaintToolItem()
        {
            return Resources.FindObjectsOfTypeAll<ItemSettings>().FirstOrDefault(item =>
                item != null && item.toolSettings != null && item.toolSettings.prefab != null &&
                item.toolSettings.prefab.GetComponentInChildren<PaintTool>(true) != null);
        }
    }
}

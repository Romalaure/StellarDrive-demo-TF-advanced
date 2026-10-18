using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Parts
{
    /// <summary>
    /// A tab panel with more rows, or longer rows, than the build menu's panel has room for is
    /// scaled down so all its parts fit and stay clickable. The room is measured: a tab that fits
    /// keeps its parts at the game's size.
    /// </summary>
    [HarmonyPatch(typeof(UI.BuildMenu.BuildMenu), "LoadTabPanel")]
    internal static class BuildMenuFitPatch
    {
        private const int FittingRows = 7;
        private const int FittingColumns = 8;
        private static readonly AccessTools.FieldRef<UI.BuildMenu.BuildMenu, Transform> Container =
            AccessTools.FieldRefAccess<UI.BuildMenu.BuildMenu, Transform>("tabPanelContainer");

        private static void Postfix(UI.BuildMenu.BuildMenu __instance)
        {
            Transform container = Container(__instance);
            if (container == null || container.childCount == 0)
                return;
            Transform panel = container.GetChild(container.childCount - 1);
            var rows = panel.Cast<Transform>().Where(r => r.GetComponentInChildren<UI.BuildMenu.BuildPartButton>(true) != null).ToList();
            if (rows.Count == 0)
                return;
            int columns = rows.Max(r => r.GetComponentsInChildren<UI.BuildMenu.BuildPartButton>(true).Length);
            float scale = Mathf.Min(1f, Measured(container, panel, rows, columns));
            if (scale < 0.999f)
            {
                var rect = (RectTransform)panel;
                rect.pivot = new Vector2(rect.pivot.x, 1f);
                panel.localScale = Vector3.one * scale;
            }
            else if (panel.localScale.x < 0.999f)
                panel.localScale = Vector3.one;
        }

        // The room the panel really has against what its rows need: parts keep their full size
        // whenever they fit (the menu's panel holds far more than three rows nowadays)
        private static float Measured(Transform container, Transform panel, System.Collections.Generic.List<Transform> rows, int columns)
        {
            var room = ((RectTransform)container).rect.size;
            var button = rows[0].GetComponentInChildren<UI.BuildMenu.BuildPartButton>(true)?.transform as RectTransform;
            Vector2 cell = button != null ? button.rect.size : Vector2.zero;
            if (room.x < 50f || room.y < 50f || cell.x < 5f || cell.y < 5f)
                return Mathf.Min((float)FittingRows / rows.Count, (float)FittingColumns / columns);
            // A little spacing around each part, as the game lays them out
            float height = rows.Count * cell.y * 1.08f, width = columns * cell.x * 1.08f;
            float fit = Mathf.Min(room.y / height, room.x / width);
            if (Common.DevHarness.Active)
                AdvancedMod.Log.Msg($"dev: build menu fit: {rows.Count} rows x {columns}, cell {cell}, room {room}, scale {Mathf.Min(1f, fit):0.00}");
            return fit;
        }
    }
}

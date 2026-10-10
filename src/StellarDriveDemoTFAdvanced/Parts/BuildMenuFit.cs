using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Parts
{
    /// <summary>
    /// The game's build menu shows three rows of eight parts and does not scroll: anything beyond
    /// is drawn outside its panel. A tab panel with more rows, or longer rows, is scaled down so
    /// all its parts fit and stay clickable.
    /// </summary>
    [HarmonyPatch(typeof(UI.BuildMenu.BuildMenu), "LoadTabPanel")]
    internal static class BuildMenuFitPatch
    {
        private const int FittingRows = 3;
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
            float scale = Mathf.Min(1f, (float)FittingRows / rows.Count, (float)FittingColumns / columns);
            if (scale < 0.999f)
            {
                var rect = (RectTransform)panel;
                rect.pivot = new Vector2(rect.pivot.x, 1f);
                panel.localScale = Vector3.one * scale;
            }
        }
    }
}

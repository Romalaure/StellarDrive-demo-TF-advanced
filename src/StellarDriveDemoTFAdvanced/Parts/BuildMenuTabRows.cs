using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace StellarDriveDemoTFAdvanced.Parts
{
    /// <summary>
    /// The build menu's tab bar on two rows. With the tabs of several mods in one row, SDModKit
    /// shrinks every label until it fits, and the labels become unreadable. Here half the tabs go
    /// on a second row above the first (the bar's frame has room for it), at their normal size;
    /// a row still too wide is scaled down as a whole.
    /// </summary>
    [HarmonyPatch(typeof(UI.BuildMenu.BuildMenu), "Start")]
    [HarmonyPriority(Priority.Last)]
    internal static class BuildMenuTabRowsPatch
    {
        private const int OneRowMax = 8;
        private static readonly AccessTools.FieldRef<UI.BuildMenu.BuildMenu, Transform> Buttons =
            AccessTools.FieldRefAccess<UI.BuildMenu.BuildMenu, Transform>("tabButtonsContainer");

        private static void Postfix(UI.BuildMenu.BuildMenu __instance)
        {
            try
            {
                Split(__instance);
            }
            catch (System.Exception e)
            {
                Common.ErrorLog.Report("build menu tab rows", e);
            }
        }

        private static void Split(UI.BuildMenu.BuildMenu menu)
        {
            var tabs = Buttons(menu) as RectTransform;
            if (tabs == null)
                return;
            var buttons = tabs.Cast<Transform>().Where(t => t.GetComponent<UI.BuildMenu.TabButton>() != null).ToList();
            if (buttons.Count <= OneRowMax)
                return;
            // SDModKit's fitter would keep shrinking the labels: the rows fit themselves instead
            foreach (MonoBehaviour fitter in tabs.GetComponents<MonoBehaviour>().Where(b => b.GetType().Name == "TabFitter").ToList())
                Object.DestroyImmediate(fitter);

            var rowObject = new GameObject("TFA_TabsRow2", typeof(RectTransform));
            var row = (RectTransform)rowObject.transform;
            row.SetParent(tabs.parent, false);
            row.anchorMin = tabs.anchorMin;
            row.anchorMax = tabs.anchorMax;
            row.pivot = tabs.pivot;
            row.sizeDelta = tabs.sizeDelta;
            row.anchoredPosition = tabs.anchoredPosition + new Vector2(0f, tabs.rect.height + 8f);
            var source = tabs.GetComponent<HorizontalLayoutGroup>();
            var layout = rowObject.AddComponent<HorizontalLayoutGroup>();
            if (source != null)
            {
                layout.spacing = source.spacing;
                layout.padding = new RectOffset(source.padding.left, source.padding.right, source.padding.top, source.padding.bottom);
                layout.childAlignment = source.childAlignment;
                layout.childControlWidth = source.childControlWidth;
                layout.childControlHeight = source.childControlHeight;
                layout.childForceExpandWidth = source.childForceExpandWidth;
                layout.childForceExpandHeight = source.childForceExpandHeight;
                layout.childScaleWidth = source.childScaleWidth;
                layout.childScaleHeight = source.childScaleHeight;
            }
            // Each tab as wide as its own label
            foreach (Transform button in buttons)
                LabelWidth(button);
            // The first half of the tabs on the upper row, in their order
            int upper = (buttons.Count + 1) / 2;
            for (int i = 0; i < upper; i++)
                buttons[i].SetParent(row, false);
            float room = VisibleWidth(menu, tabs);
            Fit(tabs, room);
            Fit(row, room);
            Center(menu, tabs);
            Center(menu, row);
        }

        // The row as wide as the menu's frame and centered on it; its layout centers the tabs inside
        private static void Center(UI.BuildMenu.BuildMenu menu, RectTransform row)
        {
            Image image = menu.GetComponentInChildren<Image>();
            if (image == null || row.parent == null)
                return;
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
            {
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.childForceExpandWidth = false;
            }
            var corners = new Vector3[4];
            image.rectTransform.GetWorldCorners(corners);
            float frameLeft = corners[0].x, frameRight = corners[2].x;
            float scale = row.lossyScale.x;
            if (scale <= 0f)
                return;
            row.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, (frameRight - frameLeft) / scale);
            row.GetWorldCorners(corners);
            float parentScale = row.parent.lossyScale.x;
            row.anchoredPosition += new Vector2(((frameLeft + frameRight) * 0.5f - (corners[0].x + corners[2].x) * 0.5f) / parentScale, 0f);
            LayoutRebuilder.ForceRebuildLayoutImmediate(row);
        }

        // The width inside the menu's frame (narrower than the bar), in the bar's units
        private static float VisibleWidth(UI.BuildMenu.BuildMenu menu, RectTransform tabs)
        {
            Image image = menu.GetComponentInChildren<Image>();
            float scale = tabs.lossyScale.x;
            if (image == null || scale <= 0f)
                return tabs.rect.width;
            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4];
            image.rectTransform.GetWorldCorners(corners);
            float width = (corners[2].x - corners[0].x) / scale - 48f;
            return width > 0f ? Mathf.Min(width, tabs.rect.width) : tabs.rect.width;
        }

        private static float LabelWidth(Transform button)
        {
            var text = button.GetComponentInChildren<TMPro.TMP_Text>(true);
            float width = text != null ? text.GetPreferredValues(text.text, float.PositiveInfinity, float.PositiveInfinity).x + 110f : 200f;
            var element = button.GetComponent<LayoutElement>() ?? button.gameObject.AddComponent<LayoutElement>();
            element.minWidth = width;
            element.preferredWidth = width;
            element.flexibleWidth = 0f;
            foreach (ContentSizeFitter fitter in button.GetComponentsInChildren<ContentSizeFitter>(true))
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            ((RectTransform)button).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            return width;
        }

        // A row wider than the bar is scaled down as a whole, from its center
        private static void Fit(RectTransform row, float room)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(row);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            float wanted = 0f;
            int count = 0;
            foreach (Transform child in row)
            {
                if (!child.gameObject.activeSelf)
                    continue;
                var element = child.GetComponent<LayoutElement>();
                wanted += element != null && element.preferredWidth > 0f ? element.preferredWidth : ((RectTransform)child).rect.width;
                count++;
            }
            if (layout != null)
                wanted += layout.spacing * Mathf.Max(0, count - 1) + layout.padding.left + layout.padding.right;
            if (wanted > room && wanted > 0f)
                row.localScale = new Vector3(room / wanted, room / wanted, 1f);
        }
    }
}

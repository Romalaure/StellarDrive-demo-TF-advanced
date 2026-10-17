using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using HarmonyLib;
using Research.Interface.Services;
using Ships.Interface.Services;
using Ships.Interface.Settings;
using StellarDriveDemoTFAdvanced.Common;
using UI.BuildMenu;
using UnityEngine;
using UnityEngine.InputSystem;
using DeviceUi = StellarDriveDemoTFAdvanced.TFItems.Devices.DeviceUi;

namespace StellarDriveDemoTFAdvanced.Parts
{
    /// <summary>
    /// A search bar in the build menu: typing a few letters shows, in place of the tab, every part
    /// of every tab whose name holds them (accents and case ignored), as the game's own buttons
    /// (click to build, hover for the info). Emptying it brings the tab back. While it is being
    /// typed in, the player's controls and the menu's tab switching keys are off.
    /// </summary>
    internal sealed class BuildSearch : MonoBehaviour
    {
        private const string FieldName = "TFA_BuildSearch";
        private const int Columns = 8, FittingRows = 3, MaxResults = 48;
        private const float Room = 46f;

        private static readonly AccessTools.FieldRef<BuildMenu, BuildMenuTabConfig[]> Tabs = AccessTools.FieldRefAccess<BuildMenu, BuildMenuTabConfig[]>("tabsConfig");
        private static readonly AccessTools.FieldRef<BuildMenu, Transform> Container = AccessTools.FieldRefAccess<BuildMenu, Transform>("tabPanelContainer");
        private static readonly AccessTools.FieldRef<BuildMenu, GameObject> PanelPrefab = AccessTools.FieldRefAccess<BuildMenu, GameObject>("tabPanelPrefab");
        private static readonly AccessTools.FieldRef<BuildMenu, GameObject> RowPrefab = AccessTools.FieldRefAccess<BuildMenu, GameObject>("rowPrefab");
        private static readonly AccessTools.FieldRef<BuildMenu, BuildPartButton> ButtonPrefab = AccessTools.FieldRefAccess<BuildMenu, BuildPartButton>("buttonPrefab");
        private static readonly AccessTools.FieldRef<BuildMenu, IResearchProvider> Research = AccessTools.FieldRefAccess<BuildMenu, IResearchProvider>("researchProvider");
        private static readonly AccessTools.FieldRef<BuildMenu, IPartThumbnailProvider> Thumbnails = AccessTools.FieldRefAccess<BuildMenu, IPartThumbnailProvider>("_thumbnailProvider");
        private static readonly AccessTools.FieldRef<BuildMenu, InputActionReference> SwitchTab = AccessTools.FieldRefAccess<BuildMenu, InputActionReference>("switchTabAction");
        private static readonly System.Reflection.MethodInfo ShowTab = AccessTools.Method(typeof(BuildMenu), "UpdateSelectionStatus");
        private static readonly System.Reflection.MethodInfo Clear = AccessTools.Method(typeof(BuildMenu), "ClearPanelContent");

        /// <summary>Whether the search bar has the keyboard (the player's controls are then off).</summary>
        public static bool Typing { get; private set; }

        private BuildMenu _menu;
        private string _query = "", _shown = "";
        private bool _moved, _focus;
        private GUIStyle _field, _hint;

        public static void Attach(BuildMenu menu)
        {
            if (menu.GetComponent<BuildSearch>() == null)
                menu.gameObject.AddComponent<BuildSearch>()._menu = menu;
        }

        /// <summary>Development: types a search in the open menu.</summary>
        public static void DevSearch(BuildMenu menu, string query)
        {
            BuildSearch search = menu != null ? menu.GetComponent<BuildSearch>() : null;
            if (search != null)
                search._query = query;
        }

        private void OnDisable()
        {
            _query = _shown = "";
            SetTyping(false);
        }

        private void OnGUI()
        {
            if (_menu == null || !(Container(_menu) is RectTransform container))
                return;
            // Room for the bar above the parts, once
            if (!_moved)
            {
                _moved = true;
                container.offsetMax = new Vector2(container.offsetMax.x, container.offsetMax.y - Room);
            }
            var corners = new Vector3[4];
            container.GetWorldCorners(corners);
            float left = corners[1].x, top = Screen.height - corners[1].y;
            float width = Mathf.Min(420f * Scale, corners[2].x - corners[1].x);
            var rect = new Rect(left, top - Room * Scale * 0.9f, width, 32f * Scale);
            if (_field == null)
            {
                _field = new GUIStyle(DeviceUi.Field);
                _hint = new GUIStyle(DeviceUi.Muted) { alignment = TextAnchor.MiddleLeft };
            }
            _field.fontSize = Mathf.RoundToInt(15f * Scale);
            _hint.fontSize = Mathf.RoundToInt(14f * Scale);
            GUI.SetNextControlName(FieldName);
            _query = GUI.TextField(rect, _query ?? "", 40, _field);
            if (_query.Length == 0 && GUI.GetNameOfFocusedControl() != FieldName)
                GUI.Label(new Rect(rect.x + 12f * Scale, rect.y, rect.width, rect.height), Loc.Pick("Rechercher une pièce…", "Search a part…", "Buscar una pieza…"), _hint);
            if (Event.current.type == EventType.Repaint)
                SetTyping(GUI.GetNameOfFocusedControl() == FieldName);
            // Escape or Enter leaves the bar (the menu stays open)
            if (Typing && Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Escape || Event.current.keyCode == KeyCode.Return))
            {
                GUI.FocusControl(null);
                Event.current.Use();
            }
            if (_query != _shown)
            {
                _shown = _query;
                Apply();
            }
        }

        private static float Scale => Mathf.Clamp(Screen.height / 1080f, 0.7f, 2.5f);

        private void SetTyping(bool typing)
        {
            if (typing == Typing)
                return;
            Typing = typing;
            InputAction action = _menu != null ? SwitchTab(_menu)?.action : null;
            if (action != null)
            {
                if (typing)
                    action.Disable();
                else
                    action.Enable();
            }
            TFItems.Common.ModMenu.RefreshControls();
        }

        private void Apply()
        {
            try
            {
                string query = Fold(_query.Trim());
                if (query.Length == 0)
                {
                    ShowTab.Invoke(_menu, null);
                    return;
                }
                IResearchProvider research = Research(_menu);
                var found = new List<PartSettings>();
                var seen = new HashSet<ushort>();
                foreach (BuildMenuTabConfig tab in Tabs(_menu) ?? new BuildMenuTabConfig[0])
                {
                    foreach (BuildMenuTabConfig.BuildMenuRow row in tab.rows ?? new BuildMenuTabConfig.BuildMenuRow[0])
                    {
                        foreach (PartSettings part in row.parts ?? new PartSettings[0])
                        {
                            if (part == null || !seen.Add(part.id) || research != null && !research.IsPartUnlocked(part))
                                continue;
                            if (Fold(part.fullLabel ?? part.name ?? "").Contains(query) || Fold(tab.name ?? "").Contains(query))
                                found.Add(part);
                        }
                    }
                }
                // The best matches first: the name starting with the search
                found = found.OrderBy(p => Fold(p.fullLabel ?? "").StartsWith(query) ? 0 : 1).ThenBy(p => p.fullLabel).Take(MaxResults).ToList();
                Clear.Invoke(_menu, null);
                Transform container = Container(_menu);
                GameObject panel = Instantiate(PanelPrefab(_menu), container);
                BuildPartButton prefab = ButtonPrefab(_menu);
                float height = ((RectTransform)prefab.transform).rect.height;
                IPartThumbnailProvider thumbnails = Thumbnails(_menu);
                for (int i = 0; i < found.Count; i += Columns)
                {
                    var row = (RectTransform)Instantiate(RowPrefab(_menu), panel.transform).transform;
                    row.sizeDelta = new Vector2(100f, height);
                    foreach (PartSettings part in found.Skip(i).Take(Columns))
                    {
                        BuildPartButton button = Instantiate(prefab, row);
                        button.partSettings = part;
                        if (thumbnails != null)
                            button.GetComponent<UI.Common.ButtonBase>()?.SetThumbnail(thumbnails.GetSmallThumbnail(part.id));
                    }
                }
                int rows = (found.Count + Columns - 1) / Columns;
                if (rows > FittingRows)
                {
                    var rect = (RectTransform)panel.transform;
                    rect.pivot = new Vector2(rect.pivot.x, 1f);
                    panel.transform.localScale = Vector3.one * ((float)FittingRows / rows);
                }
            }
            catch (Exception e)
            {
                ErrorLog.Report("build menu search", e);
            }
        }

        /// <summary>Lower case without accents: "Générateur" → "generateur".</summary>
        public static string Fold(string text)
        {
            string decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var folded = new StringBuilder(decomposed.Length);
            foreach (char c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    folded.Append(c);
            }
            return folded.ToString();
        }
    }

    [HarmonyPatch(typeof(BuildMenu), "Start")]
    internal static class BuildSearchPatch
    {
        private static void Postfix(BuildMenu __instance) => BuildSearch.Attach(__instance);
    }
}

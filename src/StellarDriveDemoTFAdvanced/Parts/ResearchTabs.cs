using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Research.Interface.Values;
using StellarDriveDemoTFAdvanced.Common;
using TMPro;
using UI.Research;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace StellarDriveDemoTFAdvanced.Parts
{
    /// <summary>
    /// Tabs at the top of the research table: the game's techs, the mod's, the other mods' (when
    /// there are any) or all of them as before. The mod's and the other mods' techs are laid out
    /// tight in their tab, by theme (lines only between the tab's own techs).
    /// The Upgrade Table mod's menu (the same window) is left alone.
    /// </summary>
    internal static class ResearchTabs
    {
        internal enum Tab { Game, Mod, Others, All }

        private static Tab _current = Tab.Game;

        private static readonly AccessTools.FieldRef<ResearchCanvas, List<ResearchSelectButton>> Buttons = AccessTools.FieldRefAccess<ResearchCanvas, List<ResearchSelectButton>>("_buttons");
        private static readonly AccessTools.FieldRef<ResearchCanvas, RectTransform> Container = AccessTools.FieldRefAccess<ResearchCanvas, RectTransform>("buttonsContainer");
        private static readonly AccessTools.FieldRef<ResearchCanvas, ResearchInfo> Info = AccessTools.FieldRefAccess<ResearchCanvas, ResearchInfo>("info");
        private static readonly AccessTools.FieldRef<ResearchInfo, Button> InfoButton = AccessTools.FieldRefAccess<ResearchInfo, Button>("researchButton");

        private static string Name(Tab tab)
        {
            switch (tab)
            {
                case Tab.Game: return Loc.Pick("Jeu de base", "Base game", "Juego base");
                case Tab.Mod: return "TF Advanced";
                case Tab.Others: return Loc.Pick("Autres mods", "Other mods", "Otros mods");
                default: return Loc.Pick("Tout", "All", "Todo");
            }
        }

        private static bool IsUpgradeMenu(ResearchCanvas canvas) =>
            canvas.GetComponentsInParent<MonoBehaviour>(true).Any(c => c != null && c.GetType().Assembly.GetName().Name == "UpgradeTable");

        /// <summary>Whose tech it is: the mod's, the game's (an asset of the game) or another mod's (made while the game runs).</summary>
        internal static Tab Owner(ResearchSettings tech)
        {
            if (ModResearch.IsModTech(tech.id))
                return Tab.Mod;
            return tech.GetInstanceID() > 0 ? Tab.Game : Tab.Others;
        }

        [HarmonyPatch(typeof(ResearchCanvas), "GenerateButtons")]
        private static class FilterPatch
        {
            private static void Postfix(ResearchCanvas __instance)
            {
                try
                {
                    if (_current != Tab.All && !IsUpgradeMenu(__instance))
                        Filter(__instance);
                }
                catch (System.Exception e)
                {
                    ErrorLog.Report("research tabs", e);
                }
            }
        }

        [HarmonyPatch(typeof(ResearchCanvas), "Awake")]
        private static class BarPatch
        {
            private static void Postfix(ResearchCanvas __instance)
            {
                try
                {
                    if (!IsUpgradeMenu(__instance))
                        AddBar(__instance);
                }
                catch (System.Exception e)
                {
                    ErrorLog.Report("research tabs bar", e);
                }
            }
        }

        private static void Filter(ResearchCanvas canvas)
        {
            List<ResearchSelectButton> buttons = Buttons(canvas);
            if (buttons == null)
                return;
            var inTab = new HashSet<ResearchSettings>(buttons.Where(b => b.research != null && Owner(b.research) == _current).Select(b => b.research));
            foreach (ResearchSelectButton button in buttons.ToList())
            {
                if (button.research != null && inTab.Contains(button.research))
                    continue;
                Object.Destroy(button.gameObject);
                buttons.Remove(button);
            }
            int maxX = 0;
            Dictionary<ResearchSettings, Vector2Int> cells = _current == Tab.Game ? null : Layout(inTab);
            foreach (ResearchSelectButton button in buttons)
            {
                Vector2Int cell = cells != null && cells.TryGetValue(button.research, out Vector2Int placed) ? placed : button.research.positionInTechTree;
                button.transform.localPosition = new Vector3(330f + 500f * cell.x, -810f + 260f * cell.y);
                maxX = Mathf.Max(maxX, cell.x);
            }
            RectTransform container = Container(canvas);
            container.sizeDelta = new Vector2(770f + maxX * 500f, container.sizeDelta.y);
        }

        // ---- Layout of a tab: each tech right of its requirements in the tab (the game's techs it
        // branches from are not shown) in the free cell nearest its wished one where no line crosses a box

        private const int MaxRow = 6;

        private static Dictionary<ResearchSettings, Vector2Int> Layout(HashSet<ResearchSettings> techs)
        {
            var wish = new Dictionary<ResearchSettings, Vector2Int>();
            foreach (ResearchSettings tech in techs)
                wish[tech] = ModResearch.TabCell(tech.id) ?? new Vector2Int(Depth(tech, techs) - 1, tech.positionInTechTree.y);
            var cells = new Dictionary<ResearchSettings, Vector2Int>();
            foreach (ResearchSettings tech in techs.OrderBy(t => Depth(t, techs)).ThenBy(t => wish[t].y).ThenBy(t => t.id))
            {
                int minX = tech.requirements.Where(r => r != null && cells.ContainsKey(r)).Select(r => cells[r].x + 1).DefaultIfEmpty(0).Max();
                Vector2Int best = new Vector2Int(minX, 0);
                float bestScore = float.MaxValue;
                for (int x = minX; x <= minX + 6; x++)
                {
                    for (int y = 0; y <= MaxRow; y++)
                    {
                        var cell = new Vector2Int(x, y);
                        if (cells.ContainsValue(cell))
                            continue;
                        float score = Crossings(tech, cell, cells) * 1000f + Mathf.Abs(x - wish[tech].x) * 1.5f + Mathf.Abs(y - wish[tech].y);
                        if (score < bestScore)
                        {
                            bestScore = score;
                            best = cell;
                        }
                    }
                }
                cells[tech] = best;
            }
            return cells;
        }

        // How many requirements deep a tech is within its tab
        private static int Depth(ResearchSettings tech, HashSet<ResearchSettings> techs, int guard = 0)
        {
            if (guard > 20 || tech.requirements == null)
                return 1;
            return 1 + tech.requirements.Where(r => r != null && techs.Contains(r)).Select(r => Depth(r, techs, guard + 1)).DefaultIfEmpty(0).Max();
        }

        private static int Crossings(ResearchSettings tech, Vector2Int cell, Dictionary<ResearchSettings, Vector2Int> cells)
        {
            int count = 0;
            foreach (ResearchSettings requirement in tech.requirements.Where(r => r != null && cells.ContainsKey(r)))
            foreach (KeyValuePair<ResearchSettings, Vector2Int> other in cells)
                if (other.Key != requirement && ModResearch.LineCrosses(cells[requirement], cell, other.Value))
                    count++;
            foreach (KeyValuePair<ResearchSettings, Vector2Int> other in cells)
            foreach (ResearchSettings requirement in other.Key.requirements ?? new ResearchSettings[0])
                if (requirement != null && cells.ContainsKey(requirement) && ModResearch.LineCrosses(cells[requirement], other.Value, cell))
                    count++;
            return count;
        }

        // ---- The tab buttons: copies of the window's own Research button

        private static void AddBar(ResearchCanvas canvas)
        {
            ResearchInfo info = Info(canvas);
            Button source = info != null ? InfoButton(info) : null;
            if (source == null)
                return;
            bool others = canvas.GetComponentInParent<Canvas>() != null && Resources.FindObjectsOfTypeAll<ResearchSettingsList>()
                .Any(l => l != null && l.list != null && l.list.Any(r => r != null && !r.hideFromTechTree && r.availableInDemo && Owner(r) == Tab.Others));
            var tabs = new List<Tab> { Tab.Game, Tab.Mod };
            if (others)
                tabs.Add(Tab.Others);
            tabs.Add(Tab.All);
            if (_current == Tab.Others && !others)
                _current = Tab.Game;

            // Two by two low in the info panel on the right, above its Research button
            var bar = new GameObject("TFA_ResearchTabs", typeof(RectTransform));
            var rect = (RectTransform)bar.transform;
            rect.SetParent(info.transform, false);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 150f);
            rect.sizeDelta = new Vector2(-40f, 104f);
            var layout = bar.AddComponent<GridLayoutGroup>();
            layout.spacing = new Vector2(10f, 8f);
            layout.cellSize = new Vector2(150f, 46f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 2;
            layout.childAlignment = TextAnchor.LowerCenter;
            foreach (Tab tab in tabs)
            {
                Button button = Object.Instantiate(source, rect);
                button.name = "TFA_Tab_" + tab;
                button.gameObject.SetActive(true);
                button.interactable = true;
                button.onClick = new Button.ButtonClickedEvent();
                Tab chosen = tab;
                button.onClick.AddListener(() => Select(canvas, chosen));
                foreach (LocalizeStringEvent localized in button.GetComponentsInChildren<LocalizeStringEvent>(true))
                    localized.enabled = false;
                foreach (TMP_Text text in button.GetComponentsInChildren<TMP_Text>(true))
                    text.text = Name(tab);
                if (button.GetComponent<LayoutElement>() is LayoutElement element)
                    element.ignoreLayout = false;
                (button.GetComponent<CanvasGroup>() ?? button.gameObject.AddComponent<CanvasGroup>()).alpha = tab == _current ? 1f : 0.5f;
            }
        }

        private static void Select(ResearchCanvas canvas, Tab tab)
        {
            _current = tab;
            foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("TFA_Tab_")))
                if (child.name.StartsWith("TFA_Tab_") && child.GetComponent<CanvasGroup>() is CanvasGroup group)
                    group.alpha = child.name == "TFA_Tab_" + tab ? 1f : 0.5f;
            AccessTools.Method(typeof(ResearchCanvas), "RefreshButtons").Invoke(canvas, null);
            AccessTools.Method(typeof(ResearchCanvas), "UpdateButtons").Invoke(canvas, null);
        }

        /// <summary>Development: shows a tab in an open research window.</summary>
        internal static void DevSelect(ResearchCanvas canvas, Tab tab) => Select(canvas, tab);
    }
}

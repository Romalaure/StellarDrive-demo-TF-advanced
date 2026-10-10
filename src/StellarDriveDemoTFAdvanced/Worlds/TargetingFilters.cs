using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Ships.Interface.Model;
using StellarDriveDemoTFAdvanced.Common;
using TMPro;
using UI.TargetingComputer;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace StellarDriveDemoTFAdvanced.Worlds
{
    /// <summary>
    /// Filters on the targeting computer's list (it shows every beacon and planet, nearest
    /// first): all, planets, the abandoned stations, the other beacons (players' ships and bases).
    /// The filter buttons are copies of the list's own buttons, at its top; the choice is kept
    /// while the game runs.
    /// </summary>
    [HarmonyPatch(typeof(TargetingComputerCanvas), "Start")]
    internal static class TargetingFilters
    {
        private enum Filter { All, Planets, Stations, Beacons }

        private static readonly (Filter Filter, string Label)[] Filters =
        {
            (Filter.All, "Tout"), (Filter.Planets, "Planètes"), (Filter.Stations, "Stations"), (Filter.Beacons, "Balises")
        };

        private static readonly AccessTools.FieldRef<TargetingComputerCanvas, List<TargetingComputerButton>> Buttons =
            AccessTools.FieldRefAccess<TargetingComputerCanvas, List<TargetingComputerButton>>("_buttons");
        private static readonly AccessTools.FieldRef<TargetingComputerCanvas, Transform> List =
            AccessTools.FieldRefAccess<TargetingComputerCanvas, Transform>("buttonsList");
        private static readonly AccessTools.FieldRef<TargetingComputerCanvas, TargetingComputerButton> Prefab =
            AccessTools.FieldRefAccess<TargetingComputerCanvas, TargetingComputerButton>("buttonPrefab");
        private static readonly AccessTools.FieldRef<TargetingComputerButton, TMP_Text> NameText =
            AccessTools.FieldRefAccess<TargetingComputerButton, TMP_Text>("targetNameTxt");
        private static readonly AccessTools.FieldRef<TargetingComputerButton, TMP_Text> DistanceText =
            AccessTools.FieldRefAccess<TargetingComputerButton, TMP_Text>("targetDistanceTxt");
        private static readonly AccessTools.FieldRef<TargetingComputerButton, RawImage> Icon =
            AccessTools.FieldRefAccess<TargetingComputerButton, RawImage>("icon");

        private static Filter _current = Filter.All;

        private static void Postfix(TargetingComputerCanvas __instance)
        {
            try
            {
                Build(__instance);
            }
            catch (Exception e)
            {
                ErrorLog.Report("targeting filters", e);
            }
        }

        private static void Build(TargetingComputerCanvas canvas)
        {
            Transform list = List(canvas);
            TargetingComputerButton prefab = Prefab(canvas);
            if (list == null || prefab == null)
                return;
            var filterButtons = new List<(Filter Filter, TargetingComputerButton Button)>();
            for (int i = 0; i < Filters.Length; i++)
            {
                (Filter filter, string label) = Filters[i];
                TargetingComputerButton button = Object.Instantiate(prefab, list);
                button.name = "TFA_TargetFilter_" + filter;
                button.transform.SetSiblingIndex(i);
                // Its own click, instead of selecting a target
                Button clickable = button.GetComponentInChildren<Button>(true);
                if (clickable != null)
                {
                    clickable.onClick = new Button.ButtonClickedEvent();
                    Filter chosen = filter;
                    clickable.onClick.AddListener(() =>
                    {
                        _current = chosen;
                        Apply(canvas, filterButtons);
                    });
                }
                if (NameText(button) != null)
                    NameText(button).text = "Filtre : " + label;
                if (DistanceText(button) != null)
                    DistanceText(button).text = "";
                if (Icon(button) != null)
                    Icon(button).enabled = false;
                filterButtons.Add((filter, button));
            }
            Apply(canvas, filterButtons);
        }

        private static void Apply(TargetingComputerCanvas canvas, List<(Filter Filter, TargetingComputerButton Button)> filterButtons)
        {
            var stations = new HashSet<string>(AbandonedStations.All.Select(s => s.Name));
            var provision = Core.Services.ServiceLocator.GetService<Ships.Interface.Services.ITargetProvisionService>();
            foreach (TargetingComputerButton button in Buttons(canvas))
            {
                if (button == null)
                    continue;
                Target target = button.Target;
                bool station = target.TargetType == TargetType.Beacon && provision != null && stations.Contains(provision.GetTargetName(target));
                bool shown;
                switch (_current)
                {
                    case Filter.Planets: shown = target.TargetType == TargetType.Planet; break;
                    case Filter.Stations: shown = station; break;
                    case Filter.Beacons: shown = target.TargetType == TargetType.Beacon && !station; break;
                    default: shown = true; break;
                }
                button.gameObject.SetActive(shown);
            }
            foreach ((Filter filter, TargetingComputerButton button) in filterButtons)
            {
                if (filter == _current)
                    button.MarkSelected();
                else
                    button.MarkUnselected();
            }
        }
    }
}

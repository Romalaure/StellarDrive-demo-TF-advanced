using System.Collections;
using System.Linq;
using HarmonyLib;
using StellarDriveDemoTFAdvanced.Common;
using UI.Guide;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Wiki
{
    /// <summary>Development check (DevWiki): opens the guide on the wiki, follows a link, photographs each step.</summary>
    internal static class WikiDev
    {
        public static void Install()
        {
            DevHarness.Steps.Add((26f, "wiki", () =>
            {
                if (Settings.DevWiki.Value)
                    MelonLoader.MelonCoroutines.Start(Run());
            }));
        }

        private static IEnumerator Run()
        {
            AdvancedMod.Log.Msg("dev: wiki pages: " + string.Join(", ", WikiPages.Names));
            var activator = Object.FindObjectsByType<GuideMenuActivator>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (activator == null)
            {
                AdvancedMod.Log.Warning("dev: wiki: no guide");
                yield break;
            }
            activator.ToggleGuide();
            yield return new WaitForSecondsRealtime(1.5f);
            var canvas = Object.FindObjectsByType<GuideCanvas>(FindObjectsSortMode.None).FirstOrDefault();
            var buttons = canvas != null ? Traverse.Create(canvas).Field<System.Collections.Generic.List<GuideEntryButton>>("_buttons").Value : null;
            GuideEntryButton wiki = buttons?.FirstOrDefault(b => b.PageContent != null && b.PageContent.EntryId == WikiGuide.EntryId);
            AdvancedMod.Log.Msg($"dev: wiki: guide {canvas != null}, {buttons?.Count} entries, wiki entry {wiki != null}");
            DevHarness.Screenshot("wiki_0");
            yield return new WaitForSecondsRealtime(0.5f);
            if (wiki != null)
            {
                canvas.SelectGuideEntry(wiki);
                yield return new WaitForSecondsRealtime(1f);
                DevHarness.Screenshot("wiki_1");
                yield return new WaitForSecondsRealtime(0.5f);
                WikiPage.Show("TF-Gaia");
                yield return new WaitForSecondsRealtime(1f);
                DevHarness.Screenshot("wiki_2");
                yield return new WaitForSecondsRealtime(0.5f);
                WikiPage.Show("Ressources");
                yield return new WaitForSecondsRealtime(1f);
                DevHarness.Screenshot("wiki_3");
                yield return new WaitForSecondsRealtime(0.5f);
                WikiPage.Show(WikiParts.PartPrefix + "7161");
                yield return new WaitForSecondsRealtime(1f);
                DevHarness.Screenshot("wiki_4");
                yield return new WaitForSecondsRealtime(0.5f);
                WikiPage.ShowSearch("batterie");
                yield return new WaitForSecondsRealtime(1f);
                DevHarness.Screenshot("wiki_5");
                yield return new WaitForSecondsRealtime(0.5f);
            }
            activator.Close();
            yield return new WaitForSecondsRealtime(1f);
            yield return Research();
        }

        // The research window, opened without a table, photographed on each tab
        private static IEnumerator Research()
        {
            var opener = Object.FindObjectsByType<UI.Research.GenericPartOpenableMenuActivator>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            GameObject prefab = opener != null ? Traverse.Create(opener).Field<GameObject>("researchMenuPrefab").Value : null;
            if (prefab == null)
            {
                AdvancedMod.Log.Warning("dev: research: no window");
                yield break;
            }
            GameObject menu = Object.Instantiate(prefab, opener.transform);
            yield return new WaitForSecondsRealtime(1.5f);
            var canvas = menu.GetComponentInChildren<UI.Research.ResearchCanvas>(true);
            foreach (var research in Resources.FindObjectsOfTypeAll<Research.Interface.Values.ResearchSettingsList>().SelectMany(l => l.list ?? new System.Collections.Generic.List<Research.Interface.Values.ResearchSettings>()).Where(r => r != null && !r.hideFromTechTree && r.availableInDemo).Distinct())
                AdvancedMod.Log.Msg($"dev: research tech {research.id} {research.techName} owner {Parts.ResearchTabs.Owner(research)} at {research.positionInTechTree}");
            foreach (Parts.ResearchTabs.Tab tab in new[] { Parts.ResearchTabs.Tab.Game, Parts.ResearchTabs.Tab.Mod, Parts.ResearchTabs.Tab.Others, Parts.ResearchTabs.Tab.All })
            {
                Parts.ResearchTabs.DevSelect(canvas, tab);
                yield return new WaitForSecondsRealtime(1f);
                DevHarness.Screenshot("research_" + tab);
                yield return new WaitForSecondsRealtime(0.5f);
            }
            Parts.ResearchTabs.DevSelect(canvas, Parts.ResearchTabs.Tab.Game);
            Object.Destroy(menu);
        }
    }
}

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
                WikiPage.Show("Pyros");
                yield return new WaitForSecondsRealtime(1f);
                DevHarness.Screenshot("wiki_2");
                yield return new WaitForSecondsRealtime(0.5f);
                WikiPage.Show("Machines");
                yield return new WaitForSecondsRealtime(1f);
                DevHarness.Screenshot("wiki_3");
                yield return new WaitForSecondsRealtime(0.5f);
            }
            activator.Close();
        }
    }
}

using System.Collections;
using System.Linq;
using System.Text;
using HarmonyLib;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Parts
{
    /// <summary>Development check (DevBuildMenu): opens the build menu, logs its tab bar and photographs each tab.</summary>
    internal static class BuildMenuDev
    {
        public static void Install()
        {
            DevHarness.Steps.Add((26f, "build menu", () =>
            {
                if (Settings.DevBuildMenu.Value)
                    MelonLoader.MelonCoroutines.Start(Run());
            }));
        }

        private static IEnumerator Run()
        {
            var activator = Object.FindObjectsByType<UI.BuildMenu.BuildMenuActivator>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (activator == null)
            {
                AdvancedMod.Log.Warning("dev: build menu: no activator");
                yield break;
            }
            // Another mod's debug window (UnityExplorer) would hide part of the menu
            var hidden = Resources.FindObjectsOfTypeAll<Canvas>()
                .Where(c => c.gameObject.scene.IsValid() && (c.name.Contains("UniverseLib") || c.name.Contains("Explorer") || c.transform.root.name.Contains("UniverseLib") || c.transform.root.name.Contains("Explorer")) && c.gameObject.activeSelf)
                .Select(c => c.gameObject).ToList();
            AdvancedMod.Log.Msg("dev: build menu: canvases " + string.Join(", ", Resources.FindObjectsOfTypeAll<Canvas>().Where(c => c.gameObject.scene.IsValid() && c.transform.parent == null).Select(c => c.name)) + "; hiding " + hidden.Count);
            hidden.ForEach(g => g.SetActive(false));
            activator.Open();
            yield return new WaitForSecondsRealtime(1.5f);
            var menu = Object.FindObjectsByType<UI.BuildMenu.BuildMenu>(FindObjectsSortMode.None).FirstOrDefault();
            if (menu == null)
            {
                AdvancedMod.Log.Warning("dev: build menu: not open");
                yield break;
            }
            var container = Traverse.Create(menu).Field<Transform>("tabButtonsContainer").Value;
            var dump = new StringBuilder();
            for (Transform up = container.parent; up != null; up = up.parent)
            {
                dump.AppendLine("ANCESTOR:");
                foreach (Transform sibling in up)
                    Dump(sibling, 0, dump, 0);
            }
            Dump(container, 0, dump);
            System.IO.File.WriteAllText(System.IO.Path.Combine(DevHarness.Folder, "buildmenu_tabs.txt"), dump.ToString());
            DevHarness.Screenshot("buildmenu_0");
            // The search bar: a word found in several tabs
            BuildSearch.DevSearch(menu, "champ");
            yield return new WaitForSecondsRealtime(1f);
            DevHarness.Screenshot("buildmenu_search");
            AdvancedMod.Log.Msg($"dev: build menu: search shows {menu.GetComponentsInChildren<UI.BuildMenu.BuildPartButton>().Length} part(s): {string.Join(", ", menu.GetComponentsInChildren<UI.BuildMenu.BuildPartButton>().Select(b => b.partSettings?.fullLabel))}");
            yield return new WaitForSecondsRealtime(0.5f);
            BuildSearch.DevSearch(menu, "");
            yield return new WaitForSecondsRealtime(0.5f);
            // The mod picker: the list of mods, then one mod's parts, a smaller and a bigger menu
            var mods = BuildSearch.DevPick(menu, null, 1f);
            AdvancedMod.Log.Msg("dev: build menu mods: " + (mods == null ? "none" : string.Join(", ", mods.Select(m => $"{m.Mod} {m.Count}"))));
            if (mods != null && mods.Count > 0)
            {
                string many = mods.OrderByDescending(m => m.Count).First().Mod;
                BuildSearch.DevPick(menu, many, 0.8f);
                yield return new WaitForSecondsRealtime(1f);
                DevHarness.Screenshot("buildmenu_mod_small");
                yield return new WaitForSecondsRealtime(0.5f);
                string water = mods.Select(m => m.Mod).FirstOrDefault(m => m.Contains("Water") && m.Contains("TFA")) ?? mods.Last().Mod;
                BuildSearch.DevPick(menu, water, 1.3f);
                yield return new WaitForSecondsRealtime(1f);
                DevHarness.Screenshot("buildmenu_mod_big");
                yield return new WaitForSecondsRealtime(0.5f);
                BuildSearch.DevPick(menu, null, 1f);
                yield return new WaitForSecondsRealtime(0.5f);
            }
            yield return new WaitForSecondsRealtime(0.5f);
            foreach (Transform row in container.parent)
                dump.AppendLine($"ROW {row.name} scale {row.localScale} pos {((RectTransform)row).anchoredPosition} size {((RectTransform)row).rect.size}");
            foreach (Transform row in container.parent)
                foreach (Transform child in row)
                    if (child is RectTransform r)
                    {
                        var corners = new Vector3[4];
                        r.GetWorldCorners(corners);
                        dump.AppendLine($"  {row.name}/{child.name} {child.GetComponentInChildren<TMPro.TMP_Text>(true)?.text} screen x {corners[0].x:0}-{corners[2].x:0}");
                    }
            var menuImage = menu.GetComponentInChildren<UnityEngine.UI.Image>();
            if (menuImage != null)
            {
                var c = new Vector3[4];
                menuImage.rectTransform.GetWorldCorners(c);
                dump.AppendLine($"MENU IMAGE {menuImage.name} screen x {c[0].x:0}-{c[2].x:0}");
            }
            System.IO.File.WriteAllText(System.IO.Path.Combine(DevHarness.Folder, "buildmenu_tabs.txt"), dump.ToString());
            var tabs = Traverse.Create(menu).Field<UI.BuildMenu.TabButton[]>("_tabs").Value;
            int shot = 1;
            foreach (UI.BuildMenu.TabButton tab in tabs ?? new UI.BuildMenu.TabButton[0])
            {
                string label = tab.GetComponentInChildren<TMPro.TMP_Text>(true)?.text ?? "";
                if (!label.StartsWith("TF"))
                    continue;
                tab.ButtonClick();
                yield return new WaitForSecondsRealtime(1f);
                DevHarness.Screenshot("buildmenu_" + shot++);
                yield return new WaitForSecondsRealtime(0.5f);
            }
            activator.Close();
            hidden.ForEach(g => g.SetActive(true));
        }

        private static void Dump(Transform t, int depth, StringBuilder text, int maxDepth = 3)
        {
            var rect = t as RectTransform;
            string components = string.Join(",", t.GetComponents<Component>().Select(c => c.GetType().Name));
            text.AppendLine($"{new string(' ', depth * 2)}{t.name} [{components}] {(rect != null ? $"size {rect.rect.size} pos {rect.anchoredPosition} anchors {rect.anchorMin}-{rect.anchorMax} pivot {rect.pivot}" : "")}");
            if (depth < maxDepth)
                foreach (Transform child in t)
                    Dump(child, depth + 1, text, maxDepth);
        }
    }
}

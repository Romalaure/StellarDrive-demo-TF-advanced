using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Core.Services;
using HarmonyLib;
using StellarDriveDemoTFAdvanced.Common;
using TMPro;
using UI.Guide;
using UI.Guide.GuideEntries;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace StellarDriveDemoTFAdvanced.Wiki
{
    /// <summary>
    /// The mod's wiki in the game's guide (G): one more entry, "Wiki TF Avancé", first in the
    /// list. Its page shows the wiki's pages (docs/wiki, built into the mod) with clickable links
    /// between them and a bar of the main pages at the top.
    /// </summary>
    [HarmonyPatch(typeof(GuideCanvas), "InstantiateButtons")]
    internal static class WikiGuide
    {
        public const uint EntryId = 74001;
        public static string Title => Loc.Pick("Wiki TF Avancé", "TF Advanced Wiki", "Wiki de TF Advanced");

        private static readonly AccessTools.FieldRef<GuideCanvas, GuideEntryButton> ButtonPrefab = AccessTools.FieldRefAccess<GuideCanvas, GuideEntryButton>("entrySelectButtonPrefab");
        private static readonly AccessTools.FieldRef<GuideCanvas, Transform> ButtonContainer = AccessTools.FieldRefAccess<GuideCanvas, Transform>("entriesButtonContainer");
        private static readonly AccessTools.FieldRef<GuideCanvas, List<GuideEntryButton>> Buttons = AccessTools.FieldRefAccess<GuideCanvas, List<GuideEntryButton>>("_buttons");
        private static readonly AccessTools.FieldRef<GuideEntryButton, GuideEntryContent> Content = AccessTools.FieldRefAccess<GuideEntryButton, GuideEntryContent>("_content");
        private static readonly AccessTools.FieldRef<GuideEntryButton, bool> Available = AccessTools.FieldRefAccess<GuideEntryButton, bool>("_isAvailable");

        private static GameObject _holder;
        private static GuideEntryContent _template;

        private static void Postfix(GuideCanvas __instance)
        {
            try
            {
                Add(__instance);
            }
            catch (System.Exception e)
            {
                ErrorLog.Report("wiki in the guide", e);
            }
        }

        private static void Add(GuideCanvas canvas)
        {
            List<GuideEntryButton> buttons = Buttons(canvas);
            GuideEntryContent template = Template(buttons);
            if (template == null || buttons.Any(b => b.PageContent != null && b.PageContent.EntryId == EntryId))
                return;
            GuideEntryButton button = Object.Instantiate(ButtonPrefab(canvas), ButtonContainer(canvas));
            Content(button) = template;
            Available(button) = true;
            // The game names its entries through localization: this one is named here
            foreach (LocalizeStringEvent localized in button.GetComponentsInChildren<LocalizeStringEvent>(true))
                localized.enabled = false;
            foreach (TMP_Text text in button.GetComponentsInChildren<TMP_Text>(true))
                text.text = Title;
            button.transform.SetAsFirstSibling();
            buttons.Insert(0, button);
        }

        /// <summary>The page object the guide copies when the entry is opened: one of the game's pages emptied, with the wiki's text.</summary>
        private static GuideEntryContent Template(List<GuideEntryButton> buttons)
        {
            if (_template != null)
                return _template;
            GuideEntryContent source = buttons.Select(b => b.PageContent).FirstOrDefault(c => c != null)
                ?? ServiceLocator.GetService<GuideEntriesProvider>()?.AllGuideEntries.FirstOrDefault();
            if (source == null)
                return null;
            _holder = new GameObject("TFA_WikiTemplates");
            _holder.SetActive(false);
            Object.DontDestroyOnLoad(_holder);
            GuideEntryContent page = Object.Instantiate(source, _holder.transform);
            page.name = "TFA_WikiPage";
            page.entryId = EntryId;
            TMP_Text style = page.GetComponentsInChildren<TMP_Text>(true).OrderByDescending(t => t.text?.Length ?? 0).FirstOrDefault();
            TMP_FontAsset font = style != null ? style.font : null;
            float size = style != null ? style.fontSize : 28f;
            Color color = style != null ? style.color : Color.white;
            foreach (Transform child in page.transform.Cast<Transform>().ToList())
                Object.DestroyImmediate(child.gameObject);
            foreach (LayoutGroup group in page.GetComponents<LayoutGroup>())
                Object.DestroyImmediate(group);
            var layout = page.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 16, 24);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            if (page.GetComponent<ContentSizeFitter>() == null)
                page.gameObject.AddComponent<ContentSizeFitter>();
            page.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(page.transform, false);
            var tmp = textObject.AddComponent<TextMeshProUGUI>();
            if (font != null)
                tmp.font = font;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.richText = true;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = true;
            textObject.AddComponent<WikiPage>();
            _template = page;
            return page;
        }
    }

    /// <summary>A wiki page in the guide: shows the current page and follows its links.</summary>
    internal sealed class WikiPage : MonoBehaviour, IPointerClickHandler
    {
        private static readonly string[] Bar = { "Home", "Systeme-Otrera", "Galaxie-Erebe", "Ressources", "Machines", "Constructions", "Stations-spatiales", "Recherche", "Itineraire" };
        private static readonly Stack<string> History = new Stack<string>();
        public static string Current = "Home";

        private TMP_Text _text;

        private void Start()
        {
            _text = GetComponent<TMP_Text>();
            Render();
        }

        public static void Show(string page)
        {
            if (!WikiPages.Exists(page) || page == Current)
                return;
            History.Push(Current);
            Current = page;
            foreach (WikiPage open in FindObjectsByType<WikiPage>(FindObjectsSortMode.None))
                open.Render();
        }

        private void Render()
        {
            if (_text == null)
                return;
            var text = new StringBuilder();
            // The main pages, then back
            text.Append("<size=80%>");
            text.Append(string.Join("  ·  ", Bar.Where(WikiPages.Exists).Select(p => p == Current
                ? "<b>" + WikiPages.Title(p) + "</b>"
                : $"<link=\"{p}\"><color=#5fc8ff>{WikiPages.Title(p)}</color></link>")));
            if (History.Count > 0)
                text.Append("    <link=\"back\"><color=#ffb060>« " + Loc.Pick("Retour", "Back", "Atrás") + "</color></link>");
            text.Append("</size>\n\n");
            text.Append(WikiPages.Render(Current));
            _text.text = text.ToString();
            var scroll = GetComponentInParent<ScrollRect>();
            if (scroll != null)
                scroll.verticalNormalizedPosition = 1f;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_text == null)
                return;
            Canvas canvas = GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            int index = TMP_TextUtilities.FindIntersectingLink(_text, eventData.position, camera);
            if (index < 0)
                return;
            string target = _text.textInfo.linkInfo[index].GetLinkID();
            if (target == "back")
            {
                if (History.Count == 0)
                    return;
                Current = History.Pop();
                foreach (WikiPage open in FindObjectsByType<WikiPage>(FindObjectsSortMode.None))
                    open.Render();
                return;
            }
            Show(target);
        }
    }

    /// <summary>The wiki's pages, built into the mod from docs/wiki, and their markdown turned into TextMeshPro text.</summary>
    internal static class WikiPages
    {
        private static Dictionary<string, string> _pages;
        private static Loc.Language _pagesLanguage;

        private static Dictionary<string, string> Pages
        {
            get
            {
                Loc.Language language = Loc.Current;
                if (_pages != null && _pagesLanguage == language)
                    return _pages;
                _pages = new Dictionary<string, string>();
                _pagesLanguage = language;
                var assembly = typeof(WikiPages).Assembly;
                // French pages are "Wiki.<page>.md", English ones "Wiki.en.<page>.md", Spanish ones
                // "Wiki.es.<page>.md" (over the English ones: a page not translated yet stays in English)
                string[] prefixes = language == Loc.Language.French ? new[] { "Wiki." }
                    : language == Loc.Language.Spanish ? new[] { "Wiki.en.", "Wiki.es." } : new[] { "Wiki.en." };
                foreach (string prefix in prefixes)
                {
                    foreach (string name in assembly.GetManifestResourceNames().Where(n => n.StartsWith(prefix) && n.EndsWith(".md")
                        && (prefix != "Wiki." || (!n.StartsWith("Wiki.en.") && !n.StartsWith("Wiki.es.")))))
                    {
                        using (Stream stream = assembly.GetManifestResourceStream(name))
                        using (var reader = new StreamReader(stream, Encoding.UTF8))
                            _pages[name.Substring(prefix.Length, name.Length - prefix.Length - 3)] = reader.ReadToEnd();
                    }
                }
                return _pages;
            }
        }

        public static IEnumerable<string> Names => Pages.Keys;

        public static bool Exists(string page) => page != null && Pages.ContainsKey(page);

        /// <summary>The page's first heading.</summary>
        public static string Title(string page)
        {
            if (page == "Home")
                return Loc.Pick("Accueil", "Home", "Inicio");
            string first = Pages.TryGetValue(page, out string md) ? md.Split('\n').FirstOrDefault(l => l.StartsWith("# ")) : null;
            return first != null ? first.Substring(2).Trim() : page;
        }

        private static readonly Regex Link = new Regex(@"\[([^\]]+)\]\(([^)]+)\)");
        private static readonly Regex Bold = new Regex(@"\*\*([^*]+)\*\*");
        private static readonly Regex Italic = new Regex(@"(?<![*\w])\*([^*]+)\*(?![*\w])");
        private static readonly Regex Code = new Regex(@"`([^`]+)`");

        public static string Render(string page)
        {
            if (!Pages.TryGetValue(page, out string md))
                return "";
            var text = new StringBuilder();
            string[] header = null;
            foreach (string raw in md.Replace("\r", "").Split('\n'))
            {
                string line = raw.TrimEnd();
                if (line.StartsWith("|"))
                {
                    string[] cells = line.Trim('|').Split('|').Select(c => Inline(c.Trim())).ToArray();
                    if (cells.All(c => Regex.IsMatch(c, @"^:?-+:?$")))
                        continue;
                    if (header == null)
                    {
                        header = cells;
                        continue;
                    }
                    // A row: its first cell in bold, the others after their column's name
                    text.Append("• <b>").Append(cells[0]).Append("</b>");
                    for (int i = 1; i < cells.Length; i++)
                    {
                        if (string.IsNullOrEmpty(cells[i]))
                            continue;
                        string column = i < header.Length ? header[i] : "";
                        text.Append(i == 1 ? " — " : " · ");
                        if (!string.IsNullOrEmpty(column) && header.Length > 2)
                            text.Append("<color=#9ab0c0>").Append(column).Append(Loc.Pick(" :", ":", ":")).Append("</color> ");
                        text.Append(cells[i]);
                    }
                    text.Append('\n');
                    continue;
                }
                header = null;
                if (line.StartsWith("# "))
                    text.Append("<size=150%><b>").Append(Inline(line.Substring(2))).Append("</b></size>\n");
                else if (line.StartsWith("## "))
                    text.Append("\n<size=120%><b><color=#7fd8ff>").Append(Inline(line.Substring(3))).Append("</color></b></size>\n");
                else if (line.StartsWith("### "))
                    text.Append("\n<b>").Append(Inline(line.Substring(4))).Append("</b>\n");
                else if (line.StartsWith("- ") || line.StartsWith("* "))
                    text.Append("• ").Append(Inline(line.Substring(2))).Append('\n');
                else if (line.StartsWith("  - "))
                    text.Append("    - ").Append(Inline(line.Substring(4))).Append('\n');
                else if (line.StartsWith("> "))
                    text.Append("<color=#ffb080>").Append(Inline(line.Substring(2))).Append("</color>\n");
                else if (Regex.IsMatch(line, @"^\d+\. "))
                    text.Append(Inline(line)).Append('\n');
                else
                    text.Append(Inline(line)).Append('\n');
            }
            return text.ToString();
        }

        private static string Inline(string s)
        {
            s = s.Replace("<", "‹");
            // Symbols the game's font lacks
            s = s.Replace("⚠️", "<b>!</b>").Replace("⚠", "<b>!</b>").Replace("→", "»");
            s = Link.Replace(s, m =>
            {
                string target = m.Groups[2].Value;
                if (target.StartsWith("http"))
                    return m.Groups[1].Value;
                string page = Path.GetFileNameWithoutExtension(target.Split('#')[0]);
                return Exists(page) ? $"<link=\"{page}\"><color=#5fc8ff><u>{m.Groups[1].Value}</u></color></link>" : m.Groups[1].Value;
            });
            s = Bold.Replace(s, "<b>$1</b>");
            s = Italic.Replace(s, "<i>$1</i>");
            s = Code.Replace(s, "<color=#ffd080>$1</color>");
            return s;
        }
    }
}

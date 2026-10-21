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

    /// <summary>
    /// A wiki page in the guide: a bar of the main pages (buttons in the guide's own look), then
    /// the page's text in blocks; a line naming a part or an item shows its icon beside it, and a
    /// planet's page opens on pictures of its plants.
    /// </summary>
    internal sealed class WikiPage : MonoBehaviour
    {
        private static readonly List<string> Bar = new List<string> { "Home", "Systeme-Otrera", "Galaxie-Erebe", "Ressources", "Machines", "Constructions", "Stations-spatiales", "Recherche", "Itineraire" };
        private const string Generated = "TFA_Wiki";

        /// <summary>Adds a page of another mod to the bar of main pages (see <see cref="WikiPages.AddSource"/>).</summary>
        public static void AddToBar(string page)
        {
            if (!Bar.Contains(page))
                Bar.Add(page);
        }
        private static readonly Stack<string> History = new Stack<string>();
        public static string Current = "Home";

        private TMP_Text _style;

        private void Start()
        {
            _style = GetComponent<TMP_Text>();
            Render();
        }

        public static void Show(string page)
        {
            if (!WikiPages.Exists(page) || page == Current)
                return;
            History.Push(Current);
            Current = page;
            RenderAll();
        }

        public static void Back()
        {
            if (History.Count == 0)
                return;
            Current = History.Pop();
            RenderAll();
        }

        private static void RenderAll()
        {
            foreach (WikiPage open in FindObjectsByType<WikiPage>(FindObjectsSortMode.None))
                open.Render();
        }

        private void Render()
        {
            if (_style == null)
                return;
            Transform page = transform.parent;
            foreach (Transform child in page.Cast<Transform>().ToList())
                if (child.name.StartsWith(Generated))
                    Destroy(child.gameObject);
            // This object only lends its font and colors
            _style.text = "";
            (GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>()).ignoreLayout = true;

            AddBar(page);
            foreach (WikiBlock block in WikiPages.Render(Current))
            {
                if (block.Gallery != null)
                    AddGallery(page, block.Gallery);
                else if (block.Icon != null)
                    AddRow(page, block.Icon, block.Text, block.Large);
                else if (!string.IsNullOrWhiteSpace(block.Text))
                    AddText(page, block.Text.TrimEnd('\n'), 1f);
            }
            var scroll = GetComponentInParent<ScrollRect>();
            if (scroll != null)
                scroll.verticalNormalizedPosition = 1f;
        }

        private TMP_Text AddText(Transform parent, string text, float scale, string name = "Text")
        {
            var textObject = new GameObject(Generated + name, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
            var tmp = textObject.AddComponent<TextMeshProUGUI>();
            tmp.font = _style.font;
            tmp.fontSize = _style.fontSize * scale;
            tmp.color = _style.color;
            tmp.richText = true;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = true;
            tmp.text = text;
            textObject.AddComponent<WikiLinks>();
            return tmp;
        }

        // The main pages and back, as buttons
        private void AddBar(Transform page)
        {
            var bar = new GameObject(Generated + "Bar", typeof(RectTransform));
            bar.transform.SetParent(page, false);
            var grid = bar.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(_style.fontSize * 8.2f, _style.fontSize * 1.9f);
            grid.spacing = new Vector2(8f, 8f);
            grid.padding = new RectOffset(0, 0, 0, 10);
            foreach (string target in Bar.Where(WikiPages.Exists))
                AddButton(bar.transform, WikiPages.Title(target), target == Current, () => Show(target));
            if (History.Count > 0)
                AddButton(bar.transform, "« " + Loc.Pick("Retour", "Back", "Atrás"), false, Back, new Color(1f, 0.7f, 0.38f));
            AddSearch(page);
        }

        /// <summary>Whether the player is typing in the search field (the game's keys wait).</summary>
        public static bool Typing { get; private set; }

        private static void SetTyping(bool typing)
        {
            if (typing == Typing)
                return;
            Typing = typing;
            StellarDriveDemoTFAdvanced.TFItems.Common.ModMenu.RefreshControls();
        }

        private void OnDisable() => SetTyping(false);

        public static void ShowSearch(string query)
        {
            WikiParts.Query = query ?? "";
            if (Current != WikiParts.SearchPage)
                History.Push(Current);
            Current = WikiParts.SearchPage;
            RenderAll();
        }

        // The search field (Enter searches the parts and the pages) and its button
        private void AddSearch(Transform page)
        {
            float height = _style.fontSize * 1.9f;
            var row = new GameObject(Generated + "SearchRow", typeof(RectTransform));
            row.transform.SetParent(page, false);
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(0, 0, 0, 12);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            var fieldObject = new GameObject(Generated + "SearchField", typeof(RectTransform));
            fieldObject.transform.SetParent(row.transform, false);
            fieldObject.AddComponent<Image>().color = new Color(0.03f, 0.08f, 0.13f, 0.95f);
            fieldObject.AddComponent<UnityEngine.UI.Outline>().effectColor = new Color(0.45f, 0.6f, 0.72f, 0.6f);
            var element = fieldObject.AddComponent<LayoutElement>();
            element.flexibleWidth = 1f;
            element.preferredHeight = element.minHeight = height;
            var area = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            area.transform.SetParent(fieldObject.transform, false);
            var areaRect = (RectTransform)area.transform;
            areaRect.anchorMin = Vector2.zero;
            areaRect.anchorMax = Vector2.one;
            areaRect.offsetMin = new Vector2(10f, 2f);
            areaRect.offsetMax = new Vector2(-10f, -2f);
            TMP_Text Inner(string name, string value, Color color)
            {
                TMP_Text inner = AddText(area.transform, value, 0.8f, name);
                Destroy(inner.GetComponent<WikiLinks>());
                inner.textWrappingMode = TextWrappingModes.NoWrap;
                inner.alignment = TextAlignmentOptions.MidlineLeft;
                inner.color = color;
                var rect = (RectTransform)inner.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                return inner;
            }
            TMP_Text placeholder = Inner("Placeholder", Loc.Pick("Chercher une pièce, un objet, une planète…", "Search a part, an item, a planet…", "Buscar una pieza, un objeto, un planeta…"), new Color(0.6f, 0.66f, 0.72f, 0.8f));
            placeholder.fontStyle = FontStyles.Italic;
            TMP_Text text = Inner("Text", "", _style.color);
            var field = fieldObject.AddComponent<TMP_InputField>();
            field.textViewport = areaRect;
            field.textComponent = text;
            field.placeholder = placeholder;
            field.fontAsset = _style.font;
            field.pointSize = _style.fontSize * 0.8f;
            field.characterLimit = 40;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.text = WikiParts.Query;
            field.onSelect.AddListener(_ => SetTyping(true));
            field.onDeselect.AddListener(_ => SetTyping(false));
            field.onSubmit.AddListener(value => { SetTyping(false); ShowSearch(value); });

            var go = new GameObject(Generated + "SearchGo", typeof(RectTransform));
            go.transform.SetParent(row.transform, false);
            var goElement = go.AddComponent<LayoutElement>();
            goElement.preferredWidth = goElement.minWidth = _style.fontSize * 6f;
            AddButton(go.transform, Loc.Pick("Chercher", "Search", "Buscar"), Current == WikiParts.SearchPage, () => ShowSearch(field.text));
            var button = (RectTransform)go.transform.GetChild(0);
            button.anchorMin = Vector2.zero;
            button.anchorMax = Vector2.one;
            button.offsetMin = button.offsetMax = Vector2.zero;
        }


        private void AddButton(Transform parent, string label, bool selected, UnityEngine.Events.UnityAction click, Color? tint = null)
        {
            var buttonObject = new GameObject(Generated + "Button", typeof(RectTransform));
            buttonObject.transform.SetParent(parent, false);
            // Flat panels like the guide's own list: blue when chosen, a thin light edge
            var image = buttonObject.AddComponent<Image>();
            image.color = selected ? new Color(0.17f, 0.45f, 0.7f, 1f) : new Color(0.06f, 0.13f, 0.2f, 0.95f);
            var edge = buttonObject.AddComponent<UnityEngine.UI.Outline>();
            edge.effectColor = new Color(0.45f, 0.6f, 0.72f, selected ? 0.9f : 0.45f);
            edge.effectDistance = new Vector2(1.5f, -1.5f);
            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(click);
            TMP_Text text = AddText(buttonObject.transform, label, 0.72f, "ButtonText");
            Destroy(text.GetComponent<WikiLinks>());
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
            if (tint.HasValue)
                text.color = tint.Value;
            var rect = (RectTransform)text.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(8f, 2f);
            rect.offsetMax = new Vector2(-8f, -2f);
        }

        // A line with the icon of what it names on its left
        private void AddRow(Transform page, WikiImages.Ref icon, string text, bool large = false)
        {
            var row = new GameObject(Generated + "Row", typeof(RectTransform));
            row.transform.SetParent(page, false);
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 14f;
            layout.padding = new RectOffset(0, 0, 4, 4);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            float size = _style.fontSize * (large ? 6f : 2.6f);
            AddPicture(row.transform, icon, size);
            TMP_Text line = AddText(row.transform, text.TrimEnd('\n'), 1f);
            var element = line.gameObject.AddComponent<LayoutElement>();
            element.flexibleWidth = 1f;
        }

        private void AddGallery(Transform page, List<WikiImages.Ref> pictures)
        {
            var gallery = new GameObject(Generated + "Gallery", typeof(RectTransform));
            gallery.transform.SetParent(page, false);
            var grid = gallery.AddComponent<GridLayoutGroup>();
            float size = _style.fontSize * 5f;
            grid.cellSize = new Vector2(size + 12f, size + _style.fontSize * 1.8f);
            grid.spacing = new Vector2(10f, 10f);
            grid.padding = new RectOffset(0, 0, 6, 10);
            foreach (WikiImages.Ref picture in pictures)
            {
                var tile = new GameObject(Generated + "Tile", typeof(RectTransform));
                tile.transform.SetParent(gallery.transform, false);
                tile.AddComponent<Image>().color = new Color(0.04f, 0.09f, 0.14f, 0.75f);
                var column = tile.AddComponent<VerticalLayoutGroup>();
                column.padding = new RectOffset(6, 6, 6, 4);
                column.childAlignment = TextAnchor.UpperCenter;
                column.childControlWidth = column.childControlHeight = true;
                column.childForceExpandWidth = column.childForceExpandHeight = false;
                AddPicture(tile.transform, picture, size);
                TMP_Text caption = AddText(tile.transform, picture.Caption, 0.62f, "Caption");
                caption.alignment = TextAlignmentOptions.Center;
            }
        }

        private static void AddPicture(Transform parent, WikiImages.Ref picture, float size)
        {
            var pictureObject = new GameObject(Generated + "Picture", typeof(RectTransform));
            pictureObject.transform.SetParent(parent, false);
            var raw = pictureObject.AddComponent<RawImage>();
            raw.raycastTarget = false;
            var element = pictureObject.AddComponent<LayoutElement>();
            element.minWidth = element.preferredWidth = size;
            element.minHeight = element.preferredHeight = size;
            pictureObject.AddComponent<WikiPicture>().Picture = picture;
        }
    }

    /// <summary>A picture of the wiki, filled in once the game has made it.</summary>
    internal sealed class WikiPicture : MonoBehaviour
    {
        public WikiImages.Ref Picture;
        private RawImage _raw;

        private void Awake()
        {
            _raw = GetComponent<RawImage>();
            _raw.color = Color.clear;
        }

        private void Update()
        {
            if (Picture == null || _raw.texture != null)
            {
                enabled = false;
                return;
            }
            Texture texture = WikiImages.Get(Picture);
            if (texture == null)
            {
                // A plant that cannot be pictured: its tile goes
                if (WikiImages.Failed(Picture) && transform.parent.name.EndsWith("Tile"))
                    transform.parent.gameObject.SetActive(false);
                return;
            }
            _raw.texture = texture;
            _raw.color = Color.white;
        }
    }

    /// <summary>Follows the links of a block of wiki text.</summary>
    internal sealed class WikiLinks : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData)
        {
            var text = GetComponent<TMP_Text>();
            if (text == null)
                return;
            Canvas canvas = GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            int index = TMP_TextUtilities.FindIntersectingLink(text, eventData.position, camera);
            if (index < 0)
                return;
            string target = text.textInfo.linkInfo[index].GetLinkID();
            if (target == "back")
                WikiPage.Back();
            else
                WikiPage.Show(target);
        }
    }

    /// <summary>A piece of a wiki page: text, a line with an icon, or a row of pictures.</summary>
    internal sealed class WikiBlock
    {
        public string Text = "";
        public WikiImages.Ref Icon;
        /// <summary>The icon shown big (a part's own page).</summary>
        public bool Large;
        public List<WikiImages.Ref> Gallery;
    }

    /// <summary>The wiki's pages, built into the mod from docs/wiki, and their markdown turned into TextMeshPro text.</summary>
    internal static class WikiPages
    {
        private static Dictionary<string, string> _pages;
        private static Loc.Language _pagesLanguage;
        private static readonly List<System.Reflection.Assembly> Sources = new List<System.Reflection.Assembly> { typeof(WikiPages).Assembly };

        /// <summary>
        /// Another mod's pages, embedded in its assembly under the same names ("Wiki.&lt;page&gt;.md",
        /// "Wiki.en.&lt;page&gt;.md", "Wiki.es.&lt;page&gt;.md"); <paramref name="barPages"/> join the bar of main pages.
        /// </summary>
        public static void AddSource(System.Reflection.Assembly assembly, params string[] barPages)
        {
            if (!Sources.Contains(assembly))
                Sources.Add(assembly);
            foreach (string page in barPages)
                WikiPage.AddToBar(page);
            _pages = null;
        }

        private static Dictionary<string, string> Pages
        {
            get
            {
                Loc.Language language = Loc.Current;
                if (_pages != null && _pagesLanguage == language)
                    return _pages;
                _pages = new Dictionary<string, string>();
                _pagesLanguage = language;
                // French pages are "Wiki.<page>.md", English ones "Wiki.en.<page>.md", Spanish ones
                // "Wiki.es.<page>.md" (over the English ones: a page not translated yet stays in English)
                string[] prefixes = language == Loc.Language.French ? new[] { "Wiki." }
                    : language == Loc.Language.Spanish ? new[] { "Wiki.en.", "Wiki.es." } : new[] { "Wiki.en." };
                foreach (System.Reflection.Assembly assembly in Sources)
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

        public static bool Exists(string page) => page != null && (Pages.ContainsKey(page) || WikiParts.Exists(page));

        /// <summary>A page's markdown ("" for the pages the wiki makes itself).</summary>
        public static string Text(string page) => Pages.TryGetValue(page, out string md) ? md : "";

        /// <summary>The page's first heading.</summary>
        public static string Title(string page)
        {
            if (page == "Home")
                return Loc.Pick("Accueil", "Home", "Inicio");
            if (!Pages.ContainsKey(page) && WikiParts.Exists(page))
                return WikiParts.Title(page);
            string first = Pages.TryGetValue(page, out string md) ? md.Split('\n').FirstOrDefault(l => l.StartsWith("# ")) : null;
            return first != null ? first.Substring(2).Trim() : page;
        }

        private static readonly Regex Link = new Regex(@"\[([^\]]+)\]\(([^)]+)\)");
        private static readonly Regex Bold = new Regex(@"\*\*([^*]+)\*\*");
        private static readonly Regex Italic = new Regex(@"(?<![*\w])\*([^*]+)\*(?![*\w])");
        private static readonly Regex Code = new Regex(@"`([^`]+)`");

        private static readonly Regex Tags = new Regex("<[^>]+>");
        private static readonly Regex LeadingBold = new Regex(@"^\*\*([^*]+)\*\*");

        public static List<WikiBlock> Render(string page)
        {
            if (!Pages.ContainsKey(page) && WikiParts.Exists(page))
                return WikiParts.Render(page);
            var blocks = new List<WikiBlock>();
            if (!Pages.TryGetValue(page, out string md))
                return blocks;
            var text = new StringBuilder();
            void Flush()
            {
                if (text.Length > 0)
                    blocks.Add(new WikiBlock { Text = text.ToString() });
                text.Clear();
            }
            // A line naming a part or an item gets a block of its own, with the icon
            void Line(string rendered, string name)
            {
                WikiImages.Ref icon = name != null ? WikiImages.Match(name) : null;
                if (icon == null)
                {
                    text.Append(rendered).Append('\n');
                    return;
                }
                Flush();
                // A part's name opens its own page
                int bold = rendered.IndexOf("<b>", System.StringComparison.Ordinal);
                if (icon.Kind == WikiImages.Kind.Part && bold >= 0 && !rendered.Contains("<link="))
                {
                    int end = rendered.IndexOf("</b>", bold, System.StringComparison.Ordinal);
                    if (end > bold)
                        rendered = rendered.Substring(0, bold) + $"<link=\"{WikiParts.PartPrefix}{icon.Id}\"><color=#5fc8ff>" + rendered.Substring(bold, end + 4 - bold) + "</color></link>" + rendered.Substring(end + 4);
                }
                blocks.Add(new WikiBlock { Text = rendered.Trim('\n'), Icon = icon });
            }
            string[] header = null;
            bool galleryDone = false;
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
                    var row = new StringBuilder();
                    row.Append("• <b>").Append(cells[0]).Append("</b>");
                    for (int i = 1; i < cells.Length; i++)
                    {
                        if (string.IsNullOrEmpty(cells[i]))
                            continue;
                        string column = i < header.Length ? header[i] : "";
                        row.Append(i == 1 ? " — " : " · ");
                        if (!string.IsNullOrEmpty(column) && header.Length > 2)
                            row.Append("<color=#9ab0c0>").Append(column).Append(Loc.Pick(" :", ":", ":")).Append("</color> ");
                        row.Append(cells[i]);
                    }
                    Line(row.ToString(), Tags.Replace(cells[0], ""));
                    continue;
                }
                header = null;
                if (line.StartsWith("# "))
                {
                    text.Append("<size=150%><b>").Append(Inline(line.Substring(2))).Append("</b></size>\n");
                    List<WikiImages.Ref> gallery = galleryDone ? null : WikiImages.PlanetGallery(line.Substring(2));
                    galleryDone = true;
                    if (gallery != null && gallery.Count > 0)
                    {
                        Flush();
                        blocks.Add(new WikiBlock { Gallery = gallery });
                    }
                }
                else if (line.StartsWith("## "))
                    Line("\n<size=120%><b><color=#7fd8ff>" + Inline(line.Substring(3)) + "</color></b></size>", line.Substring(3));
                else if (line.StartsWith("### "))
                    Line("\n<b>" + Inline(line.Substring(4)) + "</b>", line.Substring(4));
                else if (line.StartsWith("- ") || line.StartsWith("* "))
                {
                    Match bold = LeadingBold.Match(line.Substring(2));
                    Line("• " + Inline(line.Substring(2)), bold.Success ? bold.Groups[1].Value : null);
                }
                else if (line.StartsWith("  - "))
                    text.Append("    - ").Append(Inline(line.Substring(4))).Append('\n');
                else if (line.StartsWith("> "))
                    text.Append("<color=#ffb080>").Append(Inline(line.Substring(2))).Append("</color>\n");
                else
                    text.Append(Inline(line)).Append('\n');
            }
            Flush();
            return blocks;
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

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
    /// A bar over the build menu's parts:
    /// - a search field: typing a few letters shows, in place of the tab, every part of every tab
    ///   whose name holds them (accents and case ignored), as the game's own buttons (click to
    ///   build, hover for the info); emptying it brings the tab back. While it is being typed in,
    ///   the player's controls and the menu's tab switching keys are off;
    /// - a mod picker: the parts of one mod only (the game's, TF Advanced's, or any other mod's),
    ///   page by page, the search narrowing them further;
    /// - the menu's size: smaller or bigger (kept between sessions).
    /// </summary>
    internal sealed class BuildSearch : MonoBehaviour
    {
        private const string FieldName = "TFA_BuildSearch";
        private const int Columns = 8, FittingRows = 3, PerPage = Columns * FittingRows, MaxResults = 48;
        /// <summary>The interface's scale on this screen (its canvas is laid out for 3840 x 2160; 1 on a 1080p screen).</summary>
        private float UiScale => Mathf.Max(0.5f, _menu.transform.lossyScale.y * 2f);
        private float BarHeight => 34f * UiScale;
        private const string SizeKey = "TFA_BuildMenuSize";
        private const float MinSize = 0.6f, MaxSize = 1.5f;

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
        private string _mod, _shownMod;
        private int _page, _shownPage, _pages = 1;
        private bool _picking;
        private GUIStyle _field, _hint, _button, _picked;
        private List<(string Mod, int Count)> _mods;

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

        /// <summary>Development: picks a mod in the open menu (null: every mod), and the menu's size.</summary>
        public static List<(string Mod, int Count)> DevPick(BuildMenu menu, string mod, float size)
        {
            BuildSearch search = menu != null ? menu.GetComponent<BuildSearch>() : null;
            if (search == null)
                return null;
            search._mods = search.ModList();
            search._mod = mod;
            search._page = 0;
            Size = size;
            return search._mods;
        }

        private void OnEnable()
        {
            if (_menu != null)
                ApplySize();
        }

        private void OnDisable()
        {
            _query = _shown = "";
            _picking = false;
            SetTyping(false);
        }

        /// <summary>The menu's size, 1 the game's (kept in the player's preferences).</summary>
        private static float Size
        {
            get
            {
                try
                {
                    return Mathf.Clamp(PlayerPrefs.GetFloat(SizeKey, 1f), MinSize, MaxSize);
                }
                catch (Exception)
                {
                    return 1f;
                }
            }
            set
            {
                try
                {
                    PlayerPrefs.SetFloat(SizeKey, Mathf.Clamp(value, MinSize, MaxSize));
                    PlayerPrefs.Save();
                }
                catch (Exception)
                {
                }
            }
        }

        // The menu sits on its canvas, whose scale the game sets: its frames are sized instead
        // (the parts' panel, and the part's info beside it)
        private void ApplySize()
        {
            float size = Size;
            // No bigger than leaves room for the bar over it: the frame grows around its pivot,
            // its top must stay under the bar
            if (_menu.transform.Find("Menu") is RectTransform frame && frame.localScale.y > 0.01f)
            {
                var corners = new Vector3[4];
                frame.GetWorldCorners(corners);
                float pivot = Screen.height - frame.position.y, top = Screen.height - corners[1].y;
                float reach = pivot - top;
                if (reach > 1f)
                    size = Mathf.Min(size, frame.localScale.y * (pivot - BarHeight - 10f) / reach);
            }
            foreach (string name in new[] { "Menu", "PartInfo" })
            {
                Transform child = _menu.transform.Find(name);
                if (child != null && Mathf.Abs(child.localScale.x - size) > 0.001f)
                    child.localScale = new Vector3(size, size, 1f);
            }
        }

        private void OnGUI()
        {
            if (_menu == null || !(_menu.transform.Find("Menu") is RectTransform frame))
                return;
            ApplySize();
            // The bar sits just over the menu's frame, at the size of the game's own interface
            var corners = new Vector3[4];
            frame.GetWorldCorners(corners);
            float scale = UiScale;
            float left = corners[1].x, top = Screen.height - corners[1].y;
            float room = corners[2].x - corners[1].x;
            float height = BarHeight;
            float y = Mathf.Max(2f, top - height - 6f * scale);
            if (_field == null)
            {
                _field = new GUIStyle(DeviceUi.Field);
                _hint = new GUIStyle(DeviceUi.Muted) { alignment = TextAnchor.MiddleLeft };
                _button = new GUIStyle(DeviceUi.Button);
                _picked = new GUIStyle(DeviceUi.Selected);
            }
            _field.fontSize = Mathf.RoundToInt(15f * scale);
            _hint.fontSize = Mathf.RoundToInt(14f * scale);
            _button.fontSize = _picked.fontSize = Mathf.RoundToInt(14f * scale);

            // The search field
            float width = Mathf.Min(330f * scale, room * 0.42f);
            var rect = new Rect(left, y, width, height);
            GUI.SetNextControlName(FieldName);
            _query = GUI.TextField(rect, _query ?? "", 40, _field);
            if (_query.Length == 0 && GUI.GetNameOfFocusedControl() != FieldName)
                GUI.Label(new Rect(rect.x + 12f * scale, rect.y, rect.width, rect.height), Loc.Pick("Rechercher une pièce…", "Search a part…", "Buscar una pieza…"), _hint);
            if (Event.current.type == EventType.Repaint)
                SetTyping(GUI.GetNameOfFocusedControl() == FieldName);
            // Escape or Enter leaves the bar (the menu stays open)
            if (Typing && Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Escape || Event.current.keyCode == KeyCode.Return))
            {
                GUI.FocusControl(null);
                Event.current.Use();
            }

            // The mod picker
            float x = rect.xMax + 8f * scale;
            var pick = new Rect(x, y, Mathf.Min(230f * scale, room * 0.3f), height);
            string label = Loc.Pick("Mod : ", "Mod: ", "Mod: ") + (_mod ?? Loc.Pick("tous", "all", "todos")) + "  ▾";
            if (GUI.Button(pick, label, _mod != null ? _picked : _button))
            {
                _picking = !_picking;
                if (_picking)
                    _mods = ModList();
            }
            x = pick.xMax + 8f * scale;

            // Pages, when the parts shown do not fit
            if (_pages > 1)
            {
                if (GUI.Button(new Rect(x, y, height, height), "◀", _button))
                    _page = (_page + _pages - 1) % _pages;
                GUI.Label(new Rect(x + height, y, 54f * scale, height), $"{_page + 1}/{_pages}", new GUIStyle(_hint) { alignment = TextAnchor.MiddleCenter });
                if (GUI.Button(new Rect(x + height + 54f * scale, y, height, height), "▶", _button))
                    _page = (_page + 1) % _pages;
                x += height * 2f + 62f * scale;
            }

            // The menu's size
            if (GUI.Button(new Rect(x, y, height, height), "−", _button))
                Size -= 0.1f;
            if (GUI.Button(new Rect(x + height + 4f * scale, y, height, height), "+", _button))
                Size += 0.1f;

            // The mods to pick from, in a list rising above the bar
            if (_picking && _mods != null)
            {
                float line = 28f * scale;
                var all = new List<(string Mod, int Count)> { (null, _mods.Sum(m => m.Count)) };
                all.AddRange(_mods);
                var box = new Rect(pick.x, y - line * all.Count - 6f * scale, Mathf.Max(pick.width, 260f * scale), line * all.Count + 4f * scale);
                DeviceUi.Panel(box);
                for (int i = 0; i < all.Count; i++)
                {
                    var (mod, count) = all[i];
                    string name = (mod ?? Loc.Pick("Tous les mods", "Every mod", "Todos los mods")) + $"  ({count})";
                    if (GUI.Button(new Rect(box.x + 2f * scale, box.y + 2f * scale + i * line, box.width - 4f * scale, line - 2f * scale), name, mod == _mod ? _picked : _button))
                    {
                        _mod = mod;
                        _page = 0;
                        _picking = false;
                    }
                }
            }

            if (_query != _shown || _mod != _shownMod || _page != _shownPage)
            {
                if (_query != _shown || _mod != _shownMod)
                    _page = 0;
                _shown = _query;
                _shownMod = _mod;
                _shownPage = _page;
                Apply();
            }
        }

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

        // Every part of the menu the player has unlocked, once each
        private IEnumerable<(PartSettings Part, string Tab)> MenuParts()
        {
            IResearchProvider research = Research(_menu);
            var seen = new HashSet<ushort>();
            foreach (BuildMenuTabConfig tab in Tabs(_menu) ?? new BuildMenuTabConfig[0])
                foreach (BuildMenuTabConfig.BuildMenuRow row in tab.rows ?? new BuildMenuTabConfig.BuildMenuRow[0])
                    foreach (PartSettings part in row.parts ?? new PartSettings[0])
                        if (part != null && seen.Add(part.id) && (research == null || research.IsPartUnlocked(part)))
                            yield return (part, tab.name);
        }

        private List<(string Mod, int Count)> ModList() =>
            MenuParts().GroupBy(p => PartMods.Of(p.Part)).Select(g => (g.Key, g.Count()))
                .OrderBy(m => m.Key == PartMods.Game ? 0 : 1).ThenBy(m => m.Key).ToList();

        private void Apply()
        {
            try
            {
                string query = Fold(_query.Trim());
                if (query.Length == 0 && _mod == null)
                {
                    _pages = 1;
                    ShowTab.Invoke(_menu, null);
                    return;
                }
                var found = MenuParts()
                    .Where(p => _mod == null || PartMods.Of(p.Part) == _mod)
                    .Where(p => query.Length == 0 || Fold(p.Part.fullLabel ?? p.Part.name ?? "").Contains(query) || Fold(p.Tab ?? "").Contains(query))
                    .Select(p => p.Part).ToList();
                if (query.Length > 0)
                    // The best matches first: the name starting with the search
                    found = found.OrderBy(p => Fold(p.fullLabel ?? "").StartsWith(query) ? 0 : 1).ThenBy(p => p.fullLabel).ToList();
                if (_mod == null)
                {
                    // A search over every mod: one page, scaled down to fit
                    found = found.Take(MaxResults).ToList();
                    _pages = 1;
                }
                else
                {
                    _pages = Mathf.Max(1, (found.Count + PerPage - 1) / PerPage);
                    _page = Mathf.Clamp(_page, 0, _pages - 1);
                    _shownPage = _page;
                    found = found.Skip(_page * PerPage).Take(PerPage).ToList();
                }
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

    /// <summary>
    /// Which mod a part comes from: the mod whose code made it (through SDModKit, or through this
    /// mod's part factory for the mods built on TF Advanced), the reinforced hull of Submarine
    /// Add-ons, otherwise the game.
    /// </summary>
    internal static class PartMods
    {
        public static string Game => Loc.Pick("Jeu de base", "Base game", "Juego base");

        private static readonly Dictionary<ushort, string> Known = new Dictionary<ushort, string>();
        private static Dictionary<ushort, System.Reflection.Assembly> _sdk;

        public static string Of(PartSettings part)
        {
            if (Known.TryGetValue(part.id, out string mod))
                return mod;
            System.Reflection.Assembly maker = null;
            if (PartFactory.Makers.TryGetValue(part.id, out var made))
                maker = made;
            else if (Sdk().TryGetValue(part.id, out var sdk))
                maker = sdk;
            if (maker != null)
                mod = Name(maker);
            else if ((part.name ?? "").StartsWith("Reinforced", StringComparison.Ordinal))
                mod = "Submarine Add-ons";
            else
                mod = Game;
            Known[part.id] = mod;
            return mod;
        }

        // SDModKit's part definitions: each one's configuration code tells whose part it is
        private static Dictionary<ushort, System.Reflection.Assembly> Sdk()
        {
            if (_sdk != null)
                return _sdk;
            _sdk = new Dictionary<ushort, System.Reflection.Assembly>();
            try
            {
                if (AccessTools.Field(typeof(SDModKit.Game.CustomParts), "Definitions")?.GetValue(null) is System.Collections.IEnumerable definitions)
                    foreach (SDModKit.Game.CustomPartDefinition definition in definitions)
                        if (definition?.Configure != null && Maker(definition.Configure, 0) is System.Reflection.Assembly maker)
                            _sdk[definition.Id] = maker;
            }
            catch (Exception e)
            {
                AdvancedMod.Log.Warning("build menu: the mods' parts were not found: " + e.Message);
            }
            return _sdk;
        }

        // The code behind a part's configuration: a library that wraps another mod's code (the mod
        // kits) is looked through, to the delegate it holds
        private static System.Reflection.Assembly Maker(Delegate code, int depth)
        {
            System.Reflection.Assembly assembly = code.Method?.DeclaringType?.Assembly;
            if (assembly == null || depth > 3 || !IsLibrary(assembly) || code.Target == null)
                return assembly;
            foreach (System.Reflection.FieldInfo field in code.Target.GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
                if (field.GetValue(code.Target) is Delegate inner && Maker(inner, depth + 1) is System.Reflection.Assembly found && !IsLibrary(found))
                    return found;
            return assembly;
        }

        private static bool IsLibrary(System.Reflection.Assembly assembly)
        {
            string name = assembly.GetName().Name;
            return name == "SDModKit" || name == "StationKit";
        }

        // A mod's name as MelonLoader knows it, without the game's name in front
        private static string Name(System.Reflection.Assembly assembly)
        {
            string name = MelonLoader.MelonBase.RegisteredMelons.FirstOrDefault(m => m.MelonAssembly?.Assembly == assembly)?.Info?.Name ?? assembly.GetName().Name;
            foreach (string prefix in new[] { "StellarDrive Demo ", "StellarDriveDemo", "StellarDrive " })
                if (name.StartsWith(prefix, StringComparison.Ordinal) && name.Length > prefix.Length)
                    name = name.Substring(prefix.Length);
            return name.Trim();
        }
    }

    [HarmonyPatch(typeof(BuildMenu), "Start")]
    internal static class BuildSearchPatch
    {
        private static void Postfix(BuildMenu __instance) => BuildSearch.Attach(__instance);
    }
}

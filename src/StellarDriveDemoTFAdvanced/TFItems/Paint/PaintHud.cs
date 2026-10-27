using Players.Interface.Services;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using UnityEngine;
using Loc = StellarDriveDemoTFAdvanced.Common.Loc;

namespace StellarDriveDemoTFAdvanced.TFItems.Paint
{
    /// <summary>
    /// Paint HUD, replacing the game's paint menu. While the paint tool is held a small card shows
    /// the current paint and target; right click opens one panel with the color picker
    /// (saturation/value square and hue bar), quick colors, hex input, wall side mode, finish and
    /// presets. The game's menu still opens underneath (it frees the cursor and pauses the tool)
    /// but is hidden.
    /// </summary>
    internal static class PaintHud
    {
        public static bool ToolActive;
        public static bool MenuOpen;

        private const float PanelWidth = 452f;
        private const float CardWidth = 300f;
        private const int PickerSize = 176;

        // The game's menu palette (night blue panels, blue accent, pale blue text), as DeviceUi
        private static readonly Color Panel = new Color(0.05f, 0.12f, 0.19f, 0.93f);
        private static readonly Color Card = new Color(0.08f, 0.19f, 0.29f, 1f);
        private static readonly Color Chip = new Color(0.1f, 0.27f, 0.42f, 1f);
        private static readonly Color ChipHover = new Color(0.14f, 0.36f, 0.56f, 1f);
        private static readonly Color Accent = new Color(0.2f, 0.56f, 0.9f);
        private static readonly Color TextColor = new Color(0.86f, 0.94f, 1f);
        private static readonly Color MutedText = new Color(0.55f, 0.7f, 0.82f);

        private static readonly Color[] QuickColors =
        {
            new Color(0.95f, 0.95f, 0.95f), new Color(0.6f, 0.62f, 0.65f), new Color(0.3f, 0.31f, 0.33f), new Color(0.06f, 0.06f, 0.07f),
            new Color(0.8f, 0.1f, 0.1f), new Color(0.96f, 0.47f, 0.08f), new Color(0.98f, 0.82f, 0.15f), new Color(0.45f, 0.75f, 0.15f),
            new Color(0.12f, 0.45f, 0.22f), new Color(0.1f, 0.7f, 0.75f), new Color(0.15f, 0.4f, 0.9f), new Color(0.08f, 0.15f, 0.38f),
            new Color(0.5f, 0.2f, 0.75f), new Color(0.9f, 0.35f, 0.65f), new Color(0.45f, 0.28f, 0.15f), new Color(0.82f, 0.7f, 0.5f)
        };

        private static readonly (string Name, PaintFinish Finish)[] QuickFinishes =
        {
            (Loc.Pick("D'origine", "Original", "Original"), PaintFinish.None),
            ("Mat", PaintFinish.Of(0.05f, 0f, 0f)),
            ("Satiné", PaintFinish.Of(0.45f, 0f, 0f)),
            ("Brillant", PaintFinish.Of(0.92f, 0f, 0f)),
            ("Brossé", PaintFinish.Of(0.45f, 1f, 0f)),
            ("Chrome", PaintFinish.Of(0.97f, 1f, 0f)),
            ("Néon", PaintFinish.Of(0.6f, 0f, 0.7f))
        };

        private enum Drag { None, SaturationValue, Hue }

        private static Texture2D _white, _hueBar, _svSquare, _camoPreview;
        private static string _camoPreviewKey;
        private static GUIStyle _panelStyle, _cardStyle, _chipStyle, _chipActiveStyle, _swatchFrame;
        private static GUIStyle _title, _section, _label, _muted, _big, _field, _toggle;
        private static Vector2 _panelScroll;
        private static string _hexInput = "";
        private static string _presetName = "";
        private static bool _finishDirty;

        private static float _hue, _saturation, _value;
        private static Color _syncedColor = new Color(-1f, 0f, 0f);
        private static float _svTextureHue = -1f;
        private static Drag _drag;

        public static void Update()
        {
            if (_finishDirty && !MenuOpen)
            {
                _finishDirty = false;
                PaintBrush.Save();
            }
        }

        public static void Draw()
        {
            if (!ToolActive)
                return;
            var selection = GameServices.PaintSelection;
            if (selection == null)
                return;

            float scale = Mathf.Clamp(Screen.height / 1080f, 0.7f, 2.5f);
            EnsureStyles();
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;

            SyncHsv(selection.SelectedPaintColor);
            if (MenuOpen)
                DrawPanel(selection, width, height);
            else
                DrawCard(selection, width, height);

            GUI.matrix = previous;
        }

        // ---- Compact card ----

        private static void DrawCard(IPlayerPaintToolSelectionTracker selection, float width, float height)
        {
            var rect = new Rect(width - CardWidth - 28f, height - 214f, CardWidth, 178f);
            Devices.DeviceUi.Panel(rect);
            Fill(new Rect(rect.x + 14f, rect.y, 46f, 3f), Accent);

            GUILayout.BeginArea(new Rect(rect.x + 16f, rect.y + 12f, rect.width - 32f, rect.height - 22f));
            GUILayout.Label(Loc.Pick("PINCEAU", "BRUSH", "PINCEL"), _title);
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            Rect swatch = GUILayoutUtility.GetRect(58f, 58f, GUILayout.Width(58f), GUILayout.Height(58f));
            if (Camouflage.Enabled)
                DrawCamoSwatch(swatch, selection.SelectedPaintColor);
            else
                DrawSwatch(swatch, selection.SelectedPaintColor, PaintBrush.Finish);
            GUILayout.Space(12f);
            GUILayout.BeginVertical();
            if (Camouflage.Enabled)
                GUILayout.Label("Camo " + Camouflage.Palette.Name, _big);
            else
                GUILayout.Label("#" + ColorUtility.ToHtmlStringRGB(selection.SelectedPaintColor), _big);
            GUILayout.Label(FinishText(PaintBrush.Finish), _muted);
            GUILayout.Label(selection.PaintBothSides ? Loc.Pick("Murs : deux faces", "Walls: both sides", "Paredes: dos caras") : Loc.Pick("Murs : face visée", "Walls: aimed side", "Paredes: cara apuntada"), _muted);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(TargetText(), _label);
            GUILayout.Label(Loc.Pick("Clic G peindre  ·  Clic D palette  ·  Molette pipette", "L-click paint  ·  R-click palette  ·  Wheel eyedropper", "Clic izq. pintar  ·  Clic der. paleta  ·  Rueda cuentagotas"), _muted);
            GUILayout.EndArea();
        }

        // ---- Full panel ----

        private static void DrawPanel(IPlayerPaintToolSelectionTracker selection, float width, float height)
        {
            float panelHeight = Mathf.Min(height - 60f, 860f);
            var rect = new Rect(width - PanelWidth - 28f, (height - panelHeight) / 2f, PanelWidth, panelHeight);
            Devices.DeviceUi.Panel(rect);
            Fill(new Rect(rect.x + 18f, rect.y, 60f, 3f), Accent);

            GUILayout.BeginArea(new Rect(rect.x + 18f, rect.y + 14f, rect.width - 36f, rect.height - 28f));
            GUILayout.BeginHorizontal();
            GUILayout.Label(Loc.Pick("PINCEAU TF", "TF BRUSH", "PINCEL TF"), _title);
            GUILayout.FlexibleSpace();
            GUILayout.Label(Loc.Pick("clic droit pour fermer", "right click to close", "clic derecho para cerrar"), _muted);
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);

            // The sections are taller than a 1080p screen: they scroll, so the presets stay reachable
            _panelScroll = GUILayout.BeginScrollView(_panelScroll, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.ExpandHeight(true));
            DrawMode();
            GUILayout.Space(8f);
            if (Camouflage.Enabled)
            {
                DrawCamouflage(selection);
            }
            else
            {
                DrawPicker(selection);
                GUILayout.Space(10f);
                DrawQuickColors(selection);
            }
            DrawSides(selection);
            DrawMaterial();
            DrawFinish();
            DrawPresets(selection);
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        private static void DrawMode()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Loc.Pick("Couleur unie", "Plain color", "Color liso"), Camouflage.Enabled ? _chipStyle : _chipActiveStyle, GUILayout.Height(30f)))
                SetCamouflage(false);
            GUILayout.Space(6f);
            if (GUILayout.Button(Loc.Pick("Camouflage", "Camouflage", "Camuflaje"), Camouflage.Enabled ? _chipActiveStyle : _chipStyle, GUILayout.Height(30f)))
                SetCamouflage(true);
            GUILayout.EndHorizontal();
        }

        private static void DrawCamouflage(IPlayerPaintToolSelectionTracker selection)
        {
            // Pattern preview on a flat wall, one texel per wall segment
            Rect preview = GUILayoutUtility.GetRect(100f, 96f, GUILayout.ExpandWidth(true), GUILayout.Height(96f));
            UpdateCamoPreview(selection.SelectedPaintColor);
            GUI.Box(preview, GUIContent.none, _swatchFrame);
            GUI.DrawTexture(new Rect(preview.x + 2f, preview.y + 2f, preview.width - 4f, preview.height - 4f), _camoPreview, ScaleMode.StretchToFill);
            GUILayout.Space(8f);

            // Palettes, two per row
            for (int i = 0; i < Camouflage.Palettes.Length; i += 2)
            {
                GUILayout.BeginHorizontal();
                for (int j = i; j < Mathf.Min(i + 2, Camouflage.Palettes.Length); j++)
                {
                    PaletteChip(j, selection.SelectedPaintColor);
                    if (j == i)
                        GUILayout.Space(6f);
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(5f);
            }

            GUILayout.BeginHorizontal();
            for (int i = 0; i < Camouflage.StyleNames.Length; i++)
            {
                if (GUILayout.Button(Camouflage.StyleNames[i], (int)Camouflage.Style == i ? _chipActiveStyle : _chipStyle, GUILayout.Height(26f)))
                {
                    Camouflage.Style = (CamoStyle)i;
                    _finishDirty = true;
                }
                GUILayout.Space(5f);
            }
            if (GUILayout.Button(Loc.Pick("Nouveau motif", "New pattern", "Nuevo patrón"), _chipStyle, GUILayout.Height(26f)))
            {
                Camouflage.Seed++;
                _finishDirty = true;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);

            float t = (Camouflage.Scale - Camouflage.MinScale) / (Camouflage.MaxScale - Camouflage.MinScale);
            float moved = Slider(Loc.Pick("Taches", "Patches", "Manchas"), t, true, Mathf.RoundToInt(Camouflage.Scale) + " m");
            if (!Mathf.Approximately(moved, t))
            {
                Camouflage.Scale = Mathf.Lerp(Camouflage.MinScale, Camouflage.MaxScale, moved);
                _finishDirty = true;
            }

            if (Camouflage.PaletteIndex == Camouflage.Palettes.Length - 1)
            {
                GUILayout.Label(Loc.Pick("Ton sur ton : nuances de la couleur de base choisie ici.", "Tone on tone: shades of the base color chosen here.", "Tono sobre tono: matices del color base elegido aquí."), _muted);
                DrawQuickColors(selection);
            }
            else
            {
                GUILayout.Label(Loc.Pick("Balaye murs, sols et pièces : chaque segment prend la couleur du motif à sa place.", "Sweep walls, floors and parts: each segment takes the pattern's color at its place.", "Barre paredes, suelos y piezas: cada segmento toma el color del patrón en su lugar."), _muted);
            }
        }

        private static void PaletteChip(int index, Color brush)
        {
            CamoPalette palette = Camouflage.Palettes[index];
            Rect chip = GUILayoutUtility.GetRect(196f, 34f, GUILayout.Width(196f), GUILayout.Height(34f));
            bool active = Camouflage.PaletteIndex == index;
            bool hover = chip.Contains(Event.current.mousePosition);
            GUI.Box(chip, GUIContent.none, active ? _chipActiveStyle : hover ? _chipStyle : _cardStyle);
            Color[] colors = palette.Colors(brush);
            for (int i = 0; i < colors.Length; i++)
                Fill(new Rect(chip.x + 7f + i * 13f, chip.y + 7f, 12f, 20f), colors[i]);
            GUI.Label(new Rect(chip.x + 64f, chip.y + 7f, chip.width - 70f, 20f), palette.Name, _label);
            if (GUI.Button(chip, GUIContent.none, GUIStyle.none))
            {
                Camouflage.PaletteIndex = index;
                _finishDirty = true;
            }
        }

        private static void DrawCamoSwatch(Rect rect, Color brush)
        {
            UpdateCamoPreview(brush);
            GUI.Box(rect, GUIContent.none, _swatchFrame);
            var inner = new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f);
            // A square from the left of the preview
            GUI.DrawTextureWithTexCoords(inner, _camoPreview, new Rect(0f, 0f, (float)_camoPreview.height / _camoPreview.width, 1f));
        }

        private static void UpdateCamoPreview(Color brush)
        {
            string key = $"{Camouflage.PaletteIndex}/{Camouflage.Style}/{Camouflage.Scale:0.00}/{Camouflage.Seed}/{ColorUtility.ToHtmlStringRGB(brush)}";
            if (_camoPreview != null && key == _camoPreviewKey)
                return;
            _camoPreviewKey = key;
            if (_camoPreview == null)
                _camoPreview = new Texture2D(48, 11, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            Camouflage.FillPreview(_camoPreview, brush, _camoPreview.width);
        }

        private static void SetCamouflage(bool enabled)
        {
            Camouflage.Enabled = enabled;
            _finishDirty = true;
        }

        private static void DrawPicker(IPlayerPaintToolSelectionTracker selection)
        {
            GUILayout.BeginHorizontal();
            Rect sv = GUILayoutUtility.GetRect(PickerSize, PickerSize, GUILayout.Width(PickerSize), GUILayout.Height(PickerSize));
            GUILayout.Space(10f);
            Rect hue = GUILayoutUtility.GetRect(20f, PickerSize, GUILayout.Width(20f), GUILayout.Height(PickerSize));
            GUILayout.Space(14f);

            GUILayout.BeginVertical();
            Rect preview = GUILayoutUtility.GetRect(150f, 74f, GUILayout.ExpandWidth(true), GUILayout.Height(74f));
            DrawSwatch(preview, selection.SelectedPaintColor, PaintBrush.Finish);
            GUILayout.Space(8f);
            Color32 rgb = selection.SelectedPaintColor;
            GUILayout.Label($"R {rgb.r}   G {rgb.g}   B {rgb.b}", _muted);
            GUILayout.Label($"T {Mathf.RoundToInt(_hue * 360f)}°   S {Mathf.RoundToInt(_saturation * 100f)}%   L {Mathf.RoundToInt(_value * 100f)}%", _muted);
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            _hexInput = GUILayout.TextField(_hexInput, 7, _field, GUILayout.Height(26f));
            if (GUILayout.Button("OK", _chipStyle, GUILayout.Width(44f), GUILayout.Height(26f)) && ColorUtility.TryParseHtmlString(Hex(_hexInput), out Color typed))
                SetColor(selection, typed);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            // Saturation (x) / value (y) square for the current hue, and the hue bar
            UpdateSvTexture();
            GUI.DrawTexture(sv, _svSquare);
            GUI.DrawTexture(hue, _hueBar);
            DrawRing(new Vector2(sv.x + _saturation * sv.width, sv.y + (1f - _value) * sv.height));
            Fill(new Rect(hue.x - 3f, hue.y + (1f - _hue) * hue.height - 2f, hue.width + 6f, 4f), Color.white);

            HandlePickerInput(selection, sv, hue);
        }

        private static void HandlePickerInput(IPlayerPaintToolSelectionTracker selection, Rect sv, Rect hue)
        {
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (sv.Contains(e.mousePosition))
                    _drag = Drag.SaturationValue;
                else if (hue.Contains(e.mousePosition))
                    _drag = Drag.Hue;
            }
            if (_drag != Drag.None && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag))
            {
                if (_drag == Drag.SaturationValue)
                {
                    _saturation = Mathf.Clamp01((e.mousePosition.x - sv.x) / sv.width);
                    _value = Mathf.Clamp01(1f - (e.mousePosition.y - sv.y) / sv.height);
                }
                else
                {
                    _hue = Mathf.Clamp01(1f - (e.mousePosition.y - hue.y) / hue.height);
                }
                Color color = Color.HSVToRGB(_hue, _saturation, _value);
                selection.SelectedPaintColor = color;
                _syncedColor = color;
                _hexInput = "#" + ColorUtility.ToHtmlStringRGB(color);
                e.Use();
            }
            if (e.rawType == EventType.MouseUp)
                _drag = Drag.None;
        }

        private static void DrawQuickColors(IPlayerPaintToolSelectionTracker selection)
        {
            for (int row = 0; row < QuickColors.Length / 8; row++)
            {
                GUILayout.BeginHorizontal();
                for (int col = 0; col < 8; col++)
                {
                    Color color = QuickColors[row * 8 + col];
                    Rect cell = GUILayoutUtility.GetRect(44f, 26f, GUILayout.Width(44f), GUILayout.Height(26f));
                    GUI.Box(cell, GUIContent.none, _swatchFrame);
                    Fill(new Rect(cell.x + 2f, cell.y + 2f, cell.width - 4f, cell.height - 4f), color);
                    if (GUI.Button(cell, GUIContent.none, GUIStyle.none))
                        SetColor(selection, color);
                    if (col < 7)
                        GUILayout.Space(5f);
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(5f);
            }
        }

        private static void DrawSides(IPlayerPaintToolSelectionTracker selection)
        {
            SectionHeader(Loc.Pick("Murs et sols", "Walls and floors", "Paredes y suelos"));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Loc.Pick("Face visée", "Aimed side", "Cara apuntada"), selection.PaintBothSides ? _chipStyle : _chipActiveStyle, GUILayout.Height(28f)))
                selection.PaintBothSides = false;
            GUILayout.Space(6f);
            if (GUILayout.Button(Loc.Pick("Les deux faces", "Both sides", "Las dos caras"), selection.PaintBothSides ? _chipActiveStyle : _chipStyle, GUILayout.Height(28f)))
                selection.PaintBothSides = true;
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);
            PaintZoop.Bucket = GUILayout.Toggle(PaintZoop.Bucket, Loc.Pick("  Pot de peinture : un clic peint toute la coque (pas les meubles, lumières ni machines)", "  Paint pot: one click paints the whole hull (not furniture, lights or machines)", "  Bote de pintura: un clic pinta todo el casco (no muebles, luces ni máquinas)"), _toggle);
            selection.ReplaceAllIdenticalColors = GUILayout.Toggle(selection.ReplaceAllIdenticalColors, Loc.Pick("  Remplacer toute la même couleur", "  Replace all of the same color", "  Reemplazar todo el mismo color"), _toggle);
            GUILayout.Label(Loc.Pick("Face visée : peins un côté, retourne-toi, peins l'autre d'une autre couleur.", "Aimed side: paint one side, turn around, paint the other in another color.", "Cara apuntada: pinta un lado, date la vuelta, pinta el otro de otro color."), _muted);
        }

        // Material variants of the parts built in several materials (TF Advanced station modules)
        private static readonly string[] Materials = { Loc.Pick("D'origine", "Original", "Original"), Loc.Pick("Métal", "Metal", "Metal"), Loc.Pick("Bois", "Wood", "Madera"), Loc.Pick("Métal sombre", "Dark metal", "Metal oscuro"), Loc.Pick("Blanc", "White", "Blanco"), Loc.Pick("Verre", "Glass", "Vidrio") };

        private static void DrawMaterial()
        {
            SectionHeader(Loc.Pick("Matériau — modules de station", "Material — station modules", "Material — módulos de estación"));
            for (int start = 0; start < Materials.Length; start += 3)
            {
                GUILayout.BeginHorizontal();
                for (int i = start; i < Mathf.Min(start + 3, Materials.Length); i++)
                {
                    bool active = PaintBrush.Material == i;
                    if (GUILayout.Button(Materials[i], active ? _chipActiveStyle : _chipStyle, GUILayout.Height(26f)))
                        PaintBrush.Material = (byte)i;
                    if (i < start + 2)
                        GUILayout.Space(5f);
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(5f);
            }
        }

        private static void DrawFinish()
        {
            SectionHeader(Loc.Pick("Finition — portes, machines, pièces", "Finish — doors, machines, parts", "Acabado — puertas, máquinas, piezas"));
            for (int start = 0; start < QuickFinishes.Length; start += 4)
            {
                GUILayout.BeginHorizontal();
                for (int i = start; i < Mathf.Min(start + 4, QuickFinishes.Length); i++)
                {
                    bool active = PaintBrush.Finish.Equals(QuickFinishes[i].Finish);
                    if (GUILayout.Button(QuickFinishes[i].Name, active ? _chipActiveStyle : _chipStyle, GUILayout.Height(26f)))
                        SetFinish(QuickFinishes[i].Finish);
                    if (i < start + 3)
                        GUILayout.Space(5f);
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(5f);
            }

            PaintFinish finish = PaintBrush.Finish;
            float gloss = Slider(Loc.Pick("Brillance", "Gloss", "Brillo"), finish.Enabled ? finish.GlossValue : 0.5f, finish.Enabled);
            float metal = Slider(Loc.Pick("Métal", "Metal", "Metal"), finish.Enabled ? finish.MetalValue : 0f, finish.Enabled);
            float glow = Slider(Loc.Pick("Lumineux", "Glow", "Luminoso"), finish.Enabled ? finish.GlowValue : 0f, finish.Enabled);
            bool moved = finish.Enabled
                ? !Mathf.Approximately(gloss, finish.GlossValue) || !Mathf.Approximately(metal, finish.MetalValue) || !Mathf.Approximately(glow, finish.GlowValue)
                : !Mathf.Approximately(gloss, 0.5f) || metal > 0f || glow > 0f;
            if (moved)
                SetFinish(PaintFinish.Of(gloss, metal, glow));
        }

        private static void DrawPresets(IPlayerPaintToolSelectionTracker selection)
        {
            SectionHeader(Loc.Pick("Préréglages", "Presets", "Preajustes"));
            GUILayout.BeginHorizontal();
            _presetName = GUILayout.TextField(_presetName, 24, _field, GUILayout.Height(26f));
            GUILayout.Space(6f);
            if (GUILayout.Button(Loc.Pick("+ Enregistrer", "+ Save", "+ Guardar"), _chipActiveStyle, GUILayout.Width(118f), GUILayout.Height(26f)))
            {
                PaintBrush.AddPreset(_presetName);
                _presetName = "";
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);

            PaintPreset toRemove = null;
            var presets = PaintBrush.Presets;
            for (int i = 0; i < presets.Count; i += 2)
            {
                GUILayout.BeginHorizontal();
                for (int j = i; j < Mathf.Min(i + 2, presets.Count); j++)
                {
                    if (PresetChip(selection, presets[j]))
                        toRemove = presets[j];
                    if (j == i)
                        GUILayout.Space(6f);
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(6f);
            }
            if (toRemove != null)
                PaintBrush.RemovePreset(toRemove);
        }

        // Returns true when the preset's delete button was clicked
        private static bool PresetChip(IPlayerPaintToolSelectionTracker selection, PaintPreset preset)
        {
            Rect chip = GUILayoutUtility.GetRect(196f, 40f, GUILayout.Width(196f), GUILayout.Height(40f));
            bool hover = chip.Contains(Event.current.mousePosition);
            GUI.Box(chip, GUIContent.none, hover ? _chipActiveStyle : _cardStyle);
            DrawSwatch(new Rect(chip.x + 6f, chip.y + 6f, 28f, 28f), preset.Color, preset.Finish);
            GUI.Label(new Rect(chip.x + 42f, chip.y + 3f, chip.width - 70f, 20f), preset.name, _label);
            GUI.Label(new Rect(chip.x + 42f, chip.y + 20f, chip.width - 70f, 18f), ShortFinish(preset.Finish), _muted);
            var delete = new Rect(chip.xMax - 24f, chip.y + 10f, 18f, 20f);
            if (hover && GUI.Button(delete, "x", _muted))
                return true;
            if (GUI.Button(chip, GUIContent.none, GUIStyle.none))
            {
                PaintBrush.Apply(preset);
                _syncedColor = new Color(-1f, 0f, 0f);
            }
            return false;
        }

        // ---- State ----

        // Follows color changes made elsewhere (pipette, presets) without losing the hue on greys
        private static void SyncHsv(Color color)
        {
            if (_drag != Drag.None || ColorClose(color, _syncedColor))
                return;
            Color.RGBToHSV(color, out float h, out float s, out float v);
            if (s > 0.001f && v > 0.001f)
                _hue = h;
            _saturation = v > 0.001f ? s : _saturation;
            _value = v;
            _syncedColor = color;
            _hexInput = "#" + ColorUtility.ToHtmlStringRGB(color);
        }

        private static void SetColor(IPlayerPaintToolSelectionTracker selection, Color color)
        {
            selection.SelectedPaintColor = color;
            _syncedColor = new Color(-1f, 0f, 0f);
        }

        private static void SetFinish(PaintFinish finish)
        {
            PaintBrush.Finish = finish;
            _finishDirty = true;
        }

        private static bool ColorClose(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 0.002f && Mathf.Abs(a.g - b.g) < 0.002f && Mathf.Abs(a.b - b.b) < 0.002f;

        // ---- Drawing helpers ----

        private static float Slider(string label, float value, bool enabled, string valueText = null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, enabled ? _label : _muted, GUILayout.Width(86f));
            Rect track = GUILayoutUtility.GetRect(100f, 22f, GUILayout.ExpandWidth(true), GUILayout.Height(22f));
            Fill(new Rect(track.x, track.y + 9f, track.width, 4f), Chip);
            Fill(new Rect(track.x, track.y + 9f, track.width * value, 4f), enabled ? Accent : MutedText);
            Fill(new Rect(track.x + track.width * value - 5f, track.y + 4f, 10f, 14f), enabled ? Color.white : MutedText);
            Event e = Event.current;
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0 && track.Contains(e.mousePosition))
            {
                value = Mathf.Clamp01((e.mousePosition.x - track.x) / track.width);
                e.Use();
            }
            GUILayout.Label(valueText ?? Mathf.RoundToInt(value * 100f) + "%", _muted, GUILayout.Width(40f));
            GUILayout.EndHorizontal();
            return value;
        }

        private static void SectionHeader(string text)
        {
            GUILayout.Space(12f);
            GUILayout.Label(text.ToUpperInvariant(), _section);
            Rect line = GUILayoutUtility.GetRect(10f, 1f, GUILayout.ExpandWidth(true), GUILayout.Height(1f));
            Fill(line, Chip);
            GUILayout.Space(6f);
        }

        // Swatch showing the finish: shine for gloss, darker lower half for metal, halo for glow
        private static void DrawSwatch(Rect rect, Color color, PaintFinish finish)
        {
            if (finish.Enabled && finish.Glow > 0)
                Fill(new Rect(rect.x - 3f, rect.y - 3f, rect.width + 6f, rect.height + 6f), new Color(color.r, color.g, color.b, 0.35f + 0.5f * finish.GlowValue));
            GUI.Box(rect, GUIContent.none, _swatchFrame);
            var inner = new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f);
            Fill(inner, color);
            if (!finish.Enabled)
                return;
            if (finish.Metal > 0)
                Fill(new Rect(inner.x, inner.y + inner.height * 0.5f, inner.width, inner.height * 0.5f), new Color(0f, 0f, 0f, 0.3f * finish.MetalValue));
            if (finish.Gloss > 0)
                Fill(new Rect(inner.x + inner.width * 0.1f, inner.y + inner.height * 0.12f, inner.width * 0.4f, Mathf.Max(2f, inner.height * 0.1f)), new Color(1f, 1f, 1f, 0.7f * finish.GlossValue));
        }

        private static void DrawRing(Vector2 center)
        {
            Fill(new Rect(center.x - 6f, center.y - 6f, 12f, 12f), Color.white);
            Fill(new Rect(center.x - 4f, center.y - 4f, 8f, 8f), Color.black);
            Fill(new Rect(center.x - 2f, center.y - 2f, 4f, 4f), Color.HSVToRGB(_hue, _saturation, _value));
        }

        private static string FinishText(PaintFinish finish)
        {
            if (!finish.Enabled)
                return Loc.Pick("Finition d'origine", "Original finish", "Acabado original");
            string text = Loc.Pick($"Brillance {Mathf.RoundToInt(finish.GlossValue * 100f)}% · Métal {Mathf.RoundToInt(finish.MetalValue * 100f)}%", $"Gloss {Mathf.RoundToInt(finish.GlossValue * 100f)}% · Metal {Mathf.RoundToInt(finish.MetalValue * 100f)}%", $"Brillo {Mathf.RoundToInt(finish.GlossValue * 100f)}% · Metal {Mathf.RoundToInt(finish.MetalValue * 100f)}%");
            if (finish.Glow > 0)
                text += Loc.Pick($" · Lumineux {Mathf.RoundToInt(finish.GlowValue * 100f)}%", $" · Glow {Mathf.RoundToInt(finish.GlowValue * 100f)}%", $" · Luminoso {Mathf.RoundToInt(finish.GlowValue * 100f)}%");
            return text;
        }

        private static string ShortFinish(PaintFinish finish)
        {
            if (!finish.Enabled)
                return "d'origine";
            foreach ((string name, PaintFinish quick) in QuickFinishes)
            {
                if (quick.Equals(finish))
                    return name.ToLowerInvariant();
            }
            return $"B{Mathf.RoundToInt(finish.GlossValue * 100f)} M{Mathf.RoundToInt(finish.MetalValue * 100f)} L{Mathf.RoundToInt(finish.GlowValue * 100f)}";
        }

        private static string TargetText()
        {
            if (PaintToolPatches.HoveringHull)
                return Camouflage.Enabled ? Loc.Pick("> Mur / sol — camouflage", "> Wall / floor — camouflage", "> Pared / suelo — camuflaje") : Loc.Pick("> Mur / sol — couleur", "> Wall / floor — color", "> Pared / suelo — color");
            if (PaintToolPatches.HoveredPart.HasValue)
                return PaintToolPatches.HoveredPart.Value.IsInterior ? Loc.Pick("> Pièce — intérieur (couleur + finition)", "> Part — inside (color + finish)", "> Pieza — interior (color + acabado)") : Loc.Pick("> Pièce — couleur + finition", "> Part — color + finish", "> Pieza — color + acabado");
            return Loc.Pick("> Aucune cible", "> No target", "> Sin objetivo");
        }

        private static string Hex(string text)
        {
            text = text.Trim();
            return text.StartsWith("#") ? text : "#" + text;
        }

        private static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _white);
            GUI.color = previous;
        }

        // ---- Textures and styles ----

        private static void UpdateSvTexture()
        {
            if (_svSquare != null && Mathf.Abs(_svTextureHue - _hue) < 0.002f)
                return;
            _svTextureHue = _hue;
            const int size = 96;
            if (_svSquare == null)
                _svSquare = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                pixels[y * size + x] = Color.HSVToRGB(_hue, x / (size - 1f), y / (size - 1f));
            _svSquare.SetPixels(pixels);
            _svSquare.Apply();
        }

        private static void EnsureStyles()
        {
            if (_title != null)
                return;

            _white = Solid(Color.white);
            _hueBar = new Texture2D(1, 128, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < 128; y++)
                _hueBar.SetPixel(0, y, Color.HSVToRGB(y / 127f, 1f, 1f));
            _hueBar.Apply();

            _panelStyle = Rounded(Panel, 12);
            _cardStyle = Rounded(Card, 8);
            _chipStyle = Rounded(Chip, 7);
            _chipStyle.hover.background = RoundedTexture(ChipHover, 7);
            _chipStyle.active.background = RoundedTexture(Accent * 0.7f, 7);
            _chipStyle.alignment = TextAnchor.MiddleCenter;
            _chipStyle.fontSize = 13;
            _chipStyle.normal.textColor = _chipStyle.hover.textColor = _chipStyle.active.textColor = TextColor;
            _chipActiveStyle = new GUIStyle(_chipStyle) { fontStyle = FontStyle.Bold };
            _chipActiveStyle.normal.background = _chipActiveStyle.hover.background = RoundedTexture(new Color(Accent.r * 0.85f, Accent.g * 0.85f, Accent.b * 0.85f, 1f), 7);
            _chipActiveStyle.normal.textColor = _chipActiveStyle.hover.textColor = Color.white;
            _swatchFrame = Rounded(new Color(1f, 1f, 1f, 0.18f), 5);

            _title = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
            if (Devices.DeviceUi.GameFont != null)
                _title.font = Devices.DeviceUi.GameFont;
            _title.normal.textColor = TextColor;
            _section = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold };
            if (Devices.DeviceUi.GameFont != null)
                _section.font = Devices.DeviceUi.GameFont;
            _section.normal.textColor = Accent;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = false, clipping = TextClipping.Clip };
            if (Devices.DeviceUi.GameFont != null)
                _label.font = Devices.DeviceUi.GameFont;
            _label.normal.textColor = TextColor;
            _muted = new GUIStyle(_label) { fontSize = 11, wordWrap = true };
            _muted.normal.textColor = MutedText;
            _big = new GUIStyle(_label) { fontSize = 22, fontStyle = FontStyle.Bold };
            _field = new GUIStyle(GUI.skin.textField) { fontSize = 13, padding = new RectOffset(8, 8, 5, 5) };
            if (Devices.DeviceUi.GameFont != null)
                _field.font = Devices.DeviceUi.GameFont;
            _field.normal.background = _field.focused.background = _field.hover.background = RoundedTexture(new Color(0.03f, 0.035f, 0.045f, 1f), 6);
            _field.border = new RectOffset(8, 8, 8, 8);
            _field.normal.textColor = _field.focused.textColor = _field.hover.textColor = TextColor;
            _toggle = new GUIStyle(GUI.skin.toggle) { fontSize = 13 };
            if (Devices.DeviceUi.GameFont != null)
                _toggle.font = Devices.DeviceUi.GameFont;
            _toggle.normal.textColor = _toggle.onNormal.textColor = _toggle.hover.textColor = _toggle.onHover.textColor = TextColor;
        }

        private static GUIStyle Rounded(Color color, int radius) => UiTextures.RoundedStyle(color, radius);

        private static Texture2D RoundedTexture(Color color, int radius) => UiTextures.Rounded(color, radius);

        private static Texture2D Solid(Color color) => UiTextures.Solid(color);
    }
}

using System.Collections.Generic;
using System.Linq;
using FishNet.Connection;
using Ships.Interface.Model.Parts;
using Ships.Interface.Settings;
using StellarDriveDemoTFAdvanced.Common;
using StellarDriveDemoTFAdvanced.Parts;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using StellarDriveDemoTFAdvanced.TFItems.Devices;
using UnityEngine;
using UnityEngine.Rendering;
using Loc = StellarDriveDemoTFAdvanced.Common.Loc;
using MeshBuilder = StellarDriveDemoTFAdvanced.Common.MeshBuilder;
using LitMaterials = StellarDriveDemoTFAdvanced.Common.LitMaterials;

namespace StellarDriveDemoTFAdvanced.Construction
{
    /// <summary>
    /// Futuristic signs to name rooms: a lit wall panel, a small door plate and a standing hologram.
    /// Look at one and press the use key (T) to write its text and pick its color; every player
    /// sees it, the host keeps it with the world (tf-data.json, "c|text": c the color's index).
    /// The panel and the plate lie on the surface they are placed on, their text facing out of it,
    /// its top toward the part's +Z (turn it with the rotate key); the hologram stands on a floor.
    /// </summary>
    internal static class Signs
    {
        public const ushort PanelId = 8230, PlateId = 8231, HologramId = 8232;
        public const int MaxLength = 40;

        public static readonly Color[] Colors =
        {
            new Color(0.35f, 0.9f, 1f), Color.white, new Color(1f, 0.6f, 0.15f), new Color(0.35f, 1f, 0.45f),
            new Color(1f, 0.25f, 0.2f), new Color(0.75f, 0.45f, 1f), new Color(1f, 0.9f, 0.3f)
        };

        public static string[] ColorNames => new[]
        {
            Loc.Pick("Cyan", "Cyan", "Cian"), Loc.Pick("Blanc", "White", "Blanco"), Loc.Pick("Orange", "Orange", "Naranja"), Loc.Pick("Vert", "Green", "Verde"),
            Loc.Pick("Rouge", "Red", "Rojo"), Loc.Pick("Violet", "Violet", "Violeta"), Loc.Pick("Jaune", "Yellow", "Amarillo")
        };

        /// <summary>Host: sign → "c|text". Saved with the world.</summary>
        internal static readonly Dictionary<PartKey, string> ServerTexts = new Dictionary<PartKey, string>();
        internal static readonly Dictionary<PartKey, string> ClientTexts = new Dictionary<PartKey, string>();

        public static bool IsSign(ushort id) => id == PanelId || id == PlateId || id == HologramId;

        private sealed class Spec
        {
            public ushort Id;
            public string Label, Description;
            public Vector3 Size;
            public (uint, int)[] Cost;
            public bool Standing;
        }

        private static readonly Spec[] All =
        {
            new Spec { Id = PanelId, Size = new Vector3(1.2f, 0.06f, 0.36f), Cost = new[] { (101u, 2), (102u, 2), (112u, 1) },
                Label = Loc.Pick("Panneau lumineux", "Lit sign panel", "Panel luminoso"),
                Description = Loc.Pick("Un panneau de verre fumé dans un cadre d'acier, son texte en lettres lumineuses : pour nommer une pièce (Pont, Infirmerie, Soute...). Pose-le sur un mur, regarde-le et appuie sur la touche d'utilisation (T) pour écrire le texte et choisir sa couleur. Tous les joueurs le voient.", "A smoked glass panel in a steel frame, its text in glowing letters: to name a room (Bridge, Sickbay, Cargo...). Put it on a wall, look at it and press the use key (T) to write the text and pick its color. Every player sees it.", "Un panel de cristal ahumado en un marco de acero, con el texto en letras luminosas: para nombrar una sala (Puente, Enfermería, Bodega...). Colócalo en una pared, míralo y pulsa la tecla de uso (T) para escribir el texto y elegir su color. Todos los jugadores lo ven.") },
            new Spec { Id = PlateId, Size = new Vector3(0.5f, 0.04f, 0.16f), Cost = new[] { (101u, 1), (102u, 1) },
                Label = Loc.Pick("Plaque de porte", "Door plate", "Placa de puerta"),
                Description = Loc.Pick("Une petite plaque lumineuse à côté d'une porte, avec une barre de couleur : le nom ou le numéro de la pièce. T pour écrire.", "A small lit plate beside a door, with a color bar: the room's name or number. T to write it.", "Una pequeña placa luminosa junto a una puerta, con una barra de color: el nombre o el número de la sala. T para escribirlo.") },
            new Spec { Id = HologramId, Size = new Vector3(1.2f, 2f, 0.4f), Cost = new[] { (101u, 3), (112u, 3), (102u, 2), (103u, 1) }, Standing = true,
                Label = Loc.Pick("Enseigne holographique", "Holographic sign", "Letrero holográfico"),
                Description = Loc.Pick("Un projecteur au sol qui fait flotter le texte dans l'air, à hauteur des yeux, dans un halo : l'entrée d'une zone, un hangar, une boutique. T pour écrire.", "A floor projector floating the text in the air at eye height, in a glow: the entrance of an area, a hangar, a shop. T to write it.", "Un proyector de suelo que hace flotar el texto en el aire a la altura de los ojos, en un halo: la entrada de una zona, un hangar, una tienda. T para escribirlo.") },
        };

        public static void Register()
        {
            foreach (Spec part in All)
            {
                Spec spec = part;
                PartFactory.Register(spec.Id, "SignalDisplay", AdvancedTab.ConstructionName, AdvancedTab.SignRow, (settings, prefab) => Configure(spec, settings, prefab));
            }
        }

        private static void Configure(Spec spec, PartSettings settings, GameObject prefab)
        {
            PartFactory.Describe(settings, spec.Label, spec.Description, spec.Standing ? 25f : 3f, spec.Cost);
            settings.internalStateClass = null;
            settings.snappingStyle = spec.Standing ? Ships.Interface.Model.SnappingStyle.PreciseOnFloor : Ships.Interface.Model.SnappingStyle.PreciseOnAny;
            Transform visuals = PartFactory.BlankFromSignalDisplay(prefab, new Vector3(0f, spec.Size.y * 0.5f, 0f), spec.Size);
            int layer = visuals.gameObject.layer;
            var frame = new MeshBuilder();
            var dark = new MeshBuilder();
            var glow = new MeshBuilder();
            Vector3 s = spec.Size;
            // The text: where it sits, which way it reads (a TextMeshPro reads from behind its forward), its box
            Vector3 textAt;
            Quaternion textRotation;
            Vector2 textBox;
            if (spec.Id == PanelId)
            {
                // A bevelled steel frame, a smoked glass face, lit strips top and bottom
                frame.Box(new Vector3(0f, 0.015f, 0f), new Vector3(s.x, 0.03f, s.z));
                foreach (float z in new[] { -1f, 1f })
                {
                    frame.Box(new Vector3(0f, 0.04f, z * (s.z * 0.5f - 0.015f)), new Vector3(s.x, 0.02f, 0.03f));
                    glow.Box(new Vector3(0f, 0.051f, z * (s.z * 0.5f - 0.04f)), new Vector3(s.x - 0.12f, 0.004f, 0.008f));
                }
                foreach (float x in new[] { -1f, 1f })
                {
                    frame.Box(new Vector3(x * (s.x * 0.5f - 0.015f), 0.04f, 0f), new Vector3(0.03f, 0.02f, s.z - 0.06f));
                    // Corner caps, cut at an angle
                    frame.Box(new Vector3(x * (s.x * 0.5f - 0.05f), 0.052f, 0f), new Vector3(0.05f, 0.012f, s.z * 0.6f));
                }
                dark.Box(new Vector3(0f, 0.035f, 0f), new Vector3(s.x - 0.06f, 0.01f, s.z - 0.06f));
                textAt = new Vector3(0f, 0.042f, 0f);
                textRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
                textBox = new Vector2(s.x - 0.16f, s.z - 0.1f);
            }
            else if (spec.Id == PlateId)
            {
                frame.Box(new Vector3(0f, 0.01f, 0f), new Vector3(s.x, 0.02f, s.z));
                dark.Box(new Vector3(0.03f, 0.025f, 0f), new Vector3(s.x - 0.1f, 0.01f, s.z - 0.03f));
                // The color bar on the left
                glow.Box(new Vector3(-s.x * 0.5f + 0.03f, 0.026f, 0f), new Vector3(0.02f, 0.012f, s.z - 0.04f));
                textAt = new Vector3(0.03f, 0.031f, 0f);
                textRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
                textBox = new Vector2(s.x - 0.14f, s.z - 0.04f);
            }
            else
            {
                // A round base with a lens, a halo ring over it, the text floating at eye height
                frame.Cylinder(new Vector3(0f, 0f, 0f), new Vector3(0f, 0.08f, 0f), 0.2f, 0.18f, 24);
                dark.Cylinder(new Vector3(0f, 0.08f, 0f), new Vector3(0f, 0.1f, 0f), 0.12f, 0.1f, 24);
                glow.Cylinder(new Vector3(0f, 0.1f, 0f), new Vector3(0f, 0.105f, 0f), 0.08f, 0.08f, 20);
                glow.Box(new Vector3(0f, 1.25f, 0f), new Vector3(s.x - 0.1f, 0.006f, 0.006f));
                glow.Box(new Vector3(0f, 1.85f, 0f), new Vector3(s.x - 0.1f, 0.006f, 0.006f));
                textAt = new Vector3(0f, 1.55f, 0f);
                // Read from the front (-Z), like the player's other parts
                textRotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
                textBox = new Vector2(s.x - 0.1f, 0.5f);
            }
            Add(visuals, layer, "TFA_Sign" + spec.Id + "Frame", frame, LitMaterials.Get("TFA_SignFrame", new Color(0.7f, 0.72f, 0.76f), 0.6f, 0.5f), ShadowCastingMode.On);
            Add(visuals, layer, "TF_NoPaint_Sign" + spec.Id + "Dark", dark, LitMaterials.Get("TFA_SignGlass", new Color(0.03f, 0.05f, 0.07f), 0.3f, 0.85f), ShadowCastingMode.Off);
            MeshRenderer light = Add(visuals, layer, "TF_NoPaint_Sign" + spec.Id + "Glow", glow, LitMaterials.Get("TFA_SignGlow", Colors[0], 0f, 0.6f, Colors[0] * 1.4f), ShadowCastingMode.Off);
            var text = new GameObject("TF_NoPaint_Sign" + spec.Id + "Text") { layer = layer };
            text.transform.SetParent(visuals, false);
            text.transform.localPosition = textAt;
            text.transform.localRotation = textRotation;
            var tmp = text.AddComponent<TMPro.TextMeshPro>();
            tmp.font = ShipComputer.ComputerStyle.Font;
            tmp.text = spec.Label;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.3f;
            tmp.fontSizeMax = spec.Id == PlateId ? 0.9f : 2.6f;
            tmp.enableWordWrapping = true;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.color = Colors[0];
            tmp.rectTransform.sizeDelta = textBox;
            UsablePart.AddBox(visuals, new Vector3(0f, s.y * 0.5f, 0f), s + Vector3.one * 0.06f);
            var visual = visuals.gameObject.AddComponent<SignVisuals>();
            visual.Default = spec.Label;
        }

        private static MeshRenderer Add(Transform parent, int layer, string name, MeshBuilder builder, Material material, ShadowCastingMode shadows)
        {
            if (!builder.HasGeometry || material == null)
                return null;
            var child = new GameObject(name) { layer = layer };
            child.transform.SetParent(parent, false);
            child.AddComponent<MeshFilter>().sharedMesh = builder.Build(name, new Vector2(0.125f, 0.5f));
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows;
            return renderer;
        }

        // ---- Text: "c|text"

        public static string Clean(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";
            var clean = new System.Text.StringBuilder();
            foreach (char c in text.Trim())
            {
                if (clean.Length >= MaxLength)
                    break;
                if (!char.IsControl(c) && c != '<' && c != '>' && c != '|')
                    clean.Append(c);
            }
            return clean.ToString().Trim();
        }

        public static (int Color, string Text) Parse(string value)
        {
            if (string.IsNullOrEmpty(value))
                return (0, null);
            int bar = value.IndexOf('|');
            if (bar < 1 || !int.TryParse(value.Substring(0, bar), out int color))
                return (0, Clean(value));
            return (Mathf.Clamp(color, 0, Colors.Length - 1), Clean(value.Substring(bar + 1)));
        }

        public static string Format(int color, string text) => Mathf.Clamp(color, 0, Colors.Length - 1) + "|" + Clean(text);

        public static void Install()
        {
            TFNet.OnServer(TFMessageKind.SignText, OnServerSet);
            TFNet.OnClient(TFMessageKind.SignText, (kind, ship, part, value) =>
            {
                var key = new PartKey(ship, part);
                if (string.IsNullOrEmpty(value))
                    ClientTexts.Remove(key);
                else
                    ClientTexts[key] = value;
            });
            TFNet.SyncRequested += connection =>
            {
                foreach (KeyValuePair<PartKey, string> entry in ServerTexts.ToList())
                    TFNet.SendTo(connection, TFMessageKind.SignText, entry.Key.ShipId, entry.Key.PartId, entry.Value);
            };
            TFNet.Disconnected += () => ClientTexts.Clear();
        }

        private static void OnServerSet(NetworkConnection sender, TFMessageKind kind, uint shipId, ushort partId, string value)
        {
            var ships = TFItems.Common.GameServices.ShipsServer;
            if (ships == null || !ships.TryGetStatefulPart(new PartContext(shipId, partId), out var part) || part.Settings == null || !IsSign(part.Settings.id))
                return;
            (int color, string text) = Parse(value);
            var key = new PartKey(shipId, partId);
            string clean = string.IsNullOrEmpty(text) ? "" : Format(color, text);
            if (clean.Length == 0)
                ServerTexts.Remove(key);
            else
                ServerTexts[key] = clean;
            TFNet.SendToAll(TFMessageKind.SignText, shipId, partId, clean);
        }

        public static void Request(PartKey sign, int color, string text) =>
            TFNet.SendToServer(TFMessageKind.SignText, sign.ShipId, sign.PartId, Format(color, text));
    }

    /// <summary>A sign on screen: its text and color from the host, the use key opens its window.</summary>
    internal sealed class SignVisuals : UsablePart
    {
        public string Default;
        private TMPro.TextMeshPro _text;
        private Renderer _glow;
        private MaterialPropertyBlock _block;
        private string _shown;
        private float _next;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        public override string Label => Loc.Pick("Panneau", "Sign", "Letrero");

        public override void Use() => SignMenu.Show(Key);

        protected override void Awake()
        {
            base.Awake();
            _text = GetComponentInChildren<TMPro.TextMeshPro>(true);
            _glow = GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name.EndsWith("Glow"));
            _block = new MaterialPropertyBlock();
        }

        private void LateUpdate()
        {
            if (!HasContext || _text == null || Time.unscaledTime < _next)
                return;
            _next = Time.unscaledTime + 0.3f;
            Signs.ClientTexts.TryGetValue(Key, out string value);
            if (value == _shown)
                return;
            _shown = value;
            (int color, string text) = Signs.Parse(value);
            _text.text = string.IsNullOrEmpty(text) ? Default : text;
            Color c = Signs.Colors[color];
            _text.color = c;
            if (_glow != null)
            {
                _glow.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, c);
                _block.SetColor(EmissionColorId, c * 1.4f);
                _glow.SetPropertyBlock(_block);
            }
        }
    }

    /// <summary>The sign's window: its text and color.</summary>
    internal static class SignMenu
    {
        private static bool _open;
        private static PartKey _sign;
        private static string _field = "";
        private static int _color;

        public static void Show(PartKey sign)
        {
            _sign = sign;
            _open = true;
            Signs.ClientTexts.TryGetValue(sign, out string value);
            (int color, string text) = Signs.Parse(value);
            _field = text ?? "";
            _color = color;
            ModMenu.Open(() => _open = false);
        }

        public static void Draw()
        {
            if (!_open || !ModMenu.IsOpen)
                return;
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.7f, 2.5f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            Rect area = DeviceUi.Window(Screen.width / scale, Screen.height / scale, 560f, 330f, Loc.Pick("PANNEAU", "SIGN", "LETRERO"));
            GUILayout.BeginArea(area);
            GUILayout.Label(Loc.Pick("Texte (le nom de la pièce, 40 caractères au plus)", "Text (the room's name, 40 characters at most)", "Texto (el nombre de la sala, 40 caracteres como máximo)"), DeviceUi.Muted);
            _field = GUILayout.TextField(_field ?? "", Signs.MaxLength, DeviceUi.Field, GUILayout.Height(34f), GUILayout.ExpandWidth(true));
            GUILayout.Space(8f);
            GUILayout.Label(Loc.Pick("Couleur", "Color", "Color"), DeviceUi.Muted);
            GUILayout.BeginHorizontal();
            string[] names = Signs.ColorNames;
            for (int i = 0; i < Signs.Colors.Length; i++)
            {
                if (GUILayout.Button(names[i], i == _color ? DeviceUi.Selected : DeviceUi.Button, GUILayout.Height(30f)))
                    _color = i;
            }
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Loc.Pick("Appliquer", "Apply", "Aplicar"), DeviceUi.Button, GUILayout.Height(36f)))
            {
                Signs.Request(_sign, _color, _field);
                ModMenu.Close();
            }
            if (GUILayout.Button(Loc.Pick("Effacer", "Clear", "Borrar"), DeviceUi.Button, GUILayout.Height(36f)))
            {
                Signs.Request(_sign, _color, "");
                ModMenu.Close();
            }
            if (GUILayout.Button(Loc.Pick("Fermer (Échap)", "Close (Esc)", "Cerrar (Esc)"), DeviceUi.Button, GUILayout.Height(36f)))
                ModMenu.Close();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            GUI.matrix = previous;
        }
    }
}

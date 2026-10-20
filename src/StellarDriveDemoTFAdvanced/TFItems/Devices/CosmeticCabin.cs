using System.Collections.Generic;
using System.Linq;
using Ships.Interface.Settings;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using UnityEngine;
using Loc = StellarDriveDemoTFAdvanced.Common.Loc;
using MainCamera = StellarDriveDemoTFAdvanced.Common.MainCamera;

namespace StellarDriveDemoTFAdvanced.TFItems.Devices
{
    /// <summary>
    /// The cosmetic cabin: a round booth with a mirror wall and a mannequin wearing your outfit.
    /// Its window changes the colors of your suit, helmet, backpack and visor, and gives you a
    /// hat, sunglasses and a jacket. Every other player sees your outfit on you (see <see cref="Outfits"/>).
    /// </summary>
    internal static class CosmeticCabin
    {
        public const ushort CabinId = 7348;

        private static readonly Vector3 BoundsCenter = new Vector3(0f, 1.15f, 0f);
        private static readonly Vector3 BoundsSize = new Vector3(1.3f, 2.3f, 1.3f);

        public static void Register()
        {
            Lights.LampParts.RegisterDonorPart(CabinId, TFTab.DevicesRow, Configure, TFTab.ObjectsName);
            TFMod.Log.Msg("registered the cosmetic cabin");
        }

        private static void Configure(PartSettings settings, GameObject prefab)
        {
            Transform visuals = DeviceModels.Build(settings, prefab, Loc.Pick("Cabine cosmétique", "Cosmetic cabin", "Cabina cosmética"),
                Loc.Pick("Une cabine d'essayage : regarde-la et appuie sur la touche d'utilisation (T) pour changer ton apparence (couleurs de la combinaison, du casque, du sac et de la visière, chapeau, lunettes de soleil, veste). Le mannequin dans la cabine porte ta tenue. Les autres joueurs te voient avec.",
                    "A fitting booth: look at it and press the use key (T) to change your look (colors of the suit, helmet, backpack and visor, hat, sunglasses, jacket). The mannequin in the booth wears your outfit. The other players see you wearing it.",
                    "Una cabina de prueba: mírala y pulsa la tecla de uso (T) para cambiar tu aspecto (colores del traje, del casco, de la mochila y del visor, sombrero, gafas de sol, chaqueta). El maniquí de la cabina lleva tu ropa. Los demás jugadores te ven con ella."),
                60f, new[] { (101u, 8), (102u, 10), (112u, 4), (110u, 6) }, BoundsCenter, BoundsSize, Shapes(), Materials());
            UsablePart.AddBox(visuals, BoundsCenter, BoundsSize);
            DeviceModels.FlattenColliders(prefab, new Vector3(1.3f, 0.08f, 1.3f));
            visuals.gameObject.AddComponent<CabinVisuals>();
        }

        private static Dictionary<string, DeviceMaterial> Materials() => new Dictionary<string, DeviceMaterial>
        {
            ["TF_Cabin_Shell"] = new DeviceMaterial(new Color(0.9f, 0.91f, 0.93f), 0.2f, 0.7f),
            ["TF_NoPaint_CabinTrim"] = new DeviceMaterial(new Color(0.12f, 0.13f, 0.15f), 0.5f, 0.6f),
            ["TF_NoPaint_CabinMirror"] = new DeviceMaterial(new Color(0.75f, 0.82f, 0.9f), 1f, 0.98f),
            ["TF_NoPaint_CabinFloor"] = new DeviceMaterial(new Color(0.25f, 0.27f, 0.3f), 0.3f, 0.4f),
            ["TF_NoPaint_CabinGlow"] = new DeviceMaterial(new Color(1f, 0.85f, 0.6f), 0f, 0.9f, new Color(1f, 0.8f, 0.55f) * 2.2f),
            ["TF_NoPaint_CabinAccent"] = new DeviceMaterial(new Color(0.3f, 0.85f, 1f), 0f, 0.9f, new Color(0.25f, 0.8f, 1f) * 2.2f)
        };

        // A round booth open at the front (-z): a disc floor, a curved wall around the back with a
        // mirror inside, a roof with a ring of light, pillars at the opening
        private static Dictionary<string, MeshBuilder> Shapes()
        {
            var shell = new MeshBuilder();
            var trim = new MeshBuilder();
            var mirror = new MeshBuilder();
            var floor = new MeshBuilder();
            var glow = new MeshBuilder();
            var accent = new MeshBuilder();
            const float radius = 0.62f, height = 2.25f;
            floor.Cylinder(Vector3.zero, Vector3.up * 0.06f, radius, radius, 32);
            accent.Cylinder(Vector3.up * 0.061f, Vector3.up * 0.064f, 0.5f, 0.5f, 32, false, false);
            trim.Cylinder(Vector3.up * 0.064f, Vector3.up * 0.068f, 0.46f, 0.46f, 32, true, false);
            // The wall: panels around the back 240°, a mirror strip on the inside of the middle ones
            const int panels = 12;
            for (int i = 0; i < panels; i++)
            {
                float a0 = Mathf.Lerp(60f, 300f, i / (float)panels) * Mathf.Deg2Rad, a1 = Mathf.Lerp(60f, 300f, (i + 1) / (float)panels) * Mathf.Deg2Rad;
                // (angles from -z: 180° is the back)
                Vector3 p0 = new Vector3(Mathf.Sin(a0), 0f, -Mathf.Cos(a0)) * radius, p1 = new Vector3(Mathf.Sin(a1), 0f, -Mathf.Cos(a1)) * radius;
                Vector3 mid = (p0 + p1) * 0.5f, along = (p1 - p0).normalized, inward = -mid.normalized;
                float width = (p1 - p0).magnitude + 0.01f;
                shell.Box(mid + Vector3.up * height * 0.5f, new Vector3(width, height - 0.06f, 0.04f), along, Vector3.up, -inward);
                if (i >= 3 && i <= 8)
                    mirror.Box(mid + inward * 0.022f + Vector3.up * 1.15f, new Vector3(width - 0.01f, 1.7f, 0.006f), along, Vector3.up, -inward);
                if (i % 2 == 0)
                    trim.Box(p0 + Vector3.up * height * 0.5f, new Vector3(0.03f, height, 0.05f), along, Vector3.up, -inward);
            }
            // Pillars at the opening, a light down each
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 edge = new Vector3(Mathf.Sin(60f * Mathf.Deg2Rad) * s, 0f, -Mathf.Cos(60f * Mathf.Deg2Rad)) * radius;
                trim.Cylinder(edge, edge + Vector3.up * height, 0.045f, 0.045f, 12);
                glow.Box(edge + new Vector3(-s * 0.03f, height * 0.5f, -0.03f), new Vector3(0.012f, height * 0.8f, 0.012f));
            }
            // Roof, a ring of light under it and a sign plate over the opening
            shell.Cylinder(Vector3.up * height, Vector3.up * (height + 0.08f), radius + 0.04f, radius + 0.02f, 32);
            glow.Cylinder(Vector3.up * (height - 0.01f), Vector3.up * (height - 0.005f), 0.45f, 0.45f, 32, false, false);
            trim.Box(new Vector3(0f, height - 0.12f, -radius * 0.55f), new Vector3(0.5f, 0.14f, 0.03f));
            accent.Box(new Vector3(0f, height - 0.12f, -radius * 0.55f - 0.017f), new Vector3(0.42f, 0.03f, 0.004f));
            return new Dictionary<string, MeshBuilder>
            {
                ["TF_Cabin_Shell"] = shell,
                ["TF_NoPaint_CabinTrim"] = trim,
                ["TF_NoPaint_CabinMirror"] = mirror,
                ["TF_NoPaint_CabinFloor"] = floor,
                ["TF_NoPaint_CabinGlow"] = glow,
                ["TF_NoPaint_CabinAccent"] = accent
            };
        }

        // Arms down along the body, elbows a little bent (from the rig's T pose)
        private static void Relax(Transform avatar)
        {
            Transform Bone(string name) => avatar.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "mixamorig:" + name);
            Vector3 down = -avatar.up;
            foreach (string side in new[] { "Left", "Right" })
            {
                Transform arm = Bone(side + "Arm"), fore = Bone(side + "ForeArm"), hand = Bone(side + "Hand");
                if (arm == null || fore == null || hand == null)
                    continue;
                Vector3 outward = (fore.position - arm.position);
                outward -= avatar.up * Vector3.Dot(outward, avatar.up);
                Vector3 target = (down + outward.normalized * 0.18f).normalized;
                arm.rotation = Quaternion.FromToRotation(fore.position - arm.position, target) * arm.rotation;
                Vector3 bent = (down + avatar.forward * 0.25f + outward.normalized * 0.05f).normalized;
                fore.rotation = Quaternion.FromToRotation(hand.position - fore.position, bent) * fore.rotation;
            }
        }

        /// <summary>A copy of the game's other-player avatar, its scripts removed (a still mannequin).</summary>
        public static GameObject Mannequin(Transform parent)
        {
            GameObject prefab = Resources.FindObjectsOfTypeAll<Players.Visuals.OtherPlayerAnimator>()
                .Select(a => a.transform.root.gameObject).FirstOrDefault(g => g != null && !g.scene.IsValid());
            if (prefab == null)
                return null;
            var holder = new GameObject("TFA_MannequinHolder") { layer = parent.gameObject.layer };
            holder.SetActive(false);
            holder.transform.SetParent(parent, false);
            GameObject copy = Object.Instantiate(prefab, holder.transform);
            copy.name = "TFA_Mannequin";
            // Keep only what draws: the name tag goes, and the game's player scripts (they need a network player)
            foreach (Canvas canvas in copy.GetComponentsInChildren<Canvas>(true))
                Object.DestroyImmediate(canvas.gameObject);
            foreach (MonoBehaviour script in copy.GetComponentsInChildren<MonoBehaviour>(true))
                Object.DestroyImmediate(script);
            // Without its scripts the animator would hold the T pose: a still, relaxed pose instead
            foreach (Animator animator in copy.GetComponentsInChildren<Animator>(true))
                Object.DestroyImmediate(animator);
            foreach (ParticleSystem particles in copy.GetComponentsInChildren<ParticleSystem>(true))
                Object.DestroyImmediate(particles.gameObject);
            foreach (Transform t in copy.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = parent.gameObject.layer;
            // The avatar's center is 0.85 m over its feet; it faces +z, the booth opens toward -z
            copy.transform.localPosition = new Vector3(0f, 0.92f, 0.05f);
            copy.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            holder.SetActive(true);
            Relax(copy.transform);
            return copy;
        }
    }

    /// <summary>The cabin: opens the outfit window; its mannequin wears this player's outfit (made when near).</summary>
    internal sealed class CabinVisuals : UsablePart
    {
        private GameObject _mannequin;
        private int _version = -1;
        private float _next;

        public override string Label => Loc.Pick("Cabine cosmétique", "Cosmetic cabin", "Cabina cosmética");

        public override void Use() => CabinMenu.Show();

        private void LateUpdate()
        {
            if (!HasContext || Time.unscaledTime < _next)
                return;
            _next = Time.unscaledTime + 0.25f;
            Camera camera = MainCamera.Get();
            bool near = camera == null || (camera.transform.position - transform.position).sqrMagnitude < 40f * 40f;
            if (!near)
                return;
            if (_mannequin == null)
            {
                _mannequin = CosmeticCabin.Mannequin(transform);
                _version = -1;
            }
            if (_mannequin != null && _version != OutfitDresser.Version)
            {
                _version = OutfitDresser.Version;
                OutfitDresser.Wear(_mannequin, Outfits.Mine);
            }
        }
    }

    /// <summary>
    /// The outfit window, on the left so the cabin's mannequin stays in view: every accessory is a
    /// tile with its picture; clicking a tile (or one of the suit's parts) opens a color wheel for it.
    /// </summary>
    internal static class CabinMenu
    {
        private enum Target { None, Suit, Helmet, Pack, Visor, Hat, Lens, Jacket, Back }

        private static bool _open;
        private static Vector2 _scroll;
        private static Target _target;
        private static Rect _windowRect;

        public static void Show()
        {
            _open = true;
            _target = Target.None;
            ModMenu.Open(() => _open = false);
        }

        public static void Draw()
        {
            if (!_open || !ModMenu.IsOpen)
                return;
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.7f, 2.5f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float screenWidth = Screen.width / scale, screenHeight = Screen.height / scale;
            Rect area = DeviceUi.WindowAt(screenWidth, screenHeight, 40f, 640f, 860f, Loc.Pick("CABINE COSMÉTIQUE", "COSMETIC CABIN", "CABINA COSMÉTICA"));
            _windowRect = new Rect(area.x - 22f, area.y - 54f, area.width + 44f, area.height + 72f);
            GUILayout.BeginArea(area);
            Outfit outfit = Outfits.Mine;
            bool changed = false;
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));

            GUILayout.Label(Loc.Pick("COULEURS (clique pour changer)", "COLORS (click to change)", "COLORES (clic para cambiar)"), DeviceUi.Muted);
            GUILayout.BeginHorizontal();
            ColorTile(Loc.Pick("Combinaison", "Suit", "Traje"), outfit.Suit, Target.Suit);
            ColorTile(Loc.Pick("Casque", "Helmet", "Casco"), outfit.Helmet, Target.Helmet);
            ColorTile(Loc.Pick("Sac", "Pack", "Mochila"), outfit.Pack, Target.Pack);
            ColorTile(Loc.Pick("Visière", "Visor", "Visor"), outfit.Visor, Target.Visor);
            GUILayout.EndHorizontal();

            changed |= Tiles(Loc.Pick("CHAPEAU", "HAT", "SOMBRERO"), ref outfit.Hat, Outfit.Hats, "hat", outfit.HatColor, Target.Hat, new[]
            {
                Loc.Pick("Aucun", "None", "Ninguno"), Loc.Pick("Casquette", "Cap", "Gorra"), "Cowboy", Loc.Pick("Haut-de-forme", "Top hat", "Chistera"), Loc.Pick("Bonnet", "Beanie", "Gorro"),
                Loc.Pick("Couronne", "Crown", "Corona"), Loc.Pick("Béret", "Beret", "Boina"), Loc.Pick("Casque viking", "Viking helmet", "Casco vikingo"), Loc.Pick("Tricorne pirate", "Pirate tricorne", "Tricornio pirata"),
                Loc.Pick("Casque audio", "Headphones", "Auriculares"), Loc.Pick("Auréole", "Halo", "Aureola")
            });
            changed |= Tiles(Loc.Pick("LUNETTES", "GLASSES", "GAFAS"), ref outfit.Glasses, Outfit.GlassesKinds, "glasses", outfit.Lens, Target.Lens, new[]
            {
                Loc.Pick("Aucunes", "None", "Ninguna"), Loc.Pick("Soleil", "Sunglasses", "De sol"), Loc.Pick("Aviateur", "Aviator", "Aviador"), Loc.Pick("Bandeau", "Visor band", "Banda"),
                Loc.Pick("Rondes", "Round", "Redondas"), Loc.Pick("Masque de ski", "Ski goggles", "Gafas de esquí")
            });
            changed |= Tiles(Loc.Pick("VESTE", "JACKET", "CHAQUETA"), ref outfit.Jacket, Outfit.Jackets, "jacket", outfit.JacketColor, Target.Jacket, new[]
            {
                Loc.Pick("Aucune", "None", "Ninguna"), Loc.Pick("Gilet", "Vest", "Chaleco"), Loc.Pick("Blouson", "Jacket", "Cazadora"), Loc.Pick("Manteau long", "Long coat", "Abrigo largo")
            });
            changed |= Tiles(Loc.Pick("DOS", "BACK", "ESPALDA"), ref outfit.Back, Outfit.Backs, "back", outfit.BackColor, Target.Back, new[]
            {
                Loc.Pick("Rien", "Nothing", "Nada"), Loc.Pick("Cape", "Cape", "Capa"), "Jetpack", Loc.Pick("Ailes", "Wings", "Alas"), Loc.Pick("Antenne", "Antenna", "Antena")
            });
            GUILayout.EndScrollView();

            GUILayout.Label(Loc.Pick("Le mannequin de la cabine porte ta tenue ; les autres joueurs te voient avec.", "The booth's mannequin wears your outfit; the other players see you in it.", "El maniquí de la cabina lleva tu ropa; los demás jugadores te ven con ella."), DeviceUi.Muted);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Loc.Pick("Tenue d'origine", "Original look", "Aspecto original"), DeviceUi.Button, GUILayout.Height(36f)))
            {
                var fresh = new Outfit();
                outfit.Suit = fresh.Suit; outfit.Helmet = fresh.Helmet; outfit.Pack = fresh.Pack; outfit.Visor = fresh.Visor;
                outfit.Hat = 0; outfit.Glasses = 0; outfit.Jacket = 0; outfit.Back = 0;
                _target = Target.None;
                changed = true;
            }
            if (GUILayout.Button(Loc.Pick("Fermer (Échap)", "Close (Esc)", "Cerrar (Esc)"), DeviceUi.Button, GUILayout.Height(36f)))
                ModMenu.Close();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            if (_target != Target.None)
                changed |= ColorPanel(outfit);
            GUI.matrix = previous;
            if (changed)
                Outfits.Changed();
        }

        // ---- Tiles

        private const float Tile = 92f;

        private static void ColorTile(string label, Color? color, Target target)
        {
            Rect rect = GUILayoutUtility.GetRect(140f, 64f, GUILayout.Width(140f), GUILayout.Height(64f));
            bool selected = _target == target;
            DeviceUi.Fill(rect, selected ? Color.white : new Color(1f, 1f, 1f, 0.15f));
            var inner = new Rect(rect.x + 3f, rect.y + 3f, rect.width - 6f, rect.height - 6f);
            DeviceUi.Fill(inner, new Color(0.04f, 0.08f, 0.13f, 1f));
            var swatch = new Rect(inner.x + 6f, inner.y + 14f, 28f, 28f);
            if (color.HasValue)
                DeviceUi.Fill(swatch, color.Value);
            else
                GUI.Label(swatch, Loc.Pick("Jeu", "Game", "Juego"), DeviceUi.Muted);
            GUI.Label(new Rect(swatch.xMax + 6f, inner.y, inner.xMax - swatch.xMax - 8f, inner.height), label, DeviceUi.Muted);
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                _target = selected ? Target.None : target;
            GUILayout.Space(6f);
        }

        // A row of tiles with pictures; choosing one also opens its color (when it has one)
        private static bool Tiles(string label, ref int value, int count, string category, Color color, Target target, string[] names)
        {
            int before = value;
            GUILayout.Space(8f);
            GUILayout.Label(label, DeviceUi.Muted);
            int perRow = 6;
            for (int row = 0; row * perRow < count; row++)
            {
                GUILayout.BeginHorizontal();
                for (int i = row * perRow; i < Mathf.Min(count, (row + 1) * perRow); i++)
                {
                    Rect rect = GUILayoutUtility.GetRect(Tile, Tile, GUILayout.Width(Tile), GUILayout.Height(Tile));
                    bool selected = i == value;
                    DeviceUi.Fill(rect, selected ? Color.white : new Color(1f, 1f, 1f, 0.15f));
                    var inner = new Rect(rect.x + 3f, rect.y + 3f, rect.width - 6f, rect.height - 6f);
                    DeviceUi.Fill(inner, new Color(0.04f, 0.08f, 0.13f, 1f));
                    Texture picture = i == 0 ? null : CosmeticThumbs.Get(category, i, color);
                    if (picture != null)
                        GUI.DrawTexture(new Rect(inner.x + 4f, inner.y + 2f, inner.width - 8f, inner.height - 8f), picture, ScaleMode.ScaleToFit, true);
                    else
                        GUI.Label(new Rect(inner.x, inner.y, inner.width, inner.height - 16f), i == 0 ? "—" : names[i], DeviceUi.Text);
                    GUI.Label(new Rect(inner.x + 2f, inner.yMax - 20f, inner.width - 4f, 20f), names[i], DeviceUi.Muted);
                    if (GUI.Button(rect, new GUIContent("", names[i]), GUIStyle.none))
                    {
                        value = i;
                        bool colored = i > 0 && !(category == "hat" && (i == 5 || i == 7 || i == 10));
                        _target = colored ? target : Target.None;
                    }
                    GUILayout.Space(4f);
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(4f);
            }
            return value != before;
        }

        // ---- The color wheel, beside the window

        private static Texture2D _wheel;
        private const int WheelSize = 200;

        private static bool ColorPanel(Outfit outfit)
        {
            Color? current = Get(outfit, _target);
            bool nullable = _target == Target.Suit || _target == Target.Helmet || _target == Target.Pack || _target == Target.Visor;
            var panel = new Rect(_windowRect.xMax + 16f, _windowRect.y + 80f, 280f, nullable ? 420f : 380f);
            DeviceUi.Panel(panel);
            GUI.Label(new Rect(panel.x + 20f, panel.y + 12f, panel.width - 40f, 26f), TargetName(_target).ToUpperInvariant(), DeviceUi.Title);
            var wheel = new Rect(panel.x + (panel.width - WheelSize) / 2f, panel.y + 48f, WheelSize, WheelSize);
            GUI.DrawTexture(wheel, Wheel());
            Color shown = current ?? Color.white;
            Color.RGBToHSV(shown, out float h, out float s, out float v);
            // Where the color is on the wheel
            float angle = h * Mathf.PI * 2f;
            var mark = new Vector2(wheel.center.x + Mathf.Cos(angle) * s * WheelSize * 0.5f, wheel.center.y - Mathf.Sin(angle) * s * WheelSize * 0.5f);
            DeviceUi.Fill(new Rect(mark.x - 6f, mark.y - 6f, 12f, 12f), Color.black);
            DeviceUi.Fill(new Rect(mark.x - 4f, mark.y - 4f, 8f, 8f), Color.white);

            Color? picked = null;
            Event e = Event.current;
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0)
            {
                Vector2 d = (e.mousePosition - wheel.center) / (WheelSize * 0.5f);
                if (d.magnitude <= 1.05f)
                {
                    float hue = Mathf.Repeat(Mathf.Atan2(-d.y, d.x) / (Mathf.PI * 2f), 1f);
                    picked = Color.HSVToRGB(hue, Mathf.Clamp01(d.magnitude), Mathf.Max(0.05f, v));
                    e.Use();
                }
            }

            float y = wheel.yMax + 14f;
            GUI.Label(new Rect(panel.x + 20f, y, 90f, 22f), Loc.Pick("Luminosité", "Brightness", "Brillo"), DeviceUi.Muted);
            float brightness = GUI.HorizontalSlider(new Rect(panel.x + 115f, y + 6f, panel.width - 140f, 16f), v, 0.05f, 1f);
            if (!Mathf.Approximately(brightness, v) && picked == null)
                picked = Color.HSVToRGB(h, s, brightness);
            y += 32f;
            DeviceUi.Fill(new Rect(panel.x + 20f, y, 60f, 30f), shown);
            GUI.Label(new Rect(panel.x + 90f, y, 100f, 30f), "#" + ColorUtility.ToHtmlStringRGB(shown), DeviceUi.Text);
            if (GUI.Button(new Rect(panel.xMax - 80f, y, 60f, 30f), "OK", DeviceUi.Button))
                _target = Target.None;
            bool changed = false;
            if (nullable)
            {
                y += 40f;
                if (GUI.Button(new Rect(panel.x + 20f, y, panel.width - 40f, 30f), Loc.Pick("Couleur du jeu", "Game's color", "Color del juego"), current.HasValue ? DeviceUi.Button : DeviceUi.Selected))
                {
                    Set(outfit, _target, null);
                    changed = true;
                }
            }
            if (picked.HasValue)
            {
                Set(outfit, _target, picked.Value);
                changed = true;
            }
            return changed;
        }

        private static Texture2D Wheel()
        {
            if (_wheel != null)
                return _wheel;
            const int size = 256;
            _wheel = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "TF_ColorWheel" };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    // (texture rows go up, like the wheel's angle)
                    Color c = Color.HSVToRGB(Mathf.Repeat(Mathf.Atan2(dy, dx) / (Mathf.PI * 2f), 1f), Mathf.Clamp01(r), 1f);
                    c.a = Mathf.Clamp01((1f - r) * size * 0.5f);
                    pixels[y * size + x] = c;
                }
            }
            _wheel.SetPixels(pixels);
            _wheel.Apply();
            return _wheel;
        }

        private static string TargetName(Target target)
        {
            switch (target)
            {
                case Target.Suit: return Loc.Pick("Combinaison", "Suit", "Traje");
                case Target.Helmet: return Loc.Pick("Casque", "Helmet", "Casco");
                case Target.Pack: return Loc.Pick("Sac à dos", "Backpack", "Mochila");
                case Target.Visor: return Loc.Pick("Visière", "Visor", "Visor");
                case Target.Hat: return Loc.Pick("Chapeau", "Hat", "Sombrero");
                case Target.Lens: return Loc.Pick("Verres", "Lenses", "Cristales");
                case Target.Jacket: return Loc.Pick("Veste", "Jacket", "Chaqueta");
                default: return Loc.Pick("Dos", "Back", "Espalda");
            }
        }

        private static Color? Get(Outfit o, Target t)
        {
            switch (t)
            {
                case Target.Suit: return o.Suit;
                case Target.Helmet: return o.Helmet;
                case Target.Pack: return o.Pack;
                case Target.Visor: return o.Visor;
                case Target.Hat: return o.HatColor;
                case Target.Lens: return o.Lens;
                case Target.Jacket: return o.JacketColor;
                case Target.Back: return o.BackColor;
                default: return null;
            }
        }

        private static void Set(Outfit o, Target t, Color? c)
        {
            switch (t)
            {
                case Target.Suit: o.Suit = c; break;
                case Target.Helmet: o.Helmet = c; break;
                case Target.Pack: o.Pack = c; break;
                case Target.Visor: o.Visor = c; break;
                case Target.Hat: o.HatColor = c ?? o.HatColor; break;
                case Target.Lens: o.Lens = c ?? o.Lens; break;
                case Target.Jacket: o.JacketColor = c ?? o.JacketColor; break;
                case Target.Back: o.BackColor = c ?? o.BackColor; break;
            }
        }
    }

    /// <summary>
    /// Pictures of the accessories for the cabin's tiles, rendered by the game's own thumbnail
    /// renderer (the one of the inventory icons) and kept per kind and color.
    /// </summary>
    internal static class CosmeticThumbs
    {
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();
        private static Rendering.Services.ObjectThumbnailRenderer _renderer;
        private static int _renderedThisFrame, _frame;

        public static Texture Get(string category, int kind, Color color)
        {
            string key = category + kind + "_" + ColorUtility.ToHtmlStringRGB(color);
            // (null: it could not be made, not tried again)
            if (Cache.TryGetValue(key, out Texture2D cached))
                return cached;
            // A few per frame: dragging on the color wheel re-renders every tile of a row
            if (_frame != Time.frameCount)
            {
                _frame = Time.frameCount;
                _renderedThisFrame = 0;
            }
            if (_renderedThisFrame >= 3)
                return null;
            _renderedThisFrame++;
            try
            {
                if (_renderer == null)
                    _renderer = Core.Services.ServiceLocator.GetService<Rendering.Services.ObjectThumbnailRenderer>();
                if (_renderer == null)
                    return null;
                List<OutfitModels.Piece> pieces = category == "hat" ? OutfitModels.HatPieces(kind, color)
                    : category == "glasses" ? OutfitModels.GlassesPieces(kind, color)
                    : category == "back" ? OutfitModels.BackPieces(kind, color)
                    : OutfitModels.JacketIconPieces(kind, color);
                var root = new GameObject("TFA_OutfitThumb");
                root.transform.position = new Vector3(0f, -30000f, 0f);
                foreach (OutfitModels.Piece piece in pieces)
                {
                    if (!piece.Mesh.HasGeometry)
                        continue;
                    var child = new GameObject("Piece");
                    child.transform.SetParent(root.transform, false);
                    child.AddComponent<MeshFilter>().sharedMesh = piece.Mesh.Build("TFA_OutfitThumb");
                    child.AddComponent<MeshRenderer>().sharedMaterial = piece.Material();
                }
                var texture = new Texture2D(128, 128, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "TFA_" + key };
                // Seen from the front, a little from above and to the side (the camera looks along +z)
                Quaternion turn = category == "back" ? Quaternion.Euler(10f, 25f, 0f) : Quaternion.Euler(18f, 205f, 0f);
                _renderer.Render(root, turn, texture);
                if (Cache.Count > 300)
                {
                    foreach (Texture2D old in Cache.Values)
                        Object.Destroy(old);
                    Cache.Clear();
                }
                Cache[key] = texture;
                return texture;
            }
            catch (System.Exception e)
            {
                TFMod.Log.Warning("cabin: thumbnail " + key + ": " + e.Message);
                Cache[key] = null;
                return null;
            }
        }
    }
}

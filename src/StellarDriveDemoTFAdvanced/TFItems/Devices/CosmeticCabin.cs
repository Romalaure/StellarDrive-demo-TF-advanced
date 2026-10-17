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

    /// <summary>The outfit window, on the left so the cabin's mannequin stays in view.</summary>
    internal static class CabinMenu
    {
        private static bool _open;
        private static Vector2 _scroll;

        private static readonly Color[] Palette =
        {
            new Color(0.95f, 0.95f, 0.95f), new Color(0.55f, 0.57f, 0.6f), new Color(0.12f, 0.12f, 0.14f), new Color(0.85f, 0.15f, 0.12f),
            new Color(0.98f, 0.5f, 0.1f), new Color(0.98f, 0.82f, 0.15f), new Color(0.3f, 0.75f, 0.25f), new Color(0.1f, 0.45f, 0.25f),
            new Color(0.25f, 0.8f, 0.95f), new Color(0.15f, 0.35f, 0.85f), new Color(0.1f, 0.15f, 0.4f), new Color(0.55f, 0.25f, 0.85f),
            new Color(0.95f, 0.45f, 0.7f), new Color(0.5f, 0.32f, 0.18f), new Color(0.82f, 0.68f, 0.45f), new Color(0.9f, 0.75f, 0.3f)
        };

        public static void Show()
        {
            _open = true;
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
            Rect area = DeviceUi.WindowAt(screenWidth, screenHeight, 40f, 660f, 820f, Loc.Pick("CABINE COSMÉTIQUE", "COSMETIC CABIN", "CABINA COSMÉTICA"));
            GUILayout.BeginArea(area);
            Outfit outfit = Outfits.Mine;
            bool changed = false;
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            changed |= ColorRow(Loc.Pick("Combinaison", "Suit", "Traje"), ref outfit.Suit);
            changed |= ColorRow(Loc.Pick("Casque", "Helmet", "Casco"), ref outfit.Helmet);
            changed |= ColorRow(Loc.Pick("Sac à dos", "Backpack", "Mochila"), ref outfit.Pack);
            changed |= ColorRow(Loc.Pick("Visière", "Visor", "Visor"), ref outfit.Visor);
            GUILayout.Space(6f);
            changed |= Choice(Loc.Pick("Chapeau", "Hat", "Sombrero"), ref outfit.Hat, new[]
            {
                Loc.Pick("Aucun", "None", "Ninguno"), Loc.Pick("Casquette", "Cap", "Gorra"), "Cowboy", Loc.Pick("Haut-de-forme", "Top hat", "Chistera"), Loc.Pick("Bonnet", "Beanie", "Gorro"), Loc.Pick("Couronne", "Crown", "Corona")
            });
            if (outfit.Hat > 0 && outfit.Hat < 5)
                changed |= ColorRow(Loc.Pick("Couleur du chapeau", "Hat color", "Color del sombrero"), ref outfit.HatColor);
            changed |= Choice(Loc.Pick("Lunettes", "Glasses", "Gafas"), ref outfit.Glasses, new[]
            {
                Loc.Pick("Aucunes", "None", "Ninguna"), Loc.Pick("Soleil", "Sunglasses", "De sol"), Loc.Pick("Aviateur", "Aviator", "Aviador"), Loc.Pick("Bandeau", "Visor band", "Banda")
            });
            if (outfit.Glasses > 0)
                changed |= ColorRow(Loc.Pick("Couleur des verres", "Lens color", "Color de los cristales"), ref outfit.Lens);
            changed |= Choice(Loc.Pick("Veste", "Jacket", "Chaqueta"), ref outfit.Jacket, new[]
            {
                Loc.Pick("Aucune", "None", "Ninguna"), Loc.Pick("Gilet", "Vest", "Chaleco"), Loc.Pick("Blouson", "Jacket", "Cazadora"), Loc.Pick("Manteau long", "Long coat", "Abrigo largo")
            });
            if (outfit.Jacket > 0)
                changed |= ColorRow(Loc.Pick("Couleur de la veste", "Jacket color", "Color de la chaqueta"), ref outfit.JacketColor);
            GUILayout.EndScrollView();
            GUILayout.Label(Loc.Pick("Le mannequin de la cabine porte ta tenue ; les autres joueurs te voient avec.", "The booth's mannequin wears your outfit; the other players see you in it.", "El maniquí de la cabina lleva tu ropa; los demás jugadores te ven con ella."), DeviceUi.Muted);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Loc.Pick("Tenue d'origine", "Original look", "Aspecto original"), DeviceUi.Button, GUILayout.Height(36f)))
            {
                var fresh = new Outfit();
                outfit.Suit = fresh.Suit; outfit.Helmet = fresh.Helmet; outfit.Pack = fresh.Pack; outfit.Visor = fresh.Visor;
                outfit.Hat = 0; outfit.Glasses = 0; outfit.Jacket = 0;
                changed = true;
            }
            if (GUILayout.Button(Loc.Pick("Fermer (Échap)", "Close (Esc)", "Cerrar (Esc)"), DeviceUi.Button, GUILayout.Height(36f)))
                ModMenu.Close();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            GUI.matrix = previous;
            if (changed)
                Outfits.Changed();
        }

        private static bool Choice(string label, ref int value, string[] names)
        {
            int before = value;
            GUILayout.Label(label.ToUpperInvariant(), DeviceUi.Muted);
            GUILayout.BeginHorizontal();
            for (int i = 0; i < names.Length; i++)
            {
                if (GUILayout.Button(names[i], i == value ? DeviceUi.Selected : DeviceUi.Button, GUILayout.Height(30f)))
                    value = i;
            }
            GUILayout.EndHorizontal();
            return value != before;
        }

        // A row of swatches; "game" (null) for the nullable colors
        private static bool ColorRow(string label, ref Color? value)
        {
            Color? before = value;
            GUILayout.Label(label.ToUpperInvariant(), DeviceUi.Muted);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Loc.Pick("Jeu", "Game", "Juego"), !value.HasValue ? DeviceUi.Selected : DeviceUi.Button, GUILayout.Width(62f), GUILayout.Height(28f)))
                value = null;
            foreach (Color color in Palette)
            {
                if (Swatch(color, value.HasValue && Same(value.Value, color)))
                    value = color;
            }
            GUILayout.EndHorizontal();
            return before.HasValue != value.HasValue || before.HasValue && !Same(before.Value, value.Value);
        }

        private static bool ColorRow(string label, ref Color value)
        {
            Color before = value;
            GUILayout.Label(label.ToUpperInvariant(), DeviceUi.Muted);
            GUILayout.BeginHorizontal();
            foreach (Color color in Palette)
            {
                if (Swatch(color, Same(value, color)))
                    value = color;
            }
            GUILayout.EndHorizontal();
            return !Same(before, value);
        }

        private static bool Swatch(Color color, bool selected)
        {
            Rect rect = GUILayoutUtility.GetRect(26f, 28f, GUILayout.Width(26f), GUILayout.Height(28f));
            if (selected)
                DeviceUi.Fill(rect, Color.white);
            DeviceUi.Fill(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f), color);
            return GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }

        private static bool Same(Color a, Color b) => ColorUtility.ToHtmlStringRGB(a) == ColorUtility.ToHtmlStringRGB(b);
    }
}

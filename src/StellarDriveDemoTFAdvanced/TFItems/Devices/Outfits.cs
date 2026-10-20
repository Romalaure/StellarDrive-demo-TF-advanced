using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FishNet.Connection;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.TFItems.Devices
{
    /// <summary>
    /// What a player wears, chosen in the cosmetic cabin: colors for the suit, helmet, backpack and
    /// visor (null: the game's), a hat, sunglasses and a jacket (with their colors). Travels as a
    /// short text: "suit|helmet|pack|visor|hat|hatColor|glasses|lens|jacket|jacketColor|back|backColor", colors
    /// as RRGGBB or "-".
    /// </summary>
    internal sealed class Outfit
    {
        public Color? Suit, Helmet, Pack, Visor;
        public int Hat, Glasses, Jacket, Back;
        public Color HatColor = new Color(0.15f, 0.15f, 0.18f), Lens = new Color(0.05f, 0.05f, 0.08f), JacketColor = new Color(0.45f, 0.3f, 0.18f), BackColor = new Color(0.6f, 0.08f, 0.1f);

        public const int Hats = 11, GlassesKinds = 6, Jackets = 4, Backs = 5;

        public Outfit Copy() => Parse(ToString());

        public override string ToString() => string.Join("|", Hex(Suit), Hex(Helmet), Hex(Pack), Hex(Visor),
            Hat.ToString(CultureInfo.InvariantCulture), Hex(HatColor), Glasses.ToString(CultureInfo.InvariantCulture), Hex(Lens), Jacket.ToString(CultureInfo.InvariantCulture), Hex(JacketColor),
            Back.ToString(CultureInfo.InvariantCulture), Hex(BackColor));

        public static Outfit Parse(string text)
        {
            var outfit = new Outfit();
            if (string.IsNullOrEmpty(text))
                return outfit;
            string[] f = text.Split('|');
            if (f.Length < 10)
                return outfit;
            outfit.Suit = Color(f[0]);
            outfit.Helmet = Color(f[1]);
            outfit.Pack = Color(f[2]);
            outfit.Visor = Color(f[3]);
            outfit.Hat = Mathf.Clamp(Int(f[4]), 0, Hats - 1);
            outfit.HatColor = Color(f[5]) ?? outfit.HatColor;
            outfit.Glasses = Mathf.Clamp(Int(f[6]), 0, GlassesKinds - 1);
            outfit.Lens = Color(f[7]) ?? outfit.Lens;
            outfit.Jacket = Mathf.Clamp(Int(f[8]), 0, Jackets - 1);
            outfit.JacketColor = Color(f[9]) ?? outfit.JacketColor;
            // (older versions sent ten fields)
            if (f.Length >= 12)
            {
                outfit.Back = Mathf.Clamp(Int(f[10]), 0, Backs - 1);
                outfit.BackColor = Color(f[11]) ?? outfit.BackColor;
            }
            return outfit;
        }

        /// <summary>Whether a text can be an outfit (the host only passes those on).</summary>
        public static bool IsValid(string text) => text != null && text.Length <= 160 && (text.Count(c => c == '|') == 9 || text.Count(c => c == '|') == 11)
            && text.All(c => c == '|' || c == '-' || char.IsDigit(c) || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f'));

        private static int Int(string s) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;

        private static string Hex(Color? color) => color.HasValue ? ColorUtility.ToHtmlStringRGB(color.Value) : "-";

        private static Color? Color(string s) => s != "-" && s.Length == 6 && ColorUtility.TryParseHtmlString("#" + s, out Color c) ? c : (Color?)null;
    }

    /// <summary>
    /// The players' outfits over the network: each player sends theirs to the host when they
    /// join and when they change it, the host passes it to everyone (and to newcomers), every game
    /// dresses the other players' avatars with it.
    /// </summary>
    internal static class Outfits
    {
        /// <summary>Player id → outfit, on every game (the host's own copy too).</summary>
        public static readonly Dictionary<uint, Outfit> Worn = new Dictionary<uint, Outfit>();
        private static readonly Dictionary<uint, string> ServerOutfits = new Dictionary<uint, string>();

        /// <summary>This player's outfit (saved in the settings).</summary>
        public static Outfit Mine
        {
            get
            {
                if (_mine == null)
                    _mine = Outfit.Parse(StellarDriveDemoTFAdvanced.Common.DevHarness.Active && !string.IsNullOrEmpty(StellarDriveDemoTFAdvanced.Common.Settings.DevOutfit.Value)
                        ? StellarDriveDemoTFAdvanced.Common.Settings.DevOutfit.Value : Settings.Outfit.Value);
                return _mine;
            }
        }

        private static Outfit _mine;
        private static float _sendAt = -1f;

        public static void Install()
        {
            // Development: the cabin's window photographed (with DevOutfit)
            StellarDriveDemoTFAdvanced.Common.DevHarness.Steps.Add((50f, "cabin window", () =>
            {
                if (string.IsNullOrEmpty(StellarDriveDemoTFAdvanced.Common.Settings.DevOutfit.Value))
                    return;
                MelonLoader.MelonCoroutines.Start(DevWindow());
            }));
            TFNet.OnServer(TFMessageKind.Outfit, OnServerOutfit);
            TFNet.OnClient(TFMessageKind.Outfit, (kind, player, part, text) =>
            {
                Worn[player] = Outfit.Parse(text);
                OutfitDresser.Version++;
            });
            TFNet.SyncRequested += connection =>
            {
                foreach (KeyValuePair<uint, string> outfit in ServerOutfits.ToList())
                    TFNet.SendTo(connection, TFMessageKind.Outfit, outfit.Key, 0, outfit.Value);
            };
            TFNet.Synced += () => _sendAt = Time.unscaledTime + 1f;
            TFNet.Disconnected += () =>
            {
                Worn.Clear();
                ServerOutfits.Clear();
            };
        }

        /// <summary>The cabin changed this player's outfit: saved, and sent shortly (not at every click).</summary>
        public static void Changed()
        {
            Settings.Outfit.Value = Mine.ToString();
            _sendAt = Time.unscaledTime + 0.4f;
            OutfitDresser.Version++;
        }

        public static void Update()
        {
            if (_sendAt >= 0f && Time.unscaledTime >= _sendAt)
            {
                _sendAt = -1f;
                MelonLoader.MelonPreferences.Save();
                TFNet.SendToServer(TFMessageKind.Outfit, 0, 0, Mine.ToString());
            }
            OutfitDresser.Update();
        }

        private static System.Collections.IEnumerator DevWindow()
        {
            CabinMenu.Show();
            yield return new WaitForSecondsRealtime(1.5f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(StellarDriveDemoTFAdvanced.Common.DevHarness.Folder, "cabin_menu.png"));
            yield return new WaitForSecondsRealtime(1f);
            ModMenu.Close();
            StellarDriveDemoTFAdvanced.Parts.Showcase.PhotographAgain();
            yield return new WaitForSecondsRealtime(3f);
            // Close-ups of the mannequin: front and three quarters
            Transform mannequin = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).FirstOrDefault(t => t.name == "TFA_Mannequin");
            if (mannequin == null)
                yield break;
            var camera = new GameObject("TFA_DevCabinCamera").AddComponent<Camera>();
            camera.enabled = false;
            Camera main = Camera.main;
            if (main != null)
                camera.CopyFrom(main);
            camera.enabled = false;
            camera.fieldOfView = 35f;
            camera.nearClipPlane = 0.05f;
            var target = RenderTexture.GetTemporary(1200, 1200, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            var sheet = new Texture2D(1200, 1200, TextureFormat.RGBA32, false);
            Vector3 chest = mannequin.position + mannequin.up * 0.35f;
            int shot = 0;
            foreach (Vector3 direction in new[] { mannequin.forward, (mannequin.forward + mannequin.right * 0.8f).normalized, (mannequin.forward - mannequin.right * 0.6f + mannequin.up * 0.3f).normalized })
            {
                camera.transform.position = chest + direction * 1.9f;
                camera.transform.rotation = Quaternion.LookRotation(chest - camera.transform.position, mannequin.up);
                camera.Render();
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                sheet.ReadPixels(new Rect(0, 0, 1200, 1200), 0, 0);
                sheet.Apply();
                RenderTexture.active = previous;
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(StellarDriveDemoTFAdvanced.Common.DevHarness.Folder, $"cabin_mannequin_{shot++}.png"), sheet.EncodeToPNG());
                yield return null;
            }
            camera.targetTexture = null;
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.Destroy(sheet);
            UnityEngine.Object.Destroy(camera.gameObject);
        }

        private static void OnServerOutfit(NetworkConnection sender, TFMessageKind kind, uint a, ushort b, string text)
        {
            if (!Outfit.IsValid(text))
                return;
            var players = GameServices.PlayersServer;
            uint? id = null;
            if (players != null)
            {
                foreach (var player in players.ActivePlayers)
                {
                    if (player != null && (sender == null ? player.IsHost : player.ConnectionId == sender.ClientId))
                    {
                        id = player.Id;
                        break;
                    }
                }
            }
            if (id == null)
                return;
            ServerOutfits[id.Value] = text;
            TFNet.SendToAll(TFMessageKind.Outfit, id.Value, 0, text);
        }
    }

    /// <summary>Dresses the other players' avatars (and the cabins' mannequins) with their outfits.</summary>
    internal static class OutfitDresser
    {
        /// <summary>Bumped when any outfit changes: avatars re-dress.</summary>
        public static int Version;
        private static float _next;
        private static readonly System.Reflection.FieldInfo VisualField = HarmonyLib.AccessTools.Field(typeof(Players.TrackedPlayerOtherClient), "_visualObject");

        public static void Update()
        {
            if (Time.unscaledTime < _next)
                return;
            _next = Time.unscaledTime + 0.5f;
            var players = GameServices.PlayersClient;
            if (players == null)
                return;
            DressSelf();
            foreach (Players.TrackedPlayerOtherClient other in players.OtherPlayers)
            {
                if (other == null || !other.IsVisualLoaded || !(VisualField?.GetValue(other) is GameObject visual) || visual == null)
                    continue;
                Outfits.Worn.TryGetValue(other.Id, out Outfit outfit);
                Wear(visual, outfit);
            }
        }

        /// <summary>Puts an outfit on an avatar (null: the game's look), once per change.</summary>
        public static void Wear(GameObject avatar, Outfit outfit, bool firstPerson = false)
        {
            var worn = avatar.GetComponent<OutfitWorn>() ?? avatar.AddComponent<OutfitWorn>();
            string text = outfit?.ToString() ?? "";
            if (worn.Text == text)
                return;
            worn.Text = text;
            OutfitModels.Dress(avatar.transform, outfit ?? new Outfit(), firstPerson);
        }

        private static GameObject _ownBody;

        // This player's own body (seen looking down, the arms): PlayerCamera/PlayerRig/PlayerBodyWrapper/player
        private static void DressSelf()
        {
            if (_ownBody == null)
            {
                var animator = UnityEngine.Object.FindFirstObjectByType<Players.Visuals.FirstPersonPlayerAnimator>(FindObjectsInactive.Include);
                _ownBody = animator != null ? animator.gameObject : null;
            }
            if (_ownBody != null)
                Wear(_ownBody, Outfits.Mine, firstPerson: true);
        }
    }

    /// <summary>The outfit an avatar wears now.</summary>
    internal sealed class OutfitWorn : MonoBehaviour
    {
        public string Text;
    }
}

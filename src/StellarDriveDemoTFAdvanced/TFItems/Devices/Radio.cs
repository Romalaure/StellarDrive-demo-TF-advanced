using System;
using System.Collections.Generic;
using System.Linq;
using Core.Services;
using FishNet.Connection;
using FMOD.Studio;
using RuntimeManager = FMODUnity.RuntimeManager;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.State;
using Ships.Interface.Services;
using Ships.Interface.Settings;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using UnityEngine;
using Loc = StellarDriveDemoTFAdvanced.Common.Loc;

namespace StellarDriveDemoTFAdvanced.TFItems.Devices
{
    /// <summary>
    /// The radio: look at it and press the use key to play one of the game's music tracks or the
    /// sound of a YouTube link. What a radio plays is decided by the host and heard by every player
    /// near it who has the mod (each one downloads the YouTube sound on their side). The sound
    /// fades with distance and goes through the game's own sound engine (FMOD).
    /// </summary>
    internal static class Radio
    {
        public const ushort RadioId = 7161;

        private static readonly Vector3 BoundsCenter = new Vector3(0f, 0.16f, 0f);
        private static readonly Vector3 BoundsSize = new Vector3(0.56f, 0.32f, 0.26f);

        /// <summary>What a radio plays: "" (off), "game|event:/Music/..." or "yt|videoId".</summary>
        private static readonly Dictionary<PartKey, string> ServerStates = new Dictionary<PartKey, string>();
        internal static readonly Dictionary<PartKey, string> ClientStates = new Dictionary<PartKey, string>();

        public static void Register()
        {
            Lights.LampParts.RegisterDonorPart(RadioId, TFTab.DevicesRow, Configure, TFTab.ObjectsName);
            Lights.LampParts.RegisterDonorPart(SpeakerId, TFTab.DevicesRow, ConfigureSpeaker, TFTab.ObjectsName);
            TFMod.Log.Msg("registered the radio and its speaker");
        }

        public const ushort SpeakerId = 7162;
        private static readonly Vector3 SpeakerCenter = new Vector3(0f, 0.27f, 0f);
        private static readonly Vector3 SpeakerSize = new Vector3(0.36f, 0.54f, 0.3f);

        // The speaker: plays what the radios of its ship play, wherever it is in the ship
        private static void ConfigureSpeaker(PartSettings settings, GameObject prefab)
        {
            var materials = new Dictionary<string, DeviceMaterial>
            {
                ["TF_Speaker_Case"] = new DeviceMaterial(new Color(0.86f, 0.88f, 0.9f), 0.3f, 0.45f),
                ["TF_NoPaint_SpeakerTrim"] = new DeviceMaterial(new Color(0.13f, 0.14f, 0.16f), 0f, 0.15f),
                ["TF_NoPaint_SpeakerMetal"] = new DeviceMaterial(new Color(0.55f, 0.6f, 0.66f), 1f, 0.5f),
                ["TF_NoPaint_SpeakerCone"] = new DeviceMaterial(new Color(0.2f, 0.21f, 0.23f), 0.1f, 0.45f),
                ["TF_NoPaint_SpeakerGlow"] = new DeviceMaterial(new Color(0.3f, 0.85f, 1f), 0f, 0.1f, new Color(0.25f, 0.8f, 1f) * 0.75f)
            };
            Dictionary<string, Mesh> model = StellarDriveDemoTFAdvanced.Common.ObjModels.Load("speaker");
            if (model == null)
            {
                var box = new MeshBuilder();
                box.Box(SpeakerCenter, SpeakerSize);
                model = new Dictionary<string, Mesh> { ["TF_Speaker_Case"] = box.Build("TF_Speaker_Simple") };
            }
            Transform visuals = DeviceModels.Build(settings, prefab, Loc.Pick("Enceinte radio", "Radio speaker", "Altavoz de radio"),
                Loc.Pick("Enceinte : joue la musique des radios du même vaisseau ou bâtiment, où qu'elle soit posée. Regarde-la et appuie sur la touche d'utilisation (T) pour ouvrir la radio.",
                    "Speaker: plays what the radios of the same ship or building play, wherever it stands. Look at it and press the use key (T) to open the radio.",
                    "Altavoz: reproduce la música de las radios de la misma nave o edificio, esté donde esté. Míralo y pulsa la tecla de uso (T) para abrir la radio."),
                2f, new[] { (110u, 2), (112u, 2), (101u, 1) }, SpeakerCenter, SpeakerSize, model, materials);
            SpeakerTextures.Dress(visuals);
            UsablePart.AddBox(visuals, SpeakerCenter, SpeakerSize + Vector3.one * 0.06f);
            visuals.gameObject.AddComponent<SpeakerVisuals>();
        }

        /// <summary>Network handlers; installed whether or not the part could be registered.</summary>
        public static void Install()
        {
            TFNet.OnServer(TFMessageKind.Radio, OnServerRadio);
            TFNet.OnClient(TFMessageKind.Radio, (kind, ship, part, text) => ClientStates[new PartKey(ship, part)] = text);
            TFNet.OnClient(TFMessageKind.RadioRefused, (kind, ship, part, text) => RadioMenu.Refused());
            TFNet.SyncRequested += connection =>
            {
                foreach (KeyValuePair<PartKey, string> radio in ServerStates.ToList())
                    TFNet.SendTo(connection, TFMessageKind.Radio, radio.Key.ShipId, radio.Key.PartId, radio.Value);
            };
            TFNet.Disconnected += () =>
            {
                ServerStates.Clear();
                ClientStates.Clear();
                RadioSound.StopAll();
            };
        }

        private static void Configure(PartSettings settings, GameObject prefab)
        {
            Transform visuals = DeviceModels.Build(settings, prefab, "Radio",
                Loc.Pick("Enceinte radio : regarde-la et appuie sur la touche d'utilisation (T) pour écouter les musiques du jeu ou coller un lien YouTube. Tous les joueurs proches l'entendent.", "Radio speaker: look at it and press the use key (T) to play the game's music or paste a YouTube link. Every nearby player hears it.", "Altavoz de radio: mírala y pulsa la tecla de uso (T) para escuchar la música del juego o pegar un enlace de YouTube. Todos los jugadores cercanos la oyen."),
                3f, new[] { (110u, 3), (112u, 2), (102u, 1) }, BoundsCenter, BoundsSize, Shapes(), Materials());
            UsablePart.AddBox(visuals, BoundsCenter, BoundsSize + Vector3.one * 0.06f);
            AddBars(visuals);
            visuals.gameObject.AddComponent<RadioVisuals>();
        }

        private static Dictionary<string, DeviceMaterial> Materials() => new Dictionary<string, DeviceMaterial>
        {
            ["TF_Radio_Case"] = new DeviceMaterial(new Color(0.86f, 0.88f, 0.9f), 0.1f, 0.35f),
            ["TF_NoPaint_RadioTrim"] = new DeviceMaterial(new Color(0.09f, 0.1f, 0.12f), 0.3f, 0.35f),
            // Low smoothness everywhere: glossy pieces mirrored the bright sky and the lamps (flashes)
            ["TF_NoPaint_RadioMetal"] = new DeviceMaterial(new Color(0.55f, 0.6f, 0.66f), 0.8f, 0.35f),
            ["TF_NoPaint_RadioGlass"] = new DeviceMaterial(new Color(0.02f, 0.05f, 0.09f), 0.1f, 0.3f),
            ["TF_NoPaint_RadioGrille"] = new DeviceMaterial(new Color(0.16f, 0.18f, 0.21f), 0.4f, 0.4f),
            ["TF_NoPaint_RadioGlow"] = new DeviceMaterial(new Color(0.3f, 0.85f, 1f), 0f, 0.1f, new Color(0.25f, 0.8f, 1f) * 0.75f),
            ["TF_NoPaint_RadioAccent"] = new DeviceMaterial(new Color(1f, 0.5f, 0.15f), 0f, 0.1f, new Color(1f, 0.45f, 0.1f) * 0.75f)
        };

        /// <summary>The equalizer's bars on the screen (separate objects, moved by <see cref="RadioVisuals"/>).</summary>
        internal const int Bars = 11;
        internal const string BarName = "TF_NoPaint_RadioBar";
        internal static readonly Vector3 ScreenCenter = new Vector3(0f, 0.19f, -0.112f);
        internal const float ScreenWidth = 0.2f, ScreenHeight = 0.07f;

        /// <summary>
        /// A modern smart speaker: a white capsule body lying on a dark plinth, a glass screen on its
        /// front with the equalizer, two round drivers behind cyan lit rings at its ends, a light
        /// strip along its top, a slanted antenna fin and an orange power light. Front is -z.
        /// </summary>
        internal static Dictionary<string, MeshBuilder> Shapes()
        {
            var body = new MeshBuilder();
            var trim = new MeshBuilder();
            var metal = new MeshBuilder();
            var glass = new MeshBuilder();
            var grille = new MeshBuilder();
            var glow = new MeshBuilder();
            var accent = new MeshBuilder();
            const float length = 0.46f, radius = 0.105f, axisY = 0.18f;
            float half = length * 0.5f;

            // ---- Plinth: a low dark base, rounded ends, a lit groove and four pads
            trim.Box(new Vector3(0f, 0.02f, 0f), new Vector3(length - 0.06f, 0.04f, 0.16f));
            foreach (float s in new[] { -1f, 1f })
                trim.Cylinder(new Vector3(s * (half - 0.03f), 0f, 0f), new Vector3(s * (half - 0.03f), 0.04f, 0f), 0.08f, 0.08f, 20);
            glow.Box(new Vector3(0f, 0.041f, -0.081f), new Vector3(length - 0.1f, 0.006f, 0.004f));
            metal.Box(new Vector3(0f, 0.055f, 0f), new Vector3(0.12f, 0.03f, 0.08f));
            foreach (float x in new[] { -half + 0.05f, half - 0.05f })
                foreach (float z in new[] { -0.05f, 0.05f })
                    trim.Cylinder(new Vector3(x, -0.004f, z), new Vector3(x, 0.002f, z), 0.014f, 0.014f, 10);

            // ---- Capsule body along x: a cylinder and two rounded caps (stacked rings)
            body.Cylinder(new Vector3(-half + radius * 0.6f, axisY, 0f), new Vector3(half - radius * 0.6f, axisY, 0f), radius, radius, 32, false, false);
            foreach (float s in new[] { -1f, 1f })
            {
                float x0 = s * (half - radius * 0.6f);
                const int steps = 4;
                for (int i = 0; i < steps; i++)
                {
                    float a0 = i / (float)steps * Mathf.PI * 0.5f, a1 = (i + 1) / (float)steps * Mathf.PI * 0.5f;
                    float r0 = radius * Mathf.Cos(a0), r1 = radius * Mathf.Cos(a1);
                    float d0 = radius * 0.6f * Mathf.Sin(a0), d1 = radius * 0.6f * Mathf.Sin(a1);
                    body.Cylinder(new Vector3(x0 + s * d0, axisY, 0f), new Vector3(x0 + s * d1, axisY, 0f), r0, Mathf.Max(r1, 0.0001f), 32, false, i == steps - 1);
                }
                // Driver at each end: a dark grille behind a lit ring and a chrome hub
                Vector3 end = new Vector3(s * (half - 0.005f), axisY, 0f);
                grille.Cylinder(end - Vector3.right * s * 0.01f, end + Vector3.right * s * 0.006f, 0.07f, 0.07f, 28);
                for (int r = 0; r < 3; r++)
                    trim.Cylinder(end + Vector3.right * s * (0.006f + r * 0.001f), end + Vector3.right * s * (0.008f + r * 0.001f), 0.024f + r * 0.016f, 0.024f + r * 0.016f, 28, false, false);
                glow.Cylinder(end + Vector3.right * s * 0.004f, end + Vector3.right * s * 0.01f, 0.078f, 0.078f, 32, false, false);
                metal.Cylinder(end + Vector3.right * s * 0.006f, end + Vector3.right * s * 0.02f, 0.018f, 0.012f, 16);
            }

            // ---- Front screen: a curved glass band (flat facets), a frame, the bars' backing
            const int facets = 7;
            for (int i = 0; i < facets; i++)
            {
                float a0 = Mathf.Lerp(-0.42f, 0.42f, i / (float)facets), a1 = Mathf.Lerp(-0.42f, 0.42f, (i + 1) / (float)facets);
                Vector3 n0 = new Vector3(0f, Mathf.Sin(a0), -Mathf.Cos(a0)), n1 = new Vector3(0f, Mathf.Sin(a1), -Mathf.Cos(a1));
                Vector3 p0 = new Vector3(0f, axisY, 0f) + n0 * (radius + 0.002f), p1 = new Vector3(0f, axisY, 0f) + n1 * (radius + 0.002f);
                float w = ScreenWidth + 0.04f;
                glass.Block(new[]
                {
                    p0 - Vector3.right * w * 0.5f, p0 + Vector3.right * w * 0.5f, p1 - Vector3.right * w * 0.5f, p1 + Vector3.right * w * 0.5f,
                    p0 - Vector3.right * w * 0.5f - n0 * 0.003f, p0 + Vector3.right * w * 0.5f - n0 * 0.003f, p1 - Vector3.right * w * 0.5f - n1 * 0.003f, p1 + Vector3.right * w * 0.5f - n1 * 0.003f
                });
            }
            foreach (float s in new[] { -1f, 1f })
                trim.Box(new Vector3(s * (ScreenWidth * 0.5f + 0.026f), axisY, -radius * 0.9f), new Vector3(0.008f, 0.08f, 0.03f));
            // Tick line under the bars, a clock-like readout of dots on the right
            glow.Box(ScreenCenter + new Vector3(0f, -ScreenHeight * 0.5f - 0.008f, -0.006f), new Vector3(ScreenWidth, 0.002f, 0.001f));

            // ---- Top: the light strip, touch buttons and the antenna fin
            glow.Box(new Vector3(0f, axisY + radius + 0.001f, 0.02f), new Vector3(length - 0.18f, 0.004f, 0.012f));
            for (int i = 0; i < 4; i++)
            {
                var at = new Vector3(-0.09f + i * 0.06f, axisY + radius * 0.97f, -0.03f);
                trim.Cylinder(at, at + Vector3.up * 0.006f, 0.013f, 0.012f, 14);
                metal.Cylinder(at + Vector3.up * 0.006f, at + Vector3.up * 0.007f, 0.007f, 0.007f, 12);
            }
            var fin = new Vector3(0.15f, axisY + radius - 0.01f, 0.04f);
            metal.Block(new[]
            {
                fin + new Vector3(-0.025f, 0f, -0.004f), fin + new Vector3(0.03f, 0f, -0.004f), fin + new Vector3(0.02f, 0.11f, -0.003f), fin + new Vector3(0.035f, 0.11f, -0.003f),
                fin + new Vector3(-0.025f, 0f, 0.004f), fin + new Vector3(0.03f, 0f, 0.004f), fin + new Vector3(0.02f, 0.11f, 0.003f), fin + new Vector3(0.035f, 0.11f, 0.003f)
            });
            accent.Box(fin + new Vector3(0.027f, 0.112f, 0f), new Vector3(0.016f, 0.006f, 0.008f));
            // Power light beside the screen
            accent.Cylinder(new Vector3(-ScreenWidth * 0.5f - 0.05f, axisY + 0.03f, -radius * 0.93f), new Vector3(-ScreenWidth * 0.5f - 0.05f, axisY + 0.03f, -radius * 0.99f), 0.007f, 0.006f, 12);

            return new Dictionary<string, MeshBuilder>
            {
                ["TF_Radio_Case"] = body,
                ["TF_NoPaint_RadioTrim"] = trim,
                ["TF_NoPaint_RadioMetal"] = metal,
                ["TF_NoPaint_RadioGlass"] = glass,
                ["TF_NoPaint_RadioGrille"] = grille,
                ["TF_NoPaint_RadioGlow"] = glow,
                ["TF_NoPaint_RadioAccent"] = accent
            };
        }

        /// <summary>The equalizer's bars: thin lit boxes on the screen, scaled in height by the visuals.</summary>
        internal static void AddBars(Transform visuals)
        {
            var bar = new MeshBuilder();
            bar.Box(new Vector3(0f, 0.5f, 0f), Vector3.one);
            Mesh mesh = bar.Build("TF_RadioBar");
            Material material = LitMaterials.Get("TF_RadioBarGlow", new Color(0.3f, 0.85f, 1f), 0f, 0.1f, new Color(0.3f, 0.9f, 1f) * 0.75f);
            float step = ScreenWidth / Bars;
            for (int i = 0; i < Bars; i++)
            {
                var child = new GameObject(BarName + i) { layer = visuals.gameObject.layer };
                child.transform.SetParent(visuals, false);
                child.transform.localPosition = ScreenCenter + new Vector3(-ScreenWidth * 0.5f + step * (i + 0.5f), -ScreenHeight * 0.5f, -0.004f);
                child.transform.localScale = new Vector3(step * 0.6f, ScreenHeight * 0.15f, 0.002f);
                child.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = child.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        /// <summary>Asks the host to change what this radio plays.</summary>
        public static void Request(PartKey radio, string state) =>
            TFNet.SendToServer(TFMessageKind.Radio, radio.ShipId, radio.PartId, state);

        // The host keeps only well-formed states for radios that exist, so a client cannot make
        // the others fetch anything but a YouTube video id or play anything but a music event
        private static void OnServerRadio(NetworkConnection sender, TFMessageKind kind, uint shipId, ushort partId, string text)
        {
            if (!IsValidState(text))
                return;
            // The host may stop some players from changing the radios (never itself)
            string player = PlayerName(sender);
            if (sender != null && !sender.IsLocalClient && player != null && IsLocked(player))
            {
                TFMod.Log.Msg($"radio: {player} may not change the radios");
                TFNet.SendTo(sender, TFMessageKind.RadioRefused, shipId, partId, "");
                return;
            }
            var ships = ServiceLocator.GetService<IShipsServerProvider>();
            if (ships == null || !ships.TryGetShip(shipId, out IShipStateRead ship) || !ship.TryGetStatefulPart(partId, out StatefulPart part)
                || part.Settings == null || part.Settings.id != RadioId)
                return;
            var key = new PartKey(shipId, partId);
            if (text.Length == 0)
                ServerStates.Remove(key);
            else
                ServerStates[key] = text;
            TFNet.SendToAll(TFMessageKind.Radio, shipId, partId, text);
        }

        /// <summary>The name of the player on a connection (host side), or null.</summary>
        public static string PlayerName(NetworkConnection connection)
        {
            if (connection == null)
                return null;
            var player = GameServices.PlayersServer?.ActivePlayers.FirstOrDefault(p => p != null && p.ConnectionId == connection.ClientId);
            return player?.Name;
        }

        private static HashSet<string> Names(string list) =>
            new HashSet<string>((list ?? "").Split(',').Select(n => n.Trim()).Where(n => n.Length > 0), StringComparer.OrdinalIgnoreCase);

        public static bool IsLocked(string player) => Names(Settings.RadioLockedPlayers.Value).Contains(player);

        public static void SetLocked(string player, bool locked)
        {
            HashSet<string> names = Names(Settings.RadioLockedPlayers.Value);
            if (locked)
                names.Add(player);
            else
                names.Remove(player);
            Settings.RadioLockedPlayers.Value = string.Join(",", names);
            MelonLoader.MelonPreferences.Save();
        }

        /// <summary>Whether this player muted a radio for themselves.</summary>
        public static bool IsMuted(PartKey radio) => Names(Settings.RadioMuted.Value).Contains(radio.ToString());

        public static void SetMuted(PartKey radio, bool muted)
        {
            HashSet<string> keys = Names(Settings.RadioMuted.Value);
            if (muted)
                keys.Add(radio.ToString());
            else
                keys.Remove(radio.ToString());
            Settings.RadioMuted.Value = string.Join(",", keys);
            MelonLoader.MelonPreferences.Save();
        }

        public static bool IsValidState(string text)
        {
            if (text == null || text.Length > 200)
                return false;
            if (text.Length == 0)
                return true;
            if (text.StartsWith("yt|"))
            {
                string id = text.Substring(3);
                return id.Length == 11 && id.All(c => char.IsLetterOrDigit(c) && c < 128 || c == '_' || c == '-');
            }
            if (text.StartsWith("game|"))
            {
                string path = text.Substring(5);
                return path.StartsWith("event:/", StringComparison.OrdinalIgnoreCase) && path.IndexOf("music", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            return false;
        }

        /// <summary>The game's music events, read from its loaded sound banks.</summary>
        public static List<string> GameTracks()
        {
            if (_tracks != null && _tracks.Count > 0)
                return _tracks;
            _tracks = new List<string>();
            try
            {
                if (RuntimeManager.StudioSystem.getBankList(out Bank[] banks) != FMOD.RESULT.OK)
                    return _tracks;
                foreach (Bank bank in banks)
                {
                    if (bank.getEventList(out EventDescription[] events) != FMOD.RESULT.OK)
                        continue;
                    foreach (EventDescription description in events)
                    {
                        if (description.getPath(out string path) == FMOD.RESULT.OK && path != null
                            && path.IndexOf("/music/", StringComparison.OrdinalIgnoreCase) >= 0)
                            _tracks.Add(path);
                    }
                }
                _tracks.Sort(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception e)
            {
                TFMod.Log.Warning("radio: could not list the game's music: " + e.Message);
            }
            return _tracks;
        }

        private static List<string> _tracks;

        /// <summary>"event:/Music/EarthLikeAmbient" → "Earth Like Ambient".</summary>
        public static string TrackName(string path)
        {
            string name = path.Substring(path.LastIndexOf('/') + 1);
            var result = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]))
                    result.Append(' ');
                result.Append(name[i] == '_' ? ' ' : name[i]);
            }
            return result.ToString();
        }
    }

    /// <summary>The radio on screen: its equalizer bars dance while it plays (heard here), and rest low when off.</summary>
    internal sealed class RadioVisuals : UsablePart
    {
        private readonly List<Transform> _bars = new List<Transform>();
        private float _next;
        private float[] _heights;

        public override string Label => "Radio";

        public override void Use() => RadioMenu.Show(Key);

        protected override void Awake()
        {
            base.Awake();
            for (int i = 0; i < Radio.Bars; i++)
            {
                Transform bar = transform.Find(Radio.BarName + i);
                if (bar != null)
                    _bars.Add(bar);
            }
            _heights = new float[_bars.Count];
        }

        private void LateUpdate()
        {
            if (!HasContext || _bars.Count == 0 || Time.unscaledTime < _next)
                return;
            _next = Time.unscaledTime + 0.05f;
            Camera camera = MainCamera.Get();
            if (camera != null && (camera.transform.position - transform.position).sqrMagnitude > 30f * 30f)
                return;
            bool playing = Radio.ClientStates.TryGetValue(Key, out string state) && !string.IsNullOrEmpty(state) && !Radio.IsMuted(Key);
            float t = Time.time;
            for (int i = 0; i < _bars.Count; i++)
            {
                // A spectrum: lows taller, noise on top (no beat: all the bars rising at once flashed)
                float target = playing
                    ? Mathf.Clamp01(0.25f + 0.6f * Mathf.PerlinNoise(i * 0.7f, t * 3.1f) * (1.2f - i / (float)_bars.Count * 0.5f))
                    : 0.08f + 0.04f * Mathf.Sin(t * 1.5f + i * 0.5f);
                _heights[i] = Mathf.Lerp(_heights[i], target, playing ? 0.6f : 0.2f);
                Vector3 scale = _bars[i].localScale;
                scale.y = Radio.ScreenHeight * Mathf.Max(0.05f, _heights[i]);
                _bars[i].localScale = scale;
            }
        }
    }

    /// <summary>A speaker: heard like its ship's radios; using it opens the radio that plays (or any radio of the ship).</summary>
    internal sealed class SpeakerVisuals : UsablePart
    {
        public override string Label => Loc.Pick("Enceinte (radio)", "Speaker (radio)", "Altavoz (radio)");

        public override void Use()
        {
            PartKey? radio = null;
            foreach (UsablePart part in All)
            {
                if (!(part is RadioVisuals visuals) || !visuals.HasContext || visuals.Key.ShipId != Key.ShipId)
                    continue;
                radio = visuals.Key;
                if (Radio.ClientStates.TryGetValue(visuals.Key, out string state) && !string.IsNullOrEmpty(state))
                    break;
            }
            if (radio.HasValue)
                RadioMenu.Show(radio.Value);
            else
                RadioMenu.Notice(Loc.Pick("Aucune radio dans ce vaisseau : pose une radio, l'enceinte jouera sa musique.", "No radio in this ship: place a radio and the speaker will play its music.", "No hay radio en esta nave: coloca una radio y el altavoz reproducirá su música."));
        }
    }

    /// <summary>The radio's window: game tracks, a YouTube link, stop and volume.</summary>
    internal static class RadioMenu
    {
        private static bool _open;
        private static PartKey _radio;
        private static string _link = "";
        private static string _error;
        private static Vector2 _scroll, _players;

        // The other players in the world (host side)
        private static List<string> OtherPlayers()
        {
            var names = new List<string>();
            var players = GameServices.PlayersServer;
            if (players == null)
                return names;
            foreach (var player in players.ActivePlayers)
            {
                if (player == null || player.IsHost)
                    continue;
                string name = player.Name;
                if (!string.IsNullOrEmpty(name))
                    names.Add(name);
            }
            return names;
        }

        public static void Show(PartKey radio)
        {
            _radio = radio;
            _open = true;
            _error = null;
            ModMenu.Open(() => _open = false);
        }

        private static string _notice;
        private static float _noticeUntil;

        public static void Notice(string message)
        {
            _notice = message;
            _noticeUntil = Time.unscaledTime + 5f;
        }

        /// <summary>The host refused our change (it blocked us from changing the radios).</summary>
        public static void Refused() =>
            Notice(_error = Loc.Pick("L'hôte ne te laisse pas changer la musique des radios (il peut t'autoriser dans la fenêtre de la radio).",
                "The host does not let you change what the radios play (they can allow you in the radio's window).",
                "El anfitrión no te deja cambiar la música de las radios (puede permitírtelo en la ventana de la radio)."));

        public static void Draw()
        {
            if (_notice != null && Time.unscaledTime < _noticeUntil && !_open)
                DeviceUi.Toast(Screen.width, Screen.height, _notice);
            if (!_open || !ModMenu.IsOpen)
                return;
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.7f, 2.5f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale, height = Screen.height / scale;

            Radio.ClientStates.TryGetValue(_radio, out string state);
            state = state ?? "";
            bool host = TFNet.IsServer;
            List<string> others = host ? OtherPlayers() : new List<string>();
            Rect area = DeviceUi.Window(width, height, 600f, 700f + (others.Count > 0 ? 50f + Mathf.Min(others.Count, 4) * 36f : 0f), "RADIO");
            GUILayout.BeginArea(area);

            GUILayout.Label(Loc.Pick("En cours : ", "Now playing: ", "Sonando: ") + Describe(state), DeviceUi.Text);
            string status = RadioSound.Status(_radio);
            if (!string.IsNullOrEmpty(status))
                GUILayout.Label(status, DeviceUi.Muted);
            GUILayout.Space(10f);

            GUILayout.Label(Loc.Pick("LIEN YOUTUBE", "YOUTUBE LINK", "ENLACE DE YOUTUBE"), DeviceUi.Muted);
            GUILayout.BeginHorizontal();
            _link = GUILayout.TextField(_link ?? "", 300, DeviceUi.Field, GUILayout.Height(34f), GUILayout.ExpandWidth(true));
            if (GUILayout.Button(Loc.Pick("Coller", "Paste", "Pegar"), DeviceUi.Button, GUILayout.Width(80f), GUILayout.Height(34f)))
                _link = GUIUtility.systemCopyBuffer ?? "";
            if (GUILayout.Button(Loc.Pick("Lire", "Play", "Reproducir"), DeviceUi.Button, GUILayout.Width(70f), GUILayout.Height(34f)))
            {
                string id = YouTubeAudio.VideoId(_link);
                if (id == null)
                {
                    _error = Loc.Pick("Ce n'est pas un lien YouTube (youtube.com/watch?v=..., youtu.be/..., shorts/...).", "This is not a YouTube link (youtube.com/watch?v=..., youtu.be/..., shorts/...).", "No es un enlace de YouTube (youtube.com/watch?v=..., youtu.be/..., shorts/...).");
                }
                else
                {
                    _error = null;
                    Radio.Request(_radio, "yt|" + id);
                }
            }
            GUILayout.EndHorizontal();
            if (_error != null)
                GUILayout.Label(_error, DeviceUi.Muted);
            GUILayout.Space(10f);

            GUILayout.Label(Loc.Pick("MUSIQUES DU JEU", "GAME MUSIC", "MÚSICA DEL JUEGO"), DeviceUi.Muted);
            List<string> tracks = Radio.GameTracks();
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(220f));
            if (tracks.Count == 0)
                GUILayout.Label(Loc.Pick("Aucune musique trouvée dans les banques de son du jeu.", "No music found in the game's sound banks.", "No se encontró música en los bancos de sonido del juego."), DeviceUi.Muted);
            foreach (string track in tracks)
            {
                string value = "game|" + track;
                if (GUILayout.Button(Radio.TrackName(track), value == state ? DeviceUi.Selected : DeviceUi.Button, GUILayout.Height(34f)))
                    Radio.Request(_radio, value);
                GUILayout.Space(3f);
            }
            GUILayout.EndScrollView();
            GUILayout.Space(8f);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Volume", DeviceUi.Text, GUILayout.Width(70f));
            float volume = GUILayout.HorizontalSlider(Settings.RadioVolume.Value, 0f, 1f, GUILayout.Height(20f));
            if (!Mathf.Approximately(volume, Settings.RadioVolume.Value))
                Settings.RadioVolume.Value = volume;
            GUILayout.Label(Mathf.RoundToInt(volume * 100f) + "%", DeviceUi.Muted, GUILayout.Width(44f));
            GUILayout.EndHorizontal();
            // Two explicit choices: a single toggle showing its state was read as an action
            bool muted = Radio.IsMuted(_radio);
            GUILayout.BeginHorizontal();
            GUILayout.Label(Loc.Pick("Pour moi :", "For me:", "Para mí:"), DeviceUi.Text, GUILayout.Width(90f));
            if (GUILayout.Button(Loc.Pick("J'entends", "I hear it", "La oigo"), muted ? DeviceUi.Button : DeviceUi.Selected, GUILayout.Height(32f)))
                Radio.SetMuted(_radio, false);
            if (GUILayout.Button(Loc.Pick("Muet (les autres l'entendent)", "Muted (the others hear it)", "Silencio (los demás la oyen)"), muted ? DeviceUi.Selected : DeviceUi.Button, GUILayout.Height(32f)))
                Radio.SetMuted(_radio, true);
            GUILayout.EndHorizontal();
            if (others.Count > 0)
            {
                GUILayout.Space(6f);
                GUILayout.Label(Loc.Pick("JOUEURS (tu es l'hôte) : qui peut changer la musique des radios", "PLAYERS (you are the host): who may change what the radios play", "JUGADORES (eres el anfitrión): quién puede cambiar la música de las radios"), DeviceUi.Muted);
                _players = GUILayout.BeginScrollView(_players, GUILayout.Height(Mathf.Min(others.Count, 4) * 36f));
                foreach (string other in others)
                {
                    bool locked = Radio.IsLocked(other);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(other, DeviceUi.Text, GUILayout.ExpandWidth(true));
                    if (GUILayout.Button(Loc.Pick("Autorisé", "Allowed", "Permitido"), locked ? DeviceUi.Button : DeviceUi.Selected, GUILayout.Width(110f), GUILayout.Height(30f)))
                        Radio.SetLocked(other, false);
                    if (GUILayout.Button(Loc.Pick("Bloqué", "Blocked", "Bloqueado"), locked ? DeviceUi.Selected : DeviceUi.Button, GUILayout.Width(110f), GUILayout.Height(30f)))
                        Radio.SetLocked(other, true);
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();
            }
            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Loc.Pick("Éteindre", "Turn off", "Apagar"), DeviceUi.Button, GUILayout.Height(34f)))
                Radio.Request(_radio, "");
            if (GUILayout.Button(Loc.Pick("Fermer (Échap)", "Close (Esc)", "Cerrar (Esc)"), DeviceUi.Button, GUILayout.Height(34f)))
            {
                MelonLoader.MelonPreferences.Save();
                ModMenu.Close();
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            GUI.matrix = previous;
        }

        public static string Describe(string state)
        {
            if (string.IsNullOrEmpty(state))
                return Loc.Pick("éteinte", "off", "apagada");
            if (state.StartsWith("game|"))
                return Radio.TrackName(state.Substring(5));
            if (state.StartsWith("yt|"))
                return "YouTube (" + state.Substring(3) + ")";
            return state;
        }
    }

    /// <summary>
    /// Plays radios on this client through FMOD: a Studio event for the game's music, a streamed
    /// WAV for YouTube. Volume follows the distance between the camera and the radio; a radio
    /// out of sight range (not loaded) is silent.
    /// </summary>
    internal static class RadioSound
    {
        private const float FullVolumeDistance = 2f;
        private const float SilentDistance = 20f;

        private sealed class Playing
        {
            public string State;
            public EventInstance Event;
            public bool HasEvent;
            public FMOD.Sound Sound;
            public FMOD.Channel Channel;
            public bool HasChannel;
            public YouTubeAudio.Job Job;
        }

        private static readonly Dictionary<PartKey, Playing> Radios = new Dictionary<PartKey, Playing>();

        public static string Status(PartKey radio)
        {
            if (!Radios.TryGetValue(radio, out Playing playing) || playing.Job == null)
                return null;
            switch (playing.Job.Status)
            {
                case YouTubeAudio.State.Working: return playing.Job.Step + "...";
                case YouTubeAudio.State.Failed: return Loc.Pick("Échec : ", "Failed: ", "Error: ") + playing.Job.Error;
                default: return playing.HasChannel ? Loc.Pick("Lecture (en boucle)", "Playing (looped)", "Reproduciendo (en bucle)") : null;
            }
        }

        public static void Update()
        {
            if (!RuntimeManager.IsInitialized)
                return;
            Camera camera = MainCamera.Get();
            // Start, change or stop radios to match what the host says they play
            foreach (KeyValuePair<PartKey, string> entry in Radio.ClientStates)
            {
                Radios.TryGetValue(entry.Key, out Playing playing);
                if (playing != null && playing.State == entry.Value)
                    continue;
                if (playing != null)
                    Stop(playing);
                Radios[entry.Key] = Start(entry.Value);
            }
            foreach (PartKey gone in Radios.Keys.Where(k => !Radio.ClientStates.ContainsKey(k)).ToList())
            {
                Stop(Radios[gone]);
                Radios.Remove(gone);
            }

            foreach (KeyValuePair<PartKey, Playing> entry in Radios)
            {
                float volume = 0f, pan = 0f;
                if (camera != null && !Radio.IsMuted(entry.Key))
                {
                    Mix(entry.Key, camera.transform.position, camera.transform.right, out volume, out pan);
                    volume *= Mathf.Clamp01(Settings.RadioVolume.Value);
                }
                Drive(entry.Value, volume, pan);
            }
        }

        /// <summary>
        /// What the listener hears of a radio: each of its sources (the radio and the speakers of
        /// its ship) fades with its own distance; their powers add up, and the sound leans toward
        /// the louder ones (left/right).
        /// </summary>
        public static void Mix(PartKey key, Vector3 listener, Vector3 right, out float volume, out float pan)
        {
            float power = 0f, lean = 0f;
            foreach (UsablePart source in Sources(key))
            {
                Vector3 offset = source.Center - listener;
                float distance = offset.magnitude;
                float fade = Mathf.Clamp01(1f - (distance - FullVolumeDistance) / (SilentDistance - FullVolumeDistance));
                float gain = fade * fade;
                power += gain * gain;
                if (distance > 0.5f)
                    lean += gain * gain * Mathf.Clamp(Vector3.Dot(offset / distance, right), -1f, 1f) * 0.8f;
            }
            volume = Mathf.Min(1f, Mathf.Sqrt(power));
            pan = power > 0f ? lean / power : 0f;
        }

        private static Playing Start(string state)
        {
            var playing = new Playing { State = state };
            if (state.StartsWith("game|"))
            {
                try
                {
                    playing.Event = RuntimeManager.CreateInstance(state.Substring(5));
                    playing.Event.setVolume(0f);
                    playing.Event.start();
                    playing.HasEvent = true;
                }
                catch (Exception e)
                {
                    TFMod.Log.Warning("radio: could not play " + state + ": " + e.Message);
                }
            }
            else if (state.StartsWith("yt|"))
            {
                playing.Job = YouTubeAudio.Request(state.Substring(3));
            }
            return playing;
        }

        private static void Drive(Playing playing, float volume, float pan)
        {
            if (playing.HasEvent)
            {
                playing.Event.setVolume(volume);
                // (its channel group exists once the event plays)
                if (playing.Event.getChannelGroup(out FMOD.ChannelGroup group) == FMOD.RESULT.OK)
                    group.setPan(pan);
                // Music events end on their own; a radio keeps playing
                if (playing.Event.getPlaybackState(out PLAYBACK_STATE state) == FMOD.RESULT.OK && state == PLAYBACK_STATE.STOPPED)
                    playing.Event.start();
            }
            if (!playing.HasChannel && playing.Job != null && playing.Job.Status == YouTubeAudio.State.Ready && playing.Job.WavPath != null)
                OpenStream(playing);
            // Not downloaded here: the host sends it
            if (playing.Job != null && playing.Job.Status == YouTubeAudio.State.Failed && !playing.Job.FromHost && !StellarDriveDemoTFAdvanced.Common.GameServices.IsServer)
                RadioShare.AskHost(playing.Job);
            if (playing.HasChannel)
            {
                playing.Channel.setVolume(volume);
                playing.Channel.setPan(pan);
            }
        }

        private static void OpenStream(Playing playing)
        {
            FMOD.System core = RuntimeManager.CoreSystem;
            FMOD.RESULT result = core.createSound(playing.Job.WavPath, FMOD.MODE.CREATESTREAM | FMOD.MODE.LOOP_NORMAL | FMOD.MODE._2D, out FMOD.Sound sound);
            if (result != FMOD.RESULT.OK)
            {
                playing.Job.Error = Loc.Pick("le son n'a pas pu être ouvert (", "the sound could not be opened (", "no se pudo abrir el sonido (") + result + ")";
                playing.Job.Status = YouTubeAudio.State.Failed;
                return;
            }
            core.getMasterChannelGroup(out FMOD.ChannelGroup master);
            result = core.playSound(sound, master, true, out FMOD.Channel channel);
            if (result != FMOD.RESULT.OK)
            {
                sound.release();
                playing.Job.Error = Loc.Pick("le son n'a pas pu démarrer (", "the sound could not start (", "no se pudo iniciar el sonido (") + result + ")";
                playing.Job.Status = YouTubeAudio.State.Failed;
                return;
            }
            channel.setVolume(0f);
            channel.setPaused(false);
            playing.Sound = sound;
            playing.Channel = channel;
            playing.HasChannel = true;
        }

        private static void Stop(Playing playing)
        {
            try
            {
                if (playing.HasEvent)
                {
                    playing.Event.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                    playing.Event.release();
                    playing.HasEvent = false;
                }
                if (playing.HasChannel)
                {
                    playing.Channel.stop();
                    playing.Sound.release();
                    playing.HasChannel = false;
                }
            }
            catch (Exception e)
            {
                TFMod.Log.Warning("radio: stopping: " + e.Message);
            }
        }

        public static void StopAll()
        {
            foreach (Playing playing in Radios.Values)
                Stop(playing);
            Radios.Clear();
        }

        // The radio itself and the speakers of its ship
        public static IEnumerable<UsablePart> Sources(PartKey key) =>
            UsablePart.All.Where(part => part != null && part.HasContext && (part is RadioVisuals && part.Key.Equals(key) || part is SpeakerVisuals && part.Key.ShipId == key.ShipId));
    }
}

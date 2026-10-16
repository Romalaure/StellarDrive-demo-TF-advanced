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
            TFMod.Log.Msg("registered the radio");
        }

        /// <summary>Network handlers; installed whether or not the part could be registered.</summary>
        public static void Install()
        {
            TFNet.OnServer(TFMessageKind.Radio, OnServerRadio);
            TFNet.OnClient(TFMessageKind.Radio, (kind, ship, part, text) => ClientStates[new PartKey(ship, part)] = text);
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
                Loc.Pick("Radio rétro : regarde-la et appuie sur la touche d'utilisation (T) pour écouter les musiques du jeu ou coller un lien YouTube. Tous les joueurs proches l'entendent.", "Retro radio: look at it and press the use key (T) to play the game's music or paste a YouTube link. Every nearby player hears it.", "Radio retro: mírala y pulsa la tecla de uso (T) para escuchar la música del juego o pegar un enlace de YouTube. Todos los jugadores cercanos la oyen."),
                3f, new[] { (110u, 3), (112u, 2), (102u, 1) }, BoundsCenter, BoundsSize, Shapes(), Materials());
            UsablePart.AddBox(visuals, BoundsCenter, BoundsSize + Vector3.one * 0.06f);
            visuals.gameObject.AddComponent<RadioVisuals>();
        }

        private static Dictionary<string, DeviceMaterial> Materials() => new Dictionary<string, DeviceMaterial>
        {
            ["TF_Radio_Case"] = new DeviceMaterial(new Color(0.55f, 0.33f, 0.18f), 0f, 0.6f),
            ["TF_NoPaint_RadioGrille"] = new DeviceMaterial(new Color(0.78f, 0.7f, 0.55f), 0f, 0.15f),
            ["TF_NoPaint_RadioMetal"] = new DeviceMaterial(new Color(0.82f, 0.78f, 0.7f), 1f, 0.85f),
            ["TF_NoPaint_RadioTrim"] = new DeviceMaterial(new Color(0.16f, 0.15f, 0.14f), 0.6f, 0.75f),
            ["TF_NoPaint_RadioDial"] = new DeviceMaterial(new Color(1f, 0.82f, 0.5f), 0f, 0.7f, new Color(1f, 0.7f, 0.3f) * 1.6f),
            ["TF_NoPaint_RadioKnob"] = new DeviceMaterial(new Color(0.1f, 0.09f, 0.08f), 0.2f, 0.6f),
            ["TF_NoPaint_RadioLamp"] = new DeviceMaterial(new Color(1f, 0.25f, 0.15f), 0f, 0.8f, new Color(1f, 0.25f, 0.1f) * 2.2f),
            ["TF_NoPaint_RadioLed"] = new DeviceMaterial(new Color(0.3f, 1f, 0.4f), 0f, 0.8f, new Color(0.25f, 1f, 0.35f) * 2.2f)
        };

        /// <summary>
        /// A 1950s portable: a wooden case with bevelled top edges and a dark front bezel, a round
        /// speaker behind a cloth grille with chrome rings, a lit tuning dial with its scale and red
        /// needle, preset keys, two ridged knobs, a power light, a chrome handle and a telescopic
        /// antenna. Front is -z.
        /// </summary>
        internal static Dictionary<string, MeshBuilder> Shapes()
        {
            var body = new MeshBuilder();
            var grille = new MeshBuilder();
            var metal = new MeshBuilder();
            var trim = new MeshBuilder();
            var dial = new MeshBuilder();
            var knob = new MeshBuilder();
            var lamp = new MeshBuilder();
            var led = new MeshBuilder();
            const float w = 0.5f, h = 0.28f, d = 0.2f, foot = 0.012f;
            float front = -d / 2f;

            // ---- Case: a body, then bevelled top edges (corners in MeshBuilder.Block's bit order: x, y, z)
            body.Box(new Vector3(0f, foot + (h - 0.03f) / 2f, 0f), new Vector3(w, h - 0.03f, d));
            float y0 = foot + h - 0.03f, y1 = foot + h;
            body.Block(new[]
            {
                new Vector3(-w / 2f, y0, -d / 2f), new Vector3(w / 2f, y0, -d / 2f), new Vector3(-w / 2f + 0.025f, y1, -d / 2f + 0.025f), new Vector3(w / 2f - 0.025f, y1, -d / 2f + 0.025f),
                new Vector3(-w / 2f, y0, d / 2f), new Vector3(w / 2f, y0, d / 2f), new Vector3(-w / 2f + 0.025f, y1, d / 2f - 0.025f), new Vector3(w / 2f - 0.025f, y1, d / 2f - 0.025f)
            });
            // Chrome strips round the sides, vent slots on the top
            foreach (float x in new[] { -w / 2f - 0.002f, w / 2f + 0.002f })
                metal.Box(new Vector3(x, foot + 0.05f, 0f), new Vector3(0.004f, 0.008f, d - 0.02f));
            for (int i = 0; i < 6; i++)
                trim.Box(new Vector3(-0.1f + i * 0.04f, y1 + 0.001f, 0.04f), new Vector3(0.022f, 0.002f, 0.07f));
            // Rubber feet
            foreach (float x in new[] { -w / 2f + 0.05f, w / 2f - 0.05f })
                foreach (float z in new[] { -d / 2f + 0.035f, d / 2f - 0.035f })
                    knob.Cylinder(new Vector3(x, 0f, z), new Vector3(x, foot, z), 0.018f, 0.016f, 10);

            // ---- Front: a dark bezel framing the face
            trim.Box(new Vector3(0f, foot + h - 0.04f, front - 0.004f), new Vector3(w - 0.02f, 0.014f, 0.008f));
            trim.Box(new Vector3(0f, foot + 0.018f, front - 0.004f), new Vector3(w - 0.02f, 0.014f, 0.008f));
            foreach (float x in new[] { -w / 2f + 0.017f, w / 2f - 0.017f, 0.035f })
                trim.Box(new Vector3(x, foot + (h - 0.03f) / 2f, front - 0.004f), new Vector3(0.012f, h - 0.07f, 0.008f));

            // ---- Speaker on the left: cloth, a chrome ring with rings inside, a cap, a name plate
            var speaker = new Vector3(-0.105f, foot + 0.135f, front);
            grille.Box(speaker + new Vector3(0f, 0f, -0.002f), new Vector3(0.25f, 0.19f, 0.004f));
            metal.Cylinder(speaker + new Vector3(0f, 0f, -0.002f), speaker + new Vector3(0f, 0f, -0.01f), 0.088f, 0.088f, 32, false, false);
            metal.Cylinder(speaker + new Vector3(0f, 0f, -0.002f), speaker + new Vector3(0f, 0f, -0.01f), 0.08f, 0.08f, 32, false, false);
            for (int r = 0; r < 3; r++)
            {
                float radius = 0.022f + r * 0.02f;
                trim.Cylinder(speaker + new Vector3(0f, 0f, -0.004f), speaker + new Vector3(0f, 0f, -0.006f), radius + 0.003f, radius + 0.003f, 28, false, true);
                grille.Cylinder(speaker + new Vector3(0f, 0f, -0.005f), speaker + new Vector3(0f, 0f, -0.007f), radius, radius, 28, false, true);
            }
            metal.Cylinder(speaker + new Vector3(0f, 0f, -0.006f), speaker + new Vector3(0f, 0f, -0.014f), 0.016f, 0.012f, 16);
            // Two bars across the speaker, a name plate under it
            foreach (float y in new[] { -0.04f, 0.04f })
                metal.Box(speaker + new Vector3(0f, y, -0.011f), new Vector3(0.2f, 0.005f, 0.004f));
            metal.Box(speaker + new Vector3(0f, -0.093f, -0.006f), new Vector3(0.07f, 0.014f, 0.004f));

            // ---- Tuning dial on the right: chrome frame, lit scale, ticks and a red needle
            var dialAt = new Vector3(0.14f, foot + 0.205f, front);
            metal.Box(dialAt + new Vector3(0f, 0f, -0.004f), new Vector3(0.175f, 0.075f, 0.008f));
            dial.Box(dialAt + new Vector3(0f, 0f, -0.009f), new Vector3(0.158f, 0.058f, 0.004f));
            for (int i = 0; i <= 10; i++)
            {
                float x = -0.07f + i * 0.014f;
                knob.Box(dialAt + new Vector3(x, 0.017f - (i % 2) * 0.003f, -0.0115f), new Vector3(0.002f, i % 2 == 0 ? 0.016f : 0.01f, 0.001f));
            }
            knob.Box(dialAt + new Vector3(0f, -0.014f, -0.0115f), new Vector3(0.14f, 0.002f, 0.001f));
            lamp.Box(dialAt + new Vector3(0.018f, 0f, -0.0125f), new Vector3(0.003f, 0.052f, 0.001f));
            // Preset keys under the dial
            for (int i = 0; i < 5; i++)
            {
                var key = new Vector3(0.082f + i * 0.029f, foot + 0.14f, front);
                knob.Box(key + new Vector3(0f, 0f, -0.008f), new Vector3(0.022f, 0.026f, 0.016f));
                metal.Box(key + new Vector3(0f, 0f, -0.0165f), new Vector3(0.018f, 0.022f, 0.002f));
            }
            // Two ridged knobs with chrome caps
            foreach (float x in new[] { 0.095f, 0.185f })
            {
                var at = new Vector3(x, foot + 0.07f, front);
                knob.Cylinder(at, at + new Vector3(0f, 0f, -0.028f), 0.026f, 0.023f, 18);
                for (int k = 0; k < 12; k++)
                {
                    float a = k * Mathf.PI / 6f;
                    knob.Box(at + new Vector3(Mathf.Cos(a) * 0.025f, Mathf.Sin(a) * 0.025f, -0.014f), new Vector3(0.004f, 0.004f, 0.026f), Quaternion.Euler(0f, 0f, k * 30f));
                }
                metal.Cylinder(at + new Vector3(0f, 0f, -0.028f), at + new Vector3(0f, 0f, -0.031f), 0.014f, 0.012f, 14);
                metal.Box(at + new Vector3(0f, 0.017f, -0.0315f), new Vector3(0.003f, 0.01f, 0.002f));
            }
            // Power light between the knobs
            metal.Cylinder(new Vector3(0.14f, foot + 0.07f, front), new Vector3(0.14f, foot + 0.07f, front - 0.006f), 0.009f, 0.009f, 12);
            led.Cylinder(new Vector3(0.14f, foot + 0.07f, front - 0.006f), new Vector3(0.14f, foot + 0.07f, front - 0.01f), 0.006f, 0.005f, 12);

            // ---- Handle: two chrome posts and a curved grip
            foreach (float x in new[] { -0.16f, 0.16f })
            {
                metal.Box(new Vector3(x, y1 + 0.006f, 0f), new Vector3(0.03f, 0.012f, 0.03f));
                metal.Cylinder(new Vector3(x, y1 + 0.01f, 0f), new Vector3(x * 0.95f, y1 + 0.05f, 0f), 0.007f, 0.007f, 8);
            }
            const int arc = 8;
            Vector3 Grip(float t) => new Vector3(Mathf.Lerp(-0.152f, 0.152f, t), y1 + 0.05f + Mathf.Sin(t * Mathf.PI) * 0.012f, 0f);
            for (int i = 0; i < arc; i++)
                metal.Cylinder(Grip(i / (float)arc), Grip((i + 1) / (float)arc), 0.009f, 0.009f, 8, false, false);
            knob.Cylinder(new Vector3(-0.08f, y1 + 0.058f, 0f), new Vector3(0.08f, y1 + 0.058f, 0f), 0.013f, 0.013f, 10);

            // ---- Antenna: a swivel at the back corner and three segments, a ball on top
            var swivel = new Vector3(0.21f, y1 + 0.004f, d / 2f - 0.03f);
            trim.Cylinder(swivel - Vector3.up * 0.004f, swivel + Vector3.up * 0.012f, 0.016f, 0.012f, 10);
            Vector3 dir = new Vector3(0.22f, 1f, 0.08f).normalized;
            float[] lengths = { 0.1f, 0.1f, 0.09f };
            float[] radii = { 0.006f, 0.0045f, 0.0032f };
            Vector3 p = swivel + Vector3.up * 0.01f;
            for (int i = 0; i < lengths.Length; i++)
            {
                Vector3 q = p + dir * lengths[i];
                metal.Cylinder(p, q, radii[i], radii[i], 8);
                metal.Cylinder(q - dir * 0.004f, q + dir * 0.002f, radii[i] + 0.0015f, radii[i] + 0.0015f, 8);
                p = q;
            }
            metal.Cylinder(p, p + dir * 0.008f, 0.006f, 0.006f, 8);

            return new Dictionary<string, MeshBuilder>
            {
                ["TF_Radio_Case"] = body,
                ["TF_NoPaint_RadioGrille"] = grille,
                ["TF_NoPaint_RadioMetal"] = metal,
                ["TF_NoPaint_RadioTrim"] = trim,
                ["TF_NoPaint_RadioDial"] = dial,
                ["TF_NoPaint_RadioKnob"] = knob,
                ["TF_NoPaint_RadioLamp"] = lamp,
                ["TF_NoPaint_RadioLed"] = led
            };
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

    internal sealed class RadioVisuals : UsablePart
    {
        public override string Label => "Radio";

        public override void Use() => RadioMenu.Show(Key);
    }

    /// <summary>The radio's window: game tracks, a YouTube link, stop and volume.</summary>
    internal static class RadioMenu
    {
        private static bool _open;
        private static PartKey _radio;
        private static string _link = "";
        private static string _error;
        private static Vector2 _scroll;

        public static void Show(PartKey radio)
        {
            _radio = radio;
            _open = true;
            _error = null;
            ModMenu.Open(() => _open = false);
        }

        public static void Draw()
        {
            if (!_open || !ModMenu.IsOpen)
                return;
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.7f, 2.5f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale, height = Screen.height / scale;

            Radio.ClientStates.TryGetValue(_radio, out string state);
            state = state ?? "";
            Rect area = DeviceUi.Window(width, height, 600f, 640f, "RADIO");
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
        private const float FullVolumeDistance = 2.5f;
        private const float SilentDistance = 28f;

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
                Playing playing = entry.Value;
                RadioVisuals visuals = FindRadio(entry.Key);
                float volume = 0f;
                float pan = 0f;
                if (visuals != null && camera != null)
                {
                    Vector3 offset = visuals.Center - camera.transform.position;
                    float distance = offset.magnitude;
                    float fade = Mathf.Clamp01(1f - (distance - FullVolumeDistance) / (SilentDistance - FullVolumeDistance));
                    volume = fade * fade * Mathf.Clamp01(Settings.RadioVolume.Value);
                    if (distance > 0.5f)
                        pan = Mathf.Clamp(Vector3.Dot(offset / distance, camera.transform.right), -1f, 1f) * 0.6f;
                }
                Drive(playing, volume, pan);
            }
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
                // Music events end on their own; a radio keeps playing
                if (playing.Event.getPlaybackState(out PLAYBACK_STATE state) == FMOD.RESULT.OK && state == PLAYBACK_STATE.STOPPED)
                    playing.Event.start();
            }
            if (!playing.HasChannel && playing.Job != null && playing.Job.Status == YouTubeAudio.State.Ready && playing.Job.WavPath != null)
                OpenStream(playing);
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

        private static RadioVisuals FindRadio(PartKey key)
        {
            foreach (UsablePart part in UsablePart.All)
            {
                if (part is RadioVisuals radio && radio.HasContext && radio.Key.Equals(key))
                    return radio;
            }
            return null;
        }
    }
}

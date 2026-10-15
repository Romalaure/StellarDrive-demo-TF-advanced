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
                "Radio rétro : regarde-la et appuie sur la touche d'utilisation (T) pour écouter les musiques du jeu ou coller un lien YouTube. Tous les joueurs proches l'entendent.",
                3f, new[] { (110u, 3), (112u, 2), (102u, 1) }, BoundsCenter, BoundsSize, Shapes(), Materials());
            UsablePart.AddBox(visuals, BoundsCenter, BoundsSize + Vector3.one * 0.06f);
            visuals.gameObject.AddComponent<RadioVisuals>();
        }

        private static Dictionary<string, DeviceMaterial> Materials() => new Dictionary<string, DeviceMaterial>
        {
            ["TF_Radio_Case"] = new DeviceMaterial(new Color(0.55f, 0.33f, 0.18f), 0f, 0.55f),
            ["TF_NoPaint_RadioGrille"] = new DeviceMaterial(new Color(0.82f, 0.74f, 0.58f), 0f, 0.2f),
            ["TF_NoPaint_RadioMetal"] = new DeviceMaterial(new Color(0.78f, 0.74f, 0.66f), 1f, 0.8f),
            ["TF_NoPaint_RadioDial"] = new DeviceMaterial(new Color(1f, 0.8f, 0.45f), 0f, 0.7f, new Color(1f, 0.7f, 0.3f) * 1.6f),
            ["TF_NoPaint_RadioKnob"] = new DeviceMaterial(new Color(0.12f, 0.1f, 0.09f), 0.2f, 0.6f)
        };

        internal static Dictionary<string, MeshBuilder> Shapes()
        {
            var body = new MeshBuilder();
            var grille = new MeshBuilder();
            var metal = new MeshBuilder();
            var dial = new MeshBuilder();
            var knob = new MeshBuilder();
            const float w = 0.5f, h = 0.28f, d = 0.2f;
            float front = -d / 2f;

            // Wooden case with rounded-ish edges: a body and a slightly narrower top
            body.Box(new Vector3(0f, 0.01f + h / 2f, 0f), new Vector3(w, h, d));
            body.Box(new Vector3(0f, h + 0.02f, 0f), new Vector3(w - 0.03f, 0.02f, d - 0.03f));
            // Feet
            foreach (float x in new[] { -w / 2f + 0.05f, w / 2f - 0.05f })
                metal.Box(new Vector3(x, 0.005f, 0f), new Vector3(0.05f, 0.01f, d - 0.04f));
            // Speaker grille (cloth) on the left, with brass bars
            grille.Box(new Vector3(-0.09f, 0.16f, front - 0.002f), new Vector3(0.26f, 0.2f, 0.006f));
            for (int i = 0; i < 5; i++)
                metal.Box(new Vector3(-0.09f, 0.08f + i * 0.04f, front - 0.007f), new Vector3(0.26f, 0.006f, 0.006f));
            // Tuning dial on the right
            metal.Box(new Vector3(0.14f, 0.2f, front - 0.003f), new Vector3(0.17f, 0.08f, 0.008f));
            dial.Box(new Vector3(0.14f, 0.2f, front - 0.008f), new Vector3(0.15f, 0.06f, 0.004f));
            // Two knobs below the dial
            foreach (float x in new[] { 0.095f, 0.185f })
                knob.Cylinder(new Vector3(x, 0.09f, front), new Vector3(x, 0.09f, front - 0.025f), 0.022f, 0.02f, 14);
            // Handle on top and a telescopic antenna at the back
            metal.Cylinder(new Vector3(-0.15f, h + 0.03f, 0f), new Vector3(-0.15f, h + 0.07f, 0f), 0.008f, 0.008f, 8);
            metal.Cylinder(new Vector3(0.15f, h + 0.03f, 0f), new Vector3(0.15f, h + 0.07f, 0f), 0.008f, 0.008f, 8);
            metal.Cylinder(new Vector3(-0.155f, h + 0.07f, 0f), new Vector3(0.155f, h + 0.07f, 0f), 0.01f, 0.01f, 8);
            metal.Cylinder(new Vector3(0.2f, h + 0.02f, d / 2f - 0.03f), new Vector3(0.27f, h + 0.3f, d / 2f - 0.03f), 0.005f, 0.003f, 6);

            return new Dictionary<string, MeshBuilder>
            {
                ["TF_Radio_Case"] = body,
                ["TF_NoPaint_RadioGrille"] = grille,
                ["TF_NoPaint_RadioMetal"] = metal,
                ["TF_NoPaint_RadioDial"] = dial,
                ["TF_NoPaint_RadioKnob"] = knob
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

            GUILayout.Label("En cours : " + Describe(state), DeviceUi.Text);
            string status = RadioSound.Status(_radio);
            if (!string.IsNullOrEmpty(status))
                GUILayout.Label(status, DeviceUi.Muted);
            GUILayout.Space(10f);

            GUILayout.Label("LIEN YOUTUBE", DeviceUi.Muted);
            GUILayout.BeginHorizontal();
            _link = GUILayout.TextField(_link ?? "", 300, DeviceUi.Field, GUILayout.Height(34f), GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Coller", DeviceUi.Button, GUILayout.Width(80f), GUILayout.Height(34f)))
                _link = GUIUtility.systemCopyBuffer ?? "";
            if (GUILayout.Button("Lire", DeviceUi.Button, GUILayout.Width(70f), GUILayout.Height(34f)))
            {
                string id = YouTubeAudio.VideoId(_link);
                if (id == null)
                {
                    _error = "Ce n'est pas un lien YouTube (youtube.com/watch?v=..., youtu.be/..., shorts/...).";
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

            GUILayout.Label("MUSIQUES DU JEU", DeviceUi.Muted);
            List<string> tracks = Radio.GameTracks();
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(220f));
            if (tracks.Count == 0)
                GUILayout.Label("Aucune musique trouvée dans les banques de son du jeu.", DeviceUi.Muted);
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
            if (GUILayout.Button("Éteindre", DeviceUi.Button, GUILayout.Height(34f)))
                Radio.Request(_radio, "");
            if (GUILayout.Button("Fermer (Échap)", DeviceUi.Button, GUILayout.Height(34f)))
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
                return "éteinte";
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
                case YouTubeAudio.State.Failed: return "Échec : " + playing.Job.Error;
                default: return playing.HasChannel ? "Lecture (en boucle)" : null;
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
                playing.Job.Error = "le son n'a pas pu être ouvert (" + result + ")";
                playing.Job.Status = YouTubeAudio.State.Failed;
                return;
            }
            core.getMasterChannelGroup(out FMOD.ChannelGroup master);
            result = core.playSound(sound, master, true, out FMOD.Channel channel);
            if (result != FMOD.RESULT.OK)
            {
                sound.release();
                playing.Job.Error = "le son n'a pas pu démarrer (" + result + ")";
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

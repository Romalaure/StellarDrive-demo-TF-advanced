using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
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

        // Host: a radio's queue and when its track started (Time.realtimeSinceStartup, negative while the host gets it)
        private sealed class Station
        {
            public readonly RadioState State = new RadioState();
            public float StartedAt = -1f;
            public YouTubeAudio.Job Job;
        }

        private static readonly Dictionary<PartKey, Station> Stations = new Dictionary<PartKey, Station>();
        /// <summary>Client: what each radio that plays plays, as the host last said.</summary>
        internal static readonly Dictionary<PartKey, RadioState> ClientStates = new Dictionary<PartKey, RadioState>();

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
            TFNet.OnClient(TFMessageKind.Radio, (kind, ship, part, text) =>
            {
                var key = new PartKey(ship, part);
                RadioState state = RadioState.Read(text, Time.realtimeSinceStartup);
                if (state == null || state.Queue.Count == 0)
                    ClientStates.Remove(key);
                else
                    ClientStates[key] = state;
            });
            TFNet.OnClient(TFMessageKind.RadioRefused, (kind, ship, part, text) => RadioMenu.Refused());
            TFNet.SyncRequested += connection =>
            {
                foreach (KeyValuePair<PartKey, Station> radio in Stations.ToList())
                    TFNet.SendTo(connection, TFMessageKind.Radio, radio.Key.ShipId, radio.Key.PartId, Message(radio.Value));
            };
            TFNet.Disconnected += () =>
            {
                Stations.Clear();
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

        /// <summary>
        /// Asks the host to change what this radio plays: "play\n&lt;item&gt;", "add\n&lt;item&gt;", "next", "prev",
        /// "jump\n&lt;i&gt;", "remove\n&lt;i&gt;", "mode\n&lt;0-2&gt;", "vol\n&lt;0-100&gt;", "seek\n&lt;ms&gt;", "off"
        /// (an item alone plays it, "" turns off). Items: "yt|videoId" or "game|event:/Music/...".
        /// </summary>
        public static void Request(PartKey radio, string command) =>
            TFNet.SendToServer(TFMessageKind.Radio, radio.ShipId, radio.PartId, command);

        // The host keeps only well-formed items for radios that exist, so a client cannot make
        // the others fetch anything but a YouTube video id or play anything but a music event
        private static void OnServerRadio(NetworkConnection sender, TFMessageKind kind, uint shipId, ushort partId, string text)
        {
            if (text == null || text.Length > 300)
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
            if (!Apply(key, text))
                return;
            if (Stations.TryGetValue(key, out Station station))
                Broadcast(key, station);
            else
                TFNet.SendToAll(TFMessageKind.Radio, shipId, partId, "");
        }

        private static bool Apply(PartKey key, string text)
        {
            string[] words = text.Split(new[] { '\n' }, 2);
            string verb = words[0], arg = words.Length > 1 ? words[1] : "";
            Stations.TryGetValue(key, out Station station);
            RadioState state = station?.State;
            int count = state?.Queue.Count ?? 0;
            bool hasNumber = int.TryParse(arg, out int number);
            switch (verb)
            {
                case "":
                case "off":
                    Stations.Remove(key);
                    return true;
                case "play":
                    if (!IsValidItem(arg))
                        return false;
                    var fresh = new Station();
                    if (state != null)
                    {
                        fresh.State.Mode = state.Mode;
                        fresh.State.Volume = state.Volume;
                    }
                    fresh.State.Queue.Add(arg);
                    fresh.State.Titles.Add(TitleOf(arg));
                    Stations[key] = fresh;
                    StartTrack(fresh);
                    return true;
                case "add":
                    if (station == null)
                        return Apply(key, "play\n" + arg);
                    if (!IsValidItem(arg) || count >= RadioState.MaxQueue)
                        return false;
                    state.Queue.Add(arg);
                    state.Titles.Add(TitleOf(arg));
                    return true;
                case "next":
                case "prev":
                    if (station == null)
                        return false;
                    Skip(station, verb == "next" ? 1 : -1);
                    return true;
                case "jump":
                    if (station == null || !hasNumber || number < 0 || number >= count)
                        return false;
                    state.Index = number;
                    StartTrack(station);
                    return true;
                case "remove":
                    if (station == null || !hasNumber || number < 0 || number >= count)
                        return false;
                    state.Queue.RemoveAt(number);
                    state.Titles.RemoveAt(number);
                    if (state.Queue.Count == 0)
                    {
                        Stations.Remove(key);
                        return true;
                    }
                    if (number < state.Index)
                    {
                        state.Index--;
                    }
                    else if (number == state.Index)
                    {
                        if (state.Index >= state.Queue.Count)
                            state.Index = 0;
                        StartTrack(station);
                    }
                    return true;
                case "mode":
                    if (station == null || !hasNumber || number < 0 || number > 2)
                        return false;
                    state.Mode = (RadioMode)number;
                    return true;
                case "vol":
                    if (station == null || !hasNumber || number < 0 || number > 100)
                        return false;
                    state.Volume = number / 100f;
                    return true;
                case "seek":
                    if (station == null || station.StartedAt < 0f || !hasNumber || number < 0)
                        return false;
                    float at = number / 1000f;
                    if (state.Duration > 0f)
                        at = Mathf.Min(at, state.Duration - 0.5f);
                    station.StartedAt = Time.realtimeSinceStartup - Mathf.Max(0f, at);
                    return true;
                default:
                    // A bare item: what the development checks send
                    return words.Length == 1 && IsValidItem(verb) && Apply(key, "play\n" + verb);
            }
        }

        private static void Skip(Station station, int step)
        {
            RadioState state = station.State;
            int count = state.Queue.Count;
            if (state.Mode == RadioMode.Shuffle && count > 1)
                state.Index = (state.Index + UnityEngine.Random.Range(1, count)) % count;
            else
                state.Index = ((state.Index + step) % count + count) % count;
            StartTrack(station);
        }

        private static void StartTrack(Station station)
        {
            RadioState state = station.State;
            string track = state.Track;
            state.Duration = 0f;
            station.Job = null;
            if (track.StartsWith("yt|"))
            {
                // The clock starts once the host has the sound, so everyone starts together
                station.StartedAt = -1f;
                station.Job = YouTubeAudio.Request(track.Substring(3));
            }
            else
            {
                station.StartedAt = Time.realtimeSinceStartup;
                state.Duration = GameTrackSeconds(track.Substring(5));
            }
        }

        private static string TitleOf(string item) =>
            item.StartsWith("game|") ? TrackName(item.Substring(5)) : RadioTitles.Get(item.Substring(3)) ?? "";

        private static string Message(Station station)
        {
            station.State.Elapsed = station.StartedAt < 0f ? -1f : Time.realtimeSinceStartup - station.StartedAt;
            return station.State.Write();
        }

        private static void Broadcast(PartKey key, Station station) =>
            TFNet.SendToAll(TFMessageKind.Radio, key.ShipId, key.PartId, Message(station));

        private static float _nextServerUpdate;

        /// <summary>Host: starts tracks once their sound is here, fills in titles, moves on to the next track.</summary>
        public static void ServerUpdate()
        {
            if (Stations.Count == 0 || Time.unscaledTime < _nextServerUpdate || !TFNet.IsServer)
                return;
            _nextServerUpdate = Time.unscaledTime + 0.25f;
            float now = Time.realtimeSinceStartup;
            foreach (KeyValuePair<PartKey, Station> entry in Stations.ToList())
            {
                Station station = entry.Value;
                RadioState state = station.State;
                bool changed = false;
                for (int i = 0; i < state.Queue.Count; i++)
                {
                    if (state.Titles[i].Length > 0 || !state.Queue[i].StartsWith("yt|"))
                        continue;
                    string title = RadioTitles.Get(state.Queue[i].Substring(3));
                    if (!string.IsNullOrEmpty(title))
                    {
                        state.Titles[i] = title;
                        changed = true;
                    }
                }
                if (station.StartedAt < 0f && station.Job != null && station.Job.Status != YouTubeAudio.State.Working)
                {
                    station.StartedAt = now;
                    state.Duration = station.Job.Duration;
                    changed = true;
                }
                // A little early: the next track's message reaches the players before their sound loops back
                if (station.StartedAt >= 0f && state.Duration > 1f && state.Mode != RadioMode.RepeatOne && now - station.StartedAt >= state.Duration - 0.25f)
                {
                    Skip(station, 1);
                    changed = true;
                }
                if (changed)
                    Broadcast(entry.Key, station);
            }
        }

        private static float GameTrackSeconds(string path)
        {
            try
            {
                if (RuntimeManager.StudioSystem.getEvent(path, out EventDescription description) == FMOD.RESULT.OK
                    && description.getLength(out int length) == FMOD.RESULT.OK)
                    return length / 1000f;
            }
            catch (Exception e)
            {
                TFMod.Log.Warning("radio: length of " + path + ": " + e.Message);
            }
            return 0f;
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
        public static bool IsMuted(PartKey radio)
        {
            // Asked every frame for each radio: the list is parsed again only when it changes
            string text = Settings.RadioMuted.Value;
            if (text != _mutedText)
            {
                _mutedText = text;
                _muted = Names(text);
            }
            return _muted.Count > 0 && _muted.Contains(radio.ToString());
        }

        private static string _mutedText;
        private static HashSet<string> _muted;

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

        public static bool IsValidItem(string text)
        {
            if (text == null || text.Length == 0 || text.Length > 200)
                return false;
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

    /// <summary>The radio on screen: its equalizer bars follow the sound's spectrum while it plays (heard here), and rest low otherwise.</summary>
    internal sealed class RadioVisuals : UsablePart
    {
        private readonly List<Transform> _bars = new List<Transform>();
        private float _next;
        private float[] _heights, _bands;

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
            _bands = new float[_bars.Count];
        }

        private void LateUpdate()
        {
            if (!HasContext || _bars.Count == 0 || Time.unscaledTime < _next)
                return;
            _next = Time.unscaledTime + 0.05f;
            Camera camera = MainCamera.Get();
            if (camera != null && (camera.transform.position - transform.position).sqrMagnitude > 30f * 30f)
                return;
            bool playing = Radio.ClientStates.ContainsKey(Key) && !Radio.IsMuted(Key) && RadioSound.Spectrum(Key, _bands);
            float t = Time.time;
            for (int i = 0; i < _bars.Count; i++)
            {
                float target = playing ? _bands[i] : 0.08f + 0.04f * Mathf.Sin(t * 1.5f + i * 0.5f);
                // Quick up, slower down, like a level meter
                _heights[i] = Mathf.Lerp(_heights[i], target, target > _heights[i] ? 0.7f : 0.3f);
                Vector3 scale = _bars[i].localScale;
                scale.y = Radio.ScreenHeight * Mathf.Max(0.05f, _heights[i]);
                _bars[i].localScale = scale;
            }
        }
    }

    /// <summary>A speaker: heard like its ship's radios; using it opens the radio that plays (or any radio of the ship).</summary>
    internal class SpeakerVisuals : UsablePart
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
                if (Radio.ClientStates.ContainsKey(visuals.Key))
                    break;
            }
            if (radio.HasValue)
                RadioMenu.Show(radio.Value);
            else
                RadioMenu.Notice(Loc.Pick("Aucune radio dans ce vaisseau : pose une radio, l'enceinte jouera sa musique.", "No radio in this ship: place a radio and the speaker will play its music.", "No hay radio en esta nave: coloca una radio y el altavoz reproducirá su música."));
        }
    }

    /// <summary>The radio's window: what plays and how far, its queue, repeat modes and volume, a YouTube link, the game's music.</summary>
    internal static class RadioMenu
    {
        private static bool _open;
        private static PartKey _radio;
        private static string _link = "";
        private static string _error;
        private static Vector2 _body, _queue, _scroll, _players;
        private static float _volume = -1f, _volumeSent = -1f, _volumeTouched = -10f, _volumeSentAt;

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
            _volumeSent = -1f;
            _volumeTouched = -10f;
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

            Radio.ClientStates.TryGetValue(_radio, out RadioState state);
            bool host = TFNet.IsServer;
            List<string> others = host ? OtherPlayers() : new List<string>();
            Rect area = DeviceUi.Window(width, height, 660f, 930f + (others.Count > 0 ? 50f + Mathf.Min(others.Count, 4) * 36f : 0f), "RADIO");
            GUILayout.BeginArea(area);
            // Room for the scroll bar, when the window is taller than the screen
            float inner = area.width - 20f;
            _body = GUILayout.BeginScrollView(_body, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.ExpandHeight(true));

            GUILayout.Label(Loc.Pick("En cours : ", "Now playing: ", "Sonando: ") + (state == null ? Loc.Pick("éteinte", "off", "apagada") : Describe(state.Track, state.Title)), DeviceUi.Text);
            string status = RadioSound.Status(_radio);
            if (!string.IsNullOrEmpty(status))
                GUILayout.Label(status, DeviceUi.Muted);
            if (state != null)
                DrawProgress(state);
            GUILayout.Space(6f);
            DrawControls(state);
            GUILayout.Space(10f);

            GUILayout.Label(Loc.Pick("LIEN YOUTUBE", "YOUTUBE LINK", "ENLACE DE YOUTUBE"), DeviceUi.Muted);
            GUILayout.BeginHorizontal();
            // Fixed width: a field sized to its text pushes the buttons out of the window on a long link
            _link = GUILayout.TextField(_link ?? "", 300, DeviceUi.Field, GUILayout.Height(34f), GUILayout.Width(inner - 262f));
            if (GUILayout.Button(Loc.Pick("Coller", "Paste", "Pegar"), DeviceUi.Button, GUILayout.Width(76f), GUILayout.Height(34f)))
                _link = GUIUtility.systemCopyBuffer ?? "";
            if (GUILayout.Button(Loc.Pick("Lire", "Play", "Reproducir"), DeviceUi.Button, GUILayout.Width(76f), GUILayout.Height(34f)))
                SendLink("play");
            if (GUILayout.Button(Loc.Pick("+ File", "+ Queue", "+ Cola"), DeviceUi.Button, GUILayout.Width(86f), GUILayout.Height(34f)))
                SendLink("add");
            GUILayout.EndHorizontal();
            if (_error != null)
                GUILayout.Label(_error, DeviceUi.Muted);
            GUILayout.Space(8f);

            DrawQueue(state);
            GUILayout.Space(8f);

            GUILayout.Label(Loc.Pick("MUSIQUES DU JEU", "GAME MUSIC", "MÚSICA DEL JUEGO"), DeviceUi.Muted);
            List<string> tracks = Radio.GameTracks();
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(150f));
            if (tracks.Count == 0)
                GUILayout.Label(Loc.Pick("Aucune musique trouvée dans les banques de son du jeu.", "No music found in the game's sound banks.", "No se encontró música en los bancos de sonido del juego."), DeviceUi.Muted);
            foreach (string track in tracks)
            {
                string item = "game|" + track;
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(Radio.TrackName(track), state != null && item == state.Track ? DeviceUi.Selected : DeviceUi.Button, GUILayout.Height(32f)))
                    Radio.Request(_radio, "play\n" + item);
                if (GUILayout.Button("+", DeviceUi.Button, GUILayout.Width(40f), GUILayout.Height(32f)))
                    Add(state, item);
                GUILayout.EndHorizontal();
                GUILayout.Space(3f);
            }
            GUILayout.EndScrollView();
            GUILayout.Space(8f);

            GUILayout.BeginHorizontal();
            GUILayout.Label(Loc.Pick("Mon volume", "My volume", "Mi volumen"), DeviceUi.Text, GUILayout.Width(150f));
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
            GUILayout.EndScrollView();
            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Loc.Pick("Éteindre", "Turn off", "Apagar"), DeviceUi.Button, GUILayout.Height(34f)))
                Radio.Request(_radio, "off");
            if (GUILayout.Button(Loc.Pick("Fermer (Échap)", "Close (Esc)", "Cerrar (Esc)"), DeviceUi.Button, GUILayout.Height(34f)))
            {
                MelonLoader.MelonPreferences.Save();
                ModMenu.Close();
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            GUI.matrix = previous;
        }

        // Elapsed / length, a bar to click to go elsewhere in the track
        private static void DrawProgress(RadioState state)
        {
            float duration = state.Duration, at = state.Position;
            GUILayout.BeginHorizontal();
            GUILayout.Label(RadioState.Clock(at), DeviceUi.Muted, GUILayout.Width(60f));
            Rect bar = GUILayoutUtility.GetRect(100f, 22f, GUILayout.ExpandWidth(true), GUILayout.Height(22f));
            GUILayout.Label(duration > 0f ? RadioState.Clock(duration) : "--:--", DeviceUi.Muted, GUILayout.Width(60f));
            GUILayout.EndHorizontal();
            var track = new Rect(bar.x, bar.y + 7f, bar.width, 8f);
            DeviceUi.Fill(track, new Color(1f, 1f, 1f, 0.12f));
            if (duration > 0f)
                DeviceUi.Fill(new Rect(track.x, track.y, track.width * Mathf.Clamp01(at / duration), track.height), DeviceUi.Accent);
            Event e = Event.current;
            if (duration > 0f && state.Started && e.type == EventType.MouseDown && e.button == 0 && bar.Contains(e.mousePosition))
            {
                float fraction = Mathf.Clamp01((e.mousePosition.x - bar.x) / bar.width);
                Radio.Request(_radio, "seek\n" + Mathf.RoundToInt(fraction * duration * 1000f));
                e.Use();
            }
        }

        private static readonly string[] Modes =
        {
            Loc.Pick("Répéter le morceau", "Repeat track", "Repetir canción"),
            Loc.Pick("Répéter la liste", "Repeat list", "Repetir lista"),
            Loc.Pick("Aléatoire", "Shuffle", "Aleatorio")
        };

        // Previous / next, the repeat mode and the radio's volume for everyone
        private static void DrawControls(RadioState state)
        {
            GUI.enabled = state != null;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Loc.Pick("« Précédent", "« Previous", "« Anterior"), DeviceUi.Button, GUILayout.Height(32f)))
                Radio.Request(_radio, "prev");
            if (GUILayout.Button(Loc.Pick("Suivant »", "Next »", "Siguiente »"), DeviceUi.Button, GUILayout.Height(32f)))
                Radio.Request(_radio, "next");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            for (int i = 0; i < Modes.Length; i++)
            {
                if (GUILayout.Button(Modes[i], state != null && (int)state.Mode == i ? DeviceUi.Selected : DeviceUi.Button, GUILayout.Height(30f)))
                    Radio.Request(_radio, "mode\n" + i);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(Loc.Pick("Volume radio (tous)", "Radio volume (all)", "Volumen radio (todos)"), DeviceUi.Text, GUILayout.Width(150f));
            float now = Time.unscaledTime;
            // While the slider moves, it shows what was chosen rather than what the host last said
            float shown = now - _volumeTouched < 1f ? _volume : state?.Volume ?? 1f;
            float chosen = GUILayout.HorizontalSlider(shown, 0f, 1f, GUILayout.Height(20f));
            if (!Mathf.Approximately(chosen, shown))
            {
                _volume = chosen;
                _volumeTouched = now;
            }
            if (state != null && now - _volumeTouched < 1f && now - _volumeSentAt > 0.15f && Mathf.Abs(_volume - _volumeSent) > 0.005f)
            {
                Radio.Request(_radio, "vol\n" + Mathf.RoundToInt(_volume * 100f));
                _volumeSent = _volume;
                _volumeSentAt = now;
            }
            GUILayout.Label(Mathf.RoundToInt(shown * 100f) + "%", DeviceUi.Muted, GUILayout.Width(44f));
            GUILayout.EndHorizontal();
            GUI.enabled = true;
        }

        private static void DrawQueue(RadioState state)
        {
            int count = state?.Queue.Count ?? 0;
            GUILayout.Label(Loc.Pick("FILE D'ATTENTE", "QUEUE", "COLA") + $" ({count}/{RadioState.MaxQueue})", DeviceUi.Muted);
            _queue = GUILayout.BeginScrollView(_queue, GUILayout.Height(150f));
            if (count == 0)
                GUILayout.Label(Loc.Pick("Vide : « + File » ajoute un lien, « + » une musique du jeu.", "Empty: \"+ Queue\" adds a link, \"+\" a game track.", "Vacía: «+ Cola» añade un enlace, «+» una música del juego."), DeviceUi.Muted);
            int remove = -1;
            for (int i = 0; i < count; i++)
            {
                GUILayout.BeginHorizontal();
                string name = (i + 1) + ". " + Describe(state.Queue[i], state.Titles[i]);
                if (GUILayout.Button(name, i == state.Index ? DeviceUi.Selected : DeviceUi.Button, GUILayout.Height(32f)))
                    Radio.Request(_radio, "jump\n" + i);
                if (GUILayout.Button("x", DeviceUi.Button, GUILayout.Width(40f), GUILayout.Height(32f)))
                    remove = i;
                GUILayout.EndHorizontal();
                GUILayout.Space(3f);
            }
            GUILayout.EndScrollView();
            if (remove >= 0)
                Radio.Request(_radio, "remove\n" + remove);
        }

        private static void SendLink(string verb)
        {
            string id = YouTubeAudio.VideoId(_link);
            if (id == null)
            {
                _error = Loc.Pick("Ce n'est pas un lien YouTube (youtube.com/watch?v=..., youtu.be/..., shorts/...).", "This is not a YouTube link (youtube.com/watch?v=..., youtu.be/..., shorts/...).", "No es un enlace de YouTube (youtube.com/watch?v=..., youtu.be/..., shorts/...).");
                return;
            }
            Radio.ClientStates.TryGetValue(_radio, out RadioState state);
            if (verb == "add" && !Add(state, "yt|" + id))
                return;
            if (verb == "play")
                Radio.Request(_radio, "play\nyt|" + id);
            _error = null;
            _link = "";
        }

        private static bool Add(RadioState state, string item)
        {
            if (state != null && state.Queue.Count >= RadioState.MaxQueue)
            {
                _error = Loc.Pick($"La file est pleine ({RadioState.MaxQueue} morceaux).", $"The queue is full ({RadioState.MaxQueue} tracks).", $"La cola está llena ({RadioState.MaxQueue} canciones).");
                return false;
            }
            Radio.Request(_radio, "add\n" + item);
            return true;
        }

        public static string Describe(string item, string title)
        {
            if (!string.IsNullOrEmpty(title))
                return title;
            if (item.StartsWith("game|"))
                return Radio.TrackName(item.Substring(5));
            if (item.StartsWith("yt|"))
                return "YouTube (" + item.Substring(3) + ")";
            return item;
        }
    }

    /// <summary>
    /// Plays radios on this client through FMOD: a Studio event for the game's music, a streamed
    /// WAV for YouTube, both at the point of the track the host says (players hear the same part).
    /// Volume follows the distance between the camera and the radio; a radio out of sight range
    /// (not loaded) is silent. An FFT on each one feeds the radio's equalizer bars.
    /// </summary>
    internal static class RadioSound
    {
        private const float FullVolumeDistance = 2f;
        private const float SilentDistance = 20f;
        // Further off than this, the sound is moved to the host's point (closer, message delays would make it jump)
        private const uint DriftMs = 1500;

        private sealed class Playing
        {
            public string Track;
            public RadioState Applied;
            public EventInstance Event;
            public bool HasEvent;
            public FMOD.Sound Sound;
            public FMOD.Channel Channel;
            public bool HasChannel;
            public YouTubeAudio.Job Job;
            public FMOD.DSP Fft;
            public FMOD.ChannelGroup FftGroup;
            public bool HasFft, FftOnGroup;
        }

        private static readonly Dictionary<PartKey, Playing> Radios = new Dictionary<PartKey, Playing>();
        private static readonly List<PartKey> Gone = new List<PartKey>();

        public static string Status(PartKey radio)
        {
            if (!Radios.TryGetValue(radio, out Playing playing) || playing.Job == null)
                return null;
            switch (playing.Job.Status)
            {
                case YouTubeAudio.State.Working: return playing.Job.Step + "...";
                case YouTubeAudio.State.Failed: return Loc.Pick("Échec : ", "Failed: ", "Error: ") + playing.Job.Error;
                default:
                    return playing.HasChannel ? null
                        : !playing.Applied.Started ? Loc.Pick("En attente de l'hôte...", "Waiting for the host...", "Esperando al anfitrión...")
                        : null;
            }
        }

        public static void Update()
        {
            if (!RuntimeManager.IsInitialized)
                return;
            Camera camera = MainCamera.Get();
            // Start, change or stop radios to match what the host says they play
            foreach (KeyValuePair<PartKey, RadioState> entry in Radio.ClientStates)
            {
                Radios.TryGetValue(entry.Key, out Playing playing);
                if (playing != null && playing.Track == entry.Value.Track)
                {
                    if (playing.Applied != entry.Value)
                    {
                        playing.Applied = entry.Value;
                        Sync(playing, false);
                    }
                    continue;
                }
                if (playing != null)
                    Stop(playing);
                Radios[entry.Key] = Start(entry.Value);
            }
            Gone.Clear();
            foreach (PartKey key in Radios.Keys)
            {
                if (!Radio.ClientStates.ContainsKey(key))
                    Gone.Add(key);
            }
            foreach (PartKey key in Gone)
            {
                Stop(Radios[key]);
                Radios.Remove(key);
            }

            float personal = Mathf.Clamp01(Settings.RadioVolume.Value);
            foreach (KeyValuePair<PartKey, Playing> entry in Radios)
            {
                float volume = 0f, pan = 0f;
                if (camera != null && !Radio.IsMuted(entry.Key))
                {
                    Mix(entry.Key, camera.transform.position, camera.transform.right, out volume, out pan);
                    volume *= personal * entry.Value.Applied.Volume;
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
            List<UsablePart> all = UsablePart.All;
            for (int i = 0; i < all.Count; i++)
            {
                UsablePart source = all[i];
                if (!IsSource(source, key))
                    continue;
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

        private static Playing Start(RadioState state)
        {
            var playing = new Playing { Track = state.Track, Applied = state };
            if (state.Track.StartsWith("game|"))
            {
                try
                {
                    playing.Event = RuntimeManager.CreateInstance(state.Track.Substring(5));
                    playing.Event.setVolume(0f);
                    playing.Event.start();
                    playing.HasEvent = true;
                    Sync(playing, true);
                }
                catch (Exception e)
                {
                    TFMod.Log.Warning("radio: could not play " + state.Track + ": " + e.Message);
                }
            }
            else if (state.Track.StartsWith("yt|"))
            {
                playing.Job = YouTubeAudio.Request(state.Track.Substring(3));
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
                {
                    group.setPan(pan);
                    if (!playing.HasFft)
                        AttachFft(playing, group);
                }
                // Music events end on their own; a radio keeps playing
                if (playing.Event.getPlaybackState(out PLAYBACK_STATE state) == FMOD.RESULT.OK && state == PLAYBACK_STATE.STOPPED)
                {
                    // (its channel group goes with the stop: the FFT is put on the new one)
                    DetachFft(playing);
                    playing.Event.start();
                }
            }
            // The sound waits for the host's clock, so it starts at the same point for everyone
            if (!playing.HasChannel && playing.Applied.Started && playing.Job != null && playing.Job.Status == YouTubeAudio.State.Ready && playing.Job.WavPath != null)
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

        // Moves the sound to where the host says the track is (always when forced, else only when far off)
        private static void Sync(Playing playing, bool force)
        {
            RadioState state = playing.Applied;
            if (!state.Started)
                return;
            uint want = (uint)Mathf.RoundToInt(state.Position * 1000f);
            if (playing.HasChannel
                && playing.Sound.getLength(out uint length, FMOD.TIMEUNIT.MS) == FMOD.RESULT.OK && length > 0
                && playing.Channel.getPosition(out uint position, FMOD.TIMEUNIT.MS) == FMOD.RESULT.OK)
            {
                want %= length;
                if (force || Apart(position, want, length) > DriftMs)
                    playing.Channel.setPosition(want, FMOD.TIMEUNIT.MS);
            }
            if (playing.HasEvent
                && playing.Event.getDescription(out EventDescription description) == FMOD.RESULT.OK
                && description.getLength(out int eventLength) == FMOD.RESULT.OK && eventLength > 0
                && playing.Event.getTimelinePosition(out int timeline) == FMOD.RESULT.OK)
            {
                want %= (uint)eventLength;
                if (force || Apart((uint)Mathf.Max(0, timeline), want, (uint)eventLength) > DriftMs)
                    playing.Event.setTimelinePosition((int)want);
            }
        }

        // Distance between two points of a looping track
        private static uint Apart(uint a, uint b, uint length)
        {
            uint d = a > b ? a - b : b - a;
            return Math.Min(d, length - Math.Min(d, length));
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
            playing.Sound = sound;
            playing.Channel = channel;
            playing.HasChannel = true;
            Sync(playing, true);
            AttachFft(playing, channel);
            channel.setPaused(false);
        }

        // Before the volume (the input end of the chain), so the bars move however far the listener is
        private static void AttachFft(Playing playing, FMOD.Channel channel)
        {
            if (!NewFft(out FMOD.DSP dsp))
                return;
            if (channel.addDSP(FMOD.CHANNELCONTROL_DSP_INDEX.TAIL, dsp) == FMOD.RESULT.OK)
                Attached(playing, dsp, false);
            else
                dsp.release();
        }

        private static void AttachFft(Playing playing, FMOD.ChannelGroup group)
        {
            if (!NewFft(out FMOD.DSP dsp))
                return;
            if (group.addDSP(FMOD.CHANNELCONTROL_DSP_INDEX.TAIL, dsp) == FMOD.RESULT.OK)
            {
                Attached(playing, dsp, true);
                playing.FftGroup = group;
            }
            else
            {
                dsp.release();
            }
        }

        private static bool NewFft(out FMOD.DSP dsp)
        {
            if (RuntimeManager.CoreSystem.createDSPByType(FMOD.DSP_TYPE.FFT, out dsp) != FMOD.RESULT.OK)
                return false;
            dsp.setParameterInt((int)FMOD.DSP_FFT.WINDOWSIZE, FftWindow);
            return true;
        }

        private static void Attached(Playing playing, FMOD.DSP dsp, bool onGroup)
        {
            playing.Fft = dsp;
            playing.FftOnGroup = onGroup;
            playing.HasFft = true;
        }

        private const int FftWindow = 1024;
        private static float[] _bins = new float[FftWindow / 2];

        /// <summary>The level of each band (0-1, low to high pitch) of what a radio plays, or false when it is not sounding.</summary>
        public static bool Spectrum(PartKey key, float[] bands)
        {
            if (!Radios.TryGetValue(key, out Playing playing) || !playing.HasFft
                || playing.Fft.getParameterData((int)FMOD.DSP_FFT.SPECTRUMDATA, out IntPtr data, out uint _) != FMOD.RESULT.OK || data == IntPtr.Zero)
                return false;
            var fft = (FMOD.DSP_PARAMETER_FFT)Marshal.PtrToStructure(data, typeof(FMOD.DSP_PARAMETER_FFT));
            if (fft.numchannels <= 0 || fft.length < 16)
                return false;
            if (_bins.Length != fft.length)
                _bins = new float[fft.length];
            fft.getSpectrum(0, ref _bins);
            // Bands spaced by octaves, up to about a quarter of the sample rate (~12 kHz)
            float top = fft.length / 2f;
            bool sounding = false;
            for (int i = 0; i < bands.Length; i++)
            {
                int from = Mathf.Max(1, Mathf.FloorToInt(Mathf.Pow(top, i / (float)bands.Length)));
                int to = Mathf.Max(from + 1, Mathf.FloorToInt(Mathf.Pow(top, (i + 1) / (float)bands.Length)));
                float peak = 0f;
                for (int b = from; b < to && b < _bins.Length; b++)
                    peak = Mathf.Max(peak, _bins[b]);
                // -60 dB to 0 dB, the high bands lifted a little (music has less there)
                bands[i] = Mathf.Clamp01(1f + Mathf.Log10(peak + 1e-6f) / 3f + i * 0.02f);
                sounding |= peak > 1e-4f;
            }
            return sounding;
        }

        private static void DetachFft(Playing playing)
        {
            if (!playing.HasFft)
                return;
            if (playing.FftOnGroup)
                playing.FftGroup.removeDSP(playing.Fft);
            else if (playing.HasChannel)
                playing.Channel.removeDSP(playing.Fft);
            playing.Fft.release();
            playing.HasFft = false;
        }

        private static void Stop(Playing playing)
        {
            try
            {
                DetachFft(playing);
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

        private static bool IsSource(UsablePart part, PartKey key) =>
            part != null && part.HasContext && (part is RadioVisuals && part.Key.Equals(key) || part is SpeakerVisuals && part.Key.ShipId == key.ShipId);

        // The radio itself and the speakers of its ship
        public static IEnumerable<UsablePart> Sources(PartKey key) => UsablePart.All.Where(part => IsSource(part, key));
    }

    /// <summary>The server rack (a piece of furniture): a radio relay, heard like the ship's speakers.</summary>
    internal sealed class RelayVisuals : SpeakerVisuals
    {
        public override string Label => Loc.Pick("Relais radio (ouvrir la radio)", "Radio relay (open the radio)", "Repetidor de radio (abrir la radio)");
    }
}

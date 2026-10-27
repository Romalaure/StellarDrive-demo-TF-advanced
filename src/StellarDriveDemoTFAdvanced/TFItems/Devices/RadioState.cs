using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.TFItems.Devices
{
    internal enum RadioMode { RepeatOne, RepeatAll, Shuffle }

    /// <summary>
    /// What a radio plays, as the host sends it: its queue (YouTube videos and game music), the
    /// track playing, how far into it, its volume and repeat mode.
    /// </summary>
    internal sealed class RadioState
    {
        public const int MaxQueue = 30;
        private const string Magic = "r2";

        public readonly List<string> Queue = new List<string>();
        public readonly List<string> Titles = new List<string>();
        public int Index;
        public RadioMode Mode;
        public float Volume = 1f;
        /// <summary>Seconds into the track when sent; negative while the host is still getting it.</summary>
        public float Elapsed = -1f;
        /// <summary>Seconds; 0 when unknown.</summary>
        public float Duration;
        /// <summary>Client: when the track started, in Time.realtimeSinceStartup; negative while waiting for the host.</summary>
        public float StartedAt = -1f;

        public string Track => Index >= 0 && Index < Queue.Count ? Queue[Index] : "";
        public string Title => Index >= 0 && Index < Titles.Count ? Titles[Index] : "";
        public bool Started => StartedAt >= 0f;

        /// <summary>Client: seconds into the track now (looped on its length when known).</summary>
        public float Position
        {
            get
            {
                if (!Started)
                    return 0f;
                float at = Time.realtimeSinceStartup - StartedAt;
                return Duration > 0f ? at % Duration : at;
            }
        }

        public string Write()
        {
            var text = new System.Text.StringBuilder(Magic).Append('\n')
                .Append((int)Mode).Append('|').Append(Index).Append('|').Append(Mathf.RoundToInt(Volume * 100f)).Append('|')
                .Append(Elapsed < 0f ? -1 : Mathf.RoundToInt(Elapsed * 1000f)).Append('|').Append(Mathf.RoundToInt(Duration * 1000f));
            for (int i = 0; i < Queue.Count; i++)
                text.Append('\n').Append(Queue[i]).Append('\t').Append(Clean(i < Titles.Count ? Titles[i] : ""));
            return text.ToString();
        }

        /// <summary>The state in a message from the host, received at <paramref name="now"/> (Time.realtimeSinceStartup), or null.</summary>
        public static RadioState Read(string text, float now)
        {
            if (string.IsNullOrEmpty(text) || !text.StartsWith(Magic + "\n"))
                return null;
            string[] lines = text.Split('\n');
            string[] head = lines[1].Split('|');
            if (head.Length < 5 || !int.TryParse(head[0], out int mode) || !int.TryParse(head[1], out int index)
                || !int.TryParse(head[2], out int volume) || !int.TryParse(head[3], out int elapsed) || !int.TryParse(head[4], out int duration))
                return null;
            var state = new RadioState
            {
                Mode = (RadioMode)Mathf.Clamp(mode, 0, 2),
                Index = index,
                Volume = Mathf.Clamp01(volume / 100f),
                Elapsed = elapsed < 0 ? -1f : elapsed / 1000f,
                Duration = Mathf.Max(0f, duration / 1000f)
            };
            for (int i = 2; i < lines.Length && state.Queue.Count < MaxQueue; i++)
            {
                int tab = lines[i].IndexOf('\t');
                string item = tab < 0 ? lines[i] : lines[i].Substring(0, tab);
                if (!Radio.IsValidItem(item))
                    continue;
                state.Queue.Add(item);
                state.Titles.Add(tab < 0 ? "" : lines[i].Substring(tab + 1));
            }
            if (state.Elapsed >= 0f)
                state.StartedAt = now - state.Elapsed;
            return state;
        }

        public static string Clean(string title)
        {
            if (string.IsNullOrEmpty(title))
                return "";
            title = title.Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ').Trim();
            return title.Length > 90 ? title.Substring(0, 90) : title;
        }

        public static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return s >= 3600 ? $"{s / 3600}:{s / 60 % 60:00}:{s % 60:00}" : $"{s / 60}:{s % 60:00}";
        }
    }

    /// <summary>Host: YouTube video titles, asked once each from YouTube's oEmbed page on a background thread.</summary>
    internal static class RadioTitles
    {
        private static readonly Dictionary<string, string> Known = new Dictionary<string, string>();

        /// <summary>The title of a video: null while it is being asked, empty when YouTube gave none.</summary>
        public static string Get(string videoId)
        {
            lock (Known)
            {
                if (Known.TryGetValue(videoId, out string title))
                    return title;
                Known[videoId] = null;
            }
            new Thread(() => Fetch(videoId)) { IsBackground = true, Name = "TF radio title " + videoId }.Start();
            return null;
        }

        private static void Fetch(string videoId)
        {
            string title = "";
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                using (var client = new WebClient { Encoding = System.Text.Encoding.UTF8 })
                {
                    client.Headers[HttpRequestHeader.UserAgent] = "StellarDriveDemoTF";
                    string json = client.DownloadString("https://www.youtube.com/oembed?format=json&url=https%3A%2F%2Fwww.youtube.com%2Fwatch%3Fv%3D" + videoId);
                    title = RadioState.Clean((string)JObject.Parse(json)["title"]);
                }
            }
            catch (Exception e)
            {
                TFMod.Log.Warning($"radio: no title for {videoId}: {e.Message}");
            }
            lock (Known)
                Known[videoId] = title;
        }
    }
}

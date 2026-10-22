using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FishNet.Connection;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.TFItems.Devices
{
    /// <summary>
    /// A radio's music for a player whose own download failed (YouTube refusing their address,
    /// yt-dlp blocked by an antivirus, no network...): they ask the host, which downloads the
    /// video if it has not yet and sends its audio track (the compressed file yt-dlp got, a few
    /// MB) in pieces; the player decodes it like their own download.
    /// </summary>
    internal static class RadioShare
    {
        private const int ChunkLength = 16000, ChunksPerFrame = 24, MaxBytes = 60 * 1024 * 1024;
        private const float HostWait = 300f;

        // Client: the jobs waiting for the host, and the pieces received
        private static readonly Dictionary<string, YouTubeAudio.Job> Waiting = new Dictionary<string, YouTubeAudio.Job>();
        private static readonly Dictionary<string, string[]> Incoming = new Dictionary<string, string[]>();

        public static void Install()
        {
            TFNet.OnServer(TFMessageKind.RadioAudioRequest, (sender, kind, a, b, id) =>
            {
                if (sender != null && YouTubeAudio.IsVideoId(id))
                    MelonLoader.MelonCoroutines.Start(Send(sender, id));
            });
            TFNet.OnClient(TFMessageKind.RadioAudioChunk, (kind, a, index, text) => Receive(index, text));
            StellarDriveDemoTFAdvanced.Common.DevHarness.Steps.Add((30f, "radio share", () =>
            {
                if (StellarDriveDemoTFAdvanced.Common.DevHarness.Off("radioshare"))
                    MelonLoader.MelonCoroutines.Start(DevRun("Xpkc-NU1KA0"));
            }));
            TFNet.Disconnected += () =>
            {
                Waiting.Clear();
                Incoming.Clear();
            };
        }

        /// <summary>Client: this job failed here; the host is asked for the sound.</summary>
        public static void AskHost(YouTubeAudio.Job job)
        {
            job.FromHost = true;
            job.Error = null;
            job.Step = job.Text("Réception du son depuis l'hôte", "Getting the sound from the host", "Recibiendo el sonido del anfitrión");
            job.Status = YouTubeAudio.State.Working;
            Waiting[job.VideoId] = job;
            TFNet.SendToServer(TFMessageKind.RadioAudioRequest, 0, 0, job.VideoId);
            TFMod.Log.Msg($"radio: {job.VideoId} could not be downloaded here, asking the host");
        }

        // Development: the host, also a client of its own game, asks itself for a video as a player whose download failed
        private static IEnumerator DevRun(string id)
        {
            var job = new YouTubeAudio.Job { VideoId = id };
            AskHost(job);
            float until = Time.unscaledTime + 240f;
            while (job.Status == YouTubeAudio.State.Working && Time.unscaledTime < until)
                yield return null;
            TFMod.Log.Msg($"dev: radio share: {id} {job.Status} '{job.Step}' error '{job.Error}' wav {job.WavPath}");
        }

        // Host: the audio track once it has it (downloaded now if needed), in pieces over a few frames
        private static IEnumerator Send(NetworkConnection to, string id)
        {
            YouTubeAudio.Job job = YouTubeAudio.Request(id);
            float until = Time.unscaledTime + HostWait;
            string source;
            while ((source = YouTubeAudio.SourceOf(id)) == null && job.Status != YouTubeAudio.State.Failed && Time.unscaledTime < until)
                yield return null;
            byte[] bytes = null;
            try
            {
                if (source != null && new FileInfo(source).Length <= MaxBytes)
                    bytes = File.ReadAllBytes(source);
            }
            catch (Exception e)
            {
                TFMod.Log.Warning("radio: could not read " + source + ": " + e.Message);
            }
            if (bytes == null)
            {
                string why = job.Error ?? "the host does not have it";
                TFNet.SendTo(to, TFMessageKind.RadioAudioChunk, 0, 0, "0|" + id + "|" + why);
                yield break;
            }
            string packed = Convert.ToBase64String(bytes);
            int count = (packed.Length + ChunkLength - 1) / ChunkLength;
            TFMod.Log.Msg($"radio: sending {id} to a player ({bytes.Length / 1024} KB, {count} pieces)");
            for (int i = 0; i < count; i++)
            {
                int start = i * ChunkLength;
                TFNet.SendTo(to, TFMessageKind.RadioAudioChunk, 0, (ushort)i, count + "|" + id + "|" + packed.Substring(start, Math.Min(ChunkLength, packed.Length - start)));
                if (i % ChunksPerFrame == ChunksPerFrame - 1)
                    yield return null;
            }
        }

        // Client
        private static void Receive(ushort index, string text)
        {
            string[] head = text.Split(new[] { '|' }, 3);
            if (head.Length != 3 || !int.TryParse(head[0], out int count) || !Waiting.TryGetValue(head[1], out YouTubeAudio.Job job))
                return;
            string id = head[1];
            if (count == 0)
            {
                Waiting.Remove(id);
                Incoming.Remove(id);
                job.Error = job.Text("l'hôte n'a pas pu l'avoir non plus (", "the host could not get it either (", "el anfitrión tampoco pudo obtenerlo (") + head[2] + ")";
                job.Status = YouTubeAudio.State.Failed;
                return;
            }
            if (count > MaxBytes / ChunkLength * 2 || index >= count)
                return;
            if (!Incoming.TryGetValue(id, out string[] pieces) || pieces.Length != count)
                Incoming[id] = pieces = new string[count];
            pieces[index] = head[2];
            int have = pieces.Count(p => p != null);
            job.Step = job.Text("Réception du son depuis l'hôte", "Getting the sound from the host", "Recibiendo el sonido del anfitrión") + $" {have * 100 / count} %";
            if (have < count)
                return;
            Waiting.Remove(id);
            Incoming.Remove(id);
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(string.Concat(pieces));
            }
            catch (FormatException)
            {
                job.Error = "bad data from the host";
                job.Status = YouTubeAudio.State.Failed;
                return;
            }
            YouTubeAudio.FromBytes(job, bytes);
        }
    }
}

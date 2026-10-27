using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using MelonLoader.Utils;
using Loc = StellarDriveDemoTFAdvanced.Common.Loc;

namespace StellarDriveDemoTFAdvanced.TFItems.Devices
{
    /// <summary>
    /// Turns a YouTube link into a WAV file the game's sound engine can play: yt-dlp downloads the
    /// audio track (AAC), Windows Media Foundation decodes it. yt-dlp is downloaded once from its
    /// official GitHub release into UserData/TF/tools the first time a radio needs it. Only the
    /// video id is taken from the link, so nothing a player types reaches the command line.
    /// </summary>
    internal static class YouTubeAudio
    {
        public enum State { Working, Ready, Failed }

        public sealed class Job
        {
            public string VideoId;
            public volatile State Status = State.Working;
            public readonly Loc.Language Language = Loc.Current;
            public volatile string Step;

            public Job() => Step = Text("En attente", "Waiting", "En espera");

            public string Text(string french, string english, string spanish) =>
                Language == Loc.Language.French ? french : Language == Loc.Language.Spanish ? spanish : english;
            public string WavPath;
            /// <summary>Seconds of sound in the WAV, once ready.</summary>
            public float Duration;
            public string Error;
            /// <summary>Client: the sound comes from the host (this player's own download failed).</summary>
            public bool FromHost;
            /// <summary>Host: the downloaded track must be there too, for the players who cannot download it.</summary>
            public bool NeedSource;
        }

        private const string YtDlpUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
        private const int MaxSeconds = 20 * 60;
        private const int KeptFiles = 8;

        private static readonly Regex IdPattern = new Regex(
            @"(?:youtube\.com/(?:watch\?(?:[^#]*&)?v=|shorts/|embed/|live/)|youtu\.be/)([A-Za-z0-9_-]{11})(?![A-Za-z0-9_-])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Dictionary<string, Job> Jobs = new Dictionary<string, Job>();
        private static readonly object DownloadLock = new object();

        private static string Root => Path.Combine(MelonEnvironment.UserDataDirectory, "TF");
        private static string CacheDir => Path.Combine(Root, "radio");
        private static string ToolsDir => Path.Combine(Root, "tools");

        /// <summary>The 11-character video id in a YouTube link, or null if it is not one.</summary>
        public static string VideoId(string link)
        {
            if (string.IsNullOrWhiteSpace(link) || link.Length > 300)
                return null;
            Match match = IdPattern.Match(link.Trim());
            return match.Success ? match.Groups[1].Value : null;
        }

        public static bool IsVideoId(string id) => id != null && id.Length == 11 && id.All(c => char.IsLetterOrDigit(c) && c < 128 || c == '_' || c == '-');

        /// <summary>The audio track yt-dlp downloaded for this video (kept for the players who cannot download it), or null.</summary>
        public static string SourceOf(string videoId)
        {
            string path = Path.Combine(CacheDir, videoId + ".src");
            return IsVideoId(videoId) && File.Exists(path) ? path : null;
        }

        /// <summary>Client: decodes an audio track the host sent, like a download of its own.</summary>
        public static void FromBytes(Job job, byte[] bytes)
        {
            job.Step = job.Text("Préparation du son", "Preparing the sound", "Preparando el sonido");
            new Thread(() =>
            {
                try
                {
                    Directory.CreateDirectory(CacheDir);
                    string source = Path.Combine(CacheDir, job.VideoId + ".src"), wav = Path.Combine(CacheDir, job.VideoId + ".wav");
                    File.WriteAllBytes(source, bytes);
                    Decode(source, wav);
                    Trim();
                    TFMod.Log.Msg($"radio: {job.VideoId} ready (from the host, {bytes.Length / 1024} KB)");
                    Finish(job, wav);
                }
                catch (Exception e)
                {
                    job.Error = e.Message;
                    job.Status = State.Failed;
                    TFMod.Log.Warning($"radio: could not decode {job.VideoId} sent by the host: {e.Message}");
                }
            }) { IsBackground = true, Name = "TF radio " + job.VideoId }.Start();
        }

        private static void Decode(string source, string wav)
        {
            string partial = wav + ".tmp";
            Mp4Fixup.StripEditLists(source);
            MediaFoundationDecoder.ToWav(source, partial, MaxSeconds);
            if (File.Exists(wav))
                File.Delete(wav);
            File.Move(partial, wav);
        }

        /// <summary>The job for this video if one was started, without starting one.</summary>
        public static Job Peek(string videoId)
        {
            lock (Jobs)
                return Jobs.TryGetValue(videoId, out Job job) ? job : null;
        }

        /// <summary>Starts (or finds) the job for this video. Poll its status.</summary>
        public static Job Request(string videoId)
        {
            lock (Jobs)
            {
                if (Jobs.TryGetValue(videoId, out Job existing) && existing.Status != State.Failed)
                    return existing;
                var job = new Job { VideoId = videoId, NeedSource = StellarDriveDemoTFAdvanced.Common.GameServices.IsServer };
                Jobs[videoId] = job;
                var thread = new Thread(() => Run(job)) { IsBackground = true, Name = "TF radio " + videoId };
                thread.Start();
                return job;
            }
        }

        private static void Run(Job job)
        {
            try
            {
                Directory.CreateDirectory(CacheDir);
                string wav = Path.Combine(CacheDir, job.VideoId + ".wav");
                if (File.Exists(wav) && new FileInfo(wav).Length > 44 && (!job.NeedSource || SourceOf(job.VideoId) != null))
                {
                    File.SetLastWriteTimeUtc(wav, DateTime.UtcNow);
                    Finish(job, wav);
                    return;
                }

                var clock = Stopwatch.StartNew();
                string ytDlp = EnsureYtDlp(job);
                job.Step = job.Text("Téléchargement de la musique", "Downloading the music", "Descargando la música");
                string downloaded;
                try
                {
                    downloaded = Download(ytDlp, job);
                }
                catch (IOException e)
                {
                    // YouTube changes often and breaks old yt-dlp versions: update it and try once more
                    TFMod.Log.Warning($"radio: yt-dlp failed ({e.Message}), updating it and retrying");
                    job.Step = job.Text("Mise à jour de yt-dlp", "Updating yt-dlp", "Actualizando yt-dlp");
                    Update(ytDlp);
                    job.Step = job.Text("Téléchargement de la musique", "Downloading the music", "Descargando la música");
                    downloaded = Download(ytDlp, job);
                }
                long downloadMs = clock.ElapsedMilliseconds;
                job.Step = job.Text("Préparation du son", "Preparing the sound", "Preparando el sonido");
                // The downloaded track is kept for the players who cannot download it (sent by the host)
                string source = Path.Combine(CacheDir, job.VideoId + ".src");
                if (File.Exists(source))
                    File.Delete(source);
                File.Move(downloaded, source);
                Decode(source, wav);
                Trim();
                TFMod.Log.Msg($"radio: {job.VideoId} ready (download {downloadMs} ms, decode {clock.ElapsedMilliseconds - downloadMs} ms)");
                Finish(job, wav);
                UpdateIfOld(ytDlp);
            }
            catch (Exception e)
            {
                job.Error = e.Message;
                job.Status = State.Failed;
                TFMod.Log.Warning($"radio: could not get YouTube video {job.VideoId}: {e.Message}");
            }
        }

        private static void Finish(Job job, string wav)
        {
            job.Duration = WavSeconds(wav);
            job.WavPath = wav;
            job.Step = job.Text("Prêt", "Ready", "Listo");
            job.Status = State.Ready;
        }

        // The decoder writes a plain 44-byte header: byte rate at 28, data size at 40
        private static float WavSeconds(string wav)
        {
            try
            {
                using (var reader = new BinaryReader(File.OpenRead(wav)))
                {
                    reader.BaseStream.Position = 28;
                    int byteRate = reader.ReadInt32();
                    reader.BaseStream.Position = 40;
                    int size = reader.ReadInt32();
                    return byteRate > 0 ? size / (float)byteRate : 0f;
                }
            }
            catch (Exception)
            {
                return 0f;
            }
        }

        private static string EnsureYtDlp(Job job)
        {
            lock (DownloadLock)
            {
                Directory.CreateDirectory(ToolsDir);
                string path = Path.Combine(ToolsDir, "yt-dlp.exe");
                if (File.Exists(path) && new FileInfo(path).Length > 1_000_000)
                    return path;
                job.Step = job.Text("Installation de yt-dlp (une seule fois)", "Installing yt-dlp (once)", "Instalando yt-dlp (una sola vez)");
                TFMod.Log.Msg("radio: downloading yt-dlp from " + YtDlpUrl);
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                string partial = path + ".part";
                using (var client = new WebClient())
                {
                    client.Headers[HttpRequestHeader.UserAgent] = "StellarDriveDemoTF";
                    client.DownloadFile(YtDlpUrl, partial);
                }
                if (File.Exists(path))
                    File.Delete(path);
                File.Move(partial, path);
                return path;
            }
        }

        // After a song is ready, so the next one does not wait: yt-dlp older than a week updates itself
        private static void UpdateIfOld(string ytDlp)
        {
            try
            {
                if (DateTime.UtcNow - File.GetLastWriteTimeUtc(ytDlp) > TimeSpan.FromDays(7))
                    Update(ytDlp);
            }
            catch (Exception e)
            {
                TFMod.Log.Warning("radio: yt-dlp update: " + e.Message);
            }
        }

        private static void Update(string ytDlp)
        {
            lock (DownloadLock)
            {
                var start = new ProcessStartInfo(ytDlp, "-U") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                using (Process process = Process.Start(start))
                {
                    var stdout = new Thread(() => process.StandardOutput.ReadToEnd()) { IsBackground = true };
                    stdout.Start();
                    process.StandardError.ReadToEnd();
                    if (!process.WaitForExit(120_000))
                        try { process.Kill(); } catch (Exception) { }
                }
                // Marks the check done even when already up to date
                File.SetLastWriteTimeUtc(ytDlp, DateTime.UtcNow);
            }
        }

        private static string Download(string ytDlp, Job job)
        {
            string videoId = job.VideoId;
            foreach (string old in Directory.GetFiles(CacheDir, videoId + ".*").Where(f => !f.EndsWith(".wav") && !f.EndsWith(".src")))
                TryDelete(old);
            string output = Path.Combine(CacheDir, videoId + ".%(ext)s");
            string arguments = string.Join(" ", new[]
            {
                "--no-playlist", "--no-part", "--no-progress", "--no-warnings", "--no-mtime",
                "--max-filesize", "120M",
                "-f", "\"bestaudio[ext=m4a]/bestaudio[acodec^=mp4a]/18/best[ext=mp4]\"",
                "-o", "\"" + output + "\"",
                "--", "\"https://www.youtube.com/watch?v=" + videoId + "\""
            });
            var start = new ProcessStartInfo(ytDlp, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                WorkingDirectory = CacheDir
            };
            string errors;
            using (Process process = Process.Start(start))
            {
                // Read both streams so the process never blocks on a full pipe
                var stdout = new Thread(() => process.StandardOutput.ReadToEnd()) { IsBackground = true };
                stdout.Start();
                errors = process.StandardError.ReadToEnd();
                if (!process.WaitForExit(240_000))
                {
                    try { process.Kill(); } catch (Exception) { }
                    throw new TimeoutException(job.Text("yt-dlp n'a pas fini à temps", "yt-dlp did not finish in time", "yt-dlp no terminó a tiempo"));
                }
                if (process.ExitCode != 0)
                {
                    string last = errors.Split('\n').Select(l => l.Trim()).LastOrDefault(l => l.Length > 0) ?? "code " + process.ExitCode;
                    throw new IOException(last.Length > 160 ? last.Substring(0, 160) : last);
                }
            }
            string file = Directory.GetFiles(CacheDir, videoId + ".*")
                .FirstOrDefault(f => !f.EndsWith(".wav") && !f.EndsWith(".tmp") && !f.EndsWith(".src"));
            if (file == null)
                throw new FileNotFoundException(job.Text("yt-dlp n'a rien téléchargé", "yt-dlp downloaded nothing", "yt-dlp no descargó nada"));
            return file;
        }

        // Keeps the most recent decoded files only
        private static void Trim()
        {
            foreach (string pattern in new[] { "*.wav", "*.src" })
                foreach (FileInfo old in new DirectoryInfo(CacheDir).GetFiles(pattern)
                             .OrderByDescending(f => f.LastWriteTimeUtc).Skip(KeptFiles))
                    TryDelete(old.FullName);
        }

        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception)
            {
                // in use or gone; try again next time
            }
        }
    }
}

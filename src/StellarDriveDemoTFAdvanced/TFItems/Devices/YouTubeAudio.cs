using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using MelonLoader.Utils;

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
            public volatile string Step = "En attente";
            public string WavPath;
            public string Error;
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

        /// <summary>Starts (or finds) the job for this video. Poll its status.</summary>
        public static Job Request(string videoId)
        {
            lock (Jobs)
            {
                if (Jobs.TryGetValue(videoId, out Job existing) && existing.Status != State.Failed)
                    return existing;
                var job = new Job { VideoId = videoId };
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
                if (File.Exists(wav) && new FileInfo(wav).Length > 44)
                {
                    File.SetLastWriteTimeUtc(wav, DateTime.UtcNow);
                    Finish(job, wav);
                    return;
                }

                string ytDlp = EnsureYtDlp(job);
                job.Step = "Téléchargement de la musique";
                string downloaded = Download(ytDlp, job.VideoId);
                job.Step = "Préparation du son";
                string partial = wav + ".tmp";
                Mp4Fixup.StripEditLists(downloaded);
                MediaFoundationDecoder.ToWav(downloaded, partial, MaxSeconds);
                if (File.Exists(wav))
                    File.Delete(wav);
                File.Move(partial, wav);
                TryDelete(downloaded);
                Trim();
                Finish(job, wav);
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
            job.WavPath = wav;
            job.Step = "Prêt";
            job.Status = State.Ready;
        }

        private static string EnsureYtDlp(Job job)
        {
            lock (DownloadLock)
            {
                Directory.CreateDirectory(ToolsDir);
                string path = Path.Combine(ToolsDir, "yt-dlp.exe");
                if (File.Exists(path) && new FileInfo(path).Length > 1_000_000)
                    return path;
                job.Step = "Installation de yt-dlp (une seule fois)";
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

        private static string Download(string ytDlp, string videoId)
        {
            foreach (string old in Directory.GetFiles(CacheDir, videoId + ".*").Where(f => !f.EndsWith(".wav")))
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
                    throw new TimeoutException("yt-dlp n'a pas fini à temps");
                }
                if (process.ExitCode != 0)
                {
                    string last = errors.Split('\n').Select(l => l.Trim()).LastOrDefault(l => l.Length > 0) ?? "code " + process.ExitCode;
                    throw new IOException(last.Length > 160 ? last.Substring(0, 160) : last);
                }
            }
            string file = Directory.GetFiles(CacheDir, videoId + ".*")
                .FirstOrDefault(f => !f.EndsWith(".wav") && !f.EndsWith(".tmp"));
            if (file == null)
                throw new FileNotFoundException("yt-dlp n'a rien téléchargé");
            return file;
        }

        // Keeps the most recent decoded files only
        private static void Trim()
        {
            foreach (FileInfo old in new DirectoryInfo(CacheDir).GetFiles("*.wav")
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

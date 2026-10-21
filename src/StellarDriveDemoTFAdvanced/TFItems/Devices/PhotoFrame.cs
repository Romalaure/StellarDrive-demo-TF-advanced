using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using FishNet.Connection;
using Ships.Interface.Settings;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using UnityEngine;
using Loc = StellarDriveDemoTFAdvanced.Common.Loc;

namespace StellarDriveDemoTFAdvanced.TFItems.Devices
{
    /// <summary>
    /// The photo frame (part 7481): look at it and press the use key to hang one of your photos
    /// (photo mode, F8, saves them in UserData/TFAdvanced/photos; any PNG or JPG put there works).
    /// The photo, made a 960 px JPEG, goes to the host, which keeps it with the world and sends
    /// it to every player who sees the frame. The picture faces out of the surface the frame is on.
    /// </summary>
    internal static class PhotoFrame
    {
        public const ushort Id = 7481;
        private const float Width = 1.2f, Height = 0.72f, Border = 0.06f;
        internal const string PictureName = "TF_NoPaint_PhotoPicture";
        private const int ChunkLength = 16000, MaxBytes = 600000;

        /// <summary>Host: frame → photo hash. Saved with the world.</summary>
        internal static readonly Dictionary<PartKey, string> ServerFrames = new Dictionary<PartKey, string>();
        internal static readonly Dictionary<PartKey, string> ClientFrames = new Dictionary<PartKey, string>();
        private static readonly Dictionary<(int, string), string[]> Incoming = new Dictionary<(int, string), string[]>();
        private static readonly HashSet<string> Requested = new HashSet<string>();

        public static string Cache => Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "TFAdvanced", "photocache");

        public static string CachePath(string hash) => Path.Combine(Cache, hash + ".jpg");

        public static void Register() =>
            Lights.LampParts.RegisterDonorPart(Id, TFTab.DevicesRow, Configure, TFTab.ObjectsName);

        private static void Configure(PartSettings settings, GameObject prefab)
        {
            var frame = new MeshBuilder();
            float w = Width * 0.5f, h = Height * 0.5f;
            // Four bars around the picture, a backing board
            frame.Box(new Vector3(0f, 0.025f, h - Border * 0.5f), new Vector3(Width, 0.05f, Border));
            frame.Box(new Vector3(0f, 0.025f, -h + Border * 0.5f), new Vector3(Width, 0.05f, Border));
            frame.Box(new Vector3(w - Border * 0.5f, 0.025f, 0f), new Vector3(Border, 0.05f, Height - Border * 2f));
            frame.Box(new Vector3(-w + Border * 0.5f, 0.025f, 0f), new Vector3(Border, 0.05f, Height - Border * 2f));
            var board = new MeshBuilder();
            board.Box(new Vector3(0f, 0.01f, 0f), new Vector3(Width - Border, 0.02f, Height - Border));
            var shapes = new Dictionary<string, MeshBuilder> { ["TF_PhotoFrame"] = frame, ["TF_NoPaint_PhotoBoard"] = board };
            var materials = new Dictionary<string, DeviceMaterial>
            {
                ["TF_PhotoFrame"] = new DeviceMaterial(new Color(0.18f, 0.12f, 0.08f), 0f, 0.35f),
                ["TF_NoPaint_PhotoBoard"] = new DeviceMaterial(new Color(0.9f, 0.9f, 0.88f), 0f, 0.1f)
            };
            var center = new Vector3(0f, 0.025f, 0f);
            var size = new Vector3(Width, 0.05f, Height);
            Transform visuals = DeviceModels.Build(settings, prefab, Loc.Pick("Cadre photo", "Photo frame", "Marco de fotos"),
                Loc.Pick("Cadre photo : regarde-le et appuie sur la touche d'utilisation (T) pour y mettre une de tes photos (mode photo : F8). Tous les joueurs la voient. L'image regarde vers l'extérieur de la surface où il est posé : idéal sur un mur.",
                    "Photo frame: look at it and press the use key (T) to hang one of your photos in it (photo mode: F8). Every player sees it. The picture faces out of the surface it is placed on: best on a wall.",
                    "Marco de fotos: míralo y pulsa la tecla de uso (T) para poner una de tus fotos (modo foto: F8). Todos los jugadores la ven. La imagen mira hacia fuera de la superficie donde se coloca: ideal en una pared."),
                2f, new[] { (110u, 3), (102u, 1) }, center, size, shapes, materials);
            // The picture: a quad facing +Y over the board, its top toward +Z
            var picture = new GameObject(PictureName) { layer = visuals.gameObject.layer };
            picture.transform.SetParent(visuals, false);
            picture.AddComponent<MeshFilter>().sharedMesh = Quad(w - Border, h - Border, 0.022f);
            var renderer = picture.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = LitMaterials.Get("TFA_PhotoBlank", new Color(0.75f, 0.77f, 0.8f), 0f, 0.2f);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            UsablePart.AddBox(visuals, center, size + Vector3.one * 0.06f);
            visuals.gameObject.AddComponent<PhotoFrameVisuals>();
        }

        private static Mesh Quad(float halfWidth, float halfHeight, float y)
        {
            var mesh = new Mesh { name = "TFA_PhotoQuad" };
            mesh.vertices = new[] { new Vector3(-halfWidth, y, -halfHeight), new Vector3(halfWidth, y, -halfHeight), new Vector3(-halfWidth, y, halfHeight), new Vector3(halfWidth, y, halfHeight) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.normals = Enumerable.Repeat(Vector3.up, 4).ToArray();
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---- Network: photos travel in pieces, by their hash

        public static void Install()
        {
            StellarDriveDemoTFAdvanced.Common.DevHarness.Steps.Add((100f, "photo frame", () =>
            {
                if (("," + StellarDriveDemoTFAdvanced.Common.Settings.DevShowcase.Value.Replace(" ", "") + ",").Contains("," + Id + ","))
                    MelonLoader.MelonCoroutines.Start(DevRun());
            }));
            TFNet.OnServer(TFMessageKind.PhotoChunk, (sender, kind, a, index, text) =>
            {
                if (Receive(sender?.ClientId ?? -2, index, text, out string hash, out byte[] bytes) && Valid(bytes, hash))
                    Store(hash, bytes);
            });
            TFNet.OnServer(TFMessageKind.PhotoFrameSet, OnServerSet);
            TFNet.OnServer(TFMessageKind.PhotoRequest, (sender, kind, a, b, hash) =>
            {
                if (sender != null && IsHash(hash) && File.Exists(CachePath(hash)))
                    SendChunks(File.ReadAllBytes(CachePath(hash)), hash, (index, chunk) => TFNet.SendTo(sender, TFMessageKind.PhotoChunk, 0, index, chunk));
            });
            TFNet.OnClient(TFMessageKind.PhotoChunk, (kind, a, index, text) =>
            {
                if (Receive(-1, index, text, out string hash, out byte[] bytes) && Valid(bytes, hash))
                    Store(hash, bytes);
            });
            TFNet.OnClient(TFMessageKind.PhotoFrame, (kind, ship, part, hash) => ClientFrames[new PartKey(ship, part)] = hash);
            TFNet.SyncRequested += connection =>
            {
                foreach (KeyValuePair<PartKey, string> frame in ServerFrames.ToList())
                    TFNet.SendTo(connection, TFMessageKind.PhotoFrame, frame.Key.ShipId, frame.Key.PartId, frame.Value);
            };
            TFNet.Disconnected += () =>
            {
                ClientFrames.Clear();
                Incoming.Clear();
                Requested.Clear();
            };
        }

        // Development: a photo taken in photo mode, hung in the showcase's frame, photographed
        private static System.Collections.IEnumerator DevRun()
        {
            yield return PhotoMode.DevShot();
            PhotoFrameVisuals frame = UsablePart.All.OfType<PhotoFrameVisuals>().FirstOrDefault(f => f.HasContext);
            string newest = Directory.Exists(PhotoMode.Folder) ? Directory.GetFiles(PhotoMode.Folder).OrderByDescending(File.GetLastWriteTime).FirstOrDefault() : null;
            TFMod.Log.Msg($"dev: photo frame: frame {frame != null}, newest photo {newest}");
            if (frame == null || newest == null)
                yield break;
            string error = Hang(frame.Key, newest);
            yield return new WaitForSecondsRealtime(2f);
            TFMod.Log.Msg($"dev: photo frame: hung ({error ?? "ok"}), shows {Wanted(frame.Key)}, cached {File.Exists(CachePath(Wanted(frame.Key) ?? "x"))}");
            StellarDriveDemoTFAdvanced.Parts.Showcase.PhotographAgain();
        }

        private static void OnServerSet(NetworkConnection sender, TFMessageKind kind, uint shipId, ushort partId, string hash)
        {
            if (hash.Length > 0 && (!IsHash(hash) || !File.Exists(CachePath(hash))))
                return;
            var ships = GameServices.ShipsServer;
            if (ships == null || !ships.TryGetStatefulPart(new Ships.Interface.Model.Parts.PartContext(shipId, partId), out var part) || part.Settings == null || part.Settings.id != Id)
                return;
            var key = new PartKey(shipId, partId);
            if (hash.Length == 0)
                ServerFrames.Remove(key);
            else
                ServerFrames[key] = hash;
            TFNet.SendToAll(TFMessageKind.PhotoFrame, shipId, partId, hash);
        }

        /// <summary>Client: hangs a photo file in a frame (made small, sent to the host first).</summary>
        public static string Hang(PartKey frame, string file)
        {
            byte[] jpeg = Shrink(file);
            if (jpeg == null)
                return Loc.Pick("Cette image ne peut pas être lue.", "This picture cannot be read.", "Esta imagen no se puede leer.");
            string hash = Hash(jpeg);
            Store(hash, jpeg);
            SendChunks(jpeg, hash, (index, chunk) => TFNet.SendToServer(TFMessageKind.PhotoChunk, 0, index, chunk));
            TFNet.SendToServer(TFMessageKind.PhotoFrameSet, frame.ShipId, frame.PartId, hash);
            return null;
        }

        public static void Clear(PartKey frame) => TFNet.SendToServer(TFMessageKind.PhotoFrameSet, frame.ShipId, frame.PartId, "");

        /// <summary>Client: the photo a frame shows, asked of the host once if it is not here yet.</summary>
        public static string Wanted(PartKey frame)
        {
            if (!ClientFrames.TryGetValue(frame, out string hash) || string.IsNullOrEmpty(hash))
                return null;
            if (!File.Exists(CachePath(hash)) && Requested.Add(hash))
                TFNet.SendToServer(TFMessageKind.PhotoRequest, 0, 0, hash);
            return hash;
        }

        // A picture of any size, as a JPEG at most 960 px wide
        private static byte[] Shrink(string file)
        {
            try
            {
                var source = new Texture2D(2, 2);
                if (!ImageFiles.Load(source, File.ReadAllBytes(file)))
                    return null;
                int width = Mathf.Min(960, source.width);
                int height = Mathf.Max(1, Mathf.RoundToInt(width * (float)source.height / source.width));
                RenderTexture target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(source, target);
                var small = new Texture2D(width, height, TextureFormat.RGB24, false);
                RenderTexture.active = target;
                small.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                small.Apply();
                RenderTexture.active = null;
                RenderTexture.ReleaseTemporary(target);
                byte[] jpeg = ImageFiles.Jpeg(small, 85);
                UnityEngine.Object.Destroy(source);
                UnityEngine.Object.Destroy(small);
                return jpeg;
            }
            catch (Exception e)
            {
                TFMod.Log.Warning("photo frame: " + file + ": " + e.Message);
                return null;
            }
        }

        private static string Hash(byte[] bytes)
        {
            using (SHA1 sha = SHA1.Create())
                return string.Concat(sha.ComputeHash(bytes).Select(b => b.ToString("x2")));
        }

        private static bool IsHash(string text) => text != null && text.Length == 40 && text.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');

        // A JPEG of reasonable size whose hash is the one it travels under
        private static bool Valid(byte[] bytes, string hash) =>
            bytes != null && bytes.Length > 4 && bytes.Length <= MaxBytes && bytes[0] == 0xFF && bytes[1] == 0xD8 && Hash(bytes) == hash;

        private static void Store(string hash, byte[] bytes)
        {
            try
            {
                Directory.CreateDirectory(Cache);
                if (!File.Exists(CachePath(hash)))
                    File.WriteAllBytes(CachePath(hash), bytes);
            }
            catch (Exception e)
            {
                TFMod.Log.Warning("photo frame: could not keep photo " + hash + ": " + e.Message);
            }
        }

        private static void SendChunks(byte[] bytes, string hash, Action<ushort, string> send)
        {
            string packed = Convert.ToBase64String(bytes);
            int count = Math.Max(1, (packed.Length + ChunkLength - 1) / ChunkLength);
            for (int i = 0; i < count; i++)
            {
                int start = i * ChunkLength;
                send((ushort)i, count + "|" + hash + "|" + packed.Substring(start, Math.Min(ChunkLength, packed.Length - start)));
            }
        }

        private static bool Receive(int from, ushort index, string text, out string hash, out byte[] bytes)
        {
            hash = null;
            bytes = null;
            string[] head = text.Split(new[] { '|' }, 3);
            if (head.Length != 3 || !int.TryParse(head[0], out int count) || count < 1 || count > MaxBytes / ChunkLength * 2 + 2 || index >= count || !IsHash(head[1]))
                return false;
            hash = head[1];
            var key = (from, hash);
            if (!Incoming.TryGetValue(key, out string[] pieces) || pieces.Length != count)
            {
                foreach (var stale in Incoming.Keys.Where(k => k.Item1 == from).ToList())
                    Incoming.Remove(stale);
                Incoming[key] = pieces = new string[count];
            }
            pieces[index] = head[2];
            if (pieces.Any(p => p == null))
                return false;
            Incoming.Remove(key);
            try
            {
                bytes = Convert.FromBase64String(string.Concat(pieces));
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }

    /// <summary>A frame: shows its photo once it is here; using it opens the photo picker.</summary>
    internal sealed class PhotoFrameVisuals : UsablePart
    {
        private static readonly Dictionary<string, Texture2D> Loaded = new Dictionary<string, Texture2D>();
        private Renderer _picture;
        private string _shown;
        private float _next;

        public override string Label => Loc.Pick("Cadre photo", "Photo frame", "Marco de fotos");

        public override void Use() => PhotoFrameMenu.Show(Key);

        protected override void Awake()
        {
            base.Awake();
            _picture = transform.Find(PhotoFrame.PictureName)?.GetComponent<Renderer>();
        }

        private void Update()
        {
            if (!HasContext || _picture == null || Time.unscaledTime < _next)
                return;
            _next = Time.unscaledTime + 0.5f;
            string hash = PhotoFrame.Wanted(Key);
            if (hash == _shown)
                return;
            if (hash == null)
            {
                _shown = null;
                _picture.sharedMaterial = LitMaterials.Get("TFA_PhotoBlank", new Color(0.75f, 0.77f, 0.8f), 0f, 0.2f);
                return;
            }
            Texture2D texture = Load(hash);
            if (texture == null)
                return;
            _shown = hash;
            // Its own material: the photo, lit by the scene and glowing a little (seen in dark rooms)
            var material = new Material(LitMaterials.Get("TFA_PhotoBase", Color.white, 0f, 0.15f)) { name = "TFA_Photo_" + hash };
            material.SetTexture("_BaseMap", texture);
            material.SetTexture("_MainTex", texture);
            material.EnableKeyword("_EMISSION");
            material.SetTexture("_EmissionMap", texture);
            material.SetColor("_EmissionColor", Color.white * 0.35f);
            _picture.sharedMaterial = material;
        }

        private static Texture2D Load(string hash)
        {
            if (Loaded.TryGetValue(hash, out Texture2D cached) && cached != null)
                return cached;
            string path = PhotoFrame.CachePath(hash);
            if (!File.Exists(path))
                return null;
            var texture = new Texture2D(2, 2, TextureFormat.RGB24, true) { name = "TFA_Photo_" + hash, wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
            if (!ImageFiles.Load(texture, File.ReadAllBytes(path)))
                return null;
            Loaded[hash] = texture;
            return texture;
        }
    }

    /// <summary>The photo picker: the photos in UserData/TFAdvanced/photos, newest first.</summary>
    internal static class PhotoFrameMenu
    {
        private static bool _open;
        private static PartKey _frame;
        private static Vector2 _scroll;
        private static string _message;
        private static List<string> _files = new List<string>();
        private static readonly Dictionary<string, Texture2D> Thumbs = new Dictionary<string, Texture2D>();

        public static void Show(PartKey frame)
        {
            _frame = frame;
            _open = true;
            _message = null;
            try
            {
                Directory.CreateDirectory(PhotoMode.Folder);
                _files = Directory.GetFiles(PhotoMode.Folder).Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(File.GetLastWriteTime).Take(60).ToList();
            }
            catch (Exception e)
            {
                _files = new List<string>();
                _message = e.Message;
            }
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
            Rect area = DeviceUi.Window(width, height, 820f, 700f, Loc.Pick("CADRE PHOTO", "PHOTO FRAME", "MARCO DE FOTOS"));
            GUILayout.BeginArea(area);
            GUILayout.Label(_files.Count == 0
                ? Loc.Pick("Aucune photo : prends-en avec le mode photo (F8), ou mets des images PNG/JPG dans UserData/TFAdvanced/photos.", "No photo yet: take some in photo mode (F8), or put PNG/JPG pictures in UserData/TFAdvanced/photos.", "Ninguna foto: haz algunas en el modo foto (F8) o pon imágenes PNG/JPG en UserData/TFAdvanced/photos.")
                : Loc.Pick("Clique sur une photo pour l'accrocher (tous les joueurs la verront).", "Click a photo to hang it (every player will see it).", "Haz clic en una foto para colgarla (todos los jugadores la verán)."), DeviceUi.Text);
            if (_message != null)
                GUILayout.Label(_message, DeviceUi.Muted);
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(520f));
            int loadedThisFrame = 0;
            for (int i = 0; i < _files.Count; i += 3)
            {
                GUILayout.BeginHorizontal();
                foreach (string file in _files.Skip(i).Take(3))
                {
                    Texture2D thumb = Thumb(file, ref loadedThisFrame);
                    var content = thumb != null ? new GUIContent(thumb, Path.GetFileName(file)) : new GUIContent(Path.GetFileNameWithoutExtension(file));
                    if (GUILayout.Button(content, DeviceUi.Button, GUILayout.Width(250f), GUILayout.Height(150f)))
                    {
                        _message = PhotoFrame.Hang(_frame, file);
                        if (_message == null)
                            ModMenu.Close();
                    }
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(6f);
            }
            GUILayout.EndScrollView();
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Loc.Pick("Retirer la photo", "Take the photo down", "Quitar la foto"), DeviceUi.Button, GUILayout.Height(34f)))
            {
                PhotoFrame.Clear(_frame);
                ModMenu.Close();
            }
            if (GUILayout.Button(Loc.Pick("Ouvrir le dossier", "Open the folder", "Abrir la carpeta"), DeviceUi.Button, GUILayout.Height(34f)))
                Application.OpenURL("file:///" + PhotoMode.Folder.Replace('\\', '/'));
            if (GUILayout.Button(Loc.Pick("Fermer (Échap)", "Close (Esc)", "Cerrar (Esc)"), DeviceUi.Button, GUILayout.Height(34f)))
                ModMenu.Close();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            GUI.matrix = previous;
        }

        // Small copies of the photos, one read per frame
        private static Texture2D Thumb(string file, ref int loadedThisFrame)
        {
            if (Thumbs.TryGetValue(file, out Texture2D cached))
                return cached;
            if (loadedThisFrame >= 1 || Event.current.type != EventType.Repaint)
                return null;
            loadedThisFrame++;
            Texture2D small = null;
            try
            {
                var source = new Texture2D(2, 2);
                if (ImageFiles.Load(source, File.ReadAllBytes(file)))
                {
                    RenderTexture target = RenderTexture.GetTemporary(240, 135, 0, RenderTextureFormat.ARGB32);
                    Graphics.Blit(source, target);
                    small = new Texture2D(240, 135, TextureFormat.RGB24, false);
                    RenderTexture.active = target;
                    small.ReadPixels(new Rect(0, 0, 240, 135), 0, 0);
                    small.Apply();
                    RenderTexture.active = null;
                    RenderTexture.ReleaseTemporary(target);
                }
                UnityEngine.Object.Destroy(source);
            }
            catch (Exception e)
            {
                TFMod.Log.Warning("photo frame: thumbnail " + file + ": " + e.Message);
            }
            Thumbs[file] = small;
            return small;
        }
    }

    /// <summary>
    /// Image files through Unity's ImageConversion, called by reflection: its overloads taking spans
    /// need a newer netstandard than this build sees.
    /// </summary>
    internal static class ImageFiles
    {
        private static readonly System.Reflection.MethodInfo LoadMethod = typeof(ImageConversion).GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]) });
        private static readonly System.Reflection.MethodInfo JpegMethod = typeof(ImageConversion).GetMethod("EncodeToJPG", new[] { typeof(Texture2D), typeof(int) });

        public static bool Load(Texture2D texture, byte[] data) => (bool)LoadMethod.Invoke(null, new object[] { texture, data });

        public static byte[] Jpeg(Texture2D texture, int quality) => (byte[])JpegMethod.Invoke(null, new object[] { texture, quality });
    }
}

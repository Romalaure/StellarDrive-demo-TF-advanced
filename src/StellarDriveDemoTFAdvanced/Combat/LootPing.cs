using System.Collections.Generic;
using System.Globalization;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Combat
{
    /// <summary>
    /// When a pirate ship is destroyed, every player sees where its loot capsule went: a pulsing
    /// marker on screen over the capsule (on the screen's edge when it is out of view) with its
    /// distance, for <see cref="Settings.LootPingMinutes"/> minutes or until the capsule is gone.
    /// The host tells every player the capsule's ship id (<see cref="TFANet"/>); each game follows
    /// the capsule's own visual (<see cref="ShipVisuals"/>, cached), so a ping costs a few GUI draws.
    /// </summary>
    internal static class LootPing
    {
        private sealed class Ping
        {
            public uint ShipId;
            public float Until;
            public bool Seen;
            public float MissingSince = -1f;
        }

        private static readonly List<Ping> Pings = new List<Ping>();
        private static readonly Color Gold = new Color(1f, 0.78f, 0.2f);
        private static bool _listening;
        private static Texture2D _ring, _dot;
        private static GUIStyle _label;

        public static void OnSceneLoaded() => Pings.Clear();

        /// <summary>Host: a capsule was just dropped by a destroyed pirate.</summary>
        public static void Announce(uint capsuleId)
        {
            Listen();
            Add(capsuleId);
            TFANet.SendToAll(TFANet.LootPing, capsuleId, "");
        }

        /// <summary>Every frame in game: forgets pings run out or whose capsule is gone (picked up, recycled).</summary>
        public static void Update()
        {
            Listen();
            if (Pings.Count == 0)
                return;
            float now = Time.unscaledTime;
            for (int i = Pings.Count - 1; i >= 0; i--)
            {
                Ping ping = Pings[i];
                bool here = ShipVisuals.Find(ping.ShipId) != null;
                if (here)
                {
                    ping.Seen = true;
                    ping.MissingSince = -1f;
                }
                else if (ping.Seen && ping.MissingSince < 0f)
                    ping.MissingSince = now;
                if (now > ping.Until || ping.MissingSince >= 0f && now - ping.MissingSince > 5f)
                    Pings.RemoveAt(i);
            }
        }

        public static void OnGUI()
        {
            if (Pings.Count == 0 || Event.current.type != EventType.Repaint)
                return;
            Camera camera = MainCamera.Get();
            if (camera == null)
                return;
            EnsureStyle();
            foreach (Ping ping in Pings)
            {
                Transform capsule = ShipVisuals.Find(ping.ShipId);
                if (capsule != null)
                    Draw(camera, capsule.position, ping);
            }
            GUI.color = Color.white;
        }

        /// <summary>Development: the pings, for the logs.</summary>
        public static string DevState()
        {
            var parts = new List<string>();
            foreach (Ping ping in Pings)
            {
                Transform capsule = ShipVisuals.Find(ping.ShipId);
                Camera camera = MainCamera.Get();
                string screen = capsule != null && camera != null ? camera.WorldToScreenPoint(capsule.position).ToString("0") : "-";
                parts.Add($"{ping.ShipId}: {(capsule != null ? "seen" : "missing")} at screen {screen}, {ping.Until - Time.unscaledTime:0} s left");
            }
            return Pings.Count + " [" + string.Join("; ", parts) + "]";
        }

        private static void Listen()
        {
            if (_listening)
                return;
            _listening = true;
            TFANet.Received += OnMessage;
        }

        private static void OnMessage(byte kind, uint shipId, string text)
        {
            if (kind == TFANet.LootPing)
                Add(shipId);
        }

        private static void Add(uint shipId)
        {
            float until = Time.unscaledTime + Mathf.Max(0.1f, Settings.LootPingMinutes.Value) * 60f;
            Ping existing = Pings.Find(p => p.ShipId == shipId);
            if (existing != null)
            {
                existing.Until = until;
                return;
            }
            if (Settings.LootPingMinutes.Value > 0f)
                Pings.Add(new Ping { ShipId = shipId, Until = until });
        }

        private static void Draw(Camera camera, Vector3 world, Ping ping)
        {
            Vector3 screen = camera.WorldToScreenPoint(world);
            bool behind = screen.z < 0f;
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            // GUI space: y down. Behind the camera, the point is mirrored through the screen's center
            var p = new Vector2(screen.x, Screen.height - screen.y);
            if (behind)
                p = center - (p - center);
            // Kept clear of the screen's edges and of the hotbar at the bottom; sizes follow the screen's height
            float ui = Mathf.Max(1f, Screen.height / 1080f);
            float side = 60f * ui, top = 60f * ui, bottom = 170f * ui;
            bool edge = behind || p.x < side || p.x > Screen.width - side || p.y < top || p.y > Screen.height - bottom;
            if (edge)
            {
                Vector2 d = p - center;
                if (d.sqrMagnitude < 1f)
                    d = Vector2.up;
                float kx = d.x > 0f ? (Screen.width - side - center.x) / d.x : d.x < 0f ? (side - center.x) / d.x : float.MaxValue;
                float ky = d.y > 0f ? (Screen.height - bottom - center.y) / d.y : d.y < 0f ? (top - center.y) / d.y : float.MaxValue;
                p = center + d * Mathf.Min(kx, ky);
            }
            float t = Time.unscaledTime;
            // An expanding ring every 1.2 s, a steady dot in its middle; an arrow out from it when off screen
            float phase = Mathf.Repeat(t / 1.2f, 1f);
            float ring = (24f + 44f * phase) * ui;
            GUI.color = new Color(Gold.r, Gold.g, Gold.b, 1f - phase);
            GUI.DrawTexture(new Rect(p.x - ring * 0.5f, p.y - ring * 0.5f, ring, ring), _ring);
            float dot = 18f * ui;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(p.x - dot * 0.65f, p.y - dot * 0.65f, dot * 1.3f, dot * 1.3f), _dot);
            GUI.color = Gold;
            GUI.DrawTexture(new Rect(p.x - dot * 0.5f, p.y - dot * 0.5f, dot, dot), _dot);
            if (edge)
            {
                Vector2 out1 = (p - center).normalized;
                for (int k = 1; k <= 3; k++)
                {
                    float r = (4f - k) * 3f * ui;
                    Vector2 at = p + out1 * (dot * 0.5f + k * 7f * ui);
                    GUI.DrawTexture(new Rect(at.x - r, at.y - r, r * 2f, r * 2f), _dot);
                }
            }
            float meters = Vector3.Distance(camera.transform.position, world);
            int left = Mathf.Max(0, Mathf.CeilToInt(ping.Until - t));
            _label.fontSize = Mathf.RoundToInt(17f * ui);
            string text = Loc.Pick("Butin pirate", "Pirate loot", "Botín pirata") + "  " + Distance(meters)
                + "  <size=" + Mathf.RoundToInt(13f * ui) + ">" + (left / 60).ToString(CultureInfo.InvariantCulture) + ":" + (left % 60).ToString("00", CultureInfo.InvariantCulture) + "</size>";
            float w = 300f * ui, h = 28f * ui;
            float y = p.y > Screen.height * 0.5f ? p.y - ring * 0.5f - h - 4f * ui : p.y + 20f * ui;
            var rect = new Rect(Mathf.Clamp(p.x - w * 0.5f, 4f, Screen.width - w - 4f), y, w, h);
            GUI.color = Color.black;
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, _label);
            GUI.color = Gold;
            GUI.Label(rect, text, _label);
        }

        private static string Distance(float meters) =>
            meters < 1000f ? meters.ToString("0", CultureInfo.InvariantCulture) + " m" : Radar.Distance(meters);

        private static void EnsureStyle()
        {
            if (_label == null)
                _label = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true, normal = { textColor = Color.white } };
            if (_ring == null)
                _ring = Circle(64, 0.78f);
            if (_dot == null)
                _dot = Circle(32, 0f);
        }

        // A white disc (inner 0) or ring (inner: the hole's radius as a fraction), edges smoothed
        private static Texture2D Circle(int size, float inner)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "TFA_LootPing", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            float half = size * 0.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float r = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) / half;
                    float a = Mathf.Clamp01((1f - r) * half);
                    if (inner > 0f)
                        a = Mathf.Min(a, Mathf.Clamp01((r - inner) * half));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }
}

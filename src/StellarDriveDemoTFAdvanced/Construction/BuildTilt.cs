using UnityEngine;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using Loc = StellarDriveDemoTFAdvanced.Common.Loc;

namespace StellarDriveDemoTFAdvanced.Construction
{
    /// <summary>
    /// How a construction part being placed is turned beyond what the game allows (its up out of
    /// the face aimed at, turned about it): the tilt key (B) cycles upright, tipped forward, on its
    /// side and upside down, so a plank wall can stand on a hull wall, a post lie along a floor, a
    /// window close a sloped opening. Reset when another part is chosen.
    /// </summary>
    internal static class BuildTilt
    {
        private static readonly KeySetting Key = new KeySetting(() => Settings.BuildTiltKey);
        private static readonly Quaternion[] Tilts = { Quaternion.identity, Quaternion.Euler(90f, 0f, 0f), Quaternion.Euler(0f, 0f, 90f), Quaternion.Euler(180f, 0f, 0f) };
        private static int _index;
        private static ushort _part;
        private static float _seen = -10f;
        private static GUIStyle _hint;

        /// <summary>The tilt of the part being placed (asked while its preview is placed).</summary>
        public static Quaternion For(ushort partId)
        {
            if (partId != _part)
            {
                _part = partId;
                _index = 0;
            }
            _seen = Time.unscaledTime;
            return Tilts[_index];
        }

        /// <summary>Development: the tilt the next placements get.</summary>
        internal static void Force(ushort partId, int index)
        {
            _part = partId;
            _index = index % Tilts.Length;
        }

        private static bool Placing => Time.unscaledTime - _seen < 0.5f;

        public static void Update()
        {
            if (Placing && !ModMenu.IsOpen && Key.WasPressed)
                _index = (_index + 1) % Tilts.Length;
        }

        private static string Name(int i) => i == 0 ? Loc.Pick("droite", "upright", "derecha")
            : i == 1 ? Loc.Pick("basculée", "tipped forward", "inclinada")
            : i == 2 ? Loc.Pick("sur le côté", "on its side", "de lado")
            : Loc.Pick("retournée", "upside down", "invertida");

        public static void Draw()
        {
            if (!Placing || ModMenu.IsOpen || Key.Key == UnityEngine.InputSystem.Key.None)
                return;
            if (_hint == null)
                _hint = new GUIStyle(TFItems.Devices.DeviceUi.Text) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.7f, 2.5f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale, height = Screen.height / scale;
            var rect = new Rect(width / 2f - 200f, height - 200f, 400f, 30f);
            TFItems.Devices.DeviceUi.Panel(rect);
            GUI.Label(rect, $"[{Key.Label}]  " + Loc.Pick("Orientation : ", "Orientation: ", "Orientación: ") + Name(_index), _hint);
            GUI.matrix = previous;
        }
    }
}

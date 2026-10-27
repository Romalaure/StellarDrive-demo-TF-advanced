using UnityEngine;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using Loc = StellarDriveDemoTFAdvanced.Common.Loc;

namespace StellarDriveDemoTFAdvanced.Construction
{
    /// <summary>
    /// How a construction part being placed is turned beyond what the game allows (its up out of
    /// the face aimed at, turned about it): the tilt key (B) cycles upright, tipped forward, on its
    /// side and upside down, so a plank wall can stand on a hull wall, a post lie along a floor, a
    /// window close a sloped opening. Reset when another part is chosen. An automatic door switches
    /// between diagonal and straight with the mouse wheel instead, along with the game's rotation.
    /// </summary>
    internal static class BuildTilt
    {
        private static readonly KeySetting Key = new KeySetting(() => Settings.BuildTiltKey);
        private static readonly Quaternion[] Tilts = { Quaternion.identity, Quaternion.Euler(90f, 0f, 0f), Quaternion.Euler(0f, 0f, 90f), Quaternion.Euler(180f, 0f, 0f) };
        private static int _index;
        private static ushort _part;
        private static float _seen = -10f;
        private static GUIStyle _hint;
        private static bool _door;

        /// <summary>The automatic door being placed goes straight (else diagonal).</summary>
        public static bool DoorStraight { get; private set; }

        /// <summary>The tilt of the part being placed (asked while its preview is placed).</summary>
        public static Quaternion For(ushort partId)
        {
            if (partId != _part)
            {
                _part = partId;
                _index = 0;
            }
            _door = false;
            _seen = Time.unscaledTime;
            return Tilts[_index];
        }

        /// <summary>Asked by an automatic door with no context: straight or diagonal if it is the build preview.</summary>
        public static bool ForDoor()
        {
            // Thumbnails have no context either: only the door the build tool is placing counts
            var placing = FloatingSnap.Placing;
            if (placing == null || !DiagonalDoors.IsDoor(placing.id) || Time.unscaledTime - FloatingSnap.PlacingSeen > 0.5f)
                return false;
            _door = true;
            _seen = Time.unscaledTime;
            return DoorStraight;
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
            if (!Placing || _door || ModMenu.IsOpen || !Key.WasPressed)
                return;
            _index = (_index + 1) % Tilts.Length;
        }

        [HarmonyLib.HarmonyPatch(typeof(Tools.Build.BuildToolLocalClient), nameof(Tools.Build.BuildToolLocalClient.ExecuteScrollAction))]
        private static class DoorWheel
        {
            // Each notch switches the layout and the game turns the door every second one: the wheel goes through all 8
            private static bool Prefix()
            {
                if (!_door || !Placing)
                    return true;
                DoorStraight = !DoorStraight;
                return DoorStraight;
            }
        }

        private static string Name(int i) => i == 0 ? Loc.Pick("droite", "upright", "derecha")
            : i == 1 ? Loc.Pick("basculée", "tipped forward", "inclinada")
            : i == 2 ? Loc.Pick("sur le côté", "on its side", "de lado")
            : Loc.Pick("retournée", "upside down", "invertida");

        public static void Draw()
        {
            bool tilt = Placing && (_door || Key.Key != UnityEngine.InputSystem.Key.None);
            bool free = Time.unscaledTime - HullMount.FreeSeen < 0.5f && HullMount.FreeLabel != "?";
            if (!tilt && !free || ModMenu.IsOpen)
                return;
            if (_hint == null)
                _hint = new GUIStyle(TFItems.Devices.DeviceUi.Text) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.7f, 2.5f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale, height = Screen.height / scale;
            var rect = new Rect(width / 2f - 280f, height - 200f, 560f, 30f);
            TFItems.Devices.DeviceUi.Panel(rect);
            string text = !tilt ? ""
                : _door ? Loc.Pick("[Molette]  Porte : ", "[Wheel]  Door: ", "[Rueda]  Puerta: ") + (DoorStraight ? Loc.Pick("droite", "straight", "recta") : Loc.Pick("en diagonale", "diagonal", "en diagonal"))
                : $"[{Key.Label}]  " + Loc.Pick("Orientation : ", "Orientation: ", "Orientación: ") + Name(_index);
            if (free)
                text += (tilt ? "    " : "") + $"[{HullMount.FreeLabel}]  " + Loc.Pick("placement libre", "free placement", "colocación libre");
            GUI.Label(rect, text, _hint);
            GUI.matrix = previous;
        }
    }
}

using System;
using System.Collections.Generic;
using Players.Interface.Model;
using Ships.Parts.Common;
using Ships.Parts.Common.Model;
using Ships.Parts.Common.Utils;
using UnityEngine;
using Loc = StellarDriveDemoTFAdvanced.Common.Loc;

namespace StellarDriveDemoTFAdvanced.TFItems.Common
{
    /// <summary>
    /// TF parts the player uses with a key (T by default) while looking at them: the radio, the
    /// wardrobe, the teleport capsule. Looking is a ray from the camera against the part's box,
    /// so it needs no collider or game interaction; a prompt shows the key when one is in reach.
    /// </summary>
    internal static class UsableParts
    {
        private const float Reach = 3.2f;

        private static readonly KeySetting UseKey = new KeySetting(() => Settings.UseKey);
        private static UsablePart _target;
        private static Worlds.MapSystemFilter _map;
        private static GUIStyle _prompt;

        public static void Update()
        {
            _target = null;
            if (ModMenu.IsOpen || Cameras.CameraTablet.Open)
                return;
            PlayerStateType state = GameServices.LocalState.Type;
            if (state != PlayerStateType.Walking && state != PlayerStateType.CheatFlying)
                return;
            Camera camera = MainCamera.Get();
            if (camera == null)
                return;

            var ray = new Ray(camera.transform.position, camera.transform.forward);
            float best = Reach;
            foreach (UsablePart part in UsablePart.All)
            {
                if (part == null || !part.HasContext)
                    continue;
                if (part.Hit(ray, out float distance) && distance < best)
                {
                    best = distance;
                    _target = part;
                }
            }
            // A ship's star map: the use key switches the galaxy it shows
            _map = null;
            foreach (Worlds.MapSystemFilter map in Worlds.MapSystemFilter.Shown)
            {
                if (map != null && map.Hit(ray, out float distance) && distance < best)
                {
                    best = distance;
                    _map = map;
                    _target = null;
                }
            }
            if (_map != null && UseKey.WasPressed)
            {
                Worlds.MapSystemFilter.Cycle();
                Devices.NpcMenu.Result(Loc.Pick("Carte : ", "Map: ", "Mapa: ") + Worlds.MapSystemFilter.ViewName);
                return;
            }
            if (_target != null && UseKey.WasPressed)
            {
                try
                {
                    _target.Use();
                }
                catch (Exception e)
                {
                    TFMod.Log.Error("using " + _target.Label + ": " + e);
                }
            }
        }

        public static void Draw()
        {
            if (_target == null && _map == null || ModMenu.IsOpen)
                return;
            if (_prompt == null)
            {
                _prompt = new GUIStyle(Devices.DeviceUi.Text) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            }
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.7f, 2.5f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale, height = Screen.height / scale;
            var rect = new Rect(width / 2f - 180f, height / 2f + 46f, 360f, 38f);
            // (the game's panel, its textures made once)
            Devices.DeviceUi.Panel(rect);
            string label = _target != null ? _target.Label
                : Loc.Pick("Changer de galaxie (", "Change galaxy (", "Cambiar de galaxia (") + Worlds.MapSystemFilter.ViewName + ")";
            if (_map != null)
                rect = new Rect(width / 2f - 260f, rect.y, 520f, rect.height);
            GUI.Label(rect, $"[{UseKey.Label}]  {label}", _prompt);
            GUI.matrix = previous;
        }
    }

    /// <summary>
    /// A TF part the player can use. Its box (in the part's own space) is read from a child named
    /// "TF_Use cx cy cz sx sy sz", since Instantiate does not copy this component's fields.
    /// </summary>
    internal abstract class UsablePart : DefaultShipPartVisuals, IContextAwarePart
    {
        public const string BoxName = "TF_Use";

        public static readonly List<UsablePart> All = new List<UsablePart>();

        public PartKey Key { get; private set; }
        public bool HasContext { get; private set; }
        public abstract string Label { get; }

        private Bounds _box;

        public static void AddBox(Transform visuals, Vector3 center, Vector3 size)
        {
            string name = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0} {1} {2} {3} {4} {5} {6}",
                BoxName, center.x, center.y, center.z, size.x, size.y, size.z);
            var child = new GameObject(name) { layer = visuals.gameObject.layer };
            child.transform.SetParent(visuals, false);
        }

        protected virtual void Awake()
        {
            _box = new Bounds(Vector3.up * 0.5f, Vector3.one);
            foreach (Transform child in transform)
            {
                if (!child.name.StartsWith(BoxName))
                    continue;
                string[] parts = child.name.Split(' ');
                var values = new float[6];
                bool ok = parts.Length == 7;
                for (int i = 0; ok && i < 6; i++)
                    ok = float.TryParse(parts[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out values[i]);
                if (ok)
                    _box = new Bounds(new Vector3(values[0], values[1], values[2]), new Vector3(values[3], values[4], values[5]));
            }
        }

        public virtual void SetContext(ShipPartContext context)
        {
            Key = new PartKey(context.Part.ShipId, context.Part.PartId);
            HasContext = true;
        }

        protected virtual void OnEnable() => All.Add(this);

        protected virtual void OnDisable() => All.Remove(this);

        /// <summary>Ray against the part's box, in its own space.</summary>
        public bool Hit(Ray ray, out float distance)
        {
            var local = new Ray(transform.InverseTransformPoint(ray.origin), transform.InverseTransformDirection(ray.direction));
            return _box.IntersectRay(local, out distance);
        }

        public Vector3 Center => transform.TransformPoint(_box.center);

        public abstract void Use();
    }
}

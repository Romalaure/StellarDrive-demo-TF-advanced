using System.Collections.Generic;
using FishNet.Connection;
using Ships;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Interface.Settings;
using Ships.Parts.Common.Model.Reactions;
using StellarDriveDemoTFAdvanced.Common;
using StellarDriveDemoTFAdvanced.Parts;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using UnityEngine;
using UnityEngine.Rendering;
using Loc = StellarDriveDemoTFAdvanced.Common.Loc;
using MeshBuilder = StellarDriveDemoTFAdvanced.Common.MeshBuilder;
using LitMaterials = StellarDriveDemoTFAdvanced.Common.LitMaterials;

namespace StellarDriveDemoTFAdvanced.Construction
{
    /// <summary>
    /// Automatic sliding doors set on a floor: across the diagonal of one floor square (or of
    /// 2 × 2), for the diagonal walls, or straight along the square's front edge (-z), in place of
    /// a straight wall: chosen while placing it with the mouse wheel (see <see cref="BuildTilt"/>), the
    /// player who placed it then asks for that layout. It opens by itself when someone comes near
    /// and closes behind them. Its state is the part's ConstantState, kept and shared by the game:
    /// 1 open, 2 straight. Placed parts do not seal a room: it does not hold air like the game's doors.
    /// </summary>
    internal static class DiagonalDoors
    {
        public const ushort SmallId = 8233, LargeId = 8234;
        public const float Height = 2f, Thickness = 0.12f, Post = 0.08f, Lintel = 0.12f;
        public const int Open = 1, Straight = 2;
        public const string DiagonalName = "TFA_DoorDiagonal", StraightName = "TFA_DoorStraight", SizePrefix = "TFA_DoorSquares_";

        public static bool IsDoor(ushort id) => id == SmallId || id == LargeId;

        /// <summary>The diagonal door's axis: along the diagonal of its square, from (-, -) to (+, +).</summary>
        public static readonly Quaternion Turn = Quaternion.Euler(0f, -45f, 0f);

        public static float Width(int squares, bool straight) => straight ? squares : squares * Mathf.Sqrt(2f);

        /// <summary>Where the door's middle line lies in the part's frame (straight: the front edge).</summary>
        public static Vector3 Offset(int squares, bool straight) => straight ? new Vector3(0f, 0f, -squares * 0.5f) : Vector3.zero;

        public static Quaternion Axis(bool straight) => straight ? Quaternion.identity : Turn;

        public static void Register()
        {
            PartFactory.Register(SmallId, "SignalDisplay", AdvancedTab.ConstructionName, AdvancedTab.SignRow, (settings, prefab) => Configure(1, settings, prefab));
            PartFactory.Register(LargeId, "SignalDisplay", AdvancedTab.ConstructionName, AdvancedTab.SignRow, (settings, prefab) => Configure(2, settings, prefab));
        }

        private static void Configure(int squares, PartSettings settings, GameObject prefab)
        {
            PartFactory.Describe(settings,
                squares == 1 ? Loc.Pick("Porte automatique", "Automatic door", "Puerta automática") : Loc.Pick("Grande porte automatique", "Large automatic door", "Puerta automática grande"),
                Loc.Pick($"Une porte coulissante qui s'ouvre seule quand quelqu'un approche, posée au sol sur {(squares == 2 ? "2 × 2 cases" : "une case")}. En diagonale pour les murs en diagonale (enlève le mur, pose la porte à sa place), ou droite le long du bord avant de la case, à la place d'un mur droit : choisis pendant la pose avec la molette. Ne retient pas l'air comme les portes du jeu.",
                    $"A sliding door that opens by itself when someone comes near, set on the floor over {(squares == 2 ? "2 × 2 squares" : "one square")}. Diagonal for the diagonal walls (remove the wall, put the door in its place), or straight along the square's front edge, in place of a straight wall: choose while placing it with the mouse wheel. It does not hold air like the game's doors.",
                    $"Una puerta corredera que se abre sola cuando alguien se acerca, puesta en el suelo sobre {(squares == 2 ? "2 × 2 casillas" : "una casilla")}. En diagonal para las paredes diagonales (quita la pared, pon la puerta en su lugar), o recta a lo largo del borde delantero de la casilla, en lugar de una pared recta: elígelo al colocarla con la rueda del ratón. No retiene el aire como las puertas del juego."),
                squares == 1 ? 40f : 70f, squares == 1 ? new[] { (101u, 4), (102u, 1), (112u, 1) } : new[] { (101u, 7), (102u, 2), (112u, 2) });
            settings.internalStateClass = Power.PowerCatalog.MachineStateClass;
            settings.snappingStyle = Ships.Interface.Model.SnappingStyle.PreciseOnFloor;
            var size = new Vector3(squares, Height, squares);
            Transform visuals = PartFactory.BlankFromSignalDisplay(prefab, new Vector3(0f, Height * 0.5f, 0f), size);
            int layer = visuals.gameObject.layer;
            new GameObject(SizePrefix + squares) { layer = layer }.transform.SetParent(visuals, false);
            foreach (bool straight in new[] { false, true })
            {
                var root = new GameObject(straight ? StraightName : DiagonalName) { layer = layer };
                root.transform.SetParent(visuals, false);
                root.transform.localPosition = Offset(squares, straight);
                root.transform.localRotation = Axis(straight);
                root.SetActive(!straight);
                Layout(root.transform, layer, Width(squares, straight));
            }
            var plain = visuals.GetComponent<Ships.Parts.Common.DefaultShipPartVisuals>();
            if (plain != null)
                Object.DestroyImmediate(plain);
            visuals.gameObject.AddComponent<DiagonalDoorVisuals>();
            DiagonalDoorCollider.Attach(prefab, squares);
        }

        // A frame and two sliding panels along local x, centered on the root
        private static void Layout(Transform root, int layer, float w)
        {
            var frame = new MeshBuilder();
            var glow = new MeshBuilder();
            foreach (float s in new[] { -1f, 1f })
            {
                frame.Box(new Vector3(s * (w * 0.5f - Post * 0.5f), Height * 0.5f, 0f), new Vector3(Post, Height, Thickness + 0.04f));
                glow.Box(new Vector3(s * (w * 0.5f - Post - 0.004f), Height * 0.5f - Lintel * 0.5f, 0f), new Vector3(0.006f, Height - Lintel - 0.1f, 0.03f));
            }
            frame.Box(new Vector3(0f, Height - Lintel * 0.5f, 0f), new Vector3(w, Lintel, Thickness + 0.04f));
            frame.Box(new Vector3(0f, 0.01f, 0f), new Vector3(w, 0.02f, Thickness + 0.06f));
            glow.Box(new Vector3(0f, Height - Lintel * 0.5f, -(Thickness * 0.5f + 0.021f)), new Vector3(0.3f, 0.025f, 0.004f));
            glow.Box(new Vector3(0f, Height - Lintel * 0.5f, Thickness * 0.5f + 0.021f), new Vector3(0.3f, 0.025f, 0.004f));
            Material steel = LitMaterials.Get("TFA_DiagDoorFrame", new Color(0.55f, 0.57f, 0.6f), 0.6f, 0.45f);
            Material light = LitMaterials.Get("TFA_DiagDoorGlow", new Color(0.35f, 0.9f, 1f), 0f, 0.5f, new Color(0.5f, 1.5f, 2.2f));
            Add(root, layer, "TFA_DiagDoorFrame", frame, steel, ShadowCastingMode.On);
            Add(root, layer, "TF_NoPaint_DiagDoorGlow", glow, light, ShadowCastingMode.Off);
            float panel = Panel(w);
            var leaf = new MeshBuilder();
            var leafGlass = new MeshBuilder();
            var leafGlow = new MeshBuilder();
            float leafHeight = Height - Lintel - 0.03f;
            leaf.Box(new Vector3(0f, leafHeight * 0.5f + 0.02f, 0f), new Vector3(panel, leafHeight, Thickness * 0.6f));
            leafGlass.Box(new Vector3(0f, leafHeight * 0.62f, 0f), new Vector3(panel * 0.35f, leafHeight * 0.4f, Thickness * 0.62f));
            leafGlow.Box(new Vector3(0f, leafHeight * 0.3f, 0f), new Vector3(panel * 0.7f, 0.02f, Thickness * 0.62f));
            Material panelMaterial = LitMaterials.Get("TFA_DiagDoorPanel", new Color(0.72f, 0.74f, 0.78f), 0.5f, 0.5f);
            Material glass = LitMaterials.Get("TFA_DiagDoorGlass", new Color(0.08f, 0.12f, 0.16f), 0.3f, 0.9f);
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject("TFA_DiagDoorLeaf" + i) { layer = layer };
                go.transform.SetParent(root, false);
                go.transform.localPosition = new Vector3((i == 0 ? -1f : 1f) * panel * 0.5f, 0f, 0f);
                Add(go.transform, layer, "TFA_DiagDoorLeaf", leaf, panelMaterial, ShadowCastingMode.On);
                Add(go.transform, layer, "TF_NoPaint_DiagDoorLeafGlass", leafGlass, glass, ShadowCastingMode.Off);
                Add(go.transform, layer, "TF_NoPaint_DiagDoorLeafGlow", leafGlow, light, ShadowCastingMode.Off);
            }
        }

        /// <summary>One panel's width: half the opening between the posts.</summary>
        public static float Panel(float width) => width * 0.5f - Post;

        private static void Add(Transform parent, int layer, string name, MeshBuilder builder, Material material, ShadowCastingMode shadows)
        {
            if (!builder.HasGeometry || material == null)
                return;
            var child = new GameObject(name) { layer = layer };
            child.transform.SetParent(parent, false);
            child.AddComponent<MeshFilter>().sharedMesh = builder.Build(name, new Vector2(0.125f, 0.5f));
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows;
        }

        // A straight door this player just placed: its layout is asked once it shows up
        private static (uint Ship, Vector3 Coord, float Time)? _pending;

        [HarmonyLib.HarmonyPatch(typeof(Tools.Build.BuildToolLocalClient), "PlacePart")]
        private static class RememberPlaced
        {
            private static void Postfix(Tools.Build.Model.SnappedPreviewPlacement snappedPlacement)
            {
                if (snappedPlacement?.PartSettings == null || !IsDoor(snappedPlacement.PartSettings.id) || !snappedPlacement.FloatingPartPlacement.HasValue)
                    return;
                _pending = BuildTilt.DoorStraight ? (snappedPlacement.ShipId, snappedPlacement.FloatingPartPlacement.Value.Coord, Time.unscaledTime) : ((uint, Vector3, float)?)null;
            }
        }

        /// <summary>True once, for the door this player just placed straight.</summary>
        public static bool TakePending(PartKey door)
        {
            if (!_pending.HasValue || _pending.Value.Ship != door.ShipId || Time.unscaledTime - _pending.Value.Time > 10f)
                return false;
            var ships = Core.Services.ServiceLocator.GetService<Ships.Interface.Services.IShipsProvider>();
            if (ships == null || !ships.TryGetShip(door.ShipId, out Ships.Interface.Model.State.IShipStateRead ship) || !ship.TryGetStatefulPart(door.PartId, out StatefulPart part)
                || (part.LocalPosition - _pending.Value.Coord).sqrMagnitude > 0.01f)
                return false;
            _pending = null;
            return true;
        }

        public static int Decode(IStatefulPartState state) => state is ConstantState constant ? Mathf.Clamp((int)constant.ConstantValue, 0, 3) : 0;

        // ---- Requests: a player opens, closes or turns a door; the host sets its state

        public static void Install() => TFNet.OnServer(TFMessageKind.DoorState, OnRequest);

        public static void Request(PartKey door, int value) =>
            TFNet.SendToServer(TFMessageKind.DoorState, door.ShipId, door.PartId, value.ToString(System.Globalization.CultureInfo.InvariantCulture));

        private static void OnRequest(NetworkConnection sender, TFMessageKind kind, uint shipId, ushort partId, string text)
        {
            if (!int.TryParse(text, out int value) || value < 0 || value > 3)
                return;
            var ships = Common.GameServices.ShipsServer;
            if (ships == null || !(ships.GetShip(shipId) is TrackedShipServer ship))
                return;
            foreach (StatefulPart part in ship.StatefulParts)
            {
                if (part == null || part.Id != partId || part.Settings == null || !IsDoor(part.Settings.id))
                    continue;
                // A door placed before it had a state (0.44) gets one now
                var state = part.State as ConstantState ?? new ConstantState();
                if (state.ConstantValue != value || !(part.State is ConstantState))
                    ship.UpdatePartState(part.Id, state with { ConstantValue = value });
                return;
            }
        }

        // ---- Who is near (seen on this screen): the local player's eyes and the other players' avatars

        private static readonly List<Transform> Avatars = new List<Transform>();
        private static float _nextAvatars;

        public static bool LocalNear(Vector3 center, float reach)
        {
            Camera camera = Camera.main;
            return camera != null && (camera.transform.position - center).sqrMagnitude < reach * reach;
        }

        public static bool SomeoneNear(Vector3 center, float reach)
        {
            if (LocalNear(center, reach))
                return true;
            if (Time.unscaledTime >= _nextAvatars)
            {
                _nextAvatars = Time.unscaledTime + 1f;
                Avatars.Clear();
                foreach (var avatar in Object.FindObjectsOfType<Players.Visuals.OtherPlayerAnimator>())
                    if (avatar != null)
                        Avatars.Add(avatar.transform);
            }
            foreach (Transform avatar in Avatars)
                if (avatar != null && (avatar.position - center).sqrMagnitude < reach * reach)
                    return true;
            return false;
        }
    }

    /// <summary>
    /// The door on screen: the panels slide apart while it is open; this player coming near opens
    /// it, and once they walked away (nobody else near) closes it. As a build preview it shows the
    /// layout chosen with the tilt key.
    /// </summary>
    internal sealed class DiagonalDoorVisuals : Ships.Parts.Common.DefaultShipPartVisuals, Ships.Parts.Common.Model.IContextAwarePart
    {
        public PartKey Key { get; private set; }
        public bool HasContext { get; private set; }

        private int _value, _squares = 1;
        private Transform _diagonal, _straight;
        private readonly Transform[] _leaves = new Transform[2];
        private float _open, _next, _left;
        private bool _wasNear;

        public void SetContext(Ships.Parts.Common.Utils.ShipPartContext context)
        {
            Key = new PartKey(context.Part.ShipId, context.Part.PartId);
            HasContext = true;
        }

        private void Awake()
        {
            _diagonal = transform.Find(DiagonalDoors.DiagonalName);
            _straight = transform.Find(DiagonalDoors.StraightName);
            foreach (Transform child in transform)
                if (child.name.StartsWith(DiagonalDoors.SizePrefix) && int.TryParse(child.name.Substring(DiagonalDoors.SizePrefix.Length), out int squares))
                    _squares = squares;
            Show();
        }

        public override void SetNetworkState(IStatefulPartState state)
        {
            _value = DiagonalDoors.Decode(state);
            Show();
        }

        public override void ReactToNetworkStateChange(StatefulPartStateChangeEvent change) => SetNetworkState(change.NewState);

        private void Show()
        {
            bool straight = (_value & DiagonalDoors.Straight) != 0;
            if (_diagonal != null)
                _diagonal.gameObject.SetActive(!straight);
            if (_straight != null)
                _straight.gameObject.SetActive(straight);
            Transform root = straight ? _straight : _diagonal;
            for (int i = 0; i < 2; i++)
                _leaves[i] = root != null ? root.Find("TFA_DiagDoorLeaf" + i) : null;
        }

        private void LateUpdate()
        {
            if (!HasContext)
            {
                // The build preview: the layout chosen with the wheel
                int preview = BuildTilt.ForDoor() ? DiagonalDoors.Straight : 0;
                if (preview != _value)
                {
                    _value = preview;
                    Show();
                }
            }
            else if ((_value & DiagonalDoors.Straight) == 0 && Time.unscaledTime >= _next && DiagonalDoors.TakePending(Key))
                Ask(_value | DiagonalDoors.Straight);
            bool straight = (_value & DiagonalDoors.Straight) != 0, open = (_value & DiagonalDoors.Open) != 0;
            if (HasContext && Time.unscaledTime >= _next)
            {
                Vector3 center = transform.TransformPoint(DiagonalDoors.Offset(_squares, straight) + Vector3.up);
                float reach = 1.7f + 0.6f * (_squares - 1);
                if (DiagonalDoors.LocalNear(center, reach))
                {
                    _wasNear = true;
                    _left = Time.unscaledTime;
                    if (!open)
                        Ask(_value | DiagonalDoors.Open);
                }
                // The one who came through closes it behind them, once nobody is in the way
                else if (_wasNear && Time.unscaledTime - _left > 1.2f)
                {
                    if (!open)
                        _wasNear = false;
                    else if (!DiagonalDoors.SomeoneNear(center, reach))
                    {
                        _wasNear = false;
                        Ask(_value & ~DiagonalDoors.Open);
                    }
                }
            }
            _open = Mathf.MoveTowards(_open, open ? 1f : 0f, Time.deltaTime * 2.5f);
            float eased = _open * _open * (3f - 2f * _open);
            float panel = DiagonalDoors.Panel(DiagonalDoors.Width(_squares, straight));
            for (int i = 0; i < 2; i++)
            {
                if (_leaves[i] == null)
                    continue;
                // Closed: each panel fills its half; open: slid into the walls beside the door
                _leaves[i].localPosition = new Vector3((i == 0 ? -1f : 1f) * panel * (0.5f + 0.95f * eased), 0f, 0f);
            }
        }

        private void Ask(int value)
        {
            DiagonalDoors.Request(Key, value);
            _next = Time.unscaledTime + 0.5f;
        }
    }

    /// <summary>
    /// The door's collision, in both layouts (the one in use active): the posts and the lintel
    /// always, the panels while it is shut. The game hands it the door's state like its own doors.
    /// </summary>
    internal sealed class DiagonalDoorCollider : MonoBehaviour, IShipPartDynamicCollider
    {
        public GameObject GameObject => gameObject;

        public static void Attach(GameObject prefab, int squares)
        {
            int ship = LayerMask.NameToLayer("Ship");
            var root = new GameObject("TFA_DiagonalDoorColliders") { layer = ship };
            root.transform.SetParent(prefab.transform, false);
            foreach (bool straight in new[] { false, true })
            {
                var layout = new GameObject(straight ? DiagonalDoors.StraightName : DiagonalDoors.DiagonalName) { layer = ship };
                layout.transform.SetParent(root.transform, false);
                layout.transform.localPosition = DiagonalDoors.Offset(squares, straight);
                layout.transform.localRotation = DiagonalDoors.Axis(straight);
                layout.SetActive(!straight);
                float w = DiagonalDoors.Width(squares, straight);
                foreach (float s in new[] { -1f, 1f })
                    Box(layout.transform, ship, "Post", new Vector3(s * (w * 0.5f - DiagonalDoors.Post * 0.5f), DiagonalDoors.Height * 0.5f, 0f), new Vector3(DiagonalDoors.Post, DiagonalDoors.Height, DiagonalDoors.Thickness));
                Box(layout.transform, ship, "Lintel", new Vector3(0f, DiagonalDoors.Height - DiagonalDoors.Lintel * 0.5f, 0f), new Vector3(w, DiagonalDoors.Lintel, DiagonalDoors.Thickness));
                Box(layout.transform, ship, "Shut", new Vector3(0f, (DiagonalDoors.Height - DiagonalDoors.Lintel) * 0.5f, 0f), new Vector3(w - 2f * DiagonalDoors.Post, DiagonalDoors.Height - DiagonalDoors.Lintel, DiagonalDoors.Thickness * 0.6f));
            }
            root.AddComponent<DiagonalDoorCollider>();
        }

        private static void Box(Transform root, int layer, string name, Vector3 center, Vector3 size)
        {
            var piece = new GameObject(name) { layer = layer, tag = "ShipFloatingPart" };
            piece.transform.SetParent(root, false);
            piece.transform.localPosition = center;
            piece.AddComponent<BoxCollider>().size = size;
        }

        public void SetNetworkState(IStatefulPartState state)
        {
            int value = DiagonalDoors.Decode(state);
            bool straight = (value & DiagonalDoors.Straight) != 0, open = (value & DiagonalDoors.Open) != 0;
            foreach (Transform layout in transform)
            {
                layout.gameObject.SetActive(layout.name == (straight ? DiagonalDoors.StraightName : DiagonalDoors.DiagonalName));
                Transform shut = layout.Find("Shut");
                if (shut != null)
                    shut.gameObject.SetActive(!open);
            }
        }

        public void ReactToNetworkStateChange(StatefulPartStateChangeEvent change) => SetNetworkState(change.NewState);
    }
}

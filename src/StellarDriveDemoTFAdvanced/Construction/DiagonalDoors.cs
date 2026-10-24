using System.Collections.Generic;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Interface.Settings;
using Ships.Parts.Common.Model.Reactions;
using StellarDriveDemoTFAdvanced.Common;
using StellarDriveDemoTFAdvanced.Parts;
using UnityEngine;
using UnityEngine.Rendering;
using Loc = StellarDriveDemoTFAdvanced.Common.Loc;
using MeshBuilder = StellarDriveDemoTFAdvanced.Common.MeshBuilder;
using LitMaterials = StellarDriveDemoTFAdvanced.Common.LitMaterials;

namespace StellarDriveDemoTFAdvanced.Construction
{
    /// <summary>
    /// Diagonal doors: the game's doors only fit straight walls, these fit the diagonal ones.
    /// A frame and two sliding panels across the diagonal of one floor square (or of 2 × 2): take
    /// the diagonal wall out, set the door on the floor where it was (turn it to the other
    /// diagonal). It opens by itself when someone comes near and closes behind them. Placed
    /// parts do not seal a room: it does not hold air like the game's doors.
    /// </summary>
    internal static class DiagonalDoors
    {
        public const ushort SmallId = 8233, LargeId = 8234;
        public const float Height = 2f, Thickness = 0.12f, Post = 0.08f, Lintel = 0.12f;

        public static bool IsDoor(ushort id) => id == SmallId || id == LargeId;

        /// <summary>The door's axis: along the diagonal of its square, from (-, -) to (+, +).</summary>
        public static readonly Quaternion Turn = Quaternion.Euler(0f, -45f, 0f);

        public static float Width(int squares) => squares * Mathf.Sqrt(2f);

        public static void Register()
        {
            PartFactory.Register(SmallId, "SignalDisplay", AdvancedTab.ConstructionName, AdvancedTab.SignRow, (settings, prefab) => Configure(1, settings, prefab));
            PartFactory.Register(LargeId, "SignalDisplay", AdvancedTab.ConstructionName, AdvancedTab.SignRow, (settings, prefab) => Configure(2, settings, prefab));
        }

        private static void Configure(int squares, PartSettings settings, GameObject prefab)
        {
            PartFactory.Describe(settings,
                squares == 1 ? Loc.Pick("Porte diagonale", "Diagonal door", "Puerta diagonal") : Loc.Pick("Grande porte diagonale", "Large diagonal door", "Puerta diagonal grande"),
                Loc.Pick($"Une porte coulissante en travers d'un carré de sol{(squares == 2 ? " de 2 × 2" : "")}, pour les murs en diagonale. Enlève le mur diagonal, pose la porte au sol à sa place (tourne-la pour l'autre diagonale). Elle s'ouvre seule quand quelqu'un approche. Ne retient pas l'air comme les portes du jeu.",
                    $"A sliding door across {(squares == 2 ? "a 2 × 2 floor square" : "one floor square")}, for diagonal walls. Remove the diagonal wall, set the door on the floor in its place (turn it for the other diagonal). It opens by itself when someone comes near. It does not hold air like the game's doors.",
                    $"Una puerta corredera en diagonal sobre {(squares == 2 ? "un cuadrado de suelo de 2 × 2" : "un cuadrado de suelo")}, para las paredes diagonales. Quita la pared diagonal, pon la puerta en el suelo en su lugar (gírala para la otra diagonal). Se abre sola cuando alguien se acerca. No retiene el aire como las puertas del juego."),
                squares == 1 ? 40f : 70f, squares == 1 ? new[] { (101u, 4), (102u, 1), (112u, 1) } : new[] { (101u, 7), (102u, 2), (112u, 2) });
            settings.internalStateClass = null;
            settings.snappingStyle = Ships.Interface.Model.SnappingStyle.PreciseOnFloor;
            var size = new Vector3(squares, Height, squares);
            Transform visuals = PartFactory.BlankFromSignalDisplay(prefab, new Vector3(0f, Height * 0.5f, 0f), size);
            int layer = visuals.gameObject.layer;
            float w = Width(squares);
            var frame = new MeshBuilder();
            var glow = new MeshBuilder();
            foreach (float s in new[] { -1f, 1f })
            {
                frame.Box(Turn * new Vector3(s * (w * 0.5f - Post * 0.5f), Height * 0.5f, 0f), new Vector3(Post, Height, Thickness + 0.04f), Turn);
                glow.Box(Turn * new Vector3(s * (w * 0.5f - Post - 0.004f), Height * 0.5f - Lintel * 0.5f, 0f), new Vector3(0.006f, Height - Lintel - 0.1f, 0.03f), Turn);
            }
            frame.Box(Turn * new Vector3(0f, Height - Lintel * 0.5f, 0f), new Vector3(w, Lintel, Thickness + 0.04f), Turn);
            frame.Box(Turn * new Vector3(0f, 0.01f, 0f), new Vector3(w, 0.02f, Thickness + 0.06f), Turn);
            glow.Box(Turn * new Vector3(0f, Height - Lintel * 0.5f, -(Thickness * 0.5f + 0.021f)), new Vector3(0.3f, 0.025f, 0.004f), Turn);
            glow.Box(Turn * new Vector3(0f, Height - Lintel * 0.5f, Thickness * 0.5f + 0.021f), new Vector3(0.3f, 0.025f, 0.004f), Turn);
            Material steel = LitMaterials.Get("TFA_DiagDoorFrame", new Color(0.55f, 0.57f, 0.6f), 0.6f, 0.45f);
            Material light = LitMaterials.Get("TFA_DiagDoorGlow", new Color(0.35f, 0.9f, 1f), 0f, 0.5f, new Color(0.5f, 1.5f, 2.2f));
            Add(visuals, layer, "TFA_DiagDoorFrame", frame, steel, ShadowCastingMode.On);
            Add(visuals, layer, "TF_NoPaint_DiagDoorGlow", glow, light, ShadowCastingMode.Off);
            // The two panels, each in its own object so it can slide (local x along the door)
            float panel = w * 0.5f - Post;
            var leaf = new MeshBuilder();
            var leafGlass = new MeshBuilder();
            var leafGlow = new MeshBuilder();
            float leafHeight = Height - Lintel - 0.03f;
            leaf.Box(new Vector3(0f, leafHeight * 0.5f + 0.02f, 0f), new Vector3(panel, leafHeight, Thickness * 0.6f));
            leafGlass.Box(new Vector3(0f, leafHeight * 0.62f, 0f), new Vector3(panel * 0.35f, leafHeight * 0.4f, Thickness * 0.62f));
            leafGlow.Box(new Vector3(0f, leafHeight * 0.3f, 0f), new Vector3(panel * 0.7f, 0.02f, Thickness * 0.62f));
            Material panelMaterial = LitMaterials.Get("TFA_DiagDoorPanel", new Color(0.72f, 0.74f, 0.78f), 0.5f, 0.5f);
            Material glass = LitMaterials.Get("TFA_DiagDoorGlass", new Color(0.08f, 0.12f, 0.16f), 0.3f, 0.9f);
            var leaves = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject("TFA_DiagDoorLeaf" + i) { layer = layer };
                go.transform.SetParent(visuals, false);
                go.transform.localRotation = Turn;
                Add(go.transform, layer, "TFA_DiagDoorLeaf", leaf, panelMaterial, ShadowCastingMode.On);
                Add(go.transform, layer, "TF_NoPaint_DiagDoorLeafGlass", leafGlass, glass, ShadowCastingMode.Off);
                Add(go.transform, layer, "TF_NoPaint_DiagDoorLeafGlow", leafGlow, light, ShadowCastingMode.Off);
                leaves[i] = go.transform;
            }
            var door = visuals.gameObject.AddComponent<DiagonalDoorVisuals>();
            door.Leaves = leaves;
            door.Panel = panel;
            door.Reach = 1.7f + 0.6f * (squares - 1);
            DiagonalDoorCollider.Attach(prefab, squares);
        }

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

        // ---- Who is near (seen on this screen): the local player's eyes and the other players' avatars

        private static readonly List<Transform> Avatars = new List<Transform>();
        private static float _nextAvatars;

        public static bool SomeoneNear(Vector3 center, float reach)
        {
            Camera camera = Camera.main;
            if (camera != null && (camera.transform.position - center).sqrMagnitude < reach * reach)
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

    /// <summary>The panels on screen: they slide apart along the door when someone is near.</summary>
    internal sealed class DiagonalDoorVisuals : MonoBehaviour
    {
        public Transform[] Leaves;
        public float Panel, Reach;
        private float _open;

        private void LateUpdate()
        {
            if (Leaves == null)
                return;
            bool open = DiagonalDoors.SomeoneNear(transform.TransformPoint(new Vector3(0f, 1f, 0f)), Reach);
            _open = Mathf.MoveTowards(_open, open ? 1f : 0f, Time.deltaTime * 2.5f);
            float eased = _open * _open * (3f - 2f * _open);
            for (int i = 0; i < Leaves.Length; i++)
            {
                if (Leaves[i] == null)
                    continue;
                float side = i == 0 ? -1f : 1f;
                // Closed: each panel fills its half; open: slid into the walls beside the door
                Leaves[i].localPosition = DiagonalDoors.Turn * new Vector3(side * Panel * (0.5f + 0.95f * eased), 0f, 0f);
            }
        }
    }

    /// <summary>
    /// The door's collision: the posts and the lintel always, the panels while it is shut. It
    /// opens for anything moving near it in its physics scene (a player's body), other than its
    /// own ship and other ships.
    /// </summary>
    internal sealed class DiagonalDoorCollider : MonoBehaviour, IShipPartDynamicCollider
    {
        public GameObject GameObject => gameObject;

        private float _reach;
        private BoxCollider _shut;
        private Rigidbody _own;
        private float _next;
        private int _ship;
        private readonly Collider[] _hits = new Collider[16];

        public static void Attach(GameObject prefab, int squares)
        {
            int ship = LayerMask.NameToLayer("Ship");
            var root = new GameObject("TFA_DiagonalDoorColliders") { layer = ship };
            root.transform.SetParent(prefab.transform, false);
            float w = DiagonalDoors.Width(squares);
            foreach (float s in new[] { -1f, 1f })
                Box(root.transform, ship, "Post", new Vector3(s * (w * 0.5f - DiagonalDoors.Post * 0.5f), DiagonalDoors.Height * 0.5f, 0f), new Vector3(DiagonalDoors.Post, DiagonalDoors.Height, DiagonalDoors.Thickness));
            Box(root.transform, ship, "Lintel", new Vector3(0f, DiagonalDoors.Height - DiagonalDoors.Lintel * 0.5f, 0f), new Vector3(w, DiagonalDoors.Lintel, DiagonalDoors.Thickness));
            Box(root.transform, ship, "Shut", new Vector3(0f, (DiagonalDoors.Height - DiagonalDoors.Lintel) * 0.5f, 0f), new Vector3(w - 2f * DiagonalDoors.Post, DiagonalDoors.Height - DiagonalDoors.Lintel, DiagonalDoors.Thickness * 0.6f));
            root.AddComponent<DiagonalDoorCollider>();
        }

        private static void Box(Transform root, int layer, string name, Vector3 center, Vector3 size)
        {
            var piece = new GameObject(name) { layer = layer, tag = "ShipFloatingPart" };
            piece.transform.SetParent(root, false);
            piece.transform.localPosition = DiagonalDoors.Turn * center;
            piece.transform.localRotation = DiagonalDoors.Turn;
            piece.AddComponent<BoxCollider>().size = size;
        }

        private void Start()
        {
            _ship = LayerMask.NameToLayer("Ship");
            Transform shut = transform.Find("Shut");
            _shut = shut != null ? shut.GetComponent<BoxCollider>() : null;
            _own = GetComponentInParent<Rigidbody>();
            // The door's size from its lintel (fields are not copied with the collider object)
            Transform lintel = transform.Find("Lintel");
            float width = lintel != null ? lintel.GetComponent<BoxCollider>().size.x : DiagonalDoors.Width(1);
            _reach = 1.5f + 0.6f * (Mathf.Round(width / DiagonalDoors.Width(1)) - 1f);
        }

        private void FixedUpdate()
        {
            if (_shut == null || Time.time < _next)
                return;
            _next = Time.time + 0.1f;
            bool open = false;
            PhysicsScene physics = gameObject.scene.GetPhysicsScene();
            int count = physics.OverlapSphere(transform.TransformPoint(new Vector3(0f, 1f, 0f)), _reach, _hits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count && !open; i++)
            {
                Collider hit = _hits[i];
                if (hit == null || hit.gameObject.layer == _ship)
                    continue;
                Rigidbody body = hit.attachedRigidbody;
                open = body != null && body != _own;
            }
            if (_shut.enabled == open)
                _shut.enabled = !open;
        }

        public void SetNetworkState(IStatefulPartState state)
        {
        }

        public void ReactToNetworkStateChange(StatefulPartStateChangeEvent change)
        {
        }
    }
}

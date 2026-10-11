using System.Collections.Generic;
using Core.Services;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Services;
using Ships.Interface.Settings;
using Ships.Parts.Common.Model;
using Ships.Parts.Common.Model.Reactions;
using Ships.Parts.Common.Utils;
using StellarDriveDemoTFAdvanced.Common;
using StellarDriveDemoTFAdvanced.Parts;
using UnityEngine;
using UnityEngine.Rendering;

namespace StellarDriveDemoTFAdvanced.Construction
{
    /// <summary>
    /// Control seat with built-in levers: the game's Advanced Controls (sit, fly, six signal outputs)
    /// with two armrest consoles of three levers each. The levers follow the seat's six inputs
    /// (left: move right/up/forward, right: pitch/yaw/roll), the ones sent out on its six sockets.
    /// Only call when SDModKit is loaded.
    /// </summary>
    internal static class ControlSeat
    {
        private const uint Iron = 101, Copper = 112, Aluminum = 104;

        public static void Configure(PartSettings settings, GameObject prefab)
        {
            PartFactory.Describe(settings, "Siège de commande à leviers",
                "Le poste de pilotage avancé du jeu (on s'y assoit, il pilote et sort six signaux sur ses six prises) avec deux consoles d'accoudoir de trois leviers chacune. Les leviers suivent les commandes envoyées : à gauche les déplacements (latéral, vertical, avant), à droite les rotations (tangage, lacet, roulis). Pour piloter une station, une grue ou des portes depuis un fauteuil.",
                40f, new[] { (Iron, 10), (Copper, 6), (Aluminum, 4) });
            GameSkin.Capture(prefab);
            Transform visuals = prefab.GetComponentInChildren<IShipPartVisual>(true)?.GameObject.transform;
            if (visuals == null)
                return;
            int layer = visuals.gameObject.layer;
            var console = new MeshBuilder { Panel = GameSkin.PlainPanel, DoubleSided = true };
            var dark = new MeshBuilder { DoubleSided = true };
            foreach (float side in new[] { -1f, 1f })
            {
                // An armrest console outside each flight stick, a dark slot for the levers
                console.Box(new Vector3(side * 0.42f, 0.6f, -0.12f), new Vector3(0.13f, 0.2f, 0.56f));
                console.Box(new Vector3(side * 0.42f, 0.3f, -0.12f), new Vector3(0.06f, 0.4f, 0.12f));
                dark.Box(new Vector3(side * 0.42f, 0.702f, -0.12f), new Vector3(0.05f, 0.006f, 0.48f));
                for (int i = 0; i < 3; i++)
                {
                    var pivot = new GameObject(SeatLevers.LeverName + (side < 0f ? i : i + 3)) { layer = layer };
                    pivot.transform.SetParent(visuals, false);
                    pivot.transform.localPosition = new Vector3(side * 0.42f, 0.7f, -0.3f + i * 0.17f);
                    var lever = new MeshBuilder { DoubleSided = true };
                    lever.Cylinder(Vector3.zero, new Vector3(0f, 0.14f, 0f), 0.012f, 0.01f, 8);
                    var knob = new MeshBuilder { DoubleSided = true };
                    knob.Cylinder(new Vector3(0f, 0.13f, 0f), new Vector3(0f, 0.17f, 0f), 0.025f, 0.02f, 10);
                    Add(pivot.transform, layer, "TF_NoPaint_SeatLeverStick", lever, LitMaterials.Get("TFA_MachineDark", new Color(0.04f, 0.04f, 0.05f), 0.3f, 0.5f));
                    Color color = side < 0f ? MachineColors.Cyan : MachineColors.Amber;
                    Add(pivot.transform, layer, "TF_NoPaint_SeatLeverKnob", knob, LitMaterials.Get("TFA_SeatKnob" + (side < 0f ? "L" : "R"), color, 0f, 0.6f, color * 0.8f));
                }
            }
            GameSkin.AddMesh(visuals, layer, "TFA_SeatConsoles", console, LitMaterials.Get("TFA_MachineArmor", new Color(0.42f, 0.43f, 0.46f), 0.55f, 0.45f));
            Add(visuals, layer, "TF_NoPaint_SeatSlots", dark, LitMaterials.Get("TFA_MachineDark", new Color(0.04f, 0.04f, 0.05f), 0.3f, 0.5f));
            visuals.gameObject.AddComponent<SeatLevers>();
            var bounds = prefab.GetComponent<ShipPartBounds>();
            if (bounds != null)
                bounds.bounds = new Vector3(Mathf.Max(bounds.bounds.x, 0.98f), bounds.bounds.y, bounds.bounds.z);
        }

        private static void Add(Transform parent, int layer, string name, MeshBuilder builder, Material material)
        {
            var child = new GameObject(name) { layer = layer };
            child.transform.SetParent(parent, false);
            child.AddComponent<MeshFilter>().sharedMesh = builder.Build(name);
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static class MachineColors
        {
            public static readonly Color Cyan = new Color(0.25f, 0.85f, 1f);
            public static readonly Color Amber = new Color(1f, 0.65f, 0.15f);
        }
    }

    /// <summary>Tilts the control seat's six levers with its inputs.</summary>
    internal sealed class SeatLevers : MonoBehaviour, IContextAwarePart
    {
        public const string LeverName = "TFA_SeatLever";
        private const float MaxAngle = 35f;

        private readonly Transform[] _levers = new Transform[6];
        private readonly float[] _angles = new float[6];
        private PartContext _part;
        private bool _hasContext;

        public void SetContext(ShipPartContext context)
        {
            _part = context.Part;
            _hasContext = true;
        }

        private void Awake()
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name.StartsWith(LeverName) && int.TryParse(child.name.Substring(LeverName.Length), out int index) && index >= 0 && index < 6)
                    _levers[index] = child;
            }
        }

        private void Update()
        {
            if (!_hasContext)
                return;
            var ships = ServiceLocator.GetService<IShipsClientProvider>();
            Vector3 move = Vector3.zero, turn = Vector3.zero;
            if (ships != null && ships.TryGetPartState(_part, out PilotSeatAdvState seat))
            {
                move = seat.MoveInput;
                turn = seat.RotationInput;
            }
            float[] inputs = { move.x, move.y, move.z, turn.x, turn.y, turn.z };
            for (int i = 0; i < 6; i++)
            {
                _angles[i] = Mathf.MoveTowards(_angles[i], Mathf.Clamp(inputs[i], -1f, 1f) * MaxAngle, Time.deltaTime * 200f);
                if (_levers[i] != null)
                    _levers[i].localRotation = Quaternion.Euler(_angles[i], 0f, 0f);
            }
        }
    }
}

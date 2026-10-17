using System.Collections.Generic;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.StateTypes;
using StellarDriveDemoTFAdvanced.Common;
using StellarDriveDemoTFAdvanced.Power;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using UnityEngine;
using Loc = StellarDriveDemoTFAdvanced.Common.Loc;
using MainCamera = StellarDriveDemoTFAdvanced.Common.MainCamera;

namespace StellarDriveDemoTFAdvanced.Propulsion
{
    /// <summary>
    /// The landing gear's model: a mounting plate with its hinge, and the leg on its pivot: an
    /// outer cylinder with a diagonal brace, the chrome piston of the shock absorber in a coil
    /// spring, and a foot pad on a ball joint. The pivot, the piston and the spring are separate
    /// objects the visuals move (fold, compress).
    /// </summary>
    internal static class LandingGearModel
    {
        public const string PivotName = "TFA_GearPivot", PistonName = "TFA_GearPiston", SpringName = "TFA_GearSpring";
        // Along the leg from the hinge: the outer cylinder, then the piston coming out of it
        public const float OuterEnd = 0.72f, PistonLength = 0.7f;

        public static void Build(Transform visuals)
        {
            // ---- Fixed: the plate under the hull, the hinge block and its axle caps, status light
            var mount = new MachineModels.Model();
            mount.Armor.Box(new Vector3(0f, 0.025f, 0f), new Vector3(0.66f, 0.05f, 0.66f));
            mount.Armor.Block(new[]
            {
                new Vector3(-0.2f, 0.05f, -0.22f), new Vector3(0.2f, 0.05f, -0.22f), new Vector3(-0.14f, 0.2f, -0.12f), new Vector3(0.14f, 0.2f, -0.12f),
                new Vector3(-0.2f, 0.05f, 0.22f), new Vector3(0.2f, 0.05f, 0.22f), new Vector3(-0.14f, 0.2f, 0.12f), new Vector3(0.14f, 0.2f, 0.12f)
            });
            mount.Dark.Box(new Vector3(0f, 0.051f, 0f), new Vector3(0.6f, 0.002f, 0.6f));
            foreach (float s in new[] { -1f, 1f })
            {
                mount.Armor.Box(new Vector3(s * 0.13f, 0.21f, 0f), new Vector3(0.05f, 0.12f, 0.16f));
                mount.Light.Cylinder(new Vector3(s * 0.155f, LandingGear.Hip.y, 0f), new Vector3(s * 0.165f, LandingGear.Hip.y, 0f), 0.035f, 0.035f, 14);
                foreach (float z in new[] { -0.26f, 0.26f })
                    mount.Dark.Cylinder(new Vector3(s * 0.26f, 0.05f, z), new Vector3(s * 0.26f, 0.058f, z), 0.016f, 0.013f, 8);
            }
            mount.Glow.Box(new Vector3(0f, 0.06f, 0.3f), new Vector3(0.16f, 0.012f, 0.012f));
            MachineModels.Attach(visuals, mount, "GearMount", new Color(0.3f, 1f, 0.4f), new Color(0.8f, 1f, 0.85f));

            // ---- The leg, on its pivot at the hinge (deployed: along +y)
            var pivot = new GameObject(PivotName) { layer = visuals.gameObject.layer };
            pivot.transform.SetParent(visuals, false);
            pivot.transform.localPosition = LandingGear.Hip;
            var leg = new MachineModels.Model();
            leg.Armor.Cylinder(new Vector3(-0.1f, 0f, 0f), new Vector3(0.1f, 0f, 0f), 0.05f, 0.05f, 14);
            leg.Armor.Cylinder(new Vector3(0f, 0.02f, 0f), new Vector3(0f, OuterEnd, 0f), 0.075f, 0.068f, 16);
            leg.Dark.Cylinder(new Vector3(0f, OuterEnd - 0.04f, 0f), new Vector3(0f, OuterEnd + 0.01f, 0f), 0.085f, 0.085f, 16);
            leg.Light.Box(new Vector3(0f, OuterEnd * 0.5f, 0.072f), new Vector3(0.012f, OuterEnd * 0.7f, 0.006f));
            // The brace: from the outer cylinder down to a lug in front of the hinge
            leg.Armor.Box(new Vector3(0f, 0.28f, -0.11f), new Vector3(0.035f, 0.4f, 0.03f), Quaternion.Euler(-14f, 0f, 0f));
            MachineModels.Attach(pivot.transform, leg, "GearLeg", new Color(0.3f, 1f, 0.4f), new Color(0.8f, 1f, 0.85f));

            // The piston (moved along the leg by the visuals), with the foot pad at its end
            var pistonObject = new GameObject(PistonName) { layer = visuals.gameObject.layer };
            pistonObject.transform.SetParent(pivot.transform, false);
            pistonObject.transform.localPosition = new Vector3(0f, OuterEnd - 0.1f, 0f);
            var piston = new MachineModels.Model();
            piston.Light.Cylinder(new Vector3(0f, 0f, 0f), new Vector3(0f, PistonLength, 0f), 0.042f, 0.042f, 14);
            piston.Dark.Cylinder(new Vector3(0f, PistonLength - 0.02f, 0f), new Vector3(0f, PistonLength + 0.04f, 0f), 0.06f, 0.05f, 14);
            // Ball joint and a wide pad
            piston.Armor.Cylinder(new Vector3(0f, PistonLength + 0.04f, 0f), new Vector3(0f, PistonLength + 0.09f, 0f), 0.05f, 0.035f, 12);
            piston.Armor.Cylinder(new Vector3(0f, PistonLength + 0.09f, 0f), new Vector3(0f, PistonLength + 0.13f, 0f), 0.2f, 0.22f, 20);
            piston.Dark.Cylinder(new Vector3(0f, PistonLength + 0.13f, 0f), new Vector3(0f, PistonLength + 0.15f, 0f), 0.22f, 0.21f, 20);
            piston.Glow.Cylinder(new Vector3(0f, PistonLength + 0.115f, 0f), new Vector3(0f, PistonLength + 0.12f, 0f), 0.212f, 0.212f, 20, false, false);
            MachineModels.Attach(pistonObject.transform, piston, "GearPiston", new Color(0.3f, 1f, 0.4f), new Color(0.8f, 1f, 0.85f));

            // The coil spring around the piston, between the outer cylinder and the pad (scaled along the leg)
            var springObject = new GameObject(SpringName) { layer = visuals.gameObject.layer };
            springObject.transform.SetParent(pivot.transform, false);
            springObject.transform.localPosition = new Vector3(0f, OuterEnd, 0f);
            var spring = new MachineModels.Model();
            const int turns = 7, steps = 10;
            Vector3 Coil(float t) => new Vector3(Mathf.Cos(t * Mathf.PI * 2f * turns) * 0.07f, t, Mathf.Sin(t * Mathf.PI * 2f * turns) * 0.07f);
            for (int i = 0; i < turns * steps; i++)
                spring.Glow.Cylinder(Coil(i / (float)(turns * steps)), Coil((i + 1) / (float)(turns * steps)), 0.011f, 0.011f, 6, false, false);
            MachineModels.Attach(springObject.transform, spring, "GearSpring", new Color(1f, 0.6f, 0.15f), new Color(1f, 0.8f, 0.4f));
        }
    }

    /// <summary>
    /// The landing gear on screen: the leg folds out and in (1.5 s) with the state, and while it
    /// is out the piston slides into its cylinder and the spring shortens as the ground pushes on
    /// the pad (the client casts its own ray, like the host does for the force).
    /// </summary>
    internal sealed class LandingGearVisuals : UsablePart
    {
        private Transform _pivot, _piston, _spring;
        private Vector3 _pistonRest;
        private bool _deployed;
        private float _out, _compression;
        private Renderer _light;
        private MaterialPropertyBlock _block;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"), EmissionColorId = Shader.PropertyToID("_EmissionColor");

        public override string Label => _deployed ? Loc.Pick("Rentrer le train d'atterrissage", "Retract the landing gear", "Recoger el tren de aterrizaje") : Loc.Pick("Sortir le train d'atterrissage", "Deploy the landing gear", "Desplegar el tren de aterrizaje");

        public override void Use() => LandingGear.Toggle(Key.ShipId);

        protected override void Awake()
        {
            base.Awake();
            _pivot = transform.Find(LandingGearModel.PivotName);
            _piston = _pivot != null ? _pivot.Find(LandingGearModel.PistonName) : null;
            _spring = _pivot != null ? _pivot.Find(LandingGearModel.SpringName) : null;
            if (_piston != null)
                _pistonRest = _piston.localPosition;
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
                if (renderer.name.EndsWith("GearMountGlow"))
                    _light = renderer;
            _block = new MaterialPropertyBlock();
            Pose();
        }

        public override void SetNetworkState(IStatefulPartState state)
        {
            _deployed = LandingGear.Deployed(state);
            if (!HasContext)
                _out = _deployed ? 1f : 0f;
        }

        public override void ReactToNetworkStateChange(StatefulPartStateChangeEvent change) => SetNetworkState(change.NewState);

        private void LateUpdate()
        {
            if (_pivot == null)
                return;
            float target = _deployed ? 1f : 0f;
            bool moving = !Mathf.Approximately(_out, target);
            _out = Mathf.MoveTowards(_out, target, Time.deltaTime / 1.5f);
            float compression = 0f;
            if (HasContext && _out > 0.99f)
            {
                Camera camera = MainCamera.Get();
                if (camera == null || (camera.transform.position - transform.position).sqrMagnitude < 150f * 150f)
                {
                    Vector3 hip = transform.TransformPoint(LandingGear.Hip);
                    float distance = LandingGear.Probe(WorldTracking.Interface.Values.ClientScene.PhysicsScene, hip, transform.up, transform.root);
                    compression = Mathf.Clamp(LandingGear.Rest - distance, 0f, LandingGear.Travel);
                }
            }
            _compression = Mathf.Lerp(_compression, compression, 1f - Mathf.Exp(-Time.deltaTime * 15f));
            Pose();
            Color light = moving ? (Mathf.Repeat(Time.time, 0.5f) < 0.25f ? new Color(1f, 0.6f, 0.1f) : new Color(0.1f, 0.1f, 0.1f)) : _deployed ? new Color(0.3f, 1f, 0.4f) : new Color(0.1f, 0.1f, 0.1f);
            if (_light != null)
            {
                _light.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, light);
                _block.SetColor(EmissionColorId, light.maxColorComponent > 0.2f ? light * 2.5f : Color.black);
                _light.SetPropertyBlock(_block);
            }
        }

        // Folded flat along -z when in; the piston pushed in by the compression
        private void Pose()
        {
            if (_pivot == null)
                return;
            float eased = Mathf.SmoothStep(0f, 1f, _out);
            _pivot.localRotation = Quaternion.Euler((1f - eased) * -90f, 0f, 0f);
            if (_piston != null)
                _piston.localPosition = _pistonRest - Vector3.up * _compression;
            if (_spring != null)
            {
                // From the cylinder's end to the pad: 0.69 m extended
                float length = _pistonRest.y + LandingGearModel.PistonLength + 0.09f - LandingGearModel.OuterEnd - _compression;
                _spring.localScale = new Vector3(1f, Mathf.Max(0.05f, length), 1f);
            }
        }
    }
}

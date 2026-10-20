using System.Collections.Generic;
using Core.Services;
using Ships;
using Ships.Interface.Model;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.State;
using Ships.Parts.Common;
using Ships.Interface.Services;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;
using UnityEngine.Rendering;
using WorldTracking.Interface.Model.Spaces.Common;
using Ships.Interface.Model.Parts.StateTypes;
using WorldTracking.Interface.Services;
using WorldTracking.Interface.Values;

namespace StellarDriveDemoTFAdvanced.Power
{
    /// <summary>
    /// The portal generator's look in play. While it charges its two gimbal rings spin up around
    /// the floating crystal, faster and brighter with the charge, and its ring lights up; when the
    /// portal opens a <see cref="PortalRift"/> stands where the host opened it, fixed in space (it
    /// does not follow the ship), and closes when the portal does.
    /// </summary>
    internal sealed class PortalVisuals : DefaultShipPartVisuals
    {
        public const string RingName = "TF_NoPaint_PortalRing";
        public const string GimbalAName = "TF_NoPaint_PortalGimbalA", GimbalBName = "TF_NoPaint_PortalGimbalB", CrystalName = "TF_NoPaint_PortalCrystal";
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private readonly List<Renderer> _glows = new List<Renderer>();
        private MaterialPropertyBlock _block;
        private Transform _gimbalA, _gimbalB, _crystal;
        private Vector3 _crystalAt;
        private double _value;
        private float _charge = -1f, _shown = -1f;
        private float _spinA, _spinB;
        private PortalRift _rift;
        private bool _open;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            Transform ring = transform.Find(RingName);
            if (ring != null && ring.GetComponent<Renderer>() != null)
                _glows.Add(ring.GetComponent<Renderer>());
            _gimbalA = transform.Find(GimbalAName);
            // (the inner gimbal turns inside the outer one)
            _gimbalB = _gimbalA != null ? _gimbalA.Find(GimbalBName) : null;
            _crystal = transform.Find(CrystalName);
            if (_crystal != null)
                _crystalAt = _crystal.localPosition;
            // The gimbals' lit runes and the crystal take the charge's colour too
            foreach (Transform moving in new[] { _gimbalA, _gimbalB, _crystal })
            {
                if (moving == null)
                    continue;
                foreach (Renderer renderer in moving.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.name.EndsWith("Glow"))
                        _glows.Add(renderer);
                }
            }
            _charge = 0f;
        }

        public override void SetNetworkState(IStatefulPartState state)
        {
            if (!(state is ConstantState constant))
                return;
            _value = constant.ConstantValue;
            bool open = _value >= Portal.Open;
            if (open && !_open)
                Anchor();
            else if (!open && _open && _rift != null)
                _rift.Close();
            _open = open;
        }

        public override void ReactToNetworkStateChange(StatefulPartStateChangeEvent change) => SetNetworkState(change.NewState);

        private void Update()
        {
            float target = _value >= Portal.Open ? 1f : _value > 0 ? (float)System.Math.Min(_value, 100) / 100f : 0f;
            // Spins up with the charge, winds down slowly once the portal is gone
            _charge = Mathf.MoveTowards(_charge, target, Time.deltaTime * (target > _charge ? 1.5f : 0.25f));
            float drive = _charge * _charge;
            float time = Time.time;
            if (_gimbalA != null)
            {
                _spinA += Time.deltaTime * (12f + 640f * drive);
                _gimbalA.localRotation = Quaternion.Euler(0f, _spinA, 0f);
            }
            if (_gimbalB != null)
            {
                _spinB += Time.deltaTime * (8f + 470f * drive);
                _gimbalB.localRotation = Quaternion.Euler(_spinB, 0f, 0f);
            }
            if (_crystal != null)
            {
                _crystal.localPosition = _crystalAt + Vector3.up * (0.025f * Mathf.Sin(time * 1.8f));
                _crystal.localRotation = Quaternion.Euler(0f, time * (40f + 300f * drive), 0f);
                float throb = 1f + 0.18f * _charge * Mathf.Sin(time * (6f + 20f * _charge));
                _crystal.localScale = Vector3.one * throb;
            }
            // Colour: dim at rest, violet with the charge, flickering when nearly full
            float flicker = _charge > 0.85f ? 0.75f + 0.5f * Mathf.PerlinNoise(time * 14f, 0.3f) : 1f;
            float shown = Mathf.Round(_charge * 100f) + (flicker != 1f ? Mathf.Round(flicker * 20f) * 1000f : 0f);
            if (shown == _shown)
                return;
            _shown = shown;
            Color color = Color.Lerp(new Color(0.08f, 0.04f, 0.12f), MachineModels.Violet, _charge);
            foreach (Renderer renderer in _glows)
            {
                if (renderer == null)
                    continue;
                renderer.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, color);
                _block.SetColor(EmissionColorId, color * (1f + 4f * _charge) * flicker);
                renderer.SetPropertyBlock(_block);
            }
        }

        // Where the portal stands, in the space of the ship that opened it
        private void Anchor()
        {
            Transform root = transform.root;
            if (root == null || !root.name.StartsWith("ship "))
                return;
            string[] words = root.name.Split(' ');
            if (words.Length < 2 || !uint.TryParse(words[1], out uint shipId))
                return;
            var ships = ServiceLocator.GetService<IShipsClientProvider>();
            var spaces = ServiceLocator.GetService<ISpacesClientProvider>();
            var transpose = ServiceLocator.GetService<IWorldTransposeClient>();
            if (ships == null || spaces == null || transpose == null || ClientOrigin.LocalVisualSpace == null)
                return;
            uint spaceId = ships.TryGetRootShip(shipId, out IShipStateRead rootShip) ? rootShip.SpaceId : 0;
            if (!spaces.TryGetSpace(spaceId, out ISpace space))
                return;
            Vector3 center = transform.position + transform.forward * Mathf.Max(5f, PowerCatalog.PortalDistance.Value);
            if (_rift == null)
                _rift = PortalRift.Create(gameObject.layer);
            _rift.Open(space, spaceId, transpose.Position(ClientOrigin.LocalVisualSpace, space, center),
                transpose.Position(ClientOrigin.LocalVisualSpace, space, center + transform.forward), shipId);
        }

        // The ship went through (or out of sight): the portal stays to close on its own, then goes
        private void OnDestroy()
        {
            if (_rift != null)
                _rift.Release();
        }
    }

    /// <summary>
    /// The open portal, fixed in space. It opens in steps: sixteen frame segments fly in from
    /// around and lock into a ring with a flash, then the event horizon swells out of the middle:
    /// two swirls turning against each other, ripples running out to the rim, sparks spiralling
    /// in from the edge, a slowly turning rune ring and a violet glow lighting what is near. It
    /// closes the other way: the horizon collapses into a flash, the segments fly apart.
    /// </summary>
    internal sealed class PortalRift : MonoBehaviour
    {
        private const int Segments = 16, Ripples = 3;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static Mesh _segmentArmor, _segmentGlow, _segmentNode, _runeRing, _disc;
        private static Material _frame, _frameGlow, _nodeGlow, _runes, _swirlA, _swirlB, _ripple, _halo, _flash, _spark;

        private readonly Transform[] _segments = new Transform[Segments];
        private readonly Renderer[] _ripples = new Renderer[Ripples];
        private Transform _horizon, _swirlFront, _swirlBack, _runeRingObject;
        private Renderer _haloRenderer, _flashRenderer;
        private Transform _haloObject, _flashObject;
        private Light _light;
        private ParticleSystem _sparks;
        private MaterialPropertyBlock _block;
        private ISpace _space;
        private uint _spaceId, _shipId;
        private Vector3 _center, _tip;
        // The portal in the frame of the ship that opened it, kept every frame to carry it over
        // when that ship's space changes (it gets under way)
        private Vector3 _fromShip, _normalFromShip;
        private bool _hasFromShip;
        private float _openedAt, _closedAt = -1f;
        private bool _released;

        public static PortalRift Create(int layer)
        {
            Prepare();
            var root = new GameObject("TFA_Portal") { layer = layer };
            PortalRift rift = root.AddComponent<PortalRift>();
            rift.Build(layer);
            return rift;
        }

        public void Open(ISpace space, uint spaceId, Vector3 center, Vector3 tip, uint shipId)
        {
            _space = space;
            _spaceId = spaceId;
            _shipId = shipId;
            _hasFromShip = false;
            _center = center;
            _tip = tip;
            _openedAt = Time.time;
            _closedAt = -1f;
            gameObject.SetActive(true);
            if (_sparks != null)
            {
                _sparks.Clear(true);
                _sparks.Play(true);
            }
        }

        // The ship's space was replaced: the portal is put back where it stood next to the ship, in its new space
        private bool Reanchor(ISpacesClientProvider spaces, Transform ship)
        {
            var ships = ServiceLocator.GetService<IShipsClientProvider>();
            var transpose = GameServices.Transpose;
            if (_closedAt >= 0f || !_hasFromShip || ship == null || ships == null || transpose == null || !ships.TryGetRootShip(_shipId, out IShipStateRead root)
                || !spaces.TryGetSpace(root.SpaceId, out ISpace space))
                return false;
            Vector3 center = ship.TransformPoint(_fromShip), normal = ship.TransformDirection(_normalFromShip);
            _space = space;
            _spaceId = root.SpaceId;
            _center = transpose.Position(ClientOrigin.LocalVisualSpace, space, center);
            _tip = transpose.Position(ClientOrigin.LocalVisualSpace, space, center + normal);
            return true;
        }

        public void Close()
        {
            if (_closedAt < 0f)
                _closedAt = Time.time;
        }

        /// <summary>Its generator is gone: it closes, then destroys itself.</summary>
        public void Release()
        {
            _released = true;
            if (!gameObject.activeSelf)
                Destroy(gameObject);
            else
                Close();
        }

        // ------------------------------------------------------------------ building

        private static void Prepare()
        {
            if (_disc != null)
                return;
            // One frame segment, built around its own middle (the ring's +X at radius 1)
            float half = 360f / Segments * 0.5f - 1.6f;
            var armor = new MeshBuilder { DoubleSided = true };
            var glow = new MeshBuilder { DoubleSided = true };
            var node = new MeshBuilder { DoubleSided = true };
            Arc(armor, 0.985f, 1.12f, -0.07f, 0.07f, -half, half, 5);
            Arc(armor, 1.12f, 1.17f, -0.045f, 0.045f, -half * 0.55f, half * 0.55f, 3);
            Arc(armor, 0.95f, 0.985f, -0.03f, 0.03f, -half * 0.3f, half * 0.3f, 2);
            Arc(glow, 1.04f, 1.065f, 0.07f, 0.085f, -half * 0.9f, half * 0.9f, 5);
            Arc(glow, 1.04f, 1.065f, -0.085f, -0.07f, -half * 0.9f, half * 0.9f, 5);
            node.Box(new Vector3(0f, 0.19f, 0f), new Vector3(0.06f, 0.05f, 0.06f));
            node.Cylinder(new Vector3(0f, -0.035f, 0.035f), new Vector3(0f, -0.035f, 0.05f), 0.018f, 0.018f, 8);
            _segmentArmor = armor.Build("TFA_PortalSegment");
            _segmentGlow = glow.Build("TFA_PortalSegmentGlow");
            _segmentNode = node.Build("TFA_PortalSegmentNode");

            // A band of glowing glyphs (Common/RuneTexture) inside the segments
            _runeRing = RuneTexture.Ring("TFA_PortalRunes", 0.85f, 0.98f, 0f, 128, 3f);

            var disc = new MeshBuilder();
            disc.Cylinder(new Vector3(0f, 0f, -0.002f), new Vector3(0f, 0f, 0.002f), 1f, 1f, 64);
            _disc = disc.Build("TFA_PortalDisc");

            _frame = LitMaterials.Get("TFA_PortalFrame", new Color(0.24f, 0.2f, 0.3f), 0.6f, 0.6f);
            _frameGlow = LitMaterials.Get("TFA_PortalFrameGlow", MachineModels.Violet * 0.7f, 0f, 0.8f, MachineModels.Violet * 2.2f);
            _nodeGlow = LitMaterials.Get("TFA_PortalNodeGlow", new Color(0.9f, 0.75f, 1f), 0f, 0.9f, new Color(0.9f, 0.7f, 1f) * 3.5f);
            _runes = TransparentMaterials.Additive("TFA_PortalRunesTex", RuneTexture.Strip);
            if (_runes != null)
                _runes.SetColor("_BaseColor", new Color(0.75f, 0.5f, 1f) * 1.6f);
            else
                _runes = LitMaterials.Get("TFA_PortalRunes", new Color(0.7f, 0.5f, 1f), 0f, 0.8f, new Color(0.75f, 0.55f, 1f) * 2.5f);
            _swirlA = TransparentMaterials.Additive("TFA_PortalSwirl", Swirl(3, 14f, new Color(0.22f, 0.04f, 0.55f), new Color(0.7f, 0.45f, 1f)));
            _swirlB = TransparentMaterials.Additive("TFA_PortalSwirlB", Swirl(5, 22f, new Color(0.05f, 0.2f, 0.5f), new Color(0.85f, 0.6f, 1f)));
            _ripple = TransparentMaterials.Additive("TFA_PortalRipple", Ring());
            _halo = TransparentMaterials.Additive("TFA_PortalHalo", Glow(2.2f));
            _flash = TransparentMaterials.Additive("TFA_PortalFlash", Glow(4f));
            _spark = TransparentMaterials.Additive("TFA_PortalSpark", Glow(3f));
        }

        private void Build(int layer)
        {
            _block = new MaterialPropertyBlock();
            Transform root = transform;
            for (int i = 0; i < Segments; i++)
            {
                Transform segment = Child(root, "Segment" + i, layer);
                Mesh(segment, "Armor", _segmentArmor, _frame, layer);
                Mesh(segment, "Glow", _segmentGlow, _frameGlow, layer);
                Mesh(segment, "Node", _segmentNode, _nodeGlow, layer);
                _segments[i] = segment;
            }
            _runeRingObject = Child(root, "Runes", layer);
            Mesh(_runeRingObject, "Marks", _runeRing, _runes, layer);
            _horizon = Child(root, "Horizon", layer);
            _swirlBack = Child(_horizon, "SwirlBack", layer);
            _swirlBack.localPosition = new Vector3(0f, 0f, -0.02f);
            Mesh(_swirlBack, "Disc", _disc, _swirlA, layer);
            _swirlFront = Child(_horizon, "SwirlFront", layer);
            _swirlFront.localPosition = new Vector3(0f, 0f, 0.01f);
            Mesh(_swirlFront, "Disc", _disc, _swirlB, layer);
            for (int i = 0; i < Ripples; i++)
            {
                Transform ripple = Child(_horizon, "Ripple" + i, layer);
                ripple.localPosition = new Vector3(0f, 0f, 0.02f + 0.005f * i);
                _ripples[i] = Mesh(ripple, "Disc", _disc, _ripple, layer);
            }
            _haloObject = Child(root, "Halo", layer);
            _haloObject.localPosition = new Vector3(0f, 0f, -0.05f);
            _haloRenderer = Mesh(_haloObject, "Disc", _disc, _halo, layer);
            _flashObject = Child(root, "Flash", layer);
            _flashObject.localPosition = new Vector3(0f, 0f, 0.04f);
            _flashRenderer = Mesh(_flashObject, "Disc", _disc, _flash, layer);

            Transform lightObject = Child(root, "Light", layer);
            lightObject.localPosition = new Vector3(0f, 0f, 0.3f);
            _light = lightObject.gameObject.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = new Color(0.75f, 0.45f, 1f);
            _light.shadows = LightShadows.None;
            _light.intensity = 0f;
            _sparks = Sparks(root, layer);
        }

        private static Transform Child(Transform parent, string name, int layer)
        {
            var child = new GameObject(name) { layer = layer };
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static Renderer Mesh(Transform parent, string name, Mesh mesh, Material material, int layer)
        {
            Transform child = Child(parent, name, layer);
            child.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = child.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        /// <summary>Sparks born on the rim, spiralling into the heart.</summary>
        private ParticleSystem Sparks(Transform root, int layer)
        {
            if (_spark == null)
                return null;
            Transform holder = Child(root, "Sparks", layer);
            holder.localPosition = new Vector3(0f, 0f, 0.03f);
            var sparks = holder.gameObject.AddComponent<ParticleSystem>();
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = sparks.main;
            main.loop = true;
            main.duration = 5f;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.3f, 2.1f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.03f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 0.55f, 1f), new Color(0.55f, 0.8f, 1f));
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 600;
            ParticleSystem.ShapeModule shape = sparks.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1f;
            shape.radiusThickness = 0.05f;
            ParticleSystem.VelocityOverLifetimeModule velocity = sparks.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f);
            velocity.radial = new ParticleSystem.MinMaxCurve(-0.55f);
            velocity.orbitalZ = new ParticleSystem.MinMaxCurve(1.4f);
            ParticleSystem.ColorOverLifetimeModule fade = sparks.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) };
            gradient.alphaKeys = new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0.8f, 0.75f), new GradientAlphaKey(0f, 1f) };
            fade.color = new ParticleSystem.MinMaxGradient(gradient);
            ParticleSystem.SizeOverLifetimeModule size = sparks.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.3f)));
            ParticleSystem.EmissionModule emission = sparks.emission;
            emission.rateOverTime = 0f;
            var renderer = holder.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = _spark;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return sparks;
        }

        // ------------------------------------------------------------------ animation

        private void LateUpdate()
        {
            float now = Time.time;
            float t = now - _openedAt;
            float c = _closedAt >= 0f ? now - _closedAt : -1f;
            if (c > 1.6f)
            {
                if (_released)
                    Destroy(gameObject);
                else
                    gameObject.SetActive(false);
                return;
            }
            var transpose = GameServices.Transpose;
            if (transpose == null || _space == null || ClientOrigin.LocalVisualSpace == null)
                return;
            Vector3 center, normal;
            Transform ship = ShipVisuals.Find(_shipId);
            var spaces = ServiceLocator.GetService<ISpacesClientProvider>();
            if (spaces != null && !spaces.TryGetSpace(_spaceId, out _) && !Reanchor(spaces, ship))
            {
                // Its space is gone and its ship too
                Destroy(gameObject);
                return;
            }
            try
            {
                center = transpose.Position(_space, ClientOrigin.LocalVisualSpace, _center);
                normal = (transpose.Position(_space, ClientOrigin.LocalVisualSpace, _tip) - center).normalized;
            }
            catch
            {
                Destroy(gameObject);
                return;
            }
            if (ship != null)
            {
                _fromShip = ship.InverseTransformPoint(center);
                _normalFromShip = ship.InverseTransformDirection(normal);
                _hasFromShip = true;
            }
            float radius = Mathf.Max(2f, PowerCatalog.PortalRadius.Value);
            transform.position = center;
            transform.rotation = Quaternion.LookRotation(normal);
            transform.localScale = Vector3.one * radius;

            // The frame: each segment flies in from afar and turns into place, one after another
            float locked = 1f;
            for (int i = 0; i < Segments; i++)
            {
                float arrive = EaseOut(Mathf.Clamp01((t - i * 0.025f) / 0.8f));
                float leave = c < 0f ? 0f : EaseIn(Mathf.Clamp01((c - 0.35f - (i % 4) * 0.04f) / 0.8f));
                float away = (1f - arrive) + leave;
                locked = Mathf.Min(locked, arrive);
                float angle = i * 360f / Segments + 90f;
                float swing = (1f - arrive) * 110f - leave * 70f;
                Transform segment = _segments[i];
                Quaternion turn = Quaternion.Euler(0f, 0f, angle + swing);
                segment.localRotation = turn * Quaternion.Euler(0f, 0f, -90f);
                segment.localPosition = turn * Vector3.right * (1f + 1.8f * away) + Vector3.forward * (away * 0.6f);
                segment.localScale = Vector3.one * (1f - leave);
            }

            // The flashes: when the frame locks, and when the horizon collapses
            float flash = Pulse(t - 1.2f, 0.16f) + (c >= 0f ? Pulse(c - 0.42f, 0.14f) * 1.2f : 0f);

            // The event horizon swells out of the heart, overshooting a little, and falls back into it on closing
            float horizon = EaseOutBack(Mathf.Clamp01((t - 1.1f) / 1.1f));
            if (c >= 0f)
                horizon *= 1f - EaseIn(Mathf.Clamp01(c / 0.45f));
            float wobble = 1f + 0.015f * Mathf.Sin(now * 6f) + 0.01f * Mathf.Sin(now * 2.3f);
            _horizon.localScale = new Vector3(horizon * wobble, horizon * wobble, 1f);
            _swirlBack.localRotation = Quaternion.Euler(0f, 0f, -now * 65f);
            _swirlFront.localRotation = Quaternion.Euler(0f, 0f, now * 38f);
            _swirlFront.localScale = new Vector3(0.96f, 0.96f, 1f);
            for (int i = 0; i < Ripples; i++)
            {
                float phase = Mathf.Repeat(now / 2.4f + i / (float)Ripples, 1f);
                _ripples[i].transform.localScale = new Vector3(0.12f + 0.86f * phase, 0.12f + 0.86f * phase, 1f);
                Tint(_ripples[i], new Color(0.75f, 0.55f, 1f) * (Mathf.Sin(phase * Mathf.PI) * 0.7f));
            }

            float shown = Mathf.Clamp01(horizon);
            _runeRingObject.localScale = Vector3.one * Mathf.SmoothStep(0f, 1f, Mathf.Min(locked, shown + (c >= 0f ? 0f : 0.2f)));
            _runeRingObject.localRotation = Quaternion.Euler(0f, 0f, now * 12f);
            _haloObject.localScale = new Vector3(1.9f, 1.9f, 1f) * (0.6f + 0.4f * shown);
            Tint(_haloRenderer, new Color(0.55f, 0.25f, 1f) * (0.28f * shown + 0.5f * flash));
            _flashObject.gameObject.SetActive(flash > 0.01f);
            if (flash > 0.01f)
            {
                _flashObject.localScale = new Vector3(0.5f + 0.9f * flash, 0.5f + 0.9f * flash, 1f);
                Tint(_flashRenderer, new Color(0.95f, 0.85f, 1f) * flash);
            }
            _light.range = radius * 5f;
            _light.intensity = 4f * shown + 10f * flash;
            if (_sparks != null)
            {
                ParticleSystem.EmissionModule emission = _sparks.emission;
                emission.rateOverTime = c >= 0f ? 0f : 240f * shown;
            }
        }

        private void Tint(Renderer renderer, Color color)
        {
            renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, new Color(color.r, color.g, color.b, 1f));
            renderer.SetPropertyBlock(_block);
        }

        private static float EaseOut(float x) => 1f - (1f - x) * (1f - x) * (1f - x);
        private static float EaseIn(float x) => x * x * x;
        private static float EaseOutBack(float x)
        {
            const float k = 1.7f;
            float y = x - 1f;
            return 1f + (k + 1f) * y * y * y + k * y * y;
        }

        /// <summary>A bell around 0, this wide (seconds).</summary>
        private static float Pulse(float x, float width) => Mathf.Exp(-(x * x) / (width * width));

        // ------------------------------------------------------------------ meshes and textures

        /// <summary>A curved block: radii r0-r1, depth z0-z1, from angle a0 to a1 (degrees, 0 = +Y), in steps.</summary>
        private static void Arc(MeshBuilder m, float r0, float r1, float z0, float z1, float a0, float a1, int steps)
        {
            for (int s = 0; s < steps; s++)
            {
                float b0 = Mathf.Lerp(a0, a1, s / (float)steps) * Mathf.Deg2Rad, b1 = Mathf.Lerp(a0, a1, (s + 1) / (float)steps) * Mathf.Deg2Rad;
                Vector3 P(float a, float r, float z) => new Vector3(-Mathf.Sin(a) * r, Mathf.Cos(a) * r - 1f, z);
                m.Block(new[] { P(b0, r0, z0), P(b1, r0, z0), P(b0, r1, z0), P(b1, r1, z0), P(b0, r0, z1), P(b1, r0, z1), P(b0, r1, z1), P(b1, r1, z1) });
            }
        }

        /// <summary>A spiral of arms fading to the rim, brighter at the heart.</summary>
        private static Texture2D Swirl(int arms, float twist, Color outer, Color inner)
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "TFA_PortalSwirl" + arms, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float angle = Mathf.Atan2(v, u);
                    float wave = 0.5f + 0.5f * Mathf.Sin(angle * arms + r * twist);
                    float armShape = Mathf.Pow(wave, 2.2f);
                    float grain = 0.75f + 0.25f * Mathf.PerlinNoise(u * 6f + 10f, v * 6f + 10f);
                    float heart = Mathf.Clamp01(1f - r * 1.7f);
                    float edge = Mathf.Clamp01((1f - r) * 7f);
                    float a = Mathf.Clamp01((armShape * 0.85f * grain + heart * heart * 1.4f) * edge);
                    Color c = Color.Lerp(outer, inner, Mathf.Clamp01(heart * 1.3f + armShape * 0.25f));
                    pixels[y * size + x] = new Color(c.r, c.g, c.b, a);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(true);
            return texture;
        }

        /// <summary>A thin bright ring near the rim (ripples).</summary>
        private static Texture2D Ring()
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "TFA_PortalRipple", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float band = Mathf.Exp(-Mathf.Pow((r - 0.9f) / 0.035f, 2f)) + 0.15f * Mathf.Exp(-Mathf.Pow((r - 0.8f) / 0.08f, 2f));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(r < 1f ? band : 0f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(true);
            return texture;
        }

        /// <summary>A soft round glow, falling off with this power (halo, flash, sparks).</summary>
        private static Texture2D Glow(float power)
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "TFA_PortalGlow" + power, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - r), power));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(true);
            return texture;
        }
    }
}

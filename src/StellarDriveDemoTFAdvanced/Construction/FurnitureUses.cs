using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FishNet.Connection;
using HarmonyLib;
using Players;
using Ships;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Parts.StateTypes;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using UnityEngine;
using UnityEngine.Rendering;
using LitMaterials = StellarDriveDemoTFAdvanced.Common.LitMaterials;
using Loc = StellarDriveDemoTFAdvanced.Common.Loc;

namespace StellarDriveDemoTFAdvanced.Construction
{
    /// <summary>
    /// What the furniture does: the food synthesizer serves a meal that heals, the server rack
    /// relays the ship's radio, the shower and the sink run water, the floor marking is lit, blinks
    /// or is off, the wall screens scroll data and the vents' fans turn. The switches are the part's
    /// ConstantState (shared and saved by the game; the host writes it). Water and fans only run
    /// near this player and in sight; the screens and the blinking share one material each.
    /// </summary>
    internal static class FurnitureUses
    {
        public const ushort ServerRack = 8243, Synthesizer = 8249, Shower = 8265, Sink = 8266, Marking = 8271, Vent = 8275;
        private const float MealReach = 4f, MealCooldown = 60f;

        /// <summary>The pieces with a state: the synthesizer (to be found by the host), the water and the marking (their switch).</summary>
        public static bool HasState(ushort id) => id == Synthesizer || id == Shower || id == Sink || id == Marking;

        private static int Modes(ushort id) => id == Marking ? 3 : id == Shower || id == Sink ? 2 : 0;

        /// <summary>The visual behaviour of a piece one uses with the use key, null for the others.</summary>
        public static System.Type VisualsFor(ushort id)
        {
            if (TFItems.Devices.Seats.IsBed(id))
                return typeof(TFItems.Devices.BedVisuals);
            if (TFItems.Devices.Seats.IsSeat(id))
                return typeof(TFItems.Devices.SeatVisuals);
            switch (id)
            {
                case Synthesizer: return typeof(MealVisuals);
                case ServerRack: return typeof(TFItems.Devices.RelayVisuals);
                case Shower:
                case Sink: return typeof(WaterVisuals);
                case Marking: return typeof(MarkingVisuals);
                default: return null;
            }
        }

        public static bool Spins(ushort id) => id == Vent;

        public static void Install()
        {
            TFNet.OnServer(TFMessageKind.Meal, OnMeal);
            TFNet.OnServer(TFMessageKind.FurnitureMode, OnMode);
            TFNet.OnClient(TFMessageKind.MealResult, (kind, a, b, text) => Warn(text));
        }

        public static void Warn(string text) =>
            Core.Services.ServiceLocator.GetService<UI.Interface.Services.IWarningsDisplay>()?.ShowTempWarning(text);

        // ---- The meal (the host heals the player who asked, near the synthesizer, once a minute)

        public static void AskMeal(PartKey synthesizer) => TFNet.SendToServer(TFMessageKind.Meal, synthesizer.ShipId, synthesizer.PartId, "");

        private static readonly System.Reflection.MethodInfo NetworkState = AccessTools.PropertyGetter(typeof(TrackedPlayer), "NetworkState");
        private static readonly Dictionary<int, float> LastMeal = new Dictionary<int, float>();

        private static void OnMeal(NetworkConnection sender, TFMessageKind kind, uint shipId, ushort partId, string text)
        {
            TrackedPlayerServer player = sender == null ? null : GameServices.PlayersServer?.GetTrackedPlayerFromConnectionId(sender.ClientId);
            var ships = Common.GameServices.ShipsServer;
            if (player == null || ships == null || !ships.TryGetTrackedShip(shipId, out TrackedShipServer ship)
                || !ship.TryGetStatefulPart(partId, out StatefulPart part) || part.Settings == null || part.Settings.id != Synthesizer)
                return;
            float now = Time.unscaledTime;
            string message;
            if (TFItems.Devices.Npc.Distance(player, ship, ship.Position + ship.Rotation * part.LocalPosition) > MealReach)
                message = Loc.Pick("Approche-toi du synthétiseur.", "Come closer to the synthesizer.", "Acércate al sintetizador.");
            else if (LastMeal.TryGetValue(sender.ClientId, out float last) && now - last < MealCooldown)
                message = Loc.Pick("Le prochain plat est prêt dans ", "The next meal is ready in ", "El próximo plato estará listo en ") + Mathf.CeilToInt(MealCooldown - (now - last)) + " s.";
            else if (!(NetworkState?.Invoke(player, null) is Players.Network.PlayerNetworkState state))
                return;
            else if (state.Health >= Players.Utils.PlayerDefaultState.DefaultHealth - 0.5f)
                message = Loc.Pick("Tu n'as pas faim : ta santé est au maximum.", "You are not hungry: your health is full.", "No tienes hambre: tu salud está al máximo.");
            else
            {
                state.ServerSetHealth(Players.Utils.PlayerDefaultState.DefaultHealth);
                LastMeal[sender.ClientId] = now;
                message = Loc.Pick("Bon appétit : ta santé est revenue au maximum.", "Enjoy your meal: your health is back to full.", "Buen provecho: tu salud vuelve al máximo.");
            }
            TFNet.SendTo(sender, TFMessageKind.MealResult, 0, 0, message);
        }

        // ---- The switches (water on or off; the marking lit, blinking or off)

        public static void AskMode(PartKey piece, int mode) =>
            TFNet.SendToServer(TFMessageKind.FurnitureMode, piece.ShipId, piece.PartId, mode.ToString(CultureInfo.InvariantCulture));

        private static void OnMode(NetworkConnection sender, TFMessageKind kind, uint shipId, ushort partId, string text)
        {
            var ships = Common.GameServices.ShipsServer;
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int mode) || ships == null || !ships.TryGetTrackedShip(shipId, out TrackedShipServer ship)
                || !ship.TryGetStatefulPart(partId, out StatefulPart part) || part.Settings == null || mode < 0 || mode >= Modes(part.Settings.id))
                return;
            var state = part.State as ConstantState ?? new ConstantState();
            if (state.ConstantValue != mode || !(part.State is ConstantState))
                ship.UpdatePartState(part.Id, state with { ConstantValue = mode });
        }

        // ---- Shared materials: the marking's three looks, the scrolling screens

        private static readonly Color MarkColor = new Color(1f, 0.72f, 0.12f);
        private static readonly Material[] Marks = new Material[3];
        private static Material _screen;
        private static bool _blinkLit;

        /// <summary>The floor marking's light: 0 lit, 1 blinking, 2 off.</summary>
        public static Material MarkMaterial(int mode)
        {
            if (Marks[mode] != null)
                return Marks[mode];
            Marks[mode] = LitMaterials.Get("TFA_CMark" + mode, MarkColor, 0f, 0.5f, mode == 2 ? Color.black : MarkColor * 2.2f);
            return Marks[mode];
        }

        /// <summary>The wall screens' face: rows of data and a chart, scrolled up by <see cref="Update"/>.</summary>
        public static Material ScreenMaterial()
        {
            if (_screen != null)
                return _screen;
            _screen = LitMaterials.Get("TFA_CScreenData", Color.white, 0.1f, 0.9f, Color.white);
            Texture2D data = ScreenTexture();
            foreach (string map in new[] { "_BaseMap", "_MainTex", "_EmissionMap" })
                if (_screen.HasProperty(map))
                    _screen.SetTexture(map, data);
            foreach (string map in new[] { "_BumpMap" })
                if (_screen.HasProperty(map))
                    _screen.SetTexture(map, null);
            _screen.DisableKeyword("_NORMALMAP");
            return _screen;
        }

        // Dark blue, lines of "text" in cyan and white, a green chart: one tile that repeats as it scrolls
        private static Texture2D ScreenTexture()
        {
            const int w = 64, h = 128;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "TFA_ScreenData", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[w * h];
            var back = new Color32(4, 14, 30, 255);
            var random = new System.Random(5);
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = back;
            for (int row = 0; row < h; row += 6)
            {
                bool chart = row % 48 == 24;
                int length = chart ? w - 8 : random.Next(12, w - 6);
                var color = chart ? new Color32(40, 230, 110, 255) : random.Next(3) == 0 ? new Color32(220, 235, 245, 255) : new Color32(40, 170, 240, 255);
                for (int x = 4; x < 4 + length; x++)
                {
                    int y = chart ? row + (int)(2.5f + 2.5f * Mathf.Sin(x * 0.4f)) : row;
                    // Words: gaps now and then
                    if (!chart && random.Next(7) == 0)
                        continue;
                    pixels[Mathf.Clamp(y, 0, h - 1) * w + x] = color;
                    if (!chart)
                        pixels[Mathf.Min(y + 1, h - 1) * w + x] = color;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        /// <summary>Every frame in game: the screens scroll, the blinking markings flash (two material changes for all of them).</summary>
        public static void Update()
        {
            if (_screen != null && _screen.HasProperty("_BaseMap"))
                _screen.SetTextureOffset("_BaseMap", new Vector2(0f, Time.time * 0.04f % 1f));
            if (Marks[1] != null && Marks[1].HasProperty("_EmissionColor"))
            {
                bool lit = Time.time % 1f < 0.5f;
                if (lit != _blinkLit)
                {
                    _blinkLit = lit;
                    Marks[1].SetColor("_EmissionColor", lit ? MarkColor * 2.2f : Color.black);
                }
            }
        }

        // ---- The holo table: the game's star map, its hologram above the table and smaller

        public static void FitStarMap(GameObject prefab, Vector3 size)
        {
            foreach (Ships.Parts.StarMap.PlanetsHologram hologram in prefab.GetComponentsInChildren<Ships.Parts.StarMap.PlanetsHologram>(true))
            {
                hologram.transform.localPosition = new Vector3(0f, size.y - 0.3f, 0f);
                hologram.transform.localScale = Vector3.one * 0.6f;
            }
            // Its plugs at the foot of the drum
            foreach (SocketObject socket in prefab.GetComponentsInChildren<SocketObject>(true))
            {
                Vector3 at = socket.transform.localPosition;
                socket.transform.localPosition = new Vector3(0.55f, at.y, at.z * 0.5f);
            }
        }

        // ---- The water

        private static Material _water;

        /// <summary>The falling water of a shower (from its head) or a sink (from its tap), stopped.</summary>
        public static ParticleSystem Water(Transform parent, bool shower)
        {
            if (_water == null)
            {
                _water = Common.TransparentMaterials.Additive("TFA_Water", Drop());
                if (_water == null)
                    return null;
                _water.SetColor("_BaseColor", new Color(0.55f, 0.75f, 1f, 0.5f));
            }
            var spout = new GameObject("TF_NoPaint_Water") { layer = parent.gameObject.layer };
            spout.transform.SetParent(parent, false);
            spout.transform.localPosition = shower ? new Vector3(0f, 2.04f, 0.12f) : new Vector3(0f, 0.95f, 0.06f);
            // The cone emits along its +z: turned to fall along the piece's -y (its floor, in any gravity)
            spout.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var system = spout.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startSpeed = shower ? 4f : 1.5f;
            main.startLifetime = shower ? 0.48f : 0.04f;
            main.startSize = shower ? 0.03f : 0.022f;
            main.gravityModifier = 0f;
            main.maxParticles = shower ? 160 : 30;
            main.startColor = new Color(0.8f, 0.9f, 1f, 0.6f);
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = shower ? 320f : 240f;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = shower ? 10f : 1f;
            shape.radius = shower ? 0.07f : 0.006f;
            var renderer = spout.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 2f;
            renderer.velocityScale = 0.04f;
            renderer.sharedMaterial = _water;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return system;
        }

        // A soft round drop
        private static Texture2D Drop()
        {
            const int n = 16;
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "TFA_WaterDrop", wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = new Vector2(x - n / 2f + 0.5f, y - n / 2f + 0.5f).magnitude / (n / 2f);
                    float a = Mathf.Clamp01(1f - d);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            texture.Apply();
            return texture;
        }

        /// <summary>This player's eyes are within reach of a point.</summary>
        public static bool Near(Vector3 at, float reach) => DiagonalDoors.LocalNear(at, reach);
    }

    /// <summary>A piece with a switch kept in its state; the use key steps to the next mode.</summary>
    internal abstract class ModeVisuals : UsablePart
    {
        protected int Mode { get; private set; }
        protected abstract int Count { get; }

        public override void Use() => FurnitureUses.AskMode(Key, (Mode + 1) % Count);

        public override void SetNetworkState(IStatefulPartState state)
        {
            Mode = state is ConstantState constant ? Mathf.Clamp((int)constant.ConstantValue, 0, Count - 1) : 0;
            Changed();
        }

        public override void ReactToNetworkStateChange(StatefulPartStateChangeEvent change) => SetNetworkState(change.NewState);

        protected abstract void Changed();
    }

    /// <summary>The food synthesizer: the use key asks the host for a meal.</summary>
    internal sealed class MealVisuals : UsablePart
    {
        public override string Label => Loc.Pick("Prendre un repas (soigne)", "Take a meal (heals)", "Tomar una comida (cura)");

        public override void Use() => FurnitureUses.AskMeal(Key);
    }

    /// <summary>The shower and the sink: mode 1 runs the water, while this player is near.</summary>
    internal sealed class WaterVisuals : ModeVisuals
    {
        private ParticleSystem _water;
        private float _next;
        private bool _shower;

        protected override int Count => 2;

        public override string Label => Mode == 1 ? Loc.Pick("Couper l'eau", "Turn the water off", "Cerrar el agua") : Loc.Pick("Ouvrir l'eau", "Turn the water on", "Abrir el agua");

        protected override void Awake()
        {
            base.Awake();
            // The shower is the tall one
            _shower = GetComponentsInChildren<MeshRenderer>(true).Any(r => r.name.Contains(FurnitureUses.Shower.ToString(CultureInfo.InvariantCulture)));
        }

        protected override void Changed() => _next = 0f;

        private void LateUpdate()
        {
            if (!HasContext || Time.unscaledTime < _next)
                return;
            _next = Time.unscaledTime + 0.5f;
            bool run = Mode == 1 && FurnitureUses.Near(transform.position, 15f);
            if (run && _water == null)
                _water = FurnitureUses.Water(transform, _shower);
            if (_water == null)
                return;
            if (run && !_water.isPlaying)
                _water.Play();
            else if (!run && _water.isPlaying)
                _water.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    /// <summary>The floor marking: its lights lit (0), blinking (1) or off (2).</summary>
    internal sealed class MarkingVisuals : ModeVisuals
    {
        private MeshRenderer[] _lights;

        protected override int Count => 3;

        public override string Label => Loc.Pick("Marquage : ", "Marking: ", "Marcado: ") + (Mode == 0 ? Loc.Pick("allumé", "lit", "encendido") : Mode == 1 ? Loc.Pick("clignotant", "blinking", "parpadeante") : Loc.Pick("éteint", "off", "apagado"));

        protected override void Changed()
        {
            if (_lights == null)
                _lights = GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name.EndsWith("Mark")).ToArray();
            foreach (MeshRenderer light in _lights)
                light.sharedMaterial = FurnitureUses.MarkMaterial(Mode);
        }
    }

    /// <summary>Pieces whose parts turn (the vent's fan): only near this player and in sight.</summary>
    internal sealed class SpinningParts : MonoBehaviour
    {
        /// <summary>The Blender pieces that turn are C&lt;id&gt;_Spin_&lt;look&gt;; their pivot is named so too.</summary>
        public const string Prefix = "Spin_";

        private Transform[] _pivots;
        private Renderer _seen;
        private float _next;
        private bool _run;

        private void Start()
        {
            _pivots = GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith(Prefix)).ToArray();
            _seen = _pivots.Length > 0 ? _pivots[0].GetComponentInChildren<Renderer>() : null;
        }

        private void Update()
        {
            if (_seen == null)
                return;
            if (Time.unscaledTime >= _next)
            {
                _next = Time.unscaledTime + 0.5f;
                _run = _seen.isVisible && FurnitureUses.Near(transform.position, 20f);
            }
            if (!_run)
                return;
            float step = 600f * Time.deltaTime;
            foreach (Transform pivot in _pivots)
                pivot.Rotate(Vector3.up, step, Space.Self);
        }
    }
}

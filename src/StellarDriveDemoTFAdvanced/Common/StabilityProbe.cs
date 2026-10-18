using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Ships;
using Ships.Interface.Model;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.StateTypes;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Common
{
    /// <summary>
    /// Development check (with DevWorld): what shakes a ship and what costs frames. Every 5 s it
    /// logs the part state writes per state type (each one fires the game's change events and
    /// network sync), the frame times (average, worst, hitches over 50 ms), and for the ship the
    /// player is on: how much its rotation jitters from one physics step to the next, and its
    /// visual from one frame to the next.
    /// </summary>
    [HarmonyPatch(typeof(ShipState), nameof(ShipState.SetPartState))]
    internal static class StabilityProbe
    {
        private static readonly Dictionary<string, int> Writes = new Dictionary<string, int>();
        private static readonly List<float> Frames = new List<float>();
        private static float _since = -1f;
        private static Quaternion _lastVisual;
        private static uint _visualShip;
        private static float _visualJitter, _visualJitterMax;
        private static int _visualSamples;
        private static Vector3 _lastAngular;
        private static float _angularJerk, _angularJerkMax;
        private static int _physicsSamples;

        private static bool Prepare() => DevHarness.Active;

        /// <summary>The ship the player is on (0: none yet).</summary>
        public static uint VisualShip => _visualShip;

        private static void Postfix(IStatefulPartState state)
        {
            string key = state?.GetType().Name ?? "null";
            Writes.TryGetValue(key, out int n);
            Writes[key] = n + 1;
        }

        private static float _flyStart = -1f;
        private static string _flyPhase = "";

        // DevFlyTest: the seats of the probed ship ask for thrust, then a turn, then both
        private static void Fly()
        {
            if (!Settings.DevFlyTest.Value || _visualShip == 0)
                return;
            if (_flyStart < 0f)
                _flyStart = Time.unscaledTime + 25f;
            float t = Time.unscaledTime - _flyStart;
            if (t < 0f || t > 40f)
                return;
            Vector3 move = t < 10f ? Vector3.forward : t < 20f ? Vector3.zero : t < 30f ? Vector3.forward : Vector3.zero;
            Vector3 turn = t < 10f ? Vector3.zero : t < 20f ? new Vector3(0f, 0.6f, 0f) : t < 30f ? new Vector3(0.3f, 0.6f, 0f) : Vector3.zero;
            string phase = t < 10f ? "thrust" : t < 20f ? "turn" : t < 30f ? "thrust+turn" : "coast";
            if (phase != _flyPhase)
            {
                _flyPhase = phase;
                AdvancedMod.Log.Msg($"dev: fly test: {phase}");
            }
            var ships = GameServices.ShipsServer;
            if (ships == null)
                return;
            foreach (var data in ships.GetRootConnectedShips(_visualShip))
            {
                if (!(ships.GetShip(data.Ship.Id) is TrackedShipServer ship))
                    continue;
                foreach (StatefulPart part in ship.StatefulParts)
                {
                    if (part?.State is Ships.Interface.Model.Parts.State.PilotSeatState seat)
                        ship.UpdatePartState(part.Id, seat with { MoveInputY = move.y, MoveInputZ = move.z, RotationInput = turn });
                    else if (part?.State is Ships.Interface.Model.Parts.State.PilotSeatAdvState adv)
                        ship.UpdatePartState(part.Id, adv with { MoveInput = move, RotationInput = turn });
                }
            }
        }

        /// <summary>Every frame (dev only).</summary>
        public static void Update()
        {
            if (!DevHarness.Active)
                return;
            Fly();
            Trace();
            Frames.Add(Time.unscaledDeltaTime);
            SampleVisual();
            if (_since < 0f)
                _since = Time.unscaledTime;
            if (Time.unscaledTime - _since < 5f)
                return;
            _since = Time.unscaledTime;
            float avg = Frames.Count > 0 ? Frames.Average() : 0f, worst = Frames.Count > 0 ? Frames.Max() : 0f;
            int hitches = Frames.Count(f => f > 0.05f);
            AdvancedMod.Log.Msg($"dev: stability: {Frames.Count / 5f:0} fps avg {avg * 1000f:0.0} ms worst {worst * 1000f:0} ms, {hitches} hitch(es) > 50 ms; ship {_visualShip} visual turn {(_visualSamples > 0 ? _turnRate / _visualSamples : 0f):0.0}°/s, jitter avg {(_visualSamples > 0 ? _visualJitter / _visualSamples : 0f):0.00}°/s max {_visualJitterMax:0.0}°/s, physics angular jerk avg {(_physicsSamples > 0 ? _angularJerk / _physicsSamples : 0f):0.0000} max {_angularJerkMax:0.000} rad/s per step; state writes/s: "
                + string.Join(", ", Writes.OrderByDescending(w => w.Value).Take(10).Select(w => $"{w.Key} {w.Value / 5f:0}")));
            Frames.Clear();
            Writes.Clear();
            _visualJitter = _visualJitterMax = _angularJerk = _angularJerkMax = _turnRate = 0f;
            _visualSamples = _physicsSamples = 0;
        }

        // The ship under the camera: its visual's turn from one frame to the next, minus the
        // average turn (a steady spin is not a shake)
        private static float _lastTurn, _turnRate;

        private static void SampleVisual()
        {
            Camera camera = MainCamera.Get();
            var ships = GameServices.ShipsServer;
            if (camera == null || ships == null)
                return;
            uint nearest = 0;
            float best = 60f * 60f;
            foreach (var ship in ships.LoadedRootShips)
            {
                Transform visual = ShipVisuals.Find(ship.Id);
                if (visual == null)
                    continue;
                float d = (visual.position - camera.transform.position).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    nearest = ship.Id;
                }
            }
            Transform v = nearest != 0 ? ShipVisuals.Find(nearest) : null;
            if (v == null)
                return;
            if (nearest != _visualShip)
            {
                _visualShip = nearest;
                _lastVisual = v.rotation;
                return;
            }
            // Turn rate (deg/s) from one frame to the next: frames of different lengths are not a shake
            float dt = Mathf.Max(1e-4f, Time.deltaTime);
            float turn = Quaternion.Angle(_lastVisual, v.rotation) / dt;
            float jitter = Mathf.Abs(turn - _lastTurn);
            _lastTurn = turn;
            _turnRate += turn;
            _lastVisual = v.rotation;
            _visualJitter += jitter;
            _visualJitterMax = Mathf.Max(_visualJitterMax, jitter);
            _visualSamples++;
        }

        // Turn phase, 2 s: every network snapshot of the probed ship (tick, angle to the previous
        // one) and every frame (dt, visual angle), to see which one is uneven
        private static readonly System.Text.StringBuilder TraceLog = new System.Text.StringBuilder();
        private static ShipState _traced;
        private static Quaternion _lastSnap, _lastFrame;
        private static uint _lastTick;
        private static bool _traceDone;

        private static void Trace()
        {
            if (_traceDone || _flyPhase != "turn" || _visualShip == 0)
                return;
            float t = Time.unscaledTime - _flyStart;
            Transform v = ShipVisuals.Find(_visualShip);
            if (v == null)
                return;
            if (_traced == null)
            {
                if (t < 14f || !GameServices.ShipsClient.TryGetTrackedShip(_visualShip, out var client))
                    return;
                _traced = client.State;
                _traced.PhysicsStateUpdatedClient += (prev, next, tick) =>
                {
                    float rate = _lastTick != 0 && tick > _lastTick ? Quaternion.Angle(_lastSnap, next.Rotation) / (tick - _lastTick) : -1f;
                    TraceLog.Append($"S f{Time.frameCount} tick {tick} +{tick - _lastTick} {rate:0.000}°/tick; ");
                    _lastSnap = next.Rotation;
                    _lastTick = tick;
                };
                _lastFrame = v.rotation;
                return;
            }
            if (TraceLog.Length == 0)
                TraceLog.Append($"vsync {QualitySettings.vSyncCount} refresh {Screen.currentResolution.refreshRateRatio.value:0.0}; ");
            TraceLog.Append($"F f{Time.frameCount} dt {Time.deltaTime * 1000f:0.0} {Quaternion.Angle(_lastFrame, v.rotation) * 1000f:0}m°; ");
            _lastFrame = v.rotation;
            if (t > 16f)
            {
                _traceDone = true;
                AdvancedMod.Log.Msg("dev: trace: " + TraceLog);
            }
        }

        /// <summary>Host physics step of a root ship (called from the pirate steering postfix).</summary>
        public static void Physics(uint shipId, Rigidbody body)
        {
            if (!DevHarness.Active || shipId != _visualShip || body == null)
                return;
            float jerk = (body.angularVelocity - _lastAngular).magnitude;
            _lastAngular = body.angularVelocity;
            _angularJerk += jerk;
            _angularJerkMax = Mathf.Max(_angularJerkMax, jerk);
            _physicsSamples++;
        }
    }
}

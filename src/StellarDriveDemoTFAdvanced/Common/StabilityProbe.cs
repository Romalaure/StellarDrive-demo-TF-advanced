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
            Frames.Add(Time.unscaledDeltaTime);
            SampleVisual();
            if (_since < 0f)
                _since = Time.unscaledTime;
            if (Time.unscaledTime - _since < 5f)
                return;
            _since = Time.unscaledTime;
            float avg = Frames.Count > 0 ? Frames.Average() : 0f, worst = Frames.Count > 0 ? Frames.Max() : 0f;
            int hitches = Frames.Count(f => f > 0.05f);
            AdvancedMod.Log.Msg($"dev: stability: {Frames.Count / 5f:0} fps avg {avg * 1000f:0.0} ms worst {worst * 1000f:0} ms, {hitches} hitch(es) > 50 ms; ship {_visualShip} visual jitter avg {(_visualSamples > 0 ? _visualJitter / _visualSamples : 0f):0.000}° max {_visualJitterMax:0.00}°, physics angular jerk avg {(_physicsSamples > 0 ? _angularJerk / _physicsSamples : 0f):0.0000} max {_angularJerkMax:0.000} rad/s per step; state writes/s: "
                + string.Join(", ", Writes.OrderByDescending(w => w.Value).Take(10).Select(w => $"{w.Key} {w.Value / 5f:0}")));
            Frames.Clear();
            Writes.Clear();
            _visualJitter = _visualJitterMax = _angularJerk = _angularJerkMax = 0f;
            _visualSamples = _physicsSamples = 0;
        }

        // The ship under the camera: its visual's turn from one frame to the next, minus the
        // average turn (a steady spin is not a shake)
        private static float _lastTurn;

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
            float turn = Quaternion.Angle(_lastVisual, v.rotation);
            float jitter = Mathf.Abs(turn - _lastTurn);
            _lastTurn = turn;
            _lastVisual = v.rotation;
            _visualJitter += jitter;
            _visualJitterMax = Mathf.Max(_visualJitterMax, jitter);
            _visualSamples++;
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

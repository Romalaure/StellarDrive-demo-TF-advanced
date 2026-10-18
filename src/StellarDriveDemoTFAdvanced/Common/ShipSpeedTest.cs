using Core.Services;
using HarmonyLib;
using Ships;
using Ships.Interface.Model.State;
using Ships.Interface.Services;
using Ships.Physics;
using Ships.Services;
using UnityEngine;
using WorldTracking.Interface.Model.Spaces;
using WorldTracking.Interface.Model.Spaces.Common;
using WorldTracking.Interface.Services;

namespace StellarDriveDemoTFAdvanced.Common
{
    /// <summary>
    /// Development check (DevFlyTest with DevSpeedTest = m/s): the ship the player is on is
    /// carried, whatever its engines: lifted 6 s, then flown level over the planet at that
    /// speed for 60 s, then stopped. To measure what a fast flight costs with a real ship.
    /// </summary>
    [HarmonyPatch(typeof(ShipServerPhysics), "UpdateRootRigidbody")]
    internal static class ShipSpeedTest
    {
        private static readonly AccessTools.FieldRef<ShipRigidbody, Rigidbody> Body = AccessTools.FieldRefAccess<ShipRigidbody, Rigidbody>("_rigidbody");
        private static float _start = -1f;
        private static string _phase = "";
        private static Vector3 _heading;

        private static bool Prepare() => DevHarness.Active && Settings.DevFlyTest.Value && Settings.DevSpeedTest.Value > 0f;

        private static void Postfix(IShipStateWrite ____ship, ShipRigidbody ____shipRigidbody)
        {
            uint target = StabilityProbe.VisualShip;
            if (target == 0 || ____ship == null || ____shipRigidbody == null)
                return;
            var ships = GameServices.ShipsServer;
            if (ships == null)
                return;
            bool mine = false;
            foreach (var data in ships.GetRootConnectedShips(____ship.Id))
                mine |= data.Ship != null && data.Ship.Id == target;
            if (!mine || !(ships.GetShip(____ship.Id) is TrackedShipServer ship))
                return;
            Rigidbody body = Body(____shipRigidbody);
            var spaces = ServiceLocator.GetService<ISpacesServerProvider>();
            if (body == null || spaces == null || !spaces.TryGetSpace(ship.PhysicsState.SpaceId, out ISpace space) || !(space is PlanetSurfaceSpace surface))
                return;
            if (_start < 0f)
                _start = Time.unscaledTime + 25f;
            float t = Time.unscaledTime - _start;
            if (t < 0f || t > 75f)
                return;
            Vector3 up = ((Vector3)surface.Center + ship.PhysicsState.Position).normalized;
            if (_heading == Vector3.zero)
                _heading = Vector3.Cross(up, Vector3.Cross(Vector3.forward, up).sqrMagnitude > 0.01f ? Vector3.forward : Vector3.right).normalized;
            // Level: keep the heading along the ground as the planet curves away
            _heading = Vector3.ProjectOnPlane(_heading, up).normalized;
            string phase = t < 6f ? "lift" : t < 66f ? "cruise" : "stop";
            if (phase != _phase)
            {
                _phase = phase;
                AdvancedMod.Log.Msg($"dev: ship speed test: {phase} (ship {ship.Id}, space {ship.PhysicsState.SpaceId})");
            }
            body.velocity = phase == "lift" ? up * 40f : phase == "cruise" ? _heading * Settings.DevSpeedTest.Value : Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Core.Services;
using HarmonyLib;
using Ships;
using Ships.Services;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Power
{
    /// <summary>
    /// The force field against the Arsenal mod's weapons (only when that mod is installed):
    ///  - host: bullet and blast damage to a shielded ship's hull and parts is taken by the field
    ///    instead (Arsenal panel damage / 4, blast power / 20, in field points: the biggest blast takes about half a full field), and blasts no
    ///    longer push it off course while the field holds;
    ///  - every client: missiles and rockets that enter a field bubble go off on it (as when the
    ///    point defence shoots them), and the bubble shows the impact.
    /// Arsenal's methods are found by name; if a future version changes them, its weapons simply
    /// go through the field again.
    /// </summary>
    internal static class ArsenalShield
    {
        private const float PanelPoints = 0.25f, BlastPoints = 1f / 20f;

        // An explosion's blast and its panel hits on the same ship come in the same frame: counted once
        private static readonly Dictionary<uint, int> BlastFrame = new Dictionary<uint, int>();

        private static IList _threats;
        private static PropertyInfo _threatPosition, _threatShooter;
        private static MethodInfo _threatShootDown;

        public static bool Installed { get; private set; }

        public static void Install(HarmonyLib.Harmony harmony)
        {
            Type hull = AccessTools.TypeByName("Arsenal.Damage.HullDamage");
            Type parts = AccessTools.TypeByName("Arsenal.Damage.PartDamage");
            Type knock = AccessTools.TypeByName("Arsenal.Damage.ShipKnockback");
            Type projectiles = AccessTools.TypeByName("Arsenal.Weapons.Projectiles");
            Type threat = AccessTools.TypeByName("Arsenal.Weapons.IThreat");
            if (hull == null)
                return;
            int patched = 0;
            MethodInfo applyHull = AccessTools.Method(hull, "ApplyServer");
            if (applyHull != null && applyHull.GetParameters().Length == 3)
            {
                harmony.Patch(applyHull, prefix: new HarmonyMethod(typeof(ArsenalShield), nameof(HullPrefix)));
                patched++;
            }
            MethodInfo blast = parts != null ? AccessTools.Method(parts, "ApplyBlast") : null;
            if (blast != null)
            {
                harmony.Patch(blast, prefix: new HarmonyMethod(typeof(ArsenalShield), nameof(BlastPrefix)));
                patched++;
            }
            MethodInfo queue = knock != null ? AccessTools.Method(knock, "Queue") : null;
            if (queue != null)
            {
                harmony.Patch(queue, prefix: new HarmonyMethod(typeof(ArsenalShield), nameof(KnockPrefix)));
                patched++;
            }
            if (projectiles != null && threat != null)
            {
                _threats = AccessTools.Field(projectiles, "Threats")?.GetValue(null) as IList;
                _threatPosition = AccessTools.Property(threat, "Position");
                _threatShooter = AccessTools.Property(threat, "ShooterRoot");
                _threatShootDown = AccessTools.Method(threat, "ShootDown");
            }
            Installed = patched > 0;
            AdvancedMod.Log.Msg($"Arsenal found: the force field takes its weapons ({patched} damage hooks, missiles {(_threats != null ? "stopped" : "not stopped")})");
        }

        private static ShipsServerTracker Ships => Common.GameServices.ShipsServer;

        private static uint Root(uint shipId) =>
            Ships != null && Ships.TryGetRootShip(shipId, out Ships.Interface.Model.State.IShipStateRead root) ? root.Id : shipId;

        // HullDamage.ApplyServer(PanelKey key, HoleMask add, float damage)
        private static bool HullPrefix(object[] __args)
        {
            try
            {
                uint shipId = ArsenalReflection.ShipIdOf(__args[0]);
                float damage = ArsenalReflection.AsFloat(__args[2]);
                if (BlastFrame.TryGetValue(Root(shipId), out int frame) && frame == Time.frameCount)
                    return false;
                return Ships == null || !ForceField.TryAbsorb(Ships, shipId, damage * PanelPoints);
            }
            catch (Exception e)
            {
                Common.ErrorLog.Report("force field against Arsenal", e);
                return true;
            }
        }

        // PartDamage.ApplyBlast(TrackedShipServer ship, Vector3 shipLocalBlast, float radius, float power)
        private static bool BlastPrefix(TrackedShipServer ship, float power)
        {
            try
            {
                if (ship == null || Ships == null || !ForceField.TryAbsorb(Ships, ship.Id, power * BlastPoints))
                    return true;
                BlastFrame[Root(ship.Id)] = Time.frameCount;
                return false;
            }
            catch (Exception e)
            {
                Common.ErrorLog.Report("force field against Arsenal", e);
                return true;
            }
        }

        // ShipKnockback.Queue(uint rootId, Vector3 impulse, Vector3 point): a held field is not pushed
        private static bool KnockPrefix(uint rootId) => Ships == null || !ForceField.IsUp(Ships, rootId);

        /// <summary>
        /// Client: Arsenal missiles and rockets inside this bubble (not fired from its own ship) go
        /// off on it. Returns the direction of one, in the bubble's frame, for the impact ripple.
        /// </summary>
        public static Vector3? StopThreats(Transform bubble, uint shipId)
        {
            if (_threats == null || _threats.Count == 0)
                return null;
            Vector3? hit = null;
            foreach (object threat in new ArrayList(_threats))
            {
                try
                {
                    if ((uint)_threatShooter.GetValue(threat) == shipId)
                        continue;
                    Vector3 local = bubble.InverseTransformPoint((Vector3)_threatPosition.GetValue(threat));
                    if (local.magnitude > 0.5f)
                        continue;
                    _threatShootDown.Invoke(threat, null);
                    hit = local.normalized;
                }
                catch (Exception e)
                {
                    Common.ErrorLog.Report("force field against Arsenal missiles", e);
                    return hit;
                }
            }
            return hit;
        }
    }
}

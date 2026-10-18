using HarmonyLib;
using Planets.EnvironmentGeneration.Tracking;
using Planets.Objects.Common.Collisions;
using UnityEngine;
using WorldTracking.Interface.Model.Spaces;

namespace StellarDriveDemoTFAdvanced.Worlds
{
    /// <summary>
    /// Trees of a mod planet's different forests (TF Gaïa has four: woods, taiga, savanna, palm
    /// coasts) are placed independently, so two of them can stand in each other. Their physics
    /// pushed them apart with a torque (1e6 to 1e7) far over what breaks a tree's joint (1e5):
    /// the tree fell as a log, which knocked its neighbours down, and so on. Hundreds of logs
    /// every few seconds, each needing the planet's colliders worked out around it, took the game
    /// down to 1 frame per second, and cleared the forest. On the mod's planets a tree now breaks
    /// at 2e7: a ship crashing into it (about 1e8) still fells it, a neighbour or a rolling log
    /// does not; the mining laser and the hand tools cut trees as before.
    /// </summary>
    [HarmonyPatch(typeof(PlanetObjectJointBreak), "Setup")]
    internal static class TreeOverlapFix
    {
        private const float BreakTorque = 2e7f;

        private static readonly AccessTools.FieldRef<PlanetObjectJointBreak, Joint> JointField = AccessTools.FieldRefAccess<PlanetObjectJointBreak, Joint>("joint");
        private static readonly AccessTools.FieldRef<PlanetObjectJointBreak, PlanetObjectPhysics> PhysicsField = AccessTools.FieldRefAccess<PlanetObjectJointBreak, PlanetObjectPhysics>("physicsObject");

        private static void Postfix(PlanetObjectJointBreak __instance, bool isServer)
        {
            if (!isServer)
                return;
            Joint joint = JointField(__instance);
            PlanetObjectPhysics physics = PhysicsField(__instance);
            if (joint == null || !(physics?.Space is PlanetSurfaceSpace surface) || !PlanetFactory.IsModPlanet(surface.PlanetId))
                return;
            joint.breakTorque = Mathf.Max(joint.breakTorque, BreakTorque);
        }
    }
}

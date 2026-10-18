using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace StellarDriveDemoTFAdvanced.Common
{
    /// <summary>Development check (with DevWorld): time spent in a few heavy game methods, logged every 5 seconds.</summary>
    [HarmonyPatch]
    internal static class FrameTimers
    {
        private static readonly Dictionary<string, (double Ms, int Calls)> Totals = new Dictionary<string, (double, int)>();
        private static readonly Stopwatch Since = Stopwatch.StartNew();

        private static IEnumerable<MethodBase> TargetMethods() => Candidates().Where(m => m != null);

        private static IEnumerable<MethodBase> Candidates()
        {
            yield return AccessTools.Method(typeof(Managers.Server.WorldPhysicsServerUpdater), "PrePhysics");
            yield return AccessTools.Method(typeof(Managers.Server.WorldPhysicsServerUpdater), "PostPhysics");
            yield return AccessTools.Method(typeof(Planets.Services.PlanetColliders), nameof(Planets.Services.PlanetColliders.UpdateGeneratedColliders));
            yield return AccessTools.Method(typeof(Planets.EnvironmentGeneration.PlanetDenseEnvironmentVisuals), "Update");
            yield return AccessTools.Method(typeof(Planets.EnvironmentGeneration.PlanetHotspotsEnvironmentVisuals), "Update");
            yield return AccessTools.Method(typeof(Planets.Terrain.Meshes.PlanetMeshUpdater), nameof(Planets.Terrain.Meshes.PlanetMeshUpdater.UpdateTerrainMesh));
            yield return AccessTools.Method(typeof(Planets.EnvironmentGeneration.Services.PlanetDenseEnvObjectsCpuFinder), "FindObjectsNearPosition");
            yield return AccessTools.Method(typeof(Planets.EnvironmentGeneration.PlanetDenseEnvironmentPhysics), "SpawnCollider");
            yield return AccessTools.Method(typeof(Planets.EnvironmentGeneration.PlanetHotspotsEnvironmentPhysics), "SpawnCollider");
            yield return AccessTools.Method(typeof(Planets.EnvironmentGeneration.PlanetHotspotsEnvironmentPhysics), "GetCollidersAtPosition");
        }

        private static bool Prepare() => DevHarness.Active;

        private static void Prefix(out Stopwatch __state)
        {
            __state = Stopwatch.StartNew();
        }

        private static void Postfix(MethodBase __originalMethod, Stopwatch __state)
        {
            string key = __originalMethod.DeclaringType?.Name + "." + __originalMethod.Name;
            Totals.TryGetValue(key, out var total);
            Totals[key] = (total.Ms + __state.Elapsed.TotalMilliseconds, total.Calls + 1);
            if (Since.Elapsed.TotalSeconds < 5)
                return;
            AdvancedMod.Log.Msg("dev: time per 5 s: " + string.Join(", ", Totals.OrderByDescending(t => t.Value.Ms).Select(t => $"{t.Key} {t.Value.Ms:0} ms/{t.Value.Calls}")));
            Totals.Clear();
            Since.Restart();
        }
    }
}

namespace StellarDriveDemoTFAdvanced.Common
{
    [HarmonyLib.HarmonyPatch(typeof(Planets.Services.PlanetColliders), "GenerateColliders")]
    internal static class ColliderGenerationProbe
    {
        private static readonly System.Diagnostics.Stopwatch Since = System.Diagnostics.Stopwatch.StartNew();
        private static readonly System.Collections.Generic.Dictionary<string, int> Counts = new System.Collections.Generic.Dictionary<string, int>();

        private static bool Prepare() => DevHarness.Active;

        private static void Prefix(Planets.Services.PlanetColliders __instance)
        {
            var list = (System.Collections.Generic.List<WorldTracking.Interface.Model.ITrackedWorldObject>)HarmonyLib.AccessTools.Field(typeof(Planets.Services.PlanetColliders), "_objectsToGenerateCollider").GetValue(__instance);
            foreach (var o in list)
            {
                string key = o.GetType().Name + " " + o.Id;
                Counts.TryGetValue(key, out int n);
                Counts[key] = n + 1;
            }
            if (Since.Elapsed.TotalSeconds < 5)
                return;
            AdvancedMod.Log.Msg("dev: collider passes per 5 s: " + string.Join(", ", System.Linq.Enumerable.Select(Counts, c => c.Key + " x" + c.Value)));
            Counts.Clear();
            Since.Restart();
        }
    }
}

namespace StellarDriveDemoTFAdvanced.Common
{
    /// <summary>Development: where new world objects (dropped items, debris) come from.</summary>
    [HarmonyLib.HarmonyPatch(typeof(WorldObjects.TrackedWorldObjectServer), HarmonyLib.MethodType.Constructor,
        new[] { typeof(WorldObjects.Interface.Model.WorldObjectState), typeof(WorldObjects.Interface.WorldObjectPrefab), typeof(double) })]
    internal static class WorldObjectSpawnProbe
    {
        private static int _logged;
        private static readonly System.Collections.Generic.Dictionary<string, int> Counts = new System.Collections.Generic.Dictionary<string, int>();
        private static readonly System.Diagnostics.Stopwatch Since = System.Diagnostics.Stopwatch.StartNew();

        private static bool Prepare() => DevHarness.Active;

        private static void Postfix(WorldObjects.TrackedWorldObjectServer __instance)
        {
            string key = $"item {__instance.Content.ItemId} x{__instance.Content.Quantity}";
            Counts.TryGetValue(key, out int n);
            Counts[key] = n + 1;
            if (_logged < 6)
            {
                _logged++;
                AdvancedMod.Log.Msg($"dev: world object {__instance.Id} {key} space {__instance.SpaceId} at {__instance.Position}: " + System.Environment.StackTrace.Replace("\n", " | ").Substring(0, 1800));
            }
            if (Since.Elapsed.TotalSeconds >= 5)
            {
                AdvancedMod.Log.Msg("dev: world objects created per 5 s: " + string.Join(", ", System.Linq.Enumerable.Select(Counts, c => c.Key + ": " + c.Value)));
                Counts.Clear();
                Since.Restart();
            }
        }
    }
}

namespace StellarDriveDemoTFAdvanced.Common
{
    /// <summary>Development: what knocks planet objects (trees) off their joints.</summary>
    [HarmonyLib.HarmonyPatch(typeof(Planets.Objects.Common.Collisions.PlanetObjectJointBreakEventCatcher), "OnCollisionEnter")]
    internal static class JointHitProbe
    {
        public static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, string> Last = new System.Runtime.CompilerServices.ConditionalWeakTable<object, string>();

        private static bool Prepare() => DevHarness.Active;

        private static void Postfix(Planets.Objects.Common.Collisions.PlanetObjectJointBreakEventCatcher __instance, UnityEngine.Collision other)
        {
            Last.Remove(__instance);
            Last.Add(__instance, (other.collider != null ? other.collider.name + "/" + (other.collider.attachedRigidbody != null ? other.collider.attachedRigidbody.name : "static") + " [" + UnityEngine.LayerMask.LayerToName(other.collider.gameObject.layer) + "]" : "?")
                + " on " + __instance.transform.root.name + " [" + string.Join(",", System.Linq.Enumerable.Distinct(System.Linq.Enumerable.Select(__instance.transform.root.GetComponentsInChildren<UnityEngine.Collider>(), c => UnityEngine.LayerMask.LayerToName(c.gameObject.layer)))) + "]");
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(Planets.Objects.Common.Collisions.PlanetObjectJointBreakEventCatcher), "OnJointBreak")]
    internal static class JointBreakProbe
    {
        private static readonly System.Collections.Generic.Dictionary<string, int> Counts = new System.Collections.Generic.Dictionary<string, int>();
        private static readonly System.Diagnostics.Stopwatch Since = System.Diagnostics.Stopwatch.StartNew();

        private static bool Prepare() => DevHarness.Active;

        private static void Prefix(Planets.Objects.Common.Collisions.PlanetObjectJointBreakEventCatcher __instance, float breakForce)
        {
            string key = JointHitProbe.Last.TryGetValue(__instance, out string hit) ? hit : "no collision (" + __instance.name + ")";
            var joint = __instance.GetComponent<UnityEngine.Joint>();
            key = key.Split('[')[0] + " force " + (breakForce < 1e4f ? "<1e4" : breakForce < 1e5f ? "<1e5" : breakForce < 1e6f ? "<1e6" : breakForce < 1e7f ? "<1e7" : ">1e7") + " (joint " + (joint != null ? joint.breakForce + "/" + joint.breakTorque : "?") + ")";
            Counts.TryGetValue(key, out int n);
            Counts[key] = n + 1;
            if (Since.Elapsed.TotalSeconds >= 5)
            {
                AdvancedMod.Log.Msg("dev: joints broken per 5 s by: " + string.Join(", ", System.Linq.Enumerable.Take(System.Linq.Enumerable.OrderByDescending(Counts, c => c.Value), 12).Select(c => c.Key + " x" + c.Value)));
                Counts.Clear();
                Since.Restart();
            }
        }
    }
}

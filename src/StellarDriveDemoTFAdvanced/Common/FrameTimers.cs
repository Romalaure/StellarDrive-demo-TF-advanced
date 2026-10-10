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

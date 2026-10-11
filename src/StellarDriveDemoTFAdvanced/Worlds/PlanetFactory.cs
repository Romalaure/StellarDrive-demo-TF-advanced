using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core.Values;
using HarmonyLib;
using Planets;
using Planets.Network;
using Planets.Network.Model;
using Planets.Rings;
using Planets.Services;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StellarDriveDemoTFAdvanced.Worlds
{
    /// <summary>
    /// Adds the mod's planets to the star system. Each is a copy of the demo's planet (442 Otrera b,
    /// id 4) made before the game's planet trackers start, then reshaped by its <see cref="PlanetSpec"/>:
    /// terrain, colors, atmosphere, flora and points of interest. The game then generates, lights,
    /// saves and simulates it like its own planet. The other planets of the scene belong to the full
    /// game and stay untouched.
    /// </summary>
    internal static class PlanetFactory
    {
        public const ulong TemplateId = 4;

        private static readonly List<Planet> Clones = new List<Planet>();
        private static readonly FieldInfo IdField = AccessTools.Field(typeof(Planet), "id");
        private static readonly FieldInfo PositionField = AccessTools.Field(typeof(Planet), "positionAlignedToParentAxis");
        private static readonly FieldInfo ParentField = AccessTools.Field(typeof(Planet), "orbitParent");

        public static IReadOnlyList<Planet> Planets => Clones;

        public static bool IsModPlanet(ulong id) => PlanetCatalog.All.Any(p => p.Id == id);

        /// <summary>Creates the planets once per world load, before the first tracker reads the scene.</summary>
        public static void EnsureCreated(MonoBehaviour tracker)
        {
            Clones.RemoveAll(p => p == null);
            // Only in the game world: the main menu has its own planet tracker for its background
            if (Clones.Count > 0 || !Settings.NewPlanets.Value || tracker.gameObject.scene.name != "MainGame")
                return;
            Planet template = Object.FindObjectsOfType<Planet>(true).FirstOrDefault(p => p.Id == TemplateId);
            if (template == null)
            {
                AdvancedMod.Log.Warning("planets: the demo planet was not found, no new planets");
                return;
            }
            // Erebos's planets orbit their own star, made first
            Stars.Star erebos = PlanetCatalog.All.Any(p => p.System == PlanetWiki.Erebos) && Settings.Erebos.Value ? Galaxy.EnsureStar() : null;
            // Built under an inactive holder so nothing of the copy wakes up before it is reshaped
            var holder = new GameObject("TFA_PlanetHolder");
            holder.SetActive(false);
            try
            {
                foreach (PlanetSpec spec in PlanetCatalog.All)
                {
                    if (spec.System == PlanetWiki.Erebos && erebos == null)
                        continue;
                    try
                    {
                        Planet clone = Object.Instantiate(template.gameObject, holder.transform).GetComponent<Planet>();
                        clone.name = spec.Name;
                        IdField.SetValue(clone, spec.Id);
                        PositionField.SetValue(clone, spec.Position((Vector3Double)PositionField.GetValue(template)));
                        if (spec.System == PlanetWiki.Erebos)
                            ParentField.SetValue(clone, erebos);
                        // FishNet keeps scene network objects disabled until the server spawns them, and only
                        // spawns those baked in the scene: the copy runs without one (see README, multiplayer)
                        foreach (FishNet.Observing.NetworkObserver observer in clone.GetComponents<FishNet.Observing.NetworkObserver>())
                            Object.DestroyImmediate(observer);
                        foreach (FishNet.Object.NetworkObject networkObject in clone.GetComponents<FishNet.Object.NetworkObject>())
                            Object.DestroyImmediate(networkObject);
                        // Asteroid fields: the demo planet's ring, kept (own copy of its settings) on the planets that have one
                        PlanetRing ring = clone.GetComponent<PlanetRing>();
                        if (ring != null && spec.Ring == null)
                            Object.DestroyImmediate(ring);
                        else if (ring != null)
                            spec.Ring(ring);
                        PlanetShaper.Apply(clone, spec);
                        clone.transform.SetParent(template.transform.parent, false);
                        Clones.Add(clone);
                    }
                    catch (Exception e)
                    {
                        AdvancedMod.Log.Error($"planets: could not create {spec.Name}: {e}");
                    }
                }
            }
            finally
            {
                Object.Destroy(holder);
            }
            AdvancedMod.Log.Msg($"planets: created {Clones.Count} planet(s): {string.Join(", ", Clones.Select(p => p.name + " #" + p.Id))}");
        }

        /// <summary>
        /// The demo's trackers only keep planet 4. Registers the mod's planets the same way the
        /// game registers its own (PlanetsTracker.LoadPlanets), after the tracker loaded.
        /// </summary>
        public static void Register(MonoBehaviour tracker)
        {
            Type baseType = tracker.GetType().BaseType;
            var planets = (IDictionary)AccessTools.Field(baseType, "_planets").GetValue(tracker);
            MethodInfo create = AccessTools.Method(tracker.GetType(), "CreateTrackedPlanet");
            PropertyInfo trackerEvent = AccessTools.Property(baseType, "OnObjectDestroy");
            foreach (Planet planet in Clones)
            {
                if (planet == null || planets.Contains(planet.Id))
                    continue;
                planet.ResetCache();
                var tracked = (TrackedPlanet)create.Invoke(tracker, new object[] { planet });
                tracked.OnObjectDestroy += e => ((Action<PlanetDestroyedObjectEvent>)trackerEvent.GetValue(tracker))?.Invoke(e);
                planets.Add(planet.Id, tracked);
                planet.BuildCache();
            }
        }

        public static void Forget()
        {
            Clones.Clear();
            Galaxy.Forget();
        }
    }

    [HarmonyPatch(typeof(PlanetsServerTracker), "Awake")]
    internal static class PlanetsServerTrackerPatch
    {
        private static void Prefix(PlanetsServerTracker __instance) => Guard("server", () => PlanetFactory.EnsureCreated(__instance));

        private static void Postfix(PlanetsServerTracker __instance) => Guard("server", () => PlanetFactory.Register(__instance));

        internal static void Guard(string side, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                ErrorLog.Report("planets (" + side + ")", e);
            }
        }
    }

    [HarmonyPatch(typeof(PlanetsClientTracker), "Awake")]
    internal static class PlanetsClientTrackerPatch
    {
        private static void Prefix(PlanetsClientTracker __instance) => PlanetsServerTrackerPatch.Guard("client", () => PlanetFactory.EnsureCreated(__instance));

        private static void Postfix(PlanetsClientTracker __instance) => PlanetsServerTrackerPatch.Guard("client", () => PlanetFactory.Register(__instance));
    }
}

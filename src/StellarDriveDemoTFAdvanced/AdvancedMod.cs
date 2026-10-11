using System;
using System.Linq;
using System.Runtime.CompilerServices;
using MelonLoader;
using StellarDriveDemoTFAdvanced;
using StellarDriveDemoTFAdvanced.Common;
using StellarDriveDemoTFAdvanced.ShipComputer;

[assembly: MelonInfo(typeof(AdvancedMod), "StellarDrive Demo TF Advanced", "0.11.0", "Romalaure")]
[assembly: MelonGame("CuriousOwlGames", "StellarDrive")]
[assembly: MelonOptionalDependencies("SDModKit")]
[assembly: HarmonyDontPatchAll]

namespace StellarDriveDemoTFAdvanced
{
    public class AdvancedMod : MelonMod
    {
        internal static MelonLogger.Instance Log { get; private set; }

        public override void OnInitializeMelon()
        {
            Log = LoggerInstance;
            Settings.Load();
            Items.ModItems.Create();
            Propulsion.EngineCatalog.LoadSettings();
            Power.PowerCatalog.LoadSettings();
            ApplyPatches();
            Worlds.WorldDevSteps.Install();
            Worlds.PlanetVisit.Install();
            Worlds.StationVisit.Install();
            ShipComputer.DashboardDevRender.Install();
            Worlds.TreeDevRender.Install();
            Worlds.StarMapProbe.Install();
            Items.ItemProbe.Install();
            Common.FpsProbe.Install();
            Power.MachineTest.Install();
            Parts.Showcase.Install();
            Parts.BuildMenuDev.Install();
            Construction.SnapTest.Install();
            Wiki.WikiDev.Install();
            Parts.PrefabDump.Install();
            Power.PortalTest.Install();
            Construction.DockingTest.Install();
            Log.Msg("ready");
        }

        // One patch class at a time: if a game update breaks one, the others (and the game) keep working
        private void ApplyPatches()
        {
            Type[] types;
            try
            {
                types = typeof(AdvancedMod).Assembly.GetTypes();
            }
            catch (System.Reflection.ReflectionTypeLoadException e)
            {
                types = e.Types.Where(t => t != null).ToArray();
            }
            int applied = 0, failed = 0;
            foreach (Type type in types.Where(t => t.GetCustomAttributes(typeof(HarmonyLib.HarmonyPatch), false).Length > 0))
            {
                try
                {
                    HarmonyInstance.CreateClassProcessor(type).Patch();
                    applied++;
                }
                catch (Exception e)
                {
                    failed++;
                    Log.Error($"patch {type.Name} not applied, that feature is off: {e.GetBaseException().Message}");
                }
            }
            Log.Msg($"{applied} patch(es) applied" + (failed > 0 ? $", {failed} failed" : ""));
        }

        public override void OnLateInitializeMelon()
        {
            try
            {
                Parts.UpgradeTableGuard.Install(HarmonyInstance);
                Power.ArsenalShield.Install(HarmonyInstance);
            }
            catch (Exception e)
            {
                Log.Warning("Upgrade Table guard not applied: " + e.GetBaseException().Message);
            }
            // Every part of this mod is a clone of a game part made by SDModKit
            if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "SDModKit"))
                RegisterParts();
            else
                Log.Warning("SDModKit is not installed: the ship computer screens and the engines are disabled");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RegisterParts()
        {
            Try("the build tabs", Parts.AdvancedTab.Declare);
            Try("the ship computer screens", DashboardParts.Register);
            Try("the engines", Propulsion.EngineParts.Register);
            Try("the battery and the circuit breaker", Power.PowerParts.Register);
            Try("the dark matter machines and the portal", Power.SuperiorParts.Register);
            Try("the structural parts", Construction.StructureParts.Register);
            Try("the wood, glass and iron parts", Construction.MaterialParts.Register);
            Try("the station modules", Construction.StationModules.Register);
            Try("the cartography table", Construction.ScannerTable.Register);
            _partsRegistered = true;
            ScheduleSelfTest();
        }

        private static void Try(string what, Action register)
        {
            try
            {
                register();
            }
            catch (Exception e)
            {
                Log.Error("could not register " + what + ": " + e);
            }
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            GameServices.Invalidate();
            TelemetryHub.Clear();
            Power.PowerGrid.Forget();
            Worlds.BlackHole.Forget();
            Worlds.ModPlanetState.Forget();
            Worlds.BlackHoleVisuals.Destroy();
            _inGame = sceneName == "MainGame";
            Worlds.AbandonedStations.OnSceneLoaded(_inGame);
            Worlds.LootCaches.OnSceneLoaded(_inGame);
            DevHarness.OnSceneLoaded(sceneName);
            if (_partsRegistered)
            {
                EnsureResearch();
                ScheduleSelfTest();
            }
        }

        private static bool _inGame;

        public override void OnUpdate()
        {
            DevHarness.Update();
            if (_inGame && Worlds.BlackHole.EnsureAnchor())
                Worlds.BlackHole.ServerUpdate();
            if (_inGame)
                Worlds.AbandonedStations.Update();
            if (_inGame)
                Worlds.LootCaches.Update();
            if (_inGame && GameServices.IsServer)
                UpdatePortals();
            if (_partsRegistered && Settings.SelfTest.Value)
                RunSelfTest();
            if (_partsRegistered && _inGame)
                TickResearch();
        }

        public override void OnLateUpdate()
        {
            if (_inGame)
                Worlds.BlackHoleVisuals.LateUpdate();
        }

        private static bool _partsRegistered;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ScheduleSelfTest() => SelfTest.Schedule();

        private static void UpdatePortals()
        {
            try
            {
                Power.Portal.Update();
            }
            catch (Exception e)
            {
                ErrorLog.Report("portal", e);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void TickResearch() => Parts.ModResearch.Tick();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void EnsureResearch()
        {
            try
            {
                Parts.ModResearch.Ensure();
            }
            catch (Exception e)
            {
                Log.Error("research: " + e);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RunSelfTest() => SelfTest.Update();
    }
}

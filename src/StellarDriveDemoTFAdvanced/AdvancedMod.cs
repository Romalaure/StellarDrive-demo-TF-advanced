using System;
using System.Linq;
using System.Runtime.CompilerServices;
using MelonLoader;
using StellarDriveDemoTFAdvanced;
using StellarDriveDemoTFAdvanced.Common;
using StellarDriveDemoTFAdvanced.ShipComputer;

[assembly: MelonInfo(typeof(AdvancedMod), "StellarDrive Demo TF Advanced", "0.45.0", "Romalaure")]
[assembly: MelonGame("CuriousOwlGames", "StellarDrive")]
[assembly: MelonOptionalDependencies("SDModKit")]
[assembly: HarmonyDontPatchAll]
// The compatibility mod with Station Water uses the planet catalog and shaper
[assembly: InternalsVisibleTo("StellarDriveTFAWater")]

namespace StellarDriveDemoTFAdvanced
{
    public class AdvancedMod : MelonMod
    {
        internal static MelonLogger.Instance Log { get; private set; }

        public override void OnInitializeMelon()
        {
            Log = LoggerInstance;
            ErrorLog.Run("Settings.Load", () => Settings.Load());
            ErrorLog.Run("Items.ModItems.Create", () => Items.ModItems.Create());
            ErrorLog.Run("Propulsion.EngineCatalog.LoadSettings", () => Propulsion.EngineCatalog.LoadSettings());
            ErrorLog.Run("Power.PowerCatalog.LoadSettings", () => Power.PowerCatalog.LoadSettings());
            ErrorLog.Run("ApplyPatches", () => ApplyPatches());
            ErrorLog.Run("Worlds.WorldDevSteps.Install", () => Worlds.WorldDevSteps.Install());
            ErrorLog.Run("Worlds.PlanetVisit.Install", () => Worlds.PlanetVisit.Install());
            ErrorLog.Run("Worlds.StationVisit.Install", () => Worlds.StationVisit.Install());
            ErrorLog.Run("ShipComputer.DashboardDevRender.Install", () => ShipComputer.DashboardDevRender.Install());
            ErrorLog.Run("Worlds.TreeDevRender.Install", () => Worlds.TreeDevRender.Install());
            ErrorLog.Run("Worlds.StarMapProbe.Install", () => Worlds.StarMapProbe.Install());
            ErrorLog.Run("Items.ItemProbe.Install", () => Items.ItemProbe.Install());
            ErrorLog.Run("Common.FpsProbe.Install", () => Common.FpsProbe.Install());
            ErrorLog.Run("Common.InteractProbe.Install", () => Common.InteractProbe.Install());
            ErrorLog.Run("Power.MachineTest.Install", () => Power.MachineTest.Install());
            ErrorLog.Run("Power.MiningLaserTest.Install", () => Power.MiningLaserTest.Install());
            ErrorLog.Run("Power.NuclearTest.Install", () => Power.NuclearTest.Install());
            ErrorLog.Run("Combat.PirateTest.Install", () => Combat.PirateTest.Install());
            ErrorLog.Run("TFItems.TFItemsMod.Initialize", () => TFItems.TFItemsMod.Initialize());
            ErrorLog.Run("Parts.Showcase.Install", () => Parts.Showcase.Install());
            ErrorLog.Run("Parts.BuildMenuDev.Install", () => Parts.BuildMenuDev.Install());
            ErrorLog.Run("Construction.SnapTest.Install", () => Construction.SnapTest.Install());
            ErrorLog.Run("Construction.HullSnapProbe.Install", () => Construction.HullSnapProbe.Install());
            ErrorLog.Run("Construction.JoinTest.Install", () => Construction.JoinTest.Install());
            ErrorLog.Run("Wiki.WikiDev.Install", () => Wiki.WikiDev.Install());
            ErrorLog.Run("Parts.PrefabDump.Install", () => Parts.PrefabDump.Install());
            ErrorLog.Run("Power.PortalTest.Install", () => Power.PortalTest.Install());
            ErrorLog.Run("Construction.DockingTest.Install", () => Construction.DockingTest.Install());
            ErrorLog.Run("Power.FieldDoorTest.Install", () => Power.FieldDoorTest.Install());
            ErrorLog.Run("Common.SaveHook.Install", () => Common.SaveHook.Install());
            ErrorLog.Run("Propulsion.LandingGearTest.Install", () => Propulsion.LandingGearTest.Install());
            ErrorLog.Run("Log.Msg", () => Log.Msg("ready"));
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
                if (type.Namespace != null && type.Namespace.StartsWith("StellarDriveDemoTFAdvanced.TFItems") && !TFItems.TFItemsMod.Active)
                    continue;
                if ((Array.IndexOf(TFItems.TFItemsMod.ToolPatches, type.Name) >= 0 || type.Namespace == "StellarDriveDemoTFAdvanced.TFItems.Paint") && !TFItems.TFItemsMod.ToolsActive)
                    continue;
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
            HarmonyLib.Harmony harmony = HarmonyInstance;
            ErrorLog.Run("Upgrade Table guard", () => Parts.UpgradeTableGuard.Install(harmony));
            ErrorLog.Run("Arsenal shield", () => Power.ArsenalShield.Install(harmony));
            ErrorLog.Run("Arsenal reinforced hull", () => Construction.ReinforcedHull.InstallArsenal(harmony));
            ErrorLog.Run("pirates", () => Combat.Pirates.Install(harmony));
            ErrorLog.Run("other mods' fixes", () => Common.OtherModsFixes.Install(harmony));
            ErrorLog.Run("profiler", () => Common.DevProfiler.Install(harmony));
            // Every part of this mod is a clone of a game part made by SDModKit
            if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "SDModKit"))
                RegisterParts();
            else
                Log.Warning("SDModKit is not installed: the ship computer screens and the engines are disabled");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RegisterParts()
        {
            HangWatch.Start();
            Try("the build tabs", Parts.AdvancedTab.Declare);
            Try("the ship computer screens", DashboardParts.Register);
            Try("the engines", Propulsion.EngineParts.Register);
            Try("the battery and the circuit breaker", Power.PowerParts.Register);
            Try("the dark matter machines and the portal", Power.SuperiorParts.Register);
            Try("the nuclear machines", Power.NuclearParts.Register);
            Try("the pirate fairings", Combat.PirateDecor.Register);
            Try("the wrecks' dressing", Worlds.WreckDecor.Register);
            Try("the loot chests", Worlds.LootChests.Register);
            Try("the structural parts", Construction.StructureParts.Register);
            Try("the signs", Construction.Signs.Register);
            Try("the diagonal doors", Construction.DiagonalDoors.Register);
            Try("the tractor beam", Power.TractorBeam.Register);
            Try("the wood, glass and iron parts", Construction.MaterialParts.Register);
            Try("the reinforced hull", Construction.ReinforcedHull.Register);
            Try("the hull windows", Construction.HullWindows.Register);
            Try("the ship mining laser", Power.MiningLaserParts.Register);
            Try("the station modules", Construction.StationModules.Register);
            Try("the cartography table", Construction.ScannerTable.Register);
            Try("the radars", Combat.Radar.Register);
            Try("the stealth generator", Combat.Cloak.Register);
            TFItems.TFItemsMod.RegisterParts(Try);
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
            ErrorLog.Run("HangWatch.Mark", () => HangWatch.Mark("scene loaded " + sceneName));
            ErrorLog.Run("HangWatch.OnScene", () => HangWatch.OnScene());
            ErrorLog.Run("GameServices.Invalidate", () => GameServices.Invalidate());
            ErrorLog.Run("TelemetryHub.Clear", () => TelemetryHub.Clear());
            ErrorLog.Run("Power.PowerGrid.Forget", () => Power.PowerGrid.Forget());
            ErrorLog.Run("Worlds.BlackHole.Forget", () => Worlds.BlackHole.Forget());
            ErrorLog.Run("Worlds.ModPlanetState.Forget", () => Worlds.ModPlanetState.Forget());
            ErrorLog.Run("Worlds.BlackHoleVisuals.Destroy", () => Worlds.BlackHoleVisuals.Destroy());
            _inGame = sceneName == "MainGame";
            ErrorLog.Run("Worlds.AbandonedStations.OnSceneLoaded", () => Worlds.AbandonedStations.OnSceneLoaded(_inGame));
            ErrorLog.Run("Worlds.LootCaches.OnSceneLoaded", () => Worlds.LootCaches.OnSceneLoaded(_inGame));
            if (_inGame && TFItems.TFItemsMod.Active)
                ErrorLog.Run("TFItems.Devices.TraderLedger.Load", () => TFItems.Devices.TraderLedger.Load(Worlds.LootCaches.WorldDirectory()));
            ErrorLog.Run("Combat.Pirates.OnSceneLoaded", () => Combat.Pirates.OnSceneLoaded(_inGame));
            ErrorLog.Run("Combat.Radar.OnSceneLoaded", () => Combat.Radar.OnSceneLoaded());
            ErrorLog.Run("Combat.LootPing.OnSceneLoaded", () => Combat.LootPing.OnSceneLoaded());
            ErrorLog.Run("TFItems.TFItemsMod.OnSceneLoaded", () => TFItems.TFItemsMod.OnSceneLoaded());
            ErrorLog.Run("DevHarness.OnSceneLoaded", () => DevHarness.OnSceneLoaded(sceneName));
            if (_partsRegistered)
            {
                ErrorLog.Run("EnsureResearch", () => EnsureResearch());
                ErrorLog.Run("ScheduleSelfTest", () => ScheduleSelfTest());
            }
        }

        private static bool _inGame;

        public override void OnUpdate()
        {
            ErrorLog.Run("HangWatch.Tick", () => HangWatch.Tick());
            ErrorLog.Run("DevProfiler.Frame", () => DevProfiler.Frame());
            ErrorLog.Run("DevHarness.Update", () => DevHarness.Update());
            ErrorLog.Run("FramePacing.Update", () => FramePacing.Update());
            ErrorLog.Run("Worlds.FastFlight.Update", () => Worlds.FastFlight.Update());
            if (_inGame)
                ErrorLog.Run("StabilityProbe.Update", () => StabilityProbe.Update());
            if (_inGame && Worlds.BlackHole.EnsureAnchor())
                ErrorLog.Run("Worlds.BlackHole.ServerUpdate", () => Worlds.BlackHole.ServerUpdate());
            if (_inGame)
                ErrorLog.Run("Worlds.AbandonedStations.Update", () => Worlds.AbandonedStations.Update());
            if (_inGame && GameServices.IsServer)
                ErrorLog.Run("Worlds.LootChests.Update", () => Worlds.LootChests.Update());
            if (_inGame && TFItems.TFItemsMod.Active)
                ErrorLog.Run("Construction.BuildTilt.Update", () => Construction.BuildTilt.Update());
            if (_inGame && TFItems.TFItemsMod.Active)
                ErrorLog.Run("Construction.FurnitureUses.Update", () => Construction.FurnitureUses.Update());
            if (_inGame)
                ErrorLog.Run("Worlds.LootCaches.Update", () => Worlds.LootCaches.Update());
            if (_inGame && GameServices.IsServer)
                ErrorLog.Run("UpdatePortals", () => UpdatePortals());
            if (_inGame)
                ErrorLog.Run("Combat.Pirates.Update", () => Combat.Pirates.Update());
            if (_inGame)
                ErrorLog.Run("Combat.LootPing.Update", () => Combat.LootPing.Update());
            if (_inGame)
                ErrorLog.Run("Worlds.Weather.Update", () => Worlds.Weather.Update());
            if (_inGame)
                ErrorLog.Run("Worlds.Aurora.Update", () => Worlds.Aurora.Update());
            if (_inGame && _partsRegistered)
                ErrorLog.Run("UpdateRadars", () => UpdateRadars());
            if (_partsRegistered)
                ErrorLog.Run("Common.SaveHook.Update", () => Common.SaveHook.Update());
            ErrorLog.Run("TFItems.TFItemsMod.Update", () => TFItems.TFItemsMod.Update());
            if (_partsRegistered && Settings.SelfTest.Value)
                ErrorLog.Run("RunSelfTest", () => RunSelfTest());
            if (_partsRegistered && _inGame)
                ErrorLog.Run("TickResearch", () => TickResearch());
        }

        public override void OnLateUpdate()
        {
            if (_inGame)
                ErrorLog.Run("Worlds.BlackHoleVisuals.LateUpdate", () => Worlds.BlackHoleVisuals.LateUpdate());
            if (_partsRegistered)
                ErrorLog.Run("TFItems.TFItemsMod.LateUpdate", () => TFItems.TFItemsMod.LateUpdate());
        }

        private static bool _partsRegistered;

        public override void OnGUI()
        {
            // Photo mode: nothing of the mod on screen but its own help
            if (_partsRegistered && TFItems.TFItemsMod.Active && (TFItems.Devices.PhotoMode.Active || TFItems.Devices.PhotoMode.Capturing))
            {
                ErrorLog.RunGui("TFItems.Devices.PhotoMode.Draw", () => TFItems.Devices.PhotoMode.Draw());
                return;
            }
            if (_inGame)
                ErrorLog.RunGui("Combat.Radar.OnGUI", () => Combat.Radar.OnGUI());
            if (_inGame && TFItems.TFItemsMod.Active)
                ErrorLog.RunGui("Construction.BuildTilt.Draw", () => Construction.BuildTilt.Draw());
            if (_inGame)
                ErrorLog.RunGui("Combat.LootPing.OnGUI", () => Combat.LootPing.OnGUI());
            if (_inGame)
                ErrorLog.RunGui("Worlds.Weather.DrawFlash", () => Worlds.Weather.DrawFlash());
            if (_partsRegistered)
                ErrorLog.RunGui("TFItems.TFItemsMod.OnGUI", () => TFItems.TFItemsMod.OnGUI());
        }

        private static void UpdateRadars()
        {
            try
            {
                Combat.Radar.Update();
            }
            catch (Exception e)
            {
                ErrorLog.Report("radar", e);
            }
        }

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

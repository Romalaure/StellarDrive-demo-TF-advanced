using MelonLoader;

namespace StellarDriveDemoTFAdvanced.Common
{
    internal static class Settings
    {
        private static MelonPreferences_Category _category;

        public static MelonPreferences_Entry<float> ComputerRefreshRate { get; private set; }
        public static MelonPreferences_Entry<float> ComputerViewDistance { get; private set; }
        public static MelonPreferences_Entry<float> TrafficRange { get; private set; }
        public static MelonPreferences_Entry<float> CollisionWarningSeconds { get; private set; }
        public static MelonPreferences_Entry<float> CollisionWarningDistance { get; private set; }
        public static MelonPreferences_Entry<float> DescentWarningSpeed { get; private set; }
        public static MelonPreferences_Entry<float> DescentWarningAltitude { get; private set; }
        public static MelonPreferences_Entry<float> SpinWarningRate { get; private set; }
        public static MelonPreferences_Entry<bool> SelfTest { get; private set; }
        public static MelonPreferences_Entry<bool> NewPlanets { get; private set; }
        public static MelonPreferences_Entry<string> Language { get; private set; }
        public static MelonPreferences_Entry<bool> Erebos { get; private set; }
        public static MelonPreferences_Entry<bool> ResearchUnlocks { get; private set; }
        public static MelonPreferences_Entry<bool> BlackHoleEnabled { get; private set; }
        public static MelonPreferences_Entry<bool> AbandonedStations { get; private set; }
        public static MelonPreferences_Entry<float> TerrainChunkBuffer { get; private set; }
        public static MelonPreferences_Entry<bool> DevPlanetsNearby { get; private set; }
        public static MelonPreferences_Entry<bool> DevVisitPlanets { get; private set; }
        public static MelonPreferences_Entry<int> DevVisitPlanetId { get; private set; }
        public static MelonPreferences_Entry<float> DevSpeedTest { get; private set; }
        public static MelonPreferences_Entry<bool> DevMachineTest { get; private set; }
        public static MelonPreferences_Entry<bool> DevMiningLaserTest { get; private set; }
        public static MelonPreferences_Entry<int> DevMachineShield { get; private set; }
        public static MelonPreferences_Entry<int> DevNuclearTest { get; private set; }
        public static MelonPreferences_Entry<int> DevPirateTest { get; private set; }
        public static MelonPreferences_Entry<bool> DevPirateShowcase { get; private set; }
        public static MelonPreferences_Entry<bool> EngineSeatControl { get; private set; }
        public static MelonPreferences_Entry<string> DevNuclearMode { get; private set; }
        public static MelonPreferences_Entry<bool> Pirates { get; private set; }
        public static MelonPreferences_Entry<float> PirateIntervalMinutes { get; private set; }
        public static MelonPreferences_Entry<float> PirateSafeRadiusKm { get; private set; }
        public static MelonPreferences_Entry<float> PirateDangerStepKm { get; private set; }
        public static MelonPreferences_Entry<float> PirateRaidStartKm { get; private set; }
        public static MelonPreferences_Entry<float> PirateApproachKmPerSecond { get; private set; }
        public static MelonPreferences_Entry<bool> RadarAlarm { get; private set; }
        public static MelonPreferences_Entry<float> LootPingMinutes { get; private set; }
        public static MelonPreferences_Entry<bool> SteadyFrameRate { get; private set; }
        public static MelonPreferences_Entry<bool> Weather { get; private set; }
        public static MelonPreferences_Entry<bool> LowPerformance { get; private set; }
        public static MelonPreferences_Entry<bool> ClassicTextures { get; private set; }
        public static MelonPreferences_Entry<bool> OtherModsFixes { get; private set; }
        public static MelonPreferences_Entry<string> DevWeather { get; private set; }
        public static MelonPreferences_Entry<int> DevPortalTest { get; private set; }
        public static MelonPreferences_Entry<bool> DevDockingTest { get; private set; }
        public static MelonPreferences_Entry<bool> DevFieldDoorTest { get; private set; }
        public static MelonPreferences_Entry<int> DevLandingGearTest { get; private set; }
        public static MelonPreferences_Entry<string> DevOutfit { get; private set; }
        public static MelonPreferences_Entry<bool> DevTeleporterTest { get; private set; }
        public static MelonPreferences_Entry<string> DevSnapshotParts { get; private set; }
        public static MelonPreferences_Entry<string> DevShowcase { get; private set; }
        public static MelonPreferences_Entry<bool> DevBuildMenu { get; private set; }
        public static MelonPreferences_Entry<bool> DevSnapTest { get; private set; }
        public static MelonPreferences_Entry<bool> DevWiki { get; private set; }
        public static MelonPreferences_Entry<bool> DevPaintTest { get; private set; }
        public static MelonPreferences_Entry<bool> DevInteractTest { get; private set; }
        public static MelonPreferences_Entry<bool> DevMapTest { get; private set; }
        public static MelonPreferences_Entry<string> DevPrefabDump { get; private set; }
        public static MelonPreferences_Entry<bool> DevVisitBlackHole { get; private set; }
        public static MelonPreferences_Entry<bool> DevVisitStations { get; private set; }
        public static MelonPreferences_Entry<string> DevWorld { get; private set; }
        public static MelonPreferences_Entry<string> DevDisable { get; private set; }
        public static MelonPreferences_Entry<bool> DevProfile { get; private set; }
        public static MelonPreferences_Entry<bool> DevProfileGame { get; private set; }
        public static MelonPreferences_Entry<bool> DevFlyTest { get; private set; }
        public static MelonPreferences_Entry<float> DevQuitSeconds { get; private set; }

        public static void Load()
        {
            _category = MelonPreferences.CreateCategory("TFAdvanced", "StellarDrive Demo TF Advanced");
            ComputerRefreshRate = _category.CreateEntry("ComputerRefreshRate", 4f,
                description: "Ship computer screen refreshes per second (1 to 20). A ship is read once per refresh, whatever its number of screens.");
            ComputerViewDistance = _category.CreateEntry("ComputerViewDistance", 40f,
                description: "Ship computer screens farther than this from your camera (meters) stop refreshing.");
            TrafficRange = _category.CreateEntry("TrafficRange", 3000f,
                description: "Ships farther than this (meters, center to center) are not listed in the traffic readout.");
            CollisionWarningSeconds = _category.CreateEntry("CollisionWarningSeconds", 12f,
                description: "Warn when another ship's closest approach comes within this many seconds...");
            CollisionWarningDistance = _category.CreateEntry("CollisionWarningDistance", 30f,
                description: "...and that closest approach is nearer than this many meters (center to center).");
            DescentWarningSpeed = _category.CreateEntry("DescentWarningSpeed", 20f,
                description: "Warn when descending faster than this (m/s)...");
            DescentWarningAltitude = _category.CreateEntry("DescentWarningAltitude", 400f,
                description: "...while lower than this altitude above the planet's mean radius (m).");
            SpinWarningRate = _category.CreateEntry("SpinWarningRate", 60f,
                description: "Warn when the ship turns faster than this (degrees per second).");
            SelfTest = _category.CreateEntry("SelfTest", false,
                description: "For mod development: on the main menu, build every part of this mod and log what was built.");
            ResearchUnlocks = _category.CreateEntry("ResearchUnlocks", true,
                description: "The mod's parts are unlocked at the research table (its own branch of techs). Off: every part is available from the start. In multiplayer every player needs the same setting.");
            Erebos = _category.CreateEntry("Erebos", true,
                description: "The Erebos galaxy: a second star system with four planets, a black hole and wrecks, reached through a portal. Needs NewPlanets. In multiplayer every player needs the same setting.");
            Language = _category.CreateEntry("Language", "auto",
                description: "Language of the mod's texts: auto (the game's language), fr, en or es. Part names and descriptions change on the next start.");
            NewPlanets = _category.CreateEntry("NewPlanets", true,
                description: "Add the mod's planets to the star system. In multiplayer every player needs the same setting.");
            BlackHoleEnabled = _category.CreateEntry("BlackHole", true,
                description: "The black hole near TF Petra: gravity, tidal destruction, event horizon, warp stop. In multiplayer every player needs the same setting.");
            TerrainChunkBuffer = _category.CreateEntry("TerrainChunkBuffer", 1.5f,
                description: "Room for terrain chunks on the GPU, times the game's (320 in High quality). Above 1, high mountains no longer leave holes in the ground (each extra 0.1 costs about 38 MB of video memory per nearby planet). 1 = the game's own.");
            AbandonedStations = _category.CreateEntry("AbandonedStations", true,
                description: "Create the abandoned stations (with their beacons) in worlds that do not have them yet. The host decides.");
            DevPlanetsNearby = _category.CreateEntry("DevPlanetsNearby", false,
                description: "For mod development: place the mod's planets a few thousand km from the starting planet, to see them from its surface.");
            DevVisitPlanets = _category.CreateEntry("DevVisitPlanets", false,
                description: "For mod development: with DevWorld, move the player onto each mod planet and take screenshots there.");
            DevSnapshotParts = _category.CreateEntry("DevSnapshotParts", "",
                description: "For mod development: with SelfTest, the parts to render (ids or game asset names, comma separated).");
            DevShowcase = _category.CreateEntry("DevShowcase", "",
                description: "For mod development: with DevWorld, part ids (comma separated) set out on a test ship beside the player and photographed.");
            DevBuildMenu = _category.CreateEntry("DevBuildMenu", false,
                description: "For mod development: with DevWorld, open the build menu, photograph each mod tab and log its layout.");
            DevPrefabDump = _category.CreateEntry("DevPrefabDump", "",
                description: "For mod development: with DevWorld, part ids (comma separated) whose prefab tree is written to dev/prefab_dump.txt.");
            DevMapTest = _category.CreateEntry("DevMapTest", false,
                description: "For mod development: with DevShowcase (a star map in the list), let the test ship fall and log its star map's planets frame by frame.");
            DevInteractTest = _category.CreateEntry("DevInteractTest", false,
                description: "For mod development: with DevShowcase, list the modules' interactions and use the light switches and a respawn bunk.");
            DevPaintTest = _category.CreateEntry("DevPaintTest", false,
                description: "For mod development: with DevShowcase, paint the parts with the TF mod (exterior red, interior blue) before the photos.");
            DevWiki = _category.CreateEntry("DevWiki", false,
                description: "For mod development: with DevWorld, open the guide on the mod's wiki and photograph a few pages.");
            DevSnapTest = _category.CreateEntry("DevSnapTest", false,
                description: "For mod development: with DevWorld, ask the build tool where parts go when aimed at placed parts, and photograph the result.");
            DevDockingTest = _category.CreateEntry("DevDockingTest", false,
                description: "For mod development: with DevWorld, two ships with docking modules try to dock high in the air.");
            DevTeleporterTest = _category.CreateEntry("DevTeleporterTest", false,
                description: "For mod development: with DevWorld, list the teleporter's destinations, photograph its window, go to TF Gaia then to a station.");
            DevOutfit = _category.CreateEntry("DevOutfit", "",
                description: "For mod development: with DevWorld, wear this outfit (cabin format) and photograph the cabin's window.");
            DevLandingGearTest = _category.CreateEntry("DevLandingGearTest", 0,
                description: "For mod development: with DevWorld, a planet id: drop a test ship on landing gear there and follow how it lands.");
            DevFieldDoorTest = _category.CreateEntry("DevFieldDoorTest", false,
                description: "For mod development: with DevWorld, check in the game's room code that a force field door seals a doorway.");
            DevPortalTest = _category.CreateEntry("DevPortalTest", 0,
                description: "For mod development: with DevWorld, a planet id: a test ship opens a portal to that planet and flies through it.");
            DevMachineTest = _category.CreateEntry("DevMachineTest", false,
                description: "For mod development: with DevWorld, drop a wired test ship with a reactor, a force field and a condenser above the player.");
            DevMiningLaserTest = _category.CreateEntry("DevMiningLaserTest", false,
                description: "For mod development: with DevWorld, a test ship of reinforced hull with a mining laser and an item terminal lands next to the nearest rock and mines it.");
            DevNuclearMode = _category.CreateEntry("DevNuclearMode", "seat",
                description: "For mod development: with DevNuclearTest, how the engine is commanded: seat, valve, signal, transmitter, sharedvalve.");
            EngineSeatControl = _category.CreateEntry("EngineSeatControl", true,
                description: "Nuclear and dark matter engines push only while a pilot at a seat of the ship asks for thrust their way (false: like the Chemical Thruster, they push whenever fed).");
            DevPirateShowcase = _category.CreateEntry("DevPirateShowcase", false,
                description: "For mod development: with DevWorld, each pirate design held beside the player and photographed.");
            DevPirateTest = _category.CreateEntry("DevPirateTest", 0,
                description: "For mod development: with DevWorld, pirates attack the ship nearest the player (1 scout, 2 corsair, 3 raider, 4 cruiser, 5 random, 6 base, 7 hornet, 8 lancer, 9 torpedo boat, 10 dreadnought, 11-15 drawn for danger 1-5, 16 only a loot capsule and its ping).");
            Pirates = _category.CreateEntry("Pirates", true,
                description: "Pirate ships (with the Arsenal mod only, whose weapons they use) now and then attack a player's ship flying in space.");
            PirateIntervalMinutes = _category.CreateEntry("PirateIntervalMinutes", 12f,
                description: "Average time in space between two pirate attacks (minutes).");
            PirateSafeRadiusKm = _category.CreateEntry("PirateSafeRadiusKm", 2000f,
                description: "Safe zone around the home planet (442 Otrera b): no pirate attack within this distance of its centre, and pirates break off there (km, 0: none).");
            PirateDangerStepKm = _category.CreateEntry("PirateDangerStepKm", 5000000f,
                description: "Pirates get tougher the farther you fly from the home planet: danger 1 (scouts only) just outside the safe zone, one level more every this many km, 4 at most in the Otrera system, 5 in the Erebos galaxy.");
            PirateRaidStartKm = _category.CreateEntry("PirateRaidStartKm", 25000f,
                description: "Pirate raids are detected this far away (km) and close in before attacking: a radar sees them coming from its range.");
            PirateApproachKmPerSecond = _category.CreateEntry("PirateApproachKmPerSecond", 250f,
                description: "Speed of an incoming pirate raid (km/s): 25 000 km at 250 km/s = 100 s of warning with the deep space radar.");
            LootPingMinutes = _category.CreateEntry("LootPingMinutes", 10f,
                description: "When a pirate ship is destroyed, a marker on screen shows where its loot capsule is, for this many minutes or until the capsule is gone (0: no marker).");
            RadarAlarm = _category.CreateEntry("RadarAlarm", true,
                description: "A message on screen when a radar of your ship detects an incoming pirate raid.");
            Weather = _category.CreateEntry("Weather", true,
                description: "Weather on the mod's planets around you when you stand outside: rain and storms with lightning (Gaïa, Verdance, Sylvara, Thalassa), snow (Glacia, Gaïa up high), sandstorms (Aridia), ash (Pyros). Visual only.");
            OtherModsFixes = _category.CreateEntry("OtherModsFixes", true,
                description: "Fix the frame-rate costs of other mods when installed: StationKit's research check (run every frame by the submarine pack, 2.4 ms a frame) runs once every 2 s.");
            ClassicTextures = _category.CreateEntry("ClassicTextures", false,
                description: "The mod's parts in plain colors, as before version 0.41 (no plate texture, rivets or relief). Takes effect on the next start.");
            LowPerformance = _category.CreateEntry("LowPerformance", false,
                description: "For modest computers: one forest per planet instead of several (TF Gaïa), lighter rain and snow, no auroras. The forests change on the next start.");
            DevWeather = _category.CreateEntry("DevWeather", "",
                description: "For mod development: force the weather (Clear, Rain, Storm, Snow, Sand, Ash). Empty: the planets' own.");
            SteadyFrameRate = _category.CreateEntry("SteadyFrameRate", true,
                description: "With vertical sync on and the game slower than the screen (60 fps on 165 Hz), frames alternate 2 and 3 screen refreshes and turning ships jerk: wait the same number of refreshes for every frame (55 fps on 165 Hz, steady). Never under 45 fps.");
            DevNuclearTest = _category.CreateEntry("DevNuclearTest", 0,
                description: "For mod development: with DevWorld, an engine part id: a wired test ship with a centrifuge, a nuclear reactor and that engine.");
            DevMachineShield = _category.CreateEntry("DevMachineShield", 0,
                description: "For mod development: with DevMachineTest, the force field generator's part id (0: the basic one).");
            DevSpeedTest = _category.CreateEntry("DevSpeedTest", 0f,
                description: "For mod development: with DevWorld and DevVisitPlanetId, carry the player low over that planet at this speed (m/s) for 40 s and log the frame rate.");
            DevVisitPlanetId = _category.CreateEntry("DevVisitPlanetId", 0,
                description: "For mod development: with DevVisitPlanets, visit only this planet (0: all).");
            DevFlyTest = _category.CreateEntry("DevFlyTest", false,
                description: "For mod development: with DevWorld, the seats of the ship nearest the player ask for thrust, then a turn, then both (from 30 s, 10 s each).");
            DevProfile = _category.CreateEntry("DevProfile", false,
                description: "For mod development: with DevWorld, times this mod's per-frame methods and patches and logs the costliest every 15 s.");
            DevProfileGame = _category.CreateEntry("DevProfileGame", false,
                description: "For mod development: with DevProfile, also times the per-frame callbacks of the game and of the other mods.");
            DevDisable = _category.CreateEntry("DevDisable", "",
                description: "For mod development: with DevWorld, features switched off (comma separated): engines, torque, gravity, gyro, sync, pirates.");
            DevVisitBlackHole = _category.CreateEntry("DevVisitBlackHole", false,
                description: "For mod development: with DevWorld, look for the black hole from TF Petra and take screenshots.");
            DevVisitStations = _category.CreateEntry("DevVisitStations", false,
                description: "For mod development: with DevWorld, list beacons and visit the abandoned stations, taking screenshots.");
            DevWorld = _category.CreateEntry("DevWorld", "",
                description: "For mod development: full path of a world save (a copy) loaded in single player from the main menu, then probed and screenshotted. Empty: off.");
            DevQuitSeconds = _category.CreateEntry("DevQuitSeconds", 0f,
                description: "For mod development: with DevWorld, quit the game this many seconds after the world loads. 0: never.");
        }
    }
}

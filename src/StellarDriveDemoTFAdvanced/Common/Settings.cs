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
        public static MelonPreferences_Entry<bool> Erebos { get; private set; }
        public static MelonPreferences_Entry<bool> ResearchUnlocks { get; private set; }
        public static MelonPreferences_Entry<bool> BlackHoleEnabled { get; private set; }
        public static MelonPreferences_Entry<bool> AbandonedStations { get; private set; }
        public static MelonPreferences_Entry<float> TerrainChunkBuffer { get; private set; }
        public static MelonPreferences_Entry<bool> DevPlanetsNearby { get; private set; }
        public static MelonPreferences_Entry<bool> DevVisitPlanets { get; private set; }
        public static MelonPreferences_Entry<int> DevVisitPlanetId { get; private set; }
        public static MelonPreferences_Entry<bool> DevMachineTest { get; private set; }
        public static MelonPreferences_Entry<int> DevMachineShield { get; private set; }
        public static MelonPreferences_Entry<int> DevNuclearTest { get; private set; }
        public static MelonPreferences_Entry<int> DevPirateTest { get; private set; }
        public static MelonPreferences_Entry<bool> DevPirateShowcase { get; private set; }
        public static MelonPreferences_Entry<bool> EngineSeatControl { get; private set; }
        public static MelonPreferences_Entry<string> DevNuclearMode { get; private set; }
        public static MelonPreferences_Entry<bool> Pirates { get; private set; }
        public static MelonPreferences_Entry<float> PirateIntervalMinutes { get; private set; }
        public static MelonPreferences_Entry<int> DevPortalTest { get; private set; }
        public static MelonPreferences_Entry<bool> DevDockingTest { get; private set; }
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
            DevPortalTest = _category.CreateEntry("DevPortalTest", 0,
                description: "For mod development: with DevWorld, a planet id: a test ship opens a portal to that planet and flies through it.");
            DevMachineTest = _category.CreateEntry("DevMachineTest", false,
                description: "For mod development: with DevWorld, drop a wired test ship with a reactor, a force field and a condenser above the player.");
            DevNuclearMode = _category.CreateEntry("DevNuclearMode", "seat",
                description: "For mod development: with DevNuclearTest, how the engine is commanded: seat, valve, signal, transmitter, sharedvalve.");
            EngineSeatControl = _category.CreateEntry("EngineSeatControl", true,
                description: "Nuclear and dark matter engines push only while a pilot at a seat of the ship asks for thrust their way (false: like the Chemical Thruster, they push whenever fed).");
            DevPirateShowcase = _category.CreateEntry("DevPirateShowcase", false,
                description: "For mod development: with DevWorld, each pirate design held beside the player and photographed.");
            DevPirateTest = _category.CreateEntry("DevPirateTest", 0,
                description: "For mod development: with DevWorld, pirates attack the ship nearest the player (1 scout, 2 corsair, 3 raider, 4 cruiser, 5 random).");
            Pirates = _category.CreateEntry("Pirates", true,
                description: "Pirate ships (with the Arsenal mod only, whose weapons they use) now and then attack a player's ship flying in space.");
            PirateIntervalMinutes = _category.CreateEntry("PirateIntervalMinutes", 12f,
                description: "Average time in space between two pirate attacks (minutes).");
            DevNuclearTest = _category.CreateEntry("DevNuclearTest", 0,
                description: "For mod development: with DevWorld, an engine part id: a wired test ship with a centrifuge, a nuclear reactor and that engine.");
            DevMachineShield = _category.CreateEntry("DevMachineShield", 0,
                description: "For mod development: with DevMachineTest, the force field generator's part id (0: the basic one).");
            DevVisitPlanetId = _category.CreateEntry("DevVisitPlanetId", 0,
                description: "For mod development: with DevVisitPlanets, visit only this planet (0: all).");
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

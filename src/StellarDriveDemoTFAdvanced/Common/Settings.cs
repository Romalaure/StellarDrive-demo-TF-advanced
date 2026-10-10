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
        public static MelonPreferences_Entry<bool> BlackHoleEnabled { get; private set; }
        public static MelonPreferences_Entry<bool> AbandonedStations { get; private set; }
        public static MelonPreferences_Entry<bool> DevPlanetsNearby { get; private set; }
        public static MelonPreferences_Entry<bool> DevVisitPlanets { get; private set; }
        public static MelonPreferences_Entry<int> DevVisitPlanetId { get; private set; }
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
            NewPlanets = _category.CreateEntry("NewPlanets", true,
                description: "Add the mod's planets to the star system. In multiplayer every player needs the same setting.");
            BlackHoleEnabled = _category.CreateEntry("BlackHole", true,
                description: "The black hole near TF Petra: gravity, tidal destruction, event horizon, warp stop. In multiplayer every player needs the same setting.");
            AbandonedStations = _category.CreateEntry("AbandonedStations", true,
                description: "Create the abandoned stations (with their beacons) in worlds that do not have them yet. The host decides.");
            DevPlanetsNearby = _category.CreateEntry("DevPlanetsNearby", false,
                description: "For mod development: place the mod's planets a few thousand km from the starting planet, to see them from its surface.");
            DevVisitPlanets = _category.CreateEntry("DevVisitPlanets", false,
                description: "For mod development: with DevWorld, move the player onto each mod planet and take screenshots there.");
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

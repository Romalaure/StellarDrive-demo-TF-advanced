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
        }
    }
}

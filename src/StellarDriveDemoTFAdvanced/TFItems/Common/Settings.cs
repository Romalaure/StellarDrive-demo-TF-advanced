using MelonLoader;

namespace StellarDriveDemoTFAdvanced.TFItems.Common
{
    internal static class Settings
    {
        private static MelonPreferences_Category _category;

        public static MelonPreferences_Entry<bool> PaintAllParts { get; private set; }
        public static MelonPreferences_Entry<bool> DumpOnWorldLoad { get; private set; }
        public static MelonPreferences_Entry<bool> AdaptiveLamps { get; private set; }
        public static MelonPreferences_Entry<bool> CopilotCanFly { get; private set; }
        public static MelonPreferences_Entry<string> TabletKey { get; private set; }
        public static MelonPreferences_Entry<int> CameraFps { get; private set; }
        public static MelonPreferences_Entry<int> CameraResolution { get; private set; }
        public static MelonPreferences_Entry<bool> CameraSky { get; private set; }
        public static MelonPreferences_Entry<int> TabletSize { get; private set; }
        public static MelonPreferences_Entry<float> LampLightStrength { get; private set; }
        public static MelonPreferences_Entry<string> UseKey { get; private set; }
        public static MelonPreferences_Entry<string> ThirdPersonKey { get; private set; }
        public static MelonPreferences_Entry<string> PhotoKey { get; private set; }
        public static MelonPreferences_Entry<string> BuildTiltKey { get; private set; }
        public static MelonPreferences_Entry<float> ThirdPersonDistance { get; private set; }
        public static MelonPreferences_Entry<float> RadioVolume { get; private set; }
        public static MelonPreferences_Entry<string> RadioMuted { get; private set; }
        public static MelonPreferences_Entry<string> Outfit { get; private set; }
        public static MelonPreferences_Entry<string> RadioLockedPlayers { get; private set; }
        public static MelonPreferences_Entry<string> OutfitPrimary { get; private set; }
        public static MelonPreferences_Entry<string> OutfitSecondary { get; private set; }

        public static void Load()
        {
            _category = MelonPreferences.CreateCategory("TFAdvancedObjects", "StellarDrive Demo TF Advanced - TF objects (lamps, camera, radio, capsule, rack)");
            PaintAllParts = _category.CreateEntry("PaintAllParts", true,
                description: "Let the paint tool color doors, machines and every other placed part, not only walls and floors.");
            DumpOnWorldLoad = _category.CreateEntry("DumpOnWorldLoad", true,
                description: "Write every item and part definition to UserData/TF/dump.txt the first time a world loads. For mod development.");
            AdaptiveLamps = _category.CreateEntry("AdaptiveLamps", true,
                description: "Lamps shine brighter at night and indoors, softer in open daylight.");
            CopilotCanFly = _category.CreateEntry("CopilotCanFly", false,
                description: "Let the TF copilot seat fly the ship like the pilot seat. Off: like every TF chair, it has no effect on the ship.");
            TabletKey = _category.CreateEntry("TabletKey", "F9",
                description: "Key (UnityEngine.InputSystem.Key name) that opens and closes the camera tablet. Empty disables it.");
            CameraFps = _category.CreateEntry("CameraFps", 15,
                description: "Refreshes per second of the camera image on the tablet. Cameras only render while the tablet is open.");
            CameraResolution = _category.CreateEntry("CameraResolution", 960,
                description: "Largest width in pixels of the camera image. The image is never rendered wider than the tablet screen shows it.");
            // (third key: "CameraSky" off was black before the distant camera was copied, "CameraAtmosphere" on cost a lot of fps)
            CameraSky = _category.CreateEntry("CameraPlanetSky", false,
                description: "Render the planets' atmosphere and clouds in the camera image (space, stars and planets are always drawn). Costly: the image is then rendered at screen size.");
            TabletSize = _category.CreateEntry("TabletSize", 0,
                description: "Camera tablet size: 0 small, 1 large, 2 full screen. Z in the tablet cycles it.");
            LampLightStrength = _category.CreateEntry("LampLightStrength", 1f,
                description: "How strongly TF lamps light up the walls, floors and parts around them. 0 turns their lighting off.");
            UseKey = _category.CreateEntry("UseKey", "T",
                description: "Key (UnityEngine.InputSystem.Key name) to use the TF part you look at: radio, wardrobe, teleport capsule.");
            ThirdPersonKey = _category.CreateEntry("ThirdPersonKey", "V",
                description: "Key (UnityEngine.InputSystem.Key name) that switches between first and third person while flying from the pilot seat.");
            BuildTiltKey = _category.CreateEntry("BuildTiltKey", "V",
                description: "Key (UnityEngine.InputSystem.Key name) that turns the TF Construction part being placed: upright, tipped forward, on its side, upside down. Empty disables it.");
            PhotoKey = _category.CreateEntry("PhotoKey", "F8",
                description: "Key (UnityEngine.InputSystem.Key name) that enters and leaves photo mode (free camera, filters, photos into UserData/TFAdvanced/photos). Empty disables it.");
            ThirdPersonDistance = _category.CreateEntry("ThirdPersonDistance", 1f,
                description: "Third person camera distance, as a multiple of the ship size. The mouse wheel changes it in flight.");
            RadioVolume = _category.CreateEntry("RadioVolume", 0.7f,
                description: "Volume of TF radios, from 0 to 1.");
            Outfit = _category.CreateEntry("Outfit", "",
                description: "Your outfit, set in the cosmetic cabin (suit|helmet|backpack|visor colors, hat, hat color, glasses, lens color, jacket, jacket color).");
            RadioMuted = _category.CreateEntry("RadioMuted", "",
                description: "Radios you muted for yourself (ship:part, comma separated), set in the radio's window.");
            RadioLockedPlayers = _category.CreateEntry("RadioLockedPlayers", "",
                description: "When you host: players (names, comma separated) who may not change what the radios play, set in the radio's window.");
            OutfitPrimary = _category.CreateEntry("OutfitPrimary", "",
                description: "Your suit's main color (hex, like 3A7BD5), set in the TF wardrobe. Empty keeps the game's color.");
            OutfitSecondary = _category.CreateEntry("OutfitSecondary", "",
                description: "Your helmet and backpack color (hex), set in the TF wardrobe. Empty keeps the game's color.");
        }
    }
}

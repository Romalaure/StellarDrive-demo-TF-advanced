using SDModKit.Game;
using StellarDriveDemoTFAdvanced.TFItems.Lights;

namespace StellarDriveDemoTFAdvanced.TFItems.Common
{
    /// <summary>
    /// The build tab of the objects moved from the TF mod: "TF Équipement". Lamps on the first
    /// rows, then the devices (camera, teleport capsule, radio, tool rack), then the infinite parts.
    /// </summary>
    internal static class TFTab
    {
        public const string Name = "TF Équipement";
        public const string ObjectsName = Name;

        public const int LampsRow = 0;
        public const int DevicesRow = 2;
        public const int CamerasRow = DevicesRow;
        public const int InfiniteRow = 3;

        /// <summary>Only call when SDModKit is loaded.</summary>
        public static void Declare()
        {
            BuildTabs.Declare(Name, 55, LampIcon.Create());
        }
    }
}

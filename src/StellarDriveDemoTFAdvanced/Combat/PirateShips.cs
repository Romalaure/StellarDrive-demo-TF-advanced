using System.Collections.Generic;
using System.Globalization;
using System.Text;
using StellarDriveDemoTFAdvanced.Items;

namespace StellarDriveDemoTFAdvanced.Combat
{
    /// <summary>
    /// The pirate ships: an iron hull (so the Arsenal mod's guns hole it like any hull), a glass
    /// cockpit, wings, engines, and the Arsenal mod's ship weapons pointing forward (+Z, the nose).
    /// Destroyed, it leaves a loot capsule (a chest on a plate).
    /// </summary>
    internal sealed class PirateDesign
    {
        public string Key;
        public string Name;
        public float Health;
        /// <summary>Most speed relative to its target (m/s), push (m/s²), turn gain, and the range it fights at (m).</summary>
        public float MaxSpeed, Accel, Turn, Standoff;
        public string Json;
        /// <summary>What the loot capsule it leaves holds.</summary>
        public string LootJson;
    }

    internal static class PirateShips
    {
        // Game parts: iron hull, glass wall, pilot seat, storage chest; this mod's heavy engine
        private const int IronFloor = 1, IronWall = 2, GlassWall = 7, PilotSeat = 3, Chest = 30, Engine = 7312;
        // Arsenal mod's ship weapons
        public const ushort MachineGun = 9210, MissilePod = 9211, Pdc = 9213, Plasma = 9214;

        public static readonly PirateDesign Corsair = new PirateDesign
        {
            Key = "corsair", Name = "Corsaire pirate", Health = 2500f, MaxSpeed = 80f, Accel = 30f, Turn = 8f, Standoff = 170f,
            Json = Build(1, 3, false),
            LootJson = Capsule(new[] { (ModItems.Titanium, 12), (ModItems.Uranium, 5), (ModItems.NexoranAlloy, 3), (ModItems.Cryonite, 4) })
        };

        public static readonly PirateDesign Cruiser = new PirateDesign
        {
            Key = "cruiser", Name = "Croiseur pirate", Health = 7000f, MaxSpeed = 50f, Accel = 14f, Turn = 4f, Standoff = 260f,
            Json = Build(2, 4, true),
            LootJson = Capsule(new[] { (ModItems.Titanium, 25), (ModItems.Uranium, 12), (ModItems.NexoranAlloy, 8), (ModItems.QuantumCore, 2), (ModItems.Meteorite, 6), (ModItems.Artifact, 1) })
        };

        public static PirateDesign ByKey(string key) => key == Cruiser.Key ? Cruiser : Corsair;

        /// <summary>A box hull 2*halfWidth+1 wide and 2*halfLength+1 long, one deck high, wings on both sides.</summary>
        private static string Build(int halfWidth, int halfLength, bool heavy)
        {
            var hull = new List<string>();
            void Hull(int part, int x, int y, int z, int forward, int up, int voxel) =>
                hull.Add($"{{\"partId\":{part},\"x\":{x},\"y\":{y},\"z\":{z},\"forwardOrientationId\":{forward},\"upOrientationId\":{up},\"voxelPartTypeId\":{voxel},\"FrontColor\":{{\"x\":70,\"y\":62,\"z\":60}},\"BackColor\":{{\"x\":40,\"y\":36,\"z\":36}}}}");
            for (int x = -halfWidth; x <= halfWidth; x++)
            {
                for (int z = -halfLength; z <= halfLength; z++)
                {
                    Hull(IronFloor, x, 0, z, 4, 0, 0);
                    // Roof: a floor turned over, on the next level
                    Hull(IronFloor, x, 1, z, 5, 2, 0);
                }
                // Nose: glass in the middle (the cockpit), iron on the sides; stern: iron
                Hull(x == 0 || (heavy && System.Math.Abs(x) == 1) ? GlassWall : IronWall, x, 0, halfLength, 0, 4, 1);
                Hull(IronWall, x, 0, -halfLength - 1, 2, 4, 1);
            }
            for (int z = -halfLength; z <= halfLength; z++)
            {
                Hull(IronWall, -halfWidth - 1, 0, z, 3, 4, 2);
                Hull(IronWall, halfWidth, 0, z, 1, 4, 2);
            }
            // Swept wings: plates beside the hull, longer at the back
            int span = heavy ? 3 : 2;
            for (int w = 1; w <= span; w++)
            {
                for (int z = -halfLength + w - 1; z <= halfLength - 1 - w; z++)
                {
                    Hull(IronFloor, -halfWidth - w, 0, z, 4, 0, 0);
                    Hull(IronFloor, halfWidth + w, 0, z, 4, 0, 0);
                }
            }

            var parts = new List<string>();
            int id = 1;
            void Part(int settings, float x, float y, float z, int forward, int up) => parts.Add(string.Format(CultureInfo.InvariantCulture,
                "{{\"id\":{0},\"settingsId\":{1},\"coord\":{{\"x\":{2},\"y\":{3},\"z\":{4}}},\"forwardOrientationId\":{5},\"upOrientationId\":{6}}}", id++, settings, x, y, z, forward, up));
            const float deck = -0.46f, roof = 0.54f;
            float wingX = halfWidth + 1.5f;
            float nose = halfLength;
            // Pilot seat in the cockpit
            Part(PilotSeat, 0f, deck, nose - 0.6f, 0, 4);
            // Weapons, all facing forward
            Part(MachineGun, -wingX, deck, 0.5f, 0, 4);
            Part(MachineGun, wingX, deck, 0.5f, 0, 4);
            Part(MissilePod, 0f, roof, 0.5f, 0, 4);
            if (heavy)
            {
                Part(Plasma, -halfWidth, roof, nose - 1.5f, 0, 4);
                Part(Plasma, halfWidth, roof, nose - 1.5f, 0, 4);
                Part(MissilePod, -wingX - 1f, deck, -1f, 0, 4);
                Part(MissilePod, wingX + 1f, deck, -1f, 0, 4);
                Part(Pdc, 0f, roof, -halfLength + 1f, 0, 4);
            }
            // Engines on the stern, nozzles back
            float stern = -halfLength - 0.55f;
            foreach (float x in heavy ? new[] { -1.5f, 0f, 1.5f } : new[] { -0.7f, 0.7f })
                Part(Engine, x, 0f, stern, 4, 2);

            return Wrap(hull, parts, "[]");
        }

        /// <summary>A loot capsule: a floor plate and a chest of goods, left by a destroyed pirate.</summary>
        private static string Capsule((uint Item, int Count)[] goods)
        {
            var hull = new List<string>
            {
                "{\"partId\":1,\"x\":0,\"y\":0,\"z\":0,\"forwardOrientationId\":4,\"upOrientationId\":0,\"voxelPartTypeId\":0,\"FrontColor\":{\"x\":200,\"y\":150,\"z\":40},\"BackColor\":{\"x\":120,\"y\":90,\"z\":30}}"
            };
            var parts = new List<string> { "{\"id\":1,\"settingsId\":" + Chest + ",\"coord\":{\"x\":0,\"y\":-0.46,\"z\":0},\"forwardOrientationId\":0,\"upOrientationId\":4}" };
            var loot = new StringBuilder();
            foreach ((uint item, int count) in goods)
            {
                if (loot.Length > 0)
                    loot.Append(',');
                loot.Append($"{{\"hasItem\":true,\"itemId\":{item},\"quantity\":{count}}}");
            }
            for (int i = goods.Length; i < 10; i++)
                loot.Append(",{\"hasItem\":false,\"itemId\":0,\"quantity\":0}");
            return Wrap(hull, parts, "[{\"partId\":1,\"slots\":[" + loot + "]}]");
        }

        private static string Wrap(List<string> hull, List<string> parts, string storage)
        {
            var sb = new StringBuilder();
            sb.Append("{\"rootShipId\":1,\"cables\":[],\"ships\":[{");
            sb.Append("\"id\":1,\"relativeSpaceId\":0,\"spaceId\":0,\"position\":{\"x\":0,\"y\":0,\"z\":0},\"velocity\":{\"x\":0,\"y\":0,\"z\":0},\"rotation\":{\"x\":0,\"y\":0,\"z\":0,\"w\":1},");
            sb.Append("\"rooms\":[],\"hullParts\":[").Append(string.Join(",", hull)).Append("],\"statefulFloatingParts\":[").Append(string.Join(",", parts)).Append("],\"statefulHullParts\":[],\"thrustersStates\":[],");
            sb.Append("\"storageStates\":").Append(storage).Append(",\"fluidTankStates\":[],\"constantStates\":[],");
            foreach (string empty in new[] { "beaconStates", "leverStates", "warpDriveStates", "dockingDoorStates", "targeterStates", "mathGateStates",
                         "unaryMathGateStates", "memoryRegisterStates", "transmitterStates", "rotorStates", "electrolyzerStates",
                         "woodThrusterStates", "woodGeneratorStates", "ethanolDistillerStates" })
                sb.Append('"').Append(empty).Append("\":[],");
            sb.Length--;
            sb.Append("}]}");
            return sb.ToString();
        }
    }
}

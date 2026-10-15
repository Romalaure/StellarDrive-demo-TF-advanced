using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using StellarDriveDemoTFAdvanced.Items;
using UnityEngine;
using StellarDriveDemoTFAdvanced.Common;

namespace StellarDriveDemoTFAdvanced.Combat
{
    /// <summary>
    /// A kind of pirate ship, from the easy scout to the cruiser. Its hull is iron voxels (the Arsenal
    /// mod's guns hole it like any hull) dressed by a detailed fairing part without collider (see
    /// <see cref="PirateDecor"/>): nose, canopy, swept wings, fins, engine nacelles. Its weapons
    /// are the Arsenal mod's ship weapons, pointing forward (+Z, the nose).
    /// </summary>
    internal sealed class PirateDesign
    {
        public string Key;
        public string Name;
        /// <summary>Difficulty: 1 easy to 5 deadly. It meets the players where the danger (Zones.Danger) is this or one more.</summary>
        public int Level;
        public float Health;
        /// <summary>Most speed relative to its target (m/s), push (m/s²), turn gain, and the range it fights at (m).</summary>
        public float MaxSpeed, Accel, Turn, Standoff;
        /// <summary>How far its guns open fire (m).</summary>
        public float Range = 650f;
        /// <summary>Guns fire in bursts: seconds of fire, then seconds to reload.</summary>
        public float Burst, Reload;
        /// <summary>How far its aim wanders off the target (degrees): the easier, the worse it shoots.</summary>
        public float Wobble;
        /// <summary>Seconds between two missile volleys (0: no missiles).</summary>
        public float MissileInterval;
        /// <summary>Hull: half its width and half its length, in voxels.</summary>
        public int HalfWidth, HalfLength;
        /// <summary>Hull paint (0-255) and the fairing's lights.</summary>
        public Vector3Int HullColor;
        public Color Glow;
        public ushort DecorId;
        /// <summary>Spawn weight, and how many come together.</summary>
        public float Weight;
        public int GroupMin, GroupMax;
        /// <summary>Arsenal weapons: (part id, x, on the roof or the deck, z).</summary>
        public (ushort Weapon, float X, bool Roof, float Z)[] Weapons;
        public (uint Item, int Count)[] Loot;

        private string _json, _lootJson;
        public string Json => _json ?? (_json = PirateShips.Build(this));
        public string LootJson => _lootJson ?? (_lootJson = PirateShips.Capsule(Loot));
    }

    internal static class PirateShips
    {
        // Game parts: iron hull, glass wall, pilot seat, storage chest
        private const int IronFloor = 1, IronWall = 2, GlassWall = 7, PilotSeat = 3, Chest = 30;
        // Arsenal mod's ship weapons
        public const ushort MachineGun = 9210, MissilePod = 9211, Laser = 9212, Pdc = 9213, Plasma = 9214;
        public const ushort Ion = 9215, Railgun = 9216, Torpedo = 9217, RocketPod = 9218, Sam = 9219;

        public static readonly PirateDesign Scout = new PirateDesign
        {
            Key = "scout", Name = Loc.Pick("Éclaireur pirate", "Pirate scout", "Explorador pirata"), Level = 1, Health = 1200f, MaxSpeed = 95f, Accel = 34f, Turn = 7f, Standoff = 150f,
            Burst = 1.2f, Reload = 4f, Wobble = 4f, MissileInterval = 0f,
            HalfWidth = 0, HalfLength = 2, HullColor = new Vector3Int(90, 96, 104), Glow = new Color(1f, 0.75f, 0.2f), DecorId = PirateDecor.ScoutId,
            Weight = 0.35f, GroupMin = 2, GroupMax = 3,
            Weapons = new[] { (MachineGun, 0f, true, 0.5f) },
            Loot = new[] { (ModItems.Titanium, 6), (ModItems.Uranium, 2), (ModItems.Cryonite, 2) }
        };

        public static readonly PirateDesign Corsair = new PirateDesign
        {
            Key = "corsair", Name = Loc.Pick("Corsaire pirate", "Pirate corsair", "Corsario pirata"), Level = 2, Health = 2500f, MaxSpeed = 80f, Accel = 30f, Turn = 8f, Standoff = 170f,
            Burst = 1.5f, Reload = 3.5f, Wobble = 2.5f, MissileInterval = 8f,
            HalfWidth = 1, HalfLength = 3, HullColor = new Vector3Int(110, 40, 36), Glow = new Color(1f, 0.25f, 0.15f), DecorId = PirateDecor.CorsairId,
            Weight = 0.35f, GroupMin = 1, GroupMax = 2,
            Weapons = new[] { (MachineGun, -1f, true, 1f), (MachineGun, 1f, true, 1f), (MissilePod, 0f, true, -1.5f) },
            Loot = new[] { (ModItems.Titanium, 12), (ModItems.Uranium, 5), (ModItems.NexoranAlloy, 3), (ModItems.Cryonite, 4) }
        };

        public static readonly PirateDesign Raider = new PirateDesign
        {
            Key = "raider", Name = Loc.Pick("Pillard pirate", "Pirate raider", "Saqueador pirata"), Level = 3, Health = 4200f, MaxSpeed = 70f, Accel = 22f, Turn = 6f, Standoff = 210f,
            Burst = 2f, Reload = 3f, Wobble = 1.5f, MissileInterval = 6f,
            HalfWidth = 1, HalfLength = 4, HullColor = new Vector3Int(34, 32, 30), Glow = new Color(1f, 0.5f, 0.05f), DecorId = PirateDecor.RaiderId,
            Weight = 0.2f, GroupMin = 1, GroupMax = 1,
            Weapons = new[] { (MachineGun, -1f, true, 1.5f), (MachineGun, 1f, true, 1.5f), (Laser, 0f, true, 2.5f), (MissilePod, 0f, true, -2f) },
            Loot = new[] { (ModItems.Titanium, 18), (ModItems.Uranium, 8), (ModItems.NexoranAlloy, 6), (ModItems.QuantumCore, 1), (ModItems.Meteorite, 3) }
        };

        public static readonly PirateDesign Cruiser = new PirateDesign
        {
            Key = "cruiser", Name = Loc.Pick("Croiseur pirate", "Pirate cruiser", "Crucero pirata"), Level = 4, Health = 7000f, MaxSpeed = 50f, Accel = 14f, Turn = 4f, Standoff = 260f,
            Burst = 2.5f, Reload = 3f, Wobble = 1f, MissileInterval = 4f,
            HalfWidth = 2, HalfLength = 4, HullColor = new Vector3Int(30, 24, 40), Glow = new Color(0.8f, 0.3f, 1f), DecorId = PirateDecor.CruiserId,
            Weight = 0.1f, GroupMin = 1, GroupMax = 1,
            Weapons = new[] { (Plasma, -1.5f, true, 2.5f), (Plasma, 1.5f, true, 2.5f), (MachineGun, -2f, true, 0f), (MachineGun, 2f, true, 0f),
                (MissilePod, -1f, true, -2f), (MissilePod, 1f, true, -2f), (MissilePod, 0f, true, -0.5f), (Pdc, 0f, true, -3.2f) },
            Loot = new[] { (ModItems.Titanium, 25), (ModItems.Uranium, 12), (ModItems.NexoranAlloy, 8), (ModItems.QuantumCore, 2), (ModItems.Meteorite, 6), (ModItems.Artifact, 1) }
        };

        /// <summary>A fast striker: a pod of nine rockets loosed in a stream, and a machine gun.</summary>
        public static readonly PirateDesign Hornet = new PirateDesign
        {
            Key = "hornet", Name = Loc.Pick("Frelon pirate", "Pirate hornet", "Avispón pirata"), Level = 2, Health = 1800f, MaxSpeed = 105f, Accel = 40f, Turn = 9f, Standoff = 140f,
            Burst = 1.4f, Reload = 3.5f, Wobble = 3f, MissileInterval = 0f,
            HalfWidth = 0, HalfLength = 3, HullColor = new Vector3Int(150, 115, 20), Glow = new Color(1f, 0.85f, 0.1f), DecorId = PirateDecor.HornetId,
            Weight = 0.3f, GroupMin = 2, GroupMax = 3,
            Weapons = new[] { (RocketPod, 0f, true, 1f), (MachineGun, 0f, true, -1.5f) },
            Loot = new[] { (ModItems.Titanium, 8), (ModItems.Uranium, 3), (ModItems.Cryonite, 3) }
        };

        /// <summary>A sniper: a railgun along its spine and two ion cannons whose hits jam the target's weapons; it keeps far.</summary>
        public static readonly PirateDesign Lancer = new PirateDesign
        {
            Key = "lancer", Name = Loc.Pick("Lancier pirate", "Pirate lancer", "Lancero pirata"), Level = 3, Health = 3600f, MaxSpeed = 75f, Accel = 24f, Turn = 6f, Standoff = 520f, Range = 900f,
            Burst = 2.5f, Reload = 2.5f, Wobble = 1.2f, MissileInterval = 0f,
            HalfWidth = 1, HalfLength = 4, HullColor = new Vector3Int(40, 70, 115), Glow = new Color(0.35f, 0.8f, 1f), DecorId = PirateDecor.LancerId,
            Weight = 0.2f, GroupMin = 1, GroupMax = 2,
            Weapons = new[] { (Railgun, 0f, true, 2f), (Ion, -1f, true, -0.5f), (Ion, 1f, true, -0.5f) },
            Loot = new[] { (ModItems.Titanium, 16), (ModItems.Uranium, 6), (ModItems.NexoranAlloy, 5), (ModItems.QuantumCore, 1), (ModItems.Cryonite, 4) }
        };

        /// <summary>A torpedo boat: heavy homing torpedoes, a SAM battery against fighters and missiles, a point defence.</summary>
        public static readonly PirateDesign TorpedoBoat = new PirateDesign
        {
            Key = "torpedo", Name = Loc.Pick("Torpilleur pirate", "Pirate torpedo boat", "Torpedero pirata"), Level = 4, Health = 6000f, MaxSpeed = 60f, Accel = 18f, Turn = 4.5f, Standoff = 320f,
            Burst = 2f, Reload = 3f, Wobble = 1f, MissileInterval = 7f,
            HalfWidth = 1, HalfLength = 5, HullColor = new Vector3Int(62, 72, 40), Glow = new Color(0.6f, 1f, 0.3f), DecorId = PirateDecor.TorpedoId,
            Weight = 0.15f, GroupMin = 1, GroupMax = 1,
            Weapons = new[] { (Torpedo, -1f, true, 1.5f), (Torpedo, 1f, true, 1.5f), (MachineGun, -1f, true, 4f), (MachineGun, 1f, true, 4f),
                (Sam, 0f, true, -1.5f), (Pdc, 0f, true, -4f) },
            Loot = new[] { (ModItems.Titanium, 22), (ModItems.Uranium, 10), (ModItems.NexoranAlloy, 7), (ModItems.QuantumCore, 2), (ModItems.Meteorite, 4) }
        };

        /// <summary>The pirates' flagship, only in their own galaxy: plasma, railgun, ion cannons, torpedoes, missiles and every defence.</summary>
        public static readonly PirateDesign Dreadnought = new PirateDesign
        {
            Key = "dreadnought", Name = Loc.Pick("Cuirassé pirate", "Pirate dreadnought", "Acorazado pirata"), Level = 5, Health = 14000f, MaxSpeed = 40f, Accel = 10f, Turn = 3f, Standoff = 340f, Range = 800f,
            Burst = 3f, Reload = 2.5f, Wobble = 0.7f, MissileInterval = 5f,
            HalfWidth = 3, HalfLength = 6, HullColor = new Vector3Int(80, 16, 16), Glow = new Color(1f, 0.12f, 0.05f), DecorId = PirateDecor.DreadnoughtId,
            Weight = 0.1f, GroupMin = 1, GroupMax = 1,
            Weapons = new[] { (Plasma, -2f, true, 4f), (Plasma, 2f, true, 4f), (Railgun, 0f, true, 5f), (Ion, -3f, true, 1f), (Ion, 3f, true, 1f),
                (MachineGun, -3f, true, 4.5f), (MachineGun, 3f, true, 4.5f), (Torpedo, -1.5f, true, 0f), (Torpedo, 1.5f, true, 0f),
                (MissilePod, -2.5f, true, -3f), (MissilePod, 2.5f, true, -3f), (Sam, 0f, true, -2f), (Pdc, -2f, true, -5.5f), (Pdc, 2f, true, -5.5f) },
            Loot = new[] { (ModItems.Titanium, 35), (ModItems.Uranium, 18), (ModItems.NexoranAlloy, 14), (ModItems.QuantumCore, 3), (ModItems.Meteorite, 8),
                (ModItems.Artifact, 2), (ModItems.ExoticMatter, 1) }
        };

        /// <summary>Every design (the first four keep their DevPirateTest numbers).</summary>
        public static readonly PirateDesign[] All = { Scout, Corsair, Raider, Cruiser, Hornet, Lancer, TorpedoBoat, Dreadnought };

        /// <summary>A pirate base (a station in the Erebos galaxy): it does not move, fires what bears, launches missiles and fighters.</summary>
        public static readonly PirateDesign Base = new PirateDesign
        {
            Key = "base", Name = Loc.Pick("Base pirate", "Pirate base", "Base pirata"), Level = 5, Health = 20000f, MaxSpeed = 0f, Accel = 4f, Turn = 2f, Standoff = 0f,
            Burst = 2f, Reload = 3f, Wobble = 0f, MissileInterval = 5f, HalfWidth = 4, HalfLength = 6, Weight = 0f,
            HullColor = new Vector3Int(60, 30, 30), Glow = new Color(1f, 0.15f, 0.05f), Weapons = new (ushort, float, bool, float)[0],
            Loot = new[] { (ModItems.QuantumCore, 3), (ModItems.ExoticMatter, 2), (ModItems.NexoranAlloy, 12), (ModItems.Titanium, 30), (ModItems.Uranium, 20), (ModItems.Artifact, 2) }
        };

        public static PirateDesign ByKey(string key) => key == Base.Key ? Base : All.FirstOrDefault(d => d.Key == key) ?? Corsair;

        /// <summary>A design drawn at random by weight.</summary>
        public static PirateDesign Random() => Draw(All);

        /// <summary>
        /// A design for this danger (1-5): mostly the ones of its level, now and then the level
        /// below (danger 1: scouts only).
        /// </summary>
        public static PirateDesign Random(int danger)
        {
            danger = Mathf.Clamp(danger, 1, Zones.MaxDanger);
            var pool = All.Where(d => d.Level == danger || d.Level == danger - 1).ToList();
            return Draw(pool, d => d.Level == danger ? 2f : 1f);
        }

        /// <summary>The escort of a big ship (danger 4 and 5): scouts or hornets, corsairs or hornets in Erebos.</summary>
        public static PirateDesign Escort(int danger) => danger >= Zones.MaxDanger
            ? (UnityEngine.Random.value < 0.5f ? Corsair : Hornet)
            : (UnityEngine.Random.value < 0.6f ? Scout : Hornet);

        private static PirateDesign Draw(IList<PirateDesign> pool, System.Func<PirateDesign, float> bias = null)
        {
            float Weight(PirateDesign d) => d.Weight * (bias?.Invoke(d) ?? 1f);
            float roll = UnityEngine.Random.value * pool.Sum(Weight);
            foreach (PirateDesign design in pool)
            {
                roll -= Weight(design);
                if (roll <= 0f)
                    return design;
            }
            return pool.Count > 0 ? pool[pool.Count - 1] : Corsair;
        }

        /// <summary>A box hull one deck high, glass cockpit at the nose, its fairing, seat and weapons.</summary>
        internal static string Build(PirateDesign d)
        {
            int halfWidth = d.HalfWidth, halfLength = d.HalfLength;
            var hull = new List<string>();
            Vector3Int c = d.HullColor;
            void Hull(int part, int x, int y, int z, int forward, int up, int voxel) =>
                hull.Add($"{{\"partId\":{part},\"x\":{x},\"y\":{y},\"z\":{z},\"forwardOrientationId\":{forward},\"upOrientationId\":{up},\"voxelPartTypeId\":{voxel},\"FrontColor\":{{\"x\":{c.x},\"y\":{c.y},\"z\":{c.z}}},\"BackColor\":{{\"x\":{c.x / 2},\"y\":{c.y / 2},\"z\":{c.z / 2}}}}}");
            for (int x = -halfWidth; x <= halfWidth; x++)
            {
                for (int z = -halfLength; z <= halfLength; z++)
                {
                    Hull(IronFloor, x, 0, z, 4, 0, 0);
                    // Roof: a floor turned over, on the next level
                    Hull(IronFloor, x, 1, z, 5, 2, 0);
                }
                // Nose: glass (the cockpit); stern: iron
                Hull(GlassWall, x, 0, halfLength, 0, 4, 1);
                Hull(IronWall, x, 0, -halfLength - 1, 2, 4, 1);
            }
            for (int z = -halfLength; z <= halfLength; z++)
            {
                Hull(IronWall, -halfWidth - 1, 0, z, 3, 4, 2);
                Hull(IronWall, halfWidth, 0, z, 1, 4, 2);
            }

            var parts = new List<string>();
            int id = 1;
            void Part(int settings, float x, float y, float z, int forward, int up) => parts.Add(string.Format(CultureInfo.InvariantCulture,
                "{{\"id\":{0},\"settingsId\":{1},\"coord\":{{\"x\":{2},\"y\":{3},\"z\":{4}}},\"forwardOrientationId\":{5},\"upOrientationId\":{6}}}", id++, settings, x, y, z, forward, up));
            const float deck = -0.46f, roof = 0.54f;
            // The fairing, in the ship's own frame, and the pilot seat in the cockpit
            Part(d.DecorId, 0f, 0f, 0f, 0, 4);
            Part(PilotSeat, 0f, deck, halfLength - 0.6f, 0, 4);
            foreach ((ushort weapon, float x, bool onRoof, float z) in d.Weapons)
                Part(weapon, x, onRoof ? roof : deck, z, 0, 4);
            return Wrap(hull, parts, "[]");
        }

        /// <summary>A loot capsule: a floor plate and a chest of goods, left by a destroyed pirate.</summary>
        internal static string Capsule((uint Item, int Count)[] goods)
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

        /// <summary>Development: a plain 5 x 5 platform with a few tanks, for the pirates to shoot at.</summary>
        internal static string Target()
        {
            var hull = new List<string>();
            for (int x = -2; x <= 2; x++)
                for (int z = -2; z <= 2; z++)
                    hull.Add($"{{\"partId\":1,\"x\":{x},\"y\":0,\"z\":{z},\"forwardOrientationId\":4,\"upOrientationId\":0,\"voxelPartTypeId\":0,\"FrontColor\":{{\"x\":150,\"y\":150,\"z\":150}},\"BackColor\":{{\"x\":120,\"y\":120,\"z\":120}}}}");
            var parts = new List<string>();
            int id = 1;
            foreach (float x in new[] { -1.5f, 0f, 1.5f })
                foreach (float z in new[] { -1.5f, 1.5f })
                    parts.Add(string.Format(CultureInfo.InvariantCulture, "{{\"id\":{0},\"settingsId\":23,\"coord\":{{\"x\":{1},\"y\":-0.46,\"z\":{2}}},\"forwardOrientationId\":2,\"upOrientationId\":4}}", id++, x, z));
            return Wrap(hull, parts, "[]");
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

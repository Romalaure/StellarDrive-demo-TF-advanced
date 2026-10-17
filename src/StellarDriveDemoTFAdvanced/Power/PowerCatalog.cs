using MelonLoader;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Power
{
    /// <summary>
    /// Power Grid Overhaul parts and their balancing. Both parts keep their state in the game's
    /// FluidTankState (saved with the ship, synchronized to every player, and emitted by the game
    /// on signal socket 2): FluidQuantity holds the battery charge in percent or the breaker
    /// status code, FluidFlow the power through the part in watts. No fluid ever touches them:
    /// they have no fluid socket.
    /// </summary>
    internal static class PowerCatalog
    {
        public const ushort BatteryId = 7321;
        public const ushort BreakerId = 7322;
        /// <summary>Artifacts: endless power from a quantum core.</summary>
        public const ushort ReactorId = 7323;
        /// <summary>Artifacts: a battery eight times bigger, charged and drained faster.</summary>
        public const ushort CryoBatteryId = 7324;
        public const float CryoBatteryScale = 8f;
        public const float CryoBatteryRateScale = 3f;
        /// <summary>Artifacts: force field generator.</summary>
        public const ushort ShieldId = 7325;
        /// <summary>Artifacts: reinforced force field generator (more charge, more power).</summary>
        public const ushort ShieldMk2Id = 7328;
        /// <summary>Dark matter: dark matter force field generator.</summary>
        public const ushort ShieldMk3Id = 7329;
        /// <summary>Small force field generator.</summary>
        public const ushort ShieldCompactId = 7330;
        /// <summary>Force field generator that recharges fast.</summary>
        public const ushort ShieldRapidId = 7319;
        /// <summary>Nuclear: force field generator on nuclear fuel.</summary>
        public const ushort ShieldNuclearId = 7320;
        /// <summary>Artifacts: makes oxygen from power.</summary>
        public const ushort CondenserId = 7326;
        /// <summary>Artifacts: a gyroscope with several gyroscopes' torque.</summary>
        public const ushort QuantumGyroId = 7327;
        /// <summary>Dark matter: gathers dark matter from space with power.</summary>
        public const ushort DarkCollectorId = 7391;
        /// <summary>Dark matter: a tank for it (the game's fluid tank, in the machines' colors).</summary>
        public const ushort DarkTankId = 7392;
        /// <summary>Dark matter: burns it for a lot of power.</summary>
        public const ushort DarkReactorId = 7393;
        /// <summary>Dark matter: opens a portal in front of the ship to its target.</summary>
        public const ushort PortalId = 7395;
        /// <summary>Nuclear: turns uranium ore into nuclear fuel (a clone of the game's ethanol distiller).</summary>
        public const ushort CentrifugeId = 7396;
        /// <summary>Nuclear: burns nuclear fuel for power.</summary>
        public const ushort NuclearReactorId = 7397;
        /// <summary>Nuclear: a tank for nuclear fuel (the game's fluid tank, shielded).</summary>
        public const ushort NuclearTankId = 7398;
        /// <summary>Combat: pirate radars, 1 000 / 5 000 / 20 000 km.</summary>
        public const ushort RadarShortId = 7307;
        public const ushort RadarLongId = 7308;
        public const ushort RadarDeepId = 7309;
        /// <summary>Combat: stealth generator, hides the ship from the pirates.</summary>
        public const ushort CloakId = 7310;

        // Force field generator and condenser: ConstantState (one value, saved, synchronized, emitted on
        // socket 0), not FluidTankState, which the game would take for a tank on their fluid socket
        public const string MachineStateClass = "ConstantState";
        public const byte MachineSignal = 0;
        public const byte MachinePowerA = 1;
        public const byte MachinePowerB = 2;
        public const byte MachineFluid = 3;

        // Socket ids
        public const byte BatteryPowerA = 0;
        public const byte BatteryPowerB = 1;
        public const byte StatusSignal = 2;
        public const byte BreakerInput = 0;
        public const byte BreakerOutput = 1;
        public const byte BreakerControl = 3;

        // Breaker status codes in FluidQuantity
        public const float BreakerClosed = 0f;
        public const float BreakerOpen = 1f;
        public const float BreakerTripped = 2f;

        public static MelonPreferences_Entry<float> BatteryCapacityKJ { get; private set; }
        public static MelonPreferences_Entry<float> BatteryChargeWatts { get; private set; }
        public static MelonPreferences_Entry<float> BatteryDischargeWatts { get; private set; }
        public static MelonPreferences_Entry<float> BatteryEfficiency { get; private set; }
        public static MelonPreferences_Entry<float> BreakerRatingWatts { get; private set; }
        public static MelonPreferences_Entry<float> BreakerTripSeconds { get; private set; }
        public static MelonPreferences_Entry<float> BreakerAutoResetSeconds { get; private set; }

        public static MelonPreferences_Entry<float> ReactorWatts { get; private set; }
        public static MelonPreferences_Entry<float> ShieldWatts { get; private set; }
        public static MelonPreferences_Entry<float> CondenserWatts { get; private set; }
        public static MelonPreferences_Entry<float> DarkCollectorWatts { get; private set; }
        public static MelonPreferences_Entry<float> DarkCollectorPerSecond { get; private set; }
        public static MelonPreferences_Entry<float> DarkReactorWatts { get; private set; }
        public static MelonPreferences_Entry<float> DarkReactorBurnPerSecond { get; private set; }
        public static MelonPreferences_Entry<float> NuclearReactorWatts { get; private set; }
        public static MelonPreferences_Entry<float> NuclearReactorBurnPerSecond { get; private set; }
        public static MelonPreferences_Entry<float> CentrifugeWatts { get; private set; }
        public static MelonPreferences_Entry<float> CentrifugeOrePerSecond { get; private set; }
        public static MelonPreferences_Entry<float> CentrifugeFuelPerOre { get; private set; }
        public static MelonPreferences_Entry<float> PortalWatts { get; private set; }
        public static MelonPreferences_Entry<float> SolarWatts { get; private set; }
        public static MelonPreferences_Entry<float> PortalChargeSeconds { get; private set; }
        public static MelonPreferences_Entry<float> PortalDarkMatter { get; private set; }
        public static MelonPreferences_Entry<float> PortalOpenSeconds { get; private set; }
        public static MelonPreferences_Entry<float> PortalCooldownSeconds { get; private set; }
        public static MelonPreferences_Entry<float> PortalDistance { get; private set; }
        public static MelonPreferences_Entry<float> PortalRadius { get; private set; }
        public static MelonPreferences_Entry<float> CondenserOxygenPerSecond { get; private set; }
        public static MelonPreferences_Entry<float> QuantumGyroFactor { get; private set; }
        public static MelonPreferences_Entry<float> MiningLaserWatts { get; private set; }
        public static MelonPreferences_Entry<float> MiningLaserRange { get; private set; }
        public static MelonPreferences_Entry<float> MiningLaserDamagePerSecond { get; private set; }
        public static MelonPreferences_Entry<float> ShieldOxygenPerSecond { get; private set; }
        public static MelonPreferences_Entry<float> ShieldCapacity { get; private set; }
        public static MelonPreferences_Entry<float> ShieldRegenPerSecond { get; private set; }
        public static MelonPreferences_Entry<float> ShieldCooldownSeconds { get; private set; }
        public static MelonPreferences_Entry<float> RadarWatts { get; private set; }
        public static MelonPreferences_Entry<float> CloakWatts { get; private set; }
        public static MelonPreferences_Entry<float> FieldDoorWattsPerSquareMeter { get; private set; }

        /// <summary>Parts whose FluidTankState carries a status, not fluid (no fluid mass).</summary>
        public static bool HoldsNoFluid(ushort partSettingsId) => IsPowerPart(partSettingsId) || ForceField.IsGenerator(partSettingsId) || partSettingsId == CondenserId
            || partSettingsId == ShipMiningLaser.Id || partSettingsId == DarkCollectorId || partSettingsId == DarkReactorId || partSettingsId == PortalId || partSettingsId == NuclearReactorId
            || Combat.Radar.IsRadar(partSettingsId) || partSettingsId == CloakId || partSettingsId == FieldDoor.Id || partSettingsId == Propulsion.LandingGear.Id;

        public static bool IsPowerPart(ushort partSettingsId) =>
            partSettingsId == BatteryId || partSettingsId == BreakerId || partSettingsId == ReactorId || partSettingsId == CryoBatteryId;

        public static bool IsBattery(ushort partSettingsId) => partSettingsId == BatteryId || partSettingsId == CryoBatteryId;

        public static float CapacityJoules(ushort partSettingsId) => BatteryCapacityJoules * (partSettingsId == CryoBatteryId ? CryoBatteryScale : 1f);

        public static float RateScale(ushort partSettingsId) => partSettingsId == CryoBatteryId ? CryoBatteryRateScale : 1f;

        public static float BatteryCapacityJoules => Mathf.Max(1f, BatteryCapacityKJ.Value) * 1000f;

        public static void LoadSettings()
        {
            MelonPreferences_Category category = MelonPreferences.CreateCategory("TFAdvancedPower", "TF Advanced - power grid");
            // The wood generator gives 4 000 W, burning one log every 10 s at full load: 40 kJ per log
            BatteryCapacityKJ = category.CreateEntry("BatteryCapacityKJ", 1200f,
                description: "Battery capacity in kilojoules. Default: 5 minutes of a wood generator at full power (30 logs).");
            BatteryChargeWatts = category.CreateEntry("BatteryChargeWatts", 2000f,
                description: "Most power a battery takes from its network's spare generator capacity (W).");
            BatteryDischargeWatts = category.CreateEntry("BatteryDischargeWatts", 4000f,
                description: "Most power a battery gives when its network lacks power (W).");
            BatteryEfficiency = category.CreateEntry("BatteryEfficiency", 0.9f,
                description: "Share of the charging energy that is stored (0.5 to 1).");
            BreakerRatingWatts = category.CreateEntry("BreakerRatingWatts", 4000f,
                description: "Power a circuit breaker carries from its input network to its output network (W).");
            BreakerTripSeconds = category.CreateEntry("BreakerTripSeconds", 0.5f,
                description: "A breaker trips when its output network asks for more than its rating for this long (s).");
            ReactorWatts = category.CreateEntry("ReactorWatts", 12000f,
                description: "Power a quantum reactor gives its network, without fuel, without end (W). Three wood generators: 12 000 W.");
            DarkCollectorWatts = category.CreateEntry("DarkCollectorWatts", 8000f,
                description: "Power a dark matter collector draws while it works (W).");
            DarkCollectorPerSecond = category.CreateEntry("DarkCollectorPerSecond", 0.004f,
                description: "Dark matter a collector gathers (unit/s); five times more near a black hole, three times in the Erebos galaxy.");
            DarkReactorWatts = category.CreateEntry("DarkReactorWatts", 150000f,
                description: "Power a dark matter reactor gives while it has fuel (W).");
            // 40 000 W was the default before nuclear power took that place: dark matter is now well above it
            if (Mathf.Approximately(DarkReactorWatts.Value, 40000f))
                DarkReactorWatts.Value = 150000f;
            DarkReactorBurnPerSecond = category.CreateEntry("DarkReactorBurnPerSecond", 0.002f,
                description: "Dark matter a reactor burns (unit/s).");
            NuclearReactorWatts = category.CreateEntry("NuclearReactorWatts", 40000f,
                description: "Power a nuclear reactor gives while it has fuel (W).");
            NuclearReactorBurnPerSecond = category.CreateEntry("NuclearReactorBurnPerSecond", 0.002f,
                description: "Nuclear fuel a reactor burns (unit/s).");
            CentrifugeWatts = category.CreateEntry("CentrifugeWatts", 6000f,
                description: "Power an enrichment centrifuge draws while it works (W).");
            CentrifugeOrePerSecond = category.CreateEntry("CentrifugeOrePerSecond", 0.05f,
                description: "Uranium ore an enrichment centrifuge processes (ore/s).");
            CentrifugeFuelPerOre = category.CreateEntry("CentrifugeFuelPerOre", 0.2f,
                description: "Nuclear fuel made from one uranium ore (units).");
            SolarWatts = category.CreateEntry("SolarWatts", 3000f,
                description: "Power a solar panel support gives with its wings facing the star (W).");
            PortalWatts = category.CreateEntry("PortalWatts", 8000f,
                description: "Power a portal generator draws while it charges (W). Short of it, it charges slower.");
            PortalChargeSeconds = category.CreateEntry("PortalChargeSeconds", 10f,
                description: "Time a portal generator charges before the portal opens (s).");
            PortalDarkMatter = category.CreateEntry("PortalDarkMatter", 3f,
                description: "Dark matter a portal burns when it opens (units).");
            PortalOpenSeconds = category.CreateEntry("PortalOpenSeconds", 30f,
                description: "How long a portal stays open (s).");
            PortalCooldownSeconds = category.CreateEntry("PortalCooldownSeconds", 60f,
                description: "Time before the generator can open another portal (s).");
            PortalDistance = category.CreateEntry("PortalDistance", 45f,
                description: "Distance from the generator to the portal it opens, straight in front of it (m).");
            PortalRadius = category.CreateEntry("PortalRadius", 18f,
                description: "Radius of the portal (m): the ship's center must pass through it.");
            // The portal drew a whole infinite generator (20 kW) and stalled beside any other machine:
            // still at the old defaults, the settings move to the new ones
            if (PortalWatts.Value == 20000f)
                PortalWatts.Value = 8000f;
            if (PortalDarkMatter.Value == 5f)
                PortalDarkMatter.Value = 3f;
            CondenserWatts = category.CreateEntry("CondenserWatts", 1500f,
                description: "Power a cryonite condenser draws while it makes oxygen (W).");
            CondenserOxygenPerSecond = category.CreateEntry("CondenserOxygenPerSecond", 0.05f,
                description: "Oxygen a cryonite condenser puts in the line on its fluid socket (unit/s). A chemical thruster at full thrust burns 0.05.");
            MiningLaserWatts = category.CreateEntry("MiningLaserWatts", 800f,
                description: "Power a ship mining laser draws while it fires (W).");
            MiningLaserRange = category.CreateEntry("MiningLaserRange", 40f,
                description: "Reach of a ship mining laser's beam (m).");
            MiningLaserDamagePerSecond = category.CreateEntry("MiningLaserDamagePerSecond", 12f,
                description: "How fast a ship mining laser mines (the hand mining laser: 4).");
            QuantumGyroFactor = category.CreateEntry("QuantumGyroFactor", 5f,
                description: "Torque of a quantum gyroscope, in gyroscopes.");
            ShieldWatts = category.CreateEntry("ShieldWatts", 2000f,
                description: "Power a force field generator draws while its field is up (W).");
            ShieldOxygenPerSecond = category.CreateEntry("ShieldOxygenPerSecond", 0.02f,
                description: "Oxygen a force field generator draws from its fluid socket while its field is up (unit/s).");
            ShieldCapacity = category.CreateEntry("ShieldCapacity", 150f,
                description: "Damage a force field takes before breaking down, in points: an impact counts its velocity change in m/s, a part torn by black hole tides 2.");
            ShieldRegenPerSecond = category.CreateEntry("ShieldRegenPerSecond", 0.5f,
                description: "Charge a working force field regains per second (points).");
            ShieldCooldownSeconds = category.CreateEntry("ShieldCooldownSeconds", 300f,
                description: "After breaking down, the generator restarts after this long, fully charged (s).");
            RadarWatts = category.CreateEntry("RadarWatts", 400f,
                description: "Power the short range radar draws (W); the long range one draws 3 times more, the deep space one 8 times.");
            CloakWatts = category.CreateEntry("CloakWatts", 6000f,
                description: "Power a stealth generator draws while it hides the ship (W).");
            FieldDoorWattsPerSquareMeter = category.CreateEntry("FieldDoorWattsPerSquareMeter", 25f,
                description: "Power a force field door draws while its field is up, per square meter of field (W).");
            BreakerAutoResetSeconds = category.CreateEntry("BreakerAutoResetSeconds", 10f,
                description: "A tripped breaker with no control cable closes again after this long (s). 0: only a control signal resets it.");
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Core.Services;
using HarmonyLib;
using Managers.Client.Utils;
using Managers.Interface;
using WorldTracking.Interface.Model.Spaces.Common;
using Saving.Services.Converters;
using Saving.Services.Model.State;
using Ships;
using Ships.Interface.Model.Parts;
using Ships.Services;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;
using WorldTracking.Interface.Model.Spaces;
using WorldTracking.Services;
using WorldTracking.Spaces;
using Random = UnityEngine.Random;

namespace StellarDriveDemoTFAdvanced.Combat
{
    /// <summary>A pirate ship and what its pilot is doing.</summary>
    internal sealed class Pirate
    {
        public uint ShipId;
        public PirateDesign Design;
        public float Health;
        public uint Target;
        public Vector3 OrbitAxis = Vector3.up;
        public float NextThink;
        /// <summary>Guns firing, and until when the missile pods' signal is on (they fire on its rise).</summary>
        public bool Guns;
        public float PulseUntil, NextPulse;
        /// <summary>Guns fire in bursts: until when this one lasts, and until when they reload after it.</summary>
        public float BurstUntil, ReloadUntil;
        /// <summary>Seed of its wandering aim.</summary>
        public float AimSeed = UnityEngine.Random.value * 100f;
        public float LastNearPlayer;
        /// <summary>Near a planet: the planet's centre in its space (it keeps above its target), else null.</summary>
        public Vector3? PlanetCenter;
        /// <summary>Part id -> weapon kind (Arsenal part id) on this ship.</summary>
        public readonly Dictionary<ushort, ushort> Weapons = new Dictionary<ushort, ushort>();
        /// <summary>A base: its guns that bear on the target now; when it launches its next fighters; the base that launched this one.</summary>
        public readonly HashSet<ushort> Bearing = new HashSet<ushort>();
        public float NextLaunch;
        public uint LaunchedBy;
        /// <summary>Since when it has been in the safe zone (0: not), before it jumps away.</summary>
        public float SafeSince;

        public bool IsBase => Design == PirateShips.Base;
    }

    /// <summary>
    /// A pirate group on its way: detected far away (Settings.PirateRaidStartKm), it closes in on
    /// its target at Settings.PirateApproachKmPerSecond and jumps in beside it when it arrives.
    /// Radars see it coming; a stealth generator may make it lose track.
    /// </summary>
    internal sealed class Raid
    {
        public uint Target;
        public PirateDesign Design;
        public int Count;
        /// <summary>Lighter ships flying with a big one (danger 4 and 5), or null.</summary>
        public PirateDesign Escort;
        public int EscortCount;
        /// <summary>The danger where its target was when it set off (1-5).</summary>
        public int Danger;
        /// <summary>Where it comes from, seen from the target (in the target's space).</summary>
        public Vector3 Direction;
        /// <summary>Its distance to the target (m).</summary>
        public float Distance;

        public float Eta => Mathf.Max(0f, Distance - Pirates.SpawnDistance) / Mathf.Max(1000f, Settings.PirateApproachKmPerSecond.Value * 1000f);
    }

    /// <summary>
    /// Enemy pirates (host). Only with the Arsenal mod, whose weapons they carry: while a player's
    /// ship flies in space (orbit or between planets), now and then a pirate group jumps in a
    /// couple of kilometres away and attacks. Each pirate pilots its ship like a fighter: it
    /// closes in, circles its target at its fighting range, turns its nose on it, fires its guns
    /// when lined up and its missiles when the target is in their cone. The player's shots and
    /// blasts wear its hull down; at zero it blows up and drifts, a wreck with a loot chest in its
    /// cockpit. The force field protects against them like against any Arsenal weapon.
    /// The pirate list is saved beside the world (tfa_pirates.txt).
    /// </summary>
    internal static class Pirates
    {
        private const string SaveFile = "tfa_pirates.txt";
        public const float SpawnDistance = 1800f, LeaveDistance = 9000f;
        /// <summary>A hidden ship (stealth generator) is only seen this close (pirate ships, bases).</summary>
        private const float CloakedSight = 2500f, CloakedBaseSight = 1200f;

        public static readonly Dictionary<uint, Pirate> All = new Dictionary<uint, Pirate>();
        /// <summary>Pirate groups on their way.</summary>
        public static readonly List<Raid> Raids = new List<Raid>();
        private static readonly List<Pirate> ThinkBuffer = new List<Pirate>();
        /// <summary>Bases destroyed in this world (their wreck stays, they must not come back).</summary>
        private static readonly HashSet<uint> DeadBases = new HashSet<uint>();
        private static float _inSpaceSeconds;
        private static float _nextCheck;
        private static bool _loaded;

        private static ShipsServerTracker Ships => GameServices.ShipsServer;

        /// <summary>Host: whether a stealth generator hides this ship (or its assembly).</summary>
        public static bool Cloaked(uint rootShipId) => CombatPower.CloakedRoots.Count > 0 && CombatPower.CloakedRoots.Contains(rootShipId);

        public static bool Enabled => Settings.Pirates.Value && Power.ArsenalShield.Installed;

        public static void OnSceneLoaded(bool inGame)
        {
            All.Clear();
            Raids.Clear();
            DeadBases.Clear();
            Zones.Forget();
            _inSpaceSeconds = 0f;
            _loaded = false;
        }

        private static string SavePath()
        {
            string save = MainGameSceneLoadData.SaveFileFullName;
            string directory = string.IsNullOrEmpty(save) ? null : Path.GetDirectoryName(save);
            return string.IsNullOrEmpty(directory) ? null : Path.Combine(directory, SaveFile);
        }

        public static void Save(string worldDirectory)
        {
            if (string.IsNullOrEmpty(worldDirectory))
                return;
            string path = Path.Combine(worldDirectory, SaveFile);
            if (All.Count == 0 && DeadBases.Count == 0)
            {
                if (File.Exists(path))
                    File.Delete(path);
                return;
            }
            File.WriteAllLines(path, All.Values.Select(p => string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0} {1} {2:0}", p.ShipId, p.Design.Key, p.Health))
                .Concat(DeadBases.Select(id => $"{id} dead 0")));
        }

        private static void Load(ShipsServerTracker ships)
        {
            _loaded = true;
            if (DevHarness.Active)
                Zones.LogPlanets();
            string path = SavePath();
            if (path == null || !File.Exists(path))
                return;
            foreach (string line in File.ReadAllLines(path))
            {
                string[] f = line.Split(' ');
                if (f.Length < 3 || !uint.TryParse(f[0], out uint id))
                    continue;
                if (f[1] == "dead")
                {
                    DeadBases.Add(id);
                    continue;
                }
                if (ships.GetShip(id) == null)
                    continue;
                PirateDesign design = PirateShips.ByKey(f[1]);
                float.TryParse(f[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float health);
                All[id] = new Pirate { ShipId = id, Design = design, Health = health > 0f ? health : design.Health, LastNearPlayer = Time.time };
            }
            // Bases already in the world but not in the list (a world saved before they were listed)
            foreach (TrackedShipServer ship in ships.Ships)
            {
                if (All.ContainsKey(ship.Id) || DeadBases.Contains(ship.Id))
                    continue;
                string name = ship.GetAllStatefulParts<Ships.Interface.Model.Parts.State.BeaconState>().Select(p => ((Ships.Interface.Model.Parts.State.BeaconState)p.State).Name).FirstOrDefault();
                if (name != null && Worlds.AbandonedStations.All.Any(s => s.Pirate && s.Name == name))
                    AddBase(ship.Id);
            }
            AdvancedMod.Log.Msg($"pirates: {All.Count} pirate ship(s) and base(s) in this world");
        }

        /// <summary>A pirate base, just built (or found) in the world.</summary>
        public static void AddBase(uint shipId)
        {
            if (!All.ContainsKey(shipId) && !DeadBases.Contains(shipId))
                All[shipId] = new Pirate { ShipId = shipId, Design = PirateShips.Base, Health = PirateShips.Base.Health, LastNearPlayer = Time.time };
        }

        /// <summary>Host, every frame.</summary>
        public static void Update()
        {
            if (!GameServices.IsServer || !Enabled || DevHarness.Off("pirates"))
                return;
            ShipsServerTracker ships = Ships;
            if (ships == null)
                return;
            if (!_loaded)
                Load(ships);
            float now = Time.time;
            if (All.Count > 0)
            {
                ThinkBuffer.Clear();
                ThinkBuffer.AddRange(All.Values);
                foreach (Pirate pirate in ThinkBuffer)
                {
                    if (now >= pirate.NextThink)
                        Think(ships, pirate, now);
                }
            }
            if (Raids.Count > 0)
                Approach(ships, Time.deltaTime);
            if (now < _nextCheck)
                return;
            _nextCheck = now + 5f;
            Spawner(ships);
        }

        // ------------------------------------------------------------------ spawning

        /// <summary>Root ships with a player aboard or beside them (100 m), in open space.</summary>
        private static List<TrackedShipServer> PlayerShipsInSpace(ShipsServerTracker ships)
        {
            var result = new List<TrackedShipServer>();
            var players = ServiceLocator.GetService<Players.Services.PlayersServerTracker>();
            SpacesServer spaces = ServiceLocator.GetService<SpacesServer>();
            if (players == null || spaces == null)
                return result;
            foreach (var player in players.ActivePlayers)
            {
                TrackedShipServer ship = null;
                if (spaces.TryGetSpace(player.SpaceId, out ISpace space) && space is ShipRelativeSpace aboard)
                {
                    if (ships.TryGetTrackedRootShip(aboard.ShipId, out TrackedShipServer root))
                        ship = root;
                }
                else
                {
                    ship = ships.LoadedRootShips.Where(s => s.PhysicsState.SpaceId == player.SpaceId && !All.ContainsKey(s.Id))
                        .OrderBy(s => (s.PhysicsState.Position - player.Position).sqrMagnitude)
                        .FirstOrDefault(s => (s.PhysicsState.Position - player.Position).sqrMagnitude < 100f * 100f);
                }
                if (ship == null || All.ContainsKey(ship.Id) || result.Contains(ship))
                    continue;
                // Never in the safe zone around the home planet
                if (Zones.InSafeZone(ship.PhysicsState.SpaceId, ship.PhysicsState.Position))
                    continue;
                if (spaces.TryGetSpace(ship.PhysicsState.SpaceId, out ISpace shipSpace) && (shipSpace is OrbitSpace || shipSpace is TrajectorySpace))
                    result.Add(ship);
            }
            return result;
        }

        private static void Spawner(ShipsServerTracker ships)
        {
            List<TrackedShipServer> targets = PlayerShipsInSpace(ships);
            foreach (Pirate pirate in All.Values)
            {
                TrackedShipServer ship = ships.GetShip(pirate.ShipId);
                if (ship != null && targets.Any(t => t.PhysicsState.SpaceId == ship.PhysicsState.SpaceId
                        && (t.PhysicsState.Position - ship.PhysicsState.Position).sqrMagnitude < LeaveDistance * LeaveDistance))
                    pirate.LastNearPlayer = Time.time;
            }
            // Pirates left far behind for long are gone (bases stay)
            foreach (Pirate pirate in All.Values.Where(p => !p.IsBase && Time.time - p.LastNearPlayer > 600f).ToList())
                Despawn(ships, pirate);
            RaidChecks(ships);
            if (targets.Count == 0 || Raids.Count > 0 || All.Values.Any(p => !p.IsBase))
                return;
            // Hidden ships are found three times less often
            List<TrackedShipServer> visible = targets.Where(t => !Cloaked(t.Id)).ToList();
            _inSpaceSeconds += visible.Count > 0 ? 5f : 5f / 3f;
            // The farther from home, the more often they come: a third less often near home, a third more in Erebos
            int danger = targets.Max(t => Zones.Danger(t.PhysicsState.SpaceId, t.PhysicsState.Position));
            float interval = Mathf.Max(1f, Settings.PirateIntervalMinutes.Value) * 60f * DangerInterval[danger];
            // In their territory, around the Bastion, they come four times as often
            if (targets.Any(t => Zones.NearBastion(t.PhysicsState.SpaceId, t.PhysicsState.Position)))
                interval *= 0.25f;
            if (_inSpaceSeconds < interval * Random.Range(0.7f, 1.3f))
                return;
            _inSpaceSeconds = 0f;
            List<TrackedShipServer> pool = visible.Count > 0 ? visible : targets;
            TrackedShipServer target = pool[Random.Range(0, pool.Count)];
            Raid raid = NewRaid(target, Zones.Danger(target.PhysicsState.SpaceId, target.PhysicsState.Position));
            Raids.Add(raid);
            AdvancedMod.Log.Msg($"pirates: danger {raid.Danger}: {raid.Count} {raid.Design.Name}(s){(raid.Escort != null ? $" and {raid.EscortCount} {raid.Escort.Name}(s)" : "")} on their way to ship {target.Id}, {raid.Distance / 1000f:0} km out");
        }

        /// <summary>Time between two attacks for each danger (times PirateIntervalMinutes).</summary>
        private static readonly float[] DangerInterval = { 1f, 1.5f, 1.2f, 1f, 0.85f, 0.75f };

        /// <summary>
        /// A raid as tough as the danger where its target flies: scouts alone near home, then
        /// corsairs and hornets, raiders and lancers, cruisers and torpedo boats with an escort,
        /// and the dreadnoughts of Erebos.
        /// </summary>
        private static Raid NewRaid(TrackedShipServer target, int danger)
        {
            PirateDesign design = PirateShips.Random(danger);
            int count = Random.Range(design.GroupMin, design.GroupMax + 1);
            // The first pirates met, near home, come one or two at a time
            if (danger <= 1)
                count = Random.Range(1, 3);
            var raid = new Raid { Target = target.Id, Design = design, Count = count, Danger = danger, Direction = Random.onUnitSphere,
                Distance = Mathf.Max(SpawnDistance * 2f, Settings.PirateRaidStartKm.Value * 1000f) };
            if (design.Level >= 4 && Random.value < (danger >= Zones.MaxDanger ? 0.8f : 0.5f))
            {
                raid.Escort = PirateShips.Escort(danger);
                raid.EscortCount = Random.Range(1, 3);
            }
            return raid;
        }

        /// <summary>Raids close in; one that arrives jumps in beside its target.</summary>
        private static void Approach(ShipsServerTracker ships, float deltaTime)
        {
            float speed = Mathf.Max(1f, Settings.PirateApproachKmPerSecond.Value) * 1000f;
            for (int i = Raids.Count - 1; i >= 0; i--)
            {
                Raid raid = Raids[i];
                raid.Distance -= speed * deltaTime;
                if (raid.Distance > SpawnDistance)
                    continue;
                Raids.RemoveAt(i);
                TrackedShipServer target = ships.GetShip(raid.Target);
                if (target == null)
                    continue;
                // A hidden ship: they only know roughly where it is, they jump in farther out
                float distance = Cloaked(target.Id) ? SpawnDistance * 2f : SpawnDistance;
                SpawnNear(ships, target, raid.Design, raid.Count, distance);
                if (raid.Escort != null)
                    SpawnNear(ships, target, raid.Escort, raid.EscortCount, distance * 1.1f);
            }
        }

        /// <summary>Every 5 s: raids whose target is gone, in the safe zone or hidden (now and then) give up.</summary>
        private static void RaidChecks(ShipsServerTracker ships)
        {
            for (int i = Raids.Count - 1; i >= 0; i--)
            {
                Raid raid = Raids[i];
                TrackedShipServer target = ships.GetShip(raid.Target);
                string why = target == null ? "target gone"
                    : !InOpenSpace(target) ? "target landed"
                    : Zones.InSafeZone(target.PhysicsState.SpaceId, target.PhysicsState.Position) ? "safe zone"
                    : Cloaked(target.Id) && Random.value < 0.2f ? "lost track of the hidden ship" : null;
                if (why == null)
                    continue;
                Raids.RemoveAt(i);
                AdvancedMod.Log.Msg($"pirates: raid on ship {raid.Target} called off ({why})");
            }
        }

        /// <summary>In orbit or between planets (where pirates attack), not on a planet's surface.</summary>
        private static bool InOpenSpace(TrackedShipServer ship)
        {
            SpacesServer spaces = ServiceLocator.GetService<SpacesServer>();
            return spaces != null && spaces.TryGetSpace(ship.PhysicsState.SpaceId, out ISpace space) && (space is OrbitSpace || space is TrajectorySpace);
        }

        /// <summary>Where a raid is now: its target's space and a position in it.</summary>
        public static bool TryRaidPosition(ShipsServerTracker ships, Raid raid, out uint spaceId, out Vector3 position)
        {
            TrackedShipServer target = ships.GetShip(raid.Target);
            spaceId = target != null ? target.PhysicsState.SpaceId : 0u;
            position = target != null ? target.PhysicsState.Position + raid.Direction * raid.Distance : Vector3.zero;
            return target != null;
        }

        /// <summary>Whether a pirate (base or ship) at this distance sees the ship.</summary>
        private static bool Sees(Pirate pirate, TrackedShipServer ship, float sqrDistance, float reach)
        {
            float sight = Cloaked(ship.Id) ? (pirate.IsBase ? CloakedBaseSight : CloakedSight) : reach;
            return sqrDistance < sight * sight;
        }

        /// <summary>Pirates jump in near this ship and attack it.</summary>
        public static void SpawnNear(ShipsServerTracker ships, TrackedShipServer target, PirateDesign design, int count, float distance = SpawnDistance, TrackedShipServer from = null)
        {
            SpacesServer spaces = ServiceLocator.GetService<SpacesServer>();
            var ids = ServiceLocator.GetService<WorldObjectsIdsServerTracker>();
            if (spaces == null || ids == null || target == null)
                return;
            Vector3 side = Random.onUnitSphere;
            for (int i = 0; i < count; i++)
            {
                var data = JsonUtility.FromJson<ShipImportExportUtils.ShipImportExportData>(design.Json);
                SavedShipState state = new ShipsConverter().ConvertToState(data.ships).States.Cast<SavedShipState>().Single();
                state.Id = ids.CreateUniqueIdForObject();
                state.SpaceId = target.PhysicsState.SpaceId;
                Vector3 offset = Quaternion.AngleAxis(i * 25f, Vector3.Cross(side, Vector3.up).normalized + Vector3.forward * 0.01f) * side * distance;
                state.Position = target.PhysicsState.Position + offset;
                if (from != null)
                {
                    // Out of a base's hangar: beside it, heading for the target
                    state.SpaceId = from.PhysicsState.SpaceId;
                    state.Position = from.PhysicsState.Position + side * (40f + 25f * i);
                    offset = state.Position - target.PhysicsState.Position;
                }
                state.Rotation = Quaternion.LookRotation(-offset.normalized, Vector3.up);
                state.Velocity = target.PhysicsState.Velocity;
                state.RelativeSpaceId = spaces.GetOrCreateShipRelativeSpace(state.Id);
                ships.LoadShip(state);
                All[state.Id] = new Pirate { ShipId = state.Id, Design = design, Health = design.Health, Target = target.Id, LastNearPlayer = Time.time,
                    OrbitAxis = Random.onUnitSphere, LaunchedBy = from?.Id ?? 0 };
                AdvancedMod.Log.Msg($"pirates: {design.Name} {state.Id} jumps in {distance:0} m from ship {target.Id}");
            }
        }

        private static void Despawn(ShipsServerTracker ships, Pirate pirate)
        {
            All.Remove(pirate.ShipId);
            TrackedShipServer ship = ships.GetShip(pirate.ShipId);
            if (ship != null)
                ships.RemoveShip(ship.State);
            AdvancedMod.Log.Msg($"pirates: {pirate.Design.Name} {pirate.ShipId} left");
        }

        // ------------------------------------------------------------------ piloting

        private static void Think(ShipsServerTracker ships, Pirate pirate, float now)
        {
            pirate.NextThink = now + 0.25f;
            TrackedShipServer self = ships.GetShip(pirate.ShipId);
            if (self == null)
            {
                All.Remove(pirate.ShipId);
                return;
            }
            if (pirate.Weapons.Count == 0)
            {
                foreach (StatefulPart part in self.StatefulParts)
                {
                    if (part.Settings != null && part.Settings.id >= PirateShips.MachineGun && part.Settings.id <= PirateShips.Sam)
                        pirate.Weapons[part.Id] = part.Settings.id;
                }
            }
            // Target: the nearest other ship in the same space (not a pirate), within reach (bases: 3 km)
            float reach = pirate.IsBase ? 3000f : LeaveDistance;
            TrackedShipServer target = ships.GetShip(pirate.Target);
            if (target == null || All.ContainsKey(target.Id) || target.PhysicsState.SpaceId != self.PhysicsState.SpaceId
                || !Sees(pirate, target, (target.PhysicsState.Position - self.PhysicsState.Position).sqrMagnitude, reach))
            {
                // The nearest ship it sees (a hidden one only up close)
                target = null;
                float best = float.MaxValue;
                foreach (TrackedShipServer s in ships.LoadedRootShips)
                {
                    if (s.Id == self.Id || s.PhysicsState.SpaceId != self.PhysicsState.SpaceId || s.PartCount <= 3 || All.ContainsKey(s.Id))
                        continue;
                    float d = (s.PhysicsState.Position - self.PhysicsState.Position).sqrMagnitude;
                    if (d < best && Sees(pirate, s, d, reach))
                    {
                        best = d;
                        target = s;
                    }
                }
                pirate.Target = target?.Id ?? 0;
            }
            pirate.Guns = false;
            pirate.Bearing.Clear();
            // The safe zone: a fight that drifts into it is broken off, the pirates jump away
            if (!pirate.IsBase && (Zones.InSafeZone(self.PhysicsState.SpaceId, self.PhysicsState.Position)
                || (target != null && Zones.InSafeZone(target.PhysicsState.SpaceId, target.PhysicsState.Position))))
            {
                pirate.Target = 0;
                if (pirate.SafeSince <= 0f)
                    pirate.SafeSince = now;
                else if (now - pirate.SafeSince > 15f)
                    Despawn(ships, pirate);
                return;
            }
            pirate.SafeSince = 0f;
            if (pirate.IsBase)
            {
                if (target != null)
                    BaseThink(ships, pirate, self, target, now);
                return;
            }
            SpacesServer spaces = ServiceLocator.GetService<SpacesServer>();
            pirate.PlanetCenter = spaces != null && spaces.TryGetSpace(self.PhysicsState.SpaceId, out ISpace space) && space is PlanetSurfaceSpace surface
                ? -(Vector3)surface.Center : (Vector3?)null;
            if (target == null)
                return;
            Vector3 to = target.PhysicsState.Position - self.PhysicsState.Position;
            float distance = to.magnitude;
            Vector3 nose = self.PhysicsState.Rotation * Vector3.forward;
            float angle = Vector3.Angle(nose, to);
            // Guns: a burst while lined up, then a reload before the next. A hidden ship is a blurred
            // target: they must come closer to fire, and their missiles lock on later
            bool hidden = Cloaked(target.Id);
            float range = pirate.Design.Range;
            bool aligned = angle < 6f && distance < (hidden ? range * 0.6f : range);
            if (aligned && now >= pirate.ReloadUntil && now >= pirate.BurstUntil)
            {
                pirate.BurstUntil = now + pirate.Design.Burst;
                pirate.ReloadUntil = pirate.BurstUntil + pirate.Design.Reload;
            }
            pirate.Guns = aligned && now < pirate.BurstUntil;
            if (pirate.Design.MissileInterval > 0f && angle < (hidden ? 10f : 18f) && distance < (hidden ? 800f : 1300f) && now >= pirate.NextPulse)
            {
                pirate.PulseUntil = now + 0.4f;
                pirate.NextPulse = now + pirate.Design.MissileInterval;
            }
        }

        /// <summary>
        /// A base: its guns that bear on the target fire in bursts, a salvo of homing missiles now
        /// and then, and fighters out of its hangar when a ship comes near.
        /// </summary>
        private static void BaseThink(ShipsServerTracker ships, Pirate pirate, TrackedShipServer self, TrackedShipServer target, float now)
        {
            Vector3 to = target.PhysicsState.Position - self.PhysicsState.Position;
            float distance = to.magnitude;
            bool hidden = Cloaked(target.Id);
            bool any = false;
            foreach (StatefulPart part in self.StatefulParts)
            {
                if (part.Settings == null || !pirate.Weapons.ContainsKey(part.Id))
                    continue;
                Vector3 aim = self.PhysicsState.Rotation * (part.LocalRotation * Vector3.forward);
                if (Vector3.Angle(aim, to) < 14f && distance < 900f)
                {
                    pirate.Bearing.Add(part.Id);
                    any = true;
                }
            }
            if (any && now >= pirate.ReloadUntil && now >= pirate.BurstUntil)
            {
                pirate.BurstUntil = now + pirate.Design.Burst;
                pirate.ReloadUntil = pirate.BurstUntil + pirate.Design.Reload;
            }
            pirate.Guns = any && now < pirate.BurstUntil;
            if (distance < (hidden ? 1200f : 2500f) && now >= pirate.NextPulse)
            {
                pirate.NextPulse = now + pirate.Design.MissileInterval;
                Salvo(self, target, 2);
            }
            if (distance < (hidden ? 1500f : 4000f) && now >= pirate.NextLaunch && All.Values.Count(p => p.LaunchedBy == self.Id) < 3)
            {
                pirate.NextLaunch = now + 150f;
                float roll = Random.value;
                PirateDesign fighters = roll < 0.4f ? PirateShips.Scout : roll < 0.7f ? PirateShips.Hornet : PirateShips.Corsair;
                SpawnNear(ships, target, fighters, fighters == PirateShips.Corsair ? 1 : 2, SpawnDistance, self);
                AdvancedMod.Log.Msg($"pirates: base {self.Id} launches {fighters.Name}s at ship {target.Id}");
            }
        }

        private static System.Reflection.MethodInfo _launch;

        /// <summary>Homing Arsenal missiles from the base toward the target (fired where the base is seen, like the ships' own).</summary>
        private static void Salvo(TrackedShipServer self, TrackedShipServer target, int count)
        {
            try
            {
                if (_launch == null)
                    _launch = HarmonyLib.AccessTools.Method(HarmonyLib.AccessTools.TypeByName("Arsenal.Vehicles.Missiles"), "Launch");
                Transform visual = ShipVisuals.Find(self.Id);
                Transform aimAt = ShipVisuals.Find(target.Id);
                if (_launch == null || visual == null || aimAt == null)
                    return;
                System.Reflection.ParameterInfo[] p = _launch.GetParameters();
                for (int i = 0; i < count; i++)
                {
                    Vector3 dir = (aimAt.position - visual.position).normalized;
                    Vector3 from = visual.position + dir * 25f + Random.insideUnitSphere * 6f;
                    var args = new object[p.Length];
                    args[0] = from;
                    args[1] = dir * 35f;
                    args[2] = target.Id;
                    args[3] = self.Id;
                    for (int k = 4; k < p.Length; k++)
                        args[k] = k == 4 && p[k].ParameterType == typeof(bool) ? true : p[k].HasDefaultValue ? p[k].DefaultValue
                            : p[k].ParameterType == typeof(int) ? (object)(-1) : p[k].ParameterType == typeof(uint) ? (object)0u : null;
                    _launch.Invoke(null, args);
                }
            }
            catch (System.Exception e)
            {
                ErrorLog.Report("pirate base missiles", e);
            }
        }

        /// <summary>Host physics: the pirate's pilot pushes and turns its ship (called each physics step for its root rigidbody).</summary>
        public static void Steer(Pirate pirate, TrackedShipServer self, Rigidbody body, float deltaTime)
        {
            if (pirate.IsBase)
            {
                // A base keeps its station: blasts do not set it drifting or spinning
                body.AddForce(Vector3.ClampMagnitude(-body.velocity * 1.5f, 6f), ForceMode.Acceleration);
                body.AddTorque(Vector3.ClampMagnitude(-body.angularVelocity * 2f, 2f), ForceMode.Acceleration);
                return;
            }
            ShipsServerTracker ships = Ships;
            TrackedShipServer target = pirate.Target != 0 ? ships?.GetShip(pirate.Target) : null;
            PirateDesign d = pirate.Design;
            Vector3 velocity = body.velocity;
            Vector3 aim;
            Vector3 desired;
            if (target == null || target.PhysicsState.SpaceId != self.PhysicsState.SpaceId)
            {
                // Nobody to fight: come to a stop
                desired = Vector3.zero;
                aim = body.rotation * Vector3.forward;
            }
            else
            {
                Vector3 to = target.PhysicsState.Position - self.PhysicsState.Position;
                float distance = to.magnitude;
                Vector3 dir = distance > 0.01f ? to / distance : Vector3.forward;
                // Circle the target at fighting range: close in, keep away, slide around it
                Vector3 around = Vector3.Cross(pirate.OrbitAxis, dir);
                if (around.sqrMagnitude < 0.01f)
                    around = Vector3.Cross(Vector3.up, dir);
                float gap = distance - d.Standoff;
                Vector3 relative = dir * Mathf.Clamp(gap * 0.4f, -d.MaxSpeed, d.MaxSpeed) + around.normalized * d.MaxSpeed * 0.2f;
                // Near a planet it keeps 50-90 m above its target, clear of the ground
                if (pirate.PlanetCenter.HasValue)
                {
                    Vector3 sky = (self.PhysicsState.Position - pirate.PlanetCenter.Value).normalized;
                    float height = Vector3.Dot(-to, sky);
                    relative += sky * Mathf.Clamp((70f - height) * 0.6f, -15f, 25f);
                }
                desired = target.PhysicsState.Velocity + Vector3.ClampMagnitude(relative, d.MaxSpeed);
                // Its aim wanders around the target: the easier the pirate, the more it misses
                float t = Time.time * 0.6f;
                // A hidden ship blurs on their sights: their aim wanders far more
                float wobble = Cloaked(target.Id) ? d.Wobble * 2.5f + 3f : d.Wobble;
                float yaw = (Mathf.PerlinNoise(pirate.AimSeed, t) - 0.5f) * 2f * wobble;
                float pitch = (Mathf.PerlinNoise(t, pirate.AimSeed) - 0.5f) * 2f * wobble;
                Vector3 side = Vector3.Cross(Vector3.up, dir).sqrMagnitude > 1e-4f ? Vector3.Cross(Vector3.up, dir).normalized : Vector3.right;
                aim = Quaternion.AngleAxis(yaw, Vector3.Cross(dir, side)) * Quaternion.AngleAxis(pitch, side) * dir;
            }
            Vector3 push = Vector3.ClampMagnitude((desired - velocity) * 1.2f, d.Accel);
            body.AddForce(push, ForceMode.Acceleration);
            // Turn the nose on the target, damped
            Vector3 forward = body.rotation * Vector3.forward;
            Vector3 axis = Vector3.Cross(forward, aim);
            float error = Vector3.Angle(forward, aim) * Mathf.Deg2Rad;
            Vector3 turn = axis.sqrMagnitude > 1e-6f ? axis.normalized * error * d.Turn : Vector3.zero;
            // Keep its wings level-ish (up toward the target's up)
            Vector3 up = body.rotation * Vector3.up;
            Vector3 wantUp = pirate.PlanetCenter.HasValue ? (self.PhysicsState.Position - pirate.PlanetCenter.Value).normalized
                : target != null ? target.PhysicsState.Rotation * Vector3.up : Vector3.up;
            // (roll only: it must not fight the nose's aim)
            turn += forward * Vector3.Dot(Vector3.Cross(up, wantUp), forward) * d.Turn * 0.3f;
            Vector3 torque = (turn * d.Turn - body.angularVelocity * d.Turn * 1.6f);
            body.AddTorque(Vector3.ClampMagnitude(torque, 10f), ForceMode.Acceleration);
        }

        /// <summary>The fire signal a pirate's weapon reads.</summary>
        public static bool TryWeaponSignal(uint shipId, ushort partId, out double value)
        {
            value = 0;
            if (!All.TryGetValue(shipId, out Pirate pirate) || !pirate.Weapons.TryGetValue(partId, out ushort kind))
                return false;
            if (kind == PirateShips.MissilePod || kind == PirateShips.Torpedo)
                value = Time.time < pirate.PulseUntil ? 1 : 0;
            else if (kind == PirateShips.Pdc || kind == PirateShips.Sam)
                value = 1;
            else
                value = pirate.Guns && (!pirate.IsBase || pirate.Bearing.Contains(partId)) ? 1 : 0;
            // (machine guns, lasers, plasma and ion cannons, railguns and rocket pods fire in its bursts)
            return true;
        }

        // ------------------------------------------------------------------ damage

        private static bool _hooked;
        // A blast and the hull holes it makes come in the same frame: the blast alone counts
        private static readonly Dictionary<uint, int> BlastFrame = new Dictionary<uint, int>();

        /// <summary>Arsenal's hull hits and blasts wear the pirates down.</summary>
        public static void Install(HarmonyLib.Harmony harmony)
        {
            Type hull = AccessTools.TypeByName("Arsenal.Damage.HullDamage");
            Type parts = AccessTools.TypeByName("Arsenal.Damage.PartDamage");
            if (hull == null || parts == null || _hooked)
                return;
            MethodInfo apply = AccessTools.Method(hull, "ApplyServer");
            MethodInfo blast = AccessTools.Method(parts, "ApplyBlast");
            if (apply != null)
                harmony.Patch(apply, postfix: new HarmonyMethod(typeof(Pirates), nameof(HullPostfix)));
            if (blast != null)
                harmony.Patch(blast, postfix: new HarmonyMethod(typeof(Pirates), nameof(BlastPostfix)));
            _hooked = true;
            AdvancedMod.Log.Msg("pirates: Arsenal found, pirates can attack");
        }

        private static void HullPostfix(object[] __args)
        {
            try
            {
                if (All.Count == 0)
                    return;
                uint shipId = ArsenalReflection.ShipIdOf(__args[0]);
                if (BlastFrame.TryGetValue(Root(shipId), out int frame) && frame == Time.frameCount)
                    return;
                Hurt(shipId, ArsenalReflection.AsFloat(__args[2]));
            }
            catch (Exception e)
            {
                ErrorLog.Report("pirate damage", e);
            }
        }

        private static void BlastPostfix(TrackedShipServer ship, float power)
        {
            if (ship == null || All.Count == 0)
                return;
            uint root = Root(ship.Id);
            if (BlastFrame.TryGetValue(root, out int frame) && frame == Time.frameCount)
                return;
            BlastFrame[root] = Time.frameCount;
            Hurt(ship.Id, power * 0.5f);
        }

        private static uint Root(uint shipId)
        {
            ShipsServerTracker ships = Ships;
            return ships != null && ships.TryGetTrackedRootShip(shipId, out TrackedShipServer root) && root != null ? root.Id : shipId;
        }

        private static void Hurt(uint shipId, float damage)
        {
            ShipsServerTracker ships = Ships;
            shipId = Root(shipId);
            if (!All.TryGetValue(shipId, out Pirate pirate) || damage <= 0f)
                return;
            pirate.Health -= damage;
            if (pirate.Health <= 0f)
                Destroyed(ships, pirate);
        }

        /// <summary>Its hull gives way: explosions at the stern, then a dead wreck adrift.</summary>
        private static void Destroyed(ShipsServerTracker ships, Pirate pirate)
        {
            All.Remove(pirate.ShipId);
            if (pirate.IsBase)
                DeadBases.Add(pirate.ShipId);
            AdvancedMod.Log.Msg($"pirates: {pirate.Design.Name} {pirate.ShipId} destroyed");
            if (TFItems.TFItemsMod.Active)
                TFItems.Devices.TraderLedger.PirateDestroyed(pirate.IsBase);
            TrackedShipServer ship = ships?.GetShip(pirate.ShipId);
            Type explosives = AccessTools.TypeByName("Arsenal.Weapons.Explosives");
            MethodInfo explode = explosives != null ? AccessTools.Method(explosives, "ServerExplode") : null;
            if (ship == null || explode == null)
                return;
            DropLoot(ships, ship, pirate.Design);
            ParameterInfo[] p = explode.GetParameters();
            float length = pirate.Design.HalfLength + 0.5f;
            foreach (Vector3 local in new[] { new Vector3(0f, 0f, -length), new Vector3(-1.5f, 0f, -length + 1f), new Vector3(1.5f, 0f, -length + 1f) })
            {
                Vector3 at = ship.PhysicsState.Position + ship.PhysicsState.Rotation * local;
                var args = new object[p.Length];
                args[0] = ship.PhysicsState.SpaceId;
                args[1] = at;
                args[2] = 3.5f;
                args[3] = 700f;
                args[4] = 30f;
                for (int i = 5; i < p.Length; i++)
                    args[i] = p[i].HasDefaultValue ? p[i].DefaultValue : null;
                try
                {
                    explode.Invoke(null, args);
                }
                catch (Exception e)
                {
                    ErrorLog.Report("pirate explosion", e);
                    break;
                }
            }
        }

        /// <summary>Development: a scout's loot capsule thrown from the given ship (DevPirateTest 16).</summary>
        public static void DevDropLoot(TrackedShipServer ship) => DropLoot(Ships, ship, PirateShips.All[0]);

        /// <summary>The loot capsule, thrown clear of the wreck before it blows up.</summary>
        private static void DropLoot(ShipsServerTracker ships, TrackedShipServer wreck, PirateDesign design)
        {
            SpacesServer spaces = ServiceLocator.GetService<SpacesServer>();
            var ids = ServiceLocator.GetService<WorldObjectsIdsServerTracker>();
            if (spaces == null || ids == null)
                return;
            try
            {
                var data = JsonUtility.FromJson<ShipImportExportUtils.ShipImportExportData>(design.LootJson);
                SavedShipState state = new ShipsConverter().ConvertToState(data.ships).States.Cast<SavedShipState>().Single();
                state.Id = ids.CreateUniqueIdForObject();
                state.SpaceId = wreck.PhysicsState.SpaceId;
                Vector3 up = wreck.PhysicsState.Rotation * Vector3.up;
                state.Position = wreck.PhysicsState.Position + up * 12f;
                state.Rotation = wreck.PhysicsState.Rotation;
                state.Velocity = wreck.PhysicsState.Velocity + up * 3f;
                state.RelativeSpaceId = spaces.GetOrCreateShipRelativeSpace(state.Id);
                ships.LoadShip(state);
                AdvancedMod.Log.Msg($"pirates: loot capsule {state.Id} left by {design.Name}");
                LootPing.Announce(state.Id);
            }
            catch (Exception e)
            {
                ErrorLog.Report("pirate loot", e);
            }
        }

        /// <summary>Development: pirates attack the given ship now (DevPirateTest).</summary>
        public static void ForceSpawn(TrackedShipServer target, PirateDesign design, int count, float distance)
        {
            ShipsServerTracker ships = Ships;
            if (ships == null || target == null)
                return;
            _loaded = true;
            SpawnNear(ships, target, design, count, distance);
        }
    }
}

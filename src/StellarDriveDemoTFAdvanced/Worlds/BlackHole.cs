using System;
using System.Collections.Generic;
using System.Linq;
using Core.Services;
using Core.Utils;
using Core.Values;
using HarmonyLib;
using Planets.Interface.Model;
using Planets.Interface.Services;
using Players;
using Players.Services;
using Ships;
using Ships.Grid.Model;
using Ships.Interface.Model;
using Ships.Interface.Model.Joints;
using Ships.Interface.Model.Placement;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.State;
using Ships.Services;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;
using WorldProvision.Services;
using WorldTracking.Interface.Model;
using WorldTracking.Interface.Model.Spaces;
using WorldTracking.Interface.Model.Spaces.Common;
using WorldTracking.Interface.Services;
using Random = UnityEngine.Random;

namespace StellarDriveDemoTFAdvanced.Worlds
{
    /// <summary>
    /// One of the mod's black holes, in the game's own units. The game's planets are scaled down
    /// (1 g at 320 km), so are the holes: they follow Schwarzschild's relations with the speed of
    /// light scaled like the rest of the world (10 km/s). The event horizon is where the escape
    /// velocity reaches that scaled light speed: r_s = 2GM / c².
    ///  - Gravity GM/r² for ships and players everywhere outside planet ground (the game itself
    ///    has none in orbit or deep space).
    ///  - Tidal zone (below <see cref="TidalRadius"/>): ships lose parts, faster and faster.
    ///  - Event horizon: ships are destroyed and players die.
    ///  - Warp jumps stop before entering its sphere of influence, like they stop before a star.
    /// </summary>
    internal sealed class Hole
    {
        /// <summary>Light speed in the game's scale: escape velocity at the horizon of every hole.</summary>
        public const double LightSpeed = 10000.0;

        public string Name;
        /// <summary>Planet it sits next to (the demo planet when the mod planets are off).</summary>
        public ulong AnchorPlanet;
        public Vector3 OffsetFromAnchor;
        /// <summary>Event horizon radius r_s (m).</summary>
        public float Horizon;
        /// <summary>Tilt of its accretion disk.</summary>
        public Quaternion DiskTilt;

        /// <summary>GM (m³/s²) = c² r_s / 2.</summary>
        public double GravitationalParameter => LightSpeed * LightSpeed * Horizon / 2.0;
        /// <summary>Visible shadow (photon capture) radius, 3√3/2 r_s.</summary>
        public float Shadow => Horizon * 2.598f;
        /// <summary>
        /// Where tides tear ships apart: where the tidal stretch 2GM·L/r³ equals the first hole's
        /// at 5 r_s. It grows only as the cube root of the mass, so a heavier hole is gentler at its horizon.
        /// </summary>
        public float TidalRadius => (float)(50000.0 * Math.Pow(GravitationalParameter / 5e11, 1.0 / 3.0));
        /// <summary>Warp jumps end at this distance.</summary>
        public float WarpStopRadius => Horizon * 60f;
        /// <summary>Beyond this its pull is ignored (0.0005 m/s²).</summary>
        public float InfluenceRadius => (float)Math.Sqrt(GravitationalParameter / 0.0005);

        /// <summary>Its position as a space the game's coordinate transposition understands.</summary>
        public FixedWorldSpace Space { get; internal set; }
        public string AnchorName { get; internal set; }

        public Vector3 Acceleration(Vector3 toHole)
        {
            float distance = toHole.magnitude;
            if (distance < 1f || distance > InfluenceRadius)
                return Vector3.zero;
            // Inside the horizon nothing escapes; keep the pull finite for the physics
            double r = Math.Max(distance, Horizon * 0.5f);
            return toHole / distance * (float)(GravitationalParameter / (r * r));
        }

        /// <summary>Gravitational time dilation of a static observer, sqrt(1 - r_s/r).</summary>
        public float TimeDilation(float distance) => distance <= Horizon ? 0f : Mathf.Sqrt(1f - Horizon / distance);

        /// <summary>Vector from <paramref name="position"/> (in <paramref name="spaceId"/>) to the hole, in that space.</summary>
        public Vector3 Offset(IWorldTranspose transpose, uint spaceId, Vector3 position)
        {
            Vector3Double center = transpose.Position(Space, spaceId, Vector3Double.Zero);
            return (Vector3)(center - (Vector3Double)position);
        }
    }

    internal static class BlackHole
    {
        public static readonly Hole[] All =
        {
            // Horizon 10 km, 0.5 m/s² at 1 000 km
            new Hole { Name = "Trou noir de Petra", AnchorPlanet = 102, OffsetFromAnchor = new Vector3(1.9e6f, 0.9e6f, -2.1e6f), Horizon = 10000f, DiskTilt = Quaternion.Euler(48f, 35f, -12f) },
            // Two and a half times heavier, near the ice planet
            new Hole { Name = "Trou noir de Glacia", AnchorPlanet = 105, OffsetFromAnchor = new Vector3(-2.4e6f, -1.3e6f, 3.1e6f), Horizon = 25000f, DiskTilt = Quaternion.Euler(-22f, 110f, 30f) }
        };

        private static bool _hasAnchors;

        public static bool Active => _hasAnchors && Settings.BlackHoleEnabled.Value;

        /// <summary>The holes placed in the world.</summary>
        public static IEnumerable<Hole> Placed => Active ? All.Where(h => h.Space != null) : Enumerable.Empty<Hole>();

        /// <summary>Finds the anchor planets once their tracker is up (client or server).</summary>
        public static bool EnsureAnchor()
        {
            if (_hasAnchors)
                return true;
            IPlanetsProvider planets = ServiceLocator.GetService<IPlanetsClientProvider>() as IPlanetsProvider ?? ServiceLocator.GetService<IPlanetsServerProvider>();
            if (planets == null)
                return false;
            IPlanetProperties template = planets.Planets.FirstOrDefault(p => p.Id == PlanetFactory.TemplateId);
            if (template == null)
                return false;
            for (int i = 0; i < All.Length; i++)
            {
                Hole hole = All[i];
                IPlanetProperties anchor = planets.Planets.FirstOrDefault(p => p.Id == hole.AnchorPlanet);
                // Without the mod planets the holes sit around the demo planet, apart from each other
                Vector3 offset = anchor != null ? hole.OffsetFromAnchor : hole.OffsetFromAnchor * (1f + i);
                anchor = anchor ?? template;
                hole.Space = new FixedWorldSpace { Position = new PreciseWorldPosition(anchor.WorldCell, Vector3.zero) + (Vector3Double)offset };
                hole.AnchorName = anchor.Name;
                AdvancedMod.Log.Msg($"black hole: {hole.Name}, {offset.magnitude / 1000f:0} km from {anchor.Name}, horizon {hole.Horizon / 1000f:0} km, tides below {hole.TidalRadius / 1000f:0} km");
            }
            _hasAnchors = true;
            return true;
        }

        public static void Forget()
        {
            _hasAnchors = false;
            foreach (Hole hole in All)
                hole.Space = null;
        }

        /// <summary>The hole nearest to <paramref name="position"/> (in <paramref name="spaceId"/>) and the vector to it.</summary>
        public static bool TryNearest(IWorldTranspose transpose, uint spaceId, Vector3 position, out Hole nearest, out Vector3 toHole)
        {
            nearest = null;
            toHole = Vector3.zero;
            if (transpose == null)
                return false;
            foreach (Hole hole in Placed)
            {
                Vector3 offset = hole.Offset(transpose, spaceId, position);
                if (nearest == null || offset.sqrMagnitude < toHole.sqrMagnitude)
                {
                    nearest = hole;
                    toHole = offset;
                }
            }
            return nearest != null;
        }

        // ---- Host: tidal destruction and the event horizon

        private static float _nextCheck;

        public static void ServerUpdate()
        {
            if (!Active || !GameServices.IsServer || Time.unscaledTime < _nextCheck)
                return;
            const float Interval = 0.25f;
            _nextCheck = Time.unscaledTime + Interval;
            try
            {
                IWorldTranspose transpose = ServiceLocator.GetService<IWorldTransposeServer>();
                ShipsServerTracker ships = ServiceLocator.GetService<ShipsServerTracker>();
                PlayersServerTracker players = ServiceLocator.GetService<PlayersServerTracker>();
                if (transpose == null)
                    return;
                if (ships != null)
                {
                    foreach (TrackedShipServer ship in ships.LoadedRootShips.ToList())
                    {
                        if (!TryNearest(transpose, ship.SpaceId, ship.Position, out Hole hole, out Vector3 toHole))
                            continue;
                        float distance = toHole.magnitude;
                        if (distance < hole.TidalRadius)
                            Tear(ships, ship, hole, distance, Interval);
                    }
                }
                if (players != null)
                {
                    foreach (TrackedPlayerServer player in players.ActivePlayers)
                    {
                        if (TryNearest(transpose, player.SpaceId, player.Position, out Hole hole, out Vector3 toHole) && toHole.magnitude < hole.Horizon)
                            Kill(player);
                    }
                }
            }
            catch (Exception e)
            {
                ErrorLog.Report("black hole", e);
            }
        }

        // Tidal forces rip parts off, from a few per second at the edge of the zone to the whole ship at the horizon
        private static void Tear(ShipsServerTracker ships, TrackedShipServer root, Hole hole, float distance, float interval)
        {
            var parts = new List<(IShipStateWrite Ship, HullPartPosition? Hull, ushort? Part)>();
            foreach (RootRelativeShipData data in ships.GetRootConnectedShips(root.Id))
            {
                if (!(data.Ship is IShipStateWrite write))
                    continue;
                foreach (StatefulPart part in write.StatefulParts)
                    parts.Add((write, null, part.Id));
                if (data.Ship is TrackedShip tracked)
                {
                    CoordBounds bounds = tracked.GetHullCoordBounds();
                    foreach (Vector3Int coord in Coords(bounds))
                    {
                        ShipVoxel voxel = tracked.GetVoxel(coord);
                        if (voxel == null || !voxel.HasPart)
                            continue;
                        foreach (VoxelPartType type in new[] { VoxelPartType.Floor, VoxelPartType.ForwardWall, VoxelPartType.RightWall, VoxelPartType.Interior })
                        {
                            if (voxel.GetPartFromType(type).HasValue)
                                parts.Add((write, new HullPartPosition(coord, type), null));
                        }
                    }
                }
            }
            if (parts.Count == 0)
                return;
            int count;
            if (distance <= hole.Horizon)
            {
                count = parts.Count;
                AdvancedMod.Log.Msg($"black hole: ship {root.Id} crossed the event horizon of {hole.Name}");
            }
            else
            {
                // 0 at the edge of the tidal zone, 1 at the horizon
                float depth = 1f - (distance - hole.Horizon) / (hole.TidalRadius - hole.Horizon);
                float perSecond = 2f + depth * depth * 60f;
                count = Mathf.Max(Random.value < perSecond * interval % 1f ? 1 : 0, Mathf.FloorToInt(perSecond * interval));
            }
            foreach (var part in parts.OrderBy(_ => Random.value).Take(count))
            {
                if (part.Part.HasValue)
                    part.Ship.DestroyStatefulPart(part.Part.Value);
                else
                    part.Ship.DestroyHullPart(part.Hull.Value);
            }
        }

        private static IEnumerable<Vector3Int> Coords(CoordBounds bounds)
        {
            for (int x = bounds.LowerCoord.x; x <= bounds.UpperCoord.x; x++)
                for (int y = bounds.LowerCoord.y; y <= bounds.UpperCoord.y; y++)
                    for (int z = bounds.LowerCoord.z; z <= bounds.UpperCoord.z; z++)
                        yield return new Vector3Int(x, y, z);
        }

        private static readonly AccessTools.FieldRef<TrackedPlayerServer, PlayerStatusUpdater> StatusUpdater =
            AccessTools.FieldRefAccess<TrackedPlayerServer, PlayerStatusUpdater>("_statusUpdater");

        private static void Kill(TrackedPlayerServer player)
        {
            PlayerStatusUpdater status = StatusUpdater(player);
            if (status == null)
                return;
            status.OnCollisionDamage(10000f);
            AdvancedMod.Log.Msg($"black hole: player {player.Id} crossed the event horizon");
        }
    }

    /// <summary>The holes' pull, added to the game's gravity (which is zero off planet ground) for ships and players.</summary>
    [HarmonyPatch(typeof(EnvironmentProvider), nameof(EnvironmentProvider.GetGravityAtPosition))]
    internal static class BlackHoleGravityPatch
    {
        private static IWorldTranspose _transpose;
        private static ISpacesProvider _spaces;

        private static void Postfix(EnvironmentProvider __instance, uint spaceId, Vector3 position, ref Vector3 __result)
        {
            if (!BlackHole.Active)
                return;
            try
            {
                if (_transpose == null || (_transpose is UnityEngine.Object unity && unity == null))
                    _transpose = ServiceLocator.GetService<IWorldTransposeServer>() as IWorldTranspose ?? ServiceLocator.GetService<IWorldTransposeClient>();
                if (_spaces == null || (_spaces is UnityEngine.Object spacesObject && spacesObject == null))
                    _spaces = ServiceLocator.GetService<ISpacesServerProvider>() as ISpacesProvider ?? ServiceLocator.GetService<ISpacesClientProvider>();
                // Inside a ship the game uses artificial gravity
                // (and spaces the game just removed, while an object moves between them)
                if (_spaces == null || !_spaces.TryGetSpace(spaceId, out ISpace space) || space is ShipRelativeSpace)
                    return;
                if (_transpose == null || AbandonedStations.IsSheltered(_transpose, spaceId, position))
                    return;
                foreach (Hole hole in BlackHole.Placed)
                {
                    Vector3 toHole = hole.Offset(_transpose, spaceId, position);
                    if (toHole.magnitude < hole.InfluenceRadius)
                        __result += hole.Acceleration(toHole);
                }
            }
            catch (Exception e)
            {
                ErrorLog.Report("black hole gravity", e);
            }
        }
    }

    /// <summary>Warp jumps end before a hole's sphere of influence, as they end before a star.</summary>
    [HarmonyPatch(typeof(EnvironmentProvider), nameof(EnvironmentProvider.CheckWarpCollision))]
    internal static class BlackHoleWarpPatch
    {
        // a - b, exact over astronomic distances (both expressed from a's world cell)
        private static Vector3Double Relative(PreciseWorldPosition a, PreciseWorldPosition b)
        {
            return (a - a.worldCell) - (b - a.worldCell);
        }

        private static void Postfix(PreciseWorldPosition position, Vector3 velocity, ref PreciseWorldPosition? __result)
        {
            if (!BlackHole.Active || velocity == Vector3.zero)
                return;
            try
            {
                foreach (Hole hole in BlackHole.Placed)
                {
                    Vector3 start = (Vector3)Relative(position, hole.Space.Position);
                    Vector3? hit = MathUtils.LineSphereShortestIntersection(start, velocity, Vector3.zero, hole.WarpStopRadius);
                    if (!hit.HasValue)
                        continue;
                    PreciseWorldPosition stop = hole.Space.Position + hit.Value;
                    if (__result.HasValue && Relative(__result.Value, position).Magnitude < Relative(stop, position).Magnitude)
                        continue;
                    __result = stop;
                }
            }
            catch (Exception e)
            {
                ErrorLog.Report("black hole warp", e);
            }
        }
    }
}

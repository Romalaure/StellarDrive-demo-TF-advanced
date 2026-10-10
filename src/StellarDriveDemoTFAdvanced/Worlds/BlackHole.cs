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
    /// A black hole near TF Petra, in the game's own units. The game's planets are scaled down
    /// (1 g at 320 km), so is the hole: it follows Schwarzschild's relations with the speed of
    /// light scaled like the rest of the world. The event horizon is where the escape velocity
    /// reaches that scaled light speed: r_s = 2GM / c².
    ///  - Gravity GM/r² for ships and players everywhere outside planet ground (the game itself
    ///    has none in orbit or deep space).
    ///  - Tidal zone (below <see cref="TidalRadius"/>): ships lose parts, faster and faster.
    ///  - Event horizon: ships are destroyed and players die.
    ///  - Warp jumps stop before entering its sphere of influence, like they stop before a star.
    /// </summary>
    internal static class BlackHole
    {
        /// <summary>Event horizon radius r_s (m).</summary>
        public const float Horizon = 10000f;
        /// <summary>GM (m³/s²): 0.5 m/s² at 1 000 km.</summary>
        public const double GravitationalParameter = 5e11;
        /// <summary>Light speed in the game's scale: escape velocity at the horizon, sqrt(2GM / r_s) = 10 km/s.</summary>
        public static readonly double LightSpeed = Math.Sqrt(2.0 * GravitationalParameter / Horizon);
        /// <summary>Visible shadow (photon capture) radius, 3√3/2 r_s.</summary>
        public const float Shadow = Horizon * 2.598f;
        public const float TidalRadius = Horizon * 5f;
        /// <summary>Warp jumps end at this distance.</summary>
        public const float WarpStopRadius = 600000f;
        /// <summary>Beyond this its pull is ignored (0.0005 m/s²).</summary>
        public const float InfluenceRadius = 3.2e7f;
        /// <summary>Where it sits: from TF Petra (or the demo planet without the mod planets).</summary>
        private static readonly Vector3 OffsetFromAnchor = new Vector3(1.9e6f, 0.9e6f, -2.1e6f);

        private static WorldCell _anchorCell;
        private static bool _hasAnchor;
        private static FixedWorldSpace _space;

        public static bool Active => _hasAnchor && Settings.BlackHoleEnabled.Value;

        /// <summary>The hole's position as a space the game's coordinate transposition understands.</summary>
        public static FixedWorldSpace Space => _space;

        public static string AnchorName { get; private set; }

        /// <summary>Finds the anchor planet once its tracker is up (client or server).</summary>
        public static bool EnsureAnchor()
        {
            if (_hasAnchor)
                return true;
            IPlanetsProvider planets = ServiceLocator.GetService<IPlanetsClientProvider>() as IPlanetsProvider ?? ServiceLocator.GetService<IPlanetsServerProvider>();
            if (planets == null)
                return false;
            IPlanetProperties anchor = planets.Planets.FirstOrDefault(p => p.Id == 102) ?? planets.Planets.FirstOrDefault(p => p.Id == PlanetFactory.TemplateId);
            if (anchor == null)
                return false;
            _anchorCell = anchor.WorldCell;
            _space = new FixedWorldSpace { Position = new PreciseWorldPosition(_anchorCell, Vector3.zero) + (Vector3Double)OffsetFromAnchor };
            AnchorName = anchor.Name;
            _hasAnchor = true;
            AdvancedMod.Log.Msg($"black hole: {OffsetFromAnchor.magnitude / 1000f:0} km from {anchor.Name}, horizon {Horizon / 1000f:0} km, light speed (game scale) {LightSpeed / 1000.0:0.0} km/s");
            return true;
        }

        public static void Forget()
        {
            _hasAnchor = false;
            _space = null;
        }

        /// <summary>Vector from <paramref name="position"/> (in <paramref name="spaceId"/>) to the hole, in that space.</summary>
        public static bool TryOffset(IWorldTranspose transpose, uint spaceId, Vector3 position, out Vector3 toHole)
        {
            toHole = Vector3.zero;
            if (!Active || transpose == null)
                return false;
            Vector3Double center = transpose.Position(_space, spaceId, Vector3Double.Zero);
            toHole = (Vector3)(center - (Vector3Double)position);
            return true;
        }

        public static Vector3 Acceleration(Vector3 toHole)
        {
            float distance = toHole.magnitude;
            if (distance < 1f || distance > InfluenceRadius)
                return Vector3.zero;
            // Inside the horizon nothing escapes; keep the pull finite for the physics
            double r = Math.Max(distance, Horizon * 0.5f);
            return toHole / distance * (float)(GravitationalParameter / (r * r));
        }

        /// <summary>Gravitational time dilation of a static observer, sqrt(1 - r_s/r).</summary>
        public static float TimeDilation(float distance) => distance <= Horizon ? 0f : Mathf.Sqrt(1f - Horizon / distance);

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
                        if (!TryOffset(transpose, ship.SpaceId, ship.Position, out Vector3 toHole))
                            continue;
                        float distance = toHole.magnitude;
                        if (distance < TidalRadius)
                            Tear(ships, ship, distance, Interval);
                    }
                }
                if (players != null)
                {
                    foreach (TrackedPlayerServer player in players.ActivePlayers)
                    {
                        if (TryOffset(transpose, player.SpaceId, player.Position, out Vector3 toHole) && toHole.magnitude < Horizon)
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
        private static void Tear(ShipsServerTracker ships, TrackedShipServer root, float distance, float interval)
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
            if (distance <= Horizon)
            {
                count = parts.Count;
                AdvancedMod.Log.Msg($"black hole: ship {root.Id} crossed the event horizon");
            }
            else
            {
                // 0 at the edge of the tidal zone, 1 at the horizon
                float depth = 1f - (distance - Horizon) / (TidalRadius - Horizon);
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

    /// <summary>Its pull, added to the game's gravity (which is zero off planet ground) for ships and players.</summary>
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
                if (BlackHole.TryOffset(_transpose, spaceId, position, out Vector3 toHole) && toHole.magnitude < BlackHole.InfluenceRadius
                    && !AbandonedStations.IsSheltered(_transpose, spaceId, position))
                    __result += BlackHole.Acceleration(toHole);
            }
            catch (Exception e)
            {
                ErrorLog.Report("black hole gravity", e);
            }
        }
    }

    /// <summary>Warp jumps end before its sphere of influence, as they end before a star.</summary>
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
                FixedWorldSpace hole = BlackHole.Space;
                Vector3 start = (Vector3)Relative(position, hole.Position);
                Vector3? hit = MathUtils.LineSphereShortestIntersection(start, velocity, Vector3.zero, BlackHole.WarpStopRadius);
                if (!hit.HasValue)
                    return;
                PreciseWorldPosition stop = hole.Position + hit.Value;
                if (__result.HasValue && Relative(__result.Value, position).Magnitude < Relative(stop, position).Magnitude)
                    return;
                __result = stop;
            }
            catch (Exception e)
            {
                ErrorLog.Report("black hole warp", e);
            }
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Core.Services;
using Core.Utils;
using Core.Values;
using HarmonyLib;
using Planets.EnvironmentGeneration.ToSpawn;
using Planets.Network;
using Planets.Services;
using Planets.Terrain;
using Planets.Terrain.Evaluation.Node;
using Planets.Terrain.Evaluation.Services;
using Planets.Terrain.Meshes;
using Planets.Terrain.Meshes.Colliders;
using Planets.Terrain.Model;
using Planets.Terrain.Utils;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;
using WorldTracking.Interface.Model;
using WorldTracking.Interface.Model.Spaces;
using WorldTracking.Interface.Services;

namespace StellarDriveDemoTFAdvanced.Worlds
{
    /// <summary>
    /// The ground's collider around a moving player or ship is made of terrain pieces whose shape
    /// the game works out on the main thread, 625 heights each, as soon as they are needed: at
    /// speed, a new piece now and then took 10 to 15 ms of one frame (a visible jerk), and a host
    /// worked out every piece twice (once for its server, once for its own view). The shapes are
    /// now kept for a while (the second side takes the first one's), and pieces ahead of anything
    /// faster than 25 m/s are worked out in advance on a background thread, from a copy of the
    /// planet's terrain recipe. The first shapes made in the background are checked against the
    /// game's own result; on any difference that planet goes back to the game's way.
    /// </summary>
    internal static class TerrainColliderCache
    {
        private const int MaxEntries = 512;
        private const float PrefetchSpeed = 25f;

        // ---- The terrain recipe, copied so a background thread can follow it

        private enum Kind : byte { Noise, Clamp, Abs, Clear, Add, Skip }

        private struct Op
        {
            public Kind Kind;
            public byte Buffer0, Buffer1;
            public float Frequency, Lacunarity, Gain, Strength, Min, Max, Smoothness;
            public int Octaves;
            public Vector3 Center;
        }

        private sealed class Recipe
        {
            public TerrainOperationNode[] Nodes;
            public Op[] Ops;
            public float PlanetSize;
            /// <summary>Only plain steps (no craters): it can be followed off the main thread.</summary>
            public bool Threadable;
            public int Checked;
            public bool Wrong;
        }

        private sealed class Entry
        {
            public Recipe Recipe;
            public Vector3[] Vertices;
            public bool FromWorker;
        }

        private struct Job
        {
            public (TerrainOperationStack, ulong) Key;
            public Recipe Recipe;
            public Vector3Double Scale, Position;
            public Matrix4x4 Rotation;
        }

        private static readonly Dictionary<TerrainOperationStack, Recipe> Recipes = new Dictionary<TerrainOperationStack, Recipe>();
        private static readonly Dictionary<(TerrainOperationStack, ulong), Entry> Entries = new Dictionary<(TerrainOperationStack, ulong), Entry>();
        private static readonly Queue<(TerrainOperationStack, ulong)> Order = new Queue<(TerrainOperationStack, ulong)>();
        private static readonly HashSet<(TerrainOperationStack, ulong)> Pending = new HashSet<(TerrainOperationStack, ulong)>();
        private static readonly object Gate = new object();
        private static BlockingCollection<Job> _jobs;
        private static Vector3[] _template;
        private static int _hits, _misses, _made, _ahead;
        private static float _nextReport;

        public static bool Enabled => !DevHarness.Off("terraincache");

        private static Recipe RecipeOf(TerrainOperationStack stack)
        {
            IReadOnlyList<TerrainOperationNode> nodes = stack.Stack;
            if (Recipes.TryGetValue(stack, out Recipe known) && known.Nodes.Length == nodes.Count)
            {
                bool same = true;
                for (int i = 0; i < nodes.Count && same; i++)
                    same = ReferenceEquals(known.Nodes[i], nodes[i]);
                if (same)
                    return known;
            }
            var recipe = new Recipe { Nodes = nodes.ToArray(), PlanetSize = stack.PlanetSize, Threadable = true };
            recipe.Ops = new Op[recipe.Nodes.Length];
            for (int i = 0; i < recipe.Nodes.Length; i++)
            {
                TerrainOperationNode node = recipe.Nodes[i];
                var op = new Op { Buffer0 = node.BufferId0, Kind = Kind.Skip };
                switch (node)
                {
                    case AddNoiseNode noise:
                        NoiseConfig c = noise.NoiseConfig;
                        op.Kind = Kind.Noise;
                        op.Frequency = c.frequency;
                        op.Lacunarity = c.lacunarity;
                        op.Gain = c.gain;
                        op.Octaves = c.octaves;
                        op.Strength = c.strength;
                        op.Center = c.center;
                        break;
                    case ClampNode clamp:
                        op.Kind = Kind.Clamp;
                        op.Min = clamp.Min;
                        op.Max = clamp.Max;
                        op.Smoothness = clamp.Smoothness;
                        break;
                    case AbsNode _:
                        op.Kind = Kind.Abs;
                        break;
                    case ClearBufferNode _:
                        op.Kind = Kind.Clear;
                        break;
                    case AddNode add:
                        op.Kind = Kind.Add;
                        op.Buffer1 = add.BufferId1;
                        break;
                    case ColorElevationNode _:
                    case DenseEnvironmentAmbientOcclusionNode _:
                        break;
                    default:
                        // Craters (their search tree grows as it is read) and anything unknown
                        recipe.Threadable = false;
                        break;
                }
                recipe.Ops[i] = op;
            }
            Recipes[stack] = recipe;
            return recipe;
        }

        // The ground's height at a point of the unit sphere (from the planet's center)
        private static float Elevation(Recipe recipe, Vector3 point, FastNoiseLite noise)
        {
            float b0 = 0f, b1 = 0f, last = 0f;
            foreach (Op op in recipe.Ops)
            {
                float value = op.Buffer0 == 0 ? b0 : b1;
                switch (op.Kind)
                {
                    case Kind.Noise:
                        noise.SetFrequency(op.Frequency);
                        noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2S);
                        noise.SetFractalType(FastNoiseLite.FractalType.FBm);
                        noise.SetFractalLacunarity(op.Lacunarity);
                        noise.SetFractalGain(op.Gain);
                        noise.SetFractalOctaves(op.Octaves);
                        Vector3 at = point + op.Center;
                        value += noise.GetNoise(at.x, at.y, at.z) * op.Strength;
                        break;
                    case Kind.Clamp:
                        value = op.Smoothness > 0f
                            ? MathUtils.SmoothMin(MathUtils.SmoothMax(value, op.Min, op.Smoothness), op.Max, op.Smoothness)
                            : Mathf.Clamp(value, op.Min, op.Max);
                        break;
                    case Kind.Abs:
                        value = Mathf.Abs(value);
                        break;
                    case Kind.Clear:
                        value = 0f;
                        break;
                    case Kind.Add:
                        value += op.Buffer1 == 0 ? b0 : b1;
                        break;
                }
                if (op.Buffer0 == 0)
                    b0 = value;
                else
                    b1 = value;
                last = value;
            }
            return last + recipe.PlanetSize;
        }

        private static Vector3 SpherePoint(int i, Vector3Double scale, Vector3Double position, Matrix4x4 rotation) =>
            (Vector3)PlanetSphereUtils.CubeToSphere(PlanetSphereUtils.TemplateToCube((Vector3Double)_template[i], scale, position, rotation));

        // ---- The cache, in front of the game's own calculation

        private static bool _computing;

        /// <summary>Before the game works out a piece: its kept shape, when there is one.</summary>
        public static bool Before(Chunk chunk, TerrainOperationStack stack, ref ChunkMeshCpuData result)
        {
            _computing = false;
            if (!Enabled || chunk == null || stack == null)
                return true;
            Recipe recipe = RecipeOf(stack);
            Entry entry;
            lock (Gate)
                Entries.TryGetValue((stack, chunk.TreeSignature.Hash), out entry);
            if (entry != null && entry.Recipe == recipe && Trust(entry, chunk, stack))
            {
                _hits++;
                var mesh = new Mesh { vertices = entry.Vertices, triangles = QuadTemplates.QuadTemplateTriangles[0] };
                mesh.RecalculateBounds();
                result = new ChunkMeshCpuData(chunk, mesh);
                return false;
            }
            _misses++;
            _computing = true;
            return true;
        }

        /// <summary>After the game worked one out: kept for the other side and the way back.</summary>
        public static void After(Chunk chunk, TerrainOperationStack stack, ChunkMeshCpuData result)
        {
            if (!_computing || result?.Mesh == null)
                return;
            _computing = false;
            Store((stack, chunk.TreeSignature.Hash), new Entry { Recipe = RecipeOf(stack), Vertices = result.Mesh.vertices });
        }

        private static void Store((TerrainOperationStack, ulong) key, Entry entry)
        {
            lock (Gate)
            {
                if (!Entries.ContainsKey(key))
                    Order.Enqueue(key);
                Entries[key] = entry;
                while (Entries.Count > MaxEntries && Order.Count > 0)
                    Entries.Remove(Order.Dequeue());
            }
        }

        // The first shapes made in the background, compared with the game's own heights
        private static bool Trust(Entry entry, Chunk chunk, TerrainOperationStack stack)
        {
            Recipe recipe = entry.Recipe;
            if (!entry.FromWorker || recipe.Checked >= 8)
                return !recipe.Wrong;
            if (recipe.Wrong)
                return false;
            var calculator = ServiceLocator.GetService<PlanetTerrainPointCpuCalculator>();
            if (calculator == null)
                return false;
            foreach (int i in new[] { 0, _template.Length / 2, _template.Length - 1, 77 })
            {
                Vector3 point = SpherePoint(i, chunk.CubeScale, chunk.CubePosition, chunk.RotationMatrix);
                Vector3 expected = point * calculator.GetElevation(stack, point);
                if ((expected - entry.Vertices[i]).sqrMagnitude > 0.01f)
                {
                    recipe.Wrong = true;
                    AdvancedMod.Log.Warning($"terrain colliders: shapes made ahead differ from the game's ({expected} vs {entry.Vertices[i]}): made on the main thread for this planet");
                    return false;
                }
            }
            recipe.Checked++;
            return true;
        }

        // ---- Ahead of fast movers

        private sealed class Motion
        {
            public Vector3 Position, Ahead;
            public uint Space;
            public float Time;
            public Vector3 Velocity;
        }

        private static readonly Dictionary<uint, Motion> Motions = new Dictionary<uint, Motion>();
        private static readonly AccessTools.FieldRef<PlanetColliders, ISpacesProvider> SpacesField = AccessTools.FieldRefAccess<PlanetColliders, ISpacesProvider>("_spaces");
        private static readonly AccessTools.FieldRef<PlanetColliders, IPlanetCollidersProvider> PlanetsField = AccessTools.FieldRefAccess<PlanetColliders, IPlanetCollidersProvider>("_planets");
        private static readonly AccessTools.FieldRef<TrackedPlanet, PlanetTreeTraversor> TraversorField = AccessTools.FieldRefAccess<TrackedPlanet, PlanetTreeTraversor>("_planetTreeTraversor");
        private static readonly AccessTools.FieldRef<TrackedPlanet, PlanetQuadTree> TreeField = AccessTools.FieldRefAccess<TrackedPlanet, PlanetQuadTree>("_planetQuadTree");
        private static readonly AccessTools.FieldRef<TrackedPlanet, PlanetMeshCollider> MeshColliderField = AccessTools.FieldRefAccess<TrackedPlanet, PlanetMeshCollider>("_meshCollider");

        /// <summary>After the game updated the colliders around the movers of a world (server or view).</summary>
        public static void Ahead(PlanetColliders colliders, IEnumerable<ITrackedWorldObject> movers)
        {
            if (!Enabled || colliders == null || movers == null)
                return;
            Report();
            float now = Time.time;
            int seen = 0;
            foreach (ITrackedWorldObject mover in movers)
            {
                if (++seen > 16)
                    break;
                if (!(SpacesField(colliders).GetSpace(mover.SpaceId) is PlanetSurfaceSpace space))
                    continue;
                if (!Motions.TryGetValue(mover.Id, out Motion motion) || motion.Space != mover.SpaceId)
                {
                    Motions[mover.Id] = new Motion { Position = mover.Position, Ahead = mover.Position, Space = mover.SpaceId, Time = now };
                    continue;
                }
                // The view and the server both report the same mover in the same frame
                if (now - motion.Time > 0.02f)
                {
                    motion.Velocity = (mover.Position - motion.Position) / (now - motion.Time);
                    motion.Position = mover.Position;
                    motion.Time = now;
                }
                float speed = motion.Velocity.magnitude;
                if (speed < PrefetchSpeed || speed > 20000f)
                    continue;
                Vector3 ahead = mover.Position + motion.Velocity / speed * Mathf.Clamp(speed * 0.4f, 60f, 260f);
                if ((ahead - motion.Ahead).sqrMagnitude < 40f * 40f)
                    continue;
                motion.Ahead = ahead;
                if (PlanetsField(colliders).GetPlanetColliderGenerator(space.PlanetId) is TrackedPlanet planet)
                    Prefetch(planet, space, ahead);
            }
            if (Motions.Count > 256)
                Motions.Clear();
        }

        private static void Prefetch(TrackedPlanet planet, PlanetSurfaceSpace space, Vector3 position)
        {
            TerrainOperationStack stack = planet.TerrainOperationStack;
            if (stack == null)
                return;
            Recipe recipe = RecipeOf(stack);
            if (!recipe.Threadable || recipe.Wrong)
                return;
            StartWorker();
            if (_jobs.Count > 48)
                return;
            // The pieces the game would pick there (the next real update traverses again)
            PlanetTreeTraversor traversor = TraversorField(planet);
            traversor.BeforeTraversal((Vector3)space.Center + position);
            traversor.TraverseQuadTree(TreeField(planet));
            foreach (PlanetTerrainColliderToSpawn piece in MeshColliderField(planet).GetCollidersAtPosition())
            {
                Chunk chunk = piece.Chunk;
                var key = (stack, chunk.TreeSignature.Hash);
                lock (Gate)
                {
                    if (Entries.ContainsKey(key) || !Pending.Add(key))
                        continue;
                }
                _ahead++;
                _jobs.Add(new Job { Key = key, Recipe = recipe, Scale = chunk.CubeScale, Position = chunk.CubePosition, Rotation = chunk.RotationMatrix });
            }
        }

        private static void StartWorker()
        {
            if (_jobs != null)
                return;
            _template = QuadTemplates.QuadTemplateVertices[0].ToArray();
            _jobs = new BlockingCollection<Job>();
            var thread = new Thread(Work) { IsBackground = true, Name = "TFA terrain colliders", Priority = System.Threading.ThreadPriority.BelowNormal };
            thread.Start();
        }

        private static void Work()
        {
            var noise = new FastNoiseLite();
            foreach (Job job in _jobs.GetConsumingEnumerable())
            {
                try
                {
                    var vertices = new Vector3[_template.Length];
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        Vector3 point = SpherePoint(i, job.Scale, job.Position, job.Rotation);
                        vertices[i] = point * Elevation(job.Recipe, point, noise);
                    }
                    Store(job.Key, new Entry { Recipe = job.Recipe, Vertices = vertices, FromWorker = true });
                    Interlocked.Increment(ref _made);
                }
                catch (Exception e)
                {
                    job.Recipe.Wrong = true;
                    MelonLoader.MelonLogger.Warning($"[TFA] terrain colliders: background shape failed: {e.Message}");
                }
                finally
                {
                    lock (Gate)
                        Pending.Remove(job.Key);
                }
            }
        }

        private static void Report()
        {
            if (!DevHarness.Active || Time.unscaledTime < _nextReport)
                return;
            _nextReport = Time.unscaledTime + 10f;
            AdvancedMod.Log.Msg($"dev: terrain colliders: {_hits} kept, {_misses} worked out on the main thread, {_ahead} asked ahead, {_made} made ahead");
            _hits = _misses = _ahead = _made = 0;
        }
    }

    [HarmonyPatch(typeof(PlanetMeshCpuGenerator), nameof(PlanetMeshCpuGenerator.Calculate))]
    internal static class TerrainColliderCachePatch
    {
        private static bool Prefix(Chunk chunk, TerrainOperationStack operationStack, ref ChunkMeshCpuData __result) =>
            TerrainColliderCache.Before(chunk, operationStack, ref __result);

        private static void Postfix(Chunk chunk, TerrainOperationStack operationStack, ChunkMeshCpuData __result) =>
            TerrainColliderCache.After(chunk, operationStack, __result);
    }

    [HarmonyPatch(typeof(PlanetColliders), nameof(PlanetColliders.UpdateGeneratedColliders))]
    internal static class TerrainColliderAheadPatch
    {
        private static void Postfix(PlanetColliders __instance, IEnumerable<ITrackedWorldObject> activeObjects) =>
            TerrainColliderCache.Ahead(__instance, activeObjects);
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Common
{
    /// <summary>
    /// Development check (DevProfile): times every method of this mod run each frame or physics
    /// step (the Melon callbacks, Update/LateUpdate/FixedUpdate/OnGUI of its components, Tick and
    /// Draw helpers, and every Harmony patch it applies), and every 10 s logs the costliest ones
    /// (inclusive time: a method's callees count in it too), the frame time, garbage collections,
    /// and what this mod adds to the scene (lights, shadows, renderers, particles).
    /// </summary>
    internal static class DevProfiler
    {
        private sealed class Entry
        {
            public string Name;
            public long Ticks, FrameTicks;
            public int Calls;
        }

        // The methods run since the last frame, to show what a slow frame was made of
        private static readonly List<Entry> Touched = new List<Entry>();
        private static int _spikes;

        private static readonly Dictionary<MethodBase, Entry> Entries = new Dictionary<MethodBase, Entry>();
        private static readonly string[] FrameNames = { "Update", "LateUpdate", "FixedUpdate", "OnGUI", "OnUpdate", "OnLateUpdate", "OnFixedUpdate",
            "ServerUpdate", "Tick", "Draw", "BeforeTransfers", "AfterTransfers", "Steer" };
        private static float _since, _nextReport;
        private static int _frames, _gcStart, _reports;
        /// <summary>The A/B window: this mod's renderers hidden, to see what drawing them costs.</summary>
        private static readonly List<Renderer> Hidden = new List<Renderer>();
        private static readonly List<(Renderer, UnityEngine.Rendering.ShadowCastingMode)> Shadowless = new List<(Renderer, UnityEngine.Rendering.ShadowCastingMode)>();

        public static void Install(HarmonyLib.Harmony harmony)
        {
            if (!Settings.DevProfile.Value || !DevHarness.Active)
                return;
            var targets = new HashSet<MethodBase>();
            Assembly mine = typeof(DevProfiler).Assembly;
            // The patches this mod applied to the game
            foreach (MethodBase patched in HarmonyLib.Harmony.GetAllPatchedMethods().ToList())
            {
                Patches info = HarmonyLib.Harmony.GetPatchInfo(patched);
                if (info == null)
                    continue;
                foreach (Patch patch in info.Prefixes.Concat(info.Postfixes).Concat(info.Finalizers))
                {
                    if (patch.owner == harmony.Id && patch.PatchMethod.DeclaringType?.Assembly == mine)
                        targets.Add(patch.PatchMethod);
                }
            }
            // Per-frame methods of its own types
            Type[] types;
            try
            {
                types = mine.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types.Where(t => t != null).ToArray();
            }
            foreach (Type type in types)
            {
                if (type.IsGenericTypeDefinition || type == typeof(DevProfiler))
                    continue;
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (FrameNames.Contains(method.Name) && !method.IsAbstract && !method.ContainsGenericParameters && method.GetMethodBody() != null)
                        targets.Add(method);
                }
                AddCoroutine(targets, type);
            }
            if (Settings.DevProfileGame.Value)
                AddGame(targets, mine);
            if (Settings.DevProfileGame.Value)
            {
                AddModKit(targets);
                AddInside(targets);
            }
            // The part state writes, and how many change nothing
            MethodInfo write = AccessTools.Method(typeof(Ships.Interface.Model.ShipState), nameof(Ships.Interface.Model.ShipState.SetPartState));
            if (write != null)
            {
                targets.Add(write);
                harmony.Patch(write, prefix: new HarmonyMethod(typeof(DevProfiler), nameof(CountSame)));
            }
            var prefix = new HarmonyMethod(typeof(DevProfiler), nameof(Before));
            var postfix = new HarmonyMethod(typeof(DevProfiler), nameof(After));
            int count = 0;
            foreach (MethodBase target in targets)
            {
                try
                {
                    Assembly from = target.DeclaringType?.Assembly;
                    string owner = from == mine ? "" : "[" + from?.GetName().Name + "] ";
                    Type declaring = target.DeclaringType;
                    string label = declaring != null && declaring.Name.StartsWith("<") && target.Name == "MoveNext"
                        ? declaring.DeclaringType?.Name + "." + declaring.Name.Substring(1, Math.Max(1, declaring.Name.IndexOf('>') - 1)) + " (coroutine)"
                        : declaring?.Name + "." + target.Name;
                    Entries[target] = new Entry { Name = owner + label };
                    harmony.Patch(target, prefix: prefix, postfix: postfix);
                    count++;
                }
                catch (Exception e)
                {
                    Entries.Remove(target);
                    AdvancedMod.Log.Warning($"dev: profile: {target.DeclaringType?.Name}.{target.Name} not timed: {e.GetBaseException().Message}");
                }
            }
            AdvancedMod.Log.Msg($"dev: profile: timing {count} method(s)");
            InstallPhases();
            MethodInfo view = AccessTools.Method(typeof(Planets.PlanetObjects), "UpdateVisibleObjectsCreation");
            if (view != null)
                harmony.Patch(view, prefix: new HarmonyMethod(typeof(DevProfiler), nameof(PlanetView)));
        }

        // ------------------------------------------------------------------ engine phases

        private static readonly Dictionary<string, long> PhaseTicks = new Dictionary<string, long>();
        private static readonly Dictionary<string, long> PhaseStart = new Dictionary<string, long>();
        private static readonly Dictionary<string, long> PhaseFrame = new Dictionary<string, long>();
        private static int _gcLast;

        /// <summary>
        /// Markers around each phase of Unity's player loop (and around physics and rendering inside
        /// them): how long the engine spends in each, scripts or not.
        /// </summary>
        // SDModKit's own per-frame work and the update of every mod built on it
        private static void AddModKit(HashSet<MethodBase> targets)
        {
            Assembly kit = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.GetName().Name == "SDModKit");
            if (kit == null)
                return;
            foreach ((string type, string method) in new[] { ("SDModKit.ContentResearch", "Tick"), ("SDModKit.KeyTips", "A"), ("SDModKit.InteractionTips", "a"),
                         ("SDModKit.LaserTuning", "A"), ("SDModKit.ItemCables", "A"), ("SDModKit.ModKit", "A") })
            {
                Type t = kit.GetType(type) ?? kit.GetTypes().FirstOrDefault(x => x.FullName != null && x.FullName.EndsWith("." + type.Split('.').Last()));
                MethodInfo m = t?.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(x => x.Name == method && x.GetParameters().Length == 0 && x.GetMethodBody() != null);
                if (m != null)
                    targets.Add(m);
            }
            // Inside the costliest of them (the submarine pack): what it does each frame
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies().Where(x => x.GetName().Name == "SubmarineAddons" || x.GetName().Name == "StationKit"))
            {
                Type[] all;
                try { all = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException e) { all = e.Types.Where(t => t != null).ToArray(); }
                foreach (Type type in all.Where(t => t != null && !t.IsGenericTypeDefinition))
                {
                    foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if ((method.Name == "Update" || method.Name == "EnsureResearch" || method.Name == "GiveToEveryone" || method.Name == "ListenOnHost" || method.Name == "LateUpdate")
                            && !method.IsAbstract && !method.ContainsGenericParameters && method.GetMethodBody() != null)
                            targets.Add(method);
                    }
                }
            }
            Type sdMod = kit.GetTypes().FirstOrDefault(x => x.Name == "SDMod");
            if (sdMod == null)
                return;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
                catch { continue; }
                foreach (Type type in types.Where(t => t != null && t != sdMod && sdMod.IsAssignableFrom(t)))
                {
                    MethodInfo update = type.GetMethod("OnUpdate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    if (update != null && update.GetMethodBody() != null)
                        targets.Add(update);
                }
            }
        }

        private static void InstallPhases()
        {
            try
            {
                UnityEngine.LowLevel.PlayerLoopSystem loop = UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop();
                UnityEngine.LowLevel.PlayerLoopSystem[] phases = loop.subSystemList;
                for (int i = 0; i < phases.Length; i++)
                {
                    string phase = phases[i].type?.Name ?? "phase" + i;
                    var list = new List<UnityEngine.LowLevel.PlayerLoopSystem>();
                    list.Add(Marker(phase, true));
                    foreach (UnityEngine.LowLevel.PlayerLoopSystem sub in phases[i].subSystemList ?? new UnityEngine.LowLevel.PlayerLoopSystem[0])
                    {
                        string subName = sub.type?.Name ?? "";
                        // The heavy engine steps, timed on their own
                        bool watch = subName.Contains("Physics") || subName.Contains("Render") || subName.Contains("Canvas") || subName.Contains("Director")
                            || subName.Contains("Animator") || subName.Contains("ScriptRun") || subName.Contains("Gfx") || subName.Contains("Particle") || subName.Contains("Finish") || subName.Contains("Present") || subName.Contains("Wait");
                        if (watch)
                            list.Add(Marker(phase + "." + subName, true));
                        list.Add(sub);
                        if (watch)
                            list.Add(Marker(phase + "." + subName, false));
                    }
                    list.Add(Marker(phase, false));
                    phases[i].subSystemList = list.ToArray();
                }
                loop.subSystemList = phases;
                UnityEngine.LowLevel.PlayerLoop.SetPlayerLoop(loop);
                AdvancedMod.Log.Msg("dev: profile: player loop phases timed");
            }
            catch (Exception e)
            {
                AdvancedMod.Log.Warning("dev: profile: player loop not timed: " + e.Message);
            }
        }

        private struct PhaseMarker
        {
        }

        private static UnityEngine.LowLevel.PlayerLoopSystem Marker(string name, bool start) => new UnityEngine.LowLevel.PlayerLoopSystem
        {
            type = typeof(PhaseMarker),
            updateDelegate = start
                ? (UnityEngine.LowLevel.PlayerLoopSystem.UpdateFunction)(() => PhaseStart[name] = Stopwatch.GetTimestamp())
                : () =>
                {
                    if (PhaseStart.TryGetValue(name, out long from))
                    {
                        PhaseTicks.TryGetValue(name, out long sum);
                        long spent = Stopwatch.GetTimestamp() - from;
                        PhaseTicks[name] = sum + spent;
                        PhaseFrame.TryGetValue(name, out long frame);
                        PhaseFrame[name] = frame + spent;
                    }
                }
        };

        private static void ReportPhases(double frames)
        {
            foreach (var phase in PhaseTicks.OrderByDescending(p => p.Value).Take(14))
                AdvancedMod.Log.Msg($"dev: profile phase {phase.Value * 1000.0 / Stopwatch.Frequency / frames,8:0.00} ms/frame  {phase.Key}");
            PhaseTicks.Clear();
        }

        private static readonly string[] UnityCallbacks = { "Update", "LateUpdate", "FixedUpdate", "OnGUI" };
        private static readonly string[] MelonCallbacks = { "OnUpdate", "OnLateUpdate", "OnFixedUpdate", "OnGUI" };

        /// <summary>
        /// DevProfileGame: also the per-frame callbacks of the game and of the other mods (every
        /// MonoBehaviour's Update, LateUpdate, FixedUpdate and OnGUI, every Melon's OnUpdate...).
        /// </summary>
        // The steps inside the game's heaviest per-frame and per-tick updaters
        private static void AddInside(HashSet<MethodBase> targets)
        {
            foreach (string name in new[] { "Managers.Client.ClientVisualsUpdater", "Managers.Client.Services.ClientWorldVisualsUpdater",
                         "Managers.Server.WorldPhysicsServerUpdater", "Managers.Client.WorldPhysicsClientUpdater",
                         "Planets.Services.PlanetColliders", "Planets.Terrain.Meshes.Colliders.PlanetMeshCollider",
                         "Planets.EnvironmentGeneration.PlanetDenseEnvironmentPhysics", "Planets.EnvironmentGeneration.PlanetHotspotsEnvironmentPhysics",
                         "Planets.EnvironmentGeneration.PlanetStaticEnvironmentPhysics", "Planets.Terrain.Meshes.Colliders.PlanetMeshColliderChunk", "Planets.Terrain.Meshes.PlanetMeshCpuGenerator", "Planets.Terrain.Evaluation.Services.PlanetTerrainPointCpuCalculator" })
            {
                Type type = AccessTools.TypeByName(name);
                if (type == null)
                    continue;
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (!method.IsAbstract && !method.ContainsGenericParameters && method.GetMethodBody() != null && !method.Name.StartsWith("NetworkInitialize")
                        && !method.Name.StartsWith("Awake") && !method.Name.StartsWith("get_") && !method.Name.StartsWith("set_"))
                        targets.Add(method);
                }
            }
            // What the network loop runs: every message handler of the game and the mods (FishNet
            // RPC readers, broadcast handlers), and the ships' server physics steps
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string assemblyName = assembly.GetName().Name;
                if (assembly.IsDynamic || assemblyName.StartsWith("UnityEngine") || assemblyName.StartsWith("Unity.") || assemblyName.StartsWith("System") || assemblyName == "mscorlib"
                    || assemblyName.StartsWith("FishNet") || assemblyName.StartsWith("MelonLoader") || assemblyName.Contains("Harmony") || assemblyName.StartsWith("Mono."))
                    continue;
                Type[] all;
                try
                {
                    all = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    all = e.Types.Where(t => t != null).ToArray();
                }
                foreach (Type type in all)
                {
                    if (type.IsGenericTypeDefinition)
                        continue;
                    bool physics = type.FullName == "Ships.Physics.ShipServerPhysics" || type.FullName == "Ships.Services.ShipsServerTracker";
                    foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (method.IsAbstract || method.ContainsGenericParameters || method.GetMethodBody() == null)
                            continue;
                        if (method.Name.StartsWith("RpcReader___") || method.Name.StartsWith("RpcLogic___") || (physics && !method.Name.StartsWith("get_") && !method.Name.StartsWith("set_")))
                            targets.Add(method);
                    }
                }
            }
            // Each planet's visual update and the steps in it
            Type planet = AccessTools.TypeByName("Planets.Network.TrackedPlanetClient");
            if (planet == null)
                return;
            var steps = new HashSet<string> { "GetCollidersToSpawnAtPosition", "SpawnCollider", "UpdateVisuals", "UpdateVisibleObjectsCreation", "UpdateVisibleObjects", "TraverseQuadTree", "BeforeTraversal" };
            foreach (Type type in new[] { planet, AccessTools.TypeByName("Planets.Network.TrackedPlanet") }.Where(t => t != null).Concat(planet.GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Select(f => f.FieldType)).Distinct())
            {
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (steps.Contains(method.Name) && !method.IsAbstract && !method.ContainsGenericParameters && method.GetMethodBody() != null)
                        targets.Add(method);
                }
            }
        }

        private static void AddGame(HashSet<MethodBase> targets, Assembly mine)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = assembly.GetName().Name;
                if (assembly == mine || assembly.IsDynamic || name.StartsWith("UnityEngine") || name.StartsWith("Unity.") || name.StartsWith("System")
                    || name == "mscorlib" || name == "netstandard" || name.StartsWith("Mono.") || name.StartsWith("MelonLoader") || name.Contains("Harmony")
                    || name.StartsWith("Microsoft") || name.StartsWith("Il2Cpp") || name.StartsWith("Newtonsoft") || name.StartsWith("Sirenix"))
                    continue;
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types.Where(t => t != null).ToArray();
                }
                catch
                {
                    continue;
                }
                foreach (Type type in types)
                {
                    try
                    {
                        if (type.IsGenericTypeDefinition || type.IsInterface)
                            continue;
                        AddCoroutine(targets, type);
                        bool behaviour = typeof(MonoBehaviour).IsAssignableFrom(type);
                        bool melon = typeof(MelonLoader.MelonBase).IsAssignableFrom(type);
                        if (!behaviour && !melon)
                            continue;
                        foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        {
                            if ((behaviour ? UnityCallbacks : MelonCallbacks).Contains(method.Name) && method.GetParameters().Length == 0
                                && !method.IsAbstract && !method.ContainsGenericParameters && method.GetMethodBody() != null)
                                targets.Add(method);
                        }
                    }
                    catch
                    {
                        // a type that cannot be read: skipped
                    }
                }
            }
        }

        /// <summary>A coroutine's step (the compiler's iterator class): its MoveNext.</summary>
        private static void AddCoroutine(HashSet<MethodBase> targets, Type type)
        {
            if (!type.IsClass || type.IsGenericTypeDefinition || !typeof(System.Collections.IEnumerator).IsAssignableFrom(type) || !type.Name.Contains("<"))
                return;
            MethodInfo step = type.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (step != null && step.GetMethodBody() != null)
                targets.Add(step);
        }

        private static int _writes, _same;

        private static void CountSame(Ships.Interface.Model.ShipState __instance, ushort id, Ships.Interface.Model.Parts.StateTypes.IStatefulPartState state)
        {
            _writes++;
            if (__instance.TryGetPart(id, out var part) && Equals(part.State, state))
                _same++;
        }

        private static void Before(out long __state) => __state = Stopwatch.GetTimestamp();

        private static void After(long __state, MethodBase __originalMethod)
        {
            if (Entries.TryGetValue(__originalMethod, out Entry entry))
            {
                long spent = Stopwatch.GetTimestamp() - __state;
                if (entry.FrameTicks == 0)
                    Touched.Add(entry);
                entry.Ticks += spent;
                entry.FrameTicks += spent;
                entry.Calls++;
            }
        }

        // A frame over 25 ms: the timed methods that took most of it (8 per report at most)
        private static void Spike()
        {
            float ms = Time.unscaledDeltaTime * 1000f;
            if (ms > 25f && _spikes < 8 && Time.unscaledTime > 30f)
            {
                _spikes++;
                var top = Touched.OrderByDescending(e => e.FrameTicks).Take(6)
                    .Select(e => $"{e.Name} {e.FrameTicks * 1000.0 / Stopwatch.Frequency:0.0}");
                var phases = PhaseFrame.OrderByDescending(p => p.Value).Take(8)
                    .Select(p => $"{p.Key} {p.Value * 1000.0 / Stopwatch.Frequency:0.0}");
                AdvancedMod.Log.Msg($"dev: profile spike {ms:0} ms (GCs {GC.CollectionCount(0) - _gcLast}): {string.Join(", ", top)} | {string.Join(", ", phases)}");
            }
            _gcLast = GC.CollectionCount(0);
            PhaseFrame.Clear();
            foreach (Entry e in Touched)
                e.FrameTicks = 0;
            Touched.Clear();
        }

        /// <summary>Every frame (from the mod's update): counts frames, reports every 15 s.</summary>
        public static void Frame()
        {
            if (Entries.Count == 0)
                return;
            float now = Time.unscaledTime;
            if (_nextReport <= 0f)
            {
                Reset(now);
                return;
            }
            _frames++;
            Spike();
            if (now < _nextReport)
                return;
            _spikes = 0;
            float seconds = now - _since;
            double frames = Math.Max(1, _frames);
            double totalMs = 0;
            var rows = new List<(string, double, int)>();
            foreach (Entry e in Entries.Values)
            {
                if (e.Calls == 0)
                    continue;
                double ms = e.Ticks * 1000.0 / Stopwatch.Frequency;
                rows.Add((e.Name, ms, e.Calls));
            }
            AdvancedMod.Log.Msg($"dev: profile {seconds:0} s{(Hidden.Count > 0 ? $" HIDDEN ({Hidden.Count})" : Shadowless.Count > 0 ? $" NO SHADOWS ({Shadowless.Count})" : " normal")}: {_frames / seconds:0.0} fps, {seconds * 1000f / frames:0.00} ms/frame, gen0 GCs {GC.CollectionCount(0) - _gcStart}, heap {GC.GetTotalMemory(false) / 1048576:0} MB");
            foreach ((string name, double ms, int calls) in rows.OrderByDescending(r => r.Item2).Take(Settings.DevProfileGame.Value ? 40 : 25))
            {
                totalMs += ms;
                AdvancedMod.Log.Msg($"dev: profile   {ms / frames,7:0.000} ms/frame {calls / frames,8:0.0} calls/frame  {name}");
            }
            AdvancedMod.Log.Msg($"dev: profile part state writes {_writes / seconds:0}/s, of which unchanged {_same / seconds:0}/s");
            _writes = _same = 0;
            ReportPhases(frames);
            if (_reports == 4)
                Scene();
            // After a minute, the windows cycle: normal, this mod's shadows off, this mod's renderers hidden
            _reports++;
            foreach (Renderer renderer in Hidden)
            {
                if (renderer != null)
                    renderer.enabled = true;
            }
            Hidden.Clear();
            foreach ((Renderer renderer, UnityEngine.Rendering.ShadowCastingMode mode) in Shadowless)
            {
                if (renderer != null)
                    renderer.shadowCastingMode = mode;
            }
            Shadowless.Clear();
            int cycle = _reports >= 6 ? (_reports - 6) % 3 : 0;
            if (cycle != 0)
            {
                foreach (Renderer renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    if (!renderer.enabled || !Mine(renderer.transform))
                        continue;
                    if (cycle == 2)
                    {
                        renderer.enabled = false;
                        Hidden.Add(renderer);
                    }
                    else if (renderer.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off)
                    {
                        Shadowless.Add((renderer, renderer.shadowCastingMode));
                        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                }
            }
            Reset(now);
        }

        private static void Reset(float now)
        {
            foreach (Entry e in Entries.Values)
            {
                e.Ticks = 0;
                e.Calls = 0;
            }
            _since = now;
            _nextReport = now + 10f;
            _frames = 0;
            _gcStart = GC.CollectionCount(0);
        }

        /// <summary>What is in the scene: lights (with shadows), renderers and particles, this mod's apart.</summary>
        private static void Scene()
        {
            Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            int lit = lights.Count(l => l.isActiveAndEnabled), shadowed = lights.Count(l => l.isActiveAndEnabled && l.shadows != LightShadows.None);
            int mineLit = lights.Count(l => l.isActiveAndEnabled && Mine(l.transform)), mineShadowed = lights.Count(l => l.isActiveAndEnabled && l.shadows != LightShadows.None && Mine(l.transform));
            Renderer[] renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            int visible = renderers.Count(r => r.isVisible), mineRenderers = renderers.Count(r => Mine(r.transform)), mineVisible = renderers.Count(r => r.isVisible && Mine(r.transform));
            int mineShadowCasters = renderers.Count(r => r.isVisible && r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off && Mine(r.transform));
            int particles = UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Count(p => p.isPlaying);
            AdvancedMod.Log.Msg($"dev: profile scene: lights {lit} ({shadowed} with shadows), this mod's {mineLit} ({mineShadowed} with shadows); renderers {renderers.Length} ({visible} visible), this mod's {mineRenderers} ({mineVisible} visible, {mineShadowCasters} casting shadows); particle systems playing {particles}");
            foreach (var group in renderers.Where(r => r.enabled && Mine(r.transform)).GroupBy(r => Strip(r.name)).OrderByDescending(g => g.Count()).Take(15))
                AdvancedMod.Log.Msg($"dev: profile all   {group.Count(),5} enabled ({group.Count(r => r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off)} shadows) {group.Key}");
            // The names this mod's objects carry most
            foreach (var group in renderers.Where(r => r.isVisible && Mine(r.transform)).GroupBy(r => Strip(r.name)).OrderByDescending(g => g.Count()).Take(30))
            {
                Renderer first = group.First();
                int triangles = first is MeshRenderer && first.GetComponent<MeshFilter>()?.sharedMesh != null && first.GetComponent<MeshFilter>().sharedMesh.isReadable ? first.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3 : -1;
                AdvancedMod.Log.Msg($"dev: profile scene   {group.Count(),5} visible ({group.Count(r => r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off)} shadows, {triangles} tris) {group.Key} under {Strip(first.transform.parent?.name ?? "")} / {Strip(first.transform.root.name)}");
            }
        }

        private static readonly Dictionary<Planets.PlanetObjects, Vector3> PlanetViews = new Dictionary<Planets.PlanetObjects, Vector3>();
        private static readonly AccessTools.FieldRef<Planets.PlanetObjects, bool> HasFar = AccessTools.FieldRefAccess<Planets.PlanetObjects, bool>("_hasFarPlanet");
        private static readonly AccessTools.FieldRef<Planets.PlanetObjects, bool> HasMid = AccessTools.FieldRefAccess<Planets.PlanetObjects, bool>("_hasMidPlanet");
        private static readonly AccessTools.FieldRef<Planets.PlanetObjects, bool> HasNear = AccessTools.FieldRefAccess<Planets.PlanetObjects, bool>("_hasNearPlanet");

        private static void PlanetView(Planets.PlanetObjects __instance, Vector3 viewingObjectPos) => PlanetViews[__instance] = viewingObjectPos;

        /// <summary>Which planets the game holds loaded (far image, mid mesh, near terrain), and from where it sees them.</summary>
        private static void PlanetsLoaded()
        {
            foreach (var view in PlanetViews)
            {
                if (view.Key == null)
                    continue;
                AdvancedMod.Log.Msg($"dev: profile planet {view.Key.gameObject.name}: seen from {view.Value.magnitude / 1000f:0} km ({view.Value}), far {HasFar(view.Key)}, mid {HasMid(view.Key)}, near {HasNear(view.Key)}");
            }
        }

        private static bool Mine(Transform t) => t.name.StartsWith("TF") || (t.parent != null && (t.parent.name.StartsWith("TF") || (t.parent.parent != null && t.parent.parent.name.StartsWith("TF"))));

        private static string Strip(string name) => name.Length > 40 ? name.Substring(0, 40) : name;
    }
}

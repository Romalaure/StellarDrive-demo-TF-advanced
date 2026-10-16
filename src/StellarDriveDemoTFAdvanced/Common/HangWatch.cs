using System.Threading;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Common
{
    /// <summary>
    /// Development check (DevDisable "hangwatch" turns it on): a background thread that logs the
    /// last step the main thread marked when no frame has passed for 10 s. With "hangtrace" too,
    /// once the menu is up, every method of the game and of the mods it depends on marks itself.
    /// </summary>
    internal static class HangWatch
    {
        public static volatile string Step = "start";
        private static volatile int _frame;
        private static Thread _thread, _main;

        public static void Mark(string step) => Step = step;

        private static bool _traced;

        /// <summary>Once the menu is up (tracing slows the game's start a lot).</summary>
        public static void OnScene()
        {
            if (_traced || _thread == null || !DevHarness.Off("hangtrace"))
                return;
            _traced = true;
            Trace();
        }

        public static void Start()
        {
            if (_thread != null || !DevHarness.Off("hangwatch"))
                return;
            _main = Thread.CurrentThread;
            _thread = new Thread(() =>
            {
                int last = -1, still = 0;
                while (true)
                {
                    Thread.Sleep(1000);
                    int frame = _frame;
                    still = frame == last ? still + 1 : 0;
                    last = frame;
                    if (still == 10 || still == 30)
                        AdvancedMod.Log.Warning($"dev: hang: no frame for {still} s, frame {frame}, last step: {Step}\n   recent:\n      {RecentSteps()}");
                }
            }) { IsBackground = true, Name = "TFA hang watch" };
            _thread.Start();
        }

        public static void Tick() => _frame = Time.frameCount;

        // Every method of the game's ship assemblies marks the step it enters (main thread only)
        private static void Trace()
        {
            var harmony = new HarmonyLib.Harmony("tfa.hangwatch");
            var prefix = new HarmonyLib.HarmonyMethod(typeof(HangWatch), nameof(Enter));
            int patched = 0;
            foreach (System.Reflection.Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = assembly.GetName().Name;
                if (System.Array.IndexOf(new[] { "Ships", "Ships.Interface", "Assembly-CSharp", "WorldTracking", "WorldTracking.Interface", "Planets", "Players", "Saving", "Saving.Migration",
                        "Saving.Serialization", "SDModKit", "StellarIndustries", "ItemLines", "Core", "WorldObjects", "WorldProvision", "Research", "UI", "Tools", "NecessaryFixes", "BuildPlus" }, name) < 0)
                    continue;
                System.Type[] types;
                try { types = assembly.GetTypes(); } catch (System.Reflection.ReflectionTypeLoadException e) { types = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Where(e.Types, t => t != null)); }
                foreach (System.Type type in types)
                {
                    if (type.IsGenericTypeDefinition || type.IsInterface || type.Namespace == null || type.Namespace.StartsWith("FishNet"))
                        continue;
                    if (type.Namespace.Contains("Generated") || type.Name.Contains("<"))
                        continue;
                    foreach (System.Reflection.MethodInfo method in type.GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly))
                    {
                        if (method.IsAbstract || method.IsGenericMethodDefinition || method.GetMethodBody() == null || method.Name.Contains("<") || method.Name.StartsWith("get_") || method.Name.StartsWith("set_")
                            || method.Name == "Update" || method.Name == "LateUpdate" || method.Name == "FixedUpdate" || method.Name.StartsWith("op_") || method.Name == "Equals" || method.Name == "GetHashCode")
                            continue;
                        try { harmony.Patch(method, prefix: prefix); patched++; } catch { }
                    }
                }
            }
            AdvancedMod.Log.Msg($"dev: hang watch traces {patched} methods");
        }

        private static void Enter(System.Reflection.MethodBase __originalMethod)
        {
            if (Thread.CurrentThread != _main)
                return;
            System.Type type = __originalMethod.DeclaringType;
            string step = type.Assembly.GetName().Name + ":" + type.FullName + "." + __originalMethod.Name + "(" + string.Join(",", System.Linq.Enumerable.Select(__originalMethod.GetParameters(), p => p.ParameterType.Name)) + ")";
            if (step == Step)
                return;
            Step = step;
            lock (Recent)
            {
                Recent.Enqueue(step);
                while (Recent.Count > 12)
                    Recent.Dequeue();
            }
        }

        private static readonly System.Collections.Generic.Queue<string> Recent = new System.Collections.Generic.Queue<string>();

        private static string RecentSteps()
        {
            lock (Recent)
                return string.Join("\n      ", Recent);
        }
    }
}

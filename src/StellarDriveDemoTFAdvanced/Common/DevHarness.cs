using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Managers.Interface;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace StellarDriveDemoTFAdvanced.Common
{
    /// <summary>
    /// Development harness, entirely off unless DevWorld is set in MelonPreferences: from the main
    /// menu it loads that save in single player (use a copy, the game autosaves), then writes a
    /// probe of the world's celestial objects, takes screenshots and quits after DevQuitSeconds.
    /// Lets the mod's world features be checked without a person at the keyboard.
    /// </summary>
    internal static class DevHarness
    {
        private const string GameScene = "MainGame";

        private static bool _loadRequested;
        private static float _menuSince = -1f;
        private static float _worldSince = -1f;
        private static int _shots;
        private static bool _probed;

        public static string Folder => Path.Combine(MelonEnvironment.UserDataDirectory, "TFAdvanced", "dev");

        /// <summary>Extra probes and screenshots other features add while the harness runs (seconds after world load, action).</summary>
        public static readonly List<(float Delay, string Name, Action Run)> Steps = new List<(float, string, Action)>();
        private static readonly HashSet<string> Done = new HashSet<string>();

        /// <summary>Development: a feature switched off by DevDisable (comma separated names), to find what misbehaves.</summary>
        public static bool Off(string feature) => Active && !string.IsNullOrEmpty(Settings.DevDisable.Value) && ("," + Settings.DevDisable.Value.Replace(" ", "") + ",").Contains("," + feature + ",");

        public static bool Active => !string.IsNullOrWhiteSpace(Settings.DevWorld.Value);

        public static void OnSceneLoaded(string sceneName)
        {
            if (!Active)
                return;
            if (sceneName == GameScene)
            {
                _worldSince = Time.unscaledTime;
                _menuSince = -1f;
            }
            else if (!_loadRequested)
            {
                _menuSince = Time.unscaledTime;
            }
        }

        public static void Update()
        {
            if (!Active)
                return;
            float now = Time.unscaledTime;
            if (_menuSince >= 0f && !_loadRequested && now - _menuSince > 6f)
            {
                _loadRequested = true;
                string path = Settings.DevWorld.Value.Trim();
                AdvancedMod.Log.Msg("dev: loading " + path);
                MainGameSceneLoadData.Mode = MainGameSceneLoadData.GameMode.SinglePlayer;
                MainGameSceneLoadData.SaveFileFullName = path;
                SceneManager.LoadScene(GameScene, LoadSceneMode.Single);
                return;
            }
            if (_worldSince < 0f)
                return;
            float inWorld = now - _worldSince;
            if (!_probed && inWorld > 20f)
            {
                _probed = true;
                Try("world probe", WorldProbe);
            }
            foreach ((float delay, string name, Action run) in Steps)
            {
                if (inWorld > delay && Done.Add(name))
                    Try(name, run);
            }
            if (_shots < 2 && inWorld > 22f + _shots * 4f)
                Screenshot("view" + _shots++);
            float quit = Settings.DevQuitSeconds.Value;
            if (quit > 0f && inWorld > quit)
            {
                AdvancedMod.Log.Msg("dev: quitting");
                Application.Quit();
            }
        }

        public static void Screenshot(string name)
        {
            Directory.CreateDirectory(Folder);
            string path = Path.Combine(Folder, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            AdvancedMod.Log.Msg("dev: screenshot " + path);
        }

        private static void Try(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                AdvancedMod.Log.Error($"dev: {what} failed: {e}");
            }
        }

        /// <summary>Every celestial object, and the full component and settings tree of the demo's planet.</summary>
        private static void WorldProbe()
        {
            Directory.CreateDirectory(Folder);
            var text = new StringBuilder();
            Type celestial = AccessTools.TypeByName("WorldTracking.Interface.Model.CelestialObject");
            foreach (Object obj in Resources.FindObjectsOfTypeAll(celestial))
            {
                var component = (Component)obj;
                if (component.gameObject.scene.name == null)
                    continue;
                text.AppendLine($"celestial {component.GetType().FullName} '{component.name}' active={component.gameObject.activeInHierarchy} pos={component.transform.position} id={Field(component, "id")}");
            }
            Type planetType = AccessTools.TypeByName("Planets.Planet");
            foreach (Object obj in Resources.FindObjectsOfTypeAll(planetType))
            {
                var planet = (Component)obj;
                if (planet.gameObject.scene.name == null || !planet.gameObject.activeInHierarchy)
                    continue;
                text.AppendLine();
                text.AppendLine($"=== active planet '{planet.name}'");
                DumpTree(planet.transform, 0, text);
            }
            File.WriteAllText(Path.Combine(Folder, "world_probe.txt"), text.ToString());
            AdvancedMod.Log.Msg("dev: world probe written");
        }

        internal static void DumpTree(Transform node, int depth, StringBuilder text)
        {
            string indent = new string(' ', depth * 2);
            text.AppendLine($"{indent}[{node.name}] active={node.gameObject.activeSelf} layer={node.gameObject.layer} pos={node.localPosition} scale={node.localScale}");
            foreach (Component component in node.GetComponents<Component>())
            {
                if (component == null)
                    continue;
                text.AppendLine($"{indent}  + {component.GetType().FullName}");
                if (depth <= 3)
                    DumpFields(component, indent + "      ", text, 0);
            }
            if (depth < 4)
            {
                foreach (Transform child in node)
                    DumpTree(child, depth + 1, text);
            }
            else if (node.childCount > 0)
            {
                text.AppendLine($"{indent}  ({node.childCount} children)");
            }
        }

        // Serialized fields of game components and of the settings assets they reference
        private static void DumpFields(object target, string indent, StringBuilder text, int level)
        {
            Type type = target.GetType();
            if (type.Namespace != null && (type.Namespace.StartsWith("UnityEngine") || type.Namespace.StartsWith("TMPro")))
                return;
            for (Type t = type; t != null && t != typeof(MonoBehaviour) && t != typeof(ScriptableObject) && t != typeof(object); t = t.BaseType)
            {
                foreach (FieldInfo field in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (!field.IsPublic && field.GetCustomAttribute<SerializeField>() == null)
                        continue;
                    object value;
                    try
                    {
                        value = field.GetValue(target);
                    }
                    catch
                    {
                        continue;
                    }
                    text.AppendLine($"{indent}{field.Name} : {field.FieldType.Name} = {Describe(value)}");
                    if (level >= 2 || value == null)
                        continue;
                    if (value is ScriptableObject so)
                        DumpFields(so, indent + "    ", text, level + 1);
                    else if (value is IList list && !(value is string) && list.Count > 0 && list.Count <= 12)
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            object item = list[i];
                            if (item == null)
                                continue;
                            if (item is ScriptableObject || (!(item is Object) && !item.GetType().IsPrimitive && item.GetType().IsSerializable))
                            {
                                text.AppendLine($"{indent}    [{i}] {Describe(item)}");
                                DumpFields(item, indent + "        ", text, level + 1);
                            }
                        }
                    }
                    else if (!(value is Object) && !field.FieldType.IsPrimitive && !field.FieldType.IsEnum && field.FieldType.IsSerializable && !(value is string))
                    {
                        DumpFields(value, indent + "    ", text, level + 1);
                    }
                }
            }
        }

        private static string Describe(object value)
        {
            switch (value)
            {
                case null: return "null";
                case Object unity when unity == null: return "missing";
                case Object unity: return $"'{unity.name}' ({unity.GetType().Name})";
                case string s: return '"' + s + '"';
                case IList list: return $"{value.GetType().Name}[{list.Count}]" + (list.Count > 0 && list.Count <= 6 ? " {" + string.Join(", ", list.Cast<object>().Select(o => o is Object u && u != null ? u.name : o?.ToString())) + "}" : "");
                default: return value.ToString();
            }
        }

        private static object Field(object target, string name)
        {
            for (Type t = target.GetType(); t != null; t = t.BaseType)
            {
                FieldInfo field = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                    return field.GetValue(target);
            }
            return null;
        }
    }
}

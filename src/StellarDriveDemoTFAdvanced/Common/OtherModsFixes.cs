using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Common
{
    /// <summary>
    /// Performance fixes for other mods, when they are installed (setting OtherModsFixes).
    /// StationKit's HandTool.EnsureResearch (called every frame by the submarine pack for its
    /// repair torch) searches every loaded object of the game for the research list each time:
    /// 2.4 ms a frame, more than all of TF Advanced, enough to keep the game under 82 fps on a
    /// 165 Hz screen. It only makes sure a research entry exists, so once every 2 s does the same.
    /// </summary>
    internal static class OtherModsFixes
    {
        private static readonly Dictionary<object, float> NextRun = new Dictionary<object, float>();

        public static void Install(HarmonyLib.Harmony harmony)
        {
            if (!Settings.OtherModsFixes.Value)
                return;
            Type tool = AccessTools.TypeByName("StationKit.HandTool");
            MethodInfo ensure = tool != null ? AccessTools.Method(tool, "EnsureResearch") : null;
            if (ensure == null)
                return;
            harmony.Patch(ensure, prefix: new HarmonyMethod(typeof(OtherModsFixes), nameof(EnsureResearchPrefix)));
            AdvancedMod.Log.Msg("other mods: StationKit's research check limited to once every 2 s");
        }

        private static bool EnsureResearchPrefix(object __instance)
        {
            float now = Time.unscaledTime;
            if (__instance == null)
                return true;
            if (NextRun.TryGetValue(__instance, out float next) && now < next)
                return false;
            NextRun[__instance] = now + 2f;
            return true;
        }
    }
}

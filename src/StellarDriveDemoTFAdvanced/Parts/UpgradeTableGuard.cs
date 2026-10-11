using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Ships.Interface.Settings;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Parts
{
    /// <summary>
    /// The Upgrade Table mod builds its table (part 2100) from the first part it finds with a
    /// research table's state. Since this mod's laboratory module is a research table too, it
    /// could take the laboratory and turn the upgrade table into a station module. Its lookup is
    /// given the game's own research table (part 22) instead.
    /// </summary>
    internal static class UpgradeTableGuard
    {
        private const ushort GameResearchTable = 22, UpgradeTable = 2100;

        public static void Install(HarmonyLib.Harmony harmony)
        {
            Type mod = AccessTools.TypeByName("UpgradeTable.UpgradeTableMod");
            MethodInfo lookup = mod != null ? AccessTools.Method(mod, "a", Type.EmptyTypes) : null;
            if (lookup == null || lookup.ReturnType != typeof(PartSettings))
                return;
            harmony.Patch(lookup, prefix: new HarmonyMethod(typeof(UpgradeTableGuard), nameof(Prefix)));
            AdvancedMod.Log.Msg("the Upgrade Table mod builds its table from the game's research table");
        }

        private static bool Prefix(ref PartSettings __result)
        {
            PartSettings[] all = Resources.FindObjectsOfTypeAll<PartSettings>();
            PartSettings existing = all.FirstOrDefault(p => p != null && p.id == UpgradeTable);
            if (existing != null)
            {
                __result = existing;
                return false;
            }
            PartSettings source = all.FirstOrDefault(p => p != null && p.id == GameResearchTable && p.internalStateClass == "ResearchTableState");
            if (source == null)
                return true;
            PartSettings table = UnityEngine.Object.Instantiate(source);
            table.name = "UpgradeTable";
            table.id = UpgradeTable;
            table.fullLabel = "Upgrade Table";
            table.hideFlags = HideFlags.DontSave;
            __result = table;
            return false;
        }
    }
}

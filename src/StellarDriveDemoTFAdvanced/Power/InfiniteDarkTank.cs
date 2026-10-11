using System.Reflection;
using HarmonyLib;
using Ships;
using Ships.Cables.Fluids;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Interface.Settings;
using Ships.Physics;
using StellarDriveDemoTFAdvanced.Items;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Power
{
    /// <summary>
    /// Infinite dark matter tank: the game's fluid tank, always full of dark matter. Whatever the
    /// pipes draw from it is made out of nothing; its contents weigh nothing.
    /// </summary>
    internal static class InfiniteDarkTank
    {
        public const ushort Id = 7394;

        public static bool Is(PartSettings settings) => settings != null && settings.id == Id;
    }

    /// <summary>It starts full.</summary>
    [HarmonyPatch(typeof(PartStateFactory), nameof(PartStateFactory.BuildDefaultState))]
    internal static class InfiniteDarkTankStatePatch
    {
        private static void Postfix(PartSettings partSettings, ref IStatefulPartState __result)
        {
            if (InfiniteDarkTank.Is(partSettings) && __result is FluidTankState tank)
                __result = (IStatefulPartState)tank.WithFluid(ModItems.DarkMatter, FluidTankState.Capacity, 0f);
        }
    }

    /// <summary>After each step of its fluid network it is full again.</summary>
    [HarmonyPatch]
    internal static class InfiniteDarkTankRefillPatch
    {
        private static readonly System.Type Container = AccessTools.Inner(typeof(FluidGroupServerState), "PartFluidContainer");
        private static readonly FieldInfo PartField = AccessTools.Field(Container, "_part");
        private static readonly FieldInfo FluidField = AccessTools.Field(Container, "_fluidItemId");
        private static readonly FieldInfo QuantityField = AccessTools.Field(Container, "_fluidQuantity");

        private static MethodBase TargetMethod() => AccessTools.Method(Container, "CommitChanges");

        private static void Prefix(object __instance)
        {
            if (!(PartField.GetValue(__instance) is StatefulPart part) || !InfiniteDarkTank.Is(part.Settings))
                return;
            FluidField.SetValue(__instance, ModItems.DarkMatter);
            QuantityField.SetValue(__instance, FluidTankState.Capacity);
        }
    }

    /// <summary>A full tank would make the ship heavy: its contents count for nothing.</summary>
    [HarmonyPatch(typeof(StatefulPartsMassTracker), "EnsureMassChangeIsAccounted")]
    internal static class InfiniteDarkTankMassPatch
    {
        private static void Prefix(StatefulPart part, ref IStatefulPartState state)
        {
            if (part != null && InfiniteDarkTank.Is(part.Settings))
                state = null;
        }
    }
}

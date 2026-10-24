using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Ships.Cables.Fluids;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Physics;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Propulsion
{
    /// <summary>
    /// A fluid line is allowed to go below empty. The game then counts that as negative mass
    /// (half a kilo per unit). Once the tanks on a line nearly cancel out, the next burn is shared
    /// as quantity / total x consumption, and a few units of propellant write tens of thousands of
    /// units of negative fluid. The ship's mass walks back toward its real weight at 4.5% per
    /// physics step, which is the pressure share in BalanceContainersPressureWithinGroup, and
    /// jumps further negative the next time the thrusters fire. Thrust on that mass throws the ship.
    /// Nothing here changes a line that still has propellant in it.
    /// </summary>
    [HarmonyPatch(typeof(FluidGroupServerState), "BalanceContainersPressureWithinGroup")]
    internal static class FluidOverdrawPatch
    {
        private static readonly FieldInfo Consumption = AccessTools.Field(typeof(FluidGroupServerState), "_currentConsumption");
        private static readonly FieldInfo Addition = AccessTools.Field(typeof(FluidGroupServerState), "_currentAddition");

        private static void Prefix(FluidGroupServerState __instance)
        {
            if (Consumption == null)
                return;
            float consumption = (float)Consumption.GetValue(__instance);
            float addition = Addition != null ? (float)Addition.GetValue(__instance) : 0f;
            // A line that is already negative has nothing real left to burn.
            float have = __instance.InitialTotalFluid > 0f ? __instance.InitialTotalFluid + Mathf.Max(0f, addition) : Mathf.Max(0f, addition);
            if (consumption > have)
                Consumption.SetValue(__instance, have);
        }
    }

    /// <summary>Shares of a transfer are allowed to come out negative. Floor them.</summary>
    [HarmonyPatch]
    internal static class FluidQuantityFloor
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (string name in new[] { "PartFluidContainer", "CableFluidContainer", "TouchingConnectionBuffer" })
            {
                System.Type type = AccessTools.Inner(typeof(FluidGroupServerState), name);
                MethodInfo method = AccessTools.Method(type, "SetFluid");
                if (method != null)
                    yield return method;
            }
        }

        private static void Prefix(ref float fluidQuantity)
        {
            if (fluidQuantity < 0f)
                fluidQuantity = 0f;
        }
    }

    /// <summary>
    /// A tank or a pipe loaded already empty-below-zero never passes through SetFluid.
    /// Floor it as it is written back, so the saved state and the next step stay empty rather than negative.
    /// </summary>
    [HarmonyPatch]
    internal static class FluidCommitFloor
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (string name in new[] { "PartFluidContainer", "CableFluidContainer" })
            {
                System.Type type = AccessTools.Inner(typeof(FluidGroupServerState), name);
                MethodInfo method = AccessTools.Method(type, "CommitChanges");
                if (method != null)
                    yield return method;
            }
        }

        private static void Prefix(object __instance)
        {
            FieldInfo field = AccessTools.Field(__instance.GetType(), "_fluidQuantity")
                ?? AccessTools.Field(__instance.GetType(), "<FluidQuantity>k__BackingField");
            if (field != null && field.GetValue(__instance) is float quantity && quantity < 0f)
                field.SetValue(__instance, 0f);
        }
    }

    /// <summary>Whatever still reports a negative fill weighs nothing, not a negative number of kilos.</summary>
    [HarmonyPatch(typeof(StatefulPartsMassTracker), "EnsureMassChangeIsAccounted")]
    internal static class NegativeFluidMassPatch
    {
        private static void Prefix(ref IStatefulPartState state)
        {
            if (state is FluidTankState tank && tank.FluidQuantity < 0f)
                state = tank with { FluidQuantity = 0f };
        }
    }
}

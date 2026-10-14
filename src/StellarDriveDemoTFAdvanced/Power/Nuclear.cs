using System.Collections.Generic;
using HarmonyLib;
using Items.Model;
using Players.Network;
using Ships.Cables.Fluids;
using Ships.Cables.Power;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.State;
using Ships.Interface.Services;
using Ships.Parts.EthanolDistiller;
using StellarDriveDemoTFAdvanced.Common;
using StellarDriveDemoTFAdvanced.Items;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Power
{
    /// <summary>
    /// Nuclear power (host simulation). Uranium ore is mined on the planets; the enrichment
    /// centrifuge, a clone of the game's ethanol distiller, takes it in its slot (by hand or by
    /// item line) and, with power, turns it into nuclear fuel, a fluid, in the line on its fluid
    /// socket 0 (empty or already nuclear fuel). Nuclear fuel feeds the nuclear reactor (see
    /// <see cref="DarkMatter"/>) and the nuclear engines.
    /// The game keeps running its distiller code: these patches take the centrifuges out of it and
    /// make every distiller ignore uranium as biomass.
    /// </summary>
    internal static class Centrifuge
    {
        public const byte FluidSocket = 0;
        public const byte PowerSocket = 2;

        // What each centrifuge may do this tick (0-1), and the ore it has partly used
        public static readonly Dictionary<(uint, ushort), float> Planned = new Dictionary<(uint, ushort), float>();
        public static readonly Dictionary<(uint, ushort), float> Partial = new Dictionary<(uint, ushort), float>();

        public static bool Is(StatefulPart part) => part?.Settings != null && part.Settings.id == PowerCatalog.CentrifugeId;

        public static int Ore(EthanolDistillerState state)
        {
            int count = 0;
            foreach (InventorySlot slot in state.InventorySlots)
            {
                if (slot.HasItem && slot.ItemId == ModItems.Uranium)
                    count += slot.Quantity;
            }
            return count;
        }

        public static bool LineTakesFuel(FluidGroupServerState line) =>
            line.FluidItemId == 0 || line.FluidItemId == ModItems.NuclearFuel || line.Available <= 0f;

        public static void Forget()
        {
            Planned.Clear();
            Partial.Clear();
        }
    }

    /// <summary>A distiller only burns wood: uranium in its slot is not biomass.</summary>
    [HarmonyPatch(typeof(EthanolDistillerState), nameof(EthanolDistillerState.GetAvailableBiomass))]
    internal static class DistillerBiomassPatch
    {
        private static bool Prefix(EthanolDistillerState __instance, ref float __result)
        {
            InventorySlot[] slots = __instance.InventorySlots;
            if (slots == null || slots.Length == 0 || !slots[0].HasItem || slots[0].ItemId == ItemsConstants.WoodItemId)
                return true;
            __result = __instance.PartialBiomass;
            return false;
        }
    }

    /// <summary>The centrifuges' power demand, instead of the distiller's.</summary>
    [HarmonyPatch(typeof(EthanolDistillersUpdater), nameof(EthanolDistillersUpdater.GuessPowerConsumptionAndProductionRate))]
    internal static class CentrifugeDemandPatch
    {
        private static void Postfix(float deltaTime, IShipStateWrite ____ship, FluidsServerTracker ____fluidsServerTracker,
            PowerServerTracker ____powerServerTracker, Dictionary<uint, float> ____guessedProductions)
        {
            try
            {
                float oreStep = Mathf.Max(1e-4f, PowerCatalog.CentrifugeOrePerSecond.Value) * deltaTime;
                float fuelStep = oreStep * Mathf.Max(0f, PowerCatalog.CentrifugeFuelPerOre.Value);
                foreach (StatefulPart part in ____ship.GetAllStatefulParts<EthanolDistillerState>())
                {
                    if (!Centrifuge.Is(part))
                        continue;
                    // The game's distiller code leaves it alone
                    ____guessedProductions.Remove(part.Id);
                    var key = (____ship.Id, part.Id);
                    Centrifuge.Planned[key] = 0f;
                    if (!____fluidsServerTracker.TryGetGroupFromSocket(new EntityPartSocket(____ship.Id, part.Id, Centrifuge.FluidSocket), out FluidGroupServerState line)
                        || !Centrifuge.LineTakesFuel(line)
                        || !____powerServerTracker.TryGetGroupFromSocket(new EntityPartSocket(____ship.Id, part.Id, Centrifuge.PowerSocket), out PowerGroupServerState power))
                        continue;
                    Centrifuge.Partial.TryGetValue(key, out float partial);
                    float ore = Centrifuge.Ore((EthanolDistillerState)part.State) + partial;
                    float rate = Mathf.Min(Mathf.Min(ore, oreStep) / oreStep, fuelStep > 0f ? Mathf.Min(fuelStep, line.ReceivingCapacity) / fuelStep : 0f);
                    if (rate <= 0f)
                        continue;
                    power.TotalPowerNeeded += rate * Mathf.Max(0f, PowerCatalog.CentrifugeWatts.Value) * deltaTime;
                    Centrifuge.Planned[key] = rate;
                }
            }
            catch (System.Exception e)
            {
                ErrorLog.Report("centrifuge demand", e);
            }
        }
    }

    /// <summary>The centrifuges use their ore and make nuclear fuel, as far as their network powered them.</summary>
    [HarmonyPatch(typeof(EthanolDistillersUpdater), nameof(EthanolDistillersUpdater.Update))]
    internal static class CentrifugeUpdatePatch
    {
        private static void Postfix(float deltaTime, IShipStateWrite ____ship, FluidsServerTracker ____fluidsServerTracker, PowerServerTracker ____powerServerTracker)
        {
            try
            {
                float oreStep = Mathf.Max(1e-4f, PowerCatalog.CentrifugeOrePerSecond.Value) * deltaTime;
                float fuelStep = oreStep * Mathf.Max(0f, PowerCatalog.CentrifugeFuelPerOre.Value);
                foreach (StatefulPart part in ____ship.GetAllStatefulParts<EthanolDistillerState>())
                {
                    if (!Centrifuge.Is(part))
                        continue;
                    var key = (____ship.Id, part.Id);
                    var state = (EthanolDistillerState)part.State;
                    float value = 0f;
                    if (Centrifuge.Planned.TryGetValue(key, out float planned) && planned > 0f
                        && ____fluidsServerTracker.TryGetGroupFromSocket(new EntityPartSocket(____ship.Id, part.Id, Centrifuge.FluidSocket), out FluidGroupServerState line)
                        && ____powerServerTracker.TryGetGroupFromSocket(new EntityPartSocket(____ship.Id, part.Id, Centrifuge.PowerSocket), out PowerGroupServerState power))
                    {
                        value = planned * Mathf.Clamp01(power.NormalizedConsumerRate);
                        if (value > 0f)
                        {
                            Centrifuge.Partial.TryGetValue(key, out float partial);
                            int whole = Centrifuge.Ore(state);
                            float left = Mathf.Max(0f, whole + partial - value * oreStep);
                            int keep = Mathf.Min(whole, Mathf.FloorToInt(left));
                            Centrifuge.Partial[key] = Mathf.Max(0f, left - keep);
                            state = state with { InventorySlots = Take(state.InventorySlots, whole - keep) };
                            line.AddFluid(ModItems.NuclearFuel, value * fuelStep);
                        }
                    }
                    if (!Mathf.Approximately(state.NormalizedProductionRate, value) || !ReferenceEquals(state, part.State))
                        ____ship.UpdatePartState(part.Id, state with { NormalizedProductionRate = value, PartialBiomass = 0f });
                }
            }
            catch (System.Exception e)
            {
                ErrorLog.Report("centrifuge", e);
            }
        }

        // Removes this much uranium ore from the slots
        private static InventorySlot[] Take(InventorySlot[] slots, int count)
        {
            var result = (InventorySlot[])slots.Clone();
            for (int i = result.Length - 1; i >= 0 && count > 0; i--)
            {
                InventorySlot slot = result[i];
                if (!slot.HasItem || slot.ItemId != ModItems.Uranium)
                    continue;
                int used = Mathf.Min(count, slot.Quantity);
                slot.Quantity = (byte)(slot.Quantity - used);
                if (slot.Quantity == 0)
                    slot.HasItem = false;
                result[i] = slot;
                count -= used;
            }
            return result;
        }
    }

    /// <summary>A centrifuge takes uranium ore only (from the player's inventory).</summary>
    [HarmonyPatch]
    internal static class CentrifugeChestPatch
    {
        private static System.Reflection.MethodBase TargetMethod() =>
            AccessTools.Method(AccessTools.Inner(typeof(Players.Inventory.PlayerInventoryTransactions), "ChestAccessor"), "CanReceiveItem");

        private static void Postfix(uint itemId, ref bool __result, PlayerNetworkState ____state, IShipsServerProvider ____shipsServer)
        {
            try
            {
                var context = new PartContext { ShipId = ____state.ServerState.EntityId, PartId = ____state.ServerState.PartId };
                if (____shipsServer != null && ____shipsServer.TryGetStatefulPart(context, out StatefulPart part) && Centrifuge.Is(part))
                    __result = itemId == ModItems.Uranium;
            }
            catch (System.Exception e)
            {
                ErrorLog.Report("centrifuge slot", e);
            }
        }
    }

    /// <summary>
    /// Item lines (SDModKit) only know the state type: they may bring uranium ore to any distiller.
    /// A plain distiller just keeps it (it is not biomass).
    /// </summary>
    [HarmonyPatch]
    internal static class CentrifugeItemLinePatch
    {
        private static bool Prepare() => AccessTools.TypeByName("SDModKit.Game.ItemMove") != null;

        private static System.Reflection.MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("SDModKit.Game.ItemMove"), "Accepts");

        private static void Postfix(object storage, uint itemId, ref bool __result)
        {
            if (!__result && storage is EthanolDistillerState && itemId == ModItems.Uranium)
                __result = true;
        }
    }
}

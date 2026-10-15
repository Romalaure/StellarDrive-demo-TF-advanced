using System.Collections.Generic;
using System.Reflection;
using Core.Services;
using HarmonyLib;
using Items.Interface.Service;
using Items.Model;
using Research;
using SDModKit.Game;
using Ships;
using Ships.Cables.Fluids;
using Ships.Interface.Model;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Interface.Settings;
using Ships.Parts.WoodPowerGenerator;
using Ships.Physics;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using StellarDriveDemoTFAdvanced.TFItems.Lights;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.TFItems.Infinite
{
    /// <summary>
    /// Creative parts that never run out, cloned from the game's own parts so every game system
    /// (power and fluid networks, chests, saving, sync) handles them unchanged:
    /// a generator that needs no wood, oxygen and ethanol tanks that stay full, and a resource
    /// chest that refills itself. Small server-side patches keep them infinite.
    /// Only call Register when SDModKit is loaded.
    /// </summary>
    internal static class InfiniteParts
    {

        public const ushort Generator = 7121;
        public const ushort OxygenTank = 7122;
        public const ushort EthanolTank = 7123;
        public const ushort ResourceChest = 7124;

        /// <summary>Power of the infinite generator, in times a wood-fired generator (4 kW).</summary>
        public const float GeneratorMultiplier = 5f;

        private const uint Iron = 101, Glass = 102, Copper = 112;

        /// <summary>What each slot of the resource chest holds; the last slot is a bin that deletes what goes in.</summary>
        public static readonly uint[] ChestItems = { 101, 102, 112, 104, 110, 109, 100, 105, 103 };
        public const byte ChestStack = 20;

        /// <summary>Mass of every infinite part; what they hold is not counted (see InfiniteMassPatch).</summary>
        public const float Mass = 0.01f;

        private static readonly HashSet<ushort> Ids = new HashSet<ushort> { Generator, OxygenTank, EthanolTank, ResourceChest };

        public static bool IsInfinite(ushort partId) => Ids.Contains(partId);

        public static uint TankFluid(ushort partId) =>
            partId == OxygenTank ? ItemsConstants.OxygenItemId : partId == EthanolTank ? ItemsConstants.EthanolItemId : 0u;

        public static void Register()
        {
            Add(Generator, "WoodPowerGenerator", "Générateur infini",
                $"Générateur électrique qui tourne sans bois, pour toujours : {4 * GeneratorMultiplier:0} kW en continu sur son réseau électrique.",
                HardCost(Iron, 200, Copper, 150), new Color(0.55f, 0.85f, 1f));
            Add(OxygenTank, "FluidTank", "Réservoir d'oxygène infini",
                "Réservoir toujours plein d'oxygène : il alimente sans fin les tuyaux branchés (propulseurs, autres réservoirs...).",
                HardCost(Iron, 150, Glass, 120), new Color(0.55f, 0.8f, 1f));
            Add(EthanolTank, "FluidTank", "Réservoir d'éthanol infini",
                "Réservoir toujours plein d'éthanol : il alimente sans fin les tuyaux branchés (propulseurs, autres réservoirs...).",
                HardCost(Iron, 150, Glass, 120), new Color(1f, 0.75f, 0.4f));
            Add(ResourceChest, "StorageChest", "Coffre de ressources infini",
                "Coffre qui se remplit tout seul : fer, verre, cuivre, aluminium, bois, glace, biocarburant, eau et éclats stellaires à volonté. La dernière case est une poubelle : ce que tu y poses disparaît.",
                HardCost(Iron, 300, Copper, 200), new Color(1f, 0.85f, 0.35f));

            TFMod.Log.Msg($"registered {Ids.Count} infinite parts");
        }

        /// <summary>
        /// Infinite parts cost a fortune: game materials by the hundred and the mod's rarest finds
        /// (quantum cores, exotic matter, artifacts, dark matter is not an item), so they come late.
        /// </summary>
        private static (uint, int)[] HardCost(uint a, int na, uint b, int nb) => new[]
        {
            (a, na), (b, nb),
            (StellarDriveDemoTFAdvanced.Items.ModItems.QuantumCore, 8),
            (StellarDriveDemoTFAdvanced.Items.ModItems.ExoticMatter, 10),
            (StellarDriveDemoTFAdvanced.Items.ModItems.Artifact, 6),
            (StellarDriveDemoTFAdvanced.Items.ModItems.NexoranAlloy, 40),
            (StellarDriveDemoTFAdvanced.Items.ModItems.Meteorite, 30),
        };

        private static void Add(ushort id, string donor, string label, string description, (uint, int)[] cost, Color tint)
        {
            CustomParts.Register(new CustomPartDefinition
            {
                Id = id,
                Name = "TF_" + id,
                Donor = donor,
                BuildTab = TFTab.ObjectsName,
                BuildRow = TFTab.InfiniteRow,
                Configure = (settings, prefab) =>
                {
                    settings.fullLabel = label;
                    settings.description = description;
                    settings.localizedDescription = null;
                    LampParts.SetCost(settings, cost);
                    settings.mass = Mass;
                    Tint(prefab, tint);
                }
            });
        }

        // Tinted copies of the donor's materials, so the infinite version is easy to tell apart
        private static void Tint(GameObject prefab, Color tint)
        {
            int colorId = Shader.PropertyToID("_Color");
            int baseColorId = Shader.PropertyToID("_BaseColor");
            var copies = new Dictionary<Material, Material>();
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material original = materials[i];
                    if (original == null || original.renderQueue >= 2450)
                        continue;
                    if (!copies.TryGetValue(original, out Material copy))
                    {
                        copy = new Material(original) { name = original.name + "_TFInfinite" };
                        if (copy.HasProperty(colorId))
                            copy.SetColor(colorId, copy.GetColor(colorId) * tint);
                        if (copy.HasProperty(baseColorId))
                            copy.SetColor(baseColorId, copy.GetColor(baseColorId) * tint);
                        copies[original] = copy;
                    }
                    materials[i] = copy;
                }
                renderer.sharedMaterials = materials;
            }
        }

        public static InventorySlot ChestSlot(byte slotId) =>
            slotId < ChestItems.Length
                ? new InventorySlot { HasItem = true, ItemId = ChestItems[slotId], Quantity = StackOf(ChestItems[slotId]) }
                : new InventorySlot { HasItem = false };

        private static IItemSettingsProvider _items;

        // A full stack of the item, as large as the game (or a stack size mod) allows
        private static byte StackOf(uint itemId)
        {
            try
            {
                if (_items == null)
                    _items = ServiceLocator.GetService<IItemSettingsProvider>();
                ItemSettings item = _items?.GetItemSettingsById(itemId);
                if (item != null && item.maxStackSize > 0)
                    return item.maxStackSize;
            }
            catch (System.Exception)
            {
                // items not loaded yet
            }
            return ChestStack;
        }

        /// <summary>The chest's state with every resource slot full and the bin empty.</summary>
        public static IStatefulPartState Refilled(IStatefulPartState state)
        {
            if (!(state is IStoragePart storage))
                return state;
            for (byte slot = 0; slot < storage.SlotCount; slot++)
            {
                InventorySlot wanted = ChestSlot(slot);
                InventorySlot current = storage.GetSlot(slot);
                if (current.HasItem != wanted.HasItem || current.ItemId != wanted.ItemId || current.Quantity != wanted.Quantity)
                    storage = storage.WithUpdatedSlot(slot, wanted);
            }
            return (IStatefulPartState)storage;
        }

        public static bool IsResourceChest(StatefulPart part) => part?.Settings != null && part.Settings.id == ResourceChest;
    }

    /// <summary>New infinite parts start full: tanks with their fluid, the chest with its resources.</summary>
    [HarmonyPatch(typeof(PartStateFactory), nameof(PartStateFactory.BuildDefaultState))]
    internal static class InfiniteDefaultStatePatch
    {
        private static void Postfix(PartSettings partSettings, ref IStatefulPartState __result)
        {
            if (partSettings == null || !InfiniteParts.IsInfinite(partSettings.id))
                return;
            uint fluid = InfiniteParts.TankFluid(partSettings.id);
            if (fluid != 0 && __result is FluidTankState tank)
            {
                __result = (IStatefulPartState)tank.WithFluid(fluid, FluidTankState.Capacity, 0f);
            }
            else if (partSettings.id == InfiniteParts.ResourceChest)
            {
                __result = InfiniteParts.Refilled(__result);
            }
        }
    }

    /// <summary>
    /// The ship counts the items in a chest and the fluid in a tank as mass: full stacks of iron
    /// and full tanks made a ship carrying infinite parts very heavy. Their contents weigh nothing.
    /// </summary>
    [HarmonyPatch(typeof(StatefulPartsMassTracker), "EnsureMassChangeIsAccounted")]
    internal static class InfiniteMassPatch
    {
        private static void Prefix(StatefulPart part, ref IStatefulPartState state)
        {
            // Only the mass sum reads this state: without it the part weighs its settings' mass
            if (part?.Settings != null && InfiniteParts.IsInfinite(part.Settings.id))
                state = null;
        }
    }

    /// <summary>The infinite generator always runs at full power, with no wood.</summary>
    [HarmonyPatch(typeof(PowerGeneratorUpdaters), "CalculateProductionCapacity")]
    internal static class InfiniteGeneratorPatch
    {
        private static bool Prefix(float deltaTime, StatefulPart part, ref float __result)
        {
            if (part?.Settings == null || part.Settings.id != InfiniteParts.Generator)
                return true;
            __result = deltaTime * WoodPowerGeneratorState.PowerProductionRate * InfiniteParts.GeneratorMultiplier;
            return false;
        }
    }

    /// <summary>
    /// After each fluid network step, an infinite tank is put back to full with its own fluid, so
    /// whatever the network drew from it is created out of nothing.
    /// </summary>
    [HarmonyPatch]
    internal static class InfiniteTankPatch
    {
        private static readonly System.Type Container = AccessTools.Inner(typeof(FluidGroupServerState), "PartFluidContainer");
        private static readonly FieldInfo PartField = AccessTools.Field(Container, "_part");
        private static readonly FieldInfo FluidField = AccessTools.Field(Container, "_fluidItemId");
        private static readonly FieldInfo QuantityField = AccessTools.Field(Container, "_fluidQuantity");

        private static MethodBase TargetMethod() => AccessTools.Method(Container, "CommitChanges");

        private static void Prefix(object __instance)
        {
            var part = PartField.GetValue(__instance) as StatefulPart;
            if (part?.Settings == null)
                return;
            uint fluid = InfiniteParts.TankFluid(part.Settings.id);
            if (fluid == 0)
                return;
            FluidField.SetValue(__instance, fluid);
            QuantityField.SetValue(__instance, FluidTankState.Capacity);
        }
    }

    /// <summary>
    /// Taking items out of the resource chest leaves its slots full; the last slot is a bin.
    /// Runs on the server, where chest contents are decided.
    /// </summary>
    [HarmonyPatch(typeof(TrackedShipServer), nameof(TrackedShipServer.SetChestSlot))]
    internal static class InfiniteChestPatch
    {
        private static void Prefix(TrackedShipServer __instance, ushort partId, byte slotId, ref InventorySlot slotData)
        {
            if (__instance.TryGetStatefulPart(partId, out StatefulPart part) && InfiniteParts.IsResourceChest(part))
                slotData = InfiniteParts.ChestSlot(slotId);
        }
    }

    /// <summary>
    /// Whatever writes the resource chest's state (the inventory, other mods moving items, a save
    /// from an older version), it is stored full. Runs wherever ship states change.
    /// </summary>
    [HarmonyPatch(typeof(ShipState), nameof(ShipState.SetPartState))]
    internal static class InfiniteChestStatePatch
    {
        private static void Prefix(ShipState __instance, ushort id, ref IStatefulPartState state)
        {
            if (state is IStoragePart && __instance.TryGetPart(id, out StatefulPart part) && InfiniteParts.IsResourceChest(part))
                state = InfiniteParts.Refilled(state);
        }
    }

    /// <summary>Reading a resource chest slot on the server always finds it full (or the bin empty).</summary>
    [HarmonyPatch(typeof(TrackedShipServer), nameof(TrackedShipServer.TryGetChestSlot))]
    internal static class InfiniteChestReadServerPatch
    {
        private static void Postfix(TrackedShipServer __instance, ushort partId, byte slotId, ref InventorySlot slot, bool __result)
        {
            if (__result && __instance.TryGetStatefulPart(partId, out StatefulPart part) && InfiniteParts.IsResourceChest(part))
                slot = InfiniteParts.ChestSlot(slotId);
        }
    }

    /// <summary>The chest window shows the resource chest full, even before the server's update arrives.</summary>
    [HarmonyPatch(typeof(TrackedShipClient), nameof(TrackedShipClient.TryGetChestSlot))]
    internal static class InfiniteChestReadClientPatch
    {
        private static void Postfix(TrackedShipClient __instance, ushort partId, byte slotId, ref InventorySlot slot, bool __result)
        {
            if (__result && __instance.State.TryGetPart(partId, out StatefulPart part) && InfiniteParts.IsResourceChest(part))
                slot = InfiniteParts.ChestSlot(slotId);
        }
    }

    /// <summary>Infinite parts are in no research node, so the build menu would keep them locked forever.</summary>
    [HarmonyPatch(typeof(ResearchClientProxy), nameof(ResearchClientProxy.IsPartUnlocked))]
    internal static class InfiniteUnlockPatch
    {
        private static void Postfix(PartSettings part, ref bool __result)
        {
            if (!__result && part != null && InfiniteParts.IsInfinite(part.id))
                __result = true;
        }
    }
}

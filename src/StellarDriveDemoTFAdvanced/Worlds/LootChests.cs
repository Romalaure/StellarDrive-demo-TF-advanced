using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Ships;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Interface.Settings;
using Ships.Services;
using StellarDriveDemoTFAdvanced.Common;
using StellarDriveDemoTFAdvanced.Parts;
using UnityEngine;
using UnityEngine.Rendering;

namespace StellarDriveDemoTFAdvanced.Worlds
{
    /// <summary>
    /// The mod's loot chests (ids 7475-7479, common to legendary): the game's storage chest
    /// (its storage and saving) wearing a detailed chest modelled in Blender
    /// (tools/blender/chest.py): textured planks, hammered iron bands and rivets, a lock and a
    /// glow in the rarity's color. Not in the build menu: the ruins and stations place them.
    /// Like the game's loot cases they go once emptied (the host removes them when the last item
    /// is taken and nobody has them open), and take no cable (no item ports from Item Lines).
    /// </summary>
    internal static class LootChests
    {
        public const ushort FirstId = 7475;
        public const int Rarities = 5;
        // The game's loot cases are not loaded when SDModKit builds the parts: it then never finds
        // them and searches again on every part lookup (the game froze). The storage chest always is.
        private const string Donor = "StorageChest";

        private static readonly Color[] RarityColors =
        {
            new Color(0.62f, 0.63f, 0.66f), new Color(0.3f, 0.78f, 0.35f), new Color(0.25f, 0.52f, 1f), new Color(0.65f, 0.3f, 0.95f), new Color(1f, 0.76f, 0.22f)
        };

        public static bool IsLootChest(ushort id) => id >= FirstId && id < FirstId + Rarities;

        // Host: emptied chests, removed once nobody has them open
        private static readonly HashSet<(uint Ship, ushort Part)> Emptied = new HashSet<(uint, ushort)>();

        /// <summary>Host: a slot of a chest changed; an emptied loot chest goes.</summary>
        internal static void SlotChanged(TrackedShipServer ship, ushort partId)
        {
            if (ship.TryGetStatefulPart(partId, out StatefulPart part) && part.Settings != null && IsLootChest(part.Settings.id)
                && part.State is IStoragePart storage && Enumerable.Range(0, storage.SlotCount).All(i => !storage.GetSlot((byte)i).HasItem))
                Emptied.Add((ship.Id, partId));
        }

        /// <summary>Host, every frame in game.</summary>
        public static void Update()
        {
            if (Emptied.Count == 0)
                return;
            ShipsServerTracker ships = GameServices.ShipsServer;
            foreach ((uint shipId, ushort partId) in Emptied.ToList())
            {
                TrackedShipServer ship = ships?.GetShip(shipId);
                if (ship == null || !ship.TryGetStatefulPart(partId, out StatefulPart part) || !(part.State is IStoragePart storage)
                    || Enumerable.Range(0, storage.SlotCount).Any(i => storage.GetSlot((byte)i).HasItem))
                {
                    Emptied.Remove((shipId, partId));
                    continue;
                }
                // Still open in someone's chest window: wait
                if (part.State is IInteractivePart interactive && interactive.IsInUse)
                    continue;
                Emptied.Remove((shipId, partId));
                // A chest lying on its own is a one-part ship (the ruins' caches): the ship goes
                if (ship.StatefulParts.Count() <= 1 && ship.HullPartsAndPlayersMass < 1f)
                    ships.RemoveShip(ship.State);
                else
                    ship.RemoveStatefulPart(partId);
                AdvancedMod.Log.Msg($"loot chests: emptied chest {partId} of ship {shipId} gone");
            }
        }

        public static void Register()
        {
            for (int rarity = 0; rarity < Rarities; rarity++)
            {
                int r = rarity;
                PartFactory.Register((ushort)(FirstId + r), Donor, "", 0, (settings, prefab) => Configure(r, settings, prefab));
            }
            AdvancedMod.Log.Msg("registered the loot chests");
        }

        private static void Configure(int rarity, PartSettings settings, GameObject prefab)
        {
            string[] fr = { "commun", "peu commun", "rare", "épique", "légendaire" }, en = { "common", "uncommon", "rare", "epic", "legendary" }, es = { "común", "poco común", "raro", "épico", "legendario" };
            PartFactory.Describe(settings, Loc.Pick("Coffre " + fr[rarity], "Chest (" + en[rarity] + ")", "Cofre " + es[rarity]),
                Loc.Pick("Un vieux coffre trouvé dans les ruines et les stations : sa couleur dit sa rareté.", "An old chest found in ruins and stations: its color tells its rarity.", "Un viejo cofre hallado en ruinas y estaciones: su color indica su rareza."),
                30f, new[] { (110u, 6), (101u, 4) });
            Dictionary<string, Mesh> model = ObjModels.Load("chest");
            var visual = prefab.GetComponentInChildren<Ships.Parts.Common.Model.Reactions.IShipPartVisual>(true)?.GameObject.transform;
            if (model == null || visual == null || !DonorBounds(visual, out Bounds donor))
            {
                AdvancedMod.Log.Warning($"loot chest {rarity}: no model ({model != null}, {visual != null})");
                return;
            }
            Power.NuclearParts.HideDonorModel(visual);
            // Fitted on the game chest's footprint, standing on its floor
            float scale = Mathf.Clamp(Mathf.Min(donor.size.x / 0.84f, donor.size.z / 0.62f), 0.7f, 1.4f);
            if (rarity == 0)
                AdvancedMod.Log.Msg($"loot chests: donor box {donor.center} {donor.size}, chest scale {scale:0.00}");
            Color color = RarityColors[rarity];
            var looks = new Dictionary<string, Material>
            {
                ["TF_ChestWood"] = NaturalTextures.Lit("TFA_ChestWood", NaturalTextures.Wood, NaturalTextures.WoodNormal, Color.white, 0f, 0.2f, 1f),
                ["TF_ChestIron"] = NaturalTextures.Lit("TFA_ChestIron", NaturalTextures.Iron, NaturalTextures.IronNormal, new Color(0.5f, 0.5f, 0.53f), 0.8f, 0.4f, 2f),
                ["TF_ChestTrim"] = NaturalTextures.Lit("TFA_ChestTrim" + rarity, NaturalTextures.Engraved, NaturalTextures.EngravedNormal, color, 0.9f, 0.7f, 2f),
                ["TF_ChestGlow"] = LitMaterials.Get("TFA_ChestGlow" + rarity, color, 0f, 0.1f, color * (1.2f + 0.4f * rarity)),
                ["TF_ChestInside"] = LitMaterials.Get("TFA_ChestInside", new Color(0.05f, 0.04f, 0.035f), 0f, 0.1f)
            };
            var holder = new GameObject("TF_ChestModel") { layer = visual.gameObject.layer };
            holder.transform.SetParent(visual, false);
            holder.transform.localPosition = new Vector3(donor.center.x, donor.min.y, donor.center.z);
            holder.transform.localScale = Vector3.one * scale;
            foreach (KeyValuePair<string, Mesh> group in model)
            {
                if (!looks.TryGetValue(group.Key, out Material look) || look == null)
                    continue;
                var child = new GameObject("TF_Chest" + group.Key) { layer = holder.layer };
                child.transform.SetParent(holder.transform, false);
                child.AddComponent<MeshFilter>().sharedMesh = group.Value;
                var renderer = child.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = look;
                renderer.shadowCastingMode = group.Key == "TF_ChestWood" || group.Key == "TF_ChestIron" ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
            // The legendary and epic ones light what is near them
            if (rarity >= 3)
            {
                var light = new GameObject("TF_ChestLight") { layer = holder.layer };
                light.transform.SetParent(holder.transform, false);
                light.transform.localPosition = new Vector3(0f, 0.6f, 0f);
                Light glow = light.AddComponent<Light>();
                glow.type = LightType.Point;
                glow.color = color;
                glow.range = 2.5f;
                glow.intensity = 0.8f;
                glow.shadows = LightShadows.None;
            }
        }

        // The game chest's model, in the visual's frame
        private static bool DonorBounds(Transform visual, out Bounds bounds)
        {
            bounds = new Bounds();
            bool any = false;
            foreach (MeshRenderer renderer in visual.GetComponentsInChildren<MeshRenderer>(true))
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null || renderer.name.StartsWith("TF"))
                    continue;
                Bounds mesh = filter.sharedMesh.bounds;
                for (int k = 0; k < 8; k++)
                {
                    Vector3 corner = mesh.center + Vector3.Scale(mesh.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                    Vector3 p = visual.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                    if (!any)
                    {
                        bounds = new Bounds(p, Vector3.zero);
                        any = true;
                    }
                    else
                    {
                        bounds.Encapsulate(p);
                    }
                }
            }
            return any;
        }
    }

    [HarmonyPatch(typeof(TrackedShipServer), nameof(TrackedShipServer.SetChestSlot))]
    internal static class LootChestEmptiedPatch
    {
        private static void Postfix(TrackedShipServer __instance, ushort partId)
        {
            try
            {
                LootChests.SlotChanged(__instance, partId);
            }
            catch (Exception e)
            {
                ErrorLog.Report("loot chest emptied", e);
            }
        }
    }

    /// <summary>
    /// SDModKit's "attach" rules (Item Lines' item ports) graft two sockets on every part with an
    /// inventory: the loot chests are left out (their own names in the rule's exclusions).
    /// </summary>
    [HarmonyPatch]
    internal static class LootChestNoPortsPatch
    {
        private static MethodBase TargetMethod() => AccessTools.GetDeclaredMethods(typeof(SDModKit.Content.ContentAttach))
            .FirstOrDefault(m => m.IsStatic && m.ReturnType == typeof(void) && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(SDModKit.Content.ContentAttachDef));

        private static void Prefix(SDModKit.Content.ContentAttachDef __0)
        {
            for (int rarity = 0; rarity < LootChests.Rarities; rarity++)
            {
                string name = "TFA_" + (LootChests.FirstId + rarity);
                if (!__0.Exclude.Contains(name))
                    __0.Exclude.Add(name);
            }
        }
    }
}

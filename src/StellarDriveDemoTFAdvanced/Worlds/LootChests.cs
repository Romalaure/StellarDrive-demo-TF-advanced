using System.Collections.Generic;
using Ships.Interface.Settings;
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
}

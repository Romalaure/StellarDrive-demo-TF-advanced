using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Items.Model;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StellarDriveDemoTFAdvanced.Items
{
    internal sealed class ModItem
    {
        public uint Id;
        public string Name;
        public float Mass = 2f;
        /// <summary>The game item whose material (texture, shader) the model wears, tinted.</summary>
        public uint MaterialFrom;
        public Color Tint = Color.white;
        public Color Glow = Color.black;
        public Action<MeshBuilder> Model;
        /// <summary>A fluid, carried in tanks and pipes (no model, like the game's fluids).</summary>
        public bool Fluid;
        public Color FluidColor = Color.black;
        public ItemSettings Settings;
    }

    /// <summary>
    /// The mod's items: new ores found on the mod planets and rare finds from the ruins, the
    /// abandoned city and the stations, used to build the advanced parts. Each is a real game
    /// item (ItemSettings) added to the game's item list: it stacks, weighs, shows in inventories
    /// and chests, drops in the world and is saved like the game's own. Its 3D model is built by
    /// the mod and wears a game item's material (texture and shader), tinted; the game renders
    /// its inventory icon from that model.
    /// </summary>
    internal static class ModItems
    {
        public const uint Titanium = 7401;
        public const uint Quartz = 7402;
        public const uint Cryonite = 7403;
        public const uint Amber = 7404;
        public const uint Artifact = 7410;
        public const uint QuantumCore = 7411;
        public const uint ExoticMatter = 7412;
        public const uint NexoranAlloy = 7413;
        public const uint Meteorite = 7414;
        public const uint DarkMatter = 7415;
        public const uint Uranium = 7416;
        public const uint NuclearFuel = 7417;

        // Game items
        public const uint Iron = 101, Glass = 102, StellarShard = 103, Aluminum = 104, Wood = 110, Copper = 112, GoldBar = 158, Key = 151;

        public static readonly ModItem[] All =
        {
            new ModItem { Id = Titanium, Name = Loc.Pick("Minerai de titane", "Titanium ore", "Mineral de titanio"), Mass = 2.5f, MaterialFrom = Iron, Tint = new Color(0.72f, 0.8f, 0.95f), Model = OreChunk },
            new ModItem { Id = Quartz, Name = Loc.Pick("Cristal de quartz", "Quartz crystal", "Cristal de cuarzo"), Mass = 1.5f, MaterialFrom = Glass, Tint = new Color(1f, 0.68f, 0.86f), Model = m => Crystals(m, 5, 0.3f) },
            new ModItem { Id = Cryonite, Name = "Cryonite", Mass = 1.5f, MaterialFrom = StellarShard, Tint = new Color(0.45f, 0.9f, 1.2f), Glow = new Color(0.1f, 0.5f, 0.8f), Model = m => Crystals(m, 3, 0.38f) },
            new ModItem { Id = Amber, Name = Loc.Pick("Ambre fossile", "Fossil amber", "Ámbar fósil"), Mass = 1f, MaterialFrom = Glass, Tint = new Color(1f, 0.55f, 0.12f), Model = Nugget },
            new ModItem { Id = Artifact, Name = Loc.Pick("Fragment d'artefact ancien", "Ancient artifact fragment", "Fragmento de artefacto antiguo"), Mass = 3f, MaterialFrom = StellarShard, Tint = new Color(1f, 0.82f, 0.45f), Glow = new Color(0.35f, 0.22f, 0.05f), Model = Tablet },
            new ModItem { Id = QuantumCore, Name = Loc.Pick("Noyau quantique", "Quantum core", "Núcleo cuántico"), Mass = 4f, MaterialFrom = StellarShard, Tint = new Color(0.75f, 0.5f, 1.2f), Glow = new Color(0.4f, 0.15f, 0.9f), Model = Core },
            new ModItem { Id = ExoticMatter, Name = Loc.Pick("Matière exotique", "Exotic matter", "Materia exótica"), Mass = 0.5f, MaterialFrom = StellarShard, Tint = new Color(0.25f, 0.12f, 0.4f), Glow = new Color(0.55f, 0.1f, 0.35f), Model = m => Crystals(m, 4, 0.34f) },
            new ModItem { Id = NexoranAlloy, Name = Loc.Pick("Alliage nexorien", "Nexoran alloy", "Aleación nexoriana"), Mass = 3f, MaterialFrom = Iron, Tint = new Color(0.55f, 0.85f, 0.9f), Model = Ingot },
            new ModItem { Id = Meteorite, Name = Loc.Pick("Météorite", "Meteorite", "Meteorito"), Mass = 4f, MaterialFrom = Iron, Tint = new Color(0.32f, 0.27f, 0.25f), Glow = new Color(0.55f, 0.18f, 0.02f), Model = MeteoriteChunk },
            new ModItem { Id = DarkMatter, Name = Loc.Pick("Matière noire", "Dark matter", "Materia oscura"), Mass = 0.2f, Fluid = true, FluidColor = new Color(0.22f, 0.05f, 0.35f) },
            new ModItem { Id = Uranium, Name = Loc.Pick("Minerai d'uranium", "Uranium ore", "Mineral de uranio"), Mass = 5f, MaterialFrom = Iron, Tint = new Color(0.42f, 0.55f, 0.3f), Glow = new Color(0.12f, 0.45f, 0.04f), Model = OreChunk },
            new ModItem { Id = NuclearFuel, Name = Loc.Pick("Combustible nucléaire", "Nuclear fuel", "Combustible nuclear"), Mass = 1f, Fluid = true, FluidColor = new Color(0.35f, 0.95f, 0.2f) }
        };

        private static readonly AccessTools.FieldRef<ItemSettingsList, ItemSettings[]> Items = AccessTools.FieldRefAccess<ItemSettingsList, ItemSettings[]>("items");
        private static readonly AccessTools.FieldRef<ItemSettingsList, Dictionary<uint, ItemSettings>> Dict = AccessTools.FieldRefAccess<ItemSettingsList, Dictionary<uint, ItemSettings>>("_dict");
        private static readonly HashSet<ItemSettingsList> Merged = new HashSet<ItemSettingsList>();
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static GameObject _holder;

        public static ModItem Get(uint id) => All.FirstOrDefault(i => i.Id == id);

        public static ItemSettings Settings(uint id) => Get(id)?.Settings;

        /// <summary>Creates the item settings (at start, before any game list is read).</summary>
        public static void Create()
        {
            foreach (ModItem item in All)
            {
                ItemSettings settings = ScriptableObject.CreateInstance<ItemSettings>();
                settings.name = "TFA_Item" + item.Id;
                settings.hideFlags = HideFlags.DontUnloadUnusedAsset;
                settings.id = item.Id;
                settings.itemName = item.Name;
                settings.mass = item.Mass;
                settings.maxStackSize = 255;
                settings.isFluid = item.Fluid;
                settings.fluidColor = item.FluidColor;
                item.Settings = settings;
            }
        }

        /// <summary>Adds the items to a game item list, once, and builds their models when the game's materials are there.</summary>
        public static void Merge(ItemSettingsList list)
        {
            if (list == null || All[0].Settings == null)
                return;
            BuildModels(list);
            if (Merged.Contains(list))
                return;
            ItemSettings[] items = Items(list);
            if (items == null)
                return;
            var merged = items.ToList();
            foreach (ModItem item in All)
            {
                ItemSettings clash = merged.FirstOrDefault(i => i != null && i.id == item.Id && i != item.Settings);
                if (clash != null)
                {
                    AdvancedMod.Log.Error($"items: id {item.Id} is already used by '{clash.itemName}', {item.Name} not added");
                    continue;
                }
                if (!merged.Contains(item.Settings))
                    merged.Add(item.Settings);
            }
            Items(list) = merged.ToArray();
            Dict(list) = null;
            Merged.Add(list);
            AdvancedMod.Log.Msg($"items: {All.Length} items added ({merged.Count} in the game's list)");
        }

        private static void BuildModels(ItemSettingsList list)
        {
            if (All.All(i => i.Fluid || i.Settings.itemObject != null))
                return;
            ItemSettings[] items = Items(list);
            if (items == null)
                return;
            if (_holder == null)
            {
                _holder = new GameObject("TFA_ItemModels");
                _holder.SetActive(false);
                Object.DontDestroyOnLoad(_holder);
            }
            foreach (ModItem item in All.Where(i => !i.Fluid && i.Settings.itemObject == null))
            {
                ItemSettings donor = items.FirstOrDefault(i => i != null && i.id == item.MaterialFrom);
                MeshRenderer donorRenderer = donor?.itemObject?.GetComponentInChildren<MeshRenderer>(true);
                Material material = donorRenderer?.sharedMaterial;
                if (material == null)
                    continue;
                var root = new GameObject(item.Settings.name);
                root.transform.SetParent(_holder.transform, false);
                var visual = new GameObject("Visuals");
                visual.transform.SetParent(root.transform, false);
                // The donor item's own area of its texture atlas
                var builder = new MeshBuilder { Panel = MeshBuilder.UvArea(donorRenderer.GetComponent<MeshFilter>()?.sharedMesh) };
                item.Model(builder);
                // The game's object shader reads its palette color from UV1, like the donor's model
                Mesh donorMesh = donorRenderer.GetComponent<MeshFilter>()?.sharedMesh;
                visual.AddComponent<MeshFilter>().sharedMesh = builder.Build(item.Settings.name, Parts.GameSkin.MostUsedUv1(donorMesh));
                visual.AddComponent<MeshRenderer>().sharedMaterial = Tinted(material, item);
                item.Settings.itemObject = root;
            }
        }

        private static Material Tinted(Material source, ModItem item)
        {
            var material = new Material(source) { name = "TFA_Item" + item.Id };
            if (material.HasProperty(ColorId))
                material.SetColor(ColorId, material.GetColor(ColorId) * item.Tint);
            if (item.Glow != Color.black && material.HasProperty(EmissionId))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor(EmissionId, item.Glow);
            }
            return material;
        }

        // ---- Models (meters, about the size of the game's ore items: 0.3 m)

        private static void OreChunk(MeshBuilder m)
        {
            Slab(m, new[]
            {
                new Vector3(-0.15f, 0f, -0.12f), new Vector3(0.14f, 0f, -0.15f), new Vector3(-0.12f, 0f, 0.15f), new Vector3(0.16f, 0f, 0.11f),
                new Vector3(-0.1f, 0.17f, -0.08f), new Vector3(0.09f, 0.2f, -0.11f), new Vector3(-0.08f, 0.15f, 0.1f), new Vector3(0.11f, 0.19f, 0.07f)
            });
            m.Box(new Vector3(0.06f, 0.2f, 0.02f), new Vector3(0.11f, 0.08f, 0.09f), Quaternion.Euler(18f, 30f, 12f));
            m.Box(new Vector3(-0.09f, 0.08f, 0.1f), new Vector3(0.09f, 0.1f, 0.08f), Quaternion.Euler(-10f, 50f, 20f));
        }

        // Corners listed bottom (-x-z, +x-z, -x+z, +x+z) then top, in MeshBuilder.Block's bit order
        private static void Slab(MeshBuilder m, Vector3[] c)
        {
            m.Block(new[] { c[0], c[1], c[4], c[5], c[2], c[3], c[6], c[7] });
        }

        private static void Crystals(MeshBuilder m, int count, float height)
        {
            m.Cylinder(new Vector3(0f, 0f, 0f), new Vector3(0f, 0.05f, 0f), 0.14f, 0.12f, 7);
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2f / count + 0.4f;
                float h = height * (i == 0 ? 1f : 0.55f + 0.1f * (i % 3));
                Vector3 baseCenter = i == 0 ? new Vector3(0f, 0.03f, 0f) : new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.07f + new Vector3(0f, 0.03f, 0f);
                Vector3 direction = i == 0 ? Vector3.up : (Vector3.up * 1.6f + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a))).normalized;
                float r = i == 0 ? 0.055f : 0.035f;
                m.Cylinder(baseCenter, baseCenter + direction * h * 0.8f, r, r, 6, capA: true, capB: false);
                m.Cylinder(baseCenter + direction * h * 0.8f, baseCenter + direction * h, r, 0f, 6, capA: false, capB: false);
            }
        }

        private static void Nugget(MeshBuilder m)
        {
            m.Cylinder(new Vector3(0f, 0.02f, 0f), new Vector3(0.02f, 0.14f, 0.01f), 0.11f, 0.07f, 8);
            m.Cylinder(new Vector3(0.02f, 0.14f, 0.01f), new Vector3(0.02f, 0.18f, 0.01f), 0.07f, 0f, 8, capA: false, capB: false);
        }

        private static void Tablet(MeshBuilder m)
        {
            // A carved stone plaque, broken off on one corner, with raised glyphs
            Slab(m, new[]
            {
                new Vector3(-0.17f, 0f, -0.12f), new Vector3(0.17f, 0f, -0.12f), new Vector3(-0.17f, 0f, 0.12f), new Vector3(0.08f, 0f, 0.12f),
                new Vector3(-0.17f, 0.05f, -0.12f), new Vector3(0.17f, 0.05f, -0.12f), new Vector3(-0.17f, 0.05f, 0.12f), new Vector3(0.08f, 0.05f, 0.12f)
            });
            for (int i = 0; i < 3; i++)
            {
                m.Box(new Vector3(-0.1f + i * 0.08f, 0.06f, -0.05f), new Vector3(0.05f, 0.02f, 0.012f));
                m.Box(new Vector3(-0.1f + i * 0.08f, 0.06f, 0.03f), new Vector3(0.012f, 0.02f, 0.06f));
            }
            m.Cylinder(new Vector3(0.1f, 0.05f, -0.05f), new Vector3(0.1f, 0.07f, -0.05f), 0.03f, 0.03f, 8);
        }

        private static void Core(MeshBuilder m)
        {
            // A glowing core held in a ring cage
            m.Cylinder(new Vector3(0f, 0.04f, 0f), new Vector3(0f, 0.12f, 0f), 0.06f, 0.09f, 10, capA: true, capB: false);
            m.Cylinder(new Vector3(0f, 0.12f, 0f), new Vector3(0f, 0.2f, 0f), 0.09f, 0.06f, 10, capA: false, capB: true);
            m.Cylinder(new Vector3(0f, 0f, 0f), new Vector3(0f, 0.035f, 0f), 0.11f, 0.11f, 10);
            m.Cylinder(new Vector3(0f, 0.205f, 0f), new Vector3(0f, 0.24f, 0f), 0.11f, 0.11f, 10);
            foreach (float a in new[] { 0f, 120f, 240f })
            {
                Vector3 p = Quaternion.Euler(0f, a, 0f) * new Vector3(0.1f, 0f, 0f);
                m.Box(p + new Vector3(0f, 0.12f, 0f), new Vector3(0.018f, 0.2f, 0.018f));
            }
        }

        // A pitted, rounded lump of iron-nickel with fusion-crust facets
        private static void MeteoriteChunk(MeshBuilder m)
        {
            m.Cylinder(new Vector3(0f, 0f, 0f), new Vector3(0.01f, 0.1f, 0f), 0.12f, 0.16f, 7);
            m.Cylinder(new Vector3(0.01f, 0.1f, 0f), new Vector3(-0.01f, 0.2f, 0.02f), 0.16f, 0.09f, 7);
            m.Box(new Vector3(0.09f, 0.14f, 0.05f), new Vector3(0.1f, 0.09f, 0.1f), Quaternion.Euler(25f, 40f, 15f));
            m.Box(new Vector3(-0.1f, 0.07f, -0.06f), new Vector3(0.09f, 0.08f, 0.09f), Quaternion.Euler(-15f, 20f, 35f));
        }

        private static void Ingot(MeshBuilder m)
        {
            Slab(m, new[]
            {
                new Vector3(-0.17f, 0f, -0.07f), new Vector3(0.17f, 0f, -0.07f), new Vector3(-0.17f, 0f, 0.07f), new Vector3(0.17f, 0f, 0.07f),
                new Vector3(-0.14f, 0.08f, -0.045f), new Vector3(0.14f, 0.08f, -0.045f), new Vector3(-0.14f, 0.08f, 0.045f), new Vector3(0.14f, 0.08f, 0.045f)
            });
            m.Box(new Vector3(0f, 0.085f, 0f), new Vector3(0.12f, 0.008f, 0.03f));
        }
    }

    /// <summary>The game's item list gets the mod's items before any lookup.</summary>
    [HarmonyPatch(typeof(ItemSettingsList), nameof(ItemSettingsList.GetItemSettingsById))]
    internal static class ModItemsListPatch
    {
        private static readonly AccessTools.FieldRef<ItemSettingsList, Dictionary<uint, ItemSettings>> Dict =
            AccessTools.FieldRefAccess<ItemSettingsList, Dictionary<uint, ItemSettings>>("_dict");

        private static readonly System.Reflection.MethodInfo ResetDict = AccessTools.Method(typeof(ItemSettingsList), "ResetDict");

        private static void Prefix(ItemSettingsList __instance, uint id)
        {
            try
            {
                ModItems.Merge(__instance);
                Dictionary<uint, ItemSettings> dict = Dict(__instance);
                if (dict == null)
                {
                    // (the game builds it on its first lookup, after this prefix)
                    ResetDict?.Invoke(__instance, null);
                    dict = Dict(__instance);
                }
                if (dict != null && !dict.ContainsKey(id))
                    MissingItems.Stand(__instance, dict, id);
            }
            catch (Exception e)
            {
                ErrorLog.Report("items", e);
            }
        }
    }

    /// <summary>
    /// An item of a mod that was removed (say the TF mod's schematic tablet, 7201) left in an
    /// inventory or a chest: the game throws at every lookup of an unknown id, and the inventory
    /// mass is looked up at every physics step, so the world froze. Such an id gets a stand-in
    /// (a copy of the game's iron, weightless, named after it) in the lookup only: the item stays
    /// in the save as it was and comes back as itself when its mod is installed again.
    /// </summary>
    internal static class MissingItems
    {
        private const uint Template = 101;

        public static void Stand(ItemSettingsList list, Dictionary<uint, ItemSettings> dict, uint id)
        {
            if (!dict.TryGetValue(Template, out ItemSettings template) || template == null)
                return;
            ItemSettings stand = UnityEngine.Object.Instantiate(template);
            stand.name = "TFA_MissingItem_" + id;
            stand.hideFlags = HideFlags.DontUnloadUnusedAsset;
            stand.id = id;
            stand.itemName = Loc.Pick($"Objet inconnu {id} (mod retiré)", $"Unknown item {id} (removed mod)", $"Objeto desconocido {id} (mod eliminado)");
            stand.mass = 0f;
            dict[id] = stand;
            AdvancedMod.Log.Warning($"item {id} belongs to no installed mod (a removed mod's item in an inventory or a chest): a weightless stand-in keeps the game running");
        }
    }
}

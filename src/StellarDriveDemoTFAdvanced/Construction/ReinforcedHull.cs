using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core.Values;
using HarmonyLib;
using Items.Model;
using SDModKit.Game;
using Ships;
using Ships.Grid.Model;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Placement;
using Ships.Interface.Settings;
using Ships.Physics.Collision;
using StellarDriveDemoTFAdvanced.Common;
using StellarDriveDemoTFAdvanced.Parts;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Construction
{
    /// <summary>
    /// Reinforced hull: the game's iron and glass hull pieces (floors, walls, corners, slopes) under
    /// new ids, with the game's own model and hull look, painted a little darker when placed. They
    /// are real hull (the voxel grid, airtight rooms), only tougher: collisions need three times
    /// the impact to break them, Arsenal's weapons do a third of the damage and punch no holes.
    /// They weigh half again as much and cost twice the material plus titanium (iron) or iron
    /// (glass). Only call <see cref="Register"/> when SDModKit is loaded.
    /// </summary>
    internal static class ReinforcedHull
    {
        public const float Toughness = 3f;
        public const float MassFactor = 1.5f;

        // Painted when placed: a shade under the game's default (217) and its glass
        private static readonly Vector3Byte IronColor = new Vector3Byte(150, 152, 158);
        private static readonly Vector3Byte GlassColor = new Vector3Byte(140, 146, 156);

        private sealed class Spec
        {
            public ushort Id;
            public string Donor;
            public string Fr, En, Es;
        }

        private static readonly Spec[] All =
        {
            // Iron, in the game's research order
            new Spec { Id = 7421, Donor = "IronFloor", Fr = "Sol en fer renforcé", En = "Reinforced iron floor", Es = "Suelo de hierro reforzado" },
            new Spec { Id = 7422, Donor = "IronWall", Fr = "Plaque de fer renforcée", En = "Reinforced iron plate", Es = "Placa de hierro reforzada" },
            new Spec { Id = 7423, Donor = "IronWallPivoted", Fr = "Plaque de fer renforcée pivotée", En = "Reinforced pivoted iron plate", Es = "Placa de hierro reforzada girada" },
            new Spec { Id = 7424, Donor = "IronFloorCorner", Fr = "Coin de sol en fer renforcé", En = "Reinforced iron floor corner", Es = "Esquina de suelo de hierro reforzada" },
            new Spec { Id = 7425, Donor = "IronWallCorner", Fr = "Coin en fer renforcé (bas)", En = "Reinforced iron corner (lower)", Es = "Esquina de hierro reforzada (inferior)" },
            new Spec { Id = 7426, Donor = "IronWallCornerInv", Fr = "Coin en fer renforcé (haut)", En = "Reinforced iron corner (upper)", Es = "Esquina de hierro reforzada (superior)" },
            new Spec { Id = 7427, Donor = "IronSlope", Fr = "Pente en fer renforcée", En = "Reinforced iron slope", Es = "Pendiente de hierro reforzada" },
            new Spec { Id = 7428, Donor = "IronSlopeCorner", Fr = "Coin de pente en fer renforcé (bas)", En = "Reinforced iron slope corner (lower)", Es = "Esquina de pendiente de hierro reforzada (inferior)" },
            new Spec { Id = 7429, Donor = "IronSlopeCornerInv", Fr = "Coin de pente en fer renforcé (haut)", En = "Reinforced iron slope corner (upper)", Es = "Esquina de pendiente de hierro reforzada (superior)" },
            // Glass
            new Spec { Id = 7431, Donor = "GlassFloor", Fr = "Sol en verre renforcé", En = "Reinforced glass floor", Es = "Suelo de vidrio reforzado" },
            new Spec { Id = 7432, Donor = "GlassWall", Fr = "Plaque de verre renforcée", En = "Reinforced glass plate", Es = "Placa de vidrio reforzada" },
            new Spec { Id = 7433, Donor = "GlassWallPivoted", Fr = "Plaque de verre renforcée pivotée", En = "Reinforced pivoted glass plate", Es = "Placa de vidrio reforzada girada" },
            new Spec { Id = 7434, Donor = "GlassFloorCorner", Fr = "Coin de sol en verre renforcé", En = "Reinforced glass floor corner", Es = "Esquina de suelo de vidrio reforzada" },
            new Spec { Id = 7435, Donor = "GlassWallCorner", Fr = "Coin en verre renforcé (bas)", En = "Reinforced glass corner (lower)", Es = "Esquina de vidrio reforzada (inferior)" },
            new Spec { Id = 7436, Donor = "GlassWallCornerInv", Fr = "Coin en verre renforcé (haut)", En = "Reinforced glass corner (upper)", Es = "Esquina de vidrio reforzada (superior)" },
            new Spec { Id = 7437, Donor = "GlassSlope", Fr = "Pente en verre renforcée", En = "Reinforced glass slope", Es = "Pendiente de vidrio reforzada" },
            new Spec { Id = 7438, Donor = "GlassSlopeCorner", Fr = "Coin de pente en verre renforcé (bas)", En = "Reinforced glass slope corner (lower)", Es = "Esquina de pendiente de vidrio reforzada (inferior)" },
            new Spec { Id = 7439, Donor = "GlassSlopeCornerInv", Fr = "Coin de pente en verre renforcé (haut)", En = "Reinforced glass slope corner (upper)", Es = "Esquina de pendiente de vidrio reforzada (superior)" },
        };

        private static readonly HashSet<ushort> Ids = new HashSet<ushort>(All.Select(s => s.Id));

        public static IEnumerable<ushort> PartIds => All.Select(s => s.Id);

        public static bool IsReinforced(PartSettings settings) => settings != null && Ids.Contains(settings.id);

        public static void Register()
        {
            if (DevHarness.Off("reinforced"))
                return;
            foreach (Spec part in All)
            {
                Spec spec = part;
                PartFactory.Ids.Add(spec.Id);
                BuildTabs.Add(AdvancedTab.ArmorName, spec.Id < 7430 ? AdvancedTab.ReinforcedIronRow : AdvancedTab.ReinforcedGlassRow, () => Get(spec.Id));
            }
            _registered = true;
            AdvancedMod.Log.Msg($"registered {All.Length} reinforced hull pieces");
        }

        // ---- The clones. SDModKit only clones parts with a prefab (the game's hull has none): they
        // are made here the way it does, and handed to the game's part list (ReinforcedHullListPatch)

        private static readonly Dictionary<ushort, HullPartSettings> Built = new Dictionary<ushort, HullPartSettings>();
        private static bool _registered;
        private static float _nextTry;

        public static IEnumerable<HullPartSettings> Clones => Built.Values.Where(c => c != null);

        public static HullPartSettings Get(ushort id)
        {
            EnsureBuilt();
            return Built.TryGetValue(id, out HullPartSettings clone) && clone != null ? clone : null;
        }

        /// <summary>Clones the donors once they are loaded (looked for at most once a second until then).</summary>
        public static void EnsureBuilt()
        {
            PivotedDoor.EnsureBuilt();
            if (!_registered || Built.Count == All.Length && Built.Values.All(c => c != null) || Time.realtimeSinceStartup < _nextTry)
                return;
            _nextTry = Time.realtimeSinceStartup + 1f;
            try
            {
                var donors = new Dictionary<string, HullPartSettings>();
                foreach (HullPartSettings settings in Resources.FindObjectsOfTypeAll<HullPartSettings>())
                {
                    if (settings != null && !Ids.Contains(settings.id) && !donors.ContainsKey(settings.name))
                        donors[settings.name] = settings;
                }
                foreach (Spec spec in All)
                {
                    if (Built.TryGetValue(spec.Id, out HullPartSettings done) && done != null || !donors.TryGetValue(spec.Donor, out HullPartSettings donor))
                        continue;
                    HullPartSettings clone = UnityEngine.Object.Instantiate(donor);
                    clone.name = "TFA_" + spec.Id;
                    clone.hideFlags = HideFlags.DontUnloadUnusedAsset;
                    clone.id = spec.Id;
                    // A piece with its own model (the pivoted plates): its own copy of the model, so
                    // nothing keyed on the model mixes it up with the game's piece of another color
                    // (its look flickered between the two)
                    if (clone.hasItsOwnMesh && clone.part != null)
                        clone.part = SDModKit.Game.CustomParts.CloneIntoHolder(clone.part, "TFA_" + spec.Id) ?? clone.part;
                    Configure(spec, clone);
                    Built[spec.Id] = clone;
                }
                LinkPivots();
                if (Built.Count == All.Length)
                    AdvancedMod.Log.Msg($"reinforced hull: {Built.Count} pieces cloned from the game's iron and glass");
            }
            catch (Exception e)
            {
                ErrorLog.Report("reinforced hull", e);
            }
        }

        private static void Configure(Spec spec, HullPartSettings settings)
        {
            bool glass = settings.hullMaterialType == HullMaterialType.Glass;
            settings.fullLabel = Loc.Pick(spec.Fr, spec.En, spec.Es);
            settings.localizedDescription = null;
            settings.description = glass
                ? Loc.Pick("Verre feuilleté et trempé, teinté plus sombre : trois fois plus résistant aux chocs, les armes n'y font qu'un tiers des dégâts et aucun trou. Se pose comme le verre du jeu.",
                    "Laminated, tempered glass, tinted darker: three times tougher in collisions, weapons do a third of the damage and punch no holes. Placed like the game's glass.",
                    "Vidrio laminado y templado, tintado más oscuro: tres veces más resistente a los choques, las armas solo hacen un tercio del daño y ningún agujero. Se coloca como el vidrio del juego.")
                : Loc.Pick("Plaque de fer doublée de titane, plus sombre : trois fois plus résistante aux chocs, les armes n'y font qu'un tiers des dégâts et aucun trou. Une fois et demie plus lourde. Se pose comme la coque du jeu.",
                    "Iron plate backed with titanium, darker: three times tougher in collisions, weapons do a third of the damage and punch no holes. Half again as heavy. Placed like the game's hull.",
                    "Placa de hierro reforzada con titanio, más oscura: tres veces más resistente a los choques, las armas solo hacen un tercio del daño y ningún agujero. Una vez y media más pesada. Se coloca como el casco del juego.");
        }

        // The pivoted plate and the corners switch to their pivoted twin: the reinforced twin
        private static void LinkPivots()
        {
            var byDonor = new Dictionary<string, HullPartSettings>();
            foreach (Spec spec in All)
            {
                if (Built.TryGetValue(spec.Id, out HullPartSettings clone) && clone != null)
                    byDonor[spec.Donor] = clone;
            }
            foreach (HullPartSettings clone in byDonor.Values)
            {
                PartSettings pivot = clone.pivotedSettings;
                if (pivot != null && !Ids.Contains(pivot.id) && byDonor.TryGetValue(pivot.name, out HullPartSettings twin))
                    clone.pivotedSettings = twin;
            }
        }

        public static List<ItemDrop> Cost(HullPartSettings settings)
        {
            int quantity = settings.hullMeshSegmentType == HullMeshSegmentType.Corner ? 1 : 2;
            bool glass = settings.hullMaterialType == HullMaterialType.Glass;
            return new List<ItemDrop>
            {
                new ItemDrop { ItemId = glass ? ItemsConstants.GlassItemId : ItemsConstants.IronItemId, Quantity = (byte)(quantity * 2) },
                new ItemDrop { ItemId = glass ? ItemsConstants.IronItemId : Items.ModItems.Titanium, Quantity = 1 },
            };
        }

        public static HullPartColor DefaultColor(HullPartSettings settings)
        {
            Vector3Byte color = settings.hullMaterialType == HullMaterialType.Glass ? GlassColor : IronColor;
            return new HullPartColor(color, color);
        }

        // ---- Arsenal (only when installed): a third of the damage, no holes

        private static FieldInfo _keyShip, _keyPos;

        public static void InstallArsenal(HarmonyLib.Harmony harmony)
        {
            Type hull = AccessTools.TypeByName("Arsenal.Damage.HullDamage");
            Type key = AccessTools.TypeByName("Arsenal.Damage.PanelKey");
            MethodInfo apply = hull != null ? AccessTools.Method(hull, "ApplyServer") : null;
            if (apply == null || key == null || apply.GetParameters().Length != 3)
                return;
            _keyShip = AccessTools.Field(key, "ShipId");
            _keyPos = AccessTools.Field(key, "Pos");
            if (_keyShip == null || _keyPos == null)
                return;
            harmony.Patch(apply, prefix: new HarmonyMethod(typeof(ReinforcedHull), nameof(ArsenalPrefix)) { priority = Priority.Low });
            AdvancedMod.Log.Msg("Arsenal found: reinforced hull takes a third of its damage");
        }

        // HullDamage.ApplyServer(PanelKey key, HoleMask add, float damage)
        private static void ArsenalPrefix(object[] __args)
        {
            try
            {
                if (GameServices.ShipsServer == null || !GameServices.ShipsServer.TryGetTrackedShip((uint)_keyShip.GetValue(__args[0]), out TrackedShipServer ship))
                    return;
                var pos = (HullPartPosition)_keyPos.GetValue(__args[0]);
                ShipVoxel voxel = ship.GetVoxel(pos.VoxelCoord);
                HullPart? part = voxel?.GetPartFromType(pos.Type);
                if (part == null || !IsReinforced(part.Value.HullSettings))
                    return;
                __args[1] = Activator.CreateInstance(__args[1].GetType());
                __args[2] = Convert.ToSingle(__args[2]) / Toughness;
            }
            catch (Exception e)
            {
                ErrorLog.Report("reinforced hull against Arsenal", e);
            }
        }
    }

    /// <summary>
    /// The game's part list (by id: loading ships, the build tool, thumbnails) is made once from
    /// every PartSettings loaded: the reinforced pieces are cloned just before and added if missing.
    /// </summary>
    [HarmonyPatch(typeof(Ships.Interface.Services.PartSettingsProvider), "EnsureDictIsCached")]
    internal static class ReinforcedHullListPatch
    {
        private static readonly AccessTools.FieldRef<Ships.Interface.Services.PartSettingsProvider, Dictionary<ushort, PartSettings>> Dict =
            AccessTools.FieldRefAccess<Ships.Interface.Services.PartSettingsProvider, Dictionary<ushort, PartSettings>>("_partsDict");

        private static void Prefix(Ships.Interface.Services.PartSettingsProvider __instance)
        {
            if (Dict(__instance) == null)
                ReinforcedHull.EnsureBuilt();
        }

        private static void Postfix(Ships.Interface.Services.PartSettingsProvider __instance)
        {
            Dictionary<ushort, PartSettings> dict = Dict(__instance);
            if (dict == null)
                return;
            foreach (HullPartSettings clone in ReinforcedHull.Clones.Append(PivotedDoor.Clone))
            {
                if (clone != null && !dict.ContainsKey(clone.id))
                    dict[clone.id] = clone;
            }
        }
    }

    /// <summary>Reinforced hull weighs more than the game's (whose mass is per material only).</summary>
    [HarmonyPatch(typeof(HullPartSettings), nameof(HullPartSettings.Mass), MethodType.Getter)]
    internal static class ReinforcedHullMassPatch
    {
        private static void Postfix(HullPartSettings __instance, ref float __result)
        {
            if (ReinforcedHull.IsReinforced(__instance))
                __result *= ReinforcedHull.MassFactor;
        }
    }

    /// <summary>The game prices hull by material and shape: reinforced hull costs more.</summary>
    [HarmonyPatch(typeof(HullPartSettings), nameof(HullPartSettings.BuildingCost), MethodType.Getter)]
    internal static class ReinforcedHullCostPatch
    {
        private static void Postfix(HullPartSettings __instance, ref List<ItemDrop> __result)
        {
            if (ReinforcedHull.IsReinforced(__instance) && !__instance.hasItsOwnMesh)
                __result = ReinforcedHull.Cost(__instance);
        }
    }

    /// <summary>A newly placed reinforced piece (and its placement preview) starts a shade darker.</summary>
    [HarmonyPatch(typeof(HullPart), MethodType.Constructor, new[] { typeof(HullPartSettings), typeof(HullPartPlacement) })]
    internal static class ReinforcedHullColorPatch
    {
        private static void Postfix(ref HullPart __instance, HullPartSettings settings)
        {
            if (ReinforcedHull.IsReinforced(settings))
                __instance.Color = ReinforcedHull.DefaultColor(settings);
        }
    }

    /// <summary>Collisions: the impact needed to break a reinforced piece is three times the game's.</summary>
    [HarmonyPatch(typeof(ShipCollisionDestruction), "AddHullCandidate")]
    internal static class ReinforcedHullCollisionPatch
    {
        private static FieldInfo _candidates, _hardness;

        private static void Prefix(ShipCollisionDestruction __instance, out int __state)
        {
            __state = Candidates(__instance)?.Count ?? 0;
        }

        private static void Postfix(ShipCollisionDestruction __instance, HullPart part, int __state)
        {
            if (!ReinforcedHull.IsReinforced(part.HullSettings))
                return;
            IList list = Candidates(__instance);
            if (list == null || list.Count <= __state)
                return;
            object candidate = list[list.Count - 1];
            if (_hardness == null)
                _hardness = AccessTools.Field(candidate.GetType(), "Hardness");
            _hardness.SetValue(candidate, (float)_hardness.GetValue(candidate) * ReinforcedHull.Toughness);
            list[list.Count - 1] = candidate;
        }

        private static IList Candidates(ShipCollisionDestruction instance)
        {
            if (_candidates == null)
                _candidates = AccessTools.Field(typeof(ShipCollisionDestruction), "_destroyCandidates");
            return _candidates?.GetValue(instance) as IList;
        }
    }
}

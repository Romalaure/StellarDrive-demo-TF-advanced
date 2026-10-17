using System.Collections.Generic;
using System.Linq;
using Items.Model;
using Research.Interface.Values;
using SDModKit.Game;
using Ships.Interface.Settings;
using StellarDriveDemoTFAdvanced.Common;
using StellarDriveDemoTFAdvanced.Items;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Parts
{
    /// <summary>
    /// The mod's branch of the research table: every family of parts is unlocked by a tech,
    /// branched on the game's own techs (metal construction after Iron Panels, engines after
    /// Chemical Propulsion...) and paid in resources at the research table like the game's. The
    /// techs are added to the game's research list (ResearchSettings, ids 7501 and up) and laid
    /// out by SDModKit so no line runs through a box. Off with ResearchUnlocks = false: every
    /// part is then available from the start, as before.
    /// </summary>
    internal static class ModResearch
    {
        private sealed class Tech
        {
            public ushort Id;
            public string Name;
            public ushort[] Requires;
            public Vector2Int Position;
            public ushort[] Parts;
            public (uint Item, int Count)[] Cost;
        }

        private const uint Iron = 101, Glass = 102, Wood = 110, Copper = 112;

        private static readonly Tech[] All =
        {
            new Tech { Id = 7501, Name = Loc.Pick("Construction métallique", "Metal construction", "Construcción metálica"), Requires = new ushort[] { 18 }, Position = new Vector2Int(4, 6),
                Parts = Range(7331, 7338).Concat(Range(7361, 7367)).ToArray(), Cost = new[] { (Iron, 30), (Copper, 5) } },
            new Tech { Id = 7502, Name = Loc.Pick("Charpenterie", "Carpentry", "Carpintería"), Requires = new ushort[] { 6 }, Position = new Vector2Int(5, 6),
                Parts = Range(7341, 7346), Cost = new[] { (Wood, 30) } },
            new Tech { Id = 7503, Name = Loc.Pick("Verrerie architecturale", "Architectural glass", "Vidrio arquitectónico"), Requires = new ushort[] { 7 }, Position = new Vector2Int(5, 5),
                Parts = Range(7351, 7356), Cost = new[] { (Glass, 20), (Iron, 5) } },
            new Tech { Id = 7504, Name = Loc.Pick("Ordinateur de bord", "Ship computer", "Ordenador de a bordo"), Requires = new ushort[] { 15 }, Position = new Vector2Int(4, 1),
                Parts = Range(7301, 7306), Cost = new[] { (Copper, 10), (Glass, 5) } },
            new Tech { Id = 7505, Name = Loc.Pick("Propulsion avancée", "Advanced propulsion", "Propulsión avanzada"), Requires = new ushort[] { 20 }, Position = new Vector2Int(5, 4),
                Parts = Range(7311, 7314).Concat(new ushort[] { 7347 }).ToArray(), Cost = new[] { (Iron, 25), (Copper, 10) } },
            new Tech { Id = 7506, Name = Loc.Pick("Réseau électrique avancé", "Advanced power grid", "Red eléctrica avanzada"), Requires = new ushort[] { 12 }, Position = new Vector2Int(3, 3),
                Parts = new ushort[] { 7321, 7322 }, Cost = new[] { (Copper, 20), (Iron, 10) } },
            new Tech { Id = 7507, Name = Loc.Pick("Technologie des artefacts", "Artifact technology", "Tecnología de artefactos"), Requires = new ushort[] { 7506 }, Position = new Vector2Int(5, 1),
                Parts = new ushort[] { 7323, 7324, 7325, 7328, 7330, 7319, 7326, 7327, 7315, 7339, 7340 }, Cost = new[] { (ModItems.Titanium, 10), (ModItems.Cryonite, 5), (ModItems.Artifact, 1) } },
            new Tech { Id = 7508, Name = Loc.Pick("Cartographie", "Cartography", "Cartografía"), Requires = new ushort[] { 14 }, Position = new Vector2Int(3, 0),
                Parts = new ushort[] { 7381 }, Cost = new[] { (Copper, 10), (Glass, 10), (ModItems.Meteorite, 2) } },
            new Tech { Id = 7509, Name = Loc.Pick("Modules de station", "Station modules", "Módulos de estación"), Requires = new ushort[] { 7501 }, Position = new Vector2Int(6, 6),
                Parts = Range(7371, 7380).Concat(Range(7382, 7390)).ToArray(), Cost = new[] { (Iron, 40), (Glass, 15), (ModItems.Titanium, 10) } },
            new Tech { Id = 7512, Name = Loc.Pick("Énergie nucléaire", "Nuclear power", "Energía nuclear"), Requires = new ushort[] { 7507 }, Position = new Vector2Int(6, 2),
                Parts = new ushort[] { 7396, 7397, 7398, 7316, 7317, 7320 }, Cost = new[] { (ModItems.Uranium, 10), (ModItems.Titanium, 15), (Copper, 20) } },
            new Tech { Id = 7510, Name = Loc.Pick("Matière noire", "Dark matter", "Materia oscura"), Requires = new ushort[] { 7512 }, Position = new Vector2Int(7, 2),
                Parts = new ushort[] { 7391, 7392, 7393, 7394, 7329, 7318 }, Cost = new[] { (ModItems.Meteorite, 10), (ModItems.QuantumCore, 1), (ModItems.NexoranAlloy, 5) } },
            new Tech { Id = 7511, Name = Loc.Pick("Portail dimensionnel", "Dimensional portal", "Portal dimensional"), Requires = new ushort[] { 7510 }, Position = new Vector2Int(8, 2),
                Parts = new ushort[] { 7395 }, Cost = new[] { (ModItems.Meteorite, 20), (ModItems.QuantumCore, 2), (ModItems.ExoticMatter, 3), (ModItems.Artifact, 2) } },
            new Tech { Id = 7513, Name = Loc.Pick("Guerre électronique", "Electronic warfare", "Guerra electrónica"), Requires = new ushort[] { 7508 }, Position = new Vector2Int(4, 0),
                Parts = new ushort[] { 7307, 7308, 7309, 7310 }, Cost = new[] { (Copper, 25), (Glass, 10), (ModItems.Titanium, 10), (ModItems.Quartz, 5) } },
            new Tech { Id = 7514, Name = Loc.Pick("Blindage renforcé", "Reinforced plating", "Blindaje reforzado"), Requires = new ushort[] { 7501 }, Position = new Vector2Int(7, 5),
                Parts = Range(7421, 7429).Concat(Range(7431, 7439)).ToArray(), Cost = new[] { (Iron, 40), (Glass, 20), (ModItems.Titanium, 10) } },
            new Tech { Id = 7515, Name = Loc.Pick("Minage embarqué", "Ship mining", "Minería embarcada"), Requires = new ushort[] { 7506 }, Position = new Vector2Int(7, 4),
                Parts = new ushort[] { 7399 }, Cost = new[] { (Iron, 25), (Copper, 15) } },
        };

        private static readonly HashSet<ushort> Locked = new HashSet<ushort>();
        private static ResearchSettingsList _list;
        private static int _placedCount;
        private static float _nextCheck;
        private static readonly List<ResearchSettings> Added = new List<ResearchSettings>();

        /// <summary>Whether the part waits for one of the mod's techs (otherwise it is always available).</summary>
        public static bool IsResearchPart(ushort partId) => Settings.ResearchUnlocks.Value && Locked.Contains(partId);

        private static ushort[] Range(int first, int last) => Enumerable.Range(first, last - first + 1).Select(i => (ushort)i).ToArray();

        /// <summary>Adds the techs to the game's research list, once its parts exist (call on every scene load).</summary>
        public static void Ensure()
        {
            if (!Settings.ResearchUnlocks.Value)
                return;
            ResearchSettingsList list = Resources.FindObjectsOfTypeAll<ResearchSettingsList>()
                .FirstOrDefault(l => l != null && l.list != null && l.list.Any(r => r != null && r.id == 12));
            if (list == null)
                return;
            if (Added.Count > 0 && Added.All(t => list.list.Contains(t)))
                return;
            CustomParts.EnsureAll();
            // SDModKit lays out its content packs' techs (ids above 99) and would move the game's own
            // techs above 99 and ours to make room: they keep their cells, ours are placed below
            SDModKit.Content.ResearchLayout.Exclude(list.list.Where(r => r != null && r.id > 99 && !SDModKit.Content.ContentResearch.All.Contains(r))
                .Select(r => r.id).Concat(All.Select(t => t.Id)));
            list.list.RemoveAll(r => r != null && Added.Contains(r));
            Added.Clear();
            Locked.Clear();
            foreach (Tech tech in All)
            {
                var parts = tech.Parts.Select(id => CustomParts.Get(id) ?? Construction.ReinforcedHull.Get(id)).Where(p => p != null).ToArray();
                if (parts.Length == 0)
                    continue;
                var settings = ScriptableObject.CreateInstance<ResearchSettings>();
                settings.name = "TFA_Tech" + tech.Id;
                settings.hideFlags = HideFlags.DontUnloadUnusedAsset;
                settings.id = tech.Id;
                settings.techName = tech.Name;
                settings.unlockedParts = parts;
                settings.unlockedFeatures = new Research.Interface.Model.ResearchUnlockableFeature[0];
                settings.unlockedGuideEntriesIds = new uint[0];
                settings.requirements = tech.Requires.Select(id => list.list.FirstOrDefault(r => r != null && r.id == id)).Where(r => r != null).ToArray();
                settings.resourceCost = tech.Cost.Select(c => Item(c.Item, c.Count)).Where(i => i != null).ToArray();
                settings.positionInTechTree = tech.Position;
                settings.availableInDemo = true;
                settings.hideFromTechTree = false;
                list.list.Add(settings);
                Added.Add(settings);
                foreach (PartSettings part in parts)
                    Locked.Add(part.id);
            }
            Place(list);
            _list = list;
            _placedCount = list.list.Count;
            AdvancedMod.Log.Msg("research: " + string.Join(", ", Added.Select(t => $"{t.techName} {t.positionInTechTree} ({t.unlockedParts.Length} parts)")));
        }

        // ---- Layout: the tree's cells are 500 x 260 apart, boxes 410 x 170, lines from a box's right edge to the next one's left edge

        private const float CellWidth = 500f, CellHeight = 260f, HalfWidth = 205f, HalfHeight = 85f;
        private const int MaxColumn = 7, MaxRow = 6;

        private static bool Shown(ResearchSettings r) => r != null && r.availableInDemo && !r.hideFromTechTree;
        private static Vector2 Center(Vector2Int c) => new Vector2(c.x * CellWidth, c.y * CellHeight);
        private static Vector2 Out(Vector2Int c) => Center(c) + new Vector2(HalfWidth, 0f);
        private static Vector2 In(Vector2Int c) => Center(c) - new Vector2(HalfWidth, 0f);
        private static Rect Box(Vector2Int c) => new Rect(Center(c).x - HalfWidth - 10f, Center(c).y - HalfHeight - 10f, HalfWidth * 2f + 20f, HalfHeight * 2f + 20f);

        private static bool Crosses(Vector2 a, Vector2 b, Rect box)
        {
            for (int i = 1; i < 64; i++)
            {
                if (box.Contains(Vector2.Lerp(a, b, i / 64f)))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Puts each of the mod's techs in a free cell right of its requirements, as close as possible
        /// to its wished cell, where neither its lines cross a box nor a line crosses it. The game's
        /// techs never move.
        /// </summary>
        private static void Place(ResearchSettingsList list)
        {
            var shown = list.list.Where(Shown).ToList();
            foreach (ResearchSettings tech in Added)
            {
                Vector2Int wish = tech.positionInTechTree;
                int minX = tech.requirements.Where(Shown).Select(r => r.positionInTechTree.x + 1).DefaultIfEmpty(0).Max();
                Vector2Int? best = null;
                float bestScore = float.MaxValue;
                for (int x = minX; x <= MaxColumn; x++)
                {
                    for (int y = 0; y <= MaxRow; y++)
                    {
                        var cell = new Vector2Int(x, y);
                        if (shown.Any(o => o != tech && o.positionInTechTree == cell))
                            continue;
                        int crossings = Crossings(tech, cell, shown);
                        float score = crossings * 1000f + Mathf.Abs(x - wish.x) * 1.5f + Mathf.Abs(y - wish.y);
                        if (score < bestScore)
                        {
                            bestScore = score;
                            best = cell;
                        }
                    }
                }
                if (best.HasValue)
                    tech.positionInTechTree = best.Value;
                if (bestScore >= 1000f)
                    AdvancedMod.Log.Warning($"research: {tech.techName} placed at {tech.positionInTechTree}, a line crosses a box");
            }
        }

        private static int Crossings(ResearchSettings tech, Vector2Int cell, List<ResearchSettings> shown)
        {
            int count = 0;
            // Its own lines, from each requirement, through any other box
            foreach (ResearchSettings req in tech.requirements.Where(Shown))
            {
                foreach (ResearchSettings other in shown)
                {
                    if (other != tech && other != req && Crosses(Out(req.positionInTechTree), In(cell), Box(other.positionInTechTree)))
                        count++;
                }
            }
            // Other techs' lines through its box
            Rect box = Box(cell);
            foreach (ResearchSettings other in shown)
            {
                if (other == tech || other.requirements == null)
                    continue;
                foreach (ResearchSettings req in other.requirements.Where(Shown))
                {
                    if (req != tech && Crosses(Out(req.positionInTechTree), In(other.positionInTechTree), box))
                        count++;
                }
            }
            return count;
        }

        /// <summary>Other mods add techs later (when a world loads): ours move out of their way.</summary>
        public static void Tick()
        {
            if (_list == null || Time.unscaledTime < _nextCheck)
                return;
            _nextCheck = Time.unscaledTime + 2f;
            if (_list.list.Count == _placedCount)
                return;
            SDModKit.Content.ResearchLayout.Exclude(_list.list.Where(r => r != null && r.id > 99 && !SDModKit.Content.ContentResearch.All.Contains(r)).Select(r => r.id));
            Place(_list);
            _placedCount = _list.list.Count;
            AdvancedMod.Log.Msg("research: placed again: " + string.Join(", ", Added.Select(t => $"{t.techName} {t.positionInTechTree}")));
        }

        private static ItemInstance Item(uint id, int count)
        {
            ItemSettings settings = ModItems.Settings(id) ?? Resources.FindObjectsOfTypeAll<ItemSettings>().FirstOrDefault(i => i != null && i.id == id);
            return settings != null ? new ItemInstance(settings, count) : null;
        }
    }
}

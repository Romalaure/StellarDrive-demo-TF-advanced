using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Settings;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Wiki
{
    /// <summary>
    /// Pages the wiki makes from the game's own data: a page per part of the mod ("part:7161": its
    /// picture, description, mass, cost, sockets and the tech that unlocks it) and the results of a
    /// search ("search": parts and pages whose name or text has the words typed).
    /// </summary>
    internal static class WikiParts
    {
        public const string PartPrefix = "part:", SearchPage = "search";

        /// <summary>What the search field holds.</summary>
        public static string Query = "";

        public static PartSettings Part(string page)
        {
            if (page == null || !page.StartsWith(PartPrefix) || !ushort.TryParse(page.Substring(PartPrefix.Length), out ushort id) || id < 7100 || id > 7499)
                return null;
            return Resources.FindObjectsOfTypeAll<PartSettings>().FirstOrDefault(p => p != null && p.id == id);
        }

        public static bool Exists(string page) => page == SearchPage || Part(page) != null;

        public static string Title(string page) => page == SearchPage ? Loc.Pick("Recherche", "Search", "Búsqueda") : Part(page)?.fullLabel;

        public static List<WikiBlock> Render(string page) => page == SearchPage ? Search() : PartPage(Part(page));

        private static List<WikiBlock> PartPage(PartSettings part)
        {
            var blocks = new List<WikiBlock>();
            if (part == null)
                return blocks;
            blocks.Add(new WikiBlock { Text = "<size=150%><b>" + Escape(part.fullLabel) + "</b></size>\n" });
            blocks.Add(new WikiBlock { Icon = new WikiImages.Ref { Kind = WikiImages.Kind.Part, Id = part.id }, Text = Escape(part.description ?? ""), Large = true });
            var text = new StringBuilder();
            text.Append("\n<color=#9ab0c0>").Append(Loc.Pick("Masse :", "Mass:", "Masa:")).Append("</color> ").Append(part.mass.ToString("0.##")).Append(" kg\n");
            string tech = Unlocking(part);
            text.Append("<color=#9ab0c0>").Append(Loc.Pick("Recherche :", "Research:", "Investigación:")).Append("</color> ")
                .Append(tech != null ? $"<link=\"Recherche\"><color=#5fc8ff><u>{Escape(tech)}</u></color></link>" : Loc.Pick("disponible dès le début", "available from the start", "disponible desde el inicio")).Append('\n');
            blocks.Add(new WikiBlock { Text = text.ToString() });

            // The cost, each item with its icon
            List<global::Items.Model.ItemInstance> cost = Cost(part);
            if (cost.Count > 0)
            {
                blocks.Add(new WikiBlock { Text = "\n<size=120%><b><color=#7fd8ff>" + Loc.Pick("Coût", "Cost", "Coste") + "</color></b></size>" });
                foreach (global::Items.Model.ItemInstance item in cost.Where(i => i?.ItemSettings != null))
                    blocks.Add(new WikiBlock { Icon = new WikiImages.Ref { Kind = WikiImages.Kind.Item, Id = item.ItemId }, Text = $"<b>{Escape(item.ItemSettings.itemName)}</b> × {item.Quantity}" });
            }

            // The sockets, in their order (the cables plug into them by number)
            SocketObject[] sockets = part.part != null ? part.part.GetComponentsInChildren<SocketObject>(true).OrderBy(s => s.Id).ToArray() : new SocketObject[0];
            var plugs = new StringBuilder("\n<size=120%><b><color=#7fd8ff>" + Loc.Pick("Prises", "Sockets", "Conectores") + "</color></b></size>\n");
            if (sockets.Length == 0)
                plugs.Append(Loc.Pick("Aucune : elle ne se branche pas.", "None: it plugs into nothing.", "Ninguno: no se conecta.")).Append('\n');
            foreach (SocketObject socket in sockets)
                plugs.Append("• ").Append(Loc.Pick("Prise ", "Socket ", "Conector ")).Append(socket.Id + 1).Append(" — ").Append(Kind(socket.Type)).Append('\n');
            if (sockets.Length > 0)
                plugs.Append("<color=#9ab0c0>").Append(Loc.Pick("Les câbles du même type se branchent ensemble : signal (données), puissance (courant), fluide (conduites).",
                    "Cables of the same kind plug together: signal (data), power (current), fluid (pipes).",
                    "Los cables del mismo tipo se conectan entre sí: señal (datos), potencia (corriente), fluido (conductos).")).Append("</color>\n");
            blocks.Add(new WikiBlock { Text = plugs.ToString() });
            return blocks;
        }

        private static string Kind(CableType type)
        {
            switch (type)
            {
                case CableType.Power: return "<color=#ffd060>" + Loc.Pick("puissance", "power", "potencia") + "</color>";
                case CableType.Fluid: return "<color=#60c0ff>" + Loc.Pick("fluide", "fluid", "fluido") + "</color>";
                default: return "<color=#90ff90>" + Loc.Pick("signal", "signal", "señal") + "</color>";
            }
        }

        private static readonly HarmonyLib.AccessTools.FieldRef<PartSettings, List<global::Items.Model.ItemInstance>> BuildingCost =
            HarmonyLib.AccessTools.FieldRefAccess<PartSettings, List<global::Items.Model.ItemInstance>>("buildingCost");

        private static List<global::Items.Model.ItemInstance> Cost(PartSettings part) => BuildingCost(part) ?? new List<global::Items.Model.ItemInstance>();

        private static string Unlocking(PartSettings part) =>
            Resources.FindObjectsOfTypeAll<Research.Interface.Values.ResearchSettings>()
                .FirstOrDefault(r => r != null && r.unlockedParts != null && r.unlockedParts.Contains(part))?.techName;

        private static List<WikiBlock> Search()
        {
            var blocks = new List<WikiBlock>();
            string query = WikiImages.Normalize(Query, false);
            blocks.Add(new WikiBlock { Text = "<size=150%><b>" + Loc.Pick("Recherche", "Search", "Búsqueda") + (query.Length > 0 ? " : " + Escape(Query) : "") + "</b></size>\n" });
            if (query.Length < 2)
            {
                blocks.Add(new WikiBlock { Text = Loc.Pick("Tape au moins deux lettres dans le champ de recherche, puis Entrée.", "Type at least two letters in the search field, then Enter.", "Escribe al menos dos letras en el campo de búsqueda y pulsa Intro.") });
                return blocks;
            }
            string[] words = query.Split(' ');
            bool Matches(string text)
            {
                string normalized = WikiImages.Normalize(text, false);
                return words.All(w => normalized.Contains(w));
            }
            List<PartSettings> parts = Resources.FindObjectsOfTypeAll<PartSettings>()
                .Where(p => p != null && p.id >= 7100 && p.id <= 7499 && !string.IsNullOrEmpty(p.fullLabel) && Matches(p.fullLabel))
                .GroupBy(p => p.id).Select(g => g.First()).OrderBy(p => p.fullLabel).Take(40).ToList();
            List<string> pages = WikiPages.Names.Where(n => Matches(WikiPages.Title(n)) || Matches(WikiPages.Text(n))).OrderBy(WikiPages.Title).ToList();
            blocks.Add(new WikiBlock { Text = $"\n<size=120%><b><color=#7fd8ff>{Loc.Pick("Pièces", "Parts", "Piezas")} ({parts.Count})</color></b></size>" });
            foreach (PartSettings part in parts)
                blocks.Add(new WikiBlock { Icon = new WikiImages.Ref { Kind = WikiImages.Kind.Part, Id = part.id }, Text = $"<link=\"{PartPrefix}{part.id}\"><color=#5fc8ff><u>{Escape(part.fullLabel)}</u></color></link>" });
            var text = new StringBuilder($"\n<size=120%><b><color=#7fd8ff>{Loc.Pick("Pages", "Pages", "Páginas")} ({pages.Count})</color></b></size>\n");
            foreach (string page in pages)
                text.Append("• ").Append($"<link=\"{page}\"><color=#5fc8ff><u>{Escape(WikiPages.Title(page))}</u></color></link>").Append('\n');
            if (parts.Count == 0 && pages.Count == 0)
                text.Append(Loc.Pick("Rien trouvé.", "Nothing found.", "No se encontró nada.")).Append('\n');
            blocks.Add(new WikiBlock { Text = text.ToString() });
            return blocks;
        }

        private static string Escape(string s) => (s ?? "").Replace("<", "‹");
    }
}

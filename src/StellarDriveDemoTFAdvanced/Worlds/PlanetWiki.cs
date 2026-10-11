using System.Collections.Generic;
using System.Linq;

namespace StellarDriveDemoTFAdvanced.Worlds
{
    /// <summary>
    /// What a player needs to know about each world: the cartography table shows it in play, and
    /// docs/PLANETES.md and the GitHub wiki say the same.
    /// </summary>
    internal sealed class WikiEntry
    {
        public ulong Id;
        public string Name;
        public string System;
        public string Biome;
        public string Gravity;
        public string Air;
        public string[] Resources;
        public string[] Places;
        public string Use;
        public string Danger;
    }

    internal static class PlanetWiki
    {
        public const string Otrera = "442 Otrera";
        public const string Erebos = "Érèbe";

        public static readonly WikiEntry[] All =
        {
            new WikiEntry { Id = 4, Name = "442 Otrera b", System = Otrera, Biome = "tempérée (planète de départ)", Gravity = "1 g", Air = "respirable",
                Resources = new[] { "fer", "cuivre", "verre", "bois", "glace" }, Places = new[] { "épaves du jeu", "anneau d'astéroïdes" },
                Use = "point de départ : bois, fer et cuivre pour les premières pièces." },
            new WikiEntry { Id = 101, Name = "TF Aridia", System = Otrera, Biome = "désert : dunes, mesas rouges, lacs salés", Gravity = "0,9 g", Air = "respirable",
                Resources = new[] { "quartz", "fer", "cuivre", "verre" }, Places = new[] { "ruines en grès", "villages et temples", "épaves, cargos" },
                Use = "quartz ; fragments d'artefact dans les temples ; l'Avant-poste Orion tout près." },
            new WikiEntry { Id = 102, Name = "TF Petra", System = Otrera, Biome = "roche grise, cratères, anneau d'astéroïdes", Gravity = "0,45 g", Air = "aucun",
                Resources = new[] { "titane", "météorite", "cuivre", "astéroïdes" }, Places = new[] { "ruines en basalte", "épaves, cargos", "trou noir à 3 000 km" },
                Use = "titane et météorite ; accès au trou noir et à la Sonde Hadès.", Danger = "pas d'air : emportez de l'oxygène." },
            new WikiEntry { Id = 103, Name = "TF Altis", System = Otrera, Biome = "montagnes jusqu'à 8 km, neige", Gravity = "1,1 g", Air = "respirable",
                Resources = new[] { "titane", "bois (pins, bouleaux)" }, Places = new[] { "ruines et village en granit" },
                Use = "titane et bois." },
            new WikiEntry { Id = 104, Name = "TF Verdance", System = Otrera, Biome = "tempérée, lacs, forêts", Gravity = "0,95 g", Air = "respirable",
                Resources = new[] { "ambre", "bois (chênes, bouleaux, sapins)" }, Places = new[] { "ruines moussues", "villages", "épaves, cargos", "Station Kepler" },
                Use = "première destination : ambre, bois, coffres de village." },
            new WikiEntry { Id = 105, Name = "TF Glacia", System = Otrera, Biome = "glaciaire, montagnes de 9 km, anneau", Gravity = "1 g", Air = "respirable",
                Resources = new[] { "cryonite", "titane", "astéroïdes" }, Places = new[] { "ruines de glace", "épaves, cargos", "trou noir à 4 100 km" },
                Use = "cryonite : batterie à cryonite, condenseur, champ de force." },
            new WikiEntry { Id = 106, Name = "TF Nexora", System = Otrera, Biome = "futuriste, cité abandonnée", Gravity = "0,85 g", Air = "respirable",
                Resources = new[] { "noyau quantique", "alliage nexorien", "quartz" }, Places = new[] { "cité : gratte-ciel, tours, dômes, places" },
                Use = "noyaux quantiques et alliage dans les gratte-ciel." },
            new WikiEntry { Id = 107, Name = "TF Sylvara", System = Otrera, Biome = "jungle, arbres géants, fleurs", Gravity = "1,05 g", Air = "respirable",
                Resources = new[] { "ambre", "bois en quantité" }, Places = new[] { "temples et villages moussus", "épaves, cargos" },
                Use = "bois, ambre, artefacts des temples." },
            new WikiEntry { Id = 111, Name = "Pyros", System = Erebos, Biome = "volcanique : basalte noir, lacs de lave, arbres calcinés", Gravity = "1,2 g", Air = "toxique",
                Resources = new[] { "météorite (beaucoup)", "titane" }, Places = new[] { "ruines de forge en basalte", "village", "cargos", "trou noir l'Œil d'Érèbe" },
                Use = "la meilleure source de météorite ; près du trou noir, le collecteur de matière noire va cinq fois plus vite.", Danger = "air irrespirable : oxygène obligatoire." },
            new WikiEntry { Id = 112, Name = "Thalassa", System = Erebos, Biome = "océanique : îles de sable blanc, palmiers", Gravity = "0,9 g", Air = "respirable",
                Resources = new[] { "quartz", "ambre", "bois de palmier" }, Places = new[] { "villages et ruines en grès sur les îles", "épaves", "épave du Vagabond (en orbite)" },
                Use = "base tranquille dans Érèbe : air respirable, bois et quartz." },
            new WikiEntry { Id = 113, Name = "Mycélia", System = Erebos, Biome = "fongique : champignons géants lumineux, mousse violette", Gravity = "0,8 g", Air = "toxique (spores)",
                Resources = new[] { "météorite", "cryonite" }, Places = new[] { "ruines moussues", "cargos" },
                Use = "météorite et cryonite ; forêts de champignons à couper pour le bois.", Danger = "spores toxiques : oxygène obligatoire." },
            new WikiEntry { Id = 114, Name = "Cristallis", System = Erebos, Biome = "cristalline : plaines de sel, crêtes, anneau de cristal", Gravity = "0,6 g", Air = "trop mince",
                Resources = new[] { "quartz (beaucoup)", "cryonite", "météorite", "astéroïdes de cristal" }, Places = new[] { "ruines de glace", "épaves", "épave du Léviathan (en orbite)" },
                Use = "quartz et cryonite en masse ; faible gravité.", Danger = "air trop mince : oxygène obligatoire." },
        };

        private static readonly List<WikiEntry> Extra = new List<WikiEntry>();

        /// <summary>Adds the entries of worlds created elsewhere (the Erebos galaxy).</summary>
        public static void Add(WikiEntry entry)
        {
            Extra.RemoveAll(e => e.Id == entry.Id);
            Extra.Add(entry);
        }

        public static IEnumerable<WikiEntry> Entries => All.Concat(Extra);

        public static WikiEntry Get(ulong id) => Entries.FirstOrDefault(e => e.Id == id);
    }
}

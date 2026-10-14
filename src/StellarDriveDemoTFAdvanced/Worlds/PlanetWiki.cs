using System.Collections.Generic;
using System.Linq;
using StellarDriveDemoTFAdvanced.Common;

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

        /// <summary>The system's name as shown to the players (<see cref="Erebos"/> stays the key).</summary>
        public static string SystemName(string system) => system == Erebos ? Loc.Pick("Érèbe", "Erebus") : system;

        private static string[] L(string[] french, string[] english) => Loc.English ? english : french;

        public static readonly WikiEntry[] All =
        {
            new WikiEntry { Id = 4, Name = "442 Otrera b", System = Otrera, Biome = Loc.Pick("tempérée (planète de départ)", "temperate (starting planet)"), Gravity = "1 g", Air = Loc.Pick("respirable", "breathable"),
                Resources = L(new[] { "fer", "cuivre", "verre", "bois", "glace" }, new[] { "iron", "copper", "glass", "wood", "ice" }),
                Places = L(new[] { "épaves du jeu", "anneau d'astéroïdes" }, new[] { "the game's wrecks", "asteroid ring" }),
                Use = Loc.Pick("point de départ : bois, fer et cuivre pour les premières pièces.", "starting point: wood, iron and copper for the first parts.") },
            new WikiEntry { Id = 101, Name = "TF Aridia", System = Otrera, Biome = Loc.Pick("désert : dunes, mesas rouges, lacs salés", "desert: dunes, red mesas, salt lakes"), Gravity = Loc.Pick("0,9 g", "0.9 g"), Air = Loc.Pick("respirable", "breathable"),
                Resources = L(new[] { "quartz", "uranium", "fer", "cuivre", "verre" }, new[] { "quartz", "uranium", "iron", "copper", "glass" }),
                Places = L(new[] { "ruines en grès", "villages et temples", "épaves, cargos" }, new[] { "sandstone ruins", "villages and temples", "wrecks, freighters" }),
                Use = Loc.Pick("quartz ; fragments d'artefact dans les temples ; l'Avant-poste Orion tout près.", "quartz; artifact fragments in the temples; Orion Outpost close by.") },
            new WikiEntry { Id = 102, Name = "TF Petra", System = Otrera, Biome = Loc.Pick("roche grise, cratères, anneau d'astéroïdes", "grey rock, craters, asteroid ring"), Gravity = Loc.Pick("0,45 g", "0.45 g"), Air = Loc.Pick("aucun", "none"),
                Resources = L(new[] { "titane", "météorite", "uranium", "cuivre", "astéroïdes" }, new[] { "titanium", "meteorite", "uranium", "copper", "asteroids" }),
                Places = L(new[] { "ruines en basalte", "épaves, cargos", "trou noir à 3 000 km" }, new[] { "basalt ruins", "wrecks, freighters", "black hole 3,000 km away" }),
                Use = Loc.Pick("titane et météorite ; accès au trou noir et à la Sonde Hadès.", "titanium and meteorite; way to the black hole and the Hades Probe."),
                Danger = Loc.Pick("pas d'air : emportez de l'oxygène.", "no air: bring oxygen.") },
            new WikiEntry { Id = 103, Name = "TF Altis", System = Otrera, Biome = Loc.Pick("montagnes jusqu'à 8 km, neige", "mountains up to 8 km, snow"), Gravity = Loc.Pick("1,1 g", "1.1 g"), Air = Loc.Pick("respirable", "breathable"),
                Resources = L(new[] { "titane", "bois (pins, bouleaux)" }, new[] { "titanium", "wood (pines, birches)" }),
                Places = L(new[] { "ruines et village en granit" }, new[] { "granite ruins and village" }),
                Use = Loc.Pick("titane et bois.", "titanium and wood.") },
            new WikiEntry { Id = 104, Name = "TF Verdance", System = Otrera, Biome = Loc.Pick("tempérée, lacs, forêts", "temperate, lakes, forests"), Gravity = Loc.Pick("0,95 g", "0.95 g"), Air = Loc.Pick("respirable", "breathable"),
                Resources = L(new[] { "ambre", "bois (chênes, bouleaux, sapins)" }, new[] { "amber", "wood (oaks, birches, firs)" }),
                Places = L(new[] { "ruines moussues", "villages", "épaves, cargos", "Station Kepler" }, new[] { "mossy ruins", "villages", "wrecks, freighters", "Kepler Station" }),
                Use = Loc.Pick("première destination : ambre, bois, coffres de village.", "first destination: amber, wood, village chests.") },
            new WikiEntry { Id = 105, Name = "TF Glacia", System = Otrera, Biome = Loc.Pick("glaciaire, montagnes de 9 km, anneau", "glacial, 9 km mountains, ring"), Gravity = "1 g", Air = Loc.Pick("respirable", "breathable"),
                Resources = L(new[] { "cryonite", "titane", "astéroïdes" }, new[] { "cryonite", "titanium", "asteroids" }),
                Places = L(new[] { "ruines de glace", "épaves, cargos", "trou noir à 4 100 km" }, new[] { "ice ruins", "wrecks, freighters", "black hole 4,100 km away" }),
                Use = Loc.Pick("cryonite : batterie à cryonite, condenseur, champ de force.", "cryonite: cryonite battery, condenser, force field.") },
            new WikiEntry { Id = 106, Name = "TF Nexora", System = Otrera, Biome = Loc.Pick("futuriste, cité abandonnée", "futuristic, abandoned city"), Gravity = Loc.Pick("0,85 g", "0.85 g"), Air = Loc.Pick("respirable", "breathable"),
                Resources = L(new[] { "noyau quantique", "alliage nexorien", "quartz" }, new[] { "quantum core", "Nexoran alloy", "quartz" }),
                Places = L(new[] { "cité : gratte-ciel, tours, dômes, places" }, new[] { "city: skyscrapers, towers, domes, squares" }),
                Use = Loc.Pick("noyaux quantiques et alliage dans les gratte-ciel.", "quantum cores and alloy in the skyscrapers.") },
            new WikiEntry { Id = 107, Name = "TF Sylvara", System = Otrera, Biome = Loc.Pick("jungle, arbres géants, fleurs", "jungle, giant trees, flowers"), Gravity = Loc.Pick("1,05 g", "1.05 g"), Air = Loc.Pick("respirable", "breathable"),
                Resources = L(new[] { "ambre", "bois en quantité" }, new[] { "amber", "plenty of wood" }),
                Places = L(new[] { "temples et villages moussus", "épaves, cargos" }, new[] { "mossy temples and villages", "wrecks, freighters" }),
                Use = Loc.Pick("bois, ambre, artefacts des temples.", "wood, amber, temple artifacts.") },
            new WikiEntry { Id = 111, Name = "Pyros", System = Erebos, Biome = Loc.Pick("volcanique : basalte noir, lacs de lave, arbres calcinés", "volcanic: black basalt, lava lakes, charred trees"), Gravity = Loc.Pick("1,2 g", "1.2 g"), Air = Loc.Pick("toxique", "toxic"),
                Resources = L(new[] { "météorite (beaucoup)", "titane", "uranium" }, new[] { "meteorite (lots)", "titanium", "uranium" }),
                Places = L(new[] { "ruines de forge en basalte", "village", "cargos", "trou noir l'Œil d'Érèbe" }, new[] { "basalt forge ruins", "village", "freighters", "black hole the Eye of Erebus" }),
                Use = Loc.Pick("la meilleure source de météorite ; près du trou noir, le collecteur de matière noire va cinq fois plus vite.", "the best meteorite source; near the black hole the dark matter collector runs five times faster."),
                Danger = Loc.Pick("air irrespirable : oxygène obligatoire.", "unbreathable air: oxygen required.") },
            new WikiEntry { Id = 112, Name = "Thalassa", System = Erebos, Biome = Loc.Pick("océanique : îles de sable blanc, palmiers", "oceanic: white sand islands, palm trees"), Gravity = Loc.Pick("0,9 g", "0.9 g"), Air = Loc.Pick("respirable", "breathable"),
                Resources = L(new[] { "quartz", "ambre", "bois de palmier" }, new[] { "quartz", "amber", "palm wood" }),
                Places = L(new[] { "villages et ruines en grès sur les îles", "épaves", "épave du Vagabond (en orbite)" }, new[] { "sandstone villages and ruins on the islands", "wrecks", "wreck of the Wanderer (in orbit)" }),
                Use = Loc.Pick("base tranquille dans Érèbe : air respirable, bois et quartz.", "quiet base in Erebus: breathable air, wood and quartz.") },
            new WikiEntry { Id = 113, Name = "Mycélia", System = Erebos, Biome = Loc.Pick("fongique : champignons géants lumineux, mousse violette", "fungal: giant glowing mushrooms, purple moss"), Gravity = Loc.Pick("0,8 g", "0.8 g"), Air = Loc.Pick("toxique (spores)", "toxic (spores)"),
                Resources = L(new[] { "météorite", "cryonite" }, new[] { "meteorite", "cryonite" }),
                Places = L(new[] { "ruines moussues", "cargos" }, new[] { "mossy ruins", "freighters" }),
                Use = Loc.Pick("météorite et cryonite ; forêts de champignons à couper pour le bois.", "meteorite and cryonite; mushroom forests to cut for wood."),
                Danger = Loc.Pick("spores toxiques : oxygène obligatoire.", "toxic spores: oxygen required.") },
            new WikiEntry { Id = 114, Name = "Cristallis", System = Erebos, Biome = Loc.Pick("cristalline : plaines de sel, crêtes, anneau de cristal", "crystalline: salt plains, ridges, crystal ring"), Gravity = Loc.Pick("0,6 g", "0.6 g"), Air = Loc.Pick("trop mince", "too thin"),
                Resources = L(new[] { "quartz (beaucoup)", "cryonite", "météorite", "astéroïdes de cristal" }, new[] { "quartz (lots)", "cryonite", "meteorite", "crystal asteroids" }),
                Places = L(new[] { "ruines de glace", "épaves", "épave du Léviathan (en orbite)" }, new[] { "ice ruins", "wrecks", "wreck of the Leviathan (in orbit)" }),
                Use = Loc.Pick("quartz et cryonite en masse ; faible gravité.", "quartz and cryonite in bulk; low gravity."),
                Danger = Loc.Pick("air trop mince : oxygène obligatoire.", "air too thin: oxygen required.") },
            new WikiEntry { Id = 115, Name = "Bastion Écarlate", System = Erebos, Biome = Loc.Pick("artificielle : plaines de blindage, bassins rouges, forteresses pirates", "artificial: armor plains, red pools, pirate fortresses"), Gravity = Loc.Pick("1,3 g", "1.3 g"), Air = Loc.Pick("aucun", "none"),
                Resources = L(new[] { "uranium", "météorite", "butin des bases pirates" }, new[] { "uranium", "meteorite", "pirate base loot" }),
                Places = L(new[] { "forteresses, tourelles, remparts", "3 bases pirates en orbite", "anneau de débris" }, new[] { "fortresses, turrets, ramparts", "3 pirate bases in orbit", "debris ring" }),
                Use = Loc.Pick("le repaire des pirates : bases à détruire et à piller.", "the pirates' lair: bases to destroy and loot."),
                Danger = Loc.Pick("pirates très nombreux (attaques 4 fois plus fréquentes), bases armées ; pas d'air.", "many pirates (attacks 4 times as often), armed bases; no air.") },
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

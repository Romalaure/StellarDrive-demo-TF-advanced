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
        public static string SystemName(string system) => system == Erebos ? Loc.Pick("Érèbe", "Erebus", "Érebo") : system;

        private static string[] L(string[] french, string[] english, string[] spanish) => Loc.Pick(french, english, spanish);

        public static readonly WikiEntry[] All =
        {
            new WikiEntry { Id = 4, Name = "442 Otrera b", System = Otrera, Biome = Loc.Pick("tempérée (planète de départ)", "temperate (starting planet)", "templado (planeta de inicio)"), Gravity = "1 g", Air = Loc.Pick("respirable", "breathable", "respirable"),
                Resources = L(new[] { "fer", "cuivre", "verre", "bois", "glace" }, new[] { "iron", "copper", "glass", "wood", "ice" }, new[] { "hierro", "cobre", "vidrio", "madera", "hielo" }),
                Places = L(new[] { "épaves du jeu", "anneau d'astéroïdes" }, new[] { "the game's wrecks", "asteroid ring" }, new[] { "los restos del juego", "anillo de asteroides" }),
                Use = Loc.Pick("point de départ : bois, fer et cuivre pour les premières pièces.", "starting point: wood, iron and copper for the first parts.", "punto de partida: madera, hierro y cobre para las primeras piezas.") },
            new WikiEntry { Id = 101, Name = "TF Aridia", System = Otrera, Biome = Loc.Pick("désert : dunes, mesas rouges, lacs salés", "desert: dunes, red mesas, salt lakes", "desértico: dunas, mesetas rojas, lagos de sal"), Gravity = Loc.Pick("0,9 g", "0.9 g", "0,9 g"), Air = Loc.Pick("respirable", "breathable", "respirable"),
                Resources = L(new[] { "quartz", "uranium", "fer", "cuivre", "verre" }, new[] { "quartz", "uranium", "iron", "copper", "glass" }, new[] { "cuarzo", "uranio", "hierro", "cobre", "vidrio" }),
                Places = L(new[] { "ruines en grès", "villages et temples", "épaves, cargos" }, new[] { "sandstone ruins", "villages and temples", "wrecks, freighters" }, new[] { "ruinas de arenisca", "pueblos y templos", "restos, cargueros" }),
                Use = Loc.Pick("quartz ; fragments d'artefact dans les temples ; l'Avant-poste Orion tout près.", "quartz; artifact fragments in the temples; Orion Outpost close by.", "cuarzo; fragmentos de artefacto en los templos; Puesto avanzado Orión cerca.") },
            new WikiEntry { Id = 102, Name = "TF Petra", System = Otrera, Biome = Loc.Pick("roche grise, cratères, anneau d'astéroïdes", "grey rock, craters, asteroid ring", "roca gris, cráteres, anillo de asteroides"), Gravity = Loc.Pick("0,45 g", "0.45 g", "0,45 g"), Air = Loc.Pick("aucun", "none", "ninguno"),
                Resources = L(new[] { "titane", "météorite", "uranium", "cuivre", "astéroïdes" }, new[] { "titanium", "meteorite", "uranium", "copper", "asteroids" }, new[] { "titanio", "meteorito", "uranio", "cobre", "asteroides" }),
                Places = L(new[] { "ruines en basalte", "épaves, cargos", "trou noir à 3 000 km" }, new[] { "basalt ruins", "wrecks, freighters", "black hole 3,000 km away" }, new[] { "ruinas de basalto", "restos, cargueros", "agujero negro a 3.000 km" }),
                Use = Loc.Pick("titane et météorite ; accès au trou noir et à la Sonde Hadès.", "titanium and meteorite; way to the black hole and the Hades Probe.", "titanio y meteorito; camino al agujero negro y a la Sonda Hades."),
                Danger = Loc.Pick("pas d'air : emportez de l'oxygène.", "no air: bring oxygen.", "sin aire: lleva oxígeno.") },
            new WikiEntry { Id = 103, Name = "TF Altis", System = Otrera, Biome = Loc.Pick("montagnes jusqu'à 8 km, neige", "mountains up to 8 km, snow", "montañas de hasta 8 km, nieve"), Gravity = Loc.Pick("1,1 g", "1.1 g", "1,1 g"), Air = Loc.Pick("respirable", "breathable", "respirable"),
                Resources = L(new[] { "titane", "bois (pins, bouleaux)" }, new[] { "titanium", "wood (pines, birches)" }, new[] { "titanio", "madera (pinos, abedules)" }),
                Places = L(new[] { "ruines et village en granit" }, new[] { "granite ruins and village" }, new[] { "ruinas y pueblo de granito" }),
                Use = Loc.Pick("titane et bois.", "titanium and wood.", "titanio y madera.") },
            new WikiEntry { Id = 104, Name = "TF Verdance", System = Otrera, Biome = Loc.Pick("tempérée, lacs, forêts", "temperate, lakes, forests", "templado, lagos, bosques"), Gravity = Loc.Pick("0,95 g", "0.95 g", "0,95 g"), Air = Loc.Pick("respirable", "breathable", "respirable"),
                Resources = L(new[] { "ambre", "bois (chênes, bouleaux, sapins)" }, new[] { "amber", "wood (oaks, birches, firs)" }, new[] { "ámbar", "madera (robles, abedules, abetos)" }),
                Places = L(new[] { "ruines moussues", "villages", "épaves, cargos", "Station Kepler" }, new[] { "mossy ruins", "villages", "wrecks, freighters", "Kepler Station" }, new[] { "ruinas cubiertas de musgo", "pueblos", "restos, cargueros", "Estación Kepler" }),
                Use = Loc.Pick("première destination : ambre, bois, coffres de village.", "first destination: amber, wood, village chests.", "primer destino: ámbar, madera, cofres de los pueblos.") },
            new WikiEntry { Id = 105, Name = "TF Glacia", System = Otrera, Biome = Loc.Pick("glaciaire, montagnes de 9 km, anneau", "glacial, 9 km mountains, ring", "glacial, montañas de 9 km, anillo"), Gravity = "1 g", Air = Loc.Pick("respirable", "breathable", "respirable"),
                Resources = L(new[] { "cryonite", "titane", "astéroïdes" }, new[] { "cryonite", "titanium", "asteroids" }, new[] { "criónita", "titanio", "asteroides" }),
                Places = L(new[] { "ruines de glace", "épaves, cargos", "trou noir à 4 100 km" }, new[] { "ice ruins", "wrecks, freighters", "black hole 4,100 km away" }, new[] { "ruinas de hielo", "restos, cargueros", "agujero negro a 4.100 km" }),
                Use = Loc.Pick("cryonite : batterie à cryonite, condenseur, champ de force.", "cryonite: cryonite battery, condenser, force field.", "criónita: batería de criónita, condensador, campo de fuerza.") },
            new WikiEntry { Id = 106, Name = "TF Nexora", System = Otrera, Biome = Loc.Pick("futuriste, cité abandonnée", "futuristic, abandoned city", "futurista, ciudad abandonada"), Gravity = Loc.Pick("0,85 g", "0.85 g", "0,85 g"), Air = Loc.Pick("respirable", "breathable", "respirable"),
                Resources = L(new[] { "noyau quantique", "alliage nexorien", "quartz" }, new[] { "quantum core", "Nexoran alloy", "quartz" }, new[] { "núcleo cuántico", "aleación nexoriana", "cuarzo" }),
                Places = L(new[] { "cité : gratte-ciel, tours, dômes, places" }, new[] { "city: skyscrapers, towers, domes, squares" }, new[] { "ciudad: rascacielos, torres, cúpulas, plazas" }),
                Use = Loc.Pick("noyaux quantiques et alliage dans les gratte-ciel.", "quantum cores and alloy in the skyscrapers.", "núcleos cuánticos y aleación en los rascacielos.") },
            new WikiEntry { Id = 107, Name = "TF Sylvara", System = Otrera, Biome = Loc.Pick("jungle, arbres géants, fleurs", "jungle, giant trees, flowers", "jungla, árboles gigantes, flores"), Gravity = Loc.Pick("1,05 g", "1.05 g", "1,05 g"), Air = Loc.Pick("respirable", "breathable", "respirable"),
                Resources = L(new[] { "ambre", "bois en quantité" }, new[] { "amber", "plenty of wood" }, new[] { "ámbar", "madera en abundancia" }),
                Places = L(new[] { "temples et villages moussus", "épaves, cargos" }, new[] { "mossy temples and villages", "wrecks, freighters" }, new[] { "templos y pueblos cubiertos de musgo", "restos, cargueros" }),
                Use = Loc.Pick("bois, ambre, artefacts des temples.", "wood, amber, temple artifacts.", "madera, ámbar, artefactos de los templos.") },
            new WikiEntry { Id = 111, Name = "Pyros", System = Erebos, Biome = Loc.Pick("volcanique : basalte noir, lacs de lave, arbres calcinés", "volcanic: black basalt, lava lakes, charred trees", "volcánico: basalto negro, lagos de lava, árboles calcinados"), Gravity = Loc.Pick("1,2 g", "1.2 g", "1,2 g"), Air = Loc.Pick("toxique", "toxic", "tóxico"),
                Resources = L(new[] { "météorite (beaucoup)", "titane", "uranium" }, new[] { "meteorite (lots)", "titanium", "uranium" }, new[] { "meteorito (mucho)", "titanio", "uranio" }),
                Places = L(new[] { "ruines de forge en basalte", "village", "cargos", "trou noir l'Œil d'Érèbe" }, new[] { "basalt forge ruins", "village", "freighters", "black hole the Eye of Erebus" }, new[] { "ruinas de forja de basalto", "pueblo", "cargueros", "agujero negro el Ojo de Érebo" }),
                Use = Loc.Pick("la meilleure source de météorite ; près du trou noir, le collecteur de matière noire va cinq fois plus vite.", "the best meteorite source; near the black hole the dark matter collector runs five times faster.", "la mejor fuente de meteorito; cerca del agujero negro el colector de materia oscura va cinco veces más rápido."),
                Danger = Loc.Pick("air irrespirable : oxygène obligatoire.", "unbreathable air: oxygen required.", "aire irrespirable: oxígeno obligatorio.") },
            new WikiEntry { Id = 112, Name = "Thalassa", System = Erebos, Biome = Loc.Pick("océanique : îles de sable blanc, palmiers", "oceanic: white sand islands, palm trees", "oceánico: islas de arena blanca, palmeras"), Gravity = Loc.Pick("0,9 g", "0.9 g", "0,9 g"), Air = Loc.Pick("respirable", "breathable", "respirable"),
                Resources = L(new[] { "quartz", "ambre", "bois de palmier" }, new[] { "quartz", "amber", "palm wood" }, new[] { "cuarzo", "ámbar", "madera de palmera" }),
                Places = L(new[] { "villages et ruines en grès sur les îles", "épaves", "épave du Vagabond (en orbite)" }, new[] { "sandstone villages and ruins on the islands", "wrecks", "wreck of the Wanderer (in orbit)" }, new[] { "pueblos y ruinas de arenisca en las islas", "restos", "restos del Vagabundo (en órbita)" }),
                Use = Loc.Pick("base tranquille dans Érèbe : air respirable, bois et quartz.", "quiet base in Erebus: breathable air, wood and quartz.", "base tranquila en Érebo: aire respirable, madera y cuarzo.") },
            new WikiEntry { Id = 113, Name = "Mycélia", System = Erebos, Biome = Loc.Pick("fongique : champignons géants lumineux, mousse violette", "fungal: giant glowing mushrooms, purple moss", "fúngico: setas gigantes luminosas, musgo morado"), Gravity = Loc.Pick("0,8 g", "0.8 g", "0,8 g"), Air = Loc.Pick("toxique (spores)", "toxic (spores)", "tóxico (esporas)"),
                Resources = L(new[] { "météorite", "cryonite" }, new[] { "meteorite", "cryonite" }, new[] { "meteorito", "criónita" }),
                Places = L(new[] { "ruines moussues", "cargos" }, new[] { "mossy ruins", "freighters" }, new[] { "ruinas cubiertas de musgo", "cargueros" }),
                Use = Loc.Pick("météorite et cryonite ; forêts de champignons à couper pour le bois.", "meteorite and cryonite; mushroom forests to cut for wood.", "meteorito y criónita; bosques de setas para cortar madera."),
                Danger = Loc.Pick("spores toxiques : oxygène obligatoire.", "toxic spores: oxygen required.", "esporas tóxicas: oxígeno obligatorio.") },
            new WikiEntry { Id = 114, Name = "Cristallis", System = Erebos, Biome = Loc.Pick("cristalline : plaines de sel, crêtes, anneau de cristal", "crystalline: salt plains, ridges, crystal ring", "cristalino: llanuras de sal, crestas, anillo de cristal"), Gravity = Loc.Pick("0,6 g", "0.6 g", "0,6 g"), Air = Loc.Pick("trop mince", "too thin", "demasiado tenue"),
                Resources = L(new[] { "quartz (beaucoup)", "cryonite", "météorite", "astéroïdes de cristal" }, new[] { "quartz (lots)", "cryonite", "meteorite", "crystal asteroids" }, new[] { "cuarzo (mucho)", "criónita", "meteorito", "asteroides de cristal" }),
                Places = L(new[] { "ruines de glace", "épaves", "épave du Léviathan (en orbite)" }, new[] { "ice ruins", "wrecks", "wreck of the Leviathan (in orbit)" }, new[] { "ruinas de hielo", "restos", "restos del Leviatán (en órbita)" }),
                Use = Loc.Pick("quartz et cryonite en masse ; faible gravité.", "quartz and cryonite in bulk; low gravity.", "cuarzo y criónita en abundancia; gravedad baja."),
                Danger = Loc.Pick("air trop mince : oxygène obligatoire.", "air too thin: oxygen required.", "aire demasiado tenue: oxígeno obligatorio.") },
            new WikiEntry { Id = 115, Name = "Bastion Écarlate", System = Erebos, Biome = Loc.Pick("artificielle : plaines de blindage, bassins rouges, forteresses pirates", "artificial: armor plains, red pools, pirate fortresses", "artificial: llanuras de blindaje, charcas rojas, fortalezas piratas"), Gravity = Loc.Pick("1,3 g", "1.3 g", "1,3 g"), Air = Loc.Pick("aucun", "none", "ninguno"),
                Resources = L(new[] { "uranium", "météorite", "butin des bases pirates" }, new[] { "uranium", "meteorite", "pirate base loot" }, new[] { "uranio", "meteorito", "botín de las bases piratas" }),
                Places = L(new[] { "forteresses, tourelles, remparts", "3 bases pirates en orbite", "anneau de débris" }, new[] { "fortresses, turrets, ramparts", "3 pirate bases in orbit", "debris ring" }, new[] { "fortalezas, torretas, murallas", "3 bases piratas en órbita", "anillo de escombros" }),
                Use = Loc.Pick("le repaire des pirates : bases à détruire et à piller.", "the pirates' lair: bases to destroy and loot.", "la guarida de los piratas: bases que destruir y saquear."),
                Danger = Loc.Pick("pirates très nombreux (attaques 4 fois plus fréquentes), bases armées ; pas d'air.", "many pirates (attacks 4 times as often), armed bases; no air.", "muchos piratas (ataques 4 veces más frecuentes), bases armadas; sin aire.") },
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

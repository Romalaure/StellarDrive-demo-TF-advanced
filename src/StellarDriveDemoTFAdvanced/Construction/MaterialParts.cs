using System;
using System.Linq;
using Ships.Interface.Settings;
using StellarDriveDemoTFAdvanced.Common;
using StellarDriveDemoTFAdvanced.Parts;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace StellarDriveDemoTFAdvanced.Construction
{
    /// <summary>
    /// Construction sets in the game's three building materials: wood, glass and iron, and
    /// furniture. The game's hull already has floors, walls and slopes in each; these add the shapes
    /// it lacks (posts, plank floors and partitions, railings, lattices, framed windows, glass
    /// floors and balustrades, gratings, trusses, columns, gussets) and furnish rooms (sofa,
    /// armchair, chair, stool, tables, desk, rug, bookcase, plant). Their models are made in Blender
    /// (tools/blender/construction.py: real wood grain, satin steel, the hull's glass, fabrics); the
    /// shapes built here are the fallback. Stateless placed parts with the game's box collision;
    /// +Y points out of the surface. Only call when SDModKit is loaded.
    /// </summary>
    internal static class MaterialParts
    {
        private const string Donor = "SignalDisplay";

        internal sealed class Model
        {
            public readonly MeshBuilder Wood = Planar(new Color(0.78f, 0.62f, 0.45f), 0);
            public readonly MeshBuilder Iron = Planar(new Color(0.62f, 0.64f, 0.67f), 1);
            public readonly MeshBuilder DarkIron = Planar(new Color(0.3f, 0.31f, 0.33f), 1);
            public readonly MeshBuilder Glass = new MeshBuilder();
        }

        internal sealed class Spec
        {
            public ushort Id;
            public string Label;
            public string Description;
            public float Mass;
            public (uint Item, int Count)[] Cost;
            public Vector3 Size;
            public Action<Model> Build;
            /// <summary>The set: 0 wood, 1 glass, 2 iron, 3 furniture, 4 stairs, 5 more furniture, 6 futuristic furniture, 7 bathroom, 8 hangar, 9 wall dressing (build menu row and research).</summary>
            public int Set;
            /// <summary>Stairs: 1 a straight flight climbing toward +z (rise Size.y over run Size.z), 2 a spiral (270°, around y), 4 a flight's railing (along a run of Size.z).</summary>
            public int Stairs;
            /// <summary>The game part it is made from, when it keeps that part's work (a chest's storage, the star map); else a blank Signal Display.</summary>
            public string Donor;
        }

        private const uint Wood = 110, Glass = 102, Iron = 101, Copper = 112;

        public static readonly Spec[] All =
        {
            // Wood
            new Spec { Id = 7341, Set = 0, Label = Loc.Pick("Poteau en bois 2 m", "Wooden post 2 m", "Poste de madera 2 m"), Mass = 8f, Cost = new[] { (Wood, 2) }, Size = new Vector3(0.18f, 2f, 0.18f),
                Description = Loc.Pick("Poteau équarri de 2 m avec semelle et chapeau, pour les charpentes et les pontons. Sort perpendiculairement à la surface.", "Squared 2 m post with a base plate and a cap, for timber frames and jetties. Stands out perpendicular to the surface.", "Poste escuadrado de 2 m con placa de base y remate, para entramados y embarcaderos. Sobresale perpendicular a la superficie."),
                Build = m => WoodPost(m, 2f) },
            new Spec { Id = 7342, Set = 0, Label = Loc.Pick("Plancher en lattes 1 × 1", "Slatted floor 1 × 1", "Suelo de listones 1 × 1"), Mass = 6f, Cost = new[] { (Wood, 2) }, Size = new Vector3(1f, 0.06f, 1f),
                Description = Loc.Pick("Six lattes clouées sur deux traverses : un plancher en bois d'un mètre carré, à poser au sol, au mur ou au plafond.", "Six slats nailed on two crosspieces: one square metre of wooden floor, for the ground, a wall or a ceiling.", "Seis listones clavados sobre dos travesaños: un metro cuadrado de suelo de madera, para el suelo, una pared o un techo."),
                Build = m => Planks(m, 1f, 1f, 6) },
            new Spec { Id = 7343, Set = 0, Label = Loc.Pick("Cloison en planches 1 × 2", "Plank wall 1 × 2", "Pared de tablas 1 × 2"), Mass = 10f, Cost = new[] { (Wood, 3) }, Size = new Vector3(1f, 2f, 0.06f),
                Description = Loc.Pick("Planches verticales sur deux traverses, 1 m de large et 2 m de haut : cloisons, enclos, cabanes.", "Upright planks on two crosspieces, 1 m wide and 2 m high: walls, pens, huts.", "Tablas verticales sobre dos travesaños, 1 m de ancho y 2 m de alto: paredes, corrales, cabañas."),
                Build = m => PlankWall(m, 1f, 2f) },
            new Spec { Id = 7344, Set = 0, Label = Loc.Pick("Rambarde en bois 1 m", "Wooden railing 1 m", "Barandilla de madera 1 m"), Mass = 4f, Cost = new[] { (Wood, 1) }, Size = new Vector3(1f, 1f, 0.08f),
                Description = Loc.Pick("Deux poteaux, une main courante et deux lisses : garde-corps de 1 m pour passerelles et balcons.", "Two posts, a handrail and two rails: a 1 m guard rail for walkways and balconies.", "Dos postes, un pasamanos y dos travesaños: una barandilla de 1 m para pasarelas y balcones."),
                Build = m => WoodRailing(m, 1f) },
            new Spec { Id = 7345, Set = 0, Label = Loc.Pick("Treillis en bois 1 × 1", "Wooden lattice 1 × 1", "Celosía de madera 1 × 1"), Mass = 3f, Cost = new[] { (Wood, 1) }, Size = new Vector3(1f, 0.05f, 1f),
                Description = Loc.Pick("Cadre de lattes croisées en diagonale. Le vide est visuel : la collision reste une boîte pleine.", "A frame of diagonally crossed slats. The gaps are only visual: the collision stays a solid box.", "Un marco de listones cruzados en diagonal. Los huecos son solo visuales: la colisión sigue siendo un bloque macizo."),
                Build = m => Lattice(m, 1f) },
            new Spec { Id = 7346, Set = 0, Label = Loc.Pick("Étagère en bois 1 m", "Wooden shelf 1 m", "Estante de madera 1 m"), Mass = 5f, Cost = new[] { (Wood, 2) }, Size = new Vector3(1f, 0.9f, 0.32f),
                Description = Loc.Pick("Étagère murale à trois niveaux. Décorative : rien ne s'y range.", "Three-level wall shelf. Decorative: nothing can be stored on it.", "Estante de pared de tres niveles. Decorativo: no se puede guardar nada en él."),
                Build = m => Shelf(m) },
            // Glass
            new Spec { Id = 7351, Set = 1, Label = Loc.Pick("Vitre 1 × 1", "Glass pane 1 × 1", "Cristal 1 × 1"), Mass = 6f, Cost = new[] { (Glass, 2), (Iron, 1) }, Size = new Vector3(1f, 1f, 0.06f),
                Description = Loc.Pick("Vitre dans un cadre d'acier, 1 m de côté, pour fenêtres, hublots carrés et cloisons.", "A glass pane in a steel frame, 1 m square, for windows, square portholes and partitions.", "Un cristal en un marco de acero, de 1 m cuadrado, para ventanas, ojos de buey cuadrados y tabiques."),
                Build = m => Window(m, 1f, 1f, 1) },
            new Spec { Id = 7352, Set = 1, Label = Loc.Pick("Baie vitrée 2 × 2", "Picture window 2 × 2", "Ventanal 2 × 2"), Mass = 20f, Cost = new[] { (Glass, 6), (Iron, 2) }, Size = new Vector3(2f, 2f, 0.06f),
                Description = Loc.Pick("Grande baie de 2 × 2 m en quatre carreaux : vue panoramique pour les postes de pilotage et les stations.", "Big 2 × 2 m window in four panes: a panoramic view for cockpits and stations.", "Gran ventana de 2 × 2 m en cuatro paneles: una vista panorámica para cabinas y estaciones."),
                Build = m => Window(m, 2f, 2f, 2) },
            new Spec { Id = 7353, Set = 1, Label = Loc.Pick("Dalle de verre 1 × 1", "Glass floor tile 1 × 1", "Baldosa de cristal 1 × 1"), Mass = 10f, Cost = new[] { (Glass, 3), (Iron, 1) }, Size = new Vector3(1f, 0.08f, 1f),
                Description = Loc.Pick("Dalle épaisse dans un cadre d'acier, on marche dessus : sols vitrés, puits de lumière.", "A thick tile in a steel frame that you can walk on: glass floors, light wells.", "Una baldosa gruesa en un marco de acero sobre la que se puede caminar: suelos de cristal, pozos de luz."),
                Build = m => GlassFloor(m, 1f) },
            new Spec { Id = 7354, Set = 1, Label = Loc.Pick("Garde-corps vitré 1 m", "Glass railing 1 m", "Barandilla de cristal 1 m"), Mass = 7f, Cost = new[] { (Glass, 2), (Iron, 1) }, Size = new Vector3(1f, 1f, 0.06f),
                Description = Loc.Pick("Panneau de verre tenu par deux pinces et une main courante en acier.", "A glass panel held by two clamps and a steel handrail.", "Un panel de cristal sujeto por dos pinzas y un pasamanos de acero."),
                Build = m => GlassRailing(m, 1f) },
            new Spec { Id = 7355, Set = 1, Label = Loc.Pick("Verrière 1 × 1", "Skylight 1 × 1", "Claraboya 1 × 1"), Mass = 8f, Cost = new[] { (Glass, 2), (Iron, 2) }, Size = new Vector3(1f, 0.4f, 1f),
                Description = Loc.Pick("Petite pyramide vitrée à poser sur un toit ou un plancher.", "Small glass pyramid for a roof or a floor.", "Pequeña pirámide de cristal para un tejado o un suelo."),
                Build = m => Skylight(m, 1f, 0.4f) },
            new Spec { Id = 7356, Set = 1, Label = Loc.Pick("Cloison vitrée 1 × 2", "Glass partition 1 × 2", "Tabique de cristal 1 × 2"), Mass = 10f, Cost = new[] { (Glass, 3), (Iron, 1) }, Size = new Vector3(1f, 2f, 0.06f),
                Description = Loc.Pick("Cloison vitrée toute hauteur avec traverse à mi-hauteur.", "Full-height glass partition with a crossbar at mid-height.", "Tabique de cristal de altura completa con un travesaño a media altura."),
                Build = m => Window(m, 1f, 2f, 1, rows: 2) },
            // Iron
            new Spec { Id = 7361, Set = 2, Label = Loc.Pick("Caillebotis 1 × 1", "Grating 1 × 1", "Rejilla 1 × 1"), Mass = 8f, Cost = new[] { (Iron, 2) }, Size = new Vector3(1f, 0.05f, 1f),
                Description = Loc.Pick("Grille de sol en acier : passerelles, coursives, plateformes. Le vide est visuel, on marche dessus.", "Steel floor grating: walkways, gangways, platforms. The gaps are only visual, you walk on it.", "Rejilla de suelo de acero: pasarelas, corredores, plataformas. Los huecos son solo visuales, se camina sobre ella."),
                Build = m => Grating(m, 1f) },
            new Spec { Id = 7362, Set = 2, Label = Loc.Pick("Garde-corps en acier 1 m", "Steel railing 1 m", "Barandilla de acero 1 m"), Mass = 5f, Cost = new[] { (Iron, 1) }, Size = new Vector3(1f, 1f, 0.06f),
                Description = Loc.Pick("Garde-corps industriel : deux montants, main courante, lisse et plinthe.", "Industrial railing: two posts, a handrail, a mid rail and a kick plate.", "Barandilla industrial: dos postes, un pasamanos, un travesaño intermedio y un zócalo."),
                Build = m => SteelRailing(m, 1f) },
            new Spec { Id = 7363, Set = 2, Label = Loc.Pick("Garde-corps en acier 2 m", "Steel railing 2 m", "Barandilla de acero 2 m"), Mass = 9f, Cost = new[] { (Iron, 2) }, Size = new Vector3(2f, 1f, 0.06f),
                Description = Loc.Pick("Garde-corps industriel de 2 m.", "2 m industrial railing.", "Barandilla industrial de 2 m."),
                Build = m => SteelRailing(m, 2f) },
            new Spec { Id = 7364, Set = 2, Label = Loc.Pick("Treillis métallique 2 m", "Steel truss 2 m", "Celosía de acero 2 m"), Mass = 14f, Cost = new[] { (Iron, 3) }, Size = new Vector3(0.36f, 2f, 0.32f),
                Description = Loc.Pick("Poutre en treillis triangulaire : trois membrures et des diagonales. Légère et rigide, pour les grues, mâts et ponts.", "Triangular truss beam: three chords and diagonals. Light and stiff, for cranes, masts and bridges.", "Viga de celosía triangular: tres cordones y diagonales. Ligera y rígida, para grúas, mástiles y puentes."),
                Build = m => Truss(m, 2f) },
            new Spec { Id = 7365, Set = 2, Label = Loc.Pick("Colonne rivetée 2,5 m", "Riveted column 2.5 m", "Columna remachada 2,5 m"), Mass = 30f, Cost = new[] { (Iron, 4) }, Size = new Vector3(0.4f, 2.5f, 0.4f),
                Description = Loc.Pick("Colonne ronde avec base et chapiteau rivetés, pour soutenir les grands volumes.", "Round column with a riveted base and capital, to hold up large spaces.", "Columna redonda con base y capitel remachados, para sostener grandes espacios."),
                Build = m => Column(m, 2.5f) },
            new Spec { Id = 7366, Set = 2, Label = Loc.Pick("Gousset d'angle 1 × 1", "Corner gusset 1 × 1", "Escuadra de esquina 1 × 1"), Mass = 6f, Cost = new[] { (Iron, 1) }, Size = new Vector3(0.04f, 1f, 1f),
                Description = Loc.Pick("Plaque triangulaire boulonnée pour renforcer un angle. La collision est la boîte du gousset.", "Bolted triangular plate to brace a corner. The collision is the gusset's box.", "Placa triangular atornillada para reforzar una esquina. La colisión es la caja de la escuadra."),
                Build = m => Gusset(m, 1f) },
            new Spec { Id = 7367, Set = 2, Label = Loc.Pick("Tôle perforée 1 × 1", "Perforated sheet 1 × 1", "Chapa perforada 1 × 1"), Mass = 7f, Cost = new[] { (Iron, 2) }, Size = new Vector3(1f, 0.03f, 1f),
                Description = Loc.Pick("Tôle percée de trous ronds, cadre plié : bardage, cloisons techniques.", "Steel sheet punched with round holes, folded frame: cladding, service partitions.", "Chapa de acero perforada con agujeros redondos, marco plegado: revestimientos, tabiques técnicos."),
                Build = m => Perforated(m, 1f) },
            // Furniture (on floors, or beside other placed parts)
            new Spec { Id = 7440, Set = 3, Label = Loc.Pick("Canapé", "Sofa", "Sofá"), Mass = 40f, Cost = new[] { (Wood, 6), (Iron, 2) }, Size = new Vector3(2f, 0.85f, 0.9f),
                Description = Loc.Pick("Canapé trois places à coussins. Peins-le pour changer la couleur du tissu.", "Three-seat sofa with cushions. Paint it to change the fabric's color.", "Sofá de tres plazas con cojines. Píntalo para cambiar el color de la tela."), Build = m => Furniture(m, 2f, 0.85f, 0.9f) },
            new Spec { Id = 7441, Set = 3, Label = Loc.Pick("Fauteuil", "Armchair", "Sillón"), Mass = 18f, Cost = new[] { (Wood, 3), (Iron, 1) }, Size = new Vector3(0.9f, 0.85f, 0.9f),
                Description = Loc.Pick("Fauteuil assorti au canapé.", "An armchair matching the sofa.", "Un sillón a juego con el sofá."), Build = m => Furniture(m, 0.9f, 0.85f, 0.9f) },
            new Spec { Id = 7442, Set = 3, Label = Loc.Pick("Chaise", "Chair", "Silla"), Mass = 5f, Cost = new[] { (Wood, 2) }, Size = new Vector3(0.45f, 0.9f, 0.5f),
                Description = Loc.Pick("Chaise en bois à dossier à lattes et assise rembourrée.", "Wooden chair with a slatted back and a padded seat.", "Silla de madera con respaldo de listones y asiento acolchado."), Build = m => Furniture(m, 0.45f, 0.9f, 0.5f) },
            new Spec { Id = 7443, Set = 3, Label = Loc.Pick("Tabouret", "Stool", "Taburete"), Mass = 3f, Cost = new[] { (Wood, 1), (Iron, 1) }, Size = new Vector3(0.4f, 0.7f, 0.4f),
                Description = Loc.Pick("Tabouret de bar à trois pieds d'acier et assise ronde en bois.", "Bar stool on three steel legs with a round wooden seat.", "Taburete de bar de tres patas de acero con asiento redondo de madera."), Build = m => Furniture(m, 0.4f, 0.7f, 0.4f) },
            new Spec { Id = 7444, Set = 3, Label = Loc.Pick("Table", "Table", "Mesa"), Mass = 25f, Cost = new[] { (Wood, 4), (Iron, 2) }, Size = new Vector3(1.6f, 0.76f, 0.9f),
                Description = Loc.Pick("Table de 1,6 m, plateau en bois sur pieds d'acier.", "1.6 m table, a wooden top on steel legs.", "Mesa de 1,6 m, tablero de madera sobre patas de acero."), Build = m => Furniture(m, 1.6f, 0.76f, 0.9f) },
            new Spec { Id = 7445, Set = 3, Label = Loc.Pick("Table basse", "Coffee table", "Mesa baja"), Mass = 15f, Cost = new[] { (Glass, 2), (Iron, 2), (Wood, 1) }, Size = new Vector3(1.1f, 0.42f, 0.6f),
                Description = Loc.Pick("Table basse : plateau de verre sur cadre d'acier, tablette en bois dessous.", "Coffee table: a glass top on a steel frame, a wooden shelf under it.", "Mesa baja: tablero de cristal sobre un marco de acero, una balda de madera debajo."), Build = m => Furniture(m, 1.1f, 0.42f, 0.6f) },
            new Spec { Id = 7446, Set = 3, Label = Loc.Pick("Bureau", "Desk", "Escritorio"), Mass = 30f, Cost = new[] { (Wood, 5), (Iron, 2) }, Size = new Vector3(1.4f, 0.76f, 0.7f),
                Description = Loc.Pick("Bureau de 1,4 m avec caisson à trois tiroirs (décoratifs).", "1.4 m desk with a three-drawer unit (decorative).", "Escritorio de 1,4 m con cajonera de tres cajones (decorativos)."), Build = m => Furniture(m, 1.4f, 0.76f, 0.7f) },
            new Spec { Id = 7447, Set = 3, Label = Loc.Pick("Tapis", "Rug", "Alfombra"), Mass = 6f, Cost = new[] { (Wood, 2) }, Size = new Vector3(2f, 0.012f, 3f),
                Description = Loc.Pick("Tapis tissé de 2 × 3 m à bordure et franges. Peins-le pour changer sa couleur.", "A 2 × 3 m woven rug with a border and fringes. Paint it to change its color.", "Una alfombra tejida de 2 × 3 m con cenefa y flecos. Píntala para cambiar su color."), Build = m => Furniture(m, 2f, 0.012f, 3f) },
            new Spec { Id = 7448, Set = 3, Label = Loc.Pick("Bibliothèque", "Bookcase", "Librería"), Mass = 35f, Cost = new[] { (Wood, 6) }, Size = new Vector3(1f, 2f, 0.35f),
                Description = Loc.Pick("Bibliothèque de 2 m, cinq étagères pleines de livres (décoratifs).", "A 2 m bookcase, five shelves full of books (decorative).", "Una librería de 2 m, cinco baldas llenas de libros (decorativos)."), Build = m => Furniture(m, 1f, 2f, 0.35f) },
            new Spec { Id = 7449, Set = 3, Label = Loc.Pick("Plante en pot", "Potted plant", "Planta en maceta"), Mass = 12f, Cost = new[] { (Wood, 2), (Glass, 1) }, Size = new Vector3(0.5f, 1.2f, 0.5f),
                Description = Loc.Pick("Une plante verte à larges feuilles dans un pot en céramique.", "A green plant with broad leaves in a ceramic pot.", "Una planta verde de hojas anchas en una maceta de cerámica."), Build = m => Furniture(m, 0.5f, 1.2f, 0.5f) },
            // More furniture
            new Spec { Id = 8200, Set = 5, Label = Loc.Pick("Lit", "Bed", "Cama"), Mass = 35f, Cost = new[] { (Wood, 6), (Iron, 1) }, Size = new Vector3(1f, 0.9f, 2.1f),
                Description = Loc.Pick("Lit d'une place : cadre en bois, matelas, drap, couverture et oreiller. T pour dormir et passer la nuit (pas en vol).", "Single bed: a wooden frame, a mattress, a sheet, a blanket and a pillow. T to sleep and pass the night (not in flight).", "Cama individual: marco de madera, colchón, sábana, manta y almohada. T para dormir y pasar la noche (no en vuelo)."), Build = m => Furniture(m, 1f, 0.55f, 2.1f) },
            new Spec { Id = 8201, Set = 5, Label = Loc.Pick("Lits superposés", "Bunk bed", "Litera"), Mass = 45f, Cost = new[] { (Iron, 5), (Wood, 2) }, Size = new Vector3(1f, 1.8f, 2.2f),
                Description = Loc.Pick("Deux couchettes sur un cadre d'acier avec échelle et garde-corps : l'équipage dort à deux sur un mètre carré. T pour dormir.", "Two bunks on a steel frame with a ladder and a guard rail: two crew sleep on one square metre. T to sleep.", "Dos literas sobre un marco de acero con escalera y barandilla: dos tripulantes duermen en un metro cuadrado. T para dormir."), Build = m => Furniture(m, 1f, 1.8f, 2.2f) },
            new Spec { Id = 8202, Set = 5, Label = Loc.Pick("Table de chevet", "Nightstand", "Mesita de noche"), Mass = 8f, Cost = new[] { (Wood, 2), (Copper, 1) }, Size = new Vector3(0.5f, 0.65f, 0.45f),
                Description = Loc.Pick("Petit meuble à tiroir avec une lampe de chevet.", "A small cabinet with a drawer and a bedside lamp.", "Un mueble pequeño con cajón y una lámpara de noche."), Build = m => Furniture(m, 0.5f, 0.65f, 0.45f) },
            new Spec { Id = 8203, Set = 5, Label = Loc.Pick("Armoire", "Wardrobe", "Armario"), Mass = 50f, Cost = new[] { (Wood, 8), (Iron, 1) }, Size = new Vector3(1.24f, 2.1f, 0.62f),
                Description = Loc.Pick("Grande armoire à deux portes (décorative : rien ne s'y range).", "Big two-door wardrobe (decorative: nothing can be stored in it).", "Gran armario de dos puertas (decorativo: no se puede guardar nada)."), Build = m => Furniture(m, 1.24f, 2.1f, 0.62f) },
            new Spec { Id = 8204, Set = 5, Label = Loc.Pick("Commode", "Dresser", "Cómoda"), Mass = 30f, Cost = new[] { (Wood, 5), (Iron, 1) }, Size = new Vector3(1.22f, 0.85f, 0.52f),
                Description = Loc.Pick("Commode à six tiroirs (décoratifs).", "A chest of six drawers (decorative).", "Una cómoda de seis cajones (decorativos)."), Build = m => Furniture(m, 1.22f, 0.85f, 0.52f) },
            new Spec { Id = 8205, Set = 5, Label = Loc.Pick("Plan de travail de cuisine", "Kitchen counter", "Encimera de cocina"), Mass = 40f, Cost = new[] { (Iron, 4), (Wood, 3), (Copper, 1) }, Size = new Vector3(1.22f, 1.06f, 0.62f),
                Description = Loc.Pick("Meuble de cuisine : placards, plan en inox, évier et robinet. Mets-en plusieurs côte à côte.", "Kitchen unit: cupboards, a steel worktop, a sink and a tap. Put several side by side.", "Mueble de cocina: armarios, encimera de acero, fregadero y grifo. Pon varios uno al lado del otro."), Build = m => Furniture(m, 1.22f, 1.06f, 0.62f) },
            new Spec { Id = 8206, Set = 5, Label = Loc.Pick("Réfrigérateur", "Fridge", "Frigorífico"), Mass = 60f, Cost = new[] { (Iron, 6), (Copper, 2) }, Size = new Vector3(0.7f, 1.9f, 0.72f),
                Description = Loc.Pick("Réfrigérateur-congélateur à deux portes (décoratif).", "Two-door fridge-freezer (decorative).", "Frigorífico combi de dos puertas (decorativo)."), Build = m => Furniture(m, 0.7f, 1.9f, 0.72f) },
            new Spec { Id = 8207, Set = 5, Label = Loc.Pick("Meuble TV", "TV unit", "Mueble de TV"), Mass = 30f, Cost = new[] { (Wood, 4), (Glass, 2), (Copper, 2) }, Size = new Vector3(1.62f, 1.32f, 0.47f),
                Description = Loc.Pick("Meuble bas avec un grand écran plat et une barre de son.", "A low cabinet with a big flat screen and a sound bar.", "Un mueble bajo con una gran pantalla plana y una barra de sonido."), Build = m => Furniture(m, 1.62f, 1.32f, 0.47f) },
            new Spec { Id = 8208, Set = 5, Label = Loc.Pick("Banc", "Bench", "Banco"), Mass = 15f, Cost = new[] { (Wood, 3), (Iron, 1) }, Size = new Vector3(1.6f, 0.46f, 0.45f),
                Description = Loc.Pick("Banc à lattes sur pieds d'acier, trois places assises.", "A slatted bench on steel legs, three seats.", "Un banco de listones sobre patas de acero, tres plazas."), Build = m => Furniture(m, 1.6f, 0.46f, 0.45f) },
            new Spec { Id = 8209, Set = 5, Label = Loc.Pick("Chaise de bureau", "Office chair", "Silla de oficina"), Mass = 9f, Cost = new[] { (Iron, 2), (Wood, 1) }, Size = new Vector3(0.6f, 1.12f, 0.6f),
                Description = Loc.Pick("Fauteuil de bureau sur roulettes, rembourré, avec accoudoirs. On peut s'y asseoir.", "A padded office chair on casters with armrests. You can sit on it.", "Una silla de oficina acolchada con ruedas y reposabrazos. Te puedes sentar."), Build = m => Furniture(m, 0.6f, 1.12f, 0.6f) },
            new Spec { Id = 8210, Set = 5, Label = Loc.Pick("Table ronde", "Round table", "Mesa redonda"), Mass = 18f, Cost = new[] { (Wood, 3), (Iron, 1) }, Size = new Vector3(1f, 0.75f, 1f),
                Description = Loc.Pick("Table ronde d'un mètre sur pied central, une petite plante dessus.", "A 1 m round table on a pedestal, a small plant on it.", "Una mesa redonda de 1 m sobre pie central, con una plantita."), Build = m => Furniture(m, 1f, 0.75f, 1f) },
            new Spec { Id = 8211, Set = 5, Label = Loc.Pick("Jardinière", "Planter box", "Jardinera"), Mass = 25f, Cost = new[] { (Wood, 3), (Glass, 1) }, Size = new Vector3(1.22f, 0.75f, 0.42f),
                Description = Loc.Pick("Longue jardinière en bois plantée d'une rangée de plantes vertes.", "A long wooden planter with a row of green plants.", "Una jardinera larga de madera con una fila de plantas verdes."), Build = m => Furniture(m, 1.22f, 0.75f, 0.42f) },
            // Futuristic furniture (a ship's rooms: science, storage, dormitory, galley, office, lounge, mess, sickbay)
            new Spec { Id = 8240, Set = 6, Label = Loc.Pick("Paillasse de laboratoire", "Lab bench", "Mesa de laboratorio"), Mass = 40f, Cost = new[] { (Iron, 4), (Glass, 2), (Copper, 1) }, Size = new Vector3(1.6f, 1.3f, 0.7f),
                Description = Loc.Pick("Paillasse blanche à placards, plan de travail sombre, microscope et fioles lumineuses : la salle scientifique.", "A white bench with cupboards, a dark worktop, a microscope and glowing flasks: the science room.", "Mesa blanca con armarios, encimera oscura, microscopio y matraces luminosos: la sala científica."), Build = m => Furniture(m, 1.6f, 0.95f, 0.7f) },
            new Spec { Id = 8241, Set = 6, Label = Loc.Pick("Cuve à spécimen", "Specimen tank", "Tanque de especímenes"), Mass = 60f, Cost = new[] { (Glass, 6), (Iron, 3), (Copper, 2) }, Size = new Vector3(0.8f, 2.1f, 0.8f),
                Description = Loc.Pick("Un grand tube de verre rempli d'un liquide vert lumineux, sur un socle et sous un chapeau d'acier.", "A tall glass tube filled with a glowing green liquid, on a steel base and under a steel cap.", "Un tubo alto de cristal lleno de un líquido verde luminoso, sobre una base y bajo una tapa de acero."), Build = m => Furniture(m, 0.8f, 2.1f, 0.8f) },
            new Spec { Id = 8242, Set = 6, Label = Loc.Pick("Console d'analyse", "Analysis console", "Consola de análisis"), Mass = 30f, Cost = new[] { (Iron, 3), (Glass, 2), (Copper, 3) }, Size = new Vector3(1.0f, 1.3f, 0.6f),
                Description = Loc.Pick("Un pupitre incliné sur un pied blanc, son écran allumé et un clavier lumineux.", "A slanted desk on a white stand, its screen lit and a glowing keyboard.", "Un pupitre inclinado sobre un pie blanco, con la pantalla encendida y un teclado luminoso."), Build = m => Furniture(m, 1.0f, 1.3f, 0.6f) },
            new Spec { Id = 8243, Set = 6, Label = Loc.Pick("Baie de serveurs", "Server rack", "Rack de servidores"), Mass = 70f, Cost = new[] { (Iron, 6), (Copper, 4), (Glass, 1) }, Size = new Vector3(0.7f, 2.0f, 0.9f),
                Description = Loc.Pick("Une armoire sombre derrière une porte vitrée, pleine de serveurs aux voyants clignotants. Relais radio : elle joue la radio du vaisseau comme une enceinte (T pour ouvrir la radio).", "A dark cabinet behind a glass door, full of servers with blinking lights. Radio relay: it plays the ship's radio like a speaker (T opens the radio).", "Un armario oscuro tras una puerta de cristal, lleno de servidores con luces parpadeantes. Repetidor de radio: suena la radio de la nave como un altavoz (T abre la radio)."), Build = m => Furniture(m, 0.7f, 2.0f, 0.9f) },
            new Spec { Id = 8244, Set = 6, Label = Loc.Pick("Caisse de fret", "Cargo crate", "Caja de carga"), Mass = 30f, Cost = new[] { (Iron, 4) }, Size = new Vector3(1.0f, 1.0f, 1.0f), Donor = "StorageChest",
                Description = Loc.Pick("Une caisse blanche renforcée aux coins, rayée de lumière : la soute. Elle s'ouvre comme un coffre et garde ce qu'on y range.", "A white crate with reinforced corners and light stripes: the hold. It opens like a chest and keeps what you put in.", "Una caja blanca con esquinas reforzadas y franjas de luz: la bodega. Se abre como un cofre y guarda lo que metas."), Build = m => Furniture(m, 1.0f, 1.0f, 1.0f) },
            new Spec { Id = 8245, Set = 6, Label = Loc.Pick("Rayonnage de stockage", "Storage rack", "Estantería de almacén"), Mass = 45f, Cost = new[] { (Iron, 6) }, Size = new Vector3(1.6f, 2.0f, 0.6f),
                Description = Loc.Pick("Quatre étagères d'acier chargées de caisses et de bacs.", "Four steel shelves loaded with crates and bins.", "Cuatro estantes de acero cargados de cajas y cubetas."), Build = m => Furniture(m, 1.6f, 2.0f, 0.6f) },
            new Spec { Id = 8246, Set = 6, Label = Loc.Pick("Bidons sur palette", "Canisters on a pallet", "Bidones en palé"), Mass = 35f, Cost = new[] { (Iron, 4), (Copper, 1) }, Size = new Vector3(1.0f, 1.2f, 1.0f),
                Description = Loc.Pick("Deux grands bidons blancs cerclés de rouge sur une palette d'acier.", "Two big white canisters with red bands on a steel pallet.", "Dos grandes bidones blancos con franjas rojas sobre un palé de acero."), Build = m => Furniture(m, 1.0f, 1.2f, 1.0f) },
            new Spec { Id = 8247, Set = 6, Label = Loc.Pick("Capsule de sommeil", "Sleep pod", "Cápsula de sueño"), Mass = 60f, Cost = new[] { (Iron, 5), (Glass, 3), (Copper, 1) }, Size = new Vector3(1.1f, 1.2f, 2.2f),
                Description = Loc.Pick("Une couchette fermée par une verrière, bordée de lumière : le dortoir. T pour dormir et passer la nuit (pas en vol).", "A bunk closed by a glass canopy, lined with light: the dormitory. T to sleep and pass the night (not in flight).", "Una litera cerrada por una cúpula de cristal, bordeada de luz: el dormitorio. T para dormir y pasar la noche (no en vuelo)."), Build = m => Furniture(m, 1.1f, 1.2f, 2.2f) },
            new Spec { Id = 8248, Set = 6, Label = Loc.Pick("Casiers d'équipage", "Crew lockers", "Taquillas de tripulación"), Mass = 40f, Cost = new[] { (Iron, 5) }, Size = new Vector3(0.9f, 2.0f, 0.5f), Donor = "StorageChest",
                Description = Loc.Pick("Deux casiers hauts à portes ventilées, une ligne de lumière. Ils s'ouvrent comme un coffre et gardent ce qu'on y range.", "Two tall lockers with vented doors and a light line. They open like a chest and keep what you put in.", "Dos taquillas altas con puertas ventiladas y una línea de luz. Se abren como un cofre y guardan lo que metas."), Build = m => Furniture(m, 0.9f, 2.0f, 0.5f) },
            new Spec { Id = 8249, Set = 6, Label = Loc.Pick("Synthétiseur de repas", "Food synthesizer", "Sintetizador de comida"), Mass = 50f, Cost = new[] { (Iron, 4), (Glass, 1), (Copper, 3) }, Size = new Vector3(0.8f, 1.8f, 0.6f),
                Description = Loc.Pick("Un meuble blanc avec un écran de menu et une niche éclairée où sort le plat : la cuisine. T pour prendre un repas : il remet ta santé au maximum (un par minute).", "A white unit with a menu screen and a lit hatch where the meal comes out: the galley. T to take a meal: it brings your health back to full (one a minute).", "Un mueble blanco con una pantalla de menú y un hueco iluminado por donde sale el plato: la cocina. T para tomar una comida: te devuelve toda la salud (una por minuto)."), Build = m => Furniture(m, 0.8f, 1.8f, 0.6f) },
            new Spec { Id = 8250, Set = 6, Label = Loc.Pick("Îlot de cuisine", "Galley island", "Isla de cocina"), Mass = 50f, Cost = new[] { (Iron, 5), (Copper, 2) }, Size = new Vector3(1.6f, 0.95f, 0.8f),
                Description = Loc.Pick("Un îlot blanc au plan d'acier avec deux plaques à induction allumées.", "A white island with a steel top and two lit induction plates.", "Una isla blanca con encimera de acero y dos placas de inducción encendidas."), Build = m => Furniture(m, 1.6f, 0.95f, 0.8f) },
            new Spec { Id = 8251, Set = 6, Label = Loc.Pick("Bureau de commandement", "Command desk", "Escritorio de mando"), Mass = 45f, Cost = new[] { (Iron, 4), (Glass, 3), (Copper, 3) }, Size = new Vector3(1.8f, 1.25f, 0.8f),
                Description = Loc.Pick("Un bureau sombre sur deux flancs blancs, trois écrans allumés : le bureau du capitaine.", "A dark desk on two white sides with three lit screens: the captain's office.", "Un escritorio oscuro sobre dos laterales blancos, con tres pantallas encendidas: el despacho del capitán."), Build = m => Furniture(m, 1.8f, 1.25f, 0.8f) },
            new Spec { Id = 8252, Set = 6, Label = Loc.Pick("Table holographique", "Holo table", "Mesa holográfica"), Mass = 55f, Cost = new[] { (Iron, 4), (Glass, 3), (Copper, 3), (103u, 1) }, Size = new Vector3(1.4f, 1.5f, 1.4f), Donor = "StarMap",
                Description = Loc.Pick("Une table ronde lumineuse qui projette la carte du système au-dessus d'elle : la salle de briefing. Comme la carte stellaire du jeu : vise une planète pour la cibler, ses prises se branchent à l'ordinateur de visée.", "A glowing round table projecting the star system's map above it: the briefing room. Like the game's star map: aim at a planet to target it, its sockets wire to the targeting computer.", "Una mesa redonda luminosa que proyecta el mapa del sistema encima: la sala de reuniones. Como el mapa estelar del juego: apunta a un planeta para marcarlo, sus tomas se conectan al ordenador de puntería."), Build = m => Furniture(m, 1.4f, 1.5f, 1.4f) },
            new Spec { Id = 8253, Set = 6, Label = Loc.Pick("Canapé de salon", "Lounge sofa", "Sofá de salón"), Mass = 40f, Cost = new[] { (Iron, 3), (Wood, 2), (Copper, 1) }, Size = new Vector3(2.0f, 0.85f, 0.9f),
                Description = Loc.Pick("Un canapé blanc aux coussins sombres posé sur une lueur : le salon. Trois places, T pour s'asseoir.", "A white sofa with dark cushions floating on a glow: the lounge. Three seats, T to sit.", "Un sofá blanco con cojines oscuros sobre un resplandor: el salón. Tres plazas, T para sentarse."), Build = m => Furniture(m, 2.0f, 0.85f, 0.9f) },
            new Spec { Id = 8254, Set = 6, Label = Loc.Pick("Table de réfectoire", "Mess table", "Mesa de comedor"), Mass = 55f, Cost = new[] { (Iron, 6), (Copper, 1) }, Size = new Vector3(2.0f, 0.75f, 1.6f),
                Description = Loc.Pick("Une longue table blanche et ses deux bancs, quatre places : le réfectoire, le self. T pour s'asseoir.", "A long white table and its two benches, four seats: the mess hall. T to sit.", "Una mesa larga blanca con dos bancos, cuatro plazas: el comedor. T para sentarse."), Build = m => Furniture(m, 2.0f, 0.75f, 1.6f) },
            new Spec { Id = 8255, Set = 6, Label = Loc.Pick("Distributeur", "Vending machine", "Máquina expendedora"), Mass = 60f, Cost = new[] { (Iron, 5), (Glass, 2), (Copper, 2) }, Size = new Vector3(0.9f, 1.9f, 0.8f),
                Description = Loc.Pick("Un distributeur vitré plein de boîtes colorées, avec un écran : pour le self ou une coursive.", "A glass-fronted vending machine full of colored boxes, with a screen: for the mess or a corridor.", "Una máquina expendedora con frente de cristal llena de cajas de colores y una pantalla: para el comedor o un pasillo."), Build = m => Furniture(m, 0.9f, 1.9f, 0.8f) },
            new Spec { Id = 8256, Set = 6, Label = Loc.Pick("Lit médical", "Medical bed", "Cama médica"), Mass = 50f, Cost = new[] { (Iron, 4), (Glass, 1), (Copper, 3) }, Size = new Vector3(1.0f, 1.4f, 2.1f),
                Description = Loc.Pick("Un lit sur un socle blanc avec un bras et un moniteur allumé : l'infirmerie. T pour s'y reposer et passer la nuit.", "A bed on a white pedestal with an arm and a lit monitor: the sickbay. T to rest and pass the night.", "Una cama sobre un pedestal blanco con un brazo y un monitor encendido: la enfermería. T para descansar y pasar la noche."), Build = m => Furniture(m, 1.0f, 1.4f, 2.1f) },
            // Bathroom (on floors; the mirror on a wall)
            new Spec { Id = 8265, Set = 7, Label = Loc.Pick("Douche", "Shower", "Ducha"), Mass = 45f, Cost = new[] { (Glass, 4), (Iron, 3), (Copper, 2) }, Size = new Vector3(1.0f, 2.2f, 1.0f),
                Description = Loc.Pick("Une cabine vitrée sur un bac, pommeau au plafond et mitigeur. T pour ouvrir ou couper l'eau.", "A glass cubicle on a tray, a ceiling shower head and a mixer. T to turn the water on or off.", "Una cabina de cristal sobre un plato, alcachofa en el techo y grifo. T para abrir o cerrar el agua."), Build = m => Furniture(m, 1.0f, 2.2f, 1.0f) },
            new Spec { Id = 8266, Set = 7, Label = Loc.Pick("Lavabo", "Sink", "Lavabo"), Mass = 25f, Cost = new[] { (Iron, 2), (Copper, 1), (Glass, 1) }, Size = new Vector3(0.7f, 1.0f, 0.5f),
                Description = Loc.Pick("Une vasque blanche sur un meuble, un robinet chromé. T pour ouvrir ou couper l'eau.", "A white basin on a cabinet, a chrome tap. T to turn the water on or off.", "Un lavabo blanco sobre un mueble, un grifo cromado. T para abrir o cerrar el agua."), Build = m => Furniture(m, 0.7f, 1.0f, 0.5f) },
            new Spec { Id = 8267, Set = 7, Label = Loc.Pick("Toilettes", "Toilet", "Inodoro"), Mass = 25f, Cost = new[] { (Iron, 2), (Copper, 1) }, Size = new Vector3(0.45f, 0.8f, 0.7f),
                Description = Loc.Pick("Une cuvette blanche et son réservoir à bouton lumineux. T pour s'asseoir.", "A white bowl and its tank with a lit button. T to sit.", "Un inodoro blanco y su cisterna con botón iluminado. T para sentarse."), Build = m => Furniture(m, 0.45f, 0.8f, 0.7f) },
            new Spec { Id = 8268, Set = 9, Label = Loc.Pick("Miroir", "Mirror", "Espejo"), Mass = 6f, Cost = new[] { (Glass, 2), (Iron, 1) }, Size = new Vector3(0.6f, 0.03f, 0.9f),
                Description = Loc.Pick("Un miroir rétroéclairé à poser sur un mur. Il reflète la pièce (les reflets de l'environnement : rien de plus à calculer).", "A backlit mirror for a wall. It reflects the room (the environment's reflections: nothing more to render).", "Un espejo retroiluminado para una pared. Refleja la sala (los reflejos del entorno: nada más que calcular)."), Build = m => Furniture(m, 0.6f, 0.03f, 0.9f) },
            // Hangar
            new Spec { Id = 8269, Set = 8, Label = Loc.Pick("Établi", "Workbench", "Banco de trabajo"), Mass = 60f, Cost = new[] { (Iron, 6), (Wood, 2), (Copper, 2) }, Size = new Vector3(1.8f, 1.9f, 0.8f), Donor = "StorageChest",
                Description = Loc.Pick("Un établi d'atelier : plan de travail, étau, panneau à outils et lampe. Ses tiroirs s'ouvrent comme un coffre et gardent les outils et les pièces.", "A workshop bench: a worktop, a vice, a tool board and a lamp. Its drawers open like a chest and keep tools and parts.", "Un banco de taller: encimera, tornillo, panel de herramientas y lámpara. Sus cajones se abren como un cofre y guardan herramientas y piezas."), Build = m => Furniture(m, 1.8f, 1.9f, 0.8f) },
            new Spec { Id = 8271, Set = 8, Label = Loc.Pick("Marquage au sol", "Floor marking", "Marcado de suelo"), Mass = 2f, Cost = new[] { (Iron, 1), (Copper, 1) }, Size = new Vector3(1.0f, 0.02f, 1.0f),
                Description = Loc.Pick("Bandes jaunes et noires et chevrons lumineux, pour les aires de pose du hangar. T : éteint, allumé, clignotant.", "Yellow and black bands and lit chevrons, for the hangar's landing pads. T: off, lit, blinking.", "Bandas amarillas y negras y chevrones luminosos, para las zonas de aterrizaje del hangar. T: apagado, encendido, parpadeante."), Build = m => Furniture(m, 1.0f, 0.02f, 1.0f) },
            // Wall dressing (flat on a wall, like the signs)
            new Spec { Id = 8272, Set = 9, Label = Loc.Pick("Écran mural", "Wall screen", "Pantalla de pared"), Mass = 6f, Cost = new[] { (Glass, 2), (Copper, 2), (Iron, 1) }, Size = new Vector3(1.2f, 0.06f, 0.7f),
                Description = Loc.Pick("Un écran fin où défilent des données et des courbes.", "A thin screen scrolling data and charts.", "Una pantalla fina donde pasan datos y gráficas."), Build = m => Furniture(m, 1.2f, 0.06f, 0.7f) },
            new Spec { Id = 8273, Set = 9, Label = Loc.Pick("Tuyaux muraux", "Wall pipes", "Tuberías de pared"), Mass = 10f, Cost = new[] { (Iron, 2), (Copper, 2) }, Size = new Vector3(1.0f, 0.16f, 1.0f),
                Description = Loc.Pick("Trois tuyaux sur colliers, une vanne et un manomètre, pour habiller les coursives. Mets-en plusieurs à la suite.", "Three pipes on clamps, a valve and a gauge, to dress the corridors. Put several in a row.", "Tres tuberías con abrazaderas, una válvula y un manómetro, para vestir los pasillos. Pon varias seguidas."), Build = m => Furniture(m, 1.0f, 0.16f, 1.0f) },
            new Spec { Id = 8274, Set = 9, Label = Loc.Pick("Câbles muraux", "Wall cables", "Cables de pared"), Mass = 4f, Cost = new[] { (Copper, 2) }, Size = new Vector3(1.0f, 0.08f, 1.0f),
                Description = Loc.Pick("Une goulotte et des faisceaux de câbles colorés tenus par des attaches.", "A cable tray and bundles of colored cables held by ties.", "Una canaleta y mazos de cables de colores sujetos con bridas."), Build = m => Furniture(m, 1.0f, 0.08f, 1.0f) },
            new Spec { Id = 8275, Set = 9, Label = Loc.Pick("Grille d'aération", "Vent grille", "Rejilla de ventilación"), Mass = 5f, Cost = new[] { (Iron, 2), (Copper, 1) }, Size = new Vector3(0.6f, 0.08f, 0.6f),
                Description = Loc.Pick("Une grille sur un ventilateur qui tourne (seulement quand on est près et qu'on la voit).", "A grille over a turning fan (only while you are near and see it).", "Una rejilla sobre un ventilador que gira (solo cuando estás cerca y la ves)."), Build = m => Furniture(m, 0.6f, 0.08f, 0.6f) },
            new Spec { Id = 8276, Set = 9, Label = Loc.Pick("Panneau sortie", "Exit sign", "Señal de salida"), Mass = 1f, Cost = new[] { (Iron, 1), (Copper, 1) }, Size = new Vector3(0.5f, 0.03f, 0.3f),
                Description = Loc.Pick("Un panneau vert lumineux : une porte et une flèche.", "A lit green sign: a door and an arrow.", "Una señal verde iluminada: una puerta y una flecha."), Build = m => Furniture(m, 0.5f, 0.03f, 0.3f) },
            new Spec { Id = 8277, Set = 9, Label = Loc.Pick("Panneau danger", "Hazard sign", "Señal de peligro"), Mass = 1f, Cost = new[] { (Iron, 1), (Copper, 1) }, Size = new Vector3(0.4f, 0.03f, 0.4f),
                Description = Loc.Pick("Un triangle jaune et un point d'exclamation.", "A yellow triangle and an exclamation mark.", "Un triángulo amarillo y un signo de exclamación."), Build = m => Furniture(m, 0.4f, 0.03f, 0.4f) },
            new Spec { Id = 8278, Set = 9, Label = Loc.Pick("Panneau flèche", "Arrow sign", "Señal de flecha"), Mass = 1f, Cost = new[] { (Iron, 1), (Copper, 1) }, Size = new Vector3(0.5f, 0.03f, 0.25f),
                Description = Loc.Pick("Une flèche lumineuse pour guider dans les coursives (la touche de rotation la tourne).", "A lit arrow to guide along the corridors (the rotate key turns it).", "Una flecha iluminada para guiar por los pasillos (la tecla de rotación la gira)."), Build = m => Furniture(m, 0.5f, 0.03f, 0.25f) },
            // Stairs (on floors; a flight climbs 1 m toward the back, away from the player placing it)
            new Spec { Id = 8220, Set = 4, Stairs = 1, Label = Loc.Pick("Escalier en bois", "Wooden stairs", "Escalera de madera"), Mass = 25f, Cost = new[] { (Wood, 4) }, Size = new Vector3(1f, 1f, 1f),
                Description = Loc.Pick("Volée de cinq marches en bois sur deux limons : monte 1 m sur 1 m. Mets-en plusieurs à la suite pour monter plus haut.", "A flight of five wooden steps on two stringers: climbs 1 m over 1 m. Chain several to climb higher.", "Un tramo de cinco peldaños de madera sobre dos zancas: sube 1 m en 1 m. Encadena varios para subir más."), Build = m => Furniture(m, 1f, 1f, 1f) },
            new Spec { Id = 8221, Set = 4, Stairs = 1, Label = Loc.Pick("Escalier en bois doux", "Gentle wooden stairs", "Escalera de madera suave"), Mass = 40f, Cost = new[] { (Wood, 6) }, Size = new Vector3(1f, 1f, 2f),
                Description = Loc.Pick("Escalier en bois en pente douce : monte 1 m sur 2 m.", "Wooden stairs on a gentle slope: climbs 1 m over 2 m.", "Escalera de madera de pendiente suave: sube 1 m en 2 m."), Build = m => Furniture(m, 1f, 1f, 2f) },
            new Spec { Id = 8222, Set = 4, Stairs = 1, Label = Loc.Pick("Escalier industriel", "Industrial stairs", "Escalera industrial"), Mass = 30f, Cost = new[] { (Iron, 4) }, Size = new Vector3(1f, 1f, 1f),
                Description = Loc.Pick("Marches en caillebotis d'acier sur limons : monte 1 m sur 1 m.", "Steel grating steps on stringers: climbs 1 m over 1 m.", "Peldaños de rejilla de acero sobre zancas: sube 1 m en 1 m."), Build = m => Furniture(m, 1f, 1f, 1f) },
            new Spec { Id = 8223, Set = 4, Stairs = 1, Label = Loc.Pick("Escalier industriel doux", "Gentle industrial stairs", "Escalera industrial suave"), Mass = 45f, Cost = new[] { (Iron, 6) }, Size = new Vector3(1f, 1f, 2f),
                Description = Loc.Pick("Escalier en acier en pente douce : monte 1 m sur 2 m.", "Steel stairs on a gentle slope: climbs 1 m over 2 m.", "Escalera de acero de pendiente suave: sube 1 m en 2 m."), Build = m => Furniture(m, 1f, 1f, 2f) },
            new Spec { Id = 8224, Set = 4, Stairs = 1, Label = Loc.Pick("Escalier de verre", "Glass stairs", "Escalera de cristal"), Mass = 30f, Cost = new[] { (Glass, 4), (Iron, 2) }, Size = new Vector3(1f, 1f, 1f),
                Description = Loc.Pick("Marches en verre épais sur une poutre centrale chromée : monte 1 m sur 1 m.", "Thick glass steps on a central chrome spine: climbs 1 m over 1 m.", "Peldaños de cristal grueso sobre una viga central cromada: sube 1 m en 1 m."), Build = m => Furniture(m, 1f, 1f, 1f) },
            new Spec { Id = 8225, Set = 4, Stairs = 1, Label = Loc.Pick("Escalier de verre doux", "Gentle glass stairs", "Escalera de cristal suave"), Mass = 45f, Cost = new[] { (Glass, 6), (Iron, 3) }, Size = new Vector3(1f, 1f, 2f),
                Description = Loc.Pick("Escalier de verre en pente douce : monte 1 m sur 2 m.", "Glass stairs on a gentle slope: climbs 1 m over 2 m.", "Escalera de cristal de pendiente suave: sube 1 m en 2 m."), Build = m => Furniture(m, 1f, 1f, 2f) },
            new Spec { Id = 8226, Set = 4, Stairs = 1, Label = Loc.Pick("Escalier futuriste", "Futuristic stairs", "Escalera futurista"), Mass = 30f, Cost = new[] { (Iron, 4), (Copper, 2), (Glass, 1) }, Size = new Vector3(1f, 1f, 1f),
                Description = Loc.Pick("Blocs sombres aux nez de marche lumineux et lignes de lumière sur les côtés : monte 1 m sur 1 m.", "Dark blocks with lit nosings and light lines along the sides: climbs 1 m over 1 m.", "Bloques oscuros con bordes iluminados y líneas de luz en los lados: sube 1 m en 1 m."), Build = m => Furniture(m, 1f, 1f, 1f) },
            new Spec { Id = 8227, Set = 4, Stairs = 1, Label = Loc.Pick("Escalier futuriste doux", "Gentle futuristic stairs", "Escalera futurista suave"), Mass = 45f, Cost = new[] { (Iron, 6), (Copper, 3), (Glass, 1) }, Size = new Vector3(1f, 1f, 2f),
                Description = Loc.Pick("Escalier futuriste en pente douce : monte 1 m sur 2 m.", "Futuristic stairs on a gentle slope: climbs 1 m over 2 m.", "Escalera futurista de pendiente suave: sube 1 m en 2 m."), Build = m => Furniture(m, 1f, 1f, 2f) },
            new Spec { Id = 8228, Set = 4, Stairs = 2, Label = Loc.Pick("Escalier en colimaçon", "Spiral staircase", "Escalera de caracol"), Mass = 60f, Cost = new[] { (Iron, 8) }, Size = new Vector3(2f, 2f, 2f),
                Description = Loc.Pick("Escalier en colimaçon en acier avec rampe : monte 2 m en trois quarts de tour sur 2 × 2 m. Il faut une trémie au plafond.", "Steel spiral staircase with a handrail: climbs 2 m in three quarters of a turn on 2 × 2 m. It needs a hole in the ceiling.", "Escalera de caracol de acero con pasamanos: sube 2 m en tres cuartos de vuelta en 2 × 2 m. Necesita un hueco en el techo."), Build = m => Furniture(m, 2f, 2f, 2f) },
            new Spec { Id = 8229, Set = 4, Stairs = 2, Label = Loc.Pick("Colimaçon futuriste", "Futuristic spiral stairs", "Caracol futurista"), Mass = 50f, Cost = new[] { (Glass, 6), (Iron, 4), (Copper, 3) }, Size = new Vector3(2f, 2f, 2f),
                Description = Loc.Pick("Marches de verre autour d'un mât chromé, bords lumineux : monte 2 m en trois quarts de tour sur 2 × 2 m.", "Glass steps around a chrome mast, lit edges: climbs 2 m in three quarters of a turn on 2 × 2 m.", "Peldaños de cristal alrededor de un mástil cromado, bordes iluminados: sube 2 m en tres cuartos de vuelta en 2 × 2 m."), Build = m => Furniture(m, 2f, 2f, 2f) },

            new Spec { Id = 8261, Set = 4, Stairs = 4, Label = Loc.Pick("Rampe d'escalier", "Stair railing", "Barandilla de escalera"), Mass = 8f, Cost = new[] { (Iron, 2) }, Size = new Vector3(0.06f, 2f, 1f),
                Description = Loc.Pick("Une rampe en acier qui suit la pente d'une volée de 1 m : vise le côté d'un escalier posé, elle s'y colle.", "A steel railing that follows the slope of a 1 m flight: aim at the side of a placed flight and it sits against it.", "Una barandilla de acero que sigue la pendiente de un tramo de 1 m: apunta al lado de una escalera colocada y se pega a ella."), Build = m => Furniture(m, 0.06f, 2f, 1f) },
            new Spec { Id = 8262, Set = 4, Stairs = 4, Label = Loc.Pick("Rampe d'escalier doux", "Gentle stair railing", "Barandilla de escalera suave"), Mass = 12f, Cost = new[] { (Iron, 3) }, Size = new Vector3(0.06f, 2f, 2f),
                Description = Loc.Pick("La rampe des escaliers doux (2 m de long) : vise le côté d'un escalier doux posé.", "The railing of the gentle stairs (2 m long): aim at the side of a placed gentle flight.", "La barandilla de las escaleras suaves (2 m): apunta al lado de una escalera suave colocada."), Build = m => Furniture(m, 0.06f, 2f, 2f) },
        };

        public static bool IsFurniture(ushort id) => id >= 7440 && id <= 7449 || id >= 8200 && id <= 8211 || id >= 8240 && id <= 8256 || id >= 8265 && id <= 8278;

        private static readonly System.Collections.Generic.HashSet<ushort> Ids = new System.Collections.Generic.HashSet<ushort>(All.Select(s => s.Id));

        public static bool IsConstruction(ushort id) => Ids.Contains(id);

        /// <summary>
        /// Thin upright pieces (railings, partitions, windows): their box stops half their thickness
        /// short of each end, so two meeting at a corner do not overlap (the game refused the second);
        /// their models still reach the ends, where two pieces share a post.
        /// </summary>
        public static bool IsThin(ushort id) => All.FirstOrDefault(s => s.Id == id) is Spec spec && IsThin(spec);

        /// <summary>
        /// What a flight chains on: its size and the height one leaves it at; null for anything else.
        /// </summary>
        public static (Vector3 Size, float Exit)? Chain(ushort id) =>
            All.FirstOrDefault(s => s.Id == id) is Spec spec && spec.Stairs == 1 ? (spec.Size, spec.Size.y) : ((Vector3, float)?)null;

        /// <summary>A flight's railing: the run it follows (that of the flights it goes with); null for anything else.</summary>
        public static float? RailRun(ushort id) => All.FirstOrDefault(s => s.Id == id) is Spec spec && spec.Stairs == 4 ? spec.Size.z : (float?)null;

        /// <summary>How high the flights of that run climb (what a railing along one rises by).</summary>
        public static float FlightRise(float run) => All.FirstOrDefault(s => s.Stairs == 1 && Mathf.Abs(s.Size.z - run) < 0.01f)?.Size.y ?? 1f;

        /// <summary>A straight flight's size (rise y over run z), null for anything else.</summary>
        public static Vector3? StairSize(ushort id) => All.FirstOrDefault(s => s.Id == id) is Spec spec && spec.Stairs == 1 ? spec.Size : (Vector3?)null;

        private static bool IsThin(Spec spec) => spec.Set <= 2 && spec.Size.z < 0.1f && spec.Size.x >= 1f && spec.Size.y >= 0.5f;

        /// <summary>How far a thin piece's box stops short of each of its ends.</summary>
        public static float EndInset(ushort id) => All.FirstOrDefault(s => s.Id == id) is Spec spec && IsThin(spec) ? (spec.Size.z + 0.01f) * 0.5f : 0f;

        private static Vector3 BoxSize(Spec spec) => IsThin(spec) ? new Vector3(spec.Size.x - 2f * EndInset(spec.Id), spec.Size.y, spec.Size.z) : spec.Size;

        // The fallback of a piece of furniture (its Blender model missing): a plain block
        private static void Furniture(Model m, float width, float height, float depth) =>
            m.Wood.Box(new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, depth));

        private static Material _hull;
        private static Material _glass;

        public static void Register()
        {
            foreach (Spec part in All)
            {
                Spec spec = part;
                int row = spec.Set == 0 ? AdvancedTab.WoodRow : spec.Set == 1 ? AdvancedTab.GlassRow : spec.Set == 2 ? AdvancedTab.IronRow
                    : spec.Set == 4 ? AdvancedTab.StairsRow : spec.Set == 5 ? AdvancedTab.MoreFurnitureRow : spec.Set == 6 ? AdvancedTab.FutureFurnitureRow
                    : spec.Set == 7 ? AdvancedTab.BathroomRow : spec.Set == 8 ? AdvancedTab.HangarRow : spec.Set == 9 ? AdvancedTab.WallDressingRow : AdvancedTab.FurnitureRow;
                PartFactory.Register(spec.Id, spec.Donor ?? Donor, AdvancedTab.ConstructionName, row, (settings, prefab) => Configure(spec, settings, prefab));
            }
            AdvancedMod.Log.Msg($"registered {All.Length} wood, glass and iron construction parts");
        }

        private static MeshBuilder Planar(Color tint, int layer)
        {
            // The hull shader reads its texture layer (0 wood, 1 metal) from the vertex color's alpha
            return new MeshBuilder { PlanarScale = 1f, VertexColor = new Color(tint.r, tint.g, tint.b, layer) };
        }

        private static void Configure(Spec spec, PartSettings settings, GameObject prefab)
        {
            PartFactory.Describe(settings, spec.Label, spec.Description, spec.Mass, spec.Cost);
            // On any face of the hull, and on other placed parts (see FloatingSnap); furniture on floors
            settings.snappingStyle = spec.Set >= 3 && spec.Set != 9 ? Ships.Interface.Model.SnappingStyle.PreciseOnFloor : Ships.Interface.Model.SnappingStyle.PreciseOnAny;
            if (spec.Donor != null)
            {
                ConfigureOnDonor(spec, prefab);
                return;
            }
            // The pieces with a switch keep it in their state (shared and saved by the game)
            settings.internalStateClass = FurnitureUses.HasState(spec.Id) ? Power.PowerCatalog.MachineStateClass : null;
            Transform visuals = PartFactory.BlankFromSignalDisplay(prefab, new Vector3(0f, spec.Size.y * 0.5f, 0f), BoxSize(spec));
            int layer = visuals.gameObject.layer;
            if (spec.Stairs == 1 || spec.Stairs == 2)
                StairColliders.Attach(prefab, spec.Size, spec.Stairs == 2);
            // The pieces one uses: look at it and press the use key (sit, sleep, eat, water...)
            System.Type use = FurnitureUses.VisualsFor(spec.Id);
            if (use != null)
            {
                TFItems.Common.UsablePart.AddBox(visuals, new Vector3(0f, spec.Size.y * 0.5f, 0f), spec.Size + new Vector3(0.1f, 0.1f, 0.1f));
                var plain = visuals.GetComponent<Ships.Parts.Common.DefaultShipPartVisuals>();
                if (plain != null)
                    Object.DestroyImmediate(plain);
                visuals.gameObject.AddComponent(use);
            }
            else if (FurnitureUses.Spins(spec.Id))
                visuals.gameObject.AddComponent<SpinningParts>();
            if (BlenderModel(spec, visuals, layer))
            {
                ContactShadow(spec, visuals, layer);
                return;
            }
            var model = new Model();
            spec.Build(model);
            Material hull = HullMaterial();
            Add(visuals, layer, "TFA_Build" + spec.Id + "Wood", model.Wood, hull ?? LitMaterials.Get("TFA_BuildWood", new Color(0.55f, 0.38f, 0.22f), 0f, 0.25f));
            Add(visuals, layer, "TFA_Build" + spec.Id + "Iron", model.Iron, hull ?? LitMaterials.Get("TFA_BuildIron", new Color(0.6f, 0.62f, 0.65f), 0.7f, 0.4f));
            Add(visuals, layer, "TFA_Build" + spec.Id + "DarkIron", model.DarkIron, hull ?? LitMaterials.Get("TFA_BuildDarkIron", new Color(0.25f, 0.26f, 0.28f), 0.7f, 0.4f));
            Add(visuals, layer, "TF_NoPaint_Build" + spec.Id + "Glass", model.Glass, GlassMaterial(), ShadowCastingMode.Off);
        }

        // A piece kept on a game part for its work (a chest's storage, the star map): the game
        // part's model hidden, its box the piece's, the Blender model in its place
        private static void ConfigureOnDonor(Spec spec, GameObject prefab)
        {
            Transform visual = prefab.GetComponentInChildren<Ships.Parts.Common.Model.Reactions.IShipPartVisual>(true)?.GameObject.transform;
            if (visual == null)
            {
                AdvancedMod.Log.Warning($"construction {spec.Id}: no visual on the {spec.Donor}");
                return;
            }
            GameSkin.Capture(prefab);
            Power.NuclearParts.HideDonorModel(visual);
            PartFactory.SetBounds(prefab, new Vector3(0f, spec.Size.y * 0.5f, 0f), spec.Size);
            if (spec.Donor == "StarMap")
                FurnitureUses.FitStarMap(prefab, spec.Size);
            var holder = new GameObject("TFA_Model") { layer = visual.gameObject.layer };
            holder.transform.SetParent(visual, false);
            holder.transform.SetPositionAndRotation(prefab.transform.position, prefab.transform.rotation);
            if (!BlenderModel(spec, holder.transform, holder.layer))
                AdvancedMod.Log.Warning($"construction {spec.Id}: no Blender model");
            else
                ContactShadow(spec, holder.transform, holder.layer);
        }

        /// <summary>A part's Blender model from construction.obj (pieces C&lt;id&gt;_&lt;look&gt;), for parts made elsewhere (the tractor beam).</summary>
        public static bool BlenderModel(ushort id, Transform visuals) => BlenderModel(new Spec { Id = id }, visuals, visuals.gameObject.layer);

        // The part's Blender model: its pieces C<id>_<look>, each look its material
        private static bool BlenderModel(Spec spec, Transform visuals, int layer)
        {
            System.Collections.Generic.Dictionary<string, Mesh> models = ObjModels.Load("construction");
            if (models == null)
                return false;
            string prefix = "C" + spec.Id + "_";
            bool any = false;
            foreach (System.Collections.Generic.KeyValuePair<string, Mesh> piece in models)
            {
                if (!piece.Key.StartsWith(prefix))
                    continue;
                string look = piece.Key.Substring(prefix.Length);
                // Pieces that turn (a fan): C<id>_Spin_<look>, turned about their own middle
                bool spin = look.StartsWith(SpinningParts.Prefix);
                if (spin)
                    look = look.Substring(SpinningParts.Prefix.Length);
                Material material = Look(look);
                if (material == null)
                    continue;
                bool glass = look == "Glass" || look == "Water";
                // The hull's glass reads its highlight from UV1 (the shapes made here set "none" too)
                if (glass && piece.Value.uv2.Length == 0)
                    piece.Value.SetUVs(1, Enumerable.Repeat(new Vector2(0.125f, 0.5f), piece.Value.vertexCount).ToList());
                // Glass keeps its look when painted; the rest takes the paint
                var child = new GameObject((glass ? "TF_NoPaint_Build" : "TFA_Build") + spec.Id + look) { layer = layer };
                child.transform.SetParent(visuals, false);
                if (spin)
                {
                    var pivot = new GameObject(SpinningParts.Prefix + spec.Id) { layer = layer };
                    pivot.transform.SetParent(visuals, false);
                    pivot.transform.localPosition = piece.Value.bounds.center;
                    child.transform.SetParent(pivot.transform, false);
                    child.transform.localPosition = -piece.Value.bounds.center;
                }
                child.AddComponent<MeshFilter>().sharedMesh = piece.Value;
                var renderer = child.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                // Lit pieces and rugs cast no shadow: fewer draws for a room full of furniture
                renderer.shadowCastingMode = glass || look == "Rug" || look == "RugTrim" || Glows(look) ? ShadowCastingMode.Off : ShadowCastingMode.On;
                any = true;
            }
            return any;
        }

        private static Mesh _shadowQuad;
        private static Material _shadowMaterial;

        // A soft dark patch on the floor under a piece of furniture: the game's shadows alone leave it floating
        private static void ContactShadow(Spec spec, Transform visuals, int layer)
        {
            if (spec.Set < 3 || spec.Set == 9 || spec.Stairs != 0 || spec.Size.y < 0.15f)
                return;
            if (_shadowMaterial == null)
            {
                Material blended = TransparentMaterials.Blended("TFA_ContactShadow", new Color(0f, 0f, 0f, 0.55f));
                if (blended == null)
                    return;
                _shadowMaterial = new Material(blended) { name = "TFA_ContactShadowSoft" };
                _shadowMaterial.SetTexture("_BaseMap", ShadowTexture());
                _shadowQuad = new Mesh { name = "TFA_ContactShadow" };
                _shadowQuad.vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f) };
                _shadowQuad.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
                _shadowQuad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                _shadowQuad.RecalculateNormals();
                _shadowQuad.RecalculateBounds();
            }
            var go = new GameObject("TF_NoPaint_ContactShadow") { layer = layer };
            go.transform.SetParent(visuals, false);
            go.transform.localPosition = new Vector3(0f, 0.008f, 0f);
            go.transform.localScale = new Vector3(spec.Size.x + 0.3f, 1f, spec.Size.z + 0.3f);
            go.AddComponent<MeshFilter>().sharedMesh = _shadowQuad;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _shadowMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // Dark under the piece, fading out over the 15 cm around it
        private static Texture2D ShadowTexture()
        {
            const int n = 64;
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "TFA_ContactShadow" };
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = Mathf.Abs((x + 0.5f) / n * 2f - 1f), v = Mathf.Abs((y + 0.5f) / n * 2f - 1f);
                    float a = (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, u))) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, v)));
                    pixels[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static bool Glows(string look) => look == "Neon" || look == "ScreenLit" || look == "Lamp" || look == "Green" || look == "Red" || look == "Mark" || look == "ScreenData" || look == "Mirror";

        private static readonly System.Collections.Generic.Dictionary<string, Material> Looks = new System.Collections.Generic.Dictionary<string, Material>();

        private static Material Look(string look)
        {
            if (Looks.TryGetValue(look, out Material material) && material != null)
                return material;
            switch (look)
            {
                case "Wood": material = NaturalTextures.Lit("TFA_CWood", NaturalTextures.Wood, NaturalTextures.WoodNormal, new Color(1f, 0.9f, 0.76f), 0f, 0.25f, 1.5f); break;
                case "WoodDark": material = NaturalTextures.Lit("TFA_CWoodDark", NaturalTextures.Wood, NaturalTextures.WoodNormal, new Color(0.5f, 0.38f, 0.3f), 0f, 0.25f, 1.5f); break;
                case "Steel": material = LitMaterials.Get("TFA_CSteel", new Color(0.66f, 0.68f, 0.72f), 0.45f, 0.4f); break;
                case "SteelDark": material = LitMaterials.Get("TFA_CSteelDark", new Color(0.26f, 0.27f, 0.3f), 0.4f, 0.35f); break;
                case "Chrome": material = LitMaterials.Get("TFA_CChrome", new Color(0.82f, 0.83f, 0.86f), 0.7f, 0.5f); break;
                case "Glass": material = GlassMaterial(); break;
                case "Fabric": material = LitMaterials.Get("TFA_CFabric", new Color(0.36f, 0.45f, 0.58f), 0f, 0.12f); break;
                case "FabricDark": material = LitMaterials.Get("TFA_CFabricDark", new Color(0.22f, 0.27f, 0.36f), 0f, 0.12f); break;
                case "Leaf": material = LitMaterials.Get("TFA_CLeaf", new Color(0.22f, 0.5f, 0.2f), 0f, 0.3f); break;
                case "Pot": material = LitMaterials.Get("TFA_CPot", new Color(0.86f, 0.83f, 0.78f), 0f, 0.45f); break;
                case "Soil": material = LitMaterials.Get("TFA_CSoil", new Color(0.2f, 0.14f, 0.09f), 0f, 0.05f); break;
                case "Rug": material = LitMaterials.Get("TFA_CRug", new Color(0.55f, 0.2f, 0.17f), 0f, 0.05f); break;
                case "RugTrim": material = LitMaterials.Get("TFA_CRugTrim", new Color(0.86f, 0.74f, 0.52f), 0f, 0.05f); break;
                case "BookA": material = LitMaterials.Get("TFA_CBookA", new Color(0.55f, 0.16f, 0.12f), 0f, 0.3f); break;
                case "BookB": material = LitMaterials.Get("TFA_CBookB", new Color(0.16f, 0.3f, 0.5f), 0f, 0.3f); break;
                case "BookC": material = LitMaterials.Get("TFA_CBookC", new Color(0.78f, 0.64f, 0.32f), 0f, 0.3f); break;
                case "Sheet": material = LitMaterials.Get("TFA_CSheet", new Color(0.88f, 0.88f, 0.85f), 0f, 0.1f); break;
                case "Screen": material = LitMaterials.Get("TFA_CScreen", new Color(0.02f, 0.03f, 0.05f), 0.2f, 0.92f, new Color(0.02f, 0.05f, 0.09f)); break;
                case "Lamp": material = LitMaterials.Get("TFA_CLamp", new Color(1f, 0.92f, 0.75f), 0f, 0.5f, new Color(1.6f, 1.35f, 0.9f)); break;
                case "White": material = LitMaterials.Get("TFA_CWhite", new Color(0.86f, 0.88f, 0.9f), 0.1f, 0.6f); break;
                case "Green": material = LitMaterials.Get("TFA_CGreen", new Color(0.15f, 0.85f, 0.3f), 0f, 0.6f, new Color(0.15f, 1.3f, 0.35f)); break;
                case "Red": material = LitMaterials.Get("TFA_CRed", new Color(1f, 0.25f, 0.2f), 0f, 0.5f, new Color(2.2f, 0.4f, 0.3f)); break;
                case "ScreenLit": material = LitMaterials.Get("TFA_CScreenLit", new Color(0.15f, 0.45f, 0.7f), 0.1f, 0.9f, new Color(0.3f, 0.9f, 1.6f)); break;
                case "Black": material = LitMaterials.Get("TFA_CBlack", new Color(0.06f, 0.065f, 0.075f), 0.2f, 0.45f); break;
                case "Orange": material = LitMaterials.Get("TFA_COrange", new Color(0.95f, 0.45f, 0.1f), 0.1f, 0.4f); break;
                case "Neon": material = LitMaterials.Get("TFA_CNeon", new Color(0.3f, 0.85f, 1f), 0f, 0.5f, new Color(0.5f, 1.6f, 2.4f)); break;
                case "Yellow": material = LitMaterials.Get("TFA_CYellow", new Color(0.95f, 0.75f, 0.1f), 0.1f, 0.45f); break;
                case "Mirror": material = LitMaterials.Get("TFA_CMirror", new Color(0.92f, 0.93f, 0.95f), 1f, 0.97f); break;
                case "Water": material = GlassMaterial(); break;
                // Lit by FurnitureUses: the floor marking's lights (off, lit, blinking) and the wall screens' scrolling data
                case "Mark": material = FurnitureUses.MarkMaterial(0); break;
                case "ScreenData": material = FurnitureUses.ScreenMaterial(); break;
            }
            // Plain surfaces: without the panel texture LitMaterials lays on (its seams drew dark
            // crosses over fabrics, plates and the lit screens)
            if (material != null && look != "Wood" && look != "WoodDark" && look != "Glass" && look != "Water" && look != "Screen" && look != "Mark" && look != "ScreenData")
            {
                foreach (string map in new[] { "_BaseMap", "_MainTex", "_BumpMap" })
                    if (material.HasProperty(map))
                        material.SetTexture(map, null);
                material.DisableKeyword("_NORMALMAP");
            }
            Looks[look] = material;
            return material;
        }

        private static void Add(Transform parent, int layer, string name, MeshBuilder builder, Material material, ShadowCastingMode shadows = ShadowCastingMode.On)
        {
            if (!builder.HasGeometry || material == null)
                return;
            var child = new GameObject(name) { layer = layer };
            child.transform.SetParent(parent, false);
            // UV1: the hull's "no highlight" value
            child.AddComponent<MeshFilter>().sharedMesh = builder.Build(name, new Vector2(0.125f, 0.5f));
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows;
        }

        private static Material HullMaterial()
        {
            if (_hull != null)
                return _hull;
            Material source = Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(m => m != null && m.name == "HullMetalFlat");
            if (source == null)
            {
                AdvancedMod.Log.Warning("construction: the hull material was not found, plain colors used");
                return null;
            }
            _hull = new Material(source) { name = "TFA_HullLook" };
            if (_hull.HasProperty("_UseArray"))
                _hull.SetInt("_UseArray", 1);
            return _hull;
        }

        private static Material GlassMaterial()
        {
            if (_glass != null)
                return _glass;
            Material source = Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(m => m != null && m.name == "HullGlass");
            _glass = source != null ? new Material(source) { name = "TFA_Glass" } : LitMaterials.Get("TFA_GlassFallback", new Color(0.6f, 0.85f, 0.95f), 0f, 0.95f);
            return _glass;
        }

        // ---- Wood

        private static void WoodPost(Model m, float length)
        {
            m.Wood.Box(new Vector3(0f, length * 0.5f, 0f), new Vector3(0.14f, length - 0.06f, 0.14f));
            m.DarkIron.Box(new Vector3(0f, 0.015f, 0f), new Vector3(0.18f, 0.03f, 0.18f));
            m.Wood.Box(new Vector3(0f, length - 0.02f, 0f), new Vector3(0.18f, 0.04f, 0.18f));
            foreach (float y in new[] { 0.2f, length - 0.2f })
                m.DarkIron.Box(new Vector3(0f, y, 0f), new Vector3(0.15f, 0.03f, 0.15f));
        }

        private static void Planks(Model m, float width, float depth, int count)
        {
            // Two battens underneath, planks across with small gaps
            foreach (float z in new[] { -depth * 0.32f, depth * 0.32f })
                m.Wood.Box(new Vector3(0f, 0.012f, z), new Vector3(width, 0.024f, 0.08f));
            float pitch = width / count;
            for (int i = 0; i < count; i++)
            {
                float x = -width * 0.5f + pitch * (i + 0.5f);
                m.Wood.Box(new Vector3(x, 0.042f, 0f), new Vector3(pitch * 0.9f, 0.036f, depth));
                foreach (float z in new[] { -depth * 0.32f, depth * 0.32f })
                    m.DarkIron.Box(new Vector3(x, 0.061f, z), new Vector3(0.015f, 0.004f, 0.015f));
            }
        }

        private static void PlankWall(Model m, float width, float height)
        {
            int count = Mathf.RoundToInt(width / 0.16f);
            float pitch = width / count;
            for (int i = 0; i < count; i++)
                m.Wood.Box(new Vector3(-width * 0.5f + pitch * (i + 0.5f), height * 0.5f, 0.012f), new Vector3(pitch * 0.92f, height, 0.024f));
            foreach (float y in new[] { height * 0.2f, height * 0.8f })
                m.Wood.Box(new Vector3(0f, y, -0.015f), new Vector3(width, 0.1f, 0.03f));
            // A diagonal brace between the battens
            Vector3 a = new Vector3(-width * 0.42f, height * 0.2f, -0.015f), b = new Vector3(width * 0.42f, height * 0.8f, -0.015f);
            Vector3 along = (b - a).normalized;
            m.Wood.Box((a + b) * 0.5f, new Vector3(0.08f, (b - a).magnitude, 0.028f), Vector3.Cross(along, Vector3.forward), along, Vector3.forward);
        }

        private static void WoodRailing(Model m, float width)
        {
            foreach (float x in new[] { -width * 0.5f + 0.05f, width * 0.5f - 0.05f })
                m.Wood.Box(new Vector3(x, 0.5f, 0f), new Vector3(0.08f, 1f, 0.08f));
            m.Wood.Box(new Vector3(0f, 0.97f, 0f), new Vector3(width, 0.06f, 0.08f));
            foreach (float y in new[] { 0.35f, 0.65f })
                m.Wood.Box(new Vector3(0f, y, 0f), new Vector3(width - 0.1f, 0.05f, 0.04f));
        }

        private static void Lattice(Model m, float size)
        {
            float h = size * 0.5f, bar = 0.05f;
            m.Wood.Box(new Vector3(0f, 0.025f, h - bar * 0.5f), new Vector3(size, 0.05f, bar));
            m.Wood.Box(new Vector3(0f, 0.025f, -h + bar * 0.5f), new Vector3(size, 0.05f, bar));
            m.Wood.Box(new Vector3(h - bar * 0.5f, 0.025f, 0f), new Vector3(bar, 0.05f, size - 2f * bar));
            m.Wood.Box(new Vector3(-h + bar * 0.5f, 0.025f, 0f), new Vector3(bar, 0.05f, size - 2f * bar));
            // Diagonal slats both ways, clipped to the frame by their length
            for (int k = -2; k <= 2; k++)
            {
                float offset = k * size * 0.22f;
                float length = (size - 2f * bar) * 1.41f - Mathf.Abs(offset) * 1.41f;
                if (length <= 0.1f)
                    continue;
                foreach (float dir in new[] { 1f, -1f })
                {
                    Vector3 along = new Vector3(1f, 0f, dir).normalized;
                    Vector3 across = new Vector3(-dir, 0f, 1f).normalized;
                    float y = dir > 0f ? 0.018f : 0.034f;
                    m.Wood.Box(across * offset * 0.71f + Vector3.up * y, new Vector3(length, 0.016f, 0.035f), along, Vector3.up, across);
                }
            }
        }

        private static void Shelf(Model m)
        {
            foreach (float x in new[] { -0.47f, 0.47f })
                m.Wood.Box(new Vector3(x, 0.45f, 0f), new Vector3(0.04f, 0.9f, 0.3f));
            foreach (float y in new[] { 0.04f, 0.45f, 0.86f })
                m.Wood.Box(new Vector3(0f, y, 0f), new Vector3(0.9f, 0.03f, 0.3f));
            m.Wood.Box(new Vector3(0f, 0.45f, -0.14f), new Vector3(0.9f, 0.86f, 0.015f));
            // A few crates and jars
            m.Wood.Box(new Vector3(-0.25f, 0.13f, 0.02f), new Vector3(0.22f, 0.15f, 0.2f));
            m.DarkIron.Cylinder(new Vector3(0.2f, 0.47f, 0f), new Vector3(0.2f, 0.62f, 0f), 0.06f, 0.06f, 10);
            m.Glass.Cylinder(new Vector3(0.05f, 0.47f, 0f), new Vector3(0.05f, 0.6f, 0f), 0.05f, 0.04f, 10);
        }

        // ---- Glass

        private static void Window(Model m, float width, float height, int columns, int rows = 0)
        {
            if (rows == 0)
                rows = columns;
            const float frame = 0.06f, depth = 0.06f, mullion = 0.04f;
            m.Iron.Box(new Vector3(0f, frame * 0.5f, 0f), new Vector3(width, frame, depth));
            m.Iron.Box(new Vector3(0f, height - frame * 0.5f, 0f), new Vector3(width, frame, depth));
            m.Iron.Box(new Vector3(-width * 0.5f + frame * 0.5f, height * 0.5f, 0f), new Vector3(frame, height - 2f * frame, depth));
            m.Iron.Box(new Vector3(width * 0.5f - frame * 0.5f, height * 0.5f, 0f), new Vector3(frame, height - 2f * frame, depth));
            for (int c = 1; c < columns; c++)
                m.Iron.Box(new Vector3(-width * 0.5f + width * c / columns, height * 0.5f, 0f), new Vector3(mullion, height - 2f * frame, depth * 0.8f));
            for (int r = 1; r < rows; r++)
                m.Iron.Box(new Vector3(0f, height * r / rows, 0f), new Vector3(width - 2f * frame, mullion, depth * 0.8f));
            m.Glass.Box(new Vector3(0f, height * 0.5f, 0f), new Vector3(width - 2f * frame, height - 2f * frame, 0.012f));
        }

        private static void GlassFloor(Model m, float size)
        {
            const float frame = 0.06f;
            float h = size * 0.5f;
            m.Iron.Box(new Vector3(0f, 0.04f, h - frame * 0.5f), new Vector3(size, 0.08f, frame));
            m.Iron.Box(new Vector3(0f, 0.04f, -h + frame * 0.5f), new Vector3(size, 0.08f, frame));
            m.Iron.Box(new Vector3(h - frame * 0.5f, 0.04f, 0f), new Vector3(frame, 0.08f, size - 2f * frame));
            m.Iron.Box(new Vector3(-h + frame * 0.5f, 0.04f, 0f), new Vector3(frame, 0.08f, size - 2f * frame));
            m.Glass.Box(new Vector3(0f, 0.045f, 0f), new Vector3(size - 2f * frame, 0.05f, size - 2f * frame));
        }

        private static void GlassRailing(Model m, float width)
        {
            m.Glass.Box(new Vector3(0f, 0.5f, 0f), new Vector3(width - 0.04f, 0.88f, 0.014f));
            m.Iron.Box(new Vector3(0f, 0.975f, 0f), new Vector3(width, 0.05f, 0.06f));
            foreach (float x in new[] { -width * 0.35f, width * 0.35f })
            {
                m.DarkIron.Box(new Vector3(x, 0.04f, 0f), new Vector3(0.1f, 0.08f, 0.05f));
                m.DarkIron.Box(new Vector3(x, 0.92f, 0f), new Vector3(0.06f, 0.06f, 0.04f));
            }
        }

        private static void Skylight(Model m, float size, float height)
        {
            const float frame = 0.05f;
            float h = size * 0.5f;
            m.Iron.Box(new Vector3(0f, 0.03f, h - frame * 0.5f), new Vector3(size, 0.06f, frame));
            m.Iron.Box(new Vector3(0f, 0.03f, -h + frame * 0.5f), new Vector3(size, 0.06f, frame));
            m.Iron.Box(new Vector3(h - frame * 0.5f, 0.03f, 0f), new Vector3(frame, 0.06f, size - 2f * frame));
            m.Iron.Box(new Vector3(-h + frame * 0.5f, 0.03f, 0f), new Vector3(frame, 0.06f, size - 2f * frame));
            var apex = new Vector3(0f, height, 0f);
            float b = h - frame;
            Vector3[] corners = { new Vector3(-b, 0.06f, -b), new Vector3(b, 0.06f, -b), new Vector3(b, 0.06f, b), new Vector3(-b, 0.06f, b) };
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = corners[i], c = corners[(i + 1) % 4];
                // A glass face as a flat block with its top edge pinched to the apex
                m.Glass.Block(new[] { a, c, apex, apex, a + Vector3.up * 0.01f, c + Vector3.up * 0.01f, apex + Vector3.up * 0.01f, apex + Vector3.up * 0.01f });
                Vector3 ridge = (apex - a);
                Vector3 across = Vector3.Cross(ridge.normalized, (c - a).normalized).normalized;
                m.Iron.Box((a + apex) * 0.5f, new Vector3(0.03f, ridge.magnitude, 0.03f), Vector3.Cross(ridge.normalized, across).normalized, ridge.normalized, across);
            }
        }

        // ---- Iron

        private static void Grating(Model m, float size)
        {
            float h = size * 0.5f;
            const float frame = 0.05f;
            m.Iron.Box(new Vector3(0f, 0.025f, h - frame * 0.5f), new Vector3(size, 0.05f, frame));
            m.Iron.Box(new Vector3(0f, 0.025f, -h + frame * 0.5f), new Vector3(size, 0.05f, frame));
            m.Iron.Box(new Vector3(h - frame * 0.5f, 0.025f, 0f), new Vector3(frame, 0.05f, size - 2f * frame));
            m.Iron.Box(new Vector3(-h + frame * 0.5f, 0.025f, 0f), new Vector3(frame, 0.05f, size - 2f * frame));
            int bars = 12;
            for (int i = 1; i < bars; i++)
                m.DarkIron.Box(new Vector3(-h + size * i / bars, 0.025f, 0f), new Vector3(0.012f, 0.04f, size - 2f * frame));
            for (int i = 1; i < 4; i++)
                m.DarkIron.Box(new Vector3(0f, 0.042f, -h + size * i / 4f), new Vector3(size - 2f * frame, 0.008f, 0.012f));
        }

        private static void SteelRailing(Model m, float width)
        {
            int posts = Mathf.Max(2, Mathf.RoundToInt(width) + 1);
            for (int i = 0; i < posts; i++)
            {
                float x = -width * 0.5f + 0.03f + (width - 0.06f) * i / (posts - 1);
                m.Iron.Box(new Vector3(x, 0.5f, 0f), new Vector3(0.05f, 1f, 0.05f));
            }
            m.Iron.Cylinder(new Vector3(-width * 0.5f, 0.98f, 0f), new Vector3(width * 0.5f, 0.98f, 0f), 0.025f, 0.025f, 8);
            m.Iron.Cylinder(new Vector3(-width * 0.5f, 0.55f, 0f), new Vector3(width * 0.5f, 0.55f, 0f), 0.015f, 0.015f, 8);
            m.DarkIron.Box(new Vector3(0f, 0.06f, 0f), new Vector3(width, 0.12f, 0.01f));
        }

        private static void Truss(Model m, float length)
        {
            // Three chords on a triangle, diagonals between them every 0.5 m
            Vector3[] chords = { new Vector3(-0.15f, 0f, -0.1f), new Vector3(0.15f, 0f, -0.1f), new Vector3(0f, 0f, 0.14f) };
            foreach (Vector3 c in chords)
                m.Iron.Cylinder(c, c + Vector3.up * length, 0.025f, 0.025f, 6);
            int bays = Mathf.Max(1, Mathf.RoundToInt(length / 0.5f));
            for (int b = 0; b < bays; b++)
            {
                float y0 = length * b / bays, y1 = length * (b + 1) / bays;
                for (int i = 0; i < 3; i++)
                {
                    Vector3 a = chords[i] + Vector3.up * y0, c = chords[(i + 1) % 3] + Vector3.up * y1;
                    m.DarkIron.Cylinder(a, c, 0.012f, 0.012f, 5);
                    m.DarkIron.Cylinder(chords[i] + Vector3.up * y0, chords[(i + 1) % 3] + Vector3.up * y0, 0.012f, 0.012f, 5);
                }
            }
            m.Iron.Box(new Vector3(0f, 0.01f, 0f), new Vector3(0.36f, 0.02f, 0.32f));
            m.Iron.Box(new Vector3(0f, length - 0.01f, 0f), new Vector3(0.36f, 0.02f, 0.32f));
        }

        private static void Column(Model m, float height)
        {
            m.Iron.Box(new Vector3(0f, 0.04f, 0f), new Vector3(0.4f, 0.08f, 0.4f));
            m.Iron.Cylinder(new Vector3(0f, 0.08f, 0f), new Vector3(0f, 0.2f, 0f), 0.17f, 0.13f, 16);
            m.Iron.Cylinder(new Vector3(0f, 0.2f, 0f), new Vector3(0f, height - 0.2f, 0f), 0.12f, 0.12f, 16, capA: false, capB: false);
            m.Iron.Cylinder(new Vector3(0f, height - 0.2f, 0f), new Vector3(0f, height - 0.08f, 0f), 0.13f, 0.17f, 16);
            m.Iron.Box(new Vector3(0f, height - 0.04f, 0f), new Vector3(0.4f, 0.08f, 0.4f));
            // Rivet bands
            foreach (float y in new[] { 0.5f, height * 0.5f, height - 0.5f })
                m.DarkIron.Cylinder(new Vector3(0f, y - 0.02f, 0f), new Vector3(0f, y + 0.02f, 0f), 0.13f, 0.13f, 16, capA: false, capB: false);
        }

        private static void Gusset(Model m, float size)
        {
            // A right triangle in the y-z plane, its corner at the surface's edge
            float h = size * 0.5f;
            m.Iron.Block(new[]
            {
                new Vector3(-0.02f, 0f, -h), new Vector3(0.02f, 0f, -h), new Vector3(-0.02f, size, -h), new Vector3(0.02f, size, -h),
                new Vector3(-0.02f, 0f, h), new Vector3(0.02f, 0f, h), new Vector3(-0.02f, 0f, h), new Vector3(0.02f, 0f, h)
            });
            foreach (Vector3 bolt in new[] { new Vector3(0f, 0.12f, -h + 0.12f), new Vector3(0f, 0.12f, 0f), new Vector3(0f, size * 0.5f, -h + 0.12f) })
                m.DarkIron.Cylinder(bolt + Vector3.left * 0.03f, bolt + Vector3.right * 0.03f, 0.025f, 0.025f, 6);
        }

        private static void Perforated(Model m, float size)
        {
            float h = size * 0.5f;
            m.Iron.Box(new Vector3(0f, 0.008f, 0f), new Vector3(size, 0.016f, size));
            foreach (float s in new[] { -1f, 1f })
            {
                m.Iron.Box(new Vector3(0f, 0.018f, s * (h - 0.015f)), new Vector3(size, 0.036f, 0.03f));
                m.Iron.Box(new Vector3(s * (h - 0.015f), 0.018f, 0f), new Vector3(0.03f, 0.036f, size));
            }
            // Holes drawn as dark discs
            for (int x = 0; x < 7; x++)
                for (int z = 0; z < 7; z++)
                {
                    var c = new Vector3(-h + 0.11f + x * (size - 0.22f) / 6f, 0.0165f, -h + 0.11f + z * (size - 0.22f) / 6f);
                    m.DarkIron.Cylinder(c, c + Vector3.up * 0.002f, 0.03f, 0.03f, 8, capA: false);
                }
        }
    }
}

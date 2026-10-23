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
            /// <summary>The set: 0 wood, 1 glass, 2 iron, 3 furniture (build menu order and research).</summary>
            public int Set;
        }

        private const uint Wood = 110, Glass = 102, Iron = 101;

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
        };

        public static bool IsFurniture(ushort id) => id >= 7440 && id <= 7449;

        private static readonly System.Collections.Generic.HashSet<ushort> Ids = new System.Collections.Generic.HashSet<ushort>(All.Select(s => s.Id));

        public static bool IsConstruction(ushort id) => Ids.Contains(id);

        /// <summary>
        /// Thin upright pieces (railings, partitions, windows): their box stops half their thickness
        /// short of each end, so two meeting at a corner do not overlap (the game refused the second);
        /// their models still reach the ends, where two pieces share a post.
        /// </summary>
        public static bool IsThin(ushort id) => All.FirstOrDefault(s => s.Id == id) is Spec spec && IsThin(spec);

        private static bool IsThin(Spec spec) => spec.Set != 3 && spec.Size.z < 0.1f && spec.Size.x >= 1f && spec.Size.y >= 0.5f;

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
                int row = spec.Set == 0 ? AdvancedTab.WoodRow : spec.Set == 1 ? AdvancedTab.GlassRow : spec.Set == 2 ? AdvancedTab.IronRow : AdvancedTab.FurnitureRow;
                PartFactory.Register(spec.Id, Donor, AdvancedTab.ConstructionName, row, (settings, prefab) => Configure(spec, settings, prefab));
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
            settings.internalStateClass = null;
            // On any face of the hull, and on other placed parts (see FloatingSnap); furniture on floors
            settings.snappingStyle = spec.Set == 3 ? Ships.Interface.Model.SnappingStyle.PreciseOnFloor : Ships.Interface.Model.SnappingStyle.PreciseOnAny;
            Transform visuals = PartFactory.BlankFromSignalDisplay(prefab, new Vector3(0f, spec.Size.y * 0.5f, 0f), BoxSize(spec));
            int layer = visuals.gameObject.layer;
            // The seats: look at the piece and press the use key to sit
            if (TFItems.Devices.Seats.IsSeat(spec.Id))
            {
                TFItems.Common.UsablePart.AddBox(visuals, new Vector3(0f, spec.Size.y * 0.5f, 0f), spec.Size + new Vector3(0.1f, 0.1f, 0.1f));
                var plain = visuals.GetComponent<Ships.Parts.Common.DefaultShipPartVisuals>();
                if (plain != null)
                    Object.DestroyImmediate(plain);
                visuals.gameObject.AddComponent<TFItems.Devices.SeatVisuals>();
            }
            if (BlenderModel(spec, visuals, layer))
                return;
            var model = new Model();
            spec.Build(model);
            Material hull = HullMaterial();
            Add(visuals, layer, "TFA_Build" + spec.Id + "Wood", model.Wood, hull ?? LitMaterials.Get("TFA_BuildWood", new Color(0.55f, 0.38f, 0.22f), 0f, 0.25f));
            Add(visuals, layer, "TFA_Build" + spec.Id + "Iron", model.Iron, hull ?? LitMaterials.Get("TFA_BuildIron", new Color(0.6f, 0.62f, 0.65f), 0.7f, 0.4f));
            Add(visuals, layer, "TFA_Build" + spec.Id + "DarkIron", model.DarkIron, hull ?? LitMaterials.Get("TFA_BuildDarkIron", new Color(0.25f, 0.26f, 0.28f), 0.7f, 0.4f));
            Add(visuals, layer, "TF_NoPaint_Build" + spec.Id + "Glass", model.Glass, GlassMaterial(), ShadowCastingMode.Off);
        }

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
                Material material = Look(look);
                if (material == null)
                    continue;
                bool glass = look == "Glass";
                // The hull's glass reads its highlight from UV1 (the shapes made here set "none" too)
                if (glass && piece.Value.uv2.Length == 0)
                    piece.Value.SetUVs(1, Enumerable.Repeat(new Vector2(0.125f, 0.5f), piece.Value.vertexCount).ToList());
                // Glass keeps its look when painted; the rest takes the paint
                var child = new GameObject((glass ? "TF_NoPaint_Build" : "TFA_Build") + spec.Id + look) { layer = layer };
                child.transform.SetParent(visuals, false);
                child.AddComponent<MeshFilter>().sharedMesh = piece.Value;
                var renderer = child.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = glass || look == "Rug" || look == "RugTrim" ? ShadowCastingMode.Off : ShadowCastingMode.On;
                any = true;
            }
            return any;
        }

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
            }
            // Plain surfaces: without the panel texture LitMaterials lays on (its seams drew dark
            // crosses over fabrics and plates)
            if (material != null && look != "Wood" && look != "WoodDark" && look != "Glass")
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

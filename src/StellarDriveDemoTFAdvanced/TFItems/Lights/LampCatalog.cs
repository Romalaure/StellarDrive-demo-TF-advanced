using System.Collections.Generic;
using System.Linq;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using UnityEngine;
using Loc = StellarDriveDemoTFAdvanced.Common.Loc;

namespace StellarDriveDemoTFAdvanced.TFItems.Lights
{
    internal enum LampGroup { Frame, Trim, Diffuser }

    /// <summary>One placeable light. Geometry is in part space: y points out of the surface it is mounted on.</summary>
    internal sealed class LampSpec
    {
        public ushort Id;
        public string Label;
        public string Description;
        public float Mass;
        public (uint Item, int Count)[] Cost;
        public Vector3 BoundsCenter;
        public Vector3 BoundsSize;
        public LightType LightType;
        public Vector3 LightPosition;
        /// <summary>Where a spot shines, in part space. Out of the mounting surface by default.</summary>
        public Vector3 LightDirection = Vector3.up;
        public float Range;
        public float Intensity;
        public float SpotAngle;
        /// <summary>Light color while unpainted; warm white when not set.</summary>
        public Color? Color;
        /// <summary>Flash period in seconds (beacons), 0 for a steady light.</summary>
        public float Blink;
        /// <summary>Beam rotation in degrees per second around the lamp's axis (rotating beacons).</summary>
        public float Spin;
        public System.Action<Dictionary<LampGroup, MeshBuilder>> Shape;

        public Dictionary<LampGroup, MeshBuilder> BuildShapes()
        {
            var builders = new Dictionary<LampGroup, MeshBuilder>
            {
                [LampGroup.Frame] = new MeshBuilder(),
                [LampGroup.Trim] = new MeshBuilder(),
                [LampGroup.Diffuser] = new MeshBuilder()
            };
            Shape(builders);
            return builders;
        }
    }

    internal static class LampCatalog
    {
        private const uint Iron = 101;
        private const uint Glass = 102;
        private const uint Copper = 112;

        public static readonly Color DefaultLight = new Color(1f, 0.9f, 0.75f);

        /// <summary>
        /// Direction the sloped face of a corner wedge looks: away from the surface and from the wall.
        /// Declared before All, which uses it while initializing.
        /// </summary>
        private static readonly Vector3 CornerNormal = new Vector3(0f, 1f, -1f).normalized;

        public static readonly LampSpec[] All =
        {
            new LampSpec
            {
                Id = 7101,
                Label = Loc.Pick("Plafonnier", "Ceiling light", "Plafón de techo"),
                Description = Loc.Pick("Lampe ronde à fixer au plafond. Peins-la pour changer la couleur de la lumière.", "Round lamp for the ceiling. Paint it to change the light's color.", "Lámpara redonda para el techo. Píntala para cambiar el color de la luz."),
                Mass = 2f,
                Cost = new[] { (Iron, 2), (Glass, 1), (Copper, 1) },
                BoundsCenter = new Vector3(0f, 0.035f, 0f),
                BoundsSize = new Vector3(0.44f, 0.07f, 0.44f),
                LightType = LightType.Point,
                LightPosition = new Vector3(0f, 0.3f, 0f),
                Range = 8f,
                Intensity = 2.2f,
                Shape = b =>
                {
                    b[LampGroup.Frame].Cylinder(Vector3.zero, new Vector3(0f, 0.025f, 0f), 0.22f, 0.21f, 28);
                    b[LampGroup.Trim].Cylinder(new Vector3(0f, 0.025f, 0f), new Vector3(0f, 0.035f, 0f), 0.205f, 0.198f, 28);
                    b[LampGroup.Diffuser].Cylinder(new Vector3(0f, 0.035f, 0f), new Vector3(0f, 0.058f, 0f), 0.19f, 0.17f, 28);
                    b[LampGroup.Diffuser].Cylinder(new Vector3(0f, 0.058f, 0f), new Vector3(0f, 0.07f, 0f), 0.17f, 0.12f, 28);
                }
            },
            new LampSpec
            {
                Id = 7102,
                Label = Loc.Pick("Applique murale", "Wall light", "Aplique de pared"),
                Description = Loc.Pick("Petite lampe cubique pour les murs. Peins-la pour changer la couleur de la lumière.", "Small cubic lamp for walls. Paint it to change the light's color.", "Pequeña lámpara cúbica para paredes. Píntala para cambiar el color de la luz."),
                Mass = 1.5f,
                Cost = new[] { (Iron, 1), (Glass, 1), (Copper, 1) },
                BoundsCenter = new Vector3(0f, 0.055f, 0f),
                BoundsSize = new Vector3(0.2f, 0.11f, 0.2f),
                LightType = LightType.Point,
                LightPosition = new Vector3(0f, 0.25f, 0f),
                Range = 6f,
                Intensity = 1.6f,
                Shape = b =>
                {
                    b[LampGroup.Frame].Box(new Vector3(0f, 0.01f, 0f), new Vector3(0.2f, 0.02f, 0.2f));
                    b[LampGroup.Frame].Box(new Vector3(0f, 0.03f, 0f), new Vector3(0.14f, 0.02f, 0.14f));
                    b[LampGroup.Diffuser].Box(new Vector3(0f, 0.07f, 0f), new Vector3(0.12f, 0.06f, 0.12f));
                    b[LampGroup.Trim].Box(new Vector3(0f, 0.105f, 0f), new Vector3(0.135f, 0.01f, 0.135f));
                    // Corner posts holding the cap
                    foreach (float x in new[] { -0.062f, 0.062f })
                    foreach (float z in new[] { -0.062f, 0.062f })
                        b[LampGroup.Trim].Box(new Vector3(x, 0.07f, z), new Vector3(0.012f, 0.06f, 0.012f));
                }
            },
            new LampSpec
            {
                Id = 7103,
                Label = Loc.Pick("Projecteur", "Floodlight", "Foco"),
                Description = Loc.Pick("Phare puissant qui éclaire droit devant lui, à poser sur la coque extérieure, un mur ou le sol. Peins-le pour changer la couleur du faisceau.", "Powerful light shining straight ahead, for the outer hull, a wall or the floor. Paint it to change the beam's color.", "Foco potente que ilumina justo delante, para el casco exterior, una pared o el suelo. Píntalo para cambiar el color del haz."),
                Mass = 4f,
                Cost = new[] { (Iron, 3), (Glass, 2), (Copper, 2) },
                BoundsCenter = new Vector3(0f, 0.15f, 0f),
                BoundsSize = new Vector3(0.26f, 0.3f, 0.26f),
                LightType = LightType.Spot,
                LightPosition = new Vector3(0f, 0.3f, 0f),
                Range = 45f,
                Intensity = 9f,
                SpotAngle = 40f,
                Shape = b =>
                {
                    b[LampGroup.Frame].Cylinder(Vector3.zero, new Vector3(0f, 0.02f, 0f), 0.12f, 0.11f, 20);
                    // Yoke
                    b[LampGroup.Frame].Box(new Vector3(0.108f, 0.085f, 0f), new Vector3(0.016f, 0.13f, 0.05f));
                    b[LampGroup.Frame].Box(new Vector3(-0.108f, 0.085f, 0f), new Vector3(0.016f, 0.13f, 0.05f));
                    b[LampGroup.Trim].Cylinder(new Vector3(-0.118f, 0.14f, 0f), new Vector3(0.118f, 0.14f, 0f), 0.014f, 0.014f, 12);
                    // Housing with cooling rings, chrome bezel and lens
                    b[LampGroup.Frame].Cylinder(new Vector3(0f, 0.05f, 0f), new Vector3(0f, 0.27f, 0f), 0.075f, 0.092f, 24);
                    foreach (float y in new[] { 0.08f, 0.11f, 0.14f })
                        b[LampGroup.Trim].Cylinder(new Vector3(0f, y, 0f), new Vector3(0f, y + 0.008f, 0f), 0.084f, 0.086f, 24);
                    b[LampGroup.Trim].Cylinder(new Vector3(0f, 0.27f, 0f), new Vector3(0f, 0.29f, 0f), 0.1f, 0.1f, 24);
                    b[LampGroup.Diffuser].Cylinder(new Vector3(0f, 0.282f, 0f), new Vector3(0f, 0.292f, 0f), 0.088f, 0.085f, 24);
                }
            },
            new LampSpec
            {
                Id = 7104,
                Label = Loc.Pick("Tube néon", "Neon tube", "Tubo de neón"),
                Description = Loc.Pick("Réglette lumineuse d'un mètre pour couloirs, plafonds et coques. Peins-la pour changer la couleur du néon.", "One-meter light strip for corridors, ceilings and hulls. Paint it to change the neon's color.", "Regleta luminosa de un metro para pasillos, techos y cascos. Píntala para cambiar el color del neón."),
                Mass = 1.5f,
                Cost = new[] { (Iron, 1), (Glass, 2), (Copper, 1) },
                BoundsCenter = new Vector3(0f, 0.035f, 0f),
                BoundsSize = new Vector3(1f, 0.07f, 0.08f),
                LightType = LightType.Point,
                LightPosition = new Vector3(0f, 0.25f, 0f),
                Range = 6f,
                Intensity = 1.8f,
                Shape = b =>
                {
                    b[LampGroup.Frame].Box(new Vector3(0f, 0.01f, 0f), new Vector3(1f, 0.02f, 0.06f));
                    b[LampGroup.Diffuser].Cylinder(new Vector3(-0.47f, 0.045f, 0f), new Vector3(0.47f, 0.045f, 0f), 0.022f, 0.022f, 16, false, false);
                    b[LampGroup.Trim].Cylinder(new Vector3(-0.5f, 0.045f, 0f), new Vector3(-0.47f, 0.045f, 0f), 0.027f, 0.027f, 16);
                    b[LampGroup.Trim].Cylinder(new Vector3(0.47f, 0.045f, 0f), new Vector3(0.5f, 0.045f, 0f), 0.027f, 0.027f, 16);
                    foreach (float x in new[] { -0.3f, 0f, 0.3f })
                        b[LampGroup.Trim].Box(new Vector3(x, 0.03f, 0f), new Vector3(0.02f, 0.03f, 0.05f));
                }
            },

            // Corner fixtures: a wedge that fills the angle between the surface it is placed on and
            // the wall behind it (+z side), its lit face sloped at 45 degrees into the room
            new LampSpec
            {
                Id = 7105,
                Label = Loc.Pick("Lampe d'angle", "Corner lamp", "Lámpara de esquina"),
                Description = Loc.Pick("Lampe en biseau qui s'emboîte dans un coin, entre le plafond (ou le sol) et un mur : pose-la contre le mur, dos au mur, et elle éclaire la pièce en diagonale. Peins-la pour changer la couleur de la lumière.", "Beveled lamp that fits a corner between the ceiling (or floor) and a wall: place it against the wall, back to the wall, and it lights the room diagonally. Paint it to change the light's color.", "Lámpara biselada que encaja en una esquina, entre el techo (o el suelo) y una pared: colócala contra la pared, de espaldas a la pared, e ilumina la sala en diagonal. Píntala para cambiar el color de la luz."),
                Mass = 1.5f,
                Cost = new[] { (Iron, 1), (Glass, 1), (Copper, 1) },
                BoundsCenter = new Vector3(0f, 0.07f, 0f),
                BoundsSize = new Vector3(0.41f, 0.14f, 0.14f),
                LightType = LightType.Point,
                LightPosition = CornerLight(0.14f, 0.3f),
                Range = 6f,
                Intensity = 1.6f,
                Shape = b => CornerShape(b, 0.4f, 0.14f)
            },
            new LampSpec
            {
                Id = 7106,
                Label = Loc.Pick("Néon d'angle", "Corner neon", "Neón de esquina"),
                Description = Loc.Pick("Réglette d'un mètre en biseau pour les coins entre plafond et mur ou entre sol et mur : pose-la dos au mur. Idéale pour éclairer un couloir sans lampe qui dépasse. Peins-la pour changer la couleur du néon.", "One-meter beveled strip for the corners between ceiling and wall or floor and wall: place it back to the wall. Ideal to light a corridor with no lamp sticking out. Paint it to change the neon's color.", "Regleta biselada de un metro para las esquinas entre techo y pared o entre suelo y pared: colócala de espaldas a la pared. Ideal para iluminar un pasillo sin lámparas que sobresalgan. Píntala para cambiar el color del neón."),
                Mass = 1.5f,
                Cost = new[] { (Iron, 1), (Glass, 2), (Copper, 1) },
                BoundsCenter = new Vector3(0f, 0.045f, 0f),
                BoundsSize = new Vector3(1f, 0.09f, 0.09f),
                LightType = LightType.Point,
                LightPosition = CornerLight(0.09f, 0.25f),
                Range = 6f,
                Intensity = 1.8f,
                Shape = b => CornerShape(b, 1f, 0.09f)
            },
            new LampSpec
            {
                Id = 7107,
                Label = Loc.Pick("Projecteur d'angle", "Corner floodlight", "Foco de esquina"),
                Description = Loc.Pick("Projecteur fixé dans un coin, dos au mur, qui éclaire en diagonale vers le centre de la pièce ou le long de la coque. Peins-le pour changer la couleur du faisceau.", "Floodlight fixed in a corner, back to the wall, shining diagonally toward the middle of the room or along the hull. Paint it to change the beam's color.", "Foco fijado en una esquina, de espaldas a la pared, que ilumina en diagonal hacia el centro de la sala o a lo largo del casco. Píntalo para cambiar el color del haz."),
                Mass = 3f,
                Cost = new[] { (Iron, 2), (Glass, 2), (Copper, 2) },
                BoundsCenter = new Vector3(0f, 0.12f, -0.04f),
                BoundsSize = new Vector3(0.2f, 0.24f, 0.24f),
                LightType = LightType.Spot,
                LightPosition = CornerLight(0.16f, 0.16f),
                LightDirection = CornerNormal,
                Range = 30f,
                Intensity = 6f,
                SpotAngle = 60f,
                Shape = b =>
                {
                    const float size = 0.16f;
                    b[LampGroup.Frame].Wedge(-0.1f, 0.1f, 0f, size, -size / 2f, size / 2f);
                    Vector3 face = new Vector3(0f, size / 2f, 0f);
                    Vector3 lensEnd = face + CornerNormal * 0.13f;
                    b[LampGroup.Frame].Cylinder(face, lensEnd, 0.058f, 0.07f, 20);
                    b[LampGroup.Trim].Cylinder(face + CornerNormal * 0.05f, face + CornerNormal * 0.058f, 0.068f, 0.068f, 20);
                    b[LampGroup.Trim].Cylinder(lensEnd, lensEnd + CornerNormal * 0.015f, 0.078f, 0.078f, 20);
                    b[LampGroup.Diffuser].Cylinder(lensEnd + CornerNormal * 0.008f, lensEnd + CornerNormal * 0.018f, 0.066f, 0.063f, 20);
                }
            },

            new LampSpec
            {
                Id = 7108,
                Label = Loc.Pick("Spot encastré", "Recessed spot", "Foco empotrado"),
                Description = Loc.Pick("Petit spot rond presque plat, qui affleure le plafond ou le mur. Éclaire en large cône. Peins-le pour changer la couleur de la lumière.", "Small round, nearly flat spot, flush with the ceiling or wall. Lights in a wide cone. Paint it to change the light's color.", "Pequeño foco redondo casi plano, a ras del techo o de la pared. Ilumina en un cono ancho. Píntalo para cambiar el color de la luz."),
                Mass = 0.5f,
                Cost = new[] { (Iron, 1), (Glass, 1) },
                BoundsCenter = new Vector3(0f, 0.0075f, 0f),
                BoundsSize = new Vector3(0.17f, 0.015f, 0.17f),
                LightType = LightType.Spot,
                LightPosition = new Vector3(0f, 0.03f, 0f),
                Range = 7f,
                Intensity = 3f,
                SpotAngle = 120f,
                Shape = b =>
                {
                    b[LampGroup.Trim].Cylinder(Vector3.zero, new Vector3(0f, 0.012f, 0f), 0.085f, 0.08f, 24);
                    b[LampGroup.Diffuser].Cylinder(new Vector3(0f, 0.004f, 0f), new Vector3(0f, 0.015f, 0f), 0.062f, 0.058f, 24);
                }
            },
            new LampSpec
            {
                Id = 7109,
                Label = Loc.Pick("Lanterne industrielle", "Industrial lantern", "Farol industrial"),
                Description = Loc.Pick("Lampe ronde protégée par une cage métallique, pour les murs, plafonds, soutes et coursives. Peins-la pour changer la couleur de la lumière.", "Round lamp guarded by a metal cage, for walls, ceilings, holds and gangways. Paint it to change the light's color.", "Lámpara redonda protegida por una jaula metálica, para paredes, techos, bodegas y pasarelas. Píntala para cambiar el color de la luz."),
                Mass = 1.5f,
                Cost = new[] { (Iron, 2), (Glass, 1), (Copper, 1) },
                BoundsCenter = new Vector3(0f, 0.095f, 0f),
                BoundsSize = new Vector3(0.16f, 0.19f, 0.16f),
                LightType = LightType.Point,
                LightPosition = new Vector3(0f, 0.25f, 0f),
                Range = 7f,
                Intensity = 1.8f,
                Shape = b =>
                {
                    b[LampGroup.Frame].Cylinder(Vector3.zero, new Vector3(0f, 0.025f, 0f), 0.08f, 0.075f, 20);
                    b[LampGroup.Diffuser].Cylinder(new Vector3(0f, 0.025f, 0f), new Vector3(0f, 0.16f, 0f), 0.052f, 0.052f, 20);
                    foreach (float y in new[] { 0.065f, 0.12f })
                        b[LampGroup.Frame].Cylinder(new Vector3(0f, y, 0f), new Vector3(0f, y + 0.008f, 0f), 0.064f, 0.064f, 20);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * Mathf.PI / 3f;
                        b[LampGroup.Frame].Box(new Vector3(Mathf.Cos(a) * 0.06f, 0.095f, Mathf.Sin(a) * 0.06f), new Vector3(0.008f, 0.14f, 0.008f));
                    }
                    b[LampGroup.Frame].Cylinder(new Vector3(0f, 0.16f, 0f), new Vector3(0f, 0.185f, 0f), 0.07f, 0.045f, 20);
                }
            },
            new LampSpec
            {
                Id = 7110,
                Label = Loc.Pick("Panneau lumineux", "Light panel", "Panel luminoso"),
                Description = Loc.Pick("Grande dalle carrée et plate de 60 cm : lumière douce et large pour éclairer toute une pièce. Peins-la pour changer la couleur de la lumière.", "Large flat 60 cm square panel: soft, wide light for a whole room. Paint it to change the light's color.", "Gran placa cuadrada y plana de 60 cm: luz suave y amplia para iluminar toda una sala. Píntala para cambiar el color de la luz."),
                Mass = 2f,
                Cost = new[] { (Iron, 2), (Glass, 2), (Copper, 1) },
                BoundsCenter = new Vector3(0f, 0.0125f, 0f),
                BoundsSize = new Vector3(0.6f, 0.025f, 0.6f),
                LightType = LightType.Point,
                LightPosition = new Vector3(0f, 0.35f, 0f),
                Range = 9f,
                Intensity = 2.4f,
                Shape = b =>
                {
                    b[LampGroup.Frame].Box(new Vector3(0f, 0.008f, 0f), new Vector3(0.6f, 0.016f, 0.6f));
                    b[LampGroup.Trim].Box(new Vector3(0f, 0.018f, 0f), new Vector3(0.58f, 0.006f, 0.58f));
                    b[LampGroup.Diffuser].Box(new Vector3(0f, 0.021f, 0f), new Vector3(0.55f, 0.006f, 0.55f));
                }
            },
            new LampSpec
            {
                Id = 7113,
                Label = Loc.Pick("Suspension", "Pendant lamp", "Lámpara colgante"),
                Description = Loc.Pick("Lampe suspendue à un fil sous le plafond, avec abat-jour. Parfaite au-dessus d'une table ou du poste de pilotage. Peins-la pour changer la couleur de la lumière.", "Lamp hanging from the ceiling on a cord, with a shade. Perfect above a table or the cockpit. Paint it to change the light's color.", "Lámpara colgada del techo por un cable, con pantalla. Perfecta sobre una mesa o el puesto de pilotaje. Píntala para cambiar el color de la luz."),
                Mass = 1.5f,
                Cost = new[] { (Iron, 1), (Glass, 1), (Copper, 2) },
                BoundsCenter = new Vector3(0f, 0.3f, 0f),
                BoundsSize = new Vector3(0.34f, 0.6f, 0.34f),
                LightType = LightType.Point,
                LightPosition = new Vector3(0f, 0.68f, 0f),
                Range = 8f,
                Intensity = 2.2f,
                Shape = b =>
                {
                    b[LampGroup.Frame].Cylinder(Vector3.zero, new Vector3(0f, 0.02f, 0f), 0.06f, 0.05f, 20);
                    b[LampGroup.Trim].Cylinder(new Vector3(0f, 0.02f, 0f), new Vector3(0f, 0.42f, 0f), 0.005f, 0.005f, 8);
                    b[LampGroup.Trim].Cylinder(new Vector3(0f, 0.42f, 0f), new Vector3(0f, 0.45f, 0f), 0.02f, 0.03f, 16);
                    b[LampGroup.Frame].Cylinder(new Vector3(0f, 0.45f, 0f), new Vector3(0f, 0.585f, 0f), 0.035f, 0.165f, 28);
                    b[LampGroup.Diffuser].Cylinder(new Vector3(0f, 0.585f, 0f), new Vector3(0f, 0.595f, 0f), 0.158f, 0.15f, 28);
                }
            },
            new LampSpec
            {
                Id = 7114,
                Label = Loc.Pick("Balise clignotante", "Flashing beacon", "Baliza intermitente"),
                Description = Loc.Pick("Petit feu qui clignote, rouge par défaut : feux de position sur la coque, signalisation d'un sas ou d'un quai. Peins-la pour changer sa couleur.", "Small flashing light, red by default: navigation lights on the hull, marking an airlock or a dock. Paint it to change its color.", "Pequeña luz que parpadea, roja por defecto: luces de posición en el casco, señalización de una esclusa o un muelle. Píntala para cambiar su color."),
                Mass = 0.8f,
                Cost = new[] { (Iron, 1), (Glass, 1), (Copper, 1) },
                BoundsCenter = new Vector3(0f, 0.045f, 0f),
                BoundsSize = new Vector3(0.12f, 0.09f, 0.12f),
                LightType = LightType.Point,
                LightPosition = new Vector3(0f, 0.15f, 0f),
                Range = 6f,
                Intensity = 3f,
                Color = new Color(1f, 0.12f, 0.08f),
                Blink = 1.2f,
                Shape = b =>
                {
                    b[LampGroup.Frame].Cylinder(Vector3.zero, new Vector3(0f, 0.03f, 0f), 0.06f, 0.055f, 20);
                    b[LampGroup.Trim].Cylinder(new Vector3(0f, 0.03f, 0f), new Vector3(0f, 0.036f, 0f), 0.05f, 0.05f, 20);
                    b[LampGroup.Diffuser].Cylinder(new Vector3(0f, 0.036f, 0f), new Vector3(0f, 0.075f, 0f), 0.045f, 0.035f, 20);
                    b[LampGroup.Diffuser].Cylinder(new Vector3(0f, 0.075f, 0f), new Vector3(0f, 0.09f, 0f), 0.035f, 0.012f, 20);
                }
            },
            new LampSpec
            {
                Id = 7115,
                Label = Loc.Pick("Gyrophare", "Rotating beacon", "Girofaro"),
                Description = Loc.Pick("Feu tournant orange dont le faisceau balaie tout autour de lui : alerte, zone de chargement, engin de chantier. Peins-le pour changer sa couleur.", "Orange rotating light whose beam sweeps all around: alert, loading area, work vehicle. Paint it to change its color.", "Luz giratoria naranja cuyo haz barre todo a su alrededor: alerta, zona de carga, vehículo de obra. Píntalo para cambiar su color."),
                Mass = 1.2f,
                Cost = new[] { (Iron, 1), (Glass, 2), (Copper, 2) },
                BoundsCenter = new Vector3(0f, 0.085f, 0f),
                BoundsSize = new Vector3(0.16f, 0.17f, 0.16f),
                LightType = LightType.Spot,
                LightPosition = new Vector3(0f, 0.1f, 0f),
                LightDirection = Vector3.right,
                Range = 25f,
                Intensity = 6f,
                SpotAngle = 35f,
                Color = new Color(1f, 0.55f, 0.05f),
                Spin = 240f,
                Shape = b =>
                {
                    b[LampGroup.Frame].Cylinder(Vector3.zero, new Vector3(0f, 0.04f, 0f), 0.08f, 0.075f, 24);
                    b[LampGroup.Trim].Cylinder(new Vector3(0f, 0.04f, 0f), new Vector3(0f, 0.05f, 0f), 0.068f, 0.068f, 24);
                    b[LampGroup.Diffuser].Cylinder(new Vector3(0f, 0.05f, 0f), new Vector3(0f, 0.15f, 0f), 0.06f, 0.055f, 24);
                    b[LampGroup.Diffuser].Cylinder(new Vector3(0f, 0.15f, 0f), new Vector3(0f, 0.17f, 0f), 0.055f, 0.03f, 24);
                }
            },
            new LampSpec
            {
                Id = 7116,
                Label = Loc.Pick("Bande LED", "LED strip", "Tira LED"),
                Description = Loc.Pick("Ruban lumineux très fin de deux mètres, pour souligner un plafond, un sol, une marche ou le contour de la coque. Peins-la pour changer sa couleur.", "Very thin two-meter light strip, to outline a ceiling, a floor, a step or the hull's edge. Paint it to change its color.", "Cinta luminosa muy fina de dos metros, para resaltar un techo, un suelo, un escalón o el contorno del casco. Píntala para cambiar su color."),
                Mass = 0.6f,
                Cost = new[] { (Glass, 1), (Copper, 1) },
                BoundsCenter = new Vector3(0f, 0.006f, 0f),
                BoundsSize = new Vector3(2f, 0.012f, 0.03f),
                LightType = LightType.Point,
                LightPosition = new Vector3(0f, 0.2f, 0f),
                Range = 5f,
                Intensity = 1.2f,
                Shape = b =>
                {
                    b[LampGroup.Frame].Box(new Vector3(0f, 0.004f, 0f), new Vector3(2f, 0.008f, 0.03f));
                    b[LampGroup.Diffuser].Box(new Vector3(0f, 0.01f, 0f), new Vector3(1.98f, 0.004f, 0.018f));
                }
            },
            new LampSpec
            {
                Id = 7117,
                Label = Loc.Pick("Lampadaire", "Floor lamp", "Lámpara de pie"),
                Description = Loc.Pick("Lampe sur pied d'1,70 m à poser au sol, avec abat-jour : coin salon, cabine, poste d'équipage. Peins-la pour changer la couleur de la lumière.", "1.70 m standing lamp with a shade: lounge corner, cabin, crew station. Paint it to change the light's color.", "Lámpara de pie de 1,70 m con pantalla: rincón de salón, camarote, puesto de tripulación. Píntala para cambiar el color de la luz."),
                Mass = 3f,
                Cost = new[] { (Iron, 3), (Glass, 1), (Copper, 1) },
                BoundsCenter = new Vector3(0f, 0.85f, 0f),
                BoundsSize = new Vector3(0.34f, 1.7f, 0.34f),
                LightType = LightType.Point,
                LightPosition = new Vector3(0f, 1.35f, 0f),
                Range = 8f,
                Intensity = 2f,
                Shape = b =>
                {
                    b[LampGroup.Frame].Cylinder(Vector3.zero, new Vector3(0f, 0.03f, 0f), 0.15f, 0.14f, 28);
                    b[LampGroup.Trim].Cylinder(new Vector3(0f, 0.03f, 0f), new Vector3(0f, 1.46f, 0f), 0.015f, 0.015f, 12);
                    b[LampGroup.Diffuser].Cylinder(new Vector3(0f, 1.44f, 0f), new Vector3(0f, 1.455f, 0f), 0.165f, 0.165f, 28);
                    b[LampGroup.Frame].Cylinder(new Vector3(0f, 1.455f, 0f), new Vector3(0f, 1.7f, 0f), 0.17f, 0.12f, 28);
                }
            },
            // New lamps (their models are in tools/blender/lamps.py; the shapes here are the fallback)
            new LampSpec
            {
                Id = 7118,
                Label = Loc.Pick("Lampe de bureau", "Desk lamp", "Lámpara de escritorio"),
                Description = Loc.Pick("Lampe articulée à poser sur un bureau, une table ou le sol : sa tête éclaire vers le bas et devant elle. Peins-la pour changer la couleur de la lumière.", "Jointed lamp for a desk, a table or the floor: its head lights down and in front of it. Paint it to change the light's color.", "Lámpara articulada para un escritorio, una mesa o el suelo: su cabeza ilumina hacia abajo y delante. Píntala para cambiar el color de la luz."),
                Mass = 1.5f,
                Cost = new[] { (Iron, 2), (Glass, 1), (Copper, 1) },
                BoundsCenter = new Vector3(0f, 0.24f, 0.05f),
                BoundsSize = new Vector3(0.22f, 0.48f, 0.34f),
                LightType = LightType.Spot,
                LightPosition = new Vector3(0f, 0.35f, 0.2f),
                LightDirection = new Vector3(0f, -0.75f, 0.66f),
                Range = 6f,
                Intensity = 2.5f,
                SpotAngle = 75f,
                Shape = b =>
                {
                    b[LampGroup.Frame].Cylinder(Vector3.zero, new Vector3(0f, 0.025f, 0f), 0.1f, 0.095f, 24);
                    b[LampGroup.Trim].Cylinder(new Vector3(0f, 0.025f, 0.04f), new Vector3(0f, 0.3f, -0.05f), 0.008f, 0.008f, 10);
                    b[LampGroup.Trim].Cylinder(new Vector3(0f, 0.3f, -0.05f), new Vector3(0f, 0.42f, 0.13f), 0.008f, 0.008f, 10);
                    b[LampGroup.Frame].Cylinder(new Vector3(0f, 0.42f, 0.13f), new Vector3(0f, 0.345f, 0.196f), 0.026f, 0.065f, 20);
                }
            },
            new LampSpec
            {
                Id = 7119,
                Label = Loc.Pick("Hublot lumineux", "Bulkhead light", "Ojo de buey luminoso"),
                Description = Loc.Pick("Lampe ovale étanche derrière une grille, pour les soutes, les coursives et la coque extérieure. Peins-la pour changer la couleur de la lumière.", "Sealed oval lamp behind a guard, for holds, gangways and the outer hull. Paint it to change the light's color.", "Lámpara ovalada estanca tras una rejilla, para bodegas, pasarelas y el casco exterior. Píntala para cambiar el color de la luz."),
                Mass = 1.5f,
                Cost = new[] { (Iron, 2), (Glass, 1), (Copper, 1) },
                BoundsCenter = new Vector3(0f, 0.03f, 0f),
                BoundsSize = new Vector3(0.26f, 0.06f, 0.18f),
                LightType = LightType.Point,
                LightPosition = new Vector3(0f, 0.25f, 0f),
                Range = 7f,
                Intensity = 2f,
                Shape = b =>
                {
                    b[LampGroup.Frame].Box(new Vector3(0f, 0.015f, 0f), new Vector3(0.26f, 0.03f, 0.18f));
                    b[LampGroup.Diffuser].Box(new Vector3(0f, 0.04f, 0f), new Vector3(0.2f, 0.022f, 0.13f));
                }
            },
            new LampSpec
            {
                Id = 7120,
                Label = Loc.Pick("Lustre annulaire", "Ring chandelier", "Lámpara de anillo"),
                Description = Loc.Pick("Un anneau lumineux de 60 cm suspendu au plafond par trois câbles, pour les grandes salles. Peins-le pour changer la couleur de la lumière.", "A 60 cm ring of light hung from the ceiling on three cables, for big rooms. Paint it to change the light's color.", "Un anillo de luz de 60 cm colgado del techo con tres cables, para salas grandes. Píntalo para cambiar el color de la luz."),
                Mass = 4f,
                Cost = new[] { (Iron, 3), (Glass, 3), (Copper, 2) },
                BoundsCenter = new Vector3(0f, 0.27f, 0f),
                BoundsSize = new Vector3(0.64f, 0.54f, 0.64f),
                LightType = LightType.Point,
                LightPosition = new Vector3(0f, 0.6f, 0f),
                Range = 11f,
                Intensity = 2.8f,
                Shape = b =>
                {
                    b[LampGroup.Frame].Cylinder(Vector3.zero, new Vector3(0f, 0.025f, 0f), 0.08f, 0.07f, 24);
                    b[LampGroup.Diffuser].Cylinder(new Vector3(0f, 0.5f, 0f), new Vector3(0f, 0.54f, 0f), 0.32f, 0.32f, 40, false, false);
                }
            },
            new LampSpec
            {
                Id = 7125,
                Label = Loc.Pick("Applique double faisceau", "Up and down wall light", "Aplique de doble haz"),
                Description = Loc.Pick("Cylindre chromé à fixer au mur, qui éclaire vers le haut et vers le bas. Peins-le pour changer la couleur de la lumière.", "A chrome cylinder for walls, lighting up and down. Paint it to change the light's color.", "Un cilindro cromado para paredes, que ilumina hacia arriba y hacia abajo. Píntalo para cambiar el color de la luz."),
                Mass = 1f,
                Cost = new[] { (Iron, 1), (Glass, 1), (Copper, 1) },
                BoundsCenter = new Vector3(0f, 0.055f, 0f),
                BoundsSize = new Vector3(0.1f, 0.11f, 0.26f),
                LightType = LightType.Point,
                LightPosition = new Vector3(0f, 0.2f, 0f),
                Range = 6f,
                Intensity = 1.8f,
                Shape = b =>
                {
                    b[LampGroup.Frame].Box(new Vector3(0f, 0.008f, 0f), new Vector3(0.07f, 0.016f, 0.12f));
                    b[LampGroup.Trim].Cylinder(new Vector3(0f, 0.065f, -0.12f), new Vector3(0f, 0.065f, 0.12f), 0.042f, 0.042f, 24);
                }
            },
            new LampSpec
            {
                Id = 7126,
                Label = Loc.Pick("Rail à spots", "Track spots", "Riel de focos"),
                Description = Loc.Pick("Un rail de 80 cm portant trois spots orientés, pour éclairer un comptoir, un établi ou un couloir. Peins-le pour changer la couleur de la lumière.", "An 80 cm rail carrying three aimed spots, to light a counter, a workbench or a corridor. Paint it to change the light's color.", "Un riel de 80 cm con tres focos orientados, para iluminar un mostrador, un banco de trabajo o un pasillo. Píntalo para cambiar el color de la luz."),
                Mass = 2.5f,
                Cost = new[] { (Iron, 3), (Glass, 2), (Copper, 2) },
                BoundsCenter = new Vector3(0f, 0.07f, 0f),
                BoundsSize = new Vector3(0.8f, 0.14f, 0.08f),
                LightType = LightType.Spot,
                LightPosition = new Vector3(0f, 0.2f, 0f),
                Range = 12f,
                Intensity = 4f,
                SpotAngle = 90f,
                Shape = b =>
                {
                    b[LampGroup.Frame].Box(new Vector3(0f, 0.015f, 0f), new Vector3(0.8f, 0.03f, 0.04f));
                    foreach (float x in new[] { -0.28f, 0f, 0.28f })
                        b[LampGroup.Frame].Cylinder(new Vector3(x, 0.065f, 0f), new Vector3(x, 0.155f, 0f), 0.03f, 0.036f, 16);
                }
            }
        };

        private static Vector3 CornerLight(float size, float offset) => new Vector3(0f, size / 2f, 0f) + CornerNormal * offset;

        // Wedge body with chrome end caps and a glowing strip along its sloped face
        private static void CornerShape(Dictionary<LampGroup, MeshBuilder> b, float length, float size)
        {
            float half = length / 2f, z0 = -size / 2f, z1 = size / 2f;
            const float cap = 0.012f, lip = 0.005f;
            b[LampGroup.Frame].Wedge(-half + cap, half - cap, 0f, size, z0, z1);
            b[LampGroup.Trim].Wedge(-half, -half + cap, 0f, size + lip, z0 - lip, z1);
            b[LampGroup.Trim].Wedge(half - cap, half, 0f, size + lip, z0 - lip, z1);

            float slope = Mathf.Sqrt(2f) * size;
            var center = new Vector3(0f, size / 2f, 0f) + CornerNormal * 0.004f;
            b[LampGroup.Diffuser].Box(center, new Vector3(length - 2f * cap - 0.01f, 0.008f, slope * 0.7f),
                Vector3.right, CornerNormal, new Vector3(0f, 1f, 1f).normalized);
        }

        private static readonly HashSet<ushort> Ids = new HashSet<ushort>(All.Select(l => l.Id));

        public static bool IsLamp(ushort partId) => Ids.Contains(partId);

        /// <summary>All lamps side by side, for previewing models outside the game.</summary>
        internal static Dictionary<LampGroup, MeshBuilder> BuildShapes()
        {
            var merged = new Dictionary<LampGroup, MeshBuilder>
            {
                [LampGroup.Frame] = new MeshBuilder(),
                [LampGroup.Trim] = new MeshBuilder(),
                [LampGroup.Diffuser] = new MeshBuilder()
            };
            float x = 0f;
            foreach (LampSpec lamp in All)
            {
                foreach (KeyValuePair<LampGroup, MeshBuilder> entry in lamp.BuildShapes())
                    merged[entry.Key].Append(entry.Value, new Vector3(x + lamp.BoundsSize.x / 2f, 0f, 0f));
                x += lamp.BoundsSize.x + 0.15f;
            }
            return merged;
        }
    }
}

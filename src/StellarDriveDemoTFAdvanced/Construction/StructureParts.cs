using System;
using Ships.Interface.Settings;
using StellarDriveDemoTFAdvanced.Common;
using StellarDriveDemoTFAdvanced.Parts;
using UnityEngine;
using UnityEngine.Rendering;

namespace StellarDriveDemoTFAdvanced.Construction
{
    /// <summary>
    /// Advanced Construction, first set: beams, plates, an open frame and a junction. The game's
    /// hull is a voxel grid with four hard-coded shapes, so these are not hull: they are
    /// stateless placed parts (like the game's Battery Bank) with their own model, placed with
    /// the game's precise placement on any surface. Their collision is the game's box collider
    /// from the part bounds, so the open frame's hole is visual only.
    /// Part space: +Y points out of the surface; beams run along +Y. Only call when SDModKit is loaded.
    /// </summary>
    internal static class StructureParts
    {
        private const string Donor = "SignalDisplay";

        private sealed class Spec
        {
            public ushort Id;
            public string Label;
            public string Description;
            public float Mass;
            public int Iron;
            public Vector3 Size;
            public Action<MeshBuilder> Build;
        }

        private static readonly Spec[] All =
        {
            Beam(7331, Loc.Pick("Poutre 1 m", "Beam 1 m", "Viga 1 m"), 1f, 6f, 1),
            Beam(7332, Loc.Pick("Poutre 2 m", "Beam 2 m", "Viga 2 m"), 2f, 12f, 2),
            Beam(7333, Loc.Pick("Poutre 4 m", "Beam 4 m", "Viga 4 m"), 4f, 24f, 4),
            new Spec
            {
                Id = 7334, Label = Loc.Pick("Poutre renforcée 2 m", "Reinforced beam 2 m", "Viga reforzada 2 m"), Mass = 20f, Iron = 3, Size = new Vector3(0.2f, 2f, 0.2f),
                Description = Loc.Pick("Profilé en I de 2 m, plus large et plus lourd, pour les ossatures porteuses. Sort perpendiculairement à la surface où on la pose.", "2 m I-beam, wider and heavier, for load-bearing frames. Stands out perpendicular to the surface it is placed on.", "Viga en I de 2 m, más ancha y pesada, para estructuras portantes. Sobresale perpendicular a la superficie donde se coloca."),
                Build = m => IBeam(m, 2f)
            },
            new Spec
            {
                Id = 7335, Label = Loc.Pick("Plaque 1 × 1", "Plate 1 × 1", "Placa 1 × 1"), Mass = 10f, Iron = 2, Size = new Vector3(1f, 0.05f, 1f),
                Description = Loc.Pick("Plaque d'acier boulonnée de 1 × 1 m pour planchers, cloisons et ailes. Se pose à plat sur toute surface.", "Bolted 1 × 1 m steel plate for floors, walls and wings. Lies flat on any surface.", "Placa de acero atornillada de 1 × 1 m para suelos, paredes y alas. Se apoya plana sobre cualquier superficie."),
                Build = m => Plate(m, 1f, 1f)
            },
            new Spec
            {
                Id = 7336, Label = Loc.Pick("Plaque 2 × 1", "Plate 2 × 1", "Placa 2 × 1"), Mass = 20f, Iron = 4, Size = new Vector3(2f, 0.05f, 1f),
                Description = Loc.Pick("Plaque d'acier boulonnée de 2 × 1 m. Se pose à plat sur toute surface.", "Bolted 2 × 1 m steel plate. Lies flat on any surface.", "Placa de acero atornillada de 2 × 1 m. Se apoya plana sobre cualquier superficie."),
                Build = m => Plate(m, 2f, 1f)
            },
            new Spec
            {
                Id = 7337, Label = Loc.Pick("Cadre 1 × 1", "Frame 1 × 1", "Marco 1 × 1"), Mass = 6f, Iron = 2, Size = new Vector3(1f, 0.06f, 1f),
                Description = Loc.Pick("Cadre carré ouvert de 1 × 1 m pour les ossatures. Le vide est visuel : la collision du jeu reste une boîte pleine.", "Open 1 × 1 m square frame for structures. The gap is only visual: the game's collision stays a solid box.", "Marco cuadrado abierto de 1 × 1 m para estructuras. El hueco es solo visual: la colisión del juego sigue siendo un bloque macizo."),
                Build = m => Frame(m, 1f, 0.08f, 0.06f)
            },
            new Spec
            {
                Id = 7338, Label = Loc.Pick("Jonction", "Junction", "Unión"), Mass = 3f, Iron = 1, Size = new Vector3(0.36f, 0.3f, 0.36f),
                Description = Loc.Pick("Nœud d'assemblage à cinq départs pour raccorder des poutres.", "Five-way node to join beams.", "Nodo de cinco vías para unir vigas."),
                Build = Junction
            }
        };

        public static void Register()
        {
            foreach (Spec part in All)
            {
                Spec spec = part;
                PartFactory.Register(spec.Id, Donor, AdvancedTab.ConstructionName, AdvancedTab.ConstructionRow, (settings, prefab) => Configure(spec, settings, prefab));
            }
            AdvancedMod.Log.Msg($"registered {All.Length} structural parts");
        }

        private static void Configure(Spec spec, PartSettings settings, GameObject prefab)
        {
            PartFactory.Describe(settings, spec.Label, spec.Description, spec.Mass, new[] { (101u, spec.Iron) });
            // No state: a plain placed part, nothing to simulate or synchronize
            settings.internalStateClass = null;
            settings.snappingStyle = Ships.Interface.Model.SnappingStyle.PreciseOnAny;
            Transform visuals = PartFactory.BlankFromSignalDisplay(prefab, new Vector3(0f, spec.Size.y * 0.5f, 0f), spec.Size);

            // Long parts take the tall drawn panel, flat ones the wide one
            var mesh = new MeshBuilder { Panel = spec.Size.y > 0.5f ? GameSkin.TallPanel : GameSkin.PlainPanel };
            spec.Build(mesh);
            GameSkin.AddMesh(visuals, visuals.gameObject.layer, "TFA_Structure" + spec.Id, mesh,
                LitMaterials.Get("TFA_StructureSteel", new Color(0.58f, 0.6f, 0.63f), 0.7f, 0.4f), ShadowCastingMode.On);
        }

        private static Spec Beam(ushort id, string label, float length, float mass, int iron)
        {
            return new Spec
            {
                Id = id, Label = label, Mass = mass, Iron = iron, Size = new Vector3(0.14f, length, 0.14f),
                Description = Loc.Pick($"Poutre-caisson de {length:0} m (12 cm de section) avec platines aux deux bouts. Sort perpendiculairement à la surface où on la pose : verticale au sol, horizontale sur un mur.", $"{length:0} m box beam (12 cm section) with end plates at both ends. Stands out perpendicular to the surface it is placed on: upright on the ground, level on a wall.", $"Viga cajón de {length:0} m (sección de 12 cm) con placas en ambos extremos. Sobresale perpendicular a la superficie donde se coloca: vertical en el suelo, horizontal en una pared."),
                Build = m => SquareBeam(m, length)
            };
        }

        // Hollow square section, 12 cm, 8 mm walls, with a 14 cm end plate at each end
        private static void SquareBeam(MeshBuilder m, float length)
        {
            const float side = 0.12f, wall = 0.008f, plate = 0.01f;
            float half = side * 0.5f - wall * 0.5f;
            float y = length * 0.5f;
            float inner = length - 2f * plate;
            m.Box(new Vector3(half, y, 0f), new Vector3(wall, inner, side));
            m.Box(new Vector3(-half, y, 0f), new Vector3(wall, inner, side));
            m.Box(new Vector3(0f, y, half), new Vector3(side - 2f * wall, inner, wall));
            m.Box(new Vector3(0f, y, -half), new Vector3(side - 2f * wall, inner, wall));
            m.Box(new Vector3(0f, plate * 0.5f, 0f), new Vector3(0.14f, plate, 0.14f));
            m.Box(new Vector3(0f, length - plate * 0.5f, 0f), new Vector3(0.14f, plate, 0.14f));
        }

        // Two flanges and a web
        private static void IBeam(MeshBuilder m, float length)
        {
            const float width = 0.2f, flange = 0.016f, web = 0.012f, plate = 0.012f;
            float y = length * 0.5f;
            float inner = length - 2f * plate;
            m.Box(new Vector3(0f, y, width * 0.5f - flange * 0.5f), new Vector3(width, inner, flange));
            m.Box(new Vector3(0f, y, -width * 0.5f + flange * 0.5f), new Vector3(width, inner, flange));
            m.Box(new Vector3(0f, y, 0f), new Vector3(web, inner, width - 2f * flange));
            m.Box(new Vector3(0f, plate * 0.5f, 0f), new Vector3(width, plate, width));
            m.Box(new Vector3(0f, length - plate * 0.5f, 0f), new Vector3(width, plate, width));
        }

        // A plate with a bolt at each corner
        private static void Plate(MeshBuilder m, float width, float depth)
        {
            const float thickness = 0.04f;
            m.Box(new Vector3(0f, thickness * 0.5f, 0f), new Vector3(width, thickness, depth));
            float bx = width * 0.5f - 0.06f, bz = depth * 0.5f - 0.06f;
            foreach (Vector3 corner in new[] { new Vector3(bx, 0f, bz), new Vector3(-bx, 0f, bz), new Vector3(bx, 0f, -bz), new Vector3(-bx, 0f, -bz) })
                m.Box(corner + new Vector3(0f, thickness + 0.005f, 0f), new Vector3(0.03f, 0.01f, 0.03f));
        }

        // Four bars around a square opening
        private static void Frame(MeshBuilder m, float size, float bar, float thickness)
        {
            float y = thickness * 0.5f;
            float offset = size * 0.5f - bar * 0.5f;
            m.Box(new Vector3(0f, y, offset), new Vector3(size, thickness, bar));
            m.Box(new Vector3(0f, y, -offset), new Vector3(size, thickness, bar));
            m.Box(new Vector3(offset, y, 0f), new Vector3(bar, thickness, size - 2f * bar));
            m.Box(new Vector3(-offset, y, 0f), new Vector3(bar, thickness, size - 2f * bar));
        }

        // A hub with stubs toward the four sides and up, sized to the 12 cm beams
        private static void Junction(MeshBuilder m)
        {
            var hub = new Vector3(0f, 0.12f, 0f);
            m.Box(hub, new Vector3(0.16f, 0.16f, 0.16f));
            m.Box(new Vector3(0f, 0.01f, 0f), new Vector3(0.2f, 0.02f, 0.2f));
            m.Box(hub + new Vector3(0.13f, 0f, 0f), new Vector3(0.1f, 0.12f, 0.12f));
            m.Box(hub + new Vector3(-0.13f, 0f, 0f), new Vector3(0.1f, 0.12f, 0.12f));
            m.Box(hub + new Vector3(0f, 0f, 0.13f), new Vector3(0.12f, 0.12f, 0.1f));
            m.Box(hub + new Vector3(0f, 0f, -0.13f), new Vector3(0.12f, 0.12f, 0.1f));
            m.Box(hub + new Vector3(0f, 0.11f, 0f), new Vector3(0.12f, 0.06f, 0.12f));
        }
    }
}

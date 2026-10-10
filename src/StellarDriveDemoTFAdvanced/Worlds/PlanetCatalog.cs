using System;
using Core.Values;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Worlds
{
    internal sealed class PlanetSpec
    {
        public ulong Id;
        public string Name;
        public string Biome;
        /// <summary>Orbit position: the demo planet's position turned around the star's axis, and scaled.</summary>
        public float OrbitAngle;
        public float OrbitScale = 1f;
        public float Gravity = 1f;
        public float RotationHours = 1f;
        /// <summary>Its color on the ship's star map.</summary>
        public Color MapColor = Color.white;
        public Action<PlanetShaper.Context> Shape;
        /// <summary>An asteroid ring around the planet (the demo planet's, reshaped), or null for none.</summary>
        public Action<Planets.Rings.PlanetRing> Ring;

        /// <summary>Where the planet sits relative to the star (the demo planet: about 7.5 million km out).</summary>
        public Vector3Double Position(Vector3Double template)
        {
            if (Settings.DevPlanetsNearby.Value)
            {
                // Development: a few thousand kilometers from the demo planet, to be seen from its surface
                double angle = OrbitAngle * Math.PI / 180.0;
                return template + new Vector3Double(Math.Cos(angle) * 2.5e6, 0.6e6, Math.Sin(angle) * 2.5e6);
            }
            double a = OrbitAngle * Math.PI / 180.0;
            double x = template.x * Math.Cos(a) - template.z * Math.Sin(a);
            double z = template.x * Math.Sin(a) + template.z * Math.Cos(a);
            return new Vector3Double(x, template.y, z) * OrbitScale;
        }
    }

    /// <summary>
    /// The mod's planets. Built from the demo planet's own textures (grass, ice, sand), flora and
    /// points of interest, recolored and reshaped, plus the mod's wrecks and ruins.
    /// </summary>
    internal static class PlanetCatalog
    {
        public static readonly PlanetSpec[] All =
        {
            new PlanetSpec
            {
                Id = 101, Name = "TF Aridia", MapColor = new Color(0.95f, 0.62f, 0.3f), Biome = "désertique", OrbitAngle = 55f, OrbitScale = 1.15f, Gravity = 0.9f, RotationHours = 1.4f,
                Shape = p =>
                {
                    // Regions: sand dunes in the lowlands, red mesas above, dry salt flats where the demo planet has water
                    p.Material(PlanetShaper.Ground, PlanetShaper.Shore, new Color(0.62f, 0.33f, 0.22f));
                    p.Material(PlanetShaper.Shore, PlanetShaper.Shore, new Color(0.93f, 0.68f, 0.42f));
                    p.Material(PlanetShaper.Water, PlanetShaper.Shore, new Color(0.95f, 0.93f, 0.86f));
                    p.Shoreline(240f, 64.2f);
                    p.Relief(0.55f, 1.6f, new Vector3(0.31f, 0.17f, -0.23f));
                    p.Sky(new Vector3(0.22f, 0.14f, 0.07f), 2.2f, 0.25f, 0.85f, clouds: false, breathable: true);
                    GameObject acacia = p.Tree(TreeKind.Acacia);
                    GameObject dead = p.TreePrefab("FirTreeDead1");
                    // Savanna: acacias where trees grow at all, dead trees in the driest spots
                    p.Trees((acacia, 100, 0.5f), (dead, 40, 0f));
                    p.ForestDensity(0.2f);
                    p.Grass(PlanetShaper.Tinted(p.GrassPrefab, "dry", new Color(1f, 0.8f, 0.45f)));
                    PointsOfInterest.Wrecks(p, 0.7f);
                    PointsOfInterest.Ruins(p, RuinStyle.Sandstone, 0.6f);
                    PointsOfInterest.Village(p, RuinStyle.Sandstone, 0.5f);
                    PointsOfInterest.CargoWrecks(p, 0.5f);
                    PointsOfInterest.Deposits(p, OreKind.Quartz, 0.7f);
                }
            },
            new PlanetSpec
            {
                Ring = ring =>
                {
                    // A dark rubble belt (the demo ring's size: worlds saved inside it stay valid)
                    ring.ringConfig.AsteroidsTexLitColor = new Color(0.62f, 0.6f, 0.6f);
                    ring.ringConfig.ringAmbientColor *= 0.6f;
                },
                Id = 102, Name = "TF Petra", MapColor = new Color(0.6f, 0.6f, 0.65f), Biome = "rocheuse", OrbitAngle = -40f, OrbitScale = 0.8f, Gravity = 0.45f, RotationHours = 3f,
                Shape = p =>
                {
                    // Airless grey rock covered in craters, no plants
                    p.Material(PlanetShaper.Ground, PlanetShaper.Shore, new Color(0.47f, 0.47f, 0.5f));
                    p.Material(PlanetShaper.Shore, PlanetShaper.Shore, new Color(0.38f, 0.38f, 0.41f));
                    p.Material(PlanetShaper.Water, PlanetShaper.Shore, new Color(0.3f, 0.3f, 0.33f));
                    p.Relief(0.9f, 1.2f, new Vector3(-0.42f, 0.27f, 0.12f));
                    p.Craters(80, 1500f, 9000f, 40f, 160f);
                    p.NoAtmosphere();
                    p.FloraElevation(1e7f, 1e7f);
                    PointsOfInterest.Wrecks(p, 0.5f);
                    PointsOfInterest.Ruins(p, RuinStyle.Basalt, 0.5f);
                    PointsOfInterest.Village(p, RuinStyle.Basalt, 0.35f);
                    PointsOfInterest.CargoWrecks(p, 0.5f);
                    PointsOfInterest.Deposits(p, OreKind.Titanium, 0.8f);
                }
            },
            new PlanetSpec
            {
                Id = 103, Name = "TF Altis", MapColor = new Color(0.45f, 0.75f, 0.55f), Biome = "montagneuse", OrbitAngle = 150f, OrbitScale = 1.35f, Gravity = 1.1f, RotationHours = 1.2f,
                Shape = p =>
                {
                    // Regions: green valleys, snow above 1 400 m, glacier lakes; pine forests and birch groves
                    p.Material(PlanetShaper.Shore, PlanetShaper.Ground, new Color(0.45f, 0.55f, 0.42f));
                    p.Material(PlanetShaper.Ground, PlanetShaper.Water, new Color(0.95f, 0.97f, 1f));
                    p.Shoreline(1400f, 64.2f);
                    GameObject birch = p.Tree(TreeKind.Birch);
                    GameObject autumnBirch = p.Tree(TreeKind.AutumnBirch);
                    p.Relief(2.6f, 0.8f, new Vector3(0.12f, -0.36f, 0.29f));
                    p.Ceiling(8000f);
                    p.Sky(new Vector3(0.035f, 0.13f, 0.36f), 2.7f, 0.12f, 0.75f, clouds: true, breathable: true);
                    // One tree layer (separate layers overlap and knock each other down): pines in the
                    // dense forests, birch groves along their edges
                    p.Trees((p.TreePrefab("FirTreeTall1"), 90, 0.8f), (p.TreePrefab("FirTreeShort1"), 70, 0.55f),
                        (PlanetShaper.Tinted(p.TreePrefab("FirTreeTall1"), "darkpine", new Color(0.55f, 0.7f, 0.6f)), 60, 0.9f),
                        (birch, 80, 0.2f), (autumnBirch, 30, 0.15f), (p.TreePrefab("FirTreeDead1"), 4, 0.6f));
                    p.ForestDensity(1.4f);
                    PointsOfInterest.Ruins(p, RuinStyle.Granite, 0.5f);
                    PointsOfInterest.Village(p, RuinStyle.Granite, 0.35f);
                    PointsOfInterest.Deposits(p, OreKind.Titanium, 0.5f);
                    PointsOfInterest.Wrecks(p, 0.3f);
                }
            },
            new PlanetSpec
            {
                Id = 104, Name = "TF Verdance", MapColor = new Color(0.4f, 0.9f, 0.35f), Biome = "tempérée (proche de la planète de départ)", OrbitAngle = -120f, OrbitScale = 0.95f, Gravity = 0.95f, RotationHours = 0.9f,
                Shape = p =>
                {
                    // Like the demo planet, greener, with wide lakes; conifer forests and oak/birch woods in their own regions
                    p.Material(PlanetShaper.Ground, PlanetShaper.Ground, new Color(0.56f, 0.78f, 0.42f));
                    GameObject oak = p.Tree(TreeKind.Oak), autumnOak = p.Tree(TreeKind.AutumnOak);
                    GameObject birch = p.Tree(TreeKind.Birch), autumnBirch = p.Tree(TreeKind.AutumnBirch);
                    p.Relief(0.8f, 1.1f, new Vector3(0.53f, 0.08f, 0.44f));
                    p.Shoreline(140f, 128f);
                    // One tree layer: conifers in the deep forest, oak woods around them, birches at the edges
                    p.Trees((p.TreePrefab("FirTreeTall1"), 70, 0.9f), (p.TreePrefab("FirTreeShort1"), 40, 0.75f),
                        (PlanetShaper.Tinted(p.TreePrefab("FirTreeTall1"), "autumn", new Color(1.25f, 0.7f, 0.35f)), 30, 0.85f),
                        (oak, 100, 0.5f), (autumnOak, 45, 0.45f), (birch, 60, 0.15f), (autumnBirch, 25, 0.1f));
                    p.ForestDensity(0.9f);
                    p.Grass(PlanetShaper.Tinted(p.GrassPrefab, "lush", new Color(0.85f, 1.1f, 0.8f)));
                    PointsOfInterest.Ruins(p, RuinStyle.Mossy, 0.6f);
                    PointsOfInterest.Village(p, RuinStyle.Mossy, 0.5f);
                    PointsOfInterest.CargoWrecks(p, 0.4f);
                    PointsOfInterest.Deposits(p, OreKind.Amber, 0.5f);
                    PointsOfInterest.Wrecks(p, 0.4f);
                }
            },
            new PlanetSpec
            {
                Ring = ring =>
                {
                    // A bright ring of ice and rock (the demo ring's size)
                    ring.ringConfig.AsteroidsTexLitColor = new Color(0.85f, 0.95f, 1.1f);
                    ring.ringConfig.ringDirectScatteringColor *= 1.4f;
                },
                Id = 105, Name = "TF Glacia", MapColor = new Color(0.75f, 0.9f, 1f), Biome = "glaciaire", OrbitAngle = 95f, OrbitScale = 1.6f, Gravity = 1f, RotationHours = 1.6f,
                Shape = p =>
                {
                    // Frozen world: ice sheets, snowy mountains up to 9 km, frozen lakes
                    p.Material(PlanetShaper.Shore, PlanetShaper.Water, new Color(0.72f, 0.86f, 0.98f));
                    p.Material(PlanetShaper.Ground, PlanetShaper.Water, new Color(1f, 1f, 1f));
                    p.Material(PlanetShaper.Water, PlanetShaper.Water, new Color(0.55f, 0.75f, 0.95f));
                    p.Shoreline(600f, 64.2f);
                    p.Relief(2.4f, 0.9f, new Vector3(-0.27f, 0.41f, -0.15f));
                    p.Ceiling(9000f);
                    p.Sky(new Vector3(0.05f, 0.12f, 0.3f), 2.4f, 0.35f, 0.9f, clouds: true, breathable: true);
                    // A few frosted firs in the valleys, no grass
                    GameObject frosted = PlanetShaper.Tinted(p.TreePrefab("FirTreeTall1"), "frosted", new Color(0.8f, 0.92f, 1.05f));
                    GameObject frostedShort = PlanetShaper.Tinted(p.TreePrefab("FirTreeShort1"), "frosted", new Color(0.8f, 0.92f, 1.05f));
                    p.Trees((frosted, 60, 0.4f), (frostedShort, 60, 0f), (p.TreePrefab("FirTreeDead1"), 30, 0f));
                    p.ForestDensity(-0.15f);
                    p.FloraElevation(80f, 1e7f);
                    PointsOfInterest.Ruins(p, RuinStyle.Ice, 0.5f);
                    PointsOfInterest.Village(p, RuinStyle.Ice, 0.3f);
                    PointsOfInterest.Wrecks(p, 0.4f);
                    PointsOfInterest.CargoWrecks(p, 0.35f);
                    PointsOfInterest.Deposits(p, OreKind.Cryonite, 0.8f);
                    PointsOfInterest.Deposits(p, OreKind.Titanium, 0.4f);
                }
            },
            new PlanetSpec
            {
                Id = 106, Name = "TF Nexora", MapColor = new Color(0.65f, 0.35f, 1f), Biome = "futuriste (cité abandonnée)", OrbitAngle = -170f, OrbitScale = 1.05f, Gravity = 0.85f, RotationHours = 0.8f,
                Shape = p =>
                {
                    // Flat dark plains under a violet sky, the ruins of a high-tech city everywhere
                    p.Material(PlanetShaper.Ground, PlanetShaper.Shore, new Color(0.32f, 0.33f, 0.38f));
                    p.Material(PlanetShaper.Shore, PlanetShaper.Shore, new Color(0.24f, 0.25f, 0.3f));
                    p.Material(PlanetShaper.Water, PlanetShaper.Water, new Color(0.2f, 0.85f, 0.9f));
                    p.Relief(0.35f, 0.8f, new Vector3(0.19f, -0.33f, 0.47f));
                    p.Sky(new Vector3(0.16f, 0.05f, 0.3f), 2.6f, 0.2f, 0.8f, clouds: false, breathable: true);
                    // Engineered trees: cyan-leaved birches in the parks, teal grass
                    GameObject neon = PlanetShaper.Tinted(p.Tree(TreeKind.Birch), "neon", new Color(0.15f, 1.1f, 1.7f));
                    p.Trees((neon, 100, 0f));
                    p.ForestDensity(-0.25f);
                    p.Grass(PlanetShaper.Tinted(p.GrassPrefab, "teal", new Color(0.5f, 1f, 1f)));
                    PointsOfInterest.City(p, 1f);
                    PointsOfInterest.Deposits(p, OreKind.Quartz, 0.5f);
                    PointsOfInterest.CargoWrecks(p, 0.5f);
                    PointsOfInterest.Wrecks(p, 0.4f);
                }
            },
            new PlanetSpec
            {
                Id = 107, Name = "TF Sylvara", MapColor = new Color(0.2f, 0.75f, 0.35f), Biome = "jungle", OrbitAngle = 20f, OrbitScale = 0.88f, Gravity = 1.05f, RotationHours = 1.1f,
                Shape = p =>
                {
                    // Steamy jungle: deep green ground, muddy banks, warm rivers and lakes, rolling hills
                    p.Material(PlanetShaper.Ground, PlanetShaper.Ground, new Color(0.38f, 0.62f, 0.3f));
                    p.Material(PlanetShaper.Shore, PlanetShaper.Shore, new Color(0.5f, 0.38f, 0.26f));
                    p.Material(PlanetShaper.Water, PlanetShaper.Water, new Color(0.45f, 0.75f, 0.6f));
                    p.Shoreline(110f, 90f);
                    p.Relief(1.25f, 1.3f, new Vector3(-0.31f, 0.22f, 0.38f));
                    p.Sky(new Vector3(0.06f, 0.16f, 0.2f), 2.5f, 0.55f, 0.85f, clouds: true, breathable: true);
                    // Plants first: they are built from the demo's grass and fir, before the tree mix changes
                    GameObject fern = p.Plant(PlantKind.Fern), bush = p.Plant(PlantKind.Bush);
                    GameObject red = p.Plant(PlantKind.RedFlowers), yellow = p.Plant(PlantKind.YellowFlowers);
                    GameObject violet = p.Plant(PlantKind.VioletFlowers), white = p.Plant(PlantKind.WhiteFlowers);
                    GameObject grass = PlanetShaper.Tinted(p.GrassPrefab, "jungle", new Color(0.7f, 1.05f, 0.65f));
                    GameObject kapok = p.Tree(TreeKind.Kapok), palm = p.Tree(TreeKind.Palm), jungle = p.Tree(TreeKind.Jungle);
                    // One tree layer: giants in the deep jungle, canopy trees around them, palms in the clearings
                    p.Trees((kapok, 35, 0.95f), (jungle, 100, 0.7f), (palm, 60, 0.25f), (p.Tree(TreeKind.Oak), 20, 0.5f));
                    p.ForestDensity(1.5f);
                    p.Undergrowth(0.6f, (grass, 50), (fern, 30), (bush, 14), (red, 5), (yellow, 5), (violet, 4), (white, 4));
                    PointsOfInterest.Ruins(p, RuinStyle.Mossy, 0.7f);
                    PointsOfInterest.Village(p, RuinStyle.Mossy, 0.45f);
                    PointsOfInterest.Wrecks(p, 0.35f);
                    PointsOfInterest.CargoWrecks(p, 0.3f);
                    PointsOfInterest.Deposits(p, OreKind.Amber, 0.8f);
                }
            }
        };
    }
}

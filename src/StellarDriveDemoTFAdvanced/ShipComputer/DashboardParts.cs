
using Ships.Interface.Settings;
using StellarDriveDemoTFAdvanced.Common;
using StellarDriveDemoTFAdvanced.Parts;
using UnityEngine;
using UnityEngine.Rendering;

namespace StellarDriveDemoTFAdvanced.ShipComputer
{
    /// <summary>
    /// The ship computer as placeable parts: a wall screen and a floor console showing the four
    /// sections, and four small single-section screens. They need no plug and no key: each shows
    /// the ship it is built on. Part space: +Y points out of the surface the part is placed on.
    /// Only call Register when SDModKit is loaded.
    /// </summary>
    internal static class DashboardParts
    {
        private const string Donor = "SignalDisplay";

        public const ushort WallScreenId = 7301;
        public const ushort ConsoleId = 7302;
        public const ushort FlightScreenId = 7303;
        public const ushort PropulsionScreenId = 7304;
        public const ushort PowerScreenId = 7305;
        public const ushort DiagnosticsScreenId = 7306;

        private static readonly (uint, int)[] LargeCost = { (101u, 3), (102u, 3), (112u, 3) };
        private static readonly (uint, int)[] SmallCost = { (101u, 1), (102u, 1), (112u, 1) };

        public static void Register()
        {
            PartFactory.Register(WallScreenId, Donor, AdvancedTab.ComputersRow, ConfigureWallScreen);
            PartFactory.Register(ConsoleId, Donor, AdvancedTab.ComputersRow, ConfigureConsole);
            RegisterSmall(FlightScreenId, "flight", "Écran de vol",
                "Petit écran FLIGHT : vitesses, altitude au-dessus du rayon moyen, assiette, rotation et trafic du vaisseau où il est posé.");
            RegisterSmall(PropulsionScreenId, "propulsion", "Écran de propulsion",
                "Petit écran PROPULSION : poussée et débit de chaque propulseur, poussée nette, gyroscopes, cœur stellaire.");
            RegisterSmall(PowerScreenId, "power", "Écran d'énergie",
                "Petit écran POWER : générateurs, consommateurs et réseaux électriques du vaisseau.");
            RegisterSmall(DiagnosticsScreenId, "diagnostics", "Écran de diagnostic",
                "Petit écran DIAGNOSTICS : alertes vérifiées à partir des données du jeu (propulseur mal alimenté, gyroscope non branché, réseau sous-alimenté, rapprochement…).");
            AdvancedMod.Log.Msg("registered 6 ship computer screens");
        }

        private static void ConfigureWallScreen(PartSettings settings, GameObject prefab)
        {
            PartFactory.Describe(settings, "Ordinateur de bord (mural)",
                "Écran d'ingénierie à fixer sur un mur, un plafond ou la coque. Affiche en continu les quatre sections FLIGHT, PROPULSION, POWER et DIAGNOSTICS du vaisseau où il est posé, à partir des données réelles du jeu. Aucune prise ni touche : tourne-le en le posant pour l'orienter.",
                8f, LargeCost);
            var size = new Vector3(1.04f, 0.07f, 0.66f);
            Transform visuals = PartFactory.BlankFromSignalDisplay(prefab, new Vector3(0f, size.y * 0.5f, 0f), size);

            var frame = new MeshBuilder { Panel = GameSkin.PlainPanel };
            frame.Box(new Vector3(0f, 0.03f, 0f), new Vector3(1.04f, 0.06f, 0.66f));
            // Raised bezel around the glass
            frame.Box(new Vector3(0f, 0.064f, 0.31f), new Vector3(1.04f, 0.008f, 0.04f));
            frame.Box(new Vector3(0f, 0.064f, -0.31f), new Vector3(1.04f, 0.008f, 0.04f));
            frame.Box(new Vector3(0.5f, 0.064f, 0f), new Vector3(0.04f, 0.008f, 0.58f));
            frame.Box(new Vector3(-0.5f, 0.064f, 0f), new Vector3(0.04f, 0.008f, 0.58f));
            var glass = new MeshBuilder();
            glass.Box(new Vector3(0f, 0.061f, 0f), new Vector3(0.96f, 0.002f, 0.58f));

            AddScreen(visuals, frame, glass, new Vector3(0f, 0.0625f, 0f), Vector3.up, Vector3.forward, DashboardVisuals.AllSections, 960f, 580f);
        }

        private static void ConfigureConsole(PartSettings settings, GameObject prefab)
        {
            PartFactory.Describe(settings, "Console de bord",
                "Pupitre à poser au sol, écran incliné vers le pilote. Affiche les quatre sections FLIGHT, PROPULSION, POWER et DIAGNOSTICS du vaisseau où il est posé. Aucune prise ni touche.",
                30f, new[] { (101u, 6), (102u, 3), (112u, 3) });
            Transform visuals = PartFactory.BlankFromSignalDisplay(prefab, new Vector3(0f, 0.8f, 0.06f), new Vector3(1.04f, 1.6f, 0.72f));

            // Screen leaning back 40 degrees, facing +Z
            Vector3 normal = new Vector3(0f, Mathf.Sin(40f * Mathf.Deg2Rad), Mathf.Cos(40f * Mathf.Deg2Rad));
            Vector3 screenUp = new Vector3(0f, normal.z, -normal.y);
            // High enough that the screen's lower edge clears the pedestal (top at 0.93 m)
            Vector3 center = new Vector3(0f, 1.3f, 0.1f);

            var frame = new MeshBuilder { Panel = GameSkin.PlainPanel };
            frame.Box(new Vector3(0f, 0.02f, 0f), new Vector3(0.9f, 0.04f, 0.6f));
            frame.Box(new Vector3(0f, 0.48f, -0.06f), new Vector3(0.7f, 0.9f, 0.34f));
            // Neck between the pedestal and the screen housing
            frame.Box(new Vector3(0f, 1.06f, 0.0f), new Vector3(0.5f, 0.3f, 0.24f));
            frame.Box(center - normal * 0.03f, new Vector3(1.04f, 0.06f, 0.66f), Vector3.right, normal, screenUp);
            var glass = new MeshBuilder();
            glass.Box(center + normal * 0.001f, new Vector3(0.96f, 0.002f, 0.58f), Vector3.right, normal, screenUp);

            AddScreen(visuals, frame, glass, center + normal * 0.0025f, normal, screenUp, DashboardVisuals.AllSections, 960f, 580f);
        }

        private static void RegisterSmall(ushort id, string layout, string label, string description)
        {
            PartFactory.Register(id, Donor, AdvancedTab.ComputersRow, (settings, prefab) =>
            {
                PartFactory.Describe(settings, label, description + " Aucune prise ni touche.", 3f, SmallCost);
                var size = new Vector3(0.56f, 0.06f, 0.38f);
                Transform visuals = PartFactory.BlankFromSignalDisplay(prefab, new Vector3(0f, size.y * 0.5f, 0f), size);
                var frame = new MeshBuilder { Panel = GameSkin.PlainPanel };
                frame.Box(new Vector3(0f, 0.025f, 0f), new Vector3(0.56f, 0.05f, 0.38f));
                var glass = new MeshBuilder();
                glass.Box(new Vector3(0f, 0.051f, 0f), new Vector3(0.52f, 0.002f, 0.34f));
                AddScreen(visuals, frame, glass, new Vector3(0f, 0.0525f, 0f), Vector3.up, Vector3.forward, layout, 520f, 340f);
            });
        }

        /// <summary>Frame (takes the paint), dark glass, and the anchor the screen's canvas is built on.</summary>
        private static void AddScreen(Transform visuals, MeshBuilder frame, MeshBuilder glass, Vector3 position, Vector3 facing, Vector3 up,
            string layout, float width, float height)
        {
            int layer = visuals.gameObject.layer;
            GameSkin.AddMesh(visuals, layer, "TFA_Frame", frame, LitMaterials.Get("TFA_Frame", new Color(0.26f, 0.28f, 0.31f), 0.55f, 0.45f), ShadowCastingMode.On);
            // "TF_NoPaint" keeps the TF paint tool off the glass
            AddMesh(visuals, layer, "TF_NoPaint_ScreenGlass", glass,
                LitMaterials.Get("TFA_ScreenGlass", new Color(0.02f, 0.03f, 0.04f), 0f, 0.92f, new Color(0.01f, 0.018f, 0.025f)), ShadowCastingMode.Off);

            var anchor = new GameObject(DashboardVisuals.AnchorName(layout, width, height)) { layer = layer };
            anchor.transform.SetParent(visuals, false);
            anchor.transform.localPosition = position;
            // A canvas faces -Z: turn it so its front looks along `facing`, text upright along `up`
            anchor.transform.localRotation = Quaternion.LookRotation(-facing, up);
            visuals.gameObject.AddComponent<DashboardVisuals>();
        }

        private static void AddMesh(Transform parent, int layer, string name, MeshBuilder builder, Material material, ShadowCastingMode shadows)
        {
            var child = new GameObject(name) { layer = layer };
            child.transform.SetParent(parent, false);
            child.AddComponent<MeshFilter>().sharedMesh = builder.Build(name);
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows;
        }
    }
}

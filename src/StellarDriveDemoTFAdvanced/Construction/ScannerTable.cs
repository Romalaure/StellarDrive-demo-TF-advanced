using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Services;
using Planets.Interface.Model;
using Planets.Interface.Services;
using Planets.Objects;
using Ships.Interface.Settings;
using Ships.Parts.Common;
using StellarDriveDemoTFAdvanced.Common;
using StellarDriveDemoTFAdvanced.Parts;
using StellarDriveDemoTFAdvanced.Power;
using StellarDriveDemoTFAdvanced.ShipComputer;
using StellarDriveDemoTFAdvanced.Worlds;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using WorldTracking.Interface.Values;

namespace StellarDriveDemoTFAdvanced.Construction
{
    /// <summary>
    /// Cartography table: a holographic table that scans the ground around the ship (a relief map
    /// of the terrain out to <see cref="Range"/>), marks what it finds (ore deposits and rocks,
    /// chests, buildings, other ships) and shows the planet's wiki page (biome, gravity, air,
    /// resources, places, use). Each player's game scans for itself (no host simulation), while
    /// the player stands near the table.
    /// </summary>
    internal static class ScannerTable
    {
        public const ushort Id = 7381;
        public const float Range = 300f;
        /// <summary>The table top is square: the hologram's map fits inside its rim.</summary>
        public const float Size = 1.4f;

        public static void Register()
        {
            PartFactory.Register(Id, "SignalDisplay", AdvancedTab.Name, AdvancedTab.ComputersRow, Configure);
        }

        private static void Configure(PartSettings settings, GameObject prefab)
        {
            PartFactory.Describe(settings, Loc.Pick("Table de cartographie", "Cartography table", "Mesa de cartografía"),
                Loc.Pick($"Une table holographique qui cartographie le terrain sur {Range:0} m autour du vaisseau (relief en 3D), signale les gisements, rochers, coffres, bâtiments et vaisseaux, et affiche la fiche de la planète : biome, gravité, air, ressources, lieux et utilité. S'allume quand un joueur s'en approche.", $"A holographic table that maps the ground {Range:0} m around the ship (3D relief), marks deposits, rocks, chests, buildings and ships, and shows the planet's card: biome, gravity, air, resources, places and use. Lights up when a player comes near.", $"Una mesa holográfica que cartografía el terreno {Range:0} m alrededor de la nave (relieve 3D), marca yacimientos, rocas, cofres, edificios y naves, y muestra la ficha del planeta: bioma, gravedad, aire, recursos, lugares y utilidad. Se ilumina cuando un jugador se acerca."),
                60f, new[] { (112u, 10), (102u, 10), (101u, 6), (Items.ModItems.Meteorite, 2) });
            settings.internalStateClass = null;
            const float width = Size, height = 0.95f, depth = Size;
            Transform visuals = PartFactory.BlankFromSignalDisplay(prefab, new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, depth));
            var model = new MachineModels.Model();
            // A two-step octagonal plinth, a tapered pedestal with glowing edges and a collar, vents
            // on its front and back, four struts under the top
            model.Armor.Cylinder(new Vector3(0f, 0f, 0f), new Vector3(0f, 0.05f, 0f), 0.5f, 0.48f, 8);
            model.Light.Cylinder(new Vector3(0f, 0.05f, 0f), new Vector3(0f, 0.056f, 0f), 0.47f, 0.47f, 8, capA: false, capB: false);
            model.Armor.Cylinder(new Vector3(0f, 0.056f, 0f), new Vector3(0f, 0.1f, 0f), 0.42f, 0.38f, 8);
            model.Dark.Cylinder(new Vector3(0f, 0.1f, 0f), new Vector3(0f, 0.12f, 0f), 0.36f, 0.35f, 8);
            model.Armor.Cylinder(new Vector3(0f, 0.12f, 0f), new Vector3(0f, 0.7f, 0f), 0.34f, 0.28f, 8);
            for (int i = 0; i < 4; i++)
            {
                // Light strips down the pedestal's diagonal edges (the octagon's corners at 22.5° + k·45°)
                float a = (22.5f + 45f + i * 90f) * Mathf.Deg2Rad;
                var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 low = new Vector3(0f, 0.16f, 0f) + outward * 0.335f, high = new Vector3(0f, 0.66f, 0f) + outward * 0.287f;
                model.Glow.Box(Vector3.Lerp(low, high, 0.5f) + outward * 0.006f, new Vector3(0.014f, (high - low).magnitude, 0.006f), Quaternion.LookRotation(outward, (high - low).normalized));
            }
            model.Armor.Cylinder(new Vector3(0f, 0.6f, 0f), new Vector3(0f, 0.64f, 0f), 0.33f, 0.33f, 8);
            model.Glow.Cylinder(new Vector3(0f, 0.615f, 0f), new Vector3(0f, 0.625f, 0f), 0.334f, 0.334f, 8, capA: false, capB: false);
            foreach (float face in new[] { 0.29f, -0.29f })
                MachineModels.Vent(model, 0f, 0.38f, face, 0.3f, 0.3f, 4);
            foreach (float x in new[] { -1f, 1f })
                foreach (float z in new[] { -1f, 1f })
                {
                    model.Armor.Cylinder(new Vector3(x * 0.22f, 0.5f, z * 0.22f), new Vector3(x * (width * 0.42f), height - 0.08f, z * (depth * 0.42f)), 0.03f, 0.022f, 8);
                    model.Dark.Cylinder(new Vector3(x * 0.2f, 0.47f, z * 0.2f), new Vector3(x * 0.24f, 0.53f, z * 0.24f), 0.045f, 0.045f, 8);
                }
            // The top: a chamfered frame around a dark glass with a glowing grid
            model.Armor.Block(new[]
            {
                new Vector3(-width * 0.44f, height - 0.1f, -depth * 0.44f), new Vector3(width * 0.44f, height - 0.1f, -depth * 0.44f), new Vector3(-width * 0.5f, height - 0.05f, -depth * 0.5f), new Vector3(width * 0.5f, height - 0.05f, -depth * 0.5f),
                new Vector3(-width * 0.44f, height - 0.1f, depth * 0.44f), new Vector3(width * 0.44f, height - 0.1f, depth * 0.44f), new Vector3(-width * 0.5f, height - 0.05f, depth * 0.5f), new Vector3(width * 0.5f, height - 0.05f, depth * 0.5f)
            });
            float rim = 0.08f;
            model.Armor.Box(new Vector3(0f, height - 0.025f, depth * 0.5f - rim * 0.5f), new Vector3(width, 0.05f, rim));
            model.Armor.Box(new Vector3(0f, height - 0.025f, -depth * 0.5f + rim * 0.5f), new Vector3(width, 0.05f, rim));
            model.Armor.Box(new Vector3(width * 0.5f - rim * 0.5f, height - 0.025f, 0f), new Vector3(rim, 0.05f, depth - 2f * rim));
            model.Armor.Box(new Vector3(-width * 0.5f + rim * 0.5f, height - 0.025f, 0f), new Vector3(rim, 0.05f, depth - 2f * rim));
            model.Dark.Box(new Vector3(0f, height - 0.03f, 0f), new Vector3(width - 2f * rim, 0.03f, depth - 2f * rim));
            float inner = width - 2f * rim - 0.04f;
            for (int i = 1; i < 10; i++)
            {
                float t = -inner * 0.5f + inner * i / 10f;
                model.Glow.Box(new Vector3(t, height - 0.013f, 0f), new Vector3(0.004f, 0.002f, inner), null);
                model.Glow.Box(new Vector3(0f, height - 0.013f, t), new Vector3(inner, 0.002f, 0.004f), null);
            }
            // The emitter in the middle: a raised lens in rings of light
            model.Armor.Cylinder(new Vector3(0f, height - 0.02f, 0f), new Vector3(0f, height + 0.005f, 0f), 0.08f, 0.07f, 16);
            model.Core.Cylinder(new Vector3(0f, height + 0.005f, 0f), new Vector3(0f, height + 0.02f, 0f), 0.05f, 0.035f, 16);
            foreach (float r in new[] { 0.1f, 0.16f })
                model.Glow.Cylinder(new Vector3(0f, height - 0.014f, 0f), new Vector3(0f, height - 0.01f, 0f), r, r, 24, capA: false, capB: false);
            // Light strips along the rim, projector studs at the corners
            foreach (float z in new[] { depth * 0.5f + 0.003f, -depth * 0.5f - 0.003f })
                model.Light.Box(new Vector3(0f, height - 0.025f, z), new Vector3(width * 0.92f, 0.01f, 0.01f));
            foreach (float x in new[] { width * 0.5f + 0.003f, -width * 0.5f - 0.003f })
                model.Light.Box(new Vector3(x, height - 0.025f, 0f), new Vector3(0.01f, 0.01f, depth * 0.92f));
            // Projector pylons at the corners, leaning in toward the map, a lens on each
            foreach (float x in new[] { -1f, 1f })
                foreach (float z in new[] { -1f, 1f })
                {
                    Vector3 c = new Vector3(x * (width * 0.5f - rim * 0.5f), height, z * (depth * 0.5f - rim * 0.5f));
                    Vector3 lean = new Vector3(-x * 0.03f, 0.11f, -z * 0.03f);
                    model.Armor.Cylinder(c, c + Vector3.up * 0.025f, 0.035f, 0.032f, 8);
                    model.Armor.Cylinder(c + Vector3.up * 0.025f, c + lean, 0.022f, 0.016f, 8);
                    model.Light.Cylinder(c + lean * 0.5f, c + lean * 0.5f + lean.normalized * 0.006f, 0.022f, 0.022f, 8, capA: false, capB: false);
                    model.Core.Cylinder(c + lean, c + lean + lean.normalized * 0.025f, 0.018f, 0.008f, 8);
                }
            // A console on the front edge: a tilted keyboard and two small screens
            // The keyboard rises toward the table, the screens lean back: both face the player standing at the front (-Z)
            Quaternion tilt = Quaternion.Euler(-25f, 0f, 0f);
            Vector3 desk = new Vector3(0f, height - 0.08f, -depth * 0.5f - 0.1f);
            model.Armor.Box(desk, new Vector3(width * 0.6f, 0.04f, 0.2f), tilt);
            for (int row = 0; row < 3; row++)
                for (int k = 0; k < 12; k++)
                    model.Dark.Box(desk + tilt * new Vector3(-width * 0.27f + k * width * 0.049f, 0.024f, -0.06f + row * 0.045f), new Vector3(0.03f, 0.008f, 0.03f), tilt);
            foreach (float x in new[] { -width * 0.36f, width * 0.36f })
            {
                model.Dark.Box(desk + new Vector3(x, 0.03f, 0.02f), new Vector3(0.16f, 0.1f, 0.01f), Quaternion.Euler(20f, 0f, 0f));
                model.Glow.Box(desk + new Vector3(x, 0.03f, 0.014f), new Vector3(0.13f, 0.075f, 0.003f), Quaternion.Euler(20f, 0f, 0f));
            }
            model.Armor.Box(desk + new Vector3(0f, -0.15f, 0.05f), new Vector3(width * 0.3f, 0.26f, 0.06f));
            // A cable duct down the back to the floor
            model.Dark.Cylinder(new Vector3(0.15f, 0.08f, 0.3f), new Vector3(0.15f, height - 0.1f, depth * 0.4f), 0.03f, 0.03f, 8);
            model.Dark.Cylinder(new Vector3(-0.15f, 0.08f, 0.3f), new Vector3(-0.15f, height - 0.1f, depth * 0.4f), 0.03f, 0.03f, 8);
            MachineModels.Attach(visuals, model, "Scanner", MachineModels.Cyan, MachineModels.Cyan);
            visuals.gameObject.AddComponent<ScannerVisuals>();
        }
    }

    /// <summary>
    /// The table's hologram: relief, markers and the planet's page, rebuilt every few seconds while
    /// someone is near; between scans it is animated by moving transforms only (a scan blade going
    /// round, a pulse spreading out, spinning rings over the emitter, markers breathing), no mesh work.
    /// </summary>
    internal sealed class ScannerVisuals : DefaultShipPartVisuals
    {
        private const int Grid = 32;
        // The map stays inside the table's rim (ScannerTable.Size), the page above it
        private const float MapHalf = ScannerTable.Size * 0.5f - 0.11f, MapHeight = 0.4f, Top = 0.96f;
        private const float ScanSeconds = 3f;
        private static Material _holoMaterial, _panelMaterial;
        private static Mesh _bladeMesh, _ringMesh, _beamMesh, _quadMesh;

        private GameObject _holo;
        private MeshFilter _relief;
        private MeshFilter _markers;
        private Transform _blade, _pulse, _ringA, _ringB, _panel, _pageRoot;
        private TextMeshPro _page;
        private string _pageText;
        private float _next;
        private float _baseline;
        private readonly Vector3[] _heights = new Vector3[(Grid + 1) * (Grid + 1)];

        private void LateUpdate()
        {
            Transform root = transform.root;
            if (root == null || !root.name.StartsWith("ship "))
                return;
            Camera camera = MainCamera.Get();
            bool near = camera != null && (camera.transform.position - transform.position).sqrMagnitude < 25f * 25f;
            if (!near)
            {
                if (_holo != null && _holo.activeSelf)
                    _holo.SetActive(false);
                return;
            }
            if (_holo == null)
                Build();
            if (!_holo.activeSelf)
                _holo.SetActive(true);
            if (Time.unscaledTime >= _next)
            {
                _next = Time.unscaledTime + ScanSeconds;
                try
                {
                    Scan(root);
                }
                catch (System.Exception e)
                {
                    ErrorLog.Report("cartography table", e);
                }
            }
            Animate();
            // The page turns to the viewer, about the table's up axis
            Vector3 toCamera = Vector3.ProjectOnPlane(camera.transform.position - _pageRoot.position, transform.up);
            if (toCamera.sqrMagnitude > 0.01f)
                _pageRoot.rotation = Quaternion.LookRotation(-toCamera.normalized, transform.up);
        }

        private void Animate()
        {
            float t = Time.unscaledTime;
            // One turn of the blade and one pulse per scan
            float phase = 1f - Mathf.Clamp01((_next - t) / ScanSeconds);
            _blade.localRotation = Quaternion.Euler(0f, phase * 360f, 0f);
            float pulse = Mathf.Max(0.02f, phase);
            _pulse.localScale = new Vector3(pulse, 1f, pulse);
            _ringA.localRotation = Quaternion.Euler(12f, t * 70f, 0f);
            _ringB.localRotation = Quaternion.Euler(-16f, -t * 45f, 0f);
            _markers.transform.localScale = new Vector3(1f, 1f + 0.18f * Mathf.Sin(t * 4f), 1f);
        }

        private void Build()
        {
            if (_holoMaterial == null)
                _holoMaterial = TransparentMaterials.Additive("TFA_ScannerHolo", null);
            if (_panelMaterial == null)
                _panelMaterial = TransparentMaterials.Blended("TFA_ScannerPanel", new Color(0.01f, 0.03f, 0.05f, 0.82f));
            BuildMeshes();
            _holo = new GameObject("TFA_ScannerHologram") { layer = gameObject.layer };
            _holo.transform.SetParent(transform, false);
            _holo.transform.localPosition = new Vector3(0f, Top, 0f);
            _relief = Child("Relief", _holoMaterial);
            _markers = Child("Markers", _holoMaterial);
            Child("Beam", _holoMaterial).sharedMesh = _beamMesh;
            MeshFilter blade = Child("Blade", _holoMaterial);
            blade.sharedMesh = _bladeMesh;
            _blade = blade.transform;
            MeshFilter pulse = Child("Pulse", _holoMaterial);
            pulse.sharedMesh = _ringMesh;
            _pulse = pulse.transform;
            _pulse.localScale = new Vector3(0.02f, 1f, 0.02f);
            _pulse.localPosition = new Vector3(0f, 0.006f, 0f);
            MeshFilter ringA = Child("RingA", _holoMaterial), ringB = Child("RingB", _holoMaterial);
            ringA.sharedMesh = ringB.sharedMesh = _ringMesh;
            _ringA = ringA.transform;
            _ringB = ringB.transform;
            _ringA.localPosition = new Vector3(0f, 0.07f, 0f);
            _ringA.localScale = new Vector3(0.16f / MapHalf, 1f, 0.16f / MapHalf);
            _ringB.localPosition = new Vector3(0f, 0.11f, 0f);
            _ringB.localScale = new Vector3(0.11f / MapHalf, 1f, 0.11f / MapHalf);
            // The page: dark glass behind bright text, framed, turned to whoever looks
            var pageRoot = new GameObject("TFA_ScannerPage") { layer = gameObject.layer };
            pageRoot.transform.SetParent(_holo.transform, false);
            pageRoot.transform.localPosition = new Vector3(0f, MapHeight + 0.12f, 0f);
            _pageRoot = pageRoot.transform;
            var panel = new GameObject("Panel") { layer = gameObject.layer };
            panel.transform.SetParent(_pageRoot, false);
            panel.AddComponent<MeshFilter>().sharedMesh = _quadMesh;
            var panelRenderer = panel.AddComponent<MeshRenderer>();
            panelRenderer.sharedMaterial = _panelMaterial;
            panelRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _panel = panel.transform;
            var text = new GameObject("Text") { layer = gameObject.layer };
            text.transform.SetParent(_pageRoot, false);
            _page = text.AddComponent<TextMeshPro>();
            _page.font = ComputerStyle.Font;
            _page.richText = true;
            _page.color = new Color(0.85f, 0.97f, 1f);
            _page.alignment = TextAlignmentOptions.Bottom;
            _page.textWrappingMode = TextWrappingModes.Normal;
            _page.enableAutoSizing = true;
            _page.fontSizeMax = 1f;
            _page.fontSizeMin = 0.6f;
            _page.fontSize = 1f;
            _page.outlineWidth = 0.18f;
            _page.outlineColor = new Color32(0, 10, 20, 255);
            _page.rectTransform.sizeDelta = new Vector2(ScannerTable.Size * 1.25f, 0.95f);
            _page.rectTransform.pivot = new Vector2(0.5f, 0f);
            _panel.gameObject.SetActive(false);
        }

        /// <summary>Development: where to stand to read the first table's page (photos).</summary>
        public static bool DevView(out Vector3 eye, out Vector3 aim)
        {
            eye = aim = Vector3.zero;
            ScannerVisuals table = FindObjectsByType<ScannerVisuals>(FindObjectsSortMode.None).FirstOrDefault(v => v._pageRoot != null);
            if (table == null)
                return false;
            Transform t = table.transform;
            aim = t.position + t.up * (Top + MapHeight * 0.8f);
            eye = aim - t.forward * 1.9f + t.up * 0.15f;
            return true;
        }

        /// <summary>Development: every page turned to a photo camera (the player's camera turns them otherwise).</summary>
        public static void FaceAll(Vector3 eye)
        {
            foreach (ScannerVisuals table in FindObjectsByType<ScannerVisuals>(FindObjectsSortMode.None))
            {
                if (table._pageRoot == null)
                    continue;
                Vector3 toEye = Vector3.ProjectOnPlane(eye - table._pageRoot.position, table.transform.up);
                if (toEye.sqrMagnitude > 0.01f)
                    table._pageRoot.rotation = Quaternion.LookRotation(-toEye.normalized, table.transform.up);
                AdvancedMod.Log.Msg($"dev: scanner page: panel {table._panel.gameObject.activeSelf} size {table._panel.localScale}, text {table._page.textBounds.size}, font {table._page.fontSize:0.00}, panel material {table._panel.GetComponent<MeshRenderer>().sharedMaterial?.name}");
            }
        }

        // Shared by every table: built once
        private static void BuildMeshes()
        {
            if (_bladeMesh != null)
                return;
            // The scan blade: a vertical sheet from the center to the rim, bright at its leading edge and
            // trailing off behind over 40°
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            const int steps = 12;
            for (int i = 0; i <= steps; i++)
            {
                float a = -i / (float)steps * 40f * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                float fade = 1f - i / (float)steps;
                Color c = new Color(0.3f, 0.95f, 1f) * (0.45f * fade * fade);
                vertices.Add(Vector3.zero);
                vertices.Add(dir * MapHalf);
                colors.Add(c);
                colors.Add(c * 0.6f);
                if (i > 0)
                {
                    int b = i * 2;
                    triangles.AddRange(new[] { b - 2, b - 1, b + 1, b - 2, b + 1, b });
                }
            }
            // The leading edge as a thin wall of light
            int w = vertices.Count;
            vertices.Add(new Vector3(0f, 0f, 0f)); vertices.Add(new Vector3(0f, 0f, MapHalf)); vertices.Add(new Vector3(0f, MapHeight * 0.5f, MapHalf)); vertices.Add(new Vector3(0f, MapHeight * 0.5f, 0f));
            colors.Add(new Color(0.4f, 1f, 1f) * 0.5f); colors.Add(new Color(0.4f, 1f, 1f) * 0.35f); colors.Add(Color.clear); colors.Add(Color.clear);
            triangles.AddRange(new[] { w, w + 1, w + 2, w, w + 2, w + 3 });
            _bladeMesh = Mesh("TFA_ScannerBlade", vertices, colors, triangles);
            // A ring of radius MapHalf (scaled for the pulse and the emitter's rings)
            vertices.Clear(); colors.Clear(); triangles.Clear();
            const int sides = 48;
            for (int i = 0; i <= sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                vertices.Add(dir * MapHalf * 0.97f);
                vertices.Add(dir * MapHalf);
                colors.Add(new Color(0.3f, 0.95f, 1f) * 0.15f);
                colors.Add(new Color(0.5f, 1f, 1f) * 0.8f);
                if (i > 0)
                {
                    int b = i * 2;
                    triangles.AddRange(new[] { b - 2, b - 1, b + 1, b - 2, b + 1, b });
                }
            }
            _ringMesh = Mesh("TFA_ScannerRing", vertices, colors, triangles);
            // The emitter's beam: a faint cone of light from the lens up to the page
            vertices.Clear(); colors.Clear(); triangles.Clear();
            for (int i = 0; i <= 16; i++)
            {
                float a = i * Mathf.PI * 2f / 16;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                vertices.Add(dir * 0.04f + Vector3.up * 0.02f);
                vertices.Add(dir * 0.2f + Vector3.up * (MapHeight + 0.1f));
                colors.Add(new Color(0.3f, 0.9f, 1f) * 0.22f);
                colors.Add(Color.clear);
                if (i > 0)
                {
                    int b = i * 2;
                    triangles.AddRange(new[] { b - 2, b - 1, b + 1, b - 2, b + 1, b });
                }
            }
            _beamMesh = Mesh("TFA_ScannerBeam", vertices, colors, triangles);
            // A unit quad, facing -z (the page's front), for the dark glass
            vertices.Clear(); colors.Clear(); triangles.Clear();
            vertices.Add(new Vector3(-0.5f, -0.5f, 0f)); vertices.Add(new Vector3(0.5f, -0.5f, 0f)); vertices.Add(new Vector3(0.5f, 0.5f, 0f)); vertices.Add(new Vector3(-0.5f, 0.5f, 0f));
            for (int i = 0; i < 4; i++)
                colors.Add(Color.white);
            triangles.AddRange(new[] { 0, 2, 1, 0, 3, 2 });
            _quadMesh = Mesh("TFA_ScannerPanel", vertices, colors, triangles);
        }

        private static Mesh Mesh(string name, List<Vector3> vertices, List<Color> colors, List<int> triangles)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private MeshFilter Child(string name, Material material)
        {
            var go = new GameObject(name) { layer = gameObject.layer };
            go.transform.SetParent(_holo.transform, false);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go.AddComponent<MeshFilter>();
        }

        private void Scan(Transform ship)
        {
            Vector3 center = transform.position;
            Vector3 up = transform.up, right = transform.right, forward = transform.forward;
            float scale = MapHalf / ScannerTable.Range;
            // Relief: the ground under a grid around the table. The host reads the planet's terrain
            // function; a client, which has no access to it, casts rays at the loaded ground
            var planets = ServiceLocator.GetService<IPlanetsClientProvider>();
            var server = ServiceLocator.GetService<IPlanetsServerProvider>();
            var transpose = ServiceLocator.GetService<WorldTracking.Interface.Services.IWorldTransposeClient>();
            IPlanetProperties planet = ClientOrigin.LocalVisualSpace != null ? planets?.GetNearestPlanet(ClientOrigin.LocalVisualSpace) : null;
            bool terrain = server != null && planet != null && transpose != null && planet.SurfaceSpace != null;
            float baseline = float.NaN;
            int ground = 0;
            for (int z = 0; z <= Grid; z++)
            {
                for (int x = 0; x <= Grid; x++)
                {
                    float u = (x / (float)Grid * 2f - 1f) * ScannerTable.Range, v = (z / (float)Grid * 2f - 1f) * ScannerTable.Range;
                    Vector3 point = center + right * u + forward * v;
                    float height = float.NaN;
                    if (terrain)
                    {
                        Vector3 local = transpose.Position(ClientOrigin.LocalVisualSpace, planet.SurfaceSpace, point);
                        Vector3 direction = local.normalized;
                        float elevation = server.GetPlanetTerrainElevationAtPosition(planet.Id, direction);
                        if (local.magnitude - elevation < 20000f)
                        {
                            Vector3 onGround = transpose.Position(planet.SurfaceSpace, ClientOrigin.LocalVisualSpace, direction * elevation);
                            height = Vector3.Dot(onGround - center, up);
                        }
                    }
                    else
                    {
                        foreach (RaycastHit hit in Physics.RaycastAll(point + up * 800f, -up, 1600f).OrderBy(h => h.distance))
                        {
                            if (hit.collider.isTrigger || hit.collider.transform.root == ship || hit.collider.transform.root.name.StartsWith("ship "))
                                continue;
                            height = Vector3.Dot(hit.point - center, up);
                            break;
                        }
                    }
                    if (!float.IsNaN(height))
                    {
                        ground++;
                        if (x == Grid / 2 && z == Grid / 2)
                            baseline = height;
                    }
                    _heights[z * (Grid + 1) + x] = new Vector3(u * scale, height, v * scale);
                }
            }
            // Heights from the ground under the table, three times exaggerated
            if (float.IsNaN(baseline))
                baseline = 0f;
            for (int i = 0; i < _heights.Length; i++)
            {
                Vector3 h = _heights[i];
                if (!float.IsNaN(h.y))
                    _heights[i] = new Vector3(h.x, Mathf.Clamp(0.04f + (h.y - baseline) * scale * 3f, 0.005f, MapHeight), h.z);
            }
            _baseline = baseline;
            _relief.sharedMesh = ReliefMesh(ground > 0);
            // Markers: deposits and rocks, chests, ships and buildings in range
            var marks = new MeshBuilder();
            var colors = new List<Color>();
            int ores = 0, chests = 0, ships = 0, buildings = 0;
            void Mark(Vector3 world, Color color, float height)
            {
                Vector3 local = world - center;
                float u = Vector3.Dot(local, right), v = Vector3.Dot(local, forward);
                if (Mathf.Abs(u) > ScannerTable.Range || Mathf.Abs(v) > ScannerTable.Range)
                    return;
                float h = Mathf.Clamp(0.04f + (Vector3.Dot(local, up) - _baseline) * scale * 3f, 0.005f, MapHeight);
                int before = marks.VertexCount;
                marks.Box(new Vector3(u * scale, h + height * 0.5f, v * scale), new Vector3(0.012f, height, 0.012f));
                for (int i = before; i < marks.VertexCount; i++)
                    colors.Add(color);
            }
            foreach (MinablePlanetObject minable in FindObjectsByType<MinablePlanetObject>(FindObjectsSortMode.None))
            {
                if ((minable.transform.position - center).sqrMagnitude > ScannerTable.Range * ScannerTable.Range * 2f)
                    continue;
                Mark(minable.transform.position, new Color(1f, 0.6f, 0.2f), 0.05f);
                ores++;
            }
            foreach (Transform t in SceneRoots().Where(t => t.name.StartsWith("ship ") && t.name.Contains("visual") && t != ship))
            {
                // Only ships really around the table: drawn, and near in all three directions (a ship far
                // above or below would otherwise land on the map)
                if (!t.gameObject.activeInHierarchy || (t.position - center).sqrMagnitude > ScannerTable.Range * ScannerTable.Range * 2f
                    || Mathf.Abs(Vector3.Dot(t.position - center, up)) > ScannerTable.Range)
                    continue;
                // A chest: a ship made of a loot case only (the cases lying around), its model shown
                var renderers = t.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0 || !renderers.Any(r => r.enabled))
                    continue;
                bool chest = t.GetComponentsInChildren<Transform>().Any(c => c.gameObject.activeInHierarchy && (c.name.StartsWith("OpenedCase") || c.name.StartsWith("Case")));
                Mark(t.position, chest ? new Color(1f, 0.9f, 0.2f) : Color.white, chest ? 0.07f : 0.12f);
                if (chest)
                    chests++;
                else
                    ships++;
            }
            foreach (Transform t in Buildings())
            {
                if ((t.position - center).sqrMagnitude > ScannerTable.Range * ScannerTable.Range * 2f)
                    continue;
                Mark(t.position, new Color(0.75f, 0.45f, 1f), 0.09f);
                buildings++;
            }
            // The ship itself, at the center
            int self = marks.VertexCount;
            marks.Box(new Vector3(0f, 0.06f, 0f), new Vector3(0.03f, 0.03f, 0.05f));
            for (int i = self; i < marks.VertexCount; i++)
                colors.Add(new Color(0.3f, 1f, 0.5f));
            Mesh markMesh = marks.Build("TFA_ScannerMarkers");
            markMesh.SetColors(colors);
            _markers.sharedMesh = markMesh;
            SetPage(Page(ground > 0, ores, chests, ships, buildings));
        }

        private static readonly List<GameObject> RootBuffer = new List<GameObject>();
        private static readonly List<Transform> BuildingCache = new List<Transform>();
        private static float _nextBuildingScan;

        // Ruins, city and village pieces: a walk over every Transform of the scene, so done at
        // most every 15 s for all tables (buildings do not move)
        private static List<Transform> Buildings()
        {
            BuildingCache.RemoveAll(t => t == null);
            if (Time.unscaledTime < _nextBuildingScan)
                return BuildingCache;
            _nextBuildingScan = Time.unscaledTime + 15f;
            BuildingCache.Clear();
            foreach (Transform t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                string n = t.name;
                if (n.StartsWith("TFA_Ruin") || n.StartsWith("TFA_City") || n.StartsWith("TFA_Village"))
                    BuildingCache.Add(t);
            }
            return BuildingCache;
        }

        // The loaded scenes' root objects (ships' visuals are roots): far fewer than every Transform
        private static IEnumerable<Transform> SceneRoots()
        {
            var roots = new List<Transform>();
            for (int s = 0; s < UnityEngine.SceneManagement.SceneManager.sceneCount; s++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(s);
                if (!scene.isLoaded)
                    continue;
                RootBuffer.Clear();
                scene.GetRootGameObjects(RootBuffer);
                foreach (GameObject root in RootBuffer)
                {
                    if (root != null)
                        roots.Add(root.transform);
                }
            }
            return roots;
        }

        private Mesh ReliefMesh(bool hasGround)
        {
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            if (hasGround)
            {
                // A faint filled surface under the lines, so the relief reads as ground
                for (int z = 0; z < Grid; z++)
                {
                    for (int x = 0; x < Grid; x++)
                    {
                        Vector3 a = _heights[z * (Grid + 1) + x], b = _heights[z * (Grid + 1) + x + 1];
                        Vector3 c = _heights[(z + 1) * (Grid + 1) + x + 1], d = _heights[(z + 1) * (Grid + 1) + x];
                        if (float.IsNaN(a.y) || float.IsNaN(b.y) || float.IsNaN(c.y) || float.IsNaN(d.y))
                            continue;
                        int start = vertices.Count;
                        foreach (Vector3 p in new[] { a, b, c, d })
                        {
                            vertices.Add(p);
                            colors.Add(HeightColor(p.y) * 0.22f);
                        }
                        triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2, start, start + 1, start + 2, start, start + 2, start + 3 });
                    }
                }
                // Lines of the relief as thin ribbons along x and z, colored by height
                for (int z = 0; z <= Grid; z += 2)
                    for (int x = 0; x < Grid; x++)
                        Segment(_heights[z * (Grid + 1) + x], _heights[z * (Grid + 1) + x + 1], vertices, colors, triangles);
                for (int x = 0; x <= Grid; x += 2)
                    for (int z = 0; z < Grid; z++)
                        Segment(_heights[z * (Grid + 1) + x], _heights[(z + 1) * (Grid + 1) + x], vertices, colors, triangles);
            }
            // The map's frame
            Vector3[] corners = { new Vector3(-MapHalf, 0f, -MapHalf), new Vector3(MapHalf, 0f, -MapHalf), new Vector3(MapHalf, 0f, MapHalf), new Vector3(-MapHalf, 0f, MapHalf) };
            for (int i = 0; i < 4; i++)
                Segment(corners[i], corners[(i + 1) % 4], vertices, colors, triangles);
            var mesh = new Mesh { name = "TFA_ScannerRelief", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void Segment(Vector3 a, Vector3 b, List<Vector3> vertices, List<Color> colors, List<int> triangles)
        {
            if (float.IsNaN(a.y) || float.IsNaN(b.y))
                return;
            const float w = 0.0025f;
            int start = vertices.Count;
            Vector3 side = Vector3.Cross((b - a).normalized, Vector3.up) * w;
            vertices.Add(a - side); vertices.Add(a + side); vertices.Add(b + side); vertices.Add(b - side);
            vertices.Add(a + Vector3.up * w); vertices.Add(a - Vector3.up * w); vertices.Add(b - Vector3.up * w); vertices.Add(b + Vector3.up * w);
            foreach (Vector3 p in new[] { a, a, b, b, a, a, b, b })
                colors.Add(HeightColor(p.y));
            foreach (int o in new[] { 0, 4 })
            {
                triangles.Add(start + o); triangles.Add(start + o + 1); triangles.Add(start + o + 2);
                triangles.Add(start + o); triangles.Add(start + o + 2); triangles.Add(start + o + 3);
            }
        }

        // Low ground deep blue, middle cyan, peaks white
        private static Color HeightColor(float y)
        {
            float t = Mathf.InverseLerp(0f, MapHeight, y);
            return t < 0.5f ? Color.Lerp(new Color(0.1f, 0.3f, 0.9f, 0.9f), new Color(0.2f, 0.9f, 1f, 0.9f), t * 2f)
                : Color.Lerp(new Color(0.2f, 0.9f, 1f, 0.9f), new Color(0.95f, 1f, 1f, 1f), (t - 0.5f) * 2f);
        }

        // New text only when it changed; the dark glass is fitted to it
        private void SetPage(string text)
        {
            if (text == _pageText)
                return;
            _pageText = text;
            _page.text = text;
            _page.ForceMeshUpdate();
            Bounds bounds = _page.textBounds;
            if (bounds.size.x <= 0f)
            {
                _panel.gameObject.SetActive(false);
                return;
            }
            _panel.gameObject.SetActive(true);
            _panel.localPosition = new Vector3(bounds.center.x, bounds.center.y, 0.01f);
            _panel.localScale = new Vector3(bounds.size.x + 0.08f, bounds.size.y + 0.06f, 1f);
        }

        private string Page(bool ground, int ores, int chests, int ships, int buildings)
        {
            var text = new StringBuilder();
            var planets = ServiceLocator.GetService<IPlanetsClientProvider>();
            IPlanetProperties planet = ClientOrigin.LocalVisualSpace != null ? planets?.GetNearestPlanet(ClientOrigin.LocalVisualSpace) : null;
            WikiEntry entry = planet != null ? PlanetWiki.Get(planet.Id) : null;
            if (entry != null)
            {
                text.Append("<size=125%><b><color=#ffffff>").Append(entry.Name.ToUpperInvariant()).Append("</color></b></size>  <color=#7fd8ff>").Append(PlanetWiki.SystemName(entry.System)).Append("</color>\n");
                text.Append("<size=80%>");
                text.Append("<color=#ffffff>").Append(entry.Biome).Append("</color>  ·  ").Append(entry.Gravity).Append("  ·  air ").Append(entry.Air).Append('\n');
                text.Append("<color=#7fd8ff>").Append(Loc.Pick("Ressources", "Resources", "Recursos")).Append("</color>  ").Append(string.Join(", ", entry.Resources)).Append('\n');
                text.Append("<color=#7fd8ff>").Append(Loc.Pick("Lieux", "Places", "Lugares")).Append("</color>  ").Append(string.Join(", ", entry.Places)).Append('\n');
                text.Append(entry.Use);
                if (!string.IsNullOrEmpty(entry.Danger))
                    text.Append("\n<color=#ff9a6a>").Append(entry.Danger).Append("</color>");
                text.Append("</size>\n");
            }
            text.Append("<size=80%><color=#ffb060>■ ").Append(ores).Append(Loc.Pick(" gisements", " deposits", " yacimientos")).Append("</color>   <color=#ffe040>■ ").Append(chests)
                .Append(Loc.Pick(" coffres", " chests", " cofres")).Append("</color>   <color=#c080ff>■ ").Append(buildings).Append(Loc.Pick(" bâtiments", " buildings", " edificios"))
                .Append("</color>   <color=#ffffff>■ ").Append(ships).Append(Loc.Pick(" vaisseaux", " ships", " naves")).Append("</color>");
            if (!ground)
                text.Append(Loc.Pick("\nAucun sol à portée : scan de l'espace", "\nNo ground in range: scanning space", "\nSin terreno al alcance: escaneando el espacio"));
            text.Append("</size>");
            return text.ToString();
        }
    }
}

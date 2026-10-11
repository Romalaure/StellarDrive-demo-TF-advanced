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

        public static void Register()
        {
            PartFactory.Register(Id, "SignalDisplay", AdvancedTab.Name, AdvancedTab.ComputersRow, Configure);
        }

        private static void Configure(PartSettings settings, GameObject prefab)
        {
            PartFactory.Describe(settings, "Table de cartographie",
                $"Une table holographique qui cartographie le terrain sur {Range:0} m autour du vaisseau (relief en 3D), signale les gisements, rochers, coffres, bâtiments et vaisseaux, et affiche la fiche de la planète : biome, gravité, air, ressources, lieux et utilité. S'allume quand un joueur s'en approche.",
                60f, new[] { (112u, 10), (102u, 10), (101u, 6), (Items.ModItems.Meteorite, 2) });
            settings.internalStateClass = null;
            const float width = 1.6f, height = 0.95f, depth = 1.1f;
            Transform visuals = PartFactory.BlankFromSignalDisplay(prefab, new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, depth));
            var model = new MachineModels.Model();
            // A pedestal, a tilted bevel and the table top with a dark glass and glowing grid lines
            model.Armor.Box(new Vector3(0f, 0.04f, 0f), new Vector3(width * 0.7f, 0.08f, depth * 0.7f));
            model.Armor.Box(new Vector3(0f, 0.42f, 0f), new Vector3(width * 0.45f, 0.7f, depth * 0.45f));
            model.Armor.Block(new[]
            {
                new Vector3(-width * 0.25f, 0.75f, -depth * 0.25f), new Vector3(width * 0.25f, 0.75f, -depth * 0.25f), new Vector3(-width * 0.5f, height - 0.06f, -depth * 0.5f), new Vector3(width * 0.5f, height - 0.06f, -depth * 0.5f),
                new Vector3(-width * 0.25f, 0.75f, depth * 0.25f), new Vector3(width * 0.25f, 0.75f, depth * 0.25f), new Vector3(-width * 0.5f, height - 0.06f, depth * 0.5f), new Vector3(width * 0.5f, height - 0.06f, depth * 0.5f)
            });
            model.Armor.Box(new Vector3(0f, height - 0.03f, 0f), new Vector3(width, 0.06f, depth));
            model.Dark.Box(new Vector3(0f, height + 0.002f, 0f), new Vector3(width - 0.12f, 0.006f, depth - 0.12f));
            for (int i = 1; i < 8; i++)
                model.Glow.Box(new Vector3(-width * 0.5f + 0.06f + (width - 0.12f) * i / 8f, height + 0.006f, 0f), new Vector3(0.006f, 0.002f, depth - 0.12f));
            for (int i = 1; i < 6; i++)
                model.Glow.Box(new Vector3(0f, height + 0.006f, -depth * 0.5f + 0.06f + (depth - 0.12f) * i / 6f), new Vector3(width - 0.12f, 0.002f, 0.006f));
            model.Light.Box(new Vector3(0f, height - 0.03f, depth * 0.5f + 0.004f), new Vector3(width * 0.9f, 0.012f, 0.012f));
            model.Light.Box(new Vector3(0f, height - 0.03f, -depth * 0.5f - 0.004f), new Vector3(width * 0.9f, 0.012f, 0.012f));
            // Projector studs at the corners
            foreach (float x in new[] { -1f, 1f })
                foreach (float z in new[] { -1f, 1f })
                    model.Core.Cylinder(new Vector3(x * (width * 0.5f - 0.05f), height, z * (depth * 0.5f - 0.05f)), new Vector3(x * (width * 0.5f - 0.05f), height + 0.04f, z * (depth * 0.5f - 0.05f)), 0.025f, 0.015f, 6);
            MachineModels.Attach(visuals, model, "Scanner", MachineModels.Cyan, MachineModels.Cyan);
            visuals.gameObject.AddComponent<ScannerVisuals>();
        }
    }

    /// <summary>The table's hologram: relief, markers and the planet's page, rebuilt every few seconds while someone is near.</summary>
    internal sealed class ScannerVisuals : DefaultShipPartVisuals
    {
        private const int Grid = 32;
        private const float MapHalf = 0.68f, MapHeight = 0.45f, Top = 0.97f;
        private static Material _holoMaterial;

        private GameObject _holo;
        private MeshFilter _relief;
        private MeshFilter _markers;
        private TextMeshPro _page;
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
                if (_holo != null)
                    _holo.SetActive(false);
                return;
            }
            if (_holo == null)
                Build();
            _holo.SetActive(true);
            if (Time.unscaledTime >= _next)
            {
                _next = Time.unscaledTime + 3f;
                try
                {
                    Scan(root);
                }
                catch (System.Exception e)
                {
                    ErrorLog.Report("cartography table", e);
                }
            }
            // The page turns to the viewer, about the table's up axis
            Vector3 toCamera = Vector3.ProjectOnPlane(camera.transform.position - _page.transform.position, transform.up);
            if (toCamera.sqrMagnitude > 0.01f)
                _page.transform.rotation = Quaternion.LookRotation(-toCamera.normalized, transform.up);
        }

        private void Build()
        {
            if (_holoMaterial == null)
                _holoMaterial = TransparentMaterials.Additive("TFA_ScannerHolo", null);
            _holo = new GameObject("TFA_ScannerHologram") { layer = gameObject.layer };
            _holo.transform.SetParent(transform, false);
            _holo.transform.localPosition = new Vector3(0f, Top, 0f);
            _relief = Child("Relief");
            _markers = Child("Markers");
            var page = new GameObject("TFA_ScannerPage") { layer = gameObject.layer };
            page.transform.SetParent(_holo.transform, false);
            page.transform.localPosition = new Vector3(0f, MapHeight + 0.15f, 0f);
            _page = page.AddComponent<TextMeshPro>();
            _page.font = ComputerStyle.Font;
            _page.fontSize = 1.1f;
            _page.color = new Color(0.6f, 0.95f, 1f);
            _page.alignment = TextAlignmentOptions.Bottom;
            _page.textWrappingMode = TextWrappingModes.Normal;
            _page.rectTransform.sizeDelta = new Vector2(2.6f, 1.4f);
            _page.rectTransform.pivot = new Vector2(0.5f, 0f);
        }

        private MeshFilter Child(string name)
        {
            var go = new GameObject(name) { layer = gameObject.layer };
            go.transform.SetParent(_holo.transform, false);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _holoMaterial;
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
            foreach (Transform t in FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.parent == null && t.name.StartsWith("ship ") && t.name.Contains("visual") && t != ship))
            {
                bool chest = t.GetComponentsInChildren<Transform>(true).Any(c => c.name.StartsWith("OpenedCase") || c.name.StartsWith("Case"));
                Mark(t.position, chest ? new Color(1f, 0.9f, 0.2f) : Color.white, chest ? 0.07f : 0.12f);
                if (chest)
                    chests++;
                else
                    ships++;
            }
            foreach (Transform t in FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name.StartsWith("TFA_Ruin") || t.name.StartsWith("TFA_City") || t.name.StartsWith("TFA_Village")))
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
            _page.text = Page(ground > 0, ores, chests, ships, buildings);
        }

        private Mesh ReliefMesh(bool hasGround)
        {
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            if (hasGround)
            {
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

        private string Page(bool ground, int ores, int chests, int ships, int buildings)
        {
            var text = new StringBuilder();
            var planets = ServiceLocator.GetService<IPlanetsClientProvider>();
            IPlanetProperties planet = ClientOrigin.LocalVisualSpace != null ? planets?.GetNearestPlanet(ClientOrigin.LocalVisualSpace) : null;
            WikiEntry entry = planet != null ? PlanetWiki.Get(planet.Id) : null;
            if (entry != null)
            {
                text.Append("<b>").Append(entry.Name.ToUpperInvariant()).Append("</b>  <size=70%>").Append(entry.System).Append("</size>\n");
                text.Append("<size=75%>").Append(entry.Biome).Append(" · ").Append(entry.Gravity).Append(" · air ").Append(entry.Air).Append('\n');
                text.Append("Ressources : ").Append(string.Join(", ", entry.Resources)).Append('\n');
                text.Append("Lieux : ").Append(string.Join(", ", entry.Places)).Append('\n');
                text.Append(entry.Use);
                if (!string.IsNullOrEmpty(entry.Danger))
                    text.Append("\n<color=#ff9a6a>").Append(entry.Danger).Append("</color>");
                text.Append("</size>\n");
            }
            text.Append("<size=70%><color=#ffb060>■</color> ").Append(ores).Append(" gisements  <color=#ffe040>■</color> ").Append(chests)
                .Append(" coffres  <color=#c080ff>■</color> ").Append(buildings).Append(" bâtiments  <color=#ffffff>■</color> ").Append(ships).Append(" vaisseaux");
            if (!ground)
                text.Append("\nAucun sol à portée : scan de l'espace");
            text.Append("</size>");
            return text.ToString();
        }
    }
}

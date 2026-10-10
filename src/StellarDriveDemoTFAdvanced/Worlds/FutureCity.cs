using System;
using System.Collections.Generic;
using Planets.Objects;
using Planets.Objects.Common.Model;
using StellarDriveDemoTFAdvanced.Common;
using StellarDriveDemoTFAdvanced.Parts;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StellarDriveDemoTFAdvanced.Worlds
{
    /// <summary>
    /// The abandoned city of a vanished advanced civilization: skyscrapers with setbacks, domes,
    /// antenna pylons, landing pads and gateway arches. Each piece is a copy of the demo's large
    /// rock (so the game places, draws and collides it like a rock) with the city's shape, in the
    /// game's ship hull look, and light strips that still glow. They cannot be mined.
    /// </summary>
    internal static class FutureCity
    {
        private static readonly Color GlowColor = new Color(0.25f, 0.9f, 1f);
        private static GameObject[] _built;
        private static GameObject _holder;

        /// <summary>The city's pieces: big tower, small tower, dome, pylon, landing pad, arch; null when the demo rock is not available.</summary>
        public static GameObject[] Prefabs(PlanetShaper.Context planet)
        {
            if (_built != null && _built.Length > 0 && _built[0] != null)
                return _built;
            GameObject rock = planet.HotspotPrefab("RockLargeIron");
            if (rock == null)
            {
                AdvancedMod.Log.Warning("planets: the large rock prefab was not found, no city");
                return null;
            }
            _built = new[]
            {
                Piece(rock, "TowerTall", (m, g) => Tower(m, g, 52f)),
                Piece(rock, "Tower", (m, g) => Tower(m, g, 30f)),
                Piece(rock, "Dome", Dome),
                Piece(rock, "Pylon", Pylon),
                Piece(rock, "Pad", Pad),
                Piece(rock, "Arch", Arch)
            };
            return _built;
        }

        private static GameObject Piece(GameObject rock, string name, Action<MeshBuilder, MeshBuilder> build)
        {
            if (_holder == null)
            {
                _holder = new GameObject("TFA_CityPrefabs");
                _holder.SetActive(false);
                Object.DontDestroyOnLoad(_holder);
            }
            GameObject copy = Object.Instantiate(rock, _holder.transform);
            copy.name = "TFA_City" + name;

            var hull = new MeshBuilder { Panel = GameSkin.TallPanel };
            var glow = new MeshBuilder();
            build(hull, glow);
            Mesh mesh = hull.Build(copy.name, GameSkin.Ready ? GameSkin.Palette : (Vector2?)null);

            // Look: the game's hull panels, and light strips
            Transform visual = copy.transform.Find("Visuals");
            MeshRenderer renderer = visual.GetComponent<MeshRenderer>();
            renderer.GetComponent<MeshFilter>().sharedMesh = mesh;
            renderer.sharedMaterial = GameSkin.Material != null ? GameSkin.Material : LitMaterials.Get("TFA_CityHull", new Color(0.62f, 0.65f, 0.7f), 0.7f, 0.5f);
            var lights = new GameObject("Glow") { layer = visual.gameObject.layer };
            lights.transform.SetParent(visual, false);
            lights.AddComponent<MeshFilter>().sharedMesh = glow.Build(copy.name + "_Glow");
            var lightRenderer = lights.AddComponent<MeshRenderer>();
            lightRenderer.sharedMaterial = LitMaterials.Get("TFA_CityGlow", GlowColor, 0f, 0.9f, GlowColor * 3f);
            lightRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            PlanetObjectVisuals visuals = copy.GetComponentInChildren<PlanetObjectVisuals>(true);
            if (visuals != null)
                visuals.id = copy.name;

            // Collision follows the hull; no mining
            Transform physics = copy.transform.Find("Physics");
            foreach (MeshCollider collider in physics.GetComponents<MeshCollider>())
            {
                collider.convex = false;
                collider.sharedMesh = mesh;
            }
            foreach (MinablePlanetObject minable in physics.GetComponents<MinablePlanetObject>())
                Object.DestroyImmediate(minable);
            return copy;
        }

        // ---- Shapes (meters, y up from the ground, a little sunk so they sit on slopes)

        // A lobby you can walk into (glass doors gone, reception desk, pillars), then three setbacks,
        // light strips up the corners and around each setback, an antenna
        private static void Tower(MeshBuilder m, MeshBuilder g, float height)
        {
            const float lobby = 4.6f, wall = 0.5f, half = 8f;
            m.Box(new Vector3(0f, -0.4f, 0f), new Vector3(17f, 0.9f, 17f));
            // Walls: an entrance 5 m wide on the front, windows bands on the sides
            m.Box(new Vector3(-5.25f, lobby * 0.5f, half), new Vector3(5.5f, lobby, wall));
            m.Box(new Vector3(5.25f, lobby * 0.5f, half), new Vector3(5.5f, lobby, wall));
            m.Box(new Vector3(0f, lobby - 0.5f, half), new Vector3(5f, 1f, wall));
            m.Box(new Vector3(0f, lobby * 0.5f, -half), new Vector3(2f * half, lobby, wall));
            foreach (float x in new[] { -half, half })
            {
                m.Box(new Vector3(x, 0.6f, 0f), new Vector3(wall, 1.2f, 2f * half));
                m.Box(new Vector3(x, lobby - 0.6f, 0f), new Vector3(wall, 1.2f, 2f * half));
                foreach (float z in new[] { -6f, 0f, 6f })
                    m.Box(new Vector3(x, lobby * 0.5f, z), new Vector3(wall, lobby, 1.2f));
                g.Box(new Vector3(x + Mathf.Sign(x) * 0.27f, 1.25f, 0f), new Vector3(0.05f, 0.12f, 2f * half - 1f));
            }
            m.Box(new Vector3(0f, lobby + 0.25f, 0f), new Vector3(2f * half, 0.5f, 2f * half));
            // Inside: pillars carrying the shaft, a reception desk, benches
            foreach (Vector2 p in new[] { new Vector2(-4f, -4f), new Vector2(4f, -4f), new Vector2(-4f, 3f), new Vector2(4f, 3f) })
                m.Box(new Vector3(p.x, lobby * 0.5f, p.y), new Vector3(0.9f, lobby, 0.9f));
            m.Box(new Vector3(0f, 0.55f, -3.5f), new Vector3(4.5f, 1.1f, 1f));
            g.Box(new Vector3(0f, 1.12f, -2.98f), new Vector3(4.3f, 0.06f, 0.05f));
            m.Box(new Vector3(-5.5f, 0.25f, 1f), new Vector3(0.8f, 0.5f, 3f));
            m.Box(new Vector3(5.5f, 0.25f, 1f), new Vector3(0.8f, 0.5f, 3f));
            // Entrance frame
            g.Box(new Vector3(-2.55f, lobby * 0.4f, half + 0.27f), new Vector3(0.12f, lobby * 0.8f, 0.05f));
            g.Box(new Vector3(2.55f, lobby * 0.4f, half + 0.27f), new Vector3(0.12f, lobby * 0.8f, 0.05f));
            g.Box(new Vector3(0f, lobby - 1.05f, half + 0.27f), new Vector3(5.2f, 0.1f, 0.05f));
            float[] widths = { 11f, 8.5f, 5.5f };
            float[] shares = { 0.5f, 0.3f, 0.2f };
            float y = lobby + 0.5f;
            for (int i = 0; i < widths.Length; i++)
            {
                float w = widths[i], h = height * shares[i];
                m.Box(new Vector3(0f, y + h * 0.5f, 0f), new Vector3(w, h, w));
                float c = w * 0.5f + 0.05f;
                foreach (Vector2 corner in new[] { new Vector2(c, c), new Vector2(-c, c), new Vector2(c, -c), new Vector2(-c, -c) })
                    g.Box(new Vector3(corner.x, y + h * 0.5f, corner.y), new Vector3(0.3f, h * 0.9f, 0.3f));
                // A band of windows near the top of each section
                g.Box(new Vector3(0f, y + h - 1.2f, 0f), new Vector3(w + 0.12f, 0.35f, w + 0.12f));
                y += h;
            }
            m.Cylinder(new Vector3(0f, y, 0f), new Vector3(0f, y + height * 0.18f, 0f), 0.5f, 0.15f, 6);
            g.Cylinder(new Vector3(0f, y + height * 0.18f - 0.6f, 0f), new Vector3(0f, y + height * 0.18f + 0.4f, 0f), 0.45f, 0.45f, 6);
        }

        // A dome of panels you can walk into through an airlock arch: a ring wall, four bands of
        // panels up to a skylight, a lit ring at its foot, a fountain basin and terminals inside
        private static void Dome(MeshBuilder m, MeshBuilder g)
        {
            const int segments = 20;
            const float thickness = 0.35f;
            m.Cylinder(new Vector3(0f, -0.6f, 0f), new Vector3(0f, 0.1f, 0f), 12.8f, 12.8f, segments);
            float[] heights = { 0.1f, 3.2f, 6.4f, 9.2f, 11.2f };
            float[] radii = { 12f, 12f, 10.4f, 7.6f, 3.8f };
            for (int band = 0; band < heights.Length - 1; band++)
            {
                for (int i = 0; i < segments; i++)
                {
                    // The door: the front segment of the lowest band is left open
                    if (band == 0 && i == 0)
                        continue;
                    float a0 = (i - 0.5f) * Mathf.PI * 2f / segments, a1 = (i + 0.5f) * Mathf.PI * 2f / segments;
                    Vector3 bottom0 = new Vector3(Mathf.Sin(a0) * radii[band], heights[band], Mathf.Cos(a0) * radii[band]);
                    Vector3 bottom1 = new Vector3(Mathf.Sin(a1) * radii[band], heights[band], Mathf.Cos(a1) * radii[band]);
                    Vector3 top0 = new Vector3(Mathf.Sin(a0) * radii[band + 1], heights[band + 1], Mathf.Cos(a0) * radii[band + 1]);
                    Vector3 top1 = new Vector3(Mathf.Sin(a1) * radii[band + 1], heights[band + 1], Mathf.Cos(a1) * radii[band + 1]);
                    Panel(m, bottom0, bottom1, top0, top1, thickness);
                }
            }
            // Skylight ring and its glow
            m.Cylinder(new Vector3(0f, 11.2f, 0f), new Vector3(0f, 11.6f, 0f), 4f, 3.4f, segments, capA: false, capB: false);
            g.Cylinder(new Vector3(0f, 11.55f, 0f), new Vector3(0f, 11.65f, 0f), 3.3f, 3.3f, segments, capA: false, capB: true);
            g.Cylinder(new Vector3(0f, 0.12f, 0f), new Vector3(0f, 0.35f, 0f), 12.25f, 12.25f, segments, capA: false, capB: false);
            // Airlock arch in front of the opening
            m.Box(new Vector3(-2.2f, 1.7f, 12.4f), new Vector3(0.6f, 3.4f, 2f));
            m.Box(new Vector3(2.2f, 1.7f, 12.4f), new Vector3(0.6f, 3.4f, 2f));
            m.Box(new Vector3(0f, 3.6f, 12.4f), new Vector3(5f, 0.6f, 2f));
            g.Box(new Vector3(0f, 3.25f, 13.42f), new Vector3(3.6f, 0.1f, 0.05f));
            // Inside: a dry fountain basin, terminals and planters
            m.Cylinder(new Vector3(0f, 0.1f, 0f), new Vector3(0f, 0.7f, 0f), 3f, 3f, 16, capA: false, capB: false);
            m.Cylinder(new Vector3(0f, 0.1f, 0f), new Vector3(0f, 0.25f, 0f), 2.8f, 2.8f, 16);
            m.Cylinder(new Vector3(0f, 0.2f, 0f), new Vector3(0f, 2.4f, 0f), 0.35f, 0.2f, 8);
            g.Cylinder(new Vector3(0f, 2.4f, 0f), new Vector3(0f, 2.7f, 0f), 0.5f, 0.1f, 8);
            foreach (float a in new[] { 60f, 150f, 210f, 300f })
            {
                Quaternion turn = Quaternion.Euler(0f, a, 0f);
                Vector3 at = turn * new Vector3(0f, 0f, 8.5f);
                m.Box(at + new Vector3(0f, 0.7f, 0f), new Vector3(1.2f, 1.4f, 0.5f), turn);
                g.Box(at + turn * new Vector3(0f, 1.15f, -0.26f), new Vector3(0.9f, 0.45f, 0.03f), turn);
            }
        }

        // A thick quad panel between four corners, pushed inward by its thickness
        private static void Panel(MeshBuilder m, Vector3 b0, Vector3 b1, Vector3 t0, Vector3 t1, float thickness)
        {
            Vector3 normal = Vector3.Cross(t0 - b0, b1 - b0).normalized;
            if (Vector3.Dot(normal, (b0 + b1 + t0 + t1) * 0.25f) < 0f)
                normal = -normal;
            Vector3 inward = -normal * thickness;
            // Corner order of MeshBuilder.Block: bit 0 along the band, bit 1 up, bit 2 inward
            m.Block(new[] { b0, b1, t0, t1, b0 + inward, b1 + inward, t0 + inward, t1 + inward });
        }

        // A tall tapered mast with platforms and a beacon
        private static void Pylon(MeshBuilder m, MeshBuilder g)
        {
            m.Box(new Vector3(0f, 0.2f, 0f), new Vector3(5f, 1.4f, 5f));
            m.Cylinder(new Vector3(0f, 0.9f, 0f), new Vector3(0f, 34f, 0f), 1.3f, 0.45f, 8);
            foreach (float y in new[] { 12f, 22f, 30f })
            {
                float r = Mathf.Lerp(2.6f, 1.4f, y / 34f);
                m.Cylinder(new Vector3(0f, y, 0f), new Vector3(0f, y + 0.5f, 0f), r, r, 8);
                g.Cylinder(new Vector3(0f, y + 0.5f, 0f), new Vector3(0f, y + 0.65f, 0f), r * 0.9f, r * 0.9f, 8);
            }
            g.Cylinder(new Vector3(0f, 34f, 0f), new Vector3(0f, 35.6f, 0f), 0.7f, 0.2f, 8);
        }

        // A hexagonal landing pad with lit edges and a mark, and a control booth
        private static void Pad(MeshBuilder m, MeshBuilder g)
        {
            m.Cylinder(new Vector3(0f, -0.8f, 0f), new Vector3(0f, 0.7f, 0f), 12f, 12f, 6);
            for (int i = 0; i < 6; i++)
            {
                float a0 = i * Mathf.PI / 3f, a1 = (i + 1) * Mathf.PI / 3f;
                Vector3 p0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * 11.2f, p1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * 11.2f;
                Vector3 mid = (p0 + p1) * 0.5f + new Vector3(0f, 0.75f, 0f);
                g.Box(mid, new Vector3((p1 - p0).magnitude * 0.85f, 0.1f, 0.35f), Quaternion.LookRotation(Vector3.Cross(p1 - p0, Vector3.up), Vector3.up));
            }
            // An "H"
            g.Box(new Vector3(-2.2f, 0.75f, 0f), new Vector3(0.6f, 0.1f, 6f));
            g.Box(new Vector3(2.2f, 0.75f, 0f), new Vector3(0.6f, 0.1f, 6f));
            g.Box(new Vector3(0f, 0.75f, 0f), new Vector3(4.4f, 0.1f, 0.6f));
            m.Box(new Vector3(13.5f, 1.6f, 0f), new Vector3(3f, 3.4f, 3.4f));
            g.Box(new Vector3(12.0f, 2.4f, 0f), new Vector3(0.1f, 0.8f, 2.6f));
        }

        // A gateway arch over a road, lit underneath
        private static void Arch(MeshBuilder m, MeshBuilder g)
        {
            foreach (float x in new[] { -9f, 9f })
            {
                m.Box(new Vector3(x, 0.2f, 0f), new Vector3(3.4f, 1.4f, 3.4f));
                m.Box(new Vector3(x, 7.5f, 0f), new Vector3(2.2f, 14f, 2.2f));
                g.Box(new Vector3(x, 7.5f, 1.15f), new Vector3(0.3f, 12f, 0.1f));
            }
            m.Box(new Vector3(0f, 15.2f, 0f), new Vector3(22f, 1.8f, 2.8f));
            g.Box(new Vector3(0f, 14.25f, 0f), new Vector3(17f, 0.12f, 0.5f));
            // The road, half broken
            m.Box(new Vector3(0f, -0.1f, 4f), new Vector3(8f, 0.5f, 14f));
            m.Box(new Vector3(1.5f, 0.1f, -6.5f), new Vector3(6f, 0.5f, 4f), Quaternion.Euler(4f, 12f, 3f));
        }
    }
}

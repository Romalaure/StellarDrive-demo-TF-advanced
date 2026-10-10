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
                collider.sharedMesh = mesh;
            foreach (MinablePlanetObject minable in physics.GetComponents<MinablePlanetObject>())
                Object.DestroyImmediate(minable);
            return copy;
        }

        // ---- Shapes (meters, y up from the ground, a little sunk so they sit on slopes)

        // A podium, then three setbacks, light strips up the corners and around each setback, an antenna
        private static void Tower(MeshBuilder m, MeshBuilder g, float height)
        {
            m.Box(new Vector3(0f, 1f, 0f), new Vector3(16f, 4f, 16f));
            float[] widths = { 11f, 8.5f, 5.5f };
            float[] shares = { 0.5f, 0.3f, 0.2f };
            float y = 3f;
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

        // A low dome on a ring wall, a lit ring at its foot and an entrance
        private static void Dome(MeshBuilder m, MeshBuilder g)
        {
            m.Cylinder(new Vector3(0f, -0.5f, 0f), new Vector3(0f, 2.5f, 0f), 12.5f, 12.5f, 24);
            float[] heights = { 2.5f, 6f, 9f, 11f, 12.2f };
            float[] radii = { 12f, 11f, 8.5f, 5f, 0f };
            for (int i = 0; i < heights.Length - 1; i++)
                m.Cylinder(new Vector3(0f, heights[i], 0f), new Vector3(0f, heights[i + 1], 0f), radii[i], radii[i + 1], 24, capA: false, capB: radii[i + 1] > 0f);
            g.Cylinder(new Vector3(0f, 2.4f, 0f), new Vector3(0f, 2.8f, 0f), 12.15f, 12.15f, 24);
            m.Box(new Vector3(0f, 1.8f, 12.6f), new Vector3(4.5f, 4f, 3f));
            g.Box(new Vector3(0f, 1.6f, 14.12f), new Vector3(2.4f, 3f, 0.1f));
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

using System;
using System.Collections.Generic;
using Planets.Objects;
using Planets.Objects.Common.Model;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StellarDriveDemoTFAdvanced.Worlds
{
    /// <summary>
    /// Ruins of an ancient civilization: a monumental gate, standing and broken pillars, fallen
    /// blocks and an obelisk. Each piece is a copy of the demo's large rock (its stone material,
    /// visual and physics parts, so the game places, draws and collides it like a rock) with the
    /// ruin's shape, tinted per style. Ruins cannot be mined.
    /// </summary>
    internal static class Ruin
    {
        private static readonly Dictionary<RuinStyle, GameObject[]> Built = new Dictionary<RuinStyle, GameObject[]>();
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static GameObject _holder;

        /// <summary>The site's pieces, monument first; null when the demo rock is not available.</summary>
        public static GameObject[] Prefabs(PlanetShaper.Context planet, RuinStyle style)
        {
            if (Built.TryGetValue(style, out GameObject[] cached) && cached != null && cached.Length > 0 && cached[0] != null)
                return cached;
            GameObject rock = planet.HotspotPrefab("RockLargeIron");
            if (rock == null)
            {
                AdvancedMod.Log.Warning("planets: the large rock prefab was not found, no ruins");
                return null;
            }
            var pieces = new List<GameObject>();
            pieces.Add(Piece(rock, style, "Gate", Gate));
            pieces.Add(Piece(rock, style, "Pillar", m => Pillar(m, 7f, 0f)));
            pieces.Add(Piece(rock, style, "BrokenPillar", m => Pillar(m, 2.6f, 12f)));
            pieces.Add(Piece(rock, style, "Block", Block));
            pieces.Add(Piece(rock, style, "Obelisk", Obelisk));
            GameObject[] result = pieces.ToArray();
            Built[style] = result;
            return result;
        }

        private static readonly Dictionary<RuinStyle, GameObject[]> Villages = new Dictionary<RuinStyle, GameObject[]>();

        /// <summary>An abandoned settlement: a columned temple first, then houses and a watchtower.</summary>
        public static GameObject[] Village(PlanetShaper.Context planet, RuinStyle style)
        {
            if (Villages.TryGetValue(style, out GameObject[] cached) && cached != null && cached.Length > 0 && cached[0] != null)
                return cached;
            GameObject rock = planet.HotspotPrefab("RockLargeIron");
            if (rock == null)
                return null;
            GameObject[] result =
            {
                Piece(rock, style, "Temple", Temple),
                Piece(rock, style, "House", m => House(m, 7f, 6f, 3.6f, 0)),
                Piece(rock, style, "HouseSmall", m => House(m, 5f, 4.5f, 3f, 1)),
                Piece(rock, style, "Tower", Tower)
            };
            Villages[style] = result;
            return result;
        }

        private static GameObject Piece(GameObject rock, RuinStyle style, string name, Action<MeshBuilder> build)
        {
            if (_holder == null)
            {
                _holder = new GameObject("TFA_RuinPrefabs");
                _holder.SetActive(false);
                Object.DontDestroyOnLoad(_holder);
            }
            GameObject copy = Object.Instantiate(rock, _holder.transform);
            copy.name = $"TFA_Ruin{name}_{style}";

            var builder = new MeshBuilder();
            build(builder);
            Mesh mesh = builder.Build(copy.name);

            // Look: the rock's stone, tinted for the style
            MeshRenderer renderer = copy.transform.Find("Visuals").GetComponent<MeshRenderer>();
            renderer.GetComponent<MeshFilter>().sharedMesh = mesh;
            renderer.sharedMaterial = Stone(renderer.sharedMaterial, style);
            PlanetObjectVisuals visuals = copy.GetComponentInChildren<PlanetObjectVisuals>(true);
            if (visuals != null)
                visuals.id = copy.name;

            // Collision follows the shape; no mining
            Transform physics = copy.transform.Find("Physics");
            foreach (MeshCollider collider in physics.GetComponents<MeshCollider>())
                collider.sharedMesh = mesh;
            foreach (MinablePlanetObject minable in physics.GetComponents<MinablePlanetObject>())
                Object.DestroyImmediate(minable);
            return copy;
        }

        private static readonly Dictionary<(Material, RuinStyle), Material> Stones = new Dictionary<(Material, RuinStyle), Material>();

        private static Material Stone(Material rock, RuinStyle style)
        {
            if (Stones.TryGetValue((rock, style), out Material cached) && cached != null)
                return cached;
            Color tint;
            switch (style)
            {
                case RuinStyle.Sandstone: tint = new Color(1.15f, 0.88f, 0.62f); break;
                case RuinStyle.Basalt: tint = new Color(0.42f, 0.42f, 0.47f); break;
                case RuinStyle.Mossy: tint = new Color(0.72f, 0.88f, 0.6f); break;
                default: tint = new Color(0.9f, 0.88f, 0.86f); break;
            }
            var material = new Material(rock) { name = rock.name + "_" + style };
            if (material.HasProperty(ColorId))
                material.SetColor(ColorId, material.GetColor(ColorId) * tint);
            Stones[(rock, style)] = material;
            return material;
        }

        // ---- Shapes (meters, y up from the ground; a little sunk so they sit on slopes)

        // Two pillars under a lintel, on a stepped platform
        private static void Gate(MeshBuilder m)
        {
            m.Box(new Vector3(0f, -0.2f, 0f), new Vector3(12f, 1f, 6f));
            m.Box(new Vector3(0f, 0.5f, 0f), new Vector3(10f, 0.6f, 4.6f));
            foreach (float x in new[] { -3.4f, 3.4f })
            {
                m.Box(new Vector3(x, 1.1f, 0f), new Vector3(2.4f, 0.6f, 2.4f));
                m.Cylinder(new Vector3(x, 1.4f, 0f), new Vector3(x, 8.4f, 0f), 0.85f, 0.75f, 8);
                m.Box(new Vector3(x, 8.7f, 0f), new Vector3(2.2f, 0.6f, 2.2f));
            }
            m.Box(new Vector3(0f, 9.6f, 0f), new Vector3(9.4f, 1.3f, 2.4f));
            m.Box(new Vector3(0f, 10.6f, 0f), new Vector3(3f, 0.8f, 1.8f));
        }

        // Octagonal column with base and capital, optionally snapped and leaning
        private static void Pillar(MeshBuilder m, float height, float lean)
        {
            Quaternion tilt = Quaternion.Euler(lean, 0f, lean * 0.4f);
            m.Box(new Vector3(0f, -0.1f, 0f), new Vector3(2.2f, 0.8f, 2.2f));
            Vector3 top = tilt * new Vector3(0f, height, 0f);
            m.Cylinder(new Vector3(0f, 0.3f, 0f), top, 0.8f, lean > 0f ? 0.62f : 0.7f, 8);
            if (lean <= 0f)
                m.Box(top + new Vector3(0f, 0.3f, 0f), new Vector3(2f, 0.6f, 2f));
        }

        // A dressed block lying on its side, half buried
        private static void Block(MeshBuilder m)
        {
            m.Box(new Vector3(0f, 0.35f, 0f), new Vector3(3.2f, 1.3f, 1.6f), Quaternion.Euler(0f, 0f, 9f));
            m.Box(new Vector3(1.9f, 0.1f, 1.2f), new Vector3(1.2f, 0.8f, 1.1f), Quaternion.Euler(4f, 30f, 0f));
        }

        // Tall four-sided needle on a plinth
        private static void Obelisk(MeshBuilder m)
        {
            m.Box(new Vector3(0f, 0.1f, 0f), new Vector3(3f, 1.2f, 3f));
            m.Cylinder(new Vector3(0f, 0.7f, 0f), new Vector3(0f, 11f, 0f), 1.1f, 0.65f, 4);
            m.Cylinder(new Vector3(0f, 11f, 0f), new Vector3(0f, 12.4f, 0f), 0.65f, 0f, 4, capB: false);
        }

        // A temple: stepped platform, two rows of columns, a broken roof slab
        private static void Temple(MeshBuilder m)
        {
            m.Box(new Vector3(0f, -0.3f, 0f), new Vector3(16f, 1.2f, 11f));
            m.Box(new Vector3(0f, 0.6f, 0f), new Vector3(14f, 0.6f, 9f));
            foreach (float x in new[] { -5.6f, -2.8f, 0f, 2.8f, 5.6f })
            {
                foreach (float z in new[] { -3.4f, 3.4f })
                {
                    // One column of the back row has fallen
                    if (x == 5.6f && z == 3.4f)
                        continue;
                    m.Cylinder(new Vector3(x, 0.9f, z), new Vector3(x, 7.4f, z), 0.6f, 0.5f, 8);
                    m.Box(new Vector3(x, 7.7f, z), new Vector3(1.6f, 0.6f, 1.6f));
                }
            }
            m.Box(new Vector3(-1.5f, 8.4f, 0f), new Vector3(11f, 0.9f, 8.6f));
            m.Box(new Vector3(5.9f, 1.3f, 5.2f), new Vector3(6.2f, 1.1f, 1.1f), Quaternion.Euler(0f, 18f, 8f));
        }

        // Four walls with a doorway and windows, the roof long gone
        private static void House(MeshBuilder m, float width, float depth, float height, int variant)
        {
            const float wall = 0.5f;
            float hx = width * 0.5f, hz = depth * 0.5f;
            m.Box(new Vector3(0f, -0.2f, 0f), new Vector3(width + 0.6f, 0.4f, depth + 0.6f));
            // Back wall, with a window gap
            m.Box(new Vector3(-hx * 0.55f, height * 0.5f, -hz), new Vector3(width * 0.45f, height, wall));
            m.Box(new Vector3(hx * 0.55f, height * 0.5f, -hz), new Vector3(width * 0.45f, height, wall));
            m.Box(new Vector3(0f, height * 0.85f, -hz), new Vector3(width * 0.12f, height * 0.3f, wall));
            // Front wall around the doorway
            m.Box(new Vector3(-hx * 0.6f, height * 0.5f, hz), new Vector3(width * 0.4f, height, wall));
            m.Box(new Vector3(hx * 0.6f, height * 0.42f, hz), new Vector3(width * 0.4f, height * 0.84f, wall));
            // Side walls, one partly collapsed
            m.Box(new Vector3(-hx, height * 0.5f, 0f), new Vector3(wall, height, depth));
            m.Box(new Vector3(hx, height * (variant == 0 ? 0.32f : 0.5f), -depth * 0.15f), new Vector3(wall, height * (variant == 0 ? 0.64f : 1f), depth * 0.7f));
            m.Box(new Vector3(hx + 1.2f, 0.3f, depth * 0.3f), new Vector3(1.4f, 0.7f, 1f), Quaternion.Euler(0f, 25f, 12f));
        }

        // A square watchtower with crenellations
        private static void Tower(MeshBuilder m)
        {
            m.Box(new Vector3(0f, 0f, 0f), new Vector3(5f, 1f, 5f));
            m.Box(new Vector3(0f, 5.5f, 0f), new Vector3(4f, 10f, 4f));
            m.Box(new Vector3(0f, 10.8f, 0f), new Vector3(4.8f, 0.6f, 4.8f));
            foreach (Vector3 corner in new[] { new Vector3(1.9f, 0f, 1.9f), new Vector3(-1.9f, 0f, 1.9f), new Vector3(1.9f, 0f, -1.9f), new Vector3(-1.9f, 0f, -1.9f), new Vector3(0f, 0f, 1.9f), new Vector3(0f, 0f, -1.9f), new Vector3(1.9f, 0f, 0f) })
                m.Box(corner + new Vector3(0f, 11.6f, 0f), new Vector3(0.9f, 1f, 0.9f));
            // A doorway at the foot (a darker block suggests the opening)
            m.Box(new Vector3(0f, 1.6f, 2.05f), new Vector3(1.2f, 2.2f, 0.2f));
        }
    }
}

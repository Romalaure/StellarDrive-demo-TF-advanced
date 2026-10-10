using System;
using System.Collections.Generic;
using System.Linq;
using Planets.Objects;
using Items.Model;
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
            pieces.Add(Piece(rock, style, "Gate", (m, w) => Gate(m)));
            pieces.Add(Piece(rock, style, "Pillar", (m, w) => Pillar(m, 7f, 0f)));
            pieces.Add(Piece(rock, style, "BrokenPillar", (m, w) => Pillar(m, 2.6f, 12f)));
            pieces.Add(Piece(rock, style, "Block", (m, w) => Block(m)));
            pieces.Add(Piece(rock, style, "Obelisk", (m, w) => Obelisk(m)));
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
                Piece(rock, style, "House", (m, w) => House(m, w, 7f, 6f, 3.6f, 0)),
                Piece(rock, style, "HouseSmall", (m, w) => House(m, w, 5.4f, 4.6f, 3.1f, 1)),
                Piece(rock, style, "Tower", Tower),
                Piece(rock, style, "Hall", Hall)
            };
            Villages[style] = result;
            return result;
        }

        private static GameObject Piece(GameObject rock, RuinStyle style, string name, Action<MeshBuilder, MeshBuilder> build)
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
            var wood = new MeshBuilder { Panel = WoodArea() };
            build(builder, wood);
            Mesh mesh = builder.Build(copy.name);

            // Look: the rock's stone, tinted for the style
            MeshRenderer renderer = copy.transform.Find("Visuals").GetComponent<MeshRenderer>();
            renderer.GetComponent<MeshFilter>().sharedMesh = mesh;
            renderer.sharedMaterial = Stone(renderer.sharedMaterial, style);
            PlanetObjectVisuals visuals = copy.GetComponentInChildren<PlanetObjectVisuals>(true);
            if (visuals != null)
                visuals.id = copy.name;
            // Beams, furniture and doors in the game's wood
            Mesh woodMesh = null;
            Material woodMaterial = WoodMaterial();
            if (wood.HasGeometry && woodMaterial != null)
            {
                ItemSettings woodItem = Resources.FindObjectsOfTypeAll<ItemSettings>().FirstOrDefault(i => i.id == 110);
                woodMesh = wood.Build(copy.name + "_Wood", Parts.GameSkin.MostUsedUv1(woodItem?.itemObject?.GetComponentInChildren<MeshFilter>(true)?.sharedMesh));
                var woodObject = new GameObject("Wood") { layer = renderer.gameObject.layer };
                woodObject.transform.SetParent(renderer.transform, false);
                woodObject.AddComponent<MeshFilter>().sharedMesh = woodMesh;
                woodObject.AddComponent<MeshRenderer>().sharedMaterial = woodMaterial;
            }

            // Collision follows the shape (hollow: buildings can be walked into); no mining
            Transform physics = copy.transform.Find("Physics");
            foreach (MeshCollider collider in physics.GetComponents<MeshCollider>())
            {
                collider.convex = false;
                collider.sharedMesh = mesh;
            }
            if (woodMesh != null)
                physics.gameObject.AddComponent<MeshCollider>().sharedMesh = woodMesh;
            foreach (MinablePlanetObject minable in physics.GetComponents<MinablePlanetObject>())
                Object.DestroyImmediate(minable);
            return copy;
        }

        private static Material _wood;

        // The game's wood item material: planks texture, lit like the game's objects
        private static Rect WoodArea()
        {
            ItemSettings wood = Resources.FindObjectsOfTypeAll<ItemSettings>().FirstOrDefault(i => i.id == 110);
            return MeshBuilder.UvArea(wood?.itemObject?.GetComponentInChildren<MeshFilter>(true)?.sharedMesh);
        }

        private static Material WoodMaterial()
        {
            if (_wood != null)
                return _wood;
            ItemSettings wood = Resources.FindObjectsOfTypeAll<ItemSettings>().FirstOrDefault(i => i.id == 110);
            _wood = wood?.itemObject?.GetComponentInChildren<MeshRenderer>(true)?.sharedMaterial;
            return _wood;
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
                case RuinStyle.Ice: tint = new Color(0.82f, 0.95f, 1.15f); break;
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

        // A temple: stepped platform, two rows of columns, a broken roof slab, an altar between braziers
        private static void Temple(MeshBuilder m, MeshBuilder w)
        {
            m.Box(new Vector3(0f, -0.3f, 0f), new Vector3(16f, 1.2f, 11f));
            m.Box(new Vector3(0f, 0.6f, 0f), new Vector3(14f, 0.6f, 9f));
            // Front steps
            m.Box(new Vector3(0f, 0.15f, 5.9f), new Vector3(6f, 0.3f, 0.8f));
            m.Box(new Vector3(0f, 0.45f, 5.1f), new Vector3(6f, 0.3f, 0.8f));
            foreach (float x in new[] { -5.6f, -2.8f, 0f, 2.8f, 5.6f })
            {
                foreach (float z in new[] { -3.4f, 3.4f })
                {
                    // One column of the back row has fallen
                    if (x == 5.6f && z == 3.4f)
                        continue;
                    m.Box(new Vector3(x, 1.05f, z), new Vector3(1.4f, 0.3f, 1.4f));
                    m.Cylinder(new Vector3(x, 1.2f, z), new Vector3(x, 7.4f, z), 0.6f, 0.5f, 10);
                    m.Box(new Vector3(x, 7.7f, z), new Vector3(1.6f, 0.6f, 1.6f));
                }
            }
            m.Box(new Vector3(-1.5f, 8.4f, 0f), new Vector3(11f, 0.9f, 8.6f));
            m.Box(new Vector3(-1.5f, 9.1f, 0f), new Vector3(9f, 0.5f, 6f));
            m.Box(new Vector3(5.9f, 1.3f, 5.2f), new Vector3(6.2f, 1.1f, 1.1f), Quaternion.Euler(0f, 18f, 8f));
            // Altar and braziers
            m.Box(new Vector3(0f, 1.4f, -1.5f), new Vector3(2.6f, 1.0f, 1.3f));
            m.Box(new Vector3(0f, 1.95f, -1.5f), new Vector3(3f, 0.15f, 1.6f));
            foreach (float x in new[] { -2.6f, 2.6f })
            {
                m.Cylinder(new Vector3(x, 0.9f, -1.5f), new Vector3(x, 2f, -1.5f), 0.25f, 0.18f, 8);
                m.Cylinder(new Vector3(x, 2f, -1.5f), new Vector3(x, 2.35f, -1.5f), 0.45f, 0.55f, 8);
            }
            // Old offering box
            w.Box(new Vector3(-4f, 1.2f, -2.2f), new Vector3(0.9f, 0.6f, 0.7f), Quaternion.Euler(0f, 20f, 0f));
        }

        // Walls with a doorway, windows and a lintel; inside a table, a bench, shelves and a hearth;
        // two roof beams left across the top, a third fallen in
        private static void House(MeshBuilder m, MeshBuilder w, float width, float depth, float height, int variant)
        {
            const float wall = 0.45f;
            float hx = width * 0.5f, hz = depth * 0.5f;
            m.Box(new Vector3(0f, -0.25f, 0f), new Vector3(width + 0.8f, 0.5f, depth + 0.8f));
            // Inside floor, a step above the ground
            m.Box(new Vector3(0f, 0.05f, 0f), new Vector3(width - wall, 0.1f, depth - wall));
            // Front wall: doorway 1.3 m wide, 2.3 m high
            const float door = 0.65f, doorTop = 2.3f;
            WallX(m, -hx, -door, hz, height, wall);
            WallX(m, door, hx, hz, height, wall);
            m.Box(new Vector3(0f, (doorTop + height) * 0.5f, hz), new Vector3(door * 2f, height - doorTop, wall));
            w.Box(new Vector3(0f, doorTop + 0.1f, hz + wall * 0.5f + 0.05f), new Vector3(door * 2f + 0.5f, 0.22f, 0.12f));
            // Back wall: a window from 1.1 to 2.1 m
            WindowWall(m, w, -hx, hx, -hz, height, wall, 1.1f, 2.1f, 0.6f, true);
            // Side walls with a window; variant 0 has one partly collapsed
            WindowWall(m, w, -hz, hz, -hx, height, wall, 1.1f, 2.0f, 0.5f, false);
            if (variant == 0)
            {
                m.Box(new Vector3(hx, height * 0.3f, -depth * 0.15f), new Vector3(wall, height * 0.6f, depth * 0.7f));
                m.Box(new Vector3(hx, height * 0.75f, -hz + 0.5f), new Vector3(wall, height * 0.5f, 1f));
                m.Box(new Vector3(hx + 1.2f, 0.3f, depth * 0.3f), new Vector3(1.4f, 0.7f, 1f), Quaternion.Euler(0f, 25f, 12f));
            }
            else
            {
                WindowWall(m, w, -hz, hz, hx, height, wall, 1.1f, 2.0f, 0.5f, false);
            }
            // Hearth and chimney in the back corner
            m.Box(new Vector3(-hx + 0.9f, 0.6f, -hz + 0.75f), new Vector3(1.2f, 1.1f, 1.0f));
            m.Box(new Vector3(-hx + 0.9f, height + 0.6f, -hz + 0.5f), new Vector3(0.8f, 1.6f, 0.7f));
            // Roof beams (one fallen) and furniture
            w.Box(new Vector3(0f, height - 0.15f, -hz * 0.35f), new Vector3(width + 0.4f, 0.25f, 0.25f));
            w.Box(new Vector3(0f, height - 0.15f, hz * 0.35f), new Vector3(width + 0.4f, 0.25f, 0.25f));
            w.Box(new Vector3(0.6f, height * 0.5f, 0f), new Vector3(width * 0.9f, 0.22f, 0.22f), Quaternion.Euler(0f, 15f, 32f));
            Table(w, new Vector3(0.6f, 0.1f, -0.2f), 1.6f, 0.9f);
            w.Box(new Vector3(0.6f, 0.52f, -0.95f), new Vector3(1.4f, 0.08f, 0.35f));
            w.Box(new Vector3(0.6f, 0.31f, -0.95f), new Vector3(0.12f, 0.42f, 0.3f));
            // Shelves on the side wall
            w.Box(new Vector3(-hx + wall * 0.5f + 0.2f, 1.6f, hz * 0.35f), new Vector3(0.4f, 0.08f, 1.4f));
            w.Box(new Vector3(-hx + wall * 0.5f + 0.2f, 1.0f, hz * 0.35f), new Vector3(0.4f, 0.08f, 1.4f));
        }

        // A square watchtower, hollow: doorway, an inside floor, a ladder, crenellations and arrow slits
        private static void Tower(MeshBuilder m, MeshBuilder w)
        {
            const float wall = 0.6f, size = 5f, height = 11f;
            const float h = size * 0.5f;
            m.Box(new Vector3(0f, -0.2f, 0f), new Vector3(size + 1f, 0.6f, size + 1f));
            m.Box(new Vector3(0f, 0.15f, 0f), new Vector3(size - wall, 0.1f, size - wall));
            // Front wall with a doorway 1.4 x 2.5 m
            WallX(m, -h, -0.7f, h, height, wall);
            WallX(m, 0.7f, h, h, height, wall);
            m.Box(new Vector3(0f, (2.5f + height) * 0.5f, h), new Vector3(1.4f, height - 2.5f, wall));
            // Other walls, with arrow slits high up
            WindowWall(m, w, -h, h, -h, height, wall, 6.5f, 8f, 0.25f, true);
            WindowWall(m, w, -h, h, -h, height, wall, 6.5f, 8f, 0.25f, false);
            WindowWall(m, w, -h, h, h, height, wall, 6.5f, 8f, 0.25f, false);
            // Lookout floor with a hatch, crenellations
            m.Box(new Vector3(-0.6f, height - 0.2f, 0f), new Vector3(size - 1.2f - wall, 0.4f, size - wall));
            m.Box(new Vector3(0f, height + 0.3f, 0f), new Vector3(size + 0.4f, 0.6f, size + 0.4f));
            foreach (Vector3 corner in new[] { new Vector3(1.9f, 0f, 1.9f), new Vector3(-1.9f, 0f, 1.9f), new Vector3(1.9f, 0f, -1.9f), new Vector3(-1.9f, 0f, -1.9f), new Vector3(0f, 0f, 1.9f), new Vector3(0f, 0f, -1.9f), new Vector3(1.9f, 0f, 0f), new Vector3(-1.9f, 0f, 0f) })
                m.Box(corner + new Vector3(0f, height + 1.1f, 0f), new Vector3(0.9f, 1f, 0.9f));
            // Ladder up to the hatch, lintel over the door
            for (float y = 0.5f; y < height - 0.4f; y += 0.45f)
                w.Box(new Vector3(1.6f, y, 0f), new Vector3(0.12f, 0.06f, 0.8f));
            w.Box(new Vector3(1.6f, height * 0.5f, -0.42f), new Vector3(0.1f, height - 0.5f, 0.08f));
            w.Box(new Vector3(1.6f, height * 0.5f, 0.42f), new Vector3(0.1f, height - 0.5f, 0.08f));
            w.Box(new Vector3(0f, 2.6f, h + wall * 0.5f + 0.05f), new Vector3(1.9f, 0.22f, 0.12f));
        }

        // A long hall: stone walls with doors at both ends and windows, a pitched timber roof half
        // fallen in, a row of posts and a long table with benches
        private static void Hall(MeshBuilder m, MeshBuilder w)
        {
            const float wall = 0.5f, width = 8f, length = 14f, height = 3.4f;
            const float hx = width * 0.5f, hz = length * 0.5f;
            m.Box(new Vector3(0f, -0.25f, 0f), new Vector3(width + 1f, 0.5f, length + 1f));
            m.Box(new Vector3(0f, 0.05f, 0f), new Vector3(width - wall, 0.1f, length - wall));
            // Gables with a doorway
            foreach (float z in new[] { -hz, hz })
            {
                WallX(m, -hx, -0.8f, z, height, wall);
                WallX(m, 0.8f, hx, z, height, wall);
                m.Box(new Vector3(0f, (2.6f + height) * 0.5f, z), new Vector3(1.6f, height - 2.6f, wall));
                for (int i = 0; i < 4; i++)
                {
                    float y = height + 0.35f + i * 0.7f;
                    m.Box(new Vector3(0f, y, z), new Vector3(width * (1f - (i + 0.5f) / 4.5f), 0.7f, wall));
                }
                w.Box(new Vector3(0f, 2.7f, z + Mathf.Sign(z) * (wall * 0.5f + 0.05f)), new Vector3(2.1f, 0.24f, 0.12f));
            }
            // Long walls with three windows each
            foreach (float x in new[] { -hx, hx })
            {
                float z = -hz;
                foreach (float window in new[] { -4f, 0f, 4f })
                {
                    WallZ(m, z, window - 0.6f, x, height, wall);
                    m.Box(new Vector3(x, 0.6f, window), new Vector3(wall, 1.2f, 1.2f));
                    m.Box(new Vector3(x, (2.3f + height) * 0.5f, window), new Vector3(wall, height - 2.3f, 1.2f));
                    w.Box(new Vector3(x, 1.25f, window), new Vector3(wall + 0.15f, 0.1f, 1.4f));
                    z = window + 0.6f;
                }
                WallZ(m, z, hz, x, height, wall);
            }
            // Roof: ridge beam, rafters, planks on one side only (the other fell in)
            const float ridge = height + 2.8f;
            w.Box(new Vector3(0f, ridge, 0f), new Vector3(0.3f, 0.3f, length + 0.6f));
            for (float z = -hz + 0.6f; z <= hz - 0.5f; z += 2.2f)
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 a = new Vector3(side * (hx + 0.3f), height - 0.1f, z), b = new Vector3(0f, ridge, z);
                    w.Box((a + b) * 0.5f, new Vector3(0.18f, 0.18f, (b - a).magnitude), Quaternion.LookRotation(b - a));
                }
            }
            Vector3 low = new Vector3(-hx - 0.3f, height + 0.05f, 0f), high = new Vector3(0f, ridge + 0.15f, 0f);
            w.Box((low + high) * 0.5f, new Vector3((high - low).magnitude, 0.12f, length * 0.75f), Quaternion.FromToRotation(Vector3.right, (high - low).normalized));
            w.Box(new Vector3(2.2f, 1.2f, 3.5f), new Vector3(0.12f, 2.6f, 3.8f), Quaternion.Euler(10f, 5f, 55f));
            // Posts, the long table and its benches
            foreach (float z in new[] { -3.5f, 3.5f })
                w.Box(new Vector3(0f, (height + 0.1f) * 0.5f, z), new Vector3(0.3f, height + 0.1f, 0.3f));
            Table(w, new Vector3(-1.6f, 0.1f, 0f), 1.1f, 7f);
            foreach (float x in new[] { -2.6f, -0.6f })
            {
                w.Box(new Vector3(x, 0.55f, 0f), new Vector3(0.35f, 0.08f, 6.4f));
                w.Box(new Vector3(x, 0.32f, -2.8f), new Vector3(0.3f, 0.44f, 0.12f));
                w.Box(new Vector3(x, 0.32f, 2.8f), new Vector3(0.3f, 0.44f, 0.12f));
            }
        }

        // ---- Building parts

        // A wall along x (front or back), from x0 to x1 at z
        private static void WallX(MeshBuilder m, float x0, float x1, float z, float height, float thickness)
        {
            if (x1 - x0 > 0.01f)
                m.Box(new Vector3((x0 + x1) * 0.5f, height * 0.5f, z), new Vector3(x1 - x0, height, thickness));
        }

        // A wall along z (side), from z0 to z1 at x
        private static void WallZ(MeshBuilder m, float z0, float z1, float x, float height, float thickness)
        {
            if (z1 - z0 > 0.01f)
                m.Box(new Vector3(x, height * 0.5f, (z0 + z1) * 0.5f), new Vector3(thickness, height, z1 - z0));
        }

        // A wall with a centered window between sill and top, along x at z = `at` or along z at x = `at`
        private static void WindowWall(MeshBuilder m, MeshBuilder w, float from, float to, float at, float height, float thickness,
            float sill, float top, float halfWidth, bool alongX)
        {
            float mid = (from + to) * 0.5f;
            if (alongX)
            {
                WallX(m, from, mid - halfWidth, at, height, thickness);
                WallX(m, mid + halfWidth, to, at, height, thickness);
                m.Box(new Vector3(mid, sill * 0.5f, at), new Vector3(halfWidth * 2f, sill, thickness));
                m.Box(new Vector3(mid, (top + height) * 0.5f, at), new Vector3(halfWidth * 2f, height - top, thickness));
                w.Box(new Vector3(mid, sill + 0.04f, at), new Vector3(halfWidth * 2f + 0.2f, 0.08f, thickness + 0.15f));
            }
            else
            {
                WallZ(m, from, mid - halfWidth, at, height, thickness);
                WallZ(m, mid + halfWidth, to, at, height, thickness);
                m.Box(new Vector3(at, sill * 0.5f, mid), new Vector3(thickness, sill, halfWidth * 2f));
                m.Box(new Vector3(at, (top + height) * 0.5f, mid), new Vector3(thickness, height - top, halfWidth * 2f));
                w.Box(new Vector3(at, sill + 0.04f, mid), new Vector3(thickness + 0.15f, 0.08f, halfWidth * 2f + 0.2f));
            }
        }

        private static void Table(MeshBuilder w, Vector3 floor, float width, float length)
        {
            w.Box(floor + new Vector3(0f, 0.78f, 0f), new Vector3(width, 0.08f, length));
            float lx = width * 0.5f - 0.1f, lz = length * 0.5f - 0.1f;
            foreach (Vector2 leg in new[] { new Vector2(lx, lz), new Vector2(-lx, lz), new Vector2(lx, -lz), new Vector2(-lx, -lz) })
                w.Box(floor + new Vector3(leg.x, 0.37f, leg.y), new Vector3(0.1f, 0.74f, 0.1f));
        }
    }
}

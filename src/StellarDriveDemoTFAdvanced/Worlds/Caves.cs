using System;
using System.Collections.Generic;
using Planets.Objects;
using Planets.Objects.Common.Model;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StellarDriveDemoTFAdvanced.Worlds
{
    internal enum CaveStyle
    {
        /// <summary>Grey rock, violet and cyan crystals.</summary>
        Rock,
        /// <summary>Blue-white ice, pale blue crystals: the snowy mountains.</summary>
        Ice
    }

    /// <summary>
    /// Caves: a rocky mound (the planet's terrain is a height field, so the cave is a hill of its
    /// own) hollowed into a chamber, a mouth on one side, stalactites hanging from the vault,
    /// stalagmites on the floor and glowing crystals in the walls. Like the ruins, a copy of the
    /// demo's large rock: the game places, draws and collides it; its collider follows the hollow
    /// shape, so players walk in. Ore deposits are placed inside by the hotspot.
    /// </summary>
    internal static class Caves
    {
        private static readonly Dictionary<(CaveStyle, int), GameObject> Built = new Dictionary<(CaveStyle, int), GameObject>();
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static GameObject _holder;

        // Mound grid: around (theta) and up (phi); the mouth takes columns MouthFrom..MouthTo, rows below MouthRows
        private const int Around = 32, Up = 10, MouthFrom = 15, MouthTo = 16, MouthRows = 5;

        /// <summary>A cave of this style (variant 0 or 1: two shapes), or null without the demo rock.</summary>
        public static GameObject Prefab(PlanetShaper.Context planet, CaveStyle style, int variant)
        {
            if (Built.TryGetValue((style, variant), out GameObject cached) && cached != null)
                return cached;
            GameObject rock = planet.HotspotPrefab("RockLargeIron");
            if (rock == null)
                return null;
            try
            {
                GameObject cave = Build(rock, style, variant);
                Built[(style, variant)] = cave;
                return cave;
            }
            catch (Exception e)
            {
                AdvancedMod.Log.Error($"caves: could not build {style} {variant}: {e}");
                return null;
            }
        }

        private static GameObject Build(GameObject rock, CaveStyle style, int variant)
        {
            if (_holder == null)
            {
                _holder = new GameObject("TFA_CavePrefabs");
                _holder.SetActive(false);
                Object.DontDestroyOnLoad(_holder);
            }
            GameObject copy = Object.Instantiate(rock, _holder.transform);
            copy.name = $"TFA_Cave{variant}_{style}";
            var random = new System.Random(911 + variant * 37 + (int)style * 5);

            // The rock's own texture area (the rest of its texture has blank white patches)
            MeshFilter rockMesh = copy.transform.Find("Visuals").GetComponent<MeshFilter>();
            var stone = new MeshBuilder { Panel = MeshBuilder.UvArea(rockMesh.sharedMesh) };
            var glow = new MeshBuilder();
            float width = variant == 0 ? 12f : 15f, length = variant == 0 ? 16f : 20f, height = variant == 0 ? 11f : 13f;
            Mound(stone, random, width, length, height, 3f);
            Inside(stone, glow, random, width - 3f, length - 3f, height - 3f, style);
            Mesh mesh = stone.Build(copy.name);

            MeshRenderer renderer = copy.transform.Find("Visuals").GetComponent<MeshRenderer>();
            renderer.GetComponent<MeshFilter>().sharedMesh = mesh;
            var material = new Material(renderer.sharedMaterial) { name = renderer.sharedMaterial.name + "_Cave" + style };
            if (material.HasProperty(ColorId))
                material.SetColor(ColorId, material.GetColor(ColorId) * (style == CaveStyle.Ice ? new Color(0.85f, 0.97f, 1.2f) : new Color(0.78f, 0.76f, 0.74f)));
            renderer.sharedMaterial = material;
            PlanetObjectVisuals visuals = copy.GetComponentInChildren<PlanetObjectVisuals>(true);
            if (visuals != null)
                visuals.id = copy.name;

            // The crystals glow in the dark of the chamber
            Color light = style == CaveStyle.Ice ? new Color(0.45f, 0.8f, 1f) : variant == 0 ? new Color(0.65f, 0.3f, 1f) : new Color(0.2f, 0.9f, 0.85f);
            var crystals = new GameObject("Crystals") { layer = renderer.gameObject.layer };
            crystals.transform.SetParent(renderer.transform, false);
            crystals.AddComponent<MeshFilter>().sharedMesh = glow.Build(copy.name + "_Crystals");
            crystals.AddComponent<MeshRenderer>().sharedMaterial = LitMaterials.Get("TFA_CaveCrystal" + style + variant, light * 0.6f, 0.1f, 0.9f, light * 1.6f);

            // Collision follows the hollow shape: the chamber can be walked into; no mining of the cave itself
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

        // A lumpy surface radius factor, the same for the outer and inner shells up to a scale
        private static float Lumps(float theta, float phi, int seed) =>
            1f + 0.07f * Mathf.Sin(theta * 3f + seed) * Mathf.Cos(phi * 2f) + 0.05f * Mathf.Sin(theta * 7f + phi * 5f + seed * 1.7f) + 0.03f * Mathf.Sin(theta * 13f - phi * 9f);

        private static Vector3 Shell(float theta, float phi, float width, float length, float height, int seed)
        {
            float k = Lumps(theta, phi, seed);
            // Sunk 1.2 m: it sits on slopes without showing its base
            return new Vector3(width * Mathf.Cos(phi) * Mathf.Sin(theta) * k, height * Mathf.Sin(phi) * k - 1.2f, length * Mathf.Cos(phi) * Mathf.Cos(theta) * k);
        }

        private static bool Mouth(int i, int j) => i >= MouthFrom && i <= MouthTo && j < MouthRows;

        /// <summary>The mound: an outer shell, an inner vault facing in, a rim of rock around the mouth.</summary>
        private static void Mound(MeshBuilder m, System.Random r, float width, float length, float height, float wall)
        {
            int seed = r.Next(0, 100);
            Vector3 Outer(int i, int j) => Shell(i * Mathf.PI * 2f / Around, j * Mathf.PI * 0.5f / Up, width, length, height, seed);
            Vector3 Inner(int i, int j) => Shell(i * Mathf.PI * 2f / Around, j * Mathf.PI * 0.5f / Up, width - wall, length - wall, height - wall, seed);
            Vector3 middle = new Vector3(0f, 2f, 0f);
            for (int i = 0; i < Around; i++)
            {
                for (int j = 0; j < Up; j++)
                {
                    if (Mouth(i, j))
                        continue;
                    int k = (i + 1) % Around;
                    Vector3 o0 = Outer(i, j), o1 = Outer(k, j), o2 = Outer(k, j + 1), o3 = Outer(i, j + 1);
                    m.Face(o0, o1, o2, o3, (o0 + o2) * 0.5f - middle);
                    Vector3 n0 = Inner(i, j), n1 = Inner(k, j), n2 = Inner(k, j + 1), n3 = Inner(i, j + 1);
                    m.Face(n0, n1, n2, n3, middle - (n0 + n2) * 0.5f);
                }
            }
            // The mouth's rim: its two sides and its lintel
            Vector3 mouthOut = Outer(MouthFrom, 0) + Outer(MouthTo + 1, 0);
            for (int j = 0; j < MouthRows; j++)
            {
                foreach ((int i, float side) in new[] { (MouthFrom, -1f), (MouthTo + 1, 1f) })
                {
                    Vector3 a = Outer(i, j), b = Outer(i, j + 1), c = Inner(i, j + 1), d = Inner(i, j);
                    Vector3 across = Vector3.Cross(b - a, a - d) * side;
                    m.Face(a, b, c, d, Vector3.Dot(across, (a + c) * 0.5f - mouthOut * 0.5f) > 0f ? -across : across);
                }
            }
            for (int i = MouthFrom; i <= MouthTo; i++)
            {
                Vector3 a = Outer(i, MouthRows), b = Outer(i + 1, MouthRows), c = Inner(i + 1, MouthRows), d = Inner(i, MouthRows);
                m.Face(a, b, c, d, Vector3.down);
            }
            // Boulders fallen by the mouth
            for (int n = 0; n < 4; n++)
            {
                float t = (MouthFrom + 1f) * Mathf.PI * 2f / Around + F(r, -0.5f, 0.5f);
                Vector3 at = new Vector3(Mathf.Sin(t), 0f, Mathf.Cos(t)) * (length * F(r, 1.05f, 1.25f)) + Vector3.up * F(r, -0.4f, 0.1f);
                Rock(m, r, at, F(r, 0.8f, 1.8f));
            }
        }

        /// <summary>Stalactites, stalagmites, rocks and crystals in the chamber.</summary>
        private static void Inside(MeshBuilder m, MeshBuilder glow, System.Random r, float width, float length, float height, CaveStyle style)
        {
            // Hanging from the vault, standing on the floor (clear of the way in)
            for (int n = 0; n < 34; n++)
            {
                float t = F(r, 0f, Mathf.PI * 2f), reach = F(r, 0.1f, 0.75f);
                Vector3 ground = new Vector3(Mathf.Sin(t) * width * reach, -1.2f, Mathf.Cos(t) * length * reach);
                float roof = height * Mathf.Sqrt(Mathf.Max(0.05f, 1f - reach * reach)) - 1.4f;
                float hang = F(r, 0.8f, 2.8f);
                m.Cylinder(new Vector3(ground.x, roof + 0.3f, ground.z), new Vector3(ground.x, roof - hang, ground.z), F(r, 0.18f, 0.45f), 0.03f, 6);
            }
            for (int n = 0; n < 16; n++)
            {
                float t = F(r, 0f, Mathf.PI * 2f), reach = F(r, 0.35f, 0.85f);
                // Not in the passage from the mouth (toward -z) to the middle
                if (Mathf.Cos(t) < -0.6f)
                    continue;
                Vector3 at = new Vector3(Mathf.Sin(t) * width * reach, -1.3f, Mathf.Cos(t) * length * reach);
                m.Cylinder(at, at + Vector3.up * F(r, 0.6f, 2.2f), F(r, 0.25f, 0.5f), 0.04f, 6);
            }
            for (int n = 0; n < 8; n++)
            {
                float t = F(r, 0f, Mathf.PI * 2f), reach = F(r, 0.7f, 0.92f);
                if (Mathf.Cos(t) < -0.7f)
                    continue;
                Rock(m, r, new Vector3(Mathf.Sin(t) * width * reach, -0.9f, Mathf.Cos(t) * length * reach), F(r, 0.6f, 1.3f));
            }
            // Crystal clusters on the walls and the floor's edge, pointing into the chamber
            int clusters = style == CaveStyle.Ice ? 14 : 11;
            for (int n = 0; n < clusters; n++)
            {
                float t = F(r, 0f, Mathf.PI * 2f), up = F(r, 0f, 0.6f);
                if (Mathf.Cos(t) < -0.8f && up < 0.5f)
                    continue;
                Vector3 wall = new Vector3(Mathf.Sin(t) * width * Mathf.Cos(up) * 0.97f, height * Mathf.Sin(up) - 1.2f, Mathf.Cos(t) * length * Mathf.Cos(up) * 0.97f);
                Vector3 into = (new Vector3(0f, 1f, 0f) - wall).normalized;
                int count = r.Next(3, 7);
                for (int c = 0; c < count; c++)
                {
                    Vector3 dir = (into + new Vector3(F(r, -0.5f, 0.5f), F(r, -0.2f, 0.6f), F(r, -0.5f, 0.5f))).normalized;
                    float size = F(r, 0.6f, 1.8f), radius = size * 0.14f;
                    Vector3 root = wall + new Vector3(F(r, -0.3f, 0.3f), F(r, -0.2f, 0.2f), F(r, -0.3f, 0.3f));
                    glow.Cylinder(root - dir * 0.2f, root + dir * size * 0.75f, radius, radius, 6);
                    glow.Cylinder(root + dir * size * 0.75f, root + dir * size, radius, 0.01f, 6);
                }
            }
        }

        // A rough boulder: a squashed, lumpy sphere
        private static void Rock(MeshBuilder m, System.Random r, Vector3 at, float size)
        {
            const int sides = 7, rings = 4;
            float squash = F(r, 0.55f, 0.85f);
            int seed = r.Next(0, 100);
            Vector3 Point(int i, int j)
            {
                float t = i * Mathf.PI * 2f / sides, p = -Mathf.PI * 0.5f + j * Mathf.PI / rings;
                return at + new Vector3(Mathf.Cos(p) * Mathf.Sin(t), Mathf.Sin(p) * squash, Mathf.Cos(p) * Mathf.Cos(t)) * (size * Lumps(t, p, seed));
            }
            for (int i = 0; i < sides; i++)
            {
                for (int j = 0; j < rings; j++)
                {
                    Vector3 a = Point(i, j), b = Point(i + 1, j), c = Point(i + 1, j + 1), d = Point(i, j + 1);
                    m.Face(a, b, c, d, (a + c) * 0.5f - at);
                }
            }
        }

        private static float F(System.Random r, float min, float max) => min + (float)r.NextDouble() * (max - min);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Planets.Objects.Common.Model;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StellarDriveDemoTFAdvanced.Worlds
{
    internal enum TreeKind
    {
        Oak,
        AutumnOak,
        Acacia,
        Birch,
        AutumnBirch,
        /// <summary>Jungle giant: a 30 m kapok with buttress roots and a wide umbrella crown.</summary>
        Kapok,
        Palm,
        /// <summary>Jungle canopy tree, dark and dense.</summary>
        Jungle,
        /// <summary>Erebos: a giant mushroom, a pale stem under a wide glowing cap.</summary>
        Mushroom,
        /// <summary>Erebos: a charred, leafless tree of the volcanic plains.</summary>
        Charred
    }

    internal enum PlantKind
    {
        Fern,
        Bush,
        RedFlowers,
        YellowFlowers,
        VioletFlowers,
        WhiteFlowers
    }

    /// <summary>
    /// Broadleaf trees for the mod's planets. Each is a copy of the demo's fir prefab (so the game
    /// places, instances, collides, fells and recycles it like its own trees) with a new model: a
    /// bark trunk and leaf cards cut from the game's own foliage atlas, in the game's foliage
    /// materials (wind, translucency, ship-style outlines) tinted per species. The fir's distant
    /// impostor is removed: these trees fade out at the forest's view range instead.
    /// </summary>
    internal static class Trees
    {
        // Leaf clusters in the fir's FirTreeBranches atlas (u, v from the bottom-left)
        private static readonly Rect BigBranch = new Rect(0.02f, 0.082f, 0.625f, 0.83f);
        private static readonly Rect RoundCluster = new Rect(0.58f, 0.33f, 0.40f, 0.646f);
        private static readonly Rect SmallCluster = new Rect(0.718f, 0.018f, 0.244f, 0.282f);

        private static readonly Dictionary<TreeKind, GameObject> Built = new Dictionary<TreeKind, GameObject>();

        public static IEnumerable<GameObject> AllBuilt => Built.Values.Where(t => t != null);
        private static GameObject _holder;

        public static GameObject Get(GameObject fir, TreeKind kind)
        {
            if (Built.TryGetValue(kind, out GameObject cached) && cached != null)
                return cached;
            if (fir == null)
                return null;
            try
            {
                GameObject tree = Build(fir, kind);
                Built[kind] = tree;
                return tree;
            }
            catch (Exception e)
            {
                AdvancedMod.Log.Error($"trees: could not build {kind}: {e}");
                return null;
            }
        }

        private static GameObject Build(GameObject fir, TreeKind kind)
        {
            if (_holder == null)
            {
                _holder = new GameObject("TFA_TreePrefabs");
                _holder.SetActive(false);
                Object.DontDestroyOnLoad(_holder);
            }
            GameObject copy = Object.Instantiate(fir, _holder.transform);
            copy.name = "TFA_Tree_" + kind;
            PlanetObjectVisuals visuals = copy.GetComponentInChildren<PlanetObjectVisuals>(true);
            visuals.id = copy.name;
            MeshRenderer lod0 = visuals.GetComponentsInChildren<MeshRenderer>(true).First(r => r.name.Contains("LOD0"));
            Material[] firMaterials = lod0.sharedMaterials;

            // Model and materials
            lod0.GetComponent<MeshFilter>().sharedMesh = Model(kind);
            lod0.sharedMaterials = new[] { Bark(firMaterials[0], kind), Leaves(firMaterials[1], kind) };

            // No fir impostor for distant views
            foreach (Component impostor in visuals.GetComponents<Component>().Where(c => c != null && c.GetType().Name == "AmplifyImpostor").ToList())
                Object.DestroyImmediate(impostor);
            foreach (Transform child in visuals.transform.Cast<Transform>().Where(t => t.name.Contains("Impostor")).ToList())
                Object.DestroyImmediate(child.gameObject);
            LODGroup group = visuals.GetComponent<LODGroup>();
            if (group != null)
            {
                group.SetLODs(new[] { new LOD(0.01f, new Renderer[] { lod0 }) });
                group.RecalculateBounds();
            }
            // The giant's trunk is much thicker and taller than the fir's
            if (kind == TreeKind.Kapok || kind == TreeKind.Mushroom)
            {
                foreach (Collider collider in copy.GetComponentsInChildren<Collider>(true))
                {
                    if (collider.GetComponentInParent<PlanetObjectVisuals>() == null)
                        collider.transform.localScale = Vector3.Scale(collider.transform.localScale, new Vector3(2.2f, 1.5f, 2.2f));
                }
            }
            return copy;
        }

        // ---- Undergrowth: ferns, bushes and flowers on the planets' grass layer

        private static readonly Dictionary<PlantKind, GameObject> Plants = new Dictionary<PlantKind, GameObject>();

        public static IEnumerable<GameObject> AllPlants => Plants.Values.Where(p => p != null);

        /// <summary>
        /// A plant built like the grass (a copy of the game's grass prefab, instanced the same way,
        /// no collision) with a new model in the fir's foliage material: fern fronds, a leafy bush
        /// or a clump of flowers.
        /// </summary>
        public static GameObject Plant(GameObject grass, GameObject fir, PlantKind kind)
        {
            if (Plants.TryGetValue(kind, out GameObject cached) && cached != null)
                return cached;
            if (grass == null || fir == null)
                return null;
            if (_holder == null)
            {
                _holder = new GameObject("TFA_TreePrefabs");
                _holder.SetActive(false);
                Object.DontDestroyOnLoad(_holder);
            }
            MeshRenderer firLod = fir.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name.Contains("LOD0"));
            if (firLod == null || firLod.sharedMaterials.Length < 2)
                return null;
            GameObject copy = Object.Instantiate(grass, _holder.transform);
            copy.name = "TFA_Plant_" + kind;
            PlanetObjectVisuals visuals = copy.GetComponentInChildren<PlanetObjectVisuals>(true);
            if (visuals != null)
                visuals.id = copy.name;
            bool flowers = kind != PlantKind.Fern && kind != PlantKind.Bush;
            // Flowers: green leaves (the leaf atlas) first, petals second; others: stems, then leaves
            Material first = flowers ? Tinted(firLod.sharedMaterials[1], "FlowerLeaves", new Color(0.4f, 0.72f, 0.3f)) : Tinted(firLod.sharedMaterials[0], "PlantStem", new Color(0.35f, 0.5f, 0.2f));
            Material[] materials = { first, PlantLeaves(firLod.sharedMaterials[1], kind) };
            int variant = 0;
            foreach (MeshRenderer renderer in copy.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderer.GetComponent<MeshFilter>().sharedMesh = PlantModel(kind, variant++);
                renderer.sharedMaterials = materials;
            }
            Plants[kind] = copy;
            return copy;
        }

        private static Material PlantLeaves(Material fir, PlantKind kind)
        {
            switch (kind)
            {
                case PlantKind.Fern: return Tinted(fir, "FernLeaves", new Color(0.4f, 0.75f, 0.28f));
                case PlantKind.Bush: return Tinted(fir, "BushLeaves", new Color(0.3f, 0.55f, 0.22f));
                // The leaf atlas is green: strong tints give bright petals
                case PlantKind.RedFlowers: return Tinted(fir, "RedFlowers", new Color(3.2f, 0.45f, 0.55f));
                case PlantKind.YellowFlowers: return Tinted(fir, "YellowFlowers", new Color(2.6f, 1.5f, 0.3f));
                case PlantKind.VioletFlowers: return Tinted(fir, "VioletFlowers", new Color(1.9f, 0.6f, 3.2f));
                default: return Tinted(fir, "WhiteFlowers", new Color(2.6f, 1.9f, 2.6f));
            }
        }

        private static Mesh PlantModel(PlantKind kind, int variant)
        {
            var mesh = new TreeMesh(((int)kind + 1) * 131 + variant * 17);
            float scale = 0.85f + 0.12f * variant;
            switch (kind)
            {
                case PlantKind.Fern:
                    mesh.Rosette(Vector3.zero, 7 + variant, 1.3f * scale, 0.75f, BigBranch);
                    break;
                case PlantKind.Bush:
                    mesh.Crown(new Vector3(0f, 0.55f * scale, 0f), new Vector3(0.9f, 0.55f, 0.9f) * scale, 10, 0.9f * scale, RoundCluster, 0.3f);
                    break;
                default:
                    // A clump of green leaves and stems (first material), small flower heads on top (second)
                    mesh.CardsInFirstMaterial = true;
                    mesh.Rosette(Vector3.zero, 5, 0.5f * scale, 0.35f, SmallCluster);
                    mesh.CardsInFirstMaterial = false;
                    for (int i = 0; i < 4 + variant; i++)
                    {
                        float a = i * 2.4f + variant;
                        Vector3 tip = new Vector3(Mathf.Cos(a) * 0.22f, 0.45f + 0.12f * (i % 3), Mathf.Sin(a) * 0.22f) * scale;
                        mesh.Trunk(Vector3.zero, tip, 0.012f, 0.008f);
                        mesh.Blossom(tip, 0.15f * scale, RoundCluster);
                    }
                    break;
            }
            return mesh.Build("TFA_Plant_" + kind + "_" + variant);
        }

        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

        private static Material Bark(Material fir, TreeKind kind)
        {
            bool birch = kind == TreeKind.Birch || kind == TreeKind.AutumnBirch;
            switch (kind)
            {
                case TreeKind.Kapok: return Tinted(fir, "KapokBark", new Color(0.62f, 0.58f, 0.5f));
                case TreeKind.Palm: return Tinted(fir, "PalmBark", new Color(0.66f, 0.55f, 0.38f));
                case TreeKind.Jungle: return Tinted(fir, "JungleBark", new Color(0.4f, 0.34f, 0.26f));
                case TreeKind.Mushroom: return Tinted(fir, "MushroomStem", new Color(0.92f, 0.86f, 0.95f));
                case TreeKind.Charred: return Tinted(fir, "CharredBark", new Color(0.12f, 0.1f, 0.1f));
            }
            Color color = birch ? new Color(0.95f, 0.93f, 0.88f) : kind == TreeKind.Acacia ? new Color(0.5f, 0.38f, 0.3f) : new Color(0.45f, 0.36f, 0.28f);
            return Tinted(fir, (birch ? "Birch" : kind == TreeKind.Acacia ? "Acacia" : "Oak") + "Bark", color);
        }

        private static Material Leaves(Material fir, TreeKind kind)
        {
            switch (kind)
            {
                case TreeKind.Oak: return Tinted(fir, "OakLeaves", new Color(0.36f, 0.55f, 0.22f));
                case TreeKind.AutumnOak: return Tinted(fir, "AutumnOakLeaves", new Color(0.85f, 0.42f, 0.14f));
                case TreeKind.Acacia: return Tinted(fir, "AcaciaLeaves", new Color(0.56f, 0.62f, 0.26f));
                case TreeKind.Birch: return Tinted(fir, "BirchLeaves", new Color(0.55f, 0.78f, 0.3f));
                case TreeKind.Kapok: return Tinted(fir, "KapokLeaves", new Color(0.26f, 0.52f, 0.2f));
                case TreeKind.Palm: return Tinted(fir, "PalmLeaves", new Color(0.42f, 0.66f, 0.22f));
                case TreeKind.Jungle: return Tinted(fir, "JungleLeaves", new Color(0.18f, 0.42f, 0.2f));
                case TreeKind.Mushroom: return Tinted(fir, "MushroomCap", new Color(1.6f, 0.45f, 1.9f));
                case TreeKind.Charred: return Tinted(fir, "CharredEmbers", new Color(1.4f, 0.35f, 0.08f));
                default: return Tinted(fir, "AutumnBirchLeaves", new Color(0.95f, 0.78f, 0.22f));
            }
        }

        private static Material Tinted(Material source, string name, Color color)
        {
            if (Materials.TryGetValue(name, out Material cached) && cached != null)
                return cached;
            var material = new Material(source) { name = "TFA_" + name };
            if (material.HasProperty("_AlbedoColor"))
                material.SetColor("_AlbedoColor", color);
            Materials[name] = material;
            return material;
        }

        // ---- Models (meters; submesh 0 bark, 1 leaves)

        private static Mesh Model(TreeKind kind)
        {
            var mesh = new TreeMesh(kind.GetHashCode() * 7919 + 17);
            switch (kind)
            {
                case TreeKind.Oak:
                case TreeKind.AutumnOak:
                    mesh.Trunk(Vector3.zero, new Vector3(0.2f, 5.2f, 0f), 0.62f, 0.42f);
                    mesh.Trunk(new Vector3(0.2f, 4.6f, 0f), new Vector3(2.6f, 8.2f, 0.8f), 0.3f, 0.16f);
                    mesh.Trunk(new Vector3(0.2f, 4.8f, 0f), new Vector3(-2.2f, 8.6f, -1.2f), 0.3f, 0.16f);
                    mesh.Trunk(new Vector3(0.2f, 5.0f, 0f), new Vector3(0.4f, 9.4f, 2.2f), 0.28f, 0.15f);
                    mesh.Crown(new Vector3(0.1f, 9.4f, 0f), new Vector3(6.4f, 4.6f, 6.4f), 34, 4.6f, RoundCluster, 0.25f);
                    break;
                case TreeKind.Kapok:
                    mesh.Trunk(new Vector3(0f, -0.5f, 0f), new Vector3(0.2f, 24f, 0.1f), 1.5f, 0.75f);
                    // Buttress roots fanning out at the foot
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * 60f * Mathf.Deg2Rad + 0.3f;
                        var out1 = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                        mesh.Trunk(out1 * 3.8f + Vector3.down * 0.3f, out1 * 0.8f + Vector3.up * 4.5f, 0.35f, 0.5f);
                    }
                    // Big limbs carrying a wide, layered umbrella
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i * 72f * Mathf.Deg2Rad;
                        var out1 = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                        mesh.Trunk(new Vector3(0.2f, 21f, 0.1f), out1 * 8f + Vector3.up * 27.5f, 0.55f, 0.25f);
                    }
                    mesh.Crown(new Vector3(0f, 29f, 0f), new Vector3(13f, 3.2f, 13f), 64, 6f, RoundCluster, 0.7f);
                    mesh.Crown(new Vector3(0f, 31.5f, 0f), new Vector3(8f, 2.2f, 8f), 28, 5f, RoundCluster, 0.6f);
                    break;
                case TreeKind.Palm:
                {
                    // A ringed trunk curving up, a crown of feathery fronds arching out and down
                    Vector3 p = Vector3.zero;
                    for (int i = 0; i < 5; i++)
                    {
                        Vector3 next = p + new Vector3(0.45f + i * 0.12f, 2.8f, 0.1f);
                        mesh.Trunk(p, next, 0.42f - i * 0.04f, 0.38f - i * 0.04f);
                        // The scars of old fronds: a swollen ring at each joint
                        Vector3 axis = (next - p).normalized;
                        mesh.Trunk(next - axis * 0.12f, next + axis * 0.12f, 0.43f - i * 0.04f, 0.43f - i * 0.04f);
                        p = next;
                    }
                    for (int i = 0; i < 13; i++)
                        mesh.PalmFrond(p, i * 360f / 13f + 11f * (i % 3), 6f + 0.6f * (i % 2), 0.55f + 0.25f * (i % 3), SmallCluster);
                    break;
                }
                case TreeKind.Jungle:
                    mesh.Trunk(Vector3.zero, new Vector3(0.3f, 15f, 0.1f), 0.75f, 0.38f);
                    mesh.Trunk(new Vector3(0.3f, 11f, 0.1f), new Vector3(4f, 16f, 1.6f), 0.32f, 0.16f);
                    mesh.Trunk(new Vector3(0.3f, 12f, 0.1f), new Vector3(-3.4f, 16.5f, -1.8f), 0.32f, 0.16f);
                    mesh.Crown(new Vector3(0.2f, 17f, 0f), new Vector3(7.5f, 4.2f, 7.5f), 44, 4.6f, RoundCluster, 0.35f);
                    break;
                case TreeKind.Mushroom:
                    // A thick stem leaning a little, a skirt ring, then a wide flat cap
                    mesh.Trunk(new Vector3(0f, -0.4f, 0f), new Vector3(0.6f, 6f, 0.2f), 1.1f, 0.8f);
                    mesh.Trunk(new Vector3(0.6f, 6f, 0.2f), new Vector3(1.1f, 11.5f, 0.3f), 0.8f, 0.7f);
                    mesh.Trunk(new Vector3(0.75f, 7.8f, 0.22f), new Vector3(0.8f, 8.1f, 0.24f), 1.5f, 1.2f);
                    mesh.Crown(new Vector3(1.1f, 12.2f, 0.3f), new Vector3(9f, 1.6f, 9f), 46, 4.2f, RoundCluster, 0.9f);
                    mesh.Crown(new Vector3(1.1f, 13.2f, 0.3f), new Vector3(5.5f, 1.2f, 5.5f), 20, 3.4f, RoundCluster, 0.8f);
                    break;
                case TreeKind.Charred:
                    // A burnt trunk with a few bare, twisted limbs and glowing embers at their tips
                    mesh.Trunk(Vector3.zero, new Vector3(0.4f, 7f, 0.1f), 0.45f, 0.18f);
                    mesh.Trunk(new Vector3(0.3f, 4.5f, 0.1f), new Vector3(2.6f, 7.4f, 0.9f), 0.16f, 0.05f);
                    mesh.Trunk(new Vector3(0.35f, 5.4f, 0.1f), new Vector3(-2f, 8.2f, -1.1f), 0.14f, 0.05f);
                    mesh.Trunk(new Vector3(0.3f, 3.2f, 0.1f), new Vector3(-1.4f, 4.8f, 1.8f), 0.12f, 0.04f);
                    mesh.Crown(new Vector3(0.4f, 7.4f, 0.1f), new Vector3(1.2f, 1f, 1.2f), 5, 0.8f, SmallCluster, 0.2f);
                    break;
                case TreeKind.Acacia:
                    mesh.Trunk(Vector3.zero, new Vector3(0.8f, 5.6f, 0.2f), 0.36f, 0.24f);
                    mesh.Trunk(new Vector3(0.8f, 5.4f, 0.2f), new Vector3(3.6f, 8.6f, 1.0f), 0.2f, 0.12f);
                    mesh.Trunk(new Vector3(0.8f, 5.4f, 0.2f), new Vector3(-2.2f, 8.4f, -1.4f), 0.2f, 0.12f);
                    // Wide, flat umbrella
                    mesh.Crown(new Vector3(0.6f, 9.0f, 0f), new Vector3(8.5f, 1.1f, 8.5f), 30, 4.2f, BigBranch, 0.85f);
                    break;
                default:
                    mesh.Trunk(Vector3.zero, new Vector3(0.15f, 13.5f, 0.1f), 0.24f, 0.09f);
                    mesh.Crown(new Vector3(0.1f, 10.2f, 0f), new Vector3(2.9f, 5.4f, 2.9f), 26, 3.2f, SmallCluster, 0.1f);
                    break;
            }
            return mesh.Build("TFA_Tree_" + kind);
        }

        private sealed class TreeMesh
        {
            private readonly List<Vector3> _vertices = new List<Vector3>();
            private readonly List<Vector3> _normals = new List<Vector3>();
            private readonly List<Vector2> _uvs = new List<Vector2>();
            private readonly List<int> _bark = new List<int>();
            private readonly List<int> _leaves = new List<int>();
            private readonly System.Random _random;
            /// <summary>Leaf cards go in the first submesh (flowers: green leaves there, petals in the second).</summary>
            public bool CardsInFirstMaterial;

            public TreeMesh(int seed)
            {
                _random = new System.Random(seed);
            }

            public void Trunk(Vector3 a, Vector3 b, float ra, float rb)
            {
                const int sides = 8;
                Vector3 axis = (b - a).normalized;
                Vector3 u = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                Vector3 v = Vector3.Cross(axis, u);
                float length = (b - a).magnitude;
                int start = _vertices.Count;
                for (int i = 0; i <= sides; i++)
                {
                    float angle = i * Mathf.PI * 2f / sides;
                    Vector3 radial = u * Mathf.Cos(angle) + v * Mathf.Sin(angle);
                    _vertices.Add(a + radial * ra);
                    _vertices.Add(b + radial * rb);
                    _normals.Add(radial);
                    _normals.Add(radial);
                    // Bark repeats around and up the trunk like the fir's
                    _uvs.Add(new Vector2((float)i / sides, 0f));
                    _uvs.Add(new Vector2((float)i / sides, length / 4f));
                }
                for (int i = 0; i < sides; i++)
                {
                    int k = start + i * 2;
                    _bark.AddRange(new[] { k, k + 1, k + 3, k, k + 3, k + 2 });
                }
            }

            /// <summary>
            /// Leaf clusters spread over an ellipsoid crown: pairs of crossed cards, base toward the
            /// crown's center. <paramref name="flatness"/> lays them toward horizontal (acacia).
            /// </summary>
            public void Crown(Vector3 center, Vector3 radii, int clusters, float size, Rect atlas, float flatness)
            {
                float golden = Mathf.PI * (3f - Mathf.Sqrt(5f));
                for (int i = 0; i < clusters; i++)
                {
                    // Fibonacci sphere, upper part denser
                    float y = 1f - (i + 0.5f) / clusters * 1.6f;
                    float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                    float theta = golden * i;
                    var direction = new Vector3(Mathf.Cos(theta) * r, y, Mathf.Sin(theta) * r);
                    Vector3 position = center + Vector3.Scale(direction, radii) * (0.55f + 0.45f * (float)_random.NextDouble());
                    Vector3 outward = (position - center).normalized;
                    Vector3 up = Vector3.Lerp(outward + Vector3.up * 0.6f, Vector3.Cross(outward, Vector3.up).sqrMagnitude > 0.01f ? outward : Vector3.forward, flatness).normalized;
                    float scale = size * (0.8f + 0.4f * (float)_random.NextDouble());
                    Card(position - up * scale * 0.35f, up, outward, scale, atlas, center);
                    Card(position - up * scale * 0.35f, up, Vector3.Cross(up, outward).normalized, scale, atlas, center);
                }
            }

            /// <summary>Leaf cards rising from one point in a ring, leaning out (ferns, flower leaves).</summary>
            public void Rosette(Vector3 center, int count, float length, float lean, Rect atlas)
            {
                for (int i = 0; i < count; i++)
                {
                    float a = (i * 360f / count + 23f * (i % 2)) * Mathf.Deg2Rad;
                    var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    Vector3 along = (outward * lean + Vector3.up).normalized;
                    Vector3 facing = Vector3.Cross(along, Vector3.Cross(Vector3.up, outward)).normalized;
                    Card(center, along, facing, length * (0.8f + 0.2f * (i % 3) / 2f), atlas, center + Vector3.down * 0.5f);
                }
            }

            /// <summary>A small flower head: three crossed cards.</summary>
            public void Blossom(Vector3 center, float size, Rect atlas)
            {
                for (int i = 0; i < 3; i++)
                {
                    float a = i * 60f * Mathf.Deg2Rad;
                    var facing = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    Card(center - Vector3.up * size * 0.5f, Vector3.up, facing, size, atlas, center + Vector3.down * size);
                }
            }

            /// <summary>Palm fronds: long leaf cards leaving the top in a ring, arching out and down.</summary>
            public void Fronds(Vector3 top, int count, float length, Rect atlas)
            {
                for (int i = 0; i < count; i++)
                {
                    float a = (i * 360f / count + 13f * (i % 3)) * Mathf.Deg2Rad;
                    var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    float droop = 0.15f + 0.35f * (i % 3) / 2f;
                    Vector3 along = (outward - Vector3.up * droop).normalized;
                    Vector3 facing = Vector3.Cross(along, Vector3.Cross(Vector3.up, outward)).normalized;
                    Card(top, along, facing, length * (0.85f + 0.15f * (i % 2)), atlas, top + Vector3.down * 2f);
                }
            }

            /// <summary>
            /// A palm frond: a midrib leaving the top at a heading (degrees), rising a little then
            /// arching down, lined on both sides with narrow leaflets that shorten toward its tip.
            /// </summary>
            public void PalmFrond(Vector3 top, float heading, float length, float droop, Rect atlas)
            {
                float a = heading * Mathf.Deg2Rad;
                var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 side = Vector3.Cross(Vector3.up, outward).normalized;
                Vector3 At(float t) => top + outward * length * t + Vector3.up * length * (0.3f * t - droop * t * t);
                const int segments = 10;
                for (int i = 1; i <= segments; i++)
                {
                    float t = i / (float)segments;
                    Vector3 point = At(t);
                    Vector3 along = (At(Mathf.Min(1f, t + 0.05f)) - At(Mathf.Max(0f, t - 0.05f))).normalized;
                    float leaflet = 1.25f * (1f - 0.65f * t);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        // Leaflets sweep forward and hang a little below the midrib
                        Vector3 up = (side * s * 0.8f + along * 0.45f + Vector3.down * 0.25f).normalized;
                        Vector3 facing = Vector3.Cross(up, along).normalized;
                        if (facing.y < 0f)
                            facing = -facing;
                        Card(point, up, facing, leaflet, atlas, top + Vector3.down * 2f);
                    }
                }
            }

            // A double-sided leaf card standing along `up`, facing `facing`
            private void Card(Vector3 basePoint, Vector3 up, Vector3 facing, float size, Rect atlas, Vector3 crownCenter)
            {
                Vector3 side = Vector3.Cross(up, facing).normalized;
                if (side.sqrMagnitude < 0.01f)
                    side = Vector3.Cross(up, Vector3.right).normalized;
                float halfWidth = size * 0.5f * atlas.width / atlas.height;
                Vector3[] corners =
                {
                    basePoint - side * halfWidth,
                    basePoint + side * halfWidth,
                    basePoint + side * halfWidth + up * size,
                    basePoint - side * halfWidth + up * size
                };
                Vector2[] uv =
                {
                    new Vector2(atlas.xMin, atlas.yMin), new Vector2(atlas.xMax, atlas.yMin),
                    new Vector2(atlas.xMax, atlas.yMax), new Vector2(atlas.xMin, atlas.yMax)
                };
                int start = _vertices.Count;
                for (int i = 0; i < 4; i++)
                {
                    _vertices.Add(corners[i]);
                    // Soft, rounded lighting: leaves face away from the crown's middle
                    _normals.Add((corners[i] - crownCenter).normalized);
                    _uvs.Add(uv[i]);
                }
                (CardsInFirstMaterial ? _bark : _leaves).AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }

            public Mesh Build(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(_vertices);
                mesh.SetNormals(_normals);
                mesh.SetUVs(0, _uvs);
                mesh.subMeshCount = 2;
                mesh.SetTriangles(_bark, 0);
                mesh.SetTriangles(_leaves, 1);
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                return mesh;
            }
        }
    }
}

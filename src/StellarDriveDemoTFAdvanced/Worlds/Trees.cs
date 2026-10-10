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
        AutumnBirch
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
            return copy;
        }

        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

        private static Material Bark(Material fir, TreeKind kind)
        {
            bool birch = kind == TreeKind.Birch || kind == TreeKind.AutumnBirch;
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
                _leaves.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
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

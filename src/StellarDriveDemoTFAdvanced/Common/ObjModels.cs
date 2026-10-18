using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace StellarDriveDemoTFAdvanced.Common
{
    /// <summary>
    /// The mod's models made in Blender (tools/blender, exported as OBJ and embedded in the DLL):
    /// one mesh per material group, keyed by the material's name. OBJ is right-handed with Y up,
    /// Unity left-handed: z is mirrored and the triangles turned the other way.
    /// </summary>
    internal static class ObjModels
    {
        private static readonly Dictionary<string, Dictionary<string, Mesh>> Cache = new Dictionary<string, Dictionary<string, Mesh>>();

        /// <summary>The model's meshes by material name, or null when it is not in the DLL.</summary>
        public static Dictionary<string, Mesh> Load(string name)
        {
            if (Cache.TryGetValue(name, out Dictionary<string, Mesh> cached))
                return cached;
            Dictionary<string, Mesh> meshes = null;
            try
            {
                using (Stream stream = typeof(ObjModels).Assembly.GetManifestResourceStream("TFA.Models." + name + ".obj"))
                {
                    if (stream != null)
                        meshes = Parse(name, new StreamReader(stream).ReadToEnd());
                }
            }
            catch (Exception e)
            {
                AdvancedMod.Log.Error($"models: could not read {name}: {e.Message}");
            }
            if (meshes == null)
                AdvancedMod.Log.Warning($"models: {name} is not in the mod");
            Cache[name] = meshes;
            return meshes;
        }

        private sealed class Group
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Vector2> Uvs = new List<Vector2>();
            public readonly List<int> Triangles = new List<int>();
            public readonly Dictionary<(int, int), int> Index = new Dictionary<(int, int), int>();
        }

        private static Dictionary<string, Mesh> Parse(string name, string text)
        {
            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var groups = new Dictionary<string, Group>();
            Group current = null;
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length < 2)
                    continue;
                string[] f = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                switch (f[0])
                {
                    case "v":
                        positions.Add(new Vector3(F(f[1]), F(f[2]), -F(f[3])));
                        break;
                    case "vn":
                        normals.Add(new Vector3(F(f[1]), F(f[2]), -F(f[3])));
                        break;
                    case "usemtl":
                        if (!groups.TryGetValue(f[1], out current))
                            groups[f[1]] = current = new Group();
                        break;
                    case "f":
                        if (current == null)
                            groups["default"] = current = new Group();
                        // Triangles (the exporter triangulates); the mirror turns them around
                        var corners = new int[f.Length - 1];
                        for (int i = 1; i < f.Length; i++)
                            corners[i - 1] = Corner(current, f[i], positions, normals);
                        for (int i = 1; i + 1 < corners.Length; i++)
                        {
                            current.Triangles.Add(corners[0]);
                            current.Triangles.Add(corners[i + 1]);
                            current.Triangles.Add(corners[i]);
                        }
                        break;
                }
            }
            var meshes = new Dictionary<string, Mesh>();
            foreach (KeyValuePair<string, Group> entry in groups)
            {
                Group g = entry.Value;
                var mesh = new Mesh { name = "TFA_" + name + "_" + entry.Key };
                if (g.Vertices.Count > 65000)
                    mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(g.Vertices);
                mesh.SetNormals(g.Normals);
                mesh.SetUVs(0, g.Uvs);
                mesh.SetTriangles(g.Triangles, 0);
                mesh.RecalculateBounds();
                meshes[entry.Key] = mesh;
            }
            return meshes;
        }

        // "v//vn" or "v/vt/vn" (1-based): one Unity vertex per position and normal pair
        private static int Corner(Group g, string token, List<Vector3> positions, List<Vector3> normals)
        {
            string[] parts = token.Split('/');
            int p = int.Parse(parts[0], CultureInfo.InvariantCulture) - 1;
            int n = parts.Length >= 3 && parts[2].Length > 0 ? int.Parse(parts[2], CultureInfo.InvariantCulture) - 1 : -1;
            if (g.Index.TryGetValue((p, n), out int index))
                return index;
            index = g.Vertices.Count;
            g.Vertices.Add(positions[p]);
            Vector3 normal = n >= 0 ? normals[n] : Vector3.up;
            g.Normals.Add(normal);
            // Box projection along the normal's main axis: one surface plate per meter
            Vector3 p3 = positions[p], a = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
            g.Uvs.Add(a.x >= a.y && a.x >= a.z ? new Vector2(p3.z, p3.y) : a.y >= a.z ? new Vector2(p3.x, p3.z) : new Vector2(p3.x, p3.y));
            g.Index[(p, n)] = index;
            return index;
        }

        private static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);
    }
}

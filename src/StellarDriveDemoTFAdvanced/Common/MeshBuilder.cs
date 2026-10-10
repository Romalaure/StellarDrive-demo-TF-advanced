using System.Collections.Generic;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Common
{
    /// <summary>
    /// Builds simple low-poly meshes from boxes, slanted blocks and cylinders/cones.
    /// Triangles are wound automatically so they face away from the shape they belong to.
    /// Texture coordinates follow the game's part style: every box face shows one whole hand-drawn
    /// panel of a texture atlas (<see cref="Panel"/>), outline included; cylinders wrap it once.
    /// </summary>
    internal sealed class MeshBuilder
    {
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Vector3> _normals = new List<Vector3>();
        private readonly List<Vector2> _uvs = new List<Vector2>();
        private readonly List<int> _triangles = new List<int>();

        /// <summary>Atlas rectangle (0-1) the next shapes map their faces to.</summary>
        public Rect Panel = new Rect(0f, 0f, 1f, 1f);

        public Mesh Build(string name) => Build(name, null);

        /// <summary>Builds the mesh; <paramref name="paletteUv"/> fills UV1, which the game's part shader reads its ship palette color from.</summary>
        public Mesh Build(string name, Vector2? paletteUv)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(_vertices);
            mesh.SetNormals(_normals);
            mesh.SetUVs(0, _uvs);
            if (paletteUv.HasValue)
            {
                var palette = new List<Vector2>(_vertices.Count);
                for (int i = 0; i < _vertices.Count; i++)
                    palette.Add(paletteUv.Value);
                mesh.SetUVs(1, palette);
            }
            mesh.SetTriangles(_triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        /// <summary>Copies another builder's triangles, moved by an offset.</summary>
        public void Append(MeshBuilder other, Vector3 offset)
        {
            int start = _vertices.Count;
            foreach (Vector3 v in other._vertices)
                _vertices.Add(v + offset);
            _normals.AddRange(other._normals);
            _uvs.AddRange(other._uvs);
            foreach (int index in other._triangles)
                _triangles.Add(start + index);
        }

        /// <summary>Wavefront OBJ text, for previewing models outside the game.</summary>
        public string ToObj()
        {
            var text = new System.Text.StringBuilder();
            foreach (Vector3 v in _vertices)
                text.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "v {0} {1} {2}", v.x, v.y, v.z));
            for (int i = 0; i < _triangles.Count; i += 3)
                text.AppendLine($"f {_triangles[i] + 1} {_triangles[i + 1] + 1} {_triangles[i + 2] + 1}");
            return text.ToString();
        }

        public void Box(Vector3 center, Vector3 size, Quaternion? rotation = null)
        {
            Quaternion r = rotation ?? Quaternion.identity;
            Vector3 h = size * 0.5f;
            var corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                var local = new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z);
                corners[i] = center + r * local;
            }
            Block(corners);
        }

        /// <summary>Box along the given axes (orthonormal), without needing Unity's native quaternion code.</summary>
        public void Box(Vector3 center, Vector3 size, Vector3 right, Vector3 up, Vector3 forward)
        {
            Vector3 h = size * 0.5f;
            var corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                corners[i] = center
                    + right * ((i & 1) == 0 ? -h.x : h.x)
                    + up * ((i & 2) == 0 ? -h.y : h.y)
                    + forward * ((i & 4) == 0 ? -h.z : h.z);
            }
            Block(corners);
        }

        /// <summary>
        /// Any six-sided block. Corner index bits are (x, y, z): 0 = (-,-,-), 1 = (+,-,-), 2 = (-,+,-) ... 7 = (+,+,+).
        /// </summary>
        public void Block(Vector3[] c)
        {
            Vector3 centroid = Vector3.zero;
            foreach (Vector3 p in c)
                centroid += p;
            centroid /= 8f;

            int[][] faces =
            {
                new[] { 0, 2, 6, 4 }, new[] { 1, 3, 7, 5 }, // -x, +x
                new[] { 0, 1, 5, 4 }, new[] { 2, 3, 7, 6 }, // -y, +y
                new[] { 0, 1, 3, 2 }, new[] { 4, 5, 7, 6 }  // -z, +z
            };
            foreach (int[] f in faces)
            {
                Vector3 faceCenter = (c[f[0]] + c[f[1]] + c[f[2]] + c[f[3]]) * 0.25f;
                Vector3 outward = faceCenter - centroid;
                Quad(c[f[0]], c[f[1]], c[f[2]], c[f[3]], outward);
            }
        }

        /// <summary>
        /// Corner wedge running along x: its base lies on y = y0 from z0 to z1 and its back rises
        /// against z = z1 up to y1, leaving a sloped face toward -z/+y.
        /// </summary>
        public void Wedge(float x0, float x1, float y0, float y1, float z0, float z1)
        {
            var corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                bool top = (i & 2) != 0;
                float x = (i & 1) == 0 ? x0 : x1;
                float z = top || (i & 4) != 0 ? z1 : z0;
                corners[i] = new Vector3(x, top ? y1 : y0, z);
            }
            Block(corners);
        }

        /// <summary>Cylinder or cone from a to b, radius ra at a and rb at b.</summary>
        public void Cylinder(Vector3 a, Vector3 b, float ra, float rb, int sides = 16, bool capA = true, bool capB = true)
        {
            Vector3 axis = (b - a).normalized;
            Vector3 u = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 v = Vector3.Cross(axis, u);
            float length = (b - a).magnitude;
            // Side normals lean along the axis on cones
            float slope = length > 0f ? (ra - rb) / length : 0f;

            var ringA = new Vector3[sides];
            var ringB = new Vector3[sides];
            var radial = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                radial[i] = u * Mathf.Cos(angle) + v * Mathf.Sin(angle);
                ringA[i] = a + radial[i] * ra;
                ringB[i] = b + radial[i] * rb;
            }

            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                Vector3 ni = (radial[i] + axis * slope).normalized;
                Vector3 nj = (radial[j] + axis * slope).normalized;
                float ui = (float)i / sides, uj = (float)(i + 1) / sides;
                Triangle(ringA[i], ringA[j], ringB[j], ni, nj, nj, ni + nj, At(ui, 0f), At(uj, 0f), At(uj, 1f));
                Triangle(ringA[i], ringB[j], ringB[i], ni, nj, ni, ni + nj, At(ui, 0f), At(uj, 1f), At(ui, 1f));
            }

            if (capA && ra > 0f)
                Disc(a, ringA, -axis);
            if (capB && rb > 0f)
                Disc(b, ringB, axis);
        }

        private void Disc(Vector3 center, Vector3[] ring, Vector3 normal)
        {
            int count = ring.Length;
            for (int i = 0; i < count; i++)
            {
                float ai = i * Mathf.PI * 2f / count, aj = (i + 1) * Mathf.PI * 2f / count;
                Triangle(center, ring[i], ring[(i + 1) % count], normal, normal, normal, normal,
                    At(0.5f, 0.5f), At(0.5f + 0.5f * Mathf.Cos(ai), 0.5f + 0.5f * Mathf.Sin(ai)), At(0.5f + 0.5f * Mathf.Cos(aj), 0.5f + 0.5f * Mathf.Sin(aj)));
            }
        }

        private void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 outward)
        {
            // Diagonals give the normal even when two corners meet (wedges built with Block)
            Vector3 n = Vector3.Cross(p2 - p0, p3 - p1);
            if (Vector3.Dot(n, outward) < 0f)
                n = -n;
            n.Normalize();
            // The panel's long side follows the face's long side, so long beams are not squashed
            bool turn = (p1 - p0).sqrMagnitude > (p3 - p0).sqrMagnitude != Panel.width > Panel.height;
            Vector2 uv0 = At(0f, 0f), uv1 = turn ? At(0f, 1f) : At(1f, 0f), uv2 = At(1f, 1f), uv3 = turn ? At(1f, 0f) : At(0f, 1f);
            Triangle(p0, p1, p2, n, n, n, n, uv0, uv1, uv2);
            Triangle(p0, p2, p3, n, n, n, n, uv0, uv2, uv3);
        }

        // A point of the current atlas panel
        private Vector2 At(float u, float v) => new Vector2(Panel.xMin + u * Panel.width, Panel.yMin + v * Panel.height);

        private void Triangle(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 n0, Vector3 n1, Vector3 n2, Vector3 outward, Vector2 t0, Vector2 t1, Vector2 t2)
        {
            // Unity front faces: Cross(p1 - p0, p2 - p0) points out of the surface
            if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), outward) < 0f)
            {
                (p1, p2) = (p2, p1);
                (n1, n2) = (n2, n1);
                (t1, t2) = (t2, t1);
            }
            int start = _vertices.Count;
            _vertices.Add(p0);
            _vertices.Add(p1);
            _vertices.Add(p2);
            _normals.Add(n0);
            _normals.Add(n1);
            _normals.Add(n2);
            _uvs.Add(t0);
            _uvs.Add(t1);
            _uvs.Add(t2);
            _triangles.Add(start);
            _triangles.Add(start + 1);
            _triangles.Add(start + 2);
        }
    }
}

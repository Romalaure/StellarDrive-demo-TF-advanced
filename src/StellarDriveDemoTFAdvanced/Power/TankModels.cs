using Ships.Interface.Model.Parts.Common;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Power
{
    /// <summary>
    /// Armor over the game's fluid tank (a horizontal drum along z, portholes at both ends, a
    /// gauge on top at the front) for the nuclear fuel and dark matter tanks: banded hoops with a
    /// glowing line, ribs and a shield plate on top, a filler hatch and the contents' emblem on
    /// both sides. The game's drum, portholes and gauge stay, so the fill still shows.
    /// </summary>
    internal static class TankModels
    {
        public enum Kind { Nuclear, DarkMatter, Infinite }

        public static void Armor(GameObject prefab, Kind kind)
        {
            MeshRenderer drum = null;
            foreach (MeshRenderer renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.name == "Model" && renderer.GetComponentInParent<SocketObject>() == null)
                {
                    drum = renderer;
                    break;
                }
            }
            if (drum == null)
            {
                AdvancedMod.Log.Warning("tank model: the game's tank has no Model mesh, left plain");
                return;
            }
            Transform parent = drum.transform.parent;
            Drum(drum, out float radius, out Vector3 axis, out float half);
            var m = new MachineModels.Model();
            Build(m, kind, axis, radius, half);
            Color glow = kind == Kind.Nuclear ? MachineModels.Green : MachineModels.Violet;
            Color core = kind == Kind.Nuclear ? new Color(0.6f, 1f, 0.35f) : kind == Kind.Infinite ? new Color(1f, 0.55f, 1f) : new Color(0.8f, 0.5f, 1f);
            MachineModels.Attach(parent, m, "Tank" + kind, glow, core);
        }

        /// <summary>The drum's size in its parent's space: its radius, its axis' center and its half-length.</summary>
        private static void Drum(MeshRenderer drum, out float radius, out Vector3 axis, out float half)
        {
            // Measured once on the game's mesh: a drum of radius 0.42 along z, 0.96 long, center a little high
            radius = 0.42f;
            axis = new Vector3(0f, 0.02f, 0f);
            half = 0.48f;
            Mesh mesh = drum.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh == null)
                return;
            Bounds b = mesh.bounds;
            Transform t = drum.transform;
            Vector3 min = t.localPosition + Vector3.Scale(b.min, t.localScale), max = t.localPosition + Vector3.Scale(b.max, t.localScale);
            radius = (max.x - min.x) * 0.5f * 0.88f;
            half = (max.z - min.z) * 0.5f;
            axis = new Vector3((min.x + max.x) * 0.5f, max.y - radius, (min.z + max.z) * 0.5f);
        }

        private static Vector3 Around(Vector3 axis, float radius, float degrees, float z)
        {
            // 0° on top, 90° toward +x
            float a = degrees * Mathf.Deg2Rad;
            return axis + new Vector3(Mathf.Sin(a) * radius, Mathf.Cos(a) * radius, z);
        }

        private static void Build(MachineModels.Model m, Kind kind, Vector3 c, float r, float half)
        {
            Vector3 z = Vector3.forward;
            // Hoops: two near the ends, one in the middle; each an armored band with a glowing line
            foreach (float at in new[] { -half * 0.62f, 0f, half * 0.62f })
            {
                Vector3 p = c + z * at;
                m.Armor.Cylinder(p - z * 0.035f, p + z * 0.035f, r + 0.022f, r + 0.022f, 28, capA: false, capB: false);
                m.Dark.Cylinder(p - z * 0.04f, p - z * 0.035f, r + 0.026f, r + 0.026f, 28, capA: false, capB: false);
                m.Dark.Cylinder(p + z * 0.035f, p + z * 0.04f, r + 0.026f, r + 0.026f, 28, capA: false, capB: false);
                m.Glow.Cylinder(p - z * 0.008f, p + z * 0.008f, r + 0.027f, r + 0.027f, 28, capA: false, capB: false);
            }
            // Rivets along the hoops' edges
            for (int i = 0; i < 12; i++)
            {
                float deg = i * 30f + 15f;
                foreach (float at in new[] { -half * 0.62f, half * 0.62f })
                {
                    Vector3 p = Around(c, r + 0.026f, deg, at);
                    m.Light.Box(p, new Vector3(0.012f, 0.012f, 0.012f), Quaternion.LookRotation(z, (p - c - z * at).normalized));
                }
            }
            // A shield plate along the top, between two dark ribs
            float length = half * 1.1f;
            foreach (float deg in new[] { -38f, 38f })
            {
                Vector3 p = Around(c, r + 0.015f, deg, 0f);
                m.Dark.Box(p, new Vector3(0.035f, 0.035f, length), Quaternion.LookRotation(z, (p - c).normalized));
            }
            Vector3 top = Around(c, r + 0.012f, 0f, -half * 0.31f);
            m.Armor.Box(top, new Vector3(r * 0.95f, 0.02f, half * 0.5f));
            // Filler hatch on the plate's back half (the gauge is at the front)
            Vector3 hatch = top + Vector3.up * 0.01f;
            m.Armor.Cylinder(hatch, hatch + Vector3.up * 0.035f, 0.085f, 0.075f, 16);
            m.Light.Cylinder(hatch + Vector3.up * 0.035f, hatch + Vector3.up * 0.04f, 0.06f, 0.06f, 16, capA: false, capB: true);
            m.Dark.Box(hatch + Vector3.up * 0.045f, new Vector3(0.1f, 0.012f, 0.018f));
            m.Core.Cylinder(hatch + new Vector3(0.11f, 0f, 0f), hatch + new Vector3(0.11f, 0.02f, 0f), 0.014f, 0.01f, 8);
            // The contents' emblem on both sides
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 face = c + Vector3.right * side * (r + 0.004f);
                var right = new Vector3(0f, 0f, -side);
                m.Dark.Cylinder(face, face + Vector3.right * side * 0.012f, 0.17f, 0.17f, 24);
                m.Armor.Cylinder(face, face + Vector3.right * side * 0.016f, 0.19f, 0.19f, 24, capA: false, capB: false);
                Vector3 front = face + Vector3.right * side * 0.014f;
                switch (kind)
                {
                    case Kind.Nuclear:
                        // The trefoil: three blades around a dot
                        m.Glow.Cylinder(front, front + Vector3.right * side * 0.004f, 0.025f, 0.025f, 12);
                        for (int i = 0; i < 3; i++)
                        {
                            float a = (90f + i * 120f) * Mathf.Deg2Rad;
                            Vector3 dir = Vector3.up * Mathf.Sin(a) + right * Mathf.Cos(a);
                            for (int k = 0; k < 3; k++)
                            {
                                float b = a + (k - 1) * 0.32f;
                                Vector3 d = Vector3.up * Mathf.Sin(b) + right * Mathf.Cos(b);
                                m.Glow.Box(front + d * 0.095f, new Vector3(0.05f, 0.07f, 0.004f), Quaternion.LookRotation(Vector3.right * side, d));
                            }
                        }
                        break;
                    case Kind.DarkMatter:
                        // A dark core in a glowing ring
                        m.Glow.Cylinder(front, front + Vector3.right * side * 0.004f, 0.12f, 0.12f, 24, capA: false, capB: false);
                        m.Glow.Cylinder(front, front + Vector3.right * side * 0.003f, 0.125f, 0.105f, 24);
                        m.Dark.Cylinder(front + Vector3.right * side * 0.002f, front + Vector3.right * side * 0.006f, 0.07f, 0.07f, 20);
                        m.Core.Cylinder(front + Vector3.right * side * 0.004f, front + Vector3.right * side * 0.008f, 0.02f, 0.02f, 12);
                        break;
                    default:
                        // The infinity sign: two rings side by side
                        foreach (float s in new[] { -1f, 1f })
                        {
                            Vector3 loop = front + right * s * 0.065f;
                            for (int i = 0; i < 16; i++)
                            {
                                float a = i * Mathf.PI / 8f;
                                Vector3 d = Vector3.up * Mathf.Sin(a) + right * Mathf.Cos(a);
                                Vector3 tangent = Vector3.up * Mathf.Cos(a) - right * Mathf.Sin(a);
                                m.Core.Box(loop + d * 0.055f, new Vector3(0.024f, 0.022f, 0.005f), Quaternion.LookRotation(Vector3.right * side, tangent));
                            }
                        }
                        break;
                }
            }
            if (kind == Kind.Infinite)
            {
                // A small glowing crystal held over the hatch: the tank that never empties
                Vector3 o = hatch + Vector3.up * 0.06f;
                m.Core.Cylinder(o, o + Vector3.up * 0.04f, 0.005f, 0.025f, 6, capA: true, capB: false);
                m.Core.Cylinder(o + Vector3.up * 0.04f, o + Vector3.up * 0.09f, 0.025f, 0.004f, 6, capA: false, capB: true);
                foreach (float deg in new[] { 0f, 120f, 240f })
                {
                    Vector3 d = Quaternion.Euler(0f, deg, 0f) * Vector3.right;
                    m.Armor.Cylinder(hatch + Vector3.up * 0.04f + d * 0.06f, o + Vector3.up * 0.035f + d * 0.03f, 0.007f, 0.006f, 6);
                }
            }
        }
    }
}

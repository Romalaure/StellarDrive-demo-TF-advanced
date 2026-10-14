using StellarDriveDemoTFAdvanced.Common;
using StellarDriveDemoTFAdvanced.Power;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Propulsion
{
    /// <summary>
    /// Detailed models of the nuclear and dark matter engines, built over the game's thruster
    /// they replace: mount plate at the bottom (the propellant plugs on its sides stay clear),
    /// nozzle opening at the top where the game's flame comes out. Coordinates are fractions of
    /// the thruster's body: x and z of its half-width, y of its height from its base.
    /// </summary>
    internal static class EngineModels
    {
        private struct Frame
        {
            public float R, Bottom, H;

            public Vector3 P(float x, float y, float z) => new Vector3(x * R, Bottom + y * H, z * R);
            public Vector3 Axis(float y) => P(0f, y, 0f);
        }

        private static Frame Of(Bounds body) =>
            new Frame { R = Mathf.Max(body.extents.x, body.extents.z), Bottom = body.min.y, H = body.size.y };

        public static void Nuclear(MachineModels.Model m, Bounds body) => NuclearThermal(m, Of(body), false);

        public static void NuclearHeavy(MachineModels.Model m, Bounds body) => NuclearThermal(m, Of(body), true);

        /// <summary>
        /// A nuclear thermal rocket: mount plate, shadow shield, the reactor vessel with its
        /// windows and ribs, turbopumps feeding the nozzle's cooling jacket, gimbal struts and a
        /// long bell blackened by heat with its cooling tubes and a glowing lip.
        /// </summary>
        private static void NuclearThermal(MachineModels.Model m, Frame f, bool heavy)
        {
            // Mount plate and shadow shield
            m.Armor.Cylinder(f.Axis(0f), f.Axis(0.06f), f.R * 0.98f, f.R * 0.94f, 8);
            m.Light.Cylinder(f.Axis(0.06f), f.Axis(0.068f), f.R * 0.93f, f.R * 0.93f, 8, capA: false, capB: false);
            m.Dark.Cylinder(f.Axis(0.068f), f.Axis(0.1f), f.R * 0.78f, f.R * 0.74f, 16);
            // Reactor vessel
            float vLow = 0.1f, vHigh = heavy ? 0.4f : 0.44f, vR = 0.6f;
            m.Armor.Cylinder(f.Axis(vLow), f.Axis(vHigh), f.R * vR, f.R * vR, 20, capA: false, capB: false);
            foreach (float y in new[] { vLow + 0.04f, vHigh - 0.04f })
                m.Light.Cylinder(f.Axis(y), f.Axis(y + 0.012f), f.R * (vR + 0.02f), f.R * (vR + 0.02f), 20, capA: false, capB: false);
            m.Glow.Cylinder(f.Axis((vLow + vHigh) * 0.5f - 0.02f), f.Axis((vLow + vHigh) * 0.5f + 0.02f), f.R * (vR + 0.01f), f.R * (vR + 0.01f), 20, capA: false, capB: false);
            for (int i = 0; i < 12; i++)
            {
                float a = (i * 30f + 15f) * Mathf.Deg2Rad;
                var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 c = f.Axis((vLow + vHigh) * 0.5f) + outward * f.R * (vR + 0.025f);
                m.Dark.Box(c, new Vector3(f.R * 0.05f, (vHigh - vLow) * f.H * 0.86f, f.R * 0.05f), Quaternion.LookRotation(outward));
            }
            // Converging section to the throat
            float throat = vHigh + 0.1f;
            m.Armor.Cylinder(f.Axis(vHigh), f.Axis(throat), f.R * vR, f.R * 0.28f, 20, capA: true, capB: false);
            m.Core.Cylinder(f.Axis(throat - 0.01f), f.Axis(throat + 0.03f), f.R * 0.24f, f.R * 0.26f, 16, capA: true, capB: false);
            // Bell: a curved nozzle in rings, blackened, with stiffening rings and a hot lip
            const int rings = 7;
            float exitR = heavy ? 0.98f : 0.92f;
            Vector3 Bell(float t, float angle, float extra) =>
                f.Axis(throat + (1f - throat) * t) + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * f.R * (0.28f + (exitR - 0.28f) * Mathf.Sqrt(t) + extra);
            float BellR(float t) => f.R * (0.28f + (exitR - 0.28f) * Mathf.Sqrt(t));
            for (int i = 0; i < rings; i++)
            {
                float t0 = i / (float)rings, t1 = (i + 1) / (float)rings;
                m.Dark.Cylinder(f.Axis(throat + (1f - throat) * t0), f.Axis(throat + (1f - throat) * t1), BellR(t0), BellR(t1), 24, capA: false, capB: false);
            }
            foreach (float t in heavy ? new[] { 0.3f, 0.55f, 0.8f } : new[] { 0.4f, 0.75f })
            {
                float y = throat + (1f - throat) * t;
                m.Armor.Cylinder(f.Axis(y - 0.012f), f.Axis(y + 0.012f), BellR(t) + f.R * 0.03f, BellR(t) + f.R * 0.03f, 24, capA: false, capB: false);
            }
            m.Core.Cylinder(f.Axis(0.985f), f.Axis(1f), BellR(1f) + f.R * 0.01f, BellR(1f) + f.R * 0.02f, 24, capA: false, capB: false);
            // Cooling tubes down the bell's outside
            int tubes = heavy ? 16 : 12;
            for (int k = 0; k < tubes; k++)
            {
                float angle = k * Mathf.PI * 2f / tubes;
                for (int i = 0; i < rings; i++)
                {
                    float t0 = i / (float)rings, t1 = (i + 1) / (float)rings;
                    m.Armor.Cylinder(Bell(t0, angle, 0.02f), Bell(t1, angle, 0.02f), f.R * 0.018f, f.R * 0.018f, 6, capA: false, capB: false);
                }
            }
            // Turbopumps (front and back, clear of the plugs on the sides) feeding the cooling jacket
            float[] pumpAngles = heavy ? new[] { 90f, 270f, 45f, 135f } : new[] { 90f, 270f };
            foreach (float deg in pumpAngles)
            {
                float a = deg * Mathf.Deg2Rad;
                var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 foot = f.Axis(0.1f) + outward * f.R * 0.8f;
                Vector3 head = f.Axis(vHigh - 0.04f) + outward * f.R * 0.8f;
                m.Armor.Cylinder(foot, head, f.R * 0.13f, f.R * 0.11f, 12);
                m.Light.Cylinder(head, head + Vector3.up * f.H * 0.015f, f.R * 0.115f, f.R * 0.115f, 12, capA: false, capB: true);
                m.Glow.Box(Vector3.Lerp(foot, head, 0.5f) + outward * f.R * 0.125f, new Vector3(f.R * 0.06f, (head - foot).magnitude * 0.5f, f.R * 0.01f), Quaternion.LookRotation(outward));
                // Pipe up to the bell's cooling manifold
                Vector3 manifold = Bell(0.35f, a, 0.04f);
                Vector3 elbow = head + Vector3.up * f.H * 0.05f;
                m.Light.Cylinder(head, elbow, f.R * 0.035f, f.R * 0.035f, 8);
                m.Light.Cylinder(elbow, manifold, f.R * 0.035f, f.R * 0.035f, 8);
            }
            // Cooling manifold ring
            float my = throat + (1f - throat) * 0.35f;
            m.Light.Cylinder(f.Axis(my - 0.01f), f.Axis(my + 0.01f), BellR(0.35f) + f.R * 0.05f, BellR(0.35f) + f.R * 0.05f, 24, capA: false, capB: false);
            // Gimbal struts on the diagonals, from the mount plate to the bell
            for (int i = 0; i < 4; i++)
            {
                float a = (45f + i * 90f + (heavy ? 22.5f : 0f)) * Mathf.Deg2Rad;
                var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 low = f.Axis(0.06f) + outward * f.R * 0.86f;
                Vector3 high = Bell(0.15f, a, 0.03f);
                m.Armor.Cylinder(low, high, f.R * 0.03f, f.R * 0.025f, 8);
                m.Dark.Cylinder(low, low + (high - low).normalized * f.H * 0.06f, f.R * 0.045f, f.R * 0.045f, 8);
            }
            if (heavy)
            {
                // Radiator panels front and back, with glowing coolant channels
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 c = f.P(0f, 0.27f, side * 0.97f);
                    m.Armor.Box(c, new Vector3(f.R * 0.9f, f.H * 0.26f, f.R * 0.03f));
                    for (int k = 0; k < 4; k++)
                        m.Glow.Box(c + new Vector3((k - 1.5f) * f.R * 0.2f, 0f, side * f.R * 0.018f), new Vector3(f.R * 0.03f, f.H * 0.22f, f.R * 0.006f));
                    m.Light.Box(c + Vector3.up * f.H * 0.135f, new Vector3(f.R * 0.92f, f.R * 0.02f, f.R * 0.035f));
                }
            }
        }

        /// <summary>
        /// The dark matter engine: an armored base, a containment sphere ringed with field coils in
        /// a cage of ribs, and a magnetic nozzle made of floating rings held by three rails.
        /// </summary>
        public static void DarkMatter(MachineModels.Model m, Bounds body)
        {
            Frame f = Of(body);
            m.Armor.Cylinder(f.Axis(0f), f.Axis(0.07f), f.R * 0.98f, f.R * 0.92f, 8);
            m.Dark.Cylinder(f.Axis(0.07f), f.Axis(0.1f), f.R * 0.84f, f.R * 0.8f, 8);
            m.Light.Cylinder(f.Axis(0.1f), f.Axis(0.108f), f.R * 0.8f, f.R * 0.8f, 8, capA: false, capB: false);
            // Containment sphere (two cones) and its field coils
            float c = 0.3f, half = 0.17f, sR = 0.52f;
            m.Core.Cylinder(f.Axis(c - half), f.Axis(c), f.R * 0.08f, f.R * sR, 16, capA: true, capB: false);
            m.Core.Cylinder(f.Axis(c), f.Axis(c + half), f.R * sR, f.R * 0.08f, 16, capA: false, capB: true);
            foreach (float dy in new[] { -0.09f, 0f, 0.09f })
            {
                float r = f.R * (sR + 0.1f - Mathf.Abs(dy) * 1.4f);
                m.Glow.Cylinder(f.Axis(c + dy - 0.012f), f.Axis(c + dy + 0.012f), r, r, 24, capA: false, capB: false);
                m.Armor.Cylinder(f.Axis(c + dy - 0.025f), f.Axis(c + dy - 0.012f), r + f.R * 0.02f, r + f.R * 0.02f, 24, capA: false, capB: false);
            }
            // Cage of ribs around the sphere
            for (int i = 0; i < 8; i++)
            {
                float a = (i * 45f + 22.5f) * Mathf.Deg2Rad;
                var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 p0 = f.Axis(0.1f) + outward * f.R * 0.6f;
                Vector3 p1 = f.Axis(c) + outward * f.R * 0.74f;
                Vector3 p2 = f.Axis(c + half + 0.06f) + outward * f.R * 0.36f;
                m.Armor.Cylinder(p0, p1, f.R * 0.035f, f.R * 0.03f, 6);
                m.Armor.Cylinder(p1, p2, f.R * 0.03f, f.R * 0.025f, 6);
                m.Light.Box(p1 + outward * f.R * 0.03f, new Vector3(f.R * 0.012f, f.H * 0.05f, f.R * 0.012f), Quaternion.LookRotation(outward));
            }
            // Throat: a dark collar and the bright exit of the chamber
            float throat = c + half + 0.06f;
            m.Dark.Cylinder(f.Axis(throat - 0.03f), f.Axis(throat + 0.04f), f.R * 0.36f, f.R * 0.3f, 16, capA: true, capB: false);
            m.Core.Cylinder(f.Axis(throat + 0.035f), f.Axis(throat + 0.05f), f.R * 0.22f, f.R * 0.22f, 16, capA: true, capB: false);
            // Magnetic nozzle: floating rings opening upward, held by three rails
            const int rings = 5;
            float RingR(int i) => f.R * (0.38f + 0.56f * Mathf.Sqrt((i + 1) / (float)rings));
            float RingY(int i) => throat + 0.08f + (0.98f - throat - 0.08f) * i / (rings - 1);
            for (int i = 0; i < rings; i++)
            {
                float y = RingY(i), r = RingR(i);
                m.Armor.Cylinder(f.Axis(y - 0.02f), f.Axis(y + 0.02f), r, r * 1.02f, 28, capA: false, capB: false);
                m.Glow.Cylinder(f.Axis(y - 0.008f), f.Axis(y + 0.008f), r * 0.97f, r * 0.97f, 28, capA: false, capB: false);
                m.Dark.Cylinder(f.Axis(y - 0.02f), f.Axis(y - 0.032f), r * 1.02f, r * 1.04f, 28, capA: false, capB: false);
            }
            foreach (float deg in new[] { 90f, 210f, 330f })
            {
                float a = deg * Mathf.Deg2Rad;
                var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 low = f.Axis(0.1f) + outward * f.R * 0.82f;
                Vector3 mid = f.Axis(RingY(0)) + outward * (RingR(0) + f.R * 0.06f);
                Vector3 top = f.Axis(RingY(rings - 1)) + outward * (RingR(rings - 1) + f.R * 0.06f);
                m.Armor.Cylinder(low, mid, f.R * 0.05f, f.R * 0.045f, 6);
                m.Armor.Cylinder(mid, top, f.R * 0.045f, f.R * 0.035f, 6);
                m.Light.Cylinder(Vector3.Lerp(mid, top, 0.05f), Vector3.Lerp(mid, top, 0.95f), f.R * 0.012f, f.R * 0.012f, 4, capA: false, capB: false);
                for (int i = 0; i < rings; i++)
                    m.Dark.Box(f.Axis(RingY(i)) + outward * (RingR(i) + f.R * 0.03f), new Vector3(f.R * 0.08f, f.H * 0.035f, f.R * 0.08f), Quaternion.LookRotation(outward));
            }
        }
    }
}

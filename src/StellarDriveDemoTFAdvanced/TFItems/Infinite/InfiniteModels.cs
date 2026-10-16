using StellarDriveDemoTFAdvanced.Power;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.TFItems.Infinite
{
    /// <summary>
    /// What sets the infinite parts apart from the game's: over the game's model, an armored
    /// frame in the game's part look with glowing lines, the infinity sign and a small crystal,
    /// each in its color (cyan generator and oxygen, orange ethanol, gold chest). The tanks wear
    /// the same armor as the mod's other tanks (<see cref="TankModels"/>).
    /// </summary>
    internal static class InfiniteModels
    {
        private static readonly Color Gold = new Color(1f, 0.75f, 0.25f), GoldCore = new Color(1f, 0.9f, 0.55f);

        public static void Dress(ushort id, GameObject prefab)
        {
            switch (id)
            {
                case InfiniteParts.OxygenTank: TankModels.Armor(prefab, TankModels.Kind.InfiniteOxygen); break;
                case InfiniteParts.EthanolTank: TankModels.Armor(prefab, TankModels.Kind.InfiniteEthanol); break;
                case InfiniteParts.Generator: Generator(prefab); break;
                case InfiniteParts.ResourceChest: Chest(prefab); break;
            }
        }

        private static Transform Visuals(GameObject prefab)
        {
            Transform visuals = prefab.transform.Find("Visuals") ?? prefab.transform.Find("Visual");
            return visuals != null ? visuals : prefab.transform;
        }

        // The generator (0.9 × 0.64 × 0.98, its plugs low on both sides): a cage of four posts and
        // a top frame, glowing coils on the posts, a crystal over the top and the sign on the front
        private static void Generator(GameObject prefab)
        {
            var m = new MachineModels.Model();
            const float x = 0.42f, z = 0.46f, top = 0.7f;
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float sz in new[] { -1f, 1f })
                {
                    var foot = new Vector3(sx * x, 0f, sz * z);
                    m.Armor.Box(foot + Vector3.up * top * 0.5f, new Vector3(0.05f, top, 0.05f));
                    m.Dark.Box(foot + Vector3.up * 0.02f, new Vector3(0.08f, 0.04f, 0.08f));
                    for (int i = 0; i < 3; i++)
                        m.Glow.Box(foot + Vector3.up * (0.22f + i * 0.13f), new Vector3(0.058f, 0.018f, 0.058f));
                    m.Light.Box(foot + new Vector3(sx * 0.026f, top * 0.5f, 0f), new Vector3(0.004f, top * 0.8f, 0.012f));
                }
                m.Armor.Box(new Vector3(sx * x, top, 0f), new Vector3(0.05f, 0.05f, z * 2f + 0.05f));
            }
            foreach (float sz in new[] { -1f, 1f })
                m.Armor.Box(new Vector3(0f, top, sz * z), new Vector3(x * 2f + 0.05f, 0.05f, 0.05f));
            // Two cross beams carrying the crystal's cradle
            m.Armor.Box(new Vector3(0f, top + 0.01f, 0f), new Vector3(x * 2f, 0.03f, 0.04f));
            m.Armor.Box(new Vector3(0f, top + 0.01f, 0f), new Vector3(0.04f, 0.03f, z * 2f));
            m.Armor.Cylinder(new Vector3(0f, top + 0.025f, 0f), new Vector3(0f, top + 0.06f, 0f), 0.07f, 0.055f, 12);
            m.Glow.Cylinder(new Vector3(0f, top + 0.06f, 0f), new Vector3(0f, top + 0.065f, 0f), 0.05f, 0.05f, 12, capA: false, capB: true);
            Crystal(m, new Vector3(0f, top + 0.08f, 0f), 0.13f);
            // The sign on a plate across the front posts, above the game's model
            Vector3 plate = new Vector3(0f, top - 0.09f, -z - 0.03f);
            m.Dark.Box(plate, new Vector3(0.36f, 0.13f, 0.012f));
            m.Armor.Box(plate + new Vector3(0f, 0f, 0.004f), new Vector3(0.4f, 0.16f, 0.008f));
            TankModels.Infinity(m.Core, plate + new Vector3(0f, 0f, -0.008f), Vector3.back, Vector3.left, 0.8f);
            MachineModels.Attach(Visuals(prefab), m, "InfiniteGenerator", MachineModels.Cyan, new Color(0.7f, 0.97f, 1f));
        }

        // The chest (0.86 × 0.56 × 0.62, item plugs in the middle of its front and back, handles on
        // its ends): armored guards hugging its vertical edges, a gold rim round the lid with gems at
        // its corners and the sign on a plate on the lid
        private static void Chest(GameObject prefab)
        {
            var m = new MachineModels.Model();
            // Sized on the game's chest mesh (its rounded corners: the guards stand just off them)
            float hx = 0.43f, h = 0.56f, z0 = -0.2f, z1 = 0.42f;
            MeshFilter chest = null;
            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.name == "Model" && filter.sharedMesh != null)
                {
                    chest = filter;
                    break;
                }
            }
            if (chest != null)
            {
                Bounds b = chest.sharedMesh.bounds;
                // (its bounds take in the handles on the ends and the plug plates)
                hx = Mathf.Max(-b.min.x, b.max.x) - 0.065f;
                h = b.max.y;
                z0 = b.min.z + 0.045f;
                z1 = b.max.z - 0.045f;
            }
            float cz = (z0 + z1) * 0.5f, hz = (z1 - z0) * 0.5f;
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float sz in new[] { -1f, 1f })
                {
                    var corner = new Vector3(sx * hx, 0f, cz + sz * hz);
                    m.Armor.Box(corner + new Vector3(-sx * 0.022f, h * 0.5f, 0f), new Vector3(0.05f, h, 0.012f));
                    m.Armor.Box(corner + new Vector3(0f, h * 0.5f, -sz * 0.022f), new Vector3(0.012f, h, 0.05f));
                    m.Glow.Box(corner + new Vector3(-sx * 0.022f, h * 0.5f, sz * 0.007f), new Vector3(0.008f, h * 0.8f, 0.003f));
                    m.Dark.Box(corner + new Vector3(-sx * 0.02f, 0.012f, -sz * 0.02f), new Vector3(0.055f, 0.024f, 0.055f));
                    m.Core.Cylinder(corner + new Vector3(-sx * 0.02f, h, -sz * 0.02f), corner + new Vector3(-sx * 0.02f, h + 0.02f, -sz * 0.02f), 0.016f, 0.005f, 6);
                }
            }
            // Gold line round the body under the lid
            foreach (float sz in new[] { -1f, 1f })
                m.Glow.Box(new Vector3(0f, h - 0.075f, cz + sz * (hz + 0.003f)), new Vector3(hx * 2f - 0.06f, 0.01f, 0.006f));
            foreach (float sx in new[] { -1f, 1f })
                m.Glow.Box(new Vector3(sx * (hx + 0.003f), h - 0.075f, cz), new Vector3(0.006f, 0.01f, hz * 2f - 0.06f));
            // The sign on a plate set into the lid, between the game's straps
            var plate = new Vector3(0f, h + 0.006f, cz);
            m.Armor.Box(plate, new Vector3(0.3f, 0.012f, 0.17f));
            m.Dark.Box(plate + Vector3.up * 0.007f, new Vector3(0.27f, 0.004f, 0.14f));
            TankModels.Infinity(m.Core, plate + Vector3.up * 0.01f, Vector3.up, Vector3.right, 0.85f);
            MachineModels.Attach(Visuals(prefab), m, "InfiniteChest", Gold, GoldCore);
        }

        // A standing crystal: two hexagonal cones
        private static void Crystal(MachineModels.Model m, Vector3 foot, float height)
        {
            m.Core.Cylinder(foot, foot + Vector3.up * height * 0.4f, height * 0.06f, height * 0.22f, 6, capA: true, capB: false);
            m.Core.Cylinder(foot + Vector3.up * height * 0.4f, foot + Vector3.up * height, height * 0.22f, 0f, 6, capA: false, capB: false);
        }
    }
}

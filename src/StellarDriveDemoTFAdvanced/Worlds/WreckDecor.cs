using System.Collections.Generic;
using System.Linq;
using Ships.Interface.Settings;
using StellarDriveDemoTFAdvanced.Common;
using StellarDriveDemoTFAdvanced.Parts;
using StellarDriveDemoTFAdvanced.Power;
using UnityEngine;
using UnityEngine.Rendering;
using Blueprint = StellarDriveDemoTFAdvanced.Worlds.AbandonedStations.Blueprint;

namespace StellarDriveDemoTFAdvanced.Worlds
{
    /// <summary>
    /// The dressing of the big wrecks and of the Hades probe: one part per station, not in the
    /// build menu, sitting at the station's origin and holding, in its frame, everything the voxel
    /// hull cannot show. It is made from the station's own blueprint (same seed, same breaches):
    /// armour plates, greebles, vents, pipe runs and window frames on the hull faces, scorched rims,
    /// torn plating, cut girders and cables around every breach; then each ship's own pieces:
    /// the Vagabond's prow, ribs, container stacks, crane, torn-off engine section and nacelles;
    /// the Leviathan's dagger bow and blades, dorsal decks, dead turrets, bridge, hangar mouth and
    /// engine bank; the probe's gravimetric ring, dish, sensor boom and solar wings.
    /// Its big solid pieces collide (convex colliders, <see cref="DecorColliders"/>); the small
    /// ones (greebles, torn plating, cables) do not.
    /// </summary>
    internal static class WreckDecor
    {
        public const ushort VagabondId = 7461, LeviathanId = 7462, HadesId = 7463, HermesId = 7466;

        private const int GlassWall = 7;

        private sealed class Kit
        {
            public readonly MeshBuilder Hull = new MeshBuilder { Panel = GameSkin.PlainPanel };
            public readonly MeshBuilder Plate = new MeshBuilder { Panel = GameSkin.PlainPanel };
            public readonly MeshBuilder Dark = new MeshBuilder();
            public readonly MeshBuilder Trim = new MeshBuilder();
            // Open shapes (nozzle bells, dishes, torn plating) are seen from both sides
            public readonly MeshBuilder Shell = new MeshBuilder { DoubleSided = true };
            public readonly MeshBuilder Rust = new MeshBuilder { DoubleSided = true };
            public readonly MeshBuilder Glow = new MeshBuilder();
            public readonly MeshBuilder Red = new MeshBuilder();
            public readonly MeshBuilder Solar = new MeshBuilder { DoubleSided = true };
            public readonly MeshBuilder Hazard = new MeshBuilder();
            public readonly MeshBuilder[] Cargo = { new MeshBuilder(), new MeshBuilder(), new MeshBuilder() };
            // The big solid pieces (prows, blades, containers, nacelles...): each one a convex collider
            public readonly List<MeshBuilder> Solids = new List<MeshBuilder>();

            public MeshBuilder Solid()
            {
                var solid = new MeshBuilder();
                Solids.Add(solid);
                return solid;
            }
        }

        public static void Register()
        {
            foreach (ushort id in new[] { VagabondId, LeviathanId, HadesId, HermesId })
            {
                ushort part = id;
                PartFactory.Register(part, "SignalDisplay", "", 0, (settings, prefab) => Configure(part, settings, prefab));
            }
            AdvancedMod.Log.Msg("registered the wrecks' dressing");
        }

        private static void Configure(ushort id, PartSettings settings, GameObject prefab)
        {
            AbandonedStations.StationSpec spec = AbandonedStations.All.First(s => s.Decor == id);
            if (spec.Merchant)
                PartFactory.Describe(settings, Loc.Pick("Coque : " + spec.Name, spec.Name + " (hull)", "Casco: " + spec.Name),
                    Loc.Pick("L'habillage d'un cargo marchand.", "The dressing of a merchant freighter.", "El revestimiento de un carguero mercante."), 60f, new (uint, int)[0]);
            else
                PartFactory.Describe(settings, Loc.Pick("Carcasse : " + spec.Name, spec.Name + " (hull)", "Casco: " + spec.Name),
                    Loc.Pick("L'habillage d'une épave.", "The dressing of a wreck.", "El revestimiento de un pecio."), 60f, new (uint, int)[0]);
            settings.internalStateClass = PowerCatalog.MachineStateClass;
            Transform visuals = PartFactory.BlankFromSignalDisplay(prefab, Vector3.zero, Vector3.one * 0.3f);
            foreach (Collider collider in prefab.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);

            // The station exactly as it is spawned (same seed: same breaches and windows)
            var plan = new Blueprint(spec.Seed) { Spec = spec };
            spec.Build(plan);
            plan.ToJson(spec.Name);
            var kit = new Kit();
            var random = new System.Random(spec.Seed * 31 + 5);
            Dress(kit, plan, random);
            switch (id)
            {
                case VagabondId: Vagabond(kit, random); break;
                case LeviathanId: Leviathan(kit, random); break;
                case HadesId: Hades(kit, random); break;
            }

            string key = "Wreck" + id;
            Color outside = spec.Outside, band = spec.Band;
            int layer = visuals.gameObject.layer;
            Add(visuals, layer, key + "Hull", kit.Hull, LitMaterials.Get("TFA_" + key + "Hull", outside * 0.95f, 0.5f, 0.35f));
            Add(visuals, layer, key + "Plate", kit.Plate, LitMaterials.Get("TFA_" + key + "Plate", Color.Lerp(outside * 0.7f, new Color(0.3f, 0.3f, 0.32f), 0.3f), 0.55f, 0.4f));
            Add(visuals, layer, key + "Dark", kit.Dark, LitMaterials.Get("TFA_WreckDark", new Color(0.06f, 0.06f, 0.07f), 0.4f, 0.55f));
            Add(visuals, layer, key + "Trim", kit.Trim, LitMaterials.Get("TFA_WreckTrim", new Color(0.52f, 0.52f, 0.5f), 0.75f, 0.45f));
            Add(visuals, layer, key + "Shell", kit.Shell, LitMaterials.Get("TFA_WreckShell", new Color(0.16f, 0.15f, 0.15f), 0.7f, 0.5f));
            Add(visuals, layer, key + "Rust", kit.Rust, LitMaterials.Get("TFA_WreckRust", new Color(0.3f, 0.19f, 0.12f), 0.3f, 0.2f));
            Add(visuals, layer, key + "Glow", kit.Glow, LitMaterials.Get("TFA_" + key + "Glow", band, 0f, 0.8f, band * 0.9f));
            Add(visuals, layer, key + "Red", kit.Red, LitMaterials.Get("TFA_WreckRed", new Color(1f, 0.12f, 0.06f), 0f, 0.8f, new Color(1f, 0.12f, 0.06f) * 2.2f));
            Add(visuals, layer, key + "Solar", kit.Solar, LitMaterials.Get("TFA_WreckSolar", new Color(0.05f, 0.08f, 0.2f), 0.4f, 0.85f));
            Add(visuals, layer, key + "Hazard", kit.Hazard, LitMaterials.Get("TFA_WreckHazard", new Color(0.85f, 0.62f, 0.06f), 0.2f, 0.4f));
            Color[] cargo = { new Color(0.62f, 0.2f, 0.12f), new Color(0.15f, 0.3f, 0.52f), new Color(0.72f, 0.52f, 0.12f) };
            for (int i = 0; i < kit.Cargo.Length; i++)
                Add(visuals, layer, key + "Cargo" + i, kit.Cargo[i], LitMaterials.Get("TFA_WreckCargo" + i, cargo[i], 0.4f, 0.3f));
            var solids = kit.Solids.Select((s, i) => s.Build(key + "Solid" + i)).ToList();
            if (id == HermesId)
                solids.AddRange(Hermes(visuals, layer, spec));
            DecorColliders.Attach(prefab, solids);
        }

        // The merchant freighter's outside, modelled in Blender (tools/blender/merchant_hermes.py):
        // one renderer per look, the big pieces' convex colliders in the Solid_N groups
        private static IEnumerable<Mesh> Hermes(Transform visuals, int layer, AbandonedStations.StationSpec spec)
        {
            Dictionary<string, Mesh> meshes = ObjModels.Load("merchant_hermes");
            if (meshes == null)
                return Enumerable.Empty<Mesh>();
            Color outside = spec.Outside, band = spec.Band;
            Color gold = new Color(0.86f, 0.64f, 0.2f), engine = new Color(0.35f, 0.75f, 1f);
            var looks = new Dictionary<string, Material>
            {
                ["Hull"] = LitMaterials.Get("TFA_HermesHull", outside * 0.95f, 0.5f, 0.4f),
                ["Plate"] = LitMaterials.Get("TFA_HermesPlate", Color.Lerp(outside * 0.75f, new Color(0.3f, 0.3f, 0.33f), 0.3f), 0.55f, 0.45f),
                ["Dark"] = LitMaterials.Get("TFA_WreckDark", new Color(0.06f, 0.06f, 0.07f), 0.4f, 0.55f),
                ["Trim"] = LitMaterials.Get("TFA_WreckTrim", new Color(0.52f, 0.52f, 0.5f), 0.75f, 0.45f),
                ["Gold"] = LitMaterials.Get("TFA_HermesGold", gold, 0.9f, 0.7f),
                ["Glow"] = LitMaterials.Get("TFA_HermesGlow", band, 0f, 0.8f, band * 1.2f),
                ["Engine"] = LitMaterials.Get("TFA_HermesEngine", engine, 0f, 0.8f, engine * 2.4f),
                ["NavRed"] = LitMaterials.Get("TFA_WreckRed", new Color(1f, 0.12f, 0.06f), 0f, 0.8f, new Color(1f, 0.12f, 0.06f) * 2.2f),
                ["NavGreen"] = LitMaterials.Get("TFA_HermesGreen", new Color(0.1f, 1f, 0.25f), 0f, 0.8f, new Color(0.1f, 1f, 0.25f) * 2.2f),
                ["Beacon"] = LitMaterials.Get("TFA_HermesBeacon", Color.white, 0f, 0.8f, Color.white * 2.5f),
                ["Solar"] = LitMaterials.Get("TFA_WreckSolar", new Color(0.05f, 0.08f, 0.2f), 0.4f, 0.85f),
                ["Glass"] = LitMaterials.Get("TFA_HermesGlass", new Color(0.08f, 0.25f, 0.3f), 0.2f, 0.95f),
                ["Hazard"] = LitMaterials.Get("TFA_WreckHazard", new Color(0.85f, 0.62f, 0.06f), 0.2f, 0.4f),
                ["Cargo0"] = LitMaterials.Get("TFA_HermesCargo0", new Color(0.62f, 0.2f, 0.12f), 0.4f, 0.3f),
                ["Cargo1"] = LitMaterials.Get("TFA_HermesCargo1", new Color(0.15f, 0.3f, 0.52f), 0.4f, 0.3f),
                ["Cargo2"] = LitMaterials.Get("TFA_HermesCargo2", new Color(0.72f, 0.52f, 0.12f), 0.4f, 0.3f)
            };
            // Its pieces are big: plates of 1.5 m instead of the small parts' 0.5 m
            foreach (string big in new[] { "Hull", "Plate", "Cargo0", "Cargo1", "Cargo2" })
            {
                Material material = looks[big];
                foreach (string map in new[] { "_BaseMap", "_BumpMap" })
                {
                    if (material.HasProperty(map))
                        material.SetTextureScale(map, new Vector2(0.33f, 0.33f));
                }
            }
            var solids = new List<Mesh>();
            foreach (KeyValuePair<string, Mesh> group in meshes)
            {
                if (group.Key.StartsWith("Solid_"))
                {
                    solids.Add(group.Value);
                    continue;
                }
                if (!looks.TryGetValue(group.Key, out Material look))
                    continue;
                var child = new GameObject("TF_NoPaint_Hermes" + group.Key) { layer = layer };
                child.transform.SetParent(visuals, false);
                child.AddComponent<MeshFilter>().sharedMesh = group.Value;
                var renderer = child.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = look;
                // The lights and small trims cast no shadow: fewer to draw
                bool shadow = group.Key == "Hull" || group.Key == "Plate" || group.Key.StartsWith("Cargo") || group.Key == "Solar" || group.Key == "Dark";
                renderer.shadowCastingMode = shadow && !Settings.LowPerformance.Value ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
            return solids;
        }

        private static void Add(Transform parent, int layer, string name, MeshBuilder builder, Material material)
        {
            if (!builder.HasGeometry)
                return;
            var child = new GameObject("TF_NoPaint_" + name) { layer = layer };
            child.transform.SetParent(parent, false);
            child.AddComponent<MeshFilter>().sharedMesh = builder.Build(name);
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
        }

        // ---- Every hull face and every breach

        // A face's outward normal and two axes along it (u horizontal, w the other one)
        private static void Axes(int face, out Vector3 n, out Vector3 u, out Vector3 w)
        {
            n = Blueprint.Direction(face);
            if (face < 2)
            {
                u = Vector3.forward;
                w = Vector3.up;
            }
            else if (face < 4)
            {
                u = Vector3.right;
                w = Vector3.up;
            }
            else
            {
                u = Vector3.right;
                w = Vector3.forward;
            }
        }

        private static float F(System.Random r, float min, float max) => min + (float)r.NextDouble() * (max - min);

        private static void Dress(Kit k, Blueprint plan, System.Random r)
        {
            var placed = new HashSet<(Vector3Int, int)>(plan.Faces.Select(f => (f.V, f.Face)));
            foreach ((Vector3Int v, int face, int part) in plan.Faces)
            {
                Axes(face, out Vector3 n, out Vector3 u, out Vector3 w);
                Vector3 c = (Vector3)v + n * 0.5f;
                if (part == GlassWall)
                {
                    // Window frame and a mullion now and then
                    foreach (float s in new[] { -1f, 1f })
                    {
                        k.Trim.Box(c + u * (s * 0.47f) + n * 0.03f, new Vector3(0.07f, 0.05f, 1f), u, n, w);
                        k.Trim.Box(c + w * (s * 0.47f) + n * 0.03f, new Vector3(1f, 0.05f, 0.07f), u, n, w);
                    }
                    if (r.NextDouble() < 0.3)
                        k.Trim.Box(c + n * 0.03f, new Vector3(0.04f, 0.04f, 0.94f), u, n, w);
                    continue;
                }

                double roll = r.NextDouble();
                if (roll < 0.2)
                {
                    // Armour plate with a rivet line
                    k.Plate.Box(c + n * 0.035f, new Vector3(0.86f, 0.05f, 0.86f), u, n, w);
                    k.Trim.Box(c + n * 0.065f + w * 0.36f, new Vector3(0.7f, 0.012f, 0.025f), u, n, w);
                }
                else if (roll < 0.47)
                {
                    // Greebles: boxes of equipment of all sizes
                    int count = r.Next(1, 4);
                    for (int i = 0; i < count; i++)
                    {
                        float sx = F(r, 0.1f, 0.42f), sz = F(r, 0.1f, 0.42f), h = F(r, 0.04f, 0.18f);
                        Vector3 at = c + u * F(r, -0.45f + sx * 0.5f, 0.45f - sx * 0.5f) + w * F(r, -0.45f + sz * 0.5f, 0.45f - sz * 0.5f) + n * (h * 0.5f + 0.005f);
                        (r.NextDouble() < 0.6 ? k.Dark : k.Trim).Box(at, new Vector3(sx, h, sz), u, n, w);
                    }
                }
                else if (roll < 0.55)
                {
                    // Vent: a dark recess with slats
                    k.Dark.Box(c + n * 0.02f, new Vector3(0.56f, 0.035f, 0.34f), u, n, w);
                    for (int i = -1; i <= 1; i++)
                        k.Trim.Box(c + n * 0.045f + w * (i * 0.1f), new Vector3(0.56f, 0.02f, 0.03f), u, n, w);
                }

                // Pipe runs along the walls
                if (face < 4 && v.y % 3 == 1 && r.NextDouble() < 0.85)
                    k.Trim.Cylinder(c + n * 0.09f - w * 0.32f - u * 0.51f, c + n * 0.09f - w * 0.32f + u * 0.51f, 0.045f, 0.045f, 6, false, false);
                // Small antennas on the roofs
                if (face == 5 && r.NextDouble() < 0.035)
                {
                    float height = F(r, 0.5f, 1.4f);
                    k.Dark.Cylinder(c + u * 0.3f, c + u * 0.3f + n * height, 0.025f, 0.01f, 5);
                    k.Red.Box(c + u * 0.3f + n * height, Vector3.one * 0.05f);
                }
                if (r.NextDouble() < 0.012)
                    k.Red.Box(c + n * 0.04f, new Vector3(0.08f, 0.06f, 0.08f), u, n, w);
            }

            foreach ((Vector3Int v, int face) in plan.Holes)
            {
                Axes(face, out Vector3 n, out Vector3 u, out Vector3 w);
                Vector3 c = (Vector3)v + n * 0.5f;
                foreach ((Vector3 edge, Vector3 along) in new[] { (u, w), (-u, w), (w, u), (-w, u) })
                {
                    Vector3Int step = Vector3Int.RoundToInt(edge);
                    // Only where the hull goes on past this edge (beside it, or turning at a corner)
                    if (!placed.Contains((v + step, face)) && plan.Inside(v + step))
                        continue;
                    bool acrossU = Mathf.Abs(Vector3.Dot(edge, u)) > 0.5f;
                    // Scorched rim
                    k.Dark.Box(c + edge * 0.44f + n * 0.02f, acrossU ? new Vector3(0.14f, 0.03f, 1f) : new Vector3(1f, 0.03f, 0.14f), u, n, w);
                    // Torn plating, bent in or out
                    int shards = r.Next(0, 3);
                    for (int i = 0; i < shards; i++)
                    {
                        float from = F(r, -0.5f, 0.2f), length = F(r, 0.2f, 0.5f);
                        Vector3 a = c + edge * 0.5f + along * from, b = a + along * length;
                        Vector3 tip = (a + b) * 0.5f - edge * F(r, 0.15f, 0.45f) + n * F(r, -0.3f, 0.5f);
                        Shard(k.Rust, a, b, tip, n, 0.018f);
                    }
                    // A cut cable hanging out
                    if (r.NextDouble() < 0.12)
                    {
                        Vector3 a = c + edge * 0.45f + along * F(r, -0.3f, 0.3f);
                        k.Dark.Cylinder(a - n * 0.1f, a - edge * F(r, 0.2f, 0.5f) + n * F(r, 0.2f, 0.7f), 0.022f, 0.022f, 5);
                    }
                }
                // A girder across the hole, sometimes cut short
                if (r.NextDouble() < 0.3)
                {
                    float length = r.NextDouble() < 0.5 ? 1f : F(r, 0.35f, 0.7f);
                    Vector3 axis = r.NextDouble() < 0.5 ? u : w;
                    Vector3 side = axis == u ? w : u;
                    Vector3 center = c - n * 0.12f + axis * ((1f - length) * 0.5f);
                    k.Trim.Box(center, axis == u ? new Vector3(length, 0.1f, 0.08f) : new Vector3(0.08f, 0.1f, length), u, n, w);
                    if (length < 1f)
                        Shard(k.Rust, center + axis * (length * 0.5f) - side * 0.05f, center + axis * (length * 0.5f) + side * 0.05f, center + axis * (length * 0.5f + 0.15f) + n * 0.05f, n, 0.02f);
                }
            }
        }

        // ---- Shapes

        // A hexahedron from its bottom and top quads (each: back-left, back-right, front-left, front-right)
        private static void Hex(MeshBuilder m, Vector3 bbl, Vector3 bbr, Vector3 bfl, Vector3 bfr, Vector3 tbl, Vector3 tbr, Vector3 tfl, Vector3 tfr) =>
            m.Block(new[] { bbl, bbr, tbl, tbr, bfl, bfr, tfl, tfr });

        // The same, solid
        private static void Hex(Kit k, MeshBuilder m, Vector3 bbl, Vector3 bbr, Vector3 bfl, Vector3 bfr, Vector3 tbl, Vector3 tbr, Vector3 tfl, Vector3 tfr)
        {
            Hex(m, bbl, bbr, bfl, bfr, tbl, tbr, tfl, tfr);
            Hex(k.Solid(), bbl, bbr, bfl, bfr, tbl, tbr, tfl, tfr);
        }

        // A solid ring: one convex piece per segment
        private static void SolidRing(Kit k, Vector3 center, Vector3 axis, float radius, float tube, int segments)
        {
            Vector3 a = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 b = Vector3.Cross(axis.normalized, a);
            for (int i = 0; i < segments; i++)
            {
                float t0 = i * Mathf.PI * 2f / segments, t1 = (i + 1) * Mathf.PI * 2f / segments;
                k.Solid().Cylinder(center + (a * Mathf.Cos(t0) + b * Mathf.Sin(t0)) * radius, center + (a * Mathf.Cos(t1) + b * Mathf.Sin(t1)) * radius, tube, tube, 6);
            }
        }

        // A thin torn plate: base a-b, narrowing to a point
        private static void Shard(MeshBuilder m, Vector3 a, Vector3 b, Vector3 tip, Vector3 n, float t)
        {
            Vector3 narrow = (b - a) * 0.08f;
            Vector3 tl = tip - narrow, tr = tip + narrow;
            m.Block(new[] { a - n * t, b - n * t, a + n * t, b + n * t, tl - n * t, tr - n * t, tl + n * t, tr + n * t });
        }

        private static void Sphere(MeshBuilder m, Vector3 center, float radius, int rings = 8, int sides = 14)
        {
            for (int i = 0; i < rings; i++)
            {
                float a0 = Mathf.PI * (i / (float)rings - 0.5f), a1 = Mathf.PI * ((i + 1) / (float)rings - 0.5f);
                m.Cylinder(center + Vector3.up * (radius * Mathf.Sin(a0)), center + Vector3.up * (radius * Mathf.Sin(a1)),
                    radius * Mathf.Cos(a0), radius * Mathf.Cos(a1), sides, false, false);
            }
        }

        // A ring of tube around an axis
        private static void Ring(MeshBuilder m, Vector3 center, Vector3 axis, float radius, float tube, int segments, int sides = 8)
        {
            Vector3 a = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 b = Vector3.Cross(axis.normalized, a);
            for (int i = 0; i < segments; i++)
            {
                float t0 = (i - 0.03f) * Mathf.PI * 2f / segments, t1 = (i + 1.03f) * Mathf.PI * 2f / segments;
                m.Cylinder(center + (a * Mathf.Cos(t0) + b * Mathf.Sin(t0)) * radius, center + (a * Mathf.Cos(t1) + b * Mathf.Sin(t1)) * radius, tube, tube, sides, false, false);
            }
        }

        // A square lattice girder from a to b
        private static void Truss(MeshBuilder m, Vector3 a, Vector3 b, float width, float bar = 0.045f)
        {
            Vector3 d = (b - a).normalized;
            float length = (b - a).magnitude;
            Vector3 u = Vector3.Cross(d, Mathf.Abs(d.y) < 0.9f ? Vector3.up : Vector3.right).normalized * (width * 0.5f);
            Vector3 v = Vector3.Cross(d, u.normalized) * (width * 0.5f);
            Vector3[] corners = { u + v, u - v, -u - v, -u + v };
            foreach (Vector3 corner in corners)
                m.Cylinder(a + corner, b + corner, bar, bar, 5, false, false);
            int steps = Mathf.Max(1, Mathf.RoundToInt(length / width));
            for (int i = 0; i < steps; i++)
            {
                Vector3 p0 = a + d * (length * i / steps), p1 = a + d * (length * (i + 1) / steps);
                for (int c = 0; c < 4; c++)
                    m.Cylinder(p0 + corners[c], p1 + corners[(c + 1) % 4], bar * 0.7f, bar * 0.7f, 4, false, false);
            }
        }

        // A cargo container: corrugated sides, corner posts, door bars
        private static void Container(Kit k, MeshBuilder paint, Vector3 c, Vector3 size, Quaternion q)
        {
            Vector3 right = q * Vector3.right, up = q * Vector3.up, forward = q * Vector3.forward;
            paint.Box(c, size, right, up, forward);
            k.Solid().Box(c, size, right, up, forward);
            int ribs = Mathf.Max(2, (int)(size.z / 0.32f));
            for (int i = 0; i < ribs; i++)
            {
                float z = -size.z * 0.5f + (i + 0.5f) * size.z / ribs;
                foreach (float s in new[] { -1f, 1f })
                    paint.Box(c + right * (s * (size.x * 0.5f + 0.015f)) + forward * z, new Vector3(0.03f, size.y * 0.84f, 0.07f), right, up, forward);
            }
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    k.Trim.Box(c + right * (sx * size.x * 0.5f) + forward * (sz * size.z * 0.5f), new Vector3(0.1f, size.y + 0.03f, 0.1f), right, up, forward);
            foreach (float s in new[] { -0.22f, 0.22f })
                k.Dark.Box(c + forward * (size.z * 0.5f + 0.02f) + right * (s * size.x), new Vector3(0.035f, size.y * 0.86f, 0.035f), right, up, forward);
        }

        // A rocket nozzle mounted at a point, exhausting along dir
        private static void Nozzle(Kit k, Vector3 at, Vector3 dir, float r, bool lit, bool broken = false, System.Random random = null)
        {
            k.Plate.Cylinder(at - dir * 0.05f, at + dir * 0.6f, r * 1.05f, r * 0.9f, 20);
            k.Trim.Cylinder(at + dir * 0.6f, at + dir * 0.72f, r * 0.94f, r * 0.94f, 20);
            (lit ? k.Glow : k.Dark).Cylinder(at + dir * 0.66f, at + dir * 0.76f, r * 0.7f, r * 0.7f, 20);
            float bell = broken ? r * 0.6f : r * 1.35f;
            k.Shell.Cylinder(at + dir * 0.72f, at + dir * (0.72f + bell), r * 0.72f, r * (broken ? 0.95f : 1.18f), 20, false, false);
            k.Solid().Cylinder(at - dir * 0.05f, at + dir * 0.72f, r * 1.05f, r * 0.94f, 12);
            k.Solid().Cylinder(at + dir * 0.72f, at + dir * (0.72f + bell), r * 0.72f, r * (broken ? 0.95f : 1.18f), 12);
            Vector3 a = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.right).normalized, b = Vector3.Cross(dir, a);
            for (int i = 0; i < 4; i++)
            {
                float t = (i + 0.5f) * Mathf.PI * 0.5f;
                Vector3 o = a * Mathf.Cos(t) + b * Mathf.Sin(t);
                k.Dark.Cylinder(at + dir * 0.3f + o * (r * 0.95f), at + dir * (0.72f + bell * 0.6f) + o * (r * 1.05f), 0.03f + 0.03f * r, 0.03f + 0.03f * r, 5);
            }
            if (broken && random != null)
            {
                Vector3 rim = at + dir * (0.72f + bell);
                for (int i = 0; i < 5; i++)
                {
                    float t = F(random, 0f, Mathf.PI * 2f);
                    Vector3 o = a * Mathf.Cos(t) + b * Mathf.Sin(t);
                    Shard(k.Rust, rim + o * (r * 0.95f) - Vector3.Cross(o, dir) * 0.15f, rim + o * (r * 0.95f) + Vector3.Cross(o, dir) * 0.15f,
                        rim + o * (r * F(random, 0.9f, 1.3f)) + dir * F(random, 0.2f, 0.6f), o, 0.02f);
                }
            }
        }

        // A dish antenna facing dir
        private static void Dish(Kit k, Vector3 at, Vector3 dir, float radius)
        {
            float depth = radius * 0.28f;
            k.Shell.Cylinder(at, at + dir * depth * 0.6f, radius * 0.18f, radius * 0.75f, 28, true, false);
            k.Shell.Cylinder(at + dir * depth * 0.6f, at + dir * depth, radius * 0.75f, radius, 28, false, false);
            k.Solid().Cylinder(at, at + dir * depth, radius * 0.18f, radius, 16);
            Ring(k.Trim, at + dir * depth, dir, radius, 0.06f, 28, 6);
            Vector3 a = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.right).normalized, b = Vector3.Cross(dir, a);
            Vector3 focus = at + dir * (depth + radius * 0.65f);
            for (int i = 0; i < 12; i++)
            {
                float t = i * Mathf.PI / 6f;
                Vector3 o = a * Mathf.Cos(t) + b * Mathf.Sin(t);
                // Ribs behind the bowl
                k.Plate.Box(at + o * (radius * 0.55f) - dir * 0.05f + dir * depth * 0.3f, new Vector3(0.06f, 0.12f, radius * 0.8f), Vector3.Cross(o, dir), dir, o);
                if (i % 3 == 0)
                    k.Dark.Cylinder(at + dir * depth + o * (radius * 0.95f), focus, 0.035f, 0.035f, 5);
            }
            k.Plate.Cylinder(focus - dir * 0.2f, focus + dir * 0.25f, 0.18f, 0.12f, 10);
            k.Red.Cylinder(focus + dir * 0.25f, focus + dir * 0.32f, 0.1f, 0.1f, 10);
        }

        // A dead gun turret: ring, sloped housing, twin barrels (one cut on a broken turret)
        private static void Turret(Kit k, Vector3 at, float yaw, bool broken, System.Random r)
        {
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            Vector3 f = q * Vector3.forward, ri = q * Vector3.right, up = Vector3.up;
            k.Dark.Cylinder(at, at + up * 0.25f, 0.95f, 0.85f, 18);
            k.Solid().Cylinder(at, at + up * 0.25f, 0.95f, 0.85f, 12);
            Vector3 b = at + up * 0.25f;
            Hex(k, k.Plate,
                b - ri * 0.75f - f * 0.9f, b + ri * 0.75f - f * 0.9f, b - ri * 0.72f + f * 0.75f, b + ri * 0.72f + f * 0.75f,
                b + up * 0.6f - ri * 0.62f - f * 0.8f, b + up * 0.6f + ri * 0.62f - f * 0.8f, b + up * 0.42f - ri * 0.55f + f * 0.35f, b + up * 0.42f + ri * 0.55f + f * 0.35f);
            k.Trim.Box(b + up * 0.66f - f * 0.35f, new Vector3(0.3f, 0.12f, 0.3f), ri, up, f);
            foreach (float s in new[] { -0.26f, 0.26f })
            {
                Vector3 start = b + up * 0.28f + ri * s + f * 0.6f;
                bool cut = broken && s > 0f;
                Vector3 end = start + (cut ? f * 0.9f - up * 0.15f : f * 2.6f + (broken ? -up * 0.35f : Vector3.zero));
                k.Trim.Cylinder(start, end, 0.11f, 0.08f, 8);
                if (cut)
                    Shard(k.Rust, end - ri * 0.08f, end + ri * 0.08f, end + f * 0.25f + up * 0.05f, up, 0.02f);
                else
                    k.Dark.Cylinder(end, end + (end - start).normalized * 0.18f, 0.1f, 0.1f, 8);
            }
        }

        // A floating piece of debris near the ship
        private static void Debris(Kit k, Vector3 at, System.Random r)
        {
            Quaternion q = Quaternion.Euler(F(r, 0f, 360f), F(r, 0f, 360f), F(r, 0f, 360f));
            if (r.NextDouble() < 0.5)
            {
                k.Rust.Box(at, new Vector3(F(r, 0.3f, 1.4f), 0.04f, F(r, 0.3f, 1.2f)), q);
            }
            else
            {
                Vector3 d = q * Vector3.forward;
                k.Trim.Box(at, new Vector3(0.12f, 0.12f, F(r, 0.8f, 2.4f)), q);
                Shard(k.Rust, at + d * 0.4f - q * Vector3.right * 0.06f, at + d * 0.4f + q * Vector3.right * 0.06f, at + d * 0.7f, q * Vector3.up, 0.02f);
            }
        }

        // ---- The Vagabond: a heavy freighter, nose toward +z, its engine section broken off

        private static void Vagabond(Kit k, System.Random r)
        {
            // Hold: x ±4.5, y -0.5..3.5, z -18.5..20.5; crew deck ±3.5 to z 28.5; cockpit ±2.5,
            // y -0.5..2.5 to z 32.5; tower ±2.5 y 3.5..5.5 and bridge ±3.5 y 5.5..7.5, z 11.5..17.5;
            // pods x ±(4.5..8.5) y -0.5..2.5 z -14.5..-3.5; engine section ±4.5 y -0.5..3.5 z -28.5..-21.5

            // Chin and prow under the cockpit, a ram, cheek armour along the crew deck
            Hex(k, k.Plate,
                new Vector3(-3.7f, -1.05f, 23f), new Vector3(3.7f, -1.05f, 23f), new Vector3(-2.6f, -1f, 32.6f), new Vector3(2.6f, -1f, 32.6f),
                new Vector3(-3.6f, -0.53f, 23f), new Vector3(3.6f, -0.53f, 23f), new Vector3(-2.55f, -0.53f, 32.6f), new Vector3(2.55f, -0.53f, 32.6f));
            Hex(k, k.Plate,
                new Vector3(-2.6f, -1f, 32.6f), new Vector3(2.6f, -1f, 32.6f), new Vector3(-0.5f, -0.75f, 36.5f), new Vector3(0.5f, -0.75f, 36.5f),
                new Vector3(-2.55f, -0.53f, 32.6f), new Vector3(2.55f, -0.53f, 32.6f), new Vector3(-0.4f, 0.05f, 36f), new Vector3(0.4f, 0.05f, 36f));
            k.Dark.Cylinder(new Vector3(0f, -0.35f, 36.2f), new Vector3(0f, -0.35f, 38f), 0.2f, 0.02f, 8);
            k.Trim.Box(new Vector3(0f, 0.08f, 34f), new Vector3(0.12f, 0.06f, 3.4f));
            // Visor over the cockpit glass and its frame
            Hex(k, k.Plate,
                new Vector3(-2.7f, 2.52f, 28.4f), new Vector3(2.7f, 2.52f, 28.4f), new Vector3(-2.4f, 2.52f, 33.4f), new Vector3(2.4f, 2.52f, 33.4f),
                new Vector3(-2.5f, 2.95f, 28.4f), new Vector3(2.5f, 2.95f, 28.4f), new Vector3(-2.1f, 2.62f, 33.3f), new Vector3(2.1f, 2.62f, 33.3f));
            foreach (float x in new[] { -1.5f, -0.5f, 0.5f, 1.5f })
                k.Trim.Box(new Vector3(x, 1f, 32.56f), new Vector3(0.08f, 3f, 0.06f));
            k.Trim.Box(new Vector3(0f, 1f, 32.56f), new Vector3(5f, 0.08f, 0.06f));
            foreach (float s in new[] { -1f, 1f })
            {
                for (float z = 21.5f; z < 28.5f; z += 1.75f)
                    k.Plate.Box(new Vector3(s * 3.56f, 1f, z + 0.8f), new Vector3(0.08f, 2.6f, 1.6f), Quaternion.Euler(0f, 0f, s * -6f));
                k.Red.Box(new Vector3(s * 3.6f, 2.4f, 28.3f), new Vector3(0.1f, 0.1f, 0.1f));
            }

            // Hold: U frames every 3 m (not across the bridge tower), a keel below
            for (float z = -17.5f; z <= 19.6f; z += 3f)
            {
                foreach (float s in new[] { -1f, 1f })
                {
                    if ((z > -15f && z < -3f) || (s > 0f && z > 4f && z < 7f))
                        continue;
                    k.Trim.Box(new Vector3(s * 4.62f, 1.5f, z), new Vector3(0.2f, 4.3f, 0.36f));
                }
                k.Trim.Box(new Vector3(0f, -0.62f, z), new Vector3(9.4f, 0.2f, 0.36f));
                if (z < 11f || z > 18f)
                    k.Trim.Box(new Vector3(0f, 3.62f, z), new Vector3(9.4f, 0.2f, 0.36f));
            }
            Hex(k, k.Plate,
                new Vector3(-1.2f, -1.3f, -18f), new Vector3(1.2f, -1.3f, -18f), new Vector3(-1.2f, -1.3f, 20f), new Vector3(1.2f, -1.3f, 20f),
                new Vector3(-3.5f, -0.72f, -18.2f), new Vector3(3.5f, -0.72f, -18.2f), new Vector3(-3.5f, -0.72f, 22f), new Vector3(3.5f, -0.72f, 22f));
            // Pod frames
            foreach (float s in new[] { -1f, 1f })
            {
                for (float z = -13.5f; z <= -4f; z += 2.5f)
                {
                    k.Trim.Box(new Vector3(s * 8.62f, 1f, z), new Vector3(0.18f, 3.2f, 0.3f));
                    k.Trim.Box(new Vector3(s * 6.5f, 2.62f, z), new Vector3(4.2f, 0.18f, 0.3f));
                }
            }

            // Container stacks on the hold's roof, two rows, some gone or knocked askew
            foreach (float x in new[] { -3.25f, 3.25f })
            {
                k.Trim.Box(new Vector3(x - 0.55f, 3.6f, -3f), new Vector3(0.1f, 0.1f, 27f));
                k.Trim.Box(new Vector3(x + 0.55f, 3.6f, -3f), new Vector3(0.1f, 0.1f, 27f));
                for (float z = -15.2f; z < 10f; z += 2.85f)
                {
                    if (z > -1.5f && z < 2.5f)
                        continue;
                    int high = r.NextDouble() < 0.15 ? 0 : r.NextDouble() < 0.55 ? 2 : 1;
                    for (int level = 0; level < high; level++)
                    {
                        bool askew = r.NextDouble() < 0.12;
                        Quaternion q = askew ? Quaternion.Euler(F(r, -8f, 8f), F(r, 12f, 35f) * (r.NextDouble() < 0.5 ? -1f : 1f), F(r, -10f, 10f)) : Quaternion.identity;
                        Vector3 c = new Vector3(x + (askew ? F(r, -0.4f, 0.4f) : 0f), 3.68f + 0.6f + level * 1.2f + (askew ? 0.15f : 0f), z);
                        Container(k, k.Cargo[r.Next(0, 3)], c, new Vector3(1.2f, 1.15f, 2.6f), q);
                    }
                }
            }
            // A container torn loose, drifting above the hold
            Container(k, k.Cargo[1], new Vector3(1.5f, 8.2f, -6f), new Vector3(1.2f, 1.15f, 2.6f), Quaternion.Euler(25f, 50f, 70f));

            // Cargo crane at the stern of the roof
            k.Plate.Cylinder(new Vector3(-2f, 3.5f, -17f), new Vector3(-2f, 4.1f, -17f), 0.6f, 0.5f, 14);
            k.Solid().Cylinder(new Vector3(-2f, 3.5f, -17f), new Vector3(-2f, 8.3f, -17f), 0.6f, 0.5f, 8);
            Truss(k.Trim, new Vector3(-2f, 4.1f, -17f), new Vector3(-2f, 8f, -17f), 0.5f);
            Truss(k.Trim, new Vector3(-2f, 7.8f, -17.2f), new Vector3(2.5f, 9.2f, -10f), 0.4f);
            k.Dark.Cylinder(new Vector3(2.5f, 9.1f, -10f), new Vector3(2.6f, 5.8f, -10.2f), 0.025f, 0.025f, 5);
            k.Hazard.Box(new Vector3(2.6f, 5.6f, -10.2f), new Vector3(0.4f, 0.3f, 0.4f));
            k.Plate.Box(new Vector3(-2f, 8.3f, -17.3f), new Vector3(0.9f, 0.7f, 1f));

            // Bridge: visor, antenna mast, radar dish, sensor dome, lights on the corners
            Hex(k, k.Plate,
                new Vector3(-3.7f, 7.52f, 11.3f), new Vector3(3.7f, 7.52f, 11.3f), new Vector3(-3.4f, 7.52f, 18.4f), new Vector3(3.4f, 7.52f, 18.4f),
                new Vector3(-3.4f, 7.8f, 11.4f), new Vector3(3.4f, 7.8f, 11.4f), new Vector3(-3f, 7.62f, 18.2f), new Vector3(3f, 7.62f, 18.2f));
            Truss(k.Trim, new Vector3(-2f, 7.8f, 12.5f), new Vector3(-2f, 12.5f, 12.5f), 0.35f, 0.035f);
            k.Red.Box(new Vector3(-2f, 12.6f, 12.5f), Vector3.one * 0.12f);
            Dish(k, new Vector3(1.8f, 8.6f, 13f), new Vector3(0.3f, 0.6f, 0.75f).normalized, 1.5f);
            k.Trim.Cylinder(new Vector3(1.8f, 7.8f, 13f), new Vector3(1.8f, 8.6f, 13f), 0.1f, 0.1f, 8);
            Sphere(k.Hull, new Vector3(0f, 8.1f, 16.6f), 0.55f);
            foreach (float sx in new[] { -1f, 1f })
                foreach (float z in new[] { 11.6f, 17.4f })
                    k.Red.Box(new Vector3(sx * 3.4f, 7.85f, z), Vector3.one * 0.1f);

            // Landing legs folded under the hold
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float z in new[] { -12f, 13f })
                {
                    Vector3 hinge = new Vector3(sx * 3.2f, -0.75f, z);
                    k.Plate.Box(hinge, new Vector3(0.6f, 0.4f, 0.8f));
                    k.Trim.Cylinder(hinge, hinge + new Vector3(sx * 0.6f, -1.2f, 1.2f), 0.13f, 0.11f, 8);
                    k.Dark.Cylinder(hinge + new Vector3(sx * 0.6f, -1.2f, 1.2f), hinge + new Vector3(sx * 0.6f, -1.35f, 1.2f), 0.45f, 0.45f, 12);
                }
            }

            // The break: girders and conduits left between the hold and the engine section
            foreach ((float x, float y, bool whole) in new[] { (-4.2f, -0.2f, true), (4.2f, -0.2f, false), (-4.2f, 3.2f, false), (4.2f, 3.2f, true) })
            {
                float end = whole ? -21.5f : F(r, -20.3f, -19.6f);
                k.Trim.Box(new Vector3(x, y, (-18.5f + end) * 0.5f), new Vector3(0.22f, 0.32f, -18.5f - end + 0.1f));
                if (!whole)
                    Shard(k.Rust, new Vector3(x - 0.1f, y, end), new Vector3(x + 0.1f, y, end), new Vector3(x + F(r, -0.2f, 0.2f), y + F(r, -0.3f, 0.3f), end - 0.4f), Vector3.up, 0.03f);
            }
            foreach ((float x, float y, float cut) in new[] { (-1.6f, 0.3f, 1f), (0.2f, 0.3f, 0.45f), (1.8f, 0.3f, 1f) })
            {
                float end = -18.5f - 3f * cut;
                k.Plate.Cylinder(new Vector3(x, y, -18.4f), new Vector3(x, y - (1f - cut) * 0.4f, end), 0.24f, 0.24f, 10);
                if (cut < 1f)
                    k.Dark.Cylinder(new Vector3(x, y - 0.25f, end), new Vector3(x + 0.4f, y - 1f, end - 0.6f), 0.05f, 0.05f, 5);
            }
            for (int i = 0; i < 9; i++)
            {
                float x = F(r, -3.5f, 3.5f), y = F(r, 0f, 3f);
                k.Dark.Cylinder(new Vector3(x, y, -18.5f), new Vector3(x + F(r, -0.6f, 0.6f), y - F(r, 0.3f, 1.4f), -18.5f - F(r, 0.6f, 1.8f)), 0.03f, 0.03f, 5);
            }
            for (int i = 0; i < 18; i++)
                Debris(k, new Vector3(F(r, -6f, 6f), F(r, -2f, 6f), F(r, -24f, -17f)), r);

            // Engine section: radiator fins, a frame around the thrusters, two nacelles (one torn off short)
            for (float x = -3f; x <= 3.01f; x += 1.5f)
            {
                Quaternion bent = Quaternion.Euler(0f, 0f, r.NextDouble() < 0.3 ? F(r, -25f, 25f) : 0f);
                k.Plate.Box(new Vector3(x, 4.5f, -25f), new Vector3(0.06f, 2f, 4.6f), bent);
                k.Trim.Box(new Vector3(x, 5.5f, -25f), new Vector3(0.1f, 0.08f, 4.6f), bent);
            }
            k.Plate.Box(new Vector3(0f, 1.5f, -28.6f), new Vector3(9.4f, 0.25f, 0.2f));
            k.Plate.Box(new Vector3(0f, -0.4f, -28.6f), new Vector3(9.4f, 0.25f, 0.2f));
            k.Plate.Box(new Vector3(0f, 3.4f, -28.6f), new Vector3(9.4f, 0.25f, 0.2f));
            foreach (float s in new[] { -1f, 1f })
            {
                bool torn = s < 0f;
                Vector3 front = new Vector3(s * 6.4f, 1.5f, -20.5f);
                Vector3 back = torn ? new Vector3(s * 6.8f, 0.9f, -26.5f) : new Vector3(s * 6.4f, 1.5f, -30.5f);
                k.Hull.Cylinder(front, back, 1.5f, 1.6f, 20, true, !torn);
                k.Solid().Cylinder(front, back, 1.5f, 1.6f, 12);
                k.Plate.Cylinder(front - Vector3.forward * 0.6f, front, 0.9f, 1.5f, 20);
                Ring(k.Trim, Vector3.Lerp(front, back, 0.3f), back - front, 1.62f, 0.07f, 20, 5);
                Ring(k.Trim, Vector3.Lerp(front, back, 0.7f), back - front, 1.65f, 0.07f, 20, 5);
                Ring(k.Glow, Vector3.Lerp(front, back, 0.5f), back - front, 1.63f, 0.04f, 20, 5);
                // Pylon to the hull
                Hex(k, k.Plate,
                    new Vector3(s * 4.5f, 0.8f, -27.5f), new Vector3(s * 5.2f, 1.1f, -27.5f), new Vector3(s * 4.5f, 0.8f, -22f), new Vector3(s * 5.2f, 1.1f, -22f),
                    new Vector3(s * 4.5f, 2.2f, -27f), new Vector3(s * 5.2f, 1.9f, -27f), new Vector3(s * 4.5f, 2.2f, -22.5f), new Vector3(s * 5.2f, 1.9f, -22.5f));
                if (torn)
                {
                    for (int i = 0; i < 9; i++)
                    {
                        float t = i * Mathf.PI * 2f / 9f + F(r, -0.2f, 0.2f);
                        Vector3 o = new Vector3(Mathf.Cos(t), Mathf.Sin(t), 0f);
                        Shard(k.Rust, back + o * 1.55f - Vector3.Cross(o, Vector3.forward) * 0.25f, back + o * 1.55f + Vector3.Cross(o, Vector3.forward) * 0.25f,
                            back + o * F(r, 1.2f, 1.9f) + Vector3.back * F(r, 0.3f, 1f), o, 0.025f);
                    }
                    // Its nozzle drifted away
                    Nozzle(k, new Vector3(s * 8.5f, -1.5f, -34f), new Vector3(-0.3f, -0.4f, -0.86f).normalized, 1.3f, false, true, r);
                }
                else
                {
                    Nozzle(k, back, Vector3.back, 1.35f, true);
                }
            }
        }

        // ---- The Leviathan: a capital ship, bow toward +z

        private static void Leviathan(Kit k, System.Random r)
        {
            // Spine x ±3.5, y -0.5..3.5, z -30.5..30.5; bow ±2.5, y..2.5 to z 38.5; starboard hangar
            // x 3.5..10.5, z -8.5..6.5; port bay x -9.5..-3.5, z -18.5..-7.5; tower ±1.5 y 3.5..8.5
            // z -20.5..-16.5; bridge ±4.5 y 8.5..10.5 z -21.5..-15.5; engine block ±5.5 y -0.5..4.5 z -38.5..-30.5

            // Dagger bow over the forward module, to a ram
            Hex(k, k.Plate,
                new Vector3(-2.8f, -0.85f, 38.55f), new Vector3(2.8f, -0.85f, 38.55f), new Vector3(-0.25f, -0.55f, 50f), new Vector3(0.25f, -0.55f, 50f),
                new Vector3(-2.75f, 2.75f, 38.55f), new Vector3(2.75f, 2.75f, 38.55f), new Vector3(-0.15f, 0.25f, 50f), new Vector3(0.15f, 0.25f, 50f));
            k.Trim.Box(new Vector3(0f, 1.55f, 44.3f), new Vector3(0.14f, 0.1f, 11.6f), Quaternion.Euler(12.2f, 0f, 0f));
            foreach (float s in new[] { -1f, 1f })
            {
                // Cheek armour along the forward module and a row of lights to the ram
                for (float z = 31f; z < 38.5f; z += 1.9f)
                    k.Plate.Box(new Vector3(s * 2.58f, 1f, z + 0.85f), new Vector3(0.1f, 2.7f, 1.7f), Quaternion.Euler(0f, 0f, s * -5f));
                for (int i = 0; i < 5; i++)
                {
                    float z = 39.5f + i * 2.2f, t = (z - 38.55f) / 11.45f;
                    k.Red.Box(new Vector3(s * (2.85f - 2.6f * t), -0.2f - 0.3f * t, z), Vector3.one * 0.09f);
                }
            }

            // The blades: wide armoured flanges along the spine, cut around the bays
            foreach (float s in new[] { -1f, 1f })
            {
                float bayFrom = s > 0f ? -8.6f : -18.6f, bayTo = s > 0f ? 6.6f : -7.4f;
                foreach ((float z0, float z1) in new[] { (-30.3f, bayFrom), (bayTo, 32f) })
                {
                    float o0 = Outer(z0), o1 = Outer(z1);
                    Hex(k, k.Plate,
                        new Vector3(s * 3.52f, 0.95f, z0), new Vector3(s * o0, 1.1f, z0), new Vector3(s * 3.52f, 0.95f, z1), new Vector3(s * o1, 1.1f, z1),
                        new Vector3(s * 3.52f, 1.65f, z0), new Vector3(s * o0, 1.35f, z0), new Vector3(s * 3.52f, 1.65f, z1), new Vector3(s * o1, 1.35f, z1));
                    k.Trim.Cylinder(new Vector3(s * o0, 1.22f, z0), new Vector3(s * o1, 1.22f, z1), 0.09f, 0.09f, 6);
                    k.Glow.Box(new Vector3(s * (o0 + o1) * 0.5f, 1.36f, (z0 + z1) * 0.5f), new Vector3(0.06f, 0.03f, Mathf.Abs(z1 - z0) * 0.92f),
                        Quaternion.Euler(0f, Mathf.Atan2(s * (o1 - o0), z1 - z0) * Mathf.Rad2Deg, 0f));
                    // Armour seams across the top
                    for (float z = z0 + 1f; z < z1 - 0.5f; z += 2.2f)
                    {
                        float o = Outer(z);
                        k.Dark.Box(new Vector3(s * (3.52f + o) * 0.5f, 1.52f - 0.12f * 0.5f, z), new Vector3(o - 3.52f, 0.03f, 0.08f), Quaternion.Euler(0f, 0f, s * -3f));
                        if (r.NextDouble() < 0.35)
                            k.Plate.Box(new Vector3(s * F(r, 4.2f, o - 0.6f), 1.55f, z + 1.1f), new Vector3(F(r, 0.5f, 1.2f), 0.18f, F(r, 0.5f, 1.4f)));
                    }
                }
            }

            // Dorsal decks stepped along the spine (the beacon and its mast stand at z 0)
            Hex(k, k.Hull,
                new Vector3(-2.8f, 3.53f, 2f), new Vector3(2.8f, 3.53f, 2f), new Vector3(-2.8f, 3.53f, 27f), new Vector3(2.8f, 3.53f, 27f),
                new Vector3(-2.4f, 4.3f, 2.4f), new Vector3(2.4f, 4.3f, 2.4f), new Vector3(-2.2f, 4.3f, 25.5f), new Vector3(2.2f, 4.3f, 25.5f));
            Hex(k, k.Plate,
                new Vector3(-1.8f, 4.3f, 6f), new Vector3(1.8f, 4.3f, 6f), new Vector3(-1.8f, 4.3f, 21f), new Vector3(1.8f, 4.3f, 21f),
                new Vector3(-1.5f, 4.9f, 6.4f), new Vector3(1.5f, 4.9f, 6.4f), new Vector3(-1.4f, 4.9f, 20f), new Vector3(1.4f, 4.9f, 20f));
            for (int i = 0; i < 40; i++)
            {
                Vector3 at = new Vector3(F(r, -2.2f, 2.2f), 4.3f, F(r, 2.6f, 25f));
                if (Mathf.Abs(at.x) < 1.9f && at.z > 5.8f && at.z < 21.2f)
                    at.y = 4.9f;
                Vector3 size = new Vector3(F(r, 0.2f, 0.8f), F(r, 0.1f, 0.4f), F(r, 0.2f, 1f));
                (r.NextDouble() < 0.5 ? k.Dark : k.Plate).Box(at + Vector3.up * size.y * 0.5f, size);
            }
            for (float z = -28f; z < -2f; z += 2.5f)
            {
                if (z > -22f && z < -15f)
                    continue;
                k.Plate.Box(new Vector3(0f, 3.62f, z), new Vector3(F(r, 2f, 5f), 0.22f, F(r, 0.8f, 1.8f)));
            }

            // Dead turrets on the decks and blades, some broken
            Turret(k, new Vector3(0f, 4.9f, 9f), 0f, false, r);
            Turret(k, new Vector3(0f, 4.9f, 17f), 20f, true, r);
            Turret(k, new Vector3(0f, 3.75f, -11f), 170f, false, r);
            Turret(k, new Vector3(0f, 3.75f, -26f), 180f, true, r);
            foreach (float s in new[] { -1f, 1f })
            {
                Turret(k, new Vector3(s * 5.6f, BladeTop(5.6f, 18f), 18f), s * 40f, s < 0f, r);
                Turret(k, new Vector3(s * 7.2f, BladeTop(7.2f, -22f), -22f), s * 70f, false, r);
            }

            // Spine: flank ribs, running lights (most dead), a keel
            foreach (float s in new[] { -1f, 1f })
            {
                for (float z = -29f; z < 30f; z += 4f)
                {
                    bool bay = s > 0f ? z > -9f && z < 7f : z > -19f && z < -7f;
                    if (bay)
                        continue;
                    k.Plate.Box(new Vector3(s * 3.6f, 0.2f, z), new Vector3(0.2f, 1.2f, 0.45f));
                    (r.NextDouble() < 0.3 ? k.Red : k.Dark).Box(new Vector3(s * 3.64f, -0.25f, z + 2f), new Vector3(0.06f, 0.08f, 0.14f));
                }
            }
            Hex(k, k.Plate,
                new Vector3(-0.8f, -1.6f, -30f), new Vector3(0.8f, -1.6f, -30f), new Vector3(-0.6f, -1.3f, 31f), new Vector3(0.6f, -1.3f, 31f),
                new Vector3(-3f, -0.6f, -30.5f), new Vector3(3f, -0.6f, -30.5f), new Vector3(-2.4f, -0.6f, 33f), new Vector3(2.4f, -0.6f, 33f));

            // Tower and bridge: sloped skirt, bridge wings, a mast, sensor globes (one smashed)
            foreach (float s in new[] { -1f, 1f })
            {
                Hex(k, k.Plate,
                    new Vector3(s * 1.55f, 3.52f, -21f), new Vector3(s * 3.2f, 3.52f, -21f), new Vector3(s * 1.55f, 3.52f, -16f), new Vector3(s * 3.2f, 3.52f, -16f),
                    new Vector3(s * 1.55f, 7f, -20.6f), new Vector3(s * 1.7f, 7f, -20.6f), new Vector3(s * 1.55f, 7f, -16.4f), new Vector3(s * 1.7f, 7f, -16.4f));
                Hex(k, k.Plate,
                    new Vector3(s * 4.52f, 9.3f, -21.2f), new Vector3(s * 7.5f, 9.5f, -20f), new Vector3(s * 4.52f, 9.3f, -15.8f), new Vector3(s * 7.5f, 9.5f, -17f),
                    new Vector3(s * 4.52f, 10.1f, -21.2f), new Vector3(s * 7.5f, 9.8f, -20f), new Vector3(s * 4.52f, 10.1f, -15.8f), new Vector3(s * 7.5f, 9.8f, -17f));
                k.Red.Box(new Vector3(s * 7.55f, 9.7f, -18.5f), Vector3.one * 0.12f);
            }
            Hex(k, k.Plate,
                new Vector3(-4.7f, 10.52f, -21.6f), new Vector3(4.7f, 10.52f, -21.6f), new Vector3(-4.4f, 10.52f, -15.2f), new Vector3(4.4f, 10.52f, -15.2f),
                new Vector3(-4.3f, 10.9f, -21.4f), new Vector3(4.3f, 10.9f, -21.4f), new Vector3(-4f, 10.65f, -15.4f), new Vector3(4f, 10.65f, -15.4f));
            Truss(k.Trim, new Vector3(0f, 10.9f, -20f), new Vector3(0f, 16.5f, -20f), 0.45f, 0.04f);
            k.Trim.Box(new Vector3(0f, 15f, -20f), new Vector3(3.2f, 0.08f, 0.08f));
            k.Trim.Box(new Vector3(0f, 13.4f, -20f), new Vector3(2.2f, 0.08f, 0.08f));
            k.Red.Box(new Vector3(0f, 16.6f, -20f), Vector3.one * 0.14f);
            k.Trim.Cylinder(new Vector3(-2.6f, 10.9f, -18f), new Vector3(-2.6f, 11.8f, -18f), 0.08f, 0.08f, 8);
            Sphere(k.Hull, new Vector3(-2.6f, 12.5f, -18f), 0.8f);
            k.Trim.Cylinder(new Vector3(2.6f, 10.9f, -18f), new Vector3(2.6f, 11.8f, -18f), 0.08f, 0.08f, 8);
            for (int i = 0; i < 4; i++)
            {
                float t = i * Mathf.PI * 0.5f + 0.3f;
                Vector3 o = new Vector3(Mathf.Cos(t), 0.4f, Mathf.Sin(t)).normalized;
                Vector3 c = new Vector3(2.6f, 12.1f, -18f);
                Shard(k.Hull, c + o * 0.6f - Vector3.Cross(o, Vector3.up).normalized * 0.3f, c + o * 0.6f + Vector3.Cross(o, Vector3.up).normalized * 0.3f, c + o * 0.8f + Vector3.up * F(r, 0.3f, 0.7f), o, 0.03f);
            }

            // Starboard hangar mouth: a heavy frame with hazard stripes and dock lights
            k.Plate.Box(new Vector3(10.65f, 3.35f, -1f), new Vector3(0.3f, 0.35f, 15.4f));
            k.Plate.Box(new Vector3(10.65f, -0.35f, -1f), new Vector3(0.3f, 0.35f, 15.4f));
            k.Plate.Box(new Vector3(10.65f, 1.5f, -8.35f), new Vector3(0.3f, 4f, 0.35f));
            k.Plate.Box(new Vector3(10.65f, 1.5f, 6.35f), new Vector3(0.3f, 4f, 0.35f));
            for (float z = -8f; z < 6f; z += 0.8f)
                (Mathf.RoundToInt(z / 0.8f) % 2 == 0 ? k.Hazard : k.Dark).Box(new Vector3(10.82f, -0.35f, z + 0.4f), new Vector3(0.04f, 0.3f, 0.8f));
            foreach (float z in new[] { -7.5f, -4.5f, 3.5f, 5.5f })
                k.Glow.Box(new Vector3(10.84f, 3.35f, z), new Vector3(0.04f, 0.12f, 0.4f));
            k.Hull.Box(new Vector3(7f, 3.62f, -1f), new Vector3(7.2f, 0.22f, 15.2f));
            k.Solid().Box(new Vector3(7f, 3.62f, -1f), new Vector3(7.2f, 0.22f, 15.2f));

            // Port bay: collapsed roof, twisted girders, debris
            for (int i = 0; i < 9; i++)
            {
                Vector3 a = new Vector3(F(r, -9.2f, -5.8f), 3.4f, F(r, -18f, -14f));
                Vector3 d = new Vector3(F(r, -0.6f, 0.6f), F(r, 0.2f, 1f), F(r, -0.6f, 0.6f)).normalized;
                k.Trim.Box(a + d * 0.9f, new Vector3(0.14f, 0.14f, 1.8f), Quaternion.LookRotation(d, Vector3.up));
            }
            for (int i = 0; i < 26; i++)
                Debris(k, new Vector3(F(r, -14f, -5f), F(r, 2f, 9f), F(r, -24f, -10f)), r);
            for (int i = 0; i < 12; i++)
                Debris(k, new Vector3(F(r, 4f, 9f), F(r, 0f, 5f), F(r, 12f, 21f)), r);
            // Decks seen through the starboard tear
            for (float y = 1.5f; y <= 2.6f; y += 1f)
                k.Trim.Box(new Vector3(3.45f, y, 16.5f), new Vector3(0.1f, 0.1f, F(r, 2.5f, 5f)));

            // The engine block: a bank of six nozzles, two still faintly glowing, boosters, radiators
            foreach (float x in new[] { -3.3f, 0f, 3.3f })
                foreach (float y in new[] { 0.9f, 3.1f })
                    Nozzle(k, new Vector3(x, y, -38.5f), Vector3.back, 1.0f, (x == 0f && y > 2f) || (x > 0f && y < 2f), x < 0f && y > 2f, r);
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 front = new Vector3(s * 6.6f, 1.5f, -27f), back = new Vector3(s * 6.6f, 1.5f, -38.6f);
                k.Hull.Cylinder(front, back, 1.05f, 1.1f, 18);
                k.Solid().Cylinder(front + Vector3.forward * 1.2f, back, 1.05f, 1.1f, 12);
                k.Plate.Cylinder(front + Vector3.forward * 1.2f, front, 0.2f, 1.05f, 18);
                Ring(k.Trim, Vector3.Lerp(front, back, 0.35f), Vector3.back, 1.12f, 0.06f, 18, 5);
                Ring(k.Trim, Vector3.Lerp(front, back, 0.75f), Vector3.back, 1.14f, 0.06f, 18, 5);
                Nozzle(k, back, Vector3.back, 0.85f, false);
                // Radiator wing (the port one bent down)
                Quaternion bend = Quaternion.Euler(0f, 0f, s < 0f ? 28f : 0f);
                Vector3 root = new Vector3(s * 5.5f, 4.2f, -34.5f);
                for (int i = 0; i < 4; i++)
                {
                    Vector3 c = root + bend * new Vector3(s * (1.2f + i * 1.35f), 0f, 0f);
                    k.Plate.Box(c, new Vector3(1.25f, 0.05f, 4.6f), bend);
                    k.Trim.Box(c + bend * new Vector3(0f, 0.04f, 0f), new Vector3(0.06f, 0.04f, 4.6f), bend);
                }
                k.Trim.Box(root + bend * new Vector3(s * 3.2f, 0f, 2.35f), new Vector3(5.6f, 0.08f, 0.1f), bend);
                k.Trim.Box(root + bend * new Vector3(s * 3.2f, 0f, -2.35f), new Vector3(5.6f, 0.08f, 0.1f), bend);
            }
            k.Plate.Box(new Vector3(0f, 2f, -38.65f), new Vector3(11.2f, 0.25f, 0.25f));
        }

        // The Leviathan's blade edge, from wide at the stern to the bow
        private static float Outer(float z) => 9.5f - (z + 30.3f) * (5.9f / 62.3f);

        // The height of a blade's top at x (its distance from the axis) and z
        private static float BladeTop(float x, float z) => 1.65f - 0.3f * (x - 3.52f) / (Outer(z) - 3.52f);

        // ---- The Hades probe: a research probe near the black hole, bow toward +z

        private static void Hades(Kit k, System.Random r)
        {
            // Core x ±3.5 y -0.5..3.5 z ±3.5; deck ±2.5 y..2.5 z 3.5..9.5; arms y -0.5..1.5 z ±1.5 to
            // the labs x ±(8.5..13.5) y..3.5 z ±3.5; spire ±1.5 y 3.5..9.5; observatory ±3.5 y 9.5..11.5

            // Gravimetric ring around the probe, its emitters, spokes to the labs, deck and stern
            Vector3 hub = new Vector3(0f, 1.5f, 0f);
            const float radius = 17f;
            Ring(k.Hull, hub, Vector3.up, radius, 0.5f, 72, 10);
            SolidRing(k, hub, Vector3.up, radius, 0.5f, 36);
            Ring(k.Glow, hub + Vector3.up * 0.02f, Vector3.up, radius - 0.5f, 0.12f, 72, 6);
            Ring(k.Trim, hub + Vector3.up * 0.52f, Vector3.up, radius, 0.08f, 72, 5);
            Ring(k.Trim, hub - Vector3.up * 0.52f, Vector3.up, radius, 0.08f, 72, 5);
            for (int i = 0; i < 12; i++)
            {
                float t = i * Mathf.PI / 6f;
                Vector3 o = new Vector3(Mathf.Cos(t), 0f, Mathf.Sin(t));
                Vector3 at = hub + o * radius;
                k.Plate.Box(at, new Vector3(1.3f, 1.3f, 1.3f), Quaternion.LookRotation(o, Vector3.up));
                k.Solid().Box(at, new Vector3(1.3f, 1.3f, 1.3f), Quaternion.LookRotation(o, Vector3.up));
                k.Dark.Cylinder(at + Vector3.up * 0.65f, at + Vector3.up * 1.4f, 0.25f, 0.12f, 10);
                k.Red.Cylinder(at + Vector3.up * 1.4f, at + Vector3.up * 1.55f, 0.13f, 0.13f, 10);
                k.Glow.Box(at - o * 0.66f, new Vector3(0.7f, 0.25f, 0.04f), Quaternion.LookRotation(o, Vector3.up));
            }
            Truss(k.Trim, new Vector3(13.6f, 1.5f, 0f), new Vector3(16.4f, 1.5f, 0f), 0.6f);
            Truss(k.Trim, new Vector3(-13.6f, 1.5f, 0f), new Vector3(-16.4f, 1.5f, 0f), 0.6f);
            Truss(k.Trim, new Vector3(0f, 1f, 9.6f), new Vector3(0f, 1.5f, 16.4f), 0.6f);
            Truss(k.Trim, new Vector3(2.5f, 1.5f, -3.6f), new Vector3(9f, 1.5f, -14.2f), 0.6f);
            Truss(k.Trim, new Vector3(-2.5f, 1.5f, -3.6f), new Vector3(-9f, 1.5f, -14.2f), 0.6f);

            // The big dish over the observatory, on its mount
            k.Plate.Cylinder(new Vector3(0f, 11.5f, 0f), new Vector3(0f, 12.2f, 0f), 1.2f, 0.9f, 18);
            k.Solid().Cylinder(new Vector3(0f, 11.5f, 0f), new Vector3(0f, 13.6f, -0.3f), 1.2f, 0.3f, 10);
            k.Trim.Cylinder(new Vector3(0f, 12.2f, 0f), new Vector3(0f, 13.6f, -0.3f), 0.3f, 0.25f, 10);
            Dish(k, new Vector3(0f, 13.8f, -0.2f), new Vector3(0f, 0.45f, 0.89f).normalized, 4.6f);

            // Sensor boom from the deck's bow: a lattice with instrument pods, the exotic matter core at its tip
            Truss(k.Trim, new Vector3(0f, 1f, 9.6f), new Vector3(0f, 1f, 25f), 0.9f, 0.05f);
            foreach (float z in new[] { 13f, 17.5f, 21.5f })
            {
                k.Plate.Cylinder(new Vector3(0f, 1f, z - 0.8f), new Vector3(0f, 1f, z + 0.8f), 0.55f, 0.55f, 14);
                k.Solid().Cylinder(new Vector3(0f, 1f, z - 0.8f), new Vector3(0f, 1f, z + 0.8f), 0.55f, 0.55f, 10);
                Ring(k.Glow, new Vector3(0f, 1f, z), Vector3.forward, 0.57f, 0.05f, 14, 5);
                k.Dark.Cylinder(new Vector3(0.55f, 1f, z), new Vector3(1.6f, 1.6f, z), 0.04f, 0.02f, 5);
            }
            Vector3 tip = new Vector3(0f, 1f, 27f);
            k.Plate.Cylinder(new Vector3(0f, 1f, 25f), tip - Vector3.forward * 0.9f, 0.4f, 0.75f, 14);
            Sphere(k.Red, tip, 0.7f);
            k.Solid().Cylinder(new Vector3(0f, 1f, 25f), tip + Vector3.forward * 0.95f, 0.95f, 0.95f, 10);
            for (int i = 0; i < 3; i++)
                Ring(k.Trim, tip, Quaternion.Euler(0f, i * 60f, 0f) * Vector3.right, 0.95f, 0.05f, 24, 5);
            Ring(k.Trim, tip, Vector3.up, 0.95f, 0.05f, 24, 5);

            // Solar wings beyond the labs, two panels a side on a truss
            foreach (float s in new[] { -1f, 1f })
            {
                Truss(k.Trim, new Vector3(s * 13.6f, 3f, 0f), new Vector3(s * 27f, 3f, 0f), 0.4f, 0.04f);
                foreach (float z in new[] { -2.6f, 2.6f })
                {
                    Vector3 c = new Vector3(s * 20.8f, 3f, z);
                    Quaternion tilt = Quaternion.Euler(0f, 0f, s * 12f);
                    k.Solar.Box(c, new Vector3(12f, 0.04f, 4.2f), tilt);
                    k.Solid().Box(c, new Vector3(12f, 0.1f, 4.2f), tilt);
                    for (float x = -5.4f; x <= 5.5f; x += 1.2f)
                        k.Trim.Box(c + tilt * new Vector3(x, 0.03f, 0f), new Vector3(0.04f, 0.02f, 4.2f), tilt);
                    k.Trim.Box(c + tilt * new Vector3(0f, 0.03f, 0f), new Vector3(12f, 0.02f, 0.05f), tilt);
                    k.Trim.Box(c + tilt * new Vector3(0f, 0f, z > 0f ? -2.1f : 2.1f), new Vector3(12f, 0.07f, 0.08f), tilt);
                }
                // Radiator fins on the lab roof, an antenna and a hazard band
                for (float z = -2f; z <= 2.01f; z += 2f)
                {
                    k.Plate.Box(new Vector3(s * 11f, 4.42f, z), new Vector3(4.4f, 1.8f, 0.06f));
                    k.Trim.Box(new Vector3(s * 11f, 5.3f, z), new Vector3(4.4f, 0.06f, 0.1f));
                }
                k.Dark.Cylinder(new Vector3(s * 12.8f, 3.5f, 2.8f), new Vector3(s * 12.8f, 7.2f, 2.8f), 0.04f, 0.015f, 6);
                k.Red.Box(new Vector3(s * 12.8f, 7.25f, 2.8f), Vector3.one * 0.1f);
                for (float z = -3.2f; z < 3.3f; z += 0.8f)
                    (Mathf.RoundToInt(z / 0.8f) % 2 == 0 ? k.Hazard : k.Dark).Box(new Vector3(s * 13.58f, -0.2f, z + 0.4f), new Vector3(0.04f, 0.25f, 0.8f));
            }

            // Exotic matter containment on the core's roof corners
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float sz in new[] { -1f, 1f })
                {
                    Vector3 b = new Vector3(sx * 2.1f, 3.5f, sz * 2.1f);
                    k.Plate.Cylinder(b, b + Vector3.up * 1.6f, 0.36f, 0.36f, 12);
                    k.Solid().Cylinder(b, b + Vector3.up * 1.75f, 0.38f, 0.38f, 8);
                    k.Glow.Cylinder(b + Vector3.up * 0.5f, b + Vector3.up * 1.1f, 0.38f, 0.38f, 12);
                    k.Trim.Cylinder(b + Vector3.up * 1.6f, b + Vector3.up * 1.75f, 0.25f, 0.2f, 10);
                }
            }

            // Spire: stacked collars and a lit band, hazard frame around the docking door
            for (float y = 4.5f; y < 9.5f; y += 1.5f)
            {
                foreach (float s in new[] { -1f, 1f })
                {
                    k.Plate.Box(new Vector3(s * 1.6f, y, 0f), new Vector3(0.2f, 0.25f, 3.4f));
                    k.Plate.Box(new Vector3(0f, y, s * 1.6f), new Vector3(3.0f, 0.25f, 0.2f));
                }
            }
            foreach (float s in new[] { -1f, 1f })
                k.Glow.Box(new Vector3(s * 1.58f, 7.5f, 0f), new Vector3(0.03f, 0.1f, 2.6f));
            for (float x = -2.5f; x < 1.6f; x += 0.5f)
                (Mathf.RoundToInt(x / 0.5f) % 2 == 0 ? k.Hazard : k.Dark).Box(new Vector3(x + 0.25f, 1.65f, -3.58f), new Vector3(0.5f, 0.2f, 0.04f));
            // Observation deck: its bow frame
            k.Plate.Box(new Vector3(0f, 2.62f, 6.5f), new Vector3(5.4f, 0.22f, 6.2f));
            k.Solid().Box(new Vector3(0f, 2.62f, 6.5f), new Vector3(5.4f, 0.22f, 6.2f));
            k.Trim.Box(new Vector3(0f, 1f, 9.58f), new Vector3(5.2f, 0.08f, 0.06f));
        }
    }
}

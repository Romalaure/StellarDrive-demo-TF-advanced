using System.Collections.Generic;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Parts.Common;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;
using UnityEngine.Rendering;

namespace StellarDriveDemoTFAdvanced.Power
{
    /// <summary>
    /// The dark matter collector's model: an armored base with vents, a ribbed mast, a stator
    /// ring on spokes, a rotor of three vanes, a meteorite dish lined with glowing rings and, over
    /// it, a dark singularity held in a gimbal. The moving pieces are children named here, turned
    /// by <see cref="DarkCollectorVisuals"/>.
    /// </summary>
    internal static class CollectorModel
    {
        public const string Rotor = "TF_NoPaint_CollectorRotor", Gimbal = "TF_NoPaint_CollectorGimbal", Singularity = "TF_NoPaint_CollectorCore",
            Vortex = "TF_NoPaint_CollectorVortex";
        public const float Base = 0.42f, RotorY = 0.86f, DishLow = 0.98f, DishHigh = 1.38f, CoreY = 1.34f;

        public static void Build(Transform visuals, float width, float depth)
        {
            var m = new MachineModels.Model();
            // Base: the machines' housing, vents and grille in front, a light strip round the plinth
            MachineModels.Housing(m, width, Base, depth, 0.7f);
            float face = depth * 0.47f;
            MachineModels.Vent(m, -0.27f, 0.2f, face, 0.2f, 0.2f, 3);
            MachineModels.Vent(m, 0.27f, 0.2f, face, 0.2f, 0.2f, 3);
            MachineModels.Grille(m, 0.2f, face, 0.1f, 0.22f);
            m.Glow.Box(new Vector3(0f, 0.09f, face + 0.01f), new Vector3(width * 0.8f, 0.01f, 0.006f));
            // Mast: a collared, ribbed column with two glowing conduits
            m.Armor.Cylinder(new Vector3(0f, Base - 0.02f, 0f), new Vector3(0f, Base + 0.05f, 0f), 0.2f, 0.16f, 12);
            m.Armor.Cylinder(new Vector3(0f, Base + 0.05f, 0f), new Vector3(0f, DishLow, 0f), 0.085f, 0.07f, 12);
            for (int i = 0; i < 4; i++)
            {
                float y = Base + 0.12f + i * 0.08f;
                m.Dark.Cylinder(new Vector3(0f, y, 0f), new Vector3(0f, y + 0.02f, 0f), 0.095f, 0.09f, 12, capA: false, capB: false);
            }
            foreach (float x in new[] { -1f, 1f })
                m.Glow.Box(new Vector3(x * 0.082f, (Base + DishLow) * 0.5f, 0f), new Vector3(0.01f, DishLow - Base - 0.1f, 0.025f));
            // Stator: a ring on six spokes around the rotor
            const float statorR = 0.44f, statorY = RotorY - 0.08f;
            m.Armor.Cylinder(new Vector3(0f, statorY - 0.02f, 0f), new Vector3(0f, statorY + 0.02f, 0f), statorR, statorR, 32, capA: false, capB: false);
            m.Armor.Cylinder(new Vector3(0f, statorY - 0.02f, 0f), new Vector3(0f, statorY + 0.02f, 0f), statorR - 0.035f, statorR - 0.035f, 32, capA: false, capB: false);
            m.Glow.Cylinder(new Vector3(0f, statorY + 0.02f, 0f), new Vector3(0f, statorY + 0.026f, 0f), statorR - 0.017f, statorR - 0.017f, 32, capA: false, capB: false);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                m.Armor.Box(new Vector3(0f, statorY, 0f) + dir * (statorR + 0.07f) * 0.5f, new Vector3(0.025f, 0.03f, statorR - 0.06f), Quaternion.LookRotation(dir));
                m.Light.Box(new Vector3(0f, statorY, 0f) + dir * (statorR + 0.004f), new Vector3(0.03f, 0.05f, 0.008f), Quaternion.LookRotation(dir));
            }
            // Dish: meteorite outside, dark inside, glowing rings up its throat, a lit lip
            const int bands = 5;
            float DishR(float t) => 0.1f + 0.4f * Mathf.Sqrt(t);
            for (int i = 0; i < bands; i++)
            {
                float t0 = i / (float)bands, t1 = (i + 1) / (float)bands;
                float y0 = Mathf.Lerp(DishLow, DishHigh, t0), y1 = Mathf.Lerp(DishLow, DishHigh, t1);
                m.Armor.Cylinder(new Vector3(0f, y0, 0f), new Vector3(0f, y1, 0f), DishR(t0) + 0.02f, DishR(t1) + 0.02f, 32, capA: i == 0, capB: false);
                m.Dark.Cylinder(new Vector3(0f, y0 + 0.004f, 0f), new Vector3(0f, y1 + 0.004f, 0f), DishR(t0), DishR(t1), 32, capA: i == 0, capB: false);
            }
            foreach (float t in new[] { 0.3f, 0.6f, 0.85f })
            {
                float y = Mathf.Lerp(DishLow, DishHigh, t) + 0.006f;
                m.Glow.Cylinder(new Vector3(0f, y - 0.006f, 0f), new Vector3(0f, y + 0.006f, 0f), DishR(t) - 0.004f, DishR(t) - 0.002f, 32, capA: false, capB: false);
            }
            m.Light.Cylinder(new Vector3(0f, DishHigh, 0f), new Vector3(0f, DishHigh + 0.012f, 0f), DishR(1f) + 0.02f, DishR(1f) + 0.025f, 32, capA: false, capB: false);
            // Three struts from the stator up to the dish's rim, holding the gimbal's arms
            for (int i = 0; i < 3; i++)
            {
                float a = (90f + i * 120f) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 low = new Vector3(0f, statorY, 0f) + dir * statorR, high = new Vector3(0f, DishHigh + 0.03f, 0f) + dir * (DishR(1f) + 0.05f);
                m.Armor.Cylinder(low, high, 0.022f, 0.018f, 8);
                Vector3 tip = new Vector3(0f, DishHigh + 0.08f, 0f) + dir * (DishR(1f) - 0.02f);
                m.Armor.Cylinder(high, tip, 0.016f, 0.012f, 8);
                m.Core.Cylinder(tip, tip + (tip - high).normalized * 0.04f, 0.014f, 0f, 6, capA: true, capB: false);
            }
            MachineModels.Attach(visuals, m, "DarkCollector", MachineModels.Violet, MachineModels.Violet);

            // Rotor: three curved vanes on a hub, turning inside the stator
            var rotor = new MachineModels.Model();
            rotor.Armor.Cylinder(new Vector3(0f, -0.03f, 0f), new Vector3(0f, 0.03f, 0f), 0.13f, 0.13f, 16);
            rotor.Glow.Cylinder(new Vector3(0f, 0.03f, 0f), new Vector3(0f, 0.034f, 0f), 0.1f, 0.1f, 16, capA: false, capB: true);
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120f;
                for (int k = 0; k < 3; k++)
                {
                    // Each vane in three pieces, bending back as it goes out
                    float r0 = 0.13f + k * 0.085f, bend = a + k * 14f;
                    Quaternion turn = Quaternion.Euler(0f, bend, 0f);
                    Vector3 centre = turn * new Vector3(r0 + 0.045f, 0f, 0f);
                    rotor.Armor.Box(centre, new Vector3(0.095f, 0.025f, 0.06f), turn * Quaternion.Euler(18f, 0f, 0f));
                    rotor.Glow.Box(centre + turn * new Vector3(0f, 0.014f, 0.03f), new Vector3(0.085f, 0.004f, 0.006f), turn * Quaternion.Euler(18f, 0f, 0f));
                }
            }
            AttachMoving(visuals, rotor, Rotor, new Vector3(0f, RotorY, 0f));

            // The singularity: a dark core with a glowing shell, and the gimbal ring around it
            var core = new MachineModels.Model();
            core.Dark.Cylinder(new Vector3(0f, -0.09f, 0f), Vector3.zero, 0.01f, 0.09f, 12, capA: true, capB: false);
            core.Dark.Cylinder(Vector3.zero, new Vector3(0f, 0.09f, 0f), 0.09f, 0.01f, 12, capA: false, capB: true);
            core.Core.Cylinder(new Vector3(0f, -0.005f, 0f), new Vector3(0f, 0.005f, 0f), 0.11f, 0.11f, 24, capA: false, capB: false);
            AttachMoving(visuals, core, Singularity, new Vector3(0f, CoreY, 0f));
            var gimbal = new MachineModels.Model();
            for (int i = 0; i < 24; i++)
            {
                float a0 = i * 15f * Mathf.Deg2Rad, a1 = (i + 1) * 15f * Mathf.Deg2Rad;
                gimbal.Armor.Cylinder(new Vector3(Mathf.Cos(a0) * 0.19f, Mathf.Sin(a0) * 0.19f, 0f), new Vector3(Mathf.Cos(a1) * 0.19f, Mathf.Sin(a1) * 0.19f, 0f), 0.009f, 0.008f, 6, capA: false, capB: false);
                if (i % 3 == 0)
                    gimbal.Glow.Box(new Vector3(Mathf.Cos(a0) * 0.19f, Mathf.Sin(a0) * 0.19f, 0f), new Vector3(0.02f, 0.02f, 0.02f), Quaternion.Euler(0f, 0f, i * 15f));
            }
            AttachMoving(visuals, gimbal, Gimbal, new Vector3(0f, CoreY, 0f));

            // The inflow: spiral streaks from the dish's rim down to the core (see-through, added light)
            var vortex = new GameObject(Vortex) { layer = visuals.gameObject.layer };
            vortex.transform.SetParent(visuals, false);
            vortex.transform.localPosition = new Vector3(0f, CoreY, 0f);
            vortex.AddComponent<MeshFilter>().sharedMesh = VortexMesh();
            var renderer = vortex.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = TransparentMaterials.Additive("TFA_CollectorVortex", null);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            vortex.SetActive(false);
        }

        private static void AttachMoving(Transform visuals, MachineModels.Model model, string name, Vector3 at)
        {
            var holder = new GameObject(name) { layer = visuals.gameObject.layer };
            holder.transform.SetParent(visuals, false);
            holder.transform.localPosition = at;
            MachineModels.Attach(holder.transform, model, name.Substring("TF_NoPaint_".Length), MachineModels.Violet, MachineModels.Violet);
        }

        private static Mesh _vortex;

        // Six streaks spiralling in from the rim (above the core) to the core, bright at the core
        private static Mesh VortexMesh()
        {
            if (_vortex != null)
                return _vortex;
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            const int streaks = 6, steps = 16;
            for (int s = 0; s < streaks; s++)
            {
                for (int i = 0; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    float a = s * Mathf.PI * 2f / streaks + t * Mathf.PI * 1.4f;
                    float r = Mathf.Lerp(0.46f, 0.1f, t);
                    float y = Mathf.Lerp(0.1f, 0f, t * t);
                    var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    float w = Mathf.Lerp(0.03f, 0.008f, t);
                    vertices.Add(dir * (r - w) + Vector3.up * y);
                    vertices.Add(dir * (r + w) + Vector3.up * y);
                    Color c = new Color(0.75f, 0.3f, 1f) * (0.15f + 0.85f * t) * Mathf.Sin(t * Mathf.PI * 0.95f + 0.1f);
                    colors.Add(c);
                    colors.Add(c);
                    if (i > 0)
                    {
                        int b = vertices.Count - 4;
                        triangles.AddRange(new[] { b, b + 1, b + 3, b, b + 3, b + 2 });
                    }
                }
            }
            _vortex = new Mesh { name = "TFA_CollectorVortex" };
            _vortex.SetVertices(vertices);
            _vortex.SetColors(colors);
            _vortex.SetTriangles(triangles, 0);
            _vortex.RecalculateBounds();
            return _vortex;
        }
    }

    /// <summary>
    /// The collector at work (its state is its output, above 0 while it collects): the rotor
    /// spins up, the gimbal turns round the singularity, which throbs, the inflow streaks swirl
    /// into it and every glow brightens; when it stops all of it winds down and dims. Only moves
    /// transforms and sets colors, and only within 60 m of the camera.
    /// </summary>
    internal sealed class DarkCollectorVisuals : DefaultShipPartVisuals
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private Transform _rotor, _gimbal, _core, _vortex;
        private readonly List<Renderer> _glows = new List<Renderer>();
        private MaterialPropertyBlock _block;
        private bool _working, _logged;
        private float _drive, _shown = -1f, _spinRotor, _spinGimbal, _spinVortex;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _rotor = transform.Find(CollectorModel.Rotor);
            _gimbal = transform.Find(CollectorModel.Gimbal);
            _core = transform.Find(CollectorModel.Singularity);
            _vortex = transform.Find(CollectorModel.Vortex);
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.name.EndsWith("Glow") || renderer.name.EndsWith("Core"))
                    _glows.Add(renderer);
            }
        }

        public override void SetNetworkState(IStatefulPartState state)
        {
            if (state is ConstantState constant)
                _working = constant.ConstantValue > 0;
        }

        public override void ReactToNetworkStateChange(StatefulPartStateChangeEvent change) => SetNetworkState(change.NewState);

        private void Update()
        {
            Camera camera = MainCamera.Get();
            if (camera == null || (camera.transform.position - transform.position).sqrMagnitude > 60f * 60f)
                return;
            float dt = Time.deltaTime;
            // Spins up over about 2 s, winds down over about 5 s
            _drive = Mathf.MoveTowards(_drive, _working ? 1f : 0f, dt * (_working ? 0.5f : 0.2f));
            float d = _drive * _drive * (3f - 2f * _drive);
            if (DevHarness.Active && !_logged && _drive >= 1f)
            {
                _logged = true;
                AdvancedMod.Log.Msg($"dev: dark matter collector at full drive: rotor {_rotor != null}, gimbal {_gimbal != null}, core {_core != null}, vortex {_vortex != null && _vortex.gameObject.activeSelf}, {_glows.Count} glows");
            }
            float t = Time.time;
            if (_rotor != null)
            {
                _spinRotor += dt * (6f + 260f * d);
                _rotor.localRotation = Quaternion.Euler(0f, _spinRotor, 0f);
            }
            if (_gimbal != null)
            {
                _spinGimbal += dt * (4f + 120f * d);
                _gimbal.localRotation = Quaternion.Euler(25f, _spinGimbal, 0f) * Quaternion.Euler(0f, 0f, _spinGimbal * 1.7f);
            }
            if (_core != null)
            {
                _core.localPosition = new Vector3(0f, CollectorModel.CoreY + 0.02f * Mathf.Sin(t * 1.3f), 0f);
                _core.localRotation = Quaternion.Euler(0f, t * (20f + 200f * d), 0f);
                _core.localScale = Vector3.one * (1f + 0.15f * d * Mathf.Sin(t * 9f));
            }
            if (_vortex != null)
            {
                bool show = d > 0.05f;
                if (_vortex.gameObject.activeSelf != show)
                    _vortex.gameObject.SetActive(show);
                if (show)
                {
                    _spinVortex += dt * 160f * d;
                    _vortex.localRotation = Quaternion.Euler(0f, -_spinVortex, 0f);
                    _vortex.localScale = new Vector3(1f, 0.6f + 0.4f * d, 1f);
                }
            }
            // Glow: dim at rest, bright violet while collecting, a soft beat
            float glow = 0.25f + 0.75f * d * (0.85f + 0.15f * Mathf.Sin(t * 4f));
            float shown = Mathf.Round(glow * 50f);
            if (shown == _shown)
                return;
            _shown = shown;
            Color color = Color.Lerp(new Color(0.1f, 0.05f, 0.16f), MachineModels.Violet, glow);
            foreach (Renderer renderer in _glows)
            {
                if (renderer == null)
                    continue;
                renderer.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, color);
                _block.SetColor(EmissionColorId, color * (0.6f + 2.6f * glow));
                renderer.SetPropertyBlock(_block);
            }
        }
    }
}

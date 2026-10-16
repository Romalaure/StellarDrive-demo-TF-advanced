using System.Collections.Generic;
using System.Linq;
using System.Text;
using Audio.Interface;
using Core.Services;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Interface.Settings;
using Ships.Parts.Common;
using Ships.Parts.Common.Model;
using Ships.Parts.Common.Utils;
using StellarDriveDemoTFAdvanced.Common;
using StellarDriveDemoTFAdvanced.Parts;
using UnityEngine;
using UnityEngine.Rendering;

namespace StellarDriveDemoTFAdvanced.Power
{
    /// <summary>
    /// The ship mining laser's part: the player's mining laser (the game's own model, found on the
    /// tool, three times its size) on a turret base that bolts to any surface, the gun pointing
    /// out of it. Sockets on the back of the base: power on both ends, status signal, command
    /// signal and the item socket for the line to the chests. Only call when SDModKit is loaded.
    /// </summary>
    internal static class MiningLaserParts
    {
        public const string MuzzleName = "TFA_LaserMuzzle";
        private const float GunLength = 0.78f;

        public static void Register()
        {
            if (DevHarness.Off("laser"))
                return;
            SDModKit.Game.ItemCables.Install();
            PartFactory.Register(ShipMiningLaser.Id, "SignalDisplay", AdvancedTab.Name, AdvancedTab.ArtifactsRow, Configure);
            AdvancedMod.Log.Msg("registered the ship mining laser");
        }

        private static void Configure(PartSettings settings, GameObject prefab)
        {
            PartFactory.Describe(settings, Loc.Pick("Laser de minage embarqué", "Ship mining laser", "Láser de minería de nave"),
                Loc.Pick($"Le laser de minage du joueur, en grand, sur une tourelle à fixer sur la coque : son rayon part tout droit de la surface où on le pose et mine les rochers, cristaux et minerais d'astéroïde qu'il touche, jusqu'à {PowerCatalog.MiningLaserRange.Value:0} m. Ce qu'il extrait va dans le coffre relié à sa prise d'objets (jaune) par une ligne d'objets ; sans coffre relié, il ne tire pas. {PowerCatalog.MiningLaserWatts.Value:0} W. Prise de commande : sans câble il tire toujours, avec un câble quand le signal est au-dessus de 0. Prise d'état : 2 mine, 1 tire, 0 éteint, -1 sans courant, -2 sans coffre, -3 coffre plein.",
                    $"The player's mining laser, scaled up, on a turret that bolts to the hull: its beam goes straight out of the surface it is placed on and mines the rocks, crystals and asteroid ores it hits, up to {PowerCatalog.MiningLaserRange.Value:0} m. What it mines goes into the chest linked to its item socket (yellow) by an item line; with no chest linked it does not fire. {PowerCatalog.MiningLaserWatts.Value:0} W. Command socket: with no cable it always fires, with a cable while the signal is above 0. Status socket: 2 mining, 1 firing, 0 off, -1 no power, -2 no chest, -3 chest full.",
                    $"El láser de minería del jugador, en grande, sobre una torreta que se fija al casco: su rayo sale recto de la superficie donde se coloca y mina las rocas, cristales y minerales de asteroide que toca, hasta {PowerCatalog.MiningLaserRange.Value:0} m. Lo que extrae va al cofre unido a su toma de objetos (amarilla) por una línea de objetos; sin cofre unido no dispara. {PowerCatalog.MiningLaserWatts.Value:0} W. Toma de mando: sin cable dispara siempre, con cable cuando la señal es mayor que 0. Toma de estado: 2 mina, 1 dispara, 0 apagado, -1 sin energía, -2 sin cofre, -3 cofre lleno."),
                70f, new[] { (101u, 10), (112u, 8), (102u, 4), (104u, 4) });
            settings.internalStateClass = PowerCatalog.MachineStateClass;
            settings.snappingStyle = Ships.Interface.Model.SnappingStyle.PreciseOnAny;
            const float width = 0.62f, depth = 0.62f;
            Transform visuals = PartFactory.BlankFromSignalDisplay(prefab, new Vector3(0f, 0.55f, 0f), new Vector3(width, 1.1f, depth));
            int layer = visuals.gameObject.layer;

            // Turret: a square foot with the sockets at the back, a turning ring and a fork holding the gun
            var model = new MachineModels.Model();
            model.Armor.Box(new Vector3(0f, 0.07f, 0f), new Vector3(width, 0.14f, depth));
            model.Dark.Box(new Vector3(0f, 0.15f, 0f), new Vector3(width - 0.08f, 0.02f, depth - 0.08f));
            model.Armor.Cylinder(new Vector3(0f, 0.14f, 0f), new Vector3(0f, 0.24f, 0f), 0.24f, 0.22f, 20);
            model.Glow.Cylinder(new Vector3(0f, 0.24f, 0f), new Vector3(0f, 0.255f, 0f), 0.2f, 0.2f, 20);
            model.Armor.Cylinder(new Vector3(0f, 0.255f, 0f), new Vector3(0f, 0.32f, 0f), 0.2f, 0.17f, 20);
            foreach (float x in new[] { -0.17f, 0.17f })
            {
                model.Armor.Box(new Vector3(x, 0.5f, 0f), new Vector3(0.05f, 0.4f, 0.16f));
                model.Dark.Cylinder(new Vector3(x * 1.25f, 0.6f, 0f), new Vector3(x * 0.85f, 0.6f, 0f), 0.05f, 0.05f, 10);
            }
            // Cooling fins on the foot, a cable sleeve from the foot up into the fork
            foreach (float z in new[] { -0.2f, -0.12f, 0.12f, 0.2f })
                model.Dark.Box(new Vector3(0.27f, 0.08f, z), new Vector3(0.05f, 0.1f, 0.03f));
            model.Dark.Cylinder(new Vector3(0f, 0.32f, -0.08f), new Vector3(0f, 0.42f, -0.06f), 0.035f, 0.035f, 8);

            Transform gun = GunFromTool(visuals, layer);
            if (gun == null)
                BuiltGun(model);
            MachineModels.Attach(visuals, model, "MiningLaser", new Color(1f, 0.4f, 0.15f), new Color(1f, 0.55f, 0.25f));

            var muzzle = new GameObject(MuzzleName) { layer = layer };
            muzzle.transform.SetParent(visuals, false);
            muzzle.transform.localPosition = ShipMiningLaser.Muzzle;
            visuals.gameObject.AddComponent<MiningLaserVisuals>();

            var bounds = prefab.GetComponent<ShipPartBounds>();
            PowerParts.GraftRow(prefab, bounds, 0.075f, -depth * 0.5f, 0.11f,
                (PowerCatalog.MachinePowerA, CableType.Power), (ShipMiningLaser.StatusSocket, CableType.Signal), (ShipMiningLaser.CommandSocket, CableType.Signal),
                (ShipMiningLaser.ItemSocket, SDModKit.Game.ItemCables.Item), (PowerCatalog.MachinePowerB, CableType.Power));
        }

        /// <summary>
        /// The game's mining laser model, copied from the tool: its meshes and materials, turned so
        /// the tool's barrel points along +Y, scaled to the gun's length, held in the fork.
        /// </summary>
        private static Transform GunFromTool(Transform visuals, int layer)
        {
            Tools.MiningLaser.MiningLaser tool = Resources.FindObjectsOfTypeAll<Tools.MiningLaser.MiningLaser>().FirstOrDefault(t => t != null);
            if (tool == null)
            {
                AdvancedMod.Log.Warning("ship mining laser: the mining laser tool was not found, a drawn gun is used");
                return null;
            }
            Transform root = tool.transform;
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r => r.GetComponent<MeshFilter>()?.sharedMesh != null && r.sharedMaterials.All(m => m != null && m.shader != null && !m.shader.name.Contains("Particle")))
                .ToList();
            if (DevHarness.Active)
            {
                var dump = new StringBuilder("dev: mining laser tool " + root.name);
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    var filter = t.GetComponent<MeshFilter>();
                    var renderer = t.GetComponent<Renderer>();
                    dump.Append($"\n   {t.name} pos {root.InverseTransformPoint(t.position)} fwd {root.InverseTransformDirection(t.forward)} mesh {filter?.sharedMesh?.name} {filter?.sharedMesh?.bounds.size} renderer {renderer?.GetType().Name} mats {(renderer == null ? "" : string.Join(",", renderer.sharedMaterials.Select(m => m?.name + "/" + m?.shader?.name)))} layer {t.gameObject.layer}");
                }
                AdvancedMod.Log.Msg(dump.ToString());
            }
            if (renderers.Count == 0)
                return null;
            // The barrel's direction: where the laser leaves the tool, else the tool's forward
            Transform origin = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.ToLowerInvariant().Contains("origin") || t.name.ToLowerInvariant().Contains("muzzle"));
            Vector3 barrel = origin != null ? root.InverseTransformDirection(origin.forward) : Vector3.forward;
            Quaternion turn = Quaternion.FromToRotation(barrel, Vector3.up);
            // Bounds of the copied meshes in tool space, to scale and centre the gun
            var box = new Bounds();
            bool first = true;
            foreach (MeshRenderer renderer in renderers)
            {
                Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                Matrix4x4 toRoot = root.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                foreach (Vector3 corner in Corners(mesh.bounds))
                {
                    Vector3 p = turn * toRoot.MultiplyPoint3x4(corner);
                    if (first)
                        box = new Bounds(p, Vector3.zero);
                    else
                        box.Encapsulate(p);
                    first = false;
                }
            }
            float scale = GunLength / Mathf.Max(0.01f, box.size.y);
            var gun = new GameObject("TF_NoPaint_MiningLaserGun") { layer = layer };
            gun.transform.SetParent(visuals, false);
            // Its middle at the fork's axle, its tip just under the muzzle
            gun.transform.localPosition = new Vector3(0f, ShipMiningLaser.Muzzle.y - 0.02f, 0f) - box.center * scale - Vector3.up * box.extents.y * scale;
            gun.transform.localRotation = turn;
            gun.transform.localScale = Vector3.one * scale;
            foreach (MeshRenderer renderer in renderers)
            {
                Matrix4x4 toRoot = root.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                var piece = new GameObject("TF_NoPaint_" + renderer.name) { layer = layer };
                piece.transform.SetParent(gun.transform, false);
                piece.transform.localPosition = toRoot.GetColumn(3);
                piece.transform.localRotation = toRoot.rotation;
                piece.transform.localScale = toRoot.lossyScale;
                piece.AddComponent<MeshFilter>().sharedMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                var copy = piece.AddComponent<MeshRenderer>();
                copy.sharedMaterials = renderer.sharedMaterials;
                copy.shadowCastingMode = ShadowCastingMode.On;
            }
            AdvancedMod.Log.Msg($"ship mining laser: gun copied from the tool ({renderers.Count} meshes, scale {scale:0.00})");
            return gun.transform;
        }

        private static IEnumerable<Vector3> Corners(Bounds b)
        {
            for (int i = 0; i < 8; i++)
                yield return b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
        }

        // Without the tool: a barrel with cooling rings, a lens and an emitter
        private static void BuiltGun(MachineModels.Model model)
        {
            model.Armor.Box(new Vector3(0f, 0.6f, 0f), new Vector3(0.26f, 0.3f, 0.22f));
            model.Armor.Cylinder(new Vector3(0f, 0.75f, 0f), new Vector3(0f, 0.98f, 0f), 0.08f, 0.065f, 14);
            foreach (float y in new[] { 0.8f, 0.86f, 0.92f })
                model.Dark.Cylinder(new Vector3(0f, y, 0f), new Vector3(0f, y + 0.025f, 0f), 0.095f, 0.095f, 14);
            model.Core.Cylinder(new Vector3(0f, 0.98f, 0f), new Vector3(0f, 1.0f, 0f), 0.05f, 0.04f, 14);
        }
    }

    /// <summary>
    /// The beam, from the synchronized status: drawn from the muzzle to the first thing in its way
    /// (cast in the physics scene, like the host), sparks and light where it hits, the tool's own
    /// sound while it fires.
    /// </summary>
    internal sealed class MiningLaserVisuals : DefaultShipPartVisuals, IContextAwarePart
    {
        private const string FireSound = "event:/Tools/MiningLaser";
        private const string SparkSound = "Event:/Tools/MiningLaserSpark";
        private static readonly Color BeamColor = new Color(1f, 0.38f, 0.14f);
        private static Material _beamMaterial, _glowMaterial;
        private static Mesh _beamMesh, _quad;

        private Transform _muzzle;
        private bool _hasContext;
        private uint _shipId;
        private ushort _partId;
        private Ships.Interface.Model.State.IShipStateRead _ship;
        private double _value;
        private Transform _beam, _spark, _flare;
        private Light _light;
        private bool _sounding, _sparking;
        private IAudioPlayer _audio;

        private bool Firing => _value >= ShipMiningLaser.Firing;

        private void Awake()
        {
            _muzzle = transform.Find(MiningLaserParts.MuzzleName);
        }

        public void SetContext(ShipPartContext context)
        {
            _shipId = context.Part.ShipId;
            _partId = context.Part.PartId;
            _ship = context.ShipStateRead;
            _hasContext = true;
        }

        public override void SetNetworkState(IStatefulPartState state)
        {
            if (state is ConstantState constant)
                _value = constant.ConstantValue;
        }

        public override void ReactToNetworkStateChange(StatefulPartStateChangeEvent change) => SetNetworkState(change.NewState);

        private void LateUpdate()
        {
            try
            {
                Draw();
            }
            catch (System.Exception e)
            {
                ErrorLog.Report("ship mining laser beam", e);
                _value = 0;
            }
        }

        private void Draw()
        {
            if (!_hasContext || _muzzle == null)
                return;
            if (!Firing)
            {
                Show(false, false);
                return;
            }
            if (_beam == null && !Build())
                return;
            float length = Mathf.Max(1f, PowerCatalog.MiningLaserRange.Value);
            bool hit = false;
            if (_ship != null && _ship.TryGetStatefulPart(_partId, out StatefulPart part)
                && ShipMiningLaser.Ray(_shipId, part, out Ray ray, out Transform body) && ShipMiningLaser.Cast(ray, body, out RaycastHit contact))
            {
                length = contact.distance;
                hit = true;
            }
            Show(true, hit);
            float flicker = 1f + 0.12f * Mathf.Sin(Time.time * 53f) + 0.06f * Mathf.Sin(Time.time * 131f);
            float width = 0.05f * flicker;
            _beam.localScale = new Vector3(width, length, width);
            _flare.localRotation = Quaternion.AngleAxis(Time.time * 220f, Vector3.up);
            _flare.localScale = Vector3.one * (0.22f * flicker);
            if (hit)
            {
                _spark.localPosition = Vector3.up * length;
                Camera camera = MainCamera.Get();
                if (camera != null)
                    _spark.rotation = Quaternion.LookRotation(camera.transform.position - _spark.position);
                _spark.localScale = Vector3.one * (0.55f * flicker * (_value >= ShipMiningLaser.Mining ? 1.4f : 1f));
                _light.intensity = 2.5f * flicker;
            }
        }

        private void Show(bool beam, bool hit)
        {
            if (_beam != null)
            {
                _beam.gameObject.SetActive(beam);
                _flare.gameObject.SetActive(beam);
                _spark.gameObject.SetActive(beam && hit);
            }
            Sound(ref _sounding, beam, FireSound, gameObject);
            if (_spark != null)
                Sound(ref _sparking, beam && hit, SparkSound, _spark.gameObject);
        }

        private void Sound(ref bool playing, bool on, string sound, GameObject source)
        {
            if (playing == on)
                return;
            playing = on;
            try
            {
                if (_audio == null)
                    _audio = ServiceLocator.GetService<IAudioPlayer>();
                if (on)
                    _audio?.PlayAudioEvent3D(sound, source);
                else
                    _audio?.StopAudioEvent3D(sound, source);
            }
            catch (System.Exception e)
            {
                ErrorLog.Report("ship mining laser sound", e);
            }
        }

        private void OnDisable() => Show(false, false);

        private bool Build()
        {
            if (_beamMaterial == null)
            {
                _beamMaterial = TransparentMaterials.Additive("TFA_MiningBeam", BeamTexture());
                _glowMaterial = TransparentMaterials.Additive("TFA_MiningGlow", GlowTexture());
                if (_beamMaterial == null || _glowMaterial == null)
                    return false;
                _beamMaterial.SetColor("_BaseColor", BeamColor * 1.6f);
                _glowMaterial.SetColor("_BaseColor", BeamColor * 2f);
                _beamMesh = BeamMesh();
                _quad = QuadMesh();
            }
            int layer = gameObject.layer;
            _beam = Child("TF_NoPaint_MiningBeam", _beamMesh, _beamMaterial, layer, _muzzle);
            _flare = Child("TF_NoPaint_MiningFlare", _beamMesh, _glowMaterial, layer, _muzzle);
            _spark = Child("TF_NoPaint_MiningSpark", _quad, _glowMaterial, layer, _muzzle);
            _light = _spark.gameObject.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = BeamColor;
            _light.range = 4f;
            _light.shadows = LightShadows.None;
            return true;
        }

        private static Transform Child(string name, Mesh mesh, Material material, int layer, Transform parent)
        {
            var child = new GameObject(name) { layer = layer };
            child.transform.SetParent(parent, false);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            child.SetActive(false);
            return child.transform;
        }

        // Three crossed quads one unit long along +Y, one unit wide: seen as a round beam from any side
        private static Mesh BeamMesh()
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int i = 0; i < 3; i++)
            {
                Vector3 side = Quaternion.AngleAxis(i * 60f, Vector3.up) * Vector3.right * 0.5f;
                int at = vertices.Count;
                vertices.AddRange(new[] { -side, side, side + Vector3.up, -side + Vector3.up });
                uvs.AddRange(new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) });
                triangles.AddRange(new[] { at, at + 2, at + 1, at, at + 3, at + 2 });
            }
            var mesh = new Mesh { name = "TFA_MiningBeam" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.bounds = new Bounds(new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 1f));
            return mesh;
        }

        private static Mesh QuadMesh()
        {
            var mesh = new Mesh { name = "TFA_MiningSpark" };
            mesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            return mesh;
        }

        // Bright core across the beam, soft edges
        private static Texture2D BeamTexture()
        {
            const int size = 64;
            var texture = new Texture2D(size, 4, TextureFormat.RGBA32, false) { name = "TFA_MiningBeam", wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < size; x++)
            {
                float u = Mathf.Abs((x + 0.5f) / size * 2f - 1f);
                float a = Mathf.Clamp01(Mathf.Pow(1f - u, 2.2f) + (u < 0.2f ? 0.6f : 0f));
                Color c = Color.Lerp(Color.white, new Color(1f, 0.7f, 0.5f), u);
                for (int y = 0; y < 4; y++)
                    texture.SetPixel(x, y, new Color(c.r, c.g, c.b, a));
            }
            texture.Apply();
            return texture;
        }

        private static Texture2D GlowTexture()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "TFA_MiningGlow", wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    // A soft disc and four thin rays
                    float rays = Mathf.Max(0f, 1f - Mathf.Abs(u) * 14f) + Mathf.Max(0f, 1f - Mathf.Abs(v) * 14f);
                    float a = Mathf.Clamp01(Mathf.Pow(Mathf.Clamp01(1f - r), 2.5f) + rays * Mathf.Clamp01(1f - r) * 0.5f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            texture.Apply(true);
            return texture;
        }
    }
}

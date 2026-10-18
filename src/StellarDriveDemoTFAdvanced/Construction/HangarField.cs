using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Parts.Common;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;
using UnityEngine.Rendering;

namespace StellarDriveDemoTFAdvanced.Construction
{
    /// <summary>
    /// The ship hangar's mouth closes by itself: when a ship is docked to its collar, a force
    /// field rises across the big door (keeping the bay's air in), and it opens again when the
    /// ship undocks. It is a field of light, not a wall: the docked ship leaves as soon as its
    /// magnet lets go.
    /// </summary>
    internal sealed class HangarField : DefaultShipPartVisuals
    {
        private const string FieldName = "TFA_HangarField";
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Renderer _field;
        private MaterialPropertyBlock _block;
        private bool _docked;
        private float _shown;

        /// <summary>Adds the field (hidden) across the mouth, in the module's frame placed at <paramref name="offset"/>.</summary>
        public static void Attach(Transform visuals, Vector3 offset, Vector3 size, float mouthWidth, float mouthHeight, float wall)
        {
            var field = new GameObject(FieldName) { layer = visuals.gameObject.layer };
            field.transform.SetParent(visuals, false);
            field.transform.localPosition = offset;
            var mesh = new MeshBuilder { DoubleSided = true };
            // A sheet across the mouth and a few brighter bands across it
            float z = size.z * 0.5f - wall * 0.5f;
            mesh.Box(new Vector3(0f, wall + mouthHeight * 0.5f, z), new Vector3(mouthWidth, mouthHeight, 0.02f));
            for (int i = 1; i < 6; i++)
                mesh.Box(new Vector3(0f, wall + mouthHeight * i / 6f, z), new Vector3(mouthWidth, 0.05f, 0.04f));
            field.AddComponent<MeshFilter>().sharedMesh = mesh.Build(FieldName);
            var renderer = field.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = TransparentMaterials.Additive("TFA_HangarFieldMat", null);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            visuals.gameObject.AddComponent<HangarField>();
        }

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _field = transform.Find(FieldName)?.GetComponent<Renderer>();
        }

        public override void SetNetworkState(IStatefulPartState state)
        {
            if (state is DockingDoorState door)
                _docked = door.IsDocked;
        }

        public override void ReactToNetworkStateChange(StatefulPartStateChangeEvent change) => SetNetworkState(change.NewState);

        private void Update()
        {
            if (_field == null)
                return;
            // Rises over a second, a slow shimmer while up
            _shown = Mathf.MoveTowards(_shown, _docked ? 1f : 0f, Time.deltaTime);
            _field.enabled = _shown > 0.01f;
            if (!_field.enabled)
                return;
            float shimmer = 0.85f + 0.15f * Mathf.Sin(Time.time * 2.3f);
            _block.SetColor(BaseColorId, new Color(0.25f, 0.65f, 1f, 0.22f * _shown * shimmer));
            _field.SetPropertyBlock(_block);
        }
    }
}

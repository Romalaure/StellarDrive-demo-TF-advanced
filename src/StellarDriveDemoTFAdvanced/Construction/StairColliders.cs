using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Parts.Common.Model.Reactions;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Construction
{
    /// <summary>
    /// The collision of the stairs: a ramp under the steps (walked like the hull's slopes) in place
    /// of the part's box, which was a block nobody could climb. A straight flight is one tilted
    /// slab from the foot (-z, floor level) to the top edge (+z, Size.y up); a spiral is a slab per
    /// step around a pole. The game copies this object into the ship's colliders (see
    /// <see cref="Worlds.DecorColliders"/>).
    /// </summary>
    internal sealed class StairColliders : MonoBehaviour, IShipPartDynamicCollider
    {
        private const float Slab = 0.1f;
        public const int SpiralSteps = 9;
        public const float SpiralTurn = 270f;

        public GameObject GameObject => gameObject;

        public static void Attach(GameObject prefab, Vector3 size, bool spiral)
        {
            int ship = LayerMask.NameToLayer("Ship");
            var root = new GameObject("TFA_StairColliders") { layer = ship };
            root.transform.SetParent(prefab.transform, false);
            if (spiral)
            {
                float rise = size.y / SpiralSteps, step = SpiralTurn / SpiralSteps * Mathf.Deg2Rad;
                const float inner = 0.1f, outer = 0.97f, middle = (inner + outer) * 0.5f;
                float arc = middle * step;
                float pitch = Mathf.Atan2(rise, arc);
                for (int i = 0; i < SpiralSteps; i++)
                {
                    // Matches the model (tools/blender: x = r cos a, z = r sin a, climbing with a)
                    float a = (i + 0.5f) * step;
                    var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    var tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                    Vector3 forward = tangent * Mathf.Cos(pitch) + Vector3.up * Mathf.Sin(pitch);
                    Quaternion rotation = Quaternion.LookRotation(forward, Vector3.Cross(forward, radial).normalized);
                    Vector3 surface = radial * middle + Vector3.up * ((i + 0.5f) * rise);
                    Add(root.transform, ship, "Step" + i, surface - rotation * Vector3.up * (Slab * 0.5f), rotation,
                        new Vector3(outer - inner, Slab, arc / Mathf.Cos(pitch) * 1.15f));
                }
                var pole = new GameObject("Pole") { layer = ship, tag = "ShipFloatingPart" };
                pole.transform.SetParent(root.transform, false);
                var capsule = pole.AddComponent<CapsuleCollider>();
                capsule.radius = 0.08f;
                capsule.height = size.y;
                capsule.center = new Vector3(0f, size.y * 0.5f, 0f);
            }
            else
            {
                float slope = Mathf.Atan2(size.y, size.z);
                float length = Mathf.Sqrt(size.y * size.y + size.z * size.z);
                // Climbing toward +z: the slab's forward axis tilted up by the slope
                Quaternion rotation = Quaternion.Euler(-slope * Mathf.Rad2Deg, 0f, 0f);
                Vector3 surface = new Vector3(0f, size.y * 0.5f, 0f);
                Add(root.transform, ship, "Ramp", surface - rotation * Vector3.up * (Slab * 0.5f), rotation, new Vector3(size.x, Slab, length));
            }
            root.AddComponent<StairColliders>();
        }

        private static void Add(Transform root, int layer, string name, Vector3 position, Quaternion rotation, Vector3 size)
        {
            var piece = new GameObject(name) { layer = layer, tag = "ShipFloatingPart" };
            piece.transform.SetParent(root, false);
            piece.transform.localPosition = position;
            piece.transform.localRotation = rotation;
            piece.AddComponent<BoxCollider>().size = size;
        }

        public void SetNetworkState(IStatefulPartState state)
        {
        }

        public void ReactToNetworkStateChange(StatefulPartStateChangeEvent change)
        {
        }
    }
}

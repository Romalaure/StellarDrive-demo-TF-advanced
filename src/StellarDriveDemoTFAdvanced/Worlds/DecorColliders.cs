using System.Collections.Generic;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Parts.Common.Model.Reactions;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Worlds
{
    /// <summary>
    /// The colliders of a wreck's dressing: one convex collider per big solid piece. The game
    /// copies this object into the ship's colliders in place of the part's bounds box (which, for
    /// a dressing sitting at the station's origin, was an invisible block in the middle of it).
    /// </summary>
    internal sealed class DecorColliders : MonoBehaviour, IShipPartDynamicCollider
    {
        public GameObject GameObject => gameObject;

        public static void Attach(GameObject prefab, IEnumerable<Mesh> solids)
        {
            int ship = LayerMask.NameToLayer("Ship");
            var root = new GameObject("TFA_DecorColliders") { layer = ship };
            root.transform.SetParent(prefab.transform, false);
            int count = 0;
            foreach (Mesh mesh in solids)
            {
                if (mesh == null || mesh.vertexCount < 4)
                    continue;
                var piece = new GameObject("Solid" + count++) { layer = ship, tag = "ShipFloatingPart" };
                piece.transform.SetParent(root.transform, false);
                var collider = piece.AddComponent<MeshCollider>();
                collider.convex = true;
                collider.sharedMesh = mesh;
            }
            root.AddComponent<DecorColliders>();
        }

        public void SetNetworkState(IStatefulPartState state)
        {
        }

        public void ReactToNetworkStateChange(StatefulPartStateChangeEvent change)
        {
        }
    }
}

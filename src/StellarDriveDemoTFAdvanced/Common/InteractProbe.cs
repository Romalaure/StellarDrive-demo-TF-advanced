using System.Linq;
using System.Text;
using Players.Interface.Model;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Common
{
    /// <summary>
    /// Development check (DevDisable "interactprobe"): what the player could use around them.
    /// Logs the colliders near the camera, those tagged for interaction (active or not, reach),
    /// the player's state and space, and takes a screenshot, a few times after the world loads.
    /// </summary>
    internal static class InteractProbe
    {
        public static void Install()
        {
            foreach (float when in new[] { 25f, 35f })
                DevHarness.Steps.Add((when, "interact probe " + when, () => { if (DevHarness.Off("interactprobe")) Probe(when); }));
        }

        private static void Probe(float when)
        {
            Camera camera = MainCamera.Get();
            if (camera == null)
            {
                AdvancedMod.Log.Msg("dev: interact: no camera");
                return;
            }
            var log = new StringBuilder();
            var player = GameServices.PlayersClient?.LocalPlayer;
            log.Append($"dev: interact at {when} s: camera {camera.transform.position} fwd {camera.transform.forward}");
            if (player != null)
                log.Append($", player space {player.InterpolatedPhysicsState.SpaceId} pos {player.InterpolatedPhysicsState.Position}");
            // Ships live in the client physics scene; the local player's collider gives the physics position
            Vector3 at = camera.transform.position;
            GameObject body = GameObject.Find("LocalPlayerCollider 0");
            if (body != null)
                at = body.transform.position + Vector3.up * 0.7f;
            var buffer = new Collider[200];
            int count = WorldTracking.Interface.Values.ClientScene.PhysicsScene.OverlapSphere(at, 4f, buffer, ~0, QueryTriggerInteraction.Collide);
            Collider[] near = buffer.Take(count).ToArray();
            log.Append($"; physics at {at}");
            log.Append($"; {near.Length} collider(s) within 4 m");
            foreach (Collider collider in near.Where(c => c.CompareTag("Interactable")))
            {
                var interactable = collider.GetComponent<Players.Interface.Model.IInteractableObject>();
                log.Append($"\n   interactable {Path(collider.transform)} active {interactable?.IsActive} reach {interactable?.DetectionDistance} dist {Vector3.Distance(collider.ClosestPoint(camera.transform.position), camera.transform.position):0.0} layer {collider.gameObject.layer} enabled {collider.enabled}");
            }
            foreach (Collider collider in near.Where(c => !c.CompareTag("Interactable")).Take(25))
                log.Append($"\n   other {Path(collider.transform)} layer {collider.gameObject.layer} trigger {collider.isTrigger}");
            if (WorldTracking.Interface.Values.ClientScene.PhysicsScene.Raycast(at, camera.transform.forward, out RaycastHit hit, 5f, ~0, QueryTriggerInteraction.Collide))
                log.Append($"\n   looking at {Path(hit.collider.transform)} tag {hit.collider.tag} at {hit.distance:0.0} m");
            AdvancedMod.Log.Msg(log.ToString());
            DevHarness.Screenshot("interact_" + when);
        }

        private static string Path(Transform t)
        {
            string path = t.name;
            for (int i = 0; i < 3 && t.parent != null; i++)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }
    }
}

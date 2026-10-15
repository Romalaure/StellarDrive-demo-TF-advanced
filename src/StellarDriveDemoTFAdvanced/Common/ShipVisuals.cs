using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StellarDriveDemoTFAdvanced.Common
{
    /// <summary>
    /// The game draws each ship under a root object named "ship {id} visual". Finding one used to
    /// scan every Transform of the scene (thousands); it is now looked up among the scenes' root
    /// objects only, at most once per second for a ship not found, and remembered while it lives.
    /// </summary>
    internal static class ShipVisuals
    {
        private static readonly Dictionary<uint, Transform> Found = new Dictionary<uint, Transform>();
        private static readonly Dictionary<uint, float> Missed = new Dictionary<uint, float>();
        private static readonly List<GameObject> Roots = new List<GameObject>();

        public static void Forget()
        {
            Found.Clear();
            Missed.Clear();
        }

        /// <summary>The ship's root visual, or null.</summary>
        public static Transform Find(uint shipId)
        {
            if (Found.TryGetValue(shipId, out Transform cached))
            {
                if (cached != null && cached.parent == null)
                    return cached;
                Found.Remove(shipId);
            }
            float now = Time.unscaledTime;
            if (Missed.TryGetValue(shipId, out float retry) && now < retry)
                return null;
            string name = "ship " + shipId + " visual";
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                Scene scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded)
                    continue;
                Roots.Clear();
                scene.GetRootGameObjects(Roots);
                foreach (GameObject root in Roots)
                {
                    if (root != null && root.name == name)
                    {
                        Missed.Remove(shipId);
                        Found[shipId] = root.transform;
                        return root.transform;
                    }
                }
            }
            Missed[shipId] = now + 1f;
            return null;
        }

        /// <summary>The ship id of a part's visual root ("ship {id} visual"), or 0.</summary>
        public static uint IdOf(Transform root)
        {
            if (root == null)
                return 0;
            string name = root.name;
            if (!name.StartsWith("ship "))
                return 0;
            int end = name.IndexOf(' ', 5);
            return uint.TryParse(end > 5 ? name.Substring(5, end - 5) : name.Substring(5), out uint id) ? id : 0;
        }
    }
}

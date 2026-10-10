using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Core.Services;
using MelonLoader;
using Planets;
using Planets.Interface.Model;
using Planets.Interface.Services;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace StellarDriveDemoTFAdvanced.Worlds
{
    /// <summary>Development checks of the world features, run by <see cref="DevHarness"/>.</summary>
    internal static class WorldDevSteps
    {
        /// <summary>Extra things to look at: screenshot name and the scene object to aim at.</summary>
        public static readonly List<(string Name, System.Func<GameObject> Target)> Targets = new List<(string, System.Func<GameObject>)>();

        private static Quaternion? _aim;

        public static void Install()
        {
            DevHarness.Steps.Add((24f, "planets report", Report));
            DevHarness.Steps.Add((25f, "prefab probe", PrefabProbe));
            DevHarness.Steps.Add((26f, "planet shots", () => MelonCoroutines.Start(Shots())));
            RenderPipelineManager.beginCameraRendering += (context, camera) =>
            {
                if (_aim.HasValue && camera.cameraType == CameraType.Game && camera.targetTexture == null)
                    camera.transform.rotation = _aim.Value;
            };
        }

        private static void Report()
        {
            var text = new StringBuilder("planets seen by the client tracker:");
            IPlanetsClientProvider planets = ServiceLocator.GetService<IPlanetsClientProvider>();
            foreach (IPlanetProperties planet in planets.Planets)
                text.Append($"\n  #{planet.Id} {planet.Name} r={planet.Radius} g={planet.PhysicalProperties.gravityInG} atmo={planet.PhysicalProperties.HasAtmosphere} soi={planet.SphereOfInfluence:0}");
            AdvancedMod.Log.Msg(text.ToString());
        }

        /// <summary>
        /// Real screenshots toward each mod planet: for a few frames, the game's own cameras are
        /// turned toward the planet's rendered object just before they draw (planets are drawn by
        /// the game's render passes, which only run for its own cameras).
        /// </summary>
        private static IEnumerator Shots()
        {
            var targets = new List<(string, System.Func<GameObject>)>();
            foreach (Planet planet in PlanetFactory.Planets)
            {
                string prefix = planet.name + " (";
                targets.Add(("planet_" + planet.Id, () => SceneManager.GetActiveScene().GetRootGameObjects()
                    .FirstOrDefault(r => r.activeInHierarchy && r.name.StartsWith(prefix))));
            }
            targets.AddRange(Targets);
            targets.Add(("blackhole", () => SceneManager.GetActiveScene().GetRootGameObjects().Concat(Object.FindObjectsOfType<Transform>().Where(t => t.parent == null).Select(t => t.gameObject))
                .FirstOrDefault(r => r.name == "TFA Black Hole (far)")));
            foreach ((string name, System.Func<GameObject> find) in targets)
            {
                IEnumerator shot = ShootAt(find(), name);
                while (shot.MoveNext())
                    yield return shot.Current;
            }
            _aim = null;
        }

        /// <summary>A screenshot with the game's cameras turned toward a far-camera object (planet or black hole visuals).</summary>
        public static IEnumerator ShootAt(GameObject target, string name)
        {
            Camera far = Camera.allCameras.FirstOrDefault(c => c.transform.root.name.Contains("Distant"));
            if (target == null || far == null)
            {
                AdvancedMod.Log.Warning($"dev: nothing to aim at for {name}");
                yield break;
            }
            Vector3 point = target.transform.position;
            _aim = Quaternion.LookRotation((point - far.transform.position).normalized, Vector3.up);
            for (int i = 0; i < 4; i++)
                yield return null;
            DevHarness.Screenshot(name);
            for (int i = 0; i < 3; i++)
                yield return null;
            _aim = null;
            AdvancedMod.Log.Msg($"dev: aimed at {target.name} ({point - far.transform.position})");
        }

        private static void SaveTexture(Texture2D texture, string path)
        {
            var rt = RenderTexture.GetTemporary(Mathf.Min(texture.width, 1024), Mathf.Min(texture.height, 1024), 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(texture, rt);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var copy = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            copy.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(path, copy.EncodeToPNG());
            Object.Destroy(copy);
        }

        // Structure of the demo planet's hotspot and flora prefabs, to build new ones alike
        private static void PrefabProbe()
        {
            var text = new StringBuilder();
            string[] names = { "CrashedShipNoSmoke", "CrashedThruster", "RockLargeIron", "CrystallSmallGlass", "FirTreeTall1", "Grass1" };
            foreach (GameObject prefab in Resources.FindObjectsOfTypeAll<GameObject>().Where(g => names.Contains(g.name) && g.scene.name == null).GroupBy(g => g.name).Select(g => g.First()))
            {
                text.AppendLine("=== prefab " + prefab.name);
                DevHarness.DumpTree(prefab.transform, 0, text);
                foreach (MeshRenderer renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
                {
                    Material material = renderer.sharedMaterial;
                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    text.AppendLine($"  renderer {renderer.name}: mesh={filter?.sharedMesh?.name} bounds={renderer.bounds.size} mat={material?.name} shader={material?.shader?.name}");
                    if (material?.shader != null)
                    {
                        for (int i = 0; i < material.shader.GetPropertyCount(); i++)
                        {
                            if (material.shader.GetPropertyType(i) == ShaderPropertyType.Color)
                                text.AppendLine($"    color {material.shader.GetPropertyName(i)} = {material.GetColor(material.shader.GetPropertyName(i))}");
                        }
                    }
                }
                foreach (Collider collider in prefab.GetComponentsInChildren<Collider>(true))
                    text.AppendLine($"  collider {collider.name}: {collider.GetType().Name} layer={collider.gameObject.layer} trigger={collider.isTrigger}");
                // Meshes and materials in detail, for building new plants alike
                foreach (MeshRenderer renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
                {
                    Mesh mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                    if (mesh == null)
                        continue;
                    text.AppendLine($"  mesh {mesh.name}: readable={mesh.isReadable} submeshes={mesh.subMeshCount} verts={mesh.vertexCount} bounds={mesh.bounds} colors={(mesh.isReadable ? mesh.colors.Length : -1)}");
                    if (mesh.isReadable)
                    {
                        var uv = new List<Vector2>();
                        for (int channel = 0; channel < 4; channel++)
                        {
                            mesh.GetUVs(channel, uv);
                            if (uv.Count > 0)
                                text.AppendLine($"    uv{channel}: x {uv.Min(u => u.x):0.###}..{uv.Max(u => u.x):0.###} y {uv.Min(u => u.y):0.###}..{uv.Max(u => u.y):0.###}");
                        }
                        if (mesh.colors.Length > 0)
                            text.AppendLine("    colors sample: " + string.Join(" ", mesh.colors.Take(6).Select(c => $"({c.r:0.##},{c.g:0.##},{c.b:0.##},{c.a:0.##})")));
                    }
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        if (material?.shader == null)
                            continue;
                        text.AppendLine($"    material {material.name} ({material.shader.name}) keywords=[{string.Join(" ", material.shaderKeywords)}]");
                        for (int i = 0; i < material.shader.GetPropertyCount(); i++)
                        {
                            string property = material.shader.GetPropertyName(i);
                            switch (material.shader.GetPropertyType(i))
                            {
                                case ShaderPropertyType.Texture:
                                    Texture texture = material.GetTexture(property);
                                    text.AppendLine($"      {property} = {texture?.name}");
                                    if (texture is Texture2D t2 && prefab.name.StartsWith("FirTreeTall1"))
                                        SaveTexture(t2, Path.Combine(DevHarness.Folder, $"tex_{material.name}_{property}.png"));
                                    break;
                                case ShaderPropertyType.Float:
                                case ShaderPropertyType.Range:
                                    text.AppendLine($"      {property} = {material.GetFloat(property):0.###}");
                                    break;
                                case ShaderPropertyType.Vector:
                                    text.AppendLine($"      {property} = {material.GetVector(property)}");
                                    break;
                            }
                        }
                    }
                }
            }
            File.WriteAllText(Path.Combine(DevHarness.Folder, "prefab_probe.txt"), text.ToString());
            AdvancedMod.Log.Msg("dev: prefab probe written");
        }
    }
}

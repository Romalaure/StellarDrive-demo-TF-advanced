using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MelonLoader.Utils;
using SDModKit.Game;
using Ships.Interface.Settings;
using UnityEngine;
using UnityEngine.Rendering;

namespace StellarDriveDemoTFAdvanced.Common
{
    /// <summary>
    /// Development check run with <see cref="SelfTest"/>: describes how the game's own parts are
    /// textured (shader properties, textures, mesh UVs and vertex colors) and saves their textures
    /// to UserData/TFAdvanced/probe, so the mod's parts can use the same materials.
    /// </summary>
    internal static class MaterialProbe
    {
        private static readonly ushort[] Parts = { 17, 19, 23, 30, 60, 65, 82, 42 };

        public static void Run()
        {
            string folder = Path.Combine(MelonEnvironment.UserDataDirectory, "TFAdvanced", "probe");
            Directory.CreateDirectory(folder);
            var report = new StringBuilder("material probe:");
            var saved = new HashSet<Texture>();
            foreach (ushort id in Parts)
            {
                PartSettings settings = CustomParts.Get(id) ?? FindSettings(id);
                if (settings == null || settings.part == null)
                    continue;
                report.Append($"\n part {id} {settings.name}");
                foreach (MeshRenderer renderer in settings.part.GetComponentsInChildren<MeshRenderer>(true))
                {
                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    Mesh mesh = filter != null ? filter.sharedMesh : null;
                    report.Append($"\n  {renderer.name} mesh={mesh?.name} verts={mesh?.vertexCount} readable={mesh?.isReadable}");
                    if (mesh != null && mesh.isReadable)
                    {
                        var uvs = new List<Vector2>();
                        mesh.GetUVs(0, uvs);
                        if (uvs.Count > 0)
                            report.Append($" uv0=({uvs.Min(u => u.x):0.###}..{uvs.Max(u => u.x):0.###}, {uvs.Min(u => u.y):0.###}..{uvs.Max(u => u.y):0.###})");
                        var uv1 = new List<Vector2>();
                        mesh.GetUVs(1, uv1);
                        report.Append($" uv1={uv1.Count} colors={mesh.colors32.Length}");
                        if (mesh.colors32.Length > 0)
                            report.Append(" firstColors=" + string.Join(",", mesh.colors32.Take(4).Select(c => $"{c.r}/{c.g}/{c.b}/{c.a}")));
                    }
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        if (material == null)
                            continue;
                        Shader shader = material.shader;
                        report.Append($"\n   mat {material.name} shader={shader?.name} keywords=[{string.Join(" ", material.shaderKeywords)}]");
                        if (shader == null)
                            continue;
                        for (int i = 0; i < shader.GetPropertyCount(); i++)
                        {
                            string name = shader.GetPropertyName(i);
                            ShaderPropertyType type = shader.GetPropertyType(i);
                            string value;
                            switch (type)
                            {
                                case ShaderPropertyType.Color: value = material.GetColor(name).ToString(); break;
                                case ShaderPropertyType.Float:
                                case ShaderPropertyType.Range: value = material.GetFloat(name).ToString("0.###"); break;
                                case ShaderPropertyType.Vector: value = material.GetVector(name).ToString(); break;
                                case ShaderPropertyType.Texture:
                                    Texture texture = material.GetTexture(name);
                                    value = texture == null ? "null" : $"{texture.name} {texture.width}x{texture.height} {texture.dimension}";
                                    if (texture is Texture2D t2 && saved.Add(texture))
                                        Save(t2, Path.Combine(folder, Clean(material.name + "_" + name + "_" + texture.name) + ".png"));
                                    break;
                                default: value = type.ToString(); break;
                            }
                            report.Append($"\n    {name} = {value}");
                        }
                    }
                }
            }
            File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
            AdvancedMod.Log.Msg($"material probe written to {folder}");
        }

        private static PartSettings FindSettings(ushort id)
        {
            return Resources.FindObjectsOfTypeAll<PartSettings>().FirstOrDefault(s => s != null && s.id == id && s.name != null && !s.name.StartsWith("TF"));
        }

        // Textures are usually not CPU readable: copy through a render texture
        private static void Save(Texture2D texture, string path)
        {
            int width = Mathf.Min(texture.width, 2048), height = Mathf.Min(texture.height, 2048);
            RenderTexture previous = RenderTexture.active;
            RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(texture, rt);
            RenderTexture.active = rt;
            var copy = new Texture2D(width, height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            copy.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(path, copy.EncodeToPNG());
            Object.Destroy(copy);
        }

        private static string Clean(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Replace(' ', '_');
        }
    }
}

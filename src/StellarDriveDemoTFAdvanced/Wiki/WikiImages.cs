using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Core.Services;
using Ships.Interface.Services;
using Ships.Interface.Settings;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Wiki
{
    /// <summary>
    /// Pictures for the wiki: a part's or an item's own icon (the build menu's and the inventory's,
    /// made by the game), found from the name a page gives it, and pictures of the plants of each
    /// mod planet, rendered once far below the world.
    /// </summary>
    internal static class WikiImages
    {
        internal enum Kind { Part, Item, Model }

        internal sealed class Ref
        {
            public Kind Kind;
            public uint Id;
            public GameObject Model;
            public string Caption;
        }

        private static Dictionary<string, Ref> _names;
        private static Loc.Language _namesLanguage;
        private static readonly Dictionary<GameObject, Texture2D> Rendered = new Dictionary<GameObject, Texture2D>();
        private static int _frame, _renderedThisFrame;

        /// <summary>Each mod planet's plants and trees (by planet id), noted when the planet is shaped.</summary>
        private static readonly Dictionary<ulong, List<GameObject>> Flora = new Dictionary<ulong, List<GameObject>>();

        public static void NoteFlora(ulong planet, IEnumerable<GameObject> prefabs) =>
            Flora[planet] = prefabs.Where(p => p != null).Distinct().ToList();

        /// <summary>Lowercase, without accents, parentheses or punctuation.</summary>
        public static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";
            int paren = text.IndexOf('(');
            if (paren > 0)
                text = text.Substring(0, paren);
            var result = new StringBuilder();
            foreach (char c in text.Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                    continue;
                result.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
            }
            return string.Join(" ", result.ToString().Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries));
        }

        private static Dictionary<string, Ref> Names()
        {
            if (_names != null && _namesLanguage == Loc.Current)
                return _names;
            _names = new Dictionary<string, Ref>();
            _namesLanguage = Loc.Current;
            foreach (global::Items.Model.ItemSettings item in Resources.FindObjectsOfTypeAll<global::Items.Model.ItemSettings>())
            {
                string key = item != null ? Normalize(item.itemName) : "";
                if (key.Length >= 3 && !_names.ContainsKey(key))
                    _names[key] = new Ref { Kind = Kind.Item, Id = item.id };
            }
            // The mod's parts (their names are the mod's own, in the chosen language)
            foreach (PartSettings part in Resources.FindObjectsOfTypeAll<PartSettings>())
            {
                if (part == null || part.id < 7100 || part.id > 7499)
                    continue;
                string key = Normalize(part.fullLabel);
                if (key.Length >= 3)
                    _names[key] = new Ref { Kind = Kind.Part, Id = part.id };
            }
            return _names;
        }

        /// <summary>The part or item a page's name stands for: the same name, or one name starting the other.</summary>
        public static Ref Match(string name)
        {
            string key = Normalize(name);
            if (key.Length < 3)
                return null;
            Dictionary<string, Ref> names = Names();
            if (names.TryGetValue(key, out Ref exact))
                return exact;
            Ref best = null;
            int bestLength = 0;
            foreach (KeyValuePair<string, Ref> entry in names)
            {
                string shorter = entry.Key.Length < key.Length ? entry.Key : key, longer = entry.Key.Length < key.Length ? key : entry.Key;
                if (shorter.Length >= 6 && longer.StartsWith(shorter + " ") && entry.Key.Length > bestLength)
                {
                    best = entry.Value;
                    bestLength = entry.Key.Length;
                }
            }
            return best;
        }

        /// <summary>The plants and trees of the planet a page is about, or nothing.</summary>
        public static List<Ref> PlanetGallery(string pageTitle)
        {
            string title = Normalize(pageTitle);
            Worlds.WikiEntry planet = Worlds.PlanetWiki.All.FirstOrDefault(p => Normalize(p.Name) == title);
            if (planet == null || !Flora.TryGetValue(planet.Id, out List<GameObject> prefabs))
                return new List<Ref>();
            // (one picture per name: the game's firs come in several shapes)
            return prefabs.Select(p => new Ref { Kind = Kind.Model, Model = p, Caption = PlantName(p.name) }).Where(r => r.Caption != null)
                .GroupBy(r => r.Caption).Select(g => g.First()).ToList();
        }

        private static string PlantName(string prefab)
        {
            string[,] names =
            {
                { "AutumnOak", "Chêne d'automne", "Autumn oak", "Roble otoñal" }, { "AutumnBirch", "Bouleau d'automne", "Autumn birch", "Abedul otoñal" },
                { "Oak", "Chêne", "Oak", "Roble" }, { "Birch", "Bouleau", "Birch", "Abedul" }, { "Acacia", "Acacia", "Acacia", "Acacia" },
                { "Kapok", "Kapokier géant", "Giant kapok", "Ceiba gigante" }, { "Palm", "Palmier", "Palm tree", "Palmera" },
                { "Jungle", "Arbre de jungle", "Jungle tree", "Árbol de la jungla" }, { "Mushroom", "Champignon géant", "Giant mushroom", "Hongo gigante" },
                { "Charred", "Arbre calciné", "Charred tree", "Árbol calcinado" }, { "Fern", "Fougère", "Fern", "Helecho" }, { "Bush", "Buisson", "Bush", "Arbusto" },
                { "RedFlowers", "Fleurs rouges", "Red flowers", "Flores rojas" }, { "YellowFlowers", "Fleurs jaunes", "Yellow flowers", "Flores amarillas" },
                { "VioletFlowers", "Fleurs violettes", "Violet flowers", "Flores violetas" }, { "WhiteFlowers", "Fleurs blanches", "White flowers", "Flores blancas" },
                { "FirTreeDead", "Sapin mort", "Dead fir", "Abeto muerto" }, { "FirTree", "Sapin", "Fir", "Abeto" }, { "Pine", "Pin", "Pine", "Pino" }, { "Grass", "Herbe", "Grass", "Hierba" }
            };
            string tint = prefab.Contains("_neon") ? Loc.Pick(" néon", " (neon)", " neón") : "";
            for (int i = 0; i < names.GetLength(0); i++)
                if (prefab.Contains(names[i, 0]))
                    return Loc.Pick(names[i, 1], names[i, 2], names[i, 3]) + tint;
            return null;
        }

        /// <summary>Whether a model's picture was tried and could not be made.</summary>
        public static bool Failed(Ref image) => image.Kind == Kind.Model && (image.Model == null || Rendered.TryGetValue(image.Model, out Texture2D done) && done == null);

        /// <summary>The picture, or null while it is not made yet (try again on a later frame).</summary>
        public static Texture Get(Ref image)
        {
            try
            {
                switch (image.Kind)
                {
                    case Kind.Part:
                        return ServiceLocator.GetService<IPartThumbnailProvider>()?.GetSmallThumbnail((ushort)image.Id);
                    case Kind.Item:
                        return ServiceLocator.GetService<global::Items.Interface.Service.IItemThumbnailProvider>()?.GetThumbnail(image.Id);
                    default:
                        return Render(image.Model);
                }
            }
            catch (System.Exception e)
            {
                AdvancedMod.Log.Warning("wiki: picture: " + e.Message);
                return null;
            }
        }

        // A copy of the model's nearest look (its first level of detail), photographed from the side
        // and a little above; one per frame at most
        private static Texture2D Render(GameObject prefab)
        {
            if (prefab == null)
                return null;
            if (Rendered.TryGetValue(prefab, out Texture2D done))
                return done;
            if (_frame != Time.frameCount)
            {
                _frame = Time.frameCount;
                _renderedThisFrame = 0;
            }
            if (_renderedThisFrame >= 1)
                return null;
            _renderedThisFrame++;
            LODGroup lod = prefab.GetComponentInChildren<LODGroup>(true);
            IEnumerable<Renderer> shown = lod != null && lod.GetLODs().Length > 0 ? lod.GetLODs()[0].renderers.Where(r => r != null && r.enabled).ToArray() : new Renderer[0];
            // (the mod's trees and plants replaced the game's model: its levels of detail list none of theirs)
            if (!shown.Any())
                shown = prefab.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.enabled);
            var root = new GameObject("TFA_WikiModel");
            root.transform.position = new Vector3(0f, -40000f, 0f);
            var bounds = new Bounds();
            bool any = false;
            foreach (Renderer source in shown)
            {
                MeshFilter filter = source != null ? source.GetComponent<MeshFilter>() : null;
                if (!(source is MeshRenderer) || filter == null || filter.sharedMesh == null)
                    continue;
                var piece = new GameObject("Piece");
                piece.transform.SetParent(root.transform, false);
                piece.transform.localPosition = prefab.transform.InverseTransformPoint(source.transform.position);
                piece.transform.localRotation = Quaternion.Inverse(prefab.transform.rotation) * source.transform.rotation;
                piece.transform.localScale = source.transform.lossyScale;
                piece.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                piece.AddComponent<MeshRenderer>().sharedMaterials = source.sharedMaterials;
                Bounds local = filter.sharedMesh.bounds;
                local = new Bounds(piece.transform.localPosition + Vector3.Scale(local.center, piece.transform.localScale), Vector3.Scale(local.size, piece.transform.localScale));
                if (!any)
                    bounds = local;
                else
                    bounds.Encapsulate(local);
                any = true;
            }
            if (!any)
            {
                Object.Destroy(root);
                Rendered[prefab] = null;
                return null;
            }
            // Our own camera and sun far below the world, for this one frame (the game's thumbnail
            // renderer leaves the foliage black)
            const int size = 192;
            var cameraObject = new GameObject("TFA_WikiCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.1f, 0.17f, 0.24f);
            camera.fieldOfView = 30f;
            // A plain scene render: the game's atmosphere and cloud passes only run for Game cameras
            camera.cameraType = CameraType.Reflection;
            var data = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(camera);
            data.renderShadows = false;
            data.renderPostProcessing = false;
            data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;
            var sunObject = new GameObject("TFA_WikiSun");
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.5f;
            sunObject.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            Vector3 center = root.transform.TransformPoint(bounds.center);
            float reach = bounds.extents.magnitude;
            camera.transform.position = center + new Vector3(0.45f, 0.35f, -1f).normalized * (reach / Mathf.Tan(15f * Mathf.Deg2Rad) * 1.05f);
            camera.transform.LookAt(center);
            camera.nearClipPlane = Mathf.Max(0.01f, reach * 0.05f);
            camera.farClipPlane = reach * 12f;
            var target = RenderTexture.GetTemporary(size, size, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            camera.Render();
            RenderTexture.active = target;
            var texture = new Texture2D(size, size, TextureFormat.RGB24, false) { wrapMode = TextureWrapMode.Clamp, name = "TFA_Wiki_" + prefab.name };
            texture.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            texture.Apply();
            RenderTexture.active = null;
            // Nothing drawn (the grass's shader only draws on the terrain): no picture
            Color background = camera.backgroundColor;
            bool drawn = texture.GetPixels().Any(c => Mathf.Abs(c.r - background.r) + Mathf.Abs(c.g - background.g) + Mathf.Abs(c.b - background.b) > 0.08f);
            camera.targetTexture = null;
            RenderTexture.ReleaseTemporary(target);
            Object.DestroyImmediate(sunObject);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(root);
            if (DevHarness.Active)
            {
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(DevHarness.Folder, "wikimodel_" + prefab.name + ".png"), texture.EncodeToPNG());
                AdvancedMod.Log.Msg($"dev: wiki model {prefab.name}: {shown.Count()} renderer(s) [{string.Join(", ", shown.Select(r => r.name + " " + r.sharedMaterial?.shader?.name))}], bounds {bounds.size}, drawn {drawn}, {watch.ElapsedMilliseconds} ms");
            }
            if (!drawn)
            {
                Object.Destroy(texture);
                texture = null;
            }
            Rendered[prefab] = texture;
            return texture;
        }
    }
}

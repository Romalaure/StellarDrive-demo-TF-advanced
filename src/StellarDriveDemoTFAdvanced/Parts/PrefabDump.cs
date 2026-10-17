using System.Linq;
using System.Text;
using Players.Interface.Model;
using Ships.Interface.Settings;
using Ships.Parts.Common;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Parts
{
    /// <summary>Development check (DevPrefabDump): the object tree of the listed parts' prefabs, with components, colliders and interactions.</summary>
    internal static class PrefabDump
    {
        public static void Install()
        {
            DevHarness.Steps.Add((22f, "prefab dump", () =>
            {
                if (string.IsNullOrWhiteSpace(Settings.DevPrefabDump.Value))
                    return;
                var text = new StringBuilder();
                // "player": the other players' avatar prefab (what other players see)
                if (Settings.DevPrefabDump.Value.Contains("player"))
                {
                    foreach (var animator in Resources.FindObjectsOfTypeAll<Players.Visuals.OtherPlayerAnimator>())
                    {
                        text.AppendLine($"=== player avatar {animator.name} scene={animator.gameObject.scene.name}");
                        Dump(animator.transform.root, 0, text);
                        foreach (Renderer r in animator.transform.root.GetComponentsInChildren<Renderer>(true))
                            text.AppendLine($"  RENDERER {r.name} {r.GetType().Name} materials {string.Join(", ", r.sharedMaterials.Select(m => m == null ? "null" : m.name + " (" + m.shader.name + ")"))} bounds {r.bounds.size}");
                    }
                }
                PartSettings[] all = Resources.FindObjectsOfTypeAll<PartSettings>();
                foreach (string token in Settings.DevPrefabDump.Value.Split(','))
                {
                    if (!ushort.TryParse(token.Trim(), out ushort id))
                        continue;
                    PartSettings settings = all.FirstOrDefault(s => s != null && s.id == id);
                    if (settings == null || settings.part == null)
                        continue;
                    text.AppendLine($"=== {id} {settings.name} state={settings.internalStateClass} snap={settings.snappingStyle} bounds={settings.Bounds}");
                    Dump(settings.part.transform, 0, text);
                }
                System.IO.File.WriteAllText(System.IO.Path.Combine(DevHarness.Folder, "prefab_dump.txt"), text.ToString());
                AdvancedMod.Log.Msg("dev: prefab dump written");
            }));
        }

        private static void Dump(Transform t, int depth, StringBuilder text)
        {
            string pad = new string(' ', depth * 2);
            text.AppendLine($"{pad}[{t.name}] layer={t.gameObject.layer} tag={t.tag} pos={t.localPosition:F3} rot={t.localEulerAngles:F0} scale={t.localScale:F2}");
            foreach (Component c in t.GetComponents<Component>())
            {
                if (c == null || c is Transform)
                    continue;
                string extra = "";
                if (c is BoxCollider box)
                    extra = $" center={box.center:F3} size={box.size:F3} trigger={box.isTrigger}";
                if (c is PartInteraction interaction)
                    extra = " interactions=" + string.Join(";", interaction.Interactions.Select(i => i.type + ":" + i.localizedLabel?.TableReference + "/" + i.localizedLabel?.TableEntryReference));
                if (c is MeshRenderer renderer)
                    extra = " mats=" + string.Join(",", renderer.sharedMaterials.Select(m => m != null ? m.name : "null"));
                text.AppendLine($"{pad}  + {c.GetType().FullName}{extra}");
            }
            foreach (Transform child in t)
                Dump(child, depth + 1, text);
        }
    }
}

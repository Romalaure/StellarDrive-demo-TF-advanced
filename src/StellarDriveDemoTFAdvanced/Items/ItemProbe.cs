using System.IO;
using System.Linq;
using System.Text;
using Items.Model;
using Planets.Objects;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Items
{
    /// <summary>Development check: the game's items, their models, and what its rocks drop, written to UserData/TFAdvanced/dev/items.txt.</summary>
    internal static class ItemProbe
    {
        public static void Install()
        {
            DevHarness.Steps.Add((22f, "item probe", Probe));
        }

        private static void Probe()
        {
            var text = new StringBuilder();
            foreach (ItemSettings item in Resources.FindObjectsOfTypeAll<ItemSettings>().OrderBy(i => i.id))
            {
                text.AppendLine($"item {item.id} '{item.itemName}' asset {item.name} mass {item.mass} stack {item.maxStackSize} fluid {item.isFluid} gas {item.isGas} icon {(item.icon != null ? item.icon.name + " " + item.icon.width + "x" + item.icon.height : "none")} tool {(item.toolSettings != null)}");
                if (item.itemObject != null)
                {
                    foreach (Transform t in item.itemObject.GetComponentsInChildren<Transform>(true))
                    {
                        string components = string.Join(",", t.GetComponents<Component>().Select(c => c.GetType().Name));
                        var renderer = t.GetComponent<Renderer>();
                        string materials = renderer != null ? " mats " + string.Join(",", renderer.sharedMaterials.Select(m => m == null ? "null" : m.name + "(" + m.shader.name + ")")) : "";
                        var filter = t.GetComponent<MeshFilter>();
                        string mesh = filter != null && filter.sharedMesh != null ? $" mesh {filter.sharedMesh.name} {filter.sharedMesh.bounds.size}" : "";
                        text.AppendLine($"    {t.name} [{components}]{mesh}{materials} scale {t.localScale}");
                    }
                }
            }
            foreach (MinablePlanetObject minable in Resources.FindObjectsOfTypeAll<MinablePlanetObject>().Where(m => m.gameObject.scene.name == null))
                text.AppendLine($"minable {minable.transform.root.name}/{minable.name} '{minable.Label}' health {minable.MaxHealth} drops {(minable.dropRate.item != null ? minable.dropRate.item.id + " " + minable.dropRate.item.itemName : "none")} x{minable.dropRate.amountMin}-{minable.dropRate.amountMax}");
            foreach (RecyclablePlanetObject recyclable in Resources.FindObjectsOfTypeAll<RecyclablePlanetObject>().Where(m => m.gameObject.scene.name == null))
                text.AppendLine($"recyclable {recyclable.transform.root.name} drops {recyclable.ItemDrop.ItemId} x{recyclable.ItemDrop.Quantity}");
            foreach (var part in Resources.FindObjectsOfTypeAll<Ships.Interface.Settings.PartSettings>().OrderBy(p => p.id))
                text.AppendLine($"part {part.id} {part.name} '{part.fullLabel}' state {part.internalStateClass}");
            Directory.CreateDirectory(DevHarness.Folder);
            File.WriteAllText(Path.Combine(DevHarness.Folder, "items.txt"), text.ToString());
            AdvancedMod.Log.Msg("dev: items written");
        }
    }
}

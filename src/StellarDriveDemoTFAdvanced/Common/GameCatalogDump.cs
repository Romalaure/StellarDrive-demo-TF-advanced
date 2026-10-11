using System.IO;
using System.Linq;
using System.Text;
using MelonLoader.Utils;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Settings;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Common
{
    /// <summary>
    /// Development check run with SelfTest: lists every part of the game (id, asset, label, state,
    /// sockets, size, materials) and every material with its shader, to UserData/TFAdvanced/probe.
    /// </summary>
    internal static class GameCatalogDump
    {
        public static void Run()
        {
            string folder = Path.Combine(MelonEnvironment.UserDataDirectory, "TFAdvanced", "probe");
            Directory.CreateDirectory(folder);
            var parts = new StringBuilder();
            foreach (PartSettings settings in Resources.FindObjectsOfTypeAll<PartSettings>().Where(p => p != null).OrderBy(p => p.id))
            {
                GameObject prefab = settings.part;
                var bounds = prefab != null ? prefab.GetComponent<ShipPartBounds>() : null;
                string sockets = prefab == null ? "" : string.Join(",", prefab.GetComponentsInChildren<SocketObject>(true).Select(s => s.Id + ":" + s.Type));
                string materials = prefab == null ? "" : string.Join(",", prefab.GetComponentsInChildren<Renderer>(true)
                    .SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.name + "(" + m.shader?.name + ")").Distinct());
                parts.AppendLine($"part {settings.id} '{settings.name}' label='{settings.fullLabel}' type={settings.GetType().Name} state={settings.internalStateClass} mass={settings.mass} bounds={bounds?.bounds} sockets=[{sockets}] mats=[{materials}]");
            }
            File.WriteAllText(Path.Combine(folder, "parts.txt"), parts.ToString());
            var materialsText = new StringBuilder();
            foreach (Material m in Resources.FindObjectsOfTypeAll<Material>().Where(m => m != null).OrderBy(m => m.name))
            {
                string texture = m.HasProperty("_MainTex") && m.mainTexture != null ? m.mainTexture.name : (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null ? m.GetTexture("_BaseMap").name : "");
                materialsText.AppendLine($"{m.name} shader={m.shader?.name} tex={texture} queue={m.renderQueue}");
            }
            File.WriteAllText(Path.Combine(folder, "materials.txt"), materialsText.ToString());
            var research = new StringBuilder();
            foreach (var list in Resources.FindObjectsOfTypeAll<Research.Interface.Values.ResearchSettingsList>())
            {
                foreach (var r in list.list.Where(r => r != null))
                {
                    string req = r.requirements == null ? "" : string.Join(",", r.requirements.Where(q => q != null).Select(q => q.id));
                    string unlocks = r.unlockedParts == null ? "" : string.Join(",", r.unlockedParts.Where(q => q != null).Select(q => q.id));
                    string cost = r.resourceCost == null ? "" : string.Join(",", r.resourceCost.Select(c => c.ItemDrop.ToString()));
                    research.AppendLine($"tech {r.id} '{r.techName}' pos={r.positionInTechTree} demo={r.availableInDemo} hidden={r.hideFromTechTree} exclusiveDemo={r.exclusiveToDemo} requires=[{req}] parts=[{unlocks}] features=[{string.Join(",", r.unlockedFeatures ?? new Research.Interface.Model.ResearchUnlockableFeature[0])}] cost=[{cost}]");
                }
            }
            File.WriteAllText(Path.Combine(folder, "research.txt"), research.ToString());
            AdvancedMod.Log.Msg("game catalog written to " + folder);
        }
    }
}

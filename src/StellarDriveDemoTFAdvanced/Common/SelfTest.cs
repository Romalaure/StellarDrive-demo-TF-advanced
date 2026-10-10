using System.Linq;
using System.Text;
using SDModKit.Game;
using Ships.Interface.Model.Parts;
using Ships.Interface.Settings;
using StellarDriveDemoTFAdvanced.Propulsion;
using StellarDriveDemoTFAdvanced.ShipComputer;
using TMPro;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Common
{
    /// <summary>
    /// Development check, off by default (SelfTest in MelonPreferences): a few seconds after the
    /// main menu loads, has SDModKit build every part of this mod and reports what each clone
    /// looks like, and builds one screen canvas outside any ship. Lets a build be checked
    /// without loading a world. Only call when SDModKit is loaded.
    /// </summary>
    internal static class SelfTest
    {
        private static float _runAt = -1f;
        private static bool _done;

        public static void Schedule()
        {
            if (!_done && Settings.SelfTest.Value)
                _runAt = Time.unscaledTime + 8f;
        }

        public static void Update()
        {
            if (_done || _runAt < 0f || Time.unscaledTime < _runAt)
                return;
            _done = true;
            try
            {
                Run();
            }
            catch (System.Exception e)
            {
                AdvancedMod.Log.Error("self test failed: " + e);
            }
        }

        private static void Run()
        {
            var report = new StringBuilder("self test:");
            foreach (ushort id in Parts.PartFactory.Ids.OrderBy(i => i))
            {
                PartSettings settings = CustomParts.Get(id);
                if (settings == null || settings.part == null)
                {
                    report.Append($"\n  {id}: NOT BUILT");
                    continue;
                }
                GameObject prefab = settings.part;
                var bounds = prefab.GetComponent<ShipPartBounds>();
                int renderers = prefab.GetComponentsInChildren<MeshRenderer>(true).Length;
                string sockets = string.Join(",", prefab.GetComponentsInChildren<Ships.Interface.Model.Parts.Common.SocketObject>(true)
                    .Select(s => s.Id + ":" + s.Type).OrderBy(s => s));
                // The build menu thumbnail and the placement preview copy this component: a part without it breaks the build tool
                bool hasVisual = prefab.GetComponentInChildren<Ships.Parts.Common.Model.Reactions.IShipPartVisual>() != null;
                report.Append($"\n  {id} visual={(hasVisual ? "ok" : "MISSING")} '{settings.fullLabel}' {settings.GetType().Name} state={settings.internalStateClass} mass={settings.mass} bounds={bounds?.bounds} renderers={renderers} sockets=[{sockets}]");
                if (prefab.GetComponentInChildren<DashboardVisuals>(true) != null)
                {
                    Transform anchor = prefab.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith(DashboardVisuals.AnchorPrefix));
                    report.Append($" screen='{anchor?.name}'");
                }
                if (EngineCatalog.TryGet(id, out EngineSpec engine))
                    report.Append($" thrust={EngineCatalog.BaseThrust * engine.ThrustMultiplier:0}N fuel={EngineCatalog.BaseFlow * engine.FuelMultiplier:0.000}/s torque={engine.OffCenterThrust}");
            }
            AdvancedMod.Log.Msg(report.ToString());
            TestCanvas();
            MaterialProbe.Run();
            PartSnapshots.Run();
        }

        // A screen outside any ship: shows its idle text, and proves the canvas and font work here
        private static void TestCanvas()
        {
            var root = new GameObject("TFA_SelfTestScreen");
            root.SetActive(false);
            new GameObject(DashboardVisuals.AnchorName(DashboardVisuals.AllSections, 960f, 580f)).transform.SetParent(root.transform, false);
            root.AddComponent<DashboardVisuals>();
            root.SetActive(true);
            TextMeshProUGUI[] texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
            AdvancedMod.Log.Msg($"self test screen: {texts.Length} texts, font={(texts.Length > 0 ? texts[0].font?.name : "none")}, first='{(texts.Length > 1 ? texts[1].text.Replace('\n', '|') : "")}'");
            Object.Destroy(root);
        }
    }
}

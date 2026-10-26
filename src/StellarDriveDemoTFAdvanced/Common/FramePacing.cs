using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Common
{
    internal static class FramePacing
    {
        public static void Update() => DevRenderTests();

        private static bool _devTested;

        // For mod development: whether a frame waits on the graphics card (no shadows, half resolution)
        private static void DevRenderTests()
        {
            if (_devTested || !DevHarness.Active)
                return;
            _devTested = true;
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset urp)
            {
                AdvancedMod.Log.Msg($"dev: render: shadow distance {urp.shadowDistance}, cascades {urp.shadowCascadeCount}, render scale {urp.renderScale}, msaa {urp.msaaSampleCount}");
                if (DevHarness.Off("shadows"))
                    urp.shadowDistance = 0f;
                if (DevHarness.Off("fullres"))
                    urp.renderScale = 0.5f;
            }
        }
    }
}

using HarmonyLib;
using Planets.Terrain.Model;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Worlds
{
    /// <summary>
    /// Each nearby planet keeps its terrain chunks' normal, material and shadow maps in texture
    /// arrays of a fixed size (320 chunks in High quality). Close to high mountains the terrain
    /// needs more chunks than that; the game then skips the extra ones ("Could not allocate memory
    /// for chunk") and they show as holes. The arrays are made <see cref="Settings.TerrainChunkBuffer"/>
    /// times bigger.
    /// </summary>
    [HarmonyPatch(typeof(ChunksGpuData), nameof(ChunksGpuData.Allocate))]
    internal static class TerrainChunkBufferPatch
    {
        private static bool _logged;

        private static void Prefix(ref int bufferSize)
        {
            float factor = Mathf.Clamp(Settings.TerrainChunkBuffer.Value, 1f, 3f);
            int original = bufferSize;
            bufferSize = Mathf.RoundToInt(bufferSize * factor);
            if (!_logged)
            {
                _logged = true;
                AdvancedMod.Log.Msg($"terrain: room for {bufferSize} chunks per planet (game: {original})");
            }
        }
    }
}

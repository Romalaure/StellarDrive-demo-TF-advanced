using System.Linq;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Common
{
    /// <summary>Finds the camera the player sees through, ignoring cameras that render to textures.</summary>
    internal static class MainCamera
    {
        private static Camera _camera;
        private static float _nextLookup;

        public static Camera Get()
        {
            if (_camera != null && _camera.isActiveAndEnabled)
                return _camera;
            if (Time.unscaledTime < _nextLookup)
                return null;
            _nextLookup = Time.unscaledTime + 1f;
            _camera = Camera.main;
            if (_camera == null || _camera.targetTexture != null)
            {
                _camera = Camera.allCameras
                    .Where(c => c.targetTexture == null && c.isActiveAndEnabled)
                    .OrderByDescending(c => c.depth)
                    .FirstOrDefault();
            }
            return _camera;
        }
    }
}

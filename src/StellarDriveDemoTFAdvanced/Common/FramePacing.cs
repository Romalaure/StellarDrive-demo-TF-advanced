using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Common
{
    /// <summary>
    /// Ships turned in jerks. With vertical sync on a 165 Hz screen and the game at about 60 fps,
    /// frames stay on screen 2 or 3 refreshes (12 or 18 ms) one after the other, so a ship turning
    /// at a steady 25°/s moved 0.31° then 0.47° per displayed frame and the world outside jerked.
    /// When most frames need more than one refresh, vertical sync waits that many refreshes
    /// (55 fps on 165 Hz: every frame 18 ms, every turn step equal). Every 45 s it lets frames run
    /// at the player's vertical sync for half a second to see whether the game got faster.
    /// Never under 45 fps, never with vertical sync off; changing the game's option is followed.
    /// </summary>
    internal static class FramePacing
    {
        private const float MinSteadyFps = 45f;
        private const float ProbeEverySeconds = 45f;
        private static readonly int[] Counts = new int[6];
        private static int _applied = -1, _base, _divisor = 1, _beforeProbe = 1, _frames;
        private static float _nextProbe;
        private static bool _probing;

        /// <summary>Every frame.</summary>
        public static void Update()
        {
            int current = QualitySettings.vSyncCount;
            if (current != _applied)
            {
                // The game (its options) set vertical sync: that is the player's choice
                _base = _applied = current;
                _divisor = 1;
                Restart(false);
            }
            float refresh = (float)Screen.currentResolution.refreshRateRatio.value;
            if (!Settings.SteadyFrameRate.Value || _base <= 0 || refresh < 20f || DevHarness.Off("pacing"))
            {
                if (_divisor != 1)
                    Apply(1);
                return;
            }
            float vblank = _base / refresh;
            int n = Mathf.Clamp(Mathf.RoundToInt(Time.unscaledDeltaTime / vblank), 1, Counts.Length - 1);
            Counts[n]++;
            _frames++;
            if (_frames < (_probing ? 30 : 120))
                return;
            int wanted = _divisor;
            if (_probing || Fits(_divisor) < 0.9f)
            {
                // Fewest refreshes that 90 % of the frames fit in, if that rate stays playable
                wanted = 1;
                while (wanted < 4 && Fits(wanted) < 0.9f)
                    wanted++;
                while (wanted > 1 && refresh / (_base * wanted) < MinSteadyFps)
                    wanted--;
            }
            if (!_probing && wanted > 1 && Time.unscaledTime >= _nextProbe)
            {
                _beforeProbe = _divisor;
                Apply(1, true);
                Restart(true);
                return;
            }
            if (wanted != _divisor)
                Apply(wanted, _probing && wanted == _beforeProbe);
            Restart(false);
        }

        private static float Fits(int refreshes)
        {
            int fit = 0;
            for (int i = 1; i <= refreshes && i < Counts.Length; i++)
                fit += Counts[i];
            return _frames > 0 ? fit / (float)_frames : 1f;
        }

        private static void Apply(int divisor, bool quiet = false)
        {
            if (divisor != _divisor && !quiet)
                AdvancedMod.Log.Msg($"frame pacing: {(divisor > 1 ? $"one frame every {divisor} screen refreshes ({Screen.currentResolution.refreshRateRatio.value / (_base * divisor):0} fps, steady)" : "back to the game's vertical sync")}");
            _divisor = divisor;
            _nextProbe = Time.unscaledTime + ProbeEverySeconds;
            _applied = QualitySettings.vSyncCount = Mathf.Min(4, _base * divisor);
        }

        private static void Restart(bool probing)
        {
            System.Array.Clear(Counts, 0, Counts.Length);
            _frames = 0;
            _probing = probing;
        }
    }
}

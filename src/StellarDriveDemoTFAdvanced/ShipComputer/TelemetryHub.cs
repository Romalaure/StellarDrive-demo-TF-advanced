using System.Collections.Generic;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.ShipComputer
{
    /// <summary>
    /// One reading per ship, shared by every dashboard mounted on it: a ship with five screens is
    /// read once per refresh, not five times. Ships nobody looks at are forgotten.
    /// </summary>
    internal static class TelemetryHub
    {
        private const float ForgetAfter = 10f;

        internal sealed class Reading
        {
            public readonly TelemetrySampler Sampler = new TelemetrySampler();
            public readonly TelemetrySnapshot Snapshot = new TelemetrySnapshot();
            public readonly List<DiagnosticMessage> Messages = new List<DiagnosticMessage>();
            public float NextSample;
            public float LastUsed;
            public float SampleMs;
            /// <summary>Increments on every new reading, so screens redraw only when it changes.</summary>
            public int Version;
        }

        private static readonly Dictionary<uint, Reading> Readings = new Dictionary<uint, Reading>();
        private static readonly List<uint> Stale = new List<uint>();
        private static readonly System.Diagnostics.Stopwatch Watch = new System.Diagnostics.Stopwatch();
        private static float _nextCleanup;

        public static float Rate => Mathf.Clamp(Settings.ComputerRefreshRate.Value, 1f, 20f);

        /// <summary>The latest reading of the ship tree with this root, taken now if the last one is older than the refresh period.</summary>
        public static Reading Get(uint rootId)
        {
            float now = Time.unscaledTime;
            if (!Readings.TryGetValue(rootId, out Reading reading))
            {
                reading = new Reading();
                Readings[rootId] = reading;
            }
            reading.LastUsed = now;
            if (now >= reading.NextSample)
            {
                reading.NextSample = now + 1f / Rate;
                Watch.Restart();
                try
                {
                    reading.Sampler.Sample(reading.Snapshot, rootId);
                    Diagnostics.Evaluate(reading.Snapshot, reading.Messages);
                }
                catch (System.Exception e)
                {
                    ErrorLog.Report("ship computer reading", e);
                    reading.NextSample = now + 1f;
                }
                Watch.Stop();
                reading.SampleMs = (float)Watch.Elapsed.TotalMilliseconds;
                reading.Version++;
            }
            Cleanup(now);
            return reading;
        }

        public static void Clear()
        {
            Readings.Clear();
        }

        private static void Cleanup(float now)
        {
            if (now < _nextCleanup)
                return;
            _nextCleanup = now + ForgetAfter;
            Stale.Clear();
            foreach (KeyValuePair<uint, Reading> pair in Readings)
            {
                if (now - pair.Value.LastUsed > ForgetAfter)
                    Stale.Add(pair.Key);
            }
            foreach (uint id in Stale)
                Readings.Remove(id);
        }
    }
}

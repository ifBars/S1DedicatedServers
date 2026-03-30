using UnityEngine;

namespace DedicatedServerMod.Server.WebPanel
{
    /// <summary>
    /// Samples runtime frame pacing on the Unity main thread for browser-panel diagnostics.
    /// </summary>
    internal sealed class WebPanelPerformanceMetrics
    {
        private const float SampleWindowSeconds = 0.5f;
        private const float MinimumSampleSeconds = 0.0001f;
        private const double MinimumFramesPerSecond = 0.0001d;

        private int _sampledFrames;
        private float _sampledSeconds;

        public double FramesPerSecond { get; private set; }

        public double FrameTimeMilliseconds { get; private set; }

        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f)
            {
                return;
            }

            _sampledFrames++;
            _sampledSeconds += deltaSeconds;

            if (_sampledSeconds < SampleWindowSeconds)
            {
                return;
            }

            double framesPerSecond = _sampledFrames / Mathf.Max(_sampledSeconds, MinimumSampleSeconds);
            FramesPerSecond = framesPerSecond;
            FrameTimeMilliseconds = 1000d / Math.Max(framesPerSecond, MinimumFramesPerSecond);

            _sampledFrames = 0;
            _sampledSeconds = 0f;
        }
    }
}

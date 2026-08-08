using System.Diagnostics;

namespace DedicatedServerMod.Server.Core
{
    /// <summary>
    /// Limits the dedicated-server main loop when Unity's headless player ignores its target frame rate.
    /// </summary>
    internal sealed class ServerFramePacer
    {
        private readonly Stopwatch _frameInterval = Stopwatch.StartNew();
        private int _targetFrameRate = -1;

        internal void SetTargetFrameRate(int targetFrameRate)
        {
            _targetFrameRate = targetFrameRate;
            _frameInterval.Restart();
        }

        internal void WaitForNextFrame()
        {
            if (_targetFrameRate <= 0)
            {
                return;
            }

            double targetFrameMilliseconds = 1000d / _targetFrameRate;
            double remainingMilliseconds = targetFrameMilliseconds - _frameInterval.Elapsed.TotalMilliseconds;
            if (remainingMilliseconds > 0d)
            {
                Thread.Sleep((int)Math.Ceiling(remainingMilliseconds));
            }

            _frameInterval.Restart();
        }
    }
}

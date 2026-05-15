using UnityEngine;

namespace DedicatedServerMod.Utils
{
    /// <summary>
    /// Records timestamped milestones during the client join sequence.
    /// Produces a compact one-line-per-step log and a final summary.
    /// </summary>
    /// <remarks>
    /// This helper is intended for diagnostics around authentication, mod verification, loading-screen
    /// gating, and player-ready transitions. It uses Unity realtime so elapsed values continue to make
    /// sense even when gameplay time scale changes during startup.
    /// </remarks>
    public class JoinTimeline
    {
        private readonly float startTime = Time.realtimeSinceStartup;
        private readonly List<Entry> entries = new List<Entry>();

        private struct Entry
        {
            public string Step;
            public string Detail;
            public float Elapsed;
        }

        /// <summary>
        /// Records a successful join milestone.
        /// </summary>
        /// <param name="step">Short milestone name.</param>
        /// <param name="detail">Optional detail appended to the verbose log entry.</param>
        public void Mark(string step, string detail = null)
        {
            float elapsed = Time.realtimeSinceStartup - startTime;
            entries.Add(new Entry { Step = step, Detail = detail, Elapsed = elapsed });

            string msg = detail != null
                ? $"[JoinTimeline] +{elapsed:F2}s {step} ({detail})"
                : $"[JoinTimeline] +{elapsed:F2}s {step}";
            DebugLog.Verbose(msg);
        }

        /// <summary>
        /// Records a failed join milestone and writes an error log entry.
        /// </summary>
        /// <param name="step">Short milestone name.</param>
        /// <param name="error">Failure details to include in diagnostics.</param>
        public void MarkError(string step, string error)
        {
            float elapsed = Time.realtimeSinceStartup - startTime;
            entries.Add(new Entry { Step = step, Detail = $"ERROR: {error}", Elapsed = elapsed });
            DebugLog.Error($"[JoinTimeline] +{elapsed:F2}s {step} FAILED: {error}");
        }

        /// <summary>
        /// Writes a compact summary of the recorded join timeline to verbose logs.
        /// </summary>
        public void PrintSummary()
        {
            float total = Time.realtimeSinceStartup - startTime;
            DebugLog.Verbose($"[JoinTimeline] === Join complete in {total:F2}s ({entries.Count} steps) ===");
        }

        /// <summary>
        /// Returns the terminal state description for diagnostics on failure.
        /// </summary>
        /// <returns>A human-readable description of the last recorded milestone and elapsed time.</returns>
        public string GetTerminalState()
        {
            if (entries.Count == 0)
                return "No steps recorded";

            var last = entries[entries.Count - 1];
            float total = Time.realtimeSinceStartup - startTime;
            return $"Last step: {last.Step} at +{last.Elapsed:F2}s, total elapsed: {total:F2}s";
        }
    }
}

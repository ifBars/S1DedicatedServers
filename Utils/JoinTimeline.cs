using MelonLoader;
using System.Collections.Generic;
using UnityEngine;

namespace DedicatedServerMod.Utils
{
    /// <summary>
    /// Records timestamped milestones during the client join sequence.
    /// Produces a compact one-line-per-step log and a final summary.
    /// </summary>
    public class JoinTimeline
    {
        private readonly MelonLogger.Instance logger;
        private readonly float startTime;
        private readonly List<Entry> entries = new List<Entry>();

        private struct Entry
        {
            public string Step;
            public string Detail;
            public float Elapsed;
        }

        public JoinTimeline(MelonLogger.Instance logger)
        {
            this.logger = logger;
            startTime = Time.realtimeSinceStartup;
        }

        public void Mark(string step, string detail = null)
        {
            float elapsed = Time.realtimeSinceStartup - startTime;
            entries.Add(new Entry { Step = step, Detail = detail, Elapsed = elapsed });

            string msg = detail != null
                ? $"[JoinTimeline] +{elapsed:F2}s {step} ({detail})"
                : $"[JoinTimeline] +{elapsed:F2}s {step}";
            logger.Msg(msg);
        }

        public void MarkError(string step, string error)
        {
            float elapsed = Time.realtimeSinceStartup - startTime;
            entries.Add(new Entry { Step = step, Detail = $"ERROR: {error}", Elapsed = elapsed });
            logger.Error($"[JoinTimeline] +{elapsed:F2}s {step} FAILED: {error}");
        }

        public void PrintSummary()
        {
            float total = Time.realtimeSinceStartup - startTime;
            logger.Msg($"[JoinTimeline] === Join complete in {total:F2}s ({entries.Count} steps) ===");
        }

        /// <summary>
        /// Returns the terminal state description for diagnostics on failure.
        /// </summary>
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

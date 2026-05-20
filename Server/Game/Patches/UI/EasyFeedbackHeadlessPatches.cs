using DedicatedServerMod.Server.Game.Patches.Common;
using HarmonyLib;
using UnityEngine;

namespace DedicatedServerMod.Server.Game.Patches.UI
{
    /// <summary>
    /// Disables the bundled feedback form's in-memory log collector on dedicated headless servers.
    /// MelonLoader still writes logs to disk; this only prevents a UI feedback buffer from retaining them.
    /// </summary>
    internal static class EasyFeedbackHeadlessPatches
    {
        /// <summary>
        /// Prevents EasyFeedback from appending server logs to its feedback form buffer.
        /// </summary>
        /// <param name="logString">The Unity log message.</param>
        /// <param name="stackTrace">The Unity stack trace.</param>
        /// <param name="logType">The Unity log type.</param>
        /// <returns>False on dedicated headless servers to skip the original collector.</returns>
        public static bool HandleLogPrefix(string logString, string stackTrace, LogType logType)
        {
            return !DedicatedServerPatchCommon.IsDedicatedHeadlessServer();
        }
    }
}

using DedicatedServerMod.Server.Core;
using DedicatedServerMod.Shared.Configuration;
using DedicatedServerMod.Utils;

namespace DedicatedServerMod.Server.Game.Patches.Common
{
    /// <summary>
    /// Preserves dedicated-server frame pacing after Schedule I applies player-facing display preferences.
    /// </summary>
    internal static class DisplaySettingsPerformancePatches
    {
        /// <summary>
        /// Reapplies server-owned performance settings after the game's display settings complete.
        /// </summary>
        public static void ReapplyDedicatedServerPerformanceSettings()
        {
            try
            {
                ServerRuntimeConfigurationApplier.ApplyPerformanceSettings(
                    ServerConfig.Instance,
                    "Schedule I display settings");
            }
            catch (Exception ex)
            {
                DebugLog.Error("Failed to reapply dedicated-server performance settings after Schedule I display settings", ex);
            }
        }
    }
}

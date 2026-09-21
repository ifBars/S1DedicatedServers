#if CLIENT
using DedicatedServerMod.Client.Managers;
#endif

namespace DedicatedServerMod.Utils
{
    /// <summary>
    /// Identifies whether dedicated-server behavior should be active in the current build and session.
    /// </summary>
    internal static class DedicatedRuntimeContext
    {
        internal static bool IsActive
        {
            get
            {
#if SERVER
                return ShouldApply(isServerBuild: true, isDedicatedClientSession: false);
#elif CLIENT
                return ShouldApply(
                    isServerBuild: false,
                    isDedicatedClientSession: ClientConnectionManager.IsDedicatedServerSessionActive);
#else
                return false;
#endif
            }
        }

        internal static bool ShouldApply(bool isServerBuild, bool isDedicatedClientSession)
        {
            return isServerBuild || isDedicatedClientSession;
        }
    }
}

using HarmonyLib;
using DedicatedServerMod.Server.Game.Patches.Common;
#if IL2CPP
using ManagementWorldspaceCanvasType = Il2CppScheduleOne.UI.Management.ManagementWorldspaceCanvas;
#else
using ManagementWorldspaceCanvasType = ScheduleOne.UI.Management.ManagementWorldspaceCanvas;
#endif

namespace DedicatedServerMod.Server.Game.Patches.UI
{
    /// <summary>
    /// Skips management worldspace canvas updates on dedicated headless servers.
    /// </summary>
    [HarmonyPatch(typeof(ManagementWorldspaceCanvasType), "Update")]
    internal static class ManagementWorldspaceCanvasUpdatePatches
    {
        private static bool Prefix()
        {
            return !DedicatedServerPatchCommon.IsDedicatedHeadlessServer();
        }
    }

    /// <summary>
    /// Skips management worldspace canvas late updates on dedicated headless servers.
    /// </summary>
    [HarmonyPatch(typeof(ManagementWorldspaceCanvasType), "LateUpdate")]
    internal static class ManagementWorldspaceCanvasLateUpdatePatches
    {
        private static bool Prefix()
        {
            return !DedicatedServerPatchCommon.IsDedicatedHeadlessServer();
        }
    }
}

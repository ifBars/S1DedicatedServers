using HarmonyLib;
using DedicatedServerMod.Server.Game.Patches.Common;
#if IL2CPP
using TmpUpdateManagerType = Il2CppTMPro.TMP_UpdateManager;
#else
using TmpUpdateManagerType = TMPro.TMP_UpdateManager;
#endif

namespace DedicatedServerMod.Server.Game.Patches.UI
{
    /// <summary>
    /// Skips global TextMeshPro rebuild work on dedicated headless servers.
    /// </summary>
    [HarmonyPatch(typeof(TmpUpdateManagerType), "DoRebuilds")]
    internal static class TmpUpdateManagerDoRebuildsPatches
    {
        private static bool Prefix()
        {
            return !DedicatedServerPatchCommon.IsDedicatedHeadlessServer();
        }
    }
}

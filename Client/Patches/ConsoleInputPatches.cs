#if IL2CPP && CLIENT
using DedicatedServerMod.Client.Managers;
using DedicatedServerMod.Utils;
using HarmonyLib;
using Il2CppFishNet;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.UI;

namespace DedicatedServerMod.Client.Patches
{
    /// <summary>
    /// Applies dedicated-client console permissions at the IL2CPP input entry point.
    /// The native Update path can bypass the patched IsConsoleEnabled getter.
    /// </summary>
    [HarmonyPatch(typeof(ConsoleUI), "Update")]
    internal static class ConsoleInputPatches
    {
        [HarmonyPrefix]
        internal static bool UpdatePrefix(ConsoleUI __instance)
        {
            if (!DedicatedRuntimeContext.IsActive || !InstanceFinder.IsClient || InstanceFinder.IsHost)
            {
                return true;
            }

            if (__instance.canvas == null)
            {
                return true;
            }

            var toggle = __instance.ToggleConsoleReference;
            var pauseMenu = Singleton<PauseMenu>.Instance;
            if (toggle != null && toggle.action != null && toggle.action.WasPerformedThisFrame() &&
                pauseMenu != null && !pauseMenu.IsPaused && AdminStatusManager.CanOpenConsole())
            {
                __instance.SetIsOpen(!__instance.canvas.enabled);
            }

            if (__instance.canvas.enabled)
            {
                __instance.UpdateCommandHistory();
            }

            return false;
        }
    }
}
#endif

using HarmonyLib;
using DedicatedServerMod.Utils;
#if IL2CPP
using Il2CppScheduleOne.UI;
#else
using ScheduleOne.UI;
#endif

namespace DedicatedServerMod.Client.Patches
{
    /// <summary>
    /// Suppresses the internal organisation name that the native pause menu displays to dedicated-server clients.
    /// </summary>
    /// <remarks>
    /// The game refreshes this label from the authoritative organisation name. That value remains unchanged because
    /// it is part of game and persistence state; this patch changes only the dedicated-client presentation.
    /// </remarks>
    [HarmonyPatch]
    internal static class PauseMenuPatches
    {
        internal static void Initialize()
        {
            DebugLog.StartupDebug("Pause menu patches initialized");
        }

        /// <summary>
        /// Prevents native organisation-name refreshes from exposing the dedicated server's internal save name.
        /// </summary>
        [HarmonyPatch(typeof(PauseMenu), "UpdateCartelName")]
        [HarmonyPrefix]
        private static bool UpdateCartelNamePrefix(PauseMenu __instance)
        {
            if (!IsDedicatedServerSession())
            {
                return true;
            }

            HideCartelName(__instance);
            return false;
        }

        /// <summary>
        /// Clears a label refreshed before dedicated-server join verification completed when the pause menu opens.
        /// </summary>
        [HarmonyPatch(typeof(PauseMenu), nameof(PauseMenu.Pause))]
        [HarmonyPostfix]
        private static void PausePostfix(PauseMenu __instance)
        {
            if (IsDedicatedServerSession())
            {
                HideCartelName(__instance);
            }
        }

        private static bool IsDedicatedServerSession()
        {
            return Core.ClientBootstrap.Instance?.ConnectionManager?.IsConnectedToDedicatedServer ?? false;
        }

        private static void HideCartelName(PauseMenu pauseMenu)
        {
            if (pauseMenu?.CartelNameLabel != null)
            {
                pauseMenu.CartelNameLabel.text = string.Empty;
            }
        }
    }
}

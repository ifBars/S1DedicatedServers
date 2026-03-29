using HarmonyLib;
using MelonLoader;
#if IL2CPP
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.UI;
#else
using ScheduleOne.Persistence;
using ScheduleOne.UI;
#endif

namespace DedicatedServerMod.Client.Patches
{
    /// <summary>
    /// Holds the loading screen open until dedicated-server authentication and mod verification complete.
    /// </summary>
    [HarmonyPatch]
    internal static class LoadingScreenPatches
    {
        private static MelonLogger.Instance _logger;

        public static void Initialize(MelonLogger.Instance logger)
        {
            _logger = logger;
            _logger.Msg("LoadingScreen patches initialized (using attribute-based patching)");
        }

        [HarmonyPatch(typeof(LoadManager), nameof(LoadManager.GetLoadStatusText))]
        [HarmonyPostfix]
        private static void GetLoadStatusTextPostfix(ref string __result)
        {
            if (ShouldHoldLoadingScreen())
            {
                __result = "Verifying server requirements...";
            }
        }

        [HarmonyPatch(typeof(LoadingScreen), nameof(LoadingScreen.Close))]
        [HarmonyPrefix]
        private static bool ClosePrefix()
        {
            bool shouldHold = ShouldHoldLoadingScreen();
            if (shouldHold)
            {
                _logger?.Msg("Holding loading screen open until dedicated join verification completes");
            }

            return !shouldHold;
        }

        private static bool ShouldHoldLoadingScreen()
        {
            return Core.ClientBootstrap.Instance?.ConnectionManager?.ShouldBlockLoadingScreenClose ?? false;
        }
    }
}

using HarmonyLib;
#if IL2CPP
using Il2CppScheduleOne.DevUtilities;
#else
using ScheduleOne.DevUtilities;
#endif

namespace DedicatedServerMod.Server.Game
{
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.IsTutorial), MethodType.Getter)]
    internal static class GameManagerPatches
    {
        private static bool Prefix(ref bool __result)
        {
            __result = false;
            return false;
        }
    }
}

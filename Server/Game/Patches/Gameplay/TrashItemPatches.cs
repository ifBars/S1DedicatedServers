using HarmonyLib;
using DedicatedServerMod.Utils;
#if IL2CPP
using Il2CppFishNet;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.PlayerScripts;
using TrashItemType = Il2CppScheduleOne.Trash.TrashItem;
#else
using FishNet;
using ScheduleOne.DevUtilities;
using ScheduleOne.PlayerScripts;
using TrashItemType = ScheduleOne.Trash.TrashItem;
#endif

namespace DedicatedServerMod.Server.Game.Patches.Gameplay
{
    /// <summary>
    /// Prevents shutdown-time null references in trash minute callbacks when the local player
    /// movement singleton has already been torn down but staggered minute listeners are still draining.
    /// Dedicated servers still keep the native below-world cleanup behavior.
    /// </summary>
    [HarmonyPatch(typeof(TrashItemType), "MinPass")]
    internal static class TrashItemMinPassPatches
    {
        private static bool Prefix(TrashItemType __instance)
        {
            if (!InstanceFinder.IsServer || PlayerSingleton<PlayerMovement>.InstanceExists)
            {
                return true;
            }

            if (__instance == null || __instance.transform == null)
            {
                return false;
            }

            if (__instance.transform.position.y < -100f)
            {
                DebugLog.Warning("Trash item fell below the world. Destroying.");
                __instance.DestroyTrash();
            }

            return false;
        }
    }
}

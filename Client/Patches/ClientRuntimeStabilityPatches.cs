using System;
using HarmonyLib;
using DedicatedServerMod.Utils;
#if IL2CPP
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.PlayerScripts;
using MessageType = Il2CppScheduleOne.Messaging.Message;
using MSGConversationType = Il2CppScheduleOne.Messaging.MSGConversation;
using TrashItemType = Il2CppScheduleOne.Trash.TrashItem;
#else
using ScheduleOne.DevUtilities;
using ScheduleOne.PlayerScripts;
using MessageType = ScheduleOne.Messaging.Message;
using MSGConversationType = ScheduleOne.Messaging.MSGConversation;
using TrashItemType = ScheduleOne.Trash.TrashItem;
#endif

namespace DedicatedServerMod.Client.Patches
{
    /// <summary>
    /// Prevents native client minute-pass callbacks from dereferencing player movement after
    /// dedicated-server cleanup has started or before the local player singleton is rebuilt.
    /// </summary>
    [HarmonyPatch(typeof(TrashItemType), "MinPass")]
    internal static class TrashItemMinPassClientPatches
    {
        private static bool Prefix(TrashItemType __instance)
        {
            if (__instance == null || __instance.transform == null)
            {
                return false;
            }

            return PlayerSingleton<PlayerMovement>.InstanceExists;
        }
    }

    /// <summary>
    /// Suppresses stale message rollout coroutines after the phone UI has been destroyed.
    /// </summary>
    [HarmonyPatch(typeof(MSGConversationType), "RenderMessage")]
    internal static class MSGConversationRenderMessageClientPatches
    {
        private static Exception Finalizer(MSGConversationType __instance, MessageType m, Exception __exception)
        {
            if (__exception == null)
            {
                return null;
            }

            if (__exception is NullReferenceException)
            {
                DebugLog.Warning("Suppressed stale MSGConversation.RenderMessage after client cleanup.");
                return null;
            }

            return __exception;
        }
    }
}

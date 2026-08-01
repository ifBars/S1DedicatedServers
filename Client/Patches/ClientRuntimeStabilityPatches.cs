using System;
using System.Reflection;
using HarmonyLib;
using DedicatedServerMod.Utils;
#if IL2CPP
using ConfigurationServiceNetworkerType = Il2CppScheduleOne.Configuration.ConfigurationServiceNetworker;
using Il2CppScheduleOne.DevUtilities;
using NPCActionType = Il2CppScheduleOne.NPCs.Schedules.NPCAction;
using NPCEventStayInBuildingType = Il2CppScheduleOne.NPCs.Schedules.NPCEvent_StayInBuilding;
using NPCType = Il2CppScheduleOne.NPCs.NPC;
using Il2CppScheduleOne.PlayerScripts;
using MessageType = Il2CppScheduleOne.Messaging.Message;
using MSGConversationType = Il2CppScheduleOne.Messaging.MSGConversation;
using TrashItemType = Il2CppScheduleOne.Trash.TrashItem;
#else
using ConfigurationServiceNetworkerType = ScheduleOne.Configuration.ConfigurationServiceNetworker;
using ScheduleOne.DevUtilities;
using NPCActionType = ScheduleOne.NPCs.Schedules.NPCAction;
using NPCEventStayInBuildingType = ScheduleOne.NPCs.Schedules.NPCEvent_StayInBuilding;
using NPCType = ScheduleOne.NPCs.NPC;
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
    [HarmonyPatch]
    internal static class TrashItemMinPassClientPatches
    {
        [HarmonyPrepare]
        private static bool Prepare()
        {
            return ResolveTargetMethod() != null;
        }

        [HarmonyTargetMethod]
        private static MethodBase TargetMethod()
        {
            return ResolveTargetMethod();
        }

        private static bool Prefix(TrashItemType __instance)
        {
            if (__instance == null || __instance.transform == null)
            {
                return false;
            }

            return PlayerSingleton<PlayerMovement>.InstanceExists;
        }

        private static MethodBase ResolveTargetMethod()
        {
            return AccessTools.Method(typeof(TrashItemType), "MinPass")
                ?? AccessTools.Method(typeof(TrashItemType), "OnTick");
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

    /// <summary>
    /// Skips stale NPC building-entry animation RPCs when reconnect scene cleanup has left
    /// the client-side NPC action without the object graph required by its coroutine.
    /// </summary>
    [HarmonyPatch(typeof(NPCEventStayInBuildingType), "RpcLogic___PlayEnterAnimation_2166136261")]
    internal static class NPCEventStayInBuildingEnterAnimationClientPatches
    {
        private static readonly System.Reflection.FieldInfo NpcField =
            AccessTools.Field(typeof(NPCActionType), "npc");

        [HarmonyPrepare]
        private static bool Prepare()
        {
            return NpcField != null;
        }

        private static bool Prefix(NPCEventStayInBuildingType __instance)
        {
            try
            {
                if (__instance == null || !CoroutineService.InstanceExists)
                {
                    return false;
                }

                var npc = NpcField?.GetValue(__instance) as NPCType;
                if (npc == null ||
                    npc.Movement == null ||
                    npc.Avatar == null ||
                    npc.Avatar.Animation == null)
                {
                    DebugLog.Warning("Suppressed stale NPCEvent_StayInBuilding enter animation during client reconnect cleanup.");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                DebugLog.Warning($"Suppressed NPCEvent_StayInBuilding enter animation after validation failed: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Avoids client shutdown cleanup calling server-only configuration listener removal
    /// when the configuration singleton has already been destroyed.
    /// </summary>
    [HarmonyPatch(typeof(ConfigurationServiceNetworkerType), "OnDestroy")]
    internal static class ConfigurationServiceNetworkerOnDestroyClientPatches
    {
        private static bool Prefix(ConfigurationServiceNetworkerType __instance)
        {
            return __instance != null && __instance.IsServerInitialized;
        }
    }
}

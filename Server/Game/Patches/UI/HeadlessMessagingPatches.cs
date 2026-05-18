using HarmonyLib;
using DedicatedServerMod.Server.Game.Patches.Common;
using UnityEngine;
#if IL2CPP
using MessageChainListType = Il2CppSystem.Collections.Generic.List<Il2CppScheduleOne.UI.Phone.Messages.MessageChain>;
using MessageChainType = Il2CppScheduleOne.UI.Phone.Messages.MessageChain;
using MessageListType = Il2CppSystem.Collections.Generic.List<Il2CppScheduleOne.Messaging.Message>;
using MessageType = Il2CppScheduleOne.Messaging.Message;
using MSGConversationType = Il2CppScheduleOne.Messaging.MSGConversation;
using ResponseListType = Il2CppSystem.Collections.Generic.List<Il2CppScheduleOne.Messaging.Response>;
using ResponseType = Il2CppScheduleOne.Messaging.Response;
#else
using System.Collections.Generic;
using MessageChainListType = System.Collections.Generic.List<ScheduleOne.UI.Phone.Messages.MessageChain>;
using MessageChainType = ScheduleOne.UI.Phone.Messages.MessageChain;
using MessageListType = System.Collections.Generic.List<ScheduleOne.Messaging.Message>;
using MessageType = ScheduleOne.Messaging.Message;
using MSGConversationType = ScheduleOne.Messaging.MSGConversation;
using ResponseListType = System.Collections.Generic.List<ScheduleOne.Messaging.Response>;
using ResponseType = ScheduleOne.Messaging.Response;
#endif

namespace DedicatedServerMod.Server.Game.Patches.UI
{
    /// <summary>
    /// Keeps vanilla NPC text-message RPC handling authoritative on headless servers
    /// without constructing or updating the client-only phone UI.
    /// </summary>
    internal static class HeadlessMessagingPatchState
    {
        private const int MaxMessageHistory = 10;

        internal static bool ShouldBypassPhoneUi()
        {
            return DedicatedServerPatchCommon.IsDedicatedHeadlessServer();
        }

        internal static void ApplyIncomingMessage(MSGConversationType conversation, MessageType message)
        {
            if (conversation == null || message == null)
            {
                return;
            }

            if (message.messageId == -1)
            {
                message.messageId = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            }

            MessageListType history = conversation.messageHistory;
            if (ContainsMessage(history, message.messageId))
            {
                return;
            }

            history.Add(message);
            TrimMessageHistory(history);
            conversation.HasChanged = true;
        }

        internal static void ApplyIncomingMessageChain(MSGConversationType conversation, MessageChainType chain)
        {
            if (conversation == null || chain == null)
            {
                return;
            }

            if (chain.id == -1)
            {
                chain.id = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            }

            MessageChainListType chainHistory = conversation.messageChainHistory;
            if (ContainsMessageChain(chainHistory, chain.id))
            {
                return;
            }

            chainHistory.Add(chain);
            MessageListType messageHistory = conversation.messageHistory;
            for (int i = 0; i < chain.Messages.Count; i++)
            {
                bool isLastMessage = i == chain.Messages.Count - 1;
                messageHistory.Add(new MessageType(chain.Messages[i], MessageType.ESenderType.Other, isLastMessage));
                TrimMessageHistory(messageHistory);
            }

            conversation.HasChanged = true;
        }

        internal static void ApplyResponses(MSGConversationType conversation, ResponseListType responses)
        {
            if (conversation == null)
            {
                return;
            }

            conversation.currentResponses = responses ?? new ResponseListType();
            conversation.HasChanged = true;
        }

        internal static void ClearResponses(MSGConversationType conversation)
        {
            if (conversation == null)
            {
                return;
            }

            conversation.currentResponses.Clear();
            conversation.HasChanged = true;
        }

        internal static void ApplyResponseChosen(MSGConversationType conversation, ResponseType response)
        {
            if (conversation == null || response == null || conversation.currentResponses.Count <= 0)
            {
                return;
            }

            if (!response.disableDefaultResponseBehaviour)
            {
                ClearResponses(conversation);
            }

            response.callback?.Invoke();
            conversation.HasChanged = true;
        }

        private static bool ContainsMessage(MessageListType messages, int messageId)
        {
            for (int i = 0; i < messages.Count; i++)
            {
                if (messages[i].messageId == messageId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsMessageChain(MessageChainListType chains, int chainId)
        {
            for (int i = 0; i < chains.Count; i++)
            {
                if (chains[i].id == chainId)
                {
                    return true;
                }
            }

            return false;
        }

        private static void TrimMessageHistory(MessageListType messages)
        {
            while (messages.Count > MaxMessageHistory)
            {
                messages.RemoveAt(0);
            }
        }
    }

    /// <summary>
    /// Prevents server-side RPC handling from creating phone conversation UI.
    /// </summary>
    [HarmonyPatch(typeof(MSGConversationType), "EnsureUIExists")]
    internal static class MSGConversationEnsureUiHeadlessPatches
    {
        private static bool Prefix()
        {
            return !HeadlessMessagingPatchState.ShouldBypassPhoneUi();
        }
    }

    /// <summary>
    /// Applies incoming network messages to server state without rendering phone bubbles.
    /// </summary>
    [HarmonyPatch(typeof(MSGConversationType), "SendMessage")]
    internal static class MSGConversationSendMessageHeadlessPatches
    {
        private static bool Prefix(MSGConversationType __instance, MessageType message, bool network)
        {
            if (!HeadlessMessagingPatchState.ShouldBypassPhoneUi() || network)
            {
                return true;
            }

            HeadlessMessagingPatchState.ApplyIncomingMessage(__instance, message);
            return false;
        }
    }

    /// <summary>
    /// Applies incoming network message chains to server state without starting UI rollout coroutines.
    /// </summary>
    [HarmonyPatch(typeof(MSGConversationType), "SendMessageChain")]
    internal static class MSGConversationSendMessageChainHeadlessPatches
    {
        private static bool Prefix(MSGConversationType __instance, MessageChainType messages, bool network)
        {
            if (!HeadlessMessagingPatchState.ShouldBypassPhoneUi() || network)
            {
                return true;
            }

            HeadlessMessagingPatchState.ApplyIncomingMessageChain(__instance, messages);
            return false;
        }
    }

    /// <summary>
    /// Stores active responses on the server without creating response buttons.
    /// </summary>
    [HarmonyPatch(typeof(MSGConversationType), "ShowResponses")]
    internal static class MSGConversationShowResponsesHeadlessPatches
    {
        private static bool Prefix(MSGConversationType __instance, ResponseListType _responses, bool network)
        {
            if (!HeadlessMessagingPatchState.ShouldBypassPhoneUi() || network)
            {
                return true;
            }

            HeadlessMessagingPatchState.ApplyResponses(__instance, _responses);
            return false;
        }
    }

    /// <summary>
    /// Clears active response state without touching response UI containers.
    /// </summary>
    [HarmonyPatch(typeof(MSGConversationType), "ClearResponses")]
    internal static class MSGConversationClearResponsesHeadlessPatches
    {
        private static bool Prefix(MSGConversationType __instance, bool network)
        {
            if (!HeadlessMessagingPatchState.ShouldBypassPhoneUi() || network)
            {
                return true;
            }

            HeadlessMessagingPatchState.ClearResponses(__instance);
            return false;
        }
    }

    /// <summary>
    /// Runs NPC response callbacks on the server while skipping phone UI rendering.
    /// </summary>
    [HarmonyPatch(typeof(MSGConversationType), "ResponseChosen")]
    internal static class MSGConversationResponseChosenHeadlessPatches
    {
        private static bool Prefix(MSGConversationType __instance, ResponseType r, bool network)
        {
            if (!HeadlessMessagingPatchState.ShouldBypassPhoneUi() || network)
            {
                return true;
            }

            HeadlessMessagingPatchState.ApplyResponseChosen(__instance, r);
            return false;
        }
    }

    /// <summary>
    /// Suppresses any remaining server-side phone bubble rendering path.
    /// </summary>
    [HarmonyPatch(typeof(MSGConversationType), "RenderMessage")]
    internal static class MSGConversationRenderMessageHeadlessPatches
    {
        private static bool Prefix()
        {
            return !HeadlessMessagingPatchState.ShouldBypassPhoneUi();
        }
    }
}

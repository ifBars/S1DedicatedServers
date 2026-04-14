using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DedicatedServerMod.Server.Game.Patches.Common;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
#if IL2CPP
using ConversationListType = Il2CppSystem.Collections.Generic.List<Il2CppScheduleOne.Messaging.MSGConversation>;
using MessageChainType = Il2CppScheduleOne.UI.Phone.Messages.MessageChain;
using MessageType = Il2CppScheduleOne.Messaging.Message;
using MessagesAppType = Il2CppScheduleOne.UI.Phone.Messages.MessagesApp;
using MessagingManagerType = Il2CppScheduleOne.Messaging.MessagingManager;
using MSGConversationDataType = Il2CppScheduleOne.Persistence.Datas.MSGConversationData;
using MSGConversationType = Il2CppScheduleOne.Messaging.MSGConversation;
using ResponseListType = Il2CppSystem.Collections.Generic.List<Il2CppScheduleOne.Messaging.Response>;
using ResponseType = Il2CppScheduleOne.Messaging.Response;
using TextMessageDataType = Il2CppScheduleOne.Persistence.Datas.TextMessageData;
using TextResponseDataType = Il2CppScheduleOne.Persistence.Datas.TextResponseData;
#else
using ConversationListType = System.Collections.Generic.List<ScheduleOne.Messaging.MSGConversation>;
using MessageChainType = ScheduleOne.UI.Phone.Messages.MessageChain;
using MessageType = ScheduleOne.Messaging.Message;
using MessagesAppType = ScheduleOne.UI.Phone.Messages.MessagesApp;
using MessagingManagerType = ScheduleOne.Messaging.MessagingManager;
using MSGConversationDataType = ScheduleOne.Persistence.Datas.MSGConversationData;
using MSGConversationType = ScheduleOne.Messaging.MSGConversation;
using ResponseListType = System.Collections.Generic.List<ScheduleOne.Messaging.Response>;
using ResponseType = ScheduleOne.Messaging.Response;
using TextMessageDataType = ScheduleOne.Persistence.Datas.TextMessageData;
using TextResponseDataType = ScheduleOne.Persistence.Datas.TextResponseData;
#endif

namespace DedicatedServerMod.Server.Game.Patches.UI
{
    internal static class HeadlessMessagingConversationPatchCommon
    {
        private static readonly FieldInfo ReadField = AccessTools.Field(typeof(MSGConversationType), "<Read>k__BackingField");
        private static readonly FieldInfo IndexField = AccessTools.Field(typeof(MSGConversationType), "<index>k__BackingField");
        private static readonly FieldInfo IsOpenField = AccessTools.Field(typeof(MSGConversationType), "<isOpen>k__BackingField");
        private static readonly FieldInfo RollingOutField = AccessTools.Field(typeof(MSGConversationType), "<rollingOut>k__BackingField");
        private static readonly FieldInfo EntryVisibleField = AccessTools.Field(typeof(MSGConversationType), "<EntryVisible>k__BackingField");

        internal static bool ShouldBypassUi()
        {
            return DedicatedServerPatchCommon.IsDedicatedHeadlessServer();
        }

        internal static void EnsureRegistered(MSGConversationType conversation)
        {
            EnsureRegistered(conversation, null);
        }

        internal static void EnsureRegistered(MSGConversationType conversation, int? preferredIndex)
        {
            if (conversation == null)
            {
                return;
            }

            ConversationListType activeConversations = MessagesAppType.ActiveConversations;
            if (!activeConversations.Contains(conversation))
            {
                int targetIndex = preferredIndex.GetValueOrDefault(activeConversations.Count);
                targetIndex = Mathf.Clamp(targetIndex, 0, activeConversations.Count);
                activeConversations.Insert(targetIndex, conversation);
            }

            Reindex(activeConversations);
        }

        internal static void MoveToTop(MSGConversationType conversation)
        {
            if (conversation == null)
            {
                return;
            }

            ConversationListType activeConversations = MessagesAppType.ActiveConversations;
            activeConversations.Remove(conversation);
            activeConversations.Insert(0, conversation);
            Reindex(activeConversations);
        }

        internal static void ResetConversation(MSGConversationType conversation)
        {
            if (conversation == null)
            {
                return;
            }

            conversation.messageHistory.Clear();
            conversation.messageChainHistory.Clear();
            conversation.currentResponses.Clear();
            conversation.bubbles.Clear();
            EntryVisibleField?.SetValue(conversation, true);
            conversation.HasChanged = true;
            SetOpen(conversation, false);
            SetRollingOut(conversation, false);
        }

        internal static void SetRead(MSGConversationType conversation, bool read)
        {
            ReadField?.SetValue(conversation, read);
            conversation.HasChanged = true;
        }

        internal static void SetEntryVisibility(MSGConversationType conversation, bool visible)
        {
            if (conversation == null)
            {
                return;
            }

            if (!visible && !conversation.sender.ConversationCanBeHidden)
            {
                return;
            }

            EntryVisibleField?.SetValue(conversation, visible);
            if (!visible)
            {
                SetRead(conversation, true);
            }

            conversation.HasChanged = true;
        }

        internal static void SetOpen(MSGConversationType conversation, bool open)
        {
            IsOpenField?.SetValue(conversation, open);
        }

        internal static void SetRollingOut(MSGConversationType conversation, bool rollingOut)
        {
            RollingOutField?.SetValue(conversation, rollingOut);
        }

        internal static int GetConversationIndex(MSGConversationType conversation)
        {
            if (conversation == null)
            {
                return 0;
            }

            EnsureRegistered(conversation);
            int activeIndex = MessagesAppType.ActiveConversations.IndexOf(conversation);
            return activeIndex >= 0 ? activeIndex : 0;
        }

        internal static MSGConversationDataType CreateSaveData(MSGConversationType conversation)
        {
            if (conversation == null)
            {
                return new MSGConversationDataType();
            }

            List<TextMessageDataType> messageHistory = new List<TextMessageDataType>(conversation.messageHistory.Count);
            for (int i = 0; i < conversation.messageHistory.Count; i++)
            {
                messageHistory.Add(conversation.messageHistory[i].GetSaveData());
            }

            List<TextResponseDataType> activeResponses = new List<TextResponseDataType>(conversation.currentResponses.Count);
            for (int i = 0; i < conversation.currentResponses.Count; i++)
            {
                ResponseType response = conversation.currentResponses[i];
                activeResponses.Add(new TextResponseDataType(response.text, response.label));
            }

            return new MSGConversationDataType(
                GetConversationIndex(conversation),
                conversation.Read,
                messageHistory.ToArray(),
                activeResponses.ToArray(),
                !conversation.EntryVisible);
        }

        internal static void Load(MSGConversationType conversation, MSGConversationDataType data)
        {
            if (conversation == null || data == null)
            {
                return;
            }

            ResetConversation(conversation);
            EnsureRegistered(conversation, data.ConversationIndex);
            SetRead(conversation, data.Read);

            if (data.MessageHistory != null)
            {
                for (int i = 0; i < data.MessageHistory.Length; i++)
                {
                    conversation.messageHistory.Add(new MessageType(data.MessageHistory[i]));
                    TrimMessageHistory(conversation);
                }
            }

            if (data.ActiveResponses != null)
            {
                ResponseListType responses = new ResponseListType();
                for (int i = 0; i < data.ActiveResponses.Length; i++)
                {
                    responses.Add(new ResponseType(data.ActiveResponses[i].Text, data.ActiveResponses[i].Label));
                }

                conversation.currentResponses = responses;
            }
            else
            {
                conversation.currentResponses = new ResponseListType();
            }

            if (data.IsHidden)
            {
                SetEntryVisibility(conversation, false);
            }

            conversation.HasChanged = false;
            conversation.onLoaded?.Invoke();
        }

        internal static bool SendMessage(MSGConversationType conversation, MessageType message, bool notify, bool network)
        {
            if (conversation == null || message == null)
            {
                return false;
            }

            EnsureRegistered(conversation);

            if (message.messageId == -1)
            {
                message.messageId = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            }

            if (ContainsMessageId(conversation, message.messageId))
            {
                return false;
            }

            if (network)
            {
                MessagingManagerType.Instance.SendMessage(message, notify, conversation.sender.ID);
                return false;
            }

            conversation.messageHistory.Add(message);
            TrimMessageHistory(conversation);

            if (message.sender == MessageType.ESenderType.Other && notify)
            {
                SetEntryVisibility(conversation, true);
                if (!conversation.isOpen)
                {
                    SetRead(conversation, false);
                }
            }

            MoveToTop(conversation);
            conversation.HasChanged = true;
            return false;
        }

        internal static bool SendMessageChain(MSGConversationType conversation, MessageChainType messages, float initialDelay, bool notify, bool network)
        {
            if (conversation == null || messages == null)
            {
                return false;
            }

            EnsureRegistered(conversation);

            if (messages.id == -1)
            {
                messages.id = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            }

            if (ContainsMessageChainId(conversation, messages.id))
            {
                return false;
            }

            if (network)
            {
                MessagingManagerType.Instance.SendMessageChain(messages, conversation.sender.ID, initialDelay, notify);
                return false;
            }

            conversation.messageChainHistory.Add(messages);
            conversation.HasChanged = true;

            MelonCoroutines.Start(RolloutMessageChain(conversation, messages, initialDelay, notify));

            return false;
        }

        internal static void ShowResponses(MSGConversationType conversation, ResponseListType responses, float showResponseDelay, bool network)
        {
            if (conversation == null)
            {
                return;
            }

            if (network)
            {
                MessagingManagerType.Instance.ShowResponses(conversation.sender.ID, responses, showResponseDelay);
                return;
            }

            EnsureRegistered(conversation);
            conversation.currentResponses = responses ?? new ResponseListType();
            conversation.HasChanged = true;
            conversation.onResponsesShown?.Invoke();
        }

        internal static void ClearResponses(MSGConversationType conversation, bool network)
        {
            if (conversation == null)
            {
                return;
            }

            conversation.currentResponses.Clear();
            if (network)
            {
                MessagingManagerType.Instance.ClearResponses(conversation.sender.ID);
            }
        }

        private static IEnumerator RolloutMessageChain(MSGConversationType conversation, MessageChainType messageChain, float initialDelay, bool notify)
        {
            SetRollingOut(conversation, true);

            List<MessageType> rolloutMessages = new List<MessageType>(messageChain.Messages.Count);
            for (int i = 0; i < messageChain.Messages.Count; i++)
            {
                MessageType message = new MessageType(
                    messageChain.Messages[i],
                    MessageType.ESenderType.Other,
                    i == messageChain.Messages.Count - 1);
                conversation.messageHistory.Add(message);
                TrimMessageHistory(conversation);
                rolloutMessages.Add(message);
            }

            if (initialDelay > 0f)
            {
                yield return new WaitForSeconds(initialDelay);
            }

            for (int i = 0; i < rolloutMessages.Count; i++)
            {
                MoveToTop(conversation);
                if (!conversation.isOpen && notify)
                {
                    SetEntryVisibility(conversation, true);
                    SetRead(conversation, false);
                }

                if (i + 1 < rolloutMessages.Count)
                {
                    yield return new WaitForSeconds(1f);
                }
            }

            conversation.HasChanged = true;
            SetRollingOut(conversation, false);
        }

        private static void FinishMessageChainImmediately(MSGConversationType conversation, MessageChainType messageChain, bool notify)
        {
            SetRollingOut(conversation, true);

            for (int i = 0; i < messageChain.Messages.Count; i++)
            {
                conversation.messageHistory.Add(new MessageType(
                    messageChain.Messages[i],
                    MessageType.ESenderType.Other,
                    i == messageChain.Messages.Count - 1));
                TrimMessageHistory(conversation);
                MoveToTop(conversation);
            }

            if (!conversation.isOpen && notify && messageChain.Messages.Count > 0)
            {
                SetEntryVisibility(conversation, true);
                SetRead(conversation, false);
            }

            conversation.HasChanged = true;
            SetRollingOut(conversation, false);
        }

        private static void Reindex(ConversationListType activeConversations)
        {
            if (activeConversations == null)
            {
                return;
            }

            for (int i = 0; i < activeConversations.Count; i++)
            {
                IndexField?.SetValue(activeConversations[i], i);
            }
        }

        private static void TrimMessageHistory(MSGConversationType conversation)
        {
            while (conversation.messageHistory.Count > MSGConversationType.MAX_MESSAGE_HISTORY)
            {
                conversation.messageHistory.RemoveAt(0);
            }
        }

        private static bool ContainsMessageId(MSGConversationType conversation, int messageId)
        {
            for (int i = 0; i < conversation.messageHistory.Count; i++)
            {
                if (conversation.messageHistory[i].messageId == messageId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsMessageChainId(MSGConversationType conversation, int chainId)
        {
            for (int i = 0; i < conversation.messageChainHistory.Count; i++)
            {
                if (conversation.messageChainHistory[i].id == chainId)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Preserves NPC messaging state on dedicated servers without constructing the gameplay phone UI.
    /// </summary>
    [HarmonyPatch(typeof(MSGConversationType), "EnsureUIExists")]
    internal static class MSGConversationEnsureUIExistsPatches
    {
        private static bool Prefix(MSGConversationType __instance)
        {
            if (!HeadlessMessagingConversationPatchCommon.ShouldBypassUi())
            {
                return true;
            }

            HeadlessMessagingConversationPatchCommon.EnsureRegistered(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(MSGConversationType), "CreateUI")]
    internal static class MSGConversationCreateUiPatches
    {
        private static bool Prefix(MSGConversationType __instance)
        {
            if (!HeadlessMessagingConversationPatchCommon.ShouldBypassUi())
            {
                return true;
            }

            HeadlessMessagingConversationPatchCommon.EnsureRegistered(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(MSGConversationType), "MoveToTop")]
    internal static class MSGConversationMoveToTopPatches
    {
        private static bool Prefix(MSGConversationType __instance)
        {
            if (!HeadlessMessagingConversationPatchCommon.ShouldBypassUi())
            {
                return true;
            }

            HeadlessMessagingConversationPatchCommon.MoveToTop(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(MSGConversationType), "SetRead")]
    internal static class MSGConversationSetReadPatches
    {
        private static bool Prefix(MSGConversationType __instance, bool r)
        {
            if (!HeadlessMessagingConversationPatchCommon.ShouldBypassUi())
            {
                return true;
            }

            HeadlessMessagingConversationPatchCommon.SetRead(__instance, r);
            return false;
        }
    }

    [HarmonyPatch(typeof(MSGConversationType), "SetEntryVisibility")]
    internal static class MSGConversationSetEntryVisibilityPatches
    {
        private static bool Prefix(MSGConversationType __instance, bool v)
        {
            if (!HeadlessMessagingConversationPatchCommon.ShouldBypassUi())
            {
                return true;
            }

            HeadlessMessagingConversationPatchCommon.SetEntryVisibility(__instance, v);
            return false;
        }
    }

    [HarmonyPatch(typeof(MSGConversationType), "SetOpen")]
    internal static class MSGConversationSetOpenPatches
    {
        private static bool Prefix(MSGConversationType __instance)
        {
            if (!HeadlessMessagingConversationPatchCommon.ShouldBypassUi())
            {
                return true;
            }

            HeadlessMessagingConversationPatchCommon.SetOpen(__instance, false);
            return false;
        }
    }

    [HarmonyPatch(typeof(MSGConversationType), "DisplayRelationshipInfo")]
    internal static class MSGConversationDisplayRelationshipInfoPatches
    {
        private static bool Prefix()
        {
            return !HeadlessMessagingConversationPatchCommon.ShouldBypassUi();
        }
    }

    [HarmonyPatch(typeof(MSGConversationType), "SendMessage")]
    internal static class MSGConversationSendMessagePatches
    {
        private static bool Prefix(MSGConversationType __instance, MessageType message, bool notify, bool network)
        {
            return !HeadlessMessagingConversationPatchCommon.ShouldBypassUi()
                || HeadlessMessagingConversationPatchCommon.SendMessage(__instance, message, notify, network);
        }
    }

    [HarmonyPatch(typeof(MSGConversationType), "SendMessageChain")]
    internal static class MSGConversationSendMessageChainPatches
    {
        private static bool Prefix(MSGConversationType __instance, MessageChainType messages, float initialDelay, bool notify, bool network)
        {
            return !HeadlessMessagingConversationPatchCommon.ShouldBypassUi()
                || HeadlessMessagingConversationPatchCommon.SendMessageChain(__instance, messages, initialDelay, notify, network);
        }
    }

    [HarmonyPatch(typeof(MSGConversationType), "GetSaveData")]
    internal static class MSGConversationGetSaveDataPatches
    {
        private static bool Prefix(MSGConversationType __instance, ref MSGConversationDataType __result)
        {
            if (!HeadlessMessagingConversationPatchCommon.ShouldBypassUi())
            {
                return true;
            }

            __result = HeadlessMessagingConversationPatchCommon.CreateSaveData(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(MSGConversationType), "Load")]
    internal static class MSGConversationLoadPatches
    {
        private static bool Prefix(MSGConversationType __instance, MSGConversationDataType data)
        {
            if (!HeadlessMessagingConversationPatchCommon.ShouldBypassUi())
            {
                return true;
            }

            HeadlessMessagingConversationPatchCommon.Load(__instance, data);
            return false;
        }
    }

    [HarmonyPatch(typeof(MSGConversationType), "ResetConversation")]
    internal static class MSGConversationResetConversationPatches
    {
        private static bool Prefix(MSGConversationType __instance)
        {
            if (!HeadlessMessagingConversationPatchCommon.ShouldBypassUi())
            {
                return true;
            }

            HeadlessMessagingConversationPatchCommon.ResetConversation(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(MSGConversationType), "ShowResponses")]
    internal static class MSGConversationShowResponsesPatches
    {
        private static bool Prefix(MSGConversationType __instance, ResponseListType _responses, float showResponseDelay, bool network)
        {
            if (!HeadlessMessagingConversationPatchCommon.ShouldBypassUi())
            {
                return true;
            }

            HeadlessMessagingConversationPatchCommon.ShowResponses(__instance, _responses, showResponseDelay, network);
            return false;
        }
    }

    [HarmonyPatch(typeof(MSGConversationType), "CreateResponseUI")]
    internal static class MSGConversationCreateResponseUiPatches
    {
        private static bool Prefix()
        {
            return !HeadlessMessagingConversationPatchCommon.ShouldBypassUi();
        }
    }

    [HarmonyPatch(typeof(MSGConversationType), "ClearResponseUI")]
    internal static class MSGConversationClearResponseUiPatches
    {
        private static bool Prefix()
        {
            return !HeadlessMessagingConversationPatchCommon.ShouldBypassUi();
        }
    }

    [HarmonyPatch(typeof(MSGConversationType), "SetResponseContainerVisible")]
    internal static class MSGConversationSetResponseContainerVisiblePatches
    {
        private static bool Prefix()
        {
            return !HeadlessMessagingConversationPatchCommon.ShouldBypassUi();
        }
    }

    [HarmonyPatch(typeof(MSGConversationType), "ResponseChosen")]
    internal static class MSGConversationResponseChosenPatches
    {
        private static bool Prefix(MSGConversationType __instance, ResponseType r, bool network)
        {
            if (!HeadlessMessagingConversationPatchCommon.ShouldBypassUi())
            {
                return true;
            }

            if (!__instance.AreResponsesActive)
            {
                return false;
            }

            if (r.disableDefaultResponseBehaviour)
            {
                r.callback?.Invoke();
                return false;
            }

            if (network)
            {
                MessagingManagerType.Instance.SendResponse(__instance.currentResponses.IndexOf(r), __instance.sender.ID);
                return false;
            }

            HeadlessMessagingConversationPatchCommon.ClearResponses(__instance, false);
            __instance.HasChanged = true;
            HeadlessMessagingConversationPatchCommon.MoveToTop(__instance);
            r.callback?.Invoke();
            return false;
        }
    }

    [HarmonyPatch(typeof(MSGConversationType), "ClearResponses")]
    internal static class MSGConversationClearResponsesPatches
    {
        private static bool Prefix(MSGConversationType __instance, bool network)
        {
            if (!HeadlessMessagingConversationPatchCommon.ShouldBypassUi())
            {
                return true;
            }

            HeadlessMessagingConversationPatchCommon.ClearResponses(__instance, network);
            return false;
        }
    }

    [HarmonyPatch(typeof(MSGConversationType), "RenderPlayerMessage")]
    internal static class MSGConversationRenderPlayerMessagePatches
    {
        private static bool Prefix()
        {
            return !HeadlessMessagingConversationPatchCommon.ShouldBypassUi();
        }
    }

    [HarmonyPatch(typeof(MSGConversationType), "RenderMessage")]
    internal static class MSGConversationRenderMessagePatches
    {
        private static bool Prefix()
        {
            return !HeadlessMessagingConversationPatchCommon.ShouldBypassUi();
        }
    }

    [HarmonyPatch(typeof(MSGConversationType), "RefreshPreviewText")]
    internal static class MSGConversationRefreshPreviewTextPatches
    {
        private static bool Prefix()
        {
            return !HeadlessMessagingConversationPatchCommon.ShouldBypassUi();
        }
    }

    [HarmonyPatch(typeof(MSGConversationType), "CheckSendLoop")]
    internal static class MSGConversationCheckSendLoopPatches
    {
        private static bool Prefix()
        {
            return !HeadlessMessagingConversationPatchCommon.ShouldBypassUi();
        }
    }
}

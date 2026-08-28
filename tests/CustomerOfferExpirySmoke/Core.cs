using System.Reflection;
using HarmonyLib;
using MelonLoader;
using ScheduleOne.DevUtilities;
using ScheduleOne.Economy;
using ScheduleOne.Messaging;
using ScheduleOne.Persistence;
using ScheduleOne.Quests;
using UnityEngine;

[assembly: MelonInfo(typeof(CustomerOfferExpirySmoke.Core), "Customer Offer Expiry Smoke", "1.0.0", "ifBars")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace CustomerOfferExpirySmoke
{
    internal sealed class Core : MelonMod
    {
        private static readonly FieldInfo _offeredContractField = typeof(Customer).GetField(
            "offeredContractInfo",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private static MSGConversation _targetConversation;
        private static bool _clientClearReceived;

        private bool _completed;
        private bool _expiryTriggered;
        private float _startedAt;
        private float _expiryTriggeredAt;
        private Customer _customer;
        private int _chainsBefore;
        private int _thrownExceptions;

        /// <inheritdoc />
        public override void OnInitializeMelon()
        {
            new HarmonyLib.Harmony("DedicatedServerMod.Tests.CustomerOfferExpirySmoke").PatchAll();
            _startedAt = Time.realtimeSinceStartup;
            LoggerInstance.Msg("[CUSTOMER_OFFER_EXPIRY_SMOKE] START");
        }

        /// <inheritdoc />
        public override void OnUpdate()
        {
            if (_completed)
            {
                return;
            }

            if (Time.realtimeSinceStartup - _startedAt > 90f)
            {
                Complete("FAIL|reason=timeout");
                return;
            }

            LoadManager loadManager = Singleton<LoadManager>.Instance;
            if (loadManager == null || loadManager.IsLoading || !loadManager.IsGameLoaded)
            {
                return;
            }

            if (!_expiryTriggered)
            {
                Customer customer = Customer.UnlockedCustomers.FirstOrDefault(candidate =>
                    candidate?.NPC?.MSGConversation != null &&
                    candidate.NPC.DialogueHandler?.Database?.HasChain(
                        ScheduleOne.Dialogue.EDialogueModule.Customer,
                        "offer_expired") == true);
                if (customer != null)
                {
                    TriggerExpiryRegression(customer);
                }

                return;
            }

            if (_clientClearReceived || Time.realtimeSinceStartup - _expiryTriggeredAt >= 5f)
            {
                CompleteExpiryRegression();
            }
        }

        internal static void RecordClientClear(MSGConversation conversation, bool network)
        {
            if (!network && ReferenceEquals(conversation, _targetConversation))
            {
                _clientClearReceived = true;
            }
        }

        private void TriggerExpiryRegression(Customer customer)
        {
            if (_offeredContractField == null)
            {
                Complete("FAIL|reason=offered-contract-field-missing");
                return;
            }

            _customer = customer;
            MSGConversation conversation = customer.NPC.MSGConversation;
            _targetConversation = conversation;
            _clientClearReceived = false;
            _chainsBefore = conversation.messageChainHistory.Count;
            conversation.currentResponses.Add(new Response("Accept", "ACCEPT"));
            _offeredContractField.SetValue(customer, new ContractInfo());

            for (int i = 0; i < 10; i++)
            {
                try
                {
                    customer.RpcLogic___ExpireOffer_2166136261();
                }
                catch (Exception ex)
                {
                    _thrownExceptions++;
                    LoggerInstance.Error($"[CUSTOMER_OFFER_EXPIRY_SMOKE] expiry {i + 1} threw: {ex}");
                }
            }

            _expiryTriggered = true;
            _expiryTriggeredAt = Time.realtimeSinceStartup;
        }

        private void CompleteExpiryRegression()
        {
            int addedChains = _targetConversation.messageChainHistory.Count - _chainsBefore;
            bool offerCleared = _customer.OfferedContractInfo == null;
            bool responsesCleared = _targetConversation.currentResponses.Count == 0;
            bool passed = _thrownExceptions == 0 && offerCleared && responsesCleared &&
                _clientClearReceived && addedChains == 1;
            Complete(
                $"{(passed ? "PASS" : "FAIL")}|offerCleared={offerCleared}|responsesCleared={responsesCleared}|" +
                $"clientClearReceived={_clientClearReceived}|expiryExceptions={_thrownExceptions}|" +
                $"addedChains={addedChains}");
        }

        private void Complete(string result)
        {
            _completed = true;
            LoggerInstance.Msg($"[CUSTOMER_OFFER_EXPIRY_SMOKE] {result}");
            Application.Quit();
        }
    }

    [HarmonyPatch(typeof(MSGConversation), "ClearResponses")]
    internal static class MSGConversationClearResponsesObserverPatch
    {
        private static void Prefix(MSGConversation __instance, bool network)
        {
            Core.RecordClientClear(__instance, network);
        }
    }
}

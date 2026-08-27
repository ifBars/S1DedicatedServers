using System.Reflection;
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
        private static readonly FieldInfo OfferedContractField = typeof(Customer).GetField(
            "offeredContractInfo",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private bool _completed;
        private float _startedAt;

        public override void OnInitializeMelon()
        {
            _startedAt = Time.realtimeSinceStartup;
            LoggerInstance.Msg("[CUSTOMER_OFFER_EXPIRY_SMOKE] START");
        }

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

            Customer customer = Customer.UnlockedCustomers.FirstOrDefault(candidate =>
                candidate?.NPC?.MSGConversation != null &&
                candidate.NPC.DialogueHandler?.Database?.HasChain(
                    ScheduleOne.Dialogue.EDialogueModule.Customer,
                    "offer_expired") == true);
            if (customer != null)
            {
                RunExpiryRegression(customer);
            }
        }

        private void RunExpiryRegression(Customer customer)
        {
            if (OfferedContractField == null)
            {
                Complete("FAIL|reason=offered-contract-field-missing");
                return;
            }

            MSGConversation conversation = customer.NPC.MSGConversation;
            int chainsBefore = conversation.messageChainHistory.Count;
            conversation.currentResponses.Add(new Response("Accept", "ACCEPT"));
            OfferedContractField.SetValue(customer, new ContractInfo());

            int thrownExceptions = 0;
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    customer.RpcLogic___ExpireOffer_2166136261();
                }
                catch (Exception ex)
                {
                    thrownExceptions++;
                    LoggerInstance.Error($"[CUSTOMER_OFFER_EXPIRY_SMOKE] expiry {i + 1} threw: {ex}");
                }
            }

            int addedChains = conversation.messageChainHistory.Count - chainsBefore;
            bool offerCleared = customer.OfferedContractInfo == null;
            bool responsesCleared = conversation.currentResponses.Count == 0;
            bool passed = thrownExceptions == 0 && offerCleared && responsesCleared && addedChains == 1;
            Complete(
                $"{(passed ? "PASS" : "FAIL")}|offerCleared={offerCleared}|responsesCleared={responsesCleared}|" +
                $"expiryExceptions={thrownExceptions}|addedChains={addedChains}");
        }

        private void Complete(string result)
        {
            _completed = true;
            LoggerInstance.Msg($"[CUSTOMER_OFFER_EXPIRY_SMOKE] {result}");
            Application.Quit();
        }
    }
}

using System.Globalization;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Messaging;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.Persistence;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(ConversationVisibilitySmoke.Core), "Conversation Visibility Smoke", "1.0.0", "ifBars")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace ConversationVisibilitySmoke
{
    internal sealed class Core : MelonMod
    {
        private const string EnabledFlag = "--s1ds-conversation-visibility-smoke";
        private const string ResultFlag = "--s1ds-conversation-visibility-result";
        private const string RoleFlag = "--s1ds-conversation-visibility-role";
        private const string TargetNpcId = "rescheduledadditionalnpcs_tony_salas";
        private const string PackNpcPrefix = "rescheduledadditionalnpcs_";

        private bool _completed;
        private bool _enabled;
        private string _resultPath = string.Empty;
        private string _role = string.Empty;
        private float _startedAt;

        /// <inheritdoc />
        public override void OnInitializeMelon()
        {
            string[] args = Environment.GetCommandLineArgs();
            _enabled = args.Contains(EnabledFlag, StringComparer.Ordinal);
            if (!_enabled)
            {
                return;
            }

            _role = GetArgumentValue(args, RoleFlag) ?? string.Empty;
            _resultPath = GetArgumentValue(args, ResultFlag) ?? string.Empty;
            _startedAt = Time.realtimeSinceStartup;

            if ((_role != "headless" && _role != "interactive") || string.IsNullOrWhiteSpace(_resultPath))
            {
                Complete("FAIL|reason=invalid-arguments");
                return;
            }

            LoggerInstance.Msg($"[CONVERSATION_VISIBILITY_SMOKE] START|role={_role}");
        }

        /// <inheritdoc />
        public override void OnUpdate()
        {
            if (!_enabled || _completed)
            {
                return;
            }

            if (Time.realtimeSinceStartup - _startedAt > 180f)
            {
                Complete("FAIL|reason=timeout");
                return;
            }

            LoadManager loadManager = Singleton<LoadManager>.Instance;
            if (loadManager == null || loadManager.IsLoading || !loadManager.IsGameLoaded)
            {
                return;
            }

            MSGConversation? conversation = FindTargetConversation(out int packNpcCount, out string targetNpcId);
            if (conversation == null)
            {
                return;
            }

            if (_role == "headless")
            {
                RunHeadlessCheck(conversation, packNpcCount, targetNpcId);
            }
            else
            {
                RunInteractiveCheck(conversation, packNpcCount, targetNpcId);
            }
        }

        private void RunHeadlessCheck(MSGConversation conversation, int packNpcCount, string targetNpcId)
        {
            try
            {
                bool uiAbsentBefore = !conversation.uiCreated && conversation.entry == null;
                conversation.SetIsKnown(true);
                conversation.SetEntryVisibility(true);

                bool stateVisible = conversation.EntryVisible;
                bool stateKnown = conversation.IsSenderKnown;
                bool saveVisible = !conversation.GetSaveData().IsHidden;
                bool uiStillAbsent = !conversation.uiCreated && conversation.entry == null;
                bool passed = uiAbsentBefore && uiStillAbsent && stateVisible && stateKnown && saveVisible;

                Complete(
                    $"{(passed ? "PASS" : "FAIL")}|role=headless|target={targetNpcId}|packNpcCount={packNpcCount}|" +
                    $"uiAbsentBefore={uiAbsentBefore}|uiStillAbsent={uiStillAbsent}|" +
                    $"entryVisible={stateVisible}|senderKnown={stateKnown}|saveVisible={saveVisible}");
            }
            catch (Exception ex)
            {
                Complete($"FAIL|role=headless|exception={Sanitize(ex.GetType().Name + ":" + ex.Message)}");
            }
        }

        private void RunInteractiveCheck(MSGConversation conversation, int packNpcCount, string targetNpcId)
        {
            try
            {
                conversation.EnsureUIExists();
                if (!conversation.uiCreated || conversation.entry == null)
                {
                    return;
                }

                conversation.SetIsKnown(true);
                conversation.SetEntryVisibility(true);

                bool stateVisible = conversation.EntryVisible;
                bool stateKnown = conversation.IsSenderKnown;
                bool saveVisible = !conversation.GetSaveData().IsHidden;
                bool uiVisible = conversation.entry.gameObject.activeSelf;
                bool passed = stateVisible && stateKnown && saveVisible && uiVisible;

                Complete(
                    $"{(passed ? "PASS" : "FAIL")}|role=interactive|target={targetNpcId}|packNpcCount={packNpcCount}|" +
                    $"uiCreated={conversation.uiCreated}|uiVisible={uiVisible}|" +
                    $"entryVisible={stateVisible}|senderKnown={stateKnown}|saveVisible={saveVisible}");
                Application.Quit();
            }
            catch (Exception ex)
            {
                Complete($"FAIL|role=interactive|exception={Sanitize(ex.GetType().Name + ":" + ex.Message)}");
                Application.Quit();
            }
        }

        private static MSGConversation? FindTargetConversation(out int packNpcCount, out string targetNpcId)
        {
            packNpcCount = 0;
            targetNpcId = string.Empty;
            MSGConversation? target = null;
            MSGConversation? fallback = null;
            string fallbackNpcId = string.Empty;
            var registry = NPCManager.NPCRegistry;
            if (registry == null)
            {
                return null;
            }

            for (int i = 0; i < registry.Count; i++)
            {
                NPC npc = registry[i];
                if (npc == null || string.IsNullOrWhiteSpace(npc.ID))
                {
                    continue;
                }

                if (npc.ID.StartsWith(PackNpcPrefix, StringComparison.Ordinal))
                {
                    packNpcCount++;
                }

                if (fallback == null && npc.MSGConversation != null)
                {
                    fallback = npc.MSGConversation;
                    fallbackNpcId = npc.ID;
                }

                if (npc.ID == TargetNpcId)
                {
                    target = npc.MSGConversation;
                    targetNpcId = npc.ID;
                }
            }

            if (target != null)
            {
                return target;
            }

            targetNpcId = fallbackNpcId;
            return fallback;
        }

        private void Complete(string result)
        {
            _completed = true;
            string line = result + "|elapsed=" + (Time.realtimeSinceStartup - _startedAt).ToString("F1", CultureInfo.InvariantCulture);
            LoggerInstance.Msg("[CONVERSATION_VISIBILITY_SMOKE] " + line);

            try
            {
                string? directory = Path.GetDirectoryName(_resultPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(_resultPath, line);
            }
            catch (Exception ex)
            {
                LoggerInstance.Error($"[CONVERSATION_VISIBILITY_SMOKE] Could not write result: {ex}");
            }
        }

        private static string? GetArgumentValue(string[] args, string name)
        {
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (string.Equals(args[i], name, StringComparison.Ordinal))
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        private static string Sanitize(string value)
        {
            return value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');
        }
    }
}

using System.Reflection;
using MelonLoader;
using ScheduleOne.DevUtilities;
using ScheduleOne.Dialogue;
using ScheduleOne.Persistence;
using ScheduleOne.Quests;
using UnityEngine;
using UnityEngine.SceneManagement;

[assembly: MelonInfo(typeof(DialogueQuestReplaySmoke.Core), "Dialogue Quest Replay Smoke", "1.0.0", "ifBars")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace DialogueQuestReplaySmoke
{
    internal sealed class Core : MelonMod
    {
        private const string EnabledArgument = "--s1ds-dialogue-quest-smoke";
        private const string ResultArgument = "--s1ds-dialogue-quest-smoke-result";
        private const string ScreenshotArgument = "--s1ds-dialogue-quest-smoke-screenshot";
        private const string TimeoutArgument = "--s1ds-dialogue-quest-smoke-timeout-seconds";
        private const string TargetChoiceText = "I'd like to rent a room";

        private static readonly MethodInfo InteractedMethod = typeof(DialogueController).GetMethod(
            "Interacted",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private bool _enabled;
        private bool _completed;
        private bool _interactionStarted;
        private bool _screenshotRequested;
        private float _startedAt;
        private float _interactionStartedAt;
        private float _screenshotRequestedAt;
        private float _timeoutSeconds = 120f;
        private string _resultPath;
        private string _screenshotPath;
        private string _passDetails;

        public override void OnInitializeMelon()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            _enabled = Array.IndexOf(arguments, EnabledArgument) >= 0;
            if (!_enabled)
            {
                return;
            }

            _resultPath = GetArgumentValue(arguments, ResultArgument);
            _screenshotPath = GetArgumentValue(arguments, ScreenshotArgument);
            string timeoutValue = GetArgumentValue(arguments, TimeoutArgument);
            if (float.TryParse(timeoutValue, out float parsedTimeout) && parsedTimeout > 0f)
            {
                _timeoutSeconds = parsedTimeout;
            }

            _startedAt = Time.realtimeSinceStartup;
            LoggerInstance.Msg("[DIALOGUE_QUEST_SMOKE] START");
        }

        public override void OnUpdate()
        {
            if (!_enabled || _completed)
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            if (now - _startedAt >= _timeoutSeconds)
            {
                Complete($"FAIL|reason=timeout|scene={SceneManager.GetActiveScene().name}");
                return;
            }

            if (!_interactionStarted)
            {
                TryStartDialogueInteraction(now);
                return;
            }

            if (!_screenshotRequested && now - _interactionStartedAt >= 3f)
            {
                if (string.IsNullOrWhiteSpace(_screenshotPath))
                {
                    Complete($"PASS|{_passDetails}");
                    return;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(_screenshotPath) ?? ".");
                ScreenCapture.CaptureScreenshot(_screenshotPath);
                _screenshotRequested = true;
                _screenshotRequestedAt = now;
                return;
            }

            if (_screenshotRequested)
            {
                if (File.Exists(_screenshotPath) && new FileInfo(_screenshotPath).Length > 0)
                {
                    Complete($"PASS|{_passDetails}|screenshot={_screenshotPath}");
                }
                else if (now - _screenshotRequestedAt >= 10f)
                {
                    Complete($"FAIL|reason=screenshot-timeout|{_passDetails}");
                }
            }
        }

        private void TryStartDialogueInteraction(float now)
        {
            LoadManager loadManager = Singleton<LoadManager>.Instance;
            if (loadManager == null || loadManager.IsLoading || !loadManager.IsGameLoaded)
            {
                return;
            }

            DialogueController_Ming[] controllers = UnityEngine.Object.FindObjectsOfType<DialogueController_Ming>();
            DialogueController_Ming target = controllers.FirstOrDefault(controller =>
                controller != null && string.Equals(controller.BuyText, TargetChoiceText, StringComparison.Ordinal));
            if (target == null || target.PurchaseRoomQuests == null)
            {
                return;
            }

            int activeEntryCount = target.PurchaseRoomQuests.Count(entry =>
                entry != null && entry.State == EQuestState.Active);
            DialogueController.DialogueChoice targetChoice = target.Choices?.FirstOrDefault(choice =>
                choice != null && string.Equals(choice.ChoiceText, TargetChoiceText, StringComparison.Ordinal));
            bool choiceVisible = targetChoice?.ShouldShow() == true;
            if (activeEntryCount == 0 || !choiceVisible)
            {
                return;
            }

            _passDetails =
                $"scene={SceneManager.GetActiveScene().name}|controllers={controllers.Length}|" +
                $"activeEntries={activeEntryCount}|choiceVisible={choiceVisible}";

            if (InteractedMethod == null)
            {
                Complete($"FAIL|reason=interaction-method-missing|{_passDetails}");
                return;
            }

            try
            {
                InteractedMethod.Invoke(target, null);
                _interactionStarted = true;
                _interactionStartedAt = now;
                LoggerInstance.Msg($"[DIALOGUE_QUEST_SMOKE] ACTIVE|{_passDetails}");
            }
            catch (Exception ex)
            {
                Complete($"FAIL|reason=interaction-error|error={ex.GetBaseException().Message}|{_passDetails}");
            }
        }

        private void Complete(string result)
        {
            if (_completed)
            {
                return;
            }

            _completed = true;
            string marker = $"[DIALOGUE_QUEST_SMOKE] {result}";
            LoggerInstance.Msg(marker);

            if (!string.IsNullOrWhiteSpace(_resultPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_resultPath) ?? ".");
                File.WriteAllText(_resultPath, result);
            }

            Application.Quit();
        }

        private static string GetArgumentValue(string[] arguments, string name)
        {
            int index = Array.IndexOf(arguments, name);
            return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
        }
    }
}

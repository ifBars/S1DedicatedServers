#if CLIENT
using System.Reflection;
using DedicatedServerMod.Client.Core;
using DedicatedServerMod.Utils;
using HarmonyLib;
#if IL2CPP
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.UI;
#elif MONO
using ScheduleOne;
using ScheduleOne.DevUtilities;
using ScheduleOne.UI;
#else
#error DailySummaryPatches requires either the MONO or IL2CPP runtime symbol.
#endif

namespace DedicatedServerMod.Client.Patches
{
    /// <summary>
    /// Preserves daily-summary statistics until dedicated-server clients have consumed them.
    /// </summary>
    /// <remarks>
    /// A headless host completes its side of sleep immediately. The resulting sleep-end event can otherwise clear
    /// the client accumulators before the daily-summary fade finishes and before the rank summary reads its XP.
    /// </remarks>
    internal static class DailySummaryPatches
    {
        private static readonly HarmonyLib.Harmony HarmonyInstance = new HarmonyLib.Harmony(
            "DedicatedServerMod.Client.DailySummaryPatches");
        private static readonly MethodInfo ClearStatsMethod = AccessTools.Method(typeof(DailySummary), "ClearStats");

        private static bool _initialized;
        private static bool _summaryConsumed = true;
        private static bool _statsClearDeferred;
        private static bool _allowDeferredClear;

        internal static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            MethodInfo sleepStartMethod = AccessTools.Method(typeof(SleepCanvas), "SleepStart");
            MethodInfo rankUpStartEventMethod = AccessTools.Method(typeof(RankUpCanvas), nameof(RankUpCanvas.StartEvent));
            MethodInfo dailySummaryCloseMethod = AccessTools.Method(typeof(DailySummary), nameof(DailySummary.Close));
            if (sleepStartMethod == null || ClearStatsMethod == null || rankUpStartEventMethod == null ||
                dailySummaryCloseMethod == null)
            {
                DebugLog.Warning("Could not initialize dedicated-client daily-summary synchronization patches.");
                return;
            }

            HarmonyInstance.Patch(
                sleepStartMethod,
                prefix: new HarmonyMethod(typeof(DailySummaryPatches), nameof(SleepStartPrefix)));
            HarmonyInstance.Patch(
                ClearStatsMethod,
                prefix: new HarmonyMethod(typeof(DailySummaryPatches), nameof(ClearStatsPrefix)));
            HarmonyInstance.Patch(
                rankUpStartEventMethod,
                postfix: new HarmonyMethod(typeof(DailySummaryPatches), nameof(RankUpStartEventPostfix)));
            HarmonyInstance.Patch(
                dailySummaryCloseMethod,
                postfix: new HarmonyMethod(typeof(DailySummaryPatches), nameof(DailySummaryClosePostfix)));
            _initialized = true;
            DebugLog.StartupDebug("Daily summary synchronization patches initialized");
        }

        /// <summary>
        /// Patches <see cref="SleepCanvas.SleepStart"/> to begin tracking before the headless host can signal sleep
        /// completion.
        /// </summary>
        private static void SleepStartPrefix()
        {
            if (!IsDedicatedServerSession())
            {
                return;
            }

            _summaryConsumed = false;
            _statsClearDeferred = false;
        }

        /// <summary>
        /// Patches the native <c>DailySummary.ClearStats</c> method to defer its sleep-end clear until both summary
        /// panels have captured their values.
        /// </summary>
        private static bool ClearStatsPrefix()
        {
            if (_allowDeferredClear || !IsDedicatedServerSession() || _summaryConsumed)
            {
                return true;
            }

            _statsClearDeferred = true;
            return false;
        }

        /// <summary>
        /// Patches <see cref="RankUpCanvas.StartEvent"/> to clear retained values after it synchronously reads the
        /// daily XP total.
        /// </summary>
        private static void RankUpStartEventPostfix()
        {
            ConsumeSummaryAndClearStats();
        }

        /// <summary>
        /// Patches <see cref="DailySummary.Close"/> to clear tutorial summaries, which do not enqueue the rank panel.
        /// </summary>
        private static void DailySummaryClosePostfix()
        {
            if (GameManager.IS_TUTORIAL)
            {
                ConsumeSummaryAndClearStats();
            }
        }

        private static bool IsDedicatedServerSession()
        {
            return ClientBootstrap.Instance?.ConnectionManager?.IsConnectedToDedicatedServer ?? false;
        }

        private static void ConsumeSummaryAndClearStats()
        {
            if (!IsDedicatedServerSession())
            {
                return;
            }

            _summaryConsumed = true;
            if (!_statsClearDeferred)
            {
                return;
            }

            DailySummary dailySummary = DailySummary.Instance;
            if (dailySummary == null || ClearStatsMethod == null)
            {
                DebugLog.Warning("Could not complete the deferred daily-summary stats clear.");
                return;
            }

            _allowDeferredClear = true;
            try
            {
                ClearStatsMethod.Invoke(dailySummary, null);
                _statsClearDeferred = false;
            }
            catch (Exception ex)
            {
                DebugLog.Warning($"Deferred daily-summary stats clear failed: {ex.Message}");
            }
            finally
            {
                _allowDeferredClear = false;
            }
        }
    }
}
#endif

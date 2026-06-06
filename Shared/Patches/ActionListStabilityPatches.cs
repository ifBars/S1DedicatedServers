using System;
using System.Collections;
using System.Collections.Generic;
#if CLIENT
using DedicatedServerMod.Client.Core;
#endif
using DedicatedServerMod.Utils;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
#if IL2CPP
using ActionListType = Il2Cpp.ActionList;
using ActionType = Il2CppSystem.Action;
using LoadManagerType = Il2CppScheduleOne.Persistence.LoadManager;
using NativeActionListType = Il2CppSystem.Collections.Generic.List<Il2CppSystem.Action>;
using SingletonType = Il2CppScheduleOne.DevUtilities.Singleton<Il2CppScheduleOne.Persistence.LoadManager>;
#else
using ActionListType = ActionList;
using ActionType = System.Action;
using LoadManagerType = ScheduleOne.Persistence.LoadManager;
using NativeActionListType = System.Collections.Generic.List<System.Action>;
using SingletonType = ScheduleOne.DevUtilities.Singleton<ScheduleOne.Persistence.LoadManager>;
#endif

namespace DedicatedServerMod.Shared.Patches
{
    /// <summary>
    /// Runs staggered callbacks from a snapshot so time callbacks can safely
    /// subscribe or unsubscribe other callbacks while the stagger coroutine is active.
    /// </summary>
    [HarmonyPatch(typeof(ActionListType), "InvokeAllStaggered")]
    internal static class ActionListInvokeAllStaggeredPatches
    {
        private static bool Prefix(ActionListType __instance, float staggerTime)
        {
            NativeActionListType invocationList = __instance?.GetInvocationList();
            if (invocationList == null || invocationList.Count == 0)
            {
                return false;
            }

            var snapshot = new List<ActionType>(invocationList.Count);
            for (int i = 0; i < invocationList.Count; i++)
            {
                ActionType action = invocationList[i];
                if (action != null)
                {
                    snapshot.Add(action);
                }
            }

            if (snapshot.Count == 0)
            {
                return false;
            }

            MelonCoroutines.Start(InvokeSnapshotStaggered(snapshot, staggerTime));
            return false;
        }

        private static IEnumerator InvokeSnapshotStaggered(IReadOnlyList<ActionType> snapshot, float staggerTime)
        {
            float delay = snapshot.Count > 0 ? Mathf.Max(0f, staggerTime) / snapshot.Count : 0f;

            for (int i = 0; i < snapshot.Count; i++)
            {
                if (ShouldAbortStaggeredInvocation())
                {
                    yield break;
                }

                try
                {
                    snapshot[i]?.Invoke();
                }
                catch (Exception ex)
                {
                    DebugLog.Warning($"Error invoking staggered action callback: {ex.Message}");
                }

                if (delay > 0f && i + 1 < snapshot.Count)
                {
                    yield return new WaitForSeconds(delay);
                }
            }
        }

        private static bool ShouldAbortStaggeredInvocation()
        {
#if CLIENT
            var connectionManager = ClientBootstrap.Instance?.ConnectionManager;
            if (connectionManager?.IsReturningToMenu == true)
            {
                return true;
            }

            try
            {
                LoadManagerType loadManager = SingletonType.Instance;
                return loadManager != null &&
                       loadManager.IsLoading &&
                       !loadManager.IsGameLoaded &&
                       connectionManager?.IsConnecting != true;
            }
            catch
            {
                return false;
            }
#else
            return false;
#endif
        }
    }
}

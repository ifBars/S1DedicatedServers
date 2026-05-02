using HarmonyLib;
using DedicatedServerMod.Server.Game.Patches.Common;
using DedicatedServerMod.Utils;
#if IL2CPP
using Il2CppFishNet;
using Il2CppScheduleOne.DevUtilities;
using LoadManagerType = Il2CppScheduleOne.Persistence.LoadManager;
using TimeManagerType = Il2CppScheduleOne.GameTime.TimeManager;
#else
using FishNet;
using ScheduleOne.DevUtilities;
using LoadManagerType = ScheduleOne.Persistence.LoadManager;
using TimeManagerType = ScheduleOne.GameTime.TimeManager;
#endif
using UnityEngine;

namespace DedicatedServerMod.Server.Game.Patches.Gameplay
{
    [HarmonyPatch(typeof(TimeManagerType), nameof(TimeManagerType.SetTimeSpeedMultiplier))]
    internal static class TimeManagerSetTimeSpeedMultiplierPatches
    {
        private static void Prefix(ref float multiplier)
        {
            if (!InstanceFinder.IsServer || !Application.isBatchMode || multiplier > 0f)
            {
                return;
            }

            multiplier = 1f;
        }
    }

    [HarmonyPatch(typeof(TimeManagerType), "Update")]
    internal static class TimeManagerUpdatePatches
    {
        private static void Postfix(TimeManagerType __instance)
        {
            if (!InstanceFinder.IsServer || !Application.isBatchMode)
            {
                return;
            }

            var loadManager = Singleton<LoadManagerType>.Instance;
            if (loadManager == null || loadManager.IsLoading || !loadManager.IsGameLoaded || __instance.IsSleepInProgress)
            {
                return;
            }

            if (__instance.TimeSpeedMultiplier <= 0f)
            {
                __instance.SetTimeSpeedMultiplier(1f);
            }
        }
    }

    /*
    [HarmonyPatch(typeof(TimeManagerType), "TimeLoop")]
    [HarmonyPatch(typeof(TimeManagerType), "TickLoop")]
    internal static class TimeManagerLoopPatches
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var waitForEndOfFrameCtor = typeof(WaitForEndOfFrame).GetConstructor(Type.EmptyTypes);
            var waitForFixedUpdateCtor = typeof(WaitForFixedUpdate).GetConstructor(Type.EmptyTypes);
            bool patched = false;

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Newobj &&
                    codes[i].operand is System.Reflection.ConstructorInfo ctor &&
                    ctor == waitForEndOfFrameCtor)
                {
                    codes[i].operand = waitForFixedUpdateCtor;
                    patched = true;
                }
            }

            if (patched)
            {
                DebugLog.Info("Patched TimeManager coroutines to use WaitForFixedUpdate for batchmode compatibility");
            }

            return codes;
        }
    }
    */

    [HarmonyPatch(typeof(TimeManagerType), nameof(TimeManagerType.StartSleep))]
    internal static class TimeManagerStartSleepPatches
    {
        private static bool Prefix()
        {
            if (!InstanceFinder.IsServer)
            {
                return true;
            }

            return DedicatedServerPatchCommon.CountSleepEligiblePlayers() > 0;
        }
    }

    [HarmonyPatch(typeof(TimeManagerType), nameof(TimeManagerType.StartSleep))]
    internal static class TimeManagerStartSleepHeadlessPatches
    {
        private static void Postfix(TimeManagerType __instance)
        {
            ForceHeadlessHostSleepDone(__instance);
        }

        public static void ForceHeadlessHostSleepDone(TimeManagerType __instance)
        {
            if (!InstanceFinder.IsServer || !DedicatedServerPatchCommon.IsDedicatedHeadlessServer())
            {
                return;
            }

            if (__instance == null || !__instance.IsSleepInProgress || __instance.HostSleepDone)
            {
                return;
            }

            __instance.SetHostSleepDone(done: true);
        }
    }
}

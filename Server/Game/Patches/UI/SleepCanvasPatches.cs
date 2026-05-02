using HarmonyLib;
using DedicatedServerMod.Server.Game.Patches.Common;
#if IL2CPP
using Il2CppFishNet;
using Il2CppScheduleOne.UI;
#else
using FishNet;
using ScheduleOne.UI;
#endif
using UnityEngine;

namespace DedicatedServerMod.Server.Game.Patches.UI
{
    [HarmonyPatch(typeof(SleepCanvas), "SleepStart")]
    internal static class SleepCanvasPatches
    {
        private static bool Prefix()
        {
            if (!InstanceFinder.IsServer || !DedicatedServerPatchCommon.IsDedicatedHeadlessServer())
            {
                return true;
            }

            return false;
        }
    }
}

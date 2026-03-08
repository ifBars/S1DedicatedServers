using HarmonyLib;
#if IL2CPP
using Il2CppFishNet;
using Il2CppScheduleOne.UI;
#else
using FishNet;
using ScheduleOne.UI;
#endif
using UnityEngine;

namespace DedicatedServerMod.Server.Game
{
    [HarmonyPatch(typeof(SleepCanvas), "SleepStart")]
    internal static class SleepCanvasPatches
    {
        private static bool Prefix()
        {
            if (!InstanceFinder.IsServer || !Application.isBatchMode)
            {
                return true;
            }

            return false;
        }
    }
}

using HarmonyLib;
#if IL2CPP
using Il2CppFishNet;
using Il2CppScheduleOne.DevUtilities;
#else
using FishNet;
using ScheduleOne.DevUtilities;
#endif

namespace DedicatedServerMod.Server.Game.Patches.Visual
{
    [HarmonyPatch(typeof(IconGenerator), "GeneratePackagingIcon")]
    internal static class IconGeneratorPatches
    {
        private static bool Prefix(ref UnityEngine.Texture2D __result)
        {
            if (InstanceFinder.IsServer || UnityEngine.Application.isBatchMode)
            {
                __result = UnityEngine.Texture2D.whiteTexture;
                return false;
            }

            return true;
        }
    }
}

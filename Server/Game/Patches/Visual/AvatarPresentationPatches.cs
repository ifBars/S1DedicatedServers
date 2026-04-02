using HarmonyLib;
#if IL2CPP
using AvatarEffectsType = Il2CppScheduleOne.AvatarFramework.AvatarEffects;
using AvatarIkControllerType = Il2CppScheduleOne.AvatarFramework.Animation.AvatarIKController;
#else
using AvatarEffectsType = ScheduleOne.AvatarFramework.AvatarEffects;
using AvatarIkControllerType = ScheduleOne.AvatarFramework.Animation.AvatarIKController;
#endif

namespace DedicatedServerMod.Server.Game.Patches.Visual
{
    /// <summary>
    /// Skips avatar VFX, color, and emission updates on dedicated headless servers.
    /// </summary>
    [HarmonyPatch(typeof(AvatarEffectsType), "Update")]
    internal static class AvatarEffectsUpdatePatches
    {
        private static bool Prefix()
        {
            return false;
        }
    }

    /// <summary>
    /// Prevents avatar body IK from being enabled on dedicated headless servers.
    /// </summary>
    [HarmonyPatch(typeof(AvatarIkControllerType), "SetIKActive")]
    internal static class AvatarIkControllerSetIkActivePatches
    {
        private static bool Prefix(AvatarIkControllerType __instance)
        {
            if (__instance == null)
            {
                return false;
            }

            if (__instance.BodyIK != null)
            {
                __instance.BodyIK.enabled = false;
            }

            return false;
        }
    }

    /// <summary>
    /// Ensures avatar body IK starts disabled on dedicated headless servers.
    /// </summary>
    [HarmonyPatch(typeof(AvatarIkControllerType), "Awake")]
    internal static class AvatarIkControllerAwakePatches
    {
        private static void Postfix(AvatarIkControllerType __instance)
        {
            if (__instance == null)
            {
                return;
            }

            if (__instance.BodyIK != null)
            {
                __instance.BodyIK.enabled = false;
            }

            __instance.enabled = false;
        }
    }
}

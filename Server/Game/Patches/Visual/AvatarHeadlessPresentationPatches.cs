using HarmonyLib;
using DedicatedServerMod.Server.Game.Patches.Common;
using System.Collections.Generic;
#if IL2CPP
using AvatarAnimationType = Il2CppScheduleOne.AvatarFramework.Animation.AvatarAnimation;
using AvatarEquippableLookAtType = Il2CppScheduleOne.AvatarFramework.Equipping.AvatarEquippableLookAt;
using AvatarLookControllerType = Il2CppScheduleOne.AvatarFramework.Animation.AvatarLookController;
using EyeControllerType = Il2CppScheduleOne.AvatarFramework.EyeController;
#else
using AvatarAnimationType = ScheduleOne.AvatarFramework.Animation.AvatarAnimation;
using AvatarEquippableLookAtType = ScheduleOne.AvatarFramework.Equipping.AvatarEquippableLookAt;
using AvatarLookControllerType = ScheduleOne.AvatarFramework.Animation.AvatarLookController;
using EyeControllerType = ScheduleOne.AvatarFramework.EyeController;
#endif

namespace DedicatedServerMod.Server.Game.Patches.Visual
{
    internal static class AvatarStandUpClipCacheState
    {
        private static readonly HashSet<string> CachedClipKeys = new();

        internal static bool HasCachedClip(AvatarAnimationType animation, string clipName)
        {
            return animation != null &&
                   !string.IsNullOrEmpty(clipName) &&
                   CachedClipKeys.Contains(CreateKey(animation, clipName));
        }

        internal static void MarkCached(AvatarAnimationType animation, string clipName)
        {
            if (animation == null || string.IsNullOrEmpty(clipName))
            {
                return;
            }

            CachedClipKeys.Add(CreateKey(animation, clipName));
        }

        private static string CreateKey(AvatarAnimationType animation, string clipName)
        {
            return $"{animation.GetInstanceID()}::{clipName}";
        }
    }

    /// <summary>
    /// Disables repeated avatar culling and animator toggling work on dedicated headless servers.
    /// </summary>
    [HarmonyPatch(typeof(AvatarAnimationType), "Awake")]
    internal static class AvatarAnimationAwakePatches
    {
        private static void Postfix(AvatarAnimationType __instance)
        {
            if (__instance == null)
            {
                return;
            }

            __instance.CancelInvoke("UpdateAnimationActive");
        }
    }

    /// <summary>
    /// Skips avatar animation culling work on dedicated headless servers.
    /// </summary>
    [HarmonyPatch(typeof(AvatarAnimationType), "UpdateAnimationActive")]
    internal static class AvatarAnimationUpdateAnimationActivePatches
    {
        private static bool Prefix()
        {
            return !DedicatedServerPatchCommon.IsDedicatedHeadlessServer();
        }
    }

    /// <summary>
    /// Reuses the stand-up clip start poses that AvatarAnimation already cached during Start().
    /// Dedicated servers still capture the live ragdoll pose, but they skip re-sampling the
    /// static stand-up clips every time an NPC recovers.
    /// </summary>
    /// <remarks>
    /// This optimization is intentionally narrow because broader avatar animation suppression caused NPC stand-up and
    /// movement regressions. If NPC recovery, animation, or navigation starts behaving incorrectly, revisit this patch
    /// before disabling the rest of the avatar animation pipeline.
    /// </remarks>
    [HarmonyPatch(typeof(AvatarAnimationType), "PopulateAnimationStartBoneTransforms")]
    internal static class AvatarAnimationPopulateAnimationStartBoneTransformsPatches
    {
        private static bool Prefix(AvatarAnimationType __instance, string clipName)
        {
            if (!DedicatedServerPatchCommon.IsDedicatedHeadlessServer())
            {
                return true;
            }

            return !AvatarStandUpClipCacheState.HasCachedClip(__instance, clipName);
        }

        private static void Postfix(AvatarAnimationType __instance, string clipName)
        {
            if (!DedicatedServerPatchCommon.IsDedicatedHeadlessServer())
            {
                return;
            }

            AvatarStandUpClipCacheState.MarkCached(__instance, clipName);
        }
    }

    /// <summary>
    /// Prevents avatar eye blink scheduling from running on dedicated headless servers.
    /// </summary>
    [HarmonyPatch(typeof(EyeControllerType), "Update")]
    internal static class EyeControllerUpdatePatches
    {
        private static bool Prefix()
        {
            return !DedicatedServerPatchCommon.IsDedicatedHeadlessServer();
        }
    }

    /// <summary>
    /// Disables avatar look-target polling and IK state setup on dedicated headless servers.
    /// </summary>
    [HarmonyPatch(typeof(AvatarLookControllerType), "Awake")]
    internal static class AvatarLookControllerAwakePatches
    {
        private static void Postfix(AvatarLookControllerType __instance)
        {
            if (__instance == null)
            {
                return;
            }

            __instance.CancelInvoke("UpdateNearestPlayer");

            if (__instance.Aim != null)
            {
                __instance.Aim.enabled = false;
            }
        }
    }

    /// <summary>
    /// Skips nearest-player look-target scans on dedicated headless servers.
    /// </summary>
    [HarmonyPatch(typeof(AvatarLookControllerType), "UpdateNearestPlayer")]
    internal static class AvatarLookControllerUpdateNearestPlayerPatches
    {
        private static bool Prefix()
        {
            return !DedicatedServerPatchCommon.IsDedicatedHeadlessServer();
        }
    }

    /// <summary>
    /// Skips avatar eye and body look-at alignment on dedicated headless servers.
    /// </summary>
    [HarmonyPatch(typeof(AvatarLookControllerType), "LateUpdate")]
    internal static class AvatarLookControllerLateUpdatePatches
    {
        private static bool Prefix()
        {
            return !DedicatedServerPatchCommon.IsDedicatedHeadlessServer();
        }
    }

    /// <summary>
    /// Skips equippable look-at overrides on dedicated headless servers.
    /// </summary>
    [HarmonyPatch(typeof(AvatarEquippableLookAtType), "LateUpdate")]
    internal static class AvatarEquippableLookAtLateUpdatePatches
    {
        private static bool Prefix()
        {
            return !DedicatedServerPatchCommon.IsDedicatedHeadlessServer();
        }
    }

}

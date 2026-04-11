using System;
using System.Collections;
using HarmonyLib;
#if IL2CPP
using Il2CppInterop.Runtime;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Police;
using Il2CppScheduleOne.Vision;
#else
using ScheduleOne.PlayerScripts;
using ScheduleOne.Police;
using ScheduleOne.Vision;
#endif
using DedicatedServerMod.Utils;

namespace DedicatedServerMod.Client.Patches
{
    /// <summary>
    /// Reconciles local police-related presentation state that vanilla only updates on the owner-authoritative path.
    /// Dedicated servers can advance pursuit state for remote-owned players without replaying these client-side side effects.
    /// </summary>
    internal static class PolicePatches
    {
        public static void Initialize()
        {
            DebugLog.StartupDebug("Police patches initialized");
        }

        [HarmonyPatch(typeof(VisionCone), nameof(VisionCone.EventFullyNoticed))]
        [HarmonyPrefix]
        private static bool VisionConeEventFullyNoticedPrefix(VisionCone __instance, VisionEvent _event)
        {
            try
            {
                if (!ShouldSuppressPoliceVisibleNotice(__instance, _event))
                {
                    return true;
                }

                if (Traverse.Create(__instance).Field("activeVisionEvents").GetValue() is IList activeVisionEvents)
                {
                    activeVisionEvents.Remove(_event);
                }

                if (__instance.QuestionMarkPopup != null)
                {
                    __instance.QuestionMarkPopup.enabled = false;
                }

                if (__instance.ExclamationPointPopup != null)
                {
                    __instance.ExclamationPointPopup.enabled = false;
                }

                return false;
            }
            catch (Exception ex)
            {
                DebugLog.Warning($"PolicePatches: Failed to filter police visible notice: {ex.Message}");
                return true;
            }
        }

        [HarmonyPatch(typeof(PlayerCrimeData), "Update")]
        [HarmonyPostfix]
        private static void PlayerCrimeDataUpdatePostfix(PlayerCrimeData __instance)
        {
            try
            {
                if (!FishNet.InstanceFinder.IsClient
                    || __instance?.Player == null
                    || !__instance.Player.IsOwner
                    || __instance.Player.VisualState == null)
                {
                    return;
                }

                bool shouldHaveWantedState = __instance.CurrentPursuitLevel != PlayerCrimeData.EPursuitLevel.None;
                bool hasWantedState = __instance.Player.VisualState.GetState("Wanted") != null;
                if (shouldHaveWantedState == hasWantedState)
                {
                    return;
                }

                if (shouldHaveWantedState)
                {
                    __instance.Player.VisualState.ApplyState("Wanted", EVisualState.Wanted);
                }
                else
                {
                    __instance.Player.VisualState.RemoveState("Wanted");
                }

                DebugLog.Debug($"PolicePatches: Reconciled local wanted state to pursuit level {__instance.CurrentPursuitLevel}.");
            }
            catch (Exception ex)
            {
                DebugLog.Warning($"PolicePatches: Failed to reconcile wanted state: {ex.Message}");
            }
        }

        private static bool ShouldSuppressPoliceVisibleNotice(VisionCone visionCone, VisionEvent visionEvent)
        {
            Player localPlayer = Player.Local;
            Player targetPlayer = TryGetTargetPlayer(visionEvent);
            if (!FishNet.InstanceFinder.IsClient
                || visionCone == null
                || visionEvent?.Target == null
                || visionEvent.State == null
                || localPlayer == null
                || localPlayer.CrimeData == null
                || targetPlayer == null
                || targetPlayer != localPlayer
                || visionEvent.State.state != EVisualState.Visible
                || localPlayer.CrimeData.CurrentPursuitLevel != PlayerCrimeData.EPursuitLevel.None
                || localPlayer.CrimeData.BodySearchPending
                || localPlayer.IsArrested)
            {
                return false;
            }

            return UnityComponentAccess.GetComponentInParent<PoliceOfficer>(visionCone) != null;
        }

        private static Player TryGetTargetPlayer(VisionEvent visionEvent)
        {
#if IL2CPP
            object target = visionEvent?.Target;
            if (target is Il2CppSystem.Object targetObject)
            {
                return targetObject.TryCast<Player>();
            }

            return null;
#else
            return visionEvent?.Target as Player;
#endif
        }
    }
}

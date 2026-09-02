using HarmonyLib;
using MelonLoader;
using DedicatedServerMod.Utils;
#if IL2CPP
using Il2CppScheduleOne.Interaction;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.UI;
using Il2CppFishNet;
#else
using FishNet;
using ScheduleOne.Interaction;
using ScheduleOne.Persistence;
using ScheduleOne.UI;
#endif

namespace DedicatedServerMod.Client.Patches
{
    /// <summary>
    /// Suppresses native save-point interaction on dedicated-server clients.
    /// </summary>
    [HarmonyPatch]
    internal static class SavePointPatches
    {
        [HarmonyPatch(typeof(SavePoint), nameof(SavePoint.Hovered))]
        [HarmonyPrefix]
        private static bool HoveredPrefix()
        {
            return !DedicatedRuntimeContext.IsActive;
        }

        [HarmonyPatch(typeof(InteractableObject), nameof(InteractableObject.Hovered))]
        [HarmonyPrefix]
        private static bool InteractableHoveredPrefix(InteractableObject __instance)
        {
            return !DedicatedRuntimeContext.IsActive || !IsSavePointInteractable(__instance);
        }

        private static bool IsSavePointInteractable(InteractableObject interactableObject)
        {
            if (interactableObject == null)
            {
                return false;
            }

            return UnityComponentAccess.GetComponent<SavePoint>(interactableObject) != null
                || UnityComponentAccess.GetComponentInParent<SavePoint>(interactableObject) != null;
        }
    }
}

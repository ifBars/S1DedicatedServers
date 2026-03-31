using HarmonyLib;
using MelonLoader;
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
    /// Holds the loading screen open until dedicated-server authentication completes.
    /// </summary>
    [HarmonyPatch]
    internal static class SavePointPatches
    {
        [HarmonyPatch(typeof(SavePoint), nameof(SavePoint.Hovered))]
        [HarmonyPrefix]
        private static bool HoveredPrefix()
        {
            return false;
        }

        [HarmonyPatch(typeof(InteractableObject), nameof(InteractableObject.Hovered))]
        [HarmonyPrefix]
        private static bool InteractableHoveredPrefix(InteractableObject __instance)
        {
            return !IsSavePointInteractable(__instance);
        }

        private static bool IsSavePointInteractable(InteractableObject interactableObject)
        {
            if (interactableObject == null)
            {
                return false;
            }

            return interactableObject.GetComponent<SavePoint>() != null
                || interactableObject.GetComponentInParent<SavePoint>() != null;
        }
    }
}

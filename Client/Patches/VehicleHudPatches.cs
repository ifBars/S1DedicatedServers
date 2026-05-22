using System;
using HarmonyLib;
using DedicatedServerMod.Utils;
#if IL2CPP
using Il2CppFishNet;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.UI;
using LandVehicleType = Il2CppScheduleOne.Vehicles.LandVehicle;
#else
using FishNet;
using ScheduleOne.PlayerScripts;
using ScheduleOne.UI;
using LandVehicleType = ScheduleOne.Vehicles.LandVehicle;
#endif

namespace DedicatedServerMod.Client.Patches
{
    /// <summary>
    /// Reconciles vehicle HUD state when dedicated-client vehicle exit sync misses the vanilla exit event.
    /// </summary>
    internal static class VehicleHudPatches
    {
#if !IL2CPP
        private static readonly AccessTools.FieldRef<VehicleCanvas, LandVehicleType> CurrentVehicleRef =
            AccessTools.FieldRefAccess<VehicleCanvas, LandVehicleType>("currentVehicle");
#endif

        public static void Initialize()
        {
            DebugLog.StartupDebug("Vehicle HUD patches initialized");
        }

        [HarmonyPatch(typeof(VehicleCanvas), "Update")]
        [HarmonyPostfix]
        private static void VehicleCanvasUpdatePostfix(VehicleCanvas __instance)
        {
            try
            {
                if (!InstanceFinder.IsClient || InstanceFinder.IsServer || __instance?.Canvas == null)
                {
                    return;
                }

                Player localPlayer = Player.Local;
                LandVehicleType trackedVehicle = GetTrackedVehicle(__instance);
                bool localPlayerHasVehicle = localPlayer?.CurrentVehicle != null;
                bool trackedVehicleStillOccupied = trackedVehicle != null && trackedVehicle.LocalPlayerIsInVehicle;

                if (localPlayerHasVehicle && trackedVehicleStillOccupied)
                {
                    return;
                }

                if (!__instance.Canvas.enabled && trackedVehicle == null)
                {
                    return;
                }

                __instance.Canvas.enabled = false;
                __instance.DriverPromptsContainer?.SetActive(false);
                SetTrackedVehicle(__instance, null);
            }
            catch (Exception ex)
            {
                DebugLog.Warning($"VehicleHudPatches: Failed to reconcile vehicle HUD state: {ex.Message}");
            }
        }

        private static LandVehicleType GetTrackedVehicle(VehicleCanvas canvas)
        {
#if IL2CPP
            return canvas.currentVehicle;
#else
            return CurrentVehicleRef(canvas);
#endif
        }

        private static void SetTrackedVehicle(VehicleCanvas canvas, LandVehicleType vehicle)
        {
#if IL2CPP
            canvas.currentVehicle = vehicle;
#else
            CurrentVehicleRef(canvas) = vehicle;
#endif
        }
    }
}

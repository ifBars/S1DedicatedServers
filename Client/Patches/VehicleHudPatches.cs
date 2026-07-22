using System;
using System.Reflection;
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
#if IL2CPP
        private const BindingFlags InstanceMemberFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly PropertyInfo LegacyPromptsContainerProperty =
            typeof(VehicleCanvas).GetProperty("DriverPromptsContainer", InstanceMemberFlags);

        private static readonly PropertyInfo VehicleStateProperty =
            typeof(LandVehicleType).GetProperty("State", InstanceMemberFlags);

        private static readonly PropertyInfo VehiclePromptsProperty =
            typeof(VehicleCanvas).GetProperty("VehiclePrompts", InstanceMemberFlags);

        private static readonly PropertyInfo DriverPromptsProperty =
            typeof(VehicleCanvas).GetProperty("DriverPrompts", InstanceMemberFlags);
#else
        private static readonly AccessTools.FieldRef<VehicleCanvas, LandVehicleType> CurrentVehicleRef =
            AccessTools.FieldRefAccess<VehicleCanvas, LandVehicleType>("currentVehicle");
#endif

        internal static void Initialize()
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
#if IL2CPP
                HideIl2CppVehiclePrompts(__instance, trackedVehicle);
#else
                if (trackedVehicle?.State != null)
                {
                    trackedVehicle.State.UnloadModule(__instance.VehiclePrompts);
                    trackedVehicle.State.UnloadModule(__instance.DriverPrompts);
                }
#endif
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

#if IL2CPP
        private static void HideIl2CppVehiclePrompts(VehicleCanvas canvas, LandVehicleType vehicle)
        {
            object legacyContainer = LegacyPromptsContainerProperty?.GetValue(canvas);
            if (legacyContainer != null)
            {
                MethodInfo setActiveMethod = legacyContainer.GetType().GetMethod(
                    "SetActive",
                    InstanceMemberFlags,
                    null,
                    new[] { typeof(bool) },
                    null);
                setActiveMethod?.Invoke(legacyContainer, new object[] { false });
                return;
            }

            object state = vehicle != null ? VehicleStateProperty?.GetValue(vehicle) : null;
            if (state == null)
            {
                return;
            }

            UnloadStateModule(state, VehiclePromptsProperty?.GetValue(canvas));
            UnloadStateModule(state, DriverPromptsProperty?.GetValue(canvas));
        }

        private static void UnloadStateModule(object state, object module)
        {
            if (module == null)
            {
                return;
            }

            MethodInfo[] methods = state.GetType().GetMethods(InstanceMemberFlags);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                ParameterInfo[] parameters = method.GetParameters();
                if (method.Name == "UnloadModule"
                    && parameters.Length == 1
                    && parameters[0].ParameterType.IsInstanceOfType(module))
                {
                    method.Invoke(state, new[] { module });
                    return;
                }
            }
        }
#endif

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

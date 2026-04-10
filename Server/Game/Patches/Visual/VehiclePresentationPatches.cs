using HarmonyLib;
using DedicatedServerMod.Server.Game.Patches.Common;
#if IL2CPP
using VehicleAxleType = Il2CppScheduleOne.Vehicles.VehicleAxle;
#else
using VehicleAxleType = ScheduleOne.Vehicles.VehicleAxle;
#endif

namespace DedicatedServerMod.Server.Game.Patches.Visual
{
    /// <summary>
    /// Skips axle mesh alignment work on dedicated headless servers because it only updates vehicle presentation transforms.
    /// </summary>
    [HarmonyPatch(typeof(VehicleAxleType), "LateUpdate")]
    internal static class VehicleAxleLateUpdatePatches
    {
        private static bool Prefix()
        {
            return !DedicatedServerPatchCommon.IsDedicatedHeadlessServer();
        }
    }
}

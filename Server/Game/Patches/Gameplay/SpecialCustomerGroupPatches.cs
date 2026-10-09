using System;
using HarmonyLib;
#if IL2CPP
using SpecialCustomerManagerType = Il2CppScheduleOne.SpecialCustomers.SpecialCustomerManager;
using SpecialCustomerSaveDataType = Il2CppScheduleOne.SpecialCustomers.SpecialCustomerSaveData;
#else
using SpecialCustomerManagerType = ScheduleOne.SpecialCustomers.SpecialCustomerManager;
using SpecialCustomerSaveDataType = ScheduleOne.SpecialCustomers.SpecialCustomerSaveData;
#endif

namespace DedicatedServerMod.Server.Game.Patches.Gameplay
{
    internal static class SpecialCustomerGroupIdentity
    {
        internal static string Resolve(SpecialCustomerManagerType manager, string groupId)
        {
            // f7's first-visit selection returns "Hippies", while its asset and save IDs
            // are "hippies". Use the asset's spelling before native exact-match lookups.
            foreach (var group in manager._specialCustomerGroups)
            {
                if (group != null && string.Equals(group.GroupId, groupId, StringComparison.OrdinalIgnoreCase))
                {
                    return group.GroupId;
                }
            }

            return groupId;
        }
    }

    [HarmonyPatch(typeof(SpecialCustomerManagerType), "GetNextCustomerGroupOrdered")]
    internal static class SpecialCustomerSelectionPatches
    {
        private static void Postfix(SpecialCustomerManagerType __instance, ref string __result)
        {
            __result = SpecialCustomerGroupIdentity.Resolve(__instance, __result);
        }
    }

    [HarmonyPatch(typeof(SpecialCustomerManagerType), "SetData")]
    internal static class SpecialCustomerLoadPatches
    {
        private static void Prefix(SpecialCustomerManagerType __instance, SpecialCustomerSaveDataType data)
        {
            if (data != null && !string.IsNullOrEmpty(data.GroupId))
            {
                data.GroupId = SpecialCustomerGroupIdentity.Resolve(__instance, data.GroupId);
            }
        }
    }
}

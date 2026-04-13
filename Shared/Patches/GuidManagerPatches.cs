using System;
using DedicatedServerMod.Utils;
using HarmonyLib;
using UnityEngine;
#if IL2CPP
using ConsoleType = Il2CppScheduleOne.Console;
using GuidManagerType = Il2Cpp.GUIDManager;
using GuidRegisterableType = Il2CppScheduleOne.IGUIDRegisterable;
using GuidValueType = Il2CppSystem.Guid;
using StoredObjectType = Il2CppSystem.Object;
using Il2CppInterop.Runtime;
#else
using ConsoleType = ScheduleOne.Console;
using GuidManagerType = global::GUIDManager;
using GuidRegisterableType = ScheduleOne.IGUIDRegisterable;
using GuidValueType = System.Guid;
using StoredObjectType = System.Object;
#endif

namespace DedicatedServerMod.Shared.Patches;

/// <summary>
/// Replaces GUIDManager's list-backed membership checks with dictionary-backed lookups.
/// The native implementation keeps both a list and a dictionary but routes several
/// registration paths through the list, which turns GUID maintenance into O(n).
/// </summary>
[HarmonyPatch(typeof(GuidManagerType))]
internal static class GuidManagerPatches
{
    [HarmonyPrefix]
    [HarmonyPatch("RegisterObject")]
    private static bool RegisterObjectPrefix(GuidRegisterableType obj, GameObject go = null)
    {
        if (obj == null)
        {
            return false;
        }

        GuidValueType guid = obj.GUID;
        if (GuidManagerType.guidToObject.ContainsKey(guid))
        {
            ConsoleType.LogWarning("RegisterObject called and passed obj whose GUID is already registered. Replacing old entries with new", go);
            GuidManagerType.registeredGUIDs.Remove(guid);
            GuidManagerType.guidToObject.Remove(guid);
        }

        GuidManagerType.registeredGUIDs.Add(guid);
        GuidManagerType.guidToObject.Add(guid, ConvertRegisteredObject(obj));
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch("DeregisterObject")]
    private static bool DeregisterObjectPrefix(GuidRegisterableType obj)
    {
        if (obj == null)
        {
            return false;
        }

        GuidManagerType.registeredGUIDs.Remove(obj.GUID);
        GuidManagerType.guidToObject.Remove(obj.GUID);
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch("GenerateUniqueGUID")]
    private static bool GenerateUniqueGuidPrefix(ref GuidValueType __result)
    {
        GuidValueType guid;
        do
        {
            guid = NewGuid();
        }
        while (GuidManagerType.guidToObject.ContainsKey(guid));

        __result = guid;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch("IsGUIDAlreadyRegistered")]
    private static bool IsGuidAlreadyRegisteredPrefix(GuidValueType guid, ref bool __result)
    {
        __result = GuidManagerType.guidToObject.ContainsKey(guid);
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch("Clear")]
    private static bool ClearPrefix()
    {
        GuidManagerType.registeredGUIDs.Clear();
        GuidManagerType.guidToObject.Clear();
        ConsoleType.Log("GUIDManager cleared!");
        return false;
    }

    private static GuidValueType NewGuid()
    {
#if IL2CPP
        return GuidValueType.NewGuid();
#else
        return Guid.NewGuid();
#endif
    }

    private static StoredObjectType ConvertRegisteredObject(GuidRegisterableType obj)
    {
#if IL2CPP
        return obj.TryCast<StoredObjectType>();
#else
        return obj;
#endif
    }

}

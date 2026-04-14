using System.Collections.Generic;
using System.Reflection;
using DedicatedServerMod.Server.Game.Patches.Common;
using DedicatedServerMod.Utils;
using HarmonyLib;
using UnityEngine;
#if IL2CPP
using AppsCanvasType = Il2CppScheduleOne.UI.Phone.AppsCanvas;
using HomeScreenType = Il2CppScheduleOne.UI.Phone.HomeScreen;
using PhoneType = Il2CppScheduleOne.UI.Phone.Phone;
#else
using AppsCanvasType = ScheduleOne.UI.Phone.AppsCanvas;
using HomeScreenType = ScheduleOne.UI.Phone.HomeScreen;
using PhoneType = ScheduleOne.UI.Phone.Phone;
#endif

namespace DedicatedServerMod.Server.Game.Patches.UI
{
    /// <summary>
    /// Removes gameplay phone and app roots before their Awake logic can register local-player UI state.
    /// </summary>
    [HarmonyPatch]
    internal static class PhoneAppSuppressionPatches
    {
        private static readonly HashSet<string> LoggedStrippedTypes = new HashSet<string>();

        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            HashSet<MethodBase> methods = new HashSet<MethodBase>();

            AddAwakeMethod(typeof(PhoneType), methods);
            AddAwakeMethod(typeof(HomeScreenType), methods);
            AddAwakeMethod(typeof(AppsCanvasType), methods);

            Assembly assembly = typeof(PhoneType).Assembly;
            Type[] runtimeTypes;

            try
            {
                runtimeTypes = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                runtimeTypes = ex.Types;
            }

            if (runtimeTypes == null)
            {
                yield break;
            }

            for (int i = 0; i < runtimeTypes.Length; i++)
            {
                Type type = runtimeTypes[i];
                if (!IsConcretePhoneAppType(type))
                {
                    continue;
                }

                AddAwakeMethod(type, methods);
            }

            foreach (MethodBase method in methods)
            {
                yield return method;
            }
        }

        [HarmonyPrefix]
        private static bool Prefix(MonoBehaviour __instance)
        {
            if (!DedicatedServerPatchCommon.IsDedicatedHeadlessServer() || __instance == null)
            {
                return true;
            }

            Type instanceType = __instance.GetType();
            if (!ShouldSuppress(instanceType))
            {
                return true;
            }

            string typeName = instanceType.FullName ?? instanceType.Name;
            if (LoggedStrippedTypes.Add(typeName))
            {
                DebugLog.StartupDebug($"Stripping client-only phone/app root on dedicated server: {typeName}");
            }

            GameObject gameObject = __instance.gameObject;
            if (gameObject != null)
            {
                gameObject.SetActive(false);
                UnityEngine.Object.Destroy(gameObject);
            }
            else
            {
                UnityEngine.Object.Destroy(__instance);
            }

            return false;
        }

        private static void AddAwakeMethod(Type type, ISet<MethodBase> methods)
        {
            if (type == null || methods == null)
            {
                return;
            }

            MethodBase awakeMethod = AccessTools.Method(type, "Awake");
            if (awakeMethod != null)
            {
                methods.Add(awakeMethod);
            }
        }

        private static bool ShouldSuppress(Type type)
        {
            if (type == null)
            {
                return false;
            }

            return type == typeof(PhoneType)
                || type == typeof(HomeScreenType)
                || type == typeof(AppsCanvasType)
                || IsPhoneAppType(type);
        }

        private static bool IsConcretePhoneAppType(Type type)
        {
            return type != null && type.IsClass && !type.IsAbstract && IsPhoneAppType(type);
        }

        private static bool IsPhoneAppType(Type type)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                if (!current.IsGenericType)
                {
                    continue;
                }

                Type genericDefinition = current.GetGenericTypeDefinition();
                string fullName = genericDefinition.FullName;
                if (fullName == "ScheduleOne.UI.App`1" || fullName == "Il2CppScheduleOne.UI.App`1")
                {
                    return true;
                }
            }

            return false;
        }
    }
}

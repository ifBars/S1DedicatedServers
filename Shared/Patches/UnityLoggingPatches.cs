using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace DedicatedServerMod.Shared.Patches;

/// <summary>
/// Suppresses Unity warning logs before they fan out to other listeners such as UnityExplorer.
/// </summary>
[HarmonyPatch]
internal static class UnityLoggingPatches
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodBase applicationLogCallback = AccessTools.Method(
            typeof(Application),
            "CallLogCallback",
            new[] { typeof(string), typeof(string), typeof(LogType), typeof(bool) });
        Type debugLogHandlerType = AccessTools.TypeByName("UnityEngine.DebugLogHandler");
        MethodBase[] candidates =
        {
            applicationLogCallback,
            AccessTools.Method(typeof(Debug), nameof(Debug.LogWarning), new[] { typeof(object) }),
            AccessTools.Method(typeof(Debug), nameof(Debug.LogWarning), new[] { typeof(object), typeof(UnityEngine.Object) }),
            AccessTools.Method(typeof(Debug), nameof(Debug.LogWarningFormat), new[] { typeof(string), typeof(object[]) }),
            AccessTools.Method(typeof(Debug), nameof(Debug.LogWarningFormat), new[] { typeof(UnityEngine.Object), typeof(string), typeof(object[]) }),
            AccessTools.Method(typeof(Debug), nameof(Debug.LogFormat), new[] { typeof(LogType), typeof(LogOption), typeof(UnityEngine.Object), typeof(string), typeof(object[]) }),
            AccessTools.Method(typeof(Logger), nameof(Logger.Log), new[] { typeof(LogType), typeof(object) }),
            AccessTools.Method(typeof(Logger), nameof(Logger.Log), new[] { typeof(LogType), typeof(object), typeof(UnityEngine.Object) }),
            AccessTools.Method(typeof(Logger), nameof(Logger.Log), new[] { typeof(LogType), typeof(string), typeof(object) }),
            AccessTools.Method(typeof(Logger), nameof(Logger.Log), new[] { typeof(LogType), typeof(string), typeof(object), typeof(UnityEngine.Object) }),
            AccessTools.Method(typeof(Logger), nameof(Logger.LogFormat), new[] { typeof(LogType), typeof(UnityEngine.Object), typeof(string), typeof(object[]) }),
            AccessTools.Method(typeof(Logger), nameof(Logger.LogFormat), new[] { typeof(LogType), typeof(string), typeof(object[]) }),
            AccessTools.Method(typeof(Logger), nameof(Logger.LogWarning), new[] { typeof(string), typeof(object) }),
            AccessTools.Method(typeof(Logger), nameof(Logger.LogWarning), new[] { typeof(string), typeof(object), typeof(UnityEngine.Object) }),
            debugLogHandlerType != null
                ? AccessTools.Method(debugLogHandlerType, "LogFormat", new[] { typeof(LogType), typeof(UnityEngine.Object), typeof(string), typeof(object[]) })
                : null,
            debugLogHandlerType != null
                ? AccessTools.Method(debugLogHandlerType, "LogFormat", new[] { typeof(LogType), typeof(LogOption), typeof(UnityEngine.Object), typeof(string), typeof(object[]) })
                : null,
            debugLogHandlerType != null
                ? AccessTools.Method(debugLogHandlerType, "Internal_Log", new[] { typeof(LogType), typeof(LogOption), typeof(string), typeof(UnityEngine.Object) })
                : null
        };

        foreach (MethodBase candidate in candidates)
        {
            if (candidate != null)
            {
                yield return candidate;
            }
        }
    }

    [HarmonyPrefix]
    private static bool Prefix(MethodBase __originalMethod, object[] __args)
    {
        return !ShouldSuppress(__originalMethod, __args);
    }

    private static bool ShouldSuppress(MethodBase originalMethod, object[] args)
    {
        return TryGetLogType(originalMethod, args, out LogType logType) && logType == LogType.Warning;
    }

    private static bool TryGetLogType(MethodBase originalMethod, object[] args, out LogType logType)
    {
        if (originalMethod == null)
        {
            logType = LogType.Log;
            return false;
        }

        if (originalMethod.DeclaringType == typeof(Application) && originalMethod.Name == "CallLogCallback")
        {
            return TryGetLogTypeAt(args, 2, out logType);
        }

        if (originalMethod.DeclaringType == typeof(Debug))
        {
            if (originalMethod.Name == nameof(Debug.LogWarning) || originalMethod.Name == nameof(Debug.LogWarningFormat))
            {
                logType = LogType.Warning;
                return true;
            }

            if (originalMethod.Name == nameof(Debug.LogFormat))
            {
                return TryGetLogTypeAt(args, 0, out logType);
            }
        }

        if (originalMethod.DeclaringType == typeof(Logger))
        {
            if (originalMethod.Name == nameof(Logger.LogWarning))
            {
                logType = LogType.Warning;
                return true;
            }

            if (originalMethod.Name == nameof(Logger.Log) || originalMethod.Name == nameof(Logger.LogFormat))
            {
                return TryGetLogTypeAt(args, 0, out logType);
            }
        }

        if (originalMethod.DeclaringType?.FullName == "UnityEngine.DebugLogHandler")
        {
            return TryGetLogTypeAt(args, 0, out logType);
        }

        logType = LogType.Log;
        return false;
    }

    private static bool TryGetLogTypeAt(object[] args, int index, out LogType logType)
    {
        if (args != null && args.Length > index && args[index] is LogType typedLogType)
        {
            logType = typedLogType;
            return true;
        }

        logType = LogType.Log;
        return false;
    }
}
